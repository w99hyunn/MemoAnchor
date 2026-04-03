using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// VoiceElementOffsetSettings는 CalendarController.cs에 정의되어 있음

public class TimePickerController : MonoBehaviour
{
    // ==================== Panel_TextMemo ====================
    [Header("========== Panel_TextMemo ==========")]
    [Space(5)]
    
    [Header("TextMemo - UI References")]
    [SerializeField] private Button timeBt;                     // 시간 버튼
    [SerializeField] private TMP_Text timeButtonText;           // 시간 버튼 텍스트
    [SerializeField] private GameObject timePickerPanel;        // 시간 선택 패널
    [SerializeField] private Button closeButton;                // 패널 닫기 버튼
    [SerializeField] private Transform timeButtonsContainer;    // 시간 버튼들의 부모
    
    [Header("TextMemo - AM/PM Buttons")]
    [SerializeField] private Button amButton;                   // AM 버튼
    [SerializeField] private Button pmButton;                   // PM 버튼
    [SerializeField] private TMP_Text amButtonText;             // AM 버튼 텍스트
    [SerializeField] private TMP_Text pmButtonText;             // PM 버튼 텍스트
    
    [Header("TextMemo - Layout References")]
    [SerializeField] private RectTransform timePickerPanelRect; // TimePickerPanel RectTransform
    [SerializeField] private RectTransform calendarPanelRect;   // CalendarPanel RectTransform
    [SerializeField] private RectTransform deadlineRow;         // Deadline 행
    [SerializeField] private RectTransform emergencyRow;        // Emergency 행
    [SerializeField] private RectTransform assigneeRow;         // AssigneeRow
    [SerializeField] private RectTransform inputFieldBody;      // InputField_Body
    [SerializeField] private RectTransform scrollContent;       // ScrollRect의 Content
    [SerializeField] private UnityEngine.UI.ScrollRect scrollRect; // ScrollRect 컴포넌트
    
    [Header("TextMemo - Element Position")]
    [SerializeField] private bool useManualDeadlinePosition = false;
    [SerializeField] private float deadlineTargetY = 0f;
    [SerializeField] private float deadlineOffset = 0f;
    [SerializeField] private float emergencyOffset = 0f;
    [SerializeField] private float assigneeOffset = 0f;
    [SerializeField] private float bodyOffset = 0f;
    
    [Header("TextMemo - Scroll Settings")]
    [SerializeField] private float autoScrollOnOpen = 0f;
    [SerializeField] private float autoScrollOnOpenFromCalendar = 0f;
    [SerializeField] [Range(0f, 1f)] private float scrollBottomLimit = 0f;
    
    // ==================== Panel_ImageMemo ====================
    [Header("========== Panel_ImageMemo ==========")]
    [Space(5)]
    
    [Header("ImageMemo - UI References")]
    [SerializeField] private Button imageTimeBt;                // 시간 버튼
    [SerializeField] private TMP_Text imageTimeButtonText;      // 시간 버튼 텍스트
    [SerializeField] private GameObject imageMemoTimePickerPanel; // 시간 선택 패널
    [SerializeField] private Button imageCloseButton;           // 패널 닫기 버튼
    [SerializeField] private Transform imageMemoTimeButtonsContainer; // 시간 버튼들의 부모
    
    [Header("ImageMemo - AM/PM Buttons")]
    [SerializeField] private Button imageAmButton;              // AM 버튼
    [SerializeField] private Button imagePmButton;              // PM 버튼
    [SerializeField] private TMP_Text imageAmButtonText;        // AM 버튼 텍스트
    [SerializeField] private TMP_Text imagePmButtonText;        // PM 버튼 텍스트
    
    [Header("ImageMemo - Layout References")]
    [SerializeField] private RectTransform imageMemoTimePickerPanelRect; // TimePickerPanel RectTransform
    [SerializeField] private RectTransform imageMemoCalendarPanelRect;   // CalendarPanel RectTransform
    [SerializeField] private RectTransform imageMemoDeadlineRow;         // Deadline 행
    [SerializeField] private RectTransform imageMemoEmergencyRow;        // Emergency 행
    [SerializeField] private RectTransform imageMemoAssigneeRow;         // AssigneeRow
    [SerializeField] private RectTransform imageMemoInputFieldBody;      // InputField_Body
    [SerializeField] private RectTransform imageMemoImageMovie;          // ImageMovie
    [SerializeField] private RectTransform imageMemoScrollContent;       // ScrollRect의 Content
    [SerializeField] private UnityEngine.UI.ScrollRect imageMemoScrollRect; // ScrollRect 컴포넌트
    
    [Header("ImageMemo - Element Position")]
    [SerializeField] private bool imageMemoUseManualDeadlinePosition = false;
    [SerializeField] private float imageMemoDeadlineTargetY = 0f;
    [SerializeField] private float imageMemoDeadlineOffset = 0f;
    [SerializeField] private float imageMemoEmergencyOffset = 0f;
    [SerializeField] private float imageMemoAssigneeOffset = 0f;
    [SerializeField] private float imageMemoBodyOffset = 0f;
    [SerializeField] private float imageMemoImageMovieOffset = 0f;
    
    [Header("ImageMemo - Scroll Settings")]
    [SerializeField] private float imageMemoAutoScrollOnOpen = 0f;
    [SerializeField] private float imageMemoAutoScrollOnOpenFromCalendar = 0f;
    [SerializeField] private float imageMemoAutoScrollOnClose = 0f;
    [SerializeField] [Range(0f, 1f)] private float imageMemoScrollBottomLimit = 0f;
    
    // ==================== Panel_Checklist ====================
    [Header("========== Panel_Checklist ==========")]
    [Space(5)]
    
    [Header("Checklist - UI References")]
    [SerializeField] private Button checklistTimeBt;                // 시간 버튼
    [SerializeField] private TMP_Text checklistTimeButtonText;      // 시간 버튼 텍스트
    [SerializeField] private GameObject checklistTimePickerPanel;   // 시간 선택 패널
    [SerializeField] private Button checklistCloseButton;           // 패널 닫기 버튼
    [SerializeField] private Transform checklistTimeButtonsContainer; // 시간 버튼들의 부모
    
    [Header("Checklist - AM/PM Buttons")]
    [SerializeField] private Button checklistAmButton;              // AM 버튼
    [SerializeField] private Button checklistPmButton;              // PM 버튼
    [SerializeField] private TMP_Text checklistAmButtonText;        // AM 버튼 텍스트
    [SerializeField] private TMP_Text checklistPmButtonText;        // PM 버튼 텍스트
    
    [Header("Checklist - Layout References")]
    [SerializeField] private RectTransform checklistTimePickerPanelRect; // TimePickerPanel RectTransform
    [SerializeField] private RectTransform checklistCalendarPanelRect;   // CalendarPanel RectTransform
    [SerializeField] private RectTransform checklistDeadlineRow;         // Deadline 행
    [SerializeField] private RectTransform checklistEmergencyRow;        // Emergency 행
    [SerializeField] private RectTransform checklistCheckListRow;        // CheckList
    [SerializeField] private RectTransform checklistScrollContent;       // ScrollRect의 Content
    [SerializeField] private UnityEngine.UI.ScrollRect checklistScrollRect; // ScrollRect 컴포넌트
    
    [Header("Checklist - Element Position")]
    [SerializeField] private bool checklistUseManualDeadlinePosition = false;
    [SerializeField] private float checklistDeadlineTargetY = 0f;
    [SerializeField] private float checklistDeadlineOffset = 0f;
    [SerializeField] private float checklistEmergencyOffset = 0f;
    [SerializeField] private float checklistCheckListOffset = 0f;
    
    [Header("Checklist - Scroll Settings")]
    [SerializeField] private float checklistScrollExpansion = 500f;
    [SerializeField] private float checklistStretchCompensationMultiplier = 0f;
    [SerializeField] private float checklistAutoScrollOnOpen = 0f;
    [SerializeField] private float checklistAutoScrollOnOpenFromCalendar = 0f;
    [SerializeField] private float checklistAutoScrollOnClose = 0f;
    [SerializeField] [Range(0f, 1f)] private float checklistScrollBottomLimit = 0f;
    
    // ==================== Panel_VoiceMemo ====================
    [Header("========== Panel_VoiceMemo ==========")]
    [Space(5)]
    
    [Header("VoiceMemo - UI References")]
    [SerializeField] private Button voiceMemoTimeBt;                // 시간 버튼
    [SerializeField] private TMP_Text voiceMemoTimeButtonText;      // 시간 버튼 텍스트
    [SerializeField] private GameObject voiceMemoTimePickerPanel;   // 시간 선택 패널
    [SerializeField] private Button voiceMemoCloseButton;           // 패널 닫기 버튼
    [SerializeField] private Transform voiceMemoTimeButtonsContainer; // 시간 버튼들의 부모
    
    [Header("VoiceMemo - AM/PM Buttons")]
    [SerializeField] private Button voiceMemoAmButton;              // AM 버튼
    [SerializeField] private Button voiceMemoPmButton;              // PM 버튼
    [SerializeField] private TMP_Text voiceMemoAmButtonText;        // AM 버튼 텍스트
    [SerializeField] private TMP_Text voiceMemoPmButtonText;        // PM 버튼 텍스트
    
    [Header("VoiceMemo - Layout References")]
    [SerializeField] private RectTransform voiceMemoTimePickerPanelRect; // TimePickerPanel RectTransform
    [SerializeField] private RectTransform voiceMemoCalendarPanelRect;   // CalendarPanel RectTransform
    [SerializeField] private RectTransform voiceMemoDeadlineRow;         // Deadline 행
    [SerializeField] private RectTransform voiceMemoEmergencyRow;        // Emergency 행
    [SerializeField] private RectTransform voiceMemoVoiceMemoRow;        // VoiceMemo (메모 목록 영역)
    [SerializeField] private RectTransform voiceMemoScrollContent;       // ScrollRect의 Content
    [SerializeField] private UnityEngine.UI.ScrollRect voiceMemoScrollRect; // ScrollRect 컴포넌트
    
    [Header("VoiceMemo - Voice Element Offset Settings")]
    [Tooltip("Voice 하위 요소들의 오프셋 설정 (VoiceTitle, VoiceContents 등)")]
    [SerializeField] private VoiceElementOffsetSettings[] voiceMemoVoiceElements;
    
    [Header("VoiceMemo - Element Position")]
    [SerializeField] private bool voiceMemoUseManualDeadlinePosition = false;
    [SerializeField] private float voiceMemoDeadlineTargetY = 0f;
    [SerializeField] private float voiceMemoDeadlineOffset = 0f;
    [SerializeField] private float voiceMemoEmergencyOffset = 0f;
    [SerializeField] private float voiceMemoVoiceMemoOffset = 0f;
    
    [Header("VoiceMemo - Scroll Settings")]
    [SerializeField] private float voiceMemoScrollExpansion = 500f;
    [SerializeField] private float voiceMemoStretchCompensationMultiplier = 0f;
    [SerializeField] private float voiceMemoAutoScrollOnOpen = 0f;
    [SerializeField] private float voiceMemoAutoScrollOnOpenFromCalendar = 0f;
    [SerializeField] private float voiceMemoAutoScrollOnClose = 0f;
    [SerializeField] [Range(0f, 1f)] private float voiceMemoScrollBottomLimit = 0f;
    
    
    // ==================== Common Settings ====================
    [Header("========== Common Settings ==========")]
    [Space(5)]
    
    [Header("Common - Prefab")]
    [SerializeField] private GameObject timeButtonPrefab;       // 시간 버튼 프리팹
    
    [Header("Common - Cross References")]
    [SerializeField] private CalendarController calendarController;
    [SerializeField] private ChecklistUIController checklistUIController;
    [SerializeField] private VoiceMemoUIController voiceMemoUIController;
    [SerializeField] private AssigneeDropdownManager assigneeDropdownManager;
    [SerializeField] private GameObject panelChecklist;
    [SerializeField] private GameObject panelVoiceMemo;
    
    [Header("Common - Settings")]
    [SerializeField] private string timeButtonImageName = "Image";
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private float scrollHeightMultiplier = 2f;
    [SerializeField] private float extraScrollPadding = 200f;
    
    [Header("Common - Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(0.27f, 0.65f, 0.80f, 1f);
    [SerializeField] private Color timeButtonOpenColor = new Color(0.59f, 0.80f, 0.88f, 1f);
    [SerializeField] private Color timeButtonOutlineUnselected = new Color(0.59f, 0.80f, 0.88f, 1f);
    [SerializeField] private Color timeButtonOutlineSelected = new Color(0.85f, 0.85f, 0.85f, 1f);
    [SerializeField] private Color normalTextColor = Color.black;
    [SerializeField] private Color selectedTextColor = Color.white;
    
    // ==================== Private Variables ====================
    private float checklistItemScrollExpansion = 0f;
    
    private bool isAM = true;                                   // AM/PM 상태 (true = AM, false = PM)
    private int selectedHour = 9;                               // 선택된 시간 (1~12)
    private int selectedMinute = 0;                             // 선택된 분 (0, 30)
    private bool isTimePickerOpen = false;                      // 시간 선택 패널 열림 상태
    private UnityEngine.UI.Image timeButtonImage;               // TimeBt 배경 이미지 (Panel_TextMemo)
    private UnityEngine.UI.Outline timeButtonOutline;           // TimeBt 배경 Outline (Panel_TextMemo)
    private UnityEngine.UI.Image imageTimeButtonImage;          // TimeBt 배경 이미지 (Panel_ImageMemo)
    private UnityEngine.UI.Outline imageTimeButtonOutline;      // TimeBt 배경 Outline (Panel_ImageMemo)
    private List<GameObject> timeButtonObjects = new List<GameObject>(); // 생성된 시간 버튼들
    private Dictionary<string, GameObject> timeButtonMap = new Dictionary<string, GameObject>(); // 시간-버튼 매핑
    
    // 레이아웃 애니메이션을 위한 원래 위치 저장 (Panel_TextMemo)
    private Vector2 timePickerOriginalPos;                      // TimePickerPanel 원래 위치
    private Vector2 deadlineOriginalPos;                        // Deadline 원래 위치
    private Vector2 emergencyOriginalPos;                       // Emergency 원래 위치
    private Vector2 assigneeOriginalPos;                        // AssigneeRow 원래 위치
    private Vector2 bodyOriginalPos;                            // InputField_Body 원래 위치
    private float scrollContentOriginalHeight;                  // ScrollContent 원래 높이
    private Vector2 scrollContentOriginalPos;                   // ScrollContent 원래 위치
    private Dictionary<Transform, Vector2> childOriginalPositions = new Dictionary<Transform, Vector2>(); // 모든 자식의 원래 위치
    
    // 레이아웃 애니메이션을 위한 원래 위치 저장 (Panel_ImageMemo)
    private Vector2 imageMemoTimePickerOriginalPos;             // TimePickerPanel 원래 위치
    private Vector2 imageMemoDeadlineOriginalPos;               // Deadline 원래 위치
    private Vector2 imageMemoEmergencyOriginalPos;              // Emergency 원래 위치
    private Vector2 imageMemoAssigneeOriginalPos;               // AssigneeRow 원래 위치
    private Vector2 imageMemoBodyOriginalPos;                   // InputField_Body 원래 위치
    private Vector2 imageMemoImageMovieOriginalPos;             // ImageMovie 원래 위치
    private float imageMemoScrollContentOriginalHeight;         // ScrollContent 원래 높이
    private Vector2 imageMemoScrollContentOriginalPos;          // ScrollContent 원래 위치
    private Dictionary<Transform, Vector2> imageMemoChildOriginalPositions = new Dictionary<Transform, Vector2>(); // 모든 자식의 원래 위치
    
    // Panel_Checklist 원래 위치 저장
    private Vector2 checklistTimePickerOriginalPos;             // Checklist TimePickerPanel 원래 위치
    private Vector2 checklistDeadlineOriginalPos;               // Checklist Deadline 원래 위치
    private Vector2 checklistEmergencyOriginalPos;              // Checklist Emergency 원래 위치
    private Vector2 checklistCheckListOriginalPos;              // Checklist CheckList 원래 위치
    private float checklistScrollContentOriginalHeight;         // Checklist ScrollContent 원래 높이
    private Vector2 checklistScrollContentOriginalPos;          // Checklist ScrollContent 원래 위치
    private Dictionary<Transform, Vector2> checklistChildOriginalPositions = new Dictionary<Transform, Vector2>(); // Checklist 모든 자식의 원래 위치
    
    // Checklist 버튼 이미지
    private UnityEngine.UI.Image checklistTimeButtonImage;
    private UnityEngine.UI.Outline checklistTimeButtonOutline;
    
    // Panel_VoiceMemo 원래 위치 저장
    private Vector2 voiceMemoTimePickerOriginalPos;             // VoiceMemo TimePickerPanel 원래 위치
    private Vector2 voiceMemoDeadlineOriginalPos;               // VoiceMemo Deadline 원래 위치
    private Vector2 voiceMemoEmergencyOriginalPos;              // VoiceMemo Emergency 원래 위치
    private Vector2 voiceMemoVoiceMemoOriginalPos;              // VoiceMemo VoiceMemo 원래 위치
    private float voiceMemoScrollContentOriginalHeight;         // VoiceMemo ScrollContent 원래 높이
    private Vector2 voiceMemoScrollContentOriginalPos;          // VoiceMemo ScrollContent 원래 위치
    private Dictionary<Transform, Vector2> voiceMemoChildOriginalPositions = new Dictionary<Transform, Vector2>(); // VoiceMemo 모든 자식의 원래 위치
    private Dictionary<RectTransform, Vector2> voiceMemoVoiceElementsOriginalPos = new Dictionary<RectTransform, Vector2>(); // VoiceMemo Voice 요소들의 원래 위치
    
    // VoiceMemo 버튼 이미지
    private UnityEngine.UI.Image voiceMemoTimeButtonImage;
    private UnityEngine.UI.Outline voiceMemoTimeButtonOutline;
    
    // VoiceMemo 아이템 스크롤 확장량
    private float voiceMemoItemScrollExpansion = 0f;
    
    private void Start()
    {
        // 초기 시간 설정 (09:00 AM)
        selectedHour = 9;
        selectedMinute = 0;
        isAM = true;
        
        // ★★★ AssigneeDropdownManager 자동 검색 ★★★
        if (assigneeDropdownManager == null)
        {
            assigneeDropdownManager = FindObjectOfType<AssigneeDropdownManager>();
            if (assigneeDropdownManager != null)
            {
                Debug.Log("[TimePickerController] AssigneeDropdownManager 자동 검색 완료");
            }
        }
        
        // TimeBt 배경 이미지 가져오기 (Panel_TextMemo)
        if (timeBt != null)
        {
            Transform imageTransform = timeBt.transform.Find(timeButtonImageName);
            if (imageTransform != null)
            {
                timeButtonImage = imageTransform.GetComponent<UnityEngine.UI.Image>();
                
                // Outline 컴포넌트 가져오기 (없으면 추가)
                timeButtonOutline = imageTransform.GetComponent<UnityEngine.UI.Outline>();
                if (timeButtonOutline == null)
                {
                    timeButtonOutline = imageTransform.gameObject.AddComponent<UnityEngine.UI.Outline>();
                }
                
                // Outline 초기 설정 (선택 전 색상)
                if (timeButtonOutline != null)
                {
                    timeButtonOutline.effectColor = timeButtonOutlineUnselected;
                    timeButtonOutline.effectDistance = new Vector2(4, -4);
                    Debug.Log($"[TimePickerController] ▶▶▶ TimeBt Outline 초기화 완료: {timeButtonOutline.gameObject.name}");
                }
            }
            else
            {
                Debug.LogWarning($"[TimePickerController] ▶▶▶ TimeBt에서 '{timeButtonImageName}' 자식을 찾을 수 없습니다!");
            }
        }
        
        // ImageTimeBt 배경 이미지 가져오기 (Panel_ImageMemo)
        if (imageTimeBt != null)
        {
            Transform imageTransform = imageTimeBt.transform.Find(timeButtonImageName);
            if (imageTransform != null)
            {
                imageTimeButtonImage = imageTransform.GetComponent<UnityEngine.UI.Image>();
                
                // Outline 컴포넌트 가져오기 (없으면 추가)
                imageTimeButtonOutline = imageTransform.GetComponent<UnityEngine.UI.Outline>();
                if (imageTimeButtonOutline == null)
                {
                    imageTimeButtonOutline = imageTransform.gameObject.AddComponent<UnityEngine.UI.Outline>();
                }
                
                // Outline 초기 설정 (선택 전 색상)
                if (imageTimeButtonOutline != null)
                {
                    imageTimeButtonOutline.effectColor = timeButtonOutlineUnselected;
                    imageTimeButtonOutline.effectDistance = new Vector2(4, -4);
                    Debug.Log($"[TimePickerController] ▶▶▶ ImageTimeBt Outline 초기화 완료: {imageTimeButtonOutline.gameObject.name}");
                }
            }
            else
            {
                Debug.LogWarning($"[TimePickerController] ▶▶▶ ImageTimeBt에서 '{timeButtonImageName}' 자식을 찾을 수 없습니다!");
            }
            
            // ImageTimeButtonText 자동 검색
            if (imageTimeButtonText == null)
            {
                imageTimeButtonText = imageTimeBt.GetComponentInChildren<TMP_Text>();
            }
        }
        
        // TimePickerPanel RectTransform 자동 할당 (Panel_TextMemo)
        if (timePickerPanelRect == null && timePickerPanel != null)
        {
            timePickerPanelRect = timePickerPanel.GetComponent<RectTransform>();
        }
        
        // TimePickerPanel RectTransform 자동 할당 (Panel_ImageMemo)
        if (imageMemoTimePickerPanelRect == null && imageMemoTimePickerPanel != null)
        {
            imageMemoTimePickerPanelRect = imageMemoTimePickerPanel.GetComponent<RectTransform>();
        }
        
        // 원래 위치 저장 (Panel_TextMemo)
        if (timePickerPanelRect != null) timePickerOriginalPos = timePickerPanelRect.anchoredPosition;
        if (deadlineRow != null) deadlineOriginalPos = deadlineRow.anchoredPosition;
        if (emergencyRow != null) emergencyOriginalPos = emergencyRow.anchoredPosition;
        if (assigneeRow != null) assigneeOriginalPos = assigneeRow.anchoredPosition;
        if (inputFieldBody != null) bodyOriginalPos = inputFieldBody.anchoredPosition;
        
        // 원래 위치 저장 (Panel_ImageMemo)
        if (imageMemoTimePickerPanelRect != null) imageMemoTimePickerOriginalPos = imageMemoTimePickerPanelRect.anchoredPosition;
        if (imageMemoDeadlineRow != null) imageMemoDeadlineOriginalPos = imageMemoDeadlineRow.anchoredPosition;
        if (imageMemoEmergencyRow != null) imageMemoEmergencyOriginalPos = imageMemoEmergencyRow.anchoredPosition;
        if (imageMemoAssigneeRow != null) imageMemoAssigneeOriginalPos = imageMemoAssigneeRow.anchoredPosition;
        if (imageMemoInputFieldBody != null) imageMemoBodyOriginalPos = imageMemoInputFieldBody.anchoredPosition;
        if (imageMemoImageMovie != null) imageMemoImageMovieOriginalPos = imageMemoImageMovie.anchoredPosition;
        
        // ScrollContent 원래 높이 및 모든 자식 위치 저장 (Panel_TextMemo)
        if (scrollContent != null)
        {
            scrollContentOriginalHeight = scrollContent.sizeDelta.y;
            scrollContentOriginalPos = scrollContent.anchoredPosition;
            
            // 모든 자식의 원래 위치 저장
            foreach (Transform child in scrollContent)
            {
                RectTransform childRect = child.GetComponent<RectTransform>();
                if (childRect != null)
                {
                    childOriginalPositions[child] = childRect.anchoredPosition;
                }
            }
            Debug.Log($"[TimePickerController] Panel_TextMemo ScrollContent 자식 {childOriginalPositions.Count}개의 원래 위치 저장");
        }
        
        // ScrollContent 원래 높이 및 모든 자식 위치 저장 (Panel_ImageMemo)
        if (imageMemoScrollContent != null)
        {
            imageMemoScrollContentOriginalHeight = imageMemoScrollContent.sizeDelta.y;
            imageMemoScrollContentOriginalPos = imageMemoScrollContent.anchoredPosition;
            
            // 모든 자식의 원래 위치 저장
            foreach (Transform child in imageMemoScrollContent)
            {
                RectTransform childRect = child.GetComponent<RectTransform>();
                if (childRect != null)
                {
                    imageMemoChildOriginalPositions[child] = childRect.anchoredPosition;
                }
            }
            Debug.Log($"[TimePickerController] Panel_ImageMemo ScrollContent 자식 {imageMemoChildOriginalPositions.Count}개의 원래 위치 저장");
        }
        
        // ScrollRect 할당 확인
        if (scrollRect == null)
        {
            Debug.LogWarning("[TimePickerController] ScrollRect가 할당되지 않았습니다! Inspector에서 할당해주세요.");
        }
        else
        {
            Debug.Log($"[TimePickerController] ScrollRect 할당 확인: {scrollRect.name}");
        }
        
        // TimeButtonsContainer의 Grid Layout Group 패딩 설정
        if (timeButtonsContainer != null)
        {
            UnityEngine.UI.GridLayoutGroup gridLayout = timeButtonsContainer.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            if (gridLayout != null)
            {
                gridLayout.padding.top = 10; // 상단 여백 10px
            }
        }
        
        // 버튼 이벤트 연결 (Panel_TextMemo)
        if (timeBt != null)
        {
            timeBt.onClick.AddListener(OnTimeButtonClicked);
        }
        
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseButtonClicked);
        }
        
        if (amButton != null)
        {
            amButton.onClick.AddListener(OnAMButtonClicked);
        }
        
        if (pmButton != null)
        {
            pmButton.onClick.AddListener(OnPMButtonClicked);
        }
        
        // 버튼 이벤트 연결 (Panel_ImageMemo)
        if (imageTimeBt != null)
        {
            imageTimeBt.onClick.AddListener(OnTimeButtonClicked);
            Debug.Log("[TimePickerController] ImageTimeBt onClick 리스너 연결 완료");
        }
        else
        {
            Debug.LogError("[TimePickerController] ImageTimeBt가 할당되지 않았습니다! Inspector에서 할당해주세요.");
        }
        
        if (imageCloseButton != null)
        {
            imageCloseButton.onClick.AddListener(OnCloseButtonClicked);
        }
        
        if (imageAmButton != null)
        {
            imageAmButton.onClick.AddListener(OnAMButtonClicked);
        }
        
        if (imagePmButton != null)
        {
            imagePmButton.onClick.AddListener(OnPMButtonClicked);
        }
        
        // ★★★ 버튼 이벤트 연결 (Panel_Checklist) ★★★
        if (checklistTimeBt != null)
        {
            checklistTimeBt.onClick.AddListener(OnTimeButtonClicked);
            Debug.Log("[TimePickerController] ChecklistTimeBt onClick 리스너 연결 완료");
        }
        
        if (checklistCloseButton != null)
        {
            checklistCloseButton.onClick.AddListener(OnCloseButtonClicked);
        }
        
        if (checklistAmButton != null)
        {
            checklistAmButton.onClick.AddListener(OnAMButtonClicked);
            Debug.Log("[TimePickerController] ChecklistAmButton onClick 리스너 연결 완료");
        }
        
        if (checklistPmButton != null)
        {
            checklistPmButton.onClick.AddListener(OnPMButtonClicked);
            Debug.Log("[TimePickerController] ChecklistPmButton onClick 리스너 연결 완료");
        }
        
        // ★★★ 버튼 이벤트 연결 (Panel_VoiceMemo) ★★★
        if (voiceMemoTimeBt != null)
        {
            voiceMemoTimeBt.onClick.AddListener(OnTimeButtonClicked);
            Debug.Log("[TimePickerController] VoiceMemoTimeBt onClick 리스너 연결 완료");
        }
        
        if (voiceMemoCloseButton != null)
        {
            voiceMemoCloseButton.onClick.AddListener(OnCloseButtonClicked);
        }
        
        if (voiceMemoAmButton != null)
        {
            voiceMemoAmButton.onClick.AddListener(OnAMButtonClicked);
            Debug.Log("[TimePickerController] VoiceMemoAmButton onClick 리스너 연결 완료");
        }
        
        if (voiceMemoPmButton != null)
        {
            voiceMemoPmButton.onClick.AddListener(OnPMButtonClicked);
            Debug.Log("[TimePickerController] VoiceMemoPmButton onClick 리스너 연결 완료");
        }
        
        // 시간 선택 패널 초기 비활성화
        if (timePickerPanel != null)
        {
            timePickerPanel.SetActive(false);
        }
        if (imageMemoTimePickerPanel != null)
        {
            imageMemoTimePickerPanel.SetActive(false);
        }
        if (checklistTimePickerPanel != null)
        {
            checklistTimePickerPanel.SetActive(false);
        }
        if (voiceMemoTimePickerPanel != null)
        {
            voiceMemoTimePickerPanel.SetActive(false);
        }
        
        // ★★★ ChecklistTimeBt 배경 이미지 및 텍스트 가져오기 ★★★
        if (checklistTimeBt != null)
        {
            // 텍스트 자동 할당 (Inspector에서 할당되지 않은 경우)
            if (checklistTimeButtonText == null)
            {
                checklistTimeButtonText = checklistTimeBt.GetComponentInChildren<TMP_Text>();
                if (checklistTimeButtonText != null)
                {
                    Debug.Log($"[TimePickerController] ChecklistTimeBt 텍스트 자동 할당 완료: {checklistTimeButtonText.gameObject.name}");
                }
            }
            
            Transform imageTransform = checklistTimeBt.transform.Find(timeButtonImageName);
            if (imageTransform != null)
            {
                checklistTimeButtonImage = imageTransform.GetComponent<UnityEngine.UI.Image>();
                checklistTimeButtonOutline = imageTransform.GetComponent<UnityEngine.UI.Outline>();
                if (checklistTimeButtonOutline == null)
                {
                    checklistTimeButtonOutline = imageTransform.gameObject.AddComponent<UnityEngine.UI.Outline>();
                    Debug.Log("[TimePickerController] ▶▶▶ ChecklistTimeBt에 Outline 컴포넌트 추가됨");
                }
                if (checklistTimeButtonOutline != null)
                {
                    checklistTimeButtonOutline.effectColor = timeButtonOutlineUnselected;
                    checklistTimeButtonOutline.effectDistance = new Vector2(4, -4);
                    Debug.Log($"[TimePickerController] ▶▶▶ ChecklistTimeBt Outline 초기화 완료: {checklistTimeButtonOutline.gameObject.name}");
                }
            }
            else
            {
                Debug.LogWarning($"[TimePickerController] ▶▶▶ ChecklistTimeBt에서 '{timeButtonImageName}' 자식을 찾을 수 없습니다!");
            }
        }
        else
        {
            Debug.LogWarning("[TimePickerController] ▶▶▶ checklistTimeBt가 NULL입니다! Inspector에서 할당 필요");
        }
        
        // ★★★ Panel_Checklist 원래 위치 저장 ★★★
        if (checklistTimePickerPanelRect != null) checklistTimePickerOriginalPos = checklistTimePickerPanelRect.anchoredPosition;
        if (checklistDeadlineRow != null) checklistDeadlineOriginalPos = checklistDeadlineRow.anchoredPosition;
        if (checklistEmergencyRow != null) checklistEmergencyOriginalPos = checklistEmergencyRow.anchoredPosition;
        if (checklistCheckListRow != null) checklistCheckListOriginalPos = checklistCheckListRow.anchoredPosition;
        
        if (checklistScrollContent != null)
        {
            checklistScrollContentOriginalHeight = checklistScrollContent.sizeDelta.y;
            checklistScrollContentOriginalPos = checklistScrollContent.anchoredPosition;
            foreach (Transform child in checklistScrollContent)
            {
                RectTransform childRect = child.GetComponent<RectTransform>();
                if (childRect != null)
                {
                    checklistChildOriginalPositions[child] = childRect.anchoredPosition;
                }
            }
            Debug.Log($"[TimePickerController] Panel_Checklist ScrollContent 자식 {checklistChildOriginalPositions.Count}개의 원래 위치 저장");
        }
        
        // ★★★ VoiceMemoTimeBt 배경 이미지 및 텍스트 가져오기 ★★★
        if (voiceMemoTimeBt != null)
        {
            // 텍스트 자동 할당 (Inspector에서 할당되지 않은 경우)
            if (voiceMemoTimeButtonText == null)
            {
                voiceMemoTimeButtonText = voiceMemoTimeBt.GetComponentInChildren<TMP_Text>();
                if (voiceMemoTimeButtonText != null)
                {
                    Debug.Log($"[TimePickerController] VoiceMemoTimeBt 텍스트 자동 할당 완료: {voiceMemoTimeButtonText.gameObject.name}");
                }
            }
            
            Transform imageTransform = voiceMemoTimeBt.transform.Find(timeButtonImageName);
            if (imageTransform != null)
            {
                voiceMemoTimeButtonImage = imageTransform.GetComponent<UnityEngine.UI.Image>();
                voiceMemoTimeButtonOutline = imageTransform.GetComponent<UnityEngine.UI.Outline>();
                if (voiceMemoTimeButtonOutline == null)
                {
                    voiceMemoTimeButtonOutline = imageTransform.gameObject.AddComponent<UnityEngine.UI.Outline>();
                    Debug.Log("[TimePickerController] ▶▶▶ VoiceMemoTimeBt에 Outline 컴포넌트 추가됨");
                }
                if (voiceMemoTimeButtonOutline != null)
                {
                    voiceMemoTimeButtonOutline.effectColor = timeButtonOutlineUnselected;
                    voiceMemoTimeButtonOutline.effectDistance = new Vector2(4, -4);
                    Debug.Log($"[TimePickerController] ▶▶▶ VoiceMemoTimeBt Outline 초기화 완료: {voiceMemoTimeButtonOutline.gameObject.name}");
                }
            }
            else
            {
                Debug.LogWarning($"[TimePickerController] ▶▶▶ VoiceMemoTimeBt에서 '{timeButtonImageName}' 자식을 찾을 수 없습니다!");
            }
        }
        
        // ★★★ Panel_VoiceMemo 원래 위치 저장 ★★★
        if (voiceMemoTimePickerPanelRect != null) voiceMemoTimePickerOriginalPos = voiceMemoTimePickerPanelRect.anchoredPosition;
        if (voiceMemoDeadlineRow != null) voiceMemoDeadlineOriginalPos = voiceMemoDeadlineRow.anchoredPosition;
        if (voiceMemoEmergencyRow != null) voiceMemoEmergencyOriginalPos = voiceMemoEmergencyRow.anchoredPosition;
        if (voiceMemoVoiceMemoRow != null) voiceMemoVoiceMemoOriginalPos = voiceMemoVoiceMemoRow.anchoredPosition;
        
        // VoiceMemo Voice 요소들의 원래 위치 저장
        if (voiceMemoVoiceElements != null && voiceMemoVoiceElements.Length > 0)
        {
            Debug.Log($"[TimePickerController] VoiceMemo Voice 요소 개수: {voiceMemoVoiceElements.Length}");
            foreach (var voiceElement in voiceMemoVoiceElements)
            {
                if (voiceElement != null && voiceElement.element != null)
                {
                    voiceMemoVoiceElementsOriginalPos[voiceElement.element] = voiceElement.element.anchoredPosition;
                    Debug.Log($"[TimePickerController] VoiceMemo Voice 요소 원래 위치 저장: {voiceElement.element.name} = {voiceElement.element.anchoredPosition}");
                }
            }
        }
        else
        {
            Debug.LogWarning("[TimePickerController] voiceMemoVoiceElements가 비어있거나 NULL입니다! Inspector에서 Voice 요소들을 할당해주세요.");
        }
        
        Debug.Log($"[TimePickerController] ★★★ 원래 위치 비교 ★★★");
        Debug.Log($"[TimePickerController] Checklist Deadline: {checklistDeadlineOriginalPos}, VoiceMemo Deadline: {voiceMemoDeadlineOriginalPos}");
        Debug.Log($"[TimePickerController] Checklist Emergency: {checklistEmergencyOriginalPos}, VoiceMemo Emergency: {voiceMemoEmergencyOriginalPos}");
        
        if (voiceMemoScrollContent != null)
        {
            voiceMemoScrollContentOriginalHeight = voiceMemoScrollContent.sizeDelta.y;
            voiceMemoScrollContentOriginalPos = voiceMemoScrollContent.anchoredPosition;
            foreach (Transform child in voiceMemoScrollContent)
            {
                RectTransform childRect = child.GetComponent<RectTransform>();
                if (childRect != null)
                {
                    voiceMemoChildOriginalPositions[child] = childRect.anchoredPosition;
                }
            }
            Debug.Log($"[TimePickerController] Panel_VoiceMemo ScrollContent 자식 {voiceMemoChildOriginalPositions.Count}개의 원래 위치 저장");
        }
        
        // ★★★ VoiceMemoUIController 자동 검색 ★★★
        if (voiceMemoUIController == null)
        {
            voiceMemoUIController = FindObjectOfType<VoiceMemoUIController>();
            if (voiceMemoUIController != null)
            {
                Debug.Log("[TimePickerController] VoiceMemoUIController 자동 검색 완료");
            }
        }
        
        // 시간 버튼 텍스트 초기화
        UpdateTimeButtonText();
        
        // Inspector 참조 상태 확인
        if (calendarController == null)
        {
            Debug.LogWarning("[TimePickerController] CalendarController 참조가 할당되지 않았습니다! ImageMemo 패널 전환이 작동하지 않을 수 있습니다.");
        }
        else
        {
            Debug.Log("[TimePickerController] CalendarController 참조 정상 할당됨");
        }
    }
    
    // ========== 패널 감지 헬퍼 메서드 ==========
    
    /// <summary>
    /// 현재 ImageMemo 패널이 활성화되어 있는지 확인
    /// </summary>
    private bool IsImageMemoPanelActive()
    {
        // imageMemoScrollContent가 활성화되어 있으면 ImageMemo 패널이 활성화된 것
        return imageMemoScrollContent != null && imageMemoScrollContent.gameObject.activeInHierarchy;
    }
    
    private bool IsChecklistPanelActive()
    {
        // panelChecklist가 활성화되어 있으면 Checklist 패널이 활성화된 것
        if (panelChecklist != null)
            return panelChecklist.activeInHierarchy;
        return checklistScrollContent != null && checklistScrollContent.gameObject.activeInHierarchy;
    }
    
    private bool IsVoiceMemoPanelActive()
    {
        // panelVoiceMemo가 활성화되어 있으면 VoiceMemo 패널이 활성화된 것
        if (panelVoiceMemo != null)
            return panelVoiceMemo.activeInHierarchy;
        return voiceMemoScrollContent != null && voiceMemoScrollContent.gameObject.activeInHierarchy;
    }
    
    /// <summary>
    /// 특정 요소가 부모의 자손인지 확인 (ScrollContent 안에 있는지 확인용)
    /// </summary>
    private bool IsDescendantOf(Transform child, Transform parent)
    {
        if (child == null || parent == null) return false;
        Transform current = child.parent;
        while (current != null)
        {
            if (current == parent) return true;
            current = current.parent;
        }
        return false;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 TimePickerPanel 가져오기
    /// </summary>
    private GameObject GetActiveTimePickerPanel()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoTimePickerPanel;
        if (IsChecklistPanelActive()) return checklistTimePickerPanel;
        if (IsImageMemoPanelActive()) return imageMemoTimePickerPanel;
        return timePickerPanel;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 TimePickerPanelRect 가져오기
    /// </summary>
    private RectTransform GetActiveTimePickerPanelRect()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoTimePickerPanelRect;
        if (IsChecklistPanelActive()) return checklistTimePickerPanelRect;
        if (IsImageMemoPanelActive()) return imageMemoTimePickerPanelRect;
        return timePickerPanelRect;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 TimeButtonsContainer 가져오기
    /// </summary>
    private Transform GetActiveTimeButtonsContainer()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoTimeButtonsContainer;
        if (IsChecklistPanelActive()) return checklistTimeButtonsContainer;
        if (IsImageMemoPanelActive()) return imageMemoTimeButtonsContainer;
        return timeButtonsContainer;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 원래 TimePickerPanel 위치 가져오기
    /// </summary>
    private Vector2 GetActiveTimePickerOriginalPos()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoTimePickerOriginalPos;
        if (IsChecklistPanelActive()) return checklistTimePickerOriginalPos;
        if (IsImageMemoPanelActive()) return imageMemoTimePickerOriginalPos;
        return timePickerOriginalPos;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 ScrollContent 가져오기
    /// </summary>
    private RectTransform GetActiveScrollContent()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoScrollContent;
        if (IsChecklistPanelActive()) return checklistScrollContent;
        if (IsImageMemoPanelActive()) return imageMemoScrollContent;
        return scrollContent;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 ScrollRect 가져오기
    /// </summary>
    private UnityEngine.UI.ScrollRect GetActiveScrollRect()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoScrollRect;
        if (IsChecklistPanelActive()) return checklistScrollRect;
        if (IsImageMemoPanelActive()) return imageMemoScrollRect;
        return scrollRect;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 DeadlineRow 가져오기
    /// </summary>
    private RectTransform GetActiveDeadlineRow()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoDeadlineRow;
        if (IsChecklistPanelActive()) return checklistDeadlineRow;
        if (IsImageMemoPanelActive()) return imageMemoDeadlineRow;
        return deadlineRow;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 EmergencyRow 가져오기
    /// </summary>
    private RectTransform GetActiveEmergencyRow()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoEmergencyRow;
        if (IsChecklistPanelActive()) return checklistEmergencyRow;
        if (IsImageMemoPanelActive()) return imageMemoEmergencyRow;
        return emergencyRow;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 AssigneeRow 가져오기
    /// </summary>
    private RectTransform GetActiveAssigneeRow()
    {
        if (IsVoiceMemoPanelActive()) return null;
        if (IsChecklistPanelActive()) return null;
        if (IsImageMemoPanelActive()) return imageMemoAssigneeRow;
        return assigneeRow;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 InputFieldBody 가져오기
    /// </summary>
    private RectTransform GetActiveInputFieldBody()
    {
        if (IsVoiceMemoPanelActive()) return null;
        if (IsChecklistPanelActive()) return null;
        if (IsImageMemoPanelActive()) return imageMemoInputFieldBody;
        return inputFieldBody;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 ImageMovie 가져오기 (ImageMemo 전용)
    /// </summary>
    private RectTransform GetActiveImageMovie()
    {
        return IsImageMemoPanelActive() ? imageMemoImageMovie : null;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 CalendarPanelRect 가져오기
    /// </summary>
    private RectTransform GetActiveCalendarPanelRect()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoCalendarPanelRect;
        if (IsChecklistPanelActive()) return checklistCalendarPanelRect;
        if (IsImageMemoPanelActive()) return imageMemoCalendarPanelRect;
        return calendarPanelRect;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 원래 위치 값들 가져오기
    /// </summary>
    private void GetActiveOriginalPositions(out Vector2 deadlineOrig, out Vector2 emergencyOrig, out Vector2 assigneeOrig, out Vector2 bodyOrig,
                                            out Vector2 imageMovieOrig, out float scrollHeightOrig, out Vector2 scrollPosOrig, out Dictionary<Transform, Vector2> childPosOrig)
    {
        if (IsVoiceMemoPanelActive())
        {
            deadlineOrig = voiceMemoDeadlineOriginalPos;
            emergencyOrig = voiceMemoEmergencyOriginalPos;
            assigneeOrig = Vector2.zero; // VoiceMemo에는 AssigneeRow 없음
            bodyOrig = Vector2.zero; // VoiceMemo에는 InputFieldBody 없음
            imageMovieOrig = Vector2.zero;
            scrollHeightOrig = voiceMemoScrollContentOriginalHeight;
            scrollPosOrig = voiceMemoScrollContentOriginalPos;
            childPosOrig = voiceMemoChildOriginalPositions;
        }
        else if (IsChecklistPanelActive())
        {
            deadlineOrig = checklistDeadlineOriginalPos;
            emergencyOrig = checklistEmergencyOriginalPos;
            assigneeOrig = Vector2.zero; // Checklist에는 AssigneeRow 없음
            bodyOrig = Vector2.zero; // Checklist에는 InputFieldBody 없음
            imageMovieOrig = Vector2.zero;
            scrollHeightOrig = checklistScrollContentOriginalHeight;
            scrollPosOrig = checklistScrollContentOriginalPos;
            childPosOrig = checklistChildOriginalPositions;
        }
        else if (IsImageMemoPanelActive())
        {
            deadlineOrig = imageMemoDeadlineOriginalPos;
            emergencyOrig = imageMemoEmergencyOriginalPos;
            assigneeOrig = imageMemoAssigneeOriginalPos;
            bodyOrig = imageMemoBodyOriginalPos;
            imageMovieOrig = imageMemoImageMovieOriginalPos;
            scrollHeightOrig = imageMemoScrollContentOriginalHeight;
            scrollPosOrig = imageMemoScrollContentOriginalPos;
            childPosOrig = imageMemoChildOriginalPositions;
        }
        else
        {
            deadlineOrig = deadlineOriginalPos;
            emergencyOrig = emergencyOriginalPos;
            assigneeOrig = assigneeOriginalPos;
            bodyOrig = bodyOriginalPos;
            imageMovieOrig = Vector2.zero; // TextMemo에는 ImageMovie가 없음
            scrollHeightOrig = scrollContentOriginalHeight;
            scrollPosOrig = scrollContentOriginalPos;
            childPosOrig = childOriginalPositions;
        }
    }
    
    // 스크롤 위치 제한 (TimePickerPanel 열렸을 때만)
    private void LateUpdate()
    {
        if (!isTimePickerOpen)
            return;
        
        // 현재 활성화된 패널의 ScrollRect와 ScrollBottomLimit 가져오기
        UnityEngine.UI.ScrollRect activeScrollRect = GetActiveScrollRect();
        float activeScrollBottomLimit = IsImageMemoPanelActive() ? imageMemoScrollBottomLimit : scrollBottomLimit;
        
        // TimePickerPanel이 열려있고, scrollBottomLimit가 설정되어 있을 때만 적용
        if (activeScrollRect != null && activeScrollBottomLimit > 0f)
        {
            // verticalNormalizedPosition: 1 = 맨위, 0 = 맨아래
            // scrollBottomLimit가 0.3이면 맨아래 30% 지점까지만 스크롤 가능
            if (activeScrollRect.verticalNormalizedPosition < activeScrollBottomLimit)
            {
                activeScrollRect.verticalNormalizedPosition = activeScrollBottomLimit;
            }
        }
    }
    
    // TimeBt 클릭 시 시간 선택 패널 열기/닫기
    private void OnTimeButtonClicked()
    {
        string panelName = IsVoiceMemoPanelActive() ? "Panel_VoiceMemo" : (IsChecklistPanelActive() ? "Panel_Checklist" : (IsImageMemoPanelActive() ? "Panel_ImageMemo" : "Panel_TextMemo"));
        Debug.Log($"[TimePickerController] TimeBt 클릭됨 ({panelName}). 현재 TimePicker 상태: {isTimePickerOpen}");
        
        // 다른 패널(Calendar)이 열려있으면 먼저 닫기 (스크롤 위치 복원 없이)
        if (calendarController != null && calendarController.IsCalendarOpen())
        {
            Debug.Log($"[TimePickerController] {panelName}: Calendar가 열려있음 → Calendar 닫고 TimePicker 열기 (스크롤 위치 전환)");
            calendarController.CloseCalendar(restoreScrollPosition: false);
            isTimePickerOpen = true; // 시간 선택기를 열 예정
            OpenTimePicker(fromCalendar: true);
            return;
        }
        
        if (calendarController == null)
        {
            Debug.LogWarning($"[TimePickerController] {panelName}: CalendarController 참조가 null입니다! Inspector에서 할당해주세요.");
        }
        
        isTimePickerOpen = !isTimePickerOpen;
        
        if (isTimePickerOpen)
        {
            Debug.Log($"[TimePickerController] {panelName}: TimePicker 열기");
            OpenTimePicker();
        }
        else
        {
            Debug.Log($"[TimePickerController] {panelName}: TimePicker 닫기");
            CloseTimePickerInternal();
        }
    }
    
    // CloseButton 클릭 시 패널 닫기
    private void OnCloseButtonClicked()
    {
        if (isTimePickerOpen)
        {
            isTimePickerOpen = false;
            CloseTimePickerInternal();
        }
    }
    
    // 시간 선택 패널 열기
    private void OpenTimePicker(bool fromCalendar = false)
    {
        // 현재 활성화된 패널의 요소들 가져오기
        RectTransform activeScrollContent = GetActiveScrollContent();
        RectTransform activeDeadlineRow = GetActiveDeadlineRow();
        RectTransform activeEmergencyRow = GetActiveEmergencyRow();
        RectTransform activeAssigneeRow = GetActiveAssigneeRow();
        RectTransform activeInputFieldBody = GetActiveInputFieldBody();
        RectTransform activeImageMovie = GetActiveImageMovie();
        GetActiveOriginalPositions(out Vector2 deadlineOrig, out Vector2 emergencyOrig, out Vector2 assigneeOrig,
                                   out Vector2 bodyOrig, out Vector2 imageMovieOrig, out float scrollHeightOrig, 
                                   out Vector2 scrollPosOrig, out Dictionary<Transform, Vector2> childPosOrig);
        
        string panelName = IsVoiceMemoPanelActive() ? "Panel_VoiceMemo" : (IsChecklistPanelActive() ? "Panel_Checklist" : (IsImageMemoPanelActive() ? "Panel_ImageMemo" : "Panel_TextMemo"));
        Debug.Log($"[TimePickerController] OpenTimePicker - 현재 활성 패널: {panelName}");
        
        // ★★★ Checklist/VoiceMemo 패널일 때는 ScrollContent 조작 건너뜀 ★★★
        bool isChecklistActive = IsChecklistPanelActive();
        bool isVoiceMemoActive = IsVoiceMemoPanelActive();
        
        // ★★★ ScrollContent와 자식들은 항상 리셋 (위치 누적 방지) - Checklist/VoiceMemo 제외 ★★★
        if (!isChecklistActive && !isVoiceMemoActive && activeScrollContent != null)
        {
            // ScrollContent 높이 및 위치 복원
            activeScrollContent.sizeDelta = new Vector2(activeScrollContent.sizeDelta.x, scrollHeightOrig);
            activeScrollContent.anchoredPosition = scrollPosOrig;
            
            // 모든 자식을 원래 위치로 복원 (fromCalendar 여부와 관계없이 항상 리셋)
            foreach (Transform child in activeScrollContent)
            {
                if (childPosOrig.TryGetValue(child, out Vector2 originalPos))
                {
                    RectTransform childRect = child.GetComponent<RectTransform>();
                    if (childRect != null)
                    {
                        childRect.anchoredPosition = originalPos;
                    }
                }
            }
            Debug.Log($"[TimePickerController] {panelName} ScrollContent 리셋 완료 (fromCalendar: {fromCalendar})");
        }
        
        // ★★★ 특정 요소들도 원래 위치로 리셋 (ScrollContent 확장 전에) - Checklist/VoiceMemo 제외 ★★★
        if (!isChecklistActive && !isVoiceMemoActive)
        {
            if (activeDeadlineRow != null) activeDeadlineRow.anchoredPosition = deadlineOrig;
            if (activeEmergencyRow != null) activeEmergencyRow.anchoredPosition = emergencyOrig;
            if (activeAssigneeRow != null) activeAssigneeRow.anchoredPosition = assigneeOrig;
            if (activeInputFieldBody != null) activeInputFieldBody.anchoredPosition = bodyOrig;
            if (activeImageMovie != null) activeImageMovie.anchoredPosition = imageMovieOrig;
            Debug.Log($"[TimePickerController] {panelName} 특정 요소들 원래 위치로 리셋");
        }
        
        // 패널 활성화 (현재 활성화된 패널에 맞는 TimePickerPanel)
        GameObject activeTimePickerPanel = GetActiveTimePickerPanel();
        RectTransform activeTimePickerPanelRect = GetActiveTimePickerPanelRect();
        Vector2 activeTimePickerOriginalPos = GetActiveTimePickerOriginalPos();
        
        if (activeTimePickerPanel != null)
        {
            activeTimePickerPanel.SetActive(true);
            
            // ★★★ Checklist/VoiceMemo 패널일 때는 TimePickerPanel 위치 건드리지 않음 ★★★
            if (!isChecklistActive && !isVoiceMemoActive && activeTimePickerPanelRect != null)
            {
                activeTimePickerPanelRect.anchoredPosition = activeTimePickerOriginalPos;
                Debug.Log($"[TimePickerController] {panelName} TimePickerPanel 위치 리셋: {activeTimePickerOriginalPos}");
            }
        }
        
        // TimeBt 배경색 변경 (Panel_TextMemo)
        if (timeButtonImage != null && !IsImageMemoPanelActive() && !isChecklistActive && !isVoiceMemoActive)
        {
            StartCoroutine(AnimateButtonColor(timeButtonImage, timeButtonOpenColor));
        }
        
        // ImageTimeBt 배경색 변경 (Panel_ImageMemo)
        if (imageTimeButtonImage != null && IsImageMemoPanelActive() && !isChecklistActive && !isVoiceMemoActive)
        {
            StartCoroutine(AnimateButtonColor(imageTimeButtonImage, timeButtonOpenColor));
        }
        
        // ChecklistTimeBt 배경색 변경 (Panel_Checklist)
        if (checklistTimeButtonImage != null && isChecklistActive)
        {
            StartCoroutine(AnimateButtonColor(checklistTimeButtonImage, timeButtonOpenColor));
        }
        
        // VoiceMemoTimeBt 배경색 변경 (Panel_VoiceMemo)
        if (voiceMemoTimeButtonImage != null && isVoiceMemoActive)
        {
            StartCoroutine(AnimateButtonColor(voiceMemoTimeButtonImage, timeButtonOpenColor));
        }
        
        // 시간 버튼 생성 (처음 열 때만)
        if (timeButtonObjects.Count == 0)
        {
            CreateTimeButtons();
        }
        
        // AM/PM 버튼 상태 업데이트
        UpdateAMPMButtons();
        
        // 시간 버튼 색상 업데이트
        UpdateTimeButtonColors();
        
        // ★★★ Checklist 패널일 때는 TimePickerPanel 활성화 + ScrollContent 확장 + Emergency만 이동 ★★★
        if (isChecklistActive)
        {
            // TimePickerPanel 높이 가져오기
            float timePickerHeight = 0f;
            if (checklistTimePickerPanelRect != null)
            {
                timePickerHeight = checklistTimePickerPanelRect.sizeDelta.y;
            }
            
            // 체크리스트 아이템으로 인한 밀림량 및 확장량 가져오기
            float pushAmount = 0f;
            if (checklistUIController != null)
            {
                pushAmount = checklistUIController.LastPushAmount;
                // ★★★ 현재 contentExpansion 가져와서 설정 ★★★
                checklistItemScrollExpansion = checklistUIController.LastContentExpansion;
            }
            
            // 동적 확장량 계산: 기본 확장량 + 체크리스트 아이템 확장량
            // 주의: pushAmount는 위치 계산용이므로 스크롤 확장에는 포함하지 않음
            float dynamicExpansion = checklistScrollExpansion + checklistItemScrollExpansion;
            
            // ★★★ RefreshChecklistLayout에서 모든 위치와 크기를 설정하므로 여기서는 호출만 ★★★
            // pushAmount = 체크리스트 아이템으로 인한 밀림량
            RefreshChecklistLayout(pushAmount);
            Debug.Log($"[TimePickerController] OpenTimePicker에서 RefreshChecklistLayout 호출 - pushAmount: {pushAmount}, itemScrollExp: {checklistItemScrollExpansion}");
            
            // ★★★ 자동 스크롤 (Open) - 밀림량만큼 추가 스크롤 ★★★
            if (checklistScrollRect != null)
            {
                float baseScrollAmount = fromCalendar ? checklistAutoScrollOnOpenFromCalendar : checklistAutoScrollOnOpen;
                // 동적 스크롤량: 기본 스크롤량 + 밀림량
                float scrollAmount = baseScrollAmount + pushAmount;
                Debug.Log($"[TimePickerController] 자동 스크롤 계산 - base: {baseScrollAmount}, push: {pushAmount}, total: {scrollAmount}");
                if (scrollAmount != 0)
                {
                    StartCoroutine(AnimateChecklistAutoScroll(checklistScrollRect, scrollAmount));
                    Debug.Log($"[TimePickerController] Checklist 자동 스크롤 실행: {scrollAmount}px");
                }
            }
            else
            {
                Debug.LogWarning($"[TimePickerController] 자동 스크롤 실패 - ScrollRect null");
            }
            
            Debug.Log($"[TimePickerController] Checklist 패널 - TimePickerPanel 활성화 완료");
            return;
        }
        
        // ★★★ VoiceMemo 패널일 때는 TimePickerPanel 활성화 + ScrollContent 확장 + Emergency만 이동 ★★★
        if (isVoiceMemoActive)
        {
            // TimePickerPanel 높이 가져오기
            float timePickerHeight = 0f;
            if (voiceMemoTimePickerPanelRect != null)
            {
                timePickerHeight = voiceMemoTimePickerPanelRect.sizeDelta.y;
            }
            
            // 음성메모 아이템으로 인한 밀림량 가져오기
            float pushAmount = 0f;
            if (voiceMemoUIController != null)
            {
                pushAmount = voiceMemoUIController.LastPushAmount;
            }
            
            // 동적 확장량 계산: 기본 확장량 + 아이템 확장량
            float dynamicExpansion = voiceMemoScrollExpansion + voiceMemoItemScrollExpansion;
            
            // RefreshVoiceMemoLayout에서 모든 위치와 크기를 설정
            RefreshVoiceMemoLayout(pushAmount);
            Debug.Log($"[TimePickerController] OpenTimePicker에서 RefreshVoiceMemoLayout 호출 - pushAmount: {pushAmount}");
            
            // 자동 스크롤 (Open)
            if (voiceMemoScrollRect != null)
            {
                float baseScrollAmount = fromCalendar ? voiceMemoAutoScrollOnOpenFromCalendar : voiceMemoAutoScrollOnOpen;
                float scrollAmount = baseScrollAmount + pushAmount;
                Debug.Log($"[TimePickerController] VoiceMemo 자동 스크롤 계산 - base: {baseScrollAmount}, push: {pushAmount}, total: {scrollAmount}");
                if (scrollAmount != 0)
                {
                    StartCoroutine(AnimateChecklistAutoScroll(voiceMemoScrollRect, scrollAmount));
                    Debug.Log($"[TimePickerController] VoiceMemo 자동 스크롤 실행: {scrollAmount}px");
                }
            }
            
            Debug.Log($"[TimePickerController] VoiceMemo 패널 - TimePickerPanel 활성화 완료");
            return;
        }
        
        // CalendarPanel의 높이 가져오기 (현재 활성화된 패널에 맞는 CalendarPanel)
        float calendarHeight = 0f;
        RectTransform activeCalendarPanelRect = GetActiveCalendarPanelRect();
        // activeTimePickerPanelRect는 위에서 이미 선언됨
        
        if (activeCalendarPanelRect != null)
        {
            calendarHeight = activeCalendarPanelRect.sizeDelta.y;
            Debug.Log($"[TimePickerController] {panelName} CalendarPanel 높이 사용: {calendarHeight}");
        }
        else if (activeTimePickerPanelRect != null)
        {
            // CalendarPanel이 지정되지 않았으면 TimePickerPanel 높이 사용
            calendarHeight = activeTimePickerPanelRect.sizeDelta.y;
            Debug.LogWarning($"[TimePickerController] {panelName} CalendarPanelRect가 할당되지 않아 TimePickerPanel 높이 사용: {calendarHeight}");
        }
        else
        {
            Debug.LogError($"[TimePickerController] {panelName} CalendarPanelRect와 TimePickerPanelRect 모두 할당되지 않았습니다!");
        }
        
        // 기본 이동 거리 = CalendarPanel 높이 (CalendarPanel과 동일한 간격 유지)
        float baseMoveDistance = calendarHeight;
        
        // ★★★ ScrollContent 확장 및 모든 자식 아래로 밀기 ★★★
        float upwardExpansion = calendarHeight; // 블록 밖에서도 사용
        if (activeScrollContent != null)
        {
            float downwardExpansion = calendarHeight + extraScrollPadding;
            float totalExpansion = upwardExpansion + downwardExpansion;
            
            // 1. Content 높이 증가
            Vector2 newSize = activeScrollContent.sizeDelta;
            newSize.y = scrollHeightOrig + totalExpansion;
            activeScrollContent.sizeDelta = newSize;
            
            // 2. Content 내의 모든 자식 요소들을 아래로 밀기 (fromCalendar 여부와 관계없이 동일하게 처리)
            foreach (Transform child in activeScrollContent)
            {
                RectTransform childRect = child.GetComponent<RectTransform>();
                if (childRect != null)
                {
                    Vector2 pos = childRect.anchoredPosition;
                    pos.y -= upwardExpansion;
                    childRect.anchoredPosition = pos;
                }
            }
            
            Debug.Log($"[TimePickerController] {panelName} ScrollContent 확장: 높이 {scrollHeightOrig} -> {newSize.y}, 자식 이동 (fromCalendar: {fromCalendar})");
        }
        
        // ★★★ ScrollContent 외부 요소들만 별도로 밀기 ★★★
        // ScrollContent 자손인 요소들은 이미 직접 자식으로 밀렸거나 부모와 함께 이동했으므로 건드리지 않음
        Transform scrollContentTransform = activeScrollContent?.transform;
        
        if (activeDeadlineRow != null && !IsDescendantOf(activeDeadlineRow.transform, scrollContentTransform))
        {
            Vector2 pos = activeDeadlineRow.anchoredPosition;
            pos.y -= upwardExpansion;
            activeDeadlineRow.anchoredPosition = pos;
            Debug.Log($"[TimePickerController] {panelName} Deadline 별도로 밀기 (외부): {pos.y}");
        }
        if (activeEmergencyRow != null && !IsDescendantOf(activeEmergencyRow.transform, scrollContentTransform))
        {
            Vector2 pos = activeEmergencyRow.anchoredPosition;
            pos.y -= upwardExpansion;
            activeEmergencyRow.anchoredPosition = pos;
            Debug.Log($"[TimePickerController] {panelName} Emergency 별도로 밀기 (외부): {pos.y}");
        }
        if (activeAssigneeRow != null && !IsDescendantOf(activeAssigneeRow.transform, scrollContentTransform))
        {
            Vector2 pos = activeAssigneeRow.anchoredPosition;
            pos.y -= upwardExpansion;
            activeAssigneeRow.anchoredPosition = pos;
        }
        if (activeInputFieldBody != null && !IsDescendantOf(activeInputFieldBody.transform, scrollContentTransform))
        {
            Vector2 pos = activeInputFieldBody.anchoredPosition;
            pos.y -= upwardExpansion;
            activeInputFieldBody.anchoredPosition = pos;
        }
        if (activeImageMovie != null && !IsDescendantOf(activeImageMovie.transform, scrollContentTransform))
        {
            Vector2 pos = activeImageMovie.anchoredPosition;
            pos.y -= upwardExpansion;
            activeImageMovie.anchoredPosition = pos;
        }
        
        // ★★★ 그 다음 특정 요소들 이동 (현재 위치에서 시작) ★★★
        
        // 현재 활성화된 패널의 오프셋 가져오기
        bool isImageMemo = IsImageMemoPanelActive();
        bool useManualPos = isImageMemo ? imageMemoUseManualDeadlinePosition : useManualDeadlinePosition;
        float targetY = isImageMemo ? imageMemoDeadlineTargetY : deadlineTargetY;
        float deadlineOff = isImageMemo ? imageMemoDeadlineOffset : deadlineOffset;
        float emergencyOff = isImageMemo ? imageMemoEmergencyOffset : emergencyOffset;
        float assigneeOff = isImageMemo ? imageMemoAssigneeOffset : assigneeOffset;
        float bodyOff = isImageMemo ? imageMemoBodyOffset : bodyOffset;
        float imageMovieOff = isImageMemo ? imageMemoImageMovieOffset : 0f;
        
        // Deadline: 수동 위치 설정 또는 자동 계산 + deadlineOffset 적용
        float deadlineMoveAmount = 0f; // TimePickerPanel 이동용
        if (activeDeadlineRow != null)
        {
            Vector2 currentPos = activeDeadlineRow.anchoredPosition;
            Vector2 targetPos;
            if (useManualPos)
            {
                targetPos = new Vector2(currentPos.x, targetY - calendarHeight); // 보정
                deadlineMoveAmount = targetPos.y - currentPos.y; // Deadline이 이동한 거리 저장
                Debug.Log($"[TimePickerController] {panelName} Deadline 이동 (수동): {currentPos.y} -> {targetPos.y}");
            }
            else
            {
                // 기본 이동 거리 + deadlineOffset 적용 (밀린 후 위치에서 계산)
                float moveDistance = baseMoveDistance + deadlineOff;
                targetPos = currentPos + Vector2.up * moveDistance;
                deadlineMoveAmount = moveDistance;
                Debug.Log($"[TimePickerController] {panelName} Deadline 이동: {currentPos.y} -> {targetPos.y}");
            }
            StartCoroutine(AnimatePosition(activeDeadlineRow, currentPos, targetPos));
        }
        
        // TimePickerPanel도 Deadline과 같은 거리만큼 위로 이동 (Deadline 위에 유지)
        if (activeTimePickerPanelRect != null && deadlineMoveAmount != 0)
        {
            Vector2 currentPos = activeTimePickerPanelRect.anchoredPosition;
            Vector2 targetPos = currentPos + Vector2.up * deadlineMoveAmount;
            StartCoroutine(AnimatePosition(activeTimePickerPanelRect, currentPos, targetPos));
            Debug.Log($"[TimePickerController] {panelName} TimePickerPanel 이동: {currentPos.y} -> {targetPos.y}");
        }
        
        // Emergency: 아래로 이동 + emergencyOffset (밀린 후 위치에서 계산)
        if (activeEmergencyRow != null)
        {
            Vector2 currentPos = activeEmergencyRow.anchoredPosition;
            float moveDistance = baseMoveDistance + emergencyOff;
            Vector2 targetPos = currentPos + Vector2.down * moveDistance;
            StartCoroutine(AnimatePosition(activeEmergencyRow, currentPos, targetPos));
            Debug.Log($"[TimePickerController] {panelName} Emergency 이동: {currentPos.y} -> {targetPos.y}");
        }
        else
        {
            Debug.LogWarning($"[TimePickerController] {panelName} EmergencyRow가 할당되지 않았습니다!");
        }
        
        // AssigneeRow: 기본 이동 + assigneeOffset (밀린 후 위치에서 계산)
        if (activeAssigneeRow != null)
        {
            Vector2 currentPos = activeAssigneeRow.anchoredPosition;
            float moveDistance = baseMoveDistance + assigneeOff;
            Vector2 targetPos = currentPos + Vector2.up * moveDistance;
            StartCoroutine(AnimatePosition(activeAssigneeRow, currentPos, targetPos));
            Debug.Log($"[TimePickerController] {panelName} AssigneeRow 이동: {currentPos.y} -> {targetPos.y}");
        }
        
        // InputField_Body: 기본 이동 + bodyOffset (밀린 후 위치에서 계산)
        if (activeInputFieldBody != null)
        {
            Vector2 currentPos = activeInputFieldBody.anchoredPosition;
            float moveDistance = baseMoveDistance + bodyOff;
            Vector2 targetPos = currentPos + Vector2.up * moveDistance;
            StartCoroutine(AnimatePosition(activeInputFieldBody, currentPos, targetPos));
            Debug.Log($"[TimePickerController] {panelName} InputField_Body 이동: {currentPos.y} -> {targetPos.y}");
        }
        
        // ImageMovie: 기본 이동 + imageMovieOffset (ImageMemo 전용, 밀린 후 위치에서 계산)
        if (activeImageMovie != null)
        {
            Vector2 currentPos = activeImageMovie.anchoredPosition;
            float moveDistance = baseMoveDistance + imageMovieOff;
            Vector2 targetPos = currentPos + Vector2.up * moveDistance;
            StartCoroutine(AnimatePosition(activeImageMovie, currentPos, targetPos));
            Debug.Log($"[TimePickerController] {panelName} ImageMovie 이동: {currentPos.y} -> {targetPos.y}");
        }
        
        // ★★★ 자동 스크롤 (TimeBt 열릴 때 화면을 위로 스크롤) ★★★
        if (activeScrollContent != null)
        {
            // 현재 활성화된 패널에 따라 적절한 스크롤 값 선택 (isImageMemo는 이미 위에서 선언됨)
            float scrollAmount;
            
            if (fromCalendar)
            {
                scrollAmount = isImageMemo ? imageMemoAutoScrollOnOpenFromCalendar : autoScrollOnOpenFromCalendar;
                Debug.Log($"[TimePickerController] {panelName} 스크롤 값 선택 (DateBt→TimeBt): isImageMemo={isImageMemo}, imageMemoValue={imageMemoAutoScrollOnOpenFromCalendar}, textMemoValue={autoScrollOnOpenFromCalendar}, selected={scrollAmount}");
            }
            else
            {
                scrollAmount = isImageMemo ? imageMemoAutoScrollOnOpen : autoScrollOnOpen;
                Debug.Log($"[TimePickerController] {panelName} 스크롤 값 선택 (TimeBt 직접): isImageMemo={isImageMemo}, imageMemoValue={imageMemoAutoScrollOnOpen}, textMemoValue={autoScrollOnOpen}, selected={scrollAmount}");
            }
            
            if (scrollAmount > 0f)
            {
                Debug.Log($"[TimePickerController] {panelName} 자동 스크롤 실행: {scrollAmount} (fromCalendar: {fromCalendar})");
                StartCoroutine(AnimateAutoScroll(scrollAmount));
            }
            else
            {
                Debug.Log($"[TimePickerController] {panelName} 자동 스크롤 스킵: scrollAmount={scrollAmount} (fromCalendar: {fromCalendar})");
            }
        }
    }
    
    // 시간 선택 패널 닫기
    private void CloseTimePickerInternal(bool restoreScrollPosition = true)
    {
        // ★★★ 진행 중인 애니메이션 코루틴 중지 (위치 복원이 덮어씌워지지 않도록) ★★★
        StopAllCoroutines();
        
        // 현재 활성화된 패널의 요소들 가져오기
        GameObject activeTimePickerPanel = GetActiveTimePickerPanel();
        RectTransform activeTimePickerPanelRect = GetActiveTimePickerPanelRect();
        Vector2 activeTimePickerOriginalPos = GetActiveTimePickerOriginalPos();
        RectTransform activeScrollContent = GetActiveScrollContent();
        UnityEngine.UI.ScrollRect activeScrollRect = GetActiveScrollRect();
        RectTransform activeDeadlineRow = GetActiveDeadlineRow();
        RectTransform activeEmergencyRow = GetActiveEmergencyRow();
        RectTransform activeAssigneeRow = GetActiveAssigneeRow();
        RectTransform activeInputFieldBody = GetActiveInputFieldBody();
        RectTransform activeImageMovie = GetActiveImageMovie();
        GetActiveOriginalPositions(out Vector2 deadlineOrig, out Vector2 emergencyOrig, out Vector2 assigneeOrig,
                                   out Vector2 bodyOrig, out Vector2 imageMovieOrig, out float scrollHeightOrig,
                                   out Vector2 scrollPosOrig, out Dictionary<Transform, Vector2> childPosOrig);
        
        string panelName = IsVoiceMemoPanelActive() ? "Panel_VoiceMemo" : (IsChecklistPanelActive() ? "Panel_Checklist" : (IsImageMemoPanelActive() ? "Panel_ImageMemo" : "Panel_TextMemo"));
        Debug.Log($"[TimePickerController] CloseTimePickerInternal - 현재 활성 패널: {panelName}");
        
        // ★★★ Checklist/VoiceMemo 패널 여부 확인 ★★★
        bool isChecklistActive = IsChecklistPanelActive();
        bool isVoiceMemoActive = IsVoiceMemoPanelActive();
        
        // 패널 비활성화 (현재 활성화된 패널에 맞는 TimePickerPanel)
        if (activeTimePickerPanel != null)
        {
            activeTimePickerPanel.SetActive(false);
        }
        
        // TimePickerPanel 위치 원래대로 (Checklist/VoiceMemo 제외)
        if (!isChecklistActive && !isVoiceMemoActive && activeTimePickerPanelRect != null)
        {
            activeTimePickerPanelRect.anchoredPosition = activeTimePickerOriginalPos;
        }
        
        // TimeBt 배경색 원래대로 (Panel_TextMemo)
        if (timeButtonImage != null && !IsImageMemoPanelActive() && !isChecklistActive && !isVoiceMemoActive)
        {
            StartCoroutine(AnimateButtonColor(timeButtonImage, Color.white));
        }
        
        // ImageTimeBt 배경색 원래대로 (Panel_ImageMemo)
        if (imageTimeButtonImage != null && IsImageMemoPanelActive() && !isChecklistActive && !isVoiceMemoActive)
        {
            StartCoroutine(AnimateButtonColor(imageTimeButtonImage, Color.white));
        }
        
        // ChecklistTimeBt 배경색 원래대로 (Panel_Checklist)
        if (checklistTimeButtonImage != null && isChecklistActive)
        {
            StartCoroutine(AnimateButtonColor(checklistTimeButtonImage, Color.white));
        }
        
        // VoiceMemoTimeBt 배경색 원래대로 (Panel_VoiceMemo)
        if (voiceMemoTimeButtonImage != null && isVoiceMemoActive)
        {
            StartCoroutine(AnimateButtonColor(voiceMemoTimeButtonImage, Color.white));
        }
        
        // ★★★ Checklist 패널: TimePickerPanel 비활성화 + ScrollContent 복원 + Emergency 원래 위치 ★★★
        if (isChecklistActive)
        {
            // TimePickerPanel 높이 가져오기
            float timePickerHeight = 0f;
            if (checklistTimePickerPanelRect != null)
            {
                timePickerHeight = checklistTimePickerPanelRect.sizeDelta.y;
            }
            
            // ★★★ ChecklistEmergency 원래 위치로 복원 (TimePickerPanel 높이 + 오프셋만큼 위로) ★★★
            if (checklistEmergencyRow != null)
            {
                float moveAmount = timePickerHeight + checklistEmergencyOffset;
                Vector3 pos = checklistEmergencyRow.localPosition;
                pos.y += moveAmount;
                checklistEmergencyRow.localPosition = pos;
                Debug.Log($"[TimePickerController] ChecklistEmergency 원래 위치로 복원: +{moveAmount}px");
            }
            
            // ScrollContent 복원 - 요소들의 localPosition 유지
            if (activeScrollContent != null && restoreScrollPosition)
            {
                // 1. 모든 자식의 localPosition 저장
                Dictionary<Transform, Vector3> savedLocalPositions = new Dictionary<Transform, Vector3>();
                foreach (Transform child in activeScrollContent)
                {
                    savedLocalPositions[child] = child.localPosition;
                }
                
                // 2. ScrollContent 복원
                Vector2 newSize = activeScrollContent.sizeDelta;
                newSize.y = scrollHeightOrig;
                activeScrollContent.sizeDelta = newSize;
                
                // 3. 모든 자식의 localPosition 복원 (시각적 위치 유지)
                foreach (var kvp in savedLocalPositions)
                {
                    kvp.Key.localPosition = kvp.Value;
                }
                
                Debug.Log($"[TimePickerController] Checklist ScrollContent 복원: {scrollHeightOrig} (요소 위치 유지)");
            }
            
            // ★★★ 자동 스크롤 (Close) ★★★
            if (checklistScrollRect != null && checklistAutoScrollOnClose != 0)
            {
                StartCoroutine(AnimateChecklistAutoScroll(checklistScrollRect, -checklistAutoScrollOnClose));
                Debug.Log($"[TimePickerController] Checklist 닫기 시 자동 스크롤: {checklistAutoScrollOnClose}px");
            }
            
            // InputField 위치는 ScrollContent 자식 복원 시 localPosition 유지로 자동 처리됨
            
            Debug.Log($"[TimePickerController] CloseInternal - Checklist 패널: TimePickerPanel 비활성화 완료");
            return;
        }
        
        // ★★★ VoiceMemo 패널: TimePickerPanel 비활성화 + ScrollContent 복원 + Emergency 원래 위치 ★★★
        if (isVoiceMemoActive)
        {
            // TimePickerPanel 높이 가져오기
            float timePickerHeight = 0f;
            if (voiceMemoTimePickerPanelRect != null)
            {
                timePickerHeight = voiceMemoTimePickerPanelRect.sizeDelta.y;
            }
            
            // VoiceMemoEmergency 원래 위치로 복원 (TimePickerPanel 높이 + 오프셋만큼 위로)
            if (voiceMemoEmergencyRow != null)
            {
                float moveAmount = timePickerHeight + voiceMemoEmergencyOffset;
                Vector3 pos = voiceMemoEmergencyRow.localPosition;
                pos.y += moveAmount;
                voiceMemoEmergencyRow.localPosition = pos;
                Debug.Log($"[TimePickerController] VoiceMemoEmergency 원래 위치로 복원: +{moveAmount}px");
            }
            
            // ScrollContent 복원 - 요소들의 localPosition 유지
            if (activeScrollContent != null && restoreScrollPosition)
            {
                // 1. 모든 자식의 localPosition 저장
                Dictionary<Transform, Vector3> savedLocalPositions = new Dictionary<Transform, Vector3>();
                foreach (Transform child in activeScrollContent)
                {
                    savedLocalPositions[child] = child.localPosition;
                }
                
                // 2. ScrollContent 복원
                Vector2 newSize = activeScrollContent.sizeDelta;
                newSize.y = scrollHeightOrig;
                activeScrollContent.sizeDelta = newSize;
                
                // 3. 모든 자식의 localPosition 복원 (시각적 위치 유지)
                foreach (var kvp in savedLocalPositions)
                {
                    kvp.Key.localPosition = kvp.Value;
                }
                
                Debug.Log($"[TimePickerController] VoiceMemo ScrollContent 복원: {scrollHeightOrig} (요소 위치 유지)");
            }
            
            // 자동 스크롤 (Close)
            if (voiceMemoScrollRect != null && voiceMemoAutoScrollOnClose != 0)
            {
                StartCoroutine(AnimateChecklistAutoScroll(voiceMemoScrollRect, -voiceMemoAutoScrollOnClose));
                Debug.Log($"[TimePickerController] VoiceMemo 닫기 시 자동 스크롤: {voiceMemoAutoScrollOnClose}px");
            }
            
            // VoiceContent 위치 복원 (VoiceMemoUIController에 위임)
            if (voiceMemoUIController != null)
            {
                voiceMemoUIController.RestoreVoiceContentPosition();
            }
            
            Debug.Log($"[TimePickerController] CloseInternal - VoiceMemo 패널: TimePickerPanel 비활성화 완료");
            return;
        }
        
        // ★★★ ImageMemo AutoScrollOnClose 사용 여부 확인 ★★★
        bool useImageMemoAutoScroll = IsImageMemoPanelActive() && imageMemoAutoScrollOnClose > 0f && restoreScrollPosition;
        
        // ★★★ ScrollContent 및 모든 자식을 원래 위치로 직접 복원 ★★★
        if (activeScrollContent != null && restoreScrollPosition)
        {
            // 1. 모든 자식을 원래 위치로 복원 (항상)
            foreach (Transform child in activeScrollContent)
            {
                if (childPosOrig.TryGetValue(child, out Vector2 originalPos))
                {
                    RectTransform childRect = child.GetComponent<RectTransform>();
                    if (childRect != null)
                    {
                        childRect.anchoredPosition = originalPos;
                    }
                }
            }
            
            // 2. Content 높이 복원 (항상)
            Vector2 newSize = activeScrollContent.sizeDelta;
            newSize.y = scrollHeightOrig;
            activeScrollContent.sizeDelta = newSize;
            
            // 3. Content 위치 설정 - ImageMemo AutoScrollOnClose일 때는 위로 올림
            if (useImageMemoAutoScroll)
            {
                // Content를 위로 올려서 아래쪽 요소들이 보이게 (음수 = 위로 이동)
                float upwardShift = -imageMemoAutoScrollOnClose;
                activeScrollContent.anchoredPosition = new Vector2(activeScrollContent.anchoredPosition.x, upwardShift);
                Debug.Log($"### [TimePickerController] {panelName} Content를 위로 이동: {upwardShift}, 높이: {scrollHeightOrig}");
            }
            else
            {
                // 일반 복원
                activeScrollContent.anchoredPosition = scrollPosOrig;
                Debug.Log($"[TimePickerController] {panelName} ScrollContent 및 모든 자식 원래 위치로 복원 완료 (높이: {scrollHeightOrig})");
            }
        }
        
        // 특정 요소들 위치 복원 (항상)
        if (restoreScrollPosition)
        {
            if (activeDeadlineRow != null)
            {
                activeDeadlineRow.anchoredPosition = deadlineOrig;
            }
            
            if (activeEmergencyRow != null)
            {
                activeEmergencyRow.anchoredPosition = emergencyOrig;
            }
            
            if (activeAssigneeRow != null)
            {
                activeAssigneeRow.anchoredPosition = assigneeOrig;
            }
            
            if (activeInputFieldBody != null)
            {
                activeInputFieldBody.anchoredPosition = bodyOrig;
            }
            
            if (activeImageMovie != null)
            {
                activeImageMovie.anchoredPosition = imageMovieOrig;
            }
        }
        
        // ★★★ 스크롤 위치 설정 ★★★
        if (restoreScrollPosition)
        {
            if (useImageMemoAutoScroll)
            {
                // ImageMemo AutoScrollOnClose: Content가 위로 올라갔으므로 스크롤 위치 조정 필요 없음
                Debug.Log($"### [TimePickerController] {panelName} TimePicker 닫기 완료 - Content 위로 이동됨: {-imageMemoAutoScrollOnClose}");
            }
            else if (activeScrollRect != null)
            {
                // 일반: 스크롤 위치를 맨 위로 복원
                StartCoroutine(AdjustScrollPosition(1f)); // 1 = 맨 위 (원래 위치)
            }
        }
        else
        {
            Debug.Log($"[TimePickerController] {panelName} 스크롤 위치 복원 건너뜀 (다른 패널이 열릴 예정)");
        }
    }
    
    // 시간 버튼들 생성 (30분 단위, 1:00~12:00, 1:30~11:30)
    private void CreateTimeButtons()
    {
        string panelName = IsImageMemoPanelActive() ? "Panel_ImageMemo" : "Panel_TextMemo";
        Debug.Log($"[TimePickerController] CreateTimeButtons() 호출됨 - {panelName}");
        
        Transform activeTimeButtonsContainer = GetActiveTimeButtonsContainer();
        
        // null 체크
        if (timeButtonPrefab == null)
        {
            Debug.LogError("[TimePickerController] timeButtonPrefab이 null입니다! Inspector에서 할당해주세요.");
            return;
        }
        
        if (activeTimeButtonsContainer == null)
        {
            Debug.LogError($"[TimePickerController] {panelName} timeButtonsContainer가 null입니다! Inspector에서 할당해주세요.");
            return;
        }
        
        Debug.Log($"[TimePickerController] timeButtonPrefab: {timeButtonPrefab.name}");
        Debug.Log($"[TimePickerController] {panelName} timeButtonsContainer: {activeTimeButtonsContainer.name}");
        
        // 기존 버튼들 제거
        foreach (var obj in timeButtonObjects)
        {
            if (obj != null) Destroy(obj);
        }
        timeButtonObjects.Clear();
        timeButtonMap.Clear();
        
        // 1시부터 12시까지 (30분 간격)
        for (int hour = 1; hour <= 12; hour++)
        {
            // 정각 (00분)
            CreateTimeButton(hour, 0);
            
            // 30분
            CreateTimeButton(hour, 30);
        }
        
        Debug.Log($"[TimePickerController] 총 {timeButtonObjects.Count}개 버튼 생성 완료");
    }
    
    // 개별 시간 버튼 생성
    private void CreateTimeButton(int hour, int minute)
    {
        Transform activeTimeButtonsContainer = GetActiveTimeButtonsContainer();
        GameObject timeObj = Instantiate(timeButtonPrefab, activeTimeButtonsContainer);
        
        if (timeObj == null)
        {
            Debug.LogError($"[TimePickerController] 버튼 생성 실패: {hour}:{minute:D2}");
            return;
        }
        
        Button btn = timeObj.GetComponent<Button>();
        TMP_Text txt = timeObj.GetComponentInChildren<TMP_Text>();
        
        // 시간 표시 (12:00 형태)
        string timeText = $"{hour:D2}:{minute:D2}";
        if (txt != null) txt.text = timeText;
        
        Debug.Log($"[TimePickerController] 버튼 생성: {timeText}, 위치: {timeObj.transform.position}");
        
        // 버튼 클릭 이벤트
        btn.onClick.AddListener(() => OnTimeClicked(hour, minute));
        
        // 버튼 매핑 저장
        string key = $"{hour}:{minute}";
        timeButtonMap[key] = timeObj;
        
        // 리스트에 추가
        timeButtonObjects.Add(timeObj);
    }
    
    // 시간 버튼 클릭 시
    private void OnTimeClicked(int hour, int minute)
    {
        selectedHour = hour;
        selectedMinute = minute;
        
        // TimeBt 텍스트 업데이트
        UpdateTimeButtonText();
        
        // 시간 버튼 색상 업데이트
        UpdateTimeButtonColors();
        
        // TimeBt Outline 색상을 선택 후 색상으로 즉시 변경 (Panel_TextMemo)
        // 참고: 코루틴 사용 시 CloseTimePickerInternal()에서 패널이 닫히면 코루틴이 중단되어 색상 변경이 완료되지 않음
        if (timeButtonOutline != null)
        {
            timeButtonOutline.effectColor = timeButtonOutlineSelected;
            Debug.Log($"[TimePickerController] ▶▶▶ TimeBt Outline 색상 즉시 변경 완료: {timeButtonOutlineSelected}");
        }
        
        // ImageTimeBt Outline 색상을 선택 후 색상으로 즉시 변경 (Panel_ImageMemo)
        if (imageTimeButtonOutline != null)
        {
            imageTimeButtonOutline.effectColor = timeButtonOutlineSelected;
            Debug.Log($"[TimePickerController] ▶▶▶ ImageTimeBt Outline 색상 즉시 변경 완료: {timeButtonOutlineSelected}");
        }
        
        // ChecklistTimeBt Outline 색상을 선택 후 색상으로 즉시 변경 (Panel_Checklist)
        if (checklistTimeButtonOutline != null)
        {
            checklistTimeButtonOutline.effectColor = timeButtonOutlineSelected;
            Debug.Log($"[TimePickerController] ▶▶▶ ChecklistTimeBt Outline 색상 즉시 변경 완료: {timeButtonOutlineSelected}");
        }
        
        // VoiceMemoTimeBt Outline 색상을 선택 후 색상으로 즉시 변경 (Panel_VoiceMemo)
        if (voiceMemoTimeButtonOutline != null)
        {
            voiceMemoTimeButtonOutline.effectColor = timeButtonOutlineSelected;
            Debug.Log($"[TimePickerController] ▶▶▶ VoiceMemoTimeBt Outline 색상 즉시 변경 완료: {timeButtonOutlineSelected}");
        }
        
        // 패널 닫기
        isTimePickerOpen = false;
        CloseTimePickerInternal();
        
        Debug.Log($"선택된 시간: {GetSelectedTimeString()}");
    }
    
    // AM 버튼 클릭
    private void OnAMButtonClicked()
    {
        isAM = true;
        UpdateAMPMButtons();
        UpdateTimeButtonText();
    }
    
    // PM 버튼 클릭
    private void OnPMButtonClicked()
    {
        isAM = false;
        UpdateAMPMButtons();
        UpdateTimeButtonText();
    }
    
    // AM/PM 버튼 상태 업데이트
    private void UpdateAMPMButtons()
    {
        // Panel_TextMemo AM 버튼
        if (amButton != null)
        {
            // 자식 "Image" 오브젝트 찾기
            Transform amImageTransform = amButton.transform.Find("Image");
            UnityEngine.UI.Image amImage = null;
            if (amImageTransform != null)
            {
                amImage = amImageTransform.GetComponent<UnityEngine.UI.Image>();
            }
            
            if (amImage != null)
            {
                amImage.color = isAM ? selectedColor : normalColor;
            }
            
            if (amButtonText != null)
            {
                amButtonText.color = isAM ? selectedTextColor : normalTextColor;
            }
        }
        
        // Panel_TextMemo PM 버튼
        if (pmButton != null)
        {
            // 자식 "Image" 오브젝트 찾기
            Transform pmImageTransform = pmButton.transform.Find("Image");
            UnityEngine.UI.Image pmImage = null;
            if (pmImageTransform != null)
            {
                pmImage = pmImageTransform.GetComponent<UnityEngine.UI.Image>();
            }
            
            if (pmImage != null)
            {
                pmImage.color = isAM ? normalColor : selectedColor;
            }
            
            if (pmButtonText != null)
            {
                pmButtonText.color = isAM ? normalTextColor : selectedTextColor;
            }
        }
        
        // Panel_ImageMemo AM 버튼
        if (imageAmButton != null)
        {
            // 자식 "Image" 오브젝트 찾기
            Transform amImageTransform = imageAmButton.transform.Find("Image");
            UnityEngine.UI.Image amImage = null;
            if (amImageTransform != null)
            {
                amImage = amImageTransform.GetComponent<UnityEngine.UI.Image>();
            }
            
            if (amImage != null)
            {
                amImage.color = isAM ? selectedColor : normalColor;
            }
            
            if (imageAmButtonText != null)
            {
                imageAmButtonText.color = isAM ? selectedTextColor : normalTextColor;
            }
        }
        
        // Panel_ImageMemo PM 버튼
        if (imagePmButton != null)
        {
            // 자식 "Image" 오브젝트 찾기
            Transform pmImageTransform = imagePmButton.transform.Find("Image");
            UnityEngine.UI.Image pmImage = null;
            if (pmImageTransform != null)
            {
                pmImage = pmImageTransform.GetComponent<UnityEngine.UI.Image>();
            }
            
            if (pmImage != null)
            {
                pmImage.color = isAM ? normalColor : selectedColor;
            }
            
            if (imagePmButtonText != null)
            {
                imagePmButtonText.color = isAM ? normalTextColor : selectedTextColor;
            }
        }
        
        // Panel_Checklist AM 버튼
        if (checklistAmButton != null)
        {
            // 자식 "Image" 오브젝트 찾기
            Transform amImageTransform = checklistAmButton.transform.Find("Image");
            UnityEngine.UI.Image amImage = null;
            if (amImageTransform != null)
            {
                amImage = amImageTransform.GetComponent<UnityEngine.UI.Image>();
            }
            
            if (amImage != null)
            {
                amImage.color = isAM ? selectedColor : normalColor;
            }
            
            if (checklistAmButtonText != null)
            {
                checklistAmButtonText.color = isAM ? selectedTextColor : normalTextColor;
            }
        }
        
        // Panel_Checklist PM 버튼
        if (checklistPmButton != null)
        {
            // 자식 "Image" 오브젝트 찾기
            Transform pmImageTransform = checklistPmButton.transform.Find("Image");
            UnityEngine.UI.Image pmImage = null;
            if (pmImageTransform != null)
            {
                pmImage = pmImageTransform.GetComponent<UnityEngine.UI.Image>();
            }
            
            if (pmImage != null)
            {
                pmImage.color = isAM ? normalColor : selectedColor;
            }
            
            if (checklistPmButtonText != null)
            {
                checklistPmButtonText.color = isAM ? normalTextColor : selectedTextColor;
            }
        }
        
        // Panel_VoiceMemo AM 버튼
        if (voiceMemoAmButton != null)
        {
            // 자식 "Image" 오브젝트 찾기
            Transform amImageTransform = voiceMemoAmButton.transform.Find("Image");
            UnityEngine.UI.Image amImage = null;
            if (amImageTransform != null)
            {
                amImage = amImageTransform.GetComponent<UnityEngine.UI.Image>();
            }
            
            if (amImage != null)
            {
                amImage.color = isAM ? selectedColor : normalColor;
            }
            
            if (voiceMemoAmButtonText != null)
            {
                voiceMemoAmButtonText.color = isAM ? selectedTextColor : normalTextColor;
            }
        }
        
        // Panel_VoiceMemo PM 버튼
        if (voiceMemoPmButton != null)
        {
            // 자식 "Image" 오브젝트 찾기
            Transform pmImageTransform = voiceMemoPmButton.transform.Find("Image");
            UnityEngine.UI.Image pmImage = null;
            if (pmImageTransform != null)
            {
                pmImage = pmImageTransform.GetComponent<UnityEngine.UI.Image>();
            }
            
            if (pmImage != null)
            {
                pmImage.color = isAM ? normalColor : selectedColor;
            }
            
            if (voiceMemoPmButtonText != null)
            {
                voiceMemoPmButtonText.color = isAM ? normalTextColor : selectedTextColor;
            }
        }
    }
    
    // TimeBt 텍스트 업데이트 ("09:00" 형태)
    private void UpdateTimeButtonText()
    {
        // 24시간 형식으로 변환
        int hour24 = ConvertTo24Hour(selectedHour, isAM);
        string timeString = $"{hour24:D2}:{selectedMinute:D2}";
        
        // Panel_TextMemo 시간 버튼 텍스트 업데이트
        if (timeButtonText != null)
        {
            timeButtonText.text = timeString;
        }
        
        // Panel_ImageMemo 시간 버튼 텍스트 업데이트
        if (imageTimeButtonText != null)
        {
            imageTimeButtonText.text = timeString;
        }
        
        // Panel_Checklist 시간 버튼 텍스트 업데이트
        if (checklistTimeButtonText != null)
        {
            checklistTimeButtonText.text = timeString;
            Debug.Log($"[TimePickerController] ChecklistTimeBt 텍스트 업데이트: {timeString}");
        }
        
        // Panel_VoiceMemo 시간 버튼 텍스트 업데이트
        if (voiceMemoTimeButtonText != null)
        {
            voiceMemoTimeButtonText.text = timeString;
            Debug.Log($"[TimePickerController] VoiceMemoTimeBt 텍스트 업데이트: {timeString}");
        }
    }
    
    // 12시간 형식을 24시간 형식으로 변환
    private int ConvertTo24Hour(int hour12, bool isAM)
    {
        if (hour12 == 12)
        {
            // 12 AM = 0시, 12 PM = 12시
            return isAM ? 0 : 12;
        }
        else
        {
            // 1~11 AM = 1~11시, 1~11 PM = 13~23시
            return isAM ? hour12 : hour12 + 12;
        }
    }
    
    // 시간 버튼 색상 업데이트
    private void UpdateTimeButtonColors()
    {
        foreach (var kvp in timeButtonMap)
        {
            string key = kvp.Key;
            GameObject timeObj = kvp.Value;
            
            if (timeObj == null) continue;
            
            // 선택된 시간인지 확인
            string selectedKey = $"{selectedHour}:{selectedMinute}";
            bool isSelected = (key == selectedKey);
            
            // 자식 "Image" 오브젝트 찾기
            Transform imageTransform = timeObj.transform.Find("Image");
            UnityEngine.UI.Image bgImage = null;
            if (imageTransform != null)
            {
                bgImage = imageTransform.GetComponent<UnityEngine.UI.Image>();
            }
            
            TMP_Text txt = timeObj.GetComponentInChildren<TMP_Text>();
            
            if (isSelected)
            {
                if (bgImage != null) bgImage.color = selectedColor;
                if (txt != null) txt.color = selectedTextColor;
            }
            else
            {
                if (bgImage != null) bgImage.color = normalColor;
                if (txt != null) txt.color = normalTextColor;
            }
        }
    }
    
    // 위치 애니메이션
    private System.Collections.IEnumerator AnimatePosition(RectTransform target, Vector2 from, Vector2 to)
    {
        float elapsed = 0f;
        
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            // Ease-out 효과
            t = 1f - Mathf.Pow(1f - t, 3f);
            
            target.anchoredPosition = Vector2.Lerp(from, to, t);
            yield return null;
        }
        
        target.anchoredPosition = to;
    }
    
    // 자동 스크롤 애니메이션 (열릴 때)
    private System.Collections.IEnumerator AnimateAutoScroll(float scrollAmount)
    {
        // 현재 활성화된 패널의 ScrollContent 가져오기
        RectTransform activeScrollContent = GetActiveScrollContent();
        
        if (activeScrollContent == null) yield break;
        
        Vector2 startPos = activeScrollContent.anchoredPosition;
        Vector2 targetPos = startPos + new Vector2(0, scrollAmount); // 양수 = 위로 스크롤
        
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            // Ease out
            t = 1f - Mathf.Pow(1f - t, 3f);
            activeScrollContent.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }
        
        activeScrollContent.anchoredPosition = targetPos;
        string panelName = IsImageMemoPanelActive() ? "Panel_ImageMemo" : "Panel_TextMemo";
        Debug.Log($"[TimePickerController] {panelName} 자동 스크롤 완료: {startPos.y} -> {targetPos.y}");
    }
    
    // Checklist용 자동 스크롤 애니메이션 (ScrollRect의 verticalNormalizedPosition 사용)
    private System.Collections.IEnumerator AnimateChecklistAutoScroll(UnityEngine.UI.ScrollRect scrollRect, float scrollAmount)
    {
        if (scrollRect == null || scrollRect.content == null) yield break;
        
        // 한 프레임 대기 (레이아웃 업데이트 후)
        yield return null;
        
        float contentHeight = scrollRect.content.rect.height;
        float viewportHeight = scrollRect.viewport != null ? scrollRect.viewport.rect.height : scrollRect.GetComponent<RectTransform>().rect.height;
        float scrollableHeight = contentHeight - viewportHeight;
        
        if (scrollableHeight <= 0)
        {
            Debug.Log($"[TimePickerController] Checklist 스크롤 불가 - contentHeight: {contentHeight}, viewportHeight: {viewportHeight}");
            yield break;
        }
        
        // scrollAmount를 normalizedPosition 변화량으로 변환 (양수 = 위로 = normalized 증가)
        float normalizedChange = scrollAmount / scrollableHeight;
        
        float startNormalized = scrollRect.verticalNormalizedPosition;
        float targetNormalized = Mathf.Clamp01(startNormalized + normalizedChange);
        
        Debug.Log($"[TimePickerController] Checklist 자동 스크롤 시작 - start: {startNormalized}, target: {targetNormalized}, scrollAmount: {scrollAmount}, scrollableHeight: {scrollableHeight}");
        
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            // Ease out
            t = 1f - Mathf.Pow(1f - t, 3f);
            scrollRect.verticalNormalizedPosition = Mathf.Lerp(startNormalized, targetNormalized, t);
            yield return null;
        }
        
        scrollRect.verticalNormalizedPosition = targetNormalized;
        Debug.Log($"[TimePickerController] Checklist 자동 스크롤 완료: {startNormalized} -> {targetNormalized}");
    }
    
    // 자동 스크롤 애니메이션 (닫힐 때) - verticalNormalizedPosition 사용 (Content 높이 유지됨)
    private System.Collections.IEnumerator AnimateScrollRectOnClose(UnityEngine.UI.ScrollRect scrollRect, float scrollAmount)
    {
        Debug.Log($"### [TimePickerController] AnimateScrollRectOnClose 시작 - scrollRect: {(scrollRect != null ? "OK" : "NULL")}, content: {(scrollRect?.content != null ? "OK" : "NULL")}, scrollAmount: {scrollAmount}");
        
        if (scrollRect == null || scrollRect.content == null)
        {
            Debug.LogError("### [TimePickerController] AnimateScrollRectOnClose - scrollRect 또는 content가 null입니다!");
            yield break;
        }
        
        // 한 프레임 대기 (요소들이 복원된 후)
        yield return null;
        
        Debug.Log("### [TimePickerController] 1프레임 대기 완료, 스크롤 계산 시작");
        
        // Content 높이가 유지되어 있으므로 스크롤 가능한 높이 계산
        RectTransform viewportRect = scrollRect.viewport;
        if (viewportRect == null)
        {
            viewportRect = scrollRect.gameObject.GetComponent<RectTransform>();
            Debug.Log("### [TimePickerController] viewport가 null이므로 ScrollRect GameObject의 RectTransform 사용");
        }
        
        if (viewportRect == null)
        {
            Debug.LogError("### [TimePickerController] ERROR: viewport를 찾을 수 없습니다!");
            yield break;
        }
        
        float contentHeight = scrollRect.content.rect.height;
        float viewportHeight = viewportRect.rect.height;
        float scrollableHeight = Mathf.Max(0, contentHeight - viewportHeight);
        
        Debug.Log($"### [TimePickerController] 높이 계산 - content: {contentHeight:F1}, viewport: {viewportHeight:F1}, scrollable: {scrollableHeight:F1}");
        
        if (scrollableHeight <= 0)
        {
            Debug.Log("### [TimePickerController] 스크롤 불가능 - scrollableHeight가 0 이하입니다");
            yield break;
        }
        
        // scrollAmount를 normalizedPosition으로 변환
        float normalizedScroll = scrollAmount / scrollableHeight;
        
        // verticalNormalizedPosition: 1 = 맨 위, 0 = 맨 아래
        float startPos = 1f;
        float targetPos = Mathf.Clamp01(1f - normalizedScroll);
        
        Debug.Log($"### [TimePickerController] 스크롤 시작: {startPos:F3} -> {targetPos:F3} (normalized: {normalizedScroll:F3})");
        
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            // Ease out
            t = 1f - Mathf.Pow(1f - t, 3f);
            scrollRect.verticalNormalizedPosition = Mathf.Lerp(startPos, targetPos, t);
            yield return null;
        }
        
        scrollRect.verticalNormalizedPosition = targetPos;
        Debug.Log($"### [TimePickerController] 스크롤 완료: {scrollRect.verticalNormalizedPosition:F3}");
    }
    
    // 스크롤 위치 조정 (애니메이션 완료 후 실행)
    private System.Collections.IEnumerator AdjustScrollPosition(float targetPosition)
    {
        // 애니메이션 완료 대기
        yield return new WaitForSeconds(animationDuration + 0.1f);
        
        // Canvas 업데이트 여러 번 수행
        for (int i = 0; i < 3; i++)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
        }
        
        // 현재 활성화된 패널의 ScrollRect와 ScrollContent 가져오기
        UnityEngine.UI.ScrollRect activeScrollRect = GetActiveScrollRect();
        RectTransform activeScrollContent = GetActiveScrollContent();
        string panelName = IsImageMemoPanelActive() ? "Panel_ImageMemo" : "Panel_TextMemo";
        
        // 스크롤 위치 조정
        if (activeScrollRect != null)
        {
            activeScrollRect.verticalNormalizedPosition = targetPosition;
            Canvas.ForceUpdateCanvases();
            Debug.Log($"[TimePickerController] {panelName} 스크롤 위치 조정: {targetPosition}, Content 높이: {activeScrollContent?.sizeDelta.y}");
        }
        else
        {
            Debug.LogWarning($"[TimePickerController] {panelName} ScrollRect가 할당되지 않았습니다!");
        }
    }
    
    // 버튼 색상 애니메이션
    private System.Collections.IEnumerator AnimateButtonColor(UnityEngine.UI.Image image, Color targetColor)
    {
        Color fromColor = image.color;
        float elapsed = 0f;
        
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            
            image.color = Color.Lerp(fromColor, targetColor, t);
            yield return null;
        }
        
        image.color = targetColor;
    }
    
    // Outline 색상 애니메이션
    private System.Collections.IEnumerator AnimateOutlineColor(UnityEngine.UI.Outline outline, Color targetColor)
    {
        Color fromColor = outline.effectColor;
        float elapsed = 0f;
        
        Debug.Log($"[TimePickerController] ▶▶▶ AnimateOutlineColor 시작 - from: {fromColor} to: {targetColor}");
        
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            
            outline.effectColor = Color.Lerp(fromColor, targetColor, t);
            yield return null;
        }
        
        outline.effectColor = targetColor;
        Debug.Log($"[TimePickerController] ▶▶▶ AnimateOutlineColor 완료 - 최종 색상: {outline.effectColor}");
    }
    
    // 선택된 시간을 24시간 형식 문자열로 반환 ("09:00")
    public string GetSelectedTimeString()
    {
        int hour24 = ConvertTo24Hour(selectedHour, isAM);
        return $"{hour24:D2}:{selectedMinute:D2}";
    }
    
    // 선택된 시간을 12시간 형식 문자열로 반환 ("09:00 AM")
    public string GetSelectedTime12HourString()
    {
        string ampm = isAM ? "AM" : "PM";
        return $"{selectedHour:D2}:{selectedMinute:D2} {ampm}";
    }
    
    /// <summary>
    /// 시간이 선택되었는지 확인 (Outline 색상 변경용)
    /// 시간 버튼 텍스트가 비어있지 않으면 true
    /// </summary>
    public bool HasTimeSelected()
    {
        // 간단히: 시간 버튼 텍스트가 비어있지 않은지 확인
        if (timeButtonText != null && !string.IsNullOrWhiteSpace(timeButtonText.text))
        {
            return true;
        }
        if (imageTimeButtonText != null && !string.IsNullOrWhiteSpace(imageTimeButtonText.text))
        {
            return true;
        }
        if (checklistTimeButtonText != null && !string.IsNullOrWhiteSpace(checklistTimeButtonText.text))
        {
            return true;
        }
        if (voiceMemoTimeButtonText != null && !string.IsNullOrWhiteSpace(voiceMemoTimeButtonText.text))
        {
            return true;
        }
        return false;
    }
    
    // 시간 설정 (외부에서 호출 가능) - 24시간 형식
    public void SetTime(int hour24, int minute)
    {
        // 24시간 형식을 12시간 형식으로 변환
        if (hour24 == 0)
        {
            selectedHour = 12;
            isAM = true;
        }
        else if (hour24 < 12)
        {
            selectedHour = hour24;
            isAM = true;
        }
        else if (hour24 == 12)
        {
            selectedHour = 12;
            isAM = false;
        }
        else
        {
            selectedHour = hour24 - 12;
            isAM = false;
        }
        
        selectedMinute = minute;
        
        UpdateTimeButtonText();
        
        // 패널이 열려있으면 색상도 업데이트
        if (timePickerPanel != null && timePickerPanel.activeSelf)
        {
            UpdateAMPMButtons();
            UpdateTimeButtonColors();
        }
        
        Debug.Log($"[TimePickerController] 시간 설정됨: {GetSelectedTimeString()}");
    }
    
    // 시간 선택 패널 닫기 (외부에서 호출 가능)
    public void CloseTimePicker(bool restoreScrollPosition = true)
    {
        if (isTimePickerOpen)
        {
            isTimePickerOpen = false;
            CloseTimePickerInternal(restoreScrollPosition);
        }
    }
    
    // 시간 선택 패널이 열려있는지 확인 (외부에서 호출 가능)
    public bool IsTimePickerOpen()
    {
        return isTimePickerOpen;
    }
    
#if UNITY_EDITOR
    // Inspector에서 현재 Deadline 위치를 deadlineTargetY로 복사하는 헬퍼 함수
    [UnityEngine.ContextMenu("Copy Current Deadline Position")]
    private void CopyCurrentDeadlinePosition()
    {
        if (deadlineRow != null)
        {
            deadlineTargetY = deadlineRow.anchoredPosition.y;
            UnityEngine.Debug.Log($"[TimePickerController] Deadline의 현재 Y 위치를 복사했습니다: {deadlineTargetY}");
        }
        else
        {
            UnityEngine.Debug.LogWarning("[TimePickerController] DeadlineRow가 할당되지 않았습니다!");
        }
    }
#endif
    
    /// <summary>
    /// 체크리스트 아이템 추가/삭제에 따른 스크롤 확장량 설정
    /// ChecklistUIController에서 호출
    /// </summary>
    public void SetChecklistItemScrollExpansion(float expansion)
    {
        checklistItemScrollExpansion = expansion;
        Debug.Log($"[TimePickerController] 체크리스트 아이템 스크롤 확장량 설정: {expansion}");
    }
    
    /// <summary>
    /// 현재 체크리스트 아이템 스크롤 확장량 반환
    /// </summary>
    public float GetChecklistItemScrollExpansion()
    {
        return checklistItemScrollExpansion;
    }
    
    /// <summary>
    /// Checklist ScrollContent의 현재 높이 반환
    /// </summary>
    public float GetChecklistScrollContentHeight()
    {
        if (checklistScrollContent != null)
        {
            return checklistScrollContent.sizeDelta.y;
        }
        return 0f;
    }
    
    /// <summary>
    /// Checklist TimePicker가 열려있을 때의 총 스크롤 확장량 반환
    /// (타임피커 확장 + 아이템 확장)
    /// </summary>
    public float GetChecklistTotalScrollExpansion()
    {
        return checklistScrollExpansion + checklistItemScrollExpansion;
    }
    
    /// <summary>
    /// VoiceMemo - TimePicker가 열렸을 때 총 ScrollContent 확장량 반환
    /// (타임피커 확장 + 아이템 확장)
    /// </summary>
    public float GetVoiceMemoTotalScrollExpansion()
    {
        return voiceMemoScrollExpansion + voiceMemoItemScrollExpansion;
    }
    
    /// <summary>
    /// VoiceMemo TimePickerPanel RectTransform 가져오기
    /// </summary>
    public RectTransform GetVoiceTimePickerPanelRect()
    {
        return voiceMemoTimePickerPanelRect;
    }
    
    /// <summary>
    /// Checklist TimePicker가 열렸을 때 Emergency를 밀어야 하는 양 반환
    /// (TimePicker 높이 + Emergency 오프셋)
    /// </summary>
    public float GetChecklistTimePickerPushAmount()
    {
        float timePickerHeight = 0f;
        if (checklistTimePickerPanelRect != null)
        {
            timePickerHeight = checklistTimePickerPanelRect.sizeDelta.y;
        }
        return timePickerHeight + checklistEmergencyOffset;
    }
    
    /// <summary>
    /// 체크리스트 스크롤 Content 크기를 즉시 업데이트
    /// 아이템 추가/삭제 시 호출 (TimePicker 열림 여부와 관계없이)
    /// 위치 보정은 ChecklistUIController에서 처리
    /// </summary>
    public void UpdateChecklistScrollContentSize()
    {
        // Checklist 패널이 활성화되어 있지 않으면 스킵
        if (!IsChecklistPanelActive()) return;
        
        if (checklistScrollContent == null)
        {
            Debug.LogWarning("[TimePickerController] checklistScrollContent가 NULL입니다!");
            return;
        }
        
        // 새 높이 계산
        float baseHeight = checklistScrollContentOriginalHeight;
        float timePickerExpansion = 0f;
        
        // TimePicker가 열려있으면 TimePicker 확장량도 포함
        if (isTimePickerOpen && checklistTimePickerPanelRect != null)
        {
            float pushAmount = 0f;
            if (checklistUIController != null)
            {
                pushAmount = checklistUIController.LastPushAmount;
            }
            timePickerExpansion = checklistScrollExpansion + pushAmount;
        }
        
        float newHeight = baseHeight + checklistItemScrollExpansion + timePickerExpansion;
        
        Vector2 newSize = checklistScrollContent.sizeDelta;
        float previousHeight = newSize.y;
        
        // 높이 변화가 없으면 스킵
        if (Mathf.Abs(newHeight - previousHeight) < 0.01f) return;
        
        newSize.y = newHeight;
        checklistScrollContent.sizeDelta = newSize;
        
        // 위치 보정은 ChecklistUIController.UpdateLayoutBasedOnItemCount()에서 처리
        // (스트레치 앵커 보정을 여기서 하면 ChecklistUIController와 충돌)
        
        Debug.Log($"[TimePickerController] ▶▶▶ Checklist ScrollContent 즉시 업데이트: {previousHeight} -> {newHeight} (아이템확장: {checklistItemScrollExpansion})");
    }
    
    /// <summary>
    /// Checklist TimePicker가 열려있을 때 스크롤 크기만 업데이트 (레거시 - 호환성 유지)
    /// </summary>
    public void RefreshChecklistTimePickerLayout(float pushDelta)
    {
        // RefreshChecklistLayout으로 대체됨
    }
    
    /// <summary>
    /// Checklist TimePicker가 열려있을 때 전체 레이아웃 갱신
    /// 절대 위치 방식 (anchoredPosition 기준)
    /// </summary>
    /// <param name="itemPushAmount">체크리스트 아이템으로 인한 밀림량</param>
    public void RefreshChecklistLayout(float itemPushAmount)
    {
        // TimePicker가 열려있지 않으면 스킵
        if (!isTimePickerOpen || !IsChecklistPanelActive()) return;
        
        if (checklistScrollContent == null)
        {
            Debug.LogWarning("[TimePickerController] RefreshChecklistLayout - checklistScrollContent가 NULL!");
            return;
        }
        
        // ★★★ 1. 원본 위치/높이 가져오기 ★★★
        GetActiveOriginalPositions(out Vector2 deadlineOrig, out Vector2 emergencyOrig, out Vector2 assigneeOrig, 
                                   out Vector2 bodyOrig, out Vector2 imageMovieOrig, out float scrollHeightOrig, out Vector2 scrollPosOrig, 
                                   out Dictionary<Transform, Vector2> childPosOrig);
        
        // ★★★ 2. 타임피커 패널 높이 가져오기 ★★★
        float timePickerHeight = 0f;
        if (checklistTimePickerPanelRect != null)
        {
            timePickerHeight = checklistTimePickerPanelRect.sizeDelta.y;
        }
        float panelPush = timePickerHeight + checklistEmergencyOffset;
        
        // ★★★ 3. 총 확장량 및 앵커 보정 계산 ★★★
        // 주의: itemPushAmount는 위치 계산용이므로 스크롤 확장에는 포함하지 않음
        // checklistItemScrollExpansion이 이미 아이템 기반 확장을 포함함
        float totalExpansion = checklistScrollExpansion + checklistItemScrollExpansion;
        float anchorComp = totalExpansion / 2f;
        
        Debug.Log($"[TimePickerController] ★★★ Checklist RefreshLayout 값 비교 ★★★");
        Debug.Log($"  - timePickerHeight: {timePickerHeight}, checklistEmergencyOffset: {checklistEmergencyOffset}, panelPush: {panelPush}");
        Debug.Log($"  - checklistScrollExpansion: {checklistScrollExpansion}, checklistItemScrollExpansion: {checklistItemScrollExpansion}");
        Debug.Log($"  - totalExpansion: {totalExpansion}, anchorComp: {anchorComp}");
        Debug.Log($"  - scrollHeightOrig: {scrollHeightOrig}, deadlineOrig: {deadlineOrig}, emergencyOrig: {emergencyOrig}");
        
        // ★★★ 4. ScrollContent 크기 업데이트 ★★★
        Vector2 newSize = checklistScrollContent.sizeDelta;
        float previousHeight = newSize.y;
        newSize.y = scrollHeightOrig + totalExpansion;
        checklistScrollContent.sizeDelta = newSize;
        
        // ★★★ 5. 절대 위치 설정 (원래 위치 + 앵커 보정 - 밀림) ★★★
        // CheckList: 원래 위치 + 앵커 보정 (밀림 없음)
        if (checklistCheckListRow != null)
        {
            Vector2 newPos = checklistCheckListOriginalPos;
            newPos.y = checklistCheckListOriginalPos.y + anchorComp;
            checklistCheckListRow.anchoredPosition = newPos;
        }
        
        // Deadline: 원래 위치 + 앵커 보정 - 아이템 밀림
        if (checklistDeadlineRow != null)
        {
            Vector2 newPos = deadlineOrig;
            newPos.y = deadlineOrig.y + anchorComp - itemPushAmount;
            checklistDeadlineRow.anchoredPosition = newPos;
        }
        
        // TimePickerPanel: 원래 위치 + 앵커 보정 - 아이템 밀림
        if (checklistTimePickerPanelRect != null)
        {
            Vector2 newPos = checklistTimePickerOriginalPos;
            newPos.y = checklistTimePickerOriginalPos.y + anchorComp - itemPushAmount;
            checklistTimePickerPanelRect.anchoredPosition = newPos;
        }
        
        // Emergency: 원래 위치 + 앵커보정 - 패널밀림 - 아이템밀림
        // ★★★ 하단 여백 일정하게 유지: 패널 열림 + 아이템 밀림 모두 반영 ★★★
        if (checklistEmergencyRow != null)
        {
            float panelAnchorComp = checklistScrollExpansion / 2f;
            Vector2 newPos = emergencyOrig;
            // 앵커 보정 + 패널 밀림 + 아이템 밀림 모두 반영
            newPos.y = emergencyOrig.y + panelAnchorComp - panelPush - itemPushAmount;
            checklistEmergencyRow.anchoredPosition = newPos;
            
            // 하단 여백 계산 (스크롤 높이/2 + Emergency.y = 스크롤 바닥에서 Emergency까지 거리)
            float bottomMargin = (newSize.y / 2f) + newPos.y;
            Debug.Log($"[TimePickerController] Emergency - origY: {emergencyOrig.y}, panelComp: {panelAnchorComp}, panelPush: {panelPush}, itemPush: {itemPushAmount}, newY: {newPos.y}, 하단여백: {bottomMargin}");
        }
        
        Debug.Log($"[TimePickerController] RefreshChecklistLayout 완료 - 높이: {previousHeight} -> {newSize.y}, itemExpansion: {checklistItemScrollExpansion}");
        
        // InputField 위치는 ScrollContent 앵커 설정에 따라 자동 조정됨
    }
    
    /// <summary>
    /// VoiceMemo TimePicker가 열려있을 때 전체 레이아웃 갱신
    /// 절대 위치 방식 (anchoredPosition 기준)
    /// </summary>
    /// <param name="itemPushAmount">음성메모 아이템으로 인한 밀림량</param>
    public void RefreshVoiceMemoLayout(float itemPushAmount)
    {
        // TimePicker가 열려있지 않으면 스킵
        if (!isTimePickerOpen || !IsVoiceMemoPanelActive()) return;
        
        if (voiceMemoScrollContent == null)
        {
            Debug.LogWarning("[TimePickerController] RefreshVoiceMemoLayout - voiceMemoScrollContent가 NULL!");
            return;
        }
        
        // ★★★ 1. 원본 위치/높이 가져오기 ★★★
        GetActiveOriginalPositions(out Vector2 deadlineOrig, out Vector2 emergencyOrig, out Vector2 assigneeOrig, 
                                   out Vector2 bodyOrig, out Vector2 imageMovieOrig, out float scrollHeightOrig, out Vector2 scrollPosOrig, 
                                   out Dictionary<Transform, Vector2> childPosOrig);
        
        // ★★★ 2. 타임피커 패널 높이 가져오기 ★★★
        float timePickerHeight = 0f;
        if (voiceMemoTimePickerPanelRect != null)
        {
            timePickerHeight = voiceMemoTimePickerPanelRect.sizeDelta.y;
        }
        float panelPush = timePickerHeight + voiceMemoEmergencyOffset;
        
        // ★★★ 3. 총 확장량 및 앵커 보정 계산 ★★★
        float totalExpansion = voiceMemoScrollExpansion + voiceMemoItemScrollExpansion;
        float anchorComp = totalExpansion / 2f;
        
        Debug.Log($"[TimePickerController] ★★★ VoiceMemo RefreshLayout 값 비교 ★★★");
        Debug.Log($"  - timePickerHeight: {timePickerHeight}, voiceMemoEmergencyOffset: {voiceMemoEmergencyOffset}, panelPush: {panelPush}");
        Debug.Log($"  - voiceMemoScrollExpansion: {voiceMemoScrollExpansion}, voiceMemoItemScrollExpansion: {voiceMemoItemScrollExpansion}");
        Debug.Log($"  - totalExpansion: {totalExpansion}, anchorComp: {anchorComp}");
        Debug.Log($"  - scrollHeightOrig: {scrollHeightOrig}, deadlineOrig: {deadlineOrig}, emergencyOrig: {emergencyOrig}");
        
        // ★★★ 4. ScrollContent 크기 업데이트 ★★★
        Vector2 newSize = voiceMemoScrollContent.sizeDelta;
        float previousHeight = newSize.y;
        newSize.y = scrollHeightOrig + totalExpansion;
        voiceMemoScrollContent.sizeDelta = newSize;
        
        // ★★★ 5. 절대 위치 설정 (원래 위치 + 앵커 보정 - 밀림) ★★★
        // VoiceMemoRow: 원래 위치 + 앵커 보정 (부모 Voice는 기본 위치 유지)
        if (voiceMemoVoiceMemoRow != null)
        {
            Vector2 newPos = voiceMemoVoiceMemoOriginalPos;
            newPos.y = voiceMemoVoiceMemoOriginalPos.y + anchorComp;
            voiceMemoVoiceMemoRow.anchoredPosition = newPos;
            Debug.Log($"[TimePickerController] Voice 위치 조정 (TimePicker 열림): origY={voiceMemoVoiceMemoOriginalPos.y}, anchorComp={anchorComp}, newY={newPos.y}");
        }
        
        // Voice 요소들(VoiceTitle, VoiceContents)은 Voice의 자식이므로 부모를 따라 자동 이동
        // 별도의 위치 조정은 하지 않음 (필요 시 Inspector에서 voiceMemoVoiceElements 배열 사용)
        if (voiceMemoVoiceElements != null && voiceMemoVoiceElements.Length > 0)
        {
            foreach (var voiceElement in voiceMemoVoiceElements)
            {
                if (voiceElement != null && voiceElement.element != null && voiceMemoVoiceElementsOriginalPos.ContainsKey(voiceElement.element))
                {
                    // openedOffsetY가 0이 아닌 경우에만 추가 조정 (부모에 대한 상대 위치 미세 조정)
                    if (voiceElement.openedOffsetY != 0f)
                    {
                        Vector2 originalPos = voiceMemoVoiceElementsOriginalPos[voiceElement.element];
                        Vector2 newPos = originalPos;
                        newPos.y = originalPos.y + voiceElement.openedOffsetY;
                        voiceElement.element.anchoredPosition = newPos;
                        Debug.Log($"[TimePickerController] {voiceElement.element.name} 미세 조정 (TimePicker 열림): origY={originalPos.y}, openedOffset={voiceElement.openedOffsetY}, newY={newPos.y}");
                    }
                }
            }
        }
        
        // Deadline: 원래 위치 + 앵커 보정 - 아이템 밀림
        if (voiceMemoDeadlineRow != null)
        {
            Vector2 newPos = deadlineOrig;
            newPos.y = deadlineOrig.y + anchorComp - itemPushAmount;
            voiceMemoDeadlineRow.anchoredPosition = newPos;
        }
        
        // TimePickerPanel: 원래 위치 + 앵커 보정 - 아이템 밀림
        if (voiceMemoTimePickerPanelRect != null)
        {
            Vector2 newPos = voiceMemoTimePickerOriginalPos;
            newPos.y = voiceMemoTimePickerOriginalPos.y + anchorComp - itemPushAmount;
            voiceMemoTimePickerPanelRect.anchoredPosition = newPos;
        }
        
        // Emergency: 원래 위치 + 앵커보정 - 패널밀림 - 아이템밀림
        if (voiceMemoEmergencyRow != null)
        {
            float panelAnchorComp = voiceMemoScrollExpansion / 2f;
            Vector2 newPos = emergencyOrig;
            newPos.y = emergencyOrig.y + panelAnchorComp - panelPush - itemPushAmount;
            voiceMemoEmergencyRow.anchoredPosition = newPos;
            
            float bottomMargin = (newSize.y / 2f) + newPos.y;
            Debug.Log($"[TimePickerController] VoiceMemo Emergency - origY: {emergencyOrig.y}, panelComp: {panelAnchorComp}, panelPush: {panelPush}, itemPush: {itemPushAmount}, newY: {newPos.y}, 하단여백: {bottomMargin}");
        }
        
        Debug.Log($"[TimePickerController] RefreshVoiceMemoLayout 완료 - 높이: {previousHeight} -> {newSize.y}, itemExpansion: {voiceMemoItemScrollExpansion}");
    }
    
    /// <summary>
    /// VoiceMemo 패널이 열릴 때 모든 요소를 원래 위치로 리셋
    /// VoiceMemoUIController.OnEnable()에서 호출
    /// </summary>
    public void ResetVoiceMemoToOriginalLayout()
    {
        Debug.Log("[TimePickerController] ResetVoiceMemoToOriginalLayout 호출");
        
        // TimePicker가 열려있으면 닫기
        if (isTimePickerOpen && IsVoiceMemoPanelActive())
        {
            CloseTimePicker();
        }
        
        // VoiceMemo 아이템 스크롤 확장량 리셋
        voiceMemoItemScrollExpansion = 0f;
        
        // 모든 요소를 원래 위치로 복원
        if (voiceMemoDeadlineRow != null)
        {
            voiceMemoDeadlineRow.anchoredPosition = voiceMemoDeadlineOriginalPos;
            Debug.Log($"[TimePickerController] VoiceMemo Deadline 리셋: {voiceMemoDeadlineOriginalPos}");
        }
        
        if (voiceMemoEmergencyRow != null)
        {
            voiceMemoEmergencyRow.anchoredPosition = voiceMemoEmergencyOriginalPos;
            Debug.Log($"[TimePickerController] VoiceMemo Emergency 리셋: {voiceMemoEmergencyOriginalPos}");
        }
        
        if (voiceMemoVoiceMemoRow != null)
        {
            voiceMemoVoiceMemoRow.anchoredPosition = voiceMemoVoiceMemoOriginalPos;
            Debug.Log($"[TimePickerController] Voice 리셋: {voiceMemoVoiceMemoOriginalPos}");
        }
        
        // Voice 요소들(VoiceTitle, VoiceContents)은 Voice의 자식이므로 부모를 따라 자동 이동
        // closedOffsetY가 0이 아닌 경우에만 추가 조정
        if (voiceMemoVoiceElements != null && voiceMemoVoiceElements.Length > 0)
        {
            foreach (var voiceElement in voiceMemoVoiceElements)
            {
                if (voiceElement != null && voiceElement.element != null && voiceMemoVoiceElementsOriginalPos.ContainsKey(voiceElement.element))
                {
                    if (voiceElement.closedOffsetY != 0f)
                    {
                        Vector2 originalPos = voiceMemoVoiceElementsOriginalPos[voiceElement.element];
                        Vector2 resetPos = originalPos;
                        resetPos.y += voiceElement.closedOffsetY;
                        voiceElement.element.anchoredPosition = resetPos;
                        Debug.Log($"[TimePickerController] {voiceElement.element.name} 미세 조정 (TimePicker 닫힘): origY={originalPos.y}, closedOffset={voiceElement.closedOffsetY}, newY={resetPos.y}");
                    }
                }
            }
        }
        
        if (voiceMemoTimePickerPanelRect != null)
        {
            voiceMemoTimePickerPanelRect.anchoredPosition = voiceMemoTimePickerOriginalPos;
        }
        
        // ScrollContent 크기 및 위치 복원
        if (voiceMemoScrollContent != null)
        {
            voiceMemoScrollContent.sizeDelta = new Vector2(voiceMemoScrollContent.sizeDelta.x, voiceMemoScrollContentOriginalHeight);
            voiceMemoScrollContent.anchoredPosition = voiceMemoScrollContentOriginalPos;
            
            // 모든 자식 요소 원래 위치로 복원
            foreach (var kvp in voiceMemoChildOriginalPositions)
            {
                if (kvp.Key != null)
                {
                    RectTransform childRect = kvp.Key.GetComponent<RectTransform>();
                    if (childRect != null)
                    {
                        childRect.anchoredPosition = kvp.Value;
                    }
                }
            }
            Debug.Log($"[TimePickerController] VoiceMemo ScrollContent 리셋 - 높이: {voiceMemoScrollContentOriginalHeight}");
        }
        
        Debug.Log("[TimePickerController] ResetVoiceMemoToOriginalLayout 완료");
    }
}
