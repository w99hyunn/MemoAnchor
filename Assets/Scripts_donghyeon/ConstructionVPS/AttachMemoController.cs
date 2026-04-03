
// AttachMemo UI 거리 기반 상태 관리 스크립트
// - 카메라가 부착 가능한 위치에 가까워지면 UI 상태 변경
// - 버튼 클릭 시 BottomBar 활성화

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;
using System.Collections.Generic;

public class AttachMemoController : MonoBehaviour
{
    [Header("AR References")]
    [Tooltip("ARRaycastManager 컴포넌트를 넣는 자리")]
    [SerializeField] private ARRaycastManager arRaycastManager;
    
    [Tooltip("AR Camera를 넣는 자리")]
    [SerializeField] private Camera arCamera;

    [Header("AttachMemo UI Elements")]
    [Tooltip("AttachMemo 부모 오브젝트 (전체 UI 컨테이너)")]
    [SerializeField] private GameObject attachMemoContainer;
    
    [Tooltip("AttachMemoTextImage 오브젝트 (안내 텍스트 배경 이미지)")]
    [SerializeField] private GameObject attachMemoTextImage;
    
    [Tooltip("AttachMemoTextImage 안의 TMP_Text")]
    [SerializeField] private TMP_Text attachMemoText;
    
    [Tooltip("AttachMemoBt (부착 버튼)")]
    [SerializeField] private Button attachMemoBt;
    
    [Tooltip("AttachMemoBt 안의 Icon (Image 컴포넌트)")]
    [SerializeField] private Image attachMemoIcon;
    
    [Tooltip("화면 중앙의 AttachMemoIcon (위치 기준점)")]
    [SerializeField] private RectTransform centerAttachMemoIcon;

    [Header("TopLeftUI (메모 편집 시 숨김)")]
    [Tooltip("TopLeftUI 오브젝트 (메모 편집 시 숨김)")]
    [SerializeField] private GameObject topLeftUI;

    [Header("BottomBar")]
    [Tooltip("BottomBar 오브젝트를 넣는 자리")]
    [SerializeField] private GameObject bottomBar;
    
    [Tooltip("MemoUIController 참조 (메모 부착 처리용)")]
    [SerializeField] private MemoUIController memoUIController;

    [Header("Distance Thresholds (meters)")]
    [Tooltip("가까이 이동 안내를 표시할 최대 거리 (이 거리 이하면 '더 가까이 이동하십시오' 표시)")]
    [SerializeField] private float approachingDistance = 3.0f;
    
    [Tooltip("부착 가능한 거리 (이 거리 이하면 버튼 활성화)")]
    [SerializeField] private float attachableDistance = 1.5f;

    [Header("UI Colors")]
    [Tooltip("가까이 이동 안내 시 AttachMemoTextImage 색상")]
    [SerializeField] private Color approachingColor = new Color(0xBB / 255f, 0xC0 / 255f, 0xC6 / 255f, 1f); // #BBC0C6
    
    [Tooltip("부착 가능 시 Icon 색상 (흰색)")]
    [SerializeField] private Color attachableIconColor = Color.white;
    
    [Tooltip("비활성 시 Icon 색상 (회색)")]
    [SerializeField] private Color disabledIconColor = new Color(0.66f, 0.67f, 0.69f, 1f);

    [Header("Messages")]
    [Tooltip("가까이 이동 안내 메시지")]
    [SerializeField] private string approachingMessage = "더 가까이 이동하십시오.";
    
    [Tooltip("기본 안내 메시지 (너무 멀 때)")]
    [SerializeField] private string defaultMessage = "부착할 위치에 대고 화면을 누르십시오.";

    [Header("Pin Creation")]
    [Tooltip("TabPinCreate 컴포넌트 참조 (핀 생성 및 저장)")]
    [SerializeField] private TabPinCreate tabPinCreate;

    [Header("Debug")]
    [Tooltip("디버그 로그 출력 여부")]
    [SerializeField] private bool logDebug = false;

    // 현재 상태
    private enum AttachState
    {
        TooFar,         // 너무 멀리 있음
        Approaching,    // 가까이 오고 있음
        Attachable      // 부착 가능
    }
    
    private AttachState currentState = AttachState.TooFar;
    private static readonly List<ARRaycastHit> arHits = new List<ARRaycastHit>();
    
    // 부착 가능한 위치 캐시
    private Vector3 lastHitPosition;
    private Quaternion lastHitRotation;
    private bool hasValidHit = false;
    
    // 디버그용 타이머
    private float debugLogTimer = 0f;
    private const float DEBUG_LOG_INTERVAL = 3f; // 3초마다 로그

    private void Awake()
    {
        // AR Camera 자동 할당
        if (arCamera == null)
            arCamera = Camera.main;
            
        // 버튼 클릭 이벤트 연결
        if (attachMemoBt != null)
        {
            attachMemoBt.onClick.RemoveListener(OnAttachButtonClicked);
            attachMemoBt.onClick.AddListener(OnAttachButtonClicked);
        }
    }

    private void Start()
    {
        // 참조 상태 디버그 로그
        if (logDebug)
        {
            Debug.Log($"[AttachMemoController] === 초기화 상태 확인 ===");
            Debug.Log($"[AttachMemoController] arRaycastManager: {(arRaycastManager != null ? "연결됨" : "NULL")}");
            Debug.Log($"[AttachMemoController] arCamera: {(arCamera != null ? arCamera.name : "NULL")}");
            Debug.Log($"[AttachMemoController] attachMemoContainer: {(attachMemoContainer != null ? (attachMemoContainer.activeInHierarchy ? "활성" : "비활성") : "NULL")}");
            Debug.Log($"[AttachMemoController] attachMemoTextImage: {(attachMemoTextImage != null ? "연결됨" : "NULL")}");
            Debug.Log($"[AttachMemoController] attachMemoText: {(attachMemoText != null ? "연결됨" : "NULL")}");
            Debug.Log($"[AttachMemoController] attachMemoBt: {(attachMemoBt != null ? "연결됨" : "NULL")}");
            Debug.Log($"[AttachMemoController] attachMemoIcon: {(attachMemoIcon != null ? "연결됨" : "NULL")}");
            Debug.Log($"[AttachMemoController] centerAttachMemoIcon: {(centerAttachMemoIcon != null ? "연결됨" : "NULL")}");
            Debug.Log($"[AttachMemoController] bottomBar: {(bottomBar != null ? "연결됨" : "NULL")}");
            Debug.Log($"[AttachMemoController] tabPinCreate: {(tabPinCreate != null ? "연결됨" : "NULL")}");
            Debug.Log($"[AttachMemoController] approachingDistance: {approachingDistance}m, attachableDistance: {attachableDistance}m");
            Debug.Log($"[AttachMemoController] =============================");
        }
    }

    private void OnEnable()
    {
        // 활성화 시 초기 상태 설정
        currentState = AttachState.TooFar; // 직접 설정 (SetState 호출 시 상태 동일하면 무시되므로)
        
        // BottomBar가 기본적으로 비활성화되어 있는지 확인
        if (bottomBar != null && bottomBar.activeSelf)
        {
            bottomBar.SetActive(false);
            if (logDebug)
                Debug.Log("[AttachMemoController] OnEnable - BottomBar를 비활성화합니다.");
        }
        
        // 초기 UI 상태 설정
        SetTextImageVisible(true);
        SetTextMessage(defaultMessage);
        SetTextImageColor(disabledIconColor);
        SetButtonEnabled(false);
        SetIconColor(disabledIconColor);
    }

    private void Update()
    {
        // AttachMemo 컨테이너가 비활성화되어 있으면 처리하지 않음
        if (attachMemoContainer != null && !attachMemoContainer.activeInHierarchy)
            return;

        // 거리 기반 상태 업데이트
        UpdateDistanceState();
        
        // 주기적 디버그 로그 (3초마다)
        if (logDebug)
        {
            debugLogTimer += Time.deltaTime;
            if (debugLogTimer >= DEBUG_LOG_INTERVAL)
            {
                debugLogTimer = 0f;
                Debug.Log($"[AttachMemoController] 상태: {currentState}, 유효한히트: {hasValidHit}, ARRaycastManager: {(arRaycastManager != null ? "있음" : "없음")}");
            }
        }
    }

    /// <summary>
    /// 화면 중앙에서 AR 레이캐스트를 수행하여 거리 기반 상태 업데이트
    /// </summary>
    private void UpdateDistanceState()
    {
        if (arCamera == null)
            return;

        // AR 레이캐스트 매니저가 없으면 (에디터 테스트용) 물리 레이캐스트 사용
        if (arRaycastManager == null)
        {
            UpdateDistanceStateWithPhysicsRaycast();
            return;
        }

        // 화면 중앙 위치 계산
        Vector2 screenCenter = GetScreenCenterPosition();

        // AR 레이캐스트 수행
        TrackableType trackableTypes = TrackableType.PlaneWithinInfinity | TrackableType.FeaturePoint;
        
        if (arRaycastManager.Raycast(screenCenter, arHits, trackableTypes))
        {
            // 히트 성공 - 거리 계산
            Pose hitPose = arHits[0].pose;
            float distance = Vector3.Distance(arCamera.transform.position, hitPose.position);
            
            // 부착 가능한 위치 캐시
            lastHitPosition = hitPose.position;
            lastHitRotation = hitPose.rotation;
            hasValidHit = true;

            if (logDebug)
                Debug.Log($"[AttachMemoController] AR Raycast Hit - Distance: {distance:F2}m, ScreenPos: {screenCenter}");

            // 거리에 따른 상태 결정
            UpdateStateByDistance(distance);
        }
        else
        {
            // 히트 실패 - 기본 상태
            hasValidHit = false;
            SetState(AttachState.TooFar);
            
            if (logDebug)
                Debug.Log($"[AttachMemoController] AR Raycast Failed - No surface detected, ScreenPos: {screenCenter}");
        }
    }

    /// <summary>
    /// centerAttachMemoIcon의 화면 위치를 계산 (Screen Space - Overlay 캔버스 지원)
    /// </summary>
    private Vector2 GetScreenCenterPosition()
    {
        // 기본값: 화면 정중앙
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
        
        if (centerAttachMemoIcon == null)
            return screenCenter;

        // Canvas를 찾아서 렌더 모드 확인
        Canvas canvas = centerAttachMemoIcon.GetComponentInParent<Canvas>();
        if (canvas == null)
            return screenCenter;

        // Screen Space - Overlay 캔버스인 경우
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // RectTransform의 화면 좌표 직접 계산
            // Screen Space - Overlay에서 position.x, position.y가 이미 화면 좌표
            Vector3[] worldCorners = new Vector3[4];
            centerAttachMemoIcon.GetWorldCorners(worldCorners);
            
            // 중심점 계산 (4개 코너의 평균)
            Vector2 center = Vector2.zero;
            for (int i = 0; i < 4; i++)
            {
                center += new Vector2(worldCorners[i].x, worldCorners[i].y);
            }
            center /= 4f;
            
            if (logDebug)
                Debug.Log($"[AttachMemoController] Screen Space Overlay - Icon center: {center}");
            
            return center;
        }
        // Screen Space - Camera 또는 World Space 캔버스인 경우
        else
        {
            Camera renderCamera = canvas.worldCamera != null ? canvas.worldCamera : arCamera;
            if (renderCamera != null)
            {
                screenCenter = RectTransformUtility.WorldToScreenPoint(renderCamera, centerAttachMemoIcon.position);
            }
        }

        return screenCenter;
    }

    /// <summary>
    /// 물리 레이캐스트를 사용한 거리 상태 업데이트 (에디터 테스트용)
    /// </summary>
    private void UpdateDistanceStateWithPhysicsRaycast()
    {
        Vector2 screenCenter = GetScreenCenterPosition();
        Ray ray = arCamera.ScreenPointToRay(screenCenter);
        
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            float distance = hit.distance;
            lastHitPosition = hit.point;
            lastHitRotation = Quaternion.LookRotation(hit.normal);
            hasValidHit = true;

            if (logDebug)
                Debug.Log($"[AttachMemoController] Physics Raycast Hit - Distance: {distance:F2}m, ScreenPos: {screenCenter}");

            UpdateStateByDistance(distance);
        }
        else
        {
            hasValidHit = false;
            SetState(AttachState.TooFar);
            
            if (logDebug)
                Debug.Log($"[AttachMemoController] Physics Raycast Failed, ScreenPos: {screenCenter}");
        }
    }

    /// <summary>
    /// 거리에 따른 상태 업데이트
    /// </summary>
    private void UpdateStateByDistance(float distance)
    {
        if (distance <= attachableDistance)
        {
            SetState(AttachState.Attachable);
        }
        else if (distance <= approachingDistance)
        {
            SetState(AttachState.Approaching);
        }
        else
        {
            SetState(AttachState.TooFar);
        }
    }

    /// <summary>
    /// 상태 변경 및 UI 업데이트
    /// </summary>
    private void SetState(AttachState newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;
        
        if (logDebug)
            Debug.Log($"[AttachMemoController] State changed to: {newState}");

        // UI 업데이트
        switch (newState)
        {
            case AttachState.TooFar:
                // 기본 상태: 텍스트 이미지 표시, 버튼 비활성화
                SetTextImageVisible(true);
                SetTextMessage(defaultMessage);
                SetTextImageColor(disabledIconColor);
                SetButtonEnabled(false);
                SetIconColor(disabledIconColor);
                break;

            case AttachState.Approaching:
                // 가까이 이동 중: 텍스트 색상 변경, 버튼 비활성화
                SetTextImageVisible(true);
                SetTextMessage(approachingMessage);
                SetTextImageColor(approachingColor);
                SetButtonEnabled(false);
                SetIconColor(disabledIconColor);
                break;

            case AttachState.Attachable:
                // 부착 가능: 텍스트 이미지 숨김, 버튼 활성화, 아이콘 흰색
                SetTextImageVisible(false);
                SetButtonEnabled(true);
                SetIconColor(attachableIconColor);
                break;
        }
    }

    /// <summary>
    /// AttachMemoTextImage 표시/숨김
    /// </summary>
    private void SetTextImageVisible(bool visible)
    {
        if (attachMemoTextImage != null)
            attachMemoTextImage.SetActive(visible);
    }

    /// <summary>
    /// 안내 텍스트 메시지 설정
    /// </summary>
    private void SetTextMessage(string message)
    {
        if (attachMemoText != null)
            attachMemoText.text = message;
    }

    /// <summary>
    /// AttachMemoTextImage 배경 색상 설정
    /// </summary>
    private void SetTextImageColor(Color color)
    {
        if (attachMemoTextImage != null)
        {
            Image bgImage = attachMemoTextImage.GetComponent<Image>();
            if (bgImage != null)
            {
                // 알파값 유지하면서 색상만 변경
                Color newColor = color;
                newColor.a = bgImage.color.a;
                bgImage.color = newColor;
            }
        }
    }

    /// <summary>
    /// 버튼 활성화/비활성화
    /// </summary>
    private void SetButtonEnabled(bool isEnabled)
    {
        if (attachMemoBt != null)
            attachMemoBt.interactable = isEnabled;
    }

    /// <summary>
    /// Icon 색상 설정
    /// </summary>
    private void SetIconColor(Color color)
    {
        if (attachMemoIcon != null)
            attachMemoIcon.color = color;
    }

    /// <summary>
    /// 부착 버튼 클릭 시 호출
    /// </summary>
    private void OnAttachButtonClicked()
    {
        if (logDebug)
            Debug.Log("[AttachMemoController] Attach button clicked");

        // 부착 가능 상태가 아니면 무시
        if (currentState != AttachState.Attachable || !hasValidHit)
        {
            if (logDebug)
                Debug.LogWarning("[AttachMemoController] Cannot attach - not in attachable state or no valid hit");
            return;
        }

        // TabPinCreate 필수 확인
        if (tabPinCreate == null)
        {
            Debug.LogWarning("[AttachMemoController] TabPinCreate is not assigned - cannot create pin");
            return;
        }

        // TabPinCreate를 통해 핀 생성 (DB 저장 포함)
        GameObject newPin = tabPinCreate.CreatePinAtPosition(lastHitPosition, lastHitRotation);
        if (logDebug)
            Debug.Log($"[AttachMemoController] Pin created via TabPinCreate: {(newPin != null ? newPin.name : "null")}");
        
        if (newPin != null)
        {
            // 메모 편집 중 숨길 UI들 비활성화
            if (topLeftUI != null)
            {
                topLeftUI.SetActive(false);
                if (logDebug)
                    Debug.Log("[AttachMemoController] TopLeftUI 비활성화");
            }
            
            if (attachMemoContainer != null)
            {
                attachMemoContainer.SetActive(false);
                if (logDebug)
                    Debug.Log("[AttachMemoController] AttachMemoContainer 비활성화");
            }
            
            if (centerAttachMemoIcon != null)
            {
                centerAttachMemoIcon.gameObject.SetActive(false);
                if (logDebug)
                    Debug.Log("[AttachMemoController] AttachMemoIcon 비활성화");
            }

            // BottomBar 활성화
            if (bottomBar != null)
            {
                bottomBar.SetActive(true);
                if (logDebug)
                    Debug.Log("[AttachMemoController] BottomBar activated");
            }

            // MemoUIController를 통해 메모 편집 UI 열기
            if (memoUIController != null)
            {
                memoUIController.OnMemoPlaced(newPin);
                if (logDebug)
                    Debug.Log("[AttachMemoController] MemoUIController.OnMemoPlaced called");
            }
        }
    }

    /// <summary>
    /// 외부에서 AttachMemo UI 활성화
    /// </summary>
    public void ShowAttachMemoUI()
    {
        if (attachMemoContainer != null)
        {
            attachMemoContainer.SetActive(true);
            SetState(AttachState.TooFar);
        }
    }

    /// <summary>
    /// 외부에서 AttachMemo UI 비활성화
    /// </summary>
    public void HideAttachMemoUI()
    {
        if (attachMemoContainer != null)
            attachMemoContainer.SetActive(false);
    }
}
