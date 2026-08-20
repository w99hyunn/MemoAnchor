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
import cv2
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
                minimum_valid_ratio = (
                    args.android_min_valid_depth_ratio
                    if is_android_dataset(dataset)
                    else args.min_valid_depth_ratio
                )
                if valid_ratio < minimum_valid_ratio:
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


def is_android_dataset(dataset) -> bool:
    runtime_platform = str(dataset.session.get("runtime_platform", "")).lower()
    depth_provider = str(dataset.session.get("depth_provider", "")).lower()
    return runtime_platform == "android" or depth_provider == "arcore"


def regularize_android_planes(mesh: o3d.geometry.TriangleMesh, args) -> Tuple[o3d.geometry.TriangleMesh, Dict]:
    stats = {
        "applied": False,
        "detected_plane_count": 0,
        "snapped_vertex_count": 0,
        "snapped_vertex_ratio": 0.0,
        "mean_displacement_m": 0.0,
        "max_displacement_m": 0.0,
        "horizontal_plane_count": 0,
        "planes": [],
    }
    if len(mesh.vertices) == 0 or len(mesh.triangles) == 0:
        return mesh, stats

    mesh.compute_vertex_normals()
    vertices = np.asarray(mesh.vertices).copy()
    vertex_normals = np.asarray(mesh.vertex_normals).copy()
    detection_cloud = o3d.geometry.PointCloud(o3d.utility.Vector3dVector(vertices))
    detection_cloud = detection_cloud.voxel_down_sample(args.android_plane_detection_voxel_size)
    remaining = o3d.geometry.PointCloud(detection_cloud)
    planes = []
    attempts = 0
    horizontal_plane_count = 0

    while len(planes) < args.android_plane_max_count and attempts < args.android_plane_max_count * 5:
        attempts += 1
        if len(remaining.points) < args.android_plane_min_cluster_points:
            break

        model, inliers = remaining.segment_plane(
            distance_threshold=args.android_plane_detection_distance,
            ransac_n=3,
            num_iterations=1200,
            probability=0.999,
        )
        if len(inliers) < args.android_plane_min_cluster_points:
            break

        inlier_cloud = remaining.select_by_index(inliers)
        labels = np.asarray(inlier_cloud.cluster_dbscan(
            eps=args.android_plane_cluster_radius,
            min_points=8,
            print_progress=False,
        ))
        valid_labels = labels[labels >= 0]
        if valid_labels.size == 0:
            remaining = remaining.select_by_index(inliers, invert=True)
            continue

        label_counts = np.bincount(valid_labels)
        largest_label = int(np.argmax(label_counts))
        cluster_mask = labels == largest_label
        cluster_indices = np.asarray(inliers, dtype=np.int64)[cluster_mask]
        cluster_points = np.asarray(remaining.points)[cluster_indices]
        if len(cluster_points) < args.android_plane_min_cluster_points:
            remaining = remaining.select_by_index(inliers, invert=True)
            continue

        center = np.mean(cluster_points, axis=0)
        centered = cluster_points - center
        _, _, basis = np.linalg.svd(centered, full_matrices=False)
        normal = basis[2]
        model_normal = np.asarray(model[:3], dtype=np.float64)
        if np.dot(normal, model_normal) < 0.0:
            normal = -normal

        tangent_u = basis[0]
        tangent_v = basis[1]
        plane_uv = np.column_stack((centered @ tangent_u, centered @ tangent_v))
        lower = np.percentile(plane_uv, 2.0, axis=0)
        upper = np.percentile(plane_uv, 98.0, axis=0)
        spans = upper - lower
        major_span = float(np.max(spans))
        minor_span = float(np.min(spans))
        is_horizontal = abs(float(normal[1])) >= args.android_plane_horizontal_normal_y
        horizontal_limit_reached = (
            is_horizontal
            and horizontal_plane_count >= args.android_plane_max_horizontal_count
        )
        if (
            major_span >= args.android_plane_min_major_span
            and minor_span >= args.android_plane_min_minor_span
            and not horizontal_limit_reached
        ):
            planes.append({
                "center": center,
                "normal": normal,
                "tangent_u": tangent_u,
                "tangent_v": tangent_v,
                "lower": lower,
                "upper": upper,
                "spans": spans,
                "cluster_point_count": int(len(cluster_points)),
                "orientation": "horizontal" if is_horizontal else "vertical_or_sloped",
            })
            if is_horizontal:
                horizontal_plane_count += 1

        remaining = remaining.select_by_index(cluster_indices.tolist(), invert=True)

    if not planes:
        return mesh, stats

    best_distance = np.full(len(vertices), np.inf, dtype=np.float64)
    selected_plane = np.full(len(vertices), -1, dtype=np.int32)
    minimum_normal_alignment = float(np.cos(np.deg2rad(args.android_plane_max_normal_angle)))

    for plane_index, plane in enumerate(planes):
        relative = vertices - plane["center"]
        signed_distance = relative @ plane["normal"]
        plane_u = relative @ plane["tangent_u"]
        plane_v = relative @ plane["tangent_v"]
        aligned = np.abs(vertex_normals @ plane["normal"]) >= minimum_normal_alignment
        inside_footprint = (
            (plane_u >= plane["lower"][0] - args.android_plane_footprint_margin)
            & (plane_u <= plane["upper"][0] + args.android_plane_footprint_margin)
            & (plane_v >= plane["lower"][1] - args.android_plane_footprint_margin)
            & (plane_v <= plane["upper"][1] + args.android_plane_footprint_margin)
        )
        distance = np.abs(signed_distance)
        candidate = (
            aligned
            & inside_footprint
            & (distance <= args.android_plane_snap_distance)
            & (distance < best_distance)
        )
        best_distance[candidate] = distance[candidate]
        selected_plane[candidate] = plane_index

    snapped = selected_plane >= 0
    corrected = vertices.copy()
    for plane_index, plane in enumerate(planes):
        selected = selected_plane == plane_index
        if not np.any(selected):
            continue
        signed_distance = (vertices[selected] - plane["center"]) @ plane["normal"]
        corrected[selected] -= signed_distance[:, None] * plane["normal"]

    displacement = np.linalg.norm(corrected[snapped] - vertices[snapped], axis=1)
    mesh.vertices = o3d.utility.Vector3dVector(corrected)
    mesh.compute_vertex_normals()
    mesh.compute_triangle_normals()
    stats.update({
        "applied": True,
        "detected_plane_count": len(planes),
        "snapped_vertex_count": int(np.count_nonzero(snapped)),
        "snapped_vertex_ratio": float(np.count_nonzero(snapped) / len(vertices)),
        "mean_displacement_m": float(np.mean(displacement)) if displacement.size else 0.0,
        "max_displacement_m": float(np.max(displacement)) if displacement.size else 0.0,
        "horizontal_plane_count": horizontal_plane_count,
        "planes": [
            {
                "normal": plane["normal"].tolist(),
                "spans_m": plane["spans"].tolist(),
                "cluster_point_count": plane["cluster_point_count"],
                "orientation": plane["orientation"],
            }
            for plane in planes
        ],
    })
    return mesh, stats


def fill_android_mesh_holes(mesh: o3d.geometry.TriangleMesh, max_radius: float) -> Tuple[o3d.geometry.TriangleMesh, Dict]:
    stats = {
        "applied": False,
        "max_radius_m": max_radius,
        "added_triangle_count": 0,
    }
    if max_radius <= 0.0 or len(mesh.vertices) == 0 or len(mesh.triangles) == 0:
        return mesh, stats

    triangle_count_before = len(mesh.triangles)
    tensor_mesh = o3d.t.geometry.TriangleMesh.from_legacy(mesh)
    filled = tensor_mesh.fill_holes(max_radius).to_legacy()
    filled.remove_degenerate_triangles()
    filled.remove_unreferenced_vertices()
    filled.compute_vertex_normals()
    filled.compute_triangle_normals()
    added_triangle_count = max(0, len(filled.triangles) - triangle_count_before)
    stats.update({
        "applied": added_triangle_count > 0,
        "added_triangle_count": added_triangle_count,
    })
    return filled, stats


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


def sample_bilinear_rgb(image: np.ndarray, pixel_x: np.ndarray, pixel_y: np.ndarray) -> np.ndarray:
    x0 = np.floor(pixel_x).astype(np.int32)
    y0 = np.floor(pixel_y).astype(np.int32)
    x1 = np.minimum(x0 + 1, image.shape[1] - 1)
    y1 = np.minimum(y0 + 1, image.shape[0] - 1)
    weight_x = (pixel_x - x0)[:, None]
    weight_y = (pixel_y - y0)[:, None]

    top = image[y0, x0].astype(np.float64) * (1.0 - weight_x) + image[y0, x1].astype(np.float64) * weight_x
    bottom = image[y1, x0].astype(np.float64) * (1.0 - weight_x) + image[y1, x1].astype(np.float64) * weight_x
    return (top * (1.0 - weight_y) + bottom * weight_y) / 255.0


def image_sharpness_score(image: np.ndarray) -> float:
    height, width = image.shape[:2]
    scale = min(1.0, 320.0 / max(height, width))
    if scale < 1.0:
        image = cv2.resize(
            image,
            (max(1, int(round(width * scale))), max(1, int(round(height * scale)))),
            interpolation=cv2.INTER_AREA,
        )

    gray = cv2.cvtColor(image, cv2.COLOR_RGB2GRAY).astype(np.float32) / 255.0
    return float(cv2.Laplacian(gray, cv2.CV_32F).var())


def reproject_android_rgb_colors(
    mesh: o3d.geometry.TriangleMesh,
    dataset,
    used_frames: List[Dict],
    args,
) -> Dict:
    vertex_count = len(mesh.vertices)
    stats = {
        "applied": False,
        "colored_vertex_count": 0,
        "vertex_count": vertex_count,
        "coverage": 0.0,
        "source_rgb_resolution": None,
        "fusion_depth_resolution": None,
        "minimum_observations": 2,
    }
    if vertex_count == 0 or not mesh.has_vertex_colors():
        return stats

    vertices = np.asarray(mesh.vertices, dtype=np.float64)
    existing_colors = np.asarray(mesh.vertex_colors, dtype=np.float64).copy()
    if len(existing_colors) != vertex_count:
        return stats

    if not mesh.has_vertex_normals():
        mesh.compute_vertex_normals()
    normals = np.asarray(mesh.vertex_normals, dtype=np.float64)
    best_colors = np.zeros((vertex_count, 3), dtype=np.float64)
    second_colors = np.zeros((vertex_count, 3), dtype=np.float64)
    best_scores = np.zeros(vertex_count, dtype=np.float64)
    second_scores = np.zeros(vertex_count, dtype=np.float64)
    observation_counts = np.zeros(vertex_count, dtype=np.int32)
    color_transform = args.color_transform or args.depth_transform
    processed_frames = 0
    sharpness_scores = []

    for frame in used_frames:
        try:
            depth = read_depth(dataset, frame)
            confidence = read_confidence(dataset, frame)
            depth, confidence, intr = transform_depth_confidence_and_intrinsics(
                frame,
                depth,
                confidence,
                args.depth_transform,
            )
            rgb = transform_image_array(
                np.asarray(read_rgb(dataset, frame), dtype=np.uint8),
                color_transform,
            ).copy()
        except Exception:
            continue

        sharpness = image_sharpness_score(rgb)
        sharpness_weight = float(np.clip(np.sqrt(sharpness / 0.003), 0.35, 1.8))
        sharpness_scores.append(sharpness)

        if stats["source_rgb_resolution"] is None:
            stats["source_rgb_resolution"] = [int(rgb.shape[1]), int(rgb.shape[0])]
            stats["fusion_depth_resolution"] = [int(depth.shape[1]), int(depth.shape[0])]

        unity_c2w = frame_pose_matrix(frame)
        extrinsic_w2c = open3d_world_to_camera_extrinsic_from_unity_camera_to_world(unity_c2w)
        camera_points = transform_points(vertices, extrinsic_w2c)
        z = camera_points[:, 2]
        with np.errstate(divide="ignore", invalid="ignore", over="ignore"):
            depth_x = intr["fx"] * camera_points[:, 0] / z + intr["cx"]
            depth_y = intr["fy"] * camera_points[:, 1] / z + intr["cy"]

        valid = np.isfinite(camera_points).all(axis=1)
        valid &= np.isfinite(depth_x) & np.isfinite(depth_y) & (z > 0.05)
        valid &= depth_x >= 1.0
        valid &= depth_x < depth.shape[1] - 1.0
        valid &= depth_y >= 1.0
        valid &= depth_y < depth.shape[0] - 1.0
        indices = np.flatnonzero(valid)
        if len(indices) == 0:
            continue

        sample_depth_x = np.rint(depth_x[indices]).astype(np.int32)
        sample_depth_y = np.rint(depth_y[indices]).astype(np.int32)
        observed_depth = depth[sample_depth_y, sample_depth_x]
        observed_confidence = confidence[sample_depth_y, sample_depth_x]
        tolerance = np.maximum(0.08, z[indices] * 0.045)
        depth_consistent = np.isfinite(observed_depth)
        depth_consistent &= observed_depth > args.depth_min
        depth_consistent &= observed_depth < args.depth_max
        depth_consistent &= observed_confidence >= args.confidence_threshold
        depth_consistent &= np.abs(observed_depth - z[indices]) <= tolerance
        indices = indices[depth_consistent]
        if len(indices) == 0:
            continue

        rgb_x = depth_x[indices] * (rgb.shape[1] - 1) / max(1, depth.shape[1] - 1)
        rgb_y = depth_y[indices] * (rgb.shape[0] - 1) / max(1, depth.shape[0] - 1)
        rgb_inside = (rgb_x >= 0.0) & (rgb_x < rgb.shape[1] - 1.0)
        rgb_inside &= (rgb_y >= 0.0) & (rgb_y < rgb.shape[0] - 1.0)
        indices = indices[rgb_inside]
        rgb_x = rgb_x[rgb_inside]
        rgb_y = rgb_y[rgb_inside]
        if len(indices) == 0:
            continue

        samples = sample_bilinear_rgb(rgb, rgb_x, rgb_y)
        normalized_x = rgb_x / max(1, rgb.shape[1] - 1)
        normalized_y = rgb_y / max(1, rgb.shape[0] - 1)
        center_distance = np.sqrt((normalized_x - 0.5) ** 2 + (normalized_y - 0.5) ** 2)
        center_score = np.clip(1.0 - center_distance / 0.7, 0.0, 1.0)

        camera_to_world = np.linalg.inv(extrinsic_w2c)
        camera_position = camera_to_world[:3, 3]
        view = camera_position - vertices[indices]
        view_length = np.maximum(np.linalg.norm(view, axis=1), 1e-6)
        view_direction = view / view_length[:, None]
        facing_score = np.clip(np.abs(np.sum(normals[indices] * view_direction, axis=1)), 0.0, 1.0)
        weights = (0.15 + center_score * center_score) * (0.2 + facing_score * facing_score)
        weights /= 0.35 + z[indices]
        scores = weights * sharpness_weight

        better_than_best = scores > best_scores[indices]
        best_indices = indices[better_than_best]
        if len(best_indices) > 0:
            second_scores[best_indices] = best_scores[best_indices]
            second_colors[best_indices] = best_colors[best_indices]
            best_scores[best_indices] = scores[better_than_best]
            best_colors[best_indices] = samples[better_than_best]

        remaining_indices = indices[~better_than_best]
        remaining_scores = scores[~better_than_best]
        remaining_samples = samples[~better_than_best]
        better_than_second = remaining_scores > second_scores[remaining_indices]
        second_indices = remaining_indices[better_than_second]
        if len(second_indices) > 0:
            second_scores[second_indices] = remaining_scores[better_than_second]
            second_colors[second_indices] = remaining_samples[better_than_second]

        observation_counts[indices] += 1
        processed_frames += 1

    candidates = (observation_counts >= 2) & (second_scores > 1e-6)
    if not np.any(candidates):
        stats["processed_frame_count"] = processed_frames
        return stats

    candidate_indices = np.flatnonzero(candidates)
    selected_weights = best_scores[candidate_indices] + second_scores[candidate_indices]
    mean_colors = (
        best_colors[candidate_indices] * best_scores[candidate_indices, None]
        + second_colors[candidate_indices] * second_scores[candidate_indices, None]
    ) / selected_weights[:, None]
    color_deviation = np.abs(
        best_colors[candidate_indices] - second_colors[candidate_indices]
    ).mean(axis=1)
    consistent = color_deviation <= 0.16
    candidate_indices = candidate_indices[consistent]
    mean_colors = mean_colors[consistent]
    color_deviation = color_deviation[consistent]
    if len(candidate_indices) == 0:
        stats["processed_frame_count"] = processed_frames
        return stats

    source_delta = np.linalg.norm(mean_colors - existing_colors[candidate_indices], axis=1)
    blend = np.clip(0.82 - color_deviation * 2.0, 0.5, 0.82)
    blend *= np.clip(1.0 - np.maximum(0.0, source_delta - 0.25), 0.45, 1.0)
    enhanced_colors = existing_colors.copy()
    enhanced_colors[candidate_indices] = (
        existing_colors[candidate_indices] * (1.0 - blend[:, None])
        + mean_colors * blend[:, None]
    )
    mesh.vertex_colors = o3d.utility.Vector3dVector(np.clip(enhanced_colors, 0.0, 1.0))

    stats["applied"] = True
    stats["colored_vertex_count"] = int(len(candidate_indices))
    stats["coverage"] = round(len(candidate_indices) / vertex_count, 4)
    stats["processed_frame_count"] = processed_frames
    stats["average_observations"] = round(float(np.mean(observation_counts[candidate_indices])), 2)
    stats["mean_color_deviation"] = round(float(np.mean(color_deviation)), 4)
    stats["mean_blend"] = round(float(np.mean(blend)), 4)
    stats["mean_source_sharpness"] = round(float(np.mean(sharpness_scores)), 6)
    stats["selected_observations_per_vertex"] = 2
    print(
        "android_high_resolution_colors "
        f"vertices={len(candidate_indices)}/{vertex_count} "
        f"coverage={stats['coverage']:.3f} "
        f"rgb={stats['source_rgb_resolution'][0]}x{stats['source_rgb_resolution'][1]} "
        f"depth={stats['fusion_depth_resolution'][0]}x{stats['fusion_depth_resolution'][1]} "
        f"observations={stats['average_observations']:.2f} "
        f"blend={stats['mean_blend']:.3f}"
    )
    return stats


def run_reconstruction(dataset, args, output_dir: str, voxel_size: float) -> Dict:
    os.makedirs(output_dir, exist_ok=True)
    start = time.time()
    android_dataset = is_android_dataset(dataset)
    effective_voxel_size = max(voxel_size, args.android_voxel_size) if android_dataset else voxel_size
    effective_sdf_trunc = max(args.sdf_trunc, args.android_sdf_trunc) if android_dataset else args.sdf_trunc
    use_color = bool(args.assume_registered_color)
    color_type = o3d.pipelines.integration.TSDFVolumeColorType.RGB8 if use_color else o3d.pipelines.integration.TSDFVolumeColorType.NoColor
    volume = o3d.pipelines.integration.ScalableTSDFVolume(
        voxel_length=effective_voxel_size,
        sdf_trunc=effective_sdf_trunc,
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
    plane_regularization_stats = {"applied": False}
    if args.android_plane_regularization and android_dataset:
        clean, plane_regularization_stats = regularize_android_planes(clean, args)

    smoothing_stats = {
        "applied": False,
        "iterations": 0,
        "mean_displacement_m": 0.0,
        "max_displacement_m": 0.0,
    }
    if android_dataset and args.android_smoothing_iterations > 0 and len(clean.vertices) > 0:
        vertices_before_smoothing = np.asarray(clean.vertices).copy()
        clean = clean.filter_smooth_simple(number_of_iterations=args.android_smoothing_iterations)
        clean.remove_degenerate_triangles()
        clean.remove_unreferenced_vertices()
        clean.compute_vertex_normals()
        clean.compute_triangle_normals()
        vertices_after_smoothing = np.asarray(clean.vertices)
        if len(vertices_after_smoothing) == len(vertices_before_smoothing):
            smoothing_displacement = np.linalg.norm(
                vertices_after_smoothing - vertices_before_smoothing,
                axis=1,
            )
            smoothing_stats = {
                "applied": True,
                "iterations": args.android_smoothing_iterations,
                "mean_displacement_m": float(np.mean(smoothing_displacement)),
                "max_displacement_m": float(np.max(smoothing_displacement)),
            }

    hole_fill_stats = {"applied": False}
    if android_dataset:
        clean, hole_fill_stats = fill_android_mesh_holes(
            clean,
            args.android_hole_fill_max_radius,
        )

    high_resolution_color_stats = {
        "applied": False,
        "colored_vertex_count": 0,
        "vertex_count": len(clean.vertices),
        "coverage": 0.0,
    }
    if android_dataset and use_color:
        high_resolution_color_stats = reproject_android_rgb_colors(
            clean,
            dataset,
            used_frames,
            args,
        )

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
        "voxel_size": effective_voxel_size,
        "sdf_trunc": effective_sdf_trunc,
        "requested_voxel_size": voxel_size,
        "requested_sdf_trunc": args.sdf_trunc,
        "depth_min": args.depth_min,
        "depth_max": args.depth_max,
        "depth_transform": args.depth_transform,
        "color_transform": args.color_transform or args.depth_transform,
        "frame_step": args.frame_step,
        "confidence_threshold": args.confidence_threshold,
        "minimum_valid_depth_ratio": args.android_min_valid_depth_ratio if android_dataset else args.min_valid_depth_ratio,
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
        "android_plane_regularization": plane_regularization_stats,
        "android_smoothing": smoothing_stats,
        "android_hole_fill": hole_fill_stats,
        "android_high_resolution_colors": high_resolution_color_stats,
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
    parser.add_argument("--android-min-valid-depth-ratio", type=float, default=0.02)
    parser.add_argument("--android-voxel-size", type=float, default=0.025)
    parser.add_argument("--android-sdf-trunc", type=float, default=0.075)
    parser.add_argument("--android-smoothing-iterations", type=int, default=3)
    parser.add_argument("--android-hole-fill-max-radius", type=float, default=0.05)
    parser.add_argument("--pose-jump-threshold", type=float, default=0.6)
    parser.add_argument("--include-initializing", action="store_true")
    parser.add_argument("--assume-registered-color", action="store_true")
    parser.add_argument("--min-cluster-triangles", type=int, default=200)
    parser.add_argument("--android-plane-regularization", action="store_true")
    parser.add_argument("--android-plane-detection-voxel-size", type=float, default=0.04)
    parser.add_argument("--android-plane-detection-distance", type=float, default=0.025)
    parser.add_argument("--android-plane-snap-distance", type=float, default=0.04)
    parser.add_argument("--android-plane-cluster-radius", type=float, default=0.12)
    parser.add_argument("--android-plane-footprint-margin", type=float, default=0.08)
    parser.add_argument("--android-plane-max-normal-angle", type=float, default=35.0)
    parser.add_argument("--android-plane-min-cluster-points", type=int, default=500)
    parser.add_argument("--android-plane-min-major-span", type=float, default=0.75)
    parser.add_argument("--android-plane-min-minor-span", type=float, default=0.3)
    parser.add_argument("--android-plane-max-count", type=int, default=8)
    parser.add_argument("--android-plane-max-horizontal-count", type=int, default=3)
    parser.add_argument("--android-plane-horizontal-normal-y", type=float, default=0.75)
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
