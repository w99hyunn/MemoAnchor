// 탭으로 AR 화면에 핀 생성, 맵별 파일(pins_{mapId}.json)형태로 저장/복원/삭제
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TabPinCreate : MonoBehaviour
{
    // ========== 싱글톤 인스턴스 ==========
    public static TabPinCreate Instance { get; private set; }

    [Header("Runtime Debug HUD (Mobile)")]
    [SerializeField] private TMP_Text debugHudText;
    [SerializeField] private bool showRuntimeDebugHud = true;


    [Header("Tooltip Debug (Temporary)")]
    [SerializeField] private bool debugForceTooltipInFrontOfCamera = false;

    [SerializeField] private float debugTooltipForwardMeters = 0.6f;


    [Header("Tooltip Visibility Fix")]
    [Tooltip("툴팁을 핀 위치에서 카메라쪽으로 살짝 당겨 Near Clip 잘림을 피함")]
    [SerializeField] private float tooltipPullTowardCamera = 0.12f;

    [Tooltip("툴팁을 약간 위로 올리는 오프셋(m)")]
    [SerializeField] private float tooltipUpOffset = 0.06f;

    [Tooltip("툴팁이 켜질 때 카메라를 바라보게 회전")]
    [SerializeField] private bool tooltipBillboardToCamera = true;

    [Header("Tooltip Render Fix (Layer/Sorting)")]
    [Tooltip("툴팁이 켜질 때 TooltipCanvas의 레이어를 핀 루트 레이어로 재귀 통일")]
    [SerializeField] private bool forceTooltipLayerToPinLayer = true;

    [Tooltip("툴팁 캔버스를 항상 위로 올려 가려짐을 줄임(overrideSorting)")]
    [SerializeField] private bool forceTooltipSorting = true;

    [Tooltip("forceTooltipSorting=true일 때 적용할 sortingOrder 값")]
    [SerializeField] private int tooltipSortingOrder = 5000;


    [Header("ARRaycastManager")]
    [Tooltip("ARRaycastManager 컴포넌트를 넣는 자리")]
    [SerializeField] private ARRaycastManager raycastManager;

    [Header("Pins Transform")]
    [Tooltip("Pins(핀 부모 오브젝트) Transform을 넣는 자리")]
    [SerializeField] private Transform pinsTransform;

    [Header("Pin Prefab")]
    [Tooltip("탭 시 만들 Pin Prefab을 넣는 자리")]
    [SerializeField] private GameObject pinPrefab;

    [Header("Memo UI (BottomBar Controller)")]
    [Tooltip("Canvas > SafeArea > MemoUI 에 붙어있는 MemoUIController를 넣는 자리")]
    [SerializeField] private MemoUIController memoUI;

    // 핀 선택(탭) 및 툴팁 거리표시
    [Header("AR Camera (Pin Select / Tooltip)")]
    [Tooltip("AR Camera를 넣는 자리 (핀 탭 선택 / 툴팁 거리표시에 사용)")]
    [SerializeField] private Camera arCamera;

    [Header("Pin Tap Select")]
    [Tooltip("Pin 프리팹에 설정한 Layer를 선택 (비워두면 모든 레이어에서 레이캐스트)")]
    [SerializeField] private LayerMask pinLayerMask;

    [Tooltip("핀 선택 Raycast 거리")]
    [SerializeField] private float pinRayDistance = 30f;

    // (추가) 아이콘/툴팁 표시 규칙을 TabPinCreate에서 직접 관리
    [Header("Icon / Tooltip Rule (Distance Based)")]
    [Tooltip("true면 TabPinCreate가 거리 기반으로 IconCanvas/TooltipCanvas를 직접 토글")]
    [SerializeField] private bool autoToggleIconTooltip = true;

    [Tooltip("카메라와 이 거리 이하면 툴팁(그리고 편집 가능), 멀면 아이콘")]
    [SerializeField] private float tooltipDistanceMeters = 1.2f;

    [Tooltip("핀 프리팹 내부에서 아이콘 캔버스 오브젝트 이름(기본: IconCanvas)")]
    [SerializeField] private string iconCanvasObjectName = "IconCanvas";

    [Tooltip("핀 프리팹 내부에서 툴팁 캔버스 오브젝트 이름(기본: TooltipCanvas)")]
    [SerializeField] private string tooltipCanvasObjectName = "TooltipCanvas";

    [Tooltip("TooltipCanvas 하위에서 타이틀 텍스트를 찾을 때, 우선으로 찾을 오브젝트 이름(비우면 첫 TMP_Text 사용)")]
    [SerializeField] private string tooltipTitleObjectName = ""; // 예: "TitleText"

    // ✅ (추가) PinVisualRefs 우선 사용 옵션
    [Header("Pin Visual Refs (Recommended)")]
    [Tooltip("PinVisualRefs가 프리팹에 있으면 이름 찾기보다 우선 사용")]
    [SerializeField] private bool preferPinVisualRefs = true;

    [Header("Icon Sprites (MemoType)")]
    [Tooltip("텍스트 메모용 아이콘 스프라이트")]
    [SerializeField] private Sprite textIconSprite;
    [Tooltip("이미지 메모용 아이콘 스프라이트")]
    [SerializeField] private Sprite imageIconSprite;
    [Tooltip("체크리스트 메모용 아이콘 스프라이트")]
    [SerializeField] private Sprite checklistIconSprite;
    [Tooltip("음성 메모용 아이콘 스프라이트")]
    [SerializeField] private Sprite voiceIconSprite;
    [Tooltip("IconCanvas 내부의 Icon 오브젝트 이름 (기본: Icon)")]
    [SerializeField] private string iconObjectName = "Icon";

    // (추가) “근처에는 새 핀 생성 금지”
    [Header("Create Block (Near Existing Pin)")]
    [Tooltip("이 거리(m) 안에 기존 핀이 있으면 새 핀 생성하지 않음")]
    [SerializeField] private float preventCreateNearDistance = 0.6f;

    // MemoListManager와 호환을 위해 파일명 고정 (Inspector에서 변경 불가)
    private const string pinFilePrefix = "immersal_pins_";  // MemoListManager와 통일 (변경 금지!)

    [Header("Map Id Source")]
    [Tooltip("mapId(PlayerPrefs의) 자동 세팅 사용 여부 체크")]
    [SerializeField] private bool useSelectedMapIdFromPrefs = true;

    [Tooltip("PlayerPrefs(임시 저장소)에서 읽을 키 이름")]
    [SerializeField] private string selectedMapIdPrefKey = "IMMERSAL_SELECTED_MAP_ID";

    [Header("Multimap Pin Number")]
    [Tooltip("여러 맵을 사용할 때 pin의 소속 번호(자동세팅 사용 시 무시될 수 있음)")]
    [SerializeField] private int pinMapId = 0;

    [Header("Pin Create Time Limit")]
    [Tooltip("Immersal TrackingAnalyzer 컴포넌트를 넣는 자리")]
    [SerializeField] private MonoBehaviour trackingAnalyzer;

    [SerializeField] private bool pinCreateTimeLimit = true;   // true면 정합 성공 전에는 핀 생성 금지
    [SerializeField] private int limitQuality = 1;             // 정합 성공 판단 퀄리티 기준

    [Header("Pin Restoration Timing")]
    [Tooltip("true면 앱 시작 시 정합된 뒤 복원, false면 바로 복원 (false 권장!)")]
    [SerializeField] private bool pinCreateAfterAlignment = false;  // false로 변경: 즉시 복원

    [Header("Debug")]
    [Tooltip("디버그 로그를 자세히 찍을지 여부 체크")]
    [SerializeField] private bool verboseDebug = true;  // 디버그용으로 기본값 true 설정


    // pin 1개 저장 정보 구조
    [Serializable]              // Unity JsonUtility가 이 타입을 JSON으로 변환 가능하게 하기 위함
    public class PinData
    {
        public int pinMapId;           // 핀이 속한 맵 ID
        public Vector3 localPos;       // pinsTransform 기준 로컬 좌표
        public Quaternion localRot;    // pinsTransform 기준 로컬 회전

        // 메모 데이터 저장
        public string id;              // 핀/메모 고유 ID
        public string title;           // 텍스트 메모 타이틀
        public string body;            // 텍스트 메모 내용

        // 아카이빙 시스템 추가
        public string status = "Active";           // MemoStatus를 string으로 저장
        public string createdAt;                   // 생성 시간
        public string updatedAt;                   // 수정 시간
        public string completedAt;                 // 완료 시간
        public string archivedAt;                  // 보관 시간
        public string archiveReason;               // 보관 사유
        public string assignee;                    // 담당자
        public bool isAssigned;                    // AssigneeRow Toggle 상태
        public int version = 1;                    // 버전

        // 추가 기능 (선택)
        public string priority = "Normal";         // 우선순위
        public string category;                    // 카테고리
        public string location;
        public string dueDate;                     // 마감 날짜 (yyyy-MM-dd 형식)
        public string dueTime;                     // 마감 시간 (HH:mm 형식)
        public int emergencyLevel = 0;             // 긴급도 (0=선택안함, 1~3=선택됨)

        // 이미지 메모 기능
        public List<string> imagePaths = new List<string>();  // 첨부 이미지 경로 목록
        public string memoType = "text";           // 메모 타입 ("text" 또는 "image")

        // 음성 메모 기능
        public List<string> voiceRecordingPaths = new List<string>();  // 녹음 파일 경로 목록
    }

    // pin 여러개 저장 정보 구조
    [Serializable]
    public class PinDB
    {
        public List<PinData> pins = new List<PinData>();
    }

    // raycast 결과 저장
    private static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>(); // 메모리/GC 줄이기 위함

    // 현재 맵의 핀 DB 메모리
    private PinDB pinDB = new PinDB();

    // 복원 여부 체크
    private bool restorationOnce = false;
    private int loadedMapId = int.MinValue;

    // 현재 맵에 맞는 핀 저장 파일 경로를 만들기
    private string pinSavePath => Path.Combine(Application.persistentDataPath, $"{pinFilePrefix}{pinMapId}.json");

    // 현재 선택된 핀 캐시 (편집 저장에 사용)
    private GameObject currentSelectedPin;

    // ✅ (추가) PinVisualRefs 캐시 (매 프레임 Find 비용 줄임)
    private readonly Dictionary<int, PinVisualRefs> pinVisualCache = new Dictionary<int, PinVisualRefs>();


    // mapId 결정/로드/복원
    private void Awake()
    {
        // ========== 싱글톤 초기화 ==========
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (verboseDebug) Debug.Log("[TabPinCreate] Singleton Instance created");
        }
        else
        {
            if (verboseDebug) Debug.LogWarning("[TabPinCreate] Duplicate instance destroyed");
            Destroy(gameObject);
            return;
        }

        // MapBrowser 씬의 UI Canvas 정리 (탭 입력 차단 방지)
        CleanupPreviousSceneUICanvases();

        // 카메라 자동 채우기(안 넣었을 때 대비)
        if (!arCamera) arCamera = Camera.main;

        if (verboseDebug)
        {
            Debug.Log($"[TabPinCreate] Awake() start");
            Debug.Log($"[TabPinCreate] arCamera={(arCamera ? arCamera.name : "null")}");
            Debug.Log($"[TabPinCreate] raycastManager={(raycastManager ? raycastManager.name : "null")}, pinsTransform={(pinsTransform ? pinsTransform.name : "null")}, pinPrefab={(pinPrefab ? pinPrefab.name : "null")}");
            Debug.Log($"[TabPinCreate] pinCreateTimeLimit={pinCreateTimeLimit}, limitQuality={limitQuality}, pinCreateAfterAlignment={pinCreateAfterAlignment}");
            Debug.Log($"[TabPinCreate] pinLayerMask.value={pinLayerMask.value}, pinRayDistance={pinRayDistance}");
            Debug.Log($"[TabPinCreate] preferPinVisualRefs={preferPinVisualRefs}");
        }

        // PlayerPrefs에서 mapId를 읽어 pinMapId를 결정
        ResolveMapId();
        // 현재 맵에 저장된 핀 목록을 메모리(pinDB)에 로드
        LoadPinsForCurrentMap();

        // 정합 후 복원 모드가 아니면 바로 복원
        if (!pinCreateAfterAlignment)
        {
            Debug.Log($"★★★ pinCreateAfterAlignment=false → 즉시 복원 시작 ★★★");
            if (verboseDebug) Debug.Log("[TabPinCreate] pinCreateAfterAlignment=false -> RestorePinsForThisMap() immediately");
            RestorePinsForThisMap();
            restorationOnce = true;
        }
        else
        {
            Debug.LogWarning($"★★★ pinCreateAfterAlignment=true → 정합 대기 중 (메모가 바로 표시되지 않음!) ★★★");
            Debug.LogWarning($"★★★ Inspector에서 'Pin Create After Alignment'를 false로 설정하세요! ★★★");
        }

        // 초기화 완료 후 중요 정보 출력
        Debug.Log($"★★★ [TabPinCreate] Awake 완료 ★★★");
        Debug.Log($"★★★ mapId={pinMapId}");
        Debug.Log($"★★★ savePath={pinSavePath}");
        Debug.Log($"★★★ persistentDataPath={Application.persistentDataPath}");
        Debug.Log($"★★★ pinsLoaded={(pinDB?.pins?.Count ?? 0)}");
        Debug.Log($"★★★ pinFilePrefix='{pinFilePrefix}' (고정값: MemoListManager 호환)");
        Debug.Log($"★★★ pinCreateAfterAlignment={pinCreateAfterAlignment} (false 권장!)");
        Debug.Log($"★★★ useSelectedMapIdFromPrefs={useSelectedMapIdFromPrefs}");
        Debug.Log($"★★★ selectedMapIdPrefKey='{selectedMapIdPrefKey}'");

        if (verboseDebug)
        {
            Debug.Log($"[TabPinCreate] Awake mapId={pinMapId}, savePath={pinSavePath}, pinsLoaded={(pinDB?.pins?.Count ?? 0)}");
            Debug.Log($"[TabPinCreate] EventSystem.current={(EventSystem.current != null ? "존재함" : "NULL - UI 입력 감지 불가!")}");
        }
    }

    // TabPinCreate.cs 파일 안에 추가

    /// <summary>
    /// JSON 파일에서 모든 메모 데이터를 불러옵니다.
    /// </summary>
    public List<MemoData> LoadAllMemos()
    {
        List<MemoData> allMemos = new List<MemoData>();

        string path = Application.persistentDataPath;

        try
        {
            // immersal_pins_*.json 파일들 검색
            string[] files = Directory.GetFiles(path, $"{pinFilePrefix}*.json");

            if (files.Length == 0)
            {
                Debug.LogWarning("[TabPinCreate] 저장된 메모 파일(.json)이 없습니다.");
                return allMemos;
            }

            if (verboseDebug)
                Debug.Log($"[TabPinCreate] LoadAllMemos - {files.Length}개의 파일 발견");

            foreach (string filePath in files)
            {
                try
                {
                    string json = File.ReadAllText(filePath);

                    // PinDB 구조로 파싱
                    PinDB db = JsonUtility.FromJson<PinDB>(json);

                    if (db != null && db.pins != null)
                    {
                        foreach (var pinData in db.pins)
                        {
                            // PinData를 MemoData로 변환
                            MemoData memoData = new MemoData();

                            // 기본 정보
                            memoData.id = pinData.id ?? "";
                            memoData.title = pinData.title ?? "";
                            memoData.body = pinData.body ?? "";
                            memoData.content = pinData.body ?? "";
                            memoData.location = pinData.location ?? "";
                            memoData.memoType = pinData.memoType ?? "text";

                            // 날짜/시간
                            memoData.dueDate = pinData.dueDate ?? "";
                            memoData.dueTime = pinData.dueTime ?? "";
                            memoData.emergencyLevel = pinData.emergencyLevel;

                            // 타임스탬프
                            memoData.createdAt = pinData.createdAt ?? "";
                            memoData.updatedAt = pinData.updatedAt ?? "";
                            memoData.completedAt = pinData.completedAt ?? "";
                            memoData.archivedAt = pinData.archivedAt ?? "";

                            // 담당자
                            memoData.assignee = pinData.assignee ?? "";
                            memoData.isAssigned = pinData.isAssigned;

                            // 상태
                            if (Enum.TryParse(pinData.status, out MemoStatus status))
                                memoData.status = status;

                            // 우선순위
                            if (Enum.TryParse(pinData.priority, out MemoPriority priority))
                                memoData.priority = priority;

                            memoData.version = pinData.version;
                            memoData.category = pinData.category ?? "";
                            memoData.archiveReason = pinData.archiveReason ?? "";

                            // ⭐ 이미지 경로 복사
                            if (pinData.imagePaths != null && pinData.imagePaths.Count > 0)
                            {
                                memoData.imagePaths = new List<string>(pinData.imagePaths);
                            }

                            // ⭐ 음성 녹음 경로 복사
                            if (pinData.voiceRecordingPaths != null && pinData.voiceRecordingPaths.Count > 0)
                            {
                                memoData.voiceRecordingPaths = new List<string>(pinData.voiceRecordingPaths);
                            }

                            allMemos.Add(memoData);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[TabPinCreate] 파일 읽기 실패 ({filePath}): {e.Message}");
                }
            }

            if (verboseDebug)
                Debug.Log($"[TabPinCreate] LoadAllMemos 완료 - 총 {allMemos.Count}개의 메모 로드");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TabPinCreate] LoadAllMemos 오류: {e.Message}");
        }

        return allMemos;
    }

    /// <summary>
    /// ID로 특정 메모 데이터를 찾습니다.
    /// </summary>
    public MemoData GetMemoById(string memoId)
    {
        if (string.IsNullOrEmpty(memoId))
        {
            Debug.LogWarning("[TabPinCreate] GetMemoById - memoId가 비어있습니다.");
            return null;
        }

        List<MemoData> allMemos = LoadAllMemos();
        MemoData found = allMemos.Find(m => m.id == memoId);

        if (found == null)
        {
            Debug.LogWarning($"[TabPinCreate] ID '{memoId}'에 해당하는 메모를 찾을 수 없습니다.");
        }
        else
        {
            if (verboseDebug)
                Debug.Log($"[TabPinCreate] 메모 찾음 - ID: {found.id}, 제목: {found.title}, 타입: {found.memoType}");
        }

        return found;
    }

    // 씬 로드 이벤트 구독
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // (추가) 앱이 내려가거나 오브젝트가 비활성화될 때, 최신 DB를 파일에 한 번 더 저장(안전장치)
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SavePinsForCurrentMap();
    }

    // 씬이 로드될 때 파일을 다시 로드 (씬 전환 후 데이터 동기화)
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"★★★ [TabPinCreate] OnSceneLoaded 호출됨: scene={scene.name}, mode={mode} ★★★");

        // 현재 씬이 ConstructionVPS인 경우에만 다시 로드
        if (scene.name == "ConstructionVPS" || scene.name.Contains("Construction"))
        {
            Debug.Log($"★★★ [TabPinCreate] ConstructionVPS 씬 감지 - 핀 강제 재복원 시작 ★★★");

            // 모든 상태 완전 리셋
            loadedMapId = int.MinValue;
            restorationOnce = false;
            pinDB = new PinDB();  // DB 완전 초기화
            pinVisualCache.Clear();

            // 파일에서 데이터 다시 로드
            LoadPinsForCurrentMap();

            Debug.Log($"★★★ [TabPinCreate] 파일 로드 완료 - pinDB.pins.Count={pinDB.pins.Count} ★★★");

            // DB의 이미지 경로 확인 로그
            for (int i = 0; i < pinDB.pins.Count; i++)
            {
                var p = pinDB.pins[i];
                Debug.Log($"★★★ [TabPinCreate] DB핀[{i}]: id={p.id}, memoType={p.memoType}, imageCount={p.imagePaths?.Count ?? 0} ★★★");
                if (p.imagePaths != null && p.imagePaths.Count > 0)
                {
                    for (int j = 0; j < p.imagePaths.Count; j++)
                    {
                        Debug.Log($"★★★   DB imagePaths[{j}]: {p.imagePaths[j]} ★★★");
                    }
                }
            }

            // 기존 씬 핀 삭제
            ClearScenePins();

            // 핀 다시 복원
            RestorePinsForThisMap();
            restorationOnce = true;

            Debug.Log($"★★★ [TabPinCreate] OnSceneLoaded 완료 - 핀 재복원됨 ★★★");
        }
    }

    /// <summary>
    /// 씬에 있는 핀들의 MemoData를 pinDB 데이터로 동기화
    /// (씬 전환 후 파일에서 로드된 최신 데이터 반영)
    /// </summary>
    private void SyncScenePinDataFromDB()
    {
        if (pinsTransform == null)
        {
            Debug.LogWarning("[TabPinCreate] SyncScenePinDataFromDB: pinsTransform is null");
            return;
        }

        Debug.Log($"★★★ [TabPinCreate] SyncScenePinDataFromDB 시작 - 씬 핀 개수={pinsTransform.childCount}, DB 핀 개수={pinDB.pins.Count} ★★★");

        for (int i = 0; i < pinsTransform.childCount; i++)
        {
            Transform child = pinsTransform.GetChild(i);
            MemoData memo = child.GetComponentInChildren<MemoData>(true);

            if (memo == null)
            {
                Debug.Log($"[TabPinCreate] 핀 {child.name}: MemoData 없음 - 스킵");
                continue;
            }

            // DB에서 해당 메모 찾기
            PinData dbData = null;
            for (int j = 0; j < pinDB.pins.Count; j++)
            {
                if (pinDB.pins[j].id == memo.id)
                {
                    dbData = pinDB.pins[j];
                    break;
                }
            }

            if (dbData == null)
            {
                Debug.LogWarning($"[TabPinCreate] DB에서 id={memo.id}를 찾을 수 없음 - 스킵");
                continue;
            }

            // DB 데이터로 MemoData 동기화
            memo.title = dbData.title ?? "";
            memo.body = dbData.body ?? "";
            memo.content = memo.body;
            memo.location = dbData.location ?? "";
            memo.dueDate = dbData.dueDate ?? "";
            memo.dueTime = dbData.dueTime ?? "";
            memo.emergencyLevel = dbData.emergencyLevel;
            memo.assignee = dbData.assignee ?? "";
            memo.isAssigned = dbData.isAssigned;

            // 이미지 메모 필드 동기화 (핵심!)
            memo.imagePaths = dbData.imagePaths != null ? new List<string>(dbData.imagePaths) : new List<string>();
            memo.memoType = dbData.memoType ?? "text";

            // 음성 메모 필드 동기화
            memo.voiceRecordingPaths = dbData.voiceRecordingPaths != null ? new List<string>(dbData.voiceRecordingPaths) : new List<string>();

            Debug.Log($"★★★ [TabPinCreate] 핀 동기화 완료: id={memo.id}, memoType={memo.memoType}, imageCount={memo.imagePaths.Count}, voiceCount={memo.voiceRecordingPaths.Count} ★★★");
            if (memo.imagePaths.Count > 0)
            {
                for (int k = 0; k < memo.imagePaths.Count; k++)
                {
                    Debug.Log($"★★★   imagePaths[{k}]: {memo.imagePaths[k]} ★★★");
                }
            }
            if (memo.voiceRecordingPaths.Count > 0)
            {
                for (int k = 0; k < memo.voiceRecordingPaths.Count; k++)
                {
                    Debug.Log($"★★★   voiceRecordingPaths[{k}]: {memo.voiceRecordingPaths[k]} ★★★");
                }
            }
        }

        Debug.Log($"★★★ [TabPinCreate] SyncScenePinDataFromDB 완료 ★★★");
    }



    // --------------- 아카이빙 추가


    // 전체 메모 데이터 저장
    public void SaveMemoComplete(MemoData memo)
    {
        if (memo == null || string.IsNullOrWhiteSpace(memo.id))
        {
            if (verboseDebug) Debug.LogWarning("[TabPinCreate] SaveMemoComplete: memo or id is null");
            return;
        }

        // DB에서 해당 ID 찾기
        for (int i = 0; i < pinDB.pins.Count; i++)
        {
            if (pinDB.pins[i].id == memo.id)
            {
                // 전체 필드 업데이트
                pinDB.pins[i].title = memo.title ?? "";
                pinDB.pins[i].body = memo.body ?? "";

                // 아카이빙 필드 저장
                pinDB.pins[i].status = memo.status.ToString();
                pinDB.pins[i].createdAt = memo.createdAt ?? "";
                pinDB.pins[i].updatedAt = memo.updatedAt ?? "";
                pinDB.pins[i].completedAt = memo.completedAt ?? "";
                pinDB.pins[i].archivedAt = memo.archivedAt ?? "";
                pinDB.pins[i].archiveReason = memo.archiveReason ?? "";
                pinDB.pins[i].assignee = memo.assignee ?? "";
                pinDB.pins[i].version = memo.version;

                if (verboseDebug)
                    Debug.Log($"[TabPinCreate] SaveMemoComplete: id={memo.id} status={memo.status}");

                SavePinsForCurrentMap();

                // 씬 오브젝트도 동기화
                UpdateScenePinMemoComplete(memo);

                return;
            }
        }

        if (verboseDebug)
            Debug.LogWarning($"[TabPinCreate] SaveMemoComplete: id not found: {memo.id}");
    }

    // 씬에 떠있는 핀도 전체 동기화
    private void UpdateScenePinMemoComplete(MemoData sourceData)
    {
        if (pinsTransform == null || sourceData == null) return;

        for (int i = 0; i < pinsTransform.childCount; i++)
        {
            Transform child = pinsTransform.GetChild(i);
            MemoData memo = child.GetComponentInChildren<MemoData>(true);

            if (memo == null || memo.id != sourceData.id) continue;

            // 전체 필드 복사
            memo.title = sourceData.title;
            memo.body = sourceData.body;
            memo.content = sourceData.body;
            memo.status = sourceData.status;
            memo.createdAt = sourceData.createdAt;
            memo.updatedAt = sourceData.updatedAt;
            memo.completedAt = sourceData.completedAt;
            memo.archivedAt = sourceData.archivedAt;
            memo.archiveReason = sourceData.archiveReason;
            memo.assignee = sourceData.assignee;
            memo.version = sourceData.version;

            // 툴팁 타이틀 동기화
            ApplyTooltipTitle(child, memo.title);

            // 상태에 따라 가시성 처리
            HandlePinVisibilityByStatus(child.gameObject, memo.status);

            if (verboseDebug)
                Debug.Log($"[TabPinCreate] UpdateScenePinMemoComplete: pin={child.name} status={memo.status}");

            return;
        }
    }

    // 상태에 따른 핀 가시성 처리
    private void HandlePinVisibilityByStatus(GameObject pinObj, MemoStatus status)
    {
        if (pinObj == null) return;

        switch (status)
        {
            case MemoStatus.Active:
                pinObj.SetActive(true);
                break;

            case MemoStatus.Completed:
                // 완료 상태: 보이지만 반투명 처리 등 (옵션)
                pinObj.SetActive(true);
                break;

            case MemoStatus.Archived:
            case MemoStatus.Deleted:
                // 보관/삭제: 숨김
                pinObj.SetActive(false);
                break;
        }
    }


    // ---------------------------



    // 맵 변경 감지/복원, 탭 감지 시 핀 생성
    private void Update()
    {
        // UI가 열려있을 때는 핀 생성/선택 탭 로직을 아예 막는다 (입력필드 탭이 뺏기는 문제 해결)
        if (memoUI != null && memoUI.IsUIBlockingWorldInput())
        {
            // 탭이 있을 때 UI가 차단하고 있다는 것을 로그로 출력
            if (verboseDebug && (Input.touchCount > 0 || Input.GetMouseButtonDown(0)))
            {
                Debug.Log("[TabPinCreate] Tap blocked by MemoUI - UI is open and blocking world input");
            }

            // 그래도 아이콘/툴팁 자동 토글은 계속 돌리고 싶으면
            if (autoToggleIconTooltip) UpdatePinsIconTooltipByDistance();
            return;
        }

        // PlayerPrefs의 pinMapId 변경 자동 감지 후 복원
        if (useSelectedMapIdFromPrefs)
        {
            int current = PlayerPrefs.GetInt(selectedMapIdPrefKey, pinMapId);
            if (current != pinMapId)
            {
                if (verboseDebug) Debug.Log($"[TabPinCreate] MapId changed {pinMapId} -> {current}");
                pinMapId = current;
                restorationOnce = false;
                LoadPinsForCurrentMap();

                if (!pinCreateAfterAlignment)
                {
                    if (verboseDebug) Debug.Log("[TabPinCreate] pinCreateAfterAlignment=false -> RestorePinsForThisMap() after map change");
                    RestorePinsForThisMap();
                    restorationOnce = true;
                }
            }
        }

        // 정합 상태 체크 후 복원
        if (pinCreateAfterAlignment && !restorationOnce && IsLocalizedEnough())
        {
            Debug.Log($"★★★ 정합 완료 → 메모 복원 시작 ★★★");
            if (verboseDebug) Debug.Log("[TabPinCreate] Localized enough -> RestorePinsForThisMap()");
            RestorePinsForThisMap();
            restorationOnce = true;
        }

        // (추가) 거리 기반으로 아이콘/툴팁 토글 + 타이틀 동기화
        if (autoToggleIconTooltip)
        {
            UpdatePinsIconTooltipByDistance();
        }

        // 탭 감지 시 핀 생성 시도
        if (TryGetTapPosition(out Vector2 screenPos))
        {
            if (verboseDebug) Debug.Log($"[TabPinCreate] Tap detected screenPos={screenPos}");

            // 먼저 핀을 탭했는지 확인(핀 탭이면 생성하지 않고 선택/편집 모드)
            bool selected = TrySelectExistingPin(screenPos);
            if (verboseDebug) Debug.Log($"[TabPinCreate] TrySelectExistingPin result={selected}");

            if (selected)
                return;

            TryCreatePin(screenPos);
        }
    }

    // PlayerPrefs에서 mapId를 읽어 pinMapId를 결정 함수 (자동 세팅용)
    private void ResolveMapId()
    {
        if (!useSelectedMapIdFromPrefs) return;

        int id = PlayerPrefs.GetInt(selectedMapIdPrefKey, pinMapId);
        pinMapId = id;

        if (verboseDebug)
            Debug.Log($"[TabPinCreate] ResolveMapId prefKey={selectedMapIdPrefKey}, mapId={pinMapId}");
    }

    // 현재 맵의 핀 목록을 메모리(pinDB)에 로드 하는 함수
    private void LoadPinsForCurrentMap()
    {
        Debug.Log($"★★★ [LoadPins] 시작 - mapId={pinMapId}, loadedMapId={loadedMapId} ★★★");

        if (verboseDebug)
            Debug.Log($"[TabPinCreate] LoadPinsForCurrentMap() START - mapId={pinMapId}, loadedMapId={loadedMapId}");

        // 현재 mapId로 로드했는지 체크(중복 로드 방지)
        if (loadedMapId == pinMapId)
        {
            if (verboseDebug) Debug.Log($"[TabPinCreate] LoadPinsForCurrentMap() SKIP - already loaded (loadedMapId={loadedMapId})");
            return;
        }
        loadedMapId = pinMapId;

        // 맵 바뀌면 메모 DB 자체를 교체
        pinDB = new PinDB();

        // 캐시도 비움(맵 전환 시 기존 인스턴스들 무효)
        pinVisualCache.Clear();

        if (verboseDebug) Debug.Log($"[TabPinCreate] LoadPinsForCurrentMap() path={pinSavePath}");

        // 파일에서 로드 시도 (MemoListManager 호환 형식으로 저장된 파일 읽기)
        try
        {
            if (!File.Exists(pinSavePath))
            {
                if (verboseDebug) Debug.Log($"[TabPinCreate] No pin file for mapId={pinMapId}: {pinSavePath}");
                return;
            }

            FileInfo fileInfo = new FileInfo(pinSavePath);
            if (verboseDebug)
                Debug.Log($"[TabPinCreate] Pin file found: size={fileInfo.Length} bytes, modified={fileInfo.LastWriteTime}");

            string json = File.ReadAllText(pinSavePath);

            Debug.Log($"[TabPinCreate] [###] JSON 로드됨: 길이={json.Length}자");

            // JSON 내용 일부 출력 (첫 2000자)
            if (json.Length > 0)
            {
                int previewLength = Mathf.Min(2000, json.Length);
                string preview = json.Substring(0, previewLength);
                Debug.Log($"[TabPinCreate] [###] JSON 내용 (첫 {previewLength}자):\n{preview}");
                if (json.Length > 2000)
                {
                    Debug.Log($"[TabPinCreate] [###] JSON 내용 (마지막 500자):\n{json.Substring(json.Length - 500)}");
                }
            }

            if (verboseDebug)
                Debug.Log($"[TabPinCreate] JSON loaded: {(json.Length > 500 ? json.Substring(0, 500) + "..." : json)}");

            // MemoListManager 호환 형식으로 로드
            MemoListCompatibleDB compatibleDB = JsonUtility.FromJson<MemoListCompatibleDB>(json);

            Debug.Log($"[TabPinCreate] [###] JSON 역직렬화 완료: pins.Length={compatibleDB?.pins?.Length ?? 0}");

            if (compatibleDB != null && compatibleDB.pins != null)
            {
                if (verboseDebug)
                    Debug.Log($"[TabPinCreate] JSON parsed: mapId={compatibleDB.mapId}, pins.Length={compatibleDB.pins.Length}");

                // PinDB 형식으로 변환
                pinDB.pins.Clear();
                foreach (var pin in compatibleDB.pins)
                {
                    // 회전 정보 읽기 (없으면 기본값)
                    Quaternion rotation = Quaternion.identity;
                    if (pin.localRotW != 0 || pin.localRotX != 0 || pin.localRotY != 0 || pin.localRotZ != 0)
                    {
                        rotation = new Quaternion(pin.localRotX, pin.localRotY, pin.localRotZ, pin.localRotW);
                    }

                    // imagePathsJoined 문자열을 배열로 변환
                    string[] imagePathsArray = pin.GetImagePathsArray();

                    Debug.Log($"[TabPinCreate] [###] JSON 로드 후: id={pin.id}, memoType={pin.memoType ?? "text"}, imagePathsJoined={pin.imagePathsJoined ?? ""}, 배열길이={imagePathsArray.Length}");
                    if (imagePathsArray.Length > 0)
                    {
                        for (int j = 0; j < imagePathsArray.Length; j++)
                        {
                            Debug.Log($"[TabPinCreate] [###]   imagePaths[{j}]: {imagePathsArray[j]}");
                        }
                    }

                    pinDB.pins.Add(new PinData
                    {
                        pinMapId = pinMapId,
                        localPos = new Vector3(pin.localPosX, pin.localPosY, pin.localPosZ),
                        localRot = rotation,
                        id = pin.id,
                        title = pin.title,
                        body = pin.body,
                        location = pin.location,
                        status = "Active",  // 기본값
                        createdAt = "",
                        updatedAt = "",
                        version = 1,
                        isAssigned = pin.isAssigned,  // AssigneeRow Toggle 상태 로드
                        assignee = pin.assignee ?? "",  // 담당자 이름 로드
                        dueDate = pin.dueDate ?? "",    // 날짜 로드
                        dueTime = pin.dueTime ?? "",    // 시간 로드
                        emergencyLevel = pin.emergencyLevel,  // 긴급도 로드
                        imagePaths = new List<string>(imagePathsArray),  // 이미지 경로 로드 (문자열에서 변환)
                        memoType = pin.memoType ?? "text"  // 메모 타입 로드
                    });

                    if (verboseDebug)
                        Debug.Log($"[TabPinCreate]   Loaded Pin: id={pin.id}, title='{pin.title}', pos=({pin.localPosX:F2}, {pin.localPosY:F2}, {pin.localPosZ:F2})");
                }

                Debug.Log($"★★★ [LoadPins] 성공 - {pinDB.pins.Count}개 메모 로드됨 ★★★");

                if (verboseDebug)
                    Debug.Log($"[TabPinCreate] LoadPinsForCurrentMap() SUCCESS - loaded {pinDB.pins.Count} pins from {pinSavePath}");
            }
            else
            {
                Debug.LogWarning($"★★★ [LoadPins] 실패 - DB가 null이거나 pins가 없음 ★★★");
                if (verboseDebug) Debug.LogWarning($"[TabPinCreate] Compatible DB is null or has no pins");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[TabPinCreate] LoadPins FAILED: {e}");
            pinDB = new PinDB();
        }
    }

    // 탭 판단과 2D 위치 전달 함수
    private bool TryGetTapPosition(out Vector2 screenPos) // 탭 위치 내보내기 위함
    {
        // screenPos 초기화
        screenPos = default;

        // 탭 판단 (멀티 탭 무시)
        if (Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);

            // UI 터치면 핀 생성 막기
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId))
            {
                if (verboseDebug)
                {
                    Debug.Log("[TabPinCreate] Tap ignored: pointer over UI (touch)");
                    // 어떤 GameObject 위에 있는지 확인
                    var eventData = new UnityEngine.EventSystems.PointerEventData(EventSystem.current);
                    eventData.position = t.position;
                    var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                    EventSystem.current.RaycastAll(eventData, results);
                    if (results.Count > 0)
                        Debug.Log($"[TabPinCreate] UI 위의 탭 감지됨 - GameObject: {results[0].gameObject.name}");
                }
                return false;
            }

            // 탭 판단 시 탭 위치 전달
            if (t.phase == TouchPhase.Began)
            {
                screenPos = t.position;
                if (verboseDebug) Debug.Log($"[TabPinCreate] Touch detected at {screenPos}");
                return true;
            }
        }

        // 에디터/PC 환경에서 마우스로 테스트할 수 있게 지원
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0))
        {
            // UI 클릭이면 핀 생성 막기
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                if (verboseDebug)
                {
                    Debug.Log("[TabPinCreate] Tap ignored: pointer over UI (mouse)");
                    // 어떤 GameObject 위에 있는지 확인
                    var eventData = new UnityEngine.EventSystems.PointerEventData(EventSystem.current);
                    eventData.position = Input.mousePosition;
                    var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                    EventSystem.current.RaycastAll(eventData, results);
                    if (results.Count > 0)
                        Debug.Log($"[TabPinCreate] UI 위의 클릭 감지됨 - GameObject: {results[0].gameObject.name}, Canvas: {results[0].gameObject.GetComponentInParent<Canvas>()?.name}");
                }
                return false;
            }

            screenPos = Input.mousePosition;
            if (verboseDebug) Debug.Log($"[TabPinCreate] Mouse click detected at {screenPos}");
            return true;
        }
#endif

        return false;
    }

    // AR레이캐스트 위치에 핀 생성과 DB 저장 (정합전 생성 제한 시 X)
    private void TryCreatePin(Vector2 screenPos)
    {
        if (verboseDebug) Debug.Log($"[TabPinCreate] TryCreatePin start screenPos={screenPos}");

        // 핀 생성 조건 판단
        if (pinCreateTimeLimit && !IsLocalizedEnough())
        {
            if (verboseDebug) Debug.Log("[TabPinCreate] TryCreatePin blocked: not localized enough (pinCreateTimeLimit=true)");
            return;
        }

        if (raycastManager == null || pinsTransform == null || pinPrefab == null)
        {
            if (verboseDebug)
                Debug.LogWarning($"[TabPinCreate] TryCreatePin blocked: missing refs raycastManager={(raycastManager ? "OK" : "NULL")}, pinsTransform={(pinsTransform ? "OK" : "NULL")}, pinPrefab={(pinPrefab ? "OK" : "NULL")}");
            return;
        }

        // 평면이나 특징점 중 맞는 곳을 핀 위치로 쓰기
        TrackableType types = TrackableType.PlaneWithinInfinity | TrackableType.FeaturePoint;

        if (!raycastManager.Raycast(screenPos, hits, types))
        {
            if (verboseDebug) Debug.Log("[TabPinCreate] ARRaycast FAILED (no plane/feature hit)");
            return;
        }

        Pose hitPose = hits[0].pose;            // 가장 가까운 hit 좌표에 핀 생성 위함

        if (verboseDebug) Debug.Log($"[TabPinCreate] ARRaycast OK hitPose.position={hitPose.position} rot={hitPose.rotation.eulerAngles}");

        // (추가) 근처에 기존 핀이 있으면 새로 만들지 않기
        if (TryBlockCreateNearExisting(hitPose.position, out GameObject nearPin))
        {
            if (verboseDebug) Debug.Log("[TabPinCreate] Create blocked: near existing pin");

            // 근처 핀을 “선택” 처리(단, 편집 UI는 툴팁 거리에서만 열기)
            if (nearPin != null)
            {
                currentSelectedPin = nearPin;

                if (memoUI != null && arCamera != null)
                {
                    float camDist = Vector3.Distance(arCamera.transform.position, nearPin.transform.position);
                    if (camDist <= tooltipDistanceMeters)
                    {
                        memoUI.OnMemoSelected(nearPin); // 저장된 내용 로드해서 편집
                    }
                }
            }
            return;
        }

        // 부모 오브젝트 하위에 핀 생성
        GameObject pin = Instantiate(pinPrefab);
        if (verboseDebug) Debug.Log($"[TabPinCreate] Instantiate pin={pin.name} (activeSelf={pin.activeSelf})");

        pin.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);
        pin.transform.SetParent(pinsTransform, worldPositionStays: true);

        if (verboseDebug)
        {
            Renderer r = pin.GetComponentInChildren<Renderer>(true);
            Canvas c = pin.GetComponentInChildren<Canvas>(true);
            Collider col = pin.GetComponentInChildren<Collider>(true);

            Debug.Log($"[TabPinCreate] Pin parent={pinsTransform.name}, worldPos={pin.transform.position}, localPos={pin.transform.localPosition}");
            Debug.Log($"[TabPinCreate] Pin components: Renderer={(r ? r.name : "null")}, Canvas={(c ? c.name : "null")}, Collider={(col ? col.name : "null")}");
            Debug.Log($"[TabPinCreate] Pin layer={LayerMask.LayerToName(pin.layer)}({pin.layer})");
        }

        // (추가) 메모 데이터 컴포넌트 보장 + 고유 ID 생성
        MemoData memo = pin.GetComponent<MemoData>();
        if (memo == null) memo = pin.AddComponent<MemoData>();

        memo.id = Guid.NewGuid().ToString("N");
        memo.title = "";
        memo.body = "";
        memo.content = memo.body; // 호환 유지
        memo.isAssigned = false; // 초기값은 false

        // 초기 상태 및 타임스탬프 설정
        memo.status = MemoStatus.Active;
        string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        memo.createdAt = now;
        memo.updatedAt = now;
        memo.version = 1;


        if (verboseDebug) Debug.Log($"[TabPinCreate] MemoData assigned id={memo.id}");

        // 생성 직후는 아이콘만 보여야 함
        SetPinVisual(pin.transform, showIcon: true, showTooltip: false);

        // 툴팁 타이틀 텍스트도 동기화(지금은 빈 값)
        ApplyTooltipTitle(pin.transform, memo.title);

        // 아이콘 스프라이트 적용 (초기값은 text)
        ApplyIconSprite(pin.transform, memo.memoType);

        // 현재 선택된 핀 갱신 (편집 대상)
        currentSelectedPin = pin;

        // 메모 부착(생성) 순간에 하단바를 띄우기
        if (memoUI != null)
        {
            memoUI.OnMemoPlaced(pin);
            if (verboseDebug) Debug.Log("[TabPinCreate] memoUI.OnMemoPlaced called");
        }
        else
        {
            if (verboseDebug)
                Debug.LogWarning("[TabPinCreate] memoUI is null. Assign MemoUIController in inspector.");
        }

        // 핀을 현재 mapId로 저장
        PinData data = new PinData
        {
            pinMapId = pinMapId,                       // 현재 맵 ID
            localPos = pin.transform.localPosition,    // pinsTransform 기준 로컬 좌표
            localRot = pin.transform.localRotation,    // pinsTransform 기준 로컬 회전

            // 메모 데이터 저장
            id = memo.id,
            title = memo.title,
            body = memo.body,
            location = memo.location,

            // 아카이빙 필드도 저장
            status = memo.status.ToString(),
            createdAt = memo.createdAt,
            updatedAt = memo.updatedAt,
            version = memo.version
        };

        // 새 핀 데이터를 메모리 목록에 등록
        pinDB.pins.Add(data);

        // 저장 전 상태 로그
        Debug.Log($"★★★ [TabPinCreate] CreatePin - 저장 직전 상태 ★★★");
        Debug.Log($"★★★ mapId={pinMapId}, totalPins={pinDB.pins.Count}");
        Debug.Log($"★★★ pinSavePath={pinSavePath}");
        Debug.Log($"★★★ persistentDataPath={Application.persistentDataPath}");
        Debug.Log($"★★★ 생성된 메모 ID={memo.id}, title='{memo.title}', body='{memo.body}'");

        SavePinsForCurrentMap();

        // 저장 후 파일 확인
        bool fileExistsAfterSave = File.Exists(pinSavePath);
        Debug.Log($"★★★ 저장 후 파일 존재: {fileExistsAfterSave}");
        if (fileExistsAfterSave)
        {
            FileInfo info = new FileInfo(pinSavePath);
            Debug.Log($"★★★ 파일 크기: {info.Length} bytes, 수정시간: {info.LastWriteTime}");
        }

        if (verboseDebug)
            Debug.Log($"[TabPinCreate] CreatePin mapId={pinMapId}, totalPins={pinDB.pins.Count}");
    }

    // 현재 맵의 핀들만 씬에 복원 하는 함수
    private void RestorePinsForThisMap()
    {
        Debug.Log($"★★★ [RestorePins] 시작 - mapId={pinMapId}, DB에 있는 pins={pinDB.pins.Count}개 ★★★");

        if (pinsTransform == null || pinPrefab == null)
        {
            Debug.LogWarning($"★★★ [RestorePins] 중단 - pinsTransform 또는 pinPrefab이 null ★★★");
            return;
        }

        if (verboseDebug) Debug.Log("[TabPinCreate] RestorePinsForThisMap start");

        // 기존 핀 제거 (뒤에서부터)
        ClearScenePins();

        // 복원 시 캐시도 비움(씬 인스턴스 새로 만들어짐)
        pinVisualCache.Clear();

        bool needSaveBecauseMissingId = false;

        // 현재 맵의 핀만 복원
        int restored = 0;                     // 복원된 핀 개수 세기 위함
        foreach (PinData p in pinDB.pins)
        {
            // (추가) 파일 내부에 다른 mapId가 섞였을 때 대비: 현재 맵만 복원
            if (p.pinMapId != pinMapId) continue;

            GameObject pin = Instantiate(pinPrefab, pinsTransform);  // 생성과 동시에 부모까지 지정하기 위함
            pin.transform.localPosition = p.localPos;
            pin.transform.localRotation = p.localRot;

            if (verboseDebug)
                Debug.Log($"[TabPinCreate] Restore pin idx={restored} localPos={p.localPos} localRot={p.localRot.eulerAngles}");

            // 메모 데이터 복원
            MemoData memo = pin.GetComponent<MemoData>();
            if (memo == null) memo = pin.AddComponent<MemoData>();

            // 구버전 파일(id 없음) 대비
            if (string.IsNullOrWhiteSpace(p.id))
            {
                p.id = Guid.NewGuid().ToString("N");
                needSaveBecauseMissingId = true;
                if (verboseDebug) Debug.Log($"[TabPinCreate] Missing id found -> generated id={p.id}");
            }

            memo.id = p.id;
            memo.title = p.title ?? "";
            memo.body = p.body ?? "";
            memo.content = memo.body; // 호환 유지
            memo.location = p.location ?? "";

            // 이미지 메모 필드 복원
            memo.imagePaths = p.imagePaths ?? new List<string>();
            memo.memoType = p.memoType ?? "text";

            // 음성 메모 필드 복원
            memo.voiceRecordingPaths = p.voiceRecordingPaths ?? new List<string>();

            // 날짜/시간/긴급도 필드 복원
            memo.dueDate = p.dueDate ?? "";
            memo.dueTime = p.dueTime ?? "";
            memo.emergencyLevel = p.emergencyLevel;

            Debug.Log($"[TabPinCreate] [###] 핀 복원: id={p.id}, title={p.title}, memoType={memo.memoType}, imageCount={memo.imagePaths.Count}, voiceCount={memo.voiceRecordingPaths.Count}");
            Debug.Log($"[TabPinCreate] [###]   dueDate={memo.dueDate}, dueTime={memo.dueTime}, emergencyLevel={memo.emergencyLevel}");
            if (memo.imagePaths.Count > 0)
            {
                for (int i = 0; i < memo.imagePaths.Count; i++)
                {
                    Debug.Log($"[TabPinCreate] [###]   복원된 imagePaths[{i}]: {memo.imagePaths[i]}");
                }
            }
            if (memo.voiceRecordingPaths.Count > 0)
            {
                for (int i = 0; i < memo.voiceRecordingPaths.Count; i++)
                {
                    Debug.Log($"[TabPinCreate] [###]   복원된 voiceRecordingPaths[{i}]: {memo.voiceRecordingPaths[i]}");
                }
            }

            // 아카이빙 필드 복원
            memo.status = ParseMemoStatus(p.status);
            memo.createdAt = p.createdAt ?? "";
            memo.updatedAt = p.updatedAt ?? "";
            memo.completedAt = p.completedAt ?? "";
            memo.archivedAt = p.archivedAt ?? "";
            memo.archiveReason = p.archiveReason ?? "";
            memo.assignee = p.assignee ?? "";
            memo.isAssigned = p.isAssigned; // AssigneeRow Toggle 상태 복원
            memo.version = p.version;

            // 초기 타임스탬프 설정 (없으면)
            if (string.IsNullOrEmpty(memo.createdAt))
                memo.createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            if (string.IsNullOrEmpty(memo.updatedAt))
                memo.updatedAt = memo.createdAt;


            // 툴팁 타이틀 텍스트 동기화
            ApplyTooltipTitle(pin.transform, memo.title);

            // 아이콘 스프라이트 적용
            ApplyIconSprite(pin.transform, memo.memoType);

            HandlePinVisibilityByStatus(pin, memo.status);

            // 복원 직후 현재 거리 기준으로 아이콘/툴팁 상태 세팅
            UpdateOnePinVisualByDistance(pin.transform, memo);

            restored++;
        }

        // id가 없던 데이터를 채웠으면 저장해서 다음부터 안정화
        if (needSaveBecauseMissingId)
        {
            if (verboseDebug) Debug.Log("[TabPinCreate] needSaveBecauseMissingId=true -> SavePinsForCurrentMap()");
            SavePinsForCurrentMap();
        }

        Debug.Log($"★★★ [RestorePins] 완료 - {restored}개 메모 복원됨 ★★★");

        if (verboseDebug)
            Debug.Log($"[TabPinCreate] RestorePins mapId={pinMapId}, restored={restored}, file={pinSavePath}");

        // MemoList에서 선택된 메모 자동 로드
        AutoSelectMemoFromPrefs();
    }

    // MemoList에서 선택된 메모를 자동으로 선택하는 함수
    private void AutoSelectMemoFromPrefs()
    {
        // PlayerPrefs에서 선택된 메모 ID 읽기
        if (!PlayerPrefs.HasKey("SELECTED_MEMO_ID"))
            return;

        string selectedMemoId = PlayerPrefs.GetString("SELECTED_MEMO_ID", "");

        // 읽은 후 바로 삭제 (한 번만 자동 선택)
        PlayerPrefs.DeleteKey("SELECTED_MEMO_ID");
        PlayerPrefs.Save();

        if (string.IsNullOrEmpty(selectedMemoId))
            return;

        if (verboseDebug)
            Debug.Log($"[TabPinCreate] AutoSelectMemoFromPrefs: Looking for memo id={selectedMemoId}");

        // 씬에서 해당 메모 찾기
        if (pinsTransform == null)
        {
            Debug.LogWarning("[TabPinCreate] pinsTransform is null, cannot auto-select memo");
            return;
        }

        for (int i = 0; i < pinsTransform.childCount; i++)
        {
            Transform child = pinsTransform.GetChild(i);
            MemoData memo = child.GetComponentInChildren<MemoData>(true);

            if (memo != null && memo.id == selectedMemoId)
            {
                if (verboseDebug)
                    Debug.Log($"[TabPinCreate] Found memo id={selectedMemoId}, auto-selecting");

                // 메모 선택
                currentSelectedPin = memo.gameObject;

                // MemoUI가 있으면 자동으로 열기
                if (memoUI != null)
                {
                    memoUI.OnMemoSelected(currentSelectedPin);
                    if (verboseDebug)
                        Debug.Log($"[TabPinCreate] Auto-opened MemoUI for memo id={selectedMemoId}");
                }

                return;
            }
        }

        if (verboseDebug)
            Debug.LogWarning($"[TabPinCreate] Could not find memo with id={selectedMemoId}");
    }

    private MemoStatus ParseMemoStatus(string statusStr)
    {
        if (string.IsNullOrEmpty(statusStr)) return MemoStatus.Active;

        try
        {
            return (MemoStatus)Enum.Parse(typeof(MemoStatus), statusStr, true);
        }
        catch
        {
            if (verboseDebug) Debug.LogWarning($"[TabPinCreate] Invalid status string: {statusStr}");
            return MemoStatus.Active;
        }
    }

    // 현재 맵의 핀들만 삭제 하는 함수
    public void ClearPinsThisMap()
    {
        if (verboseDebug) Debug.Log("[TabPinCreate] ClearPinsThisMap()");
        pinDB.pins.Clear();
        SavePinsForCurrentMap();   // 삭제된 상태 저장 위함
        RestorePinsForThisMap();   // 씬에서 핀들도 제거 위함 (동기화)
    }

    // 현재 맵의 핀들만 파일에 JSON으로 저장 하는 함수 (MemoListManager 호환 형식)
    private void SavePinsForCurrentMap()
    {
        Debug.Log($"★★★ [SavePins] 시작 - mapId={pinMapId}, count={pinDB.pins.Count} ★★★");

        try
        {
            if (verboseDebug)
                Debug.Log($"[TabPinCreate] SavePinsForCurrentMap() START - mapId={pinMapId}, count={pinDB.pins.Count}");

            // MemoListManager 호환 형식으로 변환
            MemoListCompatibleDB compatibleDB = new MemoListCompatibleDB();
            compatibleDB.mapId = pinMapId;
            compatibleDB.pins = new MemoListCompatiblePinData[pinDB.pins.Count];

            for (int i = 0; i < pinDB.pins.Count; i++)
            {
                PinData p = pinDB.pins[i];

                // 이미지 경로를 '|'로 구분된 문자열로 변환
                string imagePathsJoined = "";
                if (p.imagePaths != null && p.imagePaths.Count > 0)
                {
                    imagePathsJoined = string.Join("|", p.imagePaths);
                }

                compatibleDB.pins[i] = new MemoListCompatiblePinData
                {
                    id = p.id,
                    title = p.title,
                    body = p.body,
                    location = p.location,
                    localPosX = p.localPos.x,
                    localPosY = p.localPos.y,
                    localPosZ = p.localPos.z,
                    isAssigned = p.isAssigned,
                    assignee = p.assignee ?? "",  // 담당자 저장 추가
                    localRotX = p.localRot.x,
                    localRotY = p.localRot.y,
                    localRotZ = p.localRot.z,
                    localRotW = p.localRot.w,
                    dueDate = p.dueDate ?? "",     // 날짜 저장
                    dueTime = p.dueTime ?? "",     // 시간 저장
                    emergencyLevel = p.emergencyLevel,  // 긴급도 저장
                    imagePathsJoined = imagePathsJoined,  // '|'로 구분된 이미지 경로 문자열
                    memoType = p.memoType ?? "text"  // 메모 타입 저장
                };

                Debug.Log($"[TabPinCreate] [###] JSON 저장 전: Pin[{i}] id={p.id}, memoType={p.memoType}, imagePaths.Count={p.imagePaths?.Count ?? 0}, imagePathsJoined={imagePathsJoined}");
                if (p.imagePaths != null && p.imagePaths.Count > 0)
                {
                    for (int j = 0; j < p.imagePaths.Count; j++)
                    {
                        Debug.Log($"[TabPinCreate] [###]   imagePaths[{j}]: {p.imagePaths[j]}");
                    }
                }

                if (verboseDebug)
                    Debug.Log($"[TabPinCreate]   Pin[{i}]: id={p.id}, title='{p.title}', body='{p.body}', location='{p.location}', pos=({p.localPos.x:F2}, {p.localPos.y:F2}, {p.localPos.z:F2})");
            }

            string json = JsonUtility.ToJson(compatibleDB, true);

            // JSON 내용 상세 로그 (마지막 핀만 - 가장 최근 저장된 핀)
            if (compatibleDB.pins != null && compatibleDB.pins.Length > 0)
            {
                var lastPin = compatibleDB.pins[compatibleDB.pins.Length - 1];
                Debug.Log($"[TabPinCreate] [###] JSON 직렬화 확인 - 마지막 핀: id={lastPin.id}, memoType={lastPin.memoType}, imagePathsJoined={lastPin.imagePathsJoined}");
            }

            File.WriteAllText(pinSavePath, json);

            // 파일이 실제로 저장되었는지 확인
            bool fileExists = File.Exists(pinSavePath);
            long fileSize = fileExists ? new FileInfo(pinSavePath).Length : 0;

            Debug.Log($"[TabPinCreate] [###] JSON 저장 완료: fileSize={fileSize} bytes");

            // JSON 일부분 출력 (첫 2000자)
            if (json.Length > 0)
            {
                int previewLength = Mathf.Min(2000, json.Length);
                string preview = json.Substring(0, previewLength);
                Debug.Log($"[TabPinCreate] [###] JSON 내용 (첫 {previewLength}자):\n{preview}");
                if (json.Length > 2000)
                {
                    Debug.Log($"[TabPinCreate] [###] JSON 내용 (마지막 500자):\n{json.Substring(json.Length - 500)}");
                }
            }

            if (verboseDebug)
            {
                Debug.Log($"[TabPinCreate] SavePins 완료 (MemoList 호환)");
                Debug.Log($"[TabPinCreate]   mapId={pinMapId}, count={pinDB.pins.Count}");
                Debug.Log($"[TabPinCreate]   path={pinSavePath}");
                Debug.Log($"[TabPinCreate]   fileExists={fileExists}, fileSize={fileSize} bytes");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[TabPinCreate] SavePins FAILED: {e}");
        }
    }

    // MemoListManager 호환용 데이터 구조
    [Serializable]
    private class MemoListCompatibleDB
    {
        public int mapId;
        public MemoListCompatiblePinData[] pins;
    }

    [Serializable]
    private class MemoListCompatiblePinData
    {
        public string id;
        public string title;
        public string body;
        public string location;
        public float localPosX;
        public float localPosY;
        public float localPosZ;
        public bool isAssigned;  // AssigneeRow Toggle 상태
        public string assignee;  // 담당자 이름

        // TabPinCreate용 추가 필드 (MemoListManager는 무시)
        public float localRotX;
        public float localRotY;
        public float localRotZ;
        public float localRotW;

        // 날짜/시간/긴급도 필드 추가
        public string dueDate = "";      // 마감 날짜 (yyyy-MM-dd 형식)
        public string dueTime = "";      // 마감 시간 (HH:mm 형식)
        public int emergencyLevel = 0;   // 긴급도 (0=선택안함, 1~3=선택됨)

        // 이미지 메모 기능 (배열 대신 단일 문자열 사용 - JsonUtility 호환성)
        public string imagePathsJoined = "";  // '|'로 구분된 이미지 경로 문자열
        public string memoType = "text";  // 메모 타입

        // 헬퍼: imagePaths를 배열로 변환
        public string[] GetImagePathsArray()
        {
            if (string.IsNullOrEmpty(imagePathsJoined))
                return new string[0];
            return imagePathsJoined.Split(new char[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries);
        }

        // 헬퍼: 배열을 imagePathsJoined 문자열로 설정
        public void SetImagePathsFromArray(string[] paths)
        {
            if (paths == null || paths.Length == 0)
                imagePathsJoined = "";
            else
                imagePathsJoined = string.Join("|", paths);
        }
    }

    // 모든 맵의 핀과 DB를 삭제 하는 함수
    public void ResetAllpinsAndAnchors()
    {
        if (verboseDebug) Debug.Log("[TabPinCreate] ResetAllpinsAndAnchors()");

        // 핀 오브젝트 삭제
        ClearScenePins();

        // 메모리 안  DB 파일 삭제
        pinDB.pins.Clear();

        // "모든 맵" 의미에 맞게 persistentDataPath의 pins_*.json 전부 삭제
        DeleteAllPinFiles();

        restorationOnce = false;
        loadedMapId = int.MinValue;

        pinVisualCache.Clear();
    }

    // 정합 상태 판단 함수
    private bool IsLocalizedEnough()
    {
        // trackingAnalyzer 참조 여부 판단
        if (trackingAnalyzer == null)
        {
            if (verboseDebug) Debug.LogWarning("[TabPinCreate] IsLocalizedEnough: trackingAnalyzer is null");
            return false;
        }

        try
        {
            // trackingAnalyzer 안에서 TrackingStatus값 꺼내기
            object trackingStatus = GetMemberValue(trackingAnalyzer, "TrackingStatus");
            if (trackingStatus == null)
            {
                if (verboseDebug) Debug.LogWarning("[TabPinCreate] TrackingStatus is null (trackingAnalyzer mismatch?)");
                return false;
            }

            // 정합 성공 횟수와 퀄리티 값 꺼내기 > 판단
            int succ = ToInt(GetMemberValue(trackingStatus, "LocalizationSuccessCount"));
            int qual = ToInt(GetMemberValue(trackingStatus, "TrackingQuality"));

            if (verboseDebug)
                Debug.Log($"[TabPinCreate] LocalizationSuccessCount={succ}, TrackingQuality={qual}, limitQuality={limitQuality}");

            return succ > 0 && qual >= limitQuality;
        }
        catch (Exception e)
        {
            if (verboseDebug) Debug.LogWarning($"[TabPinCreate] IsLocalizedEnough failed: {e}");
            return false;
        }
    }

    // Object의 Name의 변수/프로퍼티(값을 읽기/쓰기 시 처리 내용) 값 꺼내기 함수
    private static object GetMemberValue(object obj, string name)
    {
        if (obj == null) return null;

        Type t = obj.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        PropertyInfo p = t.GetProperty(name, flags);
        if (p != null) return p.GetValue(obj);

        FieldInfo f = t.GetField(name, flags);
        if (f != null) return f.GetValue(obj);

        return null;
    }

    // Object를 int로 변환하는 함수
    private static int ToInt(object v)
    {
        // Object 판단
        if (v == null) return 0;
        if (v is int i) return i;

        // enum을 int로 변환
        Type vt = v.GetType();
        if (vt.IsEnum) return (int)v;

        try { return Convert.ToInt32(v); }
        catch { return 0; }
    }

    // 현재 맵 파일/DB/씬 핀 초기화 함수
    public void ResetAllPins()
    {
        ResetAllpinsAndAnchors();
    }

    // 복원 상태 초기화 함수 (씬 재진입 시 메모 복원을 위해 필요)
    public void ResetRestorationState()
    {
        restorationOnce = false;
        loadedMapId = int.MinValue;  // 다음 Awake에서 LoadPinsForCurrentMap()이 파일을 다시 읽도록 강제

        Debug.Log($"★★★ [TabPinCreate] ResetRestorationState 호출됨 ★★★");
        Debug.Log($"★★★ restorationOnce={restorationOnce}");
        Debug.Log($"★★★ loadedMapId={loadedMapId}");
        Debug.Log($"★★★ 다음 씬 진입 시 파일을 다시 로드하도록 설정됨");

        if (verboseDebug)
            Debug.Log($"[TabPinCreate] ResetRestorationState: restorationOnce={restorationOnce}, loadedMapId={loadedMapId}");
    }

    // (중복 제거용) 핀 오브젝트 삭제
    private void ClearScenePins()
    {
        if (pinsTransform != null)
        {
            if (verboseDebug) Debug.Log($"[TabPinCreate] ClearScenePins childCount={pinsTransform.childCount}");
            for (int i = pinsTransform.childCount - 1; i >= 0; i--)
                Destroy(pinsTransform.GetChild(i).gameObject);
        }
        else
        {
            if (verboseDebug) Debug.LogWarning("[TabPinCreate] ClearScenePins: pinsTransform is null");
        }
    }

    // 모든 맵 파일 삭제(pins_*.json)
    private void DeleteAllPinFiles()
    {
        try
        {
            string dir = Application.persistentDataPath;
            if (!Directory.Exists(dir)) return;

            string pattern = $"{pinFilePrefix}*.json";
            string[] files = Directory.GetFiles(dir, pattern);

            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    File.Delete(files[i]);
                    if (verboseDebug) Debug.Log($"[TabPinCreate] Deleted pin file: {files[i]}");
                }
                catch (Exception inner)
                {
                    if (verboseDebug) Debug.LogWarning($"[TabPinCreate] Delete file failed: {files[i]} / {inner}");
                }
            }
        }
        catch (Exception e)
        {
            if (verboseDebug) Debug.LogWarning($"[TabPinCreate] DeleteAllPinFiles failed: {e}");
        }
    }

    // 핀 탭 선택(버튼처럼 탭해서 다시 수정)
    private bool TrySelectExistingPin(Vector2 screenPos)
    {
        if (!arCamera)
        {
            if (verboseDebug) Debug.LogWarning("[TabPinCreate] TrySelectExistingPin: arCamera is null");
            return false;
        }

        Ray ray = arCamera.ScreenPointToRay(screenPos);

        // pinLayerMask가 0이면(미설정) 마스크 없이 Raycast
        bool hitSomething;
        RaycastHit hit;

        if (pinLayerMask.value == 0)
            hitSomething = Physics.Raycast(ray, out hit, pinRayDistance);
        else
            hitSomething = Physics.Raycast(ray, out hit, pinRayDistance, pinLayerMask);

        if (verboseDebug)
        {
            Debug.Log($"[TabPinCreate] PinSelect Raycast hit={hitSomething} distMax={pinRayDistance} maskValue={pinLayerMask.value}");
            if (hitSomething)
                Debug.Log($"[TabPinCreate] PinSelect hit collider={hit.collider.name} hitObjLayer={LayerMask.LayerToName(hit.collider.gameObject.layer)}({hit.collider.gameObject.layer})");
        }

        if (!hitSomething) return false;

        MemoData memo = hit.collider.GetComponentInParent<MemoData>();
        if (memo == null)
        {
            if (verboseDebug) Debug.Log("[TabPinCreate] PinSelect hit but MemoData not found in parent");
            return false;
        }

        // (핀을 맞췄으면 무조건 선택으로 소비해서 새 핀 생성 루트로 못 가게 한다
        currentSelectedPin = memo.gameObject;

        // 툴팁 상태일 때만 탭 편집 가능 규칙 적용
        // 단, 여기서 return false를 하면 새 핀 생성으로 넘어가서 “빈 창” 문제가 다시 생김
        // 그래서 편집을 열지 못해도 return true로 소비한다
        float dist = Vector3.Distance(arCamera.transform.position, memo.transform.position);
        bool canEditNow = dist <= tooltipDistanceMeters;

        if (canEditNow && memoUI != null)
        {
            memoUI.OnMemoSelected(currentSelectedPin); // 저장된 값 로드 후 편집
            if (verboseDebug) Debug.Log("[TabPinCreate] memoUI.OnMemoSelected called");
        }
        else
        {
            if (verboseDebug) Debug.Log($"[TabPinCreate] PinSelect consumed but edit blocked (dist={dist:F2}, limit={tooltipDistanceMeters:F2})");
        }

        return true;
    }

    //  UI에서 호출: 특정 핀(id)의 텍스트 메모 저장(JSON 갱신)
    public void SaveTextMemoById(string id, string title, string body, string location = "")
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            if (verboseDebug) Debug.LogWarning("[TabPinCreate] SaveTextMemoById: id is null/empty");
            return;
        }

        for (int i = 0; i < pinDB.pins.Count; i++)
        {
            if (pinDB.pins[i].id == id)
            {
                pinDB.pins[i].title = title ?? "";
                pinDB.pins[i].body = body ?? "";
                pinDB.pins[i].location = location ?? "";
                if (verboseDebug) Debug.Log($"[TabPinCreate] SaveTextMemoById: updated id={id} titleLen={(pinDB.pins[i].title?.Length ?? 0)} bodyLen={(pinDB.pins[i].body?.Length ?? 0)} location={location}");
                SavePinsForCurrentMap();

                // DB만 바꾸면 씬에 떠있는 핀의 텍스트는 그대로일 수 있으므로, 씬 오브젝트도 함께 갱신
                UpdateScenePinMemo(id, title, body, location);

                return;
            }
        }

        if (verboseDebug) Debug.LogWarning($"[TabPinCreate] SaveTextMemoById: id not found in DB: {id}");
    }

    /// <summary>
    /// AssigneeToggleManager에서 호출: 특정 메모의 isAssigned 상태 업데이트
    /// </summary>
    public void UpdateMemoAssignedState(string id, bool isAssigned)
    {
        Debug.Log($"★★★ [ASSIGNEE] [TabPinCreate] UpdateMemoAssignedState 호출: id={id}, isAssigned={isAssigned}");

        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogWarning($"★★★ [ASSIGNEE] [TabPinCreate] ✗ id가 null/empty입니다!");
            return;
        }

        // PinDB에 저장
        bool foundInDB = false;
        for (int i = 0; i < pinDB.pins.Count; i++)
        {
            if (pinDB.pins[i].id == id)
            {
                pinDB.pins[i].isAssigned = isAssigned;
                foundInDB = true;
                Debug.Log($"★★★ [ASSIGNEE] [TabPinCreate] ✓ PinDB 업데이트: id={id}, isAssigned={isAssigned}");
                SavePinsForCurrentMap();
                break;
            }
        }

        if (!foundInDB)
        {
            Debug.LogWarning($"★★★ [ASSIGNEE] [TabPinCreate] ✗ PinDB에서 id={id}를 찾을 수 없습니다!");
        }

        // 씬의 MemoData에도 적용
        bool foundInScene = false;
        if (pinsTransform != null)
        {
            for (int i = 0; i < pinsTransform.childCount; i++)
            {
                Transform child = pinsTransform.GetChild(i);
                MemoData memo = child.GetComponentInChildren<MemoData>(true);
                if (memo != null && memo.id == id)
                {
                    memo.isAssigned = isAssigned;
                    foundInScene = true;
                    Debug.Log($"★★★ [ASSIGNEE] [TabPinCreate] ✓ 씬 MemoData 업데이트: id={id}");
                    break;
                }
            }
        }

        if (!foundInScene)
        {
            Debug.LogWarning($"★★★ [ASSIGNEE] [TabPinCreate] ✗ 씬에서 id={id}의 MemoData를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// MemoUIController에서 호출: 특정 메모의 assignee 이름 가져오기
    /// </summary>
    public string GetMemoAssignee(string id)
    {
        Debug.Log($"★★★ [ASSIGNEE] [TabPinCreate] GetMemoAssignee 호출: id={id}");

        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogWarning($"★★★ [ASSIGNEE] [TabPinCreate] ✗ id가 null/empty입니다!");
            return "";
        }

        for (int i = 0; i < pinDB.pins.Count; i++)
        {
            if (pinDB.pins[i].id == id)
            {
                string assignee = pinDB.pins[i].assignee ?? "";
                Debug.Log($"★★★ [ASSIGNEE] [TabPinCreate] ✓ GetMemoAssignee: id={id}, assignee={assignee}");
                return assignee;
            }
        }

        Debug.LogWarning($"★★★ [ASSIGNEE] [TabPinCreate] ✗ id={id}를 pinDB에서 찾을 수 없습니다!");
        return "";
    }

    /// <summary>
    /// AssigneeToggleManager에서 호출: 특정 메모의 assignee 이름 업데이트
    /// </summary>
    public void UpdateMemoAssignee(string id, string assigneeName)
    {
        Debug.Log($"★★★ [ASSIGNEE] [TabPinCreate] UpdateMemoAssignee 호출: id={id}, assignee={assigneeName}");

        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogWarning($"★★★ [ASSIGNEE] [TabPinCreate] ✗ id가 null/empty입니다!");
            return;
        }

        // PinDB에 저장
        bool foundInDB = false;
        for (int i = 0; i < pinDB.pins.Count; i++)
        {
            if (pinDB.pins[i].id == id)
            {
                pinDB.pins[i].assignee = assigneeName ?? "";
                foundInDB = true;
                Debug.Log($"★★★ [ASSIGNEE] [TabPinCreate] ✓ PinDB assignee 업데이트: id={id}, assignee={assigneeName}");
                SavePinsForCurrentMap();
                break;
            }
        }

        if (!foundInDB)
        {
            Debug.LogWarning($"★★★ [ASSIGNEE] [TabPinCreate] ✗ PinDB에서 id={id}를 찾을 수 없습니다!");
        }

        // 씬의 MemoData에도 적용
        bool foundInScene = false;
        if (pinsTransform != null)
        {
            for (int i = 0; i < pinsTransform.childCount; i++)
            {
                Transform child = pinsTransform.GetChild(i);
                MemoData memo = child.GetComponentInChildren<MemoData>(true);
                if (memo != null && memo.id == id)
                {
                    memo.assignee = assigneeName ?? "";
                    foundInScene = true;
                    Debug.Log($"★★★ [ASSIGNEE] [TabPinCreate] ✓ 씬 MemoData assignee 업데이트: id={id}");
                    break;
                }
            }
        }

        if (!foundInScene)
        {
            Debug.LogWarning($"★★★ [ASSIGNEE] [TabPinCreate] ✗ 씬에서 id={id}의 MemoData를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// MemoUIController에서 호출: 특정 메모의 마감 날짜 업데이트
    /// </summary>
    public void UpdateMemoDueDate(string id, string dueDate)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            if (verboseDebug) Debug.LogWarning("[TabPinCreate] UpdateMemoDueDate: id가 null/empty입니다!");
            return;
        }

        // PinDB에 저장
        bool foundInDB = false;
        for (int i = 0; i < pinDB.pins.Count; i++)
        {
            if (pinDB.pins[i].id == id)
            {
                pinDB.pins[i].dueDate = dueDate ?? "";
                foundInDB = true;
                if (verboseDebug) Debug.Log($"[TabPinCreate] PinDB dueDate 업데이트: id={id}, dueDate={dueDate}");
                SavePinsForCurrentMap();
                break;
            }
        }

        if (!foundInDB && verboseDebug)
        {
            Debug.LogWarning($"[TabPinCreate] PinDB에서 id={id}를 찾을 수 없습니다!");
        }

        // 씬의 MemoData에도 적용
        if (pinsTransform != null)
        {
            for (int i = 0; i < pinsTransform.childCount; i++)
            {
                Transform child = pinsTransform.GetChild(i);
                MemoData memo = child.GetComponentInChildren<MemoData>(true);
                if (memo != null && memo.id == id)
                {
                    memo.dueDate = dueDate ?? "";
                    if (verboseDebug) Debug.Log($"[TabPinCreate] 씬 MemoData dueDate 업데이트: id={id}");
                    break;
                }
            }
        }
    }

    /// <summary>
    /// MemoUIController에서 호출: 특정 메모의 마감 시간 업데이트
    /// </summary>
    public void UpdateMemoDueTime(string id, string dueTime)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            if (verboseDebug) Debug.LogWarning("[TabPinCreate] UpdateMemoDueTime: id가 null/empty입니다!");
            return;
        }

        // PinDB에 저장
        bool foundInDB = false;
        for (int i = 0; i < pinDB.pins.Count; i++)
        {
            if (pinDB.pins[i].id == id)
            {
                pinDB.pins[i].dueTime = dueTime ?? "";
                foundInDB = true;
                if (verboseDebug) Debug.Log($"[TabPinCreate] PinDB dueTime 업데이트: id={id}, dueTime={dueTime}");
                SavePinsForCurrentMap();
                break;
            }
        }

        if (!foundInDB && verboseDebug)
        {
            Debug.LogWarning($"[TabPinCreate] PinDB에서 id={id}를 찾을 수 없습니다!");
        }

        // 씬의 MemoData에도 적용
        if (pinsTransform != null)
        {
            for (int i = 0; i < pinsTransform.childCount; i++)
            {
                Transform child = pinsTransform.GetChild(i);
                MemoData memo = child.GetComponentInChildren<MemoData>(true);
                if (memo != null && memo.id == id)
                {
                    memo.dueTime = dueTime ?? "";
                    if (verboseDebug) Debug.Log($"[TabPinCreate] 씬 MemoData dueTime 업데이트: id={id}");
                    break;
                }
            }
        }
    }

    /// <summary>
    /// MemoUIController에서 호출: 특정 메모의 긴급도 업데이트
    /// </summary>
    public void UpdateMemoEmergencyLevel(string id, int emergencyLevel)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            if (verboseDebug) Debug.LogWarning("[TabPinCreate] UpdateMemoEmergencyLevel: id가 null/empty입니다!");
            return;
        }

        // PinDB에 저장
        bool foundInDB = false;
        for (int i = 0; i < pinDB.pins.Count; i++)
        {
            if (pinDB.pins[i].id == id)
            {
                pinDB.pins[i].emergencyLevel = emergencyLevel;
                foundInDB = true;
                if (verboseDebug) Debug.Log($"[TabPinCreate] PinDB emergencyLevel 업데이트: id={id}, emergencyLevel={emergencyLevel}");
                SavePinsForCurrentMap();
                break;
            }
        }

        if (!foundInDB && verboseDebug)
        {
            Debug.LogWarning($"[TabPinCreate] PinDB에서 id={id}를 찾을 수 없습니다!");
        }

        // 씬의 MemoData에도 적용
        if (pinsTransform != null)
        {
            for (int i = 0; i < pinsTransform.childCount; i++)
            {
                Transform child = pinsTransform.GetChild(i);
                MemoData memo = child.GetComponentInChildren<MemoData>(true);
                if (memo != null && memo.id == id)
                {
                    memo.emergencyLevel = emergencyLevel;
                    if (verboseDebug) Debug.Log($"[TabPinCreate] 씬 MemoData emergencyLevel 업데이트: id={id}");
                    break;
                }
            }
        }
    }

    /// <summary>
    /// ChecklistUIController에서 호출: 특정 메모의 memoType 업데이트
    /// </summary>
    public void UpdateMemoType(string id, string memoType)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            if (verboseDebug) Debug.LogWarning("[TabPinCreate] UpdateMemoType: id가 null/empty입니다!");
            return;
        }

        // PinDB에 저장
        bool foundInDB = false;
        for (int i = 0; i < pinDB.pins.Count; i++)
        {
            if (pinDB.pins[i].id == id)
            {
                pinDB.pins[i].memoType = memoType;
                foundInDB = true;
                Debug.Log($"[TabPinCreate] PinDB memoType 업데이트: id={id}, memoType={memoType}");
                SavePinsForCurrentMap();
                break;
            }
        }

        if (!foundInDB && verboseDebug)
        {
            Debug.LogWarning($"[TabPinCreate] PinDB에서 id={id}를 찾을 수 없습니다!");
        }

        // 씬의 MemoData에도 적용
        if (pinsTransform != null)
        {
            for (int i = 0; i < pinsTransform.childCount; i++)
            {
                Transform child = pinsTransform.GetChild(i);
                MemoData memo = child.GetComponentInChildren<MemoData>(true);
                if (memo != null && memo.id == id)
                {
                    memo.memoType = memoType;

                    // 아이콘 스프라이트도 업데이트
                    ApplyIconSprite(child, memoType);

                    Debug.Log($"[TabPinCreate] 씬 MemoData memoType 업데이트: id={id}, memoType={memoType}");
                    break;
                }
            }
        }
    }

    /// <summary>
    /// VoiceMemoUIController에서 호출: 특정 메모의 녹음 파일 경로 업데이트
    /// </summary>
    public void UpdateMemoVoiceRecordings(string id, List<string> voiceRecordingPaths)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            if (verboseDebug) Debug.LogWarning("[TabPinCreate] UpdateMemoVoiceRecordings: id가 null/empty입니다!");
            return;
        }

        // PinDB에 저장
        bool foundInDB = false;
        for (int i = 0; i < pinDB.pins.Count; i++)
        {
            if (pinDB.pins[i].id == id)
            {
                pinDB.pins[i].voiceRecordingPaths = voiceRecordingPaths ?? new List<string>();
                foundInDB = true;
                Debug.Log($"[TabPinCreate] PinDB voiceRecordingPaths 업데이트: id={id}, 파일 수={voiceRecordingPaths?.Count ?? 0}");
                SavePinsForCurrentMap();
                break;
            }
        }

        if (!foundInDB && verboseDebug)
        {
            Debug.LogWarning($"[TabPinCreate] PinDB에서 id={id}를 찾을 수 없습니다!");
        }

        // 씬의 MemoData에도 적용
        if (pinsTransform != null)
        {
            for (int i = 0; i < pinsTransform.childCount; i++)
            {
                Transform child = pinsTransform.GetChild(i);
                MemoData memo = child.GetComponentInChildren<MemoData>(true);
                if (memo != null && memo.id == id)
                {
                    memo.voiceRecordingPaths = voiceRecordingPaths ?? new List<string>();
                    Debug.Log($"[TabPinCreate] 씬 MemoData voiceRecordingPaths 업데이트: id={id}, 파일 수={voiceRecordingPaths?.Count ?? 0}");
                    break;
                }
            }
        }
    }

    /// <summary>
    /// MemoUIController에서 호출: 특정 메모의 isAssigned 상태 가져오기
    /// </summary>
    public bool GetMemoAssignedState(string id)
    {
        Debug.Log($"★★★ [ASSIGNEE] [TabPinCreate] GetMemoAssignedState 호출: id={id}");

        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogWarning($"★★★ [ASSIGNEE] [TabPinCreate] ✗ id가 null/empty입니다!");
            return false;
        }

        for (int i = 0; i < pinDB.pins.Count; i++)
        {
            if (pinDB.pins[i].id == id)
            {
                bool isAssigned = pinDB.pins[i].isAssigned;
                Debug.Log($"★★★ [ASSIGNEE] [TabPinCreate] ✓ GetMemoAssignedState: id={id}, isAssigned={isAssigned}");
                return isAssigned;
            }
        }

        Debug.LogWarning($"★★★ [ASSIGNEE] [TabPinCreate] ✗ id={id}를 pinDB에서 찾을 수 없습니다! pinDB.pins.Count={pinDB.pins.Count}");
        return false;
    }

    /// <summary>
    /// ImageMemoUIController에서 호출: 특정 메모의 이미지 경로 목록 업데이트
    /// </summary>
    public void UpdateMemoImagePaths(string id, List<string> imagePaths)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            if (verboseDebug) Debug.LogWarning("[TabPinCreate] UpdateMemoImagePaths: id가 null/empty입니다!");
            return;
        }

        // PinDB에 저장
        bool foundInDB = false;
        for (int i = 0; i < pinDB.pins.Count; i++)
        {
            if (pinDB.pins[i].id == id)
            {
                // 중요: 복사본을 저장 (참조 문제 방지)
                pinDB.pins[i].imagePaths = imagePaths != null ? new List<string>(imagePaths) : new List<string>();
                pinDB.pins[i].memoType = "image";  // 이미지가 있으면 image 타입으로 설정
                foundInDB = true;
                if (verboseDebug) Debug.Log($"[TabPinCreate] PinDB imagePaths 업데이트: id={id}, count={pinDB.pins[i].imagePaths.Count}");
                SavePinsForCurrentMap();
                break;
            }
        }

        if (!foundInDB && verboseDebug)
        {
            Debug.LogWarning($"[TabPinCreate] PinDB에서 id={id}를 찾을 수 없습니다!");
        }

        // 씬의 MemoData에도 적용
        if (pinsTransform != null)
        {
            for (int i = 0; i < pinsTransform.childCount; i++)
            {
                Transform child = pinsTransform.GetChild(i);
                MemoData memo = child.GetComponentInChildren<MemoData>(true);
                if (memo != null && memo.id == id)
                {
                    // 중요: 복사본을 저장 (참조 문제 방지)
                    memo.imagePaths = imagePaths != null ? new List<string>(imagePaths) : new List<string>();
                    memo.memoType = "image";
                    if (verboseDebug) Debug.Log($"[TabPinCreate] 씬 MemoData imagePaths 업데이트: id={id}");
                    break;
                }
            }
        }
    }

    /// <summary>
    /// ImageMemoUIController에서 호출: 특정 메모의 이미지 경로 목록 가져오기
    /// </summary>
    public List<string> GetMemoImagePaths(string id)
    {
        Debug.Log($"[TabPinCreate] [###] GetMemoImagePaths 호출: id={id}");

        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogWarning("[TabPinCreate] [###] GetMemoImagePaths: id가 null/empty입니다!");
            return new List<string>();
        }

        // 먼저 DB에서 찾기
        for (int i = 0; i < pinDB.pins.Count; i++)
        {
            if (pinDB.pins[i].id == id)
            {
                List<string> paths = pinDB.pins[i].imagePaths ?? new List<string>();
                Debug.Log($"[TabPinCreate] [###] GetMemoImagePaths DB 결과: id={id}, memoType={pinDB.pins[i].memoType}, imageCount={paths.Count}");

                // DB에 이미지 경로가 있으면 반환 (새 List 생성하여 반환)
                if (paths.Count > 0)
                {
                    for (int j = 0; j < paths.Count; j++)
                    {
                        Debug.Log($"[TabPinCreate] [###] DB 이미지 경로[{j}]: {paths[j]}");
                    }
                    // 중요: 새로운 List를 생성해서 반환 (참조 문제 방지)
                    List<string> result = new List<string>(paths);
                    Debug.Log($"[TabPinCreate] [###] 새 List 생성하여 반환: count={result.Count}");
                    return result;
                }

                // DB에 없으면 씬의 MemoData에서 가져오기 시도
                Debug.Log($"[TabPinCreate] [###] DB에 이미지 없음 → 씬에서 검색 시도");
                break;
            }
        }

        // 씬의 MemoData에서 찾기 (DB와 동기화 안 된 경우 대비)
        if (pinsTransform != null)
        {
            for (int i = 0; i < pinsTransform.childCount; i++)
            {
                Transform child = pinsTransform.GetChild(i);
                MemoData memo = child.GetComponentInChildren<MemoData>(true);

                if (memo != null && memo.id == id)
                {
                    List<string> scenePaths = memo.imagePaths ?? new List<string>();
                    Debug.Log($"[TabPinCreate] [###] 씬에서 발견: id={id}, imageCount={scenePaths.Count}");

                    if (scenePaths.Count > 0)
                    {
                        for (int j = 0; j < scenePaths.Count; j++)
                        {
                            Debug.Log($"[TabPinCreate] [###] 씬 이미지 경로[{j}]: {scenePaths[j]}");
                        }

                        // DB에도 동기화
                        for (int k = 0; k < pinDB.pins.Count; k++)
                        {
                            if (pinDB.pins[k].id == id)
                            {
                                pinDB.pins[k].imagePaths = new List<string>(scenePaths);
                                Debug.Log($"[TabPinCreate] [###] DB에 동기화 완료: imageCount={scenePaths.Count}");
                                break;
                            }
                        }
                    }

                    // 중요: 새로운 List를 생성해서 반환 (참조 문제 방지)
                    List<string> result = new List<string>(scenePaths);
                    Debug.Log($"[TabPinCreate] [###] 씬에서 새 List 생성하여 반환: count={result.Count}");
                    return result;
                }
            }
        }

        Debug.LogWarning($"[TabPinCreate] [###] GetMemoImagePaths: id={id}를 DB와 씬 모두에서 찾을 수 없습니다!");
        return new List<string>();
    }

    /// <summary>
    /// ImageMemoUIController에서 호출: 특정 메모의 타입 가져오기
    /// </summary>
    public string GetMemoType(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "text";

        for (int i = 0; i < pinDB.pins.Count; i++)
        {
            if (pinDB.pins[i].id == id)
            {
                return pinDB.pins[i].memoType ?? "text";
            }
        }

        return "text";
    }

    /// <summary>
    /// 이미지 메모 전체 저장 (텍스트 + 이미지 경로)
    /// </summary>
    public void SaveImageMemoById(string id, string title, string body, string location, List<string> imagePaths)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogWarning("[TabPinCreate] ★ SaveImageMemoById: id is null/empty");
            return;
        }

        Debug.Log($"[TabPinCreate] [###] SaveImageMemoById 호출: id={id}, title={title}, imageCount={imagePaths?.Count ?? 0}, DB 핀 개수={pinDB.pins.Count}");

        for (int i = 0; i < pinDB.pins.Count; i++)
        {
            if (pinDB.pins[i].id == id)
            {
                pinDB.pins[i].title = title ?? "";
                pinDB.pins[i].body = body ?? "";
                pinDB.pins[i].location = location ?? "";
                // 중요: 복사본을 저장 (참조 문제 방지 - CloseWithoutSaving()에서 Clear() 호출 시 영향 안 받도록)
                pinDB.pins[i].imagePaths = imagePaths != null ? new List<string>(imagePaths) : new List<string>();
                pinDB.pins[i].memoType = (imagePaths != null && imagePaths.Count > 0) ? "image" : "text";

                Debug.Log($"[TabPinCreate] [###] DB 업데이트 완료: id={id}, memoType={pinDB.pins[i].memoType}, imageCount={pinDB.pins[i].imagePaths.Count}");

                SavePinsForCurrentMap();

                // 씬 오브젝트도 함께 갱신
                Debug.Log($"[TabPinCreate] [###] 씬 핀 갱신 시작...");
                UpdateScenePinMemo(id, title, body, location);
                // 복사본 전달
                UpdateScenePinImagePaths(id, new List<string>(pinDB.pins[i].imagePaths));

                return;
            }
        }

        Debug.LogWarning($"[TabPinCreate] [###] SaveImageMemoById: id not found in DB: {id}");
    }

    /// <summary>
    /// 씬에 있는 핀의 이미지 경로 동기화
    /// </summary>
    private void UpdateScenePinImagePaths(string id, List<string> imagePaths)
    {
        if (pinsTransform == null)
        {
            Debug.LogWarning("[TabPinCreate] [###] UpdateScenePinImagePaths: pinsTransform is null");
            return;
        }

        Debug.Log($"[TabPinCreate] [###] UpdateScenePinImagePaths 시작: id={id}, 전달된 imagePaths.Count={imagePaths?.Count ?? 0}, 씬 핀 개수={pinsTransform.childCount}");
        if (imagePaths != null && imagePaths.Count > 0)
        {
            for (int i = 0; i < imagePaths.Count; i++)
            {
                Debug.Log($"[TabPinCreate] [###]   전달된 imagePaths[{i}]: {imagePaths[i]}");
            }
        }

        for (int i = 0; i < pinsTransform.childCount; i++)
        {
            Transform child = pinsTransform.GetChild(i);
            MemoData memo = child.GetComponentInChildren<MemoData>(true);

            if (memo == null)
            {
                Debug.Log($"[TabPinCreate] [###] 핀 {child.name}: MemoData 없음");
                continue;
            }

            if (memo.id != id)
            {
                Debug.Log($"[TabPinCreate] [###] 핀 {child.name}: ID 불일치 (찾는 ID={id}, 핀 ID={memo.id})");
                continue;
            }

            Debug.Log($"[TabPinCreate] [###] 핀 발견! {child.name}, 업데이트 전 memo.imagePaths.Count={memo.imagePaths?.Count ?? 0}");

            memo.imagePaths = imagePaths != null ? new List<string>(imagePaths) : new List<string>();
            memo.memoType = (imagePaths != null && imagePaths.Count > 0) ? "image" : "text";

            // 아이콘 스프라이트 업데이트
            ApplyIconSprite(child, memo.memoType);

            Debug.Log($"[TabPinCreate] [###] UpdateScenePinImagePaths 성공: 핀={child.name}, id={id}, memoType={memo.memoType}, 업데이트 후 count={memo.imagePaths.Count}, active={child.gameObject.activeSelf}");
            return;
        }

        Debug.LogWarning($"[TabPinCreate] [###] UpdateScenePinImagePaths: 씬에서 id={id}인 핀을 찾지 못했습니다!");
    }

    // 씬에 떠있는 핀(MemoData)도 같이 갱신해서 UI/툴팁 표시를 동기화
    private void UpdateScenePinMemo(string id, string title, string body, string location = "")
    {
        if (pinsTransform == null)
        {
            Debug.LogWarning("[TabPinCreate] ★ UpdateScenePinMemo: pinsTransform is null");
            return;
        }

        Debug.Log($"[TabPinCreate] ★ UpdateScenePinMemo: id={id}, title={title}");

        for (int i = 0; i < pinsTransform.childCount; i++)
        {
            Transform child = pinsTransform.GetChild(i);

            MemoData memo = child.GetComponentInChildren<MemoData>(true);
            if (memo == null) continue;

            if (memo.id != id) continue;

            memo.title = title ?? "";
            memo.body = body ?? "";
            memo.location = location ?? "";
            memo.content = memo.body; // 호환 유지

            // 툴팁 타이틀 텍스트 동기화
            ApplyTooltipTitle(child, memo.title);

            // 저장 완료 후에는 거리 기반으로 아이콘/툴팁 상태가 즉시 반영되게 함
            UpdateOnePinVisualByDistance(child, memo);

            Debug.Log($"[TabPinCreate] ★ UpdateScenePinMemo 성공: 핀={child.name}, id={id}, active={child.gameObject.activeSelf}");
            return;
        }

        Debug.LogWarning($"[TabPinCreate] ★ UpdateScenePinMemo: 씬에서 id={id}인 핀을 찾지 못했습니다!");
    }

    // 매 프레임: 모든 핀을 거리 기반으로 Icon/Tooltip 토글 + 타이틀 동기화
    private void UpdatePinsIconTooltipByDistance()
    {
        if (!arCamera || pinsTransform == null) return;

        for (int i = 0; i < pinsTransform.childCount; i++)
        {
            Transform pin = pinsTransform.GetChild(i);
            MemoData memo = pin.GetComponentInChildren<MemoData>(true);
            if (memo == null) continue;

            // 타이틀 동기화 (메모가 바뀌어도 자동 반영)
            ApplyTooltipTitle(pin, memo.title);

            // 아이콘 스프라이트 동기화 (memoType이 바뀌어도 자동 반영)
            ApplyIconSprite(pin, memo.memoType);

            UpdateOnePinVisualByDistance(pin, memo);
        }
    }
    // 핀 1개: 거리 기반 Icon/Tooltip 토글 규칙
    private void UpdateOnePinVisualByDistance(Transform pin, MemoData memo)
    {
        if (!arCamera || pin == null || memo == null) return;

        // (디버그 추가) 규칙이 실제로 돌고 있는지 / title / dist 확인
        if (verboseDebug)
        {
            float d = (arCamera ? Vector3.Distance(arCamera.transform.position, pin.position) : -1f);
            Debug.Log($"[TabPinCreate] VisualRule pin={pin.name} title='{memo.title}' dist={d:F2} limit={tooltipDistanceMeters:F2}");
        }

        // 작성 전: 아이콘만
        if (string.IsNullOrWhiteSpace(memo.title))
        {
            SetPinVisual(pin, showIcon: true, showTooltip: false);
            return;
        }

        float dist = Vector3.Distance(arCamera.transform.position, pin.position);
        bool showTooltip = dist <= tooltipDistanceMeters;

        // 가까우면 툴팁, 멀면 아이콘
        SetPinVisual(pin, showIcon: !showTooltip, showTooltip: showTooltip);

        WriteDebugHud(pin, memo, showTooltip);

    }

    // PinVisualRefs 찾기/캐시 (없으면 null)
    private PinVisualRefs GetPinVisualRefs(Transform pinRoot)
    {
        if (!preferPinVisualRefs || pinRoot == null) return null;

        int id = pinRoot.GetInstanceID();
        if (pinVisualCache.TryGetValue(id, out var cached) && cached != null)
            return cached;

        // 보통 루트에 붙이지만, 실수 대비해 children도 검색
        var refs = pinRoot.GetComponent<PinVisualRefs>();
        if (refs == null) refs = pinRoot.GetComponentInChildren<PinVisualRefs>(true);

        // 캐시 저장(없어도 저장해두면 매 프레임 GetComponent 비용 줄임)
        pinVisualCache[id] = refs;

        if (verboseDebug)
            Debug.Log($"[TabPinCreate] GetPinVisualRefs pin={pinRoot.name} found={(refs != null)}");

        return refs;
    }

    // 핀 프리팹 내부 IconCanvas/TooltipCanvas를 찾아 활성/비활성 처리
    private void SetPinVisual(Transform pinRoot, bool showIcon, bool showTooltip)
    {
        if (pinRoot == null) return;

        // 1순위: PinVisualRefs가 있으면 그걸 우선 사용
        GameObject iconGO = null;
        GameObject tipGO = null;
        Transform tipT = null;

        var refs = GetPinVisualRefs(pinRoot);
        if (refs != null)
        {
            iconGO = refs.iconCanvas;
            tipGO = refs.tooltipCanvas;
            tipT = (tipGO != null) ? tipGO.transform : null;
        }

        // 2순위: 없으면 이름으로 찾기(기존 방식)
        if (iconGO == null)
        {
            Transform iconTr = FindDeepChild(pinRoot, iconCanvasObjectName);
            if (iconTr != null) iconGO = iconTr.gameObject;
        }

        if (tipGO == null)
        {
            Transform tipTr = FindDeepChild(pinRoot, tooltipCanvasObjectName);
            if (tipTr != null)
            {
                tipGO = tipTr.gameObject;
                tipT = tipTr;
            }
        }

        // 빈 화면 방지: Tooltip을 못 찾았으면 아이콘은 유지
        if (showTooltip && tipGO == null)
        {
            if (iconGO != null) iconGO.SetActive(true);
            if (verboseDebug)
                Debug.LogWarning($"[TabPinCreate] TooltipCanvas를 찾지 못해 아이콘을 유지함. pinRoot={pinRoot.name}");
            return;
        }

        // 정상 토글
        if (iconGO != null) iconGO.SetActive(showIcon);
        if (tipGO != null) tipGO.SetActive(showTooltip);

        // Tooltip이 켜졌을 때만 “강제 표시 보정”
        if (!showTooltip || tipT == null) return;

        // (A) 다른 코드가 죽여놔도 다시 살리기: Canvas/CanvasGroup/Graphic
        {
            // Canvas 켜기
            var canv = tipT.GetComponent<Canvas>();
            if (canv != null) canv.enabled = true;

            // CanvasGroup이 있으면 alpha=1로 강제
            var cgs = tipT.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < cgs.Length; i++)
                cgs[i].alpha = 1f;

            // Graphic(Image/TMP 등) 강제 enable + alpha=1
            var graphics = tipT.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].enabled = true;
                var col = graphics[i].color;
                col.a = 1f;
                graphics[i].color = col;
            }

            var tmps = tipT.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmps.Length; i++)
            {
                tmps[i].enabled = true;
                var col = tmps[i].color;
                col.a = 1f;
                tmps[i].color = col;
            }
        }

        // (B) World Space일 때만 위치/회전 보정
        var tipCanvas = tipT.GetComponent<Canvas>();
        bool isWorldSpace = (tipCanvas == null) || (tipCanvas.renderMode == RenderMode.WorldSpace);

        if (isWorldSpace && arCamera != null)
        {
            Vector3 camPos = arCamera.transform.position;

            // 핀 기준 위치(핀 루트 기준 + 약간 위)
            Vector3 basePos = pinRoot.position + Vector3.up * tooltipUpOffset;

            // 카메라 쪽으로 당기되, 카메라 near clip 안으로는 못 들어가게 clamp
            Vector3 dirToCam = camPos - basePos;
            float len = dirToCam.magnitude;

            if (len > 0.0001f)
            {
                Vector3 pullDir = dirToCam / len;

                float safeFromCam = Mathf.Max(arCamera.nearClipPlane + 0.06f, 0.10f);
                float pull = Mathf.Min(tooltipPullTowardCamera, Mathf.Max(0f, len - safeFromCam));

                tipT.position = basePos + pullDir * pull;
            }
            else
            {
                tipT.position = basePos;
            }

            // 빌보드 + 정면 뒤집힘 보정
            if (tooltipBillboardToCamera)
            {
                Vector3 toCam = camPos - tipT.position;
                if (toCam.sqrMagnitude > 0.0001f)
                {
                    // 1) 카메라를 향하게 회전
                    tipT.rotation = Quaternion.LookRotation(-toCam, Vector3.up);

                    // 2) 만약 “정면이 반대”라면 180도 뒤집기
                    // (TMP/이미지 셰이더가 한쪽면만 그릴 때 안 보이는 문제 해결용)
                    // 뒤집기 보정 로직 제거 - LookRotation(-toCam)으로 이미 올바른 방향
                }
            }
        }
    }



    // TooltipCanvas 안의 TMP_Text에 타이틀 적용 (인스펙터 드래그 연결 없이도 동작)
    private void ApplyTooltipTitle(Transform pinRoot, string title)
    {
        if (pinRoot == null) return;

        // 1순위: PinVisualRefs.titleText 사용
        var refs = GetPinVisualRefs(pinRoot);
        if (refs != null && refs.titleText != null)
        {
            refs.titleText.enableWordWrapping = false;
            refs.titleText.overflowMode = TextOverflowModes.Ellipsis;

            string newText = title ?? "";
            if (refs.titleText.text != newText)
                refs.titleText.text = newText;

            return;
        }

        // 2순위: 기존 방식(TooltipCanvas 아래에서 TMP_Text 찾아 적용)
        Transform tipT = FindDeepChild(pinRoot, tooltipCanvasObjectName);
        if (tipT == null) return;

        TMP_Text target = null;

        // 이름 지정이 있으면 우선 찾기
        if (!string.IsNullOrWhiteSpace(tooltipTitleObjectName))
        {
            Transform t = FindDeepChild(tipT, tooltipTitleObjectName);
            if (t != null) target = t.GetComponent<TMP_Text>();
        }

        // 없으면 TooltipCanvas 아래에서 첫 TMP_Text 사용
        if (target == null)
            target = tipT.GetComponentInChildren<TMP_Text>(true);

        if (target != null)
        {
            // 한 줄 + ... 처리 (TMP 설정이 안 되어도 강제)
            target.enableWordWrapping = false;
            target.overflowMode = TextOverflowModes.Ellipsis;

            string newText = title ?? "";
            if (target.text != newText)
                target.text = newText;
        }
    }

    /// <summary>
    /// IconCanvas 안의 Icon Image에 memoType에 맞는 스프라이트 적용
    /// </summary>
    private void ApplyIconSprite(Transform pinRoot, string memoType)
    {
        if (pinRoot == null) return;

        // memoType에 따라 스프라이트 선택
        Sprite targetSprite = null;
        switch (memoType)
        {
            case "text":
                targetSprite = textIconSprite;
                break;
            case "image":
                targetSprite = imageIconSprite;
                break;
            case "checklist":
                targetSprite = checklistIconSprite;
                break;
            case "voicememo":
                targetSprite = voiceIconSprite;
                break;
            default:
                targetSprite = textIconSprite; // 기본값
                break;
        }

        if (targetSprite == null)
        {
            if (verboseDebug)
                Debug.LogWarning($"[TabPinCreate] ApplyIconSprite - {memoType}용 스프라이트가 할당되지 않았습니다!");
            return;
        }

        // IconCanvas 찾기
        Transform iconCanvasT = FindDeepChild(pinRoot, iconCanvasObjectName);
        if (iconCanvasT == null)
        {
            if (verboseDebug)
                Debug.LogWarning($"[TabPinCreate] ApplyIconSprite - IconCanvas를 찾을 수 없습니다: {pinRoot.name}");
            return;
        }

        // Icon 오브젝트 찾기
        Transform iconT = FindDeepChild(iconCanvasT, iconObjectName);
        if (iconT == null)
        {
            if (verboseDebug)
                Debug.LogWarning($"[TabPinCreate] ApplyIconSprite - Icon 오브젝트를 찾을 수 없습니다: {pinRoot.name}");
            return;
        }

        // Image 컴포넌트 가져오기
        Image iconImage = iconT.GetComponent<Image>();
        if (iconImage == null)
        {
            if (verboseDebug)
                Debug.LogWarning($"[TabPinCreate] ApplyIconSprite - Icon에 Image 컴포넌트가 없습니다: {pinRoot.name}");
            return;
        }

        // 스프라이트 적용
        iconImage.sprite = targetSprite;

        if (verboseDebug)
            Debug.Log($"[TabPinCreate] ApplyIconSprite 완료: pin={pinRoot.name}, memoType={memoType}, sprite={targetSprite.name}");
    }

    // 이름으로 자식 오브젝트를 재귀 탐색
    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null || string.IsNullOrWhiteSpace(name)) return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform c = parent.GetChild(i);
            if (c.name == name) return c;

            Transform r = FindDeepChild(c, name);
            if (r != null) return r;
        }

        return null;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (!root) return;
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }

    // 근처 생성 금지 판단
    private bool TryBlockCreateNearExisting(Vector3 worldPos, out GameObject nearPin)
    {
        nearPin = null;
        if (!pinsTransform) return false;
        if (preventCreateNearDistance <= 0f) return false;

        float best = preventCreateNearDistance;
        Transform bestT = null;

        for (int i = 0; i < pinsTransform.childCount; i++)
        {
            Transform t = pinsTransform.GetChild(i);
            float d = Vector3.Distance(worldPos, t.position);
            if (d <= best)
            {
                best = d;
                bestT = t;
            }
        }

        if (bestT != null)
        {
            nearPin = bestT.gameObject;
            return true;
        }

        return false;
    }


    // 툴팁이 "켜졌는데도 안 보이는" 케이스 강제 복구
    private void ForceMakeTooltipVisible(GameObject tipGO)
    {
        if (!tipGO) return;

        // Canvas 강제 활성 + 정렬 강제
        var canvas = tipGO.GetComponent<Canvas>();
        if (canvas)
        {
            canvas.enabled = true;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 5000; // 충분히 크게
            canvas.worldCamera = arCamera; // World Space라도 지정해두면 안전
        }

        // CanvasRenderer가 cull 중이면 다시 풀기
        var renderers = tipGO.GetComponentsInChildren<CanvasRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i]) renderers[i].cull = false;
        }

        // Graphic(이미지/TMP 포함) 전부 enable + 알파 복구
        var graphics = tipGO.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            var g = graphics[i];
            if (!g) continue;
            g.enabled = true;

            var c = g.color;
            if (c.a < 0.99f) c.a = 1f;
            g.color = c;

            g.raycastTarget = false;
        }

        // 스케일이 너무 작으면 “보이는 수준”까지 임시로 키움
        var t = tipGO.transform;
        if (t.localScale.x < 0.001f) t.localScale = Vector3.one * 0.005f;
    }


    // 실제로 tooltip이 active 상태인지 확인 (모바일 로그 최소화용)
    private void LogTooltipStateOnce(Transform pinRoot, GameObject tipGO, bool showTooltip)
    {
        if (!verboseDebug) return;
        if (!pinRoot || !tipGO) { Debug.Log($"[TabPinCreate] TooltipState pin={pinRoot?.name} tipGO=null showTooltip={showTooltip}"); return; }
        Debug.Log($"[TabPinCreate] TooltipState pin={pinRoot.name} showTooltip={showTooltip} tipActiveSelf={tipGO.activeSelf} tipActiveInHierarchy={tipGO.activeInHierarchy} scale={tipGO.transform.localScale}");
    }

    private void WriteDebugHud(Transform pin, MemoData memo, bool wantTooltip)
    {
        if (!showRuntimeDebugHud || debugHudText == null || arCamera == null || pin == null) return;

        // refs 찾기
        var refs = GetPinVisualRefs(pin);

        bool iconActive = false;
        bool tipActive = false;

        if (refs != null)
        {
            if (refs.iconCanvas != null) iconActive = refs.iconCanvas.activeInHierarchy;
            if (refs.tooltipCanvas != null) tipActive = refs.tooltipCanvas.activeInHierarchy;
        }

        // 툴팁 오브젝트/캔버스 정보
        Transform tipT = null;
        Canvas tipCanvas = null;

        if (refs != null && refs.tooltipCanvas != null)
        {
            tipT = refs.tooltipCanvas.transform;
            tipCanvas = refs.tooltipCanvas.GetComponent<Canvas>();
        }

        float dist = Vector3.Distance(arCamera.transform.position, pin.position);

        float dot = -999f;
        if (tipT != null)
        {
            Vector3 toCam = (arCamera.transform.position - tipT.position).normalized;
            dot = Vector3.Dot(tipT.forward.normalized, toCam);
        }

        debugHudText.text =
            $"[PIN DEBUG]\n" +
            $"title='{(memo != null ? memo.title : "null")}'\n" +
            $"dist={dist:F2} / limit={tooltipDistanceMeters:F2}\n" +
            $"wantTooltip={wantTooltip}\n" +
            $"iconActive={iconActive}\n" +
            $"tipActive={tipActive}\n" +
            $"tipCanvas={(tipCanvas ? "YES" : "NO")}\n" +
            $"overrideSorting={(tipCanvas ? tipCanvas.overrideSorting.ToString() : "-")}\n" +
            $"sortingOrder={(tipCanvas ? tipCanvas.sortingOrder.ToString() : "-")}\n" +
            $"worldCamera={(tipCanvas && tipCanvas.worldCamera ? tipCanvas.worldCamera.name : "null")}\n" +
            $"dot(tipForward,toCam)={dot:F2}\n";
    }

    // MapBrowser 씬의 UI Canvas 정리 (탭 입력 차단 방지)
    private void CleanupPreviousSceneUICanvases()
    {
        // 정리 대상 Canvas 이름들 (MapBrowser 씬 전용)
        string[] canvasNamesToCleanup = new string[]
        {
            "SplashCanvas",
            "AuthCanvas",
            "HomeCanvas",
            "BackgroundCanvas"
        };

        // 모든 Canvas를 찾아서 정리
        Canvas[] allCanvases = UnityEngine.Object.FindObjectsOfType<Canvas>(true);

        int cleanedCount = 0;
        int graphicCount = 0;

        foreach (Canvas canvas in allCanvases)
        {
            // Canvas 이름으로 확인
            foreach (string targetName in canvasNamesToCleanup)
            {
                if (canvas.gameObject.name == targetName)
                {
                    // MapBrowser 씬의 Canvas인지 확인 (씬 이름으로 구분)
                    // DontDestroyOnLoad 오브젝트는 scene이 null이 아니므로 이름으로 구분
                    bool isMapBrowserCanvas = canvas.gameObject.scene.name == "MapBrowser" ||
                                              canvas.gameObject.scene.name == "";  // DontDestroyOnLoad

                    if (isMapBrowserCanvas)
                    {
                        // Canvas 완전 비활성화
                        canvas.gameObject.SetActive(false);
                        cleanedCount++;

                        if (verboseDebug)
                            Debug.Log($"[TabPinCreate] MapBrowser Canvas 비활성화: {canvas.gameObject.name} (scene={canvas.gameObject.scene.name})");

                        // 모든 Graphic의 raycastTarget도 끔 (추가 안전장치)
                        UnityEngine.UI.Graphic[] graphics = canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                        foreach (var graphic in graphics)
                        {
                            graphic.raycastTarget = false;
                            graphicCount++;
                        }

                        // GraphicRaycaster도 끔
                        UnityEngine.UI.GraphicRaycaster raycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                        if (raycaster != null)
                        {
                            raycaster.enabled = false;
                        }
                    }
                    else
                    {
                        if (verboseDebug)
                            Debug.Log($"[TabPinCreate] Canvas '{canvas.gameObject.name}'는 다른 씬 것이므로 건너뜀 (scene={canvas.gameObject.scene.name})");
                    }

                    break;
                }
            }
        }

        if (verboseDebug)
            Debug.Log($"[TabPinCreate] MapBrowser UI 정리 완료 - {cleanedCount}개 Canvas 비활성화, {graphicCount}개 Graphic raycastTarget 끔");
    }

    /// <summary>
    /// 외부에서 지정된 위치에 핀을 생성하는 공개 메서드
    /// AttachMemoController 등 외부 스크립트에서 사용
    /// </summary>
    /// <param name="worldPosition">핀 생성 월드 위치</param>
    /// <param name="worldRotation">핀 생성 월드 회전</param>
    /// <returns>생성된 핀 GameObject (실패 시 null)</returns>
    public GameObject CreatePinAtPosition(Vector3 worldPosition, Quaternion worldRotation)
    {
        if (verboseDebug) Debug.Log($"[TabPinCreate] CreatePinAtPosition start pos={worldPosition}");

        // 필수 참조 체크
        if (pinsTransform == null || pinPrefab == null)
        {
            Debug.LogWarning($"[TabPinCreate] CreatePinAtPosition blocked: missing refs pinsTransform={(pinsTransform ? "OK" : "NULL")}, pinPrefab={(pinPrefab ? "OK" : "NULL")}");
            return null;
        }

        // 근처에 기존 핀이 있으면 새로 만들지 않기
        if (TryBlockCreateNearExisting(worldPosition, out GameObject nearPin))
        {
            if (verboseDebug) Debug.Log("[TabPinCreate] CreatePinAtPosition blocked: near existing pin");
            return nearPin; // 기존 핀 반환
        }

        // 핀 인스턴스 생성
        GameObject pin = Instantiate(pinPrefab);
        if (verboseDebug) Debug.Log($"[TabPinCreate] Instantiate pin={pin.name} (activeSelf={pin.activeSelf})");

        pin.transform.SetPositionAndRotation(worldPosition, worldRotation);
        pin.transform.SetParent(pinsTransform, worldPositionStays: true);

        // MemoData 컴포넌트 보장 + 고유 ID 생성
        MemoData memo = pin.GetComponent<MemoData>();
        if (memo == null) memo = pin.AddComponent<MemoData>();

        memo.id = Guid.NewGuid().ToString("N");
        memo.title = "";
        memo.body = "";
        memo.content = memo.body;
        memo.isAssigned = false;

        // 초기 상태 및 타임스탬프 설정
        memo.status = MemoStatus.Active;
        string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        memo.createdAt = now;
        memo.updatedAt = now;
        memo.version = 1;

        if (verboseDebug) Debug.Log($"[TabPinCreate] MemoData assigned id={memo.id}");

        // 생성 직후는 아이콘만 보여야 함
        SetPinVisual(pin.transform, showIcon: true, showTooltip: false);

        // 툴팁 타이틀 텍스트 동기화
        ApplyTooltipTitle(pin.transform, memo.title);

        // 현재 선택된 핀 갱신
        currentSelectedPin = pin;

        // pinDB에 저장
        PinData data = new PinData
        {
            pinMapId = pinMapId,
            localPos = pin.transform.localPosition,
            localRot = pin.transform.localRotation,
            id = memo.id,
            title = memo.title,
            body = memo.body,
            location = memo.location,
            status = memo.status.ToString(),
            createdAt = memo.createdAt,
            updatedAt = memo.updatedAt,
            version = memo.version
        };

        pinDB.pins.Add(data);
        SavePinsForCurrentMap();

        if (verboseDebug)
            Debug.Log($"[TabPinCreate] CreatePinAtPosition completed: mapId={pinMapId}, totalPins={pinDB.pins.Count}");

        return pin;
    }

    // ========== 외부에서 메모 개수 조회 메서드들 (싱글톤 사용) ==========

    /// <summary>
    /// 현재 맵의 총 메모 개수 반환
    /// </summary>
    public int GetTotalCount()
    {
        return pinDB?.pins?.Count ?? 0;
    }

    /// <summary>
    /// Active 상태 메모 개수 반환
    /// </summary>
    public int GetActiveCount()
    {
        if (pinDB?.pins == null) return 0;
        return pinDB.pins.FindAll(p => p.status == "Active").Count;
    }

    /// <summary>
    /// Completed 상태 메모 개수 반환
    /// </summary>
    public int GetCompletedCount()
    {
        if (pinDB?.pins == null) return 0;
        return pinDB.pins.FindAll(p => p.status == "Completed").Count;
    }

    /// <summary>
    /// Archived 상태 메모 개수 반환
    /// </summary>
    public int GetArchivedCount()
    {
        if (pinDB?.pins == null) return 0;
        return pinDB.pins.FindAll(p => p.status == "Archived").Count;
    }

    /// <summary>
    /// 모든 상태별 개수를 딕셔너리로 반환
    /// </summary>
    public Dictionary<string, int> GetAllCounts()
    {
        return new Dictionary<string, int>
        {
            { "Total", GetTotalCount() },
            { "Active", GetActiveCount() },
            { "Completed", GetCompletedCount() },
            { "Archived", GetArchivedCount() }
        };
    }

    /// <summary>
    /// 메모 타입별 개수 반환
    /// </summary>
    public Dictionary<string, int> GetCountsByType()
    {
        var counts = new Dictionary<string, int>();
        if (pinDB?.pins == null) return counts;

        foreach (var pin in pinDB.pins)
        {
            string type = pin.memoType ?? "text";
            if (!counts.ContainsKey(type))
                counts[type] = 0;
            counts[type]++;
        }

        return counts;
    }

    /// <summary>
    /// 특정 상태의 메모 개수 반환
    /// </summary>
    public int GetCountByStatus(string status)
    {
        if (pinDB?.pins == null) return 0;
        return pinDB.pins.FindAll(p => p.status == status).Count;
    }

    /// <summary>
    /// 특정 타입의 메모 개수 반환
    /// </summary>
    public int GetCountByType(string memoType)
    {
        if (pinDB?.pins == null) return 0;
        return pinDB.pins.FindAll(p => p.memoType == memoType).Count;
    }

    /// <summary>
    /// 특정 인덱스의 메모 데이터 반환
    /// </summary>
    public PinData GetPinDataAtIndex(int index)
    {
        if (pinDB?.pins == null || index < 0 || index >= pinDB.pins.Count)
            return null;

        return pinDB.pins[index];
    }

    /// <summary>
    /// 모든 메모 데이터 리스트 반환
    /// </summary>
    public List<PinData> GetAllPinData()
    {
        if (pinDB?.pins == null)
            return new List<PinData>();

        return new List<PinData>(pinDB.pins);
    }

}