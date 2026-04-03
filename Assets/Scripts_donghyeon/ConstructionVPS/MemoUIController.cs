
// 메모를 입력하고 편집하는 모든 UI를 관리하는 컨트롤러
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

public class MemoUIController : MonoBehaviour
{
    [Header("Assignee Check Toggle")]
    [Tooltip("지정자 체크박스(토글) 넣는 자리")]
    [SerializeField] private Toggle assigneeCheckToggle;

    [Header("Background Image")]
    [Tooltip("MemoUI가 켜질 때 함께 활성화되는 배경 이미지")]
    [SerializeField] private GameObject bgImage;

    [Header("MemoUI Container")]
    [Tooltip("패널들의 부모 오브젝트 (MemoUI)")]
    [SerializeField] private GameObject memoUIContainer;

    [Header("AttachMemo UI (저장/닫기 후 복원용)")]
    [Tooltip("TopLeftUI 오브젝트")]
    [SerializeField] private GameObject topLeftUI;
    [Tooltip("AttachMemo 컨테이너 오브젝트")]
    [SerializeField] private GameObject attachMemoContainer;
    [Tooltip("AttachMemoIcon 오브젝트")]
    [SerializeField] private GameObject attachMemoIcon;

    [Header("Bottom Bar")]
    [Tooltip("BottomBar 오브젝트를 넣는 자리")]
    [SerializeField] private GameObject bottomBar;

    [Header("Buttons (inside BottomBar)")]
    [Tooltip("BottomBar 안의 버튼들을 넣는 자리")]
    [SerializeField] private Button btnText;
    [SerializeField] private Button btnVoice;
    [SerializeField] private Button btnChecklist;
    [SerializeField] private Button btnImage;

    [Header("Panels (inside BottomBar)")]
    [Tooltip("BottomBar 안의 패널들을 넣는 자리")]
    [SerializeField] private GameObject panelText;
    [SerializeField] private GameObject panelVoice;
    [SerializeField] private GameObject panelChecklist;
    [SerializeField] private GameObject panelImage;

    [Header("Image Memo Controller")]
    [Tooltip("Panel_ImageMemo 전용 컨트롤러")]
    [SerializeField] private ImageMemoUIController imageMemoController;
    
    [Header("Checklist Controller")]
    [Tooltip("Panel_Checklist 전용 컨트롤러")]
    [SerializeField] private ChecklistUIController checklistUIController;
    
    [Header("Voice Memo Controller")]
    [Tooltip("Panel_VoiceMemo 전용 컨트롤러")]
    [SerializeField] private VoiceMemoUIController voiceMemoUIController;

    [Header("Text Memo Inputs (Panel Text)")]
    [Tooltip("TextMemo 패널 안의 TMP_InputField(타이틀) 넣는 자리")]
    [SerializeField] private TMP_InputField inputTitle;
    [Tooltip("TextMemo 패널 안의 TMP_InputField(내용) 넣는 자리")]
    [SerializeField] private TMP_InputField inputBody;
    [Tooltip("TextMemo 패널 안의 TMP_InputField(위치) 넣는 자리")]
    [SerializeField] private TMP_InputField inputLocation;
    
    [Header("InputField Outlines")]
    [Tooltip("InputField_Title의 Outline 컴포넌트")]
    [SerializeField] private Outline titleOutline;
    [Tooltip("InputField_Body의 Outline 컴포넌트")]
    [SerializeField] private Outline bodyOutline;
    
    [Header("Outline Colors")]
    [SerializeField] private Color emptyOutlineColor = new Color(0x96 / 255f, 0xCB / 255f, 0xE0 / 255f); // #96CBE0 (비어있을 때)
    [SerializeField] private Color filledOutlineColor = new Color(0xD9 / 255f, 0xD9 / 255f, 0xD9 / 255f); // #D9D9D9 (채워졌을 때)

    // 저장 버튼 & 옵션
    [Header("Text Memo Save Button (Panel Text)")]
    [Tooltip("TextMemo 패널 안의 저장 버튼 넣는 자리")]
    [SerializeField] private Button btnSaveText;
    [Tooltip("메모 부착 직후 자동으로 텍스트 패널을 열지 여부")]
    [SerializeField] private bool autoOpenTextPanelOnPlaced = true;

    // 핀 저장소 - JSON 파일에 메모 데이터를 저장하는 TabPinCreate 참조
    [Header("TabPinCreate")]
    [Tooltip("TabPinCreate를 넣는 자리 (JSON 저장 갱신용)")]
    [SerializeField] private TabPinCreate pinStore;

    [Header("Keyboard Focus")]
    [Tooltip("텍스트 패널이 열리면 자동으로 입력칸에 커서를 놓고 키보드를 띄울지 여부")]
    [SerializeField] private bool autoFocusTextInput = true;

    [Header("Back/Close Buttons")]
    [Tooltip("각 패널의 '뒤로가기/닫기' 버튼들을 전부 넣는 자리 (저장하지 않고 닫기)")]
    [SerializeField] private Button[] backButtons;
    [Tooltip("텍스트 패널의 닫기 버튼 (Btn_TextClose) - 저장하지 않고 닫기")]
    [SerializeField] private Button btnTextClose;

    // 메타 정보 (날짜/시간/사용자ID) & 지정자 UI
    [Header("Meta / Assignee UI (Panel Text)")]
    [Tooltip("메모 패널 안에 날짜를 표시할 TMP_Text 넣는 자리")]
    [SerializeField] private TMP_Text dateText;
    [Tooltip("메모 패널 안에 사용자ID를 표시할 TMP_Text 넣는 자리")]
    [SerializeField] private TMP_Text userIdText;
    [Tooltip("지정자(메모를 봐야하는 사람) 입력 TMP_InputField 넣는 자리")]
    [SerializeField] private TMP_InputField inputAssignee;

    // 달력 및 시간 선택 컨트롤러
    [Header("Calendar & Time")]
    [Tooltip("달력 UI를 관리하는 CalendarController를 넣는 자리")]
    [SerializeField] private CalendarController calendarController;
    [Tooltip("시간 선택 UI를 관리하는 TimePickerController를 넣는 자리")]
    [SerializeField] private TimePickerController timePickerController;
    
    // 긴급도 버튼 매니저
    [Header("Emergency")]
    [Tooltip("긴급도 버튼을 관리하는 EmergencyButtonManager를 넣는 자리")]
    [SerializeField] private EmergencyButtonManager emergencyButtonManager;

    // 사용자 ID 관련 설정
    [Header("User ID")]
    [Tooltip("사용자 ID를 PlayerPrefs에서 읽을 키(없으면 디바이스ID 일부로 대체)")]
    [SerializeField] private string userIdPrefKey = "MEMO_USER_ID";
    [Tooltip("PlayerPrefs에 userId가 없을 때 기기 고유 번호를 대신 사용할지")]
    [SerializeField] private bool useDeviceIdFallback = true;
    [Tooltip("패널을 열 때마다 지정자 입력칸을 비울지 결정")]
    [SerializeField] private bool clearAssigneeOnOpen = false;

    [Header("Debug")]
    [Tooltip("디버그 로그 출력 여부")]
    [SerializeField] private bool logDebug = false;

    // 현재 편집 중인 메모 GameObject
    private GameObject currentMemo;

    // Draft 시스템 - 입력 중 데이터가 지워지는 것을 방지하기 위한 임시 저장소
    private bool isLoadingUI = false;   // UI 로딩 중 플래그 (덮어쓰기 방지용)
    private string draftTitle = "";     // 임시 저장된 제목
    private string draftBody = "";      // 임시 저장된 본문
    private string draftLocation = "";  // 임시 저장된 위치
    private string draftAssignee = "";  // 임시 저장된 지정자 이름

    // 지정자 확정 이벤트 - 외부 스크립트에 지정자 확정 알림
    public event Action<string> OnAssigneeConfirmed;

    private void Awake()
    {
        // 초기 상태: MemoUI 관련 요소들 비활성화 (씬 시작 시 보이지 않도록)
        ForceHideMemoUIOnStart();
        
        // Outline 컴포넌트 자동 검색 (자식 오브젝트에서도 찾기)
        if (titleOutline == null && inputTitle != null)
        {
            titleOutline = inputTitle.GetComponent<Outline>();
            if (titleOutline == null)
            {
                titleOutline = inputTitle.GetComponentInChildren<Outline>();
            }
            Debug.Log($"[MemoUIController] titleOutline 자동 검색 결과: {(titleOutline != null ? titleOutline.gameObject.name : "NULL")}");
        }
        if (bodyOutline == null && inputBody != null)
        {
            bodyOutline = inputBody.GetComponent<Outline>();
            if (bodyOutline == null)
            {
                bodyOutline = inputBody.GetComponentInChildren<Outline>();
            }
            Debug.Log($"[MemoUIController] bodyOutline 자동 검색 결과: {(bodyOutline != null ? bodyOutline.gameObject.name : "NULL")}");
        }
        
        // Inspector에서 할당된 경우 확인
        Debug.Log($"[MemoUIController] Outline 할당 상태 - titleOutline: {(titleOutline != null ? "할당됨" : "NULL")}, bodyOutline: {(bodyOutline != null ? "할당됨" : "NULL")}");
        
        // 하단바 버튼 연결 체크
        Debug.Log($"[MemoUIController] Awake - btnText: {(btnText != null)}, btnVoice: {(btnVoice != null)}, btnChecklist: {(btnChecklist != null)}, btnImage: {(btnImage != null)}");
        Debug.Log($"[MemoUIController] Awake - panelText: {(panelText != null)}, panelVoice: {(panelVoice != null)}, panelChecklist: {(panelChecklist != null)}, panelImage: {(panelImage != null)}");
        
        if (!btnText || !btnVoice || !btnChecklist || !btnImage)
        {
            Debug.LogWarning("[MemoUIController] One or more bottom bar buttons are not assigned.");
        }

        // 하단바 버튼 클릭 이벤트 연결 - 각 버튼 클릭 시 해당 패널 열기
        if (btnText)
        {
            btnText.onClick.AddListener(() => {
                Debug.Log("[MemoUIController] btnText 클릭됨");
                OpenPanel(panelText);
            });
        }
        if (btnVoice)
        {
            btnVoice.onClick.AddListener(() => {
                Debug.Log("[MemoUIController] btnVoice 클릭됨");
                OpenPanel(panelVoice);
            });
        }
        if (btnChecklist)
        {
            btnChecklist.onClick.AddListener(() => {
                Debug.Log("[MemoUIController] btnChecklist 클릭됨");
                OpenPanel(panelChecklist);
            });
        }
        if (btnImage)
        {
            btnImage.onClick.AddListener(() => {
                Debug.Log("[MemoUIController] ★★★ btnImage 클릭됨!");
                Debug.Log($"[MemoUIController] ★★★ currentMemo: {(currentMemo ? currentMemo.name : "null")}");
                OpenPanel(panelImage);
            });
        }

        // 저장 버튼 클릭 이벤트 연결
        if (btnSaveText)
        {
            // 중복 등록 방지를 위한 기존 리스너 제거
            btnSaveText.onClick.RemoveListener(SaveTextMemoNow);
            btnSaveText.onClick.AddListener(SaveTextMemoNow);
            Debug.Log("[MemoUIController] btnSaveText 리스너 연결 완료");
        }
        else
        {
            Debug.LogWarning("[MemoUIController] btnSaveText가 할당되지 않았습니다!");
        }
        
        // 닫기 버튼 클릭 이벤트 연결 (저장하지 않고 닫기)
        if (btnTextClose)
        {
            btnTextClose.onClick.RemoveListener(CloseWithoutSaving);
            btnTextClose.onClick.AddListener(CloseWithoutSaving);
            Debug.Log("[MemoUIController] btnTextClose 리스너 연결 완료");
        }
        else
        {
            Debug.LogWarning("[MemoUIController] btnTextClose가 할당되지 않았습니다!");
        }

        // 입력 변화 감지 리스너 연결 (Draft 시스템)
        WireDraftListeners();

        // 지정자 UI 이벤트 연결
        WireAssigneeListeners();

        // 뒤로가기 버튼들 연결
        WireBackButtons();

        // 지정자 토글 초기 상태 설정
        UpdateAssigneeToggleVisibility();
        
        // ChecklistUIController 자동 할당
        if (checklistUIController == null)
        {
            checklistUIController = FindObjectOfType<ChecklistUIController>();
            if (checklistUIController != null)
            {
                Debug.Log("[MemoUIController] ChecklistUIController를 자동으로 찾았습니다.");
            }
        }
    }

    // Unity 2022.3+ Canvas 리빌드 호환성
    // 초기 UI 상태는 Unity 에디터에서 설정 (BottomBar: 비활성화, Panels: 비활성화)
    // OnEnable/Start에서 UI를 변경하지 않음

    // 새 메모 부착 완료 시 실행 함수
    public void OnMemoPlaced(GameObject memo)
    {
        // 현재 편집 중인 메모로 설정
        currentMemo = memo;

        Debug.Log($"[MemoUIController] ★★★ OnMemoPlaced 호출: memo={(currentMemo ? currentMemo.name : "null")}");
        
        if (currentMemo != null)
        {
            MemoData memoData = currentMemo.GetComponent<MemoData>();
            if (memoData != null)
            {
                Debug.Log($"[MemoUIController] ★★★ MemoData 확인: id={memoData.id}");
            }
        }

        // 하단바 표시 및 텍스트 패널 열기
        ShowBottomBarOnly();
        if (autoOpenTextPanelOnPlaced)
            OpenPanel(panelText);
        
        // 새 메모에 대해 AssigneeDropdown 초기화 (담당자 없음 상태)
        InitializeAssigneeDropdownForNewMemo();
    }
    
    // 새 메모용 AssigneeDropdown 초기화 함수
    private void InitializeAssigneeDropdownForNewMemo()
    {
        // 새 메모의 MemoData에서 ID 가져오기
        MemoData memoData = currentMemo?.GetComponent<MemoData>();
        if (memoData != null)
        {
            AssigneeDropdownManager.SetCurrentMemoId(memoData.id);
        }
        
        // AssigneeDropdownManager 찾아서 빈 상태로 초기화
        AssigneeDropdownManager dropdownManager = FindObjectOfType<AssigneeDropdownManager>();
        if (dropdownManager != null)
        {
            // 새 메모는 담당자가 없으므로 빈 문자열로 초기화
            dropdownManager.LoadAssignee("");
            Debug.Log("[MemoUIController] 새 메모 - AssigneeDropdown 초기화 완료 (담당자 없음)");
        }
    }

    // 기존 메모 선택 시 실행 함수
    public void OnMemoSelected(GameObject memo)
    {
        currentMemo = memo;

        Debug.Log($"[MemoUIController] [###] OnMemoSelected 호출: memo={(currentMemo ? currentMemo.name : "null")}");

        // 하단바 표시
        ShowBottomBarOnly();
        
        // MemoData에서 memoType 확인하여 올바른 패널 열기
        if (currentMemo != null)
        {
            MemoData memoData = currentMemo.GetComponent<MemoData>();
            if (memoData != null)
            {
                Debug.Log($"[MemoUIController] [###] OnMemoSelected - MemoData 확인: id={memoData.id}, memoType={memoData.memoType}");
                
                // memoType에 따라 적절한 패널 열기
                if (memoData.memoType == "image")
                {
                    Debug.Log("[MemoUIController] [###] 이미지 메모 → 이미지 패널 열기");
                    OpenPanel(panelImage);
                }
                else if (memoData.memoType == "checklist")
                {
                    Debug.Log("[MemoUIController] [###] 체크리스트 메모 → 체크리스트 패널 열기");
                    OpenPanel(panelChecklist);
                }
                else if (memoData.memoType == "voicememo")
                {
                    Debug.Log("[MemoUIController] [###] 음성 메모 → 음성메모 패널 열기");
                    OpenPanel(panelVoice);
                }
                else
                {
                    Debug.Log("[MemoUIController] [###] 텍스트 메모 → 텍스트 패널 열기");
                    OpenPanel(panelText);
                }
            }
            else
            {
                Debug.LogWarning("[MemoUIController] [###] MemoData가 없음 → 기본(텍스트) 패널 열기");
                OpenPanel(panelText);
            }
        }
        else
        {
            Debug.LogWarning("[MemoUIController] [###] currentMemo가 null → 기본(텍스트) 패널 열기");
            OpenPanel(panelText);
        }

        // AssigneeDropdownManager 업데이트 (드롭다운 방식)
        UpdateAssigneeDropdown();
        
        // 저장된 날짜, 시간, 긴급도 불러오기
        LoadMemoMetadata();
    }
    
    // 저장된 메모의 날짜, 시간, 긴급도를 UI에 로드
    private void LoadMemoMetadata()
    {
        if (currentMemo == null) return;
        
        MemoData memoData = currentMemo.GetComponent<MemoData>();
        if (memoData == null) return;
        
        // 날짜 로드
        if (calendarController != null && memoData.DueDateDateTime.HasValue)
        {
            calendarController.SetSelectedDate(memoData.DueDateDateTime.Value);
            if (logDebug)
                Debug.Log($"[MemoUIController] 날짜 로드: {memoData.DueDateDateTime.Value:yyyy-MM-dd}");
        }
        
        // 시간 로드
        if (timePickerController != null && !string.IsNullOrEmpty(memoData.dueTime))
        {
            // HH:mm 형식 파싱
            string[] timeParts = memoData.dueTime.Split(':');
            if (timeParts.Length == 2)
            {
                if (int.TryParse(timeParts[0], out int hour) && int.TryParse(timeParts[1], out int minute))
                {
                    timePickerController.SetTime(hour, minute);
                    if (logDebug)
                        Debug.Log($"[MemoUIController] 시간 로드: {memoData.dueTime}");
                }
            }
        }
        
        // 긴급도 로드
        if (emergencyButtonManager != null && memoData.emergencyLevel > 0)
        {
            // emergencyLevel은 1~3, 버튼 인덱스는 0~2
            int buttonIndex = memoData.emergencyLevel - 1;
            emergencyButtonManager.SetSelectedButton(buttonIndex);
            if (logDebug)
                Debug.Log($"[MemoUIController] 긴급도 로드: {memoData.emergencyLevel} (버튼 인덱스: {buttonIndex})");
        }
        else if (emergencyButtonManager != null)
        {
            // 긴급도가 설정되지 않았으면 선택 해제
            emergencyButtonManager.ClearSelection();
        }
    }

    // AssigneeDropdown 상태를 현재 메모의 assignee에 맞춰 업데이트 (드롭다운 방식)
    private void UpdateAssigneeDropdown()
    {
        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] [MemoUIController] UpdateAssigneeDropdown 시작");

        if (currentMemo == null)
        {
            Debug.LogWarning($"★★★ [ASSIGNEE_DROPDOWN] [MemoUIController] ✗ currentMemo가 null입니다!");
            return;
        }

        MemoData memoData = currentMemo.GetComponent<MemoData>();
        if (memoData == null)
        {
            Debug.LogWarning($"★★★ [ASSIGNEE_DROPDOWN] [MemoUIController] ✗ currentMemo에 MemoData가 없습니다! currentMemo={currentMemo.name}");
            return;
        }

        Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] [MemoUIController] ✓ MemoData 찾음: id={memoData.id}, title={memoData.title}");

        // AssigneeDropdownManager에 현재 메모 ID 설정
        AssigneeDropdownManager.SetCurrentMemoId(memoData.id);

        // 드롭다운 매니저를 찾아서 저장된 assignee 값 불러오기
        AssigneeDropdownManager dropdownManager = FindObjectOfType<AssigneeDropdownManager>();
        if (dropdownManager != null)
        {
            Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] [MemoUIController] ✓ AssigneeDropdownManager 찾음");

            // TabPinCreate에서 현재 메모의 assignee 값 가져오기
            TabPinCreate tabPinCreate = FindObjectOfType<TabPinCreate>();
            if (tabPinCreate != null)
            {
                string assigneeName = tabPinCreate.GetMemoAssignee(memoData.id);
                Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] [MemoUIController] ✓ TabPinCreate에서 데이터 가져옴: assignee={assigneeName}");

                dropdownManager.LoadAssignee(assigneeName);
                Debug.Log($"★★★ [ASSIGNEE_DROPDOWN] [MemoUIController] ✓ Assignee 상태 로드 완료");
            }
            else
            {
                Debug.LogWarning($"★★★ [ASSIGNEE_DROPDOWN] [MemoUIController] ✗ TabPinCreate를 찾을 수 없습니다!");
            }
        }
        else
        {
            Debug.LogWarning($"★★★ [ASSIGNEE_DROPDOWN] [MemoUIController] ✗ AssigneeDropdownManager를 찾을 수 없습니다!");
        }
    }

    // (기존 Toggle 방식 - MemoItems 페이지에서 사용하기 위해 유지)
    // AssigneeToggle 상태를 현재 메모의 isAssigned에 맞춰 업데이트
    private void UpdateAssigneeToggle()
    {
        Debug.Log($"★★★ [ASSIGNEE] [MemoUIController] UpdateAssigneeToggle 시작");

        if (currentMemo == null)
        {
            Debug.LogWarning($"★★★ [ASSIGNEE] [MemoUIController] ✗ currentMemo가 null입니다!");
            return;
        }

        MemoData memoData = currentMemo.GetComponent<MemoData>();
        if (memoData == null)
        {
            Debug.LogWarning($"★★★ [ASSIGNEE] [MemoUIController] ✗ currentMemo에 MemoData가 없습니다! currentMemo={currentMemo.name}");
            return;
        }

        Debug.Log($"★★★ [ASSIGNEE] [MemoUIController] ✓ MemoData 찾음: id={memoData.id}, title={memoData.title}");

        // AssigneeToggleManager에 현재 메모 ID 설정
        AssigneeToggleManager.SetCurrentMemoId(memoData.id);

        // Toggle과 Input 상태를 메모의 isAssigned와 assignee로 업데이트
        AssigneeToggleManager toggleManager = FindObjectOfType<AssigneeToggleManager>();
        if (toggleManager != null)
        {
            Debug.Log($"★★★ [ASSIGNEE] [MemoUIController] ✓ AssigneeToggleManager 찾음");

            // TabPinCreate에서 현재 메모의 isAssigned와 assignee 값 가져오기
            TabPinCreate tabPinCreate = FindObjectOfType<TabPinCreate>();
            if (tabPinCreate != null)
            {
                bool isAssigned = tabPinCreate.GetMemoAssignedState(memoData.id);
                string assigneeName = tabPinCreate.GetMemoAssignee(memoData.id);
                Debug.Log($"★★★ [ASSIGNEE] [MemoUIController] ✓ TabPinCreate에서 데이터 가져옴: isAssigned={isAssigned}, assignee={assigneeName}");

                toggleManager.LoadAssigneeState(isAssigned, assigneeName);
                Debug.Log($"★★★ [ASSIGNEE] [MemoUIController] ✓ Assignee 상태 로드 완료");
            }
            else
            {
                Debug.LogWarning($"★★★ [ASSIGNEE] [MemoUIController] ✗ TabPinCreate를 찾을 수 없습니다!");
            }
        }
        else
        {
            Debug.LogWarning($"★★★ [ASSIGNEE] [MemoUIController] ✗ AssigneeToggleManager를 찾을 수 없습니다!");
        }
    }

    // 모든 UI 닫기 (뒤로가기 버튼에서 호출) - 자동 저장 포함
    public void CloseAll()
    {
        Debug.Log("[MemoUIController] CloseAll() 시작 - 자동 저장 포함");

        // 닫기 전 작성 중인 내용 자동 저장
        SaveTextMemoIfOpen();

        // 모든 UI 숨김
        HideAllPanels();
        ForceHideBottomBar();
        
        Debug.Log($"[MemoUIController] CloseAll() - BottomBar 상태: {(bottomBar ? bottomBar.activeSelf.ToString() : "null")}");
        Debug.Log($"[MemoUIController] CloseAll() - PanelText 상태: {(panelText ? panelText.activeSelf.ToString() : "null")}");
        
        // 달력 및 시간 선택기 닫기
        if (calendarController != null)
        {
            calendarController.CloseCalendar();
        }
        
        if (timePickerController != null)
        {
            timePickerController.CloseTimePicker();
        }

        currentMemo = null;
        
        Debug.Log("[MemoUIController] CloseAll() 완료");
    }
    
    // 저장하지 않고 모든 UI 닫기 (Btn_TextClose에서 호출)
    public void CloseWithoutSaving()
    {
        Debug.Log("[MemoUIController] CloseWithoutSaving() 시작 - 저장 안함");

        // 저장하지 않고 모든 UI 숨김
        HideAllPanels();
        ForceHideBottomBar();
        
        Debug.Log($"[MemoUIController] CloseWithoutSaving() - BottomBar 상태: {(bottomBar ? bottomBar.activeSelf.ToString() : "null")}");
        Debug.Log($"[MemoUIController] CloseWithoutSaving() - PanelText 상태: {(panelText ? panelText.activeSelf.ToString() : "null")}");
        
        // 달력 및 시간 선택기 닫기
        if (calendarController != null)
        {
            calendarController.CloseCalendar();
        }
        
        if (timePickerController != null)
        {
            timePickerController.CloseTimePicker();
        }

        currentMemo = null;
        
        Debug.Log("[MemoUIController] CloseWithoutSaving() 완료");
    }

    // 하단바만 표시하고 모든 패널 숨기기 함수
    private void ShowBottomBarOnly()
    {
        Debug.Log("[MemoUIController] ShowBottomBarOnly() 호출");
        
        // MemoUI 컨테이너 활성화 (패널들의 부모)
        if (memoUIContainer) memoUIContainer.SetActive(true);
        
        // 하단바 활성화 & 모든 패널 비활성화
        if (bottomBar) bottomBar.SetActive(true);
        HideAllPanels();
        
        // BgImage는 패널이 열릴 때만 활성화 (BottomBar만 표시할 때는 비활성화)
        if (bgImage) bgImage.SetActive(false);
    }

    // 다른 패널은 모두 닫고 하나만 활성화
    private void OpenPanel(GameObject target)
    {
        Debug.Log($"[MemoUIController] OpenPanel 호출 - target: {(target ? target.name : "null")}");
        
        // 하단바가 비활성화 상태면 패널 열기 불가
        if (!bottomBar || !bottomBar.activeSelf)
        {
            Debug.Log("[MemoUIController] BottomBar is not active. Ignoring panel open.");
            return;
        }
        
        // MemoUI 컨테이너가 비활성화 상태면 활성화
        if (memoUIContainer && !memoUIContainer.activeSelf)
        {
            memoUIContainer.SetActive(true);
            Debug.Log("[MemoUIController] memoUIContainer 활성화");
        }

        // 패널 전환 전 현재 작성 중인 내용 저장
        SaveTextMemoIfOpen();

        // 모든 패널 닫기 & 요청한 패널만 활성화
        HideAllPanels();
        if (target)
        {
            target.SetActive(true);
            Debug.Log($"[MemoUIController] 패널 활성화: {target.name}, activeSelf: {target.activeSelf}");
            
            // 패널이 열릴 때 BgImage 활성화
            if (bgImage)
            {
                bgImage.SetActive(true);
                Debug.Log($"[MemoUIController] BgImage 활성화됨, activeSelf: {bgImage.activeSelf}");
            }
            else
            {
                Debug.LogWarning("[MemoUIController] bgImage 참조가 null입니다!");
            }
        }

        // 텍스트 패널일 경우 추가 처리
        if (target == panelText)
        {
            // MemoData에서 저장된 내용 불러오기
            LoadTextMemoToUI();

            // 자동 포커스
            if (autoFocusTextInput)
                StartCoroutine(FocusTextInputNextFrame());
        }
        
        // 이미지 패널일 경우 추가 처리
        if (target == panelImage)
        {
            Debug.Log($"[MemoUIController] ★★★ 이미지 패널 열림 - currentMemo: {(currentMemo ? currentMemo.name : "null")}");
            
            // ImageMemoUIController에 현재 메모 전달
            if (imageMemoController != null)
            {
                Debug.Log("[MemoUIController] ★★★ imageMemoController.OnPanelOpened() 호출 중...");
                imageMemoController.OnPanelOpened(currentMemo);
                Debug.Log("[MemoUIController] ★★★ imageMemoController.OnPanelOpened() 호출 완료");
            }
            else
            {
                Debug.LogError("[MemoUIController] ★★★ imageMemoController가 null입니다! Inspector에서 할당해주세요.");
            }
        }
        
        // 체크리스트 패널일 경우 추가 처리
        if (target == panelChecklist)
        {
            Debug.Log($"[MemoUIController] 체크리스트 패널 열림 - currentMemo: {(currentMemo ? currentMemo.name : "null")}");
            
            // ChecklistUIController에 현재 메모 전달
            if (checklistUIController != null)
            {
                Debug.Log("[MemoUIController] checklistUIController.OnPanelOpened() 호출 중...");
                checklistUIController.OnPanelOpened(currentMemo);
                Debug.Log("[MemoUIController] checklistUIController.OnPanelOpened() 호출 완료");
            }
            else
            {
                Debug.LogError("[MemoUIController] checklistUIController가 null입니다! Inspector에서 할당해주세요.");
            }
        }
        
        // 음성메모 패널일 경우 추가 처리
        if (target == panelVoice)
        {
            Debug.Log($"[MemoUIController] 음성메모 패널 열림 - currentMemo: {(currentMemo ? currentMemo.name : "null")}");
            
            // VoiceMemoUIController에 현재 메모 전달
            if (voiceMemoUIController != null)
            {
                if (currentMemo != null)
                {
                    MemoData memoData = currentMemo.GetComponent<MemoData>();
                    if (memoData != null)
                    {
                        // currentMemo 먼저 설정 (저장 시 필요)
                        voiceMemoUIController.SetCurrentMemo(currentMemo);
                        
                        // voiceRecordingPaths를 확인하여 로드
                        List<string> items = new List<string>();
                        if (!string.IsNullOrEmpty(memoData.body))
                        {
                            items = new List<string>(memoData.body.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries));
                        }
                        
                        Debug.Log($"[🔍TRACE] [MemoUIController] 음성메모 로드 시작");
                        Debug.Log($"[🔍TRACE] [MemoUIController]   memoData.body: '{memoData.body}'");
                        Debug.Log($"[🔍TRACE] [MemoUIController]   items.Count: {items.Count}");
                        for (int i = 0; i < items.Count; i++)
                        {
                            Debug.Log($"[🔍TRACE] [MemoUIController]     items[{i}]: '{items[i]}'");
                        }
                        
                        voiceMemoUIController.LoadVoiceMemo(
                            memoData.title, 
                            memoData.location, 
                            items, 
                            memoData.voiceRecordingPaths
                        );
                        Debug.Log($"[MemoUIController] VoiceMemoUIController에 메모 데이터 로드 완료 - items: {items.Count}, recordings: {memoData.voiceRecordingPaths?.Count ?? 0}");
                    }
                }
                else
                {
                    // 새 메모인 경우 빈 상태로 초기화
                    Debug.Log("[MemoUIController] 새 메모 - 빈 상태로 음성메모 초기화");
                    voiceMemoUIController.LoadVoiceMemo("", "", new List<string>(), new List<string>());
                }
            }
            else
            {
                Debug.LogError("[MemoUIController] voiceMemoUIController가 null입니다! Inspector에서 할당해주세요.");
            }
        }

        if (logDebug)
            Debug.Log($"[MemoUIController] OpenPanel: {(target ? target.name : "null")}, currentMemo: {(currentMemo ? currentMemo.name : "null")}");
    }

    // 모든 패널 숨기기 함수
    private void HideAllPanels()
    {
        Debug.Log("[MemoUIController] HideAllPanels() 호출");
        if (panelText)
        {
            panelText.SetActive(false);
            Debug.Log("[MemoUIController] panelText 비활성화");
        }
        if (panelVoice) panelVoice.SetActive(false);
        if (panelChecklist) panelChecklist.SetActive(false);
        if (panelImage) panelImage.SetActive(false);
    }

    // 하단바 강제 숨김 함수
    private void ForceHideBottomBar()
    {
        Debug.Log("[MemoUIController] ForceHideBottomBar() 호출");
        
        // 배경 이미지 비활성화
        if (bgImage)
        {
            bgImage.SetActive(false);
            Debug.Log("[MemoUIController] bgImage 비활성화");
        }
        
        // MemoUI 컨테이너 비활성화
        if (memoUIContainer)
        {
            memoUIContainer.SetActive(false);
            Debug.Log("[MemoUIController] memoUIContainer 비활성화");
        }
        
        if (bottomBar)
        {
            bottomBar.SetActive(false);
            Debug.Log("[MemoUIController] bottomBar 비활성화");
        }
        
        // AttachMemo UI 복원 (TopLeftUI, AttachMemo, AttachMemoIcon)
        RestoreAttachMemoUI();
    }
    
    // AttachMemo UI 복원 함수
    private void RestoreAttachMemoUI()
    {
        Debug.Log("[MemoUIController] RestoreAttachMemoUI() 호출");
        
        if (topLeftUI)
        {
            topLeftUI.SetActive(true);
            Debug.Log("[MemoUIController] topLeftUI 활성화");
        }
        
        if (attachMemoContainer)
        {
            attachMemoContainer.SetActive(true);
            Debug.Log("[MemoUIController] attachMemoContainer 활성화");
        }
        
        if (attachMemoIcon)
        {
            attachMemoIcon.SetActive(true);
            Debug.Log("[MemoUIController] attachMemoIcon 활성화");
        }
    }
    
    // 씬 시작 시 MemoUI 관련 요소들 비활성화
    private void ForceHideMemoUIOnStart()
    {
        if (logDebug)
            Debug.Log("[MemoUIController] ForceHideMemoUIOnStart() - 초기 MemoUI 비활성화");
        
        // 배경 이미지 비활성화
        if (bgImage)
            bgImage.SetActive(false);
        
        // MemoUI 컨테이너 비활성화
        if (memoUIContainer)
            memoUIContainer.SetActive(false);
        
        // BottomBar 비활성화
        if (bottomBar)
            bottomBar.SetActive(false);
        
        // 모든 패널 비활성화
        if (panelText)
            panelText.SetActive(false);
        if (panelVoice)
            panelVoice.SetActive(false);
        if (panelChecklist)
            panelChecklist.SetActive(false);
        if (panelImage)
            panelImage.SetActive(false);
    }

    // 뒤로가기 버튼들을 CloseAll 함수에 연결 함수
    private void WireBackButtons()
    {
        // 버튼 배열이 비어있으면 종료
        if (backButtons == null || backButtons.Length == 0)
        {
            if (logDebug) Debug.Log("[MemoUIController] backButtons is empty. (No close buttons wired)");
            return;
        }

        // 모든 뒤로가기 버튼에 CloseAll 연결
        for (int i = 0; i < backButtons.Length; i++)
        {
            Button b = backButtons[i];
            if (!b) continue;

            // 중복 연결 방지
            b.onClick.RemoveListener(CloseAll);
            b.onClick.AddListener(CloseAll);
        }

        if (logDebug) Debug.Log($"[MemoUIController] Wired backButtons: {backButtons.Length}");
    }

    // 입력칸 변화 감지 리스너 연결 함수 - Draft 시스템 구현
    private void WireDraftListeners()
    {
        // 제목 입력칸 변화 감지
        if (inputTitle)
        {
            inputTitle.onValueChanged.RemoveListener(OnTitleChanged);
            inputTitle.onValueChanged.AddListener(OnTitleChanged);
            
            // 선택 시 드롭다운 닫기
            inputTitle.onSelect.RemoveAllListeners();
            inputTitle.onSelect.AddListener((str) => CloseDropdownIfOpen());
        }

        // 본문 입력칸 변화 감지
        if (inputBody)
        {
            inputBody.onValueChanged.RemoveListener(OnBodyChanged);
            inputBody.onValueChanged.AddListener(OnBodyChanged);
            
            // 선택 시 드롭다운 닫기
            inputBody.onSelect.RemoveAllListeners();
            inputBody.onSelect.AddListener((str) => CloseDropdownIfOpen());
        }

        // 위치 입력칸 변화 감지
        if (inputLocation)
        {
            inputLocation.onValueChanged.RemoveListener(OnLocationChanged);
            inputLocation.onValueChanged.AddListener(OnLocationChanged);
        }
    }
    
    // InputField 선택 시 DropdownPanel과 Calendar 닫기 함수
    private void CloseDropdownIfOpen()
    {
        // Assignee Dropdown 닫기
        AssigneeDropdownManager dropdownManager = FindObjectOfType<AssigneeDropdownManager>();
        if (dropdownManager != null)
        {
            dropdownManager.CloseDropdown();
        }
        
        // Calendar 닫기
        if (calendarController != null)
        {
            calendarController.CloseCalendar();
        }
    }
    
    /// <summary>
    /// AssigneeButton의 Shadow 색상 업데이트
    /// </summary>
    private void UpdateAssigneeButtonOutlineColor()
    {
        // AssigneeDropdownManager를 통해 업데이트
        // (AssigneeDropdownManager가 LoadAssignee를 통해 자동으로 Shadow 색상을 업데이트함)
        // 여기서는 별도 작업 불필요
    }
    private void UpdateTitleOutlineColor()
    {
        if (titleOutline != null)
        {
            bool hasFilled = !string.IsNullOrWhiteSpace(draftTitle);
            titleOutline.effectColor = hasFilled ? filledOutlineColor : emptyOutlineColor;
            
            if (logDebug)
                Debug.Log($"[MemoUIController] Title Outline 색상 변경: {(hasFilled ? "채워짐" : "비어있음")} - {titleOutline.effectColor}");
        }
    }
    
    /// <summary>
    /// InputField_Body의 Outline 색상 업데이트
    /// </summary>
    private void UpdateBodyOutlineColor()
    {
        if (bodyOutline != null)
        {
            bool hasFilled = !string.IsNullOrWhiteSpace(draftBody);
            bodyOutline.effectColor = hasFilled ? filledOutlineColor : emptyOutlineColor;
            
            if (logDebug)
                Debug.Log($"[MemoUIController] Body Outline 색상 변경: {(hasFilled ? "채워짐" : "비어있음")} - {bodyOutline.effectColor}");
        }
    }

    // 제목 입력 변화 시 Draft에 임시 저장 함수
    private void OnTitleChanged(string v)
    {
        // UI 로딩 중이면 무시 (덮어쓰기 방지)
        if (isLoadingUI) return;

        // Draft에 임시 저장
        draftTitle = v ?? "";
        
        // Outline 색상 업데이트 (내용 있으면 채워진 색상, 없으면 빈 색상)
        UpdateTitleOutlineColor();
    }

    // 본문 입력 변화 시 Draft에 임시 저장 함수
    private void OnBodyChanged(string v)
    {

        if (isLoadingUI) return;
        draftBody = v ?? "";
        
        // Outline 색상 업데이트 (내용 있으면 채워진 색상, 없으면 빈 색상)
        UpdateBodyOutlineColor();
    }

    // 위치 입력 변화 시 Draft에 임시 저장 함수
    private void OnLocationChanged(string v)
    {
        if (isLoadingUI) return;
        draftLocation = v ?? "";
    }

    // 지정자 UI 이벤트 연결 함수
    private void WireAssigneeListeners()
    {
        // 지정자 입력칸 변화 감지
        if (inputAssignee)
        {
            inputAssignee.onValueChanged.RemoveListener(OnAssigneeChanged);
            inputAssignee.onValueChanged.AddListener(OnAssigneeChanged);
        }

        // 지정자 체크박스(토글) 변화 감지
        if (assigneeCheckToggle)
        {
            assigneeCheckToggle.onValueChanged.RemoveListener(OnAssigneeToggleChanged);
            assigneeCheckToggle.onValueChanged.AddListener(OnAssigneeToggleChanged);
        }
    }

    // 지정자 입력 변화 시 Draft에 임시 저장 및 토글 표시 함수      
    private void OnAssigneeChanged(string v)
    {
        if (isLoadingUI) return;

        // Draft에 임시 저장
        draftAssignee = v ?? "";

        // 토글 표시/숨김 갱신 (이름 입력하면 토글 나타남)
        UpdateAssigneeToggleVisibility();

        // 입력이 바뀌면 토글 OFF로 초기화
        if (assigneeCheckToggle) assigneeCheckToggle.isOn = false;
    }

    // 지정자 토글 변화 시 처리 - ON되면 지정자 확정 함수   
    private void OnAssigneeToggleChanged(bool isOn)
    {
        // ON될 때만 확정 처리
        if (!isOn) return;

        // 지정자 확정 함수 호출
        ConfirmAssigneeNow();
    }

    // 지정자 토글 표시/숨김 상태 갱신 함수 - 이름 입력 여부에 따라
    private void UpdateAssigneeToggleVisibility()
    {
        // 지정자 이름이 입력되었는지 확인
        bool show = !string.IsNullOrWhiteSpace(draftAssignee);

        // 토글 표시/숨김 및 활성화 상태 설정
        if (assigneeCheckToggle)
        {
            assigneeCheckToggle.gameObject.SetActive(show);
            assigneeCheckToggle.interactable = show;

            // 이름이 비면 토글도 초기화
            if (!show) assigneeCheckToggle.isOn = false;
        }

        if (logDebug)
        {
            Debug.Log($"[MemoUIController] AssigneeUI show={show} draftAssignee='{draftAssignee}' " +
                      $"toggleAssigned={(assigneeCheckToggle != null)}");
        }
    }

    // 지정자 확정 처리 함수 - 토글 ON 시 실행
    private void ConfirmAssigneeNow()
    {
        // 현재 메모가 없으면 종료
        if (!currentMemo) return;

        // 지정자 이름 가져오기
        string assignee = draftAssignee ?? "";
        if (string.IsNullOrWhiteSpace(assignee)) return;

        // MemoData에 지정자 저장 (리플렉션 사용)
        TrySetMemoAssignee(currentMemo, assignee);

        // 외부 스크립트에 이벤트 알림
        OnAssigneeConfirmed?.Invoke(assignee);

        if (logDebug) Debug.Log($"[MemoUIController] Assignee confirmed: '{assignee}'");

        // 토글 상태 갱신
        UpdateAssigneeToggleVisibility();
    }

    // MemoData의 내용을 UI에 로드하는 함수
    private void LoadTextMemoToUI()
    {
        // 현재 메모나 입력칸이 없으면 종료
        if (!currentMemo) return;
        if (!inputTitle || !inputBody)
        {
            if (logDebug) Debug.LogWarning("[MemoUIController] inputTitle/inputBody is not assigned.");
            return;
        }

        // 위치 입력칸 활성화
        if (inputLocation)
        {
            inputLocation.interactable = true;
            inputLocation.readOnly = false;
        }

        // 프리팹 세팅 검증 - 두 InputField가 같은 Text 컴포넌트를 공유하는지 확인
        if (inputTitle.textComponent != null && inputBody.textComponent != null)
        {
            if (ReferenceEquals(inputTitle.textComponent, inputBody.textComponent))
            {
                Debug.LogWarning("[MemoUIController] Title/Body TMP_InputField가 같은 Text(TMP) 컴포넌트를 공유 중입니다. " +
                    "(한쪽 입력 시 다른쪽이 지워지는 현상 발생) 각 InputField의 Text Component를 서로 다른 Text(TMP)로 다시 연결하세요.");
            }
        }

        // 입력 필드 활성화 (읽기 전용 해제)
        inputTitle.interactable = true;
        inputBody.interactable = true;
        inputTitle.readOnly = false;
        inputBody.readOnly = false;

        // MemoData 컴포넌트 가져오기
        MemoData memo = currentMemo.GetComponent<MemoData>();
        if (!memo)
        {
            // MemoData가 없으면 모든 값 초기화
            if (logDebug) Debug.LogWarning("[MemoUIController] MemoData is missing on currentMemo.");
            isLoadingUI = true;
            draftTitle = "";
            draftBody = "";
            draftLocation = "";
            inputTitle.text = "";
            inputBody.text = "";
            if (inputLocation) inputLocation.text = "";

            // 메타 정보 및 지정자도 초기화
            UpdateMetaInfoText();
            LoadAssigneeToUI(null);
            
            // Outline 색상 초기화 (비어있는 상태)
            UpdateTitleOutlineColor();
            UpdateBodyOutlineColor();

            isLoadingUI = false;
            return;
        }

        // UI 로딩 시작 (입력 변화 이벤트 무시)
        isLoadingUI = true;

        // MemoData에서 저장된 값 읽어서 Draft에 저장
        draftTitle = memo.title ?? "";
        draftBody = memo.body ?? "";
        draftLocation = memo.location ?? "";

        // UI 입력칸에 표시
        inputTitle.text = draftTitle;
        inputBody.text = draftBody;
        if (inputLocation) inputLocation.text = draftLocation;

        // 메타 정보 갱신 (날짜/시간/사용자ID)
        UpdateMetaInfoText();

        // 지정자 정보 로드
        LoadAssigneeToUI(memo);
        
        // 달력에 저장된 날짜 로드
        LoadDueDateToCalendar(memo);
        
        // 시간 로드
        if (timePickerController != null && !string.IsNullOrEmpty(memo.dueTime))
        {
            string[] timeParts = memo.dueTime.Split(':');
            if (timeParts.Length == 2 && int.TryParse(timeParts[0], out int hour) && int.TryParse(timeParts[1], out int minute))
            {
                timePickerController.SetTime(hour, minute);
                if (logDebug)
                    Debug.Log($"[MemoUIController] 시간 로드: {memo.dueTime}");
            }
        }
        
        // 긴급도 로드
        if (emergencyButtonManager != null)
        {
            if (memo.emergencyLevel > 0)
            {
                int buttonIndex = memo.emergencyLevel - 1;
                emergencyButtonManager.SetSelectedButton(buttonIndex);
                if (logDebug)
                    Debug.Log($"[MemoUIController] 긴급도 로드: {memo.emergencyLevel} (버튼 인덱스: {buttonIndex})");
            }
            else
            {
                emergencyButtonManager.ClearSelection();
            }
        }
        
        // Outline 색상 업데이트
        UpdateTitleOutlineColor();
        UpdateBodyOutlineColor();

        // UI 로딩 완료
        isLoadingUI = false;
    }

    // 저장 버튼 클릭 시 실행 함수
    private void SaveTextMemoNow()
    {
        Debug.Log("[MemoUIController] SaveTextMemoNow() 시작");
        
        // 현재 메모가 있고 입력칸이 있으면 저장 시도
        if (currentMemo && inputTitle && inputBody)
        {
            // 실제 저장 처리
            ApplySaveFromUIAndSync();
            Debug.Log("[MemoUIController] SaveTextMemoNow() - 저장 완료");
        }
        else
        {
            // 저장할 수 없는 경우 경고 로그 출력
            if (!currentMemo)
                Debug.LogWarning("[MemoUIController] SaveTextMemoNow() - currentMemo가 null이어서 저장 건너뜀");
            if (!inputTitle || !inputBody)
                Debug.LogWarning("[MemoUIController] SaveTextMemoNow() - inputTitle 또는 inputBody가 null이어서 저장 건너뜀");
        }

        Debug.Log("[MemoUIController] SaveTextMemoNow() - 이제 UI 닫기 (저장하지 않고 닫기)");

        // 저장 여부와 관계없이 모든 UI 닫기 (CloseAll은 다시 저장을 시도하므로 CloseWithoutSaving 사용)
        CloseWithoutSaving();
    }

    // 텍스트 패널이 열려있을 때만 저장 함수 - 패널 전환/닫기 시 자동 호출
    private void SaveTextMemoIfOpen()
    {
        // 텍스트 패널이 열려있지 않으면 종료
        if (!panelText || !panelText.activeSelf) return;
        if (!currentMemo) return;

        // 실제 저장 처리
        ApplySaveFromUIAndSync();
    }

    // UI의 Draft 값을 MemoData와 JSON에 저장하는 실제 저장 함수
    private void ApplySaveFromUIAndSync()
    {
        // MemoData 컴포넌트 가져오기
        MemoData memo = currentMemo.GetComponent<MemoData>();
        if (!memo)
        {
            if (logDebug) Debug.LogWarning("[MemoUIController] MemoData is missing on currentMemo (cannot save).");
            return;
        }

        // Draft를 우선 사용 (입력 중 UI 갱신으로 인한 덮어쓰기 방지)
        string title = draftTitle ?? (inputTitle ? inputTitle.text : "");
        string body = draftBody ?? (inputBody ? inputBody.text : "");
        string location = draftLocation ?? (inputLocation ? inputLocation.text : "");

        // MemoData에 저장
        memo.title = title ?? "";
        memo.body = body ?? "";
        memo.content = memo.body; // 호환성 유지
        memo.location = location ?? "";
        
        // 달력에서 선택한 날짜 저장
        if (calendarController != null)
        {
            DateTime selectedDate = calendarController.GetSelectedDate();
            memo.DueDateDateTime = selectedDate;
            
            if (logDebug)
                Debug.Log($"[MemoUIController] 선택된 날짜 저장: {selectedDate:yyyy-MM-dd}");
        }
        
        // 시간 선택기에서 선택한 시간 저장
        if (timePickerController != null)
        {
            string selectedTime = timePickerController.GetSelectedTimeString();
            memo.dueTime = selectedTime;
            
            if (logDebug)
                Debug.Log($"[MemoUIController] 선택된 시간 저장: {selectedTime}");
        }
        
        // 긴급도 버튼에서 선택한 긴급도 저장
        if (emergencyButtonManager != null)
        {
            int emergencyIndex = emergencyButtonManager.GetSelectedButtonIndex();
            // -1이면 선택 안함(0), 0이면 첫번째(1), 1이면 두번째(2), 2면 세번째(3)
            memo.emergencyLevel = emergencyIndex + 1;
            
            if (logDebug)
                Debug.Log($"[MemoUIController] 선택된 긴급도 저장: {memo.emergencyLevel} (인덱스: {emergencyIndex})");
        }

        // 지정자가 있으면 저장
        if (!string.IsNullOrWhiteSpace(draftAssignee))
            TrySetMemoAssignee(currentMemo, draftAssignee);

        // JSON 파일에도 저장
        if (pinStore != null)
        {
            // 기본 정보 저장 (제목, 본문, 위치)
            pinStore.SaveTextMemoById(memo.id, memo.title, memo.body, memo.location);
            
            // 날짜 저장
            pinStore.UpdateMemoDueDate(memo.id, memo.dueDate);
            
            // 시간 저장
            pinStore.UpdateMemoDueTime(memo.id, memo.dueTime);
            
            // 긴급도 저장
            pinStore.UpdateMemoEmergencyLevel(memo.id, memo.emergencyLevel);
            
            if (logDebug)
                Debug.Log($"[MemoUIController] 메모 저장 완료: ID={memo.id}, 제목={memo.title}, 날짜={memo.dueDate}, 시간={memo.dueTime}, 긴급도={memo.emergencyLevel}");
        }
        else
        {
            if (logDebug) Debug.LogWarning("[MemoUIController] pinStore is null. Assign TabPinCreate in inspector.");
        }
    }

    // UI가 활성화되어 있으면 입력 차단 함수
    public bool IsUIBlockingWorldInput()
    {
        // 하단바가 켜져 있으면 차단
        if (bottomBar && bottomBar.activeInHierarchy) return true;

        // 패널 중 하나라도 켜져 있으면 차단
        if (panelText && panelText.activeInHierarchy) return true;
        if (panelVoice && panelVoice.activeInHierarchy) return true;
        if (panelChecklist && panelChecklist.activeInHierarchy) return true;
        if (panelImage && panelImage.activeInHierarchy) return true;

        return false;
    }

    // 자동 포커스 설정 함수
    private IEnumerator FocusTextInputNextFrame()
    {
        // UI 레이아웃이 완전히 준비될 시간 확보
        yield return null;

        // 패널이 닫혔거나 입력칸이 없으면 종료
        if (!panelText || !panelText.activeSelf) yield break;
        if (!inputBody && !inputTitle) yield break;

        // 포커스 대상 결정 - 제목이 비어있으면 제목, 아니면 본문
        TMP_InputField target = inputTitle;
        if (target && !string.IsNullOrWhiteSpace(target.text))
            target = inputBody ? inputBody : inputTitle;

        if (!target) yield break;

        // 입력 필드 활성화
        target.interactable = true;
        target.readOnly = false;

        // 포커스 설정 및 키보드 활성화
        target.Select();
        target.ActivateInputField();
    }

    // 메타 정보 텍스트 갱신 함수
    private void UpdateMetaInfoText()
    {
        // 날짜 텍스트 갱신
        if (dateText)
        {
            // CalendarController가 있으면 선택된 날짜 사용, 없으면 현재 날짜 사용
            string dateStr;
            if (calendarController != null)
            {
                dateStr = calendarController.GetSelectedDateString("MM/dd");
            }
            else
            {
                dateStr = DateTime.Now.ToString("MM/dd");
            }
            
            // 텍스트 표시 설정 (한 줄 고정)
            dateText.enableWordWrapping = false;                 // 줄바꿈 금지
            dateText.overflowMode = TextOverflowModes.Ellipsis;  // 넘치면 ... 처리
            dateText.maxVisibleLines = 1;                        // 한 줄만 표시
            
            // 날짜 텍스트 설정
            dateText.text = dateStr;
        }
        
        // 사용자 ID 텍스트 갱신
        if (userIdText)
        {
            // 사용자 ID 가져오기 (PlayerPrefs 또는 디바이스 ID)
            string userId = PlayerPrefs.GetString(userIdPrefKey, "");
            if (string.IsNullOrWhiteSpace(userId) && useDeviceIdFallback)
            {
                // PlayerPrefs에 없으면 디바이스 ID 사용
                string dev = SystemInfo.deviceUniqueIdentifier ?? "";
                userId = (dev.Length > 8) ? dev.Substring(0, 8) : dev;
            }
            
            // 텍스트 표시 설정 (한 줄 고정)
            userIdText.enableWordWrapping = false;                 // 줄바꿈 금지
            userIdText.overflowMode = TextOverflowModes.Ellipsis;  // 넘치면 ... 처리
            userIdText.maxVisibleLines = 1;                        // 한 줄만 표시
            
            // 사용자 ID 텍스트 설정
            userIdText.text = userId;
        }
    }

    // 지정자 UI에 저장된 값 로드 함수
    private void LoadAssigneeToUI(MemoData memo)
    {
        if (!inputAssignee) return;

        // 저장된 지정자 읽기 (옵션에 따라)
        string existing = "";
        if (!clearAssigneeOnOpen && memo != null)
            existing = TryGetMemoAssignee(memo);

        // UI 로딩 시작
        isLoadingUI = true;

        // Draft와 UI에 값 설정
        draftAssignee = existing ?? "";
        inputAssignee.interactable = true;
        inputAssignee.readOnly = false;
        inputAssignee.text = draftAssignee;

        // 토글 초기화
        if (assigneeCheckToggle) assigneeCheckToggle.isOn = false;

        // 토글 표시/숨김 갱신
        UpdateAssigneeToggleVisibility();
        
        // AssigneeButton Outline 색상 업데이트
        UpdateAssigneeButtonOutlineColor();

        // UI 로딩 완료
        isLoadingUI = false;
    }
    
    // 메모에 저장된 마감일을 CalendarController에 로드하는 함수
    private void LoadDueDateToCalendar(MemoData memo)
    {
        if (calendarController == null) return;
        if (memo == null) return;
        
        // 저장된 마감일이 있으면 CalendarController에 설정
        DateTime? dueDate = memo.DueDateDateTime;
        if (dueDate.HasValue)
        {
            calendarController.SetSelectedDate(dueDate.Value);
            
            if (logDebug)
                Debug.Log($"[MemoUIController] 저장된 날짜 로드: {dueDate.Value:yyyy-MM-dd}");
        }
        
        // 날짜 텍스트 갱신
        UpdateMetaInfoText();
    }

    // MemoData에 assignee 필드가 있으면 저장 함수
    private static void TrySetMemoAssignee(GameObject memoGO, string assignee)
    {
        if (!memoGO) return;
        var memo = memoGO.GetComponent<MemoData>();
        if (!memo) return;

        // 리플렉션으로 필드/프로퍼티 찾기
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var t = memo.GetType();

        // 필드 우선 검색
        var f = t.GetField("assignee", flags);
        if (f != null && f.FieldType == typeof(string))
        {
            f.SetValue(memo, assignee ?? "");
            return;
        }

        // 프로퍼티 검색
        var p = t.GetProperty("Assignee", flags) ?? t.GetProperty("assignee", flags);
        if (p != null && p.CanWrite && p.PropertyType == typeof(string))
        {
            p.SetValue(memo, assignee ?? "");
        }
    }

    // MemoData에서 assignee 필드 읽기 함수
    private static string TryGetMemoAssignee(MemoData memo)
    {
        if (!memo) return "";

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var t = memo.GetType();

        var f = t.GetField("assignee", flags);
        if (f != null && f.FieldType == typeof(string))
            return (string)f.GetValue(memo) ?? "";

        var p = t.GetProperty("Assignee", flags) ?? t.GetProperty("assignee", flags);
        if (p != null && p.CanRead && p.PropertyType == typeof(string))
            return (string)p.GetValue(memo) ?? "";

        return "";
    }
}
