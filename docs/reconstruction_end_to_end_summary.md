# MemoAnchor RGB-D Reconstruction End-to-End 정리

작성일: 2026-07-19  
현재 결론: **iPad는 RGB-D 스캔/업로드, Mac/server는 Open3D TSDF reconstruction, iPad와 Web viewer에서 결과 확인** 구조로 진행한다.

---

## 1. 최종 판단

기존 AliceVision/RTAB-Map native 직접 연결 방식은 iOS 빌드/ARSession 생명주기/텍스처 정합 문제 때문에 리스크가 컸다.

검증 결과, 현재 가장 안정적인 방식은 다음이다.

```text
iPad Unity app
→ ARKit RGB-D dataset 저장
→ ZIP으로 Mac/server 업로드
→ server에서 Open3D TSDF reconstruction
→ result.ply 생성
→ iPad 앱이 result.ply 다운로드 후 preview
→ Mac/server에서는 HTML viewer로 확인
```

이 방식은 실제 dataset `scan_20260718_163137` 기준으로 **방 구조와 텍스처가 확인 가능한 수준의 결과**를 만들었다.

---

## 2. 전체 구조

```text
[iPad / Unity]
  ARKit camera RGB
  ARKit environment depth
  ARKit confidence
  AR camera pose
        |
        v
  RgbdRecorder/scan_<scanId>
        |
        v
  ZIP package
        |
        v
POST http://<mac-ip>:8765/upload
        |
        v
[Mac / Reconstruction Server]
  ZIP extract
  RGB-D dataset detect
  Open3D TSDF fusion
  result.ply export
        |
        +--> GET /viewer?scan=<scanId>  Web 확인
        |
        +--> GET /result/<scanId>       iPad 다운로드
```

중요한 점:

- 서버가 결과를 직접 iPad로 push하는 구조는 아니다.
- iPad가 서버에 업로드한 뒤 `/status/<scanId>`를 polling한다.
- 서버가 `done`을 반환하면 iPad가 `/result/<scanId>`를 다운로드한다.
- 앱은 다운로드한 `result.ply`를 Unity Mesh로 파싱해서 preview한다.

---

## 3. 검증한 Dataset

검증 dataset:

```text
<repo-root>/com.MemoAnchor.MemoAR 2026-07-19 01:34.56.194.xcappdata/AppData/Documents/RgbdRecorder/scan_20260718_163137
```

검증 결과:

```text
frames: 305
SessionTracking frames: 301
SessionInitializing frames: 4
rgb files: 305
depth files: 305
confidence files: 305
timestamp diff: 0ms
valid depth ratio: 거의 100%
```

---

## 4. Dataset Format

저장 구조:

```text
scan_YYYYMMDD_HHMMSS/
  session.json
  frames.jsonl
  rgb/000001.jpg
  depth/000001.bin
  confidence/000001.bin
```

확인된 format:

| 항목 | 값 |
|---|---|
| RGB | JPEG, 1440x1080 |
| Depth | `DepthFloat32`, little-endian |
| Depth unit | meter |
| Depth resolution | 256x192 |
| Depth row stride | 1024 bytes |
| Confidence | `uint8`, `OneComponent8` |
| Confidence values | 0, 1, 2 |
| Pose | Unity camera-to-world |
| Matrix order | row-major, `m00,m01,m02,m03,...` |
| Quaternion order | x, y, z, w |

---

## 5. 핵심 문제와 해결

처음 결과가 찢어지고 뒤집혀 보였던 핵심 원인은 **raw ARKit depth plane 방향과 저장된 RGB/pose 기준이 그대로 맞지 않았기 때문**이다.

검증 결과:

```text
geometry depth transform: rotate_270
color transform: rotate_90
```

이 조합이 현재 dataset에서 가장 좋은 결과를 만들었다.

---

## 6. 단일 Frame 검증 이미지

단일 frame에서 RGB, confidence, raw depth orientation 후보를 비교했다.

![Orientation contact sheet](../reconstruction_output/frame_000001/orientation_contact_sheet.png)

원본 파일:

```text
reconstruction_output/frame_000001/orientation_contact_sheet.png
```

---

## 7. RGB-Depth Edge Alignment 검증

RGB 위에 depth discontinuity edge를 overlay해서 방향 후보를 비교했다.

![Depth edge overlay variants](../reconstruction_output/frame_000001/depth_edge_overlay_variants.png)

원본 파일:

```text
reconstruction_output/frame_000001/depth_edge_overlay_variants.png
```

결론:

- RGB-depth registration은 recorder metadata만으로 완전 확정할 수 없다.
- 따라서 geometry-only TSDF를 안정 기준으로 두고, colored TSDF는 후보로 사용한다.
- 현재 색상 후보는 실제 viewer에서 충분히 보기 좋은 수준이다.

---

## 8. Geometry-only TSDF 결과

Open3D TSDF를 geometry-only로 fuse한 top-down preview.

![Geometry TSDF preview](../reconstruction_output/full_scan_rotate270/preview.png)

원본 파일:

```text
reconstruction_output/full_scan_rotate270/preview.png
```

결과 수치:

```text
used frames: 301
rejected frames: 0
vertices: 181,727
triangles: 347,922
bounds: 5.34m x 2.40m x 5.10m
```

---

## 9. Colored TSDF 결과

색상 포함 후보 결과. 현재 가장 보기 좋은 결과는:

```text
depth transform: rotate_270
color transform: rotate_90
```

![Colored TSDF top-down preview](../reconstruction_output/full_scan_rotate270_color90_candidate/preview_color_topdown.png)

원본 파일:

```text
reconstruction_output/full_scan_rotate270_color90_candidate/preview_color_topdown.png
```

주요 결과 파일:

```text
reconstruction_output/full_scan_rotate270_color90_candidate/fused_mesh_clean.ply
reconstruction_output/full_scan_rotate270_color90_candidate/fused_point_cloud.ply
```

---

## 10. 실제 Server Smoke Test 결과

서버 worker를 실제 dataset으로 테스트했다.

결과 위치:

```text
tools/reconstruction_server/data/results/server_rgbd_smoke_20260718_163137/
```

생성 파일:

```text
result.ply
result_geometry.ply
result_point_cloud.ply
preview.png
reconstruction_report.json
trajectory_report.json
used_frames.jsonl
rejected_frames.jsonl
```

결과 수치:

```text
pipeline: open3d_rgbd_tsdf
usedFrames: 301
rejectedFrames: 0
meshVertices: 181,727
meshTriangles: 347,922
processingTimeSeconds: 약 20.6초
```

---

## 11. 수정/추가한 주요 코드

### Unity iPad App

파일:

```text
Assets/Scripts_donghyeon/ARKitMeshing/ARKitMeshScanController.cs
```

주요 작업:

- Stop scan 시 reconstruction ZIP 생성
- 기존 keyframe/mesh package 유지
- `RgbdRecorder` dataset을 ZIP 내부 `rgbd_dataset/`로 복사
- 서버 업로드 `POST /upload`
- `/status/<scanId>` polling
- `/result/<scanId>` 다운로드
- binary little-endian PLY를 Unity Mesh로 파싱해 preview

### Offline Reconstruction Tool

폴더:

```text
tools/reconstruction/
```

주요 파일:

```text
reconstruction_common.py
inspect_rgbd_frame.py
reconstruct_open3d_tsdf.py
requirements.txt
README.md
```

주요 작업:

- RGB-D schema parsing
- depth/confidence binary reader
- frame inspector
- trajectory inspector
- Unity camera-to-world → Open3D extrinsic 변환
- Open3D TSDF fusion
- orientation transform option
- geometry-only / colored candidate output
- preview image 생성

### Reconstruction Server

파일:

```text
tools/reconstruction_server/server.py
```

주요 작업:

- `POST /upload`로 ZIP 수신
- ZIP extract
- ZIP 내부 RGB-D dataset 자동 탐색
- Open3D TSDF worker 실행
- `result.ply` 생성
- `/status/<scanId>` 응답
- `/result/<scanId>` 다운로드 제공
- `/viewer?scan=<scanId>` Web viewer 제공

---

## 12. 서버 실행 방법

repo root에서 실행:

```bash
cd <repo-root>

tools/reconstruction/.venv/bin/python tools/reconstruction_server/server.py \
  --host 0.0.0.0 \
  --port 8765
```

현재 검증 시 Mac LAN IP:

```text
<mac-lan-ip>
```

iPad 앱 upload URL:

```text
http://<mac-lan-ip>:8765/upload
```

Mac에서 viewer:

```text
http://127.0.0.1:8765/viewer?scan=<scanId>
```

iPad 또는 같은 Wi-Fi 기기에서 viewer:

```text
http://<mac-lan-ip>:8765/viewer?scan=<scanId>
```

---

## 13. 서버 API

### Upload

```http
POST /upload
Content-Type: application/zip
X-MemoAnchor-Scan-Id: <scanId>
```

### Status

```http
GET /status/<scanId>
```

응답 예:

```json
{
  "state": "done",
  "message": "Open3D RGB-D reconstruction complete",
  "resultFile": "result.ply",
  "viewerUrl": "/viewer?scan=<scanId>",
  "pipeline": "open3d_rgbd_tsdf"
}
```

### Result

```http
GET /result/<scanId>
```

응답:

```text
result.ply
```

### Viewer

```http
GET /viewer?scan=<scanId>
```

---

## 14. 현재 결과 확인용 URL

Smoke test scan:

```text
server_rgbd_smoke_20260718_163137
```

Mac:

```text
http://127.0.0.1:8765/viewer?scan=server_rgbd_smoke_20260718_163137
```

같은 Wi-Fi 기기:

```text
http://<mac-lan-ip>:8765/viewer?scan=server_rgbd_smoke_20260718_163137
```

---

## 15. 현재 방식의 장점

- iPad에서 무거운 reconstruction을 하지 않아도 된다.
- iOS native RTAB-Map linking 문제를 피한다.
- ARKit pose와 RGB-D frame을 그대로 활용한다.
- 서버에서 알고리즘을 빠르게 교체/튜닝할 수 있다.
- Mac browser viewer와 iPad preview를 동시에 지원한다.
- 실패 시 `reconstruction_report.json`, `used_frames.jsonl`, `rejected_frames.jsonl`로 원인 추적 가능하다.

---

## 16. 남은 리파인 작업

필수:

- server 실행/종료 스크립트 정리
- iPad UI에 업로드 진행률/처리 상태를 더 명확히 표시
- scanId를 사용자가 쉽게 확인할 수 있게 표시
- viewer URL을 status에 포함해 로그에서 바로 확인
- 오래된 result 정리 정책 추가

품질 개선:

- RGB-depth registration을 recorder 단계에서 더 명확히 저장
- `rotate_270`, `rotate_90` 보정을 metadata 기반으로 자동 결정
- mesh simplification 옵션 추가
- floor/wall cleanup 추가
- hole filling 옵션 추가
- iPad preview에서 large mesh 최적화

나중에 검토:

- RTAB-Map database/loop closure
- 앱 내부 native reconstruction
- cloud server 배포

---

## 17. 결론

현재 방식은 제품 흐름으로 정리할 수 있는 수준이다.

최종 방향:

```text
RTAB-Map native 직접 연결 X
AliceVision X
Open3D RGB-D TSDF server pipeline O
```

지금부터는 reconstruction 알고리즘을 새로 찾는 단계가 아니라, **업로드/처리/다운로드/viewer UX를 다듬는 단계**로 넘어가면 된다.

