#!/usr/bin/env python3
"""Local upload server for MemoAnchor reconstruction scan packages."""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
import threading
import time
import zipfile
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlparse


ROOT = Path(__file__).resolve().parent
DATA_ROOT = ROOT / "data"
UPLOAD_ROOT = DATA_ROOT / "uploads"
SCAN_ROOT = DATA_ROOT / "scans"
RESULT_ROOT = DATA_ROOT / "results"


def ensure_dirs() -> None:
    for path in (UPLOAD_ROOT, SCAN_ROOT, RESULT_ROOT):
        path.mkdir(parents=True, exist_ok=True)


def safe_scan_id(value: str | None) -> str:
    if not value:
        return time.strftime("%Y%m%d_%H%M%S")

    cleaned = "".join(ch for ch in value if ch.isalnum() or ch in ("-", "_"))
    return cleaned or time.strftime("%Y%m%d_%H%M%S")


def write_status(scan_id: str, **fields: object) -> None:
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


def extract_zip(zip_path: Path, scan_id: str) -> Path:
    target = SCAN_ROOT / scan_id
    if target.exists():
        shutil.rmtree(target)
    target.mkdir(parents=True, exist_ok=True)

    with zipfile.ZipFile(zip_path, "r") as archive:
        for member in archive.infolist():
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
        process = subprocess.run(
            [sys.executable, str(script), "--scan", str(scan_dir), "--out", str(result_dir)],
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
        )
    except Exception as exc:  # noqa: BLE001
        write_status(scan_id, state="failed", message=str(exc), resultFile="")


class Handler(BaseHTTPRequestHandler):
    server_version = "MemoAnchorReconstruction/0.1"

    def do_POST(self) -> None:  # noqa: N802
        parsed = urlparse(self.path)
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
        thread = threading.Thread(target=reconstruction_worker, args=(scan_id, scan_dir), daemon=True)
        thread.start()
        self.send_json(HTTPStatus.ACCEPTED, {"scanId": scan_id, "state": "queued"})

    def do_GET(self) -> None:  # noqa: N802
        parsed = urlparse(self.path)
        parts = [unquote(part) for part in parsed.path.strip("/").split("/") if part]

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
                "endpoints": ["POST /upload", "GET /status/<scanId>", "GET /result/<scanId>"],
            },
        )

    def log_message(self, fmt: str, *args: object) -> None:
        sys.stderr.write(f"[{self.log_date_time_string()}] {fmt % args}\n")

    def send_json(self, status: HTTPStatus, payload: dict[str, object]) -> None:
        data = json.dumps(payload).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="0.0.0.0")
    parser.add_argument("--port", type=int, default=8765)
    args = parser.parse_args()

    ensure_dirs()
    server = ThreadingHTTPServer((args.host, args.port), Handler)
    print(f"MemoAnchor reconstruction server listening on http://{args.host}:{args.port}")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nStopping server")
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
