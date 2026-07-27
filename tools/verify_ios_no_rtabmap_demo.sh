#!/usr/bin/env bash
set -euo pipefail

BUILD_DIR="${1:-ios_Build}"
PBXPROJ="$BUILD_DIR/Unity-iPhone.xcodeproj/project.pbxproj"

if [[ ! -f "$PBXPROJ" ]]; then
  echo "FAIL: project file not found: $PBXPROJ" >&2
  exit 1
fi

forbidden=(
  "RTABMapApp.cpp"
  "NativeWrapper.cpp"
  "CameraMobile.cpp"
  "scene.cpp"
  "background_renderer.cc"
  "point_cloud_drawable.cpp"
  "graph_drawable.cpp"
  "tango-gl"
  "librtabmap_core"
  "librtabmap_utilite"
  "libpcl_"
  "libg2o_"
  "libgtsam"
  "vtk.framework"
)

status=0
for pattern in "${forbidden[@]}"; do
  if grep -q "$pattern" "$PBXPROJ"; then
    echo "FAIL: found forbidden RTAB-Map demo/link entry: $pattern"
    status=1
  else
    echo "PASS: not found: $pattern"
  fi
done

if grep -q "OpenGLES.framework" "$PBXPROJ"; then
  echo "WARN: OpenGLES.framework is present. Verify it is required by non-RTAB Unity/plugin code."
fi

if [[ "$status" -eq 0 ]]; then
  echo "PASS: no RTAB-Map demo sources/static libraries found in $PBXPROJ"
fi

exit "$status"
