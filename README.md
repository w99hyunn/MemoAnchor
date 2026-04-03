\# Construction VPS



Unity 기반 건설 현장용 VPS(Visual Positioning System) 애플리케이션



\## 🎯 주요 기능



\- 📍 Immersal SDK를 활용한 실시간 6-DoF 포즈 추정

\- 🏗️ 건설 현장 커스텀 맵 생성 및 로컬라이제이션

\- 📏 AR 기반 거리 측정

\- 📸 위치 태그가 포함된 현장 사진 촬영



\## 🔧 개발 환경



\- Unity 2021.3 LTS 이상

\- Immersal SDK 1.18+

\- AR Foundation 5.0+

\- Android API Level 24+



\## 📦 설치 방법



\### 1. 레포 클론

```bash

git clone https://github.com/네아이디/construction-vps.git

cd construction-vps

```



\### 2. Unity에서 열기

\- Unity Hub 열기

\- \[Add] > 클론한 폴더 선택

\- 프로젝트 열기



\### 3. Immersal SDK 설치

1\. https://developers.immersal.com/ 에서 SDK 다운로드

2\. Assets > Import Package > Custom Package

3\. 다운로드한 .unitypackage 선택 후 Import



\### 4. Immersal 토큰 설정

1\. Immersal 계정 생성 및 API 토큰 발급

2\. Unity: Window > Immersal SDK > Settings

3\. Token 입력



\## 🚀 사용 방법



\### 맵 생성

1\. Immersal Mapper 앱 설치 (Google Play/App Store)

2\. 건설 현장 스캔

3\. 클라우드에 업로드

4\. Unity에서 맵 ID로 로드



\### 앱 빌드

1\. File > Build Settings

2\. Platform: Android 선택

3\. \[Build And Run]



\## 📁 프로젝트 구조

```

Assets/

├── Scenes/           # Unity 씬 파일

│   └── MainScene.unity

├── Scripts/          # C# 스크립트

│   └── VPSManager.cs

├── Prefabs/          # 프리팹

└── Resources/        # 리소스 파일

```



\## 🔑 환경 변수



`.gitignore`에 의해 제외된 민감 파일:

\- `ImmersalSDKToken.asset` - Immersal API 토큰




\## 📝 라이선스



MIT License



\## 🙏 Acknowledgments



\- Immersal SDK

\- Unity AR Foundation

