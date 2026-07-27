# MemoAnchor Reconstruction Process

The reconstruction HTTP process is an on-demand child process of the MemoAnchor ASP.NET Core server. It is not installed as a separate systemd service.

## Runtime Flow

```text
Unity iOS app
  -> authenticated reconstruction request
ASP.NET Core
  -> checks whether 127.0.0.1:8765 is ready
  -> starts Reconstruction/server.py once when needed
  -> concurrent requests reuse the same process
Python reconstruction process
  -> receives the ZIP and runs Open3D/RTAB-Map work
  -> remains alive while requests or reconstruction jobs are active
  -> exits after 60 seconds with no requests and no active jobs
```

The Python process stores uploads, extracted scans, status, and results under its own `data/` directory. Process exit does not remove those files, so ASP.NET Core can start a new process later and serve an existing result.
If ASP.NET Core or the host interrupts a `queued` or `processing` job, the next on-demand Python start resumes it from the extracted scan data.

## Linux Layout

Deploy the ASP.NET Core publish output to:

```text
/home/ubuntu/MemoAnchor/
```

The publish script includes the Python files under:

```text
/home/ubuntu/MemoAnchor/Reconstruction/
  server.py
  reconstruct_open3d.py
  reconstruct_open3d_tsdf.py
  reconstruction_common.py
  inspect_rgbd_frame.py
  requirements.txt
  .venv/
  data/
```

Create the environment once on the Linux host:

```bash
cd /home/ubuntu/MemoAnchor/Reconstruction
sudo apt-get install -y python3-venv libgl1 libgomp1
python3 -m venv .venv
.venv/bin/pip install -r requirements.txt
```

The ASP.NET Core service user must be able to execute `.venv/bin/python` and write to `Reconstruction/data`.

The production settings are:

```json
{
  "Reconstruction": {
    "BaseUrl": "http://127.0.0.1:8765",
    "WorkingDirectory": "/home/ubuntu/MemoAnchor/Reconstruction",
    "PythonExecutable": ".venv/bin/python",
    "ServerScriptPath": "server.py",
    "IdleTimeoutSeconds": 60,
    "StartupTimeoutSeconds": 20
  }
}
```

Disable the former always-on unit if it is still installed:

```bash
sudo systemctl disable --now memoanchor-reconstruction.service
sudo systemctl daemon-reload
```

Only the ASP.NET Core `memoanchor.service` remains enabled.
If `/home/ubuntu/MemoAnchorReconstruction/data` contains existing scans, copy that `data` directory into `/home/ubuntu/MemoAnchor/Reconstruction/data` before retiring the old folder. Recreate `.venv` in the new location because virtual-environment scripts can contain the old absolute path.

## Manual Development Run

From the repository root:

```bash
tools/reconstruction/.venv/bin/python tools/reconstruction_server/server.py \
  --host 127.0.0.1 \
  --port 8765 \
  --idle-timeout-seconds 60
```

ASP.NET Core normally starts this command automatically. A manual process already listening on the configured URL is reused instead of starting a duplicate.

## Endpoints

- `POST /upload` receives a scan ZIP and starts reconstruction.
- `GET /status/<scanId>` returns queued, processing, done, or failed state.
- `GET /result/<scanId>` returns the generated mesh.
- `DELETE /scan/<scanId>` removes a map's reconstruction files.
- `GET /lab` opens the reconstruction review page.
- `GET /api/runs/<scanId>` lists lab runs.
- `POST /api/runs/<scanId>` starts lab profile runs.

The root endpoint reports `activeRequests`, `activeJobs`, and `idleTimeoutSeconds` for lifecycle inspection.

## RGB-D Defaults

```text
depth transform: rotate_270
color transform: rotate_90
voxel size: 0.02 m
sdf truncation: 0.06 m
depth range: 0.15-5.0 m
confidence threshold: >= 1
```

The app-facing result is a binary little-endian `result.ply` served through the authenticated ASP.NET Core reconstruction API.
