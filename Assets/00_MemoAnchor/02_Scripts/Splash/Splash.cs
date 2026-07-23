using System.Collections.Generic;
using System.Text.RegularExpressions;
using MemoAnchor.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MemoAnchor
{
    public class Splash : MonoBehaviour
    {
        private const string HIDDEN_CLASS = "is-hidden";
        private static readonly Regex EmailRegex = new("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", RegexOptions.Compiled);
        private static readonly HashSet<string> HandledLoginResultIds = new();

        [SerializeField] private string mainScene = "Main";

        private FadeTransition fadeTransition;
        private UIDocument uiDocument;
        private AuthService authService;
        private VisualElement loginPanel;
        private VisualElement signupPanel;
        private VisualElement loginLoadingOverlay;
        private VisualElement loginLoadingSpinner;
        private VisualElement signupCompanyInputBox;
        private VisualElement signupNameInputBox;
        private VisualElement signupEmailInputBox;
        private TextField signupCompanyInput;
        private TextField signupNameInput;
        private TextField signupEmailInput;
        private Button kakaoLoginButton;
        private Button googleLoginButton;
        private Button signupSubmitButton;
        private Label signupStatusLabel;
        private bool isLoggingIn;
        private bool isCompletingLogin;
        private bool isWaitingExternalLogin;
        private int loginAttemptToken;

        private void Awake()
        {
            TryGetComponent<FadeTransition>(out fadeTransition);
            TryGetComponent<UIDocument>(out uiDocument);
            authService = new AuthService();
            ConfigureFrameRate();
            BindLoginUi();
            Application.deepLinkActivated += HandleDeepLink;
        }

        private void Start()
        {
            if (!TryHandleDeepLink(Application.absoluteURL))
            {
                _ = ShowLoginAsync();
            }
        }

        private void OnDestroy()
        {
            Application.deepLinkActivated -= HandleDeepLink;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                return;
            }

            if (isLoggingIn && isWaitingExternalLogin)
            {
                isWaitingExternalLogin = false;
                SetLoginCompletionLoading(true);
            }
        }

        private async Awaitable ShowLoginAsync()
        {
            if (isCompletingLogin)
            {
                return;
            }

            if (await TryEnterMainSceneWithCachedLoginAsync())
            {
                return;
            }

            await Awaitable.NextFrameAsync();
            if (isCompletingLogin)
            {
                return;
            }

            ShowLoginPanel();
        }

        private async Awaitable<bool> TryEnterMainSceneWithCachedLoginAsync()
        {
            isCompletingLogin = true;
            try
            {
                AuthCompletion completion = await authService.TryCompleteCachedLoginAsync();
                if (completion.IsExistingMember)
                {
                    await EnterMainSceneAsync();
                    return true;
                }
            }
            catch (System.Exception exception)
            {
                if (IsServerConnectionException(exception))
                {
                    ShowServerConnectionFailedPopup();
                }
                else
                {
                    Debug.LogException(exception);
                }
            }

            isCompletingLogin = false;
            isLoggingIn = false;
            SetLoginCompletionLoading(false);
            return false;
        }

        private void BindLoginUi()
        {
            VisualElement root = uiDocument.rootVisualElement;
            loginPanel = root.Q<VisualElement>("login-panel");
            signupPanel = root.Q<VisualElement>("signup-panel");
            loginLoadingOverlay = root.Q<VisualElement>("splash-login-loading-overlay");
            loginLoadingSpinner = root.Q<VisualElement>("splash-login-loading-spinner");
            signupCompanyInput = root.Q<TextField>("signup-company-input");
            signupNameInput = root.Q<TextField>("signup-name-input");
            signupEmailInput = root.Q<TextField>("signup-email-input");
            signupCompanyInputBox = root.Q<VisualElement>("signup-company-input-box");
            signupNameInputBox = root.Q<VisualElement>("signup-name-input-box");
            signupEmailInputBox = root.Q<VisualElement>("signup-email-input-box");
            kakaoLoginButton = root.Q<Button>("kakao-login-button");
            googleLoginButton = root.Q<Button>("google-login-button");
            signupSubmitButton = root.Q<Button>("signup-submit-button");
            signupStatusLabel = root.Q<Label>("signup-status-label");

            PrepareRuntimeVisibility();
            kakaoLoginButton.clicked += () => BeginProviderLogin("kakao");
            googleLoginButton.clicked += () => BeginProviderLogin("google");
            signupSubmitButton.clicked += () => _ = SubmitSignupAsync();
            signupCompanyInput.RegisterValueChangedCallback(_ => ClearSignupInputError(signupCompanyInputBox));
            signupNameInput.RegisterValueChangedCallback(_ => ClearSignupInputError(signupNameInputBox));
            signupEmailInput.RegisterValueChangedCallback(_ => ClearSignupInputError(signupEmailInputBox));
        }

        private void PrepareRuntimeVisibility()
        {
            SetVisible(loginPanel, false);
            SetVisible(signupPanel, false);
            SetVisible(loginLoadingOverlay, false);
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            element.EnableInClassList(HIDDEN_CLASS, !visible);
        }

        private void BeginProviderLogin(string provider)
        {
            if (isLoggingIn)
            {
                return;
            }

            isLoggingIn = true;
            isWaitingExternalLogin = true;
            loginAttemptToken++;
            SetLoginButtonsEnabled(false);
            string sessionId = authService.BeginProviderLogin(provider);
            _ = CompleteLoginSessionAsync(sessionId, loginAttemptToken);
        }

        private void HandleDeepLink(string url)
        {
            TryHandleDeepLink(url);
        }

        private bool TryHandleDeepLink(string url)
        {
            string resultId = AuthService.GetResultIdFromDeepLink(url);
            if (string.IsNullOrEmpty(resultId))
            {
                return false;
            }

            if (!HandledLoginResultIds.Add(resultId))
            {
                return false;
            }

            isWaitingExternalLogin = false;
            SetLoginCompletionLoading(true);
            ShowLoginPanel();
            _ = CompleteLoginAsync(resultId);
            return true;
        }

        private async Awaitable CompleteLoginAsync(string resultId)
        {
            if (isCompletingLogin)
            {
                return;
            }

            isCompletingLogin = true;
            isLoggingIn = true;
            isWaitingExternalLogin = false;
            SetLoginButtonsEnabled(false);
            SetLoginCompletionLoading(true);
            try
            {
                AuthCompletion completion = await authService.CompleteLoginAsync(resultId);
                await CompleteLoginAsync(completion);
            }
            catch (System.Exception exception)
            {
                RecoverLogin(exception);
            }
        }

        private async Awaitable CompleteLoginSessionAsync(string sessionId, int token)
        {
            if (isCompletingLogin)
            {
                return;
            }

            isCompletingLogin = true;
            try
            {
                AuthCompletion completion = await authService.CompleteLoginSessionAsync(sessionId);
                if (token != loginAttemptToken)
                {
                    return;
                }

                isWaitingExternalLogin = false;
                SetLoginCompletionLoading(true);
                await CompleteLoginAsync(completion);
            }
            catch (System.Exception exception)
            {
                if (token != loginAttemptToken)
                {
                    return;
                }

                RecoverLogin(exception);
            }
        }

        private async Awaitable CompleteLoginAsync(AuthCompletion completion)
        {
            if (completion.IsExistingMember)
            {
                CompleteLoginState();
                await EnterMainSceneAsync();
                return;
            }

            CompleteLoginState();
            ShowSignupPanel(completion);
        }

        private void CompleteLoginState()
        {
            isLoggingIn = false;
            isCompletingLogin = false;
            isWaitingExternalLogin = false;
            loginAttemptToken++;
        }

        private async Awaitable SubmitSignupAsync()
        {
            SetSignupButtonEnabled(false);
            if (!ValidateSignupInputs())
            {
                SetSignupStatus("필수 정보를 입력하고 이메일 형식을 확인해주세요.");
                SetSignupButtonEnabled(true);
                return;
            }

            try
            {
                SetSignupStatus("회원 정보를 저장하는 중입니다.");
                await authService.SaveSignupProfileAsync(
                    signupNameInput.value.Trim(),
                    signupEmailInput.value.Trim(),
                    signupCompanyInput.value.Trim());
                await EnterMainSceneAsync();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                SetLoginCompletionLoading(false);
                SetSignupStatus("회원가입에 실패했습니다. 다시 시도해주세요.");
                SetSignupButtonEnabled(true);
            }
        }

        private void RecoverLogin(System.Exception exception)
        {
            if (IsServerConnectionException(exception))
            {
                ShowServerConnectionFailedPopup();
            }
            else
            {
                Debug.LogException(exception);
                MemoAnchor.UI.PopupManager.ShowMessage("로그인 실패", "로그인에 실패했습니다. 다시 시도해주세요.", "확인");
            }

            isLoggingIn = false;
            isCompletingLogin = false;
            isWaitingExternalLogin = false;
            loginAttemptToken++;
            SetLoginCompletionLoading(false);
            SetLoginButtonsEnabled(true);
        }

        private static bool IsServerConnectionException(System.Exception exception)
        {
            return exception is System.InvalidOperationException
                && exception.Message == "Cannot connect to destination host";
        }

        private static void ShowServerConnectionFailedPopup()
        {
            MemoAnchor.UI.PopupManager.ShowMessage("서버 연결 실패", "로그인 서버에 연결할 수 없습니다. 서버 상태를 확인해주세요.", "확인");
        }

        private void ShowLoginPanel()
        {
            SetVisible(signupPanel, false);
            SetVisible(loginPanel, true);
        }

        private void ShowSignupPanel(AuthCompletion completion)
        {
            SetVisible(loginPanel, false);
            SetLoginCompletionLoading(false);
            MemoAnchor.UI.PopupManager.HideConfirm();
            SetVisible(signupPanel, true);
            signupNameInput.value = completion.Profile.Name;
            signupEmailInput.value = completion.Profile.Email;
            SetSignupButtonEnabled(true);
            SetSignupStatus("처음 로그인했습니다. 회원가입을 완료해주세요.");
        }

        private bool ValidateSignupInputs()
        {
            ClearSignupInputError(signupCompanyInputBox);
            ClearSignupInputError(signupNameInputBox);
            ClearSignupInputError(signupEmailInputBox);

            bool isValid = true;
            if (string.IsNullOrWhiteSpace(signupCompanyInput.value))
            {
                SetSignupInputError(signupCompanyInputBox);
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(signupNameInput.value))
            {
                SetSignupInputError(signupNameInputBox);
                isValid = false;
            }

            if (!IsValidEmail(signupEmailInput.value))
            {
                SetSignupInputError(signupEmailInputBox);
                isValid = false;
            }

            if (!isValid)
            {
                _ = ShakeInvalidSignupInputsAsync();
            }

            return isValid;
        }

        private static bool IsValidEmail(string email)
        {
            return !string.IsNullOrWhiteSpace(email) && EmailRegex.IsMatch(email.Trim());
        }

        private static void SetSignupInputError(VisualElement inputBox)
        {
            InputValidationFeedback.ShowError(inputBox);
        }

        private static void ClearSignupInputError(VisualElement inputBox)
        {
            InputValidationFeedback.ClearError(inputBox);
        }

        private async Awaitable ShakeInvalidSignupInputsAsync()
        {
            List<VisualElement> invalidInputs = new();
            AddInvalidSignupInput(invalidInputs, signupCompanyInputBox);
            AddInvalidSignupInput(invalidInputs, signupNameInputBox);
            AddInvalidSignupInput(invalidInputs, signupEmailInputBox);
            await InputValidationFeedback.ShakeAsync(invalidInputs);
        }

        private static void AddInvalidSignupInput(List<VisualElement> invalidInputs, VisualElement inputBox)
        {
            InputValidationFeedback.AddIfError(invalidInputs, inputBox);
        }


        private async Awaitable EnterMainSceneAsync()
        {
            SetLoginCompletionLoading(true);
            MemoAnchor.UI.PopupManager.HideConfirm();
            SetSignupStatus("메인 화면을 불러오는 중입니다.");
            await MainInitialData.PreloadAsync();
            await fadeTransition.FadeOutAsync();
            SetLoginCompletionLoading(false);
            SceneManager.LoadScene(mainScene);
        }

        private void SetLoginCompletionLoading(bool isLoading)
        {
            if (isLoading)
            {
                LoadingSpinnerController.ShowOverlay(loginLoadingOverlay, loginLoadingSpinner);
                return;
            }

            LoadingSpinnerController.HideOverlay(loginLoadingOverlay, loginLoadingSpinner);
        }

        private void SetLoginButtonsEnabled(bool enabled)
        {
            kakaoLoginButton.SetEnabled(enabled);
            googleLoginButton.SetEnabled(enabled);
        }

        private void SetSignupButtonEnabled(bool enabled)
        {
            signupSubmitButton.SetEnabled(enabled);
        }

        private void SetSignupStatus(string message)
        {
            signupStatusLabel.text = message;
        }

        private static void ConfigureFrameRate()
        {
            QualitySettings.vSyncCount = 0;
            int refreshRate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            Application.targetFrameRate = refreshRate > 0 ? refreshRate : 60;
        }
    }
}
