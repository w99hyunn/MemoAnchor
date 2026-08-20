#!/usr/bin/env python3
"""Local upload server for MemoAnchor reconstruction scan packages."""

from __future__ import annotations

import argparse
import json
import mimetypes
import shutil
import subprocess
import sys
import threading
import time
import zipfile
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, unquote, urlparse

from visual_localizer import localize


ROOT = Path(__file__).resolve().parent
WORKSPACE_ROOT = ROOT.parent
RECONSTRUCTION_TOOL_ROOT = ROOT
RECONSTRUCTION_SCRIPT = RECONSTRUCTION_TOOL_ROOT / "reconstruct_open3d_tsdf.py"
RECONSTRUCTION_VENV_PYTHON = ROOT / ".venv" / "bin" / "python"
DATA_ROOT = ROOT / "data"
UPLOAD_ROOT = DATA_ROOT / "uploads"
SCAN_ROOT = DATA_ROOT / "scans"
RESULT_ROOT = DATA_ROOT / "results"
RUN_ROOT = DATA_ROOT / "runs"
DEFAULT_REVIEW_SCAN_ID = "20260626_102656"
PRUNING_PROFILES = ("geometry", "clean_texture", "safe", "balanced", "aggressive", "rtabmap")
RGBD_DEFAULT_DEPTH_TRANSFORM = "rotate_270"
RGBD_DEFAULT_COLOR_TRANSFORM = "rotate_90"
MAX_UPLOAD_BYTES = 5 * 1024 * 1024 * 1024
MAX_LOCALIZATION_IMAGE_BYTES = 12 * 1024 * 1024
MAX_ARCHIVE_MEMBERS = 50_000
MAX_ARCHIVE_MEMBER_BYTES = 1024 * 1024 * 1024
MAX_ARCHIVE_UNCOMPRESSED_BYTES = 20 * 1024 * 1024 * 1024
DELETED_SCAN_IDS: set[str] = set()
DELETED_SCAN_IDS_LOCK = threading.Lock()


def ensure_dirs() -> None:
    for path in (UPLOAD_ROOT, SCAN_ROOT, RESULT_ROOT, RUN_ROOT):
        path.mkdir(parents=True, exist_ok=True)


def safe_scan_id(value: str | None) -> str:
    if not value:
        return time.strftime("%Y%m%d_%H%M%S")

    cleaned = "".join(ch for ch in value if ch.isalnum() or ch in ("-", "_"))
    return cleaned or time.strftime("%Y%m%d_%H%M%S")


def is_scan_deleted(scan_id: str) -> bool:
    with DELETED_SCAN_IDS_LOCK:
        return scan_id in DELETED_SCAN_IDS


def delete_scan_data(scan_id: str) -> bool:
    removed = False
    for root in (UPLOAD_ROOT, SCAN_ROOT, RESULT_ROOT, RUN_ROOT):
        path = root / scan_id
        if path.exists():
            shutil.rmtree(path)
            removed = True
    return removed


def write_status(scan_id: str, **fields: object) -> None:
    if is_scan_deleted(scan_id):
        return

    path = RESULT_ROOT / scan_id / "status.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    current: dict[str, object] = {}
    if path.exists():
        try:
            current = json.loads(path.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            current = {}

    current.update(fields)
    current["scanId"] = scan_id
    current["updatedAt"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    path.write_text(json.dumps(current, indent=2), encoding="utf-8")

    if "state" in fields:
        message = fields.get("message", "")
        result_file = fields.get("resultFile", "")
        suffix = f" result={result_file}" if result_file else ""
        print(f"[scan {scan_id}] {fields['state']}: {message}{suffix}", flush=True)


def read_status(scan_id: str) -> dict[str, object]:
    path = RESULT_ROOT / scan_id / "status.json"
    if not path.exists():
        return {"scanId": scan_id, "state": "missing"}

    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return {"scanId": scan_id, "state": "corrupt_status"}


def write_run_status(result_dir: Path, scan_id: str, run_id: str, **fields: object) -> None:
    result_dir.mkdir(parents=True, exist_ok=True)
    path = result_dir / "status.json"
    current: dict[str, object] = {}
    if path.exists():
        try:
            current = json.loads(path.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            current = {}

    current.update(fields)
    current["scanId"] = scan_id
    current["runId"] = run_id
    current["updatedAt"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    path.write_text(json.dumps(current, indent=2), encoding="utf-8")

    if "state" in fields:
        message = fields.get("message", "")
        result_file = fields.get("resultFile", "")
        suffix = f" result={result_file}" if result_file else ""
        print(f"[lab {scan_id}/{run_id}] {fields['state']}: {message}{suffix}", flush=True)


def read_status_file(path: Path, scan_id: str, run_id: str) -> dict[str, object]:
    if not path.exists():
        return {"scanId": scan_id, "runId": run_id, "state": "missing"}

    try:
        status = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return {"scanId": scan_id, "runId": run_id, "state": "corrupt_status"}

    status.setdefault("scanId", scan_id)
    status.setdefault("runId", run_id)
    return status


def parse_worker_metrics(stdout: str) -> dict[str, object]:
    metrics: dict[str, object] = {}
    for raw_line in stdout.splitlines():
        line = raw_line.strip()
        if line.startswith("raw_colored_vertices="):
            left, _, coverage = line.partition(" coverage=")
            colored, _, total = left.removeprefix("raw_colored_vertices=").partition("/")
            metrics["rawColoredVertices"] = int(colored)
            metrics["rawTotalVertices"] = int(total)
            if coverage:
                metrics["rawColorCoverage"] = float(coverage)
        elif line.startswith("colored_vertices="):
            colored, _, total = line.removeprefix("colored_vertices=").partition("/")
            metrics["tsdfColoredVertices"] = int(colored)
            metrics["tsdfTotalVertices"] = int(total)
        elif line.startswith("simplified_triangles="):
            tri_part, _, vertex_part = line.partition(" vertices=")
            before, _, after = tri_part.removeprefix("simplified_triangles=").partition("->")
            metrics["trianglesBeforeSimplify"] = int(before)
            metrics["trianglesAfterSimplify"] = int(after)
            if vertex_part:
                metrics["verticesAfterSimplify"] = int(vertex_part)
        elif line.startswith("baked_keyframe_texture "):
            for token in line.split()[1:]:
                key, _, value = token.partition("=")
                if key == "triangles":
                    assigned, _, total = value.partition("/")
                    metrics["texturedTriangles"] = int(assigned)
                    metrics["totalTriangles"] = int(total)
                elif key == "texture":
                    metrics["textureSize"] = value
                elif key == "tile":
                    metrics["textureTile"] = value
        elif line.startswith("local_hole_fill=loops="):
            value = line.removeprefix("local_hole_fill=loops=").split()[0]
            metrics["localHoleFillLoops"] = int(value)
        elif line.startswith("mesh_prune_") and "triangles_removed=" in line:
            name = line.split("=", 1)[0]
            metric_name = "".join(part.capitalize() for part in name.removeprefix("mesh_prune_").split("_"))
            removed = line.split("triangles_removed=", 1)[1].split()[0]
            metrics[f"{metric_name}Removed"] = int(removed)
        elif line.startswith("mesh_prune_") and "triangles=" in line:
            name, _, rest = line.partition("triangles=")
            metric_name = "".join(part.capitalize() for part in name.rstrip("=").removeprefix("mesh_prune_").split("_"))
            before, _, after_part = rest.partition("->")
            after = after_part.split()[0]
            metrics[f"{metric_name}TrianglesBefore"] = int(before)
            metrics[f"{metric_name}TrianglesAfter"] = int(after)
        elif line.startswith("plane_guided_fill=planes="):
            value = line.removeprefix("plane_guided_fill=planes=").split()[0]
            accepted, _, total = value.partition("/")
            metrics["planeFillAccepted"] = int(accepted)
            metrics["planeFillTotal"] = int(total)
        elif line.startswith("chosen_result="):
            metrics["chosenResult"] = line.removeprefix("chosen_result=")
        elif line.startswith("pruning_profile="):
            metrics["pruningProfile"] = line.removeprefix("pruning_profile=")
        elif line.startswith("geometry_mesh="):
            for token in line.removeprefix("geometry_mesh=").split():
                key, _, value = token.partition("=")
                if key == "vertices":
                    before, _, after = value.partition("->")
                    metrics["geometryVerticesBefore"] = int(before)
                    metrics["geometryVerticesAfter"] = int(after)
                elif key == "triangles":
                    before, _, after = value.partition("->")
                    metrics["geometryTrianglesBefore"] = int(before)
                    metrics["geometryTrianglesAfter"] = int(after)
        elif line.startswith("rtabmap_dataset="):
            metrics["rtabmapDataset"] = line.removeprefix("rtabmap_dataset=")
        elif line.startswith("rtabmap_capture_dataset="):
            metrics["rtabmapCaptureDataset"] = line.removeprefix("rtabmap_capture_dataset=")
        elif line.startswith("rtabmap_frames="):
            for token in line.split():
                key, _, value = token.partition("=")
                if key == "rtabmap_frames":
                    metrics["rtabmapFrames"] = int(value)
                elif key == "skipped":
                    metrics["rtabmapSkippedFrames"] = int(value)
        elif line.startswith("rtabmap_status="):
            for token in line.split():
                key, _, value = token.partition("=")
                if key == "rtabmap_status":
                    metrics["rtabmapStatus"] = value
                elif key == "tools":
                    metrics["rtabmapMissingTools"] = value
                elif key == "stage":
                    metrics["rtabmapFailedStage"] = value
                elif key == "result":
                    metrics["rtabmapResult"] = value
                elif key == "db":
                    metrics["rtabmapDatabase"] = value
        elif line.startswith("rtabmap_pose_fusion="):
            for token in line.split():
                key, _, value = token.partition("=")
                if key == "rtabmap_pose_fusion":
                    metrics["rtabmapPoseFusion"] = value
                elif key == "source_frames":
                    metrics["rtabmapFrames"] = int(value)
                elif key == "integrated":
                    metrics["rtabmapIntegratedFrames"] = int(value)
                elif key == "skipped":
                    metrics["rtabmapSkippedFrames"] = int(value)
                elif key == "reason":
                    metrics["rtabmapPoseFusionReason"] = value
                elif key == "result":
                    metrics["rtabmapResult"] = value
        elif line.startswith("rtabmap_odometry="):
            for token in line.split():
                key, _, value = token.partition("=")
                if key == "frames":
                    metrics["rtabmapFrames"] = int(value)
                elif key == "keyframes":
                    metrics["rtabmapKeyframes"] = int(value)
                elif key == "ignored":
                    metrics["rtabmapIgnoredFrames"] = int(value)
        elif line.startswith("rtabmap_dataset_exit="):
            metrics["rtabmapDatasetExit"] = int(line.removeprefix("rtabmap_dataset_exit="))
        elif line.startswith("rtabmap_export_exit="):
            metrics["rtabmapExportExit"] = int(line.removeprefix("rtabmap_export_exit="))
    return metrics


def summarize_result_files(result_dir: Path) -> dict[str, object]:
    files = {}
    for name in (
        "result.obj",
        "result.mtl",
        "result_texture.png",
        "result_mesh.jpg",
        "result.ply",
        "result_geometry.ply",
        "result_point_cloud.ply",
        "preview.png",
        "preview_color_topdown.png",
        "reconstruction_report.json",
        "trajectory_report.json",
        "used_frames.jsonl",
        "rejected_frames.jsonl",
        "result_raw_colored.ply",
        "result_tsdf.ply",
    ):
        path = result_dir / name
        if path.exists():
            files[name] = {"bytes": path.stat().st_size}
    return files


def reconstruction_python() -> Path:
    return RECONSTRUCTION_VENV_PYTHON if RECONSTRUCTION_VENV_PYTHON.exists() else Path(sys.executable)


def is_rgbd_dataset_dir(path: Path) -> bool:
    return (
        (path / "session.json").is_file()
        and (path / "frames.jsonl").is_file()
        and (path / "rgb").is_dir()
        and (path / "depth").is_dir()
        and (path / "confidence").is_dir()
    )


def find_rgbd_dataset(scan_dir: Path) -> Path | None:
    if is_rgbd_dataset_dir(scan_dir):
        return scan_dir

    candidates = []
    for session_path in scan_dir.rglob("session.json"):
        candidate = session_path.parent
        if is_rgbd_dataset_dir(candidate):
            candidates.append(candidate)

    if not candidates:
        return None

    def frame_count(candidate: Path) -> int:
        try:
            with (candidate / "frames.jsonl").open("r", encoding="utf-8-sig") as source:
                return sum(1 for line in source if line.strip())
        except OSError:
            return 0

    return max(candidates, key=frame_count)


def load_reconstruction_report(path: Path) -> dict[str, object]:
    if not path.exists():
        return {}
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return {}


def run_rgbd_reconstruction(scan_id: str, rgbd_dir: Path, result_dir: Path) -> tuple[bool, str, dict[str, object]]:
    if not RECONSTRUCTION_SCRIPT.exists():
        return False, f"Missing reconstruction script: {RECONSTRUCTION_SCRIPT}", {}

    python = reconstruction_python()
    color_dir = result_dir / "open3d_color"
    geometry_dir = result_dir / "open3d_geometry"
    stdout_parts: list[str] = []
    stderr_parts: list[str] = []

    common = [
        str(python),
        str(RECONSTRUCTION_SCRIPT),
        str(rgbd_dir),
        "--voxel-size",
        "0.02",
        "--sdf-trunc",
        "0.06",
        "--depth-min",
        "0.15",
        "--depth-max",
        "5.0",
        "--frame-step",
        "1",
        "--depth-transform",
        RGBD_DEFAULT_DEPTH_TRANSFORM,
        "--preview-point-stride",
        "8",
    ]

    jobs = [
        ("color", common + ["--output-dir", str(color_dir), "--color-transform", RGBD_DEFAULT_COLOR_TRANSFORM, "--assume-registered-color"]),
        ("geometry", common + ["--output-dir", str(geometry_dir)]),
    ]

    started = time.time()
    reports: dict[str, object] = {}
    for label, command in jobs:
        write_status(
            scan_id,
            state="processing",
            message=f"Running Open3D RGB-D {label} reconstruction",
            resultFile="",
            viewerUrl=f"/viewer?scan={scan_id}",
        )
        process = subprocess.run(
            command,
            cwd=str(WORKSPACE_ROOT),
            text=True,
            capture_output=True,
            timeout=60 * 20,
            check=False,
        )
        stdout_parts.append(f"===== {label} stdout =====\n{process.stdout}")
        stderr_parts.append(f"===== {label} stderr =====\n{process.stderr}")
        if process.returncode != 0:
            (result_dir / "worker_stdout.txt").write_text("\n".join(stdout_parts), encoding="utf-8")
            (result_dir / "worker_stderr.txt").write_text("\n".join(stderr_parts), encoding="utf-8")
            return False, f"Open3D {label} worker exited with {process.returncode}", reports
        reports[label] = load_reconstruction_report((color_dir if label == "color" else geometry_dir) / "reconstruction_report.json")

    (result_dir / "worker_stdout.txt").write_text("\n".join(stdout_parts), encoding="utf-8")
    (result_dir / "worker_stderr.txt").write_text("\n".join(stderr_parts), encoding="utf-8")

    preferred_mesh = color_dir / "fused_mesh_clean_unity.ply"
    geometry_mesh = geometry_dir / "fused_mesh_clean_unity.ply"
    point_cloud = color_dir / "fused_point_cloud_unity.ply"
    localization_mesh = color_dir / "fused_mesh_clean.ply"
    if not preferred_mesh.exists():
        preferred_mesh = geometry_mesh
        localization_mesh = geometry_dir / "fused_mesh_clean.ply"

    if not preferred_mesh.exists():
        return False, "Open3D completed but no clean mesh was produced", reports

    shutil.copy2(preferred_mesh, result_dir / "result.ply")
    shutil.copy2(localization_mesh, result_dir / "result_open3d.ply")
    if geometry_mesh.exists():
        shutil.copy2(geometry_mesh, result_dir / "result_geometry.ply")
    if point_cloud.exists():
        shutil.copy2(point_cloud, result_dir / "result_point_cloud.ply")
    for source_name, destination_name in (
        ("preview.png", "preview.png"),
        ("preview_color_topdown.png", "preview_color_topdown.png"),
        ("reconstruction_report.json", "reconstruction_report.json"),
        ("trajectory_report.json", "trajectory_report.json"),
        ("used_frames.jsonl", "used_frames.jsonl"),
        ("rejected_frames.jsonl", "rejected_frames.jsonl"),
    ):
        source = color_dir / source_name
        if source.exists():
            shutil.copy2(source, result_dir / destination_name)

    color_report = reports.get("color") if isinstance(reports.get("color"), dict) else {}
    metrics = {
        "pipeline": "open3d_rgbd_tsdf",
        "coordinateSpace": "unity_scan_world_v1",
        "rgbdDataset": str(rgbd_dir),
        "depthTransform": RGBD_DEFAULT_DEPTH_TRANSFORM,
        "colorTransform": RGBD_DEFAULT_COLOR_TRANSFORM,
        "usedFrames": color_report.get("used_frame_count"),
        "rejectedFrames": color_report.get("rejected_frame_count"),
        "meshVertices": (color_report.get("clean_mesh") or {}).get("vertex_count") if isinstance(color_report.get("clean_mesh"), dict) else None,
        "meshTriangles": (color_report.get("clean_mesh") or {}).get("triangle_count") if isinstance(color_report.get("clean_mesh"), dict) else None,
        "processingTimeSeconds": round(time.time() - started, 2),
    }
    (result_dir / "server_reconstruction_summary.json").write_text(json.dumps({
        "scanId": scan_id,
        "rgbdDataset": str(rgbd_dir),
        "resultFile": "result.ply",
        "coordinateSpace": "unity_scan_world_v1",
        "localizationMesh": "result_open3d.ply",
        "viewerUrl": f"/viewer?scan={scan_id}",
        "metrics": metrics,
        "reports": reports,
    }, indent=2), encoding="utf-8")
    return True, "Open3D RGB-D reconstruction complete", metrics


def read_scan_quality(scan_id: str) -> object:
    manifest_path = SCAN_ROOT / scan_id / "manifest.json"
    if not manifest_path.exists():
        nested = list((SCAN_ROOT / scan_id).glob("*/manifest.json"))
        manifest_path = nested[0] if nested else manifest_path

    if not manifest_path.exists():
        return None

    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return None

    return manifest.get("quality")


def lab_reconstruction_worker(scan_id: str, run_id: str, scan_dir: Path, result_dir: Path, profile: str = "balanced") -> None:
    profile_label = "best map" if profile == "rtabmap" else profile
    write_run_status(
        result_dir,
        scan_id,
        run_id,
        state="processing",
        message=f"Starting {profile_label} reconstruction",
        resultFile="",
        profile=profile,
    )
    script = ROOT / "reconstruct_open3d.py"
    started = time.time()

    try:
        process = subprocess.run(
            [sys.executable, str(script), "--scan", str(scan_dir), "--out", str(result_dir), "--profile", profile],
            cwd=str(ROOT),
            text=True,
            capture_output=True,
            timeout=60 * 20,
            check=False,
        )
        duration = round(time.time() - started, 2)
        (result_dir / "worker_stdout.txt").write_text(process.stdout, encoding="utf-8")
        (result_dir / "worker_stderr.txt").write_text(process.stderr, encoding="utf-8")
        metrics = parse_worker_metrics(process.stdout)

        if process.returncode != 0:
            failure_message = f"Worker exited with {process.returncode}"
            if metrics.get("rtabmapStatus"):
                failure_message = f"RTAB-Map {metrics['rtabmapStatus']}"
                if metrics.get("rtabmapMissingTools"):
                    failure_message += f": missing {metrics['rtabmapMissingTools']}"
                elif metrics.get("rtabmapFailedStage"):
                    failure_message += f" at {metrics['rtabmapFailedStage']}"
            write_run_status(
                result_dir,
                scan_id,
                run_id,
                state="failed",
                message=failure_message,
                resultFile="",
                profile=profile,
                durationSeconds=duration,
                metrics=metrics,
                files=summarize_result_files(result_dir),
            )
            return

        result = result_dir / "result.obj"
        if not result.exists():
            result = result_dir / "result.ply"

        completion_message = "Reconstruction complete" if result.exists() else "No result mesh produced"
        if not result.exists() and metrics.get("rtabmapStatus"):
            completion_message = f"RTAB-Map {metrics['rtabmapStatus']}"
            if metrics.get("rtabmapMissingTools"):
                completion_message += f": missing {metrics['rtabmapMissingTools']}"
            elif metrics.get("rtabmapFailedStage"):
                completion_message += f" at {metrics['rtabmapFailedStage']}"

        write_run_status(
            result_dir,
            scan_id,
            run_id,
            state="done" if result.exists() else "failed",
            message=completion_message,
            resultFile=result.name if result.exists() else "",
            profile=profile,
            durationSeconds=duration,
            metrics=metrics,
            files=summarize_result_files(result_dir),
        )
    except Exception as exc:  # noqa: BLE001
        write_run_status(result_dir, scan_id, run_id, state="failed", message=str(exc), resultFile="")


def run_lab_profile_jobs(jobs: list[tuple[str, str, Path, Path, str]]) -> None:
    for scan_id, run_id, scan_dir, result_dir, profile in jobs:
        lab_reconstruction_worker(scan_id, run_id, scan_dir, result_dir, profile)


def list_lab_runs(scan_id: str) -> list[dict[str, object]]:
    runs: list[dict[str, object]] = []
    scan_quality = read_scan_quality(scan_id)
    current_dir = RESULT_ROOT / scan_id
    if current_dir.exists():
        status = read_status(scan_id)
        stdout_path = current_dir / "worker_stdout.txt"
        if stdout_path.exists() and "metrics" not in status:
            status["metrics"] = parse_worker_metrics(stdout_path.read_text(encoding="utf-8", errors="replace"))
        status["runId"] = "current"
        status["scope"] = "current"
        status["files"] = summarize_result_files(current_dir)
        if scan_quality is not None:
            status["scanQuality"] = scan_quality
        runs.append(status)

    run_parent = RUN_ROOT / scan_id
    if run_parent.exists():
        for run_dir in sorted((path for path in run_parent.iterdir() if path.is_dir()), reverse=True):
            status_path = run_dir / "status.json"
            if not status_path.exists():
                continue
            status = read_status_file(status_path, scan_id, run_dir.name)
            status["scope"] = "run"
            status["files"] = summarize_result_files(run_dir)
            if scan_quality is not None:
                status["scanQuality"] = scan_quality
            runs.append(status)

    return runs


def lab_html() -> str:
    return f"""<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>MemoAnchor Reconstruction Lab</title>
  <style>
    :root {{
      color-scheme: dark;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
      background: #17191c;
      color: #edf0f2;
    }}
    * {{ box-sizing: border-box; }}
    body {{ margin: 0; min-height: 100vh; background: #17191c; }}
    button, input, select {{ font: inherit; }}
    .shell {{ display: grid; grid-template-columns: 360px 1fr; min-height: 100vh; }}
    .panel {{ border-right: 1px solid #30343a; padding: 18px; overflow: auto; background: #202327; }}
    .viewer {{ position: relative; min-width: 0; background: #101214; }}
    h1 {{ font-size: 20px; line-height: 1.2; margin: 0 0 14px; font-weight: 700; }}
    label {{ display: block; font-size: 12px; color: #aab2bd; margin-bottom: 6px; }}
    .row {{ display: flex; gap: 8px; align-items: center; margin-bottom: 14px; }}
    input {{
      width: 100%;
      min-width: 0;
      border: 1px solid #3b4149;
      background: #15171a;
      color: #edf0f2;
      border-radius: 6px;
      padding: 9px 10px;
    }}
    button {{
      border: 1px solid #58616c;
      background: #2d333a;
      color: #f5f7f8;
      border-radius: 6px;
      padding: 9px 11px;
      cursor: pointer;
      white-space: nowrap;
    }}
    button.primary {{ background: #1f6f58; border-color: #2b8a6f; }}
    button:disabled {{ opacity: .5; cursor: default; }}
    .runs {{ display: grid; gap: 8px; }}
    .run {{
      width: 100%;
      border: 1px solid #373d45;
      background: #191c20;
      border-radius: 8px;
      padding: 10px;
      text-align: left;
    }}
    .run.active {{ border-color: #59a88c; outline: 1px solid #59a88c; }}
    .run-title {{ display: flex; justify-content: space-between; gap: 10px; font-weight: 650; font-size: 13px; }}
    .state {{ color: #91dcc5; }}
    .state.failed {{ color: #ff9d91; }}
    .meta {{ color: #aab2bd; font-size: 12px; margin-top: 5px; line-height: 1.45; }}
    .metrics {{ display: grid; grid-template-columns: 1fr 1fr; gap: 8px; margin-top: 12px; }}
    .metric {{ background: #15171a; border: 1px solid #30343a; border-radius: 6px; padding: 8px; min-height: 56px; }}
    .metric b {{ display: block; font-size: 16px; line-height: 1.2; }}
    .metric span {{ color: #9fa8b3; font-size: 11px; }}
    #canvas {{ width: 100%; height: 100vh; display: block; }}
    .hud {{
      position: absolute;
      left: 14px;
      bottom: 14px;
      right: 14px;
      display: flex;
      justify-content: space-between;
      gap: 10px;
      pointer-events: none;
    }}
    .hud > div {{
      background: rgba(18, 20, 23, .82);
      border: 1px solid rgba(255,255,255,.12);
      border-radius: 8px;
      padding: 8px 10px;
      font-size: 12px;
      color: #dbe0e5;
      max-width: 48%;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }}
    .empty {{ color: #aab2bd; font-size: 13px; line-height: 1.45; padding: 12px 0; }}
    @media (max-width: 860px) {{
      .shell {{ grid-template-columns: 1fr; grid-template-rows: auto 62vh; }}
      .panel {{ border-right: 0; border-bottom: 1px solid #30343a; max-height: 38vh; }}
      #canvas {{ height: 62vh; }}
    }}
  </style>
  <script type="importmap">
    {{
      "imports": {{
        "three": "https://unpkg.com/three@0.160.1/build/three.module.js",
        "three/addons/": "https://unpkg.com/three@0.160.1/examples/jsm/"
      }}
    }}
  </script>
</head>
<body>
  <main class="shell">
    <aside class="panel">
      <h1>Reconstruction Lab</h1>
      <label for="scanId">Scan ID</label>
      <div class="row">
        <input id="scanId" value="{DEFAULT_REVIEW_SCAN_ID}" autocomplete="off">
        <button id="load">Load</button>
      </div>
      <div class="row">
        <button id="rerun" class="primary">Run profiles</button>
        <button id="runRtabmap">Run Best Map</button>
        <button id="refresh">Refresh</button>
      </div>
      <div id="summary" class="empty"></div>
      <div id="runs" class="runs"></div>
    </aside>
    <section class="viewer">
      <canvas id="canvas"></canvas>
      <div class="hud">
        <div id="selected">No run selected</div>
        <div id="status">Idle</div>
      </div>
    </section>
  </main>

  <script type="module">
    import * as THREE from "three";
    import {{ OrbitControls }} from "three/addons/controls/OrbitControls.js";
    import {{ OBJLoader }} from "three/addons/loaders/OBJLoader.js";
    import {{ MTLLoader }} from "three/addons/loaders/MTLLoader.js";
    import {{ PLYLoader }} from "three/addons/loaders/PLYLoader.js";

    const scanInput = document.querySelector("#scanId");
    const initialScanId = new URLSearchParams(window.location.search).get("scan");
    if (initialScanId) scanInput.value = initialScanId;
    const runsEl = document.querySelector("#runs");
    const summaryEl = document.querySelector("#summary");
    const selectedEl = document.querySelector("#selected");
    const statusEl = document.querySelector("#status");
    const rerunBtn = document.querySelector("#rerun");
    const runRtabmapBtn = document.querySelector("#runRtabmap");
    const canvas = document.querySelector("#canvas");

    const renderer = new THREE.WebGLRenderer({{ canvas, antialias: true }});
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.setClearColor(0x101214);

    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(55, 1, 0.01, 1000);
    camera.position.set(0, 1.4, 4.5);
    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;

    scene.add(new THREE.HemisphereLight(0xffffff, 0x444444, 2.4));
    const dir = new THREE.DirectionalLight(0xffffff, 1.2);
    dir.position.set(2.5, 4, 3);
    scene.add(dir);
    const grid = new THREE.GridHelper(8, 16, 0x33403e, 0x24292e);
    grid.position.y = -1.5;
    scene.add(grid);

    let model = null;
    let selectedKey = "";
    let pollTimer = 0;

    function scanId() {{
      return scanInput.value.trim() || "{DEFAULT_REVIEW_SCAN_ID}";
    }}

    function formatBytes(bytes) {{
      if (!bytes) return "-";
      const units = ["B", "KB", "MB", "GB"];
      let value = bytes;
      let unit = 0;
      while (value >= 1024 && unit < units.length - 1) {{
        value /= 1024;
        unit += 1;
      }}
      return `${{value.toFixed(value >= 10 ? 0 : 1)}} ${{units[unit]}}`;
    }}

    function metricValue(metrics, key, suffix = "") {{
      if (!metrics || metrics[key] === undefined) return "-";
      const value = metrics[key];
      return typeof value === "number" ? `${{value.toLocaleString()}}${{suffix}}` : `${{value}}${{suffix}}`;
    }}

    function percentValue(value) {{
      if (value === undefined || value === null) return "-";
      return `${{Math.round(Number(value) * 100)}}%`;
    }}

    function firstMetric(metrics, keys, suffix = "") {{
      for (const key of keys) {{
        if (metrics && metrics[key] !== undefined) return metricValue(metrics, key, suffix);
      }}
      return "-";
    }}

    function runAssetBase(run) {{
      if (run.scope === "current") return `/files/current/${{encodeURIComponent(run.scanId)}}/`;
      return `/files/run/${{encodeURIComponent(run.scanId)}}/${{encodeURIComponent(run.runId)}}/`;
    }}

    function clearModel() {{
      if (!model) return;
      scene.remove(model);
      model.traverse((child) => {{
        if (child.geometry) child.geometry.dispose();
        if (child.material) {{
          const materials = Array.isArray(child.material) ? child.material : [child.material];
          materials.forEach((material) => {{
            if (material.map) material.map.dispose();
            material.dispose();
          }});
        }}
      }});
      model = null;
    }}

    function frameObject(object) {{
      const box = new THREE.Box3().setFromObject(object);
      if (box.isEmpty()) return;
      const center = box.getCenter(new THREE.Vector3());
      const size = box.getSize(new THREE.Vector3());
      object.position.sub(center);
      const maxDim = Math.max(size.x, size.y, size.z, 1e-6);
      const distance = maxDim * 1.25 / Math.tan(THREE.MathUtils.degToRad(camera.fov * 0.5));
      camera.position.set(0, maxDim * 0.35, distance);
      camera.near = Math.max(0.01, distance / 1000);
      camera.far = distance * 20;
      camera.updateProjectionMatrix();
      controls.target.set(0, 0, 0);
      controls.update();
    }}

    async function loadModel(run) {{
      clearModel();
      if (!run.resultFile) {{
        selectedEl.textContent = `${{run.runId}} has no result`;
        return;
      }}

      const base = runAssetBase(run);
      const resultFile = run.resultFile;
      statusEl.textContent = `Loading ${{resultFile}}`;
      selectedEl.textContent = `${{run.runId}} / ${{resultFile}}`;

      if (resultFile.endsWith(".obj")) {{
        const mtlLoader = new MTLLoader();
        mtlLoader.setPath(base);
        mtlLoader.setResourcePath(base);
        mtlLoader.load("result.mtl", (materials) => {{
          materials.preload();
          const loader = new OBJLoader();
          loader.setMaterials(materials);
          loader.setPath(base);
          loader.load(resultFile, (object) => {{
            model = object;
            scene.add(model);
            frameObject(model);
            statusEl.textContent = "Loaded";
          }}, undefined, (error) => {{
            console.error(error);
            statusEl.textContent = "OBJ load failed";
          }});
        }}, undefined, (error) => {{
          console.error(error);
          statusEl.textContent = "MTL load failed";
        }});
        return;
      }}

      if (resultFile.endsWith(".ply")) {{
        const loader = new PLYLoader();
        loader.load(base + resultFile, (geometry) => {{
          geometry.computeVertexNormals();
          const hasColor = geometry.hasAttribute("color");
          const material = new THREE.MeshStandardMaterial({{
            color: hasColor ? 0xffffff : 0x94b3bd,
            vertexColors: hasColor,
            roughness: 0.82,
            metalness: 0.0
          }});
          model = new THREE.Mesh(geometry, material);
          scene.add(model);
          frameObject(model);
          statusEl.textContent = "Loaded";
        }}, undefined, (error) => {{
          console.error(error);
          statusEl.textContent = "PLY load failed";
        }});
        return;
      }}

      statusEl.textContent = "Unsupported result";
    }}

    function renderRuns(runs) {{
      runs = [...runs].sort((a, b) => {{
        const aIsRtabmap = a.profile === "rtabmap" || Boolean(a.metrics && a.metrics.rtabmapStatus);
        const bIsRtabmap = b.profile === "rtabmap" || Boolean(b.metrics && b.metrics.rtabmapStatus);
        if (aIsRtabmap && !bIsRtabmap) return -1;
        if (bIsRtabmap && !aIsRtabmap) return 1;
        if (a.scope === "current" && b.scope !== "current") return -1;
        if (b.scope === "current" && a.scope !== "current") return 1;
        return String(b.updatedAt || "").localeCompare(String(a.updatedAt || ""));
      }});
      runsEl.replaceChildren();
      if (!runs.length) {{
        summaryEl.textContent = "No result yet for this scan.";
        return;
      }}

      const done = runs.filter((run) => run.state === "done").length;
      summaryEl.textContent = `${{runs.length}} run(s), ${{done}} done`;

      for (const run of runs) {{
        const button = document.createElement("button");
        const key = `${{run.scope}}:${{run.runId}}`;
        button.className = `run${{key === selectedKey ? " active" : ""}}`;
        const metrics = run.metrics || {{}};
        const files = run.files || {{}};
        const scanQuality = run.scanQuality || {{}};
        const resultSize = run.resultFile && files[run.resultFile] ? formatBytes(files[run.resultFile].bytes) : "-";
        const rtabmapLine = metrics.rtabmapStatus
          ? `<br>rtabmap: ${{metrics.rtabmapStatus}}${{metrics.rtabmapCaptureDataset ? " · capture dataset" : ""}}${{metrics.rtabmapIntegratedFrames !== undefined ? " · integrated " + metrics.rtabmapIntegratedFrames : ""}}${{metrics.rtabmapKeyframes !== undefined ? " · keyframes " + metrics.rtabmapKeyframes : ""}}${{metrics.rtabmapIgnoredFrames !== undefined ? " · ignored " + metrics.rtabmapIgnoredFrames : ""}}${{metrics.rtabmapMissingTools ? " · missing " + metrics.rtabmapMissingTools : ""}}${{metrics.rtabmapFailedStage ? " · " + metrics.rtabmapFailedStage : ""}}`
          : "";
        const weakCoverage = scanQuality.coverageCellCount
          ? `${{scanQuality.coverageWeakCellCount || 0}} / ${{scanQuality.coverageCellCount}} (${{percentValue(scanQuality.coverageWeakCellRatio)}})`
          : "-";
        button.innerHTML = `
          <div class="run-title">
            <span>${{run.profile || metrics.pruningProfile || "baseline"}} · ${{run.runId}}</span>
            <span class="state ${{run.state === "failed" ? "failed" : ""}}">${{run.state || "unknown"}}</span>
          </div>
          <div class="meta">
            result: ${{run.resultFile || "-"}} · size: ${{resultSize}}<br>
            profile: ${{run.profile || metrics.pruningProfile || "-"}}<br>
            scan quality: ${{scanQuality.score !== undefined ? Math.round(scanQuality.score) + "% " + (scanQuality.grade || "") : "-"}}<br>
            weak coverage: ${{weakCoverage}}<br>
            updated: ${{run.updatedAt || "-"}}${{rtabmapLine}}
          </div>
          <div class="metrics">
            <div class="metric"><b>${{metricValue(metrics, "rawColorCoverage")}}</b><span>raw coverage</span></div>
            <div class="metric"><b>${{metricValue(metrics, "texturedTriangles")}}</b><span>textured tris</span></div>
            <div class="metric"><b>${{firstMetric(metrics, ["totalTriangles", "rawFinalComponentsTrianglesAfter", "tsdfFinalComponentsTrianglesAfter", "trianglesAfterSimplify"])}}</b><span>final tris</span></div>
            <div class="metric"><b>${{weakCoverage}}</b><span>weak scan cells</span></div>
            <div class="metric"><b>${{metricValue(run, "durationSeconds", "s")}}</b><span>duration</span></div>
            <div class="metric"><b>${{firstMetric(metrics, ["rtabmapIntegratedFrames", "rtabmapKeyframes", "rtabmapFrames"])}}</b><span>map frames</span></div>
          </div>
        `;
        button.addEventListener("click", () => {{
          selectedKey = key;
          document.querySelectorAll(".run").forEach((item) => item.classList.remove("active"));
          button.classList.add("active");
          loadModel(run);
        }});
        runsEl.append(button);
      }}
    }}

    async function loadRuns() {{
      const response = await fetch(`/api/runs/${{encodeURIComponent(scanId())}}`);
      const payload = await response.json();
      renderRuns(payload.runs || []);
      const latestDone = (payload.runs || []).find((run) => run.state === "done");
      if (!selectedKey && latestDone) {{
        selectedKey = `${{latestDone.scope}}:${{latestDone.runId}}`;
        loadModel(latestDone);
      }}
      const processing = (payload.runs || []).some((run) => run.state === "queued" || run.state === "processing");
      if (processing && !pollTimer) pollTimer = window.setInterval(loadRuns, 3000);
      if (!processing && pollTimer) {{
        window.clearInterval(pollTimer);
        pollTimer = 0;
      }}
    }}

    async function rerun() {{
      rerunBtn.disabled = true;
      statusEl.textContent = "Queued reconstruction profiles";
      try {{
        await fetch(`/api/runs/${{encodeURIComponent(scanId())}}`, {{ method: "POST" }});
        await loadRuns();
      }} finally {{
        rerunBtn.disabled = false;
      }}
    }}

    async function rerunRtabmap() {{
      runRtabmapBtn.disabled = true;
      statusEl.textContent = "Queued best map";
      try {{
        await fetch(`/api/runs/${{encodeURIComponent(scanId())}}?profile=rtabmap`, {{ method: "POST" }});
        await loadRuns();
      }} finally {{
        runRtabmapBtn.disabled = false;
      }}
    }}

    function resize() {{
      const rect = canvas.getBoundingClientRect();
      renderer.setSize(rect.width, rect.height, false);
      camera.aspect = rect.width / Math.max(1, rect.height);
      camera.updateProjectionMatrix();
    }}

    function animate() {{
      resize();
      controls.update();
      renderer.render(scene, camera);
      requestAnimationFrame(animate);
    }}

    document.querySelector("#load").addEventListener("click", () => {{
      selectedKey = "";
      loadRuns();
    }});
    document.querySelector("#refresh").addEventListener("click", loadRuns);
    rerunBtn.addEventListener("click", rerun);
    runRtabmapBtn.addEventListener("click", rerunRtabmap);
    window.addEventListener("resize", resize);

    animate();
    loadRuns();
  </script>
</body>
</html>"""


def extract_zip(zip_path: Path, scan_id: str) -> Path:
    target = SCAN_ROOT / scan_id
    if target.exists():
        shutil.rmtree(target)
    target.mkdir(parents=True, exist_ok=True)

    with zipfile.ZipFile(zip_path, "r") as archive:
        members = archive.infolist()
        if len(members) > MAX_ARCHIVE_MEMBERS:
            raise ValueError(f"ZIP has too many members: {len(members)}")

        total_uncompressed_bytes = sum(member.file_size for member in members)
        if total_uncompressed_bytes > MAX_ARCHIVE_UNCOMPRESSED_BYTES:
            raise ValueError(f"ZIP expands beyond limit: {total_uncompressed_bytes} bytes")

        for member in members:
            if member.file_size > MAX_ARCHIVE_MEMBER_BYTES:
                raise ValueError(f"ZIP member is too large: {member.filename}")
            destination = (target / member.filename).resolve()
            if not str(destination).startswith(str(target.resolve())):
                raise ValueError(f"Unsafe ZIP member path: {member.filename}")
            archive.extract(member, target)

    if not (target / "manifest.json").exists():
        nested_manifests = list(target.glob("*/manifest.json"))
        if nested_manifests:
            return nested_manifests[0].parent

    return target


def reconstruction_worker(scan_id: str, scan_dir: Path) -> None:
    result_dir = RESULT_ROOT / scan_id
    result_dir.mkdir(parents=True, exist_ok=True)
    write_status(scan_id, state="processing", message="Starting reconstruction", resultFile="")

    script = ROOT / "reconstruct_open3d.py"
    try:
        rgbd_dir = find_rgbd_dataset(scan_dir)
        if rgbd_dir is not None:
            ok, message, metrics = run_rgbd_reconstruction(scan_id, rgbd_dir, result_dir)
            write_status(
                scan_id,
                state="done" if ok else "failed",
                message=message,
                resultFile="result.ply" if ok else "",
                viewerUrl=f"/viewer?scan={scan_id}",
                pipeline="open3d_rgbd_tsdf",
                coordinateSpace="unity_scan_world_v1",
                metrics=metrics,
                files=summarize_result_files(result_dir),
            )
            return

        process = subprocess.run(
            [sys.executable, str(script), "--scan", str(scan_dir), "--out", str(result_dir), "--profile", "rtabmap"],
            cwd=str(ROOT),
            text=True,
            capture_output=True,
            timeout=60 * 20,
            check=False,
        )
        (result_dir / "worker_stdout.txt").write_text(process.stdout, encoding="utf-8")
        (result_dir / "worker_stderr.txt").write_text(process.stderr, encoding="utf-8")

        if process.returncode != 0:
            write_status(
                scan_id,
                state="failed",
                message=f"Worker exited with {process.returncode}",
                resultFile="",
            )
            return

        result = result_dir / "result.obj"
        if not result.exists():
            result = result_dir / "result.ply"

        write_status(
            scan_id,
            state="done" if result.exists() else "failed",
            message="Reconstruction complete" if result.exists() else "No result mesh produced",
            resultFile=result.name if result.exists() else "",
            viewerUrl=f"/viewer?scan={scan_id}",
            files=summarize_result_files(result_dir),
        )
    except Exception as exc:  # noqa: BLE001
        write_status(scan_id, state="failed", message=str(exc), resultFile="")
    finally:
        if is_scan_deleted(scan_id):
            delete_scan_data(scan_id)


class ReconstructionServer(ThreadingHTTPServer):
    daemon_threads = True

    def __init__(self, server_address: tuple[str, int], idle_timeout_seconds: int):
        super().__init__(server_address, Handler)
        self.idle_timeout_seconds = max(1, idle_timeout_seconds)
        self.last_activity = time.monotonic()
        self.active_requests = 0
        self.active_jobs = 0
        self.activity_lock = threading.Lock()

    def begin_request(self) -> None:
        with self.activity_lock:
            self.active_requests += 1
            self.last_activity = time.monotonic()

    def end_request(self) -> None:
        with self.activity_lock:
            self.active_requests = max(0, self.active_requests - 1)
            self.last_activity = time.monotonic()

    def begin_job(self) -> None:
        with self.activity_lock:
            self.active_jobs += 1
            self.last_activity = time.monotonic()

    def end_job(self) -> None:
        with self.activity_lock:
            self.active_jobs = max(0, self.active_jobs - 1)
            self.last_activity = time.monotonic()

    def start_idle_watchdog(self) -> None:
        threading.Thread(target=self.watch_idle_timeout, daemon=True).start()

    def runtime_state(self) -> dict[str, int]:
        with self.activity_lock:
            return {
                "activeRequests": self.active_requests,
                "activeJobs": self.active_jobs,
                "idleTimeoutSeconds": self.idle_timeout_seconds,
            }

    def watch_idle_timeout(self) -> None:
        while True:
            time.sleep(1)
            with self.activity_lock:
                idle_seconds = time.monotonic() - self.last_activity
                should_stop = (
                    self.active_requests == 0
                    and self.active_jobs == 0
                    and idle_seconds >= self.idle_timeout_seconds
                )
            if should_stop:
                print(
                    f"No requests or reconstruction jobs for {self.idle_timeout_seconds}s; stopping.",
                    flush=True,
                )
                self.shutdown()
                return


def run_tracked_reconstruction_job(
    server: ReconstructionServer,
    scan_id: str,
    scan_dir: Path,
) -> None:
    try:
        reconstruction_worker(scan_id, scan_dir)
    finally:
        server.end_job()


def run_tracked_lab_jobs(
    server: ReconstructionServer,
    jobs: list[tuple[str, str, Path, Path, str]],
) -> None:
    try:
        run_lab_profile_jobs(jobs)
    finally:
        server.end_job()


def resume_pending_reconstruction_jobs(server: ReconstructionServer) -> int:
    resumed = 0
    for status_path in RESULT_ROOT.glob("*/status.json"):
        try:
            status = json.loads(status_path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            continue

        if status.get("state") not in ("queued", "processing"):
            continue

        scan_id = safe_scan_id(str(status.get("scanId") or status_path.parent.name))
        scan_dir = SCAN_ROOT / scan_id
        if not scan_dir.exists():
            write_status(
                scan_id,
                state="failed",
                message="Reconstruction process restarted, but the extracted scan data is missing",
                resultFile="",
            )
            continue

        server.begin_job()
        thread = threading.Thread(
            target=run_tracked_reconstruction_job,
            args=(server, scan_id, scan_dir),
            daemon=True,
        )
        try:
            thread.start()
        except Exception:  # noqa: BLE001
            server.end_job()
            raise
        resumed += 1

    return resumed


class Handler(BaseHTTPRequestHandler):
    server_version = "MemoAnchorReconstruction/0.1"

    def handle_one_request(self) -> None:
        self.server.begin_request()
        try:
            super().handle_one_request()
        finally:
            self.server.end_request()

    def do_POST(self) -> None:  # noqa: N802
        parsed = urlparse(self.path)
        parts = [unquote(part) for part in parsed.path.strip("/").split("/") if part]

        if len(parts) == 2 and parts[0] == "localize":
            scan_id = safe_scan_id(parts[1])
            length_header = self.headers.get("Content-Length")
            if not length_header:
                self.send_json(HTTPStatus.LENGTH_REQUIRED, {"error": "Content-Length required"})
                return
            try:
                length = int(length_header)
            except ValueError:
                self.send_json(HTTPStatus.BAD_REQUEST, {"error": "invalid Content-Length"})
                return
            if length <= 0 or length > MAX_LOCALIZATION_IMAGE_BYTES:
                self.send_json(HTTPStatus.REQUEST_ENTITY_TOO_LARGE, {"error": "camera image is too large"})
                return

            image_bytes = self.rfile.read(length)
            if len(image_bytes) != length:
                self.send_json(HTTPStatus.BAD_REQUEST, {"error": "incomplete camera image"})
                return
            try:
                intrinsics = {
                    "fx": float(self.headers["X-MemoAnchor-Fx"]),
                    "fy": float(self.headers["X-MemoAnchor-Fy"]),
                    "cx": float(self.headers["X-MemoAnchor-Cx"]),
                    "cy": float(self.headers["X-MemoAnchor-Cy"]),
                }
            except (KeyError, TypeError, ValueError):
                self.send_json(HTTPStatus.BAD_REQUEST, {"error": "camera intrinsics are required"})
                return
            try:
                result = localize(SCAN_ROOT, RESULT_ROOT, scan_id, image_bytes, intrinsics)
            except FileNotFoundError as exc:
                self.send_json(HTTPStatus.NOT_FOUND, {"localized": False, "error": str(exc)})
                return
            except (RuntimeError, ValueError) as exc:
                self.send_json(HTTPStatus.UNPROCESSABLE_ENTITY, {"localized": False, "error": str(exc)})
                return
            except Exception as exc:  # noqa: BLE001
                self.send_json(HTTPStatus.INTERNAL_SERVER_ERROR, {"localized": False, "error": str(exc)})
                return

            self.send_json(HTTPStatus.OK, result)
            return

        if len(parts) == 3 and parts[0] == "api" and parts[1] == "runs":
            scan_id = safe_scan_id(parts[2])
            scan_dir = SCAN_ROOT / scan_id
            if not (scan_dir / "manifest.json").exists():
                self.send_json(HTTPStatus.NOT_FOUND, {"error": "scan not found", "scanId": scan_id})
                return

            query = parse_qs(parsed.query)
            requested_profiles = [item for item in query.get("profile", []) if item in PRUNING_PROFILES]
            profiles = requested_profiles or list(PRUNING_PROFILES)
            run_prefix = time.strftime("%Y%m%d_%H%M%S")
            queued_runs = []
            jobs: list[tuple[str, str, Path, Path, str]] = []
            for profile in profiles:
                run_id = f"{run_prefix}_{profile}"
                result_dir = RUN_ROOT / scan_id / run_id
                suffix = 1
                while result_dir.exists():
                    suffix += 1
                    result_dir = RUN_ROOT / scan_id / f"{run_id}_{suffix}"
                run_id = result_dir.name
                write_run_status(result_dir, scan_id, run_id, state="queued", message=f"Queued {profile}", resultFile="", profile=profile)
                jobs.append((scan_id, run_id, scan_dir, result_dir, profile))
                queued_runs.append({"scanId": scan_id, "runId": run_id, "state": "queued", "profile": profile})

            self.server.begin_job()
            thread = threading.Thread(target=run_tracked_lab_jobs, args=(self.server, jobs), daemon=True)
            try:
                thread.start()
            except Exception:  # noqa: BLE001
                self.server.end_job()
                raise
            self.send_json(HTTPStatus.ACCEPTED, {"scanId": scan_id, "runs": queued_runs})
            return

        if parsed.path != "/upload":
            self.send_json(HTTPStatus.NOT_FOUND, {"error": "unknown endpoint"})
            return

        length_header = self.headers.get("Content-Length")
        if not length_header:
            self.send_json(HTTPStatus.LENGTH_REQUIRED, {"error": "Content-Length required"})
            return

        try:
            length = int(length_header)
        except ValueError:
            self.send_json(HTTPStatus.BAD_REQUEST, {"error": "invalid Content-Length"})
            return

        if length <= 0 or length > MAX_UPLOAD_BYTES:
            self.send_json(HTTPStatus.REQUEST_ENTITY_TOO_LARGE, {"error": "upload is too large"})
            return

        scan_id = safe_scan_id(self.headers.get("X-MemoAnchor-Scan-Id"))
        upload_dir = UPLOAD_ROOT / scan_id
        upload_dir.mkdir(parents=True, exist_ok=True)
        zip_path = upload_dir / f"{scan_id}.zip"

        with zip_path.open("wb") as output:
            remaining = length
            while remaining > 0:
                chunk = self.rfile.read(min(1024 * 1024, remaining))
                if not chunk:
                    break
                output.write(chunk)
                remaining -= len(chunk)

        print(f"[scan {scan_id}] uploaded {zip_path.stat().st_size / (1024 * 1024):.2f} MB", flush=True)

        try:
            scan_dir = extract_zip(zip_path, scan_id)
        except Exception as exc:  # noqa: BLE001
            write_status(scan_id, state="failed", message=f"Could not extract upload: {exc}")
            self.send_json(HTTPStatus.BAD_REQUEST, {"scanId": scan_id, "error": str(exc)})
            return

        write_status(scan_id, state="queued", message="Upload received", resultFile="")
        self.server.begin_job()
        thread = threading.Thread(
            target=run_tracked_reconstruction_job,
            args=(self.server, scan_id, scan_dir),
            daemon=True,
        )
        try:
            thread.start()
        except Exception:  # noqa: BLE001
            self.server.end_job()
            raise
        self.send_json(HTTPStatus.ACCEPTED, {"scanId": scan_id, "state": "queued"})

    def do_DELETE(self) -> None:  # noqa: N802
        parsed = urlparse(self.path)
        parts = [unquote(part) for part in parsed.path.strip("/").split("/") if part]
        if len(parts) != 2 or parts[0] != "scan":
            self.send_json(HTTPStatus.NOT_FOUND, {"error": "unknown endpoint"})
            return

        scan_id = safe_scan_id(parts[1])
        with DELETED_SCAN_IDS_LOCK:
            DELETED_SCAN_IDS.add(scan_id)
        removed = delete_scan_data(scan_id)

        self.send_json(HTTPStatus.OK, {"scanId": scan_id, "removed": removed})

    def do_GET(self) -> None:  # noqa: N802
        parsed = urlparse(self.path)
        parts = [unquote(part) for part in parsed.path.strip("/").split("/") if part]

        if parsed.path in ("/lab", "/viewer"):
            self.send_html(HTTPStatus.OK, lab_html())
            return

        if parsed.path == "/favicon.ico":
            self.send_response(HTTPStatus.NO_CONTENT)
            self.end_headers()
            return

        if len(parts) == 3 and parts[0] == "api" and parts[1] == "runs":
            scan_id = safe_scan_id(parts[2])
            self.send_json(HTTPStatus.OK, {"scanId": scan_id, "runs": list_lab_runs(scan_id)})
            return

        if len(parts) >= 3 and parts[0] == "files" and parts[1] == "current":
            scan_id = safe_scan_id(parts[2])
            self.send_result_file(RESULT_ROOT / scan_id, parts[3:])
            return

        if len(parts) >= 4 and parts[0] == "files" and parts[1] == "run":
            scan_id = safe_scan_id(parts[2])
            run_id = safe_scan_id(parts[3])
            self.send_result_file(RUN_ROOT / scan_id / run_id, parts[4:])
            return

        if len(parts) == 2 and parts[0] == "status":
            scan_id = safe_scan_id(parts[1])
            self.send_json(HTTPStatus.OK, read_status(scan_id))
            return

        if len(parts) == 2 and parts[0] == "result":
            scan_id = safe_scan_id(parts[1])
            status = read_status(scan_id)
            result_file = status.get("resultFile")
            if not result_file:
                self.send_json(HTTPStatus.NOT_FOUND, {"error": "result not ready", "status": status})
                return

            result_path = RESULT_ROOT / scan_id / str(result_file)
            if not result_path.exists():
                self.send_json(HTTPStatus.NOT_FOUND, {"error": "result file missing", "status": status})
                return

            self.send_response(HTTPStatus.OK)
            self.send_header("Content-Type", "application/octet-stream")
            self.send_header("Content-Length", str(result_path.stat().st_size))
            self.send_header("Content-Disposition", f'attachment; filename="{result_path.name}"')
            self.end_headers()
            with result_path.open("rb") as source:
                shutil.copyfileobj(source, self.wfile)
            return

        self.send_json(
            HTTPStatus.OK,
            {
                "service": "MemoAnchor reconstruction server",
                **self.server.runtime_state(),
                "endpoints": [
                    "POST /upload",
                    "POST /localize/<scanId>",
                    "GET /status/<scanId>",
                    "GET /result/<scanId>",
                    "DELETE /scan/<scanId>",
                    "GET /lab",
                    "GET /api/runs/<scanId>",
                    "POST /api/runs/<scanId>",
                ],
            },
        )

    def log_message(self, fmt: str, *args: object) -> None:
        sys.stdout.write(f"[{self.log_date_time_string()}] {fmt % args}\n")
        sys.stdout.flush()

    def send_json(self, status: HTTPStatus, payload: dict[str, object]) -> None:
        data = json.dumps(payload).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def send_html(self, status: HTTPStatus, html: str) -> None:
        data = html.encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def send_result_file(self, base_dir: Path, relative_parts: list[str]) -> None:
        if not relative_parts:
            self.send_json(HTTPStatus.BAD_REQUEST, {"error": "file path required"})
            return

        relative = Path(*relative_parts)
        if relative.is_absolute() or ".." in relative.parts:
            self.send_json(HTTPStatus.BAD_REQUEST, {"error": "unsafe file path"})
            return

        base = base_dir.resolve()
        path = (base_dir / relative).resolve()
        if not str(path).startswith(str(base)) or not path.exists() or not path.is_file():
            self.send_json(HTTPStatus.NOT_FOUND, {"error": "file not found"})
            return

        suffix = path.suffix.lower()
        content_type = {
            ".obj": "text/plain; charset=utf-8",
            ".mtl": "text/plain; charset=utf-8",
            ".ply": "application/octet-stream",
            ".png": "image/png",
        }.get(suffix, mimetypes.guess_type(path.name)[0] or "application/octet-stream")
        self.send_response(HTTPStatus.OK)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(path.stat().st_size))
        self.end_headers()
        with path.open("rb") as source:
            shutil.copyfileobj(source, self.wfile)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="0.0.0.0")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--idle-timeout-seconds", type=int, default=60)
    args = parser.parse_args()

    ensure_dirs()
    server = ReconstructionServer((args.host, args.port), args.idle_timeout_seconds)
    resumed_jobs = resume_pending_reconstruction_jobs(server)
    server.start_idle_watchdog()
    print(
        f"MemoAnchor reconstruction server listening on http://{args.host}:{args.port} "
        f"(idle timeout: {server.idle_timeout_seconds}s)",
        flush=True,
    )
    if resumed_jobs:
        print(f"Resumed {resumed_jobs} interrupted reconstruction job(s).", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nStopping server")
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
