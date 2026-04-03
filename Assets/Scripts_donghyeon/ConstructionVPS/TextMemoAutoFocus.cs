
// 패널이 켜질 때 키보드 활성화 및 텍스트 입력 필드에 포커스 주기
using System.Collections;
using UnityEngine;
using TMPro;

public class TextMemoAutoFocus : MonoBehaviour
{
    [Header("Text Memo Input Field")]
    [Tooltip("포커스를 주고자 하는 TMP_InputField를 넣는 자리")]
    [SerializeField] private TMP_InputField inputField;

    // 패널이 활성화될 때 자동 호출되는 함수 (Unity 2022.3+ Canvas 리빌드 호환성)
    private void OnEnable()
    {
        StartCoroutine(FocusNextFrame());
    }

    // 포커스 실행 함수
    private IEnumerator FocusNextFrame()
    {
        // Canvas 리빌드 완전 완료 대기
        yield return null;
        yield return new WaitForEndOfFrame();

        if (!inputField) yield break;

        // 입력 필드 활성화
        inputField.interactable = true;

        // 포커스 + 키보드 활성화
        inputField.Select();
        inputField.ActivateInputField();

        // 그래도 키보드가 안 뜨는 기기 강제로 띄우기
        if (TouchScreenKeyboard.isSupported && TouchScreenKeyboard.visible == false)
        {
            TouchScreenKeyboard.Open(
                inputField.text,
                TouchScreenKeyboardType.Default,
                autocorrection: true,
                multiline: inputField.lineType != TMP_InputField.LineType.SingleLine
            );
        }
    }
}
