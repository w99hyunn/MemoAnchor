#!/usr/bin/env python3
import argparse
import json
import math
import os
import statistics
import struct
import sys
from collections import Counter


def issue(level, message, issues):
    issues.append({"level": level, "message": message})
    print(f"{level}: {message}")


def read_json(path):
    with open(path, "r", encoding="utf-8-sig") as f:
        return json.load(f)


def read_frames(path):
    frames = []
    with open(path, "r", encoding="utf-8-sig") as f:
        for line_no, line in enumerate(f, start=1):
            line = line.strip()
            if not line:
                continue
            try:
                frames.append(json.loads(line))
            except json.JSONDecodeError as exc:
                raise ValueError(f"frames.jsonl line {line_no} is not valid JSON: {exc}") from exc
    return frames


def jpeg_dimensions(path):
    with open(path, "rb") as f:
        data = f.read()
    if len(data) < 4 or data[0:2] != b"\xff\xd8":
        return None
    i = 2
    while i + 9 < len(data):
        if data[i] != 0xFF:
            i += 1
            continue
        marker = data[i + 1]
        i += 2
        while marker == 0xFF and i < len(data):
            marker = data[i]
            i += 1
        if marker in (0xD8, 0xD9):
            continue
        if i + 2 > len(data):
            break
        length = int.from_bytes(data[i:i + 2], "big")
        if length < 2 or i + length > len(data):
            break
        if marker in (0xC0, 0xC1, 0xC2, 0xC3, 0xC5, 0xC6, 0xC7, 0xC9, 0xCA, 0xCB, 0xCD, 0xCE, 0xCF):
            height = int.from_bytes(data[i + 3:i + 5], "big")
            width = int.from_bytes(data[i + 5:i + 7], "big")
            return width, height
        i += length
    return None


def depth_stats(path, width, height, row_stride, little_endian=True):
    with open(path, "rb") as f:
        data = f.read()
    expected_tight = width * height * 4
    expected_stride = row_stride * height if row_stride else expected_tight
    values = []
    finite = 0
    nan_inf = 0
    valid = 0
    endian = "<f" if little_endian else ">f"
    for y in range(max(0, height)):
        row = y * (row_stride or width * 4)
        for x in range(max(0, width)):
            offset = row + x * 4
            if offset + 4 > len(data):
                break
            value = struct.unpack_from(endian, data, offset)[0]
            if math.isfinite(value):
                finite += 1
                if value > 0:
                    valid += 1
                    values.append(value)
            else:
                nan_inf += 1
    stats = {
        "bytes": len(data),
        "expected_tight_bytes": expected_tight,
        "expected_stride_bytes": expected_stride,
        "finite_count": finite,
        "nan_inf_count": nan_inf,
        "valid_count": valid,
        "valid_ratio": valid / max(1, width * height),
        "nan_inf_ratio": nan_inf / max(1, width * height),
    }
    if values:
        stats.update({
            "min": min(values),
            "max": max(values),
            "median": statistics.median(values),
        })
    return stats


def main():
    parser = argparse.ArgumentParser(description="Validate a MemoAnchor RGB-D recorder dataset.")
    parser.add_argument("dataset", help="Path to scan_YYYYMMDD_HHMMSS dataset folder")
    args = parser.parse_args()

    dataset = os.path.abspath(args.dataset)
    issues = []
    summary = {"dataset": dataset, "issues": issues}

    session_path = os.path.join(dataset, "session.json")
    frames_path = os.path.join(dataset, "frames.jsonl")

    if not os.path.isfile(session_path):
        issue("FAIL", "session.json is missing", issues)
        return finish(summary, issues)
    if not os.path.isfile(frames_path):
        issue("FAIL", "frames.jsonl is missing", issues)
        return finish(summary, issues)

    try:
        session = read_json(session_path)
        frames = read_frames(frames_path)
    except Exception as exc:
        issue("FAIL", str(exc), issues)
        return finish(summary, issues)

    summary["session"] = session
    summary["frame_count"] = len(frames)
    if not frames:
        issue("FAIL", "frames.jsonl contains no frames", issues)
        return finish(summary, issues)

    frame_ids = [frame.get("frame_id") for frame in frames]
    duplicate_ids = [fid for fid, count in Counter(frame_ids).items() if count > 1]
    if duplicate_ids:
        issue("FAIL", f"duplicate frame ids: {duplicate_ids[:10]}", issues)

    sorted_ids = sorted(fid for fid in frame_ids if isinstance(fid, int))
    if sorted_ids and sorted_ids != list(range(sorted_ids[0], sorted_ids[0] + len(sorted_ids))):
        issue("WARN", "frame ids are not contiguous; this can happen when the recorder drops old queued frames", issues)
    if frame_ids != sorted_ids:
        issue("WARN", "frame ids are not monotonic in frames.jsonl", issues)

    rgb_dims = set()
    depth_dims = set()
    timestamp_deltas = []
    rgb_timestamps = []
    tracking_states = Counter()
    pose_jumps = []
    last_pos = None
    depth_summaries = []

    for frame in frames:
        fid = frame.get("frame_id")
        rgb_rel = frame.get("rgb_file", "")
        depth_rel = frame.get("depth_file", "")
        conf_rel = frame.get("confidence_file", "")
        rgb_path = os.path.join(dataset, rgb_rel)
        depth_path = os.path.join(dataset, depth_rel)
        conf_path = os.path.join(dataset, conf_rel)

        for label, path in (("RGB", rgb_path), ("depth", depth_path), ("confidence", conf_path)):
            if not os.path.isfile(path):
                issue("FAIL", f"frame {fid}: missing {label} file {path}", issues)
            elif os.path.getsize(path) <= 0:
                issue("FAIL", f"frame {fid}: {label} file is empty {path}", issues)

        if os.path.isfile(rgb_path):
            dims = jpeg_dimensions(rgb_path)
            expected = (frame.get("rgb_width"), frame.get("rgb_height"))
            if dims is None:
                issue("WARN", f"frame {fid}: RGB file dimensions could not be read as JPEG", issues)
            elif dims != expected:
                issue("FAIL", f"frame {fid}: RGB dimensions mismatch file={dims} metadata={expected}", issues)
            else:
                rgb_dims.add(dims)

        width = int(frame.get("depth_width") or 0)
        height = int(frame.get("depth_height") or 0)
        row_stride = int(frame.get("depth_row_stride") or 0)
        if os.path.isfile(depth_path) and width > 0 and height > 0:
            stats = depth_stats(depth_path, width, height, row_stride, bool(frame.get("depth_little_endian", True)))
            depth_summaries.append(stats)
            if stats["bytes"] < min(stats["expected_tight_bytes"], stats["expected_stride_bytes"]):
                issue("FAIL", f"frame {fid}: depth file too small ({stats['bytes']} bytes)", issues)
            if stats["valid_ratio"] < 0.05:
                issue("WARN", f"frame {fid}: very low valid depth ratio {stats['valid_ratio']:.1%}", issues)
        depth_dims.add((width, height))

        conf_w = int(frame.get("confidence_width") or 0)
        conf_h = int(frame.get("confidence_height") or 0)
        if os.path.isfile(conf_path) and conf_w > 0 and conf_h > 0:
            if os.path.getsize(conf_path) < conf_w * conf_h:
                issue("WARN", f"frame {fid}: confidence file smaller than width*height", issues)

        rgb_ts = frame.get("rgb_timestamp")
        if isinstance(rgb_ts, (int, float)):
            rgb_timestamps.append(float(rgb_ts))
        delta = frame.get("timestamp_difference_ms")
        if isinstance(delta, (int, float)):
            timestamp_deltas.append(float(delta))

        fx = float(frame.get("fx") or 0)
        fy = float(frame.get("fy") or 0)
        cx = float(frame.get("cx") or 0)
        cy = float(frame.get("cy") or 0)
        if frame.get("has_intrinsics") and (fx <= 0 or fy <= 0 or cx < 0 or cy < 0):
            issue("FAIL", f"frame {fid}: invalid intrinsics fx={fx} fy={fy} cx={cx} cy={cy}", issues)
        if frame.get("has_intrinsics") and (cx > max(1, frame.get("rgb_width", 0)) * 2 or cy > max(1, frame.get("rgb_height", 0)) * 2):
            issue("WARN", f"frame {fid}: principal point is outside expected range", issues)

        quat = frame.get("camera_rotation") or []
        if len(quat) == 4:
            norm = math.sqrt(sum(float(v) * float(v) for v in quat))
            if abs(norm - 1.0) > 0.05:
                issue("WARN", f"frame {fid}: quaternion norm is {norm:.3f}", issues)

        pos = frame.get("camera_position") or []
        if len(pos) == 3:
            pos = tuple(float(v) for v in pos)
            if last_pos is not None:
                jump = math.sqrt(sum((pos[i] - last_pos[i]) ** 2 for i in range(3)))
                pose_jumps.append(jump)
                if jump > 1.0:
                    issue("WARN", f"frame {fid}: pose translation jump {jump:.2f}m", issues)
            last_pos = pos

        tracking_states[str(frame.get("tracking_state", "unknown"))] += 1

    if any(rgb_timestamps[i] > rgb_timestamps[i + 1] for i in range(len(rgb_timestamps) - 1)):
        issue("FAIL", "RGB timestamps are not monotonic", issues)

    if len(rgb_dims) > 1:
        issue("WARN", f"RGB resolution changed: {sorted(rgb_dims)}", issues)
    if len(depth_dims) > 1:
        issue("WARN", f"depth resolution changed: {sorted(depth_dims)}", issues)

    summary["tracking_states"] = dict(tracking_states)
    summary["rgb_dimensions"] = sorted(rgb_dims)
    summary["depth_dimensions"] = sorted(depth_dims)
    summary["timestamp_difference_ms"] = numeric_summary(timestamp_deltas)
    summary["pose_jump_m"] = numeric_summary(pose_jumps)
    summary["depth_valid_ratio"] = numeric_summary([s["valid_ratio"] for s in depth_summaries])
    summary["depth_nan_inf_ratio"] = numeric_summary([s["nan_inf_ratio"] for s in depth_summaries])
    summary["depth_min_m"] = numeric_summary([s["min"] for s in depth_summaries if "min" in s])
    summary["depth_max_m"] = numeric_summary([s["max"] for s in depth_summaries if "max" in s])
    summary["depth_median_m"] = numeric_summary([s["median"] for s in depth_summaries if "median" in s])

    return finish(summary, issues)


def numeric_summary(values):
    values = [float(v) for v in values if isinstance(v, (int, float)) and math.isfinite(float(v))]
    if not values:
        return {}
    return {
        "count": len(values),
        "min": min(values),
        "max": max(values),
        "median": statistics.median(values),
        "mean": statistics.fmean(values),
    }


def finish(summary, issues):
    has_fail = any(item["level"] == "FAIL" for item in issues)
    has_warn = any(item["level"] == "WARN" for item in issues)
    status = "FAIL" if has_fail else "WARN" if has_warn else "PASS"
    summary["status"] = status
    out_path = os.path.join(summary["dataset"], "validation_summary.json")
    try:
        with open(out_path, "w", encoding="utf-8") as f:
            json.dump(summary, f, ensure_ascii=False, indent=2)
        print(f"{status}: wrote {out_path}")
    except Exception as exc:
        print(f"WARN: could not write validation summary: {exc}")
    return 1 if has_fail else 0


if __name__ == "__main__":
    sys.exit(main())
