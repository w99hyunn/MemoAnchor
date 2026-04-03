
// 로그인/회원가입 화면 전환을 관리하는 스크립트
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AuthFlowController : MonoBehaviour
{
    [Header("Canvas Groups")]
    [Tooltip("전체 인증 화면 Canvas")]
    [SerializeField] private Canvas authCanvas;

    [Tooltip("홈 화면 Canvas")]
    [SerializeField] private Canvas homeCanvas;

    [Tooltip("홈 화면 전환 시 흰색 페이드 효과")]
    [SerializeField] private Image whiteTransitionPanel;

    [Header("Panels")]
    [Tooltip("초기 로그인 메인 화면")]
    [SerializeField] private GameObject loginMainPanel;
    [SerializeField] private CanvasGroup loginMainCanvasGroup;

    [Tooltip("로그인 입력 화면")]
    [SerializeField] private GameObject loginInputPanel;
    [SerializeField] private CanvasGroup loginInputCanvasGroup;

    [Tooltip("회원가입 입력 화면")]
    [SerializeField] private GameObject signupInputPanel;
    [SerializeField] private CanvasGroup signupInputCanvasGroup;

    [Header("Buttons - Login Main")]
    [SerializeField] private Button loginButton;
    [SerializeField] private Button signupButton;

    [Header("Buttons - Login Input")]
    [SerializeField] private Button loginBackButton;
    [SerializeField] private Button loginCompleteButton;

    [Header("Buttons - Signup Input")]
    [SerializeField] private Button signupBackButton;
    [SerializeField] private Button signupCompleteButton;

    [Header("Fade Settings")]
    [Tooltip("패널 전환 시 페이드 효과 사용")]
    [SerializeField] private bool useFadeEffect = true;

    [Tooltip("페이드 인/아웃 시간")]
    [SerializeField] private float fadeDuration = 0.3f;

    [Tooltip("홈 화면 전환 시 흰색 페이드 시간")]
    [SerializeField] private float homeTransitionDuration = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private Coroutine currentFadeCoroutine;

    // Unity 에디터에서 Inspector 값이 변경될 때 호출
    private void OnValidate()
    {
        // 에디터 모드에서만 실행
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // CanvasGroup이 이미 연결되어 있다면 alpha 확인 및 수정
            if (loginMainCanvasGroup != null && loginMainCanvasGroup.alpha < 0.99f)
            {
                loginMainCanvasGroup.alpha = 1f;
                Debug.Log($"[AuthFlow] LoginMainPanel CanvasGroup alpha를 1로 수정했습니다.");
            }
            if (loginInputCanvasGroup != null && loginInputCanvasGroup.alpha < 0.99f)
            {
                loginInputCanvasGroup.alpha = 1f;
                Debug.Log($"[AuthFlow] LoginInputPanel CanvasGroup alpha를 1로 수정했습니다.");
            }
            if (signupInputCanvasGroup != null && signupInputCanvasGroup.alpha < 0.99f)
            {
                signupInputCanvasGroup.alpha = 1f;
                Debug.Log($"[AuthFlow] SignupInputPanel CanvasGroup alpha를 1로 수정했습니다.");
            }
        }
#endif
    }

    private void Awake()
    {
        // CanvasGroup 자동 찾기 (연결되지 않은 경우)
        if (loginMainPanel && loginMainCanvasGroup == null)
            loginMainCanvasGroup = GetOrAddCanvasGroup(loginMainPanel);
        if (loginInputPanel && loginInputCanvasGroup == null)
            loginInputCanvasGroup = GetOrAddCanvasGroup(loginInputPanel);
        if (signupInputPanel && signupInputCanvasGroup == null)
            signupInputCanvasGroup = GetOrAddCanvasGroup(signupInputPanel);

        // LoginMainPanel이 Splash보다 먼저 보이지 않도록 즉시 투명하게 설정
        if (loginMainCanvasGroup != null)
        {
            loginMainCanvasGroup.alpha = 0f;
            loginMainCanvasGroup.interactable = false;
            loginMainCanvasGroup.blocksRaycasts = false;
        }

        // 흰색 전환 패널 자동 생성
        if (whiteTransitionPanel == null && authCanvas != null)
        {
            CreateWhiteTransitionPanel();
        }

        // 버튼 이벤트 연결
        RegisterButtonEvents();

        if (showDebugLogs)
            Debug.Log("[AuthFlow] Awake 완료");
    }

    private void Start()
    {
        // Canvas 리빌드가 안전하게 완료된 후 초기화
        StartCoroutine(InitializeAfterCanvasReady());
    }

    private IEnumerator InitializeAfterCanvasReady()
    {
        // Canvas 리빌드 완전 완료 대기 (여러 프레임)
        yield return null; // 첫 번째 프레임
        yield return new WaitForEndOfFrame(); // Canvas.SendWillRenderCanvases 완료 대기

        // AuthCanvas가 비활성화되어 있으면 초기화하지 않음
        if (authCanvas != null && !authCanvas.gameObject.activeInHierarchy)
        {
            if (showDebugLogs)
                Debug.Log("[AuthFlow] AuthCanvas가 비활성화되어 있어 초기화를 건너뜁니다.");
            yield break;
        }

        // 흰색 전환 패널 명시적으로 투명하게 초기화 및 비활성화
        if (whiteTransitionPanel != null)
        {
            whiteTransitionPanel.color = new Color(1f, 1f, 1f, 0f);
            whiteTransitionPanel.raycastTarget = false;
            whiteTransitionPanel.gameObject.SetActive(false);
        }

        // 초기 상태: authCanvas는 활성화 상태 유지, 홈 화면은 숨김
        if (authCanvas) authCanvas.gameObject.SetActive(true);
        if (homeCanvas) homeCanvas.gameObject.SetActive(false);

        // 모든 패널 비활성화
        if (loginMainPanel) loginMainPanel.SetActive(false);
        if (loginInputPanel) loginInputPanel.SetActive(false);
        if (signupInputPanel) signupInputPanel.SetActive(false);

        // LoginMainPanel을 활성화하되, SplashScreen이 끝날 때까지 투명하게 설정
        if (loginMainPanel)
        {
            loginMainPanel.SetActive(true);
            if (loginMainCanvasGroup != null)
            {
                loginMainCanvasGroup.alpha = 0f; // 투명하게 시작
                loginMainCanvasGroup.interactable = false;
                loginMainCanvasGroup.blocksRaycasts = false;
            }
        }

        // 다른 CanvasGroup 초기화
        if (loginInputCanvasGroup != null)
        {
            loginInputCanvasGroup.alpha = 1f;
            loginInputCanvasGroup.interactable = true;
            loginInputCanvasGroup.blocksRaycasts = true;
        }
        if (signupInputCanvasGroup != null)
        {
            signupInputCanvasGroup.alpha = 1f;
            signupInputCanvasGroup.interactable = true;
            signupInputCanvasGroup.blocksRaycasts = true;
        }

        // 모든 입력 필드 비활성화 및 EventSystem 선택 해제
        DeactivateAllInputFields();
        ClearEventSystemSelection();

        if (showDebugLogs)
            Debug.Log("[AuthFlow] 초기화 완료 - LoginMainPanel 투명 상태로 준비됨");
    }

    /// <summary>
    /// SplashScreen이 끝난 후 호출되는 공개 메서드
    /// LoginMainPanel을 표시합니다
    /// </summary>
    public void ShowLoginMainAfterSplash()
    {
        if (showDebugLogs)
            Debug.Log("[AuthFlow] ShowLoginMainAfterSplash 호출됨");

        StartCoroutine(FadeInLoginMain());
    }

    private IEnumerator FadeInLoginMain()
    {
        // 한 프레임 대기 (모든 UI 초기화 완료 대기)
        yield return null;

        // LoginMainPanel 페이드인
        if (loginMainPanel != null && loginMainCanvasGroup != null)
        {
            loginMainPanel.SetActive(true);

            float elapsed = 0f;
            float duration = 0.3f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                loginMainCanvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }

            loginMainCanvasGroup.alpha = 1f;
            loginMainCanvasGroup.interactable = true;
            loginMainCanvasGroup.blocksRaycasts = true;

            if (showDebugLogs)
                Debug.Log("[AuthFlow] LoginMainPanel 페이드인 완료");
        }

        // 입력 필드 및 키보드 정리
        DeactivateAllInputFields();
        ClearEventSystemSelection();
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject obj)
    {
        CanvasGroup cg = obj.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = obj.AddComponent<CanvasGroup>();
            // 생성 시 기본값 설정
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;

            if (showDebugLogs)
                Debug.Log($"[AuthFlow] CanvasGroup 생성 및 초기화: {obj.name}");
        }
        return cg;
    }

    private void CreateWhiteTransitionPanel()
    {
        // 흰색 전환 패널 생성
        GameObject panelObj = new GameObject("WhiteTransitionPanel");
        panelObj.transform.SetParent(authCanvas.transform, false);

        whiteTransitionPanel = panelObj.AddComponent<Image>();
        whiteTransitionPanel.color = new Color(1f, 1f, 1f, 0f); // 초기 투명
        whiteTransitionPanel.raycastTarget = false; // 입력 차단 방지

        // 전체 화면 크기로 설정
        RectTransform rt = whiteTransitionPanel.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // 맨 위에 표시
        panelObj.transform.SetAsLastSibling();
    }

    private void RegisterButtonEvents()
    {
        // 메인 화면 버튼
        if (loginButton) loginButton.onClick.AddListener(OnLoginButtonClicked);
        if (signupButton) signupButton.onClick.AddListener(OnSignupButtonClicked);

        // 로그인 입력 화면 버튼
        if (loginBackButton) loginBackButton.onClick.AddListener(OnLoginBackClicked);
        if (loginCompleteButton) loginCompleteButton.onClick.AddListener(OnLoginComplete);

        // 회원가입 입력 화면 버튼
        if (signupBackButton) signupBackButton.onClick.AddListener(OnSignupBackClicked);
        if (signupCompleteButton) signupCompleteButton.onClick.AddListener(OnSignupComplete);
    }

    private void OnDestroy()
    {
        // 메모리 누수 방지
        if (loginButton) loginButton.onClick.RemoveListener(OnLoginButtonClicked);
        if (signupButton) signupButton.onClick.RemoveListener(OnSignupButtonClicked);
        if (loginBackButton) loginBackButton.onClick.RemoveListener(OnLoginBackClicked);
        if (loginCompleteButton) loginCompleteButton.onClick.RemoveListener(OnLoginComplete);
        if (signupBackButton) signupBackButton.onClick.RemoveListener(OnSignupBackClicked);
        if (signupCompleteButton) signupCompleteButton.onClick.RemoveListener(OnSignupComplete);
    }

    // === 화면 전환 함수들 ===

    private void ShowLoginMain()
    {
        if (showDebugLogs) Debug.Log("[AuthFlow] 로그인 메인 화면 표시");

        // 흰색 전환 패널 비활성화
        if (whiteTransitionPanel != null)
        {
            whiteTransitionPanel.color = new Color(1f, 1f, 1f, 0f);
            whiteTransitionPanel.raycastTarget = false;
            whiteTransitionPanel.gameObject.SetActive(false);
        }

        // 모든 입력 필드 포커스 해제 및 키보드 숨김
        DeactivateAllInputFields();
        ClearEventSystemSelection();

        if (useFadeEffect)
        {
            if (currentFadeCoroutine != null) StopCoroutine(currentFadeCoroutine);
            currentFadeCoroutine = StartCoroutine(FadeToPanel(loginMainPanel, loginMainCanvasGroup));
        }
        else
        {
            // 페이드 효과를 사용하지 않는 경우 alpha를 명시적으로 1로 설정
            if (loginMainCanvasGroup != null)
            {
                loginMainCanvasGroup.alpha = 1f;
                loginMainCanvasGroup.interactable = true;
                loginMainCanvasGroup.blocksRaycasts = true;
            }
            if (loginMainPanel) loginMainPanel.SetActive(true);
            if (loginInputPanel) loginInputPanel.SetActive(false);
            if (signupInputPanel) signupInputPanel.SetActive(false);
        }
    }

    private void ShowLoginInput()
    {
        if (showDebugLogs) Debug.Log("[AuthFlow] 로그인 입력 화면 표시");

        if (useFadeEffect)
        {
            if (currentFadeCoroutine != null) StopCoroutine(currentFadeCoroutine);
            currentFadeCoroutine = StartCoroutine(FadeToPanel(loginInputPanel, loginInputCanvasGroup));
        }
        else
        {
            // 페이드 효과를 사용하지 않는 경우 alpha를 명시적으로 1로 설정
            if (loginInputCanvasGroup != null)
            {
                loginInputCanvasGroup.alpha = 1f;
                loginInputCanvasGroup.interactable = true;
                loginInputCanvasGroup.blocksRaycasts = true;
            }
            if (loginMainPanel) loginMainPanel.SetActive(false);
            if (loginInputPanel) loginInputPanel.SetActive(true);
            if (signupInputPanel) signupInputPanel.SetActive(false);
        }
    }

    private void ShowSignupInput()
    {
        if (showDebugLogs) Debug.Log("[AuthFlow] 회원가입 입력 화면 표시");

        if (useFadeEffect)
        {
            if (currentFadeCoroutine != null) StopCoroutine(currentFadeCoroutine);
            currentFadeCoroutine = StartCoroutine(FadeToPanel(signupInputPanel, signupInputCanvasGroup));
        }
        else
        {
            // 페이드 효과를 사용하지 않는 경우 alpha를 명시적으로 1로 설정
            if (signupInputCanvasGroup != null)
            {
                signupInputCanvasGroup.alpha = 1f;
                signupInputCanvasGroup.interactable = true;
                signupInputCanvasGroup.blocksRaycasts = true;
            }
            if (loginMainPanel) loginMainPanel.SetActive(false);
            if (loginInputPanel) loginInputPanel.SetActive(false);
            if (signupInputPanel) signupInputPanel.SetActive(true);
        }
    }

    private void ShowHome()
    {
        if (showDebugLogs) Debug.Log("[AuthFlow] 홈 화면으로 이동");

        // 흰색 페이드 효과와 함께 홈 화면으로 전환
        StartCoroutine(TransitionToHome());
    }

    private IEnumerator TransitionToHome()
    {
        // 1단계: 흰색으로 페이드아웃
        if (whiteTransitionPanel != null)
        {
            whiteTransitionPanel.gameObject.SetActive(true); // GameObject 활성화
            whiteTransitionPanel.raycastTarget = true; // 전환 중 입력 차단
            whiteTransitionPanel.color = new Color(1f, 1f, 1f, 0f); // 시작은 투명

            float elapsed = 0f;
            while (elapsed < homeTransitionDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / homeTransitionDuration);
                whiteTransitionPanel.color = new Color(1f, 1f, 1f, alpha);
                yield return null;
            }
            whiteTransitionPanel.color = Color.white; // 완전히 불투명
        }

        // 2단계: 화면 전환
        if (authCanvas) authCanvas.gameObject.SetActive(false);
        if (homeCanvas) homeCanvas.gameObject.SetActive(true);

        // 3단계: 페이드인 (흰색에서 투명으로)
        if (whiteTransitionPanel != null)
        {
            float elapsed = 0f;
            while (elapsed < homeTransitionDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / homeTransitionDuration);
                whiteTransitionPanel.color = new Color(1f, 1f, 1f, alpha);
                yield return null;
            }
            whiteTransitionPanel.color = new Color(1f, 1f, 1f, 0f); // 완전히 투명
            whiteTransitionPanel.raycastTarget = false; // 전환 완료 후 입력 허용
            whiteTransitionPanel.gameObject.SetActive(false); // GameObject 비활성화
        }
    }

    // === 페이드 효과 함수 ===

    private IEnumerator FadeToPanel(GameObject targetPanel, CanvasGroup targetCanvasGroup)
    {
        // 1단계: 현재 활성화된 패널 페이드아웃
        CanvasGroup currentCanvasGroup = null;

        if (loginMainPanel.activeSelf && loginMainPanel != targetPanel)
            currentCanvasGroup = loginMainCanvasGroup;
        else if (loginInputPanel.activeSelf && loginInputPanel != targetPanel)
            currentCanvasGroup = loginInputCanvasGroup;
        else if (signupInputPanel.activeSelf && signupInputPanel != targetPanel)
            currentCanvasGroup = signupInputCanvasGroup;

        if (currentCanvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                currentCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }
            currentCanvasGroup.alpha = 0f;
        }

        // 모든 패널 비활성화
        if (loginMainPanel) loginMainPanel.SetActive(false);
        if (loginInputPanel) loginInputPanel.SetActive(false);
        if (signupInputPanel) signupInputPanel.SetActive(false);

        // 2단계: 타겟 패널 활성화 및 페이드인
        if (targetPanel && targetCanvasGroup)
        {
            targetPanel.SetActive(true);
            targetCanvasGroup.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                targetCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }
            targetCanvasGroup.alpha = 1f;
        }

        // LoginMainPanel로 전환된 경우 키보드 다시 한번 확인 및 숨김
        if (targetPanel == loginMainPanel)
        {
            yield return new WaitForEndOfFrame(); // 한 프레임 대기

            // 흰색 전환 패널 비활성화
            if (whiteTransitionPanel != null)
            {
                whiteTransitionPanel.color = new Color(1f, 1f, 1f, 0f);
                whiteTransitionPanel.raycastTarget = false;
                whiteTransitionPanel.gameObject.SetActive(false);
            }

            DeactivateAllInputFields();
            ClearEventSystemSelection();
        }
    }

    // === 유틸리티 함수들 ===

    /// <summary>
    /// 모든 입력 필드의 포커스를 해제하고 키보드를 숨김
    /// </summary>
    private void DeactivateAllInputFields()
    {
        // 모든 TMP_InputField 찾기
        TMP_InputField[] allInputFields = authCanvas.GetComponentsInChildren<TMP_InputField>(true);

        foreach (var inputField in allInputFields)
        {
            if (inputField != null && inputField.isFocused)
            {
                inputField.DeactivateInputField();
                if (showDebugLogs)
                    Debug.Log($"[AuthFlow] 입력 필드 비활성화: {inputField.name}");
            }
        }

        // 모바일 키보드 강제 숨김
#if UNITY_ANDROID || UNITY_IOS
        if (TouchScreenKeyboard.visible)
        {
            TouchScreenKeyboard.hideInput = true;
            if (showDebugLogs)
                Debug.Log("[AuthFlow] 모바일 키보드 숨김");
        }
#endif
    }

    /// <summary>
    /// EventSystem의 현재 선택 해제
    /// </summary>
    private void ClearEventSystemSelection()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            eventSystem.SetSelectedGameObject(null);
            if (showDebugLogs)
                Debug.Log("[AuthFlow] EventSystem 선택 해제");
        }
    }

    // === 버튼 클릭 이벤트 핸들러들 ===

    private void OnLoginButtonClicked()
    {
        ShowLoginInput();
    }

    private void OnSignupButtonClicked()
    {
        ShowSignupInput();
    }

    private void OnLoginBackClicked()
    {
        ShowLoginMain();
    }

    private void OnSignupBackClicked()
    {
        ShowLoginMain();
    }

    private void OnLoginComplete()
    {
        if (showDebugLogs) Debug.Log("[AuthFlow] 로그인 완료");

        // 실제 인증은 없고 바로 홈으로 이동
        ShowHome();
    }

    private void OnSignupComplete()
    {
        if (showDebugLogs) Debug.Log("[AuthFlow] 회원가입 완료");

        // 실제 인증은 없고 바로 홈으로 이동
        ShowHome();
    }
}