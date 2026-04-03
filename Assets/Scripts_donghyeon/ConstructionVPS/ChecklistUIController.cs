using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ChecklistUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField inputTitle;
    [SerializeField] private TMP_InputField inputLocation;
    [SerializeField] private ScrollRect scrollView; // ScrollView의 ScrollRect 컴포넌트
    [SerializeField] private RectTransform checklistContent; // ScrollView의 Content
    [SerializeField] private GameObject checklistItemPrefab;
    [SerializeField] private Button addItemButton;
    [SerializeField] private Button btnSave;
    [SerializeField] private Button btnClose;
    
    [Header("Layout References (메모 추가 시 밀릴 요소들)")]
    [SerializeField] private RectTransform checklistDeadline;       // ChecklistDeadline RectTransform
    [SerializeField] private RectTransform checklistEmergency;      // ChecklistEmergency RectTransform
    [SerializeField] private RectTransform calendarPanelRect;       // ChecklistCalendarPanel RectTransform
    [SerializeField] private RectTransform timePickerPanelRect;     // ChecklistTimePickerPanel RectTransform
    
    [Header("Panel References (Calendar/TimePicker 열림 상태 체크용)")]
    [SerializeField] private GameObject calendarPanel;   // ChecklistCalendarPanel (GameObject)
    [SerializeField] private GameObject timePickerPanel; // ChecklistTimePickerPanel (GameObject)
    
    [Header("Controller References (레이아웃 재계산용)")]
    [SerializeField] private CalendarController calendarController;
    [SerializeField] private TimePickerController timePickerController;
    [SerializeField] private MemoUIController memoUIController;
    
    [Header("Emergency & TabPinCreate")]
    [Tooltip("긴급도 버튼을 관리하는 EmergencyButtonManager를 넣는 자리")]
    [SerializeField] private EmergencyButtonManager emergencyButtonManager;
    [Tooltip("TabPinCreate를 넣는 자리 (JSON 저장 갱신용)")]
    [SerializeField] private TabPinCreate pinStore;
    
    [Header("Settings")]
    [SerializeField] private int maxItems = 10; // 최대 10개
    [SerializeField] private int minVisibleSlots = 2; // 화면에 항상 보이는 최소 슬롯 수
    
    [Header("InputField Outlines")]
    [Tooltip("ChecklistInputField_Title의 Outline 컴포넌트")]
    [SerializeField] private Outline titleOutline;
    [Tooltip("ChecklistInputField_Location의 Outline 컴포넌트")]
    [SerializeField] private Outline locationOutline;
    
    [Header("Outline Colors")]
    [SerializeField] private Color emptyOutlineColor = new Color(0x96 / 255f, 0xCB / 255f, 0xE0 / 255f); // #96CBE0 (비어있을 때)
    [SerializeField] private Color filledOutlineColor = new Color(0xD9 / 255f, 0xD9 / 255f, 0xD9 / 255f); // #D9D9D9 (채워졌을 때)
    
    [Header("Layout Push Settings (메모 추가 시 레이아웃 조정)")]
    [Tooltip("이 개수까지만 Deadline/Emergency가 밀림 (기본 슬롯 포함)")]
    [SerializeField] private int maxPushItems = 4; // 4개까지만 밀림 적용
    [Tooltip("메모 1개 추가당 밀리는 양")]
    [SerializeField] private float pushAmountPerItem = 130f;
    
    private List<GameObject> checklistItems = new List<GameObject>();
    
    // 현재 편집 중인 메모 GameObject
    private GameObject currentMemo;
    
    // 원래 위치 저장
    private Vector2 deadlineOriginalPos;
    private Vector2 emergencyOriginalPos;
    private Vector2 calendarPanelOriginalPos;
    private Vector2 timePickerPanelOriginalPos;
    private bool isOriginalPositionsSaved = false;
    
    // ★★★ 마지막으로 적용된 밀림 양 저장 (추가/삭제 시 변화량 계산용) ★★★
    private float lastPushAmount = 0f;
    
    // ★★★ Content 원래 높이 저장 ★★★
    private float contentOriginalHeight = 0f;
    
    // ★★★ Content 확장량 (아이템당 130px) ★★★
    [Header("Content Expansion Settings")]
    [SerializeField] private float contentExpansionPerItem = 130f;
    
    // ★★★ 마지막으로 적용된 Content 확장량 ★★★
    private float lastContentExpansion = 0f;
    
    // ★★★ CheckList 영역 (밀리지 않음) ★★★
    [Header("CheckList Area")]
    [SerializeField] private RectTransform checklistCheckList;
    private Vector2 checkListOriginalPos;
    
    [Header("Delete Button Hide Settings (닫기 버튼 근처에서 삭제 버튼 숨김)")]
    [Tooltip("ChecklistBtn_TextClose의 RectTransform (닫기 버튼)")]
    [SerializeField] private RectTransform checklistBtnTextCloseRect;
    [Tooltip("이 반경 안에 들어오면 삭제 버튼 숨김 및 아이템 너비 축소 (픽셀 단위)")]
    [SerializeField] private float deleteButtonHideRadius = 500f;
    [Tooltip("이 반경 밖으로 나가면 삭제 버튼 표시 및 아이템 너비 복원 (깜빡임 방지용, hideRadius보다 커야 함)")]
    [SerializeField] private float deleteButtonShowRadius = 550f;
    [Tooltip("닫기 버튼 근처에서 아이템 너비")]
    [SerializeField] private float itemCompactWidth = 778f;
    [Tooltip("아이템 기본 너비")]
    [SerializeField] private float itemOriginalWidth = 850f;
    
    // ★★★ 외부에서 접근할 수 있는 프로퍼티 ★★★
    public float LastPushAmount => lastPushAmount;
    public float LastContentExpansion => lastContentExpansion;
    
    void Awake()
    {
        // ★★★ 자동 할당: Inspector에서 할당되지 않은 경우 이름으로 찾기 ★★★
        AutoAssignReferences();
        
        // 원래 위치 저장 (OnEnable보다 먼저 호출됨)
        SaveOriginalPositions();
    }
    
    /// <summary>
    /// Inspector에서 할당되지 않은 참조들을 자동으로 찾아서 할당
    /// </summary>
    void AutoAssignReferences()
    {
        // ChecklistDeadline 자동 할당
        if (checklistDeadline == null)
        {
            GameObject deadlineObj = GameObject.Find("ChecklistDeadline");
            if (deadlineObj != null)
            {
                checklistDeadline = deadlineObj.GetComponent<RectTransform>();
                Debug.Log("[ChecklistUIController] ChecklistDeadline 자동 할당 완료");
            }
        }
        
        // ChecklistEmergency 자동 할당
        if (checklistEmergency == null)
        {
            GameObject emergencyObj = GameObject.Find("ChecklistEmergency");
            if (emergencyObj != null)
            {
                checklistEmergency = emergencyObj.GetComponent<RectTransform>();
                Debug.Log("[ChecklistUIController] ChecklistEmergency 자동 할당 완료");
            }
        }
        
        // ChecklistCalendarPanel 자동 할당
        if (calendarPanelRect == null)
        {
            GameObject calendarObj = GameObject.Find("ChecklistCalendarPanel");
            if (calendarObj != null)
            {
                calendarPanelRect = calendarObj.GetComponent<RectTransform>();
                calendarPanel = calendarObj;
                Debug.Log("[ChecklistUIController] ChecklistCalendarPanel 자동 할당 완료");
            }
        }
        
        // ChecklistTimePickerPanel 자동 할당
        if (timePickerPanelRect == null)
        {
            GameObject timePickerObj = GameObject.Find("ChecklistTimePickerPanel");
            if (timePickerObj != null)
            {
                timePickerPanelRect = timePickerObj.GetComponent<RectTransform>();
                timePickerPanel = timePickerObj;
                Debug.Log("[ChecklistUIController] ChecklistTimePickerPanel 자동 할당 완료");
            }
        }
        
        // CheckList 자동 할당 (체크리스트 메모 영역)
        if (checklistCheckList == null)
        {
            GameObject checkListObj = GameObject.Find("CheckList");
            if (checkListObj != null)
            {
                checklistCheckList = checkListObj.GetComponent<RectTransform>();
                Debug.Log("[ChecklistUIController] CheckList 자동 할당 완료");
            }
        }
        
        // ★★★ ChecklistBtn_TextClose 자동 할당 (삭제 버튼 숨김용) ★★★
        if (checklistBtnTextCloseRect == null)
        {
            GameObject closeObj = GameObject.Find("ChecklistBtn_TextClose");
            if (closeObj != null)
            {
                checklistBtnTextCloseRect = closeObj.GetComponent<RectTransform>();
                Debug.Log("[ChecklistUIController] ChecklistBtn_TextClose 자동 할당 완료");
            }
        }
        
        // 할당 결과 로그
        Debug.Log($"[ChecklistUIController] 참조 상태 - Deadline: {(checklistDeadline != null ? "OK" : "NULL")}, Emergency: {(checklistEmergency != null ? "OK" : "NULL")}, CalendarPanel: {(calendarPanelRect != null ? "OK" : "NULL")}, TimePickerPanel: {(timePickerPanelRect != null ? "OK" : "NULL")}, TextClose: {(checklistBtnTextCloseRect != null ? "OK" : "NULL")}");
    }
    
    void Start()
    {
        // 버튼 이벤트 연결
        if (addItemButton != null)
            addItemButton.onClick.AddListener(OnAddItemClicked);
        if (btnSave != null)
            btnSave.onClick.AddListener(OnSaveClicked);
        if (btnClose != null)
            btnClose.onClick.AddListener(OnCloseClicked);
        
        // ★★★ CalendarController/TimePickerController 자동 할당 (Inspector에서 할당되지 않은 경우) ★★★
        if (calendarController == null)
        {
            calendarController = FindObjectOfType<CalendarController>();
            if (calendarController != null)
            {
                Debug.Log("[ChecklistUIController] CalendarController를 자동으로 찾았습니다.");
            }
        }
        if (timePickerController == null)
        {
            timePickerController = FindObjectOfType<TimePickerController>();
            if (timePickerController != null)
            {
                Debug.Log("[ChecklistUIController] TimePickerController를 자동으로 찾았습니다.");
            }
        }
        if (memoUIController == null)
        {
            memoUIController = FindObjectOfType<MemoUIController>();
            if (memoUIController != null)
            {
                Debug.Log("[ChecklistUIController] MemoUIController를 자동으로 찾았습니다.");
            }
        }
        if (emergencyButtonManager == null)
        {
            emergencyButtonManager = FindObjectOfType<EmergencyButtonManager>();
            if (emergencyButtonManager != null)
            {
                Debug.Log("[ChecklistUIController] EmergencyButtonManager를 자동으로 찾았습니다.");
            }
        }
        if (pinStore == null)
        {
            pinStore = FindObjectOfType<TabPinCreate>();
            if (pinStore != null)
            {
                Debug.Log("[ChecklistUIController] TabPinCreate를 자동으로 찾았습니다.");
            }
        }
        
        // ★★★ Outline 컴포넌트 자동 검색 (자체 -> 자식 -> 부모 순서) ★★★
        if (titleOutline == null && inputTitle != null)
        {
            // 1. InputField 자체에서 검색
            titleOutline = inputTitle.GetComponent<Outline>();
            // 2. 자식에서 검색
            if (titleOutline == null)
            {
                titleOutline = inputTitle.GetComponentInChildren<Outline>();
            }
            // 3. 부모에서 검색 (InputField가 다른 오브젝트의 자식일 수 있음)
            if (titleOutline == null && inputTitle.transform.parent != null)
            {
                titleOutline = inputTitle.transform.parent.GetComponent<Outline>();
            }
            Debug.Log($"[ChecklistUIController] titleOutline 자동 검색 결과: {(titleOutline != null ? titleOutline.gameObject.name : "NULL")}");
        }
        if (locationOutline == null && inputLocation != null)
        {
            // 1. InputField 자체에서 검색
            locationOutline = inputLocation.GetComponent<Outline>();
            // 2. 자식에서 검색
            if (locationOutline == null)
            {
                locationOutline = inputLocation.GetComponentInChildren<Outline>();
            }
            // 3. 부모에서 검색
            if (locationOutline == null && inputLocation.transform.parent != null)
            {
                locationOutline = inputLocation.transform.parent.GetComponent<Outline>();
            }
            Debug.Log($"[ChecklistUIController] locationOutline 자동 검색 결과: {(locationOutline != null ? locationOutline.gameObject.name : "NULL")}");
        }
        
        // ★★★ InputField 텍스트 변경 이벤트 연결 (Outline 색상 업데이트용) ★★★
        if (inputTitle != null)
        {
            inputTitle.onValueChanged.AddListener(OnTitleTextChanged);
        }
        if (inputLocation != null)
        {
            inputLocation.onValueChanged.AddListener(OnLocationTextChanged);
        }
        
        // 초기 상태 확인
        Debug.Log($"[ChecklistUIController] Start - ScrollView: {(scrollView != null ? "연결됨" : "NULL")}, Content: {(checklistContent != null ? "연결됨" : "NULL")}, Prefab: {(checklistItemPrefab != null ? "연결됨" : "NULL")}");
        Debug.Log($"[ChecklistUIController] Deadline 원래 위치: {deadlineOriginalPos}, Emergency 원래 위치: {emergencyOriginalPos}");
        Debug.Log($"[ChecklistUIController] Outline 할당 상태 - titleOutline: {(titleOutline != null ? "할당됨" : "NULL")}, locationOutline: {(locationOutline != null ? "할당됨" : "NULL")}");
    }
    
    /// <summary>
    /// 원래 위치 저장 (한 번만 실행)
    /// </summary>
    void SaveOriginalPositions()
    {
        if (isOriginalPositionsSaved) return;
        
        if (checklistDeadline != null)
            deadlineOriginalPos = checklistDeadline.anchoredPosition;
        if (checklistEmergency != null)
            emergencyOriginalPos = checklistEmergency.anchoredPosition;
        if (calendarPanelRect != null)
            calendarPanelOriginalPos = calendarPanelRect.anchoredPosition;
        if (timePickerPanelRect != null)
            timePickerPanelOriginalPos = timePickerPanelRect.anchoredPosition;
        if (checklistCheckList != null)
            checkListOriginalPos = checklistCheckList.anchoredPosition;
        
        // Content 원래 높이 저장
        if (checklistContent != null)
        {
            contentOriginalHeight = checklistContent.sizeDelta.y;
            Debug.Log($"[ChecklistUIController] Content 원래 높이 저장됨: {contentOriginalHeight}");
        }
        
        isOriginalPositionsSaved = true;
        Debug.Log($"[ChecklistUIController] 원래 위치 저장 완료 - Deadline: {deadlineOriginalPos}, Emergency: {emergencyOriginalPos}, ContentHeight: {contentOriginalHeight}");
    }
    
    void OnEnable()
    {
        // ★★★ 참조가 할당되지 않았을 수 있으므로 먼저 자동 할당 시도 ★★★
        if (checklistDeadline == null || checklistEmergency == null)
        {
            AutoAssignReferences();
        }
        
        // 패널이 활성화될 때 원래 위치 저장
        SaveOriginalPositions();
        
        // ★★★ 패널이 활성화될 때마다 초기화 (복원됨) ★★★
        InitializePanel();
        
        Debug.Log($"[ChecklistUIController] OnEnable - Deadline: {(checklistDeadline != null ? "OK" : "NULL")}, deadlineOriginalPos: {deadlineOriginalPos}");
    }
    
    void InitializePanel()
    {
        // Content 안의 기존 체크리스트 아이템들을 정리 (AddItemButton 제외)
        if (checklistContent != null)
        {
            List<Transform> itemsToRemove = new List<Transform>();
            
            foreach (Transform child in checklistContent)
            {
                // AddItemButton이 아닌 자식들만 삭제 대상에 추가
                if (addItemButton != null && child.gameObject != addItemButton.gameObject)
                {
                    itemsToRemove.Add(child);
                }
            }
            
            // 리스트에 추가된 아이템들 삭제
            foreach (Transform item in itemsToRemove)
            {
                Destroy(item.gameObject);
            }
            
            Debug.Log($"[ChecklistUIController] 기존 아이템 {itemsToRemove.Count}개 정리 완료");
        }
        
        // 체크리스트 아이템 리스트 초기화
        checklistItems.Clear();
        
        // ★★★ 레이아웃 상태 변수 먼저 리셋 (ResetLayoutPositions에서도 하지만 명시적으로) ★★★
        lastPushAmount = 0f;
        lastContentExpansion = 0f;
        
        // 레이아웃 원래 위치로 복원
        ResetLayoutPositions();
        
        // 최소 슬롯 수만큼 빈 아이템 생성 (레이아웃 업데이트 건너뜀 - 2개는 밀림 없음)
        CreateMinimumSlots(skipLayoutUpdate: true);
        
        // 초기에는 추가 버튼이 표시되어야 함
        if (addItemButton != null)
        {
            addItemButton.gameObject.SetActive(true);
            addItemButton.transform.SetAsLastSibling();
        }
        
        // 스크롤을 맨 위로 초기화
        if (scrollView != null)
        {
            scrollView.verticalNormalizedPosition = 1f; // 1 = 맨 위
        }
        
        // ★★★ InputField 초기화 및 Outline 색상 초기화 ★★★
        if (inputTitle != null)
            inputTitle.text = "";
        if (inputLocation != null)
            inputLocation.text = "";
        InitializeOutlineColors();
    }
    
    /// <summary>
    /// 패널이 열릴 때 MemoUIController에서 호출 (currentMemo 전달)
    /// </summary>
    public void OnPanelOpened(GameObject memo)
    {
        Debug.Log($"[ChecklistUIController] OnPanelOpened 호출: memo={(memo != null ? memo.name : "null")}");
        
        currentMemo = memo;
        
        // 저장된 체크리스트 데이터 로드
        if (currentMemo != null)
        {
            MemoData memoData = currentMemo.GetComponent<MemoData>();
            if (memoData != null)
            {
                // body를 줄바꿈으로 분리해서 체크리스트 아이템으로 로드
                List<string> loadedItems = new List<string>();
                if (!string.IsNullOrEmpty(memoData.body))
                {
                    string[] items = memoData.body.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
                    loadedItems = new List<string>(items);
                }
                
                // 체크리스트 로드 (LoadChecklist가 제목/위치도 설정함)
                LoadChecklist(memoData.title ?? "", memoData.location ?? "", loadedItems);
                
                Debug.Log($"[ChecklistUIController] 저장된 체크리스트 로드: 제목={memoData.title}, 위치={memoData.location}, 아이템 수={loadedItems.Count}");
            }
            else
            {
                Debug.LogWarning("[ChecklistUIController] MemoData가 없습니다! - 빈 상태로 초기화");
                // MemoData가 없어도 빈 상태로 초기화
                LoadChecklist("", "", new List<string>());
            }
        }
        else
        {
            Debug.LogWarning("[ChecklistUIController] currentMemo가 null! - 빈 상태로 초기화");
            // currentMemo가 null이어도 빈 상태로 초기화
            LoadChecklist("", "", new List<string>());
        }
    }
    
    /// <summary>
    /// 최소 슬롯 수만큼 빈 체크리스트 아이템 생성
    /// </summary>
    /// <param name="skipLayoutUpdate">true면 레이아웃 업데이트 건너뜀 (초기화 시)</param>
    void CreateMinimumSlots(bool skipLayoutUpdate = false)
    {
        for (int i = 0; i < minVisibleSlots; i++)
        {
            CreateChecklistItem("", skipLayoutUpdate);
        }
        Debug.Log($"[ChecklistUIController] 최소 슬롯 {minVisibleSlots}개 생성 완료 (skipLayoutUpdate: {skipLayoutUpdate})");
    }
    
    void OnAddItemClicked()
    {
        Debug.Log($"[ChecklistUIController] Add Item 버튼 클릭됨! 현재 아이템 수: {checklistItems.Count}");
        
        if (checklistItems.Count >= maxItems)
        {
            Debug.Log("최대 10개까지만 추가 가능합니다.");
            return;
        }
        
        // 빈 아이템 생성
        CreateChecklistItem("");
        
        // 10개 도달 시 추가 버튼 숨기기
        if (checklistItems.Count >= maxItems)
        {
            addItemButton.gameObject.SetActive(false);
            Debug.Log("[ChecklistUIController] 10개 도달 - 추가 버튼 숨김");
        }
        
        Debug.Log($"[ChecklistUIController] 총 아이템 수: {checklistItems.Count}");
        
        // 스크롤을 맨 아래로 이동 (추가 버튼이 항상 5번째 자리에 보이도록)
        StartCoroutine(ScrollToBottomDelayed());
    }
    
    /// <summary>
    /// 체크리스트 아이템 생성 (공통 메서드)
    /// </summary>
    /// <param name="content">아이템 내용</param>
    /// <param name="skipLayoutUpdate">true면 레이아웃 업데이트 건너뜀 (초기화/클리어 시)</param>
    GameObject CreateChecklistItem(string content, bool skipLayoutUpdate = false)
    {
        // Null 체크
        if (checklistContent == null)
        {
            Debug.LogError("[ChecklistUIController] checklistContent가 null입니다! Inspector에서 Content를 연결해주세요.");
            return null;
        }
        
        if (checklistItemPrefab == null)
        {
            Debug.LogError("[ChecklistUIController] checklistItemPrefab가 null입니다! Inspector에서 프리팹을 연결해주세요.");
            return null;
        }
        
        // 체크리스트 아이템 생성
        GameObject newItem = Instantiate(checklistItemPrefab, checklistContent);
        newItem.SetActive(true);
        
        // 높이 강제 설정
        RectTransform itemRect = newItem.GetComponent<RectTransform>();
        if (itemRect != null)
        {
            itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, 130f);
        }
        
        // Layout Element 추가/설정
        UnityEngine.UI.LayoutElement layoutElement = newItem.GetComponent<UnityEngine.UI.LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = newItem.AddComponent<UnityEngine.UI.LayoutElement>();
        }
        layoutElement.preferredHeight = 130f;
        
        // AddItemButton 바로 앞에 배치
        if (addItemButton != null)
        {
            int addButtonIndex = addItemButton.transform.GetSiblingIndex();
            newItem.transform.SetSiblingIndex(addButtonIndex);
        }
        
        // 내용 설정 및 Outline 색상 관리
        TMP_InputField inputField = newItem.GetComponentInChildren<TMP_InputField>();
        if (inputField != null)
        {
            // ★★★ 체크리스트 아이템 InputField Outline 색상 관리 ★★★
            // InputField > Image에서 Outline 찾기
            Outline itemOutline = inputField.GetComponentInChildren<Outline>();
            if (itemOutline != null)
            {
                // 텍스트 변경 이벤트 연결
                inputField.onValueChanged.AddListener((text) => {
                    bool hasFilled = !string.IsNullOrEmpty(text);
                    itemOutline.effectColor = hasFilled ? filledOutlineColor : emptyOutlineColor;
                });
                
                // 초기 색상 설정
                bool initialFilled = !string.IsNullOrEmpty(content);
                itemOutline.effectColor = initialFilled ? filledOutlineColor : emptyOutlineColor;
            }
            
            // 내용 설정
            if (!string.IsNullOrEmpty(content))
            {
                inputField.text = content;
            }
        }
        
        // 삭제 버튼 이벤트 연결
        Transform deleteBtnTransform = newItem.transform.Find("DeleteButton");
        if (deleteBtnTransform != null)
        {
            Button deleteBtn = deleteBtnTransform.GetComponent<Button>();
            if (deleteBtn != null)
            {
                deleteBtn.onClick.AddListener(() => OnDeleteItemClicked(newItem));
            }
        }
        
        checklistItems.Add(newItem);
        
        // 아이템 수에 따른 레이아웃 업데이트 (skipLayoutUpdate가 false일 때만)
        if (!skipLayoutUpdate)
        {
            UpdateLayoutBasedOnItemCount();
        }
        
        Debug.Log($"[ChecklistUIController] 아이템 생성 완료: {newItem.name} (skipLayoutUpdate: {skipLayoutUpdate})");
        
        return newItem;
    }
    
    void OnDeleteItemClicked(GameObject item)
    {
        // 최소 슬롯 수 이하일 때는 내용만 지우기
        if (checklistItems.Count <= minVisibleSlots)
        {
            // 내용만 지우기
            TMP_InputField inputField = item.GetComponentInChildren<TMP_InputField>();
            if (inputField != null)
            {
                inputField.text = "";
                
                // ★★★ Outline 색상도 비어있는 상태로 업데이트 ★★★
                Outline itemOutline = inputField.GetComponentInChildren<Outline>();
                if (itemOutline != null)
                {
                    itemOutline.effectColor = emptyOutlineColor;
                }
                
                Debug.Log($"[ChecklistUIController] 최소 슬롯 유지 - 내용만 삭제됨. 남은 아이템 수: {checklistItems.Count}");
            }
        }
        else
        {
            // 3개 이상일 때는 프리팹 자체를 삭제
            checklistItems.Remove(item);
            Destroy(item);
            
            Debug.Log($"[ChecklistUIController] 아이템 삭제됨. 남은 아이템 수: {checklistItems.Count}");
            
            // 삭제 후 9개 이하가 되면 추가 버튼 다시 표시
            if (checklistItems.Count < maxItems)
            {
                if (addItemButton != null && !addItemButton.gameObject.activeSelf)
                {
                    addItemButton.gameObject.SetActive(true);
                    // 추가 버튼을 맨 마지막으로 이동
                    addItemButton.transform.SetAsLastSibling();
                    Debug.Log("[ChecklistUIController] 9개 이하로 감소 - 추가 버튼 다시 표시");
                }
            }
            
            // ★★★ Destroy가 프레임 끝에 실행되므로 한 프레임 대기 후 레이아웃 업데이트 ★★★
            StartCoroutine(DelayedLayoutUpdateAfterDelete());
        }
    }
    
    /// <summary>
    /// 삭제 후 한 프레임 대기하고 레이아웃 업데이트
    /// Destroy가 프레임 끝에 실행되므로 레이아웃 갱신을 위해 대기 필요
    /// </summary>
    private System.Collections.IEnumerator DelayedLayoutUpdateAfterDelete()
    {
        // Destroy가 완료될 때까지 한 프레임 대기
        yield return null;
        
        // 레이아웃 강제 갱신
        Canvas.ForceUpdateCanvases();
        
        // 아이템 수에 따른 레이아웃 업데이트
        UpdateLayoutBasedOnItemCount();
        
        Debug.Log($"[ChecklistUIController] 삭제 후 레이아웃 업데이트 완료. 아이템 수: {checklistItems.Count}");
    }
    
    void OnSaveClicked()
    {
        // 체크리스트 데이터 수집 (빈 슬롯은 제외)
        List<string> checklistData = new List<string>();
        foreach (var item in checklistItems)
        {
            TMP_InputField inputField = item.GetComponentInChildren<TMP_InputField>();
            if (inputField != null && !string.IsNullOrEmpty(inputField.text.Trim()))
            {
                checklistData.Add(inputField.text.Trim());
            }
        }
        
        Debug.Log($"[ChecklistUIController] 저장할 체크리스트 아이템 수: {checklistData.Count}개");
        
        // 저장 로직 구현 (MemoUIController와 유사하게)
        SaveChecklist(inputTitle.text != null ? inputTitle.text : "", 
                     inputLocation.text != null ? inputLocation.text : "", 
                     checklistData);
        
        // ★★★ 패널 정리 및 닫기 ★★★
        ClearAllItems(recreateMinSlots: false, resetLayout: true);
        
        // Calendar/TimePicker도 닫기
        if (calendarController != null)
            calendarController.CloseCalendar();
        if (timePickerController != null)
            timePickerController.CloseTimePicker();
        
        // MemoUI 닫기
        if (memoUIController != null)
        {
            memoUIController.CloseWithoutSaving();
        }
    }
    
    void SaveChecklist(string title, string location, List<string> items)
    {
        if (currentMemo == null)
        {
            Debug.LogWarning("[ChecklistUIController] currentMemo가 null입니다! 저장할 수 없습니다.");
            return;
        }
        
        MemoData memoData = currentMemo.GetComponent<MemoData>();
        if (memoData == null)
        {
            Debug.LogWarning("[ChecklistUIController] MemoData가 없습니다!");
            return;
        }
        
        // 메모 데이터 저장
        memoData.title = title ?? "";
        memoData.body = string.Join("\n", items); // 체크리스트 아이템들을 줄바꿈으로 합쳐서 저장
        memoData.content = memoData.body;
        memoData.location = location ?? "";
        memoData.memoType = "checklist";
        
        // 날짜 저장
        if (calendarController != null)
        {
            System.DateTime selectedDate = calendarController.GetSelectedDate();
            memoData.DueDateDateTime = selectedDate;
            Debug.Log($"[ChecklistUIController] 선택된 날짜 저장: {selectedDate:yyyy-MM-dd}");
        }
        
        // 시간 저장
        if (timePickerController != null)
        {
            string selectedTime = timePickerController.GetSelectedTimeString();
            memoData.dueTime = selectedTime;
            Debug.Log($"[ChecklistUIController] 선택된 시간 저장: {selectedTime}");
        }
        
        // 긴급도 저장
        if (emergencyButtonManager != null)
        {
            int emergencyIndex = emergencyButtonManager.GetSelectedButtonIndex();
            memoData.emergencyLevel = emergencyIndex + 1;
            Debug.Log($"[ChecklistUIController] 선택된 긴급도 저장: {memoData.emergencyLevel} (인덱스: {emergencyIndex})");
        }
        
        // JSON 저장
        if (pinStore != null)
        {
            pinStore.SaveTextMemoById(memoData.id, memoData.title, memoData.body, memoData.location);
            pinStore.UpdateMemoDueDate(memoData.id, memoData.dueDate);
            pinStore.UpdateMemoDueTime(memoData.id, memoData.dueTime);
            pinStore.UpdateMemoEmergencyLevel(memoData.id, memoData.emergencyLevel);
            pinStore.UpdateMemoType(memoData.id, "checklist");
            
            Debug.Log($"[ChecklistUIController] 체크리스트 저장 완료: ID={memoData.id}, 제목={memoData.title}, 아이템 수={items.Count}");
        }
        else
        {
            Debug.LogWarning("[ChecklistUIController] pinStore가 null입니다! TabPinCreate를 할당해주세요.");
        }
    }
    
    void OnCloseClicked()
    {
        // ★★★ 패널 정리 및 닫기 ★★★
        ClearAllItems(recreateMinSlots: false, resetLayout: true);
        
        // Calendar/TimePicker도 닫기
        if (calendarController != null)
            calendarController.CloseCalendar();
        if (timePickerController != null)
            timePickerController.CloseTimePicker();
        
        // MemoUI 닫기
        if (memoUIController != null)
        {
            memoUIController.CloseWithoutSaving();
        }
    }
    
    /// <summary>
    /// 모든 체크리스트 아이템 삭제
    /// </summary>
    /// <param name="recreateMinSlots">true면 최소 슬롯 재생성, false면 생성 안함 (LoadChecklistCoroutine에서 직접 처리할 때)</param>
    /// <param name="resetLayout">true면 레이아웃 리셋, false면 리셋 안함 (LoadChecklistCoroutine에서 직접 처리할 때)</param>
    void ClearAllItems(bool recreateMinSlots = true, bool resetLayout = true)
    {
        foreach (var item in checklistItems)
        {
            if (item != null) Destroy(item);
        }
        checklistItems.Clear();
        
        // ★★★ 씬에 처음부터 있는 아이템도 삭제 (checklistContent의 모든 자식 중 addItemButton 제외) ★★★
        if (checklistContent != null)
        {
            List<Transform> itemsToRemove = new List<Transform>();
            foreach (Transform child in checklistContent)
            {
                // AddItemButton이 아닌 자식들만 삭제 대상에 추가
                if (addItemButton != null && child.gameObject != addItemButton.gameObject)
                {
                    itemsToRemove.Add(child);
                }
                else if (addItemButton == null)
                {
                    // addItemButton이 null이면 모든 자식 삭제
                    itemsToRemove.Add(child);
                }
            }
            
            foreach (Transform item in itemsToRemove)
            {
                Destroy(item.gameObject);
            }
            
            if (itemsToRemove.Count > 0)
            {
                Debug.Log($"[ChecklistUIController] 씬에 있던 기존 아이템 {itemsToRemove.Count}개 삭제");
            }
        }
        
        // ★★★ 레이아웃 리셋 (옵션) ★★★
        if (resetLayout)
        {
            ResetLayoutPositions();
            lastPushAmount = 0f;
            lastContentExpansion = 0f;
        }
        
        // 최소 슬롯 수만큼 빈 아이템 다시 생성 (옵션)
        if (recreateMinSlots)
        {
            CreateMinimumSlots(skipLayoutUpdate: true);
        }
        
        // 추가 버튼 다시 표시
        if (addItemButton != null)
        {
            addItemButton.gameObject.SetActive(true);
            addItemButton.transform.SetAsLastSibling();
        }
        
        Debug.Log($"[ChecklistUIController] 모든 아이템 삭제 완료 - recreateMinSlots={recreateMinSlots}, resetLayout={resetLayout}");
    }
    
    // 기존 체크리스트 로드 (편집 모드)
    public void LoadChecklist(string title, string location, List<string> items)
    {
        Debug.Log($"[ChecklistUIController] LoadChecklist 시작: title={title}, items.Count={items.Count}");
        
        // 코루틴으로 로드 (Destroy 후 한 프레임 대기 필요)
        StartCoroutine(LoadChecklistCoroutine(title, location, items));
    }
    
    /// <summary>
    /// 체크리스트 로드 코루틴 (Destroy 후 한 프레임 대기하여 아이템 생성)
    /// </summary>
    private System.Collections.IEnumerator LoadChecklistCoroutine(string title, string location, List<string> items)
    {
        // 기존 아이템 삭제 (최소 슬롯 재생성 안함, 레이아웃 리셋)
        ClearAllItems(recreateMinSlots: false, resetLayout: true);
        
        // ★★★ Destroy가 다음 프레임에 실행되므로 한 프레임 대기 ★★★
        yield return null;
        
        // ★★★ 레이아웃 변수 리셋 (원래 위치는 이미 저장되어 있음) ★★★
        lastPushAmount = 0f;
        lastContentExpansion = 0f;
        
        Debug.Log($"[ChecklistUIController] LoadChecklistCoroutine - 레이아웃 리셋 완료, contentOriginalHeight={contentOriginalHeight}");
        
        if (inputTitle != null)
            inputTitle.text = title;
        if (inputLocation != null)
            inputLocation.text = location;
        
        // ★★★ Outline 색상 업데이트 (데이터 로드 후) ★★★
        InitializeOutlineColors();
        
        // 최소 슬롯 수와 로드할 아이템 수 중 큰 값만큼 생성 (레이아웃 업데이트 건너뜀)
        int itemsToCreate = Mathf.Max(minVisibleSlots, items.Count);
        itemsToCreate = Mathf.Min(itemsToCreate, maxItems); // 최대 10개까지
        
        for (int i = 0; i < itemsToCreate; i++)
        {
            string content = i < items.Count ? items[i] : "";
            CreateChecklistItem(content, skipLayoutUpdate: true);
        }
        
        Debug.Log($"[ChecklistUIController] LoadChecklist 완료: 생성된 아이템 수={checklistItems.Count}");
        
        // 로드 완료 후 레이아웃 한 번만 업데이트
        UpdateLayoutBasedOnItemCount();
        
        // 로드 후 10개면 추가 버튼 숨기기
        if (checklistItems.Count >= maxItems && addItemButton != null)
        {
            addItemButton.gameObject.SetActive(false);
            Debug.Log("[ChecklistUIController] 로드 완료 - 10개 도달하여 추가 버튼 숨김");
        }
        
        // 스크롤을 맨 위로 초기화
        if (scrollView != null)
        {
            scrollView.verticalNormalizedPosition = 1f; // 맨 위
        }
    }
    
    /// <summary>
    /// 스크롤을 맨 아래로 이동하여 추가 버튼이 항상 5번째 자리에 보이도록 함
    /// </summary>
    void ScrollToBottom()
    {
        if (scrollView != null)
        {
            // Canvas.ForceUpdateCanvases()를 호출하여 레이아웃 즉시 갱신
            Canvas.ForceUpdateCanvases();
            
            // verticalNormalizedPosition: 0 = 맨 아래, 1 = 맨 위
            scrollView.verticalNormalizedPosition = 0f;
            
            Debug.Log("[ChecklistUIController] 스크롤을 맨 아래로 이동");
        }
    }
    
    /// <summary>
    /// 레이아웃 갱신을 위해 다음 프레임에 스크롤 이동
    /// </summary>
    System.Collections.IEnumerator ScrollToBottomDelayed()
    {
        // 한 프레임 대기 (레이아웃이 갱신될 시간을 줌)
        yield return null;
        
        ScrollToBottom();
    }
    
    /// <summary>
    /// 레이아웃 위치를 원래대로 복원
    /// </summary>
    void ResetLayoutPositions()
    {
        // 원래 위치가 저장되지 않았으면 먼저 저장
        if (!isOriginalPositionsSaved)
        {
            SaveOriginalPositions();
        }
        
        if (checklistDeadline != null)
            checklistDeadline.anchoredPosition = deadlineOriginalPos;
        if (checklistEmergency != null)
            checklistEmergency.anchoredPosition = emergencyOriginalPos;
        if (calendarPanelRect != null)
            calendarPanelRect.anchoredPosition = calendarPanelOriginalPos;
        if (timePickerPanelRect != null)
            timePickerPanelRect.anchoredPosition = timePickerPanelOriginalPos;
        if (checklistCheckList != null)
            checklistCheckList.anchoredPosition = checkListOriginalPos;
        
        // Content 높이도 원래대로 복원
        if (checklistContent != null && contentOriginalHeight > 0)
        {
            Vector2 newSize = checklistContent.sizeDelta;
            newSize.y = contentOriginalHeight;
            checklistContent.sizeDelta = newSize;
        }
        
        // ★★★ 밀림 상태 변수도 리셋 ★★★
        lastPushAmount = 0f;
        lastContentExpansion = 0f;
        
        Debug.Log($"[ChecklistUIController] 레이아웃 위치 복원 완료 - lastPushAmount: 0, lastContentExpansion: 0");
    }
    
    /// <summary>
    /// Calendar가 열려있는지 확인 (Controller의 상태로 확인)
    /// </summary>
    bool IsCalendarOpen()
    {
        // CalendarController의 IsCalendarOpen() 메서드 사용 (내부 상태 확인)
        if (calendarController != null)
        {
            return calendarController.IsCalendarOpen();
        }
        return false;
    }
    
    /// <summary>
    /// TimePicker가 열려있는지 확인 (Controller의 상태로 확인)
    /// </summary>
    bool IsTimePickerOpen()
    {
        // TimePickerController의 IsTimePickerOpen() 메서드 사용 (내부 상태 확인)
        if (timePickerController != null)
        {
            return timePickerController.IsTimePickerOpen();
        }
        return false;
    }
    
    /// <summary>
    /// 아이템 수에 따라 Deadline, Emergency 위치 조정
    /// - 기본 슬롯(minVisibleSlots) 이후부터 maxPushItems까지만 밀림 적용
    /// - 메모 1개 추가당 pushAmountPerItem(130) 만큼 밀림
    /// - Calendar/TimePicker가 열린 상태에서는 변화량(delta)만 적용
    /// </summary>
    void UpdateLayoutBasedOnItemCount()
    {
        Debug.Log($"[ChecklistUIController] UpdateLayoutBasedOnItemCount 호출됨! 아이템 수: {checklistItems.Count}");
        
        int currentItemCount = checklistItems.Count;
        
        // 기본 슬롯 수를 초과한 아이템 수 계산
        int extraItems = Mathf.Max(0, currentItemCount - minVisibleSlots);
        
        // 최대 밀림 적용 개수 계산 (maxPushItems - minVisibleSlots)
        int maxExtraItems = maxPushItems - minVisibleSlots;
        
        // 실제 밀림 적용할 아이템 수 (최대치 제한)
        int pushCount = Mathf.Min(extraItems, maxExtraItems);
        
        // 총 밀림 양 계산
        float totalPushAmount = pushCount * pushAmountPerItem;
        
        Debug.Log($"[ChecklistUIController] 레이아웃 계산 - 아이템: {currentItemCount}, minSlots: {minVisibleSlots}, maxPush: {maxPushItems}, extra: {extraItems}, pushCount: {pushCount}, totalPush: {totalPushAmount}");
        
        // Calendar/TimePicker 열림 상태 확인
        bool isCalendarCurrentlyOpen = IsCalendarOpen();
        bool isTimePickerCurrentlyOpen = IsTimePickerOpen();
        bool isAnyPanelOpen = isCalendarCurrentlyOpen || isTimePickerCurrentlyOpen;
        
        // ★★★ Calendar/TimePicker가 열려있으면 변화량(delta)만큼 현재 위치 조정 ★★★
        if (isAnyPanelOpen)
        {
            // ★★★ 최소 밀림 양: Calendar/TimePicker가 열려있을 때는 최소 pushAmountPerItem(130px) 유지 ★★★
            // 아이템이 삭제되어도 처음 3개 있을 때의 간격(130px) 이하로 줄어들지 않음
            float effectivePushAmount = Mathf.Max(totalPushAmount, pushAmountPerItem);
            
            // 변화량 계산 (현재 push - 이전 push)
            float deltaOffset = effectivePushAmount - lastPushAmount;
            
            Debug.Log($"[ChecklistUIController] 패널 열림 상태 - lastPush: {lastPushAmount}, effectivePush: {effectivePushAmount}, delta: {deltaOffset}");
            
            if (Mathf.Abs(deltaOffset) < 0.01f)
            {
                Debug.Log($"[ChecklistUIController] 변화량 없음 - 레이아웃 유지");
                return;
            }
            
            Debug.Log($"[ChecklistUIController] Calendar/TimePicker 열림 상태 - 변화량 {deltaOffset}px 적용");
            
            // ChecklistUIController가 직접 요소들의 위치 조정 (변화량만 적용)
            if (checklistDeadline != null)
            {
                Vector2 pos = checklistDeadline.anchoredPosition;
                pos.y -= deltaOffset; // 양수면 아래로, 음수면 위로
                checklistDeadline.anchoredPosition = pos;
                Debug.Log($"[ChecklistUIController] Deadline 이동: {pos.y}");
            }
            if (checklistEmergency != null)
            {
                Vector2 pos = checklistEmergency.anchoredPosition;
                pos.y -= deltaOffset;
                checklistEmergency.anchoredPosition = pos;
            }
            if (calendarPanelRect != null)
            {
                Vector2 pos = calendarPanelRect.anchoredPosition;
                pos.y -= deltaOffset;
                calendarPanelRect.anchoredPosition = pos;
                Debug.Log($"[ChecklistUIController] CalendarPanel 이동: {pos.y}");
            }
            if (timePickerPanelRect != null)
            {
                Vector2 pos = timePickerPanelRect.anchoredPosition;
                pos.y -= deltaOffset;
                timePickerPanelRect.anchoredPosition = pos;
            }
            
            // lastPushAmount 업데이트
            lastPushAmount = effectivePushAmount;
            return; // Calendar/TimePicker가 열려있으면 여기서 종료 (기본 위치 설정하지 않음)
        }
        
        // ★★★ Calendar/TimePicker가 닫혀있을 때만 기본 위치 설정 ★★★
        // Deadline 위치 조정 (아래로 밀기 = Y값 감소)
        if (checklistDeadline != null)
        {
            Vector2 newDeadlinePos = deadlineOriginalPos;
            newDeadlinePos.y -= totalPushAmount;
            checklistDeadline.anchoredPosition = newDeadlinePos;
            Debug.Log($"[ChecklistUIController] Deadline 이동: {deadlineOriginalPos.y} -> {newDeadlinePos.y}");
        }
        else
        {
            Debug.LogWarning("[ChecklistUIController] checklistDeadline이 NULL입니다! Inspector에서 연결해주세요.");
        }
        
        // Emergency 위치 조정 (아래로 밀기 = Y값 감소)
        if (checklistEmergency != null)
        {
            Vector2 newEmergencyPos = emergencyOriginalPos;
            newEmergencyPos.y -= totalPushAmount;
            checklistEmergency.anchoredPosition = newEmergencyPos;
            Debug.Log($"[ChecklistUIController] Emergency 이동: {emergencyOriginalPos.y} -> {newEmergencyPos.y}");
        }
        else
        {
            Debug.LogWarning("[ChecklistUIController] checklistEmergency가 NULL입니다! Inspector에서 연결해주세요.");
        }
        
        // CalendarPanel 위치 조정 (아래로 밀기 = Y값 감소)
        if (calendarPanelRect != null)
        {
            Vector2 newCalendarPos = calendarPanelOriginalPos;
            newCalendarPos.y -= totalPushAmount;
            calendarPanelRect.anchoredPosition = newCalendarPos;
            Debug.Log($"[ChecklistUIController] CalendarPanel 이동: {calendarPanelOriginalPos.y} -> {newCalendarPos.y}");
        }
        else
        {
            Debug.LogWarning("[ChecklistUIController] calendarPanelRect가 NULL입니다! Inspector에서 연결해주세요.");
        }
        
        // TimePickerPanel 위치 조정 (아래로 밀기 = Y값 감소)
        if (timePickerPanelRect != null)
        {
            Vector2 newTimePickerPos = timePickerPanelOriginalPos;
            newTimePickerPos.y -= totalPushAmount;
            timePickerPanelRect.anchoredPosition = newTimePickerPos;
            Debug.Log($"[ChecklistUIController] TimePickerPanel 이동: {timePickerPanelOriginalPos.y} -> {newTimePickerPos.y}");
        }
        else
        {
            Debug.LogWarning("[ChecklistUIController] timePickerPanelRect가 NULL입니다! Inspector에서 연결해주세요.");
        }
        
        // ★★★ lastPushAmount 업데이트 ★★★
        lastPushAmount = totalPushAmount;
        
        Debug.Log($"[ChecklistUIController] 기본 레이아웃 업데이트 완료 - 총 밀림: {totalPushAmount}px, lastPush 업데이트: {lastPushAmount}");
    }
    
    /// <summary>
    /// 현재 아이템 수에 따른 밀림 양 계산 (외부에서 호출 가능)
    /// CalendarController/TimePickerController에서 사용
    /// </summary>
    public float GetCurrentPushAmount()
    {
        int currentItemCount = checklistItems.Count;
        int extraItems = Mathf.Max(0, currentItemCount - minVisibleSlots);
        int maxExtraItems = maxPushItems - minVisibleSlots;
        int pushCount = Mathf.Min(extraItems, maxExtraItems);
        return pushCount * pushAmountPerItem;
    }
    
    /// <summary>
    /// Calendar/TimePicker 패널이 열릴 때 호출 - 패널 높이만큼 Deadline, Emergency 추가 밀기
    /// </summary>
    public void ApplyPanelOpenPush(float panelHeight)
    {
        Debug.Log($"[ChecklistUIController] ApplyPanelOpenPush 호출됨 - 패널 높이: {panelHeight}");
        
        if (checklistDeadline != null)
        {
            Vector2 pos = checklistDeadline.anchoredPosition;
            pos.y -= panelHeight;
            checklistDeadline.anchoredPosition = pos;
            Debug.Log($"[ChecklistUIController] Deadline 밀기 완료: Y = {pos.y}");
        }
        else
        {
            Debug.LogWarning("[ChecklistUIController] checklistDeadline이 NULL입니다! Inspector에서 연결하거나 GameObject 이름을 'ChecklistDeadline'으로 확인하세요.");
        }
        
        if (checklistEmergency != null)
        {
            Vector2 pos = checklistEmergency.anchoredPosition;
            pos.y -= panelHeight;
            checklistEmergency.anchoredPosition = pos;
            Debug.Log($"[ChecklistUIController] Emergency 밀기 완료: Y = {pos.y}");
        }
        else
        {
            Debug.LogWarning("[ChecklistUIController] checklistEmergency가 NULL입니다! Inspector에서 연결하거나 GameObject 이름을 'ChecklistEmergency'로 확인하세요.");
        }
    }
    
    /// <summary>
    /// Calendar/TimePicker 패널이 닫힐 때 호출 - 레이아웃을 기본 상태로 재설정
    /// 패널이 닫힌 후에는 원래 위치 + totalPushAmount로 재설정해야 함
    /// </summary>
    public void RefreshLayoutOnPanelClose()
    {
        float totalPushAmount = GetCurrentPushAmount();
        
        Debug.Log($"[ChecklistUIController] RefreshLayoutOnPanelClose - totalPushAmount: {totalPushAmount}");
        
        // 모든 요소를 원래 위치 + totalPushAmount로 재설정
        if (checklistDeadline != null)
        {
            Vector2 newPos = deadlineOriginalPos;
            newPos.y -= totalPushAmount;
            checklistDeadline.anchoredPosition = newPos;
            Debug.Log($"[ChecklistUIController] Deadline 재설정: {newPos.y}");
        }
        if (checklistEmergency != null)
        {
            Vector2 newPos = emergencyOriginalPos;
            newPos.y -= totalPushAmount;
            checklistEmergency.anchoredPosition = newPos;
            Debug.Log($"[ChecklistUIController] Emergency 재설정: {newPos.y}");
        }
        if (calendarPanelRect != null)
        {
            Vector2 newPos = calendarPanelOriginalPos;
            newPos.y -= totalPushAmount;
            calendarPanelRect.anchoredPosition = newPos;
            Debug.Log($"[ChecklistUIController] CalendarPanel 재설정: {newPos.y}");
        }
        if (timePickerPanelRect != null)
        {
            Vector2 newPos = timePickerPanelOriginalPos;
            newPos.y -= totalPushAmount;
            timePickerPanelRect.anchoredPosition = newPos;
            Debug.Log($"[ChecklistUIController] TimePickerPanel 재설정: {newPos.y}");
        }
        
        // lastPushAmount 업데이트
        lastPushAmount = totalPushAmount;
    }
    
    // ★★★ Public Getters for CalendarController/TimePickerController ★★★
    public RectTransform GetChecklistDeadline() => checklistDeadline;
    public RectTransform GetChecklistEmergency() => checklistEmergency;
    public RectTransform GetCalendarPanelRect() => calendarPanelRect;
    public RectTransform GetTimePickerPanelRect() => timePickerPanelRect;
    public Vector2 GetDeadlineOriginalPos() => deadlineOriginalPos;
    public Vector2 GetEmergencyOriginalPos() => emergencyOriginalPos;
    public Vector2 GetCalendarPanelOriginalPos() => calendarPanelOriginalPos;
    public Vector2 GetTimePickerPanelOriginalPos() => timePickerPanelOriginalPos;
    
    /// <summary>
    /// Calendar/TimePicker 패널이 열릴 때 Emergency를 패널 높이만큼 밑으로 이동
    /// </summary>
    public void PushEmergencyDownForPanel(float panelHeight)
    {
        if (checklistEmergency != null)
        {
            Vector2 pos = checklistEmergency.anchoredPosition;
            pos.y -= panelHeight;
            checklistEmergency.anchoredPosition = pos;
            Debug.Log($"[ChecklistUIController] Emergency 패널용 밀기 완료: Y = {pos.y}");
        }
        else
        {
            Debug.LogWarning("[ChecklistUIController] checklistEmergency가 NULL입니다!");
        }
    }
    
    // ★★★ InputField Outline 색상 관리 ★★★
    
    /// <summary>
    /// Title 텍스트 변경 이벤트 핸들러
    /// </summary>
    private void OnTitleTextChanged(string text)
    {
        UpdateTitleOutlineColor();
    }
    
    /// <summary>
    /// Location 텍스트 변경 이벤트 핸들러
    /// </summary>
    private void OnLocationTextChanged(string text)
    {
        UpdateLocationOutlineColor();
    }
    
    /// <summary>
    /// Title Outline 색상 업데이트 (비어있으면 #96CBE0, 채워지면 #D9D9D9)
    /// </summary>
    private void UpdateTitleOutlineColor()
    {
        if (titleOutline != null)
        {
            bool hasFilled = inputTitle != null && !string.IsNullOrEmpty(inputTitle.text);
            Color newColor = hasFilled ? filledOutlineColor : emptyOutlineColor;
            titleOutline.effectColor = newColor;
            Debug.Log($"[ChecklistUIController] Title Outline 색상 변경: {(hasFilled ? "채워짐" : "비어있음")} - {newColor}");
        }
    }
    
    /// <summary>
    /// Location Outline 색상 업데이트 (비어있으면 #96CBE0, 채워지면 #D9D9D9)
    /// </summary>
    private void UpdateLocationOutlineColor()
    {
        if (locationOutline != null)
        {
            bool hasFilled = inputLocation != null && !string.IsNullOrEmpty(inputLocation.text);
            Color newColor = hasFilled ? filledOutlineColor : emptyOutlineColor;
            locationOutline.effectColor = newColor;
            Debug.Log($"[ChecklistUIController] Location Outline 색상 변경: {(hasFilled ? "채워짐" : "비어있음")} - {newColor}");
        }
    }
    
    /// <summary>
    /// 모든 InputField Outline 색상 초기화 (패널 열릴 때 호출)
    /// </summary>
    private void InitializeOutlineColors()
    {
        UpdateTitleOutlineColor();
        UpdateLocationOutlineColor();
    }
    
    // ★★★ 삭제 버튼 숨김 및 아이템 너비 조정 (닫기 버튼 근처) ★★★
    
    // ★★★ 디버그용 프레임 카운터 (매 프레임 로그 방지) ★★★
    private int debugFrameCounter = 0;
    
    /// <summary>
    /// 매 프레임 체크리스트 아이템 위치를 체크하여 ChecklistBtn_TextClose 근처면 삭제 버튼 숨김 및 너비 축소
    /// </summary>
    private void LateUpdate()
    {
        // 패널이 활성화되어 있지 않으면 스킵
        if (!gameObject.activeInHierarchy) return;
        
        debugFrameCounter++;
        
        // 60프레임마다 디버그 로그 (1초에 한 번 정도)
        if (debugFrameCounter % 60 == 0)
        {
            Debug.Log($"[ChecklistUIController] LateUpdate 체크 - items: {checklistItems.Count}, closeBtn: {(checklistBtnTextCloseRect != null ? "OK" : "NULL")}");
        }
        
        // 체크리스트 아이템이 있고 닫기 버튼이 할당되어 있을 때만 체크
        if (checklistItems.Count > 0 && checklistBtnTextCloseRect != null)
        {
            CheckItemsProximityToCloseButton();
        }
    }
    
    /// <summary>
    /// 체크리스트 아이템들의 닫기 버튼 근접 여부 체크 및 조정
    /// </summary>
    private void CheckItemsProximityToCloseButton()
    {
        if (checklistBtnTextCloseRect == null || checklistItems.Count == 0) return;
        
        // 닫기 버튼의 스크린 좌표 계산
        Vector2 closeBtnScreenPos = GetScreenPosition(checklistBtnTextCloseRect);
        
        // 60프레임마다 거리 로그 출력
        bool shouldLog = (debugFrameCounter % 60 == 0);
        
        foreach (var item in checklistItems)
        {
            if (item == null) continue;
            
            RectTransform itemRect = item.GetComponent<RectTransform>();
            if (itemRect == null) continue;
            
            // 삭제 버튼 찾기
            Transform deleteBtnTransform = item.transform.Find("DeleteButton");
            
            if (shouldLog)
            {
                Debug.Log($"[ChecklistUIController] {item.name} - DeleteButton: {(deleteBtnTransform != null ? "찾음" : "NULL")}");
            }
            
            if (deleteBtnTransform == null) continue;
            
            // ★★★ 아이템의 스크린 좌표로 거리 계산 (삭제 버튼이 숨겨져 있어도 작동) ★★★
            Vector2 itemScreenPos = GetScreenPosition(itemRect);
            float distance = Vector2.Distance(closeBtnScreenPos, itemScreenPos);
            
            // 현재 상태 확인
            bool isCurrentlyHidden = !deleteBtnTransform.gameObject.activeSelf;
            
            // ★★★ 히스테리시스: 숨길 때와 보일 때 다른 반경 사용 (깜빡임 방지) ★★★
            bool shouldHide = distance < deleteButtonHideRadius;
            bool shouldShow = distance > deleteButtonShowRadius;
            
            if (shouldLog)
            {
                Debug.Log($"[ChecklistUIController] {item.name} - 거리: {distance:F1}, 숨김반경: {deleteButtonHideRadius}, 표시반경: {deleteButtonShowRadius}");
            }
            
            if (shouldHide && !isCurrentlyHidden)
            {
                // 반경 안으로 들어옴: 삭제 버튼 숨기고 너비 축소
                deleteBtnTransform.gameObject.SetActive(false);
                
                Vector2 sizeDelta = itemRect.sizeDelta;
                sizeDelta.x = itemCompactWidth;
                itemRect.sizeDelta = sizeDelta;
                
                Debug.Log($"[ChecklistUIController] {item.name} 삭제 버튼 숨김, 너비 축소: {itemCompactWidth}");
            }
            else if (shouldShow && isCurrentlyHidden)
            {
                // 반경 밖으로 나감: 삭제 버튼 보이고 너비 원래대로
                deleteBtnTransform.gameObject.SetActive(true);
                
                Vector2 sizeDelta = itemRect.sizeDelta;
                sizeDelta.x = itemOriginalWidth;
                itemRect.sizeDelta = sizeDelta;
                
                Debug.Log($"[ChecklistUIController] {item.name} 삭제 버튼 표시, 너비 복원: {itemOriginalWidth}");
            }
            // shouldHide와 shouldShow 둘 다 아닌 경우 (hideRadius < distance < showRadius): 현재 상태 유지
        }
    }
    
    /// <summary>
    /// RectTransform의 중심 위치를 실제 스크린 좌표로 반환 (Canvas 고려)
    /// </summary>
    private Vector2 GetScreenPosition(RectTransform rectTransform)
    {
        if (rectTransform == null) return Vector2.zero;
        
        // Canvas 찾기
        Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
        if (canvas == null) return Vector2.zero;
        
        // Root Canvas 찾기 (중첩된 Canvas 대응)
        Canvas rootCanvas = canvas.rootCanvas;
        
        // GetWorldCorners로 4개 코너 얻기
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        
        // 중심 계산
        Vector3 center = (corners[0] + corners[2]) / 2f;
        
        if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Screen Space - Overlay에서 스크린 좌표 계산
            // world 좌표에 scaleFactor 적용
            float scaleFactor = rootCanvas.scaleFactor;
            return new Vector2(center.x * scaleFactor, center.y * scaleFactor);
        }
        else
        {
            // Screen Space - Camera 또는 World Space
            Camera cam = rootCanvas.worldCamera ?? Camera.main;
            if (cam != null)
            {
                return RectTransformUtility.WorldToScreenPoint(cam, center);
            }
        }
        
        return new Vector2(center.x, center.y);
    }
}