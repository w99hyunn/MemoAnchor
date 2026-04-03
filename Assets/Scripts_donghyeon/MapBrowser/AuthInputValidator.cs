// 로그인/회원가입 입력 검증 스크립트
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AuthInputValidator : MonoBehaviour
{
    [Header("Input Fields")]
    [Tooltip("이메일 입력 필드")]
    [SerializeField] private TMP_InputField emailInput;

    [Tooltip("비밀번호 입력 필드")]
    [SerializeField] private TMP_InputField passwordInput;

    [Tooltip("비밀번호 확인 입력 필드 (회원가입 전용)")]
    [SerializeField] private TMP_InputField passwordConfirmInput;

    [Header("Complete Button")]
    [Tooltip("완료 버튼")]
    [SerializeField] private Button completeButton;

    [Header("Auto Focus")]
    [Tooltip("화면 활성화 시 자동으로 이메일 필드에 포커스 (입력 패널에서만 true로 설정)")]
    [SerializeField] private bool autoFocusOnEnable = false;

    [Tooltip("자동 포커스 지연 시간 (초)")]
    [SerializeField] private float autoFocusDelay = 0.3f;

    private void Awake()
    {
        // 입력 필드 설정 (키보드 Enter를 완료로 사용하지 않음)
        ConfigureInputFields();

        // 입력 변화 감지
        if (emailInput) emailInput.onValueChanged.AddListener(OnInputChanged);
        if (passwordInput) passwordInput.onValueChanged.AddListener(OnInputChanged);
        if (passwordConfirmInput) passwordConfirmInput.onValueChanged.AddListener(OnInputChanged);
    }

    // Unity 2022.3+ Canvas 리빌드 호환성
    private void Start()
    {
        StartCoroutine(ValidateInputsAfterCanvasReady());
    }

    private IEnumerator ValidateInputsAfterCanvasReady()
    {
        // Canvas 리빌드 완전 완료 대기
        yield return null;
        yield return new WaitForEndOfFrame();

        // 초기 상태 확인
        OnInputChanged("");
    }

    private void ConfigureInputFields()
    {
        // 입력 필드를 Single Line으로 설정하여 키보드 Enter 동작 방지
        if (emailInput != null)
        {
            emailInput.lineType = TMP_InputField.LineType.SingleLine;
        }
        if (passwordInput != null)
        {
            passwordInput.lineType = TMP_InputField.LineType.SingleLine;
        }
        if (passwordConfirmInput != null)
        {
            passwordConfirmInput.lineType = TMP_InputField.LineType.SingleLine;
        }
    }

    private void OnEnable()
    {
        // 패널이 활성화될 때 자동으로 이메일 필드에 포커스 (입력 필드가 있을 때만)
        if (autoFocusOnEnable && emailInput != null && emailInput.gameObject.activeInHierarchy)
        {
            // 페이드 효과 완료 후 포커스하도록 약간 지연
            StartCoroutine(DelayedFocus());
        }
    }

    private void OnDisable()
    {
        // 패널이 비활성화될 때 입력 필드 포커스 해제
        if (emailInput != null && emailInput.isFocused)
        {
            emailInput.DeactivateInputField();
        }
        if (passwordInput != null && passwordInput.isFocused)
        {
            passwordInput.DeactivateInputField();
        }
        if (passwordConfirmInput != null && passwordConfirmInput.isFocused)
        {
            passwordConfirmInput.DeactivateInputField();
        }
    }

    private IEnumerator DelayedFocus()
    {
        yield return new WaitForSeconds(autoFocusDelay);

        // 여전히 활성화되어 있고 입력 필드가 유효한지 확인
        if (emailInput != null && emailInput.gameObject.activeInHierarchy && gameObject.activeInHierarchy)
        {
            emailInput.Select();
            emailInput.ActivateInputField();
        }
    }

    private void OnDestroy()
    {
        // 모든 코루틴 정지
        StopAllCoroutines();

        // 이벤트 리스너 제거
        if (emailInput)
            emailInput.onValueChanged.RemoveListener(OnInputChanged);
        if (passwordInput)
            passwordInput.onValueChanged.RemoveListener(OnInputChanged);
        if (passwordConfirmInput)
            passwordConfirmInput.onValueChanged.RemoveListener(OnInputChanged);
    }

    private void OnInputChanged(string value)
    {
        bool isValid = ValidateInputs();

        if (completeButton)
            completeButton.interactable = isValid;
    }

    private bool ValidateInputs()
    {
        // 이메일과 비밀번호는 필수
        bool hasEmail = emailInput != null && !string.IsNullOrWhiteSpace(emailInput.text);
        bool hasPassword = passwordInput != null && !string.IsNullOrWhiteSpace(passwordInput.text);

        // 회원가입인 경우 비밀번호 확인도 체크
        bool hasPasswordConfirm = true;
        if (passwordConfirmInput != null && passwordConfirmInput.gameObject.activeInHierarchy)
        {
            hasPasswordConfirm = !string.IsNullOrWhiteSpace(passwordConfirmInput.text);
        }

        return hasEmail && hasPassword && hasPasswordConfirm;
    }
}