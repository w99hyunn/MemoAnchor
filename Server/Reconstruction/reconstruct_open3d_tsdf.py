#!/usr/bin/env python3
import argparse
import json
import os
import time
from typing import Dict, List, Tuple

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
from PIL import Image

try:
    import open3d as o3d
except ModuleNotFoundError as exc:
    raise SystemExit("Open3D is not installed. Run: python3 -m pip install -r Server/Reconstruction/requirements.txt") from exc

from reconstruction_common import (
    analyze_trajectory,
    confidence_mask,
    dataset_path,
    depth_valid_mask,
    filter_frames,
    frame_depth_stats,
    frame_pose_matrix,
    frame_pose_position,
    geometry_registration_status,
    load_dataset,
    open3d_world_to_camera_extrinsic_from_unity_camera_to_world,
    read_confidence,
    read_depth,
    read_rgb,
    resize_rgb_to_depth,
    scaled_intrinsics_for_depth,
    transform_depth_confidence_and_intrinsics,
    transform_image_array,
    transform_points,
    unity_camera_to_world_to_open3d_camera_to_world,
    unproject_depth_to_camera_points,
    write_json,
    write_jsonl,
    write_trajectory_ply,
)


def make_intrinsic(intr: Dict) -> o3d.camera.PinholeCameraIntrinsic:
    return o3d.camera.PinholeCameraIntrinsic(
        int(intr["depth_width"]),
        int(intr["depth_height"]),
        intr["fx"],
        intr["fy"],
        intr["cx"],
        intr["cy"],
    )


def frame_files_exist(dataset_root: str, frame: Dict) -> bool:
    return (
        os.path.isfile(os.path.join(dataset_root, frame.get("rgb_file", "")))
        and os.path.isfile(os.path.join(dataset_root, frame.get("depth_file", "")))
        and os.path.isfile(os.path.join(dataset_root, frame.get("confidence_file", "")))
    )


def filter_reconstruction_frames(dataset, args) -> Tuple[List[Dict], List[Dict]]:
    used = []
    rejected = []
    last_timestamp = None
    last_position = None

    for frame in filter_frames(dataset.frames, include_initializing=args.include_initializing, frame_step=args.frame_step):
        reasons = []
        if frame.get("tracking_state") != "SessionTracking" and not args.include_initializing:
            reasons.append("tracking_state_not_session_tracking")
        if not frame.get("has_intrinsics"):
            reasons.append("missing_intrinsics")
        if not frame_files_exist(dataset.root, frame):
            reasons.append("missing_rgb_depth_or_confidence_file")

        timestamp = float(frame.get("rgb_timestamp", 0.0))
        if last_timestamp is not None and timestamp <= last_timestamp:
            reasons.append("non_monotonic_rgb_timestamp")

        try:
            pose = frame_pose_matrix(frame)
            if not np.all(np.isfinite(pose)):
                reasons.append("invalid_pose_matrix")
        except Exception:
            reasons.append("invalid_pose_matrix")

        try:
            position = frame_pose_position(frame)
            if last_position is not None:
                pose_jump = float(np.linalg.norm(position - last_position))
                if pose_jump > args.pose_jump_threshold:
                    reasons.append(f"pose_jump_{pose_jump:.3f}m")
        except Exception:
            reasons.append("invalid_pose_position")

        if not reasons:
            try:
                depth = read_depth(dataset, frame)
                confidence = read_confidence(dataset, frame)
                valid = (
                    depth_valid_mask(depth, args.depth_min, args.depth_max)
                    & confidence_mask(confidence, args.confidence_threshold)
                )
                valid_ratio = float(np.count_nonzero(valid) / max(1, valid.size))
                if valid_ratio < args.min_valid_depth_ratio:
                    reasons.append(f"valid_depth_ratio_{valid_ratio:.3f}_below_threshold")
            except Exception as exc:
                reasons.append(f"depth_or_confidence_read_failed:{exc}")

        record = {"frame_id": int(frame["frame_id"]), "reasons": reasons}
        if reasons:
            rejected.append(record)
        else:
            used.append(frame)
            last_timestamp = timestamp
            last_position = frame_pose_position(frame)

    return used, rejected


def make_rgbd_for_frame(dataset, frame, args, use_color: bool):
    depth = read_depth(dataset, frame)
    confidence = read_confidence(dataset, frame)
    depth, confidence, intrinsics = transform_depth_confidence_and_intrinsics(frame, depth, confidence, args.depth_transform)
    valid = (
        depth_valid_mask(depth, args.depth_min, args.depth_max)
        & confidence_mask(confidence, args.confidence_threshold)
    )
    depth_filtered = depth.astype(np.float32, copy=True)
    depth_filtered[~valid] = 0.0

    if use_color:
        color = np.asarray(resize_rgb_to_depth(read_rgb(dataset, frame), frame), dtype=np.uint8)
        color_transform = args.color_transform or args.depth_transform
        color = transform_image_array(color, color_transform).copy()
        if color.shape[:2] != depth.shape[:2]:
            color_image = Image.fromarray(color, mode="RGB")
            color = np.asarray(color_image.resize((depth.shape[1], depth.shape[0]), Image.Resampling.BILINEAR), dtype=np.uint8)
    else:
        color = np.zeros((depth.shape[0], depth.shape[1], 3), dtype=np.uint8)
        color[:, :, :] = 180

    rgbd = o3d.geometry.RGBDImage.create_from_color_and_depth(
        o3d.geometry.Image(color),
        o3d.geometry.Image(depth_filtered),
        depth_scale=1.0,
        depth_trunc=args.depth_max,
        convert_rgb_to_intensity=False,
    )
    return rgbd, valid, depth, intrinsics


def clean_mesh(mesh: o3d.geometry.TriangleMesh, min_cluster_triangles: int) -> Tuple[o3d.geometry.TriangleMesh, Dict]:
    cleaned = o3d.geometry.TriangleMesh(mesh)
    cleaned.remove_duplicated_vertices()
    cleaned.remove_duplicated_triangles()
    cleaned.remove_degenerate_triangles()
    cleaned.remove_unreferenced_vertices()
    try:
        cleaned.remove_non_manifold_edges()
    except Exception:
        pass

    component_count = 0
    removed_triangles = 0
    if len(cleaned.triangles) > 0:
        clusters, counts, _ = cleaned.cluster_connected_triangles()
        clusters = np.asarray(clusters)
        counts = np.asarray(counts)
        component_count = int(len(counts))
        if min_cluster_triangles > 0 and component_count > 0:
            remove_mask = np.zeros(len(clusters), dtype=bool)
            for cluster_id, count in enumerate(counts):
                if count < min_cluster_triangles:
                    remove_mask |= clusters == cluster_id
            removed_triangles = int(np.count_nonzero(remove_mask))
            cleaned.remove_triangles_by_mask(remove_mask.tolist())
            cleaned.remove_unreferenced_vertices()

    cleaned.compute_vertex_normals()
    return cleaned, {
        "connected_component_count": component_count,
        "removed_small_component_triangles": removed_triangles,
    }


def geometry_stats(geometry) -> Dict:
    if hasattr(geometry, "vertices"):
        vertices = np.asarray(geometry.vertices)
    elif hasattr(geometry, "points"):
        vertices = np.asarray(geometry.points)
    else:
        vertices = np.empty((0, 3))
    bounds = {}
    if vertices.size:
        bounds = {
            "min": vertices.min(axis=0).tolist(),
            "max": vertices.max(axis=0).tolist(),
            "size": (vertices.max(axis=0) - vertices.min(axis=0)).tolist(),
        }
    stats = {"bounds": bounds}
    if hasattr(geometry, "vertices"):
        stats["vertex_count"] = int(len(geometry.vertices))
    if hasattr(geometry, "triangles"):
        stats["triangle_count"] = int(len(geometry.triangles))
    if hasattr(geometry, "points"):
        stats["point_count"] = int(len(geometry.points))
    return stats


def save_topdown_preview(path: str, points: np.ndarray, title: str) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    fig, ax = plt.subplots(figsize=(7, 7), dpi=140)
    if points.size:
        sample = points
        if len(sample) > 200000:
            sample = sample[np.linspace(0, len(sample) - 1, 200000).astype(int)]
        ax.scatter(sample[:, 0], sample[:, 2], s=0.1, c=sample[:, 1], cmap="viridis")
    ax.set_title(title)
    ax.set_xlabel("Open3D world X")
    ax.set_ylabel("Open3D world Z")
    ax.axis("equal")
    fig.tight_layout()
    fig.savefig(path)
    plt.close(fig)


def save_subset_previews(dataset, used_frames: List[Dict], args, output_dir: str) -> List[str]:
    checkpoints = [1, 10, 50, 100, len(used_frames)]
    labels = ["001_frame", "010_frames", "050_frames", "100_frames", "all_frames"]
    outputs = []
    for count, label in zip(checkpoints, labels):
        count = min(count, len(used_frames))
        if count <= 0:
            continue
        world_points = []
        for frame in used_frames[:count]:
            depth = read_depth(dataset, frame)
            confidence = read_confidence(dataset, frame)
            depth, confidence, intr = transform_depth_confidence_and_intrinsics(frame, depth, confidence, args.depth_transform)
            valid = depth_valid_mask(depth, args.depth_min, args.depth_max) & confidence_mask(confidence, args.confidence_threshold)
            points, _ = unproject_depth_to_camera_points(depth, intr, valid, stride=max(1, args.preview_point_stride))
            c2w = unity_camera_to_world_to_open3d_camera_to_world(frame_pose_matrix(frame))
            world_points.append(transform_points(points, c2w))
        merged = np.concatenate(world_points, axis=0) if world_points else np.empty((0, 3))
        path = os.path.join(output_dir, f"preview_{label}.png")
        save_topdown_preview(path, merged, f"{label.replace('_', ' ')} top-down accumulation")
        outputs.append(path)
    return outputs


def run_reconstruction(dataset, args, output_dir: str, voxel_size: float) -> Dict:
    os.makedirs(output_dir, exist_ok=True)
    start = time.time()
    use_color = bool(args.assume_registered_color)
    color_type = o3d.pipelines.integration.TSDFVolumeColorType.RGB8 if use_color else o3d.pipelines.integration.TSDFVolumeColorType.NoColor
    volume = o3d.pipelines.integration.ScalableTSDFVolume(
        voxel_length=voxel_size,
        sdf_trunc=args.sdf_trunc,
        color_type=color_type,
    )

    used_frames, rejected_frames = filter_reconstruction_frames(dataset, args)
    frame_reports = []

    for frame in used_frames:
        rgbd, valid, depth, intr = make_rgbd_for_frame(dataset, frame, args, use_color)
        intrinsic = make_intrinsic(intr)
        unity_c2w = frame_pose_matrix(frame)
        extrinsic_w2c = open3d_world_to_camera_extrinsic_from_unity_camera_to_world(unity_c2w)
        volume.integrate(rgbd, intrinsic, extrinsic_w2c)
        stats = frame_depth_stats(depth, valid)
        frame_reports.append({
            "frame_id": int(frame["frame_id"]),
            "tracking_state": frame.get("tracking_state"),
            "valid_depth_ratio": stats["valid_ratio"],
            "rgb_timestamp": frame.get("rgb_timestamp"),
            "timestamp_difference_ms": frame.get("timestamp_difference_ms"),
        })

    point_cloud = volume.extract_point_cloud()
    raw_mesh = volume.extract_triangle_mesh()
    raw_mesh.compute_vertex_normals()
    clean, cleanup_stats = clean_mesh(raw_mesh, args.min_cluster_triangles)

    fused_pcd_path = os.path.join(output_dir, "fused_point_cloud.ply")
    unity_fused_pcd_path = os.path.join(output_dir, "fused_point_cloud_unity.ply")
    raw_mesh_path = os.path.join(output_dir, "fused_mesh_raw.ply")
    clean_mesh_ply_path = os.path.join(output_dir, "fused_mesh_clean.ply")
    unity_clean_mesh_ply_path = os.path.join(output_dir, "fused_mesh_clean_unity.ply")
    clean_mesh_obj_path = os.path.join(output_dir, "fused_mesh_clean.obj")
    o3d.io.write_point_cloud(fused_pcd_path, point_cloud)
    o3d.io.write_triangle_mesh(raw_mesh_path, raw_mesh)
    o3d.io.write_triangle_mesh(clean_mesh_ply_path, clean)
    o3d.io.write_triangle_mesh(clean_mesh_obj_path, clean)

    # TSDF stays in Open3D's right-handed world internally. Public artifacts use
    # the Unity scan world recorded by the client so every consumer can use the
    # mesh, localized camera pose, and memo anchors without a display-time flip.
    open3d_world_to_unity_world = np.diag([1.0, 1.0, -1.0, 1.0])
    unity_point_cloud = o3d.geometry.PointCloud(point_cloud)
    unity_point_cloud.transform(open3d_world_to_unity_world)
    o3d.io.write_point_cloud(unity_fused_pcd_path, unity_point_cloud)

    unity_clean = o3d.geometry.TriangleMesh(clean)
    unity_clean.transform(open3d_world_to_unity_world)
    unity_triangles = np.asarray(unity_clean.triangles)
    if unity_triangles.size > 0:
        unity_clean.triangles = o3d.utility.Vector3iVector(unity_triangles[:, [0, 2, 1]])
    unity_clean.compute_vertex_normals()
    unity_clean.compute_triangle_normals()
    o3d.io.write_triangle_mesh(unity_clean_mesh_ply_path, unity_clean)

    trajectory = analyze_trajectory(dataset.frames, include_initializing=args.include_initializing, pose_jump_threshold_m=args.pose_jump_threshold)
    write_trajectory_ply(os.path.join(output_dir, "camera_trajectory.ply"), trajectory)
    write_json(os.path.join(output_dir, "trajectory_report.json"), trajectory)
    write_jsonl(os.path.join(output_dir, "used_frames.jsonl"), frame_reports)
    write_jsonl(os.path.join(output_dir, "rejected_frames.jsonl"), rejected_frames)

    point_array = np.asarray(point_cloud.points)
    save_topdown_preview(os.path.join(output_dir, "preview.png"), point_array, "TSDF fused point cloud top-down")
    subset_previews = save_subset_previews(dataset, used_frames, args, output_dir)

    elapsed = time.time() - start
    registration = geometry_registration_status(dataset, dataset.frames[0] if dataset.frames else {})
    report = {
        "status": "PASS" if len(used_frames) > 0 else "FAIL",
        "dataset": dataset.root,
        "voxel_size": voxel_size,
        "sdf_trunc": args.sdf_trunc,
        "depth_min": args.depth_min,
        "depth_max": args.depth_max,
        "depth_transform": args.depth_transform,
        "color_transform": args.color_transform or args.depth_transform,
        "frame_step": args.frame_step,
        "confidence_threshold": args.confidence_threshold,
        "used_frame_count": len(used_frames),
        "rejected_frame_count": len(rejected_frames),
        "color_enabled": use_color,
        "color_policy": "colored TSDF enabled by --assume-registered-color" if use_color else "geometry-only TSDF because RGB-depth registration is not proven by recorder metadata",
        "rgb_depth_registration": registration,
        "coordinate_conversion": {
            "unity_camera_to_world_to_open3d_camera_to_world": "S_world @ unity_camera_to_world @ C_camera, with S_world=diag(1,1,-1,1), C_camera=diag(1,-1,1,1)",
            "tsdf_extrinsic": "Open3D integrate() receives world-to-camera, inverse of converted camera-to-world.",
            "public_artifact_space": "unity_scan_world_v1",
            "public_artifact_conversion": "Unity public mesh = diag(1,1,-1,1) @ Open3D internal mesh; reflected triangle winding is reversed.",
        },
        "trajectory": {k: v for k, v in trajectory.items() if k != "frames"},
        "raw_mesh": geometry_stats(raw_mesh),
        "clean_mesh": {**geometry_stats(clean), **cleanup_stats},
        "point_cloud": geometry_stats(point_cloud),
        "processing_time_seconds": elapsed,
        "outputs": {
            "fused_point_cloud": fused_pcd_path,
            "fused_point_cloud_unity": unity_fused_pcd_path,
            "fused_mesh_raw": raw_mesh_path,
            "fused_mesh_clean_ply": clean_mesh_ply_path,
            "fused_mesh_clean_unity_ply": unity_clean_mesh_ply_path,
            "fused_mesh_clean_obj": clean_mesh_obj_path,
            "camera_trajectory": os.path.join(output_dir, "camera_trajectory.ply"),
            "preview": os.path.join(output_dir, "preview.png"),
            "subset_previews": subset_previews,
        },
        "geometry_quality_checks": {
            "point_cloud_behind_camera_single_frame": "Inspect frame reports/preview; per-frame unprojection creates z>0 camera points before pose fusion.",
            "floor_wall_orientation": "Use preview_*.png and PLY viewer; automatic semantic floor detection is not implemented in this baseline.",
            "mirror_or_90_degree_rotation": "Coordinate conversion is isolated and should be visually checked against trajectory direction.",
            "double_walls_or_pose_drift": "Compare preview_001/010/050/100/all to identify when accumulation diverges.",
        },
    }
    write_json(os.path.join(output_dir, "reconstruction_report.json"), report)
    return report


def run_sweep(dataset, args) -> int:
    os.makedirs(args.output_dir, exist_ok=True)
    sweep_voxels = [0.01, 0.02, 0.03]
    reports = []
    for voxel in sweep_voxels:
        subdir = os.path.join(args.output_dir, f"voxel_{voxel:.2f}".replace(".", "_"))
        report = run_reconstruction(dataset, args, subdir, voxel)
        mesh_path = report["outputs"]["fused_mesh_clean_ply"]
        report["output_size_bytes"] = os.path.getsize(mesh_path) if os.path.exists(mesh_path) else 0
        reports.append({
            "voxel_size": voxel,
            "vertex_count": report["clean_mesh"].get("vertex_count", 0),
            "triangle_count": report["clean_mesh"].get("triangle_count", 0),
            "mesh_bounds": report["clean_mesh"].get("bounds", {}),
            "processing_time_seconds": report["processing_time_seconds"],
            "output_size_bytes": report["output_size_bytes"],
            "connected_component_count": report["clean_mesh"].get("connected_component_count", 0),
            "rejected_frame_count": report["rejected_frame_count"],
            "output_dir": subdir,
        })
    write_json(os.path.join(args.output_dir, "sweep_report.json"), {"sweep": reports})
    print(json.dumps({"status": "PASS", "sweep_report": os.path.join(args.output_dir, "sweep_report.json")}, indent=2))
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Reconstruct a MemoAnchor RGB-D recorder dataset with Open3D TSDF.")
    parser.add_argument("dataset")
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--voxel-size", type=float, default=0.02)
    parser.add_argument("--sdf-trunc", type=float, default=0.06)
    parser.add_argument("--depth-min", type=float, default=0.15)
    parser.add_argument("--depth-max", type=float, default=5.0)
    parser.add_argument(
        "--depth-transform",
        default="as_saved",
        choices=["as_saved", "flip_left_right", "flip_top_bottom", "rotate_90", "rotate_180", "rotate_270"],
        help="Transform raw depth/confidence before TSDF. Use this to validate ARKit raw plane orientation against camera pose.",
    )
    parser.add_argument(
        "--color-transform",
        default=None,
        choices=["as_saved", "flip_left_right", "flip_top_bottom", "rotate_90", "rotate_180", "rotate_270"],
        help="Transform resized RGB before colored TSDF. Defaults to --depth-transform.",
    )
    parser.add_argument("--frame-step", type=int, default=1)
    parser.add_argument("--confidence-threshold", type=int, default=1, choices=[0, 1, 2])
    parser.add_argument("--min-valid-depth-ratio", type=float, default=0.05)
    parser.add_argument("--pose-jump-threshold", type=float, default=0.6)
    parser.add_argument("--include-initializing", action="store_true")
    parser.add_argument("--assume-registered-color", action="store_true")
    parser.add_argument("--min-cluster-triangles", type=int, default=200)
    parser.add_argument("--preview-point-stride", type=int, default=8)
    parser.add_argument("--sweep", action="store_true")
    args = parser.parse_args()

    dataset = load_dataset(args.dataset)
    if args.sweep:
        return run_sweep(dataset, args)
    report = run_reconstruction(dataset, args, args.output_dir, args.voxel_size)
    print(json.dumps({
        "status": report["status"],
        "used_frame_count": report["used_frame_count"],
        "rejected_frame_count": report["rejected_frame_count"],
        "clean_mesh": report["clean_mesh"],
        "output_dir": os.path.abspath(args.output_dir),
    }, indent=2))
    return 0 if report["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
