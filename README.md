# MemoAnchor

iPad(LiDAR)로 공간을 스캔하고, MemoAnchor ASP.NET Core API를 경유해 서버의
Python/Open3D 워커에서 3D mesh를 복원한 뒤 앱에서 결과를 확인하는 파이프라인입니다.

```text
iPad (Unity + ARKit)          ASP.NET Core API             Python/Open3D
─────────────────────         ─────────────────────         ─────────────
맵 메타데이터 생성       ───▶ 인증·맵 권한 확인
RGB-D ZIP 업로드         ───▶ 스트리밍 프록시          ───▶ TSDF 복원
상태/결과 조회           ───▶ 권한 확인 후 중계       ◀─── result.ply
앱 내 mesh preview       ◀─── PLY 스트리밍
```

## 현재 상태

검증된 offline/online reconstruction 파이프라인입니다.
실제 스캔 데이터(`scan_20260718_163137`, 301 frames)로 방 구조와 색상이 확인 가능한
mesh(181,727 vertices / 347,922 triangles, 약 20초)를 생성하는 것까지 확인했습니다.

| 영역 | 상태 |
| --- | --- |
| iPad RGB-D 데이터셋 기록 | 동작 |
| ZIP 패키징 + 서버 업로드 | 동작 |
| 서버 Open3D TSDF 복원 | 동작 |
| 브라우저 HTML 뷰어 | 동작 |
| iPad 앱 내 mesh preview | 동작 |
| RTAB-Map iOS native 통합 | **보류** (스캐폴딩만 존재, 현재 경로 아님) |

## 빠른 시작

```bash
# 1. Python 환경 (Open3D는 Python 3.10 권장)
python3.10 -m venv tools/reconstruction/.venv
tools/reconstruction/.venv/bin/python -m pip install -r tools/reconstruction/requirements.txt

# 2. ASP.NET 서버와 같은 호스트에서 내부 복원 워커 실행
tools/reconstruction/.venv/bin/python tools/reconstruction_server/server.py --host 127.0.0.1 --port 8765

# 3. 앱 로그인 → 스캔 탭에서 맵 정보 입력 → 스캔 시작 → 결과 확인
```

전체 절차는 **[docs/GUIDE.md](docs/GUIDE.md)** 를 참고하세요.

## 저장소 구조

```text
Assets/
  Scenes/ARKitMeshScanScene.unity        스캔 씬
  Scripts_donghyeon/ARKitMeshing/        스캔 컨트롤러 · RGB-D 레코더 · 업로드
  Editor/                                iOS 빌드 · 씬 빌더 에디터 스크립트
  Plugins/iOS/                           네이티브 플러그인 (RTAB-Map 스텁, 보류)
  Shaders/PreviewVertexColors.shader     vertex color preview 셰이더

tools/
  reconstruction/                        Open3D 오프라인 복원 파이프라인
    reconstruct_open3d_tsdf.py           TSDF 복원 메인
    inspect_rgbd_frame.py                단일 프레임 진단
    reconstruction_common.py             좌표 변환 · 데이터셋 로더
    viewer.html                          단독 실행형 웹 뷰어
  reconstruction_server/                 업로드 수신 + 복원 워커 + /viewer 서버
  validate_rgbd_dataset.py               데이터셋 무결성 검증
  rtabmap_ios/                           RTAB-Map iOS 연동 스캐폴딩 (보류)

docs/
  GUIDE.md                               셋업 · 실행 가이드
  reconstruction_end_to_end_summary.md   전체 검증 기록
  reconstruction_summary_short.md        요약 + 결과 이미지
```

## 요구 사항

| 항목 | 버전 |
| --- | --- |
| Unity | **6000.3.12f1** (프로젝트에 고정된 버전) |
| AR Foundation / ARKit XR Plugin | 5.2.0 — `packages-lock.json` 으로 자동 해석됨 |
| 기기 | LiDAR 탑재 iPad / iPhone, iOS 16+ |
| Mac | macOS + Xcode (iOS 빌드·서명) |
| 서버 Python | 3.10~3.12 (Open3D wheel 호환 버전) |

Unity 패키지는 `Packages/packages-lock.json` 이 함께 커밋되어 있으므로 프로젝트를 열면
자동으로 동일 버전이 설치됩니다. 별도로 받을 SDK는 없습니다.

## 환경 변수

실제 키는 커밋되지 않습니다. 예시 파일을 복사해서 사용하세요.

```bash
cp .env.example .env
cp Assets/StreamingAssets/gemini.env.example Assets/StreamingAssets/gemini.env
```

`.env` / `gemini.env` / `ImmersalSDKToken.asset` 은 `.gitignore` 로 제외되어 있습니다.

## 라이선스

MIT License
