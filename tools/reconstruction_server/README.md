# MemoAnchor Reconstruction Server

Local receiver and reconstruction worker for ARKit scan packages.

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

The server stores uploads under `data/` and exposes:

- `POST /upload` - receive a scan ZIP from the app
- `GET /status/<scanId>` - inspect processing state
- `GET /result/<scanId>` - download the generated result

## Result Quality

The worker now builds two Open3D outputs when possible:

- `result_raw_colored.ply`: ARKit raw mesh with keyframe RGB projected onto vertices.
- `result_tsdf.ply`: RGB-D TSDF fusion mesh.
- `result.ply`: the result selected for app preview. For MemoAnchor this prefers
  the colored ARKit mesh because it keeps a usable mesh for notes and is often
  more recognizable than low-resolution TSDF.

The worker also does conservative local hole filling. It looks for closed
boundary loops on the measured mesh and fills only small, mostly planar holes.
It does not generate a room-sized cube or synthetic wall shell.

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
