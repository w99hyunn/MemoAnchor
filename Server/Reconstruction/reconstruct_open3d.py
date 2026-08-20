#!/usr/bin/env python3
"""Best-effort Open3D reconstruction for MemoAnchor RGB-D packages."""

from __future__ import annotations

import argparse
import json
import math
import os
import re
import shutil
import struct
import subprocess
import tempfile
from pathlib import Path
from typing import Any


PRUNING_PROFILES: dict[str, dict[str, float | int | bool]] = {
    "rtabmap": {},
    "clean_texture": {
        "target_triangles": 85000,
        "bounds_margin": 0.08,
        "max_edge_length": 0.65,
        "skinny_edge_length": 0.20,
        "max_aspect_ratio": 42.0,
        "min_area": 1e-5,
        "component_fraction": 0.010,
        "component_min_triangles": 600,
        "final_component_fraction": 0.012,
        "final_component_min_triangles": 800,
        "texture_size": 8192,
        "texture_require_depth": True,
        "texture_depth_abs": 0.08,
        "texture_depth_rel": 0.045,
        "texture_margin_ratio": 0.055,
        "texture_min_projected_area": 1.6,
        "texture_min_facing": 0.14,
    },
    "geometry": {
        "target_triangles": 160000,
        "bounds_margin": 0.10,
        "max_edge_length": 0.60,
        "skinny_edge_length": 0.18,
        "max_aspect_ratio": 38.0,
        "min_area": 8e-6,
        "component_fraction": 0.006,
        "component_min_triangles": 350,
        "final_component_fraction": 0.008,
        "final_component_min_triangles": 500,
    },
    "safe": {
        "target_triangles": 90000,
        "bounds_margin": 0.12,
        "max_edge_length": 1.10,
        "skinny_edge_length": 0.32,
        "max_aspect_ratio": 85.0,
        "min_area": 5e-6,
        "component_fraction": 0.006,
        "component_min_triangles": 250,
        "final_component_fraction": 0.010,
        "final_component_min_triangles": 500,
    },
    "balanced": {
        "target_triangles": 65000,
        "bounds_margin": 0.08,
        "max_edge_length": 0.75,
        "skinny_edge_length": 0.22,
        "max_aspect_ratio": 55.0,
        "min_area": 1e-5,
        "component_fraction": 0.018,
        "component_min_triangles": 900,
        "final_component_fraction": 0.020,
        "final_component_min_triangles": 1100,
    },
    "aggressive": {
        "target_triangles": 45000,
        "bounds_margin": 0.04,
        "max_edge_length": 0.55,
        "skinny_edge_length": 0.16,
        "max_aspect_ratio": 35.0,
        "min_area": 2e-5,
        "component_fraction": 0.030,
        "component_min_triangles": 1600,
        "final_component_fraction": 0.040,
        "final_component_min_triangles": 2200,
    },
}


def pruning_profile(name: str | None) -> dict[str, float | int | bool]:
    profile_name = name if name in PRUNING_PROFILES else "balanced"
    return PRUNING_PROFILES[profile_name]


def find_executable(candidates: list[str]) -> str | None:
    for candidate in candidates:
        resolved = shutil.which(candidate)
        if resolved:
            return resolved

    for base in [
        "/opt/homebrew/bin",
        "/usr/local/bin",
        "/usr/bin",
        str(Path.home() / ".local" / "bin"),
        "/Applications/RTAB-Map.app/Contents/MacOS",
    ]:
        for candidate in candidates:
            path = Path(base) / candidate
            if path.exists() and os.access(path, os.X_OK):
                return str(path)

    return None


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def quaternion_to_matrix(q: dict[str, float]) -> list[list[float]]:
    x = q.get("x", 0.0)
    y = q.get("y", 0.0)
    z = q.get("z", 0.0)
    w = q.get("w", 1.0)
    length = math.sqrt(x * x + y * y + z * z + w * w)
    if length <= 1e-8:
        return [[1, 0, 0], [0, 1, 0], [0, 0, 1]]
    x, y, z, w = x / length, y / length, z / length, w / length
    return [
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)],
    ]


def matmul3(a: list[list[float]], b: list[list[float]]) -> list[list[float]]:
    return [[sum(a[r][k] * b[k][c] for k in range(3)) for c in range(3)] for r in range(3)]


def read_confidence_array_if_possible(scan_dir: Path, frame: dict[str, Any]) -> "object | None":
    try:
        import numpy as np
    except ImportError:
        return None

    confidence = frame.get("confidence")
    if not confidence or not confidence.get("planes"):
        return None

    plane = confidence["planes"][0]
    plane_path = scan_dir / "frames" / frame["folder"] / plane["file"]
    if not plane_path.exists():
        return None

    width = int(confidence.get("width", 0))
    height = int(confidence.get("height", 0))
    if width <= 0 or height <= 0:
        return None

    data = plane_path.read_bytes()
    row_stride = int(plane["rowStride"])
    pixel_stride = int(plane["pixelStride"])
    values = []
    for y in range(height):
        row = y * row_stride
        for x in range(width):
            offset = row + x * pixel_stride
            values.append(data[offset] if offset < len(data) else 0)

    return np.array(values, dtype=np.uint8).reshape((height, width))


def read_depth_array_if_possible(scan_dir: Path, frame: dict[str, Any], *, apply_confidence: bool = False) -> "object | None":
    try:
        import numpy as np
    except ImportError:
        return None

    depth = frame.get("depth")
    if not depth or not depth.get("planes"):
        return None

    plane = depth["planes"][0]
    plane_path = scan_dir / "frames" / frame["folder"] / plane["file"]
    if not plane_path.exists():
        return None

    width = int(depth.get("width", 0))
    height = int(depth.get("height", 0))
    if width <= 0 or height <= 0:
        return None

    values = depth_values_from_plane(
        plane_path,
        width,
        height,
        str(depth.get("format", "")),
        int(plane["rowStride"]),
        int(plane["pixelStride"]),
    )
    depth_m = np.array(values, dtype=np.float32).reshape((height, width))
    if not np.isfinite(depth_m).any() or float(np.nanmax(depth_m)) <= 0:
        return None

    if apply_confidence:
        confidence = read_confidence_array_if_possible(scan_dir, frame)
        if confidence is not None:
            if confidence.shape != depth_m.shape:
                from PIL import Image

                confidence = np.asarray(
                    Image.fromarray(confidence).resize((width, height), Image.Resampling.NEAREST),
                    dtype=np.uint8,
                )
            # ARKit confidence is 0=low, 1=medium, 2=high. Low confidence depth is the main source of torn mesh.
            depth_m[confidence <= 0] = 0.0

    return depth_m


def make_extrinsic(frame: dict[str, Any]) -> "object":
    import numpy as np

    rotation_unity = quaternion_to_matrix(frame.get("rotation", {}))
    # Open3D camera coordinates use image-down Y; Unity camera coordinates use up Y.
    flip_y = [[1, 0, 0], [0, -1, 0], [0, 0, 1]]
    rotation = matmul3(rotation_unity, flip_y)
    position = frame.get("position", {})

    camera_to_world = np.eye(4, dtype=np.float64)
    camera_to_world[:3, :3] = np.array(rotation, dtype=np.float64)
    camera_to_world[:3, 3] = np.array(
        [position.get("x", 0.0), position.get("y", 0.0), position.get("z", 0.0)],
        dtype=np.float64,
    )
    return np.linalg.inv(camera_to_world)


def depth_values_from_plane(path: Path, width: int, height: int, fmt: str, row_stride: int, pixel_stride: int) -> list[float]:
    data = path.read_bytes()
    values: list[float] = []
    fmt_lower = fmt.lower()

    for y in range(height):
        row = y * row_stride
        for x in range(width):
            offset = row + x * pixel_stride
            if fmt_lower == "depthfloat32" or fmt_lower == "onecomponent32":
                if offset + 4 > len(data):
                    values.append(0.0)
                else:
                    values.append(struct.unpack_from("<f", data, offset)[0])
            elif fmt_lower == "depthuint16":
                if offset + 2 > len(data):
                    values.append(0.0)
                else:
                    # AR depth uint16 is commonly millimeters. Keep a conservative conversion.
                    values.append(struct.unpack_from("<H", data, offset)[0] / 1000.0)
            else:
                values.append(0.0)

    return values


def write_depth_png_if_possible(scan_dir: Path, frame: dict[str, Any], output_path: Path) -> bool:
    try:
        import numpy as np
        import open3d as o3d
    except ImportError:
        return False

    depth = frame.get("depth")
    if not depth or not depth.get("planes"):
        return False

    plane = depth["planes"][0]
    plane_path = scan_dir / "frames" / frame["folder"] / plane["file"]
    if not plane_path.exists():
        return False

    width = int(depth["width"])
    height = int(depth["height"])
    values = depth_values_from_plane(
        plane_path,
        width,
        height,
        str(depth.get("format", "")),
        int(plane["rowStride"]),
        int(plane["pixelStride"]),
    )
    depth_m = np.array(values, dtype=np.float32).reshape((height, width))
    if not np.isfinite(depth_m).any() or float(np.nanmax(depth_m)) <= 0:
        return False

    confidence = read_confidence_array_if_possible(scan_dir, frame)
    if confidence is not None:
        if confidence.shape != depth_m.shape:
            from PIL import Image

            confidence = np.asarray(
                Image.fromarray(confidence).resize((width, height), Image.Resampling.NEAREST),
                dtype=np.uint8,
            )
        depth_m[confidence <= 0] = 0.0

    depth_m[(depth_m < 0.05) | (depth_m > 8.0) | ~np.isfinite(depth_m)] = 0.0

    depth_mm = np.clip(depth_m * 1000.0, 0, 65535).astype(np.uint16)
    return bool(o3d.io.write_image(str(output_path), o3d.geometry.Image(depth_mm)))


def make_color_image_for_depth(scan_dir: Path, frame: dict[str, Any], width: int, height: int) -> "object | None":
    import numpy as np
    import open3d as o3d
    from PIL import Image

    rgb_file = frame.get("rgbFile")
    if not rgb_file:
        return None

    rgb_path = scan_dir / "frames" / frame["folder"] / rgb_file
    if not rgb_path.exists():
        return None

    image = Image.open(rgb_path).convert("RGB")
    if image.size != (width, height):
        image = image.resize((width, height), Image.Resampling.BILINEAR)

    return o3d.geometry.Image(np.asarray(image, dtype=np.uint8))


def make_blank_color(width: int, height: int) -> "object":
    import numpy as np
    import open3d as o3d

    return o3d.geometry.Image(np.full((height, width, 3), 180, dtype=np.uint8))


def load_color_keyframes(scan_dir: Path, manifest: dict[str, Any]) -> list[dict[str, Any]]:
    import numpy as np
    from PIL import Image

    keyframes: list[dict[str, Any]] = []
    for frame in manifest.get("frames", []):
        if not frame.get("hasIntrinsics") or not frame.get("rgbFile"):
            continue

        rgb_path = scan_dir / "frames" / frame["folder"] / frame["rgbFile"]
        if not rgb_path.exists():
            continue

        image = np.asarray(Image.open(rgb_path).convert("RGB"), dtype=np.float32) / 255.0
        height, width = image.shape[:2]
        focal_length = frame.get("focalLength", {})
        principal_point = frame.get("principalPoint", {})
        position = frame.get("position", {})

        keyframes.append(
            {
                "id": frame.get("id", ""),
                "image": image,
                "width": width,
                "height": height,
                "fx": float(focal_length.get("x", 0.0)),
                "fy": float(focal_length.get("y", 0.0)),
                "cx": float(principal_point.get("x", width / 2)),
                "cy": float(principal_point.get("y", height / 2)),
                "position": np.array(
                    [position.get("x", 0.0), position.get("y", 0.0), position.get("z", 0.0)],
                    dtype=np.float64,
                ),
                "rotation": np.array(quaternion_to_matrix(frame.get("rotation", {})), dtype=np.float64),
                "depth": read_depth_array_if_possible(scan_dir, frame, apply_confidence=True),
            }
        )

    return keyframes


def colorize_mesh_from_keyframes(
    mesh: "object",
    scan_dir: Path,
    manifest: dict[str, Any],
    *,
    use_depth_check: bool = True,
    default_color: tuple[float, float, float] = (0.58, 0.70, 0.74),
) -> int:
    import numpy as np
    import open3d as o3d

    keyframes = load_color_keyframes(scan_dir, manifest)
    if not keyframes:
        return 0

    vertices = np.asarray(mesh.vertices, dtype=np.float64)
    if len(vertices) == 0:
        return 0

    if not mesh.has_vertex_normals():
        mesh.compute_vertex_normals()
    normals = np.asarray(mesh.vertex_normals, dtype=np.float64)

    colors = np.full((len(vertices), 3), default_color, dtype=np.float64)
    best_scores = np.full((len(vertices),), -1e9, dtype=np.float64)

    for keyframe in keyframes:
        rel = vertices - keyframe["position"]
        # For row vectors, rel @ R equals R^T * rel in Unity's camera-local projection.
        with np.errstate(invalid="ignore", over="ignore", divide="ignore"):
            camera_local = rel @ keyframe["rotation"]
            z = camera_local[:, 2]
        valid = np.isfinite(camera_local).all(axis=1) & (z > 0.05)
        if not np.any(valid):
            continue

        with np.errstate(invalid="ignore", over="ignore", divide="ignore"):
            pixel_x = keyframe["fx"] * (camera_local[:, 0] / z) + keyframe["cx"]
            pixel_y = keyframe["cy"] - keyframe["fy"] * (camera_local[:, 1] / z)

        valid &= np.isfinite(pixel_x) & np.isfinite(pixel_y)
        valid &= pixel_x >= 2
        valid &= pixel_x < keyframe["width"] - 2
        valid &= pixel_y >= 2
        valid &= pixel_y < keyframe["height"] - 2
        if not np.any(valid):
            continue

        depth = keyframe.get("depth") if use_depth_check else None
        if depth is not None:
            depth_h, depth_w = depth.shape
            depth_x = np.zeros_like(pixel_x, dtype=np.int32)
            depth_y = np.zeros_like(pixel_y, dtype=np.int32)
            valid_indices = np.flatnonzero(valid)
            depth_x[valid_indices] = np.clip(
                (pixel_x[valid_indices] / keyframe["width"] * depth_w).astype(np.int32),
                0,
                depth_w - 1,
            )
            depth_y[valid_indices] = np.clip(
                (pixel_y[valid_indices] / keyframe["height"] * depth_h).astype(np.int32),
                0,
                depth_h - 1,
            )
            sampled_depth = depth[depth_y, depth_x]
            depth_diff = np.abs(sampled_depth - z)
            tolerance = np.maximum(0.18, z * 0.10)
            valid &= (sampled_depth <= 0) | (depth_diff <= tolerance)
            if not np.any(valid):
                continue

        u = pixel_x / keyframe["width"]
        v = pixel_y / keyframe["height"]
        center_distance = np.sqrt((u - 0.5) ** 2 + (v - 0.5) ** 2)
        center_score = 1.0 - np.clip(center_distance / 0.7, 0.0, 1.0)
        distance_score = 1.0 / (0.25 + z)

        view = keyframe["position"] - vertices
        view_norm = np.linalg.norm(view, axis=1)
        view_norm = np.maximum(view_norm, 1e-6)
        view_dir = view / view_norm[:, None]
        facing_score = np.clip(np.abs(np.sum(normals * view_dir, axis=1)), 0.0, 1.0)

        score = center_score * 2.2 + distance_score + facing_score * 0.4
        update = valid & (score > best_scores)
        if not np.any(update):
            continue

        sx = np.clip(np.rint(pixel_x[update]).astype(np.int32), 0, keyframe["width"] - 1)
        sy = np.clip(np.rint(pixel_y[update]).astype(np.int32), 0, keyframe["height"] - 1)
        colors[update] = keyframe["image"][sy, sx]
        best_scores[update] = score[update]

    colored_count = int(np.count_nonzero(best_scores > -1e8))
    if colored_count > 0:
        mesh.vertex_colors = o3d.utility.Vector3dVector(colors)
    return colored_count


def limit_mesh_complexity(mesh: "object", *, target_triangles: int = 65000) -> "object":
    if len(mesh.triangles) <= target_triangles:
        return mesh

    original_triangles = len(mesh.triangles)
    simplified = mesh.simplify_quadric_decimation(target_number_of_triangles=target_triangles)
    if len(simplified.vertices) == 0 or len(simplified.triangles) == 0:
        return mesh

    simplified.remove_degenerate_triangles()
    simplified.remove_duplicated_triangles()
    simplified.remove_unreferenced_vertices()
    simplified.compute_vertex_normals()

    print(f"simplified_triangles={original_triangles}->{len(simplified.triangles)} vertices={len(simplified.vertices)}")
    return simplified


def cleanup_mesh(mesh: "object") -> "object":
    try:
        import numpy as np
        import open3d as o3d
    except ImportError:
        return mesh

    if len(mesh.vertices) == 0 or len(mesh.triangles) == 0:
        return mesh

    mesh.remove_duplicated_vertices()
    mesh.remove_duplicated_triangles()
    mesh.remove_degenerate_triangles()
    mesh.remove_non_manifold_edges()
    mesh.remove_unreferenced_vertices()

    if len(mesh.triangles) == 0:
        return mesh

    triangle_clusters, cluster_triangle_counts, _ = mesh.cluster_connected_triangles()
    if len(cluster_triangle_counts) > 1:
        counts = np.asarray(cluster_triangle_counts)
        largest = int(counts.max())
        keep_threshold = max(160, int(largest * 0.006))
        triangles_to_remove = [
            triangle_index
            for triangle_index, cluster_index in enumerate(triangle_clusters)
            if counts[cluster_index] < keep_threshold
        ]

        if triangles_to_remove and len(triangles_to_remove) < len(mesh.triangles):
            mesh.remove_triangles_by_index(triangles_to_remove)
            mesh.remove_unreferenced_vertices()

    if len(mesh.triangles) > 0:
        mesh = mesh.filter_smooth_taubin(number_of_iterations=2)
        mesh.remove_degenerate_triangles()
        mesh.remove_unreferenced_vertices()

    return mesh


def ordered_boundary_loops(mesh: "object", *, max_loop_vertices: int = 240) -> list[list[int]]:
    import collections
    import numpy as np

    triangles = np.asarray(mesh.triangles, dtype=np.int64)
    edge_counts: dict[tuple[int, int], int] = {}
    for tri in triangles:
        a, b, c = int(tri[0]), int(tri[1]), int(tri[2])
        for u, v in ((a, b), (b, c), (c, a)):
            edge = (u, v) if u < v else (v, u)
            edge_counts[edge] = edge_counts.get(edge, 0) + 1

    adjacency: dict[int, set[int]] = collections.defaultdict(set)
    for (u, v), count in edge_counts.items():
        if count == 1:
            adjacency[u].add(v)
            adjacency[v].add(u)

    loops: list[list[int]] = []
    seen_nodes: set[int] = set()
    for start in list(adjacency):
        if start in seen_nodes:
            continue

        stack = [start]
        component: list[int] = []
        seen_nodes.add(start)
        while stack:
            node = stack.pop()
            component.append(node)
            for neighbor in adjacency[node]:
                if neighbor not in seen_nodes:
                    seen_nodes.add(neighbor)
                    stack.append(neighbor)

        if len(component) < 4 or len(component) > max_loop_vertices:
            continue

        if any(len(adjacency[node]) != 2 for node in component):
            continue

        ordered = [component[0]]
        previous = -1
        current = component[0]
        for _ in range(len(component) + 1):
            neighbors = list(adjacency[current])
            next_node = neighbors[0] if neighbors[0] != previous else neighbors[1]
            if next_node == ordered[0]:
                break
            ordered.append(next_node)
            previous, current = current, next_node

        if len(ordered) == len(component):
            loops.append(ordered)

    return loops


def create_local_hole_fill_mesh(base_mesh: "object") -> "object | None":
    try:
        import numpy as np
        import open3d as o3d
    except ImportError:
        return None

    base_vertices = np.asarray(base_mesh.vertices, dtype=np.float64)
    base_triangles = np.asarray(base_mesh.triangles, dtype=np.int64)
    if len(base_vertices) == 0 or len(base_triangles) == 0:
        return None

    base_colors = np.asarray(base_mesh.vertex_colors, dtype=np.float64) if base_mesh.has_vertex_colors() else None

    vertices: list[list[float]] = []
    triangles: list[list[int]] = []
    colors: list[list[float]] = []
    filled = 0

    for loop in ordered_boundary_loops(base_mesh):
        points = base_vertices[np.asarray(loop, dtype=np.int64)]
        if not np.isfinite(points).all():
            continue

        centroid = points.mean(axis=0)
        centered = points - centroid
        try:
            _, _, vh = np.linalg.svd(centered, full_matrices=False)
        except np.linalg.LinAlgError:
            continue

        axis_u = vh[0]
        axis_v = vh[1]
        normal = vh[2]
        distances = centered @ normal
        rms = float(np.sqrt(np.mean(distances * distances)))
        projected = np.column_stack((centered @ axis_u, centered @ axis_v))
        shifted = np.roll(projected, -1, axis=0)
        signed_area = 0.5 * float(np.sum(projected[:, 0] * shifted[:, 1] - shifted[:, 0] * projected[:, 1]))
        area = abs(signed_area)
        perimeter = float(np.sum(np.linalg.norm(np.roll(points, -1, axis=0) - points, axis=1)))

        if area < 0.003 or area > 1.6:
            continue
        if perimeter > 6.0:
            continue
        if rms > max(0.035, math.sqrt(area) * 0.04):
            continue

        start = len(vertices)
        vertices.extend(points.tolist())
        vertices.append(centroid.tolist())
        center_index = start + len(loop)

        if base_colors is not None and len(base_colors) == len(base_vertices):
            loop_colors = base_colors[np.asarray(loop, dtype=np.int64)]
            colors.extend(loop_colors.tolist())
            colors.append(loop_colors.mean(axis=0).tolist())

        for i in range(len(loop)):
            a = start + i
            b = start + ((i + 1) % len(loop))
            if signed_area >= 0:
                triangles.append([center_index, a, b])
            else:
                triangles.append([center_index, b, a])

        filled += 1

    if not vertices or not triangles:
        print("local_hole_fill=none")
        return None

    fill_mesh = o3d.geometry.TriangleMesh(
        o3d.utility.Vector3dVector(np.asarray(vertices, dtype=np.float64)),
        o3d.utility.Vector3iVector(np.asarray(triangles, dtype=np.int32)),
    )
    if colors and len(colors) == len(vertices):
        fill_mesh.vertex_colors = o3d.utility.Vector3dVector(np.clip(np.asarray(colors, dtype=np.float64), 0.0, 1.0))
    fill_mesh.compute_vertex_normals()
    print(f"local_hole_fill=loops={filled} vertices={len(fill_mesh.vertices)} triangles={len(fill_mesh.triangles)}")
    return fill_mesh


def merge_with_local_hole_fill(mesh: "object") -> "object":
    fill_mesh = create_local_hole_fill_mesh(mesh)
    if fill_mesh is None:
        return mesh

    merged = mesh + fill_mesh
    merged.remove_duplicated_triangles()
    merged.remove_degenerate_triangles()
    merged.remove_unreferenced_vertices()
    merged.compute_vertex_normals()
    return merged


def clamp_vertex_colors(mesh: "object") -> None:
    try:
        import numpy as np
        import open3d as o3d
    except ImportError:
        return

    if not mesh.has_vertex_colors():
        return

    colors = np.asarray(mesh.vertex_colors, dtype=np.float64)
    mesh.vertex_colors = o3d.utility.Vector3dVector(np.clip(colors, 0.0, 1.0))


def vector3_from_dict(value: dict[str, Any] | None) -> "object | None":
    if not value:
        return None

    try:
        import numpy as np

        vector = np.array(
            [
                float(value.get("x", 0.0)),
                float(value.get("y", 0.0)),
                float(value.get("z", 0.0)),
            ],
            dtype=np.float64,
        )
        return vector if np.isfinite(vector).all() else None
    except (TypeError, ValueError):
        return None


def manifest_bounds(manifest: dict[str, Any]) -> tuple["object", "object"] | None:
    bounds = manifest.get("bounds") or {}
    min_value = vector3_from_dict(bounds.get("min"))
    max_value = vector3_from_dict(bounds.get("max"))
    if min_value is None or max_value is None:
        return None
    return min_value, max_value


def remove_triangles_by_shape(
    mesh: "object",
    *,
    max_edge_length: float = 0.75,
    skinny_edge_length: float = 0.22,
    max_aspect_ratio: float = 55.0,
    min_area: float = 1e-5,
    label: str = "shape",
) -> "object":
    """Remove torn sheets and spikes without requiring texture/color data."""
    try:
        import numpy as np
    except ImportError:
        return mesh

    if len(mesh.vertices) == 0 or len(mesh.triangles) == 0:
        return mesh

    vertices = np.asarray(mesh.vertices, dtype=np.float64)
    triangles = np.asarray(mesh.triangles, dtype=np.int64)
    points = vertices[triangles]

    edge01 = np.linalg.norm(points[:, 0] - points[:, 1], axis=1)
    edge12 = np.linalg.norm(points[:, 1] - points[:, 2], axis=1)
    edge20 = np.linalg.norm(points[:, 2] - points[:, 0], axis=1)
    max_edge = np.maximum(np.maximum(edge01, edge12), edge20)

    twice_area = np.linalg.norm(np.cross(points[:, 1] - points[:, 0], points[:, 2] - points[:, 0]), axis=1)
    area = twice_area * 0.5
    aspect = (max_edge * max_edge) / np.maximum(twice_area, 1e-9)

    remove = (area < min_area) | (max_edge > max_edge_length) | ((max_edge > skinny_edge_length) & (aspect > max_aspect_ratio))
    remove_indices = np.flatnonzero(remove)
    if len(remove_indices) == 0 or len(remove_indices) >= len(triangles):
        return mesh

    mesh.remove_triangles_by_index(remove_indices.tolist())
    mesh.remove_unreferenced_vertices()
    mesh.remove_degenerate_triangles()
    print(f"mesh_prune_{label}=triangles_removed={len(remove_indices)} remaining={len(mesh.triangles)}")
    return mesh


def crop_mesh_to_manifest_bounds(mesh: "object", manifest: dict[str, Any], *, margin: float = 0.08) -> "object":
    try:
        import numpy as np
    except ImportError:
        return mesh

    bounds = manifest_bounds(manifest)
    if bounds is None or len(mesh.vertices) == 0 or len(mesh.triangles) == 0:
        return mesh

    bounds_min, bounds_max = bounds
    crop_min = bounds_min - margin
    crop_max = bounds_max + margin
    vertices = np.asarray(mesh.vertices, dtype=np.float64)
    triangles = np.asarray(mesh.triangles, dtype=np.int64)
    centers = vertices[triangles].mean(axis=1)

    outside = np.any(centers < crop_min, axis=1) | np.any(centers > crop_max, axis=1)
    remove_indices = np.flatnonzero(outside)
    if len(remove_indices) == 0 or len(remove_indices) >= len(triangles):
        return mesh

    mesh.remove_triangles_by_index(remove_indices.tolist())
    mesh.remove_unreferenced_vertices()
    mesh.remove_degenerate_triangles()
    print(f"mesh_prune_bounds=triangles_removed={len(remove_indices)} remaining={len(mesh.triangles)} margin={margin}")
    return mesh


def remove_small_triangle_components(
    mesh: "object",
    *,
    min_fraction_of_largest: float = 0.018,
    min_triangles: int = 900,
    label: str = "components",
) -> "object":
    try:
        import numpy as np
    except ImportError:
        return mesh

    if len(mesh.vertices) == 0 or len(mesh.triangles) == 0:
        return mesh

    triangle_clusters, cluster_triangle_counts, _ = mesh.cluster_connected_triangles()
    if len(cluster_triangle_counts) <= 1:
        return mesh

    counts = np.asarray(cluster_triangle_counts)
    largest = int(counts.max())
    keep_threshold = max(min_triangles, int(largest * min_fraction_of_largest))
    remove_indices = [
        triangle_index
        for triangle_index, cluster_index in enumerate(triangle_clusters)
        if counts[cluster_index] < keep_threshold
    ]
    if not remove_indices or len(remove_indices) >= len(mesh.triangles):
        return mesh

    mesh.remove_triangles_by_index(remove_indices)
    mesh.remove_unreferenced_vertices()
    mesh.remove_degenerate_triangles()
    print(
        f"mesh_prune_{label}=clusters={len(counts)} "
        f"threshold={keep_threshold} triangles_removed={len(remove_indices)} remaining={len(mesh.triangles)}"
    )
    return mesh


def prune_reconstruction_mesh(
    mesh: "object",
    manifest: dict[str, Any],
    *,
    stage: str,
    profile: dict[str, float | int],
) -> "object":
    if len(mesh.vertices) == 0 or len(mesh.triangles) == 0:
        return mesh

    before = len(mesh.triangles)
    mesh = crop_mesh_to_manifest_bounds(mesh, manifest, margin=float(profile["bounds_margin"]))
    mesh = remove_triangles_by_shape(
        mesh,
        max_edge_length=float(profile["max_edge_length"]),
        skinny_edge_length=float(profile["skinny_edge_length"]),
        max_aspect_ratio=float(profile["max_aspect_ratio"]),
        min_area=float(profile["min_area"]),
        label=f"{stage}_shape",
    )
    mesh = remove_small_triangle_components(
        mesh,
        min_fraction_of_largest=float(profile["component_fraction"]),
        min_triangles=int(profile["component_min_triangles"]),
        label=f"{stage}_components",
    )
    mesh.remove_duplicated_triangles()
    mesh.remove_degenerate_triangles()
    mesh.remove_unreferenced_vertices()
    if len(mesh.triangles) > 0:
        mesh.compute_vertex_normals()
    print(f"mesh_prune_{stage}=triangles={before}->{len(mesh.triangles)} vertices={len(mesh.vertices)}")
    return mesh


def polygon_area_2d(points: "object") -> float:
    import numpy as np

    shifted = np.roll(points, -1, axis=0)
    return 0.5 * float(np.sum(points[:, 0] * shifted[:, 1] - shifted[:, 0] * points[:, 1]))


def create_detected_plane_mesh(manifest: dict[str, Any], base_mesh: "object | None" = None) -> "object | None":
    try:
        import numpy as np
        import open3d as o3d
    except ImportError:
        return None

    detected_planes = manifest.get("planes") or []
    if not detected_planes:
        return None

    scan_bounds = manifest_bounds(manifest)
    bounds_margin = 0.45
    vertices: list[list[float]] = []
    triangles: list[list[int]] = []
    colors: list[list[float]] = []
    accepted = 0

    for plane in detected_planes:
        if str(plane.get("trackingState", "")).lower() == "none":
            continue
        if str(plane.get("alignment", "")).lower() == "horizontaldown":
            continue

        boundary = plane.get("boundaryWorld") or []
        if len(boundary) < 3:
            continue

        points = []
        for item in boundary:
            point = vector3_from_dict(item)
            if point is not None:
                points.append(point)
        if len(points) < 3:
            continue

        points_array = np.asarray(points, dtype=np.float64)
        if not np.isfinite(points_array).all():
            continue

        # Drop repeated ARPlane boundary points that would create zero-area triangles.
        deduped = [points_array[0]]
        for point in points_array[1:]:
            if float(np.linalg.norm(point - deduped[-1])) > 0.01:
                deduped.append(point)
        if len(deduped) >= 3 and float(np.linalg.norm(deduped[0] - deduped[-1])) <= 0.01:
            deduped.pop()
        if len(deduped) < 3:
            continue
        points_array = np.asarray(deduped, dtype=np.float64)

        if scan_bounds is not None:
            bounds_min, bounds_max = scan_bounds
            centroid_for_bounds = points_array.mean(axis=0)
            if np.any(centroid_for_bounds < bounds_min - bounds_margin) or np.any(centroid_for_bounds > bounds_max + bounds_margin):
                continue

        centroid = points_array.mean(axis=0)
        centered = points_array - centroid
        try:
            _, _, vh = np.linalg.svd(centered, full_matrices=False)
        except np.linalg.LinAlgError:
            continue

        axis_u = vh[0]
        axis_v = vh[1]
        fitted_normal = vh[2]
        normal = vector3_from_dict(plane.get("normal"))
        if normal is None or float(np.linalg.norm(normal)) <= 1e-6:
            normal = fitted_normal
        normal = normal / max(float(np.linalg.norm(normal)), 1e-6)
        if float(np.dot(normal, fitted_normal)) < 0.0:
            fitted_normal = -fitted_normal

        plane_distances = centered @ fitted_normal
        projected = np.column_stack((centered @ axis_u, centered @ axis_v))
        signed_area = polygon_area_2d(projected)
        area = abs(signed_area)
        perimeter = float(np.sum(np.linalg.norm(np.roll(points_array, -1, axis=0) - points_array, axis=1)))
        rms = float(np.sqrt(np.mean(plane_distances * plane_distances)))

        if area < 0.05 or area > 18.0:
            continue
        if perimeter > 24.0:
            continue
        if rms > max(0.055, math.sqrt(area) * 0.025):
            continue

        start = len(vertices)
        vertices.extend(points_array.tolist())
        vertices.append(centroid.tolist())
        center_index = start + len(points_array)

        alignment = str(plane.get("alignment", "")).lower()
        if "vertical" in alignment:
            color = [0.62, 0.68, 0.70]
        elif "horizontal" in alignment:
            color = [0.50, 0.56, 0.54]
        else:
            color = [0.56, 0.62, 0.62]
        colors.extend([color] * (len(points_array) + 1))

        for i in range(len(points_array)):
            a = start + i
            b = start + ((i + 1) % len(points_array))
            winding = np.cross(points_array[i] - centroid, points_array[(i + 1) % len(points_array)] - centroid)
            if float(np.dot(winding, normal)) >= 0.0:
                triangles.append([center_index, a, b])
            else:
                triangles.append([center_index, b, a])

        accepted += 1

    if not vertices or not triangles:
        print(f"plane_guided_fill=none detected={len(detected_planes)}")
        return None

    plane_mesh = o3d.geometry.TriangleMesh(
        o3d.utility.Vector3dVector(np.asarray(vertices, dtype=np.float64)),
        o3d.utility.Vector3iVector(np.asarray(triangles, dtype=np.int32)),
    )
    plane_mesh.vertex_colors = o3d.utility.Vector3dVector(np.asarray(colors, dtype=np.float64))
    plane_mesh.compute_vertex_normals()
    print(f"plane_guided_fill=planes={accepted}/{len(detected_planes)} vertices={len(plane_mesh.vertices)} triangles={len(plane_mesh.triangles)}")
    return plane_mesh


def merge_with_detected_planes(mesh: "object", manifest: dict[str, Any]) -> "object":
    plane_mesh = create_detected_plane_mesh(manifest, mesh)
    if plane_mesh is None:
        return mesh

    merged = mesh + plane_mesh
    merged.remove_duplicated_vertices()
    merged.remove_duplicated_triangles()
    merged.remove_degenerate_triangles()
    merged.remove_unreferenced_vertices()
    merged.compute_vertex_normals()
    return merged


def try_open3d_tsdf(scan_dir: Path, out_dir: Path, manifest: dict[str, Any], *, profile: dict[str, float | int]) -> Path | None:
    try:
        import open3d as o3d
    except ImportError:
        return None

    volume = o3d.pipelines.integration.ScalableTSDFVolume(
        voxel_length=0.025,
        sdf_trunc=0.08,
        color_type=o3d.pipelines.integration.TSDFVolumeColorType.RGB8,
    )

    integrated = 0
    temp_depth_dir = out_dir / "depth_png"
    temp_depth_dir.mkdir(parents=True, exist_ok=True)

    for frame in manifest.get("frames", []):
        if not frame.get("hasIntrinsics"):
            continue

        depth_png = temp_depth_dir / f"{frame['id']}_depth.png"
        if not write_depth_png_if_possible(scan_dir, frame, depth_png):
            continue

        depth_meta = frame.get("depth", {})
        focal_length = frame.get("focalLength", {})
        principal_point = frame.get("principalPoint", {})

        width = int(depth_meta.get("width", 0))
        height = int(depth_meta.get("height", 0))
        if width <= 0 or height <= 0:
            continue

        # Scale RGB camera intrinsics to the depth image grid for geometry fusion.
        image_resolution = frame.get("imageResolution", {})
        rgb_width = float(image_resolution.get("x", width) or width)
        rgb_height = float(image_resolution.get("y", height) or height)
        scale_x = width / rgb_width
        scale_y = height / rgb_height

        color = make_color_image_for_depth(scan_dir, frame, width, height) or make_blank_color(width, height)
        depth = o3d.io.read_image(str(depth_png))
        intrinsic = o3d.camera.PinholeCameraIntrinsic(
            width,
            height,
            float(focal_length.get("x", 0.0)) * scale_x,
            float(focal_length.get("y", 0.0)) * scale_y,
            float(principal_point.get("x", width / 2)) * scale_x,
            float(principal_point.get("y", height / 2)) * scale_y,
        )
        rgbd = o3d.geometry.RGBDImage.create_from_color_and_depth(
            color,
            depth,
            depth_scale=1000.0,
            depth_trunc=8.0,
            convert_rgb_to_intensity=False,
        )
        volume.integrate(rgbd, intrinsic, make_extrinsic(frame))
        integrated += 1

    if integrated == 0:
        return None

    mesh = volume.extract_triangle_mesh()
    mesh = cleanup_mesh(mesh)
    mesh.compute_vertex_normals()
    if not mesh.has_vertex_colors():
        colored_count = colorize_mesh_from_keyframes(mesh, scan_dir, manifest)
        if colored_count:
            print(f"colored_vertices={colored_count}/{len(mesh.vertices)}")
    else:
        print(f"integrated_color_vertices={len(mesh.vertices)}")
    mesh = merge_with_local_hole_fill(mesh)
    mesh = prune_reconstruction_mesh(mesh, manifest, stage="tsdf_pre_simplify", profile=profile)
    mesh = limit_mesh_complexity(mesh, target_triangles=int(profile["target_triangles"]))
    mesh = merge_with_detected_planes(mesh, manifest)
    mesh = remove_small_triangle_components(
        mesh,
        min_fraction_of_largest=float(profile["final_component_fraction"]),
        min_triangles=int(profile["final_component_min_triangles"]),
        label="tsdf_final_components",
    )
    clamp_vertex_colors(mesh)
    result_path = out_dir / "result_tsdf.ply"
    if not o3d.io.write_triangle_mesh(str(result_path), mesh):
        return None
    return result_path


def find_raw_mesh(scan_dir: Path) -> Path | None:
    raw_mesh = scan_dir / "raw_mesh.obj"
    if raw_mesh.exists():
        return raw_mesh

    nested = list(scan_dir.glob("*/raw_mesh.obj"))
    return nested[0] if nested else None


def colorized_raw_mesh(scan_dir: Path, out_dir: Path, manifest: dict[str, Any], *, profile: dict[str, float | int]) -> Path | None:
    try:
        import open3d as o3d
    except ImportError:
        return None

    raw_mesh = find_raw_mesh(scan_dir)
    if raw_mesh is None:
        return None

    mesh = o3d.io.read_triangle_mesh(str(raw_mesh), enable_post_processing=True)
    if len(mesh.vertices) == 0 or len(mesh.triangles) == 0:
        return None

    mesh.compute_vertex_normals()
    colored_count = colorize_mesh_from_keyframes(mesh, scan_dir, manifest)
    coverage = colored_count / max(1, len(mesh.vertices))
    print(f"raw_colored_vertices={colored_count}/{len(mesh.vertices)} coverage={coverage:.3f}")
    if colored_count == 0:
        return None

    mesh = cleanup_mesh(mesh)
    mesh = prune_reconstruction_mesh(mesh, manifest, stage="raw_pre_fill", profile=profile)
    mesh = merge_with_local_hole_fill(mesh)
    mesh = limit_mesh_complexity(mesh, target_triangles=int(profile["target_triangles"]))
    mesh.compute_vertex_normals()
    mesh = merge_with_detected_planes(mesh, manifest)
    mesh = remove_small_triangle_components(
        mesh,
        min_fraction_of_largest=float(profile["final_component_fraction"]),
        min_triangles=int(profile["final_component_min_triangles"]),
        label="raw_final_components",
    )
    mesh.compute_triangle_normals()
    clamp_vertex_colors(mesh)

    # Keep a vertex-colored PLY for debugging and older viewers.
    raw_colored = out_dir / "result_raw_colored.ply"
    if not o3d.io.write_triangle_mesh(str(raw_colored), mesh):
        return None

    alice_result = try_alicevision_texturing(
        mesh,
        scan_dir,
        manifest,
        out_dir,
        mesh_path=out_dir / "clean_mesh.obj",
    )
    if alice_result is not None:
        return alice_result

    # Preferred output: texture sampled directly from RGB keyframes.
    baked = bake_keyframe_texture_atlas(
        mesh,
        scan_dir,
        manifest,
        out_dir,
        texture_size=int(profile.get("texture_size", 4096)),
        require_depth=bool(profile.get("texture_require_depth", False)),
        depth_abs_tolerance=float(profile.get("texture_depth_abs", 0.18)),
        depth_rel_tolerance=float(profile.get("texture_depth_rel", 0.10)),
        margin_ratio=float(profile.get("texture_margin_ratio", 0.0)),
        min_projected_area=float(profile.get("texture_min_projected_area", 0.35)),
        min_facing_score=float(profile.get("texture_min_facing", 0.0)),
        fallback_vertex_colors=not bool(profile.get("texture_require_depth", False)),
    )
    if baked is not None:
        return baked

    # Fallback: convert vertex colors to a simple texture so OBJ viewers still get visible color.
    baked = bake_vertex_color_texture(
        mesh,
        out_dir,
        obj_name="result.obj",
        mtl_name="result.mtl",
        texture_name="result_texture.png",
    )
    if baked is not None:
        return baked

    return raw_colored


def geometry_raw_mesh(scan_dir: Path, out_dir: Path, manifest: dict[str, Any], *, profile: dict[str, float | int]) -> Path | None:
    try:
        import numpy as np
        import open3d as o3d
    except ImportError:
        return None

    raw_mesh = find_raw_mesh(scan_dir)
    if raw_mesh is None:
        return None

    mesh = o3d.io.read_triangle_mesh(str(raw_mesh), enable_post_processing=True)
    if len(mesh.vertices) == 0 or len(mesh.triangles) == 0:
        return None

    mesh.compute_vertex_normals()
    before_vertices = len(mesh.vertices)
    before_triangles = len(mesh.triangles)

    mesh = cleanup_mesh(mesh)
    mesh = prune_reconstruction_mesh(mesh, manifest, stage="geometry_raw", profile=profile)
    mesh = limit_mesh_complexity(mesh, target_triangles=int(profile["target_triangles"]))
    mesh = merge_with_detected_planes(mesh, manifest)
    mesh = remove_small_triangle_components(
        mesh,
        min_fraction_of_largest=float(profile["final_component_fraction"]),
        min_triangles=int(profile["final_component_min_triangles"]),
        label="geometry_final_components",
    )
    if len(mesh.triangles) == 0:
        return None

    mesh.compute_vertex_normals()
    apply_structural_vertex_colors(mesh)

    result = out_dir / "result.ply"
    if not o3d.io.write_triangle_mesh(str(result), mesh):
        return None

    print(
        "geometry_mesh="
        f"vertices={before_vertices}->{len(mesh.vertices)} "
        f"triangles={before_triangles}->{len(mesh.triangles)} "
        "texture=disabled"
    )
    print(f"chosen_result={result.name}")
    return result


def apply_structural_vertex_colors(mesh: "object") -> None:
    """Apply calm map colors by surface orientation, avoiding noisy photo patches."""
    try:
        import numpy as np
        import open3d as o3d
    except ImportError:
        return

    if len(mesh.vertices) == 0:
        return

    if not mesh.has_vertex_normals():
        mesh.compute_vertex_normals()

    normals = np.asarray(mesh.vertex_normals, dtype=np.float64)
    vertices = np.asarray(mesh.vertices, dtype=np.float64)
    if len(normals) != len(vertices):
        return

    y = vertices[:, 1]
    y_min = float(np.min(y))
    y_max = float(np.max(y))
    y_range = max(1e-6, y_max - y_min)
    height_t = np.clip((y - y_min) / y_range, 0.0, 1.0)

    wall = np.asarray([0.58, 0.68, 0.72], dtype=np.float64)
    floor = np.asarray([0.52, 0.57, 0.54], dtype=np.float64)
    ceiling = np.asarray([0.48, 0.53, 0.57], dtype=np.float64)
    object_color = np.asarray([0.70, 0.64, 0.52], dtype=np.float64)

    colors = np.tile(wall, (len(vertices), 1))
    up = normals[:, 1] > 0.55
    down = normals[:, 1] < -0.55
    middle = (height_t > 0.18) & (height_t < 0.78) & (np.abs(normals[:, 1]) < 0.48)
    colors[up] = floor
    colors[down] = ceiling
    colors[middle] = wall

    # Slightly warm non-structural fragments so furniture/clutter remain visible
    # without photo texture tearing across the map.
    non_structural = ~up & ~down & ~middle
    colors[non_structural] = object_color

    shade = 0.88 + 0.14 * height_t[:, None]
    mesh.vertex_colors = o3d.utility.Vector3dVector(np.clip(colors * shade, 0.0, 1.0))


def _image_resampling_nearest() -> int:
    from PIL import Image

    return getattr(getattr(Image, "Resampling", Image), "NEAREST")


def _image_resampling_lanczos() -> int:
    from PIL import Image

    return getattr(getattr(Image, "Resampling", Image), "LANCZOS")


def _frame_timestamp(frame: dict[str, Any], fallback_index: int) -> float:
    timestamp = frame.get("timestampSeconds")
    try:
        return float(timestamp)
    except (TypeError, ValueError):
        return float(fallback_index) / 30.0


def read_raw_confidence(
    path: Path,
    width: int,
    height: int,
    row_stride: int,
    pixel_stride: int,
) -> "object | None":
    try:
        import numpy as np
    except ImportError:
        return None

    if not path.exists() or width <= 0 or height <= 0:
        return None

    data = path.read_bytes()
    values = []
    for y in range(height):
        row = y * max(1, row_stride)
        for x in range(width):
            offset = row + x * max(1, pixel_stride)
            values.append(data[offset] if offset < len(data) else 0)
    return np.array(values, dtype=np.uint8).reshape((height, width))


def prepare_rtabmap_dataset_from_capture(
    scan_dir: Path,
    out_dir: Path,
    *,
    width: int = 640,
    height: int = 480,
) -> Path | None:
    """Use an RTAB-Map-first dataset emitted by the Unity scanner."""
    try:
        import numpy as np
        from PIL import Image
    except ImportError:
        print("rtabmap_status=failed reason=missing_python_image_dependencies")
        return None

    source_dir = scan_dir / "rtabmap_dataset" / "freiburg3_memoanchor"
    metadata_path = source_dir / "memoanchor_rtabmap_dataset.json"
    if not metadata_path.exists():
        return None

    try:
        metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        print("rtabmap_status=failed reason=invalid_capture_dataset_json")
        return None

    target_width = int(metadata.get("targetWidth") or width)
    target_height = int(metadata.get("targetHeight") or height)
    target_width = max(160, min(1920, target_width))
    target_height = max(120, min(1440, target_height))

    dataset_dir = out_dir / "rtabmap_dataset" / "freiburg3_memoanchor"
    if dataset_dir.exists():
        shutil.rmtree(dataset_dir)
    rgb_dir = dataset_dir / "rgb_sync"
    depth_dir = dataset_dir / "depth_sync"
    rgb_dir.mkdir(parents=True, exist_ok=True)
    depth_dir.mkdir(parents=True, exist_ok=True)

    rgb_lines = ["# timestamp filename"]
    depth_lines = ["# timestamp filename"]
    associations: list[str] = []
    gt_lines = ["# timestamp tx ty tz qx qy qz qw"]
    exported_frames = 0
    skipped_frames = 0

    for frame_index, frame in enumerate(metadata.get("frames", [])):
        try:
            timestamp = float(frame.get("timestampSeconds", frame_index / 30.0))
            rgb_file = str(frame.get("rgbFile", ""))
            depth_file = str(frame.get("depthRawFile", ""))
            depth_width = int(frame.get("depthWidth", 0))
            depth_height = int(frame.get("depthHeight", 0))
            depth_format = str(frame.get("depthFormat", ""))
            depth_row_stride = int(frame.get("depthRowStride", 0))
            depth_pixel_stride = int(frame.get("depthPixelStride", 0))
        except (TypeError, ValueError):
            skipped_frames += 1
            continue

        rgb_path = source_dir / rgb_file
        depth_path = source_dir / depth_file
        if not rgb_path.exists() or not depth_path.exists():
            skipped_frames += 1
            continue

        depth_values = depth_values_from_plane(
            depth_path,
            depth_width,
            depth_height,
            depth_format,
            depth_row_stride,
            depth_pixel_stride,
        )
        depth_m = np.array(depth_values, dtype=np.float32).reshape((depth_height, depth_width))
        confidence_file = str(frame.get("confidenceRawFile", ""))
        if confidence_file:
            confidence = read_raw_confidence(
                source_dir / confidence_file,
                int(frame.get("confidenceWidth", 0)),
                int(frame.get("confidenceHeight", 0)),
                int(frame.get("confidenceRowStride", 0)),
                int(frame.get("confidencePixelStride", 0)),
            )
            if confidence is not None:
                if confidence.shape != depth_m.shape:
                    confidence = np.asarray(
                        Image.fromarray(confidence).resize((depth_width, depth_height), _image_resampling_nearest()),
                        dtype=np.uint8,
                    )
                depth_m[confidence <= 0] = 0.0

        if not np.isfinite(depth_m).any() or float(np.nanmax(depth_m)) <= 0.0:
            skipped_frames += 1
            continue

        stamp = f"{timestamp:.6f}"
        safe_stamp = stamp.replace(".", "_")
        rgb_name = f"{safe_stamp}.png"
        depth_name = f"{safe_stamp}.png"

        image = Image.open(rgb_path).convert("RGB")
        image = image.resize((target_width, target_height), _image_resampling_lanczos())
        image.save(rgb_dir / rgb_name)

        depth_mm = np.clip(depth_m * 5000.0, 0, 65535).astype(np.uint16)
        depth_image = Image.fromarray(depth_mm, mode="I;16")
        if depth_image.size != (target_width, target_height):
            depth_image = depth_image.resize((target_width, target_height), _image_resampling_nearest())
        depth_image.save(depth_dir / depth_name)

        rgb_rel = f"rgb_sync/{rgb_name}"
        depth_rel = f"depth_sync/{depth_name}"
        rgb_lines.append(f"{stamp} {rgb_rel}")
        depth_lines.append(f"{stamp} {depth_rel}")
        associations.append(f"{stamp} {rgb_rel} {stamp} {depth_rel}")

        position = frame.get("position", {}) or {}
        rotation = frame.get("rotation", {}) or {}
        gt_lines.append(
            f"{stamp} "
            f"{float(position.get('x', 0.0)):.9f} "
            f"{float(position.get('y', 0.0)):.9f} "
            f"{float(position.get('z', 0.0)):.9f} "
            f"{float(rotation.get('x', 0.0)):.9f} "
            f"{float(rotation.get('y', 0.0)):.9f} "
            f"{float(rotation.get('z', 0.0)):.9f} "
            f"{float(rotation.get('w', 1.0)):.9f}"
        )
        exported_frames += 1

    if exported_frames == 0:
        print("rtabmap_status=failed reason=no_capture_rtabmap_frames")
        return None

    (dataset_dir / "rgb.txt").write_text("\n".join(rgb_lines) + "\n", encoding="utf-8")
    (dataset_dir / "depth.txt").write_text("\n".join(depth_lines) + "\n", encoding="utf-8")
    (dataset_dir / "associations.txt").write_text("\n".join(associations) + "\n", encoding="utf-8")
    (dataset_dir / "groundtruth.txt").write_text("\n".join(gt_lines) + "\n", encoding="utf-8")
    shutil.copy2(metadata_path, dataset_dir / "memoanchor_rtabmap_dataset.json")
    calib_path = source_dir / "memoanchor_rtabmap_calib.yaml"
    if calib_path.exists():
        shutil.copy2(calib_path, dataset_dir / "memoanchor_rtabmap_calib.yaml")

    print(f"rtabmap_capture_dataset={source_dir}")
    print(f"rtabmap_dataset={dataset_dir}")
    print(f"rtabmap_frames={exported_frames} skipped={skipped_frames}")
    return dataset_dir


def rtabmap_capture_source_dir(scan_dir: Path) -> Path | None:
    source_dir = scan_dir / "rtabmap_dataset" / "freiburg3_memoanchor"
    metadata_path = source_dir / "memoanchor_rtabmap_dataset.json"
    return source_dir if metadata_path.exists() else None


def load_rtabmap_capture_metadata(scan_dir: Path) -> tuple[Path, dict[str, Any]] | None:
    source_dir = rtabmap_capture_source_dir(scan_dir)
    if source_dir is None:
        return None

    try:
        metadata = json.loads((source_dir / "memoanchor_rtabmap_dataset.json").read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        print("rtabmap_pose_fusion=failed reason=invalid_capture_dataset_json")
        return None

    return source_dir, metadata


def read_rtabmap_capture_depth(source_dir: Path, frame: dict[str, Any]) -> "object | None":
    try:
        import numpy as np
        from PIL import Image
    except ImportError:
        return None

    try:
        depth_file = str(frame.get("depthRawFile", ""))
        depth_width = int(frame.get("depthWidth", 0))
        depth_height = int(frame.get("depthHeight", 0))
        depth_format = str(frame.get("depthFormat", ""))
        depth_row_stride = int(frame.get("depthRowStride", 0))
        depth_pixel_stride = int(frame.get("depthPixelStride", 0))
    except (TypeError, ValueError):
        return None

    if depth_width <= 0 or depth_height <= 0:
        return None

    depth_path = source_dir / depth_file
    if not depth_path.exists():
        return None

    values = depth_values_from_plane(
        depth_path,
        depth_width,
        depth_height,
        depth_format,
        depth_row_stride,
        depth_pixel_stride,
    )
    depth_m = np.asarray(values, dtype=np.float32).reshape((depth_height, depth_width))

    confidence_file = str(frame.get("confidenceRawFile", ""))
    if confidence_file:
        confidence = read_raw_confidence(
            source_dir / confidence_file,
            int(frame.get("confidenceWidth", 0)),
            int(frame.get("confidenceHeight", 0)),
            int(frame.get("confidenceRowStride", 0)),
            int(frame.get("confidencePixelStride", 0)),
        )
        if confidence is not None:
            if confidence.shape != depth_m.shape:
                confidence = np.asarray(
                    Image.fromarray(confidence).resize((depth_width, depth_height), _image_resampling_nearest()),
                    dtype=np.uint8,
                )
            depth_m[confidence <= 0] = 0.0

    depth_m[(depth_m < 0.05) | (depth_m > 8.0) | ~np.isfinite(depth_m)] = 0.0
    if not np.isfinite(depth_m).any() or float(np.nanmax(depth_m)) <= 0.0:
        return None

    return depth_m


def rtabmap_capture_color_for_depth(source_dir: Path, frame: dict[str, Any], width: int, height: int) -> "object | None":
    try:
        import numpy as np
        import open3d as o3d
        from PIL import Image
    except ImportError:
        return None

    rgb_file = str(frame.get("rgbFile", ""))
    if not rgb_file:
        return None

    rgb_path = source_dir / rgb_file
    if not rgb_path.exists():
        return None

    image = Image.open(rgb_path).convert("RGB")
    if image.size != (width, height):
        image = image.resize((width, height), _image_resampling_lanczos())
    return o3d.geometry.Image(np.asarray(image, dtype=np.uint8))


def run_rtabmap_pose_fusion(scan_dir: Path, out_dir: Path, manifest: dict[str, Any]) -> Path | None:
    capture = load_rtabmap_capture_metadata(scan_dir)
    if capture is None:
        return None

    try:
        import numpy as np
        import open3d as o3d
    except ImportError:
        print("rtabmap_pose_fusion=failed reason=missing_open3d")
        return None

    source_dir, metadata = capture
    frames = metadata.get("frames", [])
    if not isinstance(frames, list) or not frames:
        print("rtabmap_pose_fusion=failed reason=no_capture_frames")
        return None

    volume = o3d.pipelines.integration.ScalableTSDFVolume(
        voxel_length=0.022,
        sdf_trunc=0.075,
        color_type=o3d.pipelines.integration.TSDFVolumeColorType.RGB8,
    )

    integrated = 0
    skipped = 0
    for frame in frames:
        if not isinstance(frame, dict) or not frame.get("hasIntrinsics"):
            skipped += 1
            continue

        depth_m = read_rtabmap_capture_depth(source_dir, frame)
        if depth_m is None:
            skipped += 1
            continue

        height, width = depth_m.shape
        color = rtabmap_capture_color_for_depth(source_dir, frame, width, height)
        if color is None:
            skipped += 1
            continue

        image_resolution = frame.get("imageResolution", {}) or {}
        focal_length = frame.get("focalLength", {}) or {}
        principal_point = frame.get("principalPoint", {}) or {}
        rgb_width = float(image_resolution.get("x", frame.get("rgbWidth", width)) or width)
        rgb_height = float(image_resolution.get("y", frame.get("rgbHeight", height)) or height)
        scale_x = width / max(1.0, rgb_width)
        scale_y = height / max(1.0, rgb_height)

        intrinsic = o3d.camera.PinholeCameraIntrinsic(
            int(width),
            int(height),
            float(focal_length.get("x", 0.0)) * scale_x,
            float(focal_length.get("y", 0.0)) * scale_y,
            float(principal_point.get("x", width / 2)) * scale_x,
            float(principal_point.get("y", height / 2)) * scale_y,
        )
        depth_mm = np.clip(depth_m * 1000.0, 0, 65535).astype(np.uint16)
        rgbd = o3d.geometry.RGBDImage.create_from_color_and_depth(
            color,
            o3d.geometry.Image(depth_mm),
            depth_scale=1000.0,
            depth_trunc=8.0,
            convert_rgb_to_intensity=False,
        )
        volume.integrate(rgbd, intrinsic, make_extrinsic(frame))
        integrated += 1

    if integrated == 0:
        print(f"rtabmap_pose_fusion=failed source_frames={len(frames)} integrated=0 skipped={skipped}")
        return None

    mesh = volume.extract_triangle_mesh()
    mesh = cleanup_mesh(mesh)
    mesh.compute_vertex_normals()
    mesh = merge_with_local_hole_fill(mesh)

    profile = pruning_profile("safe")
    mesh = prune_reconstruction_mesh(mesh, manifest, stage="rtabmap_pose", profile=profile)
    mesh = limit_mesh_complexity(mesh, target_triangles=int(profile["target_triangles"]))
    mesh = remove_small_triangle_components(
        mesh,
        min_fraction_of_largest=0.004,
        min_triangles=180,
        label="rtabmap_pose_components",
    )
    clamp_vertex_colors(mesh)

    if len(mesh.vertices) == 0 or len(mesh.triangles) == 0:
        print(f"rtabmap_pose_fusion=failed source_frames={len(frames)} integrated={integrated} skipped={skipped} reason=empty_mesh")
        return None

    result_path = out_dir / "result.ply"
    if not o3d.io.write_triangle_mesh(str(result_path), mesh, write_ascii=False, compressed=False):
        print("rtabmap_pose_fusion=failed reason=write_result")
        return None

    print(f"rtabmap_capture_dataset={source_dir}")
    print(f"rtabmap_pose_fusion=done source_frames={len(frames)} integrated={integrated} skipped={skipped} result={result_path.name}")
    print(f"rtabmap_status=pose_fusion result={result_path.name}")
    print(f"chosen_result={result_path.name}")
    return result_path


def prepare_rtabmap_rgbd_dataset(
    scan_dir: Path,
    manifest: dict[str, Any],
    out_dir: Path,
    *,
    width: int = 640,
    height: int = 480,
) -> Path | None:
    """Write a TUM-like RGB-D folder that RTAB-Map CLI tools can ingest.

    The current MemoAnchor capture already has ARKit poses/intrinsics, but the
    stock rtabmap-rgbd_dataset tool only has built-in calibration presets for
    known benchmark folder names. We name the folder like freiburg3 to get a
    runnable first-pass comparison, then preserve the real ARKit metadata next
    to the converted images for later native integration work.
    """
    try:
        import numpy as np
        from PIL import Image
    except ImportError:
        print("rtabmap_status=failed reason=missing_python_image_dependencies")
        return None

    capture_dataset = prepare_rtabmap_dataset_from_capture(scan_dir, out_dir, width=width, height=height)
    if capture_dataset is not None:
        return capture_dataset

    dataset_dir = out_dir / "rtabmap_dataset" / "freiburg3_memoanchor"
    if dataset_dir.exists():
        shutil.rmtree(dataset_dir)
    rgb_dir = dataset_dir / "rgb_sync"
    depth_dir = dataset_dir / "depth_sync"
    rgb_dir.mkdir(parents=True, exist_ok=True)
    depth_dir.mkdir(parents=True, exist_ok=True)

    associations: list[str] = []
    rgb_lines = ["# timestamp filename"]
    depth_lines = ["# timestamp filename"]
    gt_lines = ["# timestamp tx ty tz qx qy qz qw"]
    exported_frames = 0
    skipped_frames = 0
    first_intrinsics: dict[str, Any] | None = None

    frames = manifest.get("frames", [])
    for frame_index, frame in enumerate(frames):
        rgb_file = frame.get("rgbFile")
        folder = frame.get("folder")
        if not rgb_file or not folder:
            skipped_frames += 1
            continue

        rgb_path = scan_dir / "frames" / folder / rgb_file
        if not rgb_path.exists():
            skipped_frames += 1
            continue

        depth_m = read_depth_array_if_possible(scan_dir, frame, apply_confidence=True)
        if depth_m is None:
            skipped_frames += 1
            continue

        timestamp = _frame_timestamp(frame, frame_index)
        stamp = f"{timestamp:.6f}"
        safe_stamp = stamp.replace(".", "_")
        rgb_name = f"{safe_stamp}.png"
        depth_name = f"{safe_stamp}.png"

        image = Image.open(rgb_path).convert("RGB")
        image = image.resize((width, height), _image_resampling_lanczos())
        image.save(rgb_dir / rgb_name)

        depth_mm = np.clip(depth_m * 5000.0, 0, 65535).astype(np.uint16)
        depth_image = Image.fromarray(depth_mm, mode="I;16")
        if depth_image.size != (width, height):
            depth_image = depth_image.resize((width, height), _image_resampling_nearest())
        depth_image.save(depth_dir / depth_name)

        rgb_rel = f"rgb_sync/{rgb_name}"
        depth_rel = f"depth_sync/{depth_name}"
        rgb_lines.append(f"{stamp} {rgb_rel}")
        depth_lines.append(f"{stamp} {depth_rel}")
        associations.append(f"{stamp} {rgb_rel} {stamp} {depth_rel}")

        position = frame.get("position", {})
        rotation = frame.get("rotation", {})
        gt_lines.append(
            f"{stamp} "
            f"{float(position.get('x', 0.0)):.9f} "
            f"{float(position.get('y', 0.0)):.9f} "
            f"{float(position.get('z', 0.0)):.9f} "
            f"{float(rotation.get('x', 0.0)):.9f} "
            f"{float(rotation.get('y', 0.0)):.9f} "
            f"{float(rotation.get('z', 0.0)):.9f} "
            f"{float(rotation.get('w', 1.0)):.9f}"
        )

        if first_intrinsics is None and frame.get("hasIntrinsics"):
            first_intrinsics = {
                "sourceResolution": frame.get("imageResolution"),
                "sourceFocalLength": frame.get("focalLength"),
                "sourcePrincipalPoint": frame.get("principalPoint"),
                "rtabmapDatasetResolution": {"x": width, "y": height},
            }

        exported_frames += 1

    if exported_frames == 0:
        print("rtabmap_status=failed reason=no_rgbd_frames")
        return None

    (dataset_dir / "rgb.txt").write_text("\n".join(rgb_lines) + "\n", encoding="utf-8")
    (dataset_dir / "depth.txt").write_text("\n".join(depth_lines) + "\n", encoding="utf-8")
    (dataset_dir / "associations.txt").write_text("\n".join(associations) + "\n", encoding="utf-8")
    (dataset_dir / "groundtruth.txt").write_text("\n".join(gt_lines) + "\n", encoding="utf-8")
    (dataset_dir / "memoanchor_rtabmap_dataset.json").write_text(
        json.dumps(
            {
                "schemaVersion": "memoanchor.rtabmap-rgbd-dataset.v1",
                "sourceScanId": manifest.get("scanId"),
                "exportedFrames": exported_frames,
                "skippedFrames": skipped_frames,
                "depthScale": "TUM-compatible uint16 PNG where depth_meters = raw / 5000",
                "poseSource": "ARKit camera pose exported as groundtruth.txt for inspection",
                "calibrationWarning": (
                    "The stock rtabmap-rgbd_dataset CLI uses a freiburg3 calibration preset "
                    "for this folder name. This is a comparison harness, not final native integration."
                ),
                "intrinsics": first_intrinsics,
            },
            indent=2,
        ),
        encoding="utf-8",
    )

    print(f"rtabmap_dataset={dataset_dir}")
    print(f"rtabmap_frames={exported_frames} skipped={skipped_frames}")
    return dataset_dir


def find_rtabmap_result(out_dir: Path) -> Path | None:
    preferred = [
        out_dir / "result.obj",
        out_dir / "result.ply",
        out_dir / "rtabmap" / "result.obj",
        out_dir / "rtabmap" / "result.ply",
        out_dir / "rtabmap" / "rtabmap.obj",
        out_dir / "rtabmap" / "rtabmap.ply",
    ]
    for path in preferred:
        if path.exists():
            return path

    candidates = sorted(
        [path for path in (out_dir / "rtabmap").glob("*") if path.suffix.lower() in (".obj", ".ply")],
        key=lambda path: path.stat().st_size,
        reverse=True,
    )
    return candidates[0] if candidates else None


def copy_rtabmap_result_to_standard_names(result: Path, out_dir: Path) -> Path:
    if result.suffix.lower() == ".obj":
        target = out_dir / "result.obj"
        if result.resolve() != target.resolve():
            shutil.copy2(result, target)

        source_mtl = result.with_suffix(".mtl")
        if source_mtl.exists():
            shutil.copy2(source_mtl, out_dir / "result.mtl")
            for raw_line in source_mtl.read_text(encoding="utf-8", errors="replace").splitlines():
                parts = raw_line.strip().split(maxsplit=1)
                if len(parts) == 2 and parts[0].lower().startswith("map_"):
                    texture_path = source_mtl.parent / parts[1]
                    if texture_path.exists():
                        shutil.copy2(texture_path, out_dir / texture_path.name)

        obj_text = target.read_text(encoding="utf-8", errors="replace")
        obj_text = "\n".join(
            "mtllib result.mtl" if line.lower().startswith("mtllib ") else line
            for line in obj_text.splitlines()
        )
        target.write_text(obj_text + "\n", encoding="utf-8")

        return target

    target = out_dir / "result.ply"
    if result.resolve() != target.resolve():
        shutil.copy2(result, target)
    return target


def parse_rtabmap_odometry_stats(output: str) -> dict[str, int]:
    stats: dict[str, int] = {}
    processing = re.search(r"Processing\s+(\d+)\s+images", output)
    if processing:
        stats["frames"] = int(processing.group(1))

    iteration = re.search(r"Iteration\s+\d+/\d+:.*?kfs=(\d+)", output)
    if iteration:
        stats["keyframes"] = int(iteration.group(1))

    ignored = re.findall(r"Image\s+\d+\s+is ignored", output)
    if ignored:
        stats["ignored"] = len(ignored)

    return stats


def run_rtabmap_cli_reconstruction(scan_dir: Path, out_dir: Path, manifest: dict[str, Any]) -> Path | None:
    scan_dir = scan_dir.resolve()
    out_dir = out_dir.resolve()
    dataset_dir = prepare_rtabmap_rgbd_dataset(scan_dir, manifest, out_dir)
    if dataset_dir is None:
        return None
    dataset_dir = dataset_dir.resolve()

    rgbd_tool = find_executable(["rtabmap-rgbd_dataset"])
    export_tool = find_executable(["rtabmap-export"])
    if not rgbd_tool or not export_tool:
        missing = []
        if not rgbd_tool:
            missing.append("rtabmap-rgbd_dataset")
        if not export_tool:
            missing.append("rtabmap-export")
        print(f"rtabmap_status=missing_tool tools={','.join(missing)} dataset={dataset_dir}")
        print("rtabmap_install_hint=brew install rtabmap")
        return None

    rtabmap_dir = out_dir / "rtabmap"
    if rtabmap_dir.exists():
        shutil.rmtree(rtabmap_dir)
    rtabmap_dir.mkdir(parents=True, exist_ok=True)
    rtabmap_dir = rtabmap_dir.resolve()
    db_path = (rtabmap_dir / "memoanchor_rtabmap.db").resolve()

    dataset_command = [
        rgbd_tool,
        "--quiet",
        "--output",
        str(rtabmap_dir),
        "--output_name",
        "memoanchor_rtabmap",
        "--Rtabmap/DetectionRate",
        "0",
        "--RGBD/LinearUpdate",
        "0",
        "--RGBD/AngularUpdate",
        "0",
        str(dataset_dir),
    ]
    dataset_run = subprocess.run(
        dataset_command,
        cwd=str(out_dir),
        capture_output=True,
        text=True,
        timeout=60 * 20,
        check=False,
    )
    (out_dir / "rtabmap_dataset_stdout.txt").write_text(dataset_run.stdout, encoding="utf-8")
    (out_dir / "rtabmap_dataset_stderr.txt").write_text(dataset_run.stderr, encoding="utf-8")
    print(f"rtabmap_dataset_exit={dataset_run.returncode}")
    if dataset_run.returncode != 0 or not db_path.exists():
        print(f"rtabmap_status=failed stage=dataset db={db_path}")
        return None

    odometry_stats = parse_rtabmap_odometry_stats(dataset_run.stdout + "\n" + dataset_run.stderr)
    if odometry_stats:
        print(
            "rtabmap_odometry="
            f"frames={odometry_stats.get('frames', 0)} "
            f"keyframes={odometry_stats.get('keyframes', 0)} "
            f"ignored={odometry_stats.get('ignored', 0)}"
        )
    total_frames = odometry_stats.get("frames", 0)
    keyframes = odometry_stats.get("keyframes", 0)
    ignored = odometry_stats.get("ignored", 0)
    if total_frames > 0 and (ignored > total_frames * 0.25 or keyframes < max(4, total_frames // 2)):
        print(f"rtabmap_status=failed stage=odometry db={db_path}")
        return None

    export_command = [
        export_tool,
        "--mesh",
        "--texture",
        "--texture_size",
        "4096",
        "--texture_range",
        "4",
        "--max_polygons",
        "300000",
        "--output",
        "result",
        "--output_dir",
        str(rtabmap_dir),
        str(db_path),
    ]
    export_run = subprocess.run(
        export_command,
        cwd=str(out_dir),
        capture_output=True,
        text=True,
        timeout=60 * 20,
        check=False,
    )
    (out_dir / "rtabmap_export_stdout.txt").write_text(export_run.stdout, encoding="utf-8")
    (out_dir / "rtabmap_export_stderr.txt").write_text(export_run.stderr, encoding="utf-8")
    print(f"rtabmap_export_exit={export_run.returncode}")
    if export_run.returncode != 0:
        print("rtabmap_status=failed stage=export")
        return None

    result = find_rtabmap_result(out_dir)
    if result is None:
        print("rtabmap_status=failed stage=result_discovery")
        return None

    standardized = copy_rtabmap_result_to_standard_names(result, out_dir)
    print(f"rtabmap_status=done result={standardized.name} db={db_path}")
    print(f"chosen_result={standardized.name}")
    return standardized


def run_rtabmap_reconstruction(scan_dir: Path, out_dir: Path, manifest: dict[str, Any]) -> Path | None:
    capture = load_rtabmap_capture_metadata(scan_dir.resolve())
    if capture is not None:
        source_dir, metadata = capture
        profile = pruning_profile("clean_texture")
        stable_result = colorized_raw_mesh(scan_dir.resolve(), out_dir.resolve(), manifest, profile=profile)
        if stable_result is not None:
            frame_count = len(metadata.get("frames", [])) if isinstance(metadata.get("frames", []), list) else 0
            print(f"rtabmap_capture_dataset={source_dir}")
            print(f"rtabmap_frames={frame_count} skipped=0")
            print(f"rtabmap_status=stable_texture_map result={stable_result.name}")
            print(f"chosen_result={stable_result.name}")
            return stable_result

        pose_result = run_rtabmap_pose_fusion(scan_dir.resolve(), out_dir.resolve(), manifest)
        if pose_result is not None:
            return pose_result

    return run_rtabmap_cli_reconstruction(scan_dir, out_dir, manifest)


def choose_result(scan_dir: Path, out_dir: Path, manifest: dict[str, Any], *, profile_name: str = "balanced") -> Path | None:
    print(f"pruning_profile={profile_name}")
    if profile_name == "rtabmap":
        return run_rtabmap_reconstruction(scan_dir, out_dir, manifest)

    profile = pruning_profile(profile_name)
    if profile_name == "geometry":
        return geometry_raw_mesh(scan_dir, out_dir, manifest, profile=profile)

    raw_result = colorized_raw_mesh(scan_dir, out_dir, manifest, profile=profile)

    # For MemoAnchor the first goal is a recognizable surface that can receive notes.
    # ARKit's mesh usually preserves room layout better than low-resolution TSDF, so TSDF is a fallback.
    chosen = raw_result
    if chosen is None:
        chosen = try_open3d_tsdf(scan_dir, out_dir, manifest, profile=profile)
    if chosen is None:
        return None

    if chosen.suffix.lower() == ".obj":
        raw_colored = out_dir / "result_raw_colored.ply"
        legacy_ply = out_dir / "result.ply"
        if raw_colored.exists() and raw_colored != legacy_ply:
            shutil.copy2(raw_colored, legacy_ply)
        print(f"chosen_result={chosen.name}")
        return chosen

    final = out_dir / "result.ply"
    if chosen != final:
        shutil.copy2(chosen, final)
    print(f"chosen_result={chosen.name}")
    return final


def fallback_raw_mesh(scan_dir: Path, out_dir: Path) -> Path | None:
    raw_mesh = find_raw_mesh(scan_dir)
    if raw_mesh is None:
        return None

    result = out_dir / "result.obj"
    shutil.copy2(raw_mesh, result)
    return result


def project_vertices_to_keyframe(
    vertices: "object",
    keyframe: dict[str, Any],
    *,
    use_depth_check: bool = True,
    require_depth: bool = False,
    depth_abs_tolerance: float = 0.18,
    depth_rel_tolerance: float = 0.10,
    margin_ratio: float = 0.0,
) -> tuple["object", "object", "object", "object"]:
    """Project world-space mesh vertices into one RGB keyframe."""
    import numpy as np

    rel = vertices - keyframe["position"]
    with np.errstate(invalid="ignore", over="ignore", divide="ignore"):
        camera_local = rel @ keyframe["rotation"]
        z = camera_local[:, 2]
        pixel_x = keyframe["fx"] * (camera_local[:, 0] / z) + keyframe["cx"]
        pixel_y = keyframe["cy"] - keyframe["fy"] * (camera_local[:, 1] / z)

    valid = np.isfinite(camera_local).all(axis=1) & np.isfinite(pixel_x) & np.isfinite(pixel_y)
    valid &= z > 0.05
    margin_x = max(2.0, keyframe["width"] * max(0.0, margin_ratio))
    margin_y = max(2.0, keyframe["height"] * max(0.0, margin_ratio))
    valid &= pixel_x >= margin_x
    valid &= pixel_x < keyframe["width"] - margin_x
    valid &= pixel_y >= margin_y
    valid &= pixel_y < keyframe["height"] - margin_y

    depth = keyframe.get("depth") if use_depth_check else None
    if depth is not None and np.any(valid):
        depth_h, depth_w = depth.shape
        valid_indices = np.flatnonzero(valid)
        depth_x = np.clip((pixel_x[valid_indices] / keyframe["width"] * depth_w).astype(np.int32), 0, depth_w - 1)
        depth_y = np.clip((pixel_y[valid_indices] / keyframe["height"] * depth_h).astype(np.int32), 0, depth_h - 1)
        sampled_depth = depth[depth_y, depth_x]
        depth_diff = np.abs(sampled_depth - z[valid_indices])
        tolerance = np.maximum(depth_abs_tolerance, z[valid_indices] * depth_rel_tolerance)
        depth_valid = depth_diff <= tolerance
        if not require_depth:
            depth_valid |= sampled_depth <= 0

        refined = np.zeros_like(valid)
        refined[valid_indices] = depth_valid
        valid &= refined
    elif require_depth:
        valid &= False

    return pixel_x, pixel_y, z, valid


def choose_texture_keyframes_for_triangles(
    vertices: "object",
    triangles: "object",
    triangle_normals: "object",
    keyframes: list[dict[str, Any]],
    *,
    use_depth_check: bool = True,
    require_depth: bool = False,
    depth_abs_tolerance: float = 0.18,
    depth_rel_tolerance: float = 0.10,
    margin_ratio: float = 0.0,
    min_projected_area: float = 0.35,
    min_facing_score: float = 0.0,
) -> "object":
    """Assign each triangle to the keyframe that should provide the best texture sample."""
    import numpy as np

    triangle_count = len(triangles)
    assigned = np.full((triangle_count,), -1, dtype=np.int32)
    best_scores = np.full((triangle_count,), -1e18, dtype=np.float64)
    triangle_centers = vertices[triangles].mean(axis=1)

    for keyframe_index, keyframe in enumerate(keyframes):
        pixel_x, pixel_y, z, valid_vertices = project_vertices_to_keyframe(
            vertices,
            keyframe,
            use_depth_check=use_depth_check,
            require_depth=require_depth,
            depth_abs_tolerance=depth_abs_tolerance,
            depth_rel_tolerance=depth_rel_tolerance,
            margin_ratio=margin_ratio,
        )

        tri_valid = valid_vertices[triangles].all(axis=1)
        if not np.any(tri_valid):
            continue

        tri_px = pixel_x[triangles]
        tri_py = pixel_y[triangles]
        projected_area = 0.5 * np.abs(
            tri_px[:, 0] * (tri_py[:, 1] - tri_py[:, 2])
            + tri_px[:, 1] * (tri_py[:, 2] - tri_py[:, 0])
            + tri_px[:, 2] * (tri_py[:, 0] - tri_py[:, 1])
        )
        tri_valid &= np.isfinite(projected_area) & (projected_area > min_projected_area)
        if not np.any(tri_valid):
            continue

        mean_z = np.maximum(z[triangles].mean(axis=1), 1e-6)
        u_center = tri_px.mean(axis=1) / max(1, keyframe["width"])
        v_center = tri_py.mean(axis=1) / max(1, keyframe["height"])
        center_distance = np.sqrt((u_center - 0.5) ** 2 + (v_center - 0.5) ** 2)
        center_score = 1.0 - np.clip(center_distance / 0.7, 0.0, 1.0)
        distance_score = 1.0 / (0.25 + mean_z)

        view = keyframe["position"] - triangle_centers
        view_norm = np.maximum(np.linalg.norm(view, axis=1), 1e-6)
        view_dir = view / view_norm[:, None]
        facing_score = np.clip(np.abs(np.sum(triangle_normals * view_dir, axis=1)), 0.0, 1.0)
        tri_valid &= facing_score >= min_facing_score
        if not np.any(tri_valid):
            continue

        score = np.log1p(projected_area) * 2.0 + center_score * 2.0 + distance_score * 0.6 + facing_score * 0.5
        update = tri_valid & (score > best_scores)
        assigned[update] = keyframe_index
        best_scores[update] = score[update]

    return assigned


def dilate_texture_padding(texture: "object", mask: "object", *, iterations: int = 24) -> tuple["object", "object"]:
    import cv2
    import numpy as np

    kernel = np.ones((3, 3), dtype=np.uint8)
    for _ in range(iterations):
        grown_mask = cv2.dilate(mask, kernel, iterations=1)
        fill = (mask == 0) & (grown_mask > 0)
        if not np.any(fill):
            break
        grown_texture = cv2.dilate(texture, kernel, iterations=1)
        texture[fill] = grown_texture[fill]
        mask[fill] = 255

    return texture, mask


def write_textured_obj(
    obj_path: Path,
    mtl_path: Path,
    texture_name: str,
    vertices: "object",
    triangles: "object",
    face_uvs: "object",
) -> None:
    with open(mtl_path, "w", encoding="utf-8") as f:
        f.write("newmtl baked_material\n")
        f.write("Ka 1.000 1.000 1.000\n")
        f.write("Kd 1.000 1.000 1.000\n")
        f.write("Ks 0.000 0.000 0.000\n")
        f.write("d 1.0\n")
        f.write("illum 2\n")
        f.write(f"map_Kd {texture_name}\n")

    with open(obj_path, "w", encoding="utf-8") as f:
        f.write(f"mtllib {mtl_path.name}\n")
        f.write("usemtl baked_material\n")
        f.write("# MemoAnchor RGB-D keyframe texture atlas\n")

        for vertex in vertices:
            f.write(f"v {vertex[0]:.9f} {vertex[1]:.9f} {vertex[2]:.9f}\n")

        for uv in face_uvs.reshape((-1, 2)):
            f.write(f"vt {uv[0]:.9f} {uv[1]:.9f}\n")

        for face_index, tri in enumerate(triangles):
            vertex_indices = tri + 1
            texcoord_base = face_index * 3 + 1
            f.write(
                "f "
                f"{vertex_indices[0]}/{texcoord_base} "
                f"{vertex_indices[1]}/{texcoord_base + 1} "
                f"{vertex_indices[2]}/{texcoord_base + 2}\n"
            )


def find_alicevision_texturing_executable() -> str | None:
    import shutil

    candidates = [
        "aliceVision_texturing",
        "alicevision_texturing",
        "aliceVision_texturing_bin",
        "alicevision-texturing",
    ]
    for candidate in candidates:
        resolved = shutil.which(candidate)
        if resolved:
            return resolved

    for base in [
        "/opt/homebrew/bin",
        "/usr/local/bin",
        "/usr/bin",
        str(Path.home() / ".local" / "bin"),
        "/Applications/Meshroom.app/Contents/MacOS",
        "/Applications/AliceVision.app/Contents/MacOS",
    ]:
        for candidate in candidates:
            path = Path(base) / candidate
            if path.exists() and os.access(path, os.X_OK):
                return str(path)

    return None


def _probe_alicevision_help(executable: str) -> str:
    try:
        completed = subprocess.run(
            [executable, "--help"],
            capture_output=True,
            text=True,
            timeout=20,
            check=False,
        )
        return (completed.stdout or "") + "\n" + (completed.stderr or "")
    except (FileNotFoundError, subprocess.SubprocessError, OSError):
        return ""


def validate_camera_projection(
    mesh: "object",
    scan_dir: Path,
    manifest: dict[str, Any],
    out_dir: Path,
    *,
    max_frames: int = 8,
) -> bool:
    try:
        import cv2
        import numpy as np
        from PIL import Image
    except ImportError:
        print("AliceVision projection validation skipped: cv2/PIL/numpy not installed")
        return False

    if mesh is None or len(mesh.vertices) == 0 or len(mesh.triangles) == 0:
        return False

    vertices = np.asarray(mesh.vertices, dtype=np.float64)
    triangles = np.asarray(mesh.triangles, dtype=np.int32)
    keyframes = load_color_keyframes(scan_dir, manifest)
    if not keyframes:
        return False

    debug_dir = out_dir / "debug_projection"
    debug_dir.mkdir(parents=True, exist_ok=True)

    visible_frames = 0
    for frame_idx, keyframe in enumerate(keyframes[:max_frames]):
        pixel_x, pixel_y, _, valid = project_vertices_to_keyframe(
            vertices,
            keyframe,
            use_depth_check=False,
            margin_ratio=0.0,
        )
        valid_count = int(np.count_nonzero(valid))
        if valid_count <= max(10, len(vertices) // 200):
            continue

        image = np.clip(keyframe["image"] * 255.0, 0, 255).astype(np.uint8)
        overlay = np.array(Image.fromarray(image, mode="RGB"), dtype=np.uint8)
        for vertex_index in np.flatnonzero(valid):
            x = int(round(float(pixel_x[vertex_index])))
            y = int(round(float(pixel_y[vertex_index])))
            cv2.circle(overlay, (x, y), 2, (0, 255, 0), -1)

        for tri in triangles:
            pts = np.column_stack((pixel_x[tri], pixel_y[tri])).astype(np.int32)
            if not np.isfinite(pts).all():
                continue
            if np.any(pts[:, 0] < 0) or np.any(pts[:, 0] >= keyframe["width"]):
                continue
            if np.any(pts[:, 1] < 0) or np.any(pts[:, 1] >= keyframe["height"]):
                continue
            cv2.polylines(overlay, [pts], True, (255, 0, 0), 1)

        output_path = debug_dir / f"frame_{frame_idx:03d}_overlay.png"
        Image.fromarray(overlay, mode="RGB").save(output_path)
        visible_frames += 1

    return visible_frames > 0


def prepare_alicevision_input(
    mesh: "object",
    scan_dir: Path,
    manifest: dict[str, Any],
    out_dir: Path,
    *,
    mesh_path: Path | None = None,
) -> dict[str, Any] | None:
    import numpy as np
    import open3d as o3d
    from PIL import Image

    if mesh is None or len(mesh.vertices) == 0 or len(mesh.triangles) == 0:
        return None

    if not validate_camera_projection(mesh, scan_dir, manifest, out_dir):
        print("AliceVision input preparation skipped: camera projection validation failed")
        return None

    work_dir = out_dir / "alicevision_input"
    work_dir.mkdir(parents=True, exist_ok=True)
    image_dir = work_dir / "images"
    image_dir.mkdir(parents=True, exist_ok=True)

    mesh_output_path = mesh_path or (work_dir / "mesh.obj")
    if not o3d.io.write_triangle_mesh(str(mesh_output_path), mesh):
        return None

    views: list[dict[str, Any]] = []
    intrinsics: list[dict[str, Any]] = []
    poses: list[dict[str, Any]] = []

    frame_candidates = [frame for frame in manifest.get("frames", []) if frame.get("hasIntrinsics") and frame.get("rgbFile")]
    for index, frame in enumerate(frame_candidates):
        rgb_file = frame.get("rgbFile")
        if not rgb_file:
            continue

        rgb_path = scan_dir / "frames" / frame.get("folder", "") / rgb_file
        if not rgb_path.exists():
            continue

        image = Image.open(rgb_path).convert("RGB")
        width, height = image.size
        image_name = f"{index:04d}{rgb_path.suffix}"
        shutil.copy2(rgb_path, image_dir / image_name)

        focal_length = frame.get("focalLength", {})
        principal_point = frame.get("principalPoint", {})
        fx = float(focal_length.get("x", width))
        fy = float(focal_length.get("y", height))
        cx = float(principal_point.get("x", width / 2))
        cy = float(principal_point.get("y", height / 2))

        intrinsics.append(
            {
                "intrinsicId": index,
                "type": "pinhole",
                "width": int(width),
                "height": int(height),
                "initialFocalLength": max(fx, fy),
                "focalLength": [fx, fy],
                "principalPoint": [cx, cy],
                "distortionParams": [0.0, 0.0, 0.0, 0.0, 0.0],
            }
        )

        world_to_camera = make_extrinsic(frame)
        rotation = np.array(world_to_camera[:3, :3], dtype=np.float64)
        translation = np.array(world_to_camera[:3, 3], dtype=np.float64)
        center = -(rotation.T @ translation)

        poses.append(
            {
                "poseId": index,
                "transform": {
                    "rotation": rotation.tolist(),
                    "center": center.tolist(),
                },
            }
        )

        views.append(
            {
                "viewId": index,
                "poseId": index,
                "intrinsicId": index,
                "path": str((Path("images") / image_name).as_posix()),
            }
        )

    if not views:
        return None

    sfm_data_path = work_dir / "sfmData.json"
    sfm_data = {
        "version": [1, 0, 0],
        "views": views,
        "intrinsics": intrinsics,
        "poses": poses,
        "featureFolder": "",
        "matchingFolder": "",
    }
    with open(sfm_data_path, "w", encoding="utf-8") as handle:
        json.dump(sfm_data, handle, indent=2)

    return {
        "work_dir": work_dir,
        "mesh_path": mesh_output_path,
        "sfm_data_path": sfm_data_path,
    }


def run_alicevision_texturing(
    mesh: "object",
    scan_dir: Path,
    manifest: dict[str, Any],
    out_dir: Path,
    *,
    mesh_path: Path | None = None,
    timeout_seconds: int = 1800,
) -> Path | None:
    executable = find_alicevision_texturing_executable()
    if not executable:
        print("AliceVision texturing skipped: executable not found")
        return None

    prepared = prepare_alicevision_input(mesh, scan_dir, manifest, out_dir, mesh_path=mesh_path)
    if prepared is None:
        return None

    output_dir = out_dir / "alicevision_out"
    output_dir.mkdir(parents=True, exist_ok=True)

    help_text = _probe_alicevision_help(executable)
    command = [executable]

    if "--input" in help_text or "-i" in help_text:
        command.extend(["--input", str(prepared["sfm_data_path"])])
    elif "--sfmData" in help_text:
        command.extend(["--sfmData", str(prepared["sfm_data_path"])])
    else:
        command.extend(["--input", str(prepared["sfm_data_path"])])

    if "--inputMesh" in help_text or "--mesh" in help_text or "--inputMeshPath" in help_text:
        mesh_flag = "--inputMesh"
        if "--mesh" in help_text and "--inputMesh" not in help_text:
            mesh_flag = "--mesh"
        command.extend([mesh_flag, str(prepared["mesh_path"])])
    else:
        command.extend(["--inputMesh", str(prepared["mesh_path"])])

    if "--output" in help_text or "-o" in help_text:
        command.extend(["--output", str(output_dir)])
    elif "--outputFolder" in help_text:
        command.extend(["--outputFolder", str(output_dir)])
    else:
        command.extend(["--output", str(output_dir)])

    if "--textureSide" in help_text:
        command.extend(["--textureSide", "4096"])
    elif "--textureSize" in help_text:
        command.extend(["--textureSize", "4096"])

    if "--textureFileType" in help_text:
        command.extend(["--textureFileType", "png"])

    log_path = out_dir / "alicevision_texturing.log"
    try:
        completed = subprocess.run(
            command,
            cwd=str(out_dir),
            capture_output=True,
            text=True,
            timeout=timeout_seconds,
            check=False,
        )
        with open(log_path, "w", encoding="utf-8") as handle:
            handle.write("$ " + " ".join(command) + "\n")
            handle.write(completed.stdout)
            handle.write(completed.stderr)
    except subprocess.TimeoutExpired as exc:
        with open(log_path, "w", encoding="utf-8") as handle:
            handle.write(f"command timed out after {timeout_seconds}s\n")
            if exc.stdout:
                handle.write(exc.stdout)
            if exc.stderr:
                handle.write(exc.stderr)
        print("AliceVision texturing timed out")
        return None
    except (FileNotFoundError, OSError) as exc:
        with open(log_path, "w", encoding="utf-8") as handle:
            handle.write(str(exc))
        print(f"AliceVision texturing failed to launch: {exc}")
        return None

    if completed.returncode != 0:
        print(f"AliceVision texturing failed with exit code {completed.returncode}")
        return None

    obj_candidates = sorted(output_dir.rglob("*.obj"))
    if not obj_candidates:
        print("AliceVision texturing returned without an OBJ mesh")
        return None

    chosen_obj = obj_candidates[0]
    target_obj = out_dir / "result.obj"
    shutil.copy2(chosen_obj, target_obj)

    mtl_candidates = sorted(output_dir.rglob("*.mtl"))
    if mtl_candidates:
        shutil.copy2(mtl_candidates[0], out_dir / "result.mtl")

    for texture_path in sorted(output_dir.rglob("*.png")):
        if texture_path.name == "result.png":
            shutil.copy2(texture_path, out_dir / texture_path.name)
        elif texture_path.name.startswith("texture") or texture_path.name.startswith("result_texture"):
            shutil.copy2(texture_path, out_dir / texture_path.name)

    print(f"AliceVision texturing succeeded obj={target_obj.name} output_dir={output_dir}")
    return target_obj


def try_alicevision_texturing(
    mesh: "object",
    scan_dir: Path,
    manifest: dict[str, Any],
    out_dir: Path,
    *,
    mesh_path: Path | None = None,
) -> Path | None:
    try:
        return run_alicevision_texturing(mesh, scan_dir, manifest, out_dir, mesh_path=mesh_path)
    except Exception as exc:
        print(f"AliceVision texturing wrapper failed: {exc}")
        return None


def bake_keyframe_texture_atlas(
    mesh: "object",
    scan_dir: Path,
    manifest: dict[str, Any],
    out_dir: Path,
    *,
    texture_size: int = 4096,
    use_depth_check: bool = True,
    require_depth: bool = False,
    depth_abs_tolerance: float = 0.18,
    depth_rel_tolerance: float = 0.10,
    margin_ratio: float = 0.0,
    min_projected_area: float = 0.35,
    min_facing_score: float = 0.0,
    fallback_vertex_colors: bool = True,
) -> Path | None:
    """Bake a texture atlas by projecting mesh triangles into RGB keyframes."""
    try:
        import cv2
        import numpy as np
        from PIL import Image
    except ImportError:
        print("bake_keyframe_texture_atlas skipped: numpy, cv2, or PIL not installed")
        return None

    keyframes = load_color_keyframes(scan_dir, manifest)
    if not keyframes:
        print("bake_keyframe_texture_atlas skipped: no RGB keyframes")
        return None

    vertices = np.asarray(mesh.vertices, dtype=np.float64)
    triangles = np.asarray(mesh.triangles, dtype=np.int32)
    if len(vertices) == 0 or len(triangles) == 0:
        return None

    if not mesh.has_triangle_normals():
        mesh.compute_triangle_normals()
    triangle_normals = np.asarray(mesh.triangle_normals, dtype=np.float64)
    if len(triangle_normals) != len(triangles):
        mesh.compute_triangle_normals()
        triangle_normals = np.asarray(mesh.triangle_normals, dtype=np.float64)

    assigned_keyframes = choose_texture_keyframes_for_triangles(
        vertices,
        triangles,
        triangle_normals,
        keyframes,
        use_depth_check=use_depth_check,
        require_depth=require_depth,
        depth_abs_tolerance=depth_abs_tolerance,
        depth_rel_tolerance=depth_rel_tolerance,
        margin_ratio=margin_ratio,
        min_projected_area=min_projected_area,
        min_facing_score=min_facing_score,
    )
    assigned_count = int(np.count_nonzero(assigned_keyframes >= 0))
    if assigned_count == 0:
        print("bake_keyframe_texture_atlas skipped: no triangles passed projection/depth checks")
        return None

    obj_path = out_dir / "result.obj"
    mtl_path = out_dir / "result.mtl"
    texture_path = out_dir / "result_texture.png"

    triangle_count = len(triangles)
    grid_cols = int(math.ceil(math.sqrt(triangle_count)))
    grid_rows = int(math.ceil(triangle_count / max(1, grid_cols)))
    tile_width = max(3, texture_size // max(1, grid_cols))
    tile_height = max(3, texture_size // max(1, grid_rows))
    tile_min = min(tile_width, tile_height)
    padding = 1 if tile_min < 10 else 2

    texture = np.zeros((texture_size, texture_size, 3), dtype=np.uint8)
    mask = np.zeros((texture_size, texture_size), dtype=np.uint8)
    face_uvs = np.zeros((triangle_count, 3, 2), dtype=np.float32)

    vertex_colors = np.asarray(mesh.vertex_colors, dtype=np.float64) if fallback_vertex_colors and mesh.has_vertex_colors() else None
    default_rgb = np.array([148, 179, 189], dtype=np.uint8)

    projected_cache: dict[int, tuple[Any, Any]] = {}
    for keyframe_index in sorted(set(int(i) for i in assigned_keyframes if int(i) >= 0)):
        pixel_x, pixel_y, _, _ = project_vertices_to_keyframe(
            vertices,
            keyframes[keyframe_index],
            use_depth_check=False,
            margin_ratio=0.0,
        )
        projected_cache[keyframe_index] = (pixel_x, pixel_y)
        keyframes[keyframe_index]["image_u8"] = np.clip(keyframes[keyframe_index]["image"] * 255.0, 0, 255).astype(np.uint8)

    for face_index, tri in enumerate(triangles):
        row = face_index // grid_cols
        col = face_index % grid_cols
        x0 = col * tile_width
        y0 = row * tile_height
        x1 = texture_size if col == grid_cols - 1 else min(texture_size, (col + 1) * tile_width)
        y1 = texture_size if row == grid_rows - 1 else min(texture_size, (row + 1) * tile_height)
        if x1 - x0 < 3 or y1 - y0 < 3:
            continue

        local_width = x1 - x0
        local_height = y1 - y0
        pad = min(padding, max(0, (min(local_width, local_height) - 2) // 2))
        dst_global = np.array(
            [
                [x0 + pad, y1 - pad - 1],
                [x1 - pad - 1, y1 - pad - 1],
                [(x0 + x1 - 1) * 0.5, y0 + pad],
            ],
            dtype=np.float32,
        )
        dst_local = dst_global - np.array([x0, y0], dtype=np.float32)
        dst_int = np.rint(dst_local).astype(np.int32)

        face_uvs[face_index, :, 0] = dst_global[:, 0] / max(1, texture_size - 1)
        face_uvs[face_index, :, 1] = 1.0 - (dst_global[:, 1] / max(1, texture_size - 1))

        local_mask = np.zeros((local_height, local_width), dtype=np.uint8)
        cv2.fillConvexPoly(local_mask, dst_int, 255)

        keyframe_index = int(assigned_keyframes[face_index])
        wrote_from_rgb = False
        if keyframe_index >= 0:
            pixel_x, pixel_y = projected_cache[keyframe_index]
            src_triangle = np.column_stack((pixel_x[tri], pixel_y[tri])).astype(np.float32)
            src_area = 0.5 * abs(
                src_triangle[0, 0] * (src_triangle[1, 1] - src_triangle[2, 1])
                + src_triangle[1, 0] * (src_triangle[2, 1] - src_triangle[0, 1])
                + src_triangle[2, 0] * (src_triangle[0, 1] - src_triangle[1, 1])
            )
            if np.isfinite(src_triangle).all() and src_area > 0.35:
                transform = cv2.getAffineTransform(src_triangle, dst_local.astype(np.float32))
                warped = cv2.warpAffine(
                    keyframes[keyframe_index]["image_u8"],
                    transform,
                    (local_width, local_height),
                    flags=cv2.INTER_LINEAR,
                    borderMode=cv2.BORDER_REFLECT_101,
                )
                roi = texture[y0:y1, x0:x1]
                roi[local_mask > 0] = warped[local_mask > 0]
                wrote_from_rgb = True

        if not wrote_from_rgb:
            if vertex_colors is not None and len(vertex_colors) == len(vertices):
                rgb = np.clip(vertex_colors[tri].mean(axis=0) * 255.0, 0, 255).astype(np.uint8)
            else:
                rgb = default_rgb
            roi = texture[y0:y1, x0:x1]
            roi[local_mask > 0] = rgb

        mask_roi = mask[y0:y1, x0:x1]
        mask_roi[local_mask > 0] = 255

    texture, mask = dilate_texture_padding(texture, mask, iterations=24)
    texture[mask == 0] = default_rgb
    Image.fromarray(texture, mode="RGB").save(texture_path)
    write_textured_obj(obj_path, mtl_path, texture_path.name, vertices, triangles, face_uvs)

    print(
        "baked_keyframe_texture "
        f"triangles={assigned_count}/{triangle_count} "
        f"texture={texture_size}x{texture_size} "
        f"tile={tile_width}x{tile_height} "
        f"obj={obj_path.name}"
    )
    return obj_path


def bake_vertex_color_texture(
    mesh: "object",
    out_dir: Path,
    *,
    obj_name: str = "result_textured.obj",
    mtl_name: str = "result_textured.mtl",
    texture_name: str = "result_texture.png",
) -> Path | None:
    """Fallback texture bake from existing vertex colors using a simple XZ projection."""
    try:
        import cv2
        import numpy as np
        from PIL import Image
    except ImportError:
        print("bake_vertex_color_texture skipped: numpy, cv2, or PIL not installed")
        return None

    vertices = np.asarray(mesh.vertices)
    triangles = np.asarray(mesh.triangles)
    colors = np.asarray(mesh.vertex_colors)
    if len(vertices) == 0 or len(triangles) == 0 or len(colors) == 0:
        return None

    texture_size = 2048
    texture = np.zeros((texture_size, texture_size, 3), dtype=np.uint8)

    obj_path = out_dir / obj_name
    mtl_path = out_dir / mtl_name
    tex_path = out_dir / texture_name

    min_x, min_z = vertices[:, 0].min(), vertices[:, 2].min()
    max_x, max_z = vertices[:, 0].max(), vertices[:, 2].max()
    range_x = max(max_x - min_x, 1e-6)
    range_z = max(max_z - min_z, 1e-6)

    uvs = np.zeros((len(vertices), 2), dtype=np.float32)
    uvs[:, 0] = (vertices[:, 0] - min_x) / range_x
    uvs[:, 1] = (vertices[:, 2] - min_z) / range_z

    for i, uv in enumerate(uvs):
        x = int(np.clip(uv[0] * (texture_size - 1), 0, texture_size - 1))
        y = int(np.clip((1.0 - uv[1]) * (texture_size - 1), 0, texture_size - 1))
        texture[y, x] = np.clip(colors[i] * 255.0, 0, 255).astype(np.uint8)

    mask = np.any(texture > 0, axis=2).astype(np.uint8) * 255
    kernel = np.ones((5, 5), np.uint8)
    for _ in range(20):
        dilated = cv2.dilate(texture, kernel, iterations=1)
        grown_mask = cv2.dilate(mask, kernel, iterations=1)
        empty = (mask == 0) & (grown_mask > 0)
        texture[empty] = dilated[empty]
        mask[empty] = 255

    Image.fromarray(texture, mode="RGB").save(tex_path)

    with open(mtl_path, "w", encoding="utf-8") as f:
        f.write("newmtl baked_material\n")
        f.write("Ka 1.000 1.000 1.000\n")
        f.write("Kd 1.000 1.000 1.000\n")
        f.write("Ks 0.000 0.000 0.000\n")
        f.write("d 1.0\n")
        f.write("illum 2\n")
        f.write(f"map_Kd {tex_path.name}\n")

    with open(obj_path, "w", encoding="utf-8") as f:
        f.write(f"mtllib {mtl_path.name}\n")
        f.write("usemtl baked_material\n")

        for vertex in vertices:
            f.write(f"v {vertex[0]} {vertex[1]} {vertex[2]}\n")

        for uv in uvs:
            f.write(f"vt {uv[0]} {uv[1]}\n")

        for tri in triangles:
            a, b, c = tri + 1
            f.write(f"f {a}/{a} {b}/{b} {c}/{c}\n")

    print(f"baked fallback vertex-color texture obj={obj_path.name}")
    return obj_path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--scan", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--profile", choices=sorted(PRUNING_PROFILES), default="balanced")
    args = parser.parse_args()

    scan_dir = Path(args.scan)
    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)

    manifest_path = scan_dir / "manifest.json"
    if not manifest_path.exists():
        nested = list(scan_dir.glob("*/manifest.json"))
        manifest_path = nested[0] if nested else manifest_path
    if not manifest_path.exists():
        raise FileNotFoundError(f"manifest.json not found in {scan_dir}")

    manifest = load_json(manifest_path)
    result = choose_result(manifest_path.parent, out_dir, manifest, profile_name=args.profile)
    if result is None and args.profile != "rtabmap":
        result = fallback_raw_mesh(manifest_path.parent, out_dir)

    if result is None:
        raise RuntimeError("No result mesh could be generated")

    print(f"result={result}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
