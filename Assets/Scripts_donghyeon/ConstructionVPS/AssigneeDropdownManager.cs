using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// InputField 이동 설정 클래스 - 드롭다운 열림/닫힘 시 각각의 오프셋 설정
/// </summary>
[System.Serializable]
public class InputFieldPushSettings
{
    [Tooltip("이동할 InputField")]
    public RectTransform inputField;
    
    [Tooltip("드롭다운 닫힘 상태에서의 Y 오프셋 (AssigneeRow로부터의 거리)")]
    public float closedOffsetY = 0f;
    
    [Tooltip("드롭다운 열림 상태에서의 Y 오프셋 (AssigneeRow로부터의 거리)")]
    public float openedOffsetY = 0f;
}

/// <summary>
/// AssigneeRow를 탭하면 드롭다운으로 4개의 이름 중 선택할 수 있는 매니저
/// 단일 인스턴스로 Panel_TextMemo, Panel_ImageMemo, Panel_Checklist 모두 관리
/// </summary>
public class AssigneeDropdownManager : MonoBehaviour
{
    [Header("UI References - Panel_TextMemo")]
    [SerializeField] private Button textMemoAssigneeButton;
    [SerializeField] private Image textMemoButtonBackground;
    [SerializeField] private TMP_Text textMemoSelectedNameText;
    [SerializeField] private RectTransform textMemoDropdownIcon;
    [SerializeField] private Image textMemoDropdownIconImage;
    [SerializeField] private GameObject textMemoDropdownPanel;
    [SerializeField] private Transform textMemoDropdownContent;
    [SerializeField] private Shadow textMemoButtonShadow;
    [SerializeField] private RectTransform textMemoAssigneeRowRect; // AssigneeRow의 RectTransform (기준점)
    
    [Header("UI References - Panel_ImageMemo")]
    [SerializeField] private Button imageMemoAssigneeButton;
    [SerializeField] private Image imageMemoButtonBackground;
    [SerializeField] private TMP_Text imageMemoSelectedNameText;
    [SerializeField] private RectTransform imageMemoDropdownIcon;
    [SerializeField] private Image imageMemoDropdownIconImage;
    [SerializeField] private GameObject imageMemoDropdownPanel;
    [SerializeField] private Transform imageMemoDropdownContent;
    [SerializeField] private Shadow imageMemoButtonShadow;
    [SerializeField] private RectTransform imageMemoAssigneeRowRect;
    
    [Header("UI References - Panel_Checklist")]
    [SerializeField] private Button checklistAssigneeButton;
    [SerializeField] private Image checklistButtonBackground;
    [SerializeField] private TMP_Text checklistSelectedNameText;
    [SerializeField] private RectTransform checklistDropdownIcon;
    [SerializeField] private Image checklistDropdownIconImage;
    [SerializeField] private GameObject checklistDropdownPanel;
    [SerializeField] private Transform checklistDropdownContent;
    [SerializeField] private Shadow checklistButtonShadow;
    [SerializeField] private RectTransform checklistAssigneeRowRect;
    
    [Header("UI References - Panel_VoiceMemo")]
    [SerializeField] private Button voiceMemoAssigneeButton;
    [SerializeField] private Image voiceMemoButtonBackground;
    [SerializeField] private TMP_Text voiceMemoSelectedNameText;
    [SerializeField] private RectTransform voiceMemoDropdownIcon;
    [SerializeField] private Image voiceMemoDropdownIconImage;
    [SerializeField] private GameObject voiceMemoDropdownPanel;
    [SerializeField] private Transform voiceMemoDropdownContent;
    [SerializeField] private Shadow voiceMemoButtonShadow;
    [SerializeField] private RectTransform voiceMemoAssigneeRowRect;
    
    [Header("Common Prefab")]
    [SerializeField] private GameObject dropdownItemPrefab;
    
    [Header("InputFields to Push Down - Panel_TextMemo")]
    [Tooltip("Panel_TextMemo: 드롭다운이 열릴 때 이동할 InputField들과 각각의 오프셋 설정")]
    [SerializeField] private InputFieldPushSettings[] inputFieldsToPush;
    
    [Header("InputFields to Push Down - Panel_ImageMemo")]
    [Tooltip("Panel_ImageMemo: 드롭다운이 열릴 때 이동할 InputField들과 각각의 오프셋 설정")]
    [SerializeField] private InputFieldPushSettings[] imageMemoInputFieldsToPush;
    
    [Header("InputFields to Push Down - Panel_Checklist")]
    [Tooltip("Panel_Checklist: 드롭다운이 열릴 때 이동할 InputField들과 각각의 오프셋 설정")]
    [SerializeField] private InputFieldPushSettings[] checklistInputFieldsToPush;
    
    [Header("InputFields to Push Down - Panel_VoiceMemo")]
    [Tooltip("Panel_VoiceMemo: 드롭다운이 열릴 때 이동할 InputField들과 각각의 오프셋 설정")]
    [SerializeField] private InputFieldPushSettings[] voiceMemoInputFieldsToPush;
    
    [Header("Scroll Content - Panel_TextMemo")]
    [Tooltip("Panel_TextMemo ScrollRect의 Content (크기 조정용)")]
    [SerializeField] private RectTransform scrollContent;
    [Tooltip("Panel_TextMemo ScrollRect 컴포넌트 (스크롤 위치 조정용)")]
    [SerializeField] private UnityEngine.UI.ScrollRect scrollRect;
    
    [Header("Scroll Content - Panel_ImageMemo")]
    [Tooltip("Panel_ImageMemo ScrollRect의 Content (크기 조정용)")]
    [SerializeField] private RectTransform imageMemoScrollContent;
    [Tooltip("Panel_ImageMemo ScrollRect 컴포넌트 (스크롤 위치 조정용)")]
    [SerializeField] private UnityEngine.UI.ScrollRect imageMemoScrollRect;
    
    [Header("Scroll Content - Panel_Checklist")]
    [Tooltip("Panel_Checklist ScrollRect의 Content (크기 조정용)")]
    [SerializeField] private RectTransform checklistScrollContent;
    [Tooltip("Panel_Checklist ScrollRect 컴포넌트 (스크롤 위치 조정용)")]
    [SerializeField] private UnityEngine.UI.ScrollRect checklistScrollRect;
    
    [Header("Scroll Content - Panel_VoiceMemo")]
    [Tooltip("Panel_VoiceMemo ScrollRect의 Content (크기 조정용)")]
    [SerializeField] private RectTransform voiceMemoScrollContent;
    [Tooltip("Panel_VoiceMemo ScrollRect 컴포넌트 (스크롤 위치 조정용)")]
    [SerializeField] private UnityEngine.UI.ScrollRect voiceMemoScrollRect;
    
    [Header("Scroll Settings - Panel_VoiceMemo")]
    [Tooltip("Panel_VoiceMemo 스크롤 하단 제한 (0~1, 0=맨아래, 1=맨위). 예: 0.3이면 맨아래 30% 지점까지만 스크롤 가능")]
    [SerializeField] [Range(0f, 1f)] private float voiceMemoScrollBottomLimit = 0f;
    
    [Header("Panel Offset Settings - Panel_VoiceMemo")]
    [Tooltip("VoiceCalendarPanel - AssigneeDropdown 닫힘 상태 Y 오프셋")]
    [SerializeField] private float voiceCalendarPanelClosedOffsetY = 0f;
    [Tooltip("VoiceCalendarPanel - AssigneeDropdown 열림 상태 Y 오프셋")]
    [SerializeField] private float voiceCalendarPanelOpenedOffsetY = 0f;
    [Tooltip("VoiceTimePickerPanel - AssigneeDropdown 닫힘 상태 Y 오프셋")]
    [SerializeField] private float voiceTimePickerPanelClosedOffsetY = 0f;
    [Tooltip("VoiceTimePickerPanel - AssigneeDropdown 열림 상태 Y 오프셋")]
    [SerializeField] private float voiceTimePickerPanelOpenedOffsetY = 0f;
    
    [Header("Common Settings")]
    [SerializeField]
    private List<string> assigneeNames = new List<string>
    {
        "이나연",
        "김서진",
        "전예슬"
    };
    
    [Header("Common Colors")]
    [SerializeField] private Color closedBackgroundColor = Color.white;
    [SerializeField] private Color openedBackgroundColor = new Color(0x96 / 255f, 0xCB / 255f, 0xE0 / 255f); // #96CBE0
    [SerializeField] private Color closedTextColor = new Color(0xBD / 255f, 0xBD / 255f, 0xBD / 255f); // #BDBDBD (회색)
    [SerializeField] private Color openedTextColor = new Color(0x57 / 255f, 0x57 / 255f, 0x57 / 255f); // #575757 (진한 회색)
    [SerializeField] private Color emptyShadowColor = new Color(0x96 / 255f, 0xCB / 255f, 0xE0 / 255f, 0.5f); // #96CBE0 반투명 (비어있을 때)
    [SerializeField] private Color filledShadowColor = new Color(0xD9 / 255f, 0xD9 / 255f, 0xD9 / 255f, 0.5f); // #D9D9D9 반투명 (채워졌을 때)
    
    [Header("Common Settings - Dropdown")]
    [Tooltip("드롭다운 패널의 대략적인 높이")]
    [SerializeField] private float dropdownPanelHeight = 400f;
    [Tooltip("스크롤 높이 배율 (드롭다운 높이 * 배율)")]
    [SerializeField] private float scrollHeightMultiplier = 1.5f;
    [Tooltip("추가 스크롤 여유 공간")]
    [SerializeField] private float extraScrollPadding = 100f;
    
    [Header("Rounded Corners Shader")]
    [Tooltip("둥근 모서리용 셰이더 (모바일 빌드에서 필요)")]
    [SerializeField] private Shader roundedCornersShader;
    
    [Header("Rounded Corners Settings")]
    [SerializeField] private float cornerRadius = 70f; // 모서리 둥글기 반경 (패널 모서리와 일치하도록 조정)
    [SerializeField] private float bgExtendAmount = 10f; // 배경 확장량 (여백 제거용)
    
    [Header("Item Size")]
    [SerializeField] private float itemHeight = 130f; // 각 아이템의 높이 (기본 140 → 130으로 줄임)
    
    [Header("Panel Padding")]
    [SerializeField] private float panelPaddingTop = 0f; // 패널 상단 여백
    [SerializeField] private float panelPaddingBottom = 0f; // 패널 하단 여백
    
    [Header("ImageMemoUIController Reference")]
    [Tooltip("ImageMemoUIController 참조 (드롭다운 닫힐 때 알림용)")]
    [SerializeField] private ImageMemoUIController imageMemoUIController;
    
    [Header("Checklist Layout Controllers")]
    [Tooltip("CalendarController 참조 (Checklist 패널 레이아웃 동기화용)")]
    [SerializeField] private CalendarController calendarController;
    [Tooltip("TimePickerController 참조 (Checklist 패널 레이아웃 동기화용)")]
    [SerializeField] private TimePickerController timePickerController;
    [Tooltip("ChecklistUIController 참조 (현재 밀림량 가져오기용)")]
    [SerializeField] private ChecklistUIController checklistUIController;
    [Tooltip("VoiceMemoUIController 참조 (현재 밀림량 가져오기용)")]
    [SerializeField] private VoiceMemoUIController voiceMemoUIController;
    
    [Header("Animation Settings")]
    [Tooltip("애니메이션 지속 시간")]
    [SerializeField] private float animationDuration = 0.3f;

    // 현재 편집 중인 메모 ID
    private static string currentMemoId = null;
    
    // 각 패널별 선택된 담당자 및 드롭다운 상태
    private string textMemoSelectedAssignee = "";
    private string imageMemoSelectedAssignee = "";
    private string checklistSelectedAssignee = "";
    private string voiceMemoSelectedAssignee = "";
    private bool textMemoIsDropdownOpen = false;
    private bool imageMemoIsDropdownOpen = false;
    private bool checklistIsDropdownOpen = false;
    private bool voiceMemoIsDropdownOpen = false;
    
    // Panel_TextMemo ScrollContent 원래 높이 및 위치
    private float scrollContentOriginalHeight;
    private Vector2 scrollContentOriginalPos;
    
    // Panel_ImageMemo ScrollContent 원래 높이 및 위치
    private float imageMemoScrollContentOriginalHeight;
    private Vector2 imageMemoScrollContentOriginalPos;
    
    // Panel_Checklist ScrollContent 원래 높이 및 위치
    private float checklistScrollContentOriginalHeight;
    private Vector2 checklistScrollContentOriginalPos;
    
    // Panel_VoiceMemo ScrollContent 원래 높이 및 위치
    private float voiceMemoScrollContentOriginalHeight;
    private Vector2 voiceMemoScrollContentOriginalPos;

    // ========== 패널 감지 헬퍼 메서드 ==========
    
    /// <summary>
    /// Panel_Checklist가 현재 활성화되어 있는지 확인
    /// </summary>
    private bool IsChecklistPanelActive()
    {
        return checklistScrollContent != null && checklistScrollContent.gameObject.activeInHierarchy;
    }
    
    /// <summary>
    /// Panel_VoiceMemo가 현재 활성화되어 있는지 확인
    /// </summary>
    private bool IsVoiceMemoPanelActive()
    {
        return voiceMemoScrollContent != null && voiceMemoScrollContent.gameObject.activeInHierarchy;
    }
    
    /// <summary>
    /// Panel_ImageMemo가 현재 활성화되어 있는지 확인
    /// </summary>
    private bool IsImageMemoPanelActive()
    {
        return imageMemoScrollContent != null && imageMemoScrollContent.gameObject.activeInHierarchy;
    }
    
    /// <summary>
    /// 현재 활성화된 패널 이름 가져오기
    /// </summary>
    private string GetActivePanelName()
    {
        if (IsVoiceMemoPanelActive()) return "Panel_VoiceMemo";
        if (IsChecklistPanelActive()) return "Panel_Checklist";
        if (IsImageMemoPanelActive()) return "Panel_ImageMemo";
        return "Panel_TextMemo";
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 AssigneeButton 가져오기
    /// </summary>
    private Button GetActiveAssigneeButton()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoAssigneeButton;
        if (IsChecklistPanelActive()) return checklistAssigneeButton;
        if (IsImageMemoPanelActive()) return imageMemoAssigneeButton;
        return textMemoAssigneeButton;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 DropdownPanel 가져오기
    /// </summary>
    private GameObject GetActiveDropdownPanel()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoDropdownPanel;
        if (IsChecklistPanelActive()) return checklistDropdownPanel;
        if (IsImageMemoPanelActive()) return imageMemoDropdownPanel;
        return textMemoDropdownPanel;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 DropdownContent 가져오기
    /// </summary>
    private Transform GetActiveDropdownContent()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoDropdownContent;
        if (IsChecklistPanelActive()) return checklistDropdownContent;
        if (IsImageMemoPanelActive()) return imageMemoDropdownContent;
        return textMemoDropdownContent;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 ButtonBackground 가져오기
    /// </summary>
    private Image GetActiveButtonBackground()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoButtonBackground;
        if (IsChecklistPanelActive()) return checklistButtonBackground;
        if (IsImageMemoPanelActive()) return imageMemoButtonBackground;
        return textMemoButtonBackground;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 SelectedNameText 가져오기
    /// </summary>
    private TMP_Text GetActiveSelectedNameText()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoSelectedNameText;
        if (IsChecklistPanelActive()) return checklistSelectedNameText;
        if (IsImageMemoPanelActive()) return imageMemoSelectedNameText;
        return textMemoSelectedNameText;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 DropdownIcon 가져오기
    /// </summary>
    private RectTransform GetActiveDropdownIcon()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoDropdownIcon;
        if (IsChecklistPanelActive()) return checklistDropdownIcon;
        if (IsImageMemoPanelActive()) return imageMemoDropdownIcon;
        return textMemoDropdownIcon;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 DropdownIconImage 가져오기
    /// </summary>
    private Image GetActiveDropdownIconImage()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoDropdownIconImage;
        if (IsChecklistPanelActive()) return checklistDropdownIconImage;
        if (IsImageMemoPanelActive()) return imageMemoDropdownIconImage;
        return textMemoDropdownIconImage;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 ButtonShadow 가져오기
    /// </summary>
    private Shadow GetActiveButtonShadow()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoButtonShadow;
        if (IsChecklistPanelActive()) return checklistButtonShadow;
        if (IsImageMemoPanelActive()) return imageMemoButtonShadow;
        return textMemoButtonShadow;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 AssigneeRowRect 가져오기
    /// </summary>
    private RectTransform GetActiveAssigneeRowRect()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoAssigneeRowRect;
        if (IsChecklistPanelActive()) return checklistAssigneeRowRect;
        if (IsImageMemoPanelActive()) return imageMemoAssigneeRowRect;
        return textMemoAssigneeRowRect;
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
    /// 현재 활성화된 패널의 InputFieldPushSettings 배열 가져오기
    /// </summary>
    private InputFieldPushSettings[] GetActiveInputFieldsToPush()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoInputFieldsToPush;
        if (IsChecklistPanelActive()) return checklistInputFieldsToPush;
        if (IsImageMemoPanelActive()) return imageMemoInputFieldsToPush;
        return inputFieldsToPush;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 SelectedAssignee 가져오기/설정하기
    /// </summary>
    private string GetActiveSelectedAssignee()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoSelectedAssignee;
        if (IsChecklistPanelActive()) return checklistSelectedAssignee;
        if (IsImageMemoPanelActive()) return imageMemoSelectedAssignee;
        return textMemoSelectedAssignee;
    }
    
    private void SetActiveSelectedAssignee(string value)
    {
        if (IsVoiceMemoPanelActive()) voiceMemoSelectedAssignee = value;
        else if (IsChecklistPanelActive()) checklistSelectedAssignee = value;
        else if (IsImageMemoPanelActive()) imageMemoSelectedAssignee = value;
        else textMemoSelectedAssignee = value;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 IsDropdownOpen 가져오기/설정하기
    /// </summary>
    private bool GetActiveIsDropdownOpen()
    {
        if (IsVoiceMemoPanelActive()) return voiceMemoIsDropdownOpen;
        if (IsChecklistPanelActive()) return checklistIsDropdownOpen;
        if (IsImageMemoPanelActive()) return imageMemoIsDropdownOpen;
        return textMemoIsDropdownOpen;
    }
    
    private void SetActiveIsDropdownOpen(bool value)
    {
        if (IsVoiceMemoPanelActive()) voiceMemoIsDropdownOpen = value;
        else if (IsChecklistPanelActive()) checklistIsDropdownOpen = value;
        else if (IsImageMemoPanelActive()) imageMemoIsDropdownOpen = value;
        else textMemoIsDropdownOpen = value;
    }
    
    /// <summary>
    /// 현재 활성화된 패널의 원래 위치 값들 가져오기
    /// </summary>
    private void GetActiveOriginalValues(out float scrollHeightOrig, out Vector2 scrollPosOrig)
    {
        if (IsVoiceMemoPanelActive())
        {
            scrollHeightOrig = voiceMemoScrollContentOriginalHeight;
            scrollPosOrig = voiceMemoScrollContentOriginalPos;
        }
        else if (IsChecklistPanelActive())
        {
            scrollHeightOrig = checklistScrollContentOriginalHeight;
            scrollPosOrig = checklistScrollContentOriginalPos;
        }
        else if (IsImageMemoPanelActive())
        {
            scrollHeightOrig = imageMemoScrollContentOriginalHeight;
            scrollPosOrig = imageMemoScrollContentOriginalPos;
        }
        else
        {
            scrollHeightOrig = scrollContentOriginalHeight;
            scrollPosOrig = scrollContentOriginalPos;
        }
    }
    
    private void Start()
    {
        // 둥근 모서리 셰이더 전역 설정 (모바일 빌드용)
        if (roundedCornersShader != null)
        {
            RoundedCornersImage.SetGlobalShader(roundedCornersShader);
            Debug.Log("★★★ [ASSIGNEE_DROPDOWN] 둥근 모서리 셰이더 전역 설정 완료");
        }
        
        // ImageMemoUIController 자동 검색
        if (imageMemoUIController == null)
        {
            imageMemoUIController = FindObjectOfType<ImageMemoUIController>();
            if (imageMemoUIController != null)
            {
                Debug.Log("★★★ [ASSIGNEE_DROPDOWN] ImageMemoUIController 자동 검색 완료");
            }
        }
        
        // ★★★ Checklist 레이아웃 동기화용 컨트롤러 자동 검색 ★★★
        if (calendarController == null)
        {
            calendarController = FindObjectOfType<CalendarController>();
            if (calendarController != null)
            {
                Debug.Log("★★★ [ASSIGNEE_DROPDOWN] CalendarController 자동 검색 완료");
            }
        }
        if (timePickerController == null)
        {
            timePickerController = FindObjectOfType<TimePickerController>();
            if (timePickerController != null)
            {
                Debug.Log("★★★ [ASSIGNEE_DROPDOWN] TimePickerController 자동 검색 완료");
            }
        }
        if (checklistUIController == null)
        {
            checklistUIController = FindObjectOfType<ChecklistUIController>();
            if (checklistUIController != null)
            {
                Debug.Log("★★★ [ASSIGNEE_DROPDOWN] ChecklistUIController 자동 검색 완료");
            }
        }
        
        // ========== Panel_TextMemo 초기화 ==========
        InitializePanelUI(
            "Panel_TextMemo",
            textMemoAssigneeButton,
            ref textMemoButtonBackground,
            ref textMemoSelectedNameText,
            ref textMemoDropdownIcon,
            ref textMemoDropdownIconImage,
            ref textMemoButtonShadow,
            textMemoDropdownPanel,
            textMemoDropdownContent,
            ref textMemoAssigneeRowRect
        );
        
        // ========== Panel_ImageMemo 초기화 ==========
        InitializePanelUI(
            "Panel_ImageMemo",
            imageMemoAssigneeButton,
            ref imageMemoButtonBackground,
            ref imageMemoSelectedNameText,
            ref imageMemoDropdownIcon,
            ref imageMemoDropdownIconImage,
            ref imageMemoButtonShadow,
            imageMemoDropdownPanel,
            imageMemoDropdownContent,
            ref imageMemoAssigneeRowRect
        );
        
        // ========== Panel_Checklist 초기화 ==========
        InitializePanelUI(
            "Panel_Checklist",
            checklistAssigneeButton,
            ref checklistButtonBackground,
            ref checklistSelectedNameText,
            ref checklistDropdownIcon,
            ref checklistDropdownIconImage,
            ref checklistButtonShadow,
            checklistDropdownPanel,
            checklistDropdownContent,
            ref checklistAssigneeRowRect
        );
        
        // ========== Panel_VoiceMemo 초기화 ==========
        InitializePanelUI(
            "Panel_VoiceMemo",
            voiceMemoAssigneeButton,
            ref voiceMemoButtonBackground,
            ref voiceMemoSelectedNameText,
            ref voiceMemoDropdownIcon,
            ref voiceMemoDropdownIconImage,
            ref voiceMemoButtonShadow,
            voiceMemoDropdownPanel,
            voiceMemoDropdownContent,
            ref voiceMemoAssigneeRowRect
        );
        
        // ScrollContent 원래 높이 및 위치 저장
        if (scrollContent != null)
        {
            scrollContentOriginalHeight = scrollContent.sizeDelta.y;
            scrollContentOriginalPos = scrollContent.anchoredPosition;
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] Panel_TextMemo ScrollContent 원래 높이: {scrollContentOriginalHeight}");
        }
        
        if (imageMemoScrollContent != null)
        {
            imageMemoScrollContentOriginalHeight = imageMemoScrollContent.sizeDelta.y;
            imageMemoScrollContentOriginalPos = imageMemoScrollContent.anchoredPosition;
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] Panel_ImageMemo ScrollContent 원래 높이: {imageMemoScrollContentOriginalHeight}");
        }
        
        if (checklistScrollContent != null)
        {
            checklistScrollContentOriginalHeight = checklistScrollContent.sizeDelta.y;
            checklistScrollContentOriginalPos = checklistScrollContent.anchoredPosition;
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] Panel_Checklist ScrollContent 원래 높이: {checklistScrollContentOriginalHeight}");
        }
        
        // ScrollRect 할당 확인
        if (scrollRect == null)
            Debug.LogWarning("★★★ [ASSIGNEE_DROPDOWN] Panel_TextMemo ScrollRect가 할당되지 않았습니다!");
        else
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] Panel_TextMemo ScrollRect 할당 확인: {scrollRect.name}");
        
        if (imageMemoScrollRect == null)
            Debug.LogWarning("★★★ [ASSIGNEE_DROPDOWN] Panel_ImageMemo ScrollRect가 할당되지 않았습니다!");
        else
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] Panel_ImageMemo ScrollRect 할당 확인: {imageMemoScrollRect.name}");
        
        if (checklistScrollRect == null)
            Debug.LogWarning("★★★ [ASSIGNEE_DROPDOWN] Panel_Checklist ScrollRect가 할당되지 않았습니다!");
        else
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] Panel_Checklist ScrollRect 할당 확인: {checklistScrollRect.name}");
        
        if (voiceMemoScrollContent != null)
        {
            voiceMemoScrollContentOriginalHeight = voiceMemoScrollContent.sizeDelta.y;
            voiceMemoScrollContentOriginalPos = voiceMemoScrollContent.anchoredPosition;
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] Panel_VoiceMemo ScrollContent 원래 높이: {voiceMemoScrollContentOriginalHeight}");
        }
        
        if (voiceMemoScrollRect == null)
            Debug.LogWarning("★★★ [ASSIGNEE_DROPDOWN] Panel_VoiceMemo ScrollRect가 할당되지 않았습니다!");
        else
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] Panel_VoiceMemo ScrollRect 할당 확인: {voiceMemoScrollRect.name}");
        
        // ★★★ VoiceMemoUIController 자동 검색 ★★★
        if (voiceMemoUIController == null)
        {
            voiceMemoUIController = FindObjectOfType<VoiceMemoUIController>();
            if (voiceMemoUIController != null)
            {
                Debug.Log("★★★ [ASSIGNEE_DROPDOWN] VoiceMemoUIController 자동 검색 완료");
            }
        }

        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] 모든 패널 초기화 완료");
    }
    
    /// <summary>
    /// 각 패널의 UI 요소 초기화 (자동 검색, 버튼 연결, 드롭다운 아이템 생성)
    /// </summary>
    private void InitializePanelUI(
        string panelName,
        Button assigneeButton,
        ref Image buttonBackground,
        ref TMP_Text selectedNameText,
        ref RectTransform dropdownIcon,
        ref Image dropdownIconImage,
        ref Shadow buttonShadow,
        GameObject dropdownPanel,
        Transform dropdownContent,
        ref RectTransform assigneeRowRect)
    {
        if (assigneeButton == null)
        {
            Debug.LogWarning($"★★★ [ASSIGNEE_DROPDOWN] {panelName} - AssigneeButton이 할당되지 않았습니다!");
            return;
        }
        
        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] {panelName} 초기화 시작");
        
        // AssigneeRow의 RectTransform 가져오기 (AssigneeButton의 부모 또는 자신)
        if (assigneeRowRect == null)
        {
            assigneeRowRect = assigneeButton.GetComponent<RectTransform>();
        }
        
        // buttonShadow 자동 검색 (BgImage의 Shadow)
        if (buttonShadow == null)
        {
            Transform bgImage = assigneeButton.transform.Find("BgImage");
            if (bgImage != null)
            {
                buttonShadow = bgImage.GetComponent<Shadow>();
            }
        }
        
        // buttonBackground 자동 검색
        if (buttonBackground == null)
        {
            buttonBackground = assigneeButton.GetComponent<Image>();
        }

        // selectedNameText 자동 검색
        if (selectedNameText == null)
        {
            selectedNameText = assigneeButton.GetComponentInChildren<TMP_Text>();
        }

        // dropdownIcon 자동 검색 (IconImage 이름으로 찾기)
        if (dropdownIcon == null)
        {
            Transform iconTransform = assigneeButton.transform.Find("IconImage");
            if (iconTransform != null)
            {
                dropdownIcon = iconTransform.GetComponent<RectTransform>();
                dropdownIconImage = iconTransform.GetComponent<Image>();
                Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] {panelName} - dropdownIcon 자동 검색 성공");
            }
            else
            {
                Debug.LogWarning($"★★★ [ASSIGNEE_DROPDOWN] {panelName} - IconImage를 찾을 수 없습니다!");
            }
        }
        
        // dropdownIconImage 자동 검색
        if (dropdownIconImage == null && dropdownIcon != null)
        {
            dropdownIconImage = dropdownIcon.GetComponent<Image>();
        }

        // 버튼 이벤트 연결
        assigneeButton.onClick.RemoveAllListeners(); // 중복 방지
        assigneeButton.onClick.AddListener(ToggleDropdown);
        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] {panelName} - AssigneeButton 이벤트 연결 완료");

        // 드롭다운 초기 상태는 숨김
        if (dropdownPanel != null)
        {
            dropdownPanel.SetActive(false);
            
            // CanvasGroup이 있으면 Interactable 활성화
            var canvasGroup = dropdownPanel.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] {panelName} - CanvasGroup interactable=true, blocksRaycasts=true 설정");
            }
            
            // DropdownPanel의 Outline이 잘리는 문제 수정
            FixDropdownPanelOutline(dropdownPanel);
            
            // 드롭다운 아이템 생성
            CreateDropdownItems(dropdownPanel, dropdownContent);
        }

        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] {panelName} 초기화 완료");
    }

    private void ToggleDropdown()
    {
        GameObject dropdownPanel = GetActiveDropdownPanel();
        if (dropdownPanel == null) return;

        bool isDropdownOpen = !dropdownPanel.activeSelf;
        SetActiveIsDropdownOpen(isDropdownOpen);
        dropdownPanel.SetActive(isDropdownOpen);

        // 버튼 외형 업데이트 (색상, 아이콘)
        UpdateButtonAppearance(isDropdownOpen);
        
        // InputField 위치 업데이트
        UpdateInputFieldPositions(isDropdownOpen);
        
        // VoiceMemo 패널의 Calendar/TimePicker Panel 위치 업데이트
        UpdateVoiceMemoPanelPositions(isDropdownOpen);

        string panelName = GetActivePanelName();
        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] {panelName} 드롭다운 {(isDropdownOpen ? "열림" : "닫힘")}");
        
        // 드롭다운 열림 시 Content 크기 확인
        Transform dropdownContent = GetActiveDropdownContent();
        if (isDropdownOpen && dropdownContent is RectTransform contentRect)
        {
            Canvas.ForceUpdateCanvases();
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] [열림 시] Content SizeDelta: {contentRect.sizeDelta}");
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] [열림 시] Content Rect Height: {contentRect.rect.height}");
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] [열림 시] Content 자식 수: {contentRect.childCount}");
            
            // 각 자식의 위치 및 실제 크기 확인
            for (int i = 0; i < contentRect.childCount; i++)
            {
                RectTransform child = contentRect.GetChild(i) as RectTransform;
                if (child != null)
                {
                    Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] 자식[{i}] '{child.name}' " +
                             $"위치: {child.anchoredPosition}, " +
                             $"sizeDelta: {child.sizeDelta}, " +
                             $"rect: ({child.rect.width:F2}, {child.rect.height:F2}), " +
                             $"anchor: ({child.anchorMin.x:F2},{child.anchorMin.y:F2})-({child.anchorMax.x:F2},{child.anchorMax.y:F2})");
                }
            }
        }
        
        // 현재 활성화된 패널의 ScrollContent 및 설정 가져오기
        RectTransform activeScrollContent = GetActiveScrollContent();
        UnityEngine.UI.ScrollRect activeScrollRect = GetActiveScrollRect();
        InputFieldPushSettings[] activeInputFields = GetActiveInputFieldsToPush();
        GetActiveOriginalValues(out float scrollHeightOrig, out Vector2 scrollPosOrig);
        
        // ScrollContent 조정: 아래로 이동한 요소들을 스크롤로 볼 수 있도록
        if (activeScrollContent != null)
        {
            if (isDropdownOpen)
            {
                // 드롭다운 열림: 아래로 이동한 요소들을 위해 Content를 아래로 확장
                // InputField들이 아래로 이동하므로, 그 만큼 Content 높이 증가
                float maxDownwardMove = 0f;
                
                if (activeInputFields != null)
                {
                    foreach (var setting in activeInputFields)
                    {
                        if (setting == null || setting.inputField == null) continue;
                        // closedOffsetY와 openedOffsetY의 차이가 이동량
                        float moveAmount = Mathf.Abs(setting.closedOffsetY - setting.openedOffsetY);
                        if (moveAmount > maxDownwardMove)
                        {
                            maxDownwardMove = moveAmount;
                        }
                    }
                }
                
                // 드롭다운 패널 높이 + 가장 큰 이동량 + 여유 공간
                float additionalHeight = dropdownPanelHeight + maxDownwardMove + extraScrollPadding;
                
                // ★★★ Checklist/VoiceMemo 패널에서 Calendar/TimePicker가 열려있으면 확장 포함 ★★★
                float baseHeight = scrollHeightOrig;
                if (IsChecklistPanelActive())
                {
                    baseHeight = GetChecklistScrollHeightWithPanelExpansion(scrollHeightOrig);
                }
                else if (IsVoiceMemoPanelActive())
                {
                    baseHeight = GetVoiceMemoScrollHeightWithPanelExpansion(scrollHeightOrig);
                }
                
                // 1. Content 높이 증가 (아래로 확장)
                Vector2 newSize = activeScrollContent.sizeDelta;
                newSize.y = baseHeight + additionalHeight;
                activeScrollContent.sizeDelta = newSize;
                
                Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] {panelName} ScrollContent 높이 증가: {baseHeight} -> {newSize.y} (이동량: {maxDownwardMove})");
            }
            else
            {
                // 드롭다운 닫힘: 높이 및 위치 복원
                // ★★★ Checklist/VoiceMemo 패널에서 Calendar/TimePicker가 열려있으면 확장 유지 ★★★
                float targetHeight = scrollHeightOrig;
                if (IsChecklistPanelActive())
                {
                    targetHeight = GetChecklistScrollHeightWithPanelExpansion(scrollHeightOrig);
                }
                else if (IsVoiceMemoPanelActive())
                {
                    targetHeight = GetVoiceMemoScrollHeightWithPanelExpansion(scrollHeightOrig);
                }
                
                Vector2 newSize = activeScrollContent.sizeDelta;
                newSize.y = targetHeight;
                activeScrollContent.sizeDelta = newSize;
                activeScrollContent.anchoredPosition = scrollPosOrig;
                
                Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] {panelName} ScrollContent 복원: 높이={newSize.y}");
            }
        }
        
        // 스크롤 위치 조정
        if (activeScrollRect != null)
        {
            if (isDropdownOpen)
            {
                // 드롭다운 열림: 스크롤 위치 변경하지 않음 (현재 위치 유지)
                // ★★★ Checklist 패널일 때 Calendar/TimePicker 레이아웃 갱신 ★★★
                RefreshChecklistLayoutIfNeeded();
            }
            else
            {
                // 드롭다운 닫힘: 스크롤을 위로 복원
                StartCoroutine(AdjustScrollPosition(activeScrollRect, 1f)); // 1 = 맨 위
                
                // ImageMemoUIController에 드롭다운 닫힘 알림 (ImageMemo 패널일 때만)
                if (IsImageMemoPanelActive() && imageMemoUIController != null)
                {
                    imageMemoUIController.OnDropdownClosed();
                }
                
                // VoiceContent 위치 복원 (VoiceMemo 패널일 때)
                if (IsVoiceMemoPanelActive() && voiceMemoUIController != null)
                {
                    voiceMemoUIController.RestoreVoiceContentPosition();
                }
                
                // ★★★ Checklist 패널일 때 Calendar/TimePicker 레이아웃 갱신 ★★★
                RefreshChecklistLayoutIfNeeded();
            }
        }
    }
    
    /// <summary>
    /// Checklist 패널에서 Calendar/TimePicker가 열려있을 때의 ScrollContent 높이 계산
    /// </summary>
    private float GetChecklistScrollHeightWithPanelExpansion(float baseHeight)
    {
        float expansion = 0f;
        
        // Calendar가 열려있으면 확장량 추가
        if (calendarController != null && calendarController.IsCalendarOpen())
        {
            expansion = calendarController.GetChecklistTotalScrollExpansion();
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] Checklist Calendar 확장량: {expansion}");
        }
        // TimePicker가 열려있으면 확장량 추가
        else if (timePickerController != null && timePickerController.IsTimePickerOpen())
        {
            expansion = timePickerController.GetChecklistTotalScrollExpansion();
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] Checklist TimePicker 확장량: {expansion}");
        }
        
        return baseHeight + expansion;
    }
    
    /// <summary>
    /// VoiceMemo 패널에서 Calendar/TimePicker가 열려있을 때의 ScrollContent 높이 계산
    /// </summary>
    private float GetVoiceMemoScrollHeightWithPanelExpansion(float baseHeight)
    {
        float expansion = 0f;
        
        // Calendar가 열려있으면 확장량 추가
        if (calendarController != null && calendarController.IsCalendarOpen())
        {
            expansion = calendarController.GetVoiceMemoTotalScrollExpansion();
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] VoiceMemo Calendar 확장량: {expansion}");
        }
        // TimePicker가 열려있으면 확장량 추가
        else if (timePickerController != null && timePickerController.IsTimePickerOpen())
        {
            expansion = timePickerController.GetVoiceMemoTotalScrollExpansion();
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] VoiceMemo TimePicker 확장량: {expansion}");
        }
        
        return baseHeight + expansion;
    }
    
    /// <summary>
    /// Checklist/VoiceMemo 패널에서 Calendar/TimePicker가 열려있으면 레이아웃 갱신
    /// Dropdown 열림/닫힘 시 ScrollContent 크기가 변경되어 레이아웃 동기화 필요
    /// </summary>
    private void RefreshChecklistLayoutIfNeeded()
    {
        // ★★★ Checklist 패널 처리 ★★★
        if (IsChecklistPanelActive())
        {
            float pushAmount = 0f;
            if (checklistUIController != null)
            {
                pushAmount = checklistUIController.LastPushAmount;
            }
            
            // CalendarController의 Calendar가 열려있으면 레이아웃 갱신
            if (calendarController != null && calendarController.IsCalendarOpen())
            {
                calendarController.RefreshChecklistLayout(pushAmount);
                Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] Checklist Calendar 레이아웃 갱신 - pushAmount: {pushAmount}");
            }
            
            // TimePickerController의 TimePicker가 열려있으면 레이아웃 갱신
            if (timePickerController != null && timePickerController.IsTimePickerOpen())
            {
                timePickerController.RefreshChecklistLayout(pushAmount);
                Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] Checklist TimePicker 레이아웃 갱신 - pushAmount: {pushAmount}");
            }
            return;
        }
        
        // ★★★ VoiceMemo 패널 처리 ★★★
        // VoiceMemo는 고정된 2개의 아이템만 있으므로, Calendar/TimePicker가 열려있을 때
        // AssigneeDropdown으로 인한 추가 레이아웃 갱신이 필요없음
        // (Calendar/TimePicker가 이미 Voice 요소들을 올바르게 조정했음)
        if (IsVoiceMemoPanelActive())
        {
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] VoiceMemo 패널 - Calendar/TimePicker가 열려있으면 추가 레이아웃 갱신 스킵");
            // 레이아웃 갱신을 하지 않음 (중복 조정 방지)
        }
    }
    
    /// <summary>
    /// 드롭다운을 강제로 닫기 (외부에서 호출 가능)
    /// </summary>
    public void CloseDropdown()
    {
        GameObject dropdownPanel = GetActiveDropdownPanel();
        if (dropdownPanel == null) return;
        if (!dropdownPanel.activeSelf) return; // 이미 닫혀있으면 무시
        
        // 현재 활성화된 패널의 ScrollContent 및 설정 가져오기
        RectTransform activeScrollContent = GetActiveScrollContent();
        UnityEngine.UI.ScrollRect activeScrollRect = GetActiveScrollRect();
        GetActiveOriginalValues(out float scrollHeightOrig, out Vector2 scrollPosOrig);
        string panelName = GetActivePanelName();
        
        SetActiveIsDropdownOpen(false);
        dropdownPanel.SetActive(false);
        
        // 모든 아이템 색상 리셋 (모바일 터치 누적 방지)
        DropdownItemStateHandler.ResetAllItems();
        
        // 버튼 외형을 닫힌 상태로 업데이트
        UpdateButtonAppearance(false);
        
        // InputField 위치 복원
        UpdateInputFieldPositions(false);
        
        // ScrollContent 높이 및 위치 복원
        if (activeScrollContent != null)
        {
            // ★★★ Checklist 패널에서 Calendar/TimePicker가 열려있으면 확장 유지 ★★★
            float targetHeight = scrollHeightOrig;
            if (IsChecklistPanelActive())
            {
                targetHeight = GetChecklistScrollHeightWithPanelExpansion(scrollHeightOrig);
            }
            
            Vector2 newSize = activeScrollContent.sizeDelta;
            newSize.y = targetHeight;
            activeScrollContent.sizeDelta = newSize;
            activeScrollContent.anchoredPosition = scrollPosOrig;
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] {panelName} ScrollContent 복원 (외부 호출): 높이={newSize.y}");
        }
        
        // 스크롤 위치 복원
        if (activeScrollRect != null)
        {
            StartCoroutine(AdjustScrollPosition(activeScrollRect, 1f)); // 1 = 맨 위
        }
        
        // ImageMemoUIController에 드롭다운 닫힘 알림 (ImageMemo 패널일 때만)
        if (IsImageMemoPanelActive() && imageMemoUIController != null)
        {
            imageMemoUIController.OnDropdownClosed();
        }
        
        // VoiceContent 위치 복원 (VoiceMemo 패널일 때)
        if (IsVoiceMemoPanelActive() && voiceMemoUIController != null)
        {
            voiceMemoUIController.RestoreVoiceContentPosition();
        }
        
        // ★★★ Checklist 패널일 때 Calendar/TimePicker 레이아웃 갱신 ★★★
        RefreshChecklistLayoutIfNeeded();
        
        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] 드롭다운 강제 닫힘 (외부 호출) - {panelName}");
    }
    
    /// <summary>
    /// InputField들을 드롭다운 상태에 맞게 위치 조정 (애니메이션)
    /// ★★★ 현재 위치에서 상대적으로 이동 (Calendar/TimePicker 상태와 무관) ★★★
    /// </summary>
    private void UpdateInputFieldPositions(bool isOpen)
    {
        // 현재 활성화된 패널의 InputFields 배열 가져오기
        InputFieldPushSettings[] activeInputFields = GetActiveInputFieldsToPush();
        string panelName = GetActivePanelName();
        
        if (activeInputFields == null)
        {
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] {panelName} - InputFields가 없음");
            return;
        }
        
        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] {panelName} - InputField 위치 업데이트 시작 (isOpen: {isOpen})");
        
        foreach (var setting in activeInputFields)
        {
            if (setting == null || setting.inputField == null) continue;
            
            // ★★★ 이동량 계산 (열림: 아래로, 닫힘: 위로) ★★★
            // 각 요소는 Inspector에서 설정된 자체 offset 값 사용
            float moveAmount = setting.openedOffsetY - setting.closedOffsetY;
            
            Vector2 currentPos = setting.inputField.anchoredPosition;
            Vector2 newPos;
            
            if (isOpen)
            {
                // 드롭다운 열림: 현재 위치에서 moveAmount만큼 아래로 (음수 방향)
                newPos = new Vector2(currentPos.x, currentPos.y + moveAmount);
            }
            else
            {
                // 드롭다운 닫힘: 현재 위치에서 moveAmount만큼 위로 (양수 방향)
                newPos = new Vector2(currentPos.x, currentPos.y - moveAmount);
            }
            
            // 애니메이션으로 이동
            StartCoroutine(AnimatePosition(setting.inputField, currentPos, newPos));
            
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] {panelName} - {setting.inputField.name} 위치 조정: " +
                     $"currentY={currentPos.y}, moveAmount={moveAmount}, newY={newPos.y}");
        }
    }
    
    /// <summary>
    /// VoiceMemo 패널의 Calendar/TimePicker Panel 위치 업데이트
    /// </summary>
    private void UpdateVoiceMemoPanelPositions(bool isOpen)
    {
        // VoiceMemo 패널이 아니면 스킵
        if (!IsVoiceMemoPanelActive())
            return;
        
        // CalendarPanel 위치 조정
        if (calendarController != null && calendarController.IsCalendarOpen())
        {
            RectTransform voiceCalendarPanelRect = calendarController.GetVoiceCalendarPanelRect();
            if (voiceCalendarPanelRect != null)
            {
                float moveAmount = voiceCalendarPanelOpenedOffsetY - voiceCalendarPanelClosedOffsetY;
                Vector2 currentPos = voiceCalendarPanelRect.anchoredPosition;
                Vector2 newPos;
                
                if (isOpen)
                {
                    // 드롭다운 열림: 현재 위치에서 moveAmount만큼 이동
                    newPos = new Vector2(currentPos.x, currentPos.y + moveAmount);
                }
                else
                {
                    // 드롭다운 닫힘: 현재 위치에서 moveAmount만큼 복원
                    newPos = new Vector2(currentPos.x, currentPos.y - moveAmount);
                }
                
                StartCoroutine(AnimatePosition(voiceCalendarPanelRect, currentPos, newPos));
                Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] VoiceCalendarPanel 위치 조정: currentY={currentPos.y}, moveAmount={moveAmount}, newY={newPos.y}");
            }
        }
        
        // TimePickerPanel 위치 조정
        if (timePickerController != null && timePickerController.IsTimePickerOpen())
        {
            RectTransform voiceTimePickerPanelRect = timePickerController.GetVoiceTimePickerPanelRect();
            if (voiceTimePickerPanelRect != null)
            {
                float moveAmount = voiceTimePickerPanelOpenedOffsetY - voiceTimePickerPanelClosedOffsetY;
                Vector2 currentPos = voiceTimePickerPanelRect.anchoredPosition;
                Vector2 newPos;
                
                if (isOpen)
                {
                    // 드롭다운 열림: 현재 위치에서 moveAmount만큼 이동
                    newPos = new Vector2(currentPos.x, currentPos.y + moveAmount);
                }
                else
                {
                    // 드롭다운 닫힘: 현재 위치에서 moveAmount만큼 복원
                    newPos = new Vector2(currentPos.x, currentPos.y - moveAmount);
                }
                
                StartCoroutine(AnimatePosition(voiceTimePickerPanelRect, currentPos, newPos));
                Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] VoiceTimePickerPanel 위치 조정: currentY={currentPos.y}, moveAmount={moveAmount}, newY={newPos.y}");
            }
        }
    }

    // 버튼 외형 업데이트 (배경색, 텍스트 색, 아이콘 회전, 아이콘 색상)
    private void UpdateButtonAppearance(bool isOpen)
    {
        Image buttonBackground = GetActiveButtonBackground();
        TMP_Text selectedNameText = GetActiveSelectedNameText();
        RectTransform dropdownIcon = GetActiveDropdownIcon();
        Image dropdownIconImage = GetActiveDropdownIconImage();
        
        if (isOpen)
        {
            // 드롭다운 열림: 배경 #96CBE0, 텍스트 흰색, 아이콘 아래 방향, 아이콘 흰색
            if (buttonBackground != null)
            {
                buttonBackground.color = openedBackgroundColor;
            }

            if (selectedNameText != null)
            {
                selectedNameText.color = openedTextColor;
            }

            // 아이콘 회전: 아래 방향 (0도)
            if (dropdownIcon != null)
            {
                dropdownIcon.localEulerAngles = new Vector3(0, 0, 0);
            }
            
            if (dropdownIconImage != null)
            {
                dropdownIconImage.color = openedTextColor;
            }
        }
        else
        {
            // 드롭다운 닫힘: 배경 흰색, 텍스트 검정, 아이콘 오른쪽 방향, 아이콘 검정
            if (buttonBackground != null)
            {
                buttonBackground.color = closedBackgroundColor;
            }

            if (selectedNameText != null)
            {
                selectedNameText.color = closedTextColor;
            }

            // 아이콘 회전: 오른쪽 방향 (90도)
            if (dropdownIcon != null)
            {
                dropdownIcon.localEulerAngles = new Vector3(0, 0, 90);
            }
            
            if (dropdownIconImage != null)
            {
                dropdownIconImage.color = closedTextColor;
            }
        }
    }
    
    /// <summary>
    /// AssigneeButton의 Shadow 색상 업데이트 (선택 여부에 따라)
    /// </summary>
    /// <param name="isFilled">담당자가 선택되었는지 여부</param>
    private void UpdateButtonShadowColor(bool isFilled)
    {
        Shadow buttonShadow = GetActiveButtonShadow();
        if (buttonShadow != null)
        {
            buttonShadow.effectColor = isFilled ? filledShadowColor : emptyShadowColor;
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] Shadow 색상 변경: {(isFilled ? "채워짐" : "비어있음")} - {buttonShadow.effectColor}");
        }
    }

    private void CreateDropdownItems(GameObject dropdownPanel, Transform dropdownContent)
    {
        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] CreateDropdownItems 시작");
        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] dropdownPanel={dropdownPanel != null}");
        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] dropdownContent={dropdownContent != null}");
        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] dropdownItemPrefab={dropdownItemPrefab != null}");
        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] assigneeNames.Count={assigneeNames.Count}");

        if (dropdownContent == null || dropdownItemPrefab == null)
        {
            Debug.LogError("★★★ [ASSIGNEE_DROPDOWN] dropdownContent 또는 dropdownItemPrefab이 설정되지 않았습니다!");
            return;
        }

        // 기존 아이템 제거
        int existingChildCount = dropdownContent.childCount;
        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] 기존 자식 수: {existingChildCount}");
        foreach (Transform child in dropdownContent)
        {
            Destroy(child.gameObject);
        }

        // assigneeNames 내용 출력
        for (int i = 0; i < assigneeNames.Count; i++)
        {
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] assigneeNames[{i}] = '{assigneeNames[i]}'");
        }

        // 각 이름에 대한 버튼 생성
        int createdCount = 0;
        int totalCount = assigneeNames.Count;
        
        for (int i = 0; i < assigneeNames.Count; i++)
        {
            string name = assigneeNames[i];
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] 아이템 생성 시작: '{name}' (index={i})");
            GameObject itemObj = Instantiate(dropdownItemPrefab, dropdownContent);
            itemObj.name = $"DropdownItem_{name}";
            
            // RectTransform 먼저 설정 (anchor를 top-stretch로 변경하여 너비 확보)
            RectTransform itemRect = itemObj.GetComponent<RectTransform>();
            if (itemRect != null)
            {
                // Anchor를 좌우 stretch로 설정하여 부모 너비를 따르도록
                itemRect.anchorMin = new Vector2(0f, 1f); // 좌상단 기준
                itemRect.anchorMax = new Vector2(1f, 1f); // 우상단 기준
                itemRect.pivot = new Vector2(0.5f, 1f); // 피벗은 상단 중앙
                itemRect.sizeDelta = new Vector2(0f, itemHeight); // 너비는 stretch, 높이는 130px
                itemRect.anchoredPosition = new Vector2(0f, 0f); // 위치는 레이아웃에 맡김
                Debug.Log($"▶▶▶ [CreateDropdownItems] '{name}' RectTransform 설정: anchor=(0,1)-(1,1), sizeDelta=(0, {itemHeight})");
            }
            
            // ContentSizeFitter 제거 (있다면 레이아웃 간섭 방지)
            var sizeFitter = itemObj.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (sizeFitter != null)
            {
                UnityEngine.Object.DestroyImmediate(sizeFitter);
                Debug.Log($"▶▶▶ [CreateDropdownItems] '{name}' ContentSizeFitter 제거");
            }
            
            // LayoutElement 추가 및 설정
            var layoutElement = itemObj.GetComponent<UnityEngine.UI.LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = itemObj.AddComponent<UnityEngine.UI.LayoutElement>();
                Debug.Log($"▶▶▶ [CreateDropdownItems] '{name}' LayoutElement 추가");
            }
            
            layoutElement.preferredHeight = itemHeight;
            layoutElement.minHeight = itemHeight;
            layoutElement.flexibleHeight = 0; // 확장 금지
            layoutElement.ignoreLayout = false;
            Debug.Log($"▶▶▶ [CreateDropdownItems] '{name}' LayoutElement 설정: preferred={itemHeight}, min={itemHeight}, flexible=0");

            // 텍스트 설정
            TMP_Text itemText = itemObj.GetComponentInChildren<TMP_Text>();
            if (itemText != null)
            {
                itemText.text = name;
                itemText.color = new Color(0.19607843f, 0.19607843f, 0.19607843f); // #323232 (진한 회색)
                Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] 텍스트 설정 완료: '{name}'");
            }
            else
            {
                Debug.LogWarning($"★★★ [ASSIGNEE_DROPDOWN] TMP_Text를 찾을 수 없음: '{name}'");
            }

            // 버튼 이벤트 연결
            Button itemButton = itemObj.GetComponent<Button>();
            if (itemButton == null)
            {
                itemButton = itemObj.GetComponentInChildren<Button>();
            }

            if (itemButton != null)
            {
                string capturedName = name;
                TMP_Text capturedText = itemText;
                
                // Button 자체의 Image 참조 (스코프 전체에서 사용)
                Image buttonBaseImage = itemButton.GetComponent<Image>();
                
                // BgImage라는 이름의 자식 Image를 찾기
                Transform bgImageTransform = itemObj.transform.Find("BgImage");
                Image buttonImage = null;
                
                if (bgImageTransform != null)
                {
                    buttonImage = bgImageTransform.GetComponent<Image>();
                    Debug.Log($"▶▶▶ [CreateDropdownItems] '{name}' - 기존 BgImage 발견");
                }
                else
                {
                    Debug.Log($"▶▶▶ [CreateDropdownItems] '{name}' - BgImage가 없어서 생성합니다");
                    
                    // Button의 기존 Image를 투명하게 설정
                    if (buttonBaseImage != null)
                    {
                        buttonBaseImage.color = new Color(1f, 1f, 1f, 0f); // 완전 투명
                        Debug.Log($"▶▶▶ [CreateDropdownItems] '{name}' - Button Image를 투명으로 설정");
                    }
                    
                    // BgImage 자식 GameObject 생성
                    GameObject bgImageObj = new GameObject("BgImage");
                    bgImageObj.transform.SetParent(itemObj.transform, false);
                    bgImageObj.transform.SetAsFirstSibling(); // 텍스트보다 뒤에 배치
                    
                    // RectTransform 설정 (부모를 꽉 채움)
                    RectTransform bgRect = bgImageObj.AddComponent<RectTransform>();
                    bgRect.anchorMin = Vector2.zero;
                    bgRect.anchorMax = Vector2.one;
                    bgRect.offsetMin = Vector2.zero;
                    bgRect.offsetMax = Vector2.zero;
                    bgRect.sizeDelta = Vector2.zero;
                    
                    // Image 컴포넌트 추가
                    buttonImage = bgImageObj.AddComponent<Image>();
                    buttonImage.color = new Color(1f, 1f, 1f, 0f); // 초기에는 투명 (호버 시 #96CBE0로 변경됨)
                    buttonImage.raycastTarget = true;
                    buttonImage.type = Image.Type.Simple;
                    
                    Debug.Log($"▶▶▶ [CreateDropdownItems] '{name}' - BgImage 생성 완료 (투명 배경, raycastTarget=true)");
                }

                // 첫 번째 또는 마지막 아이템에 둥근 모서리 적용
                if (buttonImage != null)
                {
                    bool isFirst = (i == 0);
                    bool isLast = (i == totalCount - 1);
                    
                    // RectTransform 확인 (이미 위에서 설정되었지만 재확인)
                    RectTransform bgRect = buttonImage.GetComponent<RectTransform>();
                    if (bgRect != null && bgRect.gameObject != itemButton.gameObject)
                    {
                        // BgImage를 부모(아이템)를 완전히 채우도록 재설정
                        bgRect.anchorMin = Vector2.zero;
                        bgRect.anchorMax = Vector2.one;
                        bgRect.offsetMin = Vector2.zero;
                        bgRect.offsetMax = Vector2.zero;
                        bgRect.sizeDelta = Vector2.zero;
                    }
                    
                    if (isFirst || isLast)
                    {
                        // 기존 RoundedCornersImage 제거 (중복 방지)
                        var existingRounded = buttonImage.gameObject.GetComponent<RoundedCornersImage>();
                        if (existingRounded != null)
                        {
                            UnityEngine.Object.DestroyImmediate(existingRounded);
                        }
                        
                        RoundedCornersImage roundedCorners = buttonImage.gameObject.AddComponent<RoundedCornersImage>();
                        
                        if (isFirst && isLast)
                        {
                            // 아이템이 1개뿐인 경우: 모든 모서리 둥글게
                            roundedCorners.SetRadius(cornerRadius);
                            Debug.Log($"▶▶▶ [CreateDropdownItems] '{name}' - 유일한 아이템: 모든 모서리 둥글게 ({cornerRadius})");
                        }
                        else if (isFirst)
                        {
                            // 첫 번째 아이템: 위쪽 모서리만 둥글게 (좌상, 우상)
                            roundedCorners.SetTopCornersRadius(cornerRadius);
                            Debug.Log($"▶▶▶ [CreateDropdownItems] '{name}' - 첫 번째 아이템: 위쪽 모서리 둥글게 ({cornerRadius})");
                        }
                        else if (isLast)
                        {
                            // 마지막 아이템: 아래쪽 모서리만 둥글게 (좌하, 우하)
                            roundedCorners.SetBottomCornersRadius(cornerRadius);
                            Debug.Log($"▶▶▶ [CreateDropdownItems] '{name}' - 마지막 아이템: 아래쪽 모서리 둥글게 ({cornerRadius})");
                        }
                    }
                    else
                    {
                        Debug.Log($"▶▶▶ [CreateDropdownItems] '{name}' - 중간 아이템: 둥근 모서리 없음");
                    }
                }
                
                // RoundedCornersImage 추가 후 LayoutElement 다시 강제 설정 (크기 고정)
                if (layoutElement != null)
                {
                    layoutElement.preferredHeight = itemHeight;
                    layoutElement.minHeight = itemHeight;
                    layoutElement.flexibleHeight = 0;
                    Debug.Log($"▶▶▶ [CreateDropdownItems] '{name}' RoundedCorners 추가 후 LayoutElement 재설정: {itemHeight}");
                }
                
                // 아이템 RectTransform 다시 강제 설정 (anchor stretch 유지)
                if (itemRect != null)
                {
                    itemRect.anchorMin = new Vector2(0f, 1f);
                    itemRect.anchorMax = new Vector2(1f, 1f);
                    itemRect.pivot = new Vector2(0.5f, 1f);
                    itemRect.sizeDelta = new Vector2(0f, itemHeight); // stretch 모드에서 너비 0은 정상
                    Debug.Log($"▶▶▶ [CreateDropdownItems] '{name}' RoundedCorners 추가 후 RectTransform 재설정: anchor=(0,1)-(1,1), sizeDelta=(0, {itemHeight})");
                }

                // 버튼 이벤트: 클릭 시 담당자 선택
                itemButton.onClick.AddListener(() => OnSelectAssignee(capturedName));

                // Button 자체의 Image는 raycastTarget false로 설정 (BgImage만 raycast 받음)
                if (buttonBaseImage != null)
                {
                    buttonBaseImage.raycastTarget = false;
                    Debug.Log($"▶▶▶ [CreateDropdownItems] '{name}' - Button Image raycastTarget = false");
                }
                
                // Button의 Interactable 확인
                Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] '{name}' Button.interactable: {itemButton.interactable}");
                
                // Button의 Navigation 설정 (키보드/컨트롤러용, 호버에는 영향 없음)
                var navigation = itemButton.navigation;
                navigation.mode = UnityEngine.UI.Navigation.Mode.None;
                itemButton.navigation = navigation;
                
                // Button의 Transition을 None으로 설정 (커스텀 호버 효과 사용)
                itemButton.transition = UnityEngine.UI.Selectable.Transition.None;
                
                // 버튼 상태 변화 감지를 위한 커스텀 컴포넌트 추가
                var stateHandler = itemObj.AddComponent<DropdownItemStateHandler>();
                stateHandler.Initialize(capturedText, buttonImage);
                
                // 최종 아이템 크기 확인
                Canvas.ForceUpdateCanvases();
                Vector2 finalSize = itemRect != null ? itemRect.sizeDelta : Vector2.zero;
                float finalHeight = itemRect != null ? itemRect.rect.height : 0;
                
                Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] '{name}' 버튼 이벤트 연결 완료");
                Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] '{name}' StateHandler 추가 - Text: {(capturedText != null ? capturedText.text : "NULL")}, BgImage: {(buttonImage != null ? buttonImage.gameObject.name : "NULL")}");
                Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] '{name}' 최종 크기: sizeDelta={finalSize}, rect.height={finalHeight}");
            }
            else
            {
                Debug.LogWarning($"★★★ [ASSIGNEE_DROPDOWN] Button을 찾을 수 없음: '{name}'");
            }

            createdCount++;
        }

        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] {createdCount}/{assigneeNames.Count}개의 드롭다운 아이템 생성 완료");
        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] 최종 자식 수: {dropdownContent.childCount}");
        
        // Content 크기 확인
        if (dropdownContent is RectTransform contentRect)
        {
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] Content SizeDelta: {contentRect.sizeDelta}");
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] Content Rect: width={contentRect.rect.width:F2}, height={contentRect.rect.height:F2}");
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] Content Anchor: ({contentRect.anchorMin.x:F2},{contentRect.anchorMin.y:F2})-({contentRect.anchorMax.x:F2},{contentRect.anchorMax.y:F2})");
        }
        
        // 패널 높이를 아이템 총 높이에 맞게 동적 조정
        AdjustPanelHeight(dropdownPanel, dropdownContent);
        
        // ★★★ 핵심 수정: VerticalLayoutGroup 비활성화 후 수동으로 위치 설정
        // VerticalLayoutGroup이 활성화되어 있으면 anchor를 계속 (0,1)-(0,1)로 변경함
        var layoutGroupToDisable = dropdownContent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        if (layoutGroupToDisable != null)
        {
            layoutGroupToDisable.enabled = false;
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] VerticalLayoutGroup 비활성화 완료");
        }
        
        // Content의 너비 가져오기 (수동 위치 설정에 사용)
        RectTransform contentRectTransform = dropdownContent as RectTransform;
        float contentWidth = contentRectTransform != null ? contentRectTransform.rect.width : 940f;
        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] Content 너비: {contentWidth}");
        
        // 수동으로 아이템 위치 및 anchor 설정
        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] === 수동 위치 설정 시작 ===");
        float currentY = -panelPaddingTop; // 상단 패딩부터 시작
        
        for (int i = 0; i < dropdownContent.childCount; i++)
        {
            Transform child = dropdownContent.GetChild(i);
            RectTransform childRect = child.GetComponent<RectTransform>();
            
            if (childRect != null)
            {
                // Horizontal Stretch 모드로 설정
                childRect.anchorMin = new Vector2(0f, 1f);
                childRect.anchorMax = new Vector2(1f, 1f);
                childRect.pivot = new Vector2(0.5f, 1f);
                
                // 위치 수동 설정 (anchor가 stretch이므로 x=0이면 중앙 정렬)
                childRect.anchoredPosition = new Vector2(0f, currentY);
                
                // 크기 설정 (stretch 모드에서 sizeDelta.x=0은 부모 너비를 따름)
                childRect.sizeDelta = new Vector2(0f, itemHeight);
                
                Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] '{child.name}' 수동 설정: " +
                         $"position=(0, {currentY}), " +
                         $"anchor=(0,1)-(1,1), " +
                         $"sizeDelta=(0, {itemHeight})");
                
                currentY -= itemHeight; // 다음 아이템 위치 (간격 0)
            }
        }
        
        // 레이아웃 강제 업데이트
        Canvas.ForceUpdateCanvases();
        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] 수동 위치 설정 완료, 레이아웃 업데이트");
        
        // 최종 크기 확인
        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] === 최종 아이템 크기 확인 ===");
        for (int i = 0; i < dropdownContent.childCount; i++)
        {
            Transform child = dropdownContent.GetChild(i);
            RectTransform childRect = child.GetComponent<RectTransform>();
            var childLayout = child.GetComponent<UnityEngine.UI.LayoutElement>();
            
            if (childRect != null)
            {
                string layoutInfo = childLayout != null 
                    ? $"LayoutElement(preferred={childLayout.preferredHeight}, min={childLayout.minHeight}, flex={childLayout.flexibleHeight})" 
                    : "LayoutElement 없음";
                    
                Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] 아이템[{i}] '{child.name}': " +
                         $"position={childRect.anchoredPosition}, " +
                         $"sizeDelta=({childRect.sizeDelta.x:F2}, {childRect.sizeDelta.y:F2}), " +
                         $"rect=({childRect.rect.width:F2}, {childRect.rect.height:F2}), " +
                         $"anchor=({childRect.anchorMin.x:F2},{childRect.anchorMin.y:F2})-({childRect.anchorMax.x:F2},{childRect.anchorMax.y:F2}), " +
                         $"{layoutInfo}");
            }
        }
    }
    
    /// <summary>
    /// DropdownPanel의 높이를 아이템 총 높이에 맞게 조정
    /// </summary>
    private void AdjustPanelHeight(GameObject dropdownPanel, Transform dropdownContent)
    {
        if (dropdownPanel == null) return;
        
        RectTransform panelRect = dropdownPanel.GetComponent<RectTransform>();
        if (panelRect == null) return;
        
        // 총 높이 계산 (아이템 수 × 설정된 아이템 높이 + 상하 패딩)
        float itemsHeight = assigneeNames.Count * itemHeight;
        float totalHeight = itemsHeight + panelPaddingTop + panelPaddingBottom;
        
        // 패널 높이 조정
        Vector2 currentSize = panelRect.sizeDelta;
        panelRect.sizeDelta = new Vector2(currentSize.x, totalHeight);
        
        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] 패널 높이 조정: {currentSize.y} → {totalHeight} (아이템 {itemsHeight}px + 상단 {panelPaddingTop}px + 하단 {panelPaddingBottom}px)");
        
        // Content의 VerticalLayoutGroup padding 및 spacing 설정
        if (dropdownContent != null)
        {
            var layoutGroup = dropdownContent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (layoutGroup != null)
            {
                RectOffset newPadding = new RectOffset(
                    layoutGroup.padding.left,
                    layoutGroup.padding.right,
                    Mathf.RoundToInt(panelPaddingTop),
                    Mathf.RoundToInt(panelPaddingBottom)
                );
                layoutGroup.padding = newPadding;
                layoutGroup.spacing = 0f; // 아이템 간 간격 0으로 설정
                layoutGroup.childControlHeight = false; // 자식 높이를 LayoutElement가 제어하도록
                layoutGroup.childForceExpandHeight = false; // 자식을 강제로 확장하지 않음
                layoutGroup.childControlWidth = false; // 자식 너비도 LayoutElement가 제어
                layoutGroup.childForceExpandWidth = false; // 자식 너비를 강제로 확장하지 않음
                Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] VerticalLayoutGroup 설정: padding(top={panelPaddingTop}, bottom={panelPaddingBottom}), spacing=0, childControl=false");
            }
            else
            {
                Debug.LogWarning("★★★ [ASSIGNEE_DROPDOWN] VerticalLayoutGroup을 찾을 수 없음!");
            }
        }
        
        // 레이아웃 강제 업데이트 (여러 번 호출하여 확실히 적용)
        Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(dropdownContent as RectTransform);
        Canvas.ForceUpdateCanvases();
        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] 레이아웃 강제 업데이트 완료");
    }
    
    /// <summary>
    /// DropdownPanel의 Outline이 잘리는 문제 수정
    /// RoundedCornersImage와 Outline이 함께 사용될 때 Outline이 잘리는 현상 방지
    /// ScrollView가 BgImage의 둥근 모서리를 가리는 문제 해결
    /// </summary>
    private void FixDropdownPanelOutline(GameObject dropdownPanel)
    {
        if (dropdownPanel == null) return;
        
        // DropdownPanel의 Image 컴포넌트 찾기
        Image panelImage = dropdownPanel.GetComponent<Image>();
        if (panelImage == null) return;
        
        // Outline 컴포넌트 찾기
        Outline outline = dropdownPanel.GetComponent<Outline>();
        if (outline != null)
        {
            // Outline이 있으면 Shadow로 교체 (RoundedCornersImage와 호환성 더 좋음)
            Color outlineColor = outline.effectColor;
            Vector2 outlineDistance = outline.effectDistance;
            
            // Outline 제거
            DestroyImmediate(outline);
            
            // Shadow 추가 (거리를 더 크게 설정하여 더 잘 보이도록)
            Shadow shadow = dropdownPanel.AddComponent<Shadow>();
            shadow.effectColor = outlineColor;
            // effectDistance를 더 크게 설정 (기존보다 1.5배)
            shadow.effectDistance = new Vector2(
                outlineDistance.x * 1.5f, 
                outlineDistance.y * 1.5f
            );
            
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] DropdownPanel의 Outline을 Shadow로 교체 (distance: {shadow.effectDistance})");
        }
        
        // RoundedCornersImage 확인 및 Mask 컴포넌트 조정
        RoundedCornersImage rounded = panelImage.GetComponent<RoundedCornersImage>();
        if (rounded != null)
        {
            // Mask 컴포넌트가 Shadow를 잘라내지 않도록 확인
            Mask mask = dropdownPanel.GetComponent<Mask>();
            if (mask != null)
            {
                // showMaskGraphic을 true로 설정하여 Shadow가 보이도록
                if (mask.showMaskGraphic == false)
                {
                    mask.showMaskGraphic = true;
                    Debug.Log("★★★ [ASSIGNEE_DROPDOWN] Mask.showMaskGraphic을 true로 설정하여 Shadow 표시");
                }
            }
            
            Debug.Log("★★★ [ASSIGNEE_DROPDOWN] DropdownPanel에 RoundedCornersImage 적용 확인됨");
        }
        
        // ScrollView가 BgImage의 둥근 모서리를 가리는 문제 해결
        FixScrollViewClipping(dropdownPanel);
    }
    
    /// <summary>
    /// ScrollView가 DropdownPanel의 둥근 모서리(BgImage)를 가리지 않도록 설정
    /// Panel_TextMemo 구조를 참조: BgImage가 DropdownPanel을 꽉 채우고, ScrollView는 위에 렌더링되지만 BgImage를 가리지 않도록
    /// </summary>
    private void FixScrollViewClipping(GameObject dropdownPanel)
    {
        if (dropdownPanel == null) return;
        
        // BgImage 찾기 (DropdownPanel의 첫 번째 자식이어야 함)
        Transform bgImageTransform = null;
        if (dropdownPanel.transform.childCount > 0)
        {
            bgImageTransform = dropdownPanel.transform.GetChild(0);
            if (bgImageTransform.name != "BgImage")
            {
                Debug.LogWarning($"★★★ [ASSIGNEE_DROPDOWN] 첫 번째 자식이 BgImage가 아닙니다: {bgImageTransform.name}");
            }
        }
        
        // BgImage가 DropdownPanel을 완전히 채우도록 설정 (Panel_TextMemo 방식)
        if (bgImageTransform != null)
        {
            RectTransform bgRect = bgImageTransform.GetComponent<RectTransform>();
            Image bgImage = bgImageTransform.GetComponent<Image>();
            
            if (bgRect != null)
            {
                // Stretch 앵커로 부모를 완전히 채움
                bgRect.anchorMin = Vector2.zero;  // (0, 0)
                bgRect.anchorMax = Vector2.one;   // (1, 1)
                bgRect.sizeDelta = Vector2.zero;  // offset도 0
                bgRect.anchoredPosition = Vector2.zero;
                
                Debug.Log("★★★ [ASSIGNEE_DROPDOWN] BgImage가 DropdownPanel을 완전히 채우도록 설정 (Panel_TextMemo 방식)");
            }
            
            // BgImage에 Shadow가 있는지 확인
            if (bgImage != null)
            {
                Shadow bgShadow = bgImage.GetComponent<Shadow>();
                if (bgShadow == null)
                {
                    Debug.LogWarning("★★★ [ASSIGNEE_DROPDOWN] BgImage에 Shadow가 없습니다!");
                }
                else
                {
                    Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] BgImage Shadow 확인: color={bgShadow.effectColor}, distance={bgShadow.effectDistance}");
                }
                
                // BgImage에 RoundedCornersImage 확인
                RoundedCornersImage bgRounded = bgImage.GetComponent<RoundedCornersImage>();
                if (bgRounded == null)
                {
                    Debug.LogWarning("★★★ [ASSIGNEE_DROPDOWN] BgImage에 RoundedCornersImage가 없습니다!");
                }
                else
                {
                    var radius = bgRounded.GetRadius();
                    Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] BgImage RoundedCornersImage 확인: radius={radius.topLeft}");
                }
            }
        }
        
        // ScrollView 찾기 (두 번째 자식)
        UnityEngine.UI.ScrollRect scrollRect = dropdownPanel.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
        if (scrollRect == null)
        {
            Debug.LogWarning("★★★ [ASSIGNEE_DROPDOWN] ScrollView를 찾을 수 없음");
            return;
        }
        
        // ScrollView의 RectTransform도 stretch로 설정 (BgImage 위에 렌더링되지만 투명)
        RectTransform scrollRectTransform = scrollRect.GetComponent<RectTransform>();
        if (scrollRectTransform != null)
        {
            // ScrollView도 부모를 꽉 채우되, 내부 Content가 Mask로 잘림
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.sizeDelta = Vector2.zero;
            scrollRectTransform.anchoredPosition = Vector2.zero;
            
            Debug.Log("★★★ [ASSIGNEE_DROPDOWN] ScrollView도 DropdownPanel을 꽉 채우도록 설정");
            
            // ScrollView의 Image 컴포넌트들을 찾아서 올바른 설정 적용 (Panel_TextMemo 방식)
            Image[] scrollImages = scrollRect.GetComponents<Image>();
            if (scrollImages != null && scrollImages.Length > 0)
            {
                // Panel_TextMemo의 ScrollView는 첫 번째 Image가 투명(alpha=0)
                // 두 번째 Image가 불투명(alpha=1)이지만, 실제로는 첫 번째만 필요
                foreach (var scrollImage in scrollImages)
                {
                    // ScrollView의 Image는 투명하게 설정 (BgImage만 보이도록)
                    Color imgColor = scrollImage.color;
                    imgColor.a = 0f;  // 완전히 투명
                    scrollImage.color = imgColor;
                    scrollImage.raycastTarget = true;  // 클릭은 받아야 함
                    Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] ScrollView Image를 투명하게 설정 (alpha=0)");
                }
            }
        }
        
        // Viewport에 Mask 설정 확인 - 중요!
        if (scrollRect.viewport != null)
        {
            Mask viewportMask = scrollRect.viewport.GetComponent<Mask>();
            if (viewportMask == null)
            {
                Debug.LogWarning("★★★ [ASSIGNEE_DROPDOWN] Viewport에 Mask가 없습니다!");
            }
            else
            {
                viewportMask.showMaskGraphic = false;  // Viewport의 Mask 그래픽은 숨김 (투명)
                Debug.Log("★★★ [ASSIGNEE_DROPDOWN] Viewport Mask.showMaskGraphic = false 설정");
            }
            
            // Viewport Image 설정 (Panel_TextMemo 방식)
            Image viewportImage = scrollRect.viewport.GetComponent<Image>();
            if (viewportImage != null && bgImageTransform != null)
            {
                // Panel_TextMemo의 Viewport는 UISprite (Background)를 사용하고 Sliced 타입
                viewportImage.sprite = UnityEngine.Resources.Load<Sprite>("UI/Skin/Background");
                if (viewportImage.sprite == null)
                {
                    // Unity 기본 스프라이트 사용 (fileID: 10917)
                    viewportImage.sprite = UnityEngine.Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
                }
                viewportImage.type = Image.Type.Sliced;
                viewportImage.color = Color.white;
                Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] Viewport Image 설정: sprite={viewportImage.sprite?.name}, type=Sliced");
                
                // Viewport에 RoundedCornersImage 추가하여 내용물도 둥글게 잘림 (Panel_TextMemo 방식)
                Image bgImage = bgImageTransform.GetComponent<Image>();
                RoundedCornersImage bgRounded = bgImage?.GetComponent<RoundedCornersImage>();
                
                if (bgRounded != null)
                {
                    RoundedCornersImage viewportRounded = viewportImage.GetComponent<RoundedCornersImage>();
                    if (viewportRounded == null)
                    {
                        viewportRounded = viewportImage.gameObject.AddComponent<RoundedCornersImage>();
                        var radius = bgRounded.GetRadius();
                        viewportRounded.SetRadius(radius.topLeft, radius.topRight, radius.bottomRight, radius.bottomLeft);
                        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] Viewport에 RoundedCornersImage 추가 (BgImage와 동일한 radius: {radius.topLeft})");
                    }
                }
            }
        }
    }

    private void OnSelectAssignee(string name)
    {
        SetActiveSelectedAssignee(name);
        TMP_Text selectedNameText = GetActiveSelectedNameText();

        // 선택된 이름 표시
        if (selectedNameText != null)
        {
            selectedNameText.text = name;
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] 선택된 이름 표시: {name}");
        }
        
        // Shadow 색상 업데이트 (담당자가 선택됨)
        UpdateButtonShadowColor(true);

        // 드롭다운 닫기
        GameObject dropdownPanel = GetActiveDropdownPanel();
        if (dropdownPanel != null)
        {
            SetActiveIsDropdownOpen(false);
            dropdownPanel.SetActive(false);
            
            // 모든 아이템 색상 리셋 (모바일 터치 누적 방지)
            DropdownItemStateHandler.ResetAllItems();

            // 버튼 외형을 닫힌 상태로 업데이트
            UpdateButtonAppearance(false);
            
            // InputField 위치 복원
            UpdateInputFieldPositions(false);
            
            // 현재 활성화된 패널에 따라 ScrollContent 복원
            RectTransform activeScrollContent = GetActiveScrollContent();
            UnityEngine.UI.ScrollRect activeScrollRect = GetActiveScrollRect();
            GetActiveOriginalValues(out float scrollHeightOrig, out Vector2 scrollPosOrig);
            
            // ScrollContent 높이 및 위치 복원
            if (activeScrollContent != null)
            {
                Vector2 newSize = activeScrollContent.sizeDelta;
                newSize.y = scrollHeightOrig;
                activeScrollContent.sizeDelta = newSize;
                activeScrollContent.anchoredPosition = scrollPosOrig;
            }
            
            // 스크롤 위치 복원
            if (activeScrollRect != null)
            {
                StartCoroutine(AdjustScrollPosition(activeScrollRect, 1f)); // 1 = 맨 위
            }
            
            // ImageMemoUIController에 드롭다운 닫힘 알림 (ImageMemo 패널일 때만)
            if (IsImageMemoPanelActive() && imageMemoUIController != null)
            {
                imageMemoUIController.OnDropdownClosed();
            }
        }

        // 현재 메모에 담당자 저장
        if (!string.IsNullOrEmpty(currentMemoId))
        {
            TabPinCreate tabPinCreate = FindObjectOfType<TabPinCreate>();
            if (tabPinCreate != null)
            {
                tabPinCreate.UpdateMemoAssignee(currentMemoId, name);
                // isAssigned도 true로 설정
                tabPinCreate.UpdateMemoAssignedState(currentMemoId, true);
            }
        }

        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] 담당자 선택 완료: {name}, memoId={currentMemoId}");
    }

    /// <summary>
    /// 현재 편집 중인 메모 ID 설정 (TabPinCreate에서 호출)
    /// </summary>
    public static void SetCurrentMemoId(string memoId)
    {
        currentMemoId = memoId;
        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] 현재 메모 ID 설정: {memoId}");
    }

    /// <summary>
    /// 저장된 담당자 이름 불러오기 (MemoUIController에서 호출)
    /// </summary>
    public void LoadAssignee(string assigneeName)
    {
        SetActiveSelectedAssignee(assigneeName ?? "");
        string selectedAssignee = GetActiveSelectedAssignee();
        TMP_Text selectedNameText = GetActiveSelectedNameText();

        if (selectedNameText != null)
        {
            // 저장된 이름이 있으면 표시, 없으면 기본 텍스트
            string displayText = string.IsNullOrEmpty(selectedAssignee) ? "처리 대상자 선택" : selectedAssignee;
            selectedNameText.text = displayText;

            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] 담당자 불러오기: '{assigneeName}' → 표시: '{displayText}'");
        }
        else
        {
            Debug.LogWarning("★★★ [ASSIGNEE_DROPDOWN] selectedNameText가 null입니다!");
        }
        
        // Shadow 색상 업데이트 (담당자가 있으면 채워진 색상, 없으면 빈 색상)
        bool hasFilled = !string.IsNullOrEmpty(selectedAssignee);
        UpdateButtonShadowColor(hasFilled);

        // 드롭다운이 열려있으면 닫기
        GameObject dropdownPanel = GetActiveDropdownPanel();
        if (dropdownPanel != null && dropdownPanel.activeSelf)
        {
            SetActiveIsDropdownOpen(false);
            dropdownPanel.SetActive(false);
            
            // InputField 위치 복원
            UpdateInputFieldPositions(false);
            
            // 현재 활성화된 패널에 따라 ScrollContent 복원
            RectTransform activeScrollContent = GetActiveScrollContent();
            UnityEngine.UI.ScrollRect activeScrollRect = GetActiveScrollRect();
            GetActiveOriginalValues(out float scrollHeightOrig, out Vector2 scrollPosOrig);
            
            // ScrollContent 높이 및 위치 복원
            if (activeScrollContent != null)
            {
                Vector2 newSize = activeScrollContent.sizeDelta;
                newSize.y = scrollHeightOrig;
                activeScrollContent.sizeDelta = newSize;
                activeScrollContent.anchoredPosition = scrollPosOrig;
            }
            
            // 스크롤 위치 복원
            if (activeScrollRect != null)
            {
                StartCoroutine(AdjustScrollPosition(activeScrollRect, 1f)); // 1 = 맨 위
            }
            
            // ImageMemoUIController에 드롭다운 닫힘 알림 (ImageMemo 패널일 때만)
            if (IsImageMemoPanelActive() && imageMemoUIController != null)
            {
                imageMemoUIController.OnDropdownClosed();
            }
        }

        // 버튼 외형을 닫힌 상태로 업데이트
        UpdateButtonAppearance(false);
    }

    /// <summary>
    /// VoiceMemo 패널의 스크롤 하단 제한 적용
    /// </summary>
    private void LateUpdate()
    {
        // VoiceMemo 패널이 활성화되어 있고, 드롭다운이 열려있을 때만 적용
        if (!IsVoiceMemoPanelActive() || !voiceMemoIsDropdownOpen)
            return;
        
        // voiceMemoScrollBottomLimit가 설정되어 있을 때만 적용
        if (voiceMemoScrollRect != null && voiceMemoScrollBottomLimit > 0f)
        {
            // verticalNormalizedPosition: 1 = 맨위, 0 = 맨아래
            // scrollBottomLimit가 0.3이면 맨아래 30% 지점까지만 스크롤 가능
            if (voiceMemoScrollRect.verticalNormalizedPosition < voiceMemoScrollBottomLimit)
            {
                voiceMemoScrollRect.verticalNormalizedPosition = voiceMemoScrollBottomLimit;
            }
        }
    }

    private void OnDestroy()
    {
        // 모든 패널의 버튼 이벤트 제거
        if (textMemoAssigneeButton != null)
        {
            textMemoAssigneeButton.onClick.RemoveListener(ToggleDropdown);
        }
        
        if (imageMemoAssigneeButton != null)
        {
            imageMemoAssigneeButton.onClick.RemoveListener(ToggleDropdown);
        }
        
        if (checklistAssigneeButton != null)
        {
            checklistAssigneeButton.onClick.RemoveListener(ToggleDropdown);
        }
        
        if (voiceMemoAssigneeButton != null)
        {
            voiceMemoAssigneeButton.onClick.RemoveListener(ToggleDropdown);
        }
    }
    
    /// <summary>
    /// UI 요소 위치 애니메이션
    /// </summary>
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
    
    /// <summary>
    /// 스크롤 위치 조정 (애니메이션 완료 후 실행)
    /// </summary>
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
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] 스크롤 위치 조정: {targetPosition}, Content 높이: {(contentRect != null ? contentRect.sizeDelta.y.ToString() : "null")}");
        }
        else
        {
            Debug.LogWarning("★★★ [ASSIGNEE_DROPDOWN] ScrollRect가 할당되지 않았습니다!");
        }
    }
}

/// <summary>
/// 드롭다운 아이템의 호버/선택 상태에 따라 배경과 텍스트 색상을 변경하는 헬퍼 컴포넌트
/// </summary>
public class DropdownItemStateHandler : MonoBehaviour, 
    UnityEngine.EventSystems.IPointerEnterHandler, 
    UnityEngine.EventSystems.IPointerExitHandler,
    UnityEngine.EventSystems.IPointerDownHandler,
    UnityEngine.EventSystems.IPointerUpHandler
{
    private TMP_Text textComponent;
    private Image backgroundImage;
    private Color normalTextColor = new Color(0.19607843f, 0.19607843f, 0.19607843f); // #323232
    private Color highlightedTextColor = Color.white;
    private Color normalBgColor = new Color(1f, 1f, 1f, 0f); // 투명
    private Color highlightedBgColor = new Color(0.5882353f, 0.79607844f, 0.8784314f, 1f); // #96CBE0
    private bool isPointerOver = false;
    
    // 모든 핸들러 인스턴스 관리 (다른 아이템 리셋용)
    private static List<DropdownItemStateHandler> allHandlers = new List<DropdownItemStateHandler>();

    public void Initialize(TMP_Text text, Image background)
    {
        textComponent = text;
        backgroundImage = background;
        
        Debug.Log($"★★★ [DropdownItemStateHandler] Initialize - GameObject: {gameObject.name}");
        Debug.Log($"★★★ [DropdownItemStateHandler] Text: {(text != null ? text.text : "NULL")}, Background: {(background != null ? background.gameObject.name : "NULL")}");
        
        // 핸들러 리스트에 등록
        if (!allHandlers.Contains(this))
            allHandlers.Add(this);
        
        // 초기 배경 색상을 투명으로 설정
        if (backgroundImage != null)
        {
            // Sprite는 유지 (프리팹에 설정된 스프라이트 사용)
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.color = normalBgColor;
            backgroundImage.raycastTarget = true; // Raycast 활성화
            Debug.Log($"★★★ [DropdownItemStateHandler] 배경 초기화 완료 - color: {backgroundImage.color}, raycastTarget: {backgroundImage.raycastTarget}");
        }
        else
        {
            Debug.LogWarning($"★★★ [DropdownItemStateHandler] backgroundImage가 NULL입니다!");
        }
    }
    
    private void OnDestroy()
    {
        // 핸들러 리스트에서 제거
        allHandlers.Remove(this);
    }

    private void SetColors(bool highlighted)
    {
        if (textComponent != null)
        {
            Color textColor = highlighted ? highlightedTextColor : normalTextColor;
            textComponent.color = textColor;
            Debug.Log($"★★★ [DropdownItemStateHandler] SetColors - Text: '{textComponent.text}' color = {textColor}");
        }
        else
        {
            Debug.LogWarning($"★★★ [DropdownItemStateHandler] SetColors - textComponent is NULL!");
        }
        
        if (backgroundImage != null)
        {
            Color targetColor = highlighted ? highlightedBgColor : normalBgColor;
            backgroundImage.color = targetColor;
            Debug.Log($"★★★ [DropdownItemStateHandler] SetColors - BgImage: '{backgroundImage.gameObject.name}' color = {targetColor}, highlighted={highlighted}");
        }
        else
        {
            Debug.LogWarning($"★★★ [DropdownItemStateHandler] SetColors - backgroundImage is NULL!");
        }
    }
    
    /// <summary>
    /// 이 아이템을 제외한 모든 아이템의 색상을 리셋
    /// </summary>
    private void ResetOtherItems()
    {
        foreach (var handler in allHandlers)
        {
            if (handler != null && handler != this)
            {
                handler.ForceReset();
            }
        }
    }
    
    /// <summary>
    /// 외부에서 강제로 색상 리셋 (다른 아이템 선택 시 호출)
    /// </summary>
    public void ForceReset()
    {
        isPointerOver = false;
        SetColors(false);
    }
    
    /// <summary>
    /// 모든 아이템의 색상을 리셋 (드롭다운 닫힐 때 호출)
    /// </summary>
    public static void ResetAllItems()
    {
        foreach (var handler in allHandlers)
        {
            if (handler != null)
            {
                handler.ForceReset();
            }
        }
    }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
    {
        Debug.Log($"★★★ [DropdownItemStateHandler] OnPointerEnter - {gameObject.name}");
        isPointerOver = true;
        ResetOtherItems(); // 다른 아이템 리셋
        SetColors(true);
        Debug.Log($"★★★ [DropdownItemStateHandler] 색상 변경 완료 - Text: {(textComponent != null ? textComponent.color.ToString() : "NULL")}, Bg: {(backgroundImage != null ? backgroundImage.color.ToString() : "NULL")}");
    }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
    {
        Debug.Log($"★★★ [DropdownItemStateHandler] OnPointerExit - {gameObject.name}");
        isPointerOver = false;
        SetColors(false);
    }

    public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
    {
        ResetOtherItems(); // 다른 아이템 리셋 (모바일 터치용)
        SetColors(true);
    }

    public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData)
    {
        // 모바일에서는 PointerUp 후 바로 리셋
        // PC에서는 isPointerOver 상태에 따라 결정
        #if UNITY_ANDROID || UNITY_IOS
        SetColors(false);
        #else
        SetColors(isPointerOver);
        #endif
    }
}
