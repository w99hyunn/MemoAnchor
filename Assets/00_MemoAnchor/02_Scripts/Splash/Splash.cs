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
        private const string INPUT_ERROR_CLASS = "is-error";
        private const int SHAKE_FRAME_COUNT = 12;
        private const float SHAKE_OFFSET = 24f;
        private static readonly Regex EmailRegex = new("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", RegexOptions.Compiled);

        [SerializeField] private string mainScene = "Main";
        [SerializeField] private string serverBaseUrl = "https://localhost:7001";
        [SerializeField] private string kakaoProviderName = "oidc-kakao";
        [SerializeField] private string googleProviderName = "oidc-google";

        private FadeTransition fadeTransition;
        private UIDocument uiDocument;
        private SplashAuthService authService;
        private VisualElement loginPanel;
        private VisualElement signupPanel;
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

        private void Awake()
        {
            TryGetComponent<FadeTransition>(out fadeTransition);
            TryGetComponent<UIDocument>(out uiDocument);
            authService = new SplashAuthService(serverBaseUrl, kakaoProviderName, googleProviderName);
            ConfigureFrameRate();
            BindLoginUi();
            Application.deepLinkActivated += HandleDeepLink;
        }

        private void Start()
        {
            _ = ShowLoginAsync();
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                HandleDeepLink(Application.absoluteURL);
            }
        }

        private void OnDestroy()
        {
            Application.deepLinkActivated -= HandleDeepLink;
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
                SplashAuthCompletion completion = await authService.TryCompleteCachedLoginAsync();
                if (completion.IsExistingMember)
                {
                    SceneManager.LoadScene(mainScene);
                    return true;
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }

            isCompletingLogin = false;
            isLoggingIn = false;
            return false;
        }

        private void BindLoginUi()
        {
            VisualElement root = uiDocument.rootVisualElement;
            loginPanel = root.Q<VisualElement>("login-panel");
            signupPanel = root.Q<VisualElement>("signup-panel");
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
            SetLoginButtonsEnabled(false);
            MemoAnchor.UI.PopupManager.ShowMessage("로그인 진행 중", "브라우저에서 로그인을 완료해주세요.", "확인");
            string sessionId = authService.BeginProviderLogin(provider);
            _ = CompleteLoginSessionAsync(sessionId);
        }

        private void HandleDeepLink(string url)
        {
            string resultId = SplashAuthService.GetResultIdFromDeepLink(url);
            if (string.IsNullOrEmpty(resultId))
            {
                return;
            }

            ShowLoginPanel();
            _ = CompleteLoginAsync(resultId);
        }

        private async Awaitable CompleteLoginAsync(string resultId)
        {
            if (isCompletingLogin)
            {
                return;
            }

            isCompletingLogin = true;
            isLoggingIn = true;
            SetLoginButtonsEnabled(false);
            try
            {
                SplashAuthCompletion completion = await authService.CompleteLoginAsync(resultId);
                await CompleteLoginAsync(completion);
            }
            catch (System.Exception exception)
            {
                RecoverLogin(exception);
            }
        }

        private async Awaitable CompleteLoginSessionAsync(string sessionId)
        {
            if (isCompletingLogin)
            {
                return;
            }

            isCompletingLogin = true;
            try
            {
                SplashAuthCompletion completion = await authService.CompleteLoginSessionAsync(sessionId);
                await CompleteLoginAsync(completion);
            }
            catch (System.Exception exception)
            {
                RecoverLogin(exception);
            }
        }

        private async Awaitable CompleteLoginAsync(SplashAuthCompletion completion)
        {
            if (completion.IsExistingMember)
            {
                await EnterMainSceneAsync();
                return;
            }

            ShowSignupPanel(completion);
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
                SetSignupStatus("회원가입에 실패했습니다. 다시 시도해주세요.");
                SetSignupButtonEnabled(true);
            }
        }

        private void RecoverLogin(System.Exception exception)
        {
            Debug.LogException(exception);
            MemoAnchor.UI.PopupManager.ShowMessage("로그인 실패", "로그인에 실패했습니다. 다시 시도해주세요.", "확인");
            isLoggingIn = false;
            isCompletingLogin = false;
            SetLoginButtonsEnabled(true);
        }

        private void ShowLoginPanel()
        {
            SetVisible(signupPanel, false);
            SetVisible(loginPanel, true);
        }

        private void ShowSignupPanel(SplashAuthCompletion completion)
        {
            SetVisible(loginPanel, false);
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
            inputBox.AddToClassList(INPUT_ERROR_CLASS);
        }

        private static void ClearSignupInputError(VisualElement inputBox)
        {
            inputBox.RemoveFromClassList(INPUT_ERROR_CLASS);
            inputBox.style.translate = new Translate(0, 0, 0);
        }

        private async Awaitable ShakeInvalidSignupInputsAsync()
        {
            List<VisualElement> invalidInputs = new();
            AddInvalidSignupInput(invalidInputs, signupCompanyInputBox);
            AddInvalidSignupInput(invalidInputs, signupNameInputBox);
            AddInvalidSignupInput(invalidInputs, signupEmailInputBox);

            for (int i = 0; i < SHAKE_FRAME_COUNT; i++)
            {
                float offset = i % 2 == 0 ? -SHAKE_OFFSET : SHAKE_OFFSET;
                foreach (VisualElement inputBox in invalidInputs)
                {
                    inputBox.style.translate = new Translate(offset, 0, 0);
                }

                await Awaitable.NextFrameAsync();
            }

            foreach (VisualElement inputBox in invalidInputs)
            {
                inputBox.style.translate = new Translate(0, 0, 0);
            }
        }

        private static void AddInvalidSignupInput(List<VisualElement> invalidInputs, VisualElement inputBox)
        {
            if (inputBox.ClassListContains(INPUT_ERROR_CLASS))
            {
                invalidInputs.Add(inputBox);
            }
        }


        private async Awaitable EnterMainSceneAsync()
        {
            MemoAnchor.UI.PopupManager.HideConfirm();
            SetSignupStatus("메인 화면으로 이동합니다.");
            await fadeTransition.FadeOutAsync();
            SceneManager.LoadScene(mainScene);
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
            // Let mobile use the device refresh rate instead of default low-power cap.
            QualitySettings.vSyncCount = 0;
            int refreshRate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            Application.targetFrameRate = refreshRate > 0 ? refreshRate : 60;
        }
    }
}
