# MemoAnchor RGB-D Recorder Phase A/B

This phase intentionally does not build RTAB-Map core mapping, database processing, loop closure, mesh reconstruction, or textured mesh export.

## Build Rule

Delete the old generated Xcode project before testing this phase:

```bash
rm -rf ios_Build
```

Then build from Unity with:

```text
MemoAnchor > Build > iOS ARKit Mesh Scan
```

Use a fresh empty output directory. The old `ios_Build` may already contain RTAB-Map demo sources and static libraries from earlier experiments, so it is not a reliable source of truth.

## Xcode Project Check

After Unity creates a new iOS project, run:

```bash
tools/verify_ios_no_rtabmap_demo.sh ios_Build
```

These entries must not appear in `Unity-iPhone.xcodeproj/project.pbxproj`:

```text
RTABMapApp.cpp
NativeWrapper.cpp
CameraMobile.cpp
scene.cpp
background_renderer.cc
point_cloud_drawable.cpp
graph_drawable.cpp
tango-gl/*
librtabmap_core.a
librtabmap_utilite.a
PCL/g2o/GTSAM/VTK RTAB-Map dependencies
```

## Runtime Check

Expected runtime properties:

```text
No RTABMapApp.
No second ARSession.
No native access to ARSession.currentFrame.
Stable Unity AR Foundation RGB-D recorder.
```

The recorder writes datasets under:

```text
Application.persistentDataPath/RgbdRecorder/scan_YYYYMMDD_HHMMSS/
  session.json
  frames.jsonl
  rgb/
    000001.jpg
  depth/
    000001.bin
  confidence/
    000001.bin
```

## Dataset Validation

After pulling a dataset folder from the device, run:

```bash
python3 tools/validate_rgbd_dataset.py /path/to/scan_YYYYMMDD_HHMMSS
```

The validator writes:

```text
validation_summary.json
```

`WARN` means the dataset is readable but needs review. `FAIL` means a structural mismatch or missing data must be fixed before any Phase C mapping work.
