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
_MESH_CACHE: dict[str, tuple[float, "LocalizationMesh"]] = {}
_INDEX_CACHE_LOCK = threading.Lock()


@dataclass
class ReferenceFrame:
    frame_id: int
    keypoints: Any
    descriptors: Any
    retrieval_descriptors: Any
    metadata: dict[str, Any]


@dataclass
class LocalizationMesh:
    scene: Any


def _normalize_capture_image(image: Any) -> Any:
    import cv2

    # Unity records every RGB frame with XRCpuImage.Transformation.MirrorY,
    # which mirrors left and right. Undo it before applying the camera intrinsics.
    return cv2.flip(image, 1)


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
    import numpy as np

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
        image = _normalize_capture_image(image)
        keypoints, descriptors = orb.detectAndCompute(image, None)
        if descriptors is None or len(keypoints) < MIN_HOMOGRAPHY_INLIERS:
            continue
        strongest = np.argsort([-keypoint.response for keypoint in keypoints])[:320]
        references.append(ReferenceFrame(
            int(record.get("frame_id", 0)),
            keypoints,
            descriptors,
            descriptors[strongest],
            record,
        ))

    if not references:
        raise RuntimeError("No usable visual reference frames were found for this scan.")

    with _INDEX_CACHE_LOCK:
        _INDEX_CACHE[scan_id] = (modified_at, references)
    return dataset, references


def _query_orientations(image: Any) -> list[tuple[int, Any]]:
    # XRCpuImage is captured in the sensor orientation for both scan references
    # and localization queries, independent of the screen orientation.
    return [(0, image)]


def _load_localization_mesh(scan_id: str, result_root: Path) -> LocalizationMesh:
    import open3d as o3d

    status_path = result_root / scan_id / "status.json"
    if not status_path.exists():
        raise FileNotFoundError("Reconstruction status was not found.")
    status = json.loads(status_path.read_text(encoding="utf-8"))
    mesh_path = result_root / scan_id / "result_open3d.ply"
    if not mesh_path.exists():
        raise FileNotFoundError("Reconstruction mesh was not found.")

    modified_at = mesh_path.stat().st_mtime
    with _INDEX_CACHE_LOCK:
        cached = _MESH_CACHE.get(scan_id)
        if cached is not None and cached[0] == modified_at:
            return cached[1]

    mesh = o3d.io.read_triangle_mesh(str(mesh_path))
    if len(mesh.vertices) < 6 or len(mesh.triangles) < 2:
        raise RuntimeError("Reconstruction mesh has too few vertices for localization.")
    scene = o3d.t.geometry.RaycastingScene()
    scene.add_triangles(o3d.t.geometry.TriangleMesh.from_legacy(mesh))
    localization_mesh = LocalizationMesh(scene)

    with _INDEX_CACHE_LOCK:
        _MESH_CACHE[scan_id] = (modified_at, localization_mesh)
    return localization_mesh


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


def _estimate_query_pose(
    best: dict[str, Any],
    localization_mesh: LocalizationMesh,
    intrinsics: dict[str, float],
) -> tuple[list[float], list[float], int] | None:
    import cv2
    import numpy as np
    import open3d as o3d

    if best["rotation"] != 0:
        return None

    reference = best["reference"]
    frame = reference.metadata
    reference_camera_to_world = _unity_camera_to_open3d_camera(_matrix_from_frame(frame))
    homography_mask = best["homographyMask"]
    matches = [
        match
        for index, match in enumerate(best["goodMatches"])
        if homography_mask[index]
    ]
    if len(matches) < 8:
        return None

    reference_points = np.asarray(
        [reference.keypoints[match.trainIdx].pt for match in matches],
        dtype=np.float64,
    )
    camera_directions = np.column_stack([
        (reference_points[:, 0] - float(frame["cx"])) / float(frame["fx"]),
        (reference_points[:, 1] - float(frame["cy"])) / float(frame["fy"]),
        np.ones(len(reference_points), dtype=np.float64),
    ])
    camera_directions /= np.linalg.norm(camera_directions, axis=1, keepdims=True)
    world_directions = camera_directions @ reference_camera_to_world[:3, :3].T
    origins = np.repeat(reference_camera_to_world[None, :3, 3], len(matches), axis=0)
    rays = np.concatenate([origins, world_directions], axis=1).astype(np.float32)
    hits = localization_mesh.scene.cast_rays(o3d.core.Tensor(rays))
    hit_distances = hits["t_hit"].numpy().astype(np.float64)
    valid_hits = np.isfinite(hit_distances) & (hit_distances > 0.12) & (hit_distances < 8.0)
    if int(np.count_nonzero(valid_hits)) < 8:
        return None

    object_points = origins[valid_hits] + world_directions[valid_hits] * hit_distances[valid_hits, None]
    image_points = np.asarray(
        [best["queryKeypoints"][match.queryIdx].pt for match, valid in zip(matches, valid_hits) if valid],
        dtype=np.float64,
    )

    if len(object_points) < 8:
        return None

    camera_matrix = np.asarray([
        [intrinsics["fx"], 0.0, intrinsics["cx"]],
        [0.0, intrinsics["fy"], intrinsics["cy"]],
        [0.0, 0.0, 1.0],
    ], dtype=np.float64)
    success, rotation_vector, translation, inliers = cv2.solvePnPRansac(
        object_points,
        image_points,
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
            object_points[inlier_indices],
            image_points[inlier_indices],
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
    image = _normalize_capture_image(image)

    orb = cv2.ORB_create(nfeatures=2000, scaleFactor=1.2, nlevels=8, fastThreshold=12)
    matcher = cv2.BFMatcher(cv2.NORM_HAMMING, crossCheck=False)
    best: dict[str, Any] | None = None

    for rotation, oriented in _query_orientations(image):
        query_keypoints, query_descriptors = orb.detectAndCompute(oriented, None)
        if query_descriptors is None or len(query_keypoints) < MIN_HOMOGRAPHY_INLIERS:
            continue

        strongest_query = np.argsort([-keypoint.response for keypoint in query_keypoints])[:500]
        query_retrieval_descriptors = query_descriptors[strongest_query]
        retrieval_scores: list[tuple[int, ReferenceFrame]] = []
        for reference in references:
            pairs = matcher.knnMatch(query_retrieval_descriptors, reference.retrieval_descriptors, k=2)
            retrieval_score = sum(1 for first, second in pairs if first.distance < 0.80 * second.distance)
            retrieval_scores.append((retrieval_score, reference))

        retrieval_scores.sort(key=lambda item: item[0], reverse=True)
        for _, reference in retrieval_scores[:12]:
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
                    "homographyMask": mask.ravel().astype(bool),
                }

    if best is None or best["inliers"] < MIN_HOMOGRAPHY_INLIERS or best["inlierRatio"] < 0.22:
        return {
            "localized": False,
            "confidence": 0.0,
            "message": "스캔 당시 위치와 비슷한 곳에서 주변을 천천히 비춰주세요.",
        }

    reference = best["reference"]
    localization_mesh = _load_localization_mesh(scan_id, result_root)
    pose = _estimate_query_pose(best, localization_mesh, intrinsics)
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
