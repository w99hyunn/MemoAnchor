"""Visual relocalization against the RGB frames captured with a MemoAnchor scan."""

from __future__ import annotations

import json
import threading
from dataclasses import dataclass
from pathlib import Path
from typing import Any


MAX_REFERENCE_FRAMES = 120
MIN_HOMOGRAPHY_INLIERS = 18
_INDEX_CACHE: dict[str, tuple[float, list["ReferenceFrame"]]] = {}
_MESH_CACHE: dict[str, tuple[float, Any]] = {}
_INDEX_CACHE_LOCK = threading.Lock()


@dataclass
class ReferenceFrame:
    frame_id: int
    keypoints: Any
    descriptors: Any
    metadata: dict[str, Any]


def _is_rgbd_dataset(path: Path) -> bool:
    return (path / "frames.jsonl").is_file() and (path / "rgb").is_dir()


def _find_rgbd_dataset(scan_dir: Path) -> Path | None:
    if _is_rgbd_dataset(scan_dir):
        return scan_dir

    candidates = [path.parent for path in scan_dir.rglob("frames.jsonl") if _is_rgbd_dataset(path.parent)]
    if not candidates:
        return None
    return max(candidates, key=lambda path: (path / "frames.jsonl").stat().st_size)


def _sample_records(records: list[dict[str, Any]]) -> list[dict[str, Any]]:
    if len(records) <= MAX_REFERENCE_FRAMES:
        return records

    last = len(records) - 1
    indices = sorted({round(index * last / (MAX_REFERENCE_FRAMES - 1)) for index in range(MAX_REFERENCE_FRAMES)})
    return [records[index] for index in indices]


def _load_index(scan_id: str, scan_dir: Path) -> tuple[Path, list[ReferenceFrame]]:
    import cv2

    dataset = _find_rgbd_dataset(scan_dir)
    if dataset is None:
        raise FileNotFoundError("RGB-D reference frames were not found for this scan.")

    frames_path = dataset / "frames.jsonl"
    modified_at = frames_path.stat().st_mtime
    with _INDEX_CACHE_LOCK:
        cached = _INDEX_CACHE.get(scan_id)
        if cached is not None and cached[0] == modified_at:
            return dataset, cached[1]

    records: list[dict[str, Any]] = []
    with frames_path.open("r", encoding="utf-8-sig") as source:
        for line in source:
            if not line.strip():
                continue
            record = json.loads(line)
            if (record.get("camera_position")
                    and record.get("camera_rotation")
                    and record.get("rgb_file")
                    and record.get("has_intrinsics")):
                records.append(record)

    orb = cv2.ORB_create(nfeatures=1800, scaleFactor=1.2, nlevels=8, fastThreshold=12)
    references: list[ReferenceFrame] = []
    for record in _sample_records(records):
        image_path = dataset / str(record["rgb_file"])
        image = cv2.imread(str(image_path), cv2.IMREAD_GRAYSCALE)
        if image is None:
            continue
        keypoints, descriptors = orb.detectAndCompute(image, None)
        if descriptors is None or len(keypoints) < MIN_HOMOGRAPHY_INLIERS:
            continue
        references.append(ReferenceFrame(int(record.get("frame_id", 0)), keypoints, descriptors, record))

    if not references:
        raise RuntimeError("No usable visual reference frames were found for this scan.")

    with _INDEX_CACHE_LOCK:
        _INDEX_CACHE[scan_id] = (modified_at, references)
    return dataset, references


def _query_orientations(image: Any) -> list[tuple[int, Any]]:
    import cv2

    return [
        (0, image),
        (90, cv2.rotate(image, cv2.ROTATE_90_CLOCKWISE)),
        (180, cv2.rotate(image, cv2.ROTATE_180)),
        (270, cv2.rotate(image, cv2.ROTATE_90_COUNTERCLOCKWISE)),
    ]


def _load_mesh_vertices(scan_id: str, result_root: Path) -> Any:
    import numpy as np
    import open3d as o3d

    status_path = result_root / scan_id / "status.json"
    if not status_path.exists():
        raise FileNotFoundError("Reconstruction status was not found.")
    status = json.loads(status_path.read_text(encoding="utf-8"))
    result_file = str(status.get("resultFile", ""))
    mesh_path = result_root / scan_id / result_file
    if not result_file or not mesh_path.exists():
        raise FileNotFoundError("Reconstruction mesh was not found.")

    modified_at = mesh_path.stat().st_mtime
    with _INDEX_CACHE_LOCK:
        cached = _MESH_CACHE.get(scan_id)
        if cached is not None and cached[0] == modified_at:
            return cached[1]

    mesh = o3d.io.read_triangle_mesh(str(mesh_path))
    vertices = np.asarray(mesh.vertices, dtype=np.float64)
    if vertices.shape[0] < 6:
        raise RuntimeError("Reconstruction mesh has too few vertices for localization.")
    if vertices.shape[0] > 300_000:
        step = max(1, vertices.shape[0] // 300_000)
        vertices = vertices[::step]

    with _INDEX_CACHE_LOCK:
        _MESH_CACHE[scan_id] = (modified_at, vertices)
    return vertices


def _matrix_from_frame(frame: dict[str, Any]) -> Any:
    import numpy as np

    values = frame.get("camera_to_world_matrix", [])
    if len(values) != 16:
        raise ValueError("Reference frame pose is invalid.")
    return np.asarray(values, dtype=np.float64).reshape(4, 4)


def _unity_camera_to_open3d_camera(unity_camera_to_world: Any) -> Any:
    import numpy as np

    world_z_flip = np.diag([1.0, 1.0, -1.0, 1.0])
    camera_y_flip = np.diag([1.0, -1.0, 1.0, 1.0])
    return world_z_flip @ unity_camera_to_world @ camera_y_flip


def _quaternion_from_rotation(rotation: Any) -> list[float]:
    import numpy as np

    trace = float(np.trace(rotation))
    if trace > 0.0:
        scale = (trace + 1.0) ** 0.5 * 2.0
        w = 0.25 * scale
        x = (rotation[2, 1] - rotation[1, 2]) / scale
        y = (rotation[0, 2] - rotation[2, 0]) / scale
        z = (rotation[1, 0] - rotation[0, 1]) / scale
    else:
        axis = int(np.argmax(np.diag(rotation)))
        if axis == 0:
            scale = (1.0 + rotation[0, 0] - rotation[1, 1] - rotation[2, 2]) ** 0.5 * 2.0
            x = 0.25 * scale
            y = (rotation[0, 1] + rotation[1, 0]) / scale
            z = (rotation[0, 2] + rotation[2, 0]) / scale
            w = (rotation[2, 1] - rotation[1, 2]) / scale
        elif axis == 1:
            scale = (1.0 + rotation[1, 1] - rotation[0, 0] - rotation[2, 2]) ** 0.5 * 2.0
            x = (rotation[0, 1] + rotation[1, 0]) / scale
            y = 0.25 * scale
            z = (rotation[1, 2] + rotation[2, 1]) / scale
            w = (rotation[0, 2] - rotation[2, 0]) / scale
        else:
            scale = (1.0 + rotation[2, 2] - rotation[0, 0] - rotation[1, 1]) ** 0.5 * 2.0
            x = (rotation[0, 2] + rotation[2, 0]) / scale
            y = (rotation[1, 2] + rotation[2, 1]) / scale
            z = 0.25 * scale
            w = (rotation[1, 0] - rotation[0, 1]) / scale
    quaternion = np.asarray([x, y, z, w], dtype=np.float64)
    quaternion /= max(float(np.linalg.norm(quaternion)), 1e-9)
    return quaternion.tolist()


def _estimate_query_pose(best: dict[str, Any], vertices: Any, intrinsics: dict[str, float]) -> tuple[list[float], list[float], int] | None:
    import cv2
    import numpy as np

    if best["rotation"] != 0:
        return None

    reference = best["reference"]
    frame = reference.metadata
    reference_camera_to_world = _unity_camera_to_open3d_camera(_matrix_from_frame(frame))
    world_to_reference_camera = np.linalg.inv(reference_camera_to_world)
    homogeneous = np.concatenate([vertices, np.ones((vertices.shape[0], 1), dtype=np.float64)], axis=1)
    camera_points = (world_to_reference_camera @ homogeneous.T).T[:, :3]
    valid = camera_points[:, 2] > 0.12
    camera_points = camera_points[valid]
    world_points = vertices[valid]
    if camera_points.shape[0] < 6:
        return None

    fx = float(frame["fx"])
    fy = float(frame["fy"])
    cx = float(frame["cx"])
    cy = float(frame["cy"])
    projected = np.column_stack([
        fx * camera_points[:, 0] / camera_points[:, 2] + cx,
        fy * camera_points[:, 1] / camera_points[:, 2] + cy,
    ])
    inside = (
        (projected[:, 0] >= 0.0)
        & (projected[:, 0] < float(frame["rgb_width"]))
        & (projected[:, 1] >= 0.0)
        & (projected[:, 1] < float(frame["rgb_height"]))
    )
    projected = projected[inside]
    world_points = world_points[inside]
    if projected.shape[0] < 6:
        return None

    object_points: list[Any] = []
    image_points: list[Any] = []
    used_vertex_indices: set[int] = set()
    for match in best["goodMatches"]:
        reference_point = np.asarray(reference.keypoints[match.trainIdx].pt, dtype=np.float64)
        distances = np.sum((projected - reference_point) ** 2, axis=1)
        vertex_index = int(np.argmin(distances))
        if distances[vertex_index] > 12.0 ** 2 or vertex_index in used_vertex_indices:
            continue
        used_vertex_indices.add(vertex_index)
        object_points.append(world_points[vertex_index])
        image_points.append(best["queryKeypoints"][match.queryIdx].pt)

    if len(object_points) < 8:
        return None

    camera_matrix = np.asarray([
        [intrinsics["fx"], 0.0, intrinsics["cx"]],
        [0.0, intrinsics["fy"], intrinsics["cy"]],
        [0.0, 0.0, 1.0],
    ], dtype=np.float64)
    success, rotation_vector, translation, inliers = cv2.solvePnPRansac(
        np.asarray(object_points, dtype=np.float64),
        np.asarray(image_points, dtype=np.float64),
        camera_matrix,
        None,
        iterationsCount=240,
        reprojectionError=5.0,
        confidence=0.999,
        flags=cv2.SOLVEPNP_EPNP,
    )
    if not success or inliers is None or len(inliers) < 6:
        return None

    inlier_indices = inliers.reshape(-1)
    if hasattr(cv2, "solvePnPRefineLM"):
        rotation_vector, translation = cv2.solvePnPRefineLM(
            np.asarray(object_points, dtype=np.float64)[inlier_indices],
            np.asarray(image_points, dtype=np.float64)[inlier_indices],
            camera_matrix,
            None,
            rotation_vector,
            translation,
        )

    world_to_camera = np.eye(4, dtype=np.float64)
    world_to_camera[:3, :3] = cv2.Rodrigues(rotation_vector)[0]
    world_to_camera[:3, 3] = translation.reshape(3)
    open3d_camera_to_world = np.linalg.inv(world_to_camera)
    world_z_flip = np.diag([1.0, 1.0, -1.0, 1.0])
    camera_y_flip = np.diag([1.0, -1.0, 1.0, 1.0])
    unity_camera_to_world = world_z_flip @ open3d_camera_to_world @ camera_y_flip
    return (
        unity_camera_to_world[:3, 3].tolist(),
        _quaternion_from_rotation(unity_camera_to_world[:3, :3]),
        int(len(inliers)),
    )


def localize(
    scan_root: Path,
    result_root: Path,
    scan_id: str,
    jpeg_bytes: bytes,
    intrinsics: dict[str, float],
) -> dict[str, Any]:
    import cv2
    import numpy as np

    scan_dir = scan_root / scan_id
    if not scan_dir.exists():
        raise FileNotFoundError("Scan data was not found.")

    _, references = _load_index(scan_id, scan_dir)
    encoded = np.frombuffer(jpeg_bytes, dtype=np.uint8)
    image = cv2.imdecode(encoded, cv2.IMREAD_GRAYSCALE)
    if image is None:
        raise ValueError("The camera image could not be decoded.")

    orb = cv2.ORB_create(nfeatures=2000, scaleFactor=1.2, nlevels=8, fastThreshold=12)
    matcher = cv2.BFMatcher(cv2.NORM_HAMMING, crossCheck=False)
    best: dict[str, Any] | None = None

    for rotation, oriented in _query_orientations(image):
        query_keypoints, query_descriptors = orb.detectAndCompute(oriented, None)
        if query_descriptors is None or len(query_keypoints) < MIN_HOMOGRAPHY_INLIERS:
            continue

        for reference in references:
            pairs = matcher.knnMatch(query_descriptors, reference.descriptors, k=2)
            good = [first for first, second in pairs if first.distance < 0.74 * second.distance]
            if len(good) < MIN_HOMOGRAPHY_INLIERS:
                continue

            query_points = np.float32([query_keypoints[match.queryIdx].pt for match in good]).reshape(-1, 1, 2)
            reference_points = np.float32([reference.keypoints[match.trainIdx].pt for match in good]).reshape(-1, 1, 2)
            _, mask = cv2.findHomography(query_points, reference_points, cv2.RANSAC, 4.5)
            if mask is None:
                continue

            inliers = int(mask.ravel().sum())
            inlier_ratio = inliers / max(1, len(good))
            score = inliers * (0.55 + 0.45 * inlier_ratio)
            if best is None or score > best["score"]:
                best = {
                    "score": score,
                    "inliers": inliers,
                    "matches": len(good),
                    "inlierRatio": inlier_ratio,
                    "rotation": rotation,
                    "reference": reference,
                    "queryKeypoints": query_keypoints,
                    "goodMatches": good,
                }

    if best is None or best["inliers"] < MIN_HOMOGRAPHY_INLIERS or best["inlierRatio"] < 0.22:
        return {
            "localized": False,
            "confidence": 0.0,
            "message": "스캔 당시 위치와 비슷한 곳에서 주변을 천천히 비춰주세요.",
        }

    reference = best["reference"]
    vertices = _load_mesh_vertices(scan_id, result_root)
    pose = _estimate_query_pose(best, vertices, intrinsics)
    if pose is None:
        return {
            "localized": False,
            "confidence": 0.0,
            "message": "위치를 계산할 수 없습니다. 스캔 당시 위치에 더 가까이 이동해주세요.",
        }

    camera_position, camera_rotation, pose_inliers = pose
    confidence = min(
        1.0,
        (best["inliers"] / 65.0)
        * min(1.0, best["inlierRatio"] / 0.55)
        * min(1.0, pose_inliers / 18.0),
    )
    return {
        "localized": True,
        "confidence": round(confidence, 4),
        "message": "스캔 공간을 찾았습니다.",
        "matchedFrameId": reference.frame_id,
        "matchedOrientation": best["rotation"],
        "inlierMatches": best["inliers"],
        "poseInliers": pose_inliers,
        "poseMethod": "mesh_pnp",
        "cameraPosition": camera_position,
        "cameraRotation": camera_rotation,
    }
