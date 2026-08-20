#!/usr/bin/env python3
import argparse
import json
import os
from typing import Iterable, Optional

import numpy as np

from reconstruction_common import (
    confidence_mask,
    confidence_to_visualization,
    dataset_path,
    depth_edges,
    depth_to_visualization,
    depth_valid_mask,
    frame_by_id,
    frame_depth_stats,
    geometry_registration_status,
    load_dataset,
    load_frame_images,
    make_orientation_contact_sheet,
    overlay_edges_on_rgb,
    resize_rgb_to_depth,
    sample_depth_colors,
    scaled_intrinsics_for_depth,
    unproject_depth_to_camera_points,
    write_json,
)


def write_ascii_ply(path: str, points: np.ndarray, colors: Optional[np.ndarray] = None) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    has_color = colors is not None and len(colors) == len(points)
    with open(path, "w", encoding="utf-8") as f:
        f.write("ply\n")
        f.write("format ascii 1.0\n")
        f.write(f"element vertex {len(points)}\n")
        f.write("property float x\n")
        f.write("property float y\n")
        f.write("property float z\n")
        if has_color:
            f.write("property uchar red\n")
            f.write("property uchar green\n")
            f.write("property uchar blue\n")
        f.write("end_header\n")
        if has_color:
            rgb = np.clip(colors * 255.0, 0, 255).astype(np.uint8)
            for point, color in zip(points, rgb):
                f.write(f"{point[0]:.7f} {point[1]:.7f} {point[2]:.7f} {int(color[0])} {int(color[1])} {int(color[2])}\n")
        else:
            for point in points:
                f.write(f"{point[0]:.7f} {point[1]:.7f} {point[2]:.7f}\n")


def point_bounds(points: np.ndarray) -> dict:
    if points.size == 0:
        return {}
    return {
        "min": points.min(axis=0).tolist(),
        "max": points.max(axis=0).tolist(),
        "size": (points.max(axis=0) - points.min(axis=0)).tolist(),
    }


def count_by_confidence(confidence: np.ndarray, valid_mask: np.ndarray) -> dict:
    result = {}
    for value in sorted(int(v) for v in np.unique(confidence)):
        result[str(value)] = int(np.count_nonzero(valid_mask & (confidence == value)))
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description="Inspect one MemoAnchor RGB-D recorder frame.")
    parser.add_argument("dataset")
    parser.add_argument("--frame-id", type=int, default=1)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--confidence-threshold", type=int, default=1, choices=[0, 1, 2])
    parser.add_argument("--depth-min", type=float, default=0.15)
    parser.add_argument("--depth-max", type=float, default=5.0)
    parser.add_argument("--point-stride", type=int, default=1)
    args = parser.parse_args()

    dataset = load_dataset(args.dataset)
    frame = frame_by_id(dataset, args.frame_id)
    os.makedirs(args.output_dir, exist_ok=True)

    images = load_frame_images(dataset, frame, args.depth_min, args.depth_max)
    intrinsics = scaled_intrinsics_for_depth(frame)
    registration = geometry_registration_status(dataset, frame)

    raw_valid = depth_valid_mask(images.depth, 0.0, float("inf"))
    filtered_valid = (
        depth_valid_mask(images.depth, args.depth_min, args.depth_max)
        & confidence_mask(images.confidence, args.confidence_threshold)
    )

    rgb_at_depth = resize_rgb_to_depth(images.rgb, frame)
    depth_raw_vis = depth_to_visualization(images.depth, raw_valid)
    depth_filtered_vis = depth_to_visualization(images.depth, filtered_valid)
    confidence_vis = confidence_to_visualization(images.confidence)
    edges = depth_edges(images.depth, filtered_valid)
    overlay = overlay_edges_on_rgb(rgb_at_depth, edges)
    contact_sheet = make_orientation_contact_sheet(images.rgb, depth_raw_vis, confidence_vis)

    images.rgb.save(os.path.join(args.output_dir, "rgb_original.png"))
    depth_raw_vis.save(os.path.join(args.output_dir, "depth_raw_visualization.png"))
    depth_filtered_vis.save(os.path.join(args.output_dir, "depth_filtered_visualization.png"))
    confidence_vis.save(os.path.join(args.output_dir, "confidence_visualization.png"))
    rgb_at_depth.save(os.path.join(args.output_dir, "rgb_at_depth_resolution.png"))
    overlay.save(os.path.join(args.output_dir, "depth_edge_overlay_on_rgb.png"))
    contact_sheet.save(os.path.join(args.output_dir, "orientation_contact_sheet.png"))

    points, pixels_uv = unproject_depth_to_camera_points(
        images.depth,
        intrinsics,
        filtered_valid,
        stride=max(1, args.point_stride),
    )
    colors = sample_depth_colors(rgb_at_depth, pixels_uv)
    write_ascii_ply(os.path.join(args.output_dir, "camera_point_cloud.ply"), points)
    write_ascii_ply(os.path.join(args.output_dir, "camera_point_cloud_colored.ply"), points, colors)

    color_success = float(len(colors) / max(1, len(points)))
    report = {
        "dataset": dataset.root,
        "frame_id": int(frame["frame_id"]),
        "source_files": {
            "rgb": dataset_path(dataset, frame["rgb_file"]),
            "depth": dataset_path(dataset, frame["depth_file"]),
            "confidence": dataset_path(dataset, frame["confidence_file"]),
        },
        "schema": {
            "depth_dtype": "float32",
            "depth_byte_order": "little-endian" if frame.get("depth_little_endian", True) else "big-endian",
            "depth_format": frame.get("depth_format"),
            "depth_unit": frame.get("depth_unit"),
            "invalid_depth_representation": frame.get("invalid_depth_policy"),
            "confidence_dtype": "uint8",
            "confidence_format": frame.get("confidence_format"),
            "confidence_value_range_observed": [int(images.confidence.min()), int(images.confidence.max())],
            "confidence_value_meaning": frame.get("confidence_value_meaning"),
            "rgb_orientation": frame.get("image_orientation"),
            "depth_orientation": "raw XRCpuImage depth plane; recorder did not rotate/flip depth",
            "applied_rotation_flip": frame.get("applied_rotation_flip"),
            "pose_convention": dataset.session.get("pose_convention"),
            "matrix_serialization_order": dataset.session.get("matrix_serialization_order"),
            "quaternion_order": dataset.session.get("quaternion_order"),
            "unity_coordinate_system": dataset.session.get("coordinate_system"),
        },
        "rgb_depth_alignment": registration,
        "intrinsics": {
            "rgb_resolution_intrinsics": {
                "width": int(frame["rgb_width"]),
                "height": int(frame["rgb_height"]),
                "fx": float(frame["fx"]),
                "fy": float(frame["fy"]),
                "cx": float(frame["cx"]),
                "cy": float(frame["cy"]),
            },
            "depth_resolution_intrinsics": intrinsics,
        },
        "filters": {
            "depth_min_m": args.depth_min,
            "depth_max_m": args.depth_max,
            "confidence_threshold": args.confidence_threshold,
        },
        "depth_stats_raw": frame_depth_stats(images.depth, raw_valid),
        "depth_stats_filtered": frame_depth_stats(images.depth, filtered_valid),
        "confidence_point_count": count_by_confidence(images.confidence, depth_valid_mask(images.depth, args.depth_min, args.depth_max)),
        "point_cloud": {
            "coordinate_convention": "Open3D pinhole camera: +X right, +Y image-down, +Z forward from camera. This single-frame cloud intentionally stays in camera coordinates.",
            "point_count": int(points.shape[0]),
            "point_stride": max(1, args.point_stride),
            "bounds": point_bounds(points),
            "color_assignment_success_ratio": color_success,
        },
        "outputs": sorted(os.listdir(args.output_dir)),
    }
    write_json(os.path.join(args.output_dir, "frame_report.json"), report)
    print(json.dumps({
        "status": "PASS",
        "frame_id": int(frame["frame_id"]),
        "point_count": int(points.shape[0]),
        "valid_ratio": report["depth_stats_filtered"]["valid_ratio"],
        "output_dir": os.path.abspath(args.output_dir),
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
