
// 메모 작성 시 메타정보 표시, 메모 지정자를 입력받아 확정하는 UI 컨트롤러
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MemoMetaUI : MonoBehaviour
{
    // UI 참조 컴포넌트
    [Header("UI Refs")]
    [Tooltip("메모 창에 날짜/시간/사용자ID를 표시할 TMP_Text 넣는 자리")]
    [SerializeField] private TMP_Text metaText;

    [Tooltip("지정자(메모를 봐야하는 사람) 입력칸 TMP_InputField 넣는 자리")]
    [SerializeField] private TMP_InputField assigneeInput;

    [Tooltip("지정자 입력 시 나타날 체크 버튼(토글) Button 넣는 자리")]
    [SerializeField] private Button assigneeCheckButton;

    // 사용자 ID 관련 설정
    [Header("User Id Source")]
    [Tooltip("PlayerPrefs에서 사용자 ID를 읽을 키 이름(없으면 deviceUniqueIdentifier로 대체)")]
    [SerializeField] private string userIdPrefKey = "USER_ID";

    // 지정자 확정 시 외부에 알리는 이벤트
    public event Action<string> OnAssigneeConfirmed;

    private void Awake()
    {
        // 버튼(토글)은 기본 숨김
        if (assigneeCheckButton != null)
            assigneeCheckButton.gameObject.SetActive(false);

        // 입력 변화에 따라 버튼(토글) 노출 결정
        if (assigneeInput != null)
            assigneeInput.onValueChanged.AddListener(OnAssigneeChanged);

        // 버튼(토글) 클릭 처리
        if (assigneeCheckButton != null)
            assigneeCheckButton.onClick.AddListener(ConfirmAssignee);
    }

    // 활성화 시 메타정보 갱신 및 지정자 UI 상태 갱신 함수 (Unity 2022.3+ Canvas 리빌드 호환성)
    private void OnEnable()
    {
        // Canvas 리빌드 완료 후 UI 갱신
        StartCoroutine(RefreshUIAfterCanvasReady());
    }

    private IEnumerator RefreshUIAfterCanvasReady()
    {
        // Canvas 리빌드 완전 완료 대기
        yield return null;
        yield return new WaitForEndOfFrame();

        RefreshMetaText();
        RefreshAssigneeUI();
    }

    // 오브젝트 소멸 시 이벤트 해제 함수 (메모리 누수 방지)
    private void OnDestroy()
    {
        if (assigneeInput != null)
            assigneeInput.onValueChanged.RemoveListener(OnAssigneeChanged);

        if (assigneeCheckButton != null)
            assigneeCheckButton.onClick.RemoveListener(ConfirmAssignee);
    }

    // 메타정보 텍스트 갱신 함수
    private void RefreshMetaText()
    {
        if (metaText == null) return;

        // 사용자 ID 가져오기 - PlayerPrefs 우선, 없으면 기기 고유값
        string userId = PlayerPrefs.GetString(userIdPrefKey, "");
        if (string.IsNullOrWhiteSpace(userId))
            userId = SystemInfo.deviceUniqueIdentifier;

        // 현재 날짜/시간 가져오기
        DateTime now = DateTime.Now;
        string fmt = "yyyy-MM-dd HH:mm";
        metaText.text = $"{now.ToString(fmt)} | User: {userId}";
    }

    // 지정자 UI 상태 갱신 함수
    private void RefreshAssigneeUI()
    {
        if (assigneeInput == null || assigneeCheckButton == null) return;

        string text = assigneeInput.text ?? "";
        bool hasValue = !string.IsNullOrWhiteSpace(text);

        // 값이 있으면 체크 버튼 노출
        assigneeCheckButton.gameObject.SetActive(hasValue);
    }

    // 지정자 입력 변화 시 UI 상태 갱신 함수
    private void OnAssigneeChanged(string value)
    {
        RefreshAssigneeUI();
    }

    // 지정자 확정 시 처리 함수
    private void ConfirmAssignee()
    {
        if (assigneeInput == null) return;

        string assignee = (assigneeInput.text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(assignee)) return;

        // 지정자 확정 시 입력 잠그기
        assigneeInput.interactable = false;

        // 외부에 알리기(저장 로직 연결용)
        OnAssigneeConfirmed?.Invoke(assignee);
    }
}
