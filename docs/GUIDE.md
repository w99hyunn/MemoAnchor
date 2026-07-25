# MemoAnchor 실행 가이드

iPad 스캔 → 서버 복원 → iPad/웹 확인까지의 전체 절차입니다.

- [0. 준비물](#0-준비물)
- [1. Python 복원 환경 설치](#1-python-복원-환경-설치)
- [2. 복원 서버 실행](#2-복원-서버-실행)
- [3. Unity 씬 설정](#3-unity-씬-설정)
- [4. iOS 빌드 및 설치](#4-ios-빌드-및-설치)
- [5. 스캔 → 업로드 → 확인](#5-스캔--업로드--확인)
- [6. 오프라인 복원 (서버 없이)](#6-오프라인-복원-서버-없이)
- [7. 데이터 위치 정리](#7-데이터-위치-정리)
- [8. 트러블슈팅](#8-트러블슈팅)

---

## 0. 준비물

| 항목 | 요구 사항 |
| --- | --- |
| 기기 | LiDAR 탑재 iPad / iPhone (iOS 16 이상) |
| Unity | 2021.3 LTS 이상, AR Foundation 5.x + ARKit XR Plugin |
| Mac | macOS + Xcode (iOS 빌드/서명) |
| Python | 3.10 (Open3D 호환 버전) |
| 네트워크 | iPad와 Mac이 **같은 Wi-Fi** 에 연결 |

> Open3D는 Python 3.13에서 설치되지 않는 경우가 많습니다. 3.10 가상환경을 따로 만드는 것을 권장합니다.

---

## 1. Python 복원 환경 설치

저장소 루트에서 실행합니다.

```bash
python3.10 -m venv tools/reconstruction/.venv
tools/reconstruction/.venv/bin/python -m pip install --upgrade pip
tools/reconstruction/.venv/bin/python -m pip install -r tools/reconstruction/requirements.txt
```

설치 확인:

```bash
tools/reconstruction/.venv/bin/python -c "import open3d; print(open3d.__version__)"
```

---

## 2. 복원 서버 실행

```bash
tools/reconstruction/.venv/bin/python tools/reconstruction_server/server.py --host 0.0.0.0 --port 8765
```

`--host 0.0.0.0` 이어야 iPad에서 접근할 수 있습니다.

### Mac의 LAN IP 확인

```bash
ipconfig getifaddr en0   # Wi-Fi
```

예: `192.168.0.42`

### 엔드포인트

| 메서드 | 경로 | 용도 |
| --- | --- | --- |
| `POST` | `/upload` | iPad가 스캔 ZIP 업로드 |
| `GET` | `/status/<scanId>` | 복원 진행 상태 polling |
| `GET` | `/result/<scanId>` | 결과 `result.ply` 다운로드 (앱 preview용) |
| `GET` | `/viewer?scan=<scanId>` | 브라우저 3D 뷰어 |

확인용 주소:

```text
Mac         http://127.0.0.1:8765/viewer?scan=<scanId>
같은 Wi-Fi   http://<mac-lan-ip>:8765/viewer?scan=<scanId>
```

---

## 3. Unity 씬 설정

1. Unity에서 프로젝트를 열고 `Assets/Scenes/ARKitMeshScanScene.unity` 를 엽니다.
2. Hierarchy에서 `ARKitMeshScanController` 가 붙은 오브젝트를 선택합니다.
3. Inspector에서 아래 값을 설정합니다.

### RGB-D Recorder

| 필드 | 권장값 | 설명 |
| --- | --- | --- |
| `Record Rgbd Dataset On Scan` | `true` | 스캔 중 RGB/Depth/Confidence/Pose 기록 |
| `Rgbd Recorder Frame Interval Seconds` | `0.2` | 프레임 저장 간격 (초) |
| `Rgbd Recorder Max Queue` | `4` | 디스크 쓰기 큐 크기 |

### 업로드

| 필드 | 권장값 | 설명 |
| --- | --- | --- |
| `Package Reconstruction Scan On Stop` | `true` | 스캔 종료 시 ZIP 패키징 |
| `Upload Reconstruction Package On Stop` | `true` | 종료 시 자동 업로드 |
| `Reconstruction Upload Url` | `http://<mac-lan-ip>:8765/upload` | **본인 Mac IP로 직접 입력** |

> `Reconstruction Upload Url` 은 머신마다 다르므로 저장소에는 비워둔 채로 커밋되어 있습니다.
> 클론한 뒤 반드시 본인 Mac의 LAN IP를 넣어야 업로드가 동작합니다.

### 스캔 품질 게이트 (선택)

`Require Minimum Quality To Stop` 이 `true` 이면 아래 기준을 만족해야 스캔을 종료할 수 있습니다.
데모 중 스캔이 안 끝나면 이 값을 `false` 로 두거나 `Minimum Stop Quality Score` 를 낮추세요.

| 기준 | 기본값 |
| --- | --- |
| 최소 품질 점수 | 78 |
| 권장 스캔 시간 | 75초 |
| 권장 키프레임 수 | 90 |
| 권장 카메라 이동 거리 | 3m |
| 권장 mesh 삼각형 수 | 75,000 |

---

## 4. iOS 빌드 및 설치

### Unity에서 빌드

에디터 메뉴에서 **MemoAnchor → Build → iOS ARKit Mesh Scan** 을 실행합니다.
출력 경로는 `ios_Build/` 입니다 (git 제외됨, 빌드 시 기존 폴더는 삭제 후 재생성).

CLI로 빌드하려면:

```bash
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -executeMethod MemoAnchorIOSBuild.BuildARKitMeshScan
```

### Xcode

1. `ios_Build/Unity-iPhone.xcodeproj` 를 엽니다.
2. Signing & Capabilities에서 본인 Team을 지정합니다.
3. iPad를 연결하고 Run합니다.

### HTTP 업로드 허용 (ATS)

업로드 주소가 `https` 가 아닌 `http` 이므로, Xcode의 `Info.plist` 에 예외가 필요합니다.

```xml
<key>NSAppTransportSecurity</key>
<dict>
  <key>NSAllowsLocalNetworking</key>
  <true/>
</dict>
```

또한 iOS는 최초 접속 시 **로컬 네트워크 접근 권한** 팝업을 띄웁니다. 반드시 허용하세요.

### 빌드에 RTAB-Map이 섞이지 않았는지 검증 (선택)

```bash
bash tools/verify_ios_no_rtabmap_demo.sh ios_Build
```

---

## 5. 스캔 → 업로드 → 확인

1. Mac에서 서버를 켜둡니다 (2단계).
2. iPad에서 앱을 실행하고 스캔을 시작합니다.
3. 공간을 천천히, 겹치게 훑습니다.
   - 빠르게 휘두르면 키프레임이 품질 기준에서 걸러집니다.
   - 권장: 75초 이상, 3m 이상 이동.
4. 스캔을 종료하면 앱이 자동으로 ZIP을 만들어 `/upload` 로 전송합니다.
5. 앱이 `/status/<scanId>` 를 polling하고, 완료되면 `/result/<scanId>` 를 받아 mesh를 preview합니다.
6. Mac 브라우저에서도 동시에 확인 가능합니다.

```text
http://127.0.0.1:8765/viewer?scan=<scanId>
```

서버 콘솔에 사용된 프레임 수와 제외된 프레임 수가 출력됩니다.
검증 스캔 기준으로 301 frames 사용 / 0 frames 제외 / 약 20초 소요였습니다.

---

## 6. 오프라인 복원 (서버 없이)

기기에서 데이터셋을 직접 꺼내 로컬에서 복원하는 경로입니다. 파라미터 튜닝할 때 사용합니다.

### 6-1. 기기에서 데이터셋 추출

Xcode → Window → Devices and Simulators → 앱 선택 → **Download Container**.
받은 `.xcappdata` 안에 데이터셋이 있습니다.

```text
<앱이름>.xcappdata/AppData/Documents/RgbdRecorder/scan_YYYYMMDD_HHMMSS/
```

데이터셋 구조:

```text
scan_YYYYMMDD_HHMMSS/
  session.json
  frames.jsonl
  rgb/000001.jpg          JPEG 1440x1080
  depth/000001.bin        float32 little-endian, meter, 256x192
  confidence/000001.bin   uint8 (0=low, 1=medium, 2=high)
```

### 6-2. 데이터셋 검증

```bash
tools/reconstruction/.venv/bin/python tools/validate_rgbd_dataset.py \
  "/path/to/scan_YYYYMMDD_HHMMSS"
```

프레임 수 불일치, 타임스탬프 어긋남, 깨진 depth 파일을 잡아냅니다.

### 6-3. 단일 프레임 진단

방향(orientation)이 맞는지 먼저 눈으로 확인합니다.

```bash
tools/reconstruction/.venv/bin/python tools/reconstruction/inspect_rgbd_frame.py \
  "/path/to/scan_YYYYMMDD_HHMMSS" \
  --frame-id 1 \
  --output-dir reconstruction_output/frame_000001
```

`orientation_contact_sheet.png` 와 `depth_edge_overlay_on_rgb.png` 에서
depth 경계가 RGB 물체 경계와 맞는 후보를 고릅니다.

### 6-4. TSDF 복원

**geometry only (기본, 가장 안전)**

```bash
tools/reconstruction/.venv/bin/python tools/reconstruction/reconstruct_open3d_tsdf.py \
  "/path/to/scan_YYYYMMDD_HHMMSS" \
  --output-dir reconstruction_output/full_scan \
  --depth-transform rotate_270 \
  --voxel-size 0.02 \
  --sdf-trunc 0.06 \
  --depth-min 0.15 \
  --depth-max 5.0 \
  --frame-step 1
```

**색상 포함 (검증된 조합)**

```bash
tools/reconstruction/.venv/bin/python tools/reconstruction/reconstruct_open3d_tsdf.py \
  "/path/to/scan_YYYYMMDD_HHMMSS" \
  --output-dir reconstruction_output/full_scan_color \
  --depth-transform rotate_270 \
  --color-transform rotate_90 \
  --assume-registered-color
```

RGB는 `MirrorY` 가 적용되어 저장되고 depth는 원본 그대로 저장되기 때문에,
색상 정합은 자동으로 보장되지 않습니다. 그래서 색상 TSDF는 `--assume-registered-color` 를
명시했을 때만 동작합니다.

**방향 후보 전수 비교**

```bash
tools/reconstruction/.venv/bin/python tools/reconstruction/reconstruct_open3d_tsdf.py \
  "/path/to/scan_YYYYMMDD_HHMMSS" \
  --output-dir reconstruction_output/sweep \
  --frame-step 2 --sweep
```

`--depth-transform` 후보: `as_saved`, `flip_left_right`, `flip_top_bottom`, `rotate_90`, `rotate_180`, `rotate_270`

### 6-5. 결과 확인

주요 산출물:

```text
fused_mesh_clean.ply       ← 최종 mesh
fused_mesh_clean.obj
fused_point_cloud.ply
camera_trajectory.ply
reconstruction_report.json
used_frames.jsonl / rejected_frames.jsonl
preview*.png
```

단독 뷰어로 보려면 결과 폴더에 뷰어를 복사한 뒤 정적 서버를 띄웁니다.

```bash
cp tools/reconstruction/viewer.html reconstruction_output/
python3 -m http.server 8899 --directory reconstruction_output
# http://127.0.0.1:8899/viewer.html
```

---

## 7. 데이터 위치 정리

| 위치 | 내용 | git |
| --- | --- | --- |
| iPad `Documents/RgbdRecorder/scan_<id>/` | 원본 RGB-D 데이터셋 | — |
| `tools/reconstruction_server/data/scans/<id>/` | 서버가 받은 ZIP 압축 해제본 | 제외 |
| `tools/reconstruction_server/data/results/<id>/result.ply` | 서버 복원 결과 | 제외 |
| `reconstruction_output/` | 오프라인 복원 결과 | 제외 |
| `ios_Build/` | Unity가 생성한 Xcode 프로젝트 | 제외 |

용량이 큰 산출물은 전부 `.gitignore` 로 제외되어 있습니다. 저장소에는 **코드와 문서만** 올라갑니다.

---

## 8. 트러블슈팅

### 업로드가 안 됩니다

- 서버를 `--host 0.0.0.0` 으로 띄웠는지 확인.
- iPad와 Mac이 같은 Wi-Fi인지 확인. 게스트 네트워크는 기기 간 통신이 막혀 있는 경우가 많습니다.
- Mac 방화벽에서 Python 수신 허용.
- iPad 사파리에서 `http://<mac-lan-ip>:8765/viewer` 가 열리는지 먼저 확인.
- iOS 로컬 네트워크 권한 팝업을 거절했다면: 설정 → 앱 → 로컬 네트워크 재허용.
- Inspector의 `Reconstruction Upload Url` 이 비어 있지 않은지 확인 (클론 직후에는 비어 있습니다).

### mesh가 뒤틀리거나 벽이 겹쳐 나옵니다

depth orientation이 틀렸을 가능성이 큽니다. `--sweep` 으로 후보를 전부 뽑아 비교하세요.
검증된 데이터셋에서는 `rotate_270` 이 정답이었습니다.

### 색이 엉뚱한 면에 입혀집니다

`--color-transform` 값을 바꿔가며 확인하세요. 검증된 조합은 `depth rotate_270 + color rotate_90` 입니다.
정합이 확실하지 않으면 geometry-only로 먼저 구조를 확인하는 편이 낫습니다.

### 제외된 프레임(rejected)이 많습니다

`rejected_frames.jsonl` 에 사유가 기록됩니다. 흔한 원인:

- 스캔 초반 `SessionInitializing` 상태 프레임
- 카메라를 너무 빨리 움직여 RGB/Depth 타임스탬프가 어긋난 프레임
- depth confidence가 낮은 프레임

천천히, 겹치게 다시 스캔하면 개선됩니다.

### Open3D 설치가 실패합니다

Python 3.13에서는 휠이 없습니다. 3.10 가상환경을 사용하세요 (1단계 참고).

### 서버는 완료됐는데 앱에 mesh가 안 뜹니다

브라우저에서 `/viewer?scan=<scanId>` 가 정상인지 먼저 확인하세요.
뷰어에서 보인다면 서버는 정상이고, 앱의 다운로드/파싱 쪽 문제입니다.
앱은 binary little-endian PLY만 파싱합니다.

---

## 참고: RTAB-Map iOS 통합에 대해

`tools/rtabmap_ios/` 와 `Assets/Plugins/iOS/MemoAnchorRtabmapUnity.mm` 는
RTAB-Map을 앱 내부에서 직접 돌리려던 시도의 스캐폴딩입니다.

iOS 빌드 복잡도와 ARSession 소유권 충돌 리스크 때문에 **현재 경로가 아닙니다.**
플러그인은 RTAB-Map 헤더가 링크되지 않은 상태에서는 스텁으로 컴파일되므로,
그대로 두어도 빌드에 영향을 주지 않습니다.

앱 내부 실시간 복원이 필요해지면 그때 다시 꺼내면 됩니다.
자세한 내용은 [tools/rtabmap_ios/README.md](../tools/rtabmap_ios/README.md) 참고.
