// 이미지 메모를 입력하고 편집하는 UI를 관리하는 컨트롤러
// Panel_ImageMemo 전용 컨트롤러 (Panel_TextMemo의 기능을 복사하고 이미지 기능 추가)
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class ImageMemoUIController : MonoBehaviour
{
    [Header("Assignee Dropdown")]
    [Tooltip("지정자 드롭다운 매니저")]
    [SerializeField] private AssigneeDropdownManager assigneeDropdownManager;

    [Header("Image Memo Inputs (Panel Image)")]
    [Tooltip("ImageMemo 패널 안의 TMP_InputField(타이틀) 넣는 자리")]
    [SerializeField] private TMP_InputField inputTitle;
    [Tooltip("ImageMemo 패널 안의 TMP_InputField(내용) 넣는 자리")]
    [SerializeField] private TMP_InputField inputBody;
    [Tooltip("ImageMemo 패널 안의 TMP_InputField(위치) 넣는 자리")]
    [SerializeField] private TMP_InputField inputLocation;
    
    [Header("InputField Outlines")]
    [Tooltip("InputField_Title의 Outline 컴포넌트")]
    [SerializeField] private Outline titleOutline;
    [Tooltip("InputField_Body의 Outline 컴포넌트")]
    [SerializeField] private Outline bodyOutline;
    
    [Header("InputField Body Height")]
    [Tooltip("ImageInputField_Body의 RectTransform (높이 조절용)")]
    [SerializeField] private RectTransform bodyRectTransform;
    [Tooltip("입력 시작 시 Body의 높이")]
    [SerializeField] private float expandedBodyHeight = 600f;
    [Tooltip("기본 Body 높이")]
    [SerializeField] private float defaultBodyHeight = 476f;
    
    [Header("Elements to Move with Body")]
    [Tooltip("ImageMovie의 RectTransform")]
    [SerializeField] private RectTransform imageMovieRectTransform;
    [Tooltip("Deadline의 RectTransform")]
    [SerializeField] private RectTransform deadlineRectTransform;
    [Tooltip("ImageEmergency의 RectTransform")]
    [SerializeField] private RectTransform imageEmergencyRectTransform;
    [Tooltip("요소들이 아래로 밀려나는 정도 (양수: 아래로, 음수: 위로)")]
    [SerializeField] private float elementsMoveDistance = 124f;
    
    // Body 높이 확장 여부
    private bool isBodyExpanded;
    
    // 원래 위치 저장
    private Vector2 originalImageMoviePosition;
    private Vector2 originalDeadlinePosition;
    private Vector2 originalImageEmergencyPosition;
    
    [Header("Outline Colors")]
    [SerializeField] private Color emptyOutlineColor = new Color(0x96 / 255f, 0xCB / 255f, 0xE0 / 255f); // #96CBE0 (비어있을 때)
    [SerializeField] private Color filledOutlineColor = new Color(0xD9 / 255f, 0xD9 / 255f, 0xD9 / 255f); // #D9D9D9 (채워졌을 때)

    [Header("Image Button Container")]
    [Tooltip("이미지 버튼들이 배치될 컨테이너 (HorizontalLayoutGroup 필요)")]
    [SerializeField] private RectTransform imageButtonContainer;
    
    [Header("Add Image Button (기본 버튼)")]
    [Tooltip("이미지 추가 버튼 (기본 1개, 아이콘+Shadow 있음)")]
    [SerializeField] private Button addImageButton;
    [Tooltip("추가 버튼의 Outline 컴포넌트 (레거시, Shadow로 대체됨)")]
    [SerializeField] private Outline addImageButtonOutline;
    [Tooltip("추가 버튼의 Shadow 컴포넌트 (RoundedCornersImage 호환)")]
    private Shadow addImageButtonShadow;
    [Tooltip("추가 버튼의 아이콘 Image")]
    [SerializeField] private Image addImageButtonIcon;
    [Tooltip("빈 슬롯 아이콘 스프라이트")]
    [SerializeField] private Sprite emptySlotSprite;

    [Header("Image Button Settings")]
    [Tooltip("이미지 버튼 크기")]
    [SerializeField] private Vector2 imageButtonSize = new Vector2(200, 200);
    [Tooltip("이미지 버튼 모서리 반경 (라운드)")]
    [SerializeField] private float imageButtonRadius = 20f;
    
    [Header("Gallery/Camera UI")]
    [Tooltip("갤러리/카메라 선택 UI (GalleryCamera GameObject)")]
    [SerializeField] private GameObject galleryCameraUI;
    [Tooltip("갤러리 버튼")]
    [SerializeField] private Button galleryButton;
    [Tooltip("카메라 버튼")]
    [SerializeField] private Button cameraButton;
    
    [Header("Delete Button Settings")]
    [Tooltip("삭제 버튼 프리팹 (Inspector에서 할당)")]
    [SerializeField] private GameObject deleteButtonPrefab;
    [Tooltip("삭제 버튼 크기 (프리팹 미할당 시 자동 생성용)")]
    [SerializeField] private Vector2 deleteButtonSize = new Vector2(50, 50);
    [Tooltip("삭제 버튼 아이콘 (프리팹 미할당 시 자동 생성용)")]
    [SerializeField] private Sprite deleteButtonSprite;
    
    [Header("Delete Button Hide Settings")]
    [Tooltip("ImageBtn_TextClose의 RectTransform (닫기 버튼)")]
    [SerializeField] private RectTransform imageBtnTextCloseRect;
    [Tooltip("이 반경 안에 들어오면 삭제 버튼 숨김 (픽셀 단위)")]
    [SerializeField] private float deleteButtonHideRadius = 150f;

    [Header("Save & Close Buttons")]
    [Tooltip("ImageMemo 패널 안의 저장 버튼 넣는 자리")]
    [SerializeField] private Button btnSaveImage;
    [Tooltip("ImageMemo 패널의 닫기 버튼 - 저장하지 않고 닫기")]
    [SerializeField] private Button btnImageClose;

    [Header("Calendar & Time")]
    [Tooltip("달력 UI를 관리하는 CalendarController를 넣는 자리")]
    [SerializeField] private CalendarController calendarController;
    [Tooltip("시간 선택 UI를 관리하는 TimePickerController를 넣는 자리")]
    [SerializeField] private TimePickerController timePickerController;
    
    [Header("Emergency")]
    [Tooltip("긴급도 버튼을 관리하는 EmergencyButtonManager를 넣는 자리")]
    [SerializeField] private EmergencyButtonManager emergencyButtonManager;

    [Header("Meta UI")]
    [Tooltip("메모 패널 안에 날짜를 표시할 TMP_Text 넣는 자리")]
    [SerializeField] private TMP_Text dateText;
    [Tooltip("메모 패널 안에 사용자ID를 표시할 TMP_Text 넣는 자리")]
    [SerializeField] private TMP_Text userIdText;

    [Header("TabPinCreate")]
    [Tooltip("TabPinCreate를 넣는 자리 (JSON 저장 갱신용)")]
    [SerializeField] private TabPinCreate pinStore;

    [Header("MemoUIController")]
    [Tooltip("MemoUIController 참조 (패널 닫기 등 공통 기능용)")]
    [SerializeField] private MemoUIController memoUIController;

    [Header("User ID")]
    [Tooltip("사용자 ID를 PlayerPrefs에서 읽을 키")]
    [SerializeField] private string userIdPrefKey = "MEMO_USER_ID";
    [Tooltip("PlayerPrefs에 userId가 없을 때 기기 고유 번호를 대신 사용할지")]
    [SerializeField] private bool useDeviceIdFallback = true;

    [Header("Background Image")]
    [Tooltip("Canvas 하위의 BgImage (패널 열릴 때 활성화, 닫힐 때 비활성화)")]
    [SerializeField] private GameObject bgImage;

    [Header("Debug")]
    [Tooltip("디버그 로그 출력 여부")]
    [SerializeField] private bool logDebug = false;

    // 현재 편집 중인 메모 GameObject
    private GameObject currentMemo;

    // Draft 시스템 - 입력 중 데이터가 지워지는 것을 방지하기 위한 임시 저장소
    private bool isLoadingUI = false;
    private string draftTitle = "";
    private string draftBody = "";
    private string draftLocation = "";

    // 이미지 경로 목록 (최대 3개)
    private List<string> imagePaths = new List<string>();
    private const int MAX_IMAGES = 3;

    // 동적으로 생성된 이미지 버튼들
    private List<GameObject> createdImageButtons = new List<GameObject>();
    
    // 동적으로 생성된 삭제 버튼들 (위치 체크용)
    private List<GameObject> createdDeleteButtons = new List<GameObject>();

    // 이미지 저장 폴더
    private string imageSaveFolder => Path.Combine(Application.persistentDataPath, "MemoImages");

    private void Awake()
    {
        // 이미지 저장 폴더 생성
        if (!Directory.Exists(imageSaveFolder))
        {
            Directory.CreateDirectory(imageSaveFolder);
        }

        // Outline 컴포넌트 자동 검색
        if (titleOutline == null && inputTitle != null)
        {
            titleOutline = inputTitle.GetComponent<Outline>();
        }
        if (bodyOutline == null && inputBody != null)
        {
            bodyOutline = inputBody.GetComponent<Outline>();
        }
        
        // bodyRectTransform 자동 검색 (inputBody의 부모 또는 자신)
        if (bodyRectTransform == null && inputBody != null)
        {
            // inputBody가 속한 GameObject의 이름이 ImageInputField_Body인지 확인
            Transform current = inputBody.transform;
            while (current != null)
            {
                if (current.gameObject.name == "ImageInputField_Body")
                {
                    bodyRectTransform = current.GetComponent<RectTransform>();
                    if (bodyRectTransform != null)
                    {
                        // 기본 높이 저장
                        defaultBodyHeight = bodyRectTransform.sizeDelta.y;
                        Debug.Log($"[ImageMemoUIController] bodyRectTransform 자동 검색됨: {current.gameObject.name}, 기본 높이={defaultBodyHeight}");
                        break;
                    }
                }
                current = current.parent;
            }
        }
        
        // ImageMovie, Deadline, ImageEmergency 자동 검색 및 원래 위치 저장
        if (bodyRectTransform != null)
        {
            Transform parent = bodyRectTransform.parent;
            if (parent != null)
            {
                // ImageMovie 검색
                if (imageMovieRectTransform == null)
                {
                    Transform imageMovie = parent.Find("ImageMovie");
                    if (imageMovie != null)
                    {
                        imageMovieRectTransform = imageMovie.GetComponent<RectTransform>();
                        if (imageMovieRectTransform != null)
                        {
                            originalImageMoviePosition = imageMovieRectTransform.anchoredPosition;
                            Debug.Log($"[ImageMemoUIController] ImageMovie 자동 검색됨: 원래 위치={originalImageMoviePosition}");
                        }
                    }
                }
                else
                {
                    originalImageMoviePosition = imageMovieRectTransform.anchoredPosition;
                }
                
                // Deadline 검색
                if (deadlineRectTransform == null)
                {
                    Transform deadline = parent.Find("Deadline");
                    if (deadline != null)
                    {
                        deadlineRectTransform = deadline.GetComponent<RectTransform>();
                        if (deadlineRectTransform != null)
                        {
                            originalDeadlinePosition = deadlineRectTransform.anchoredPosition;
                            Debug.Log($"[ImageMemoUIController] Deadline 자동 검색됨: 원래 위치={originalDeadlinePosition}");
                        }
                    }
                }
                else
                {
                    originalDeadlinePosition = deadlineRectTransform.anchoredPosition;
                }
                
                // ImageEmergency 검색
                if (imageEmergencyRectTransform == null)
                {
                    Transform imageEmergency = parent.Find("ImageEmergency");
                    if (imageEmergency != null)
                    {
                        imageEmergencyRectTransform = imageEmergency.GetComponent<RectTransform>();
                        if (imageEmergencyRectTransform != null)
                        {
                            originalImageEmergencyPosition = imageEmergencyRectTransform.anchoredPosition;
                            Debug.Log($"[ImageMemoUIController] ImageEmergency 자동 검색됨: 원래 위치={originalImageEmergencyPosition}");
                        }
                    }
                }
                else
                {
                    originalImageEmergencyPosition = imageEmergencyRectTransform.anchoredPosition;
                }
            }
        }
        
        // addImageButton의 Shadow 자동 검색 또는 추가
        // RoundedCornersImage와 호환성을 위해 Outline 대신 Shadow 사용
        if (addImageButton != null)
        {
            // 기존 Outline이 있으면 Shadow로 교체
            Outline existingOutline = addImageButton.GetComponent<Outline>();
            if (existingOutline != null)
            {
                Color outlineColor = existingOutline.effectColor;
                Vector2 outlineDistance = existingOutline.effectDistance;
                DestroyImmediate(existingOutline);
                
                addImageButtonShadow = addImageButton.gameObject.AddComponent<Shadow>();
                addImageButtonShadow.effectColor = outlineColor;
                addImageButtonShadow.effectDistance = outlineDistance;
                
                Debug.Log("[ImageMemoUIController] addImageButton의 Outline을 Shadow로 교체 (RoundedCornersImage 호환)");
            }
            else
            {
                // Shadow 검색
                addImageButtonShadow = addImageButton.GetComponent<Shadow>();
                if (addImageButtonShadow == null)
                {
                    // Shadow가 없으면 추가
                    addImageButtonShadow = addImageButton.gameObject.AddComponent<Shadow>();
                    addImageButtonShadow.effectColor = emptyOutlineColor;
                    addImageButtonShadow.effectDistance = new Vector2(2, -2);
                    
                    Debug.Log("[ImageMemoUIController] addImageButton에 Shadow 컴포넌트 추가됨");
                }
            }
            
            // addImageButtonIcon 자동 검색
            if (addImageButtonIcon == null)
            {
                addImageButtonIcon = addImageButton.GetComponentInChildren<Image>(true);
                if (addImageButtonIcon != null && addImageButtonIcon.gameObject == addImageButton.gameObject)
                {
                    // Button의 Image는 제외하고 자식의 Image 찾기
                    Image[] images = addImageButton.GetComponentsInChildren<Image>(true);
                    if (images.Length > 1)
                    {
                        addImageButtonIcon = images[1];  // 두 번째 Image (첫 번째는 Button 배경)
                    }
                }
                if (addImageButtonIcon != null)
                {
                    // 아이콘이 잘리지 않도록 RectTransform 설정
                    RectTransform iconRect = addImageButtonIcon.GetComponent<RectTransform>();
                    if (iconRect != null)
                    {
                        // 충분한 padding 추가 (20px) - RoundedCornersImage 때문에 잘리지 않도록
                        iconRect.anchorMin = Vector2.zero;
                        iconRect.anchorMax = Vector2.one;
                        iconRect.offsetMin = new Vector2(20, 20);  // left, bottom padding
                        iconRect.offsetMax = new Vector2(-20, -20);  // right, top padding (음수)
                    }
                    Debug.Log("[ImageMemoUIController] addImageButtonIcon 자동 검색됨");
                }
            }
        }

        // 저장 버튼 연결
        if (btnSaveImage)
        {
            btnSaveImage.onClick.RemoveListener(SaveImageMemoNow);
            btnSaveImage.onClick.AddListener(SaveImageMemoNow);
            Debug.Log("[ImageMemoUIController] ★★★ btnSaveImage 리스너 연결 완료");
        }
        else
        {
            Debug.LogError("[ImageMemoUIController] ★★★ btnSaveImage가 null입니다! Inspector에서 할당해주세요.");
        }

        // 닫기 버튼 연결
        if (btnImageClose)
        {
            btnImageClose.onClick.RemoveListener(CloseWithoutSaving);
            btnImageClose.onClick.AddListener(CloseWithoutSaving);
            Debug.Log("[ImageMemoUIController] btnImageClose 리스너 연결 완료");
        }

        // 이미지 추가 버튼 연결
        if (addImageButton)
        {
            addImageButton.onClick.RemoveListener(OnAddImageClicked);
            addImageButton.onClick.AddListener(OnAddImageClicked);
            Debug.Log("[ImageMemoUIController] addImageButton 리스너 연결 완료");
        }
        
        // 갤러리 버튼 연결
        if (galleryButton)
        {
            galleryButton.onClick.RemoveListener(OnGalleryButtonClicked);
            galleryButton.onClick.AddListener(OnGalleryButtonClicked);
            Debug.Log("[ImageMemoUIController] galleryButton 리스너 연결 완료");
        }
        
        // 카메라 버튼 연결
        if (cameraButton)
        {
            cameraButton.onClick.RemoveListener(OnCameraButtonClicked);
            cameraButton.onClick.AddListener(OnCameraButtonClicked);
            Debug.Log("[ImageMemoUIController] cameraButton 리스너 연결 완료");
        }
        
        // GalleryCamera UI 초기 상태 (비활성화)
        if (galleryCameraUI != null)
        {
            galleryCameraUI.SetActive(false);
        }

        // 입력 변화 감지 리스너 연결
        WireDraftListeners();
        
        // 입력 시작 감지 리스너 연결
        WireInputFieldSelectListeners();
        
        // 초기 상태 설정 (추가 버튼만 표시)
        InitializeImageButtonState();
        
        // Inspector 할당 확인
        Debug.Log("[ImageMemoUIController] ★★★ === Inspector 할당 상태 확인 ===");
        Debug.Log($"[ImageMemoUIController] ★★★ btnSaveImage: {(btnSaveImage != null ? "할당됨" : "NULL - 할당 필요!")}");
        Debug.Log($"[ImageMemoUIController] ★★★ btnImageClose: {(btnImageClose != null ? "할당됨" : "NULL")}");
        Debug.Log($"[ImageMemoUIController] ★★★ inputTitle: {(inputTitle != null ? "할당됨" : "NULL - 할당 필요!")}");
        Debug.Log($"[ImageMemoUIController] ★★★ inputBody: {(inputBody != null ? "할당됨" : "NULL - 할당 필요!")}");
        Debug.Log($"[ImageMemoUIController] ★★★ pinStore (TabPinCreate): {(pinStore != null ? "할당됨" : "NULL - 할당 필요!")}");
        Debug.Log($"[ImageMemoUIController] ★★★ memoUIController: {(memoUIController != null ? "할당됨" : "NULL")}");
        Debug.Log($"[ImageMemoUIController] ★★★ calendarController: {(calendarController != null ? "할당됨" : "NULL")}");
        Debug.Log($"[ImageMemoUIController] ★★★ timePickerController: {(timePickerController != null ? "할당됨" : "NULL")}");
        Debug.Log($"[ImageMemoUIController] ★★★ emergencyButtonManager: {(emergencyButtonManager != null ? "할당됨" : "NULL")}");
        Debug.Log("[ImageMemoUIController] ★★★ ============================");
    }
    
    /// <summary>
    /// 매 프레임 삭제 버튼 위치를 체크하여 ImageBtn_TextClose 근처면 숨김
    /// </summary>
    private void LateUpdate()
    {
        // 삭제 버튼이 있을 때만 체크
        if (createdDeleteButtons.Count > 0 && imageBtnTextCloseRect != null)
        {
            CheckDeleteButtonsVisibilityRealtime();
        }
    }
    
    /// <summary>
    /// 실시간 삭제 버튼 가시성 체크 (LateUpdate에서 호출)
    /// </summary>
    private void CheckDeleteButtonsVisibilityRealtime()
    {
        if (imageBtnTextCloseRect == null || createdDeleteButtons.Count == 0) return;
        
        // 가장 우측에 있는 삭제 버튼 찾기 (스크린 X 좌표 기준)
        GameObject rightmostDeleteBtn = null;
        float maxX = float.MinValue;
        
        foreach (var deleteBtn in createdDeleteButtons)
        {
            if (deleteBtn == null) continue;
            
            RectTransform deleteRect = deleteBtn.GetComponent<RectTransform>();
            if (deleteRect == null) continue;
            
            Vector2 screenPos = GetScreenPosition(deleteRect);
            
            if (screenPos.x > maxX)
            {
                maxX = screenPos.x;
                rightmostDeleteBtn = deleteBtn;
            }
        }
        
        if (rightmostDeleteBtn == null) return;
        
        // 가장 우측 삭제 버튼과 ImageBtn_TextClose 사이의 거리 계산
        RectTransform rightmostRect = rightmostDeleteBtn.GetComponent<RectTransform>();
        float distance = GetDistanceBetweenRectTransforms(imageBtnTextCloseRect, rightmostRect);
        
        // 반경 안에 있으면 숨김, 밖이면 보임
        bool shouldHide = distance < deleteButtonHideRadius;
        
        // 상태가 변경될 때만 SetActive 호출 (성능 최적화)
        if (rightmostDeleteBtn.activeSelf == shouldHide)
        {
            rightmostDeleteBtn.SetActive(!shouldHide);
        }
    }
    
    /// <summary>
    /// 이미지 버튼 초기 상태 설정
    /// </summary>
    private void InitializeImageButtonState()
    {
        // 추가 버튼 Shadow 색상 설정 (#96CBE0)
        if (addImageButtonShadow != null)
        {
            addImageButtonShadow.effectColor = emptyOutlineColor;
            addImageButtonShadow.enabled = true;
        }
        
        // 추가 버튼 아이콘 설정
        if (addImageButtonIcon != null)
        {
            if (emptySlotSprite != null)
            {
                addImageButtonIcon.sprite = emptySlotSprite;
            }
            
            // 아이콘이 잘리지 않도록 preserveAspect 설정
            addImageButtonIcon.preserveAspect = true;
            addImageButtonIcon.type = Image.Type.Simple;
        }
        
        // 추가 버튼에 라운드 적용 확인
        if (addImageButton != null)
        {
            addImageButton.gameObject.SetActive(true);
            
            // addImageButton을 항상 첫 번째(맨 좌측)로 배치
            addImageButton.transform.SetAsFirstSibling();
            
            // addImageButton의 배경 Image에 RoundedCornersImage 적용 (아이콘이 아닌 버튼 자체에)
            Image buttonBg = addImageButton.GetComponent<Image>();
            if (buttonBg != null)
            {
                RoundedCornersImage rounded = buttonBg.GetComponent<RoundedCornersImage>();
                if (rounded == null)
                {
                    rounded = buttonBg.gameObject.AddComponent<RoundedCornersImage>();
                    rounded.SetRadius(imageButtonRadius);
                    if (logDebug)
                        Debug.Log("[ImageMemoUIController] addImageButton 배경에 RoundedCornersImage 추가됨");
                }
            }
        }
    }

    /// <summary>
    /// 패널이 열릴 때 호출 - 현재 메모 설정 및 UI 초기화
    /// </summary>
    public void OnPanelOpened(GameObject memo)
    {
        currentMemo = memo;

        Debug.Log($"[ImageMemoUIController] [###] OnPanelOpened 호출됨: memo={(currentMemo ? currentMemo.name : "null")}");
        
        // BgImage 활성화
        if (bgImage != null)
        {
            bgImage.SetActive(true);
            Debug.Log("[ImageMemoUIController] BgImage 활성화됨");
        }
        
        if (currentMemo != null)
        {
            MemoData memoData = currentMemo.GetComponent<MemoData>();
            if (memoData != null)
            {
                Debug.Log($"[ImageMemoUIController] [###] MemoData 확인: id={memoData.id}, memoType={memoData.memoType}");
            }
            else
            {
                Debug.LogWarning("[ImageMemoUIController] [###] MemoData가 없습니다!");
            }
        }

        // 기존 이미지 버튼들 정리
        ClearCreatedImageButtons();
        imagePaths.Clear();
        
        // ImageInputField_Body 높이 초기화
        CollapseBodyHeight();

        // 메모 데이터 로드
        LoadImageMemoToUI();

        // 메타 정보 갱신
        UpdateMetaInfoText();

        // Assignee 드롭다운 업데이트
        UpdateAssigneeDropdown();
    }

    /// <summary>
    /// AssigneeDropdown 상태를 현재 메모의 assignee에 맞춰 업데이트
    /// </summary>
    private void UpdateAssigneeDropdown()
    {
        if (currentMemo == null) return;

        MemoData memoData = currentMemo.GetComponent<MemoData>();
        if (memoData == null) return;

        // AssigneeDropdownManager에 현재 메모 ID 설정
        AssigneeDropdownManager.SetCurrentMemoId(memoData.id);

        // 드롭다운 매니저를 찾아서 저장된 assignee 값 불러오기
        AssigneeDropdownManager dropdownManager = assigneeDropdownManager ?? FindObjectOfType<AssigneeDropdownManager>();
        if (dropdownManager != null && pinStore != null)
        {
            string assigneeName = pinStore.GetMemoAssignee(memoData.id);
            dropdownManager.LoadAssignee(assigneeName);
            
            if (logDebug)
                Debug.Log($"[ImageMemoUIController] Assignee 로드: {assigneeName}");
        }
    }

    /// <summary>
    /// 메모 데이터를 UI에 로드
    /// </summary>
    private void LoadImageMemoToUI()
    {
        if (currentMemo == null) return;

        if (inputTitle == null || inputBody == null)
        {
            if (logDebug) Debug.LogWarning("[ImageMemoUIController] inputTitle/inputBody가 할당되지 않았습니다.");
            return;
        }

        MemoData memo = currentMemo.GetComponent<MemoData>();
        if (memo == null)
        {
            // MemoData가 없으면 초기화
            isLoadingUI = true;
            draftTitle = "";
            draftBody = "";
            draftLocation = "";
            imagePaths.Clear();
            
            inputTitle.text = "";
            inputBody.text = "";
            if (inputLocation) inputLocation.text = "";
            
            UpdateTitleOutlineColor();
            UpdateBodyOutlineColor();
            UpdateImageSlots();
            
            isLoadingUI = false;
            return;
        }

        // UI 로딩 시작
        isLoadingUI = true;

        // 텍스트 데이터 로드
        draftTitle = memo.title ?? "";
        draftBody = memo.body ?? "";
        draftLocation = memo.location ?? "";

        inputTitle.text = draftTitle;
        inputBody.text = draftBody;
        if (inputLocation) inputLocation.text = draftLocation;

        // 이미지 경로 로드
        imagePaths.Clear();
        Debug.Log($"[ImageMemoUIController] [###] 이미지 경로 로드 시작: id={memo.id}, memoType={memo.memoType}");
        
        if (memo.imagePaths != null)
        {
            Debug.Log($"[ImageMemoUIController] [###] MemoData.imagePaths에서 로드: {memo.imagePaths.Count}개");
            imagePaths.AddRange(memo.imagePaths);
        }
        else
        {
            Debug.Log("[ImageMemoUIController] [###] MemoData.imagePaths가 null");
        }
        
        // TabPinCreate에서도 이미지 경로 로드 시도
        if (pinStore != null)
        {
            List<string> storedPaths = pinStore.GetMemoImagePaths(memo.id);
            Debug.Log($"[ImageMemoUIController] [###] TabPinCreate.GetMemoImagePaths 결과: {(storedPaths != null ? storedPaths.Count : 0)}개");
            
            if (storedPaths != null && storedPaths.Count > 0)
            {
                imagePaths.Clear();
                imagePaths.AddRange(storedPaths);
                Debug.Log($"[ImageMemoUIController] [###] TabPinCreate에서 이미지 경로 로드됨: {storedPaths.Count}개");
                for (int i = 0; i < storedPaths.Count; i++)
                {
                    Debug.Log($"[ImageMemoUIController] [###] 이미지 경로[{i}]: {storedPaths[i]}");
                }
            }
        }
        
        Debug.Log($"[ImageMemoUIController] [###] 최종 imagePaths.Count: {imagePaths.Count}");

        // 날짜/시간/긴급도 로드
        LoadMemoMetadata(memo);

        // UI 업데이트
        UpdateTitleOutlineColor();
        UpdateBodyOutlineColor();
        UpdateImageSlots();

        isLoadingUI = false;

        if (logDebug)
            Debug.Log($"[ImageMemoUIController] 메모 로드 완료: title={draftTitle}, images={imagePaths.Count}");
    }

    /// <summary>
    /// 저장된 메모의 날짜, 시간, 긴급도를 UI에 로드
    /// </summary>
    private void LoadMemoMetadata(MemoData memo)
    {
        if (memo == null) return;

        // 날짜 로드
        if (calendarController != null && memo.DueDateDateTime.HasValue)
        {
            calendarController.SetSelectedDate(memo.DueDateDateTime.Value);
        }

        // 시간 로드
        if (timePickerController != null && !string.IsNullOrEmpty(memo.dueTime))
        {
            string[] timeParts = memo.dueTime.Split(':');
            if (timeParts.Length == 2 && int.TryParse(timeParts[0], out int hour) && int.TryParse(timeParts[1], out int minute))
            {
                timePickerController.SetTime(hour, minute);
            }
        }

        // 긴급도 로드
        if (emergencyButtonManager != null)
        {
            if (memo.emergencyLevel > 0)
            {
                emergencyButtonManager.SetSelectedButton(memo.emergencyLevel - 1);
            }
            else
            {
                emergencyButtonManager.ClearSelection();
            }
        }
    }

    /// <summary>
    /// 이미지 버튼 UI 업데이트 (동적 생성/삭제)
    /// </summary>
    private void UpdateImageSlots()
    {
        Debug.Log($"[ImageMemoUIController] [###] UpdateImageSlots() 호출됨: imagePaths.Count={imagePaths.Count}");
        
        // 기존에 생성된 이미지 버튼들 모두 삭제
        ClearCreatedImageButtons();
        
        // addImageButton을 항상 맨 앞(좌측)에 배치
        if (addImageButton != null)
        {
            addImageButton.transform.SetAsFirstSibling();
        }
        
        // 이미지가 있으면 이미지 버튼들 생성
        if (imagePaths.Count > 0)
        {
            Debug.Log($"[ImageMemoUIController] [###] 이미지가 {imagePaths.Count}개 있음 - 이미지 버튼 생성 시작");
            
            // 추가 버튼 설정 (이미지가 있을 때)
            if (addImageButton != null)
            {
                // 최대 개수 미만이면 추가 버튼도 표시 (Outline 없이)
                if (imagePaths.Count < MAX_IMAGES)
                {
                    addImageButton.gameObject.SetActive(true);
                    // 이미지가 있을 때는 추가 버튼의 Shadow 비활성화
                    if (addImageButtonShadow != null)
                    {
                        addImageButtonShadow.enabled = false;
                    }
                    Debug.Log($"[ImageMemoUIController] ★★★ 추가 버튼 표시 (Shadow 비활성화)");
                }
                else
                {
                    addImageButton.gameObject.SetActive(false);
                    Debug.Log($"[ImageMemoUIController] ★★★ 최대 개수 도달 - 추가 버튼 숨김");
                }
            }
            
            // 각 이미지에 대해 버튼 생성 (역순으로 생성하여 최신 이미지가 좌측에 오도록)
            // 가장 오래된 이미지(index=0)를 먼저 생성하면 우측에 배치됨
            // 가장 최신 이미지(index=imagePaths.Count-1)를 마지막에 생성하면 좌측에 배치됨
            for (int i = 0; i < imagePaths.Count; i++)
            {
                Debug.Log($"[ImageMemoUIController] ★★★ 이미지 버튼 생성 중: index={i}, path={imagePaths[i]}");
                CreateImageButton(i, imagePaths[i]);
            }
        }
        else
        {
            Debug.Log("[ImageMemoUIController] [###] 이미지 없음 - 추가 버튼만 표시");
            
            // 이미지가 없으면 추가 버튼만 표시 (Shadow 있음)
            if (addImageButton != null)
            {
                addImageButton.gameObject.SetActive(true);
                if (addImageButtonShadow != null)
                {
                    addImageButtonShadow.effectColor = emptyOutlineColor;
                    addImageButtonShadow.enabled = true;
                }
            }
        }
        
        Debug.Log($"[ImageMemoUIController] [###] UpdateImageSlots() 완료: 생성된 이미지 버튼 수={createdImageButtons.Count}");
        
        // 레이아웃 업데이트 후 삭제 버튼 위치 체크 (ImageBtn_TextClose 근처면 숨김)
        StartCoroutine(CheckDeleteButtonsVisibilityAfterLayout());
    }
    
    /// <summary>
    /// 동적으로 생성된 이미지 버튼들 모두 삭제
    /// </summary>
    private void ClearCreatedImageButtons()
    {
        foreach (var btn in createdImageButtons)
        {
            if (btn != null)
            {
                Destroy(btn);
            }
        }
        createdImageButtons.Clear();
        createdDeleteButtons.Clear();
    }
    
    /// <summary>
    /// 이미지 버튼 동적 생성
    /// </summary>
    private void CreateImageButton(int index, string imagePath)
    {
        if (imageButtonContainer == null)
        {
            Debug.LogWarning("[ImageMemoUIController] imageButtonContainer가 할당되지 않았습니다.");
            return;
        }
        
        // 버튼 컨테이너 생성
        GameObject buttonObj = new GameObject($"ImageButton_{index}");
        buttonObj.transform.SetParent(imageButtonContainer, false);
        
        // RectTransform 설정 (버튼 크기 고정)
        RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
        rectTransform.sizeDelta = imageButtonSize;
        
        // 이미지를 표시할 자식 오브젝트 생성
        GameObject imageObj = new GameObject("Image");
        imageObj.transform.SetParent(buttonObj.transform, false);
        
        // 이미지 RectTransform 설정 (부모를 꽉 채움)
        RectTransform imageRect = imageObj.AddComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;  // (0, 0)
        imageRect.anchorMax = Vector2.one;   // (1, 1)
        imageRect.sizeDelta = Vector2.zero;  // stretch
        imageRect.anchoredPosition = Vector2.zero;
        
        // Image 컴포넌트 추가
        Image bgImage = imageObj.AddComponent<Image>();
        bgImage.color = Color.white;
        bgImage.type = Image.Type.Simple;  // Simple 타입으로 설정
        bgImage.preserveAspect = false;  // 종횡비 유지 안 함 (버튼 크기에 맞춤)
        
        // 둥근 모서리 적용 (RoundedCornersImage 컴포넌트 추가)
        RoundedCornersImage roundedCorners = imageObj.AddComponent<RoundedCornersImage>();
        roundedCorners.SetRadius(imageButtonRadius);
        
        // Button 컴포넌트 추가
        Button button = buttonObj.AddComponent<Button>();
        
        // 이미지 로드 및 표시
        StartCoroutine(LoadImageToButton(bgImage, imagePath));
        
        // 삭제 버튼 생성 (우측 상단)
        CreateDeleteButton(buttonObj.transform, index);
        
        // 이미지 버튼을 addImageButton 바로 다음에 배치 (최신 이미지가 좌측에 오도록)
        // addImageButton은 항상 index 0에 있으므로, 새 버튼은 index 1에 삽입
        buttonObj.transform.SetSiblingIndex(1);
        
        createdImageButtons.Add(buttonObj);
        
        if (logDebug)
            Debug.Log($"[ImageMemoUIController] 이미지 버튼 생성: index={index}");
    }
    
    /// <summary>
    /// 삭제 버튼 생성 (우측 상단)
    /// </summary>
    private void CreateDeleteButton(Transform parent, int imageIndex)
    {
        GameObject deleteObj;
        
        // 프리팹이 할당되어 있으면 프리팹 인스턴스화
        if (deleteButtonPrefab != null)
        {
            deleteObj = Instantiate(deleteButtonPrefab, parent);
            deleteObj.name = "DeleteButton";
            
            // RectTransform 설정 (우측 상단)
            RectTransform rectTransform = deleteObj.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = deleteObj.AddComponent<RectTransform>();
            }
            rectTransform.anchorMin = new Vector2(1, 1);
            rectTransform.anchorMax = new Vector2(1, 1);
            rectTransform.pivot = new Vector2(1, 1);
            rectTransform.anchoredPosition = new Vector2(-5, -5);
            
            // Button 컴포넌트 찾기 또는 추가
            Button button = deleteObj.GetComponent<Button>();
            if (button == null)
            {
                button = deleteObj.AddComponent<Button>();
            }
            
            // 클릭 이벤트 연결
            int capturedIndex = imageIndex;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnDeleteImageClicked(capturedIndex));
            
            // 삭제 버튼 리스트에 추가
            createdDeleteButtons.Add(deleteObj);
            
            if (logDebug)
                Debug.Log($"[ImageMemoUIController] 삭제 버튼 프리팹 인스턴스화: index={imageIndex}");
        }
        else
        {
            // 프리팹이 없으면 자동 생성 (기존 방식)
            deleteObj = new GameObject("DeleteButton");
            deleteObj.transform.SetParent(parent, false);
            
            // RectTransform 설정 (우측 상단)
            RectTransform rectTransform = deleteObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1, 1);
            rectTransform.anchorMax = new Vector2(1, 1);
            rectTransform.pivot = new Vector2(1, 1);
            rectTransform.anchoredPosition = new Vector2(-5, -5);
            rectTransform.sizeDelta = deleteButtonSize;
            
            // Image 컴포넌트 추가
            Image image = deleteObj.AddComponent<Image>();
            if (deleteButtonSprite != null)
            {
                image.sprite = deleteButtonSprite;
            }
            else
            {
                // 기본 X 표시 (스프라이트 없을 때)
                image.color = new Color(0.8f, 0.2f, 0.2f, 1f);
            }
            
            // Button 컴포넌트 추가
            Button button = deleteObj.AddComponent<Button>();
            int capturedIndex = imageIndex;
            button.onClick.AddListener(() => OnDeleteImageClicked(capturedIndex));
            
            // X 텍스트 추가 (스프라이트 없을 때)
            if (deleteButtonSprite == null)
            {
                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(deleteObj.transform, false);
                
                RectTransform textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.sizeDelta = Vector2.zero;
                
                TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
                text.text = "X";
                text.fontSize = 24;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
            }
            
            // 삭제 버튼 리스트에 추가
            createdDeleteButtons.Add(deleteObj);
            
            if (logDebug)
                Debug.Log($"[ImageMemoUIController] 삭제 버튼 자동 생성: index={imageIndex}");
        }
    }

    /// <summary>
    /// 이미지 파일을 버튼에 로드 (EXIF orientation 자동 처리)
    /// </summary>
    private IEnumerator LoadImageToButton(Image targetImage, string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            if (logDebug) Debug.LogWarning($"[ImageMemoUIController] 이미지 파일 없음: {path}");
            yield break;
        }

        if (targetImage == null)
            yield break;

#if NATIVE_GALLERY_ENABLED && (UNITY_ANDROID || UNITY_IOS)
        // NativeGallery의 LoadImageAtPath를 사용하면 EXIF orientation을 자동으로 처리
        Texture2D texture = NativeGallery.LoadImageAtPath(path, maxSize: 2048, markTextureNonReadable: false);
        
        if (texture != null)
        {
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            targetImage.sprite = sprite;
            targetImage.color = Color.white;
            targetImage.preserveAspect = false;  // 이미지를 버튼 크기에 맞춰 꽉 차게 표시
            
            if (logDebug)
                Debug.Log($"[ImageMemoUIController] 이미지 로드 성공 (orientation 처리됨): path={path}");
        }
        else
        {
            Debug.LogWarning($"[ImageMemoUIController] 이미지 로드 실패: {path}");
        }
#else
        // NativeGallery가 없을 때는 기본 방식 사용 (에디터 환경)
        byte[] imageBytes = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2);
        
        if (texture.LoadImage(imageBytes))
        {
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            targetImage.sprite = sprite;
            targetImage.color = Color.white;
            targetImage.preserveAspect = false;  // 이미지를 버튼 크기에 맞춰 꽉 차게 표시
            
            if (logDebug)
                Debug.Log($"[ImageMemoUIController] 이미지 로드 성공: path={path}");
        }
        else
        {
            Debug.LogWarning($"[ImageMemoUIController] 이미지 로드 실패: {path}");
        }
#endif

        yield return null;
    }

    /// <summary>
    /// 이미지 추가 버튼 클릭 - GalleryCamera UI 활성화
    /// </summary>
    private void OnAddImageClicked()
    {
        Debug.Log("[ImageMemoUIController] ★★★ ImageButton(이미지 추가 버튼) 클릭됨!");
        Debug.Log($"[ImageMemoUIController] ★★★ 현재 이미지 개수: {imagePaths.Count}/{MAX_IMAGES}");
        
        if (imagePaths.Count >= MAX_IMAGES)
        {
            Debug.LogWarning($"[ImageMemoUIController] ★★★ 최대 이미지 개수({MAX_IMAGES}개)에 도달했습니다.");
            return;
        }

        // GalleryCamera UI 활성화
        if (galleryCameraUI != null)
        {
            galleryCameraUI.SetActive(true);
            Debug.Log("[ImageMemoUIController] ★★★ GalleryCamera UI 활성화됨!");
        }
        else
        {
            // GalleryCamera UI가 없으면 기존 방식대로 바로 갤러리 열기
            Debug.LogWarning("[ImageMemoUIController] ★★★ GalleryCamera UI가 null입니다. 바로 갤러리를 엽니다.");
            PickImageFromGallery();
        }
    }
    
    /// <summary>
    /// 갤러리 버튼 클릭 - 갤러리에서 이미지 선택
    /// </summary>
    private void OnGalleryButtonClicked()
    {
        Debug.Log("[ImageMemoUIController] ★★★ 갤러리 버튼 클릭!");
        
        // GalleryCamera UI 비활성화
        if (galleryCameraUI != null)
        {
            galleryCameraUI.SetActive(false);
        }
        
        // 갤러리에서 이미지 선택
        PickImageFromGallery();
    }
    
    /// <summary>
    /// 카메라 버튼 클릭 - 카메라로 사진 촬영
    /// </summary>
    private void OnCameraButtonClicked()
    {
        Debug.Log("[ImageMemoUIController] ★★★ 카메라 버튼 클릭!");
        
        // GalleryCamera UI 비활성화
        if (galleryCameraUI != null)
        {
            galleryCameraUI.SetActive(false);
        }
        
        // 카메라로 사진 촬영
        TakePhotoWithCamera();
    }

    /// <summary>
    /// 갤러리에서 이미지 선택 (최대 3개)
    /// NativeGallery 플러그인 설치 후 Player Settings > Scripting Define Symbols에 
    /// NATIVE_GALLERY_ENABLED 추가 필요
    /// </summary>
    private void PickImageFromGallery()
    {
        int remainingSlots = MAX_IMAGES - imagePaths.Count;
        
        Debug.Log($"[ImageMemoUIController] ★★★ 갤러리 열기 - 최대 {remainingSlots}개 선택 가능");
        
#if NATIVE_GALLERY_ENABLED && (UNITY_ANDROID || UNITY_IOS)
        // NativeGallery 플러그인을 사용하여 갤러리 접근
        Debug.Log("[ImageMemoUIController] ★★★ NativeGallery 사용 (Android/iOS)");
        OpenGalleryWithNativeGallery(remainingSlots);
#else
        // NativeGallery 플러그인이 없거나 에디터 환경
        Debug.Log("[ImageMemoUIController] ★★★ 테스트 이미지 추가 (NativeGallery 없음)");
        AddTestImage();
#endif
    }
    
    /// <summary>
    /// 카메라로 사진 촬영
    /// NativeCamera 플러그인 설치 후 Player Settings > Scripting Define Symbols에 
    /// NATIVE_CAMERA_ENABLED 추가 필요
    /// </summary>
    private void TakePhotoWithCamera()
    {
        if (imagePaths.Count >= MAX_IMAGES)
        {
            Debug.Log("[ImageMemoUIController] 최대 이미지 개수(3개)에 도달했습니다.");
            return;
        }
        
        Debug.Log("[ImageMemoUIController] 카메라 열기");
        
#if NATIVE_CAMERA_ENABLED && (UNITY_ANDROID || UNITY_IOS)
        // NativeCamera 플러그인을 사용하여 카메라 접근
        OpenCameraWithNativeCamera();
#else
        // NativeCamera 플러그인이 없거나 에디터 환경
        Debug.Log("[ImageMemoUIController] 테스트 이미지 추가 (카메라 기능은 NATIVE_CAMERA_ENABLED 심볼 필요)");
        AddTestImage();
#endif
    }
    
#if NATIVE_GALLERY_ENABLED && (UNITY_ANDROID || UNITY_IOS)
    /// <summary>
    /// NativeGallery를 사용하여 갤러리 열기
    /// </summary>
    private void OpenGalleryWithNativeGallery(int maxCount)
    {
        // 다중 선택 모드로 갤러리 열기
        // NativeGallery.GetImagesFromGallery는 권한이 없으면 자동으로 요청함
        NativeGallery.GetImagesFromGallery((paths) =>
        {
            Debug.Log($"[ImageMemoUIController] 갤러리에서 선택된 이미지: {(paths != null ? paths.Length : 0)}개");
            
            if (paths != null && paths.Length > 0)
            {
                int addCount = Mathf.Min(paths.Length, maxCount);
                for (int i = 0; i < addCount; i++)
                {
                    ProcessSelectedImage(paths[i]);
                }
            }
        }, "이미지 선택 (최대 " + maxCount + "개)", "image/*");
    }
#endif

#if NATIVE_CAMERA_ENABLED && (UNITY_ANDROID || UNITY_IOS)
    /// <summary>
    /// NativeCamera를 사용하여 카메라 열기
    /// </summary>
    private void OpenCameraWithNativeCamera()
    {
        // 카메라 권한 체크 (false = 카메라 권한)
        bool hasPermission = NativeCamera.CheckPermission(false);
        
        if (!hasPermission)
        {
            // 권한이 없을 때 - TakePicture가 자동으로 권한을 요청하므로 그냥 진행
            Debug.Log("[ImageMemoUIController] 카메라 권한 없음 - 권한 요청 예정");
        }
        
        // 카메라 촬영
        // NativeCamera.TakePicture는 권한이 없으면 자동으로 요청함
        NativeCamera.TakePicture((path) =>
        {
            Debug.Log($"[ImageMemoUIController] 카메라로 촬영된 이미지: {path}");
            
            if (!string.IsNullOrEmpty(path))
            {
                ProcessSelectedImage(path);
            }
        }, maxSize: 2048);  // 최대 해상도 2048px (메모리 절약)
    }
#endif

    /// <summary>
    /// 테스트용 이미지 추가 (에디터/PC 환경)
    /// </summary>
    private void AddTestImage()
    {
        Debug.Log("[ImageMemoUIController] ★★★ AddTestImage() 호출됨!");
        
        if (imagePaths.Count >= MAX_IMAGES)
        {
            Debug.LogWarning($"[ImageMemoUIController] ★★★ 최대 이미지 개수({MAX_IMAGES})에 도달했습니다.");
            return;
        }
        
        // 테스트용 더미 이미지 생성
        string testImagePath = Path.Combine(imageSaveFolder, $"test_image_{DateTime.Now.Ticks}.png");
        Debug.Log($"[ImageMemoUIController] ★★★ 테스트 이미지 생성 중: {testImagePath}");
        
        // 간단한 색상 이미지 생성
        Texture2D testTexture = new Texture2D(200, 200);
        Color baseColor = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
        Color[] colors = new Color[200 * 200];
        for (int i = 0; i < colors.Length; i++)
        {
            // 그라데이션 효과
            float t = (float)i / colors.Length;
            colors[i] = Color.Lerp(baseColor, Color.white, t * 0.3f);
        }
        testTexture.SetPixels(colors);
        testTexture.Apply();
        
        byte[] bytes = testTexture.EncodeToPNG();
        File.WriteAllBytes(testImagePath, bytes);
        
        Destroy(testTexture);
        
        imagePaths.Add(testImagePath);
        Debug.Log($"[ImageMemoUIController] ★★★ imagePaths에 추가됨! 현재 개수: {imagePaths.Count}");
        
        UpdateImageSlots();
        
        Debug.Log($"[ImageMemoUIController] ★★★ 테스트 이미지 추가 완료: path={testImagePath}, 총 이미지={imagePaths.Count}");
    }

    /// <summary>
    /// 선택된 이미지 처리 (갤러리에서 선택 후)
    /// </summary>
    private void ProcessSelectedImage(string sourcePath)
    {
        Debug.Log($"[ImageMemoUIController] ★★★ ProcessSelectedImage() 호출됨: path={sourcePath}");
        
        if (imagePaths.Count >= MAX_IMAGES)
        {
            Debug.LogWarning($"[ImageMemoUIController] ★★★ 최대 이미지 개수({MAX_IMAGES})에 도달하여 추가 이미지 무시됨.");
            return;
        }
        
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
        {
            Debug.LogWarning($"[ImageMemoUIController] ★★★ 유효하지 않은 이미지 경로: {sourcePath}");
            return;
        }

        try
        {
            // 이미지를 앱 저장소로 복사
            string fileName = $"memo_img_{DateTime.Now.Ticks}{Path.GetExtension(sourcePath)}";
            string destPath = Path.Combine(imageSaveFolder, fileName);
            
            Debug.Log($"[ImageMemoUIController] ★★★ 이미지 복사 중: {sourcePath} → {destPath}");
            
            File.Copy(sourcePath, destPath, true);
            
            imagePaths.Add(destPath);
            Debug.Log($"[ImageMemoUIController] ★★★ imagePaths에 추가됨! 현재 개수: {imagePaths.Count}");
            
            UpdateImageSlots();
            
            Debug.Log($"[ImageMemoUIController] ★★★ 이미지 추가 완료: {destPath}, 총 이미지={imagePaths.Count}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ImageMemoUIController] ★★★ 이미지 복사 실패: {e.Message}");
        }
    }

    /// <summary>
    /// 이미지 삭제 버튼 클릭
    /// </summary>
    private void OnDeleteImageClicked(int index)
    {
        if (index < 0 || index >= imagePaths.Count)
        {
            Debug.LogWarning($"[ImageMemoUIController] 유효하지 않은 삭제 인덱스: {index}");
            return;
        }

        string deletedPath = imagePaths[index];
        imagePaths.RemoveAt(index);
        
        // 파일 삭제 (선택사항 - 주석 해제하면 파일도 삭제됨)
        // try { if (File.Exists(deletedPath)) File.Delete(deletedPath); } catch { }

        // UI 업데이트
        UpdateImageSlots();
        
        if (logDebug)
            Debug.Log($"[ImageMemoUIController] 이미지 삭제됨: index={index}, 남은 이미지 수: {imagePaths.Count}");
    }

    /// <summary>
    /// 저장 버튼 클릭
    /// </summary>
    private void SaveImageMemoNow()
    {
        Debug.Log("[ImageMemoUIController] ★★★ SaveImageMemoNow() 호출됨!");
        Debug.Log($"[ImageMemoUIController] ★★★ currentMemo={(currentMemo != null ? currentMemo.name : "null")}");
        Debug.Log($"[ImageMemoUIController] ★★★ inputTitle={(inputTitle != null ? "있음" : "null")}");
        Debug.Log($"[ImageMemoUIController] ★★★ inputBody={(inputBody != null ? "있음" : "null")}");
        Debug.Log($"[ImageMemoUIController] ★★★ pinStore={(pinStore != null ? "있음" : "null")}");

        if (currentMemo != null && inputTitle != null && inputBody != null)
        {
            MemoData memoData = currentMemo.GetComponent<MemoData>();
            if (memoData != null)
            {
                Debug.Log($"[ImageMemoUIController] ★★★ 저장 시작: id={memoData.id}, title={inputTitle.text}");
            }
            
            ApplySaveFromUIAndSync();
            Debug.Log("[ImageMemoUIController] ★★★ ApplySaveFromUIAndSync() 완료");
        }
        else
        {
            Debug.LogWarning($"[ImageMemoUIController] ★★★ 저장 실패 - currentMemo={(currentMemo != null)}, inputTitle={(inputTitle != null)}, inputBody={(inputBody != null)}");
        }

        // UI 닫기
        Debug.Log("[ImageMemoUIController] ★★★ CloseWithoutSaving() 호출");
        CloseWithoutSaving();
    }

    /// <summary>
    /// UI의 Draft 값을 MemoData와 JSON에 저장
    /// </summary>
    private void ApplySaveFromUIAndSync()
    {
        MemoData memo = currentMemo.GetComponent<MemoData>();
        if (memo == null)
        {
            Debug.LogWarning("[ImageMemoUIController] MemoData가 없습니다.");
            return;
        }

        // 텍스트 데이터 저장
        string title = draftTitle ?? (inputTitle != null ? inputTitle.text : "");
        string body = draftBody ?? (inputBody != null ? inputBody.text : "");
        string location = draftLocation ?? (inputLocation != null ? inputLocation.text : "");

        memo.title = title;
        memo.body = body;
        memo.content = memo.body;
        memo.location = location;

        // 이미지 경로 저장
        memo.imagePaths = new List<string>(imagePaths);
        memo.memoType = imagePaths.Count > 0 ? "image" : "text";

        Debug.Log($"[ImageMemoUIController] ★ 이미지 메모 저장: id={memo.id}, title={title}, memoType={memo.memoType}, imageCount={imagePaths.Count}, 핀 오브젝트={currentMemo.name}");

        // 날짜 저장
        if (calendarController != null)
        {
            DateTime selectedDate = calendarController.GetSelectedDate();
            memo.DueDateDateTime = selectedDate;
        }

        // 시간 저장
        if (timePickerController != null)
        {
            memo.dueTime = timePickerController.GetSelectedTimeString();
        }

        // 긴급도 저장
        if (emergencyButtonManager != null)
        {
            int emergencyIndex = emergencyButtonManager.GetSelectedButtonIndex();
            memo.emergencyLevel = emergencyIndex + 1;
        }

        // JSON 저장
        if (pinStore != null)
        {
            pinStore.SaveImageMemoById(memo.id, memo.title, memo.body, memo.location, imagePaths);
            pinStore.UpdateMemoDueDate(memo.id, memo.dueDate);
            pinStore.UpdateMemoDueTime(memo.id, memo.dueTime);
            pinStore.UpdateMemoEmergencyLevel(memo.id, memo.emergencyLevel);

            Debug.Log($"[ImageMemoUIController] ★ TabPinCreate.SaveImageMemoById 호출 완료: id={memo.id}, title={memo.title}, images={imagePaths.Count}");
        }
        else
        {
            Debug.LogWarning("[ImageMemoUIController] ★ pinStore가 null입니다! Inspector에서 TabPinCreate를 할당해주세요.");
        }
        
        // 핀의 활성화 상태 확인
        Debug.Log($"[ImageMemoUIController] ★ 핀 상태: active={currentMemo.activeSelf}, activeInHierarchy={currentMemo.activeInHierarchy}");
    }

    /// <summary>
    /// 저장하지 않고 닫기
    /// </summary>
    private void CloseWithoutSaving()
    {
        Debug.Log("[ImageMemoUIController] CloseWithoutSaving()");

        // 동적 생성된 이미지 버튼들 정리
        ClearCreatedImageButtons();
        
        // 이미지 경로 목록 초기화
        imagePaths.Clear();
        
        // 추가 버튼 초기 상태로 복원
        InitializeImageButtonState();
        
        // GalleryCamera UI 닫기
        if (galleryCameraUI != null)
        {
            galleryCameraUI.SetActive(false);
        }

        // 달력/시간 선택기 닫기
        if (calendarController != null) calendarController.CloseCalendar();
        if (timePickerController != null) timePickerController.CloseTimePicker();
        
        // ImageInputField_Body 높이 원래대로 복원
        CollapseBodyHeight();

        // BgImage 비활성화
        if (bgImage != null)
        {
            bgImage.SetActive(false);
            Debug.Log("[ImageMemoUIController] BgImage 비활성화됨");
        }

        // MemoUIController의 CloseWithoutSaving 호출
        if (memoUIController != null)
        {
            memoUIController.CloseWithoutSaving();
        }

        currentMemo = null;
    }

    /// <summary>
    /// 입력 변화 감지 리스너 연결
    /// </summary>
    private void WireDraftListeners()
    {
        if (inputTitle)
        {
            inputTitle.onValueChanged.RemoveListener(OnTitleChanged);
            inputTitle.onValueChanged.AddListener(OnTitleChanged);
        }

        if (inputBody)
        {
            inputBody.onValueChanged.RemoveListener(OnBodyChanged);
            inputBody.onValueChanged.AddListener(OnBodyChanged);
        }

        if (inputLocation)
        {
            inputLocation.onValueChanged.RemoveListener(OnLocationChanged);
            inputLocation.onValueChanged.AddListener(OnLocationChanged);
        }
    }
    
    /// <summary>
    /// 입력 필드 선택 감지 리스너 연결
    /// </summary>
    private void WireInputFieldSelectListeners()
    {
        if (inputTitle)
        {
            inputTitle.onSelect.RemoveListener(OnInputFieldSelected);
            inputTitle.onSelect.AddListener(OnInputFieldSelected);
            inputTitle.onEndEdit.RemoveListener(OnInputFieldEndEdit);
            inputTitle.onEndEdit.AddListener(OnInputFieldEndEdit);
        }

        if (inputBody)
        {
            inputBody.onSelect.RemoveListener(OnInputFieldSelected);
            inputBody.onSelect.AddListener(OnInputFieldSelected);
            inputBody.onEndEdit.RemoveListener(OnInputFieldEndEdit);
            inputBody.onEndEdit.AddListener(OnInputFieldEndEdit);
        }
    }
    
    /// <summary>
    /// InputField가 선택되었을 때 호출 (입력 시작)
    /// </summary>
    private void OnInputFieldSelected(string value)
    {
        // ImageInputField_Body의 높이를 확장
        ExpandBodyHeight();
    }
    
    /// <summary>
    /// InputField 입력이 끝났을 때 호출 (입력 종료)
    /// </summary>
    private void OnInputFieldEndEdit(string value)
    {
        // ImageInputField_Body의 높이를 원래대로 복원
        CollapseBodyHeight();
    }
    
    /// <summary>
    /// ImageInputField_Body의 높이 확장
    /// </summary>
    private void ExpandBodyHeight()
    {
        if (bodyRectTransform != null && !isBodyExpanded)
        {
            // Body 높이 확장
            Vector2 sizeDelta = bodyRectTransform.sizeDelta;
            sizeDelta.y = expandedBodyHeight;
            bodyRectTransform.sizeDelta = sizeDelta;
            
            // 다른 요소들을 아래로 이동 (인스펙터에서 설정한 거리만큼)
            MoveElementsDown(elementsMoveDistance);
            
            isBodyExpanded = true;
            
            if (logDebug)
                Debug.Log($"[ImageMemoUIController] ImageInputField_Body 높이 확장: {defaultBodyHeight} → {expandedBodyHeight}, 요소들 {elementsMoveDistance}만큼 아래로 이동");
        }
    }
    
    /// <summary>
    /// ImageInputField_Body의 높이 원래대로 복원
    /// </summary>
    private void CollapseBodyHeight()
    {
        if (bodyRectTransform != null && isBodyExpanded)
        {
            // Body 높이 원래대로
            Vector2 sizeDelta = bodyRectTransform.sizeDelta;
            sizeDelta.y = defaultBodyHeight;
            bodyRectTransform.sizeDelta = sizeDelta;
            
            // 다른 요소들을 원래 위치로 복원
            RestoreElementsPosition();
            
            isBodyExpanded = false;
            
            if (logDebug)
                Debug.Log($"[ImageMemoUIController] ImageInputField_Body 높이 복원: {expandedBodyHeight} → {defaultBodyHeight}, 요소들 원래 위치로 복원");
        }
    }
    
    /// <summary>
    /// ImageMovie, Deadline, ImageEmergency를 아래로 이동
    /// </summary>
    private void MoveElementsDown(float delta)
    {
        // ImageMovie 이동
        if (imageMovieRectTransform != null)
        {
            Vector2 pos = imageMovieRectTransform.anchoredPosition;
            pos.y -= delta;  // Y축은 위가 +이므로 빼기
            imageMovieRectTransform.anchoredPosition = pos;
            
            if (logDebug)
                Debug.Log($"[ImageMemoUIController] ImageMovie 이동: {originalImageMoviePosition} → {pos}");
        }
        
        // Deadline 이동
        if (deadlineRectTransform != null)
        {
            Vector2 pos = deadlineRectTransform.anchoredPosition;
            pos.y -= delta;
            deadlineRectTransform.anchoredPosition = pos;
            
            if (logDebug)
                Debug.Log($"[ImageMemoUIController] Deadline 이동: {originalDeadlinePosition} → {pos}");
        }
        
        // ImageEmergency 이동
        if (imageEmergencyRectTransform != null)
        {
            Vector2 pos = imageEmergencyRectTransform.anchoredPosition;
            pos.y -= delta;
            imageEmergencyRectTransform.anchoredPosition = pos;
            
            if (logDebug)
                Debug.Log($"[ImageMemoUIController] ImageEmergency 이동: {originalImageEmergencyPosition} → {pos}");
        }
    }
    
    /// <summary>
    /// ImageMovie, Deadline, ImageEmergency를 원래 위치로 복원
    /// </summary>
    private void RestoreElementsPosition()
    {
        // ImageMovie 복원
        if (imageMovieRectTransform != null)
        {
            imageMovieRectTransform.anchoredPosition = originalImageMoviePosition;
            
            if (logDebug)
                Debug.Log($"[ImageMemoUIController] ImageMovie 위치 복원: {originalImageMoviePosition}");
        }
        
        // Deadline 복원
        if (deadlineRectTransform != null)
        {
            deadlineRectTransform.anchoredPosition = originalDeadlinePosition;
            
            if (logDebug)
                Debug.Log($"[ImageMemoUIController] Deadline 위치 복원: {originalDeadlinePosition}");
        }
        
        // ImageEmergency 복원
        if (imageEmergencyRectTransform != null)
        {
            imageEmergencyRectTransform.anchoredPosition = originalImageEmergencyPosition;
            
            if (logDebug)
                Debug.Log($"[ImageMemoUIController] ImageEmergency 위치 복원: {originalImageEmergencyPosition}");
        }
    }

    private void OnTitleChanged(string v)
    {
        if (isLoadingUI) return;
        draftTitle = v ?? "";
        UpdateTitleOutlineColor();
    }

    private void OnBodyChanged(string v)
    {
        if (isLoadingUI) return;
        draftBody = v ?? "";
        UpdateBodyOutlineColor();
    }

    private void OnLocationChanged(string v)
    {
        if (isLoadingUI) return;
        draftLocation = v ?? "";
    }

    /// <summary>
    /// Title Outline 색상 업데이트
    /// </summary>
    private void UpdateTitleOutlineColor()
    {
        if (titleOutline != null)
        {
            bool hasFilled = !string.IsNullOrWhiteSpace(draftTitle);
            Color newColor = hasFilled ? filledOutlineColor : emptyOutlineColor;
            titleOutline.effectColor = newColor;
            Debug.Log($"[ImageMemoUIController] ★ UpdateTitleOutlineColor - hasFilled: {hasFilled}, color: {newColor}");
        }
        else
        {
            Debug.LogWarning("[ImageMemoUIController] ★ UpdateTitleOutlineColor - titleOutline이 NULL입니다!");
        }
    }

    /// <summary>
    /// Body Outline 색상 업데이트
    /// </summary>
    private void UpdateBodyOutlineColor()
    {
        if (bodyOutline != null)
        {
            bool hasFilled = !string.IsNullOrWhiteSpace(draftBody);
            bodyOutline.effectColor = hasFilled ? filledOutlineColor : emptyOutlineColor;
        }
    }

    /// <summary>
    /// 메타 정보 텍스트 갱신
    /// </summary>
    private void UpdateMetaInfoText()
    {
        // 날짜 텍스트 갱신
        if (dateText)
        {
            string dateStr;
            if (calendarController != null)
            {
                dateStr = calendarController.GetSelectedDateString("MM/dd");
            }
            else
            {
                dateStr = DateTime.Now.ToString("MM/dd");
            }

            dateText.enableWordWrapping = false;
            dateText.overflowMode = TextOverflowModes.Ellipsis;
            dateText.maxVisibleLines = 1;
            dateText.text = dateStr;
        }

        // 사용자 ID 텍스트 갱신
        if (userIdText)
        {
            string userId = PlayerPrefs.GetString(userIdPrefKey, "");
            if (string.IsNullOrWhiteSpace(userId) && useDeviceIdFallback)
            {
                string dev = SystemInfo.deviceUniqueIdentifier ?? "";
                userId = (dev.Length > 8) ? dev.Substring(0, 8) : dev;
            }

            userIdText.enableWordWrapping = false;
            userIdText.overflowMode = TextOverflowModes.Ellipsis;
            userIdText.maxVisibleLines = 1;
            userIdText.text = userId;
        }
    }

    /// <summary>
    /// 현재 편집 중인 메모 설정
    /// </summary>
    public void SetCurrentMemo(GameObject memo)
    {
        currentMemo = memo;
    }

    /// <summary>
    /// 현재 편집 중인 메모 가져오기
    /// </summary>
    public GameObject GetCurrentMemo()
    {
        return currentMemo;
    }
    
    /// <summary>
    /// GalleryCamera UI 닫기 (외부에서 호출 가능)
    /// </summary>
    public void CloseGalleryCameraUI()
    {
        if (galleryCameraUI != null)
        {
            galleryCameraUI.SetActive(false);
            Debug.Log("[ImageMemoUIController] GalleryCamera UI 닫힘");
        }
    }
    
    /// <summary>
    /// 드롭다운이 닫힐 때 호출 - 밀려난 요소들을 원래 위치로 복원
    /// </summary>
    public void OnDropdownClosed()
    {
        // InputField가 확장되어 있으면 요소들을 원래 위치로 복원
        if (isBodyExpanded)
        {
            RestoreElementsPosition();
            if (logDebug)
                Debug.Log("[ImageMemoUIController] 드롭다운 닫힘 - 요소들 위치 복원");
        }
    }
    
    /// <summary>
    /// 레이아웃 업데이트 후 삭제 버튼들의 가시성 체크
    /// ImageBtn_TextClose와의 거리가 반경 안에 있으면 숨김
    /// </summary>
    private IEnumerator CheckDeleteButtonsVisibilityAfterLayout()
    {
        Debug.Log($"[ImageMemoUIController] ★ CheckDeleteButtonsVisibilityAfterLayout 시작: createdDeleteButtons.Count={createdDeleteButtons.Count}");
        
        // 레이아웃 업데이트를 위해 1프레임 대기
        yield return null;
        
        // 추가로 LayoutGroup 강제 업데이트
        if (imageButtonContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(imageButtonContainer);
        }
        
        // 한 프레임 더 대기 (레이아웃 완전 적용)
        yield return null;
        
        // 추가 대기 (Canvas 업데이트)
        yield return new WaitForEndOfFrame();
        
        CheckDeleteButtonsVisibility();
    }
    
    /// <summary>
    /// 삭제 버튼들의 가시성 체크 (ImageBtn_TextClose 근처면 숨김)
    /// 가장 우측(마지막) 이미지 버튼의 삭제 버튼만 체크
    /// </summary>
    private void CheckDeleteButtonsVisibility()
    {
        Debug.Log($"[ImageMemoUIController] ★ CheckDeleteButtonsVisibility 호출됨");
        Debug.Log($"[ImageMemoUIController] ★ imageBtnTextCloseRect={(imageBtnTextCloseRect != null ? imageBtnTextCloseRect.name : "NULL")}");
        Debug.Log($"[ImageMemoUIController] ★ createdDeleteButtons.Count={createdDeleteButtons.Count}");
        
        if (imageBtnTextCloseRect == null)
        {
            Debug.LogWarning("[ImageMemoUIController] ★ imageBtnTextCloseRect가 할당되지 않아 삭제 버튼 숨김 체크 스킵! Inspector에서 ImageBtn_TextClose를 할당해주세요.");
            return;
        }
        
        if (createdDeleteButtons.Count == 0)
        {
            Debug.Log("[ImageMemoUIController] ★ 삭제 버튼이 없어서 체크 스킵");
            return;
        }
        
        // ImageBtn_TextClose의 중심 좌표 계산 (GetWorldCorners 사용)
        Vector2 textCloseCenterPos = GetRectTransformCenterPosition(imageBtnTextCloseRect);
        Debug.Log($"[ImageMemoUIController] ★ ImageBtn_TextClose 중심 위치: {textCloseCenterPos}");
        
        // 가장 우측에 있는 이미지 버튼의 삭제 버튼 찾기
        GameObject rightmostDeleteBtn = null;
        float maxX = float.MinValue;
        
        foreach (var deleteBtn in createdDeleteButtons)
        {
            if (deleteBtn == null) continue;
            
            RectTransform deleteRect = deleteBtn.GetComponent<RectTransform>();
            if (deleteRect == null) continue;
            
            Vector2 deleteBtnCenterPos = GetRectTransformCenterPosition(deleteRect);
            Debug.Log($"[ImageMemoUIController] ★ 삭제 버튼 '{deleteBtn.name}' 중심 위치: {deleteBtnCenterPos}");
            
            if (deleteBtnCenterPos.x > maxX)
            {
                maxX = deleteBtnCenterPos.x;
                rightmostDeleteBtn = deleteBtn;
            }
        }
        
        if (rightmostDeleteBtn == null)
        {
            Debug.LogWarning("[ImageMemoUIController] ★ 가장 우측 삭제 버튼을 찾지 못함");
            return;
        }
        
        // 가장 우측 삭제 버튼의 RectTransform
        RectTransform rightmostRect = rightmostDeleteBtn.GetComponent<RectTransform>();
        
        // 스크린 좌표 기준으로 거리 계산 (enableLog=true로 디버그 로그 출력)
        float distance = GetDistanceBetweenRectTransforms(imageBtnTextCloseRect, rightmostRect, enableLog: true);
        
        // 반경 안에 있으면 숨김
        bool shouldHide = distance < deleteButtonHideRadius;
        rightmostDeleteBtn.SetActive(!shouldHide);
        
        Debug.Log($"[ImageMemoUIController] ★ 가장 우측 삭제 버튼 '{rightmostDeleteBtn.name}': 스크린거리={distance:F1}, hideRadius={deleteButtonHideRadius}, 숨김={shouldHide}");
    }
    
    /// <summary>
    /// RectTransform의 중심 위치를 world 좌표로 반환
    /// TransformPoint + rect.center를 사용하여 정확한 위치 계산
    /// </summary>
    private Vector2 GetRectTransformCenterPosition(RectTransform rectTransform)
    {
        if (rectTransform == null) return Vector2.zero;
        
        // rect.center는 pivot 기준 로컬 좌표의 중심
        // TransformPoint로 world 좌표로 변환
        Vector3 worldCenter = rectTransform.TransformPoint(rectTransform.rect.center);
        
        return new Vector2(worldCenter.x, worldCenter.y);
    }
    
    /// <summary>
    /// 두 RectTransform 사이의 거리를 실제 화면(스크린) 좌표 기준으로 계산
    /// </summary>
    private float GetDistanceBetweenRectTransforms(RectTransform rect1, RectTransform rect2, bool enableLog = false)
    {
        if (rect1 == null || rect2 == null) return float.MaxValue;
        
        // 각 RectTransform의 스크린 좌표 얻기
        Vector2 screenPos1 = GetScreenPosition(rect1, enableLog);
        Vector2 screenPos2 = GetScreenPosition(rect2, enableLog);
        
        if (enableLog)
            Debug.Log($"[ImageMemoUIController] ★★ 스크린 좌표: rect1={screenPos1}, rect2={screenPos2}");
        
        return Vector2.Distance(screenPos1, screenPos2);
    }
    
    /// <summary>
    /// RectTransform의 중심 위치를 실제 스크린 좌표로 반환
    /// </summary>
    private Vector2 GetScreenPosition(RectTransform rectTransform, bool enableLog = false)
    {
        if (rectTransform == null) return Vector2.zero;
        
        // 디버그: 오브젝트 정보 출력 (enableLog가 true일 때만)
        if (enableLog)
        {
            Debug.Log($"[ImageMemoUIController] ★★★ GetScreenPosition - name={rectTransform.name}, " +
                      $"active={rectTransform.gameObject.activeInHierarchy}, " +
                      $"anchoredPos={rectTransform.anchoredPosition}, " +
                      $"position={rectTransform.position}");
        }
        
        // Canvas 찾기
        Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            if (enableLog)
                Debug.LogWarning($"[ImageMemoUIController] ★★★ {rectTransform.name}의 Canvas를 찾을 수 없음!");
            return Vector2.zero;
        }
        
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
            Vector2 screenPos = new Vector2(center.x * scaleFactor, center.y * scaleFactor);
            
            return screenPos;
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