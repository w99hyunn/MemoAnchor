using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

/// <summary>
/// VoiceMemo Voice 요소 오프셋 설정 클래스 - Calendar 열림/닫힘 시 각각의 오프셋 설정
/// </summary>
[System.Serializable]
public class VoiceElementOffsetSettings
{
    [Tooltip("조절할 요소 (VoiceTitle, VoiceContents 등)")]
    public RectTransform element;
    
    [Tooltip("Calendar/TimePicker 닫힘 상태에서의 Y 오프셋")]
    public float closedOffsetY = 0f;
    
    [Tooltip("Calendar/TimePicker 열림 상태에서의 Y 오프셋")]
    public float openedOffsetY = 0f;
}

public class CalendarController : MonoBehaviour
{
    // ==================== Panel_TextMemo ====================
    [Header("========== Panel_TextMemo ==========")]
    [Space(5)]
    
    [Header("TextMemo - UI References")]
    [SerializeField] private Button dateBt;                    // 날짜 버튼
    [SerializeField] private TMP_Text dateButtonText;          // 날짜 버튼 텍스트
    [SerializeField] private GameObject calendarPanel;         // 달력 패널
    [SerializeField] private TMP_Text monthYearText;           // 년월 표시
    [SerializeField] private Button prevMonthButton;           // 이전 달 버튼
    [SerializeField] private Button nextMonthButton;           // 다음 달 버튼
    [SerializeField] private Button closeButton;               // 달력 닫기 버튼
    [SerializeField] private Transform calendarDaysContainer;  // 날짜 버튼들의 부모
    
    [Header("TextMemo - Layout References")]
    [SerializeField] private RectTransform calendarPanelRect;  // CalendarPanel RectTransform
    [SerializeField] private RectTransform deadlineRow;        // Deadline 행
    [SerializeField] private RectTransform emergencyRow;       // Emergency 행
    [SerializeField] private RectTransform assigneeRow;        // AssigneeRow
    [SerializeField] private RectTransform inputFieldBody;     // InputField_Body
    [SerializeField] private RectTransform scrollContent;      // ScrollRect의 Content
    [SerializeField] private UnityEngine.UI.ScrollRect scrollRect; // ScrollRect 컴포넌트
    
    [Header("TextMemo - Element Position")]
    [Tooltip("Deadline 위치를 수동으로 설정")]
    [SerializeField] private bool useManualDeadlinePosition = false;
    [SerializeField] private float deadlineTargetY = 0f;
    [SerializeField] private float deadlineOffset = 0f;
    [SerializeField] private float emergencyOffset = 0f;
    [SerializeField] private float assigneeOffset = 0f;
    [SerializeField] private float bodyOffset = 0f;
    
    [Header("TextMemo - Scroll Settings")]
    [SerializeField] private float autoScrollOnOpen = 0f;
    [SerializeField] private float autoScrollOnOpenFromTimePicker = 0f;
    [SerializeField] [Range(0f, 1f)] private float scrollBottomLimit = 0f;
    [SerializeField] private float scrollDownAmountOnClose = 0f;
    
    // ==================== Panel_ImageMemo ====================
    [Header("========== Panel_ImageMemo ==========")]
    [Space(5)]
    
    [Header("ImageMemo - UI References")]
    [SerializeField] private Button imageDateBt;               // 날짜 버튼
    [SerializeField] private TMP_Text imageDateButtonText;     // 날짜 버튼 텍스트
    [SerializeField] private GameObject imageMemoCalendarPanel; // 달력 패널
    [SerializeField] private TMP_Text imageMemoMonthYearText;  // 년월 표시
    [SerializeField] private Button imagePrevMonthButton;      // 이전 달 버튼
    [SerializeField] private Button imageNextMonthButton;      // 다음 달 버튼
    [SerializeField] private Button imageCloseButton;          // 달력 닫기 버튼
    [SerializeField] private Transform imageMemoCalendarDaysContainer; // 날짜 버튼들의 부모
    
    [Header("ImageMemo - Layout References")]
    [SerializeField] private RectTransform imageMemoCalendarPanelRect; // CalendarPanel RectTransform
    [SerializeField] private RectTransform imageMemoDeadlineRow;       // Deadline 행
    [SerializeField] private RectTransform imageMemoEmergencyRow;      // Emergency 행
    [SerializeField] private RectTransform imageMemoAssigneeRow;       // AssigneeRow
    [SerializeField] private RectTransform imageMemoInputFieldBody;    // InputField_Body
    [SerializeField] private RectTransform imageMemoImageMovie;        // ImageMovie
    [SerializeField] private RectTransform imageMemoScrollContent;     // ScrollRect의 Content
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
    [SerializeField] private float imageMemoAutoScrollOnOpenFromTimePicker = 0f;
    [SerializeField] [Range(0f, 1f)] private float imageMemoScrollBottomLimit = 0f;
    [SerializeField] private float imageMemoScrollDownAmountOnClose = 0f;
    
    // ==================== Panel_Checklist ====================
    [Header("========== Panel_Checklist ==========")]
    [Space(5)]
    
    [Header("Checklist - UI References")]
    [SerializeField] private Button checklistDateBt;               // 날짜 버튼
    [SerializeField] private TMP_Text checklistDateButtonText;     // 날짜 버튼 텍스트
    [SerializeField] private GameObject checklistCalendarPanel;    // 달력 패널
    [SerializeField] private TMP_Text checklistMonthYearText;      // 년월 표시
    [SerializeField] private Button checklistPrevMonthButton;      // 이전 달 버튼
    [SerializeField] private Button checklistNextMonthButton;      // 다음 달 버튼
    [SerializeField] private Button checklistCloseButton;          // 달력 닫기 버튼
    [SerializeField] private Transform checklistCalendarDaysContainer; // 날짜 버튼들의 부모
    
    [Header("Checklist - Layout References")]
    [SerializeField] private RectTransform checklistCalendarPanelRect; // CalendarPanel RectTransform
    [SerializeField] private RectTransform checklistDeadlineRow;       // Deadline 행
    [SerializeField] private RectTransform checklistEmergencyRow;      // Emergency 행
    [SerializeField] private RectTransform checklistCheckListRow;      // CheckList
    [SerializeField] private RectTransform checklistScrollContent;     // ScrollRect의 Content
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
    [SerializeField] private float checklistAutoScrollOnOpenFromTimePicker = 0f;
    [SerializeField] [Range(0f, 1f)] private float checklistScrollBottomLimit = 0f;
    [SerializeField] private float checklistScrollDownAmountOnClose = 0f;
    
    // ==================== Panel_VoiceMemo ====================
    [Header("========== Panel_VoiceMemo ==========")]
    [Space(5)]
    
    [Header("VoiceMemo - UI References")]
    [SerializeField] private Button voiceMemoDateBt;               // 날짜 버튼
    [SerializeField] private TMP_Text voiceMemoDateButtonText;     // 날짜 버튼 텍스트
    [SerializeField] private GameObject voiceMemoCalendarPanel;    // 달력 패널
    [SerializeField] private TMP_Text voiceMemoMonthYearText;      // 년월 표시
    [SerializeField] private Button voiceMemoPrevMonthButton;      // 이전 달 버튼
    [SerializeField] private Button voiceMemoNextMonthButton;      // 다음 달 버튼
    [SerializeField] private Button voiceMemoCloseButton;          // 달력 닫기 버튼
    [SerializeField] private Transform voiceMemoCalendarDaysContainer; // 날짜 버튼들의 부모
    
    [Header("VoiceMemo - Layout References")]
    [SerializeField] private RectTransform voiceMemoCalendarPanelRect; // CalendarPanel RectTransform
    [SerializeField] private RectTransform voiceMemoDeadlineRow;       // Deadline 행
    [SerializeField] private RectTransform voiceMemoEmergencyRow;      // Emergency 행
    [SerializeField] private RectTransform voiceMemoVoiceMemoRow;      // VoiceMemo (메모 목록 영역)
    [SerializeField] private RectTransform voiceMemoScrollContent;     // ScrollRect의 Content
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
    [SerializeField] private float voiceMemoAutoScrollOnOpenFromTimePicker = 0f;
    [SerializeField] [Range(0f, 1f)] private float voiceMemoScrollBottomLimit = 0f;
    [SerializeField] private float voiceMemoScrollDownAmountOnClose = 0f;
    
    
    // ==================== Common Settings ====================
    [Header("========== Common Settings ==========")]
    [Space(5)]
    
    [Header("Common - Prefab")]
    [SerializeField] private GameObject dayButtonPrefab;       // 날짜 버튼 프리팹
    
    [Header("Common - Cross References")]
    [SerializeField] private TimePickerController timePickerController;
    [SerializeField] private ChecklistUIController checklistUIController;
    [SerializeField] private VoiceMemoUIController voiceMemoUIController;
    [SerializeField] private AssigneeDropdownManager assigneeDropdownManager;
    [SerializeField] private GameObject panelChecklist;
    [SerializeField] private GameObject panelVoiceMemo;
    
    [Header("Common - Settings")]
    [SerializeField] private string dateButtonImageName = "Image";
    [SerializeField] private string backgroundObjectName = "Background";
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private float scrollHeightMultiplier = 2f;
    [SerializeField] private float extraScrollPadding = 200f;
    
    [Header("Common - Colors")]
    [SerializeField] private Color todayColor = new Color(0.59f, 0.85f, 0.95f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.27f, 0.65f, 0.80f, 1f);
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color dateButtonOpenColor = new Color(0.59f, 0.80f, 0.88f, 1f);
    [SerializeField] private Color dateButtonOutlineUnselected = new Color(0.59f, 0.80f, 0.88f, 1f);
    [SerializeField] private Color dateButtonOutlineSelected = new Color(0.85f, 0.85f, 0.85f, 1f);
    
    [Header("Common - Text Colors")]
    [SerializeField] private Color normalTextColor = Color.black;
    [SerializeField] private Color selectedTextColor = Color.white;
    [SerializeField] private Color todayTextColor = Color.white;
    
    // ==================== Private Variables ====================
    private float checklistItemScrollExpansion = 0f;
    
    private DateTime currentDisplayDate;                       // 현재 표시중인 달
    private DateTime selectedDate;                             // 선택된 날짜
    private List<GameObject> allDayObjects = new List<GameObject>(); // 생성된 모든 날짜 오브젝트 (빈 칸 포함)
    private Dictionary<DateTime, GameObject> dayButtonMap = new Dictionary<DateTime, GameObject>(); // 날짜-버튼 매핑
    private bool isCalendarOpen = false;                       // 캘린더 열림 상태
    private UnityEngine.UI.Image dateButtonImage;              // DateBt 배경 이미지 (Panel_TextMemo)
    private UnityEngine.UI.Outline dateButtonOutline;          // DateBt 배경 Outline (Panel_TextMemo)
    private UnityEngine.UI.Image imageDateButtonImage;         // ImageDateBt 배경 이미지 (Panel_ImageMemo)
    private UnityEngine.UI.Outline imageDateButtonOutline;     // ImageDateBt 배경 Outline (Panel_ImageMemo)
    private Vector2 calendarOriginalPos;                       // CalendarPanel 원래 위치 (Panel_TextMemo)
    private Vector2 imageMemoCalendarOriginalPos;              // CalendarPanel 원래 위치 (Panel_ImageMemo)
    
    // Panel_TextMemo 원래 위치
    private Vector2 deadlineOriginalPos;                       // Deadline 원래 위치
    private Vector2 emergencyOriginalPos;                      // Emergency 원래 위치
    private Vector2 assigneeOriginalPos;                       // AssigneeRow 원래 위치
    private Vector2 bodyOriginalPos;                           // InputField_Body 원래 위치
    private float scrollContentOriginalHeight;                 // ScrollContent 원래 높이
    private Vector2 scrollContentOriginalPos;                  // ScrollContent 원래 위치
    private Vector2 scrollContentOriginalOffsetMax;            // ScrollContent 원래 offsetMax
    private Vector2 scrollContentOriginalOffsetMin;            // ScrollContent 원래 offsetMin
    private Dictionary<Transform, Vector2> childOriginalPositions = new Dictionary<Transform, Vector2>(); // 모든 자식의 원래 위치
    
    // Panel_ImageMemo 원래 위치
    private Vector2 imageMemoDeadlineOriginalPos;              // ImageMemo Deadline 원래 위치
    private Vector2 imageMemoEmergencyOriginalPos;             // ImageMemo Emergency 원래 위치
    private Vector2 imageMemoAssigneeOriginalPos;              // ImageMemo AssigneeRow 원래 위치
    private Vector2 imageMemoBodyOriginalPos;                  // ImageMemo InputField_Body 원래 위치
    private Vector2 imageMemoImageMovieOriginalPos;            // ImageMemo ImageMovie 원래 위치
    private float imageMemoScrollContentOriginalHeight;        // ImageMemo ScrollContent 원래 높이
    private Vector2 imageMemoScrollContentOriginalPos;         // ImageMemo ScrollContent 원래 위치
    private Vector2 imageMemoScrollContentOriginalOffsetMax;   // ImageMemo ScrollContent 원래 offsetMax
    private Vector2 imageMemoScrollContentOriginalOffsetMin;   // ImageMemo ScrollContent 원래 offsetMin
    private Dictionary<Transform, Vector2> imageMemoChildOriginalPositions = new Dictionary<Transform, Vector2>(); // ImageMemo 모든 자식의 원래 위치
    
    // Panel_Checklist 원래 위치 저장
    private Vector2 checklistCalendarOriginalPos;               // Checklist CalendarPanel 원래 위치
    private Vector2 checklistDeadlineOriginalPos;               // Checklist Deadline 원래 위치
    private Vector2 checklistEmergencyOriginalPos;              // Checklist Emergency 원래 위치
    private Vector2 checklistCheckListOriginalPos;              // Checklist CheckList 원래 위치
    private float checklistScrollContentOriginalHeight;         // Checklist ScrollContent 원래 높이
    private Vector2 checklistScrollContentOriginalPos;          // Checklist ScrollContent 원래 위치
    private Dictionary<Transform, Vector2> checklistChildOriginalPositions = new Dictionary<Transform, Vector2>(); // Checklist 모든 자식의 원래 위치
    
    // Checklist 버튼 이미지
    private UnityEngine.UI.Image checklistDateButtonImage;
    private UnityEngine.UI.Outline checklistDateButtonOutline;
    
    // Panel_VoiceMemo 원래 위치 저장
    private Vector2 voiceMemoCalendarOriginalPos;               // VoiceMemo CalendarPanel 원래 위치
    private Vector2 voiceMemoDeadlineOriginalPos;               // VoiceMemo Deadline 원래 위치
    private Vector2 voiceMemoEmergencyOriginalPos;              // VoiceMemo Emergency 원래 위치
    private Vector2 voiceMemoVoiceMemoOriginalPos;              // VoiceMemo VoiceMemo 원래 위치
    private float voiceMemoScrollContentOriginalHeight;         // VoiceMemo ScrollContent 원래 높이
    private Vector2 voiceMemoScrollContentOriginalPos;          // VoiceMemo ScrollContent 원래 위치
    private Dictionary<Transform, Vector2> voiceMemoChildOriginalPositions = new Dictionary<Transform, Vector2>(); // VoiceMemo 모든 자식의 원래 위치
    private Dictionary<RectTransform, Vector2> voiceMemoVoiceElementsOriginalPos = new Dictionary<RectTransform, Vector2>(); // VoiceMemo Voice 요소들의 원래 위치
    
    // VoiceMemo 버튼 이미지
    private UnityEngine.UI.Image voiceMemoDateButtonImage;
    private UnityEngine.UI.Outline voiceMemoDateButtonOutline;
    
    // VoiceMemo 아이템 스크롤 확장량
    private float voiceMemoItemScrollExpansion = 0f;
    
    // 닫을 때 스크롤 복원 추적용
    private bool isWaitingForScrollRestore = false;                  // 스크롤 복원 대기 중인지
    private UnityEngine.UI.ScrollRect scrollRectToRestore;           // 복원할 ScrollRect
    private UnityEngine.UI.ScrollRect.MovementType originalMovementType; // 원래 movementType
    private Vector2 originalContentPosition;                         // 원래 Content 위치
    private float scrollDownAmountUsed;                              // 사용된 스크롤 양 (복원 임계값 계산용)
    
    private void Start()
    {
        // 초기 날짜 설정 (오늘)
        currentDisplayDate = DateTime.Now;
        selectedDate = DateTime.Now;
        
        // ★★★ AssigneeDropdownManager 자동 검색 ★★★
        if (assigneeDropdownManager == null)
        {
            assigneeDropdownManager = FindObjectOfType<AssigneeDropdownManager>();
            if (assigneeDropdownManager != null)
            {
                Debug.Log("[CalendarController] AssigneeDropdownManager 자동 검색 완료");
            }
        }
        
        // DateBt 배경 이미지 가져오기 (자식 "Image" 오브젝트) - Panel_TextMemo
        if (dateBt != null)
        {
            Transform imageTransform = dateBt.transform.Find(dateButtonImageName);
            if (imageTransform != null)
            {
                dateButtonImage = imageTransform.GetComponent<UnityEngine.UI.Image>();
                
                // Outline 컴포넌트 가져오기 (없으면 추가)
                dateButtonOutline = imageTransform.GetComponent<UnityEngine.UI.Outline>();
                if (dateButtonOutline == null)
                {
                    dateButtonOutline = imageTransform.gameObject.AddComponent<UnityEngine.UI.Outline>();
                }
                
                // Outline 초기 설정 (선택 전 색상)
                if (dateButtonOutline != null)
                {
                    dateButtonOutline.effectColor = dateButtonOutlineUnselected;
                    dateButtonOutline.effectDistance = new Vector2(4, -4);
                    Debug.Log($"[CalendarController] ▶▶▶ DateBt Outline 초기화 완료: {dateButtonOutline.gameObject.name}");
                }
            }
            else
            {
                Debug.LogWarning($"[CalendarController] ▶▶▶ DateBt에서 '{dateButtonImageName}' 자식을 찾을 수 없습니다!");
            }
        }
        
        // ImageDateBt 배경 이미지 가져오기 (자식 "Image" 오브젝트) - Panel_ImageMemo
        if (imageDateBt != null)
        {
            Transform imageTransform = imageDateBt.transform.Find(dateButtonImageName);
            if (imageTransform != null)
            {
                imageDateButtonImage = imageTransform.GetComponent<UnityEngine.UI.Image>();
                
                // Outline 컴포넌트 가져오기 (없으면 추가)
                imageDateButtonOutline = imageTransform.GetComponent<UnityEngine.UI.Outline>();
                if (imageDateButtonOutline == null)
                {
                    imageDateButtonOutline = imageTransform.gameObject.AddComponent<UnityEngine.UI.Outline>();
                }
                
                // Outline 초기 설정 (선택 전 색상)
                if (imageDateButtonOutline != null)
                {
                    imageDateButtonOutline.effectColor = dateButtonOutlineUnselected;
                    imageDateButtonOutline.effectDistance = new Vector2(4, -4);
                    Debug.Log($"[CalendarController] ▶▶▶ ImageDateBt Outline 초기화 완료: {imageDateButtonOutline.gameObject.name}");
                }
            }
            else
            {
                Debug.LogWarning($"[CalendarController] ▶▶▶ ImageDateBt에서 '{dateButtonImageName}' 자식을 찾을 수 없습니다!");
            }
            
            // ImageDateBt 텍스트 자동 검색 (없으면 찾기)
            if (imageDateButtonText == null)
            {
                imageDateButtonText = imageDateBt.GetComponentInChildren<TMP_Text>();
                if (imageDateButtonText != null)
                {
                    Debug.Log("[CalendarController] ImageDateBt 텍스트 자동 검색 성공");
                }
            }
        }
        
        // CalendarPanel이 지정되지 않았으면 GameObject에서 가져오기
        if (calendarPanelRect == null && calendarPanel != null)
        {
            calendarPanelRect = calendarPanel.GetComponent<RectTransform>();
        }
        if (imageMemoCalendarPanelRect == null && imageMemoCalendarPanel != null)
        {
            imageMemoCalendarPanelRect = imageMemoCalendarPanel.GetComponent<RectTransform>();
        }
        
        // MonthYearText 자동 검색
        if (monthYearText == null && calendarPanel != null)
        {
            // MonthYearText는 특정 위치에 있을 것으로 예상
            TMP_Text[] texts = calendarPanel.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length > 0)
            {
                // 첫 번째 TMP_Text를 MonthYearText로 사용
                monthYearText = texts[0];
            }
        }
        if (imageMemoMonthYearText == null && imageMemoCalendarPanel != null)
        {
            TMP_Text[] texts = imageMemoCalendarPanel.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length > 0)
            {
                imageMemoMonthYearText = texts[0];
            }
        }
        
        // CalendarDaysContainer 자동 검색 (이름으로 찾기)
        if (calendarDaysContainer == null && calendarPanel != null)
        {
            Transform contentTransform = calendarPanel.transform.Find("ScrollView/Viewport/Content");
            if (contentTransform != null)
            {
                calendarDaysContainer = contentTransform;
                Debug.Log("[CalendarController] Panel_TextMemo CalendarDaysContainer 자동 검색 성공");
            }
        }
        if (imageMemoCalendarDaysContainer == null && imageMemoCalendarPanel != null)
        {
            Transform contentTransform = imageMemoCalendarPanel.transform.Find("ScrollView/Viewport/Content");
            if (contentTransform != null)
            {
                imageMemoCalendarDaysContainer = contentTransform;
                Debug.Log("[CalendarController] Panel_ImageMemo CalendarDaysContainer 자동 검색 성공");
            }
        }
        
        // 버튼 자동 검색 (Panel_TextMemo)
        if (prevMonthButton == null && calendarPanel != null)
        {
            Transform prevBtn = calendarPanel.transform.Find("PrevMonthButton");
            if (prevBtn != null) prevMonthButton = prevBtn.GetComponent<Button>();
        }
        if (nextMonthButton == null && calendarPanel != null)
        {
            Transform nextBtn = calendarPanel.transform.Find("NextMonthButton");
            if (nextBtn != null) nextMonthButton = nextBtn.GetComponent<Button>();
        }
        if (closeButton == null && calendarPanel != null)
        {
            Transform closeBtn = calendarPanel.transform.Find("CloseButton");
            if (closeBtn != null) closeButton = closeBtn.GetComponent<Button>();
        }
        
        // 버튼 자동 검색 (Panel_ImageMemo)
        if (imagePrevMonthButton == null && imageMemoCalendarPanel != null)
        {
            Transform prevBtn = imageMemoCalendarPanel.transform.Find("PrevMonthButton");
            if (prevBtn != null) imagePrevMonthButton = prevBtn.GetComponent<Button>();
        }
        if (imageNextMonthButton == null && imageMemoCalendarPanel != null)
        {
            Transform nextBtn = imageMemoCalendarPanel.transform.Find("NextMonthButton");
            if (nextBtn != null) imageNextMonthButton = nextBtn.GetComponent<Button>();
        }
        if (imageCloseButton == null && imageMemoCalendarPanel != null)
        {
            Transform closeBtn = imageMemoCalendarPanel.transform.Find("CloseButton");
            if (closeBtn != null) imageCloseButton = closeBtn.GetComponent<Button>();
        }
        
        // Panel_TextMemo 원래 위치 저장
        if (calendarPanelRect != null) calendarOriginalPos = calendarPanelRect.anchoredPosition;
        
        // Panel_ImageMemo 원래 위치 저장
        if (imageMemoCalendarPanelRect != null) imageMemoCalendarOriginalPos = imageMemoCalendarPanelRect.anchoredPosition;
        if (deadlineRow != null) deadlineOriginalPos = deadlineRow.anchoredPosition;
        if (emergencyRow != null) emergencyOriginalPos = emergencyRow.anchoredPosition;
        if (assigneeRow != null) assigneeOriginalPos = assigneeRow.anchoredPosition;
        if (inputFieldBody != null) bodyOriginalPos = inputFieldBody.anchoredPosition;
        
        // Panel_TextMemo ScrollContent 원래 높이 및 모든 자식 위치 저장
        if (scrollContent != null)
        {
            scrollContentOriginalHeight = scrollContent.sizeDelta.y;
            scrollContentOriginalPos = scrollContent.anchoredPosition;
            scrollContentOriginalOffsetMax = scrollContent.offsetMax;
            scrollContentOriginalOffsetMin = scrollContent.offsetMin;
            
            // 모든 자식의 원래 위치 저장
            foreach (Transform child in scrollContent)
            {
                RectTransform childRect = child.GetComponent<RectTransform>();
                if (childRect != null)
                {
                    childOriginalPositions[child] = childRect.anchoredPosition;
                }
            }
            Debug.Log($"[CalendarController] Panel_TextMemo ScrollContent 자식 {childOriginalPositions.Count}개의 원래 위치 저장");
        }
        
        // Panel_ImageMemo ScrollContent 자동 검색 (할당되지 않은 경우)
        if (imageMemoScrollContent == null)
        {
            // ImageContent GameObject를 찾아서 그 안의 ScrollContent를 찾음
            GameObject imageContentObj = GameObject.Find("ImageContent");
            if (imageContentObj != null)
            {
                Transform scrollViewTransform = imageContentObj.transform.Find("ScrollView");
                if (scrollViewTransform != null)
                {
                    Transform viewportTransform = scrollViewTransform.Find("Viewport");
                    if (viewportTransform != null)
                    {
                        Transform contentTransform = viewportTransform.Find("Content");
                        if (contentTransform != null)
                        {
                            imageMemoScrollContent = contentTransform.GetComponent<RectTransform>();
                            Debug.Log("[CalendarController] Panel_ImageMemo ScrollContent 자동 검색 성공");
                        }
                    }
                }
            }
            
            if (imageMemoScrollContent == null)
            {
                Debug.LogWarning("[CalendarController] Panel_ImageMemo ScrollContent를 찾을 수 없습니다. Inspector에서 수동으로 할당해주세요.");
            }
        }
        
        // Panel_ImageMemo ScrollRect 자동 검색 (할당되지 않은 경우)
        if (imageMemoScrollRect == null && imageMemoScrollContent != null)
        {
            // ScrollContent의 부모의 부모가 ScrollView이고, 거기에 ScrollRect가 있음
            Transform scrollViewTransform = imageMemoScrollContent.parent?.parent;
            if (scrollViewTransform != null)
            {
                imageMemoScrollRect = scrollViewTransform.GetComponent<UnityEngine.UI.ScrollRect>();
                if (imageMemoScrollRect != null)
                {
                    Debug.Log("[CalendarController] Panel_ImageMemo ScrollRect 자동 검색 성공");
                }
            }
        }
        
        // Panel_ImageMemo 원래 위치 저장
        if (imageMemoDeadlineRow != null) imageMemoDeadlineOriginalPos = imageMemoDeadlineRow.anchoredPosition;
        if (imageMemoEmergencyRow != null) imageMemoEmergencyOriginalPos = imageMemoEmergencyRow.anchoredPosition;
        if (imageMemoAssigneeRow != null) imageMemoAssigneeOriginalPos = imageMemoAssigneeRow.anchoredPosition;
        if (imageMemoInputFieldBody != null) imageMemoBodyOriginalPos = imageMemoInputFieldBody.anchoredPosition;
        if (imageMemoImageMovie != null) imageMemoImageMovieOriginalPos = imageMemoImageMovie.anchoredPosition;
        
        // Panel_ImageMemo ScrollContent 원래 높이 및 모든 자식 위치 저장
        if (imageMemoScrollContent != null)
        {
            imageMemoScrollContentOriginalHeight = imageMemoScrollContent.sizeDelta.y;
            imageMemoScrollContentOriginalPos = imageMemoScrollContent.anchoredPosition;
            imageMemoScrollContentOriginalOffsetMax = imageMemoScrollContent.offsetMax;
            imageMemoScrollContentOriginalOffsetMin = imageMemoScrollContent.offsetMin;
            
            // 모든 자식의 원래 위치 저장
            foreach (Transform child in imageMemoScrollContent)
            {
                RectTransform childRect = child.GetComponent<RectTransform>();
                if (childRect != null)
                {
                    imageMemoChildOriginalPositions[child] = childRect.anchoredPosition;
                }
            }
            Debug.Log($"[CalendarController] Panel_ImageMemo ScrollContent 자식 {imageMemoChildOriginalPositions.Count}개의 원래 위치 저장");
        }
        
        // ScrollRect 할당 확인
        if (scrollRect == null)
        {
            Debug.LogWarning("[CalendarController] ScrollRect가 할당되지 않았습니다! Inspector에서 할당해주세요.");
        }
        else
        {
            Debug.Log($"[CalendarController] ScrollRect 할당 확인: {scrollRect.name}");
        }
        
        // 버튼 이벤트 연결
        if (dateBt != null)
        {
            dateBt.onClick.AddListener(OnDateButtonClicked);
        }
        if (imageDateBt != null)
        {
            imageDateBt.onClick.AddListener(OnDateButtonClicked);
            Debug.Log("[CalendarController] ImageDateBt onClick 리스너 연결 완료");
        }
        else
        {
            Debug.LogError("[CalendarController] ImageDateBt가 할당되지 않았습니다! Inspector에서 할당해주세요.");
        }
        
        // ★★★ Checklist 날짜 버튼 리스너 등록 ★★★
        if (checklistDateBt != null)
        {
            checklistDateBt.onClick.AddListener(OnDateButtonClicked);
            Debug.Log("[CalendarController] ChecklistDateBt onClick 리스너 연결 완료");
        }
        
        // ★★★ VoiceMemo 날짜 버튼 리스너 등록 ★★★
        if (voiceMemoDateBt != null)
        {
            voiceMemoDateBt.onClick.AddListener(OnDateButtonClicked);
            Debug.Log("[CalendarController] VoiceMemoDateBt onClick 리스너 연결 완료");
        }
        
        // Panel_TextMemo 버튼들
        if (prevMonthButton != null)
        {
            prevMonthButton.onClick.AddListener(OnPrevMonthClicked);
        }
        if (nextMonthButton != null)
        {
            nextMonthButton.onClick.AddListener(OnNextMonthClicked);
        }
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseButtonClicked);
        }
        
        // Panel_ImageMemo 버튼들
        if (imagePrevMonthButton != null)
        {
            imagePrevMonthButton.onClick.AddListener(OnPrevMonthClicked);
        }
        if (imageNextMonthButton != null)
        {
            imageNextMonthButton.onClick.AddListener(OnNextMonthClicked);
        }
        if (imageCloseButton != null)
        {
            imageCloseButton.onClick.AddListener(OnCloseButtonClicked);
        }
        
        // ★★★ Panel_Checklist 버튼들 ★★★
        if (checklistPrevMonthButton != null)
        {
            checklistPrevMonthButton.onClick.AddListener(OnPrevMonthClicked);
        }
        if (checklistNextMonthButton != null)
        {
            checklistNextMonthButton.onClick.AddListener(OnNextMonthClicked);
        }
        if (checklistCloseButton != null)
        {
            checklistCloseButton.onClick.AddListener(OnCloseButtonClicked);
        }
        
        // ★★★ Panel_VoiceMemo 버튼들 ★★★
        if (voiceMemoPrevMonthButton != null)
        {
            voiceMemoPrevMonthButton.onClick.AddListener(OnPrevMonthClicked);
        }
        if (voiceMemoNextMonthButton != null)
        {
            voiceMemoNextMonthButton.onClick.AddListener(OnNextMonthClicked);
        }
        if (voiceMemoCloseButton != null)
        {
            voiceMemoCloseButton.onClick.AddListener(OnCloseButtonClicked);
        }
        
        // 달력 패널 초기 비활성화
        if (calendarPanel != null)
        {
            calendarPanel.SetActive(false);
        }
        if (imageMemoCalendarPanel != null)
        {
            imageMemoCalendarPanel.SetActive(false);
        }
        if (checklistCalendarPanel != null)
        {
            checklistCalendarPanel.SetActive(false);
        }
        if (voiceMemoCalendarPanel != null)
        {
            voiceMemoCalendarPanel.SetActive(false);
        }
        
        // ★★★ ChecklistDateBt 배경 이미지 및 텍스트 가져오기 ★★★
        if (checklistDateBt != null)
        {
            // 텍스트 자동 할당 (Inspector에서 할당되지 않은 경우)
            if (checklistDateButtonText == null)
            {
                checklistDateButtonText = checklistDateBt.GetComponentInChildren<TMP_Text>();
                if (checklistDateButtonText != null)
                {
                    Debug.Log($"[CalendarController] ChecklistDateBt 텍스트 자동 할당 완료: {checklistDateButtonText.gameObject.name}");
                }
            }
            
            Transform imageTransform = checklistDateBt.transform.Find(dateButtonImageName);
            if (imageTransform != null)
            {
                checklistDateButtonImage = imageTransform.GetComponent<UnityEngine.UI.Image>();
                checklistDateButtonOutline = imageTransform.GetComponent<UnityEngine.UI.Outline>();
                if (checklistDateButtonOutline == null)
                {
                    checklistDateButtonOutline = imageTransform.gameObject.AddComponent<UnityEngine.UI.Outline>();
                    Debug.Log("[CalendarController] ▶▶▶ ChecklistDateBt에 Outline 컴포넌트 추가됨");
                }
                if (checklistDateButtonOutline != null)
                {
                    checklistDateButtonOutline.effectColor = dateButtonOutlineUnselected;
                    checklistDateButtonOutline.effectDistance = new Vector2(4, -4);
                    Debug.Log($"[CalendarController] ▶▶▶ ChecklistDateBt Outline 초기화 완료: {checklistDateButtonOutline.gameObject.name}");
                }
            }
            else
            {
                Debug.LogWarning($"[CalendarController] ▶▶▶ ChecklistDateBt에서 '{dateButtonImageName}' 자식을 찾을 수 없습니다!");
            }
        }
        else
        {
            Debug.LogWarning("[CalendarController] ▶▶▶ checklistDateBt가 NULL입니다! Inspector에서 할당 필요");
        }
        
        // ★★★ VoiceMemoDateBt 배경 이미지 및 텍스트 가져오기 ★★★
        if (voiceMemoDateBt != null)
        {
            // 텍스트 자동 할당 (Inspector에서 할당되지 않은 경우)
            if (voiceMemoDateButtonText == null)
            {
                voiceMemoDateButtonText = voiceMemoDateBt.GetComponentInChildren<TMP_Text>();
                if (voiceMemoDateButtonText != null)
                {
                    Debug.Log($"[CalendarController] VoiceMemoDateBt 텍스트 자동 할당 완료: {voiceMemoDateButtonText.gameObject.name}");
                }
            }
            
            Transform imageTransform = voiceMemoDateBt.transform.Find(dateButtonImageName);
            if (imageTransform != null)
            {
                voiceMemoDateButtonImage = imageTransform.GetComponent<UnityEngine.UI.Image>();
                voiceMemoDateButtonOutline = imageTransform.GetComponent<UnityEngine.UI.Outline>();
                if (voiceMemoDateButtonOutline == null)
                {
                    voiceMemoDateButtonOutline = imageTransform.gameObject.AddComponent<UnityEngine.UI.Outline>();
                    Debug.Log("[CalendarController] ▶▶▶ VoiceMemoDateBt에 Outline 컴포넌트 추가됨");
                }
                if (voiceMemoDateButtonOutline != null)
                {
                    voiceMemoDateButtonOutline.effectColor = dateButtonOutlineUnselected;
                    voiceMemoDateButtonOutline.effectDistance = new Vector2(4, -4);
                    Debug.Log($"[CalendarController] ▶▶▶ VoiceMemoDateBt Outline 초기화 완료: {voiceMemoDateButtonOutline.gameObject.name}");
                }
            }
            else
            {
                Debug.LogWarning($"[CalendarController] ▶▶▶ VoiceMemoDateBt에서 '{dateButtonImageName}' 자식을 찾을 수 없습니다!");
            }
        }
        else
        {
            Debug.LogWarning("[CalendarController] ▶▶▶ voiceMemoDateBt가 NULL입니다! Inspector에서 할당 필요");
        }
        
        // ★★★ Panel_Checklist 원래 위치 저장 ★★★
        if (checklistCalendarPanelRect != null) checklistCalendarOriginalPos = checklistCalendarPanelRect.anchoredPosition;
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
            Debug.Log($"[CalendarController] Panel_Checklist ScrollContent 자식 {checklistChildOriginalPositions.Count}개의 원래 위치 저장");
        }
        
        // ★★★ Panel_VoiceMemo 원래 위치 저장 ★★★
        if (voiceMemoCalendarPanelRect != null) voiceMemoCalendarOriginalPos = voiceMemoCalendarPanelRect.anchoredPosition;
        if (voiceMemoDeadlineRow != null) voiceMemoDeadlineOriginalPos = voiceMemoDeadlineRow.anchoredPosition;
        if (voiceMemoEmergencyRow != null) voiceMemoEmergencyOriginalPos = voiceMemoEmergencyRow.anchoredPosition;
        if (voiceMemoVoiceMemoRow != null) voiceMemoVoiceMemoOriginalPos = voiceMemoVoiceMemoRow.anchoredPosition;
        
        // VoiceMemo Voice 요소들의 원래 위치 저장
        if (voiceMemoVoiceElements != null && voiceMemoVoiceElements.Length > 0)
        {
            Debug.Log($"[CalendarController] VoiceMemo Voice 요소 개수: {voiceMemoVoiceElements.Length}");
            foreach (var voiceElement in voiceMemoVoiceElements)
            {
                if (voiceElement != null && voiceElement.element != null)
                {
                    voiceMemoVoiceElementsOriginalPos[voiceElement.element] = voiceElement.element.anchoredPosition;
                    Debug.Log($"[CalendarController] VoiceMemo Voice 요소 원래 위치 저장: {voiceElement.element.name} = {voiceElement.element.anchoredPosition}");
                }
            }
        }
        else
        {
            Debug.LogWarning("[CalendarController] voiceMemoVoiceElements가 비어있거나 NULL입니다! Inspector에서 Voice 요소들을 할당해주세요.");
        }
        
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
            Debug.Log($"[CalendarController] Panel_VoiceMemo ScrollContent 자식 {voiceMemoChildOriginalPositions.Count}개의 원래 위치 저장");
        }
        
        // 날짜 버튼 텍스트 초기화
        UpdateDateButtonText();
        
        // Inspector 참조 상태 확인
        if (timePickerController == null)
        {
            Debug.LogWarning("[CalendarController] TimePickerController 참조가 할당되지 않았습니다! ImageMemo 패널 전환이 작동하지 않을 수 있습니다.");
        }
        else
        {
            Debug.Log("[CalendarController] TimePickerController 참조 정상 할당됨");
        }
    }
    
    // ========== 패널 감지 헬퍼 메서드 ==========
    
    /// <summary>
    /// Panel_ImageMemo가 현재 활성화되어 있는지 확인
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
    /// 현재 활성화된 패널의 CalendarPanel 가져오기
    /// </summary>
    private GameObject GetActiveCalendarPanel()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoCalendarPanel;
        if (IsChecklistPanelActive()) return checklistCalendarPanel;
        if (IsImageMemoPanelActive()) return imageMemoCalendarPanel;
        return calendarPanel;
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
    /// 현재 활성화된 패널의 MonthYearText 가져오기
    /// </summary>
    private TMP_Text GetActiveMonthYearText()
    {
        if (IsChecklistPanelActive()) return checklistMonthYearText;
        if (IsImageMemoPanelActive()) return imageMemoMonthYearText;
        return monthYearText;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 원래 CalendarPanel 위치 가져오기
    /// </summary>
    private Vector2 GetActiveCalendarOriginalPos()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoCalendarOriginalPos;
        if (IsChecklistPanelActive()) return checklistCalendarOriginalPos;
        if (IsImageMemoPanelActive()) return imageMemoCalendarOriginalPos;
        return calendarOriginalPos;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 CalendarDaysContainer 가져오기
    /// </summary>
    private Transform GetActiveCalendarDaysContainer()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoCalendarDaysContainer;
        if (IsChecklistPanelActive()) return checklistCalendarDaysContainer;
        if (IsImageMemoPanelActive()) return imageMemoCalendarDaysContainer;
        return calendarDaysContainer;
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
        if (IsVoiceMemoPanelActive()) return null; // VoiceMemo에는 AssigneeRow 없음
        if (IsChecklistPanelActive()) return null; // Checklist에는 AssigneeRow 없음
        if (IsImageMemoPanelActive()) return imageMemoAssigneeRow;
        return assigneeRow;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 InputFieldBody 가져오기
    /// </summary>
    private RectTransform GetActiveInputFieldBody()
    {
        if (IsVoiceMemoPanelActive()) return null; // VoiceMemo에는 InputFieldBody 없음
        if (IsChecklistPanelActive()) return null; // Checklist에는 InputFieldBody 없음
        if (IsImageMemoPanelActive()) return imageMemoInputFieldBody;
        return inputFieldBody;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 ImageMovie 가져오기 (ImageMemo 전용)
    /// </summary>
    private RectTransform GetActiveImageMovie()
    {
        if (IsImageMemoPanelActive()) return imageMemoImageMovie;
        return null;
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
            imageMovieOrig = Vector2.zero; // TextMemo에는 ImageMovie 없음
            scrollHeightOrig = scrollContentOriginalHeight;
            scrollPosOrig = scrollContentOriginalPos;
            childPosOrig = childOriginalPositions;
        }
    }
    
    // 스크롤 위치 제한 및 복원 감지
    private void LateUpdate()
    {
        // ★★★ 스크롤 복원 대기 중인 경우: 사용자가 원래 위치로 스크롤했는지 확인 ★★★
        if (isWaitingForScrollRestore && scrollRectToRestore != null)
        {
            RectTransform content = scrollRectToRestore.content;
            if (content != null)
            {
                // 현재 Content 위치가 원래 위치 근처로 돌아왔는지 확인
                // 임계값을 스크롤 양의 10%로 설정 (최소 5픽셀)
                float threshold = Mathf.Max(5f, scrollDownAmountUsed * 0.1f);
                float currentY = content.anchoredPosition.y;
                float targetY = originalContentPosition.y;
                
                // 사용자가 위로 스크롤하여 원래 위치 + 임계값 이하로 돌아왔을 때 복원
                if (currentY <= targetY + threshold)
                {
                    // movementType을 원래대로 복원
                    scrollRectToRestore.movementType = originalMovementType;
                    
                    // Content 위치를 정확히 원래 위치로 설정
                    content.anchoredPosition = originalContentPosition;
                    
                    Debug.Log($"### [CalendarController] 스크롤 복원 완료 - movementType: {originalMovementType}, Content.y: {originalContentPosition.y}, threshold: {threshold}");
                    
                    // 복원 대기 상태 해제
                    isWaitingForScrollRestore = false;
                    scrollRectToRestore = null;
                }
            }
        }
        
        // ★★★ CalendarPanel 열렸을 때 스크롤 제한 ★★★
        if (!isCalendarOpen)
            return;
        
        // 현재 활성화된 패널의 ScrollRect와 ScrollBottomLimit 가져오기
        bool isImageMemoActive = IsImageMemoPanelActive();
        UnityEngine.UI.ScrollRect activeScrollRect = GetActiveScrollRect();
        float activeScrollBottomLimit = isImageMemoActive ? imageMemoScrollBottomLimit : scrollBottomLimit;
        
        // 디버그: 한 번만 로그 출력 (매 프레임마다 출력하지 않도록)
        if (Time.frameCount % 60 == 0) // 60프레임마다 한 번씩 로그
        {
            string panelName = isImageMemoActive ? "Panel_ImageMemo" : "Panel_TextMemo";
            string scrollContentStatus = imageMemoScrollContent != null 
                ? $"할당됨 (active: {imageMemoScrollContent.gameObject.activeInHierarchy})" 
                : "null";
            string scrollRectStatus = activeScrollRect != null 
                ? $"할당됨 (position: {activeScrollRect.verticalNormalizedPosition:F2})" 
                : "null";
            
            Debug.Log($"[CalendarController LateUpdate] 활성 패널: {panelName}\n" +
                      $"  - ScrollBottomLimit 사용 중: {activeScrollBottomLimit}\n" +
                      $"  - imageMemoScrollContent: {scrollContentStatus}\n" +
                      $"  - activeScrollRect: {scrollRectStatus}\n" +
                      $"  - TextMemo Limit: {scrollBottomLimit}, ImageMemo Limit: {imageMemoScrollBottomLimit}");
        }
        
        // CalendarPanel이 열려있고, scrollBottomLimit가 설정되어 있을 때만 적용
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
    
    // DateBt 클릭 시 달력 열기/닫기
    private void OnDateButtonClicked()
    {
        string panelName = IsChecklistPanelActive() ? "Panel_Checklist" : (IsImageMemoPanelActive() ? "Panel_ImageMemo" : "Panel_TextMemo");
        Debug.Log($"[CalendarController] DateBt 클릭됨 ({panelName}). 현재 Calendar 상태: {isCalendarOpen}");
        
        // 다른 패널(TimePicker)이 열려있으면 먼저 닫기 (스크롤 위치 복원 없이)
        if (timePickerController != null && timePickerController.IsTimePickerOpen())
        {
            Debug.Log($"[CalendarController] {panelName}: TimePicker가 열려있음 → TimePicker 닫고 Calendar 열기 (스크롤 위치 전환)");
            timePickerController.CloseTimePicker(restoreScrollPosition: false);
            isCalendarOpen = true; // 달력을 열 예정
            OpenCalendar(fromTimePicker: true);
            return;
        }
        
        if (timePickerController == null)
        {
            Debug.LogWarning($"[CalendarController] {panelName}: TimePickerController 참조가 null입니다! Inspector에서 할당해주세요.");
        }
        
        isCalendarOpen = !isCalendarOpen;
        
        if (isCalendarOpen)
        {
            Debug.Log($"[CalendarController] {panelName}: Calendar 열기");
            OpenCalendar();
        }
        else
        {
            Debug.Log($"[CalendarController] {panelName}: Calendar 닫기");
            CloseCalendarInternal();
        }
    }
    
    // CloseButton 클릭 시 달력 닫기
    private void OnCloseButtonClicked()
    {
        if (isCalendarOpen)
        {
            isCalendarOpen = false;
            CloseCalendarInternal();
        }
    }
    
    // 달력 열기
    private void OpenCalendar(bool fromTimePicker = false)
    {
        // ★★★ 스크롤 복원 대기 상태 초기화 (이전 상태 정리) ★★★
        if (isWaitingForScrollRestore && scrollRectToRestore != null)
        {
            // 원래 movementType으로 복원
            scrollRectToRestore.movementType = originalMovementType;
            Debug.Log($"### [CalendarController] OpenCalendar - 이전 스크롤 복원 상태 정리");
        }
        isWaitingForScrollRestore = false;
        scrollRectToRestore = null;
        
        // 현재 활성화된 패널의 요소들 가져오기
        RectTransform activeScrollContent = GetActiveScrollContent();
        RectTransform activeDeadlineRow = GetActiveDeadlineRow();
        RectTransform activeEmergencyRow = GetActiveEmergencyRow();
        RectTransform activeAssigneeRow = GetActiveAssigneeRow();
        RectTransform activeInputFieldBody = GetActiveInputFieldBody();
        RectTransform activeImageMovie = GetActiveImageMovie();
        GetActiveOriginalPositions(out Vector2 deadlineOrig, out Vector2 emergencyOrig, out Vector2 assigneeOrig, 
                                   out Vector2 bodyOrig, out Vector2 imageMovieOrig, out float scrollHeightOrig, out Vector2 scrollPosOrig, 
                                   out Dictionary<Transform, Vector2> childPosOrig);
        
        string panelName = IsVoiceMemoPanelActive() ? "Panel_VoiceMemo" : (IsChecklistPanelActive() ? "Panel_Checklist" : (IsImageMemoPanelActive() ? "Panel_ImageMemo" : "Panel_TextMemo"));
        Debug.Log($"[CalendarController] OpenCalendar - 현재 활성 패널: {panelName}");
        
        // ★★★ Checklist/VoiceMemo 패널일 때는 ScrollContent 조작 건너뜀 ★★★
        bool isChecklistActive = IsChecklistPanelActive();
        bool isVoiceMemoActive = IsVoiceMemoPanelActive();
        
        // ★★★ ScrollContent와 자식들은 항상 리셋 (위치 누적 방지) - Checklist/VoiceMemo 제외 ★★★
        if (!isChecklistActive && !isVoiceMemoActive && activeScrollContent != null)
        {
            // ScrollContent 높이 및 위치 복원
            activeScrollContent.sizeDelta = new Vector2(activeScrollContent.sizeDelta.x, scrollHeightOrig);
            activeScrollContent.anchoredPosition = scrollPosOrig; // 스크롤 위치 복원
            
            // 모든 자식을 원래 위치로 복원 (fromTimePicker 여부와 관계없이 항상 리셋)
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
            Debug.Log($"[CalendarController] {panelName} ScrollContent 리셋 완료 (fromTimePicker: {fromTimePicker})");
        }
        
        // ★★★ 특정 요소들도 원래 위치로 리셋 (ScrollContent 확장 전에) - Checklist/VoiceMemo 제외 ★★★
        if (!isChecklistActive && !isVoiceMemoActive)
        {
            if (activeDeadlineRow != null) activeDeadlineRow.anchoredPosition = deadlineOrig;
            if (activeEmergencyRow != null) activeEmergencyRow.anchoredPosition = emergencyOrig;
            if (activeAssigneeRow != null) activeAssigneeRow.anchoredPosition = assigneeOrig;
            if (activeInputFieldBody != null) activeInputFieldBody.anchoredPosition = bodyOrig;
            if (activeImageMovie != null) activeImageMovie.anchoredPosition = imageMovieOrig;
            Debug.Log($"[CalendarController] {panelName} 특정 요소들 원래 위치로 리셋");
        }
        
        // 달력 패널 활성화 (현재 활성화된 패널에 맞는 CalendarPanel)
        GameObject activeCalendarPanel = GetActiveCalendarPanel();
        RectTransform activeCalendarPanelRect = GetActiveCalendarPanelRect();
        Vector2 activeCalendarOriginalPos = GetActiveCalendarOriginalPos();
        
        Debug.Log($"[CalendarController] ★★★ activeCalendarPanel: {(activeCalendarPanel != null ? activeCalendarPanel.name : "NULL")}, isVoiceMemoActive: {isVoiceMemoActive}, isChecklistActive: {isChecklistActive}");
        
        if (activeCalendarPanel != null)
        {
            activeCalendarPanel.SetActive(true);
            Debug.Log($"[CalendarController] CalendarPanel 활성화됨: {activeCalendarPanel.name}");
            
            // ★★★ Checklist/VoiceMemo 패널일 때는 CalendarPanel 위치 건드리지 않음 ★★★
            if (!isChecklistActive && !isVoiceMemoActive && activeCalendarPanelRect != null)
            {
                activeCalendarPanelRect.anchoredPosition = activeCalendarOriginalPos;
                Debug.Log($"[CalendarController] {panelName} CalendarPanel 위치 리셋: {activeCalendarOriginalPos}");
            }
        }
        UpdateCalendar();
        
        // DateBt 배경색 변경 (Panel_TextMemo)
        if (dateButtonImage != null && !isChecklistActive)
        {
            StartCoroutine(AnimateButtonColor(dateButtonImage, dateButtonOpenColor));
        }
        
        // ImageDateBt 배경색 변경 (Panel_ImageMemo)
        if (imageDateButtonImage != null && !isChecklistActive)
        {
            StartCoroutine(AnimateButtonColor(imageDateButtonImage, dateButtonOpenColor));
        }
        
        // ChecklistDateBt 배경색 변경 (Panel_Checklist)
        if (checklistDateButtonImage != null && isChecklistActive)
        {
            StartCoroutine(AnimateButtonColor(checklistDateButtonImage, dateButtonOpenColor));
        }
        
        // VoiceMemoDateBt 배경색 변경 (Panel_VoiceMemo)
        if (voiceMemoDateButtonImage != null && isVoiceMemoActive)
        {
            StartCoroutine(AnimateButtonColor(voiceMemoDateButtonImage, dateButtonOpenColor));
        }
        
        // ★★★ VoiceMemo 패널일 때는 CalendarPanel 활성화 + ScrollContent 확장 + Emergency만 이동 ★★★
        if (isVoiceMemoActive)
        {
            // CalendarPanel 높이 가져오기
            float voiceMemoCalHeight = 0f;
            if (voiceMemoCalendarPanelRect != null)
            {
                voiceMemoCalHeight = voiceMemoCalendarPanelRect.sizeDelta.y;
            }
            
            // 음성메모 아이템으로 인한 밀림량 가져오기
            float pushAmount = 0f;
            if (voiceMemoUIController != null)
            {
                pushAmount = voiceMemoUIController.LastPushAmount;
            }
            
            // RefreshVoiceMemoLayout에서 모든 위치와 크기를 설정
            RefreshVoiceMemoLayout(pushAmount);
            Debug.Log($"[CalendarController] OpenCalendar에서 RefreshVoiceMemoLayout 호출 - pushAmount: {pushAmount}");
            
            // 자동 스크롤 (Open)
            if (voiceMemoScrollRect != null)
            {
                float baseScrollAmount = fromTimePicker ? voiceMemoAutoScrollOnOpenFromTimePicker : voiceMemoAutoScrollOnOpen;
                float scrollAmount = baseScrollAmount + pushAmount;
                Debug.Log($"[CalendarController] VoiceMemo 자동 스크롤 계산 - base: {baseScrollAmount}, push: {pushAmount}, total: {scrollAmount}");
                if (scrollAmount != 0)
                {
                    StartCoroutine(AnimateChecklistAutoScroll(voiceMemoScrollRect, scrollAmount));
                    Debug.Log($"[CalendarController] VoiceMemo 자동 스크롤 실행: {scrollAmount}px");
                }
            }
            
            Debug.Log($"[CalendarController] VoiceMemo 패널 - CalendarPanel 활성화 완료");
            return;
        }
        
        // ★★★ Checklist 패널일 때는 CalendarPanel 활성화 + ScrollContent 확장 + Emergency만 이동 ★★★
        if (isChecklistActive)
        {
            // CalendarPanel 높이 가져오기
            float checklistCalHeight = 0f;
            if (checklistCalendarPanelRect != null)
            {
                checklistCalHeight = checklistCalendarPanelRect.sizeDelta.y;
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
            Debug.Log($"[CalendarController] OpenCalendar에서 RefreshChecklistLayout 호출 - pushAmount: {pushAmount}, itemScrollExp: {checklistItemScrollExpansion}");
            
            // ★★★ 자동 스크롤 (Open) - 밀림량만큼 추가 스크롤 ★★★
            if (checklistScrollRect != null)
            {
                float baseScrollAmount = fromTimePicker ? checklistAutoScrollOnOpenFromTimePicker : checklistAutoScrollOnOpen;
                // 동적 스크롤량: 기본 스크롤량 + 밀림량
                float scrollAmount = baseScrollAmount + pushAmount;
                Debug.Log($"[CalendarController] 자동 스크롤 계산 - base: {baseScrollAmount}, push: {pushAmount}, total: {scrollAmount}");
                if (scrollAmount != 0)
                {
                    StartCoroutine(AnimateChecklistAutoScroll(checklistScrollRect, scrollAmount));
                    Debug.Log($"[CalendarController] Checklist 자동 스크롤 실행: {scrollAmount}px");
                }
            }
            else
            {
                Debug.LogWarning($"[CalendarController] 자동 스크롤 실패 - ScrollRect null");
            }
            
            Debug.Log($"[CalendarController] Checklist 패널 - CalendarPanel 활성화 완료");
            return;
        }
        
        // CalendarPanel의 높이 가져오기 (이미 위에서 가져온 activeCalendarPanelRect 사용)
        float calendarHeight = 0f;
        
        if (activeCalendarPanelRect != null)
        {
            calendarHeight = activeCalendarPanelRect.sizeDelta.y;
            Debug.Log($"[CalendarController] {panelName} CalendarPanel 높이: {calendarHeight}");
        }
        
        // 기본 이동 거리 = CalendarPanel 높이 (요소들 간 원래 간격 유지)
        float baseMoveDistance = calendarHeight;
        
        // ★★★ ScrollContent 확장 및 모든 자식 아래로 밀기 ★★★
        // 이렇게 해야 위로 이동한 요소들이 Content 영역 안에 유지됨
        float upwardExpansion = calendarHeight; // 블록 밖에서도 사용
        if (activeScrollContent != null)
        {
            float downwardExpansion = calendarHeight + extraScrollPadding;
            float totalExpansion = upwardExpansion + downwardExpansion;
            
            // 1. Content 높이 증가
            Vector2 newSize = activeScrollContent.sizeDelta;
            newSize.y = scrollHeightOrig + totalExpansion;
            activeScrollContent.sizeDelta = newSize;
            
            // 2. Content 내의 모든 자식 요소들을 아래로 밀기 (fromTimePicker 여부와 관계없이 동일하게 처리)
            foreach (Transform child in activeScrollContent)
            {
                RectTransform childRect = child.GetComponent<RectTransform>();
                if (childRect != null)
                {
                    Vector2 pos = childRect.anchoredPosition;
                    pos.y -= upwardExpansion; // 아래로 밀기
                    childRect.anchoredPosition = pos;
                }
            }
            
            Debug.Log($"[CalendarController] {panelName} ScrollContent 확장: 높이 {scrollHeightOrig} -> {newSize.y}, 자식 이동 (fromTimePicker: {fromTimePicker})");
        }
        
        // ★★★ ScrollContent 외부 요소들만 별도로 밀기 ★★★
        // ScrollContent 자손인 요소들은 이미 직접 자식으로 밀렸거나 부모와 함께 이동했으므로 건드리지 않음
        Transform scrollContentTransform = activeScrollContent?.transform;
        
        if (activeDeadlineRow != null && !IsDescendantOf(activeDeadlineRow.transform, scrollContentTransform))
        {
            Vector2 pos = activeDeadlineRow.anchoredPosition;
            pos.y -= upwardExpansion;
            activeDeadlineRow.anchoredPosition = pos;
            Debug.Log($"[CalendarController] {panelName} Deadline 별도로 밀기 (외부): {pos.y}");
        }
        if (activeEmergencyRow != null && !IsDescendantOf(activeEmergencyRow.transform, scrollContentTransform))
        {
            Vector2 pos = activeEmergencyRow.anchoredPosition;
            pos.y -= upwardExpansion;
            activeEmergencyRow.anchoredPosition = pos;
            Debug.Log($"[CalendarController] {panelName} Emergency 별도로 밀기 (외부): {pos.y}");
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
        
        // 현재 활성화된 패널에 따라 적절한 오프셋 값 선택
        bool isImageMemo = IsImageMemoPanelActive();
        bool useManualPos = isImageMemo ? imageMemoUseManualDeadlinePosition : useManualDeadlinePosition;
        float targetY = isImageMemo ? imageMemoDeadlineTargetY : deadlineTargetY;
        float deadlineOff = isImageMemo ? imageMemoDeadlineOffset : deadlineOffset;
        float emergencyOff = isImageMemo ? imageMemoEmergencyOffset : emergencyOffset;
        float assigneeOff = isImageMemo ? imageMemoAssigneeOffset : assigneeOffset;
        float bodyOff = isImageMemo ? imageMemoBodyOffset : bodyOffset;
        
        // Deadline: 수동 위치 설정 또는 자동 계산 + deadlineOffset 적용
        float deadlineMoveAmount = 0f; // CalendarPanel 이동용
        if (activeDeadlineRow != null)
        {
            Vector2 currentPos = activeDeadlineRow.anchoredPosition;
            Vector2 targetPos;
            if (useManualPos)
            {
                targetPos = new Vector2(currentPos.x, targetY - calendarHeight); // 보정
                deadlineMoveAmount = targetPos.y - currentPos.y; // Deadline이 이동한 거리 저장
                Debug.Log($"[CalendarController] {panelName} Deadline 이동 (수동): {currentPos.y} -> {targetPos.y}");
            }
            else
            {
                // 기본 이동 거리 + deadlineOffset 적용 (밀린 후 위치에서 계산)
                float moveDistance = baseMoveDistance + deadlineOff;
                targetPos = currentPos + Vector2.up * moveDistance;
                deadlineMoveAmount = moveDistance;
                Debug.Log($"[CalendarController] {panelName} Deadline 이동: {currentPos.y} -> {targetPos.y}");
            }
            StartCoroutine(AnimatePosition(activeDeadlineRow, currentPos, targetPos));
        }
        
        // CalendarPanel도 Deadline과 같은 거리만큼 위로 이동 (Deadline 위에 유지)
        if (activeCalendarPanelRect != null && deadlineMoveAmount != 0)
        {
            Vector2 currentPos = activeCalendarPanelRect.anchoredPosition;
            Vector2 targetPos = currentPos + Vector2.up * deadlineMoveAmount;
            StartCoroutine(AnimatePosition(activeCalendarPanelRect, currentPos, targetPos));
            Debug.Log($"[CalendarController] {panelName} CalendarPanel 이동: {currentPos.y} -> {targetPos.y}");
        }
        
        // Emergency: 아래로 이동 + emergencyOffset (밀린 후 위치에서 계산)
        if (activeEmergencyRow != null)
        {
            Vector2 currentPos = activeEmergencyRow.anchoredPosition;
            float moveDistance = baseMoveDistance + emergencyOff;
            Vector2 targetPos = currentPos + Vector2.down * moveDistance;
            StartCoroutine(AnimatePosition(activeEmergencyRow, currentPos, targetPos));
            Debug.Log($"[CalendarController] {panelName} Emergency 이동: {currentPos.y} -> {targetPos.y}");
        }
        
        // AssigneeRow: 기본 이동 + assigneeOffset (밀린 후 위치에서 계산)
        if (activeAssigneeRow != null)
        {
            Vector2 currentPos = activeAssigneeRow.anchoredPosition;
            float moveDistance = baseMoveDistance + assigneeOff;
            Vector2 targetPos = currentPos + Vector2.up * moveDistance;
            StartCoroutine(AnimatePosition(activeAssigneeRow, currentPos, targetPos));
            Debug.Log($"[CalendarController] {panelName} AssigneeRow 이동: {currentPos.y} -> {targetPos.y}");
        }
        
        // InputField_Body: 기본 이동 + bodyOffset (밀린 후 위치에서 계산)
        if (activeInputFieldBody != null)
        {
            Vector2 currentPos = activeInputFieldBody.anchoredPosition;
            float moveDistance = baseMoveDistance + bodyOff;
            Vector2 targetPos = currentPos + Vector2.up * moveDistance;
            StartCoroutine(AnimatePosition(activeInputFieldBody, currentPos, targetPos));
            Debug.Log($"[CalendarController] {panelName} InputField_Body 이동: {currentPos.y} -> {targetPos.y}");
        }
        
        // ImageMovie: 기본 이동 + imageMemoImageMovieOffset (ImageMemo 전용, 밀린 후 위치에서 계산)
        if (activeImageMovie != null)
        {
            Vector2 currentPos = activeImageMovie.anchoredPosition;
            float moveDistance = baseMoveDistance + imageMemoImageMovieOffset;
            Vector2 targetPos = currentPos + Vector2.up * moveDistance;
            StartCoroutine(AnimatePosition(activeImageMovie, currentPos, targetPos));
            Debug.Log($"[CalendarController] {panelName} ImageMovie 이동: {currentPos.y} -> {targetPos.y}");
        }
        
        // ★★★ 자동 스크롤 (DateBt 열릴 때 화면을 위로 스크롤) ★★★
        if (activeScrollContent != null)
        {
            // 현재 활성화된 패널에 따라 적절한 스크롤 값 선택 (isImageMemo는 이미 위에서 선언됨)
            float scrollAmount;
            
            if (fromTimePicker)
            {
                scrollAmount = isImageMemo ? imageMemoAutoScrollOnOpenFromTimePicker : autoScrollOnOpenFromTimePicker;
                Debug.Log($"[CalendarController] {panelName} 스크롤 값 선택 (TimeBt→DateBt): isImageMemo={isImageMemo}, imageMemoValue={imageMemoAutoScrollOnOpenFromTimePicker}, textMemoValue={autoScrollOnOpenFromTimePicker}, selected={scrollAmount}");
            }
            else
            {
                scrollAmount = isImageMemo ? imageMemoAutoScrollOnOpen : autoScrollOnOpen;
                Debug.Log($"[CalendarController] {panelName} 스크롤 값 선택 (DateBt 직접): isImageMemo={isImageMemo}, imageMemoValue={imageMemoAutoScrollOnOpen}, textMemoValue={autoScrollOnOpen}, selected={scrollAmount}");
            }
            
            if (scrollAmount > 0f)
            {
                Debug.Log($"[CalendarController] {panelName} 자동 스크롤 실행: {scrollAmount} (fromTimePicker: {fromTimePicker})");
                StartCoroutine(AnimateAutoScroll(activeScrollContent, scrollAmount));
            }
            else
            {
                Debug.Log($"[CalendarController] {panelName} 자동 스크롤 스킵: scrollAmount={scrollAmount} (fromTimePicker: {fromTimePicker})");
            }
        }
    }
    
    // 자동 스크롤 애니메이션 (열릴 때)
    private System.Collections.IEnumerator AnimateAutoScroll(RectTransform targetScrollContent, float scrollAmount)
    {
        if (targetScrollContent == null) yield break;
        
        Vector2 startPos = targetScrollContent.anchoredPosition;
        Vector2 targetPos = startPos + new Vector2(0, scrollAmount); // 양수 = 위로 스크롤
        
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            // Ease out
            t = 1f - Mathf.Pow(1f - t, 3f);
            targetScrollContent.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }
        
        targetScrollContent.anchoredPosition = targetPos;
        Debug.Log($"[CalendarController] 자동 스크롤 완료: {startPos.y} -> {targetPos.y}");
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
            Debug.Log($"[CalendarController] Checklist 스크롤 불가 - contentHeight: {contentHeight}, viewportHeight: {viewportHeight}");
            yield break;
        }
        
        // scrollAmount를 normalizedPosition 변화량으로 변환 (양수 = 위로 = normalized 증가)
        float normalizedChange = scrollAmount / scrollableHeight;
        
        float startNormalized = scrollRect.verticalNormalizedPosition;
        float targetNormalized = Mathf.Clamp01(startNormalized + normalizedChange);
        
        Debug.Log($"[CalendarController] Checklist 자동 스크롤 시작 - start: {startNormalized}, target: {targetNormalized}, scrollAmount: {scrollAmount}, scrollableHeight: {scrollableHeight}");
        
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
        Debug.Log($"[CalendarController] Checklist 자동 스크롤 완료: {startNormalized} -> {targetNormalized}");
    }
    
    // 자동 스크롤 애니메이션 (닫힐 때) - verticalNormalizedPosition 사용 (Content 높이 유지됨)
    private System.Collections.IEnumerator AnimateScrollRectOnClose(UnityEngine.UI.ScrollRect scrollRect, float scrollAmount)
    {
        Debug.Log($"### [CalendarController] AnimateScrollRectOnClose 시작 - scrollRect: {(scrollRect != null ? "OK" : "NULL")}, content: {(scrollRect?.content != null ? "OK" : "NULL")}, scrollAmount: {scrollAmount}");
        
        if (scrollRect == null || scrollRect.content == null)
        {
            Debug.LogError("### [CalendarController] AnimateScrollRectOnClose - scrollRect 또는 content가 null입니다!");
            yield break;
        }
        
        // 한 프레임 대기 (요소들이 복원된 후)
        yield return null;
        
        Debug.Log("### [CalendarController] 1프레임 대기 완료, 스크롤 계산 시작");
        
        // Content 높이가 유지되어 있으므로 스크롤 가능한 높이 계산
        RectTransform viewportRect = scrollRect.viewport;
        if (viewportRect == null)
        {
            viewportRect = scrollRect.gameObject.GetComponent<RectTransform>();
            Debug.Log("### [CalendarController] viewport가 null이므로 ScrollRect GameObject의 RectTransform 사용");
        }
        
        if (viewportRect == null)
        {
            Debug.LogError("### [CalendarController] ERROR: viewport를 찾을 수 없습니다!");
            yield break;
        }
        
        float contentHeight = scrollRect.content.rect.height;
        float viewportHeight = viewportRect.rect.height;
        float scrollableHeight = Mathf.Max(0, contentHeight - viewportHeight);
        
        Debug.Log($"### [CalendarController] 높이 계산 - content: {contentHeight:F1}, viewport: {viewportHeight:F1}, scrollable: {scrollableHeight:F1}");
        
        if (scrollableHeight <= 0)
        {
            Debug.Log("### [CalendarController] 스크롤 불가능 - scrollableHeight가 0 이하입니다");
            yield break;
        }
        
        // scrollAmount를 normalizedPosition으로 변환
        float normalizedScroll = scrollAmount / scrollableHeight;
        
        // verticalNormalizedPosition: 1 = 맨 위, 0 = 맨 아래
        float startPos = 1f;
        float targetPos = Mathf.Clamp01(1f - normalizedScroll);
        
        Debug.Log($"### [CalendarController] 스크롤 시작: {startPos:F3} -> {targetPos:F3} (normalized: {normalizedScroll:F3})");
        
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
        Debug.Log($"### [CalendarController] 스크롤 완료: {scrollRect.verticalNormalizedPosition:F3}");
    }
    
    // 달력 닫기 (내부용)
    private void CloseCalendarInternal(bool restoreScrollPosition = true)
    {
        // ★★★ 진행 중인 애니메이션 코루틴 중지 (위치 복원이 덮어씌워지지 않도록) ★★★
        StopAllCoroutines();
        
        // 현재 활성화된 패널의 요소들 가져오기
        RectTransform activeScrollContent = GetActiveScrollContent();
        UnityEngine.UI.ScrollRect activeScrollRect = GetActiveScrollRect();
        RectTransform activeDeadlineRow = GetActiveDeadlineRow();
        RectTransform activeEmergencyRow = GetActiveEmergencyRow();
        RectTransform activeAssigneeRow = GetActiveAssigneeRow();
        RectTransform activeInputFieldBody = GetActiveInputFieldBody();
        RectTransform activeImageMovie = GetActiveImageMovie();
        GetActiveOriginalPositions(out Vector2 deadlineOrig, out Vector2 emergencyOrig, out Vector2 assigneeOrig, 
                                   out Vector2 bodyOrig, out Vector2 imageMovieOrig, out float scrollHeightOrig, out Vector2 scrollPosOrig, 
                                   out Dictionary<Transform, Vector2> childPosOrig);
        
        string panelName = IsVoiceMemoPanelActive() ? "Panel_VoiceMemo" : (IsChecklistPanelActive() ? "Panel_Checklist" : (IsImageMemoPanelActive() ? "Panel_ImageMemo" : "Panel_TextMemo"));
        Debug.Log($"[CalendarController] CloseCalendarInternal - 현재 활성 패널: {panelName}");
        
        // ★★★ Checklist/VoiceMemo 패널 여부 확인 ★★★
        bool isChecklistActive = IsChecklistPanelActive();
        bool isVoiceMemoActive = IsVoiceMemoPanelActive();
        
        // 달력 패널 비활성화 (현재 활성화된 패널에 맞는 CalendarPanel)
        GameObject activeCalendarPanel = GetActiveCalendarPanel();
        if (activeCalendarPanel != null)
        {
            activeCalendarPanel.SetActive(false);
        }
        
        // CalendarPanel 위치 원래대로 (Checklist/VoiceMemo 제외)
        RectTransform activeCalendarPanelRect = GetActiveCalendarPanelRect();
        Vector2 activeCalendarOriginalPos = GetActiveCalendarOriginalPos();
        if (!isChecklistActive && !isVoiceMemoActive && activeCalendarPanelRect != null)
        {
            activeCalendarPanelRect.anchoredPosition = activeCalendarOriginalPos;
        }
        
        // DateBt 배경색 원래대로 (Panel_TextMemo)
        if (dateButtonImage != null && !isChecklistActive)
        {
            StartCoroutine(AnimateButtonColor(dateButtonImage, Color.white));
        }
        
        // ImageDateBt 배경색 원래대로 (Panel_ImageMemo)
        if (imageDateButtonImage != null && !isChecklistActive)
        {
            StartCoroutine(AnimateButtonColor(imageDateButtonImage, Color.white));
        }
        
        // ChecklistDateBt 배경색 원래대로 (Panel_Checklist)
        if (checklistDateButtonImage != null && isChecklistActive)
        {
            StartCoroutine(AnimateButtonColor(checklistDateButtonImage, Color.white));
        }
        
        // VoiceMemoDateBt 배경색 원래대로 (Panel_VoiceMemo)
        if (voiceMemoDateButtonImage != null && isVoiceMemoActive)
        {
            StartCoroutine(AnimateButtonColor(voiceMemoDateButtonImage, Color.white));
        }
        
        // ★★★ VoiceMemo 패널: CalendarPanel 비활성화 + ScrollContent 복원 + Emergency 원래 위치 ★★★
        if (isVoiceMemoActive)
        {
            // CalendarPanel 높이 가져오기
            float voiceMemoCalHeight = 0f;
            if (voiceMemoCalendarPanelRect != null)
            {
                voiceMemoCalHeight = voiceMemoCalendarPanelRect.sizeDelta.y;
            }
            
            // VoiceMemoEmergency 원래 위치로 복원
            if (voiceMemoEmergencyRow != null)
            {
                float moveAmount = voiceMemoCalHeight + voiceMemoEmergencyOffset;
                Vector3 pos = voiceMemoEmergencyRow.localPosition;
                pos.y += moveAmount;
                voiceMemoEmergencyRow.localPosition = pos;
                Debug.Log($"[CalendarController] VoiceMemoEmergency 원래 위치로 복원: +{moveAmount}px");
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
                
                Debug.Log($"[CalendarController] VoiceMemo ScrollContent 복원: {scrollHeightOrig} (요소 위치 유지)");
            }
            
            // 자동 스크롤 (Close)
            if (voiceMemoScrollRect != null && voiceMemoScrollDownAmountOnClose != 0)
            {
                StartCoroutine(AnimateChecklistAutoScroll(voiceMemoScrollRect, -voiceMemoScrollDownAmountOnClose));
                Debug.Log($"[CalendarController] VoiceMemo 닫기 시 자동 스크롤: {voiceMemoScrollDownAmountOnClose}px");
            }
            
            // VoiceContent 위치 복원 (VoiceMemoUIController에 위임)
            if (voiceMemoUIController != null)
            {
                voiceMemoUIController.RestoreVoiceContentPosition();
            }
            
            Debug.Log($"[CalendarController] CloseInternal - VoiceMemo 패널: CalendarPanel 비활성화 완료");
            return;
        }
        
        // ★★★ Checklist 패널: CalendarPanel 비활성화 + ScrollContent 복원 + Emergency 원래 위치 ★★★
        if (isChecklistActive)
        {
            // CalendarPanel 높이 가져오기
            float checklistCalHeight = 0f;
            if (checklistCalendarPanelRect != null)
            {
                checklistCalHeight = checklistCalendarPanelRect.sizeDelta.y;
            }
            
            // ★★★ ChecklistEmergency 원래 위치로 복원 (CalendarPanel 높이 + 오프셋만큼 위로) ★★★
            if (checklistEmergencyRow != null)
            {
                float moveAmount = checklistCalHeight + checklistEmergencyOffset;
                Vector3 pos = checklistEmergencyRow.localPosition;
                pos.y += moveAmount;
                checklistEmergencyRow.localPosition = pos;
                Debug.Log($"[CalendarController] ChecklistEmergency 원래 위치로 복원: +{moveAmount}px");
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
                
                Debug.Log($"[CalendarController] Checklist ScrollContent 복원: {scrollHeightOrig} (요소 위치 유지)");
            }
            
            // ★★★ 자동 스크롤 (Close) ★★★
            if (checklistScrollRect != null && checklistScrollDownAmountOnClose != 0)
            {
                StartCoroutine(AnimateChecklistAutoScroll(checklistScrollRect, -checklistScrollDownAmountOnClose));
                Debug.Log($"[CalendarController] Checklist 닫기 시 자동 스크롤: {checklistScrollDownAmountOnClose}px");
            }
            
            // InputField 위치는 ScrollContent 자식 복원 시 localPosition 유지로 자동 처리됨
            
            Debug.Log($"[CalendarController] CloseInternal - Checklist 패널: CalendarPanel 비활성화 완료");
            return;
        }
        
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
            
            // 2. Content 높이 복원
            Vector2 newSize = activeScrollContent.sizeDelta;
            newSize.y = scrollHeightOrig;
            activeScrollContent.sizeDelta = newSize;
            
            // 3. Content 위치 복원
            activeScrollContent.anchoredPosition = scrollPosOrig;
            Debug.Log($"### [CalendarController] {panelName} ScrollContent 복원 완료 (높이: {scrollHeightOrig})");
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
        
        // ★★★ 스크롤 위치 설정 (닫을 때 자동 스크롤) ★★★
        if (restoreScrollPosition && activeScrollContent != null)
        {
            // 현재 활성화된 패널에 따라 적절한 스크롤 양 선택
            bool isImageMemo = IsImageMemoPanelActive();
            float scrollDownAmount = isImageMemo ? imageMemoScrollDownAmountOnClose : scrollDownAmountOnClose;
            
            Debug.Log($"### [CalendarController] {panelName} Calendar 닫기 - isImageMemo: {isImageMemo}, scrollDownAmountOnClose: {scrollDownAmountOnClose}, imageMemoScrollDownAmountOnClose: {imageMemoScrollDownAmountOnClose}, 최종 scrollDownAmount: {scrollDownAmount}");
            
            if (scrollDownAmount > 0f)
            {
                // Content를 위로 이동시켜서 아래쪽 요소가 보이게 함 (양수값 = 위로 이동 = 아래쪽 요소 보임)
                StartCoroutine(AnimateScrollDownOnClose(activeScrollContent, scrollDownAmount));
            }
        }
        else if (!restoreScrollPosition)
        {
            Debug.Log("[CalendarController] 스크롤 위치 복원 건너뜀 (다른 패널이 열릴 예정)");
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
        
        Debug.Log($"[CalendarController] ▶▶▶ AnimateOutlineColor 시작 - from: {fromColor} to: {targetColor}");
        
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            
            outline.effectColor = Color.Lerp(fromColor, targetColor, t);
            yield return null;
        }
        
        outline.effectColor = targetColor;
        Debug.Log($"[CalendarController] ▶▶▶ AnimateOutlineColor 완료 - 최종 색상: {outline.effectColor}");
    }
    
    // 스크롤 위치 조정 (애니메이션 완료 후 실행)
    private System.Collections.IEnumerator AdjustScrollPosition(UnityEngine.UI.ScrollRect targetScrollRect, float targetPosition)
    {
        // 애니메이션 완료 대기 (animationDuration + 여유 시간)
        yield return new WaitForSeconds(animationDuration + 0.1f);
        
        // Canvas 업데이트 여러 번 수행
        for (int i = 0; i < 3; i++)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
        }
        
        // 스크롤 위치 조정
        if (targetScrollRect != null)
        {
            targetScrollRect.verticalNormalizedPosition = targetPosition;
            Canvas.ForceUpdateCanvases();
            RectTransform contentRect = targetScrollRect.content;
            Debug.Log($"[CalendarController] 스크롤 위치 조정: {targetPosition}, Content 높이: {(contentRect != null ? contentRect.sizeDelta.y.ToString() : "null")}");
        }
        else
        {
            Debug.LogWarning("[CalendarController] ScrollRect가 할당되지 않았습니다!");
        }
    }
    
    // Calendar 닫을 때 Content를 직접 이동시켜서 아래로 스크롤하는 애니메이션
    // (Content 높이를 변경하지 않고 ScrollRect의 movementType을 Unrestricted로 설정하여 스크롤 제한 해제)
    private System.Collections.IEnumerator AnimateScrollDownOnClose(RectTransform targetScrollContent, float scrollDownAmount)
    {
        Debug.Log($"### [CalendarController] AnimateScrollDownOnClose 시작 - scrollDownAmount: {scrollDownAmount}");
        
        if (targetScrollContent == null)
        {
            Debug.LogWarning("[CalendarController] AnimateScrollDownOnClose - ScrollContent가 null입니다!");
            yield break;
        }
        
        // 한 프레임 대기 (요소들이 복원된 후)
        yield return null;
        Canvas.ForceUpdateCanvases();
        
        // 현재 활성화된 ScrollRect 가져오기
        UnityEngine.UI.ScrollRect activeScrollRect = GetActiveScrollRect();
        if (activeScrollRect == null)
        {
            Debug.LogWarning("[CalendarController] AnimateScrollDownOnClose - ScrollRect가 null입니다!");
            yield break;
        }
        
        // 1. 원래 상태 저장
        originalMovementType = activeScrollRect.movementType;
        originalContentPosition = targetScrollContent.anchoredPosition;
        scrollRectToRestore = activeScrollRect;
        
        // 2. ScrollRect의 movementType을 Unrestricted로 변경 (스크롤 제한 해제)
        activeScrollRect.movementType = UnityEngine.UI.ScrollRect.MovementType.Unrestricted;
        
        Debug.Log($"### [CalendarController] ScrollRect movementType 변경: {originalMovementType} -> Unrestricted");
        
        // 3. 시작 위치 (원래 위치)
        Vector2 startPos = targetScrollContent.anchoredPosition;
        // 목표 위치 (Content를 위로 이동 = anchoredPosition.y 증가 = 아래쪽 요소가 보임)
        Vector2 targetPos = new Vector2(startPos.x, startPos.y + scrollDownAmount);
        
        Debug.Log($"### [CalendarController] Content 이동 - originalContentPosition: {originalContentPosition.y}, startPos: {startPos.y}, targetPos: {targetPos.y}, scrollDownAmount: {scrollDownAmount}");
        
        // 4. 애니메이션 실행
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            // Ease out
            t = 1f - Mathf.Pow(1f - t, 3f);
            targetScrollContent.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }
        
        targetScrollContent.anchoredPosition = targetPos;
        Debug.Log($"### [CalendarController] 닫기 스크롤 애니메이션 완료: {startPos.y} -> {targetPos.y}");
        
        // 5. 스크롤 복원 대기 상태 활성화
        isWaitingForScrollRestore = true;
        scrollDownAmountUsed = scrollDownAmount;
        Debug.Log($"### [CalendarController] 스크롤 복원 대기 활성화 - 원래 위치: {originalContentPosition.y}, 스크롤양: {scrollDownAmount}");
    }
    
    // 달력 UI 갱신
    private void UpdateCalendar()
    {
        // 년월 텍스트 업데이트 (현재 활성화된 패널에 맞는 MonthYearText)
        TMP_Text activeMonthYearText = GetActiveMonthYearText();
        if (activeMonthYearText != null)
        {
            activeMonthYearText.text = currentDisplayDate.ToString("yyyy년 MM월");
        }
        
        // 기존 날짜 버튼들 모두 제거 (빈 칸 포함)
        foreach (var obj in allDayObjects)
        {
            if (obj != null) Destroy(obj);
        }
        allDayObjects.Clear();
        dayButtonMap.Clear();
        
        // 해당 월의 첫날과 마지막날 계산
        DateTime firstDayOfMonth = new DateTime(currentDisplayDate.Year, currentDisplayDate.Month, 1);
        int daysInMonth = DateTime.DaysInMonth(currentDisplayDate.Year, currentDisplayDate.Month);
        int startDayOfWeek = (int)firstDayOfMonth.DayOfWeek; // 일요일=0, 월요일=1, ... 토요일=6
        
        // 빈 칸 생성 (월 시작 전, 일요일부터 시작)
        for (int i = 0; i < startDayOfWeek; i++)
        {
            CreateEmptyDayButton();
        }
        
        // 날짜 버튼 생성
        for (int day = 1; day <= daysInMonth; day++)
        {
            CreateDayButton(day);
        }
    }
    
    // 빈 날짜 칸 생성
    private void CreateEmptyDayButton()
    {
        Transform activeContainer = GetActiveCalendarDaysContainer();
        GameObject emptyObj = Instantiate(dayButtonPrefab, activeContainer);
        Button btn = emptyObj.GetComponent<Button>();
        btn.interactable = false;
        
        // 텍스트 비우기
        TMP_Text txt = emptyObj.GetComponentInChildren<TMP_Text>();
        if (txt != null) txt.text = "";
        
        // 모든 배경 이미지 숨기기
        HideAllBackgrounds(emptyObj);
        
        // 리스트에 추가
        allDayObjects.Add(emptyObj);
    }
    
    // 날짜 버튼 생성
    private void CreateDayButton(int day)
    {
        Transform activeContainer = GetActiveCalendarDaysContainer();
        GameObject dayObj = Instantiate(dayButtonPrefab, activeContainer);
        Button btn = dayObj.GetComponent<Button>();
        TMP_Text txt = dayObj.GetComponentInChildren<TMP_Text>();
        if (txt != null) txt.text = day.ToString();
        
        // 날짜 클릭 이벤트
        DateTime clickedDate = new DateTime(currentDisplayDate.Year, currentDisplayDate.Month, day);
        btn.onClick.AddListener(() => OnDayClicked(clickedDate));
        
        // 버튼 매핑 저장
        dayButtonMap[clickedDate.Date] = dayObj;
        
        // 먼저 모든 배경 숨기기
        HideAllBackgrounds(dayObj);
        
        // 배경 설정
        SetDayBackground(dayObj, clickedDate);
        
        // 리스트에 추가
        allDayObjects.Add(dayObj);
    }
    
    // 오브젝트의 모든 배경 이미지 숨기기
    private void HideAllBackgrounds(GameObject dayObj)
    {
        // 자식 중 Background 이름의 오브젝트 찾아서 숨기기
        Transform bgTransform = dayObj.transform.Find(backgroundObjectName);
        if (bgTransform != null)
        {
            Image bgImage = bgTransform.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.enabled = false;
            }
        }
        
        // 루트 오브젝트의 Image도 처리 (프리팹 구조에 따라)
        Image rootImage = dayObj.GetComponent<Image>();
        if (rootImage != null)
        {
            // Button의 Target Graphic으로 사용되는지 확인
            Button btn = dayObj.GetComponent<Button>();
            if (btn != null && btn.targetGraphic == rootImage)
            {
                // Button의 Target Graphic이면 색상만 투명하게
                rootImage.color = new Color(1, 1, 1, 0);
            }
        }
    }
    
    // 날짜에 맞는 배경 및 텍스트 색상 설정
    private void SetDayBackground(GameObject dayObj, DateTime date)
    {
        // 자식 중 Background 찾기
        Transform bgTransform = dayObj.transform.Find(backgroundObjectName);
        Image bgImage = null;
        
        if (bgTransform != null)
        {
            bgImage = bgTransform.GetComponent<Image>();
        }
        
        // Background 자식이 없으면 루트의 Image 사용
        if (bgImage == null)
        {
            bgImage = dayObj.GetComponent<Image>();
        }
        
        // 텍스트 컴포넌트 찾기
        TMP_Text txt = dayObj.GetComponentInChildren<TMP_Text>();
        
        // 선택된 날짜인 경우
        if (date.Date == selectedDate.Date)
        {
            if (bgImage != null)
            {
                bgImage.color = selectedColor;
                bgImage.enabled = true;
            }
            if (txt != null) txt.color = selectedTextColor; // 흰색
        }
        // 일반 날짜 - 배경 숨김
        else
        {
            if (bgImage != null) bgImage.enabled = false;
            if (txt != null) txt.color = normalTextColor; // 검정색
        }
    }
    
    // 날짜 선택 시
    private void OnDayClicked(DateTime date)
    {
        selectedDate = date;
        UpdateDateButtonText();
        UpdateDayButtonColors(); // 색상 업데이트
        
        // DateBt Outline 색상을 선택 후 색상으로 즉시 변경 (Panel_TextMemo)
        // 참고: 코루틴 사용 시 CloseCalendarInternal()에서 패널이 닫히면 코루틴이 중단되어 색상 변경이 완료되지 않음
        if (dateButtonOutline != null)
        {
            dateButtonOutline.effectColor = dateButtonOutlineSelected;
            Debug.Log($"[CalendarController] ▶▶▶ DateBt Outline 색상 즉시 변경 완료: {dateButtonOutlineSelected}");
        }
        
        // ImageDateBt Outline 색상을 선택 후 색상으로 즉시 변경 (Panel_ImageMemo)
        if (imageDateButtonOutline != null)
        {
            imageDateButtonOutline.effectColor = dateButtonOutlineSelected;
            Debug.Log($"[CalendarController] ▶▶▶ ImageDateBt Outline 색상 즉시 변경 완료: {dateButtonOutlineSelected}");
        }
        
        // ChecklistDateBt Outline 색상을 선택 후 색상으로 즉시 변경 (Panel_Checklist)
        if (checklistDateButtonOutline != null)
        {
            checklistDateButtonOutline.effectColor = dateButtonOutlineSelected;
            Debug.Log($"[CalendarController] ▶▶▶ ChecklistDateBt Outline 색상 즉시 변경 완료: {dateButtonOutlineSelected}");
        }
        
        // 달력 닫기
        isCalendarOpen = false;
        CloseCalendarInternal();
        
        Debug.Log($"선택된 날짜: {selectedDate:yyyy-MM-dd}");
    }
    
    // DateBt 텍스트 업데이트
    private void UpdateDateButtonText()
    {
        string dateString = selectedDate.ToString("MM/dd");
        
        // Panel_TextMemo DateBt 텍스트 업데이트
        if (dateButtonText != null)
        {
            dateButtonText.text = dateString;
        }
        
        // Panel_ImageMemo ImageDateBt 텍스트 업데이트
        if (imageDateButtonText != null)
        {
            imageDateButtonText.text = dateString;
        }
        
        // Panel_Checklist ChecklistDateBt 텍스트 업데이트
        if (checklistDateButtonText != null)
        {
            checklistDateButtonText.text = dateString;
            Debug.Log($"[CalendarController] ChecklistDateBt 텍스트 업데이트: {dateString}");
        }
    }
    
    // 모든 날짜 버튼의 색상 업데이트
    private void UpdateDayButtonColors()
    {
        foreach (var kvp in dayButtonMap)
        {
            DateTime date = kvp.Key;
            GameObject dayObj = kvp.Value;
            
            if (dayObj == null) continue;
            
            // 배경 설정
            SetDayBackground(dayObj, date);
        }
    }
    
    // 이전 달로 이동
    private void OnPrevMonthClicked()
    {
        currentDisplayDate = currentDisplayDate.AddMonths(-1);
        UpdateCalendar();
    }
    
    // 다음 달로 이동
    private void OnNextMonthClicked()
    {
        currentDisplayDate = currentDisplayDate.AddMonths(1);
        UpdateCalendar();
    }
    
    // 달력 닫기 (외부에서 호출 가능)
    public void CloseCalendar(bool restoreScrollPosition = true)
    {
        if (isCalendarOpen)
        {
            isCalendarOpen = false;
            CloseCalendarInternal(restoreScrollPosition);
        }
    }
    
    // 달력이 열려있는지 확인 (외부에서 호출 가능)
    public bool IsCalendarOpen()
    {
        return isCalendarOpen;
    }
    
    // 선택된 날짜 설정 (외부에서 호출 가능)
    public void SetSelectedDate(DateTime date)
    {
        selectedDate = date;
        currentDisplayDate = date;
        UpdateDateButtonText();
        
        // 달력이 열려있으면 색상도 업데이트
        GameObject activeCalendarPanel = GetActiveCalendarPanel();
        if (activeCalendarPanel != null && activeCalendarPanel.activeSelf)
        {
            UpdateDayButtonColors();
        }
        
        Debug.Log($"[CalendarController] 날짜 설정됨: {selectedDate:yyyy-MM-dd}");
    }
    
    // 선택된 날짜 가져오기
    public DateTime GetSelectedDate()
    {
        return selectedDate;
    }
    
    // 선택된 날짜를 문자열로 가져오기
    public string GetSelectedDateString(string format = "yyyy-MM-dd")
    {
        return selectedDate.ToString(format);
    }
    
    /// <summary>
    /// 날짜가 선택되었는지 확인 (Outline 색상 변경용)
    /// 오늘 날짜와 다른 날짜가 선택되었거나, 사용자가 명시적으로 날짜를 클릭한 경우 true
    /// </summary>
    public bool HasDateSelected()
    {
        // 간단히: 항상 기본값(오늘)이 설정되어 있으므로 true 반환
        // 또는 날짜 버튼 텍스트가 비어있지 않은지 확인
        if (dateButtonText != null && !string.IsNullOrWhiteSpace(dateButtonText.text))
        {
            return true;
        }
        if (imageDateButtonText != null && !string.IsNullOrWhiteSpace(imageDateButtonText.text))
        {
            return true;
        }
        if (checklistDateButtonText != null && !string.IsNullOrWhiteSpace(checklistDateButtonText.text))
        {
            return true;
        }
        return false;
    }
    
#if UNITY_EDITOR
    // Inspector에서 현재 TextMemo Deadline 위치를 복사하는 헬퍼 함수
    [UnityEngine.ContextMenu("Copy Current TextMemo Deadline Position")]
    private void CopyCurrentTextMemoDeadlinePosition()
    {
        if (deadlineRow != null)
        {
            deadlineTargetY = deadlineRow.anchoredPosition.y;
            UnityEngine.Debug.Log($"[CalendarController] Panel_TextMemo Deadline의 현재 Y 위치를 복사했습니다: {deadlineTargetY}");
        }
        else
        {
            UnityEngine.Debug.LogWarning("[CalendarController] Panel_TextMemo DeadlineRow가 할당되지 않았습니다!");
        }
    }
    
    // Inspector에서 현재 ImageMemo Deadline 위치를 복사하는 헬퍼 함수
    [UnityEngine.ContextMenu("Copy Current ImageMemo Deadline Position")]
    private void CopyCurrentImageMemoDeadlinePosition()
    {
        if (imageMemoDeadlineRow != null)
        {
            imageMemoDeadlineTargetY = imageMemoDeadlineRow.anchoredPosition.y;
            UnityEngine.Debug.Log($"[CalendarController] Panel_ImageMemo Deadline의 현재 Y 위치를 복사했습니다: {imageMemoDeadlineTargetY}");
        }
        else
        {
            UnityEngine.Debug.LogWarning("[CalendarController] Panel_ImageMemo DeadlineRow가 할당되지 않았습니다!");
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
        Debug.Log($"[CalendarController] 체크리스트 아이템 스크롤 확장량 설정: {expansion}");
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
    /// Checklist Calendar가 열려있을 때의 총 스크롤 확장량 반환
    /// (캘린더 확장 + 아이템 확장)
    /// </summary>
    public float GetChecklistTotalScrollExpansion()
    {
        return checklistScrollExpansion + checklistItemScrollExpansion;
    }
    
    /// <summary>
    /// VoiceMemo - Calendar가 열렸을 때 총 ScrollContent 확장량 반환
    /// (캘린더 확장 + 아이템 확장)
    /// </summary>
    public float GetVoiceMemoTotalScrollExpansion()
    {
        return voiceMemoScrollExpansion + voiceMemoItemScrollExpansion;
    }
    
    /// <summary>
    /// VoiceMemo CalendarPanel RectTransform 가져오기
    /// </summary>
    public RectTransform GetVoiceCalendarPanelRect()
    {
        return voiceMemoCalendarPanelRect;
    }
    
    /// <summary>
    /// Checklist Calendar가 열렸을 때 Emergency를 밀어야 하는 양 반환
    /// (Calendar 높이 + Emergency 오프셋)
    /// </summary>
    public float GetChecklistCalendarPushAmount()
    {
        float calendarHeight = 0f;
        if (checklistCalendarPanelRect != null)
        {
            calendarHeight = checklistCalendarPanelRect.sizeDelta.y;
        }
        return calendarHeight + checklistEmergencyOffset;
    }
    
    /// <summary>
    /// 체크리스트 스크롤 Content 크기를 즉시 업데이트
    /// 아이템 추가/삭제 시 호출 (Calendar 열림 여부와 관계없이)
    /// 위치 보정은 ChecklistUIController에서 처리
    /// </summary>
    public void UpdateChecklistScrollContentSize()
    {
        // Checklist 패널이 활성화되어 있지 않으면 스킵
        if (!IsChecklistPanelActive()) return;
        
        if (checklistScrollContent == null)
        {
            Debug.LogWarning("[CalendarController] checklistScrollContent가 NULL입니다!");
            return;
        }
        
        // 새 높이 계산
        float baseHeight = checklistScrollContentOriginalHeight;
        float calendarExpansion = 0f;
        
        // Calendar가 열려있으면 Calendar 확장량도 포함
        if (isCalendarOpen && checklistCalendarPanelRect != null)
        {
            float pushAmount = 0f;
            if (checklistUIController != null)
            {
                pushAmount = checklistUIController.LastPushAmount;
            }
            calendarExpansion = checklistScrollExpansion + pushAmount;
        }
        
        float newHeight = baseHeight + checklistItemScrollExpansion + calendarExpansion;
        
        Vector2 newSize = checklistScrollContent.sizeDelta;
        float previousHeight = newSize.y;
        
        // 높이 변화가 없으면 스킵
        if (Mathf.Abs(newHeight - previousHeight) < 0.01f) return;
        
        newSize.y = newHeight;
        checklistScrollContent.sizeDelta = newSize;
        
        // 위치 보정은 ChecklistUIController.UpdateLayoutBasedOnItemCount()에서 처리
        // (스트레치 앵커 보정을 여기서 하면 ChecklistUIController와 충돌)
        
        Debug.Log($"[CalendarController] ▶▶▶ Checklist ScrollContent 즉시 업데이트: {previousHeight} -> {newHeight} (아이템확장: {checklistItemScrollExpansion})");
    }
    
    /// <summary>
    /// Checklist Calendar가 열려있을 때 스크롤 크기만 업데이트 (레거시 - 호환성 유지)
    /// </summary>
    public void RefreshChecklistCalendarLayout(float pushDelta)
    {
        // RefreshChecklistLayout으로 대체됨
    }
    
    /// <summary>
    /// Checklist Calendar가 열려있을 때 전체 레이아웃 갱신
    /// 절대 위치 방식 (anchoredPosition 기준)
    /// </summary>
    /// <param name="itemPushAmount">체크리스트 아이템으로 인한 밀림량</param>
    public void RefreshChecklistLayout(float itemPushAmount)
    {
        // Calendar가 열려있지 않으면 스킵
        if (!isCalendarOpen || !IsChecklistPanelActive()) return;
        
        if (checklistScrollContent == null)
        {
            Debug.LogWarning("[CalendarController] RefreshChecklistLayout - checklistScrollContent가 NULL!");
            return;
        }
        
        // ★★★ 1. 원본 위치/높이 가져오기 ★★★
        GetActiveOriginalPositions(out Vector2 deadlineOrig, out Vector2 emergencyOrig, out Vector2 assigneeOrig, 
                                   out Vector2 bodyOrig, out Vector2 imageMovieOrig, out float scrollHeightOrig, out Vector2 scrollPosOrig, 
                                   out Dictionary<Transform, Vector2> childPosOrig);
        
        // ★★★ 2. 캘린더 패널 높이 가져오기 ★★★
        float calendarHeight = 0f;
        if (checklistCalendarPanelRect != null)
        {
            calendarHeight = checklistCalendarPanelRect.sizeDelta.y;
        }
        float panelPush = calendarHeight + checklistEmergencyOffset;
        
        // ★★★ 3. 총 확장량 및 앵커 보정 계산 ★★★
        // 주의: itemPushAmount는 위치 계산용이므로 스크롤 확장에는 포함하지 않음
        // checklistItemScrollExpansion이 이미 아이템 기반 확장을 포함함
        float totalExpansion = checklistScrollExpansion + checklistItemScrollExpansion;
        float anchorComp = totalExpansion / 2f;
        
        Debug.Log($"[CalendarController] ★★★ Checklist RefreshLayout 값 비교 ★★★");
        Debug.Log($"  - calendarHeight: {calendarHeight}, checklistEmergencyOffset: {checklistEmergencyOffset}, panelPush: {panelPush}");
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
        
        // CalendarPanel: 원래 위치 + 앵커 보정 - 아이템 밀림
        if (checklistCalendarPanelRect != null)
        {
            Vector2 newPos = checklistCalendarOriginalPos;
            newPos.y = checklistCalendarOriginalPos.y + anchorComp - itemPushAmount;
            checklistCalendarPanelRect.anchoredPosition = newPos;
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
            Debug.Log($"[CalendarController] Emergency - origY: {emergencyOrig.y}, panelComp: {panelAnchorComp}, panelPush: {panelPush}, itemPush: {itemPushAmount}, newY: {newPos.y}, 하단여백: {bottomMargin}");
        }
        
        Debug.Log($"[CalendarController] RefreshChecklistLayout 완료 - 높이: {previousHeight} -> {newSize.y}, itemExpansion: {checklistItemScrollExpansion}");
        
        // InputField 위치는 ScrollContent 앵커 설정에 따라 자동 조정됨
    }
    
    /// <summary>
    /// VoiceMemo 패널 레이아웃 갱신 (Calendar 열려있을 때)
    /// VoiceMemo 아이템 수 변경 시 호출
    /// </summary>
    public void RefreshVoiceMemoLayout(float itemPushAmount)
    {
        // Calendar가 열려있지 않으면 스킵
        if (!isCalendarOpen || !IsVoiceMemoPanelActive()) return;
        
        if (voiceMemoScrollContent == null)
        {
            Debug.LogWarning("[CalendarController] RefreshVoiceMemoLayout - voiceMemoScrollContent가 NULL!");
            return;
        }
        
        // ★★★ 1. 원본 위치/높이 가져오기 ★★★
        GetActiveOriginalPositions(out Vector2 deadlineOrig, out Vector2 emergencyOrig, out Vector2 assigneeOrig, 
                                   out Vector2 bodyOrig, out Vector2 imageMovieOrig, out float scrollHeightOrig, out Vector2 scrollPosOrig, 
                                   out Dictionary<Transform, Vector2> childPosOrig);
        
        // ★★★ 2. 캘린더 패널 높이 가져오기 ★★★
        float calendarHeight = 0f;
        if (voiceMemoCalendarPanelRect != null)
        {
            calendarHeight = voiceMemoCalendarPanelRect.sizeDelta.y;
        }
        float panelPush = calendarHeight + voiceMemoEmergencyOffset;
        
        // ★★★ 3. 총 확장량 및 앵커 보정 계산 ★★★
        float totalExpansion = voiceMemoScrollExpansion + voiceMemoItemScrollExpansion;
        float anchorComp = totalExpansion / 2f;
        
        Debug.Log($"[CalendarController] ★★★ VoiceMemo RefreshLayout 값 비교 ★★★");
        Debug.Log($"  - calendarHeight: {calendarHeight}, voiceMemoEmergencyOffset: {voiceMemoEmergencyOffset}, panelPush: {panelPush}");
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
            Debug.Log($"[CalendarController] Voice 위치 조정 (Calendar 열림): origY={voiceMemoVoiceMemoOriginalPos.y}, anchorComp={anchorComp}, newY={newPos.y}");
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
                        Debug.Log($"[CalendarController] {voiceElement.element.name} 미세 조정 (Calendar 열림): origY={originalPos.y}, openedOffset={voiceElement.openedOffsetY}, newY={newPos.y}");
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
        
        // CalendarPanel: 원래 위치 + 앵커 보정 - 아이템 밀림
        if (voiceMemoCalendarPanelRect != null)
        {
            Vector2 newPos = voiceMemoCalendarOriginalPos;
            newPos.y = voiceMemoCalendarOriginalPos.y + anchorComp - itemPushAmount;
            voiceMemoCalendarPanelRect.anchoredPosition = newPos;
        }
        
        // Emergency: 원래 위치 + 앵커보정 - 패널밀림 - 아이템밀림
        if (voiceMemoEmergencyRow != null)
        {
            float panelAnchorComp = voiceMemoScrollExpansion / 2f;
            Vector2 newPos = emergencyOrig;
            newPos.y = emergencyOrig.y + panelAnchorComp - panelPush - itemPushAmount;
            voiceMemoEmergencyRow.anchoredPosition = newPos;
            
            float bottomMargin = (newSize.y / 2f) + newPos.y;
            Debug.Log($"[CalendarController] VoiceMemo Emergency - origY: {emergencyOrig.y}, panelComp: {panelAnchorComp}, panelPush: {panelPush}, itemPush: {itemPushAmount}, newY: {newPos.y}, 하단여백: {bottomMargin}");
        }
        
        Debug.Log($"[CalendarController] RefreshVoiceMemoLayout 완료 - 높이: {previousHeight} -> {newSize.y}, itemExpansion: {voiceMemoItemScrollExpansion}");
    }
    
    /// <summary>
    /// VoiceMemo 패널이 열릴 때 모든 요소를 원래 위치로 리셋
    /// VoiceMemoUIController.OnEnable()에서 호출
    /// </summary>
    public void ResetVoiceMemoToOriginalLayout()
    {
        Debug.Log("[CalendarController] ResetVoiceMemoToOriginalLayout 호출");
        
        // Calendar가 열려있으면 닫기
        if (isCalendarOpen && IsVoiceMemoPanelActive())
        {
            CloseCalendar();
        }
        
        // VoiceMemo 아이템 스크롤 확장량 리셋
        voiceMemoItemScrollExpansion = 0f;
        
        // 모든 요소를 원래 위치로 복원
        if (voiceMemoDeadlineRow != null)
        {
            voiceMemoDeadlineRow.anchoredPosition = voiceMemoDeadlineOriginalPos;
            Debug.Log($"[CalendarController] VoiceMemo Deadline 리셋: {voiceMemoDeadlineOriginalPos}");
        }
        
        if (voiceMemoEmergencyRow != null)
        {
            voiceMemoEmergencyRow.anchoredPosition = voiceMemoEmergencyOriginalPos;
            Debug.Log($"[CalendarController] VoiceMemo Emergency 리셋: {voiceMemoEmergencyOriginalPos}");
        }
        
        if (voiceMemoVoiceMemoRow != null)
        {
            voiceMemoVoiceMemoRow.anchoredPosition = voiceMemoVoiceMemoOriginalPos;
            Debug.Log($"[CalendarController] Voice 리셋: {voiceMemoVoiceMemoOriginalPos}");
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
                        Debug.Log($"[CalendarController] {voiceElement.element.name} 미세 조정 (Calendar 닫힘): origY={originalPos.y}, closedOffset={voiceElement.closedOffsetY}, newY={resetPos.y}");
                    }
                }
            }
        }
        
        if (voiceMemoCalendarPanelRect != null)
        {
            voiceMemoCalendarPanelRect.anchoredPosition = voiceMemoCalendarOriginalPos;
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
            Debug.Log($"[CalendarController] VoiceMemo ScrollContent 리셋 - 높이: {voiceMemoScrollContentOriginalHeight}");
        }
        
        Debug.Log("[CalendarController] ResetVoiceMemoToOriginalLayout 완료");
    }
}