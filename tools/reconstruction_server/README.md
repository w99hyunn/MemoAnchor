# MemoAnchor Reconstruction Server

Local receiver and reconstruction worker for ARKit scan packages.

## Current End-to-End Flow

The current working path is:

```text
iPad Unity app
  -> records RgbdRecorder/scan_<scanId>
  -> packages the RGB-D dataset into the upload ZIP as rgbd_dataset/
  -> POST /upload to the Mac/server

Mac/server
  -> extracts the ZIP under tools/reconstruction_server/data/scans/<scanId>
  -> finds session.json + frames.jsonl + rgb/depth/confidence
  -> runs tools/reconstruction/reconstruct_open3d_tsdf.py
  -> writes tools/reconstruction_server/data/results/<scanId>/result.ply
  -> serves /viewer?scan=<scanId> for browser inspection
  -> serves /result/<scanId> for the iPad app preview
```

Start the server from the repo root:

```bash
tools/reconstruction/.venv/bin/python tools/reconstruction_server/server.py --host 0.0.0.0 --port 8765
```

Set the iPad Unity `reconstructionUploadUrl` to:

```text
http://<mac-lan-ip>:8765/upload
```

For this Mac during validation, the LAN address was:

```text
http://<mac-lan-ip>:8765/upload
```

After upload, inspect from the Mac:

```text
http://127.0.0.1:8765/viewer?scan=<scanId>
```

or from another device on the same Wi-Fi:

```text
http://<mac-lan-ip>:8765/viewer?scan=<scanId>
```

The iPad app polls:

```text
GET /status/<scanId>
```

and downloads:

```text
GET /result/<scanId>
```

The server returns `result.ply`, which the app already parses as a binary little-endian PLY mesh.

## Open3D RGB-D Defaults

For the validated `RgbdRecorder` dataset, the server uses:

```text
depth transform: rotate_270
color transform: rotate_90
voxel size: 0.02 m
sdf truncation: 0.06 m
depth range: 0.15-5.0 m
confidence threshold: >= 1
```

Outputs per scan:

```text
tools/reconstruction_server/data/results/<scanId>/result.ply
tools/reconstruction_server/data/results/<scanId>/result_geometry.ply
tools/reconstruction_server/data/results/<scanId>/result_point_cloud.ply
tools/reconstruction_server/data/results/<scanId>/reconstruction_report.json
tools/reconstruction_server/data/results/<scanId>/trajectory_report.json
tools/reconstruction_server/data/results/<scanId>/preview.png
```

## Run

Open3D currently works reliably from Python 3.10 on this Mac. Start the
server with the same Python that has Open3D installed, because the worker uses
the server process' Python executable.

```bash
cd tools/reconstruction_server
python3.10 -m pip install --user -r requirements.txt
python3.10 server.py --host 0.0.0.0 --port 8765
```

Set the Unity `ARKitMeshScanController` fields:

- `uploadReconstructionPackageOnStop`: enabled
- `reconstructionUploadUrl`: `http://<your-mac-ip>:8765/upload`

Current scan capture is intentionally strict for reconstruction quality:

- live ARKit mesh and coverage markers stay visible while scanning;
- only synchronized RGB-D keyframes are kept by default;
- RGB/depth timestamp drift, depth confidence, skipped fast-motion frames, and
  coverage are shown live in the scan HUD;
- Stop is blocked until the scan quality reaches the configured good threshold.

The server stores uploads under `data/` and exposes:

- `POST /upload` - receive a scan ZIP from the app
- `GET /status/<scanId>` - inspect processing state
- `GET /result/<scanId>` - download the generated result
- `GET /lab` - browser lab for repeatedly testing reconstruction changes
- `GET /api/runs/<scanId>` - list baseline and lab runs
- `POST /api/runs/<scanId>` - rerun the current reconstruction code against an existing scan

`/lab` defaults to the fixed review scan `20260626_102656`. Use **Run current
code** after changing `reconstruct_open3d.py`; each run is stored under
`data/runs/20260626_102656/<timestamp>/` so the output, metrics, and model preview
can be compared against the existing `data/results/20260626_102656` baseline.
New Unity scan packages also include a `quality` block in `manifest.json`; the
lab shows that score so scan-capture problems can be separated from server-side
reconstruction problems.

The lab's **Run profiles** button creates reconstruction candidates for the same
scan:

- `geometry`: geometry-only ARKit mesh cleanup.
- `clean_texture`: stricter texture baking profile.
- `safe`: light pruning, preserves more geometry.
- `balanced`: default pruning.
- `aggressive`: stronger pruning, removes more scraps and detail.
- `rtabmap`: for current Unity scan packages, uses the RTAB-Map-first capture
  folder for frame accounting but produces the visible map from the stable
  ARKit world mesh with keyframe texture projection. Legacy scans fall back to
  the RTAB-Map CLI comparison path when the tools are installed.

To run one profile from the terminal:

```bash
python3 reconstruct_open3d.py --scan data/scans/20260626_102656 --out data/runs/20260626_102656/manual_safe --profile safe
```

To compare RTAB-Map:

```bash
brew install rtabmap
python3 reconstruct_open3d.py --scan data/scans/20260626_102656 --out data/runs/20260626_102656/manual_rtabmap --profile rtabmap
```

The `rtabmap` profile writes a converted dataset under
`<out>/rtabmap_dataset/freiburg3_memoanchor/` for inspection when it needs the
CLI path. New Unity scan packages also include an RTAB-Map-first capture folder:

```text
rtabmap_dataset/freiburg3_memoanchor/
  rgb_sync/
  depth_raw/
  confidence_raw/
  rgb.txt
  depth_raw.txt
  associations.txt
  groundtruth.txt
  memoanchor_rtabmap_calib.yaml
  memoanchor_rtabmap_dataset.json
```

The server uses this folder first when present, but the visible result is built
from ARKit's stable world mesh. The raw RGB-D frames remain packaged for
inspection and future native RTAB-Map integration. This avoids treating
`rtabmap-rgbd_dataset` output as valid when the CLI ignores most frames because
no odometry was supplied. Older scan packages still fall back to the legacy
manifest-to-RTAB-Map converter, but that path now fails at the odometry stage if
too many frames are ignored.

## Result Quality

The worker now builds two Open3D outputs when possible:

- `result_raw_colored.ply`: ARKit raw mesh with keyframe RGB projected onto vertices.
- `result_tsdf.ply`: RGB-D TSDF fusion mesh.
- `result.obj` + `result.mtl` + `result_texture.png`: preferred textured output
  when keyframe texture baking succeeds.
- `result.ply`: legacy result for app preview and older viewers.

The worker also does conservative local hole filling. It looks for closed
boundary loops on the measured mesh and fills only small, mostly planar holes.
It does not generate a room-sized cube or synthetic wall shell.

After cleanup, the worker prunes geometry-only mesh artifacts before texture
work: triangles outside scan bounds, long skinny torn triangles, and small
disconnected components. This keeps the large room surfaces while removing
floating scraps and edge spikes.

If the Unity scan package includes ARFoundation plane detections, the worker
adds those bounded floor/wall/table polygons as plane-guided support after
measured-mesh cleanup. This helps larger planar gaps close without inventing
surfaces outside the scanned bounds.

The server works without Open3D, but only copies `raw_mesh.obj` as a fallback
result. If `GET /status/<scanId>` reports `result.obj`, the fallback path was used.

For better scans, move around the room slowly, keep the camera pointed at surfaces
from multiple angles, and avoid stopping immediately after AR tracking starts.
The app captures more high-resolution RGB keyframes now, so uploads are larger
but the projected color map is much more useful.

The longer-term production-quality target remains:

```text
RGB-D package -> cleaned measured mesh -> local hole filling -> color projection / texture baking -> result mesh
```
