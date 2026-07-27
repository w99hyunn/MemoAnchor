#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
VENDOR_DIR="${ROOT_DIR}/tools/rtabmap_ios/vendor"
RTABMAP_DIR="${VENDOR_DIR}/rtabmap"
RTABMAP_REPO="${RTABMAP_REPO:-https://github.com/introlab/rtabmap.git}"
RTABMAP_REF="${RTABMAP_REF:-master}"

mkdir -p "${VENDOR_DIR}"

if [ -d "${RTABMAP_DIR}/.git" ]; then
  git -C "${RTABMAP_DIR}" fetch --depth 1 origin "${RTABMAP_REF}"
  git -C "${RTABMAP_DIR}" checkout FETCH_HEAD
else
  git clone --depth 1 --branch "${RTABMAP_REF}" "${RTABMAP_REPO}" "${RTABMAP_DIR}"
fi

echo "RTAB-Map iOS source ready:"
echo "${RTABMAP_DIR}/app/ios"
