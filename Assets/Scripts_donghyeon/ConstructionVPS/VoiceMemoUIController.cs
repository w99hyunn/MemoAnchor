using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 음성메모 UI 컨트롤러
/// - 스크롤 없이 고정 레이아웃 (2개 기본 슬롯 + 추가 버튼)
/// - 추가 버튼 클릭 시 3번째 메모 추가 후 버튼 숨김
/// </summary>
public class VoiceMemoUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField inputTitle;
    [SerializeField] private TMP_InputField inputLocation;
    [SerializeField] private RectTransform voiceMemoContent; // 메모 아이템들이 들어갈 Content (스크롤 없음)
    [SerializeField] private GameObject voiceMemoItemPrefab; // 음성메모 아이템 프리팹
    [SerializeField] private Button addItemButton;
    [SerializeField] private Button btnSave;
    [SerializeField] private Button btnClose;
    
    // ★★★ 레이아웃 관리는 TimePickerController/CalendarController가 담당 ★★★
    // VoiceMemoUIController는 음성메모 아이템만 관리
    
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
    [SerializeField] private int maxItems = 3; // 최대 3개 (2개 기본 + 1개 추가)
    [SerializeField] private int minVisibleSlots = 2; // 화면에 항상 보이는 최소 슬롯 수
    
    [Header("InputField Outlines")]
    [Tooltip("VoiceMemoInputField_Title의 Outline 컴포넌트")]
    [SerializeField] private Outline titleOutline;
    [Tooltip("VoiceMemoInputField_Location의 Outline 컴포넌트")]
    [SerializeField] private Outline locationOutline;
    
    [Header("Outline Colors")]
    [SerializeField] private Color emptyOutlineColor = new Color(0x96 / 255f, 0xCB / 255f, 0xE0 / 255f); // #96CBE0 (비어있을 때)
    [SerializeField] private Color filledOutlineColor = new Color(0xD9 / 255f, 0xD9 / 255f, 0xD9 / 255f); // #D9D9D9 (채워졌을 때)
    
    [Header("Layout Push Settings (메모 추가 시 레이아웃 조정)")]
    [Tooltip("메모 1개 추가당 밀리는 양 (TimePickerController/CalendarController에서 사용)")]
    [SerializeField] private float pushAmountPerItem = 130f;
    
    [Header("Debug")]
    [Tooltip("상세 디버그 로그 출력")]
    [SerializeField] private bool verboseDebug = false;
    
    private List<GameObject> voiceMemoItems = new List<GameObject>();
    
    // 현재 편집 중인 메모 GameObject
    private GameObject currentMemo;
    
    // 마지막으로 적용된 밀림 양 저장 (TimePickerController/CalendarController에서 사용)
    private float lastPushAmount = 0f;
    
    // Voice Content 위치 고정을 위한 변수
    private Vector2 originalVoiceContentPosition;
    private bool positionSaved = false;
    
    // 외부에서 접근할 수 있는 프로퍼티
    public float LastPushAmount => lastPushAmount;
    
    /// <summary>
    /// Voice Content 원래 위치 복원 (CalendarController/TimePickerController에서 호출)
    /// </summary>
    public void RestoreVoiceContentPosition()
    {
        if (positionSaved && voiceMemoContent != null)
        {
            voiceMemoContent.anchoredPosition = originalVoiceContentPosition;
            Debug.Log($"[VoiceMemoUIController] Voice Content 위치 복원: {originalVoiceContentPosition}");
        }
    }
    
    void Awake()
    {
        // ★★★ 레이아웃 관리는 TimePickerController/CalendarController가 담당 ★★★
        Debug.Log("[VoiceMemoUIController] Awake - 레이아웃은 TimePickerController/CalendarController가 관리");
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
        
        // CalendarController/TimePickerController 자동 할당 (Inspector에서 할당되지 않은 경우)
        if (calendarController == null)
        {
            calendarController = FindObjectOfType<CalendarController>();
            if (calendarController != null)
            {
                Debug.Log("[VoiceMemoUIController] CalendarController를 자동으로 찾았습니다.");
            }
        }
        if (timePickerController == null)
        {
            timePickerController = FindObjectOfType<TimePickerController>();
            if (timePickerController != null)
            {
                Debug.Log("[VoiceMemoUIController] TimePickerController를 자동으로 찾았습니다.");
            }
        }
        if (memoUIController == null)
        {
            memoUIController = FindObjectOfType<MemoUIController>();
            if (memoUIController != null)
            {
                Debug.Log("[VoiceMemoUIController] MemoUIController를 자동으로 찾았습니다.");
            }
        }
        if (emergencyButtonManager == null)
        {
            emergencyButtonManager = FindObjectOfType<EmergencyButtonManager>();
            if (emergencyButtonManager != null)
            {
                Debug.Log("[VoiceMemoUIController] EmergencyButtonManager를 자동으로 찾았습니다.");
            }
        }
        if (pinStore == null)
        {
            pinStore = FindObjectOfType<TabPinCreate>();
            if (pinStore != null)
            {
                Debug.Log("[VoiceMemoUIController] TabPinCreate를 자동으로 찾았습니다.");
            }
        }
        
        // Outline 컴포넌트 자동 검색 (자체 -> 자식 -> 부모 순서)
        if (titleOutline == null && inputTitle != null)
        {
            titleOutline = inputTitle.GetComponent<Outline>();
            if (titleOutline == null)
            {
                titleOutline = inputTitle.GetComponentInChildren<Outline>();
            }
            if (titleOutline == null && inputTitle.transform.parent != null)
            {
                titleOutline = inputTitle.transform.parent.GetComponent<Outline>();
            }
            Debug.Log($"[VoiceMemoUIController] titleOutline 자동 검색 결과: {(titleOutline != null ? titleOutline.gameObject.name : "NULL")}");
        }
        if (locationOutline == null && inputLocation != null)
        {
            locationOutline = inputLocation.GetComponent<Outline>();
            if (locationOutline == null)
            {
                locationOutline = inputLocation.GetComponentInChildren<Outline>();
            }
            if (locationOutline == null && inputLocation.transform.parent != null)
            {
                locationOutline = inputLocation.transform.parent.GetComponent<Outline>();
            }
            Debug.Log($"[VoiceMemoUIController] locationOutline 자동 검색 결과: {(locationOutline != null ? locationOutline.gameObject.name : "NULL")}");
        }
        
        // InputField 텍스트 변경 이벤트 연결 (Outline 색상 업데이트용)
        if (inputTitle != null)
        {
            inputTitle.onValueChanged.AddListener(OnTitleTextChanged);
        }
        if (inputLocation != null)
        {
            inputLocation.onValueChanged.AddListener(OnLocationTextChanged);
        }
        
        // 초기 상태 확인
        Debug.Log($"[VoiceMemoUIController] Start - Content: {(voiceMemoContent != null ? "연결됨" : "NULL")}, Prefab: {(voiceMemoItemPrefab != null ? "연결됨" : "NULL")}");
    }
    
    void OnEnable()
    {
        // ★★★ 한 프레임 대기 후 레이아웃 리셋 (Start()가 먼저 호출되도록) ★★★
        StartCoroutine(DelayedInitialize());
    }
    
    /// <summary>
    /// 한 프레임 대기 후 초기화 (TimePickerController/CalendarController.Start()가 먼저 실행되도록)
    /// </summary>
    private System.Collections.IEnumerator DelayedInitialize()
    {
        // 한 프레임 대기 - Start()가 호출된 후
        yield return null;
        
        // 컨트롤러 자동 할당
        if (timePickerController == null)
            timePickerController = FindObjectOfType<TimePickerController>();
        if (calendarController == null)
            calendarController = FindObjectOfType<CalendarController>();
        
        // ★★★ TimePickerController/CalendarController에서 레이아웃 리셋 ★★★
        if (timePickerController != null)
        {
            timePickerController.ResetVoiceMemoToOriginalLayout();
            Debug.Log("[VoiceMemoUIController] TimePickerController.ResetVoiceMemoToOriginalLayout() 호출");
        }
        if (calendarController != null)
        {
            calendarController.ResetVoiceMemoToOriginalLayout();
            Debug.Log("[VoiceMemoUIController] CalendarController.ResetVoiceMemoToOriginalLayout() 호출");
        }
        
        // 패널이 활성화될 때마다 초기화
        InitializePanel();
        
        Debug.Log("[VoiceMemoUIController] OnEnable - 패널 초기화 완료 (DelayedInitialize)");
    }
    
    void InitializePanel()
    {
        // 초기화 로그
        Debug.Log($"[VoiceMemoUIController] InitializePanel 시작");
        
        // Content 안의 기존 음성메모 아이템들을 정리 (AddItemButton 제외)
        if (voiceMemoContent != null)
        {
            List<Transform> itemsToRemove = new List<Transform>();
            
            int totalChildren = voiceMemoContent.childCount;
            Debug.Log($"[VoiceMemoUIController] Content 자식 수: {totalChildren}");
            
            foreach (Transform child in voiceMemoContent)
            {
                // AddItemButton이 아닌 자식들만 삭제 대상에 추가
                if (addItemButton != null && child.gameObject != addItemButton.gameObject)
                {
                    itemsToRemove.Add(child);
                    Debug.Log($"[VoiceMemoUIController] 삭제 대상 추가: {child.name}");
                }
                else
                {
                    Debug.Log($"[VoiceMemoUIController] 유지: {child.name} (AddItemButton)");
                }
            }
            
            // 리스트에 추가된 아이템들 삭제
            foreach (Transform item in itemsToRemove)
            {
                Debug.Log($"[VoiceMemoUIController] 삭제: {item.name}");
                Destroy(item.gameObject);
            }
            
            Debug.Log($"[VoiceMemoUIController] 기존 아이템 {itemsToRemove.Count}개 정리 완료");
        }
        else
        {
            Debug.LogError("[VoiceMemoUIController] voiceMemoContent가 NULL입니다! Inspector에서 Content를 연결해주세요!");
        }
        
        // 음성메모 아이템 리스트 초기화
        voiceMemoItems.Clear();
        Debug.Log($"[VoiceMemoUIController] voiceMemoItems 리스트 초기화 완료");
        
        // 레이아웃 상태 변수 리셋 (TimePickerController/CalendarController에서 사용)
        lastPushAmount = 0f;
        
        // ★★★ 아이템은 생성하지 않음 - LoadVoiceMemo()에서 생성 ★★★
        Debug.Log($"[VoiceMemoUIController] 아이템 생성은 LoadVoiceMemo()에서 처리");
        
        // Layout 강제 업데이트
        Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(voiceMemoContent);
        
        // Voice Content 위치 저장 (처음 한 번만)
        if (!positionSaved && voiceMemoContent != null)
        {
            originalVoiceContentPosition = voiceMemoContent.anchoredPosition;
            positionSaved = true;
            Debug.Log($"[VoiceMemoUIController] Voice Content 원래 위치 저장: {originalVoiceContentPosition}");
        }
        
        // 초기에는 추가 버튼이 표시되어야 함
        if (addItemButton != null)
        {
            addItemButton.gameObject.SetActive(true);
            addItemButton.transform.SetAsLastSibling();
        }
        
        // InputField 초기화 및 Outline 색상 초기화
        if (inputTitle != null)
            inputTitle.text = "";
        if (inputLocation != null)
            inputLocation.text = "";
        InitializeOutlineColors();
        
        Debug.Log($"[VoiceMemoUIController] InitializePanel 완료");
    }
    
    /// <summary>
    /// 패널이 열릴 때 MemoUIController에서 호출 (currentMemo 전달)
    /// </summary>
    public void OnPanelOpened(GameObject memo)
    {
        Debug.Log($"[VoiceMemoUIController] OnPanelOpened 호출: memo={(memo != null ? memo.name : "null")}");
        
        currentMemo = memo;
        
        // 저장된 음성메모 데이터 로드
        if (currentMemo != null)
        {
            MemoData memoData = currentMemo.GetComponent<MemoData>();
            if (memoData != null)
            {
                // body를 줄바꿈으로 분리해서 음성메모 아이템으로 로드
                List<string> loadedItems = new List<string>();
                if (!string.IsNullOrEmpty(memoData.body))
                {
                    string[] items = memoData.body.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
                    loadedItems = new List<string>(items);
                }
                
                // 녹음 파일 경로 로드
                List<string> recordingPaths = memoData.voiceRecordingPaths ?? new List<string>();
                
                // 음성메모 로드 (LoadVoiceMemo가 제목/위치도 설정함)
                LoadVoiceMemo(memoData.title ?? "", memoData.location ?? "", loadedItems, recordingPaths);
                
                Debug.Log($"[VoiceMemoUIController] 저장된 음성메모 로드: 제목={memoData.title}, 위치={memoData.location}, 아이템 수={loadedItems.Count}, 녹음 파일 수={recordingPaths.Count}");
            }
            else
            {
                Debug.LogWarning("[VoiceMemoUIController] MemoData가 없습니다! - 빈 상태로 초기화");
                LoadVoiceMemo("", "", new List<string>(), new List<string>());
            }
        }
        else
        {
            Debug.LogWarning("[VoiceMemoUIController] currentMemo가 null! - 빈 상태로 초기화");
            LoadVoiceMemo("", "", new List<string>(), new List<string>());
        }
    }
    
    /// <summary>
    /// 현재 편집 중인 메모 설정 (외부에서 호출)
    /// </summary>
    public void SetCurrentMemo(GameObject memo)
    {
        currentMemo = memo;
        Debug.Log($"[VoiceMemoUIController] SetCurrentMemo: {(memo != null ? memo.name : "null")}");
    }
    
    /// <summary>
    /// 최소 슬롯 수만큼 빈 음성메모 아이템 생성
    /// </summary>
    /// <param name="skipLayoutUpdate">true면 레이아웃 업데이트 건너뜀 (초기화 시)</param>
    void CreateMinimumSlots(bool skipLayoutUpdate = false)
    {
        for (int i = 0; i < minVisibleSlots; i++)
        {
            CreateVoiceMemoItem("", skipLayoutUpdate);
        }
        if (verboseDebug) Debug.Log($"[VoiceMemoUIController] 최소 슬롯 {minVisibleSlots}개 생성 완료");
    }
    
    void OnAddItemClicked()
    {
        Debug.Log($"[VoiceMemoUIController] Add Item 버튼 클릭됨! 현재 아이템 수: {voiceMemoItems.Count}");
        
        // 이미 최대 개수면 추가 불가
        if (voiceMemoItems.Count >= maxItems)
        {
            Debug.Log($"최대 {maxItems}개까지만 추가 가능합니다.");
            return;
        }
        
        // 빈 아이템 생성
        CreateVoiceMemoItem("");
        
        // 3번째 아이템이 추가되면 즉시 추가 버튼 숨기기
        if (voiceMemoItems.Count >= maxItems)
        {
            addItemButton.gameObject.SetActive(false);
            Debug.Log("[VoiceMemoUIController] 3개 도달 - 추가 버튼 숨김");
        }
        
        Debug.Log($"[VoiceMemoUIController] 총 아이템 수: {voiceMemoItems.Count}");
    }
    
    /// <summary>
    /// 음성메모 아이템 생성 (공통 메서드)
    /// </summary>
    /// <param name="content">아이템 내용</param>
    /// <param name="skipLayoutUpdate">true면 레이아웃 업데이트 건너뜀 (초기화/클리어 시)</param>
    GameObject CreateVoiceMemoItem(string content, bool skipLayoutUpdate = false)
    {
        // Null 체크
        if (voiceMemoContent == null)
        {
            Debug.LogError("[VoiceMemoUIController] voiceMemoContent가 null입니다! Inspector에서 Content를 연결해주세요.");
            return null;
        }
        
        if (voiceMemoItemPrefab == null)
        {
            Debug.LogError("[VoiceMemoUIController] voiceMemoItemPrefab가 null입니다! Inspector에서 프리팹을 연결해주세요.");
            return null;
        }
        
        // 음성메모 아이템 생성
        GameObject newItem = Instantiate(voiceMemoItemPrefab, voiceMemoContent);
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
            // 음성메모 아이템 InputField Outline 색상 관리
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
        
        // VoiceRecorderUI 컴포넌트에 아이템 번호 설정
        VoiceRecorderUI recorderUI = newItem.GetComponent<VoiceRecorderUI>();
        if (recorderUI != null)
        {
            // 아이템 번호 설정 (1부터 시작)
            int itemNumber = voiceMemoItems.Count + 1;
            recorderUI.SetItemNumber(itemNumber);
            Debug.Log($"[VoiceMemoUIController] 아이템 번호 설정: {itemNumber}");
        }
        
        voiceMemoItems.Add(newItem);
        
        // 아이템 수에 따른 레이아웃 업데이트 (skipLayoutUpdate가 false일 때만)
        if (!skipLayoutUpdate)
        {
            UpdateLayoutBasedOnItemCount();
        }
        
        if (verboseDebug) Debug.Log($"[VoiceMemoUIController] 아이템 생성 완료: {newItem.name}");
        
        return newItem;
    }
    
    void OnDeleteItemClicked(GameObject item)
    {
        // 최소 슬롯 수 이하일 때는 내용만 지우기
        if (voiceMemoItems.Count <= minVisibleSlots)
        {
            // 내용만 지우기
            TMP_InputField inputField = item.GetComponentInChildren<TMP_InputField>();
            if (inputField != null)
            {
                inputField.text = "";
                
                // Outline 색상도 비어있는 상태로 업데이트
                Outline itemOutline = inputField.GetComponentInChildren<Outline>();
                if (itemOutline != null)
                {
                    itemOutline.effectColor = emptyOutlineColor;
                }
                
                Debug.Log($"[VoiceMemoUIController] 최소 슬롯 유지 - 내용만 삭제됨. 남은 아이템 수: {voiceMemoItems.Count}");
            }
        }
        else
        {
            // 3번째 아이템 삭제 시 프리팹 자체를 삭제
            voiceMemoItems.Remove(item);
            Destroy(item);
            
            Debug.Log($"[VoiceMemoUIController] 아이템 삭제됨. 남은 아이템 수: {voiceMemoItems.Count}");
            
            // 삭제 후 2개 이하가 되면 추가 버튼 다시 표시
            if (voiceMemoItems.Count < maxItems)
            {
                if (addItemButton != null && !addItemButton.gameObject.activeSelf)
                {
                    addItemButton.gameObject.SetActive(true);
                    // 추가 버튼을 맨 마지막으로 이동
                    addItemButton.transform.SetAsLastSibling();
                    Debug.Log("[VoiceMemoUIController] 2개 이하로 감소 - 추가 버튼 다시 표시");
                }
            }
            
            // Destroy가 프레임 끝에 실행되므로 한 프레임 대기 후 레이아웃 업데이트
            StartCoroutine(DelayedLayoutUpdateAfterDelete());
        }
    }
    
    /// <summary>
    /// 삭제 후 한 프레임 대기하고 레이아웃 업데이트
    /// </summary>
    private System.Collections.IEnumerator DelayedLayoutUpdateAfterDelete()
    {
        // Destroy가 완료될 때까지 한 프레임 대기
        yield return null;
        
        // 레이아웃 강제 갱신
        Canvas.ForceUpdateCanvases();
        
        // 아이템 수에 따른 레이아웃 업데이트
        UpdateLayoutBasedOnItemCount();
        
        Debug.Log($"[VoiceMemoUIController] 삭제 후 레이아웃 업데이트 완료. 아이템 수: {voiceMemoItems.Count}");
    }
    
    void OnSaveClicked()
    {
        Debug.Log($"[🔍TRACE] [VoiceMemoUIController] OnSaveClicked 시작");
        
        // 음성메모 데이터 수집 (빈 슬롯은 제외)
        List<string> voiceMemoData = new List<string>();
        List<string> voiceRecordingPaths = new List<string>();
        
        foreach (var item in voiceMemoItems)
        {
            TMP_InputField inputField = item.GetComponentInChildren<TMP_InputField>();
            VoiceRecorderUI recorderUI = item.GetComponent<VoiceRecorderUI>();
            
            // VoiceRecorderUI 상태 확인
            string itemText = "";
            if (recorderUI != null)
            {
                // "음성 녹음X" 상태 확인
                if (recorderUI.IsRecordingCompleted())
                {
                    itemText = recorderUI.GetStatusText(); // "음성 녹음1", "음성 녹음2" 등
                    Debug.Log($"[🔍TRACE] [VoiceMemoUIController] 녹음 완료 상태: '{itemText}'");
                }
                // 녹음 파일이 있는 경우
                else if (recorderUI.HasRecording())
                {
                    itemText = recorderUI.GetStatusText(); // 파일명
                    Debug.Log($"[🔍TRACE] [VoiceMemoUIController] 녹음 파일 있음: '{itemText}'");
                }
            }
            
            // VoiceRecorderUI에 상태가 없으면 InputField 텍스트 확인
            if (string.IsNullOrEmpty(itemText) && inputField != null && !string.IsNullOrEmpty(inputField.text.Trim()))
            {
                itemText = inputField.text.Trim();
                Debug.Log($"[🔍TRACE] [VoiceMemoUIController] InputField 텍스트: '{itemText}'");
            }
            
            // 텍스트가 있으면 저장
            if (!string.IsNullOrEmpty(itemText))
            {
                voiceMemoData.Add(itemText);
                Debug.Log($"[🔍TRACE] [VoiceMemoUIController] voiceMemoData에 추가: '{itemText}'");
            }
            
            // 녹음 파일 경로 수집
            if (recorderUI != null && recorderUI.HasRecording())
            {
                string recordingPath = recorderUI.GetRecordedFilePath();
                if (!string.IsNullOrEmpty(recordingPath))
                {
                    voiceRecordingPaths.Add(recordingPath);
                    Debug.Log($"[VoiceMemoUIController] 녹음 파일 추가: {recordingPath}");
                }
            }
        }
        
        Debug.Log($"[🔍TRACE] [VoiceMemoUIController] 저장할 음성메모 아이템 수: {voiceMemoData.Count}개, 녹음 파일: {voiceRecordingPaths.Count}개");
        
        // 저장 로직 구현
        SaveVoiceMemo(inputTitle.text != null ? inputTitle.text : "", 
                     inputLocation.text != null ? inputLocation.text : "", 
                     voiceMemoData, 
                     voiceRecordingPaths);
        
        // 패널 정리 및 닫기
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
    
    void SaveVoiceMemo(string title, string location, List<string> items, List<string> recordingPaths)
    {
        if (currentMemo == null)
        {
            Debug.LogWarning("[VoiceMemoUIController] currentMemo가 null입니다! 저장할 수 없습니다.");
            return;
        }
        
        MemoData memoData = currentMemo.GetComponent<MemoData>();
        if (memoData == null)
        {
            Debug.LogWarning("[VoiceMemoUIController] MemoData가 없습니다!");
            return;
        }
        
        // 메모 데이터 저장
        memoData.title = title ?? "";
        memoData.body = string.Join("\n", items); // 음성메모 아이템들을 줄바꿈으로 합쳐서 저장
        memoData.content = memoData.body;
        memoData.location = location ?? "";
        memoData.memoType = "voicememo"; // 음성메모 타입으로 저장
        
        // 녹음 파일 경로 저장
        memoData.voiceRecordingPaths.Clear();
        if (recordingPaths != null && recordingPaths.Count > 0)
        {
            memoData.voiceRecordingPaths.AddRange(recordingPaths);
            Debug.Log($"[VoiceMemoUIController] 녹음 파일 {recordingPaths.Count}개 저장");
        }
        
        // 날짜 저장
        if (calendarController != null)
        {
            System.DateTime selectedDate = calendarController.GetSelectedDate();
            memoData.DueDateDateTime = selectedDate;
            Debug.Log($"[VoiceMemoUIController] 선택된 날짜 저장: {selectedDate:yyyy-MM-dd}");
        }
        
        // 시간 저장
        if (timePickerController != null)
        {
            string selectedTime = timePickerController.GetSelectedTimeString();
            memoData.dueTime = selectedTime;
            Debug.Log($"[VoiceMemoUIController] 선택된 시간 저장: {selectedTime}");
        }
        
        // 긴급도 저장
        if (emergencyButtonManager != null)
        {
            int emergencyIndex = emergencyButtonManager.GetSelectedButtonIndex();
            memoData.emergencyLevel = emergencyIndex + 1;
            Debug.Log($"[VoiceMemoUIController] 선택된 긴급도 저장: {memoData.emergencyLevel} (인덱스: {emergencyIndex})");
        }
        
        // JSON 저장
        if (pinStore != null)
        {
            pinStore.SaveTextMemoById(memoData.id, memoData.title, memoData.body, memoData.location);
            pinStore.UpdateMemoDueDate(memoData.id, memoData.dueDate);
            pinStore.UpdateMemoDueTime(memoData.id, memoData.dueTime);
            pinStore.UpdateMemoEmergencyLevel(memoData.id, memoData.emergencyLevel);
            pinStore.UpdateMemoType(memoData.id, "voicememo");
            pinStore.UpdateMemoVoiceRecordings(memoData.id, memoData.voiceRecordingPaths);
            
            Debug.Log($"[VoiceMemoUIController] 음성메모 저장 완료: ID={memoData.id}, 제목={memoData.title}, 아이템 수={items.Count}, 녹음 파일 수={recordingPaths?.Count ?? 0}");
        }
        else
        {
            Debug.LogWarning("[VoiceMemoUIController] pinStore가 null입니다! TabPinCreate를 할당해주세요.");
        }
    }
    
    void OnCloseClicked()
    {
        // 패널 정리 및 닫기
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
    /// 모든 음성메모 아이템 삭제
    /// </summary>
    /// <param name="recreateMinSlots">true면 최소 슬롯 재생성, false면 생성 안함</param>
    /// <param name="resetLayout">true면 레이아웃 리셋, false면 리셋 안함</param>
    void ClearAllItems(bool recreateMinSlots = true, bool resetLayout = true)
    {
        foreach (var item in voiceMemoItems)
        {
            if (item != null) Destroy(item);
        }
        voiceMemoItems.Clear();
        
        // 씬에 처음부터 있는 아이템도 삭제 (voiceMemoContent의 모든 자식 중 addItemButton 제외)
        if (voiceMemoContent != null)
        {
            List<Transform> itemsToRemove = new List<Transform>();
            foreach (Transform child in voiceMemoContent)
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
                Debug.Log($"[VoiceMemoUIController] 씬에 있던 기존 아이템 {itemsToRemove.Count}개 삭제");
            }
        }
        
        // 레이아웃 상태 리셋 (옵션) - 실제 레이아웃은 TimePickerController/CalendarController가 관리
        if (resetLayout)
        {
            lastPushAmount = 0f;
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
        
        Debug.Log($"[VoiceMemoUIController] 모든 아이템 삭제 완료 - recreateMinSlots={recreateMinSlots}, resetLayout={resetLayout}");
    }
    
    // 기존 음성메모 로드 (편집 모드)
    public void LoadVoiceMemo(string title, string location, List<string> items, List<string> recordingPaths = null)
    {
        Debug.Log($"[VoiceMemoUIController] LoadVoiceMemo 시작: title={title}, items.Count={items.Count}, recordingPaths.Count={recordingPaths?.Count ?? 0}");
        
        // 코루틴으로 로드 (Destroy 후 한 프레임 대기 필요)
        StartCoroutine(LoadVoiceMemoCoroutine(title, location, items, recordingPaths ?? new List<string>()));
    }
    
    /// <summary>
    /// 음성메모 로드 코루틴 (Destroy 후 한 프레임 대기하여 아이템 생성)
    /// </summary>
    private System.Collections.IEnumerator LoadVoiceMemoCoroutine(string title, string location, List<string> items, List<string> recordingPaths)
    {
        // 기존 아이템 삭제 (최소 슬롯 재생성 안함, 레이아웃 리셋)
        ClearAllItems(recreateMinSlots: false, resetLayout: true);
        
        // Destroy가 다음 프레임에 실행되므로 한 프레임 대기
        yield return null;
        
        // 레이아웃 변수 리셋
        lastPushAmount = 0f;
        
        Debug.Log($"[VoiceMemoUIController] LoadVoiceMemoCoroutine - 레이아웃 리셋 완료");
        Debug.Log($"[🔍TRACE] [VoiceMemoUIController] 로드할 items: {items.Count}개");
        for (int j = 0; j < items.Count; j++)
        {
            Debug.Log($"[🔍TRACE] [VoiceMemoUIController]   items[{j}]: '{items[j]}'");
        }
        Debug.Log($"[🔍TRACE] [VoiceMemoUIController] 로드할 recordingPaths: {recordingPaths.Count}개");
        
        if (inputTitle != null)
            inputTitle.text = title;
        if (inputLocation != null)
            inputLocation.text = location;
        
        // Outline 색상 업데이트 (데이터 로드 후)
        InitializeOutlineColors();
        
        // 최소 슬롯 수와 로드할 아이템 수 중 큰 값만큼 생성 (레이아웃 업데이트 건너뜀)
        int itemsToCreate = Mathf.Max(minVisibleSlots, items.Count);
        itemsToCreate = Mathf.Max(itemsToCreate, recordingPaths.Count); // 녹음 파일 수도 고려
        itemsToCreate = Mathf.Min(itemsToCreate, maxItems); // 최대 3개까지
        
        for (int i = 0; i < itemsToCreate; i++)
        {
            string content = i < items.Count ? items[i] : "";
            
            // "음성 녹음X" 형태면 InputField에 넣지 않음
            bool isVoiceRecordingStatus = !string.IsNullOrEmpty(content) && content.StartsWith("음성 녹음");
            string itemContent = isVoiceRecordingStatus ? "" : content;
            
            Debug.Log($"[🔍TRACE] [VoiceMemoUIController] 아이템 {i} 생성 - content: '{content}', isVoiceRecordingStatus: {isVoiceRecordingStatus}");
            
            GameObject itemObj = CreateVoiceMemoItem(itemContent, skipLayoutUpdate: true);
            
            // VoiceRecorderUI 컴포넌트 가져오기
            VoiceRecorderUI recorderUI = itemObj != null ? itemObj.GetComponent<VoiceRecorderUI>() : null;
            
            // 저장된 내용이 "음성 녹음X" 형태인지 확인
            if (isVoiceRecordingStatus && recorderUI != null)
            {
                // "음성 녹음X" 상태 복원
                recorderUI.SetStatusText(content);
                Debug.Log($"[🔍TRACE] [VoiceMemoUIController] 아이템 {i}에 상태 복원: {content}");
            }
            // 녹음 파일 경로 설정
            else if (itemObj != null && i < recordingPaths.Count && !string.IsNullOrEmpty(recordingPaths[i]))
            {
                if (recorderUI != null)
                {
                    recorderUI.SetRecordedFilePath(recordingPaths[i]);
                    Debug.Log($"[VoiceMemoUIController] 아이템 {i}에 녹음 파일 설정: {recordingPaths[i]}");
                }
            }
        }
        
        Debug.Log($"[VoiceMemoUIController] LoadVoiceMemo 완료: 생성된 아이템 수={voiceMemoItems.Count}");
        
        // 로드 완료 후 레이아웃 한 번만 업데이트
        UpdateLayoutBasedOnItemCount();
        
        // 로드 후 3개면 추가 버튼 숨기기
        if (voiceMemoItems.Count >= maxItems && addItemButton != null)
        {
            addItemButton.gameObject.SetActive(false);
            Debug.Log("[VoiceMemoUIController] 로드 완료 - 3개 도달하여 추가 버튼 숨김");
        }
    }
    
    /// <summary>
    /// 아이템 수에 따른 밀림 양 업데이트 (TimePickerController/CalendarController에서 사용)
    /// ★★★ 실제 레이아웃 조정은 TimePickerController/CalendarController가 담당 ★★★
    /// </summary>
    void UpdateLayoutBasedOnItemCount()
    {
        int currentItemCount = voiceMemoItems.Count;
        
        // 기본 슬롯 수를 초과한 아이템 수 계산 (3개일 때만 1개 초과)
        int extraItems = Mathf.Max(0, currentItemCount - minVisibleSlots);
        
        // 총 밀림 양 계산 및 저장 (TimePickerController/CalendarController에서 LastPushAmount로 접근)
        lastPushAmount = extraItems * pushAmountPerItem;
        
        Debug.Log($"[VoiceMemoUIController] 밀림 양 업데이트 - 아이템: {currentItemCount}, extra: {extraItems}, lastPushAmount: {lastPushAmount}");
    }
    
    /// <summary>
    /// 현재 아이템 수에 따른 밀림 양 계산 (외부에서 호출 가능)
    /// CalendarController/TimePickerController에서 사용
    /// </summary>
    public float GetCurrentPushAmount()
    {
        int currentItemCount = voiceMemoItems.Count;
        int extraItems = Mathf.Max(0, currentItemCount - minVisibleSlots);
        return extraItems * pushAmountPerItem;
    }
    
    // InputField Outline 색상 관리
    
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
            Debug.Log($"[VoiceMemoUIController] Title Outline 색상 변경: {(hasFilled ? "채워짐" : "비어있음")} - {newColor}");
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
            Debug.Log($"[VoiceMemoUIController] Location Outline 색상 변경: {(hasFilled ? "채워짐" : "비어있음")} - {newColor}");
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
}
