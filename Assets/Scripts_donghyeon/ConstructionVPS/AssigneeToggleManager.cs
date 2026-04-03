using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// AssigneeRow의 Toggle과 Input 상태를 현재 선택된 메모에 저장하는 스크립트
/// </summary>
public class AssigneeToggleManager : MonoBehaviour
{
    [SerializeField] private Toggle assigneeToggle;
    [SerializeField] private TMP_InputField assigneeInput;

    // 현재 편집 중인 메모 ID를 저장 (TabPinCreate에서 설정)
    private static string currentMemoId = null;

    private void Start()
    {
        if (assigneeToggle == null)
        {
            assigneeToggle = GetComponent<Toggle>();
        }

        // AssigneeInput 자동 검색
        if (assigneeInput == null)
        {
            assigneeInput = GetComponentInChildren<TMP_InputField>();
            if (assigneeInput == null)
            {
                // 형제 오브젝트에서 찾기
                Transform parent = transform;
                assigneeInput = parent.GetComponentInChildren<TMP_InputField>();
            }
        }

        if (assigneeToggle != null)
        {
            // Toggle 값 변경 시 PIN 데이터에 저장
            assigneeToggle.onValueChanged.AddListener(OnToggleValueChanged);

            Debug.Log($"★★★ [ASSIGNEE] [AssigneeToggleManager] 초기화 완료");
        }
        else
        {
            Debug.LogWarning("★★★ [ASSIGNEE] [AssigneeToggleManager] assigneeToggle이 설정되지 않았습니다!");
        }

        if (assigneeInput != null)
        {
            // InputField 값 변경 시 PIN 데이터에 저장
            assigneeInput.onEndEdit.AddListener(OnInputEndEdit);
            Debug.Log($"★★★ [ASSIGNEE] [AssigneeToggleManager] AssigneeInput 연결 완료");
        }
        else
        {
            Debug.LogWarning("★★★ [ASSIGNEE] [AssigneeToggleManager] assigneeInput을 찾을 수 없습니다!");
        }
    }

    private void OnToggleValueChanged(bool isOn)
    {
        if (string.IsNullOrEmpty(currentMemoId))
        {
            Debug.LogWarning($"★★★ [ASSIGNEE] [AssigneeToggleManager] ✗ currentMemoId가 설정되지 않았습니다!");
            return;
        }

        // TabPinCreate에서 현재 메모의 isAssigned 업데이트하도록 이벤트 발송
        TabPinCreate tabPinCreate = FindObjectOfType<TabPinCreate>();
        if (tabPinCreate != null)
        {
            tabPinCreate.UpdateMemoAssignedState(currentMemoId, isOn);
        }

        Debug.Log($"★★★ [ASSIGNEE] [AssigneeToggleManager] Toggle 상태 변경: memoId={currentMemoId}, isOn={isOn}");
    }

    private void OnInputEndEdit(string value)
    {
        if (string.IsNullOrEmpty(currentMemoId))
        {
            Debug.LogWarning($"★★★ [ASSIGNEE] [AssigneeToggleManager] ✗ currentMemoId가 설정되지 않았습니다!");
            return;
        }

        // TabPinCreate에서 현재 메모의 assignee 업데이트
        TabPinCreate tabPinCreate = FindObjectOfType<TabPinCreate>();
        if (tabPinCreate != null)
        {
            tabPinCreate.UpdateMemoAssignee(currentMemoId, value);
        }

        Debug.Log($"★★★ [ASSIGNEE] [AssigneeToggleManager] Assignee 이름 변경: memoId={currentMemoId}, assignee={value}");
    }

    private void OnDestroy()
    {
        if (assigneeToggle != null)
        {
            assigneeToggle.onValueChanged.RemoveListener(OnToggleValueChanged);
        }

        if (assigneeInput != null)
        {
            assigneeInput.onEndEdit.RemoveListener(OnInputEndEdit);
        }
    }

    /// <summary>
    /// 현재 편집 중인 메모 ID 설정 (TabPinCreate에서 호출)
    /// </summary>
    public static void SetCurrentMemoId(string memoId)
    {
        currentMemoId = memoId;
        Debug.Log($"★★★ [ASSIGNEE] [AssigneeToggleManager] 현재 메모 ID 설정: {memoId}");
    }

    /// <summary>
    /// 현재 메모의 isAssigned 상태와 assignee 이름으로 UI 업데이트 (TabPinCreate에서 호출)
    /// </summary>
    public void LoadAssigneeState(bool isAssigned, string assigneeName)
    {
        Debug.Log($"★★★ [ASSIGNEE] [AssigneeToggleManager] LoadAssigneeState 호출: isAssigned={isAssigned}, assignee={assigneeName}");

        if (assigneeToggle != null)
        {
            // onValueChanged 이벤트를 트리거하지 않고 값만 설정
            assigneeToggle.SetIsOnWithoutNotify(isAssigned);
            Debug.Log($"★★★ [ASSIGNEE] [AssigneeToggleManager] ✓ Toggle 상태 불러옴: {isAssigned}, Toggle.isOn={assigneeToggle.isOn}");
        }
        else
        {
            Debug.LogWarning($"★★★ [ASSIGNEE] [AssigneeToggleManager] ✗ assigneeToggle이 null입니다!");
        }

        if (assigneeInput != null)
        {
            // onEndEdit 이벤트를 트리거하지 않고 값만 설정
            assigneeInput.SetTextWithoutNotify(assigneeName ?? "");
            Debug.Log($"★★★ [ASSIGNEE] [AssigneeToggleManager] ✓ Assignee 이름 불러옴: {assigneeName}");
        }
        else
        {
            Debug.LogWarning($"★★★ [ASSIGNEE] [AssigneeToggleManager] ✗ assigneeInput이 null입니다!");
        }
    }
}
