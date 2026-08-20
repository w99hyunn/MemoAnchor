#!/usr/bin/env python3
import json
import math
import os
from dataclasses import dataclass
from typing import Dict, Iterable, List, Optional, Tuple

import numpy as np
from PIL import Image, ImageDraw


@dataclass
class RgbdDataset:
    root: str
    session: Dict
    frames: List[Dict]


@dataclass
class FrameImages:
    rgb: Image.Image
    depth: np.ndarray
    confidence: np.ndarray
    valid_depth_mask: np.ndarray


def load_dataset(dataset_root: str) -> RgbdDataset:
    root = os.path.abspath(dataset_root)
    with open(os.path.join(root, "session.json"), "r", encoding="utf-8-sig") as f:
        session = json.load(f)
    frames = []
    with open(os.path.join(root, "frames.jsonl"), "r", encoding="utf-8-sig") as f:
        for line in f:
            line = line.strip()
            if line:
                frames.append(json.loads(line))
    return RgbdDataset(root=root, session=session, frames=frames)


def frame_by_id(dataset: RgbdDataset, frame_id: int) -> Dict:
    for frame in dataset.frames:
        if int(frame["frame_id"]) == frame_id:
            return frame
    raise KeyError(f"frame_id {frame_id} was not found")


def dataset_path(dataset: RgbdDataset, rel_path: str) -> str:
    return os.path.join(dataset.root, rel_path)


def read_rgb(dataset: RgbdDataset, frame: Dict) -> Image.Image:
    return Image.open(dataset_path(dataset, frame["rgb_file"])).convert("RGB")


def read_depth(dataset: RgbdDataset, frame: Dict) -> np.ndarray:
    if frame.get("depth_format") != "DepthFloat32":
        raise ValueError(f"Unsupported depth format: {frame.get('depth_format')}")
    width = int(frame["depth_width"])
    height = int(frame["depth_height"])
    row_stride = int(frame["depth_row_stride"])
    little = bool(frame.get("depth_little_endian", True))
    dtype = np.dtype("<f4" if little else ">f4")
    data = np.fromfile(dataset_path(dataset, frame["depth_file"]), dtype=np.uint8)
    expected = row_stride * height
    if data.size < expected:
        raise ValueError(f"Depth file is too small: {data.size} < {expected}")
    depth = np.empty((height, width), dtype=np.float32)
    for y in range(height):
        row = data[y * row_stride:y * row_stride + width * 4]
        depth[y, :] = np.frombuffer(row.tobytes(), dtype=dtype, count=width)
    return depth


def read_confidence(dataset: RgbdDataset, frame: Dict) -> np.ndarray:
    if frame.get("confidence_format") != "OneComponent8":
        raise ValueError(f"Unsupported confidence format: {frame.get('confidence_format')}")
    width = int(frame["confidence_width"])
    height = int(frame["confidence_height"])
    row_stride = int(frame["confidence_row_stride"])
    pixel_stride = int(frame["confidence_pixel_stride"])
    data = np.fromfile(dataset_path(dataset, frame["confidence_file"]), dtype=np.uint8)
    expected = row_stride * height
    if data.size < expected:
        raise ValueError(f"Confidence file is too small: {data.size} < {expected}")
    confidence = np.empty((height, width), dtype=np.uint8)
    for y in range(height):
        row = data[y * row_stride:y * row_stride + row_stride]
        confidence[y, :] = row[:width * pixel_stride:pixel_stride]
    return confidence


def load_frame_images(dataset: RgbdDataset, frame: Dict, depth_min: float, depth_max: float) -> FrameImages:
    rgb = read_rgb(dataset, frame)
    depth = read_depth(dataset, frame)
    confidence = read_confidence(dataset, frame)
    valid = np.isfinite(depth) & (depth > depth_min) & (depth < depth_max)
    return FrameImages(rgb=rgb, depth=depth, confidence=confidence, valid_depth_mask=valid)


def scaled_intrinsics_for_depth(frame: Dict) -> Dict[str, float]:
    rgb_w = float(frame["rgb_width"])
    rgb_h = float(frame["rgb_height"])
    depth_w = float(frame["depth_width"])
    depth_h = float(frame["depth_height"])
    scale_x = depth_w / rgb_w
    scale_y = depth_h / rgb_h
    return {
        "fx": float(frame["fx"]) * scale_x,
        "fy": float(frame["fy"]) * scale_y,
        "cx": float(frame["cx"]) * scale_x,
        "cy": float(frame["cy"]) * scale_y,
        "scale_x": scale_x,
        "scale_y": scale_y,
        "rgb_width": int(rgb_w),
        "rgb_height": int(rgb_h),
        "depth_width": int(depth_w),
        "depth_height": int(depth_h),
    }


def transform_image_array(array: np.ndarray, transform_name: str) -> np.ndarray:
    if transform_name == "as_saved":
        return array
    if transform_name == "flip_left_right":
        return np.fliplr(array)
    if transform_name == "flip_top_bottom":
        return np.flipud(array)
    if transform_name == "rotate_90":
        return np.rot90(array, 1)
    if transform_name == "rotate_180":
        return np.rot90(array, 2)
    if transform_name == "rotate_270":
        return np.rot90(array, 3)
    raise ValueError(f"Unsupported depth transform: {transform_name}")


def transform_intrinsics(intrinsics: Dict[str, float], transform_name: str) -> Dict[str, float]:
    width = float(intrinsics["depth_width"])
    height = float(intrinsics["depth_height"])
    fx = float(intrinsics["fx"])
    fy = float(intrinsics["fy"])
    cx = float(intrinsics["cx"])
    cy = float(intrinsics["cy"])

    if transform_name == "as_saved":
        out = {"fx": fx, "fy": fy, "cx": cx, "cy": cy, "depth_width": int(width), "depth_height": int(height)}
    elif transform_name == "flip_left_right":
        out = {"fx": fx, "fy": fy, "cx": width - 1.0 - cx, "cy": cy, "depth_width": int(width), "depth_height": int(height)}
    elif transform_name == "flip_top_bottom":
        out = {"fx": fx, "fy": fy, "cx": cx, "cy": height - 1.0 - cy, "depth_width": int(width), "depth_height": int(height)}
    elif transform_name == "rotate_90":
        out = {"fx": fy, "fy": fx, "cx": cy, "cy": width - 1.0 - cx, "depth_width": int(height), "depth_height": int(width)}
    elif transform_name == "rotate_180":
        out = {"fx": fx, "fy": fy, "cx": width - 1.0 - cx, "cy": height - 1.0 - cy, "depth_width": int(width), "depth_height": int(height)}
    elif transform_name == "rotate_270":
        out = {"fx": fy, "fy": fx, "cx": height - 1.0 - cy, "cy": cx, "depth_width": int(height), "depth_height": int(width)}
    else:
        raise ValueError(f"Unsupported depth transform: {transform_name}")

    for key in ("scale_x", "scale_y", "rgb_width", "rgb_height"):
        if key in intrinsics:
            out[key] = intrinsics[key]
    return out


def transform_depth_confidence_and_intrinsics(
    frame: Dict,
    depth: np.ndarray,
    confidence: np.ndarray,
    transform_name: str,
) -> Tuple[np.ndarray, np.ndarray, Dict[str, float]]:
    intrinsics = transform_intrinsics(scaled_intrinsics_for_depth(frame), transform_name)
    return (
        transform_image_array(depth, transform_name).copy(),
        transform_image_array(confidence, transform_name).copy(),
        intrinsics,
    )


def geometry_registration_status(dataset: RgbdDataset, frame: Dict) -> Dict:
    rgb_aspect = float(frame["rgb_width"]) / float(frame["rgb_height"])
    depth_aspect = float(frame["depth_width"]) / float(frame["depth_height"])
    same_aspect = abs(rgb_aspect - depth_aspect) < 1e-4
    source_note = frame.get("applied_rotation_flip", "")
    session_note = dataset.session.get("timestamp_policy", "")
    return {
        "same_aspect_ratio": same_aspect,
        "rgb_aspect": rgb_aspect,
        "depth_aspect": depth_aspect,
        "same_provider_timestamp": abs(float(frame["rgb_timestamp"]) - float(frame["depth_timestamp"])) < 1e-6,
        "rgb_orientation": frame.get("image_orientation", "unknown"),
        "applied_rotation_flip": source_note,
        "timestamp_policy": session_note,
        "registered_color_confirmed": False,
        "reason": (
            "RGB used XRCpuImage full inputRect with MirrorY and depth/confidence were raw unrotated planes. "
            "Aspect and timestamps match, but recorder metadata does not prove ARKit RGB-depth registration/crop transform. "
            "Scripts therefore allow color for inspection but default TSDF is geometry-only."
        ),
    }


def resize_rgb_to_depth(rgb: Image.Image, frame: Dict) -> Image.Image:
    return rgb.resize((int(frame["depth_width"]), int(frame["depth_height"])), Image.Resampling.BILINEAR)


def depth_valid_mask(depth: np.ndarray, depth_min: float, depth_max: float) -> np.ndarray:
    return np.isfinite(depth) & (depth > depth_min) & (depth < depth_max)


def confidence_mask(confidence: np.ndarray, threshold: int) -> np.ndarray:
    return confidence >= int(threshold)


def depth_to_visualization(depth: np.ndarray, valid_mask: Optional[np.ndarray] = None) -> Image.Image:
    if valid_mask is None:
        valid_mask = np.isfinite(depth) & (depth > 0)
    if not np.any(valid_mask):
        return Image.fromarray(np.zeros(depth.shape, dtype=np.uint8), mode="L").convert("RGB")
    values = depth[valid_mask]
    lo, hi = np.percentile(values, [2, 98])
    if not math.isfinite(float(lo)) or not math.isfinite(float(hi)) or hi <= lo:
        lo, hi = float(values.min()), float(values.max())
    normalized = np.zeros(depth.shape, dtype=np.float32)
    normalized[valid_mask] = np.clip((depth[valid_mask] - lo) / max(1e-6, hi - lo), 0.0, 1.0)
    gray = (normalized * 255).astype(np.uint8)
    return Image.fromarray(gray, mode="L").convert("RGB")


def confidence_to_visualization(confidence: np.ndarray) -> Image.Image:
    colors = np.zeros((confidence.shape[0], confidence.shape[1], 3), dtype=np.uint8)
    colors[confidence == 0] = (180, 40, 40)
    colors[confidence == 1] = (230, 180, 40)
    colors[confidence >= 2] = (40, 200, 90)
    return Image.fromarray(colors, mode="RGB")


def depth_edges(depth: np.ndarray, valid_mask: np.ndarray, percentile: float = 92.0) -> np.ndarray:
    dz_x = np.zeros_like(depth, dtype=np.float32)
    dz_y = np.zeros_like(depth, dtype=np.float32)
    dz_x[:, 1:] = np.abs(depth[:, 1:] - depth[:, :-1])
    dz_y[1:, :] = np.abs(depth[1:, :] - depth[:-1, :])
    mag = np.maximum(dz_x, dz_y)
    mag[~valid_mask] = 0
    valid_edges = mag[mag > 0]
    if valid_edges.size == 0:
        return np.zeros_like(depth, dtype=bool)
    threshold = np.percentile(valid_edges, percentile)
    return mag >= threshold


def overlay_edges_on_rgb(rgb_at_depth: Image.Image, edges: np.ndarray) -> Image.Image:
    image = rgb_at_depth.copy()
    arr = np.array(image)
    arr[edges] = (255, 40, 40)
    return Image.fromarray(arr, mode="RGB")


def make_orientation_contact_sheet(rgb: Image.Image, depth_vis: Image.Image, confidence_vis: Image.Image) -> Image.Image:
    variants = [
        ("as_saved", depth_vis),
        ("flip_left_right", depth_vis.transpose(Image.Transpose.FLIP_LEFT_RIGHT)),
        ("flip_top_bottom", depth_vis.transpose(Image.Transpose.FLIP_TOP_BOTTOM)),
        ("rotate_90", depth_vis.transpose(Image.Transpose.ROTATE_90)),
        ("rotate_180", depth_vis.transpose(Image.Transpose.ROTATE_180)),
        ("rotate_270", depth_vis.transpose(Image.Transpose.ROTATE_270)),
    ]
    thumb_w, thumb_h = 256, 192
    sheet = Image.new("RGB", (thumb_w * 3, (thumb_h + 28) * 3), (20, 20, 20))
    draw = ImageDraw.Draw(sheet)
    rgb_thumb = rgb.resize((thumb_w, thumb_h), Image.Resampling.BILINEAR)
    conf_thumb = confidence_vis.resize((thumb_w, thumb_h), Image.Resampling.NEAREST)
    base = [("rgb_resized", rgb_thumb), ("confidence", conf_thumb)] + variants
    for i, (name, image) in enumerate(base[:9]):
        x = (i % 3) * thumb_w
        y = (i // 3) * (thumb_h + 28)
        sheet.paste(image.resize((thumb_w, thumb_h), Image.Resampling.NEAREST), (x, y + 28))
        draw.text((x + 6, y + 6), name, fill=(240, 240, 240))
    return sheet


def unproject_depth_to_camera_points(
    depth: np.ndarray,
    intrinsics: Dict[str, float],
    valid_mask: np.ndarray,
    stride: int = 1,
) -> Tuple[np.ndarray, np.ndarray]:
    ys, xs = np.nonzero(valid_mask)
    if stride > 1:
        xs = xs[::stride]
        ys = ys[::stride]
    z = depth[ys, xs].astype(np.float64)
    fx, fy = intrinsics["fx"], intrinsics["fy"]
    cx, cy = intrinsics["cx"], intrinsics["cy"]
    x = (xs.astype(np.float64) - cx) * z / fx
    y = (ys.astype(np.float64) - cy) * z / fy
    points = np.stack([x, y, z], axis=1)
    return points, np.stack([xs, ys], axis=1)


def sample_depth_colors(rgb_at_depth: Image.Image, pixels_uv: np.ndarray) -> np.ndarray:
    arr = np.asarray(rgb_at_depth, dtype=np.float32) / 255.0
    if pixels_uv.size == 0:
        return np.empty((0, 3), dtype=np.float32)
    u = np.clip(pixels_uv[:, 0], 0, arr.shape[1] - 1)
    v = np.clip(pixels_uv[:, 1], 0, arr.shape[0] - 1)
    return arr[v, u, :]


def frame_depth_stats(depth: np.ndarray, valid_mask: np.ndarray) -> Dict:
    total = int(depth.size)
    finite = np.isfinite(depth)
    invalid = ~valid_mask
    report = {
        "total_pixels": total,
        "finite_pixels": int(np.count_nonzero(finite)),
        "nan_inf_pixels": int(np.count_nonzero(~finite)),
        "zero_or_negative_pixels": int(np.count_nonzero(np.isfinite(depth) & (depth <= 0))),
        "valid_pixels": int(np.count_nonzero(valid_mask)),
        "valid_ratio": float(np.count_nonzero(valid_mask) / max(1, total)),
        "invalid_pixels": int(np.count_nonzero(invalid)),
    }
    if np.any(valid_mask):
        values = depth[valid_mask]
        report.update({
            "min_m": float(np.min(values)),
            "max_m": float(np.max(values)),
            "median_m": float(np.median(values)),
            "mean_m": float(np.mean(values)),
        })
    return report


def matrix_from_row_major(values: Iterable[float]) -> np.ndarray:
    arr = np.asarray(list(values), dtype=np.float64)
    if arr.size != 16:
        raise ValueError(f"Expected 16 matrix values, got {arr.size}")
    return arr.reshape((4, 4))


def quaternion_norm(frame: Dict) -> float:
    q = np.asarray(frame.get("camera_rotation", [0, 0, 0, 0]), dtype=np.float64)
    return float(np.linalg.norm(q))


def rotation_angle_degrees(matrix_a: np.ndarray, matrix_b: np.ndarray) -> float:
    r = matrix_a[:3, :3].T @ matrix_b[:3, :3]
    trace = float(np.trace(r))
    cos_theta = np.clip((trace - 1.0) * 0.5, -1.0, 1.0)
    return float(np.degrees(np.arccos(cos_theta)))


def unity_camera_to_world_to_open3d_camera_to_world(unity_camera_to_world: np.ndarray) -> np.ndarray:
    """Convert Unity camera-to-world pose to an Open3D-style camera-to-world pose.

    Unity camera local axes recorded here are +X right, +Y up, +Z forward.
    Open3D pinhole camera axes are +X right, +Y down, +Z forward.
    Unity world is stored as +X right, +Y up, +Z forward in the recorder metadata.

    The world Z flip makes the world basis right-handed for Open3D visualization/TSDF.
    The camera Y flip maps Unity camera up to Open3D image-down camera coordinates.
    This remains a named, isolated conversion so it can be replaced after visual checks.
    """
    unity_world_to_open3d_world = np.diag([1.0, 1.0, -1.0, 1.0])
    open3d_camera_to_unity_camera = np.diag([1.0, -1.0, 1.0, 1.0])
    return unity_world_to_open3d_world @ unity_camera_to_world @ open3d_camera_to_unity_camera


def open3d_world_to_camera_extrinsic_from_unity_camera_to_world(unity_camera_to_world: np.ndarray) -> np.ndarray:
    return np.linalg.inv(unity_camera_to_world_to_open3d_camera_to_world(unity_camera_to_world))


def transform_points(points: np.ndarray, transform: np.ndarray) -> np.ndarray:
    if points.size == 0:
        return points.copy()
    safe_points = points.astype(np.float64, copy=False)
    safe_transform = transform.astype(np.float64, copy=False)
    homo = np.concatenate([safe_points, np.ones((safe_points.shape[0], 1), dtype=np.float64)], axis=1)
    with np.errstate(divide="ignore", over="ignore", invalid="ignore"):
        out = (safe_transform @ homo.T).T
    return out[:, :3]


def filter_frames(
    frames: List[Dict],
    include_initializing: bool = False,
    frame_step: int = 1,
) -> List[Dict]:
    selected = []
    for i, frame in enumerate(frames):
        if frame_step > 1 and (i % frame_step) != 0:
            continue
        if not include_initializing and frame.get("tracking_state") != "SessionTracking":
            continue
        selected.append(frame)
    return selected


def write_json(path: str, data: Dict) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)


def write_jsonl(path: str, records: Iterable[Dict]) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        for record in records:
            f.write(json.dumps(record, ensure_ascii=False) + "\n")


def frame_pose_matrix(frame: Dict) -> np.ndarray:
    return matrix_from_row_major(frame["camera_to_world_matrix"])


def frame_pose_position(frame: Dict) -> np.ndarray:
    matrix = frame_pose_matrix(frame)
    matrix_position = matrix[:3, 3]
    explicit = np.asarray(frame.get("camera_position", matrix_position), dtype=np.float64)
    if np.linalg.norm(matrix_position - explicit) > 1e-4:
        return explicit
    return matrix_position


def analyze_trajectory(frames: List[Dict], include_initializing: bool = False, pose_jump_threshold_m: float = 0.6) -> Dict:
    records = []
    positions = []
    previous_frame = None
    previous_matrix = None
    total_distance = 0.0
    jumps = []
    tracking_counts = {}

    for frame in frames:
        state = str(frame.get("tracking_state", "unknown"))
        tracking_counts[state] = tracking_counts.get(state, 0) + 1
        if not include_initializing and state != "SessionTracking":
            continue

        position = frame_pose_position(frame)
        matrix = frame_pose_matrix(frame)
        q_norm = quaternion_norm(frame)
        translation_delta = 0.0
        rotation_delta = 0.0
        jump = False
        if previous_frame is not None:
            translation_delta = float(np.linalg.norm(position - positions[-1]))
            rotation_delta = rotation_angle_degrees(previous_matrix, matrix)
            total_distance += translation_delta
            jump = translation_delta > pose_jump_threshold_m
            if jump:
                jumps.append({
                    "frame_id": int(frame["frame_id"]),
                    "previous_frame_id": int(previous_frame["frame_id"]),
                    "translation_delta_m": translation_delta,
                    "rotation_delta_deg": rotation_delta,
                })

        positions.append(position)
        records.append({
            "frame_id": int(frame["frame_id"]),
            "tracking_state": state,
            "position": position.tolist(),
            "translation_delta_m": translation_delta,
            "rotation_delta_deg": rotation_delta,
            "quaternion_norm": q_norm,
            "pose_jump": jump,
        })
        previous_frame = frame
        previous_matrix = matrix

    if positions:
        position_array = np.asarray(positions, dtype=np.float64)
        bounds = {
            "min": position_array.min(axis=0).tolist(),
            "max": position_array.max(axis=0).tolist(),
            "size": (position_array.max(axis=0) - position_array.min(axis=0)).tolist(),
        }
        start_end_distance = float(np.linalg.norm(position_array[-1] - position_array[0]))
    else:
        bounds = {}
        start_end_distance = 0.0

    return {
        "include_initializing": include_initializing,
        "input_frame_count": len(frames),
        "trajectory_frame_count": len(records),
        "tracking_state_counts_all_frames": tracking_counts,
        "total_distance_m": total_distance,
        "start_to_end_distance_m": start_end_distance,
        "pose_jump_threshold_m": pose_jump_threshold_m,
        "pose_jumps": jumps,
        "trajectory_bounds": bounds,
        "frames": records,
    }


def write_trajectory_ply(path: str, trajectory_report: Dict) -> None:
    frames = trajectory_report.get("frames", [])
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write("ply\n")
        f.write("format ascii 1.0\n")
        f.write(f"element vertex {len(frames)}\n")
        f.write("property float x\n")
        f.write("property float y\n")
        f.write("property float z\n")
        f.write("property uchar red\n")
        f.write("property uchar green\n")
        f.write("property uchar blue\n")
        edge_count = max(0, len(frames) - 1)
        f.write(f"element edge {edge_count}\n")
        f.write("property int vertex1\n")
        f.write("property int vertex2\n")
        f.write("end_header\n")
        for record in frames:
            x, y, z = record["position"]
            if record.get("tracking_state") == "SessionTracking":
                color = (40, 220, 90)
            else:
                color = (230, 190, 40)
            if record.get("pose_jump"):
                color = (255, 40, 40)
            f.write(f"{x:.7f} {y:.7f} {z:.7f} {color[0]} {color[1]} {color[2]}\n")
        for i in range(edge_count):
            f.write(f"{i} {i + 1}\n")
