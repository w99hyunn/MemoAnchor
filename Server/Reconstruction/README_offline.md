# MemoAnchor Offline RGB-D Reconstruction

This folder is Phase C offline reconstruction tooling. It does not modify Unity iOS native plugins, RTAB-Map iOS code, RTAB-Map databases, loop closure, online mapping, or server upload.

## Install

```bash
python3 -m pip install -r Server/Reconstruction/requirements.txt
```

On this machine, the default conda Python was 3.13 and Open3D was not available for it. The verified local environment is:

```bash
python3.10 -m venv Server/Reconstruction/.venv
Server/Reconstruction/.venv/bin/python -m pip install -r Server/Reconstruction/requirements.txt
```

## Dataset Schema Confirmed

The validated dataset stores:

```text
scan_YYYYMMDD_HHMMSS/
  session.json
  frames.jsonl
  rgb/000001.jpg
  depth/000001.bin
  confidence/000001.bin
```

Depth is `DepthFloat32`, little-endian, one float32 meter value per pixel. Current resolution is 256x192 with row stride 1024 bytes and pixel stride 4 bytes.

Confidence is `OneComponent8`, one uint8 per pixel. Observed values are 0, 1, 2. Recorder metadata describes these as ARKit depth confidence values: 0 low, 1 medium, 2 high.

RGB is JPEG, currently 1440x1080. Recorder source converts the AR camera CPU image to RGBA32 with `XRCpuImage.Transformation.MirrorY`, then JPEG encodes it. Depth and confidence are stored as raw unrotated/unflipped CPU image planes.

Intrinsics in `frames.jsonl` are scaled to the saved RGB resolution. For depth processing:

```text
fx_depth = fx_rgb * depth_width / rgb_width
fy_depth = fy_rgb * depth_height / rgb_height
cx_depth = cx_rgb * depth_width / rgb_width
cy_depth = cy_rgb * depth_height / rgb_height
```

Pose is Unity camera-to-world. Matrix is serialized row-major as `m00,m01,m02,m03,m10,...,m33`. Quaternion order is `x,y,z,w`. Unity recorder metadata says world units are meters, with +X right and +Y up.

RGB/depth registration is not fully proven by recorder metadata because RGB has `MirrorY` applied while depth/confidence are raw. The tools default to geometry-only TSDF and only use colored TSDF when `--assume-registered-color` is passed.

## Single Frame Inspector

```bash
python3 Server/Reconstruction/inspect_rgbd_frame.py \
  "/path/to/scan_YYYYMMDD_HHMMSS" \
  --frame-id 1 \
  --output-dir reconstruction_output/frame_000001
```

Outputs:

```text
rgb_original.png
depth_raw_visualization.png
depth_filtered_visualization.png
confidence_visualization.png
rgb_at_depth_resolution.png
depth_edge_overlay_on_rgb.png
orientation_contact_sheet.png
camera_point_cloud.ply
camera_point_cloud_colored.ply
frame_report.json
```

## TSDF Reconstruction

Geometry-only default:

```bash
Server/Reconstruction/.venv/bin/python Server/Reconstruction/reconstruct_open3d_tsdf.py \
  "/path/to/scan_YYYYMMDD_HHMMSS" \
  --output-dir reconstruction_output/full_scan \
  --voxel-size 0.02 \
  --sdf-trunc 0.06 \
  --depth-min 0.15 \
  --depth-max 5.0 \
  --frame-step 1
```

For the validated `scan_20260718_163137` export, the raw ARKit depth plane matched the camera pose best with:

```bash
  --depth-transform rotate_270
```

The orientation sweep supported by `--depth-transform` is:

```text
as_saved
flip_left_right
flip_top_bottom
rotate_90
rotate_180
rotate_270
```

Colored candidate, only when visually acceptable:

```bash
Server/Reconstruction/.venv/bin/python Server/Reconstruction/reconstruct_open3d_tsdf.py \
  "/path/to/scan_YYYYMMDD_HHMMSS" \
  --output-dir reconstruction_output/full_scan_color \
  --depth-transform rotate_270 \
  --color-transform rotate_90 \
  --assume-registered-color
```

Voxel sweep:

```bash
Server/Reconstruction/.venv/bin/python Server/Reconstruction/reconstruct_open3d_tsdf.py \
  "/path/to/scan_YYYYMMDD_HHMMSS" \
  --output-dir reconstruction_output/sweep \
  --depth-transform rotate_270 \
  --frame-step 2 \
  --sweep
```

Outputs include:

```text
fused_point_cloud.ply
fused_mesh_raw.ply
fused_mesh_clean.ply
fused_mesh_clean.obj
camera_trajectory.ply
trajectory_report.json
used_frames.jsonl
rejected_frames.jsonl
reconstruction_report.json
preview.png
preview_001_frame.png
preview_010_frames.png
preview_050_frames.png
preview_100_frames.png
preview_all_frames.png
```

## Coordinate Conversion

All Unity to Open3D pose conversion is isolated in:

```python
unity_camera_to_world_to_open3d_camera_to_world()
```

The current candidate is:

```text
Open3D camera-to-world = diag(1,1,-1,1) @ Unity camera-to-world @ diag(1,-1,1,1)
```

Open3D TSDF integration receives the inverse, because `integrate()` expects world-to-camera extrinsic.
