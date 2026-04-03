using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 체크리스트 메모 씬에서 체크리스트 항목들을 표시
/// </summary>
public class ChecklistMemoViewer : MemoViewerBase
{
    [Header("Checklist Memo Specific")]
    [Tooltip("체크리스트 항목들을 담을 부모 Transform")]
    [SerializeField] private Transform checklistContainer;

    [Tooltip("체크리스트 항목 프리팹 (Toggle + Text)")]
    [SerializeField] private GameObject checklistItemPrefab;

    [Tooltip("전체 진행률을 표시할 슬라이더")]
    [SerializeField] private Slider progressSlider;

    [Tooltip("진행률 텍스트 (예: 3/10 완료)")]
    [SerializeField] private TMP_Text progressText;

    [Tooltip("완료된 항목 개수 표시")]
    [SerializeField] private TMP_Text completedCountText;

    [Header("Checklist Item UI Names")]
    [Tooltip("항목 프리팹 내부의 Toggle 오브젝트 이름")]
    [SerializeField] private string toggleObjectName = "Toggle";

    [Tooltip("항목 프리팹 내부의 Text 오브젝트 이름")]
    [SerializeField] private string textObjectName = "Text";

    private List<ChecklistItem> checklistItems = new List<ChecklistItem>();

    // 체크리스트 항목 데이터 구조
    [System.Serializable]
    public class ChecklistItemData
    {
        public string text;
        public bool isChecked;
    }

    // 런타임 체크리스트 항목
    private class ChecklistItem
    {
        public GameObject gameObject;
        public Toggle toggle;
        public TMP_Text text;
        public bool isChecked;
    }

    protected override void Start()
    {
        base.Start();

        // 체크리스트 메모 전용 데이터 표시
        DisplayChecklistMemoData();
    }

    /// <summary>
    /// 체크리스트 메모 전용 데이터 표시
    /// </summary>
    private void DisplayChecklistMemoData()
    {
        if (currentMemoData == null)
        {
            Debug.LogWarning("[ChecklistMemoViewer] No memo data to display!");
            return;
        }

        // body에서 체크리스트 항목 파싱
        List<ChecklistItemData> items = ParseChecklistFromBody(currentMemoData.body);

        if (items.Count == 0)
        {
            Debug.LogWarning("[ChecklistMemoViewer] No checklist items found!");
            
            if (progressText != null)
            {
                progressText.text = "체크리스트 항목 없음";
            }
            return;
        }

        // 체크리스트 항목 생성
        CreateChecklistItems(items);

        // 진행률 업데이트
        UpdateProgress();

        if (verboseDebug)
        {
            Debug.Log($"[ChecklistMemoViewer] Created {checklistItems.Count} checklist items");
        }
    }

    /// <summary>
    /// body 텍스트에서 체크리스트 항목 파싱
    /// 형식: "[ ] 항목1\n[x] 항목2\n[ ] 항목3" 또는 "- 항목1\n- 항목2"
    /// </summary>
    private List<ChecklistItemData> ParseChecklistFromBody(string body)
    {
        List<ChecklistItemData> items = new List<ChecklistItemData>();

        if (string.IsNullOrEmpty(body)) return items;

        // 줄바꿈으로 분리
        string[] lines = body.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine)) continue;

            ChecklistItemData item = new ChecklistItemData();

            // "[ ] 항목" 또는 "[x] 항목" 형식 체크
            if (trimmedLine.StartsWith("[") && trimmedLine.Length > 3)
            {
                char checkChar = trimmedLine[1];
                item.isChecked = (checkChar == 'x' || checkChar == 'X' || checkChar == 'v' || checkChar == 'V');
                item.text = trimmedLine.Substring(3).Trim();
            }
            // "- 항목" 형식 체크
            else if (trimmedLine.StartsWith("-") || trimmedLine.StartsWith("•"))
            {
                item.isChecked = false;
                item.text = trimmedLine.Substring(1).Trim();
            }
            // 번호 형식 "1. 항목" 체크
            else if (char.IsDigit(trimmedLine[0]) && trimmedLine.Contains("."))
            {
                int dotIndex = trimmedLine.IndexOf('.');
                item.isChecked = false;
                item.text = trimmedLine.Substring(dotIndex + 1).Trim();
            }
            // 그 외는 그냥 텍스트로
            else
            {
                item.isChecked = false;
                item.text = trimmedLine;
            }

            if (!string.IsNullOrEmpty(item.text))
            {
                items.Add(item);
            }
        }

        return items;
    }

    /// <summary>
    /// 체크리스트 항목들을 UI로 생성
    /// </summary>
    private void CreateChecklistItems(List<ChecklistItemData> itemsData)
    {
        if (checklistContainer == null || checklistItemPrefab == null)
        {
            Debug.LogError("[ChecklistMemoViewer] checklistContainer or checklistItemPrefab is null!");
            return;
        }

        // 기존 항목 삭제
        ClearChecklistItems();

        // 새 항목 생성
        foreach (var itemData in itemsData)
        {
            GameObject itemObj = Instantiate(checklistItemPrefab, checklistContainer);
            
            ChecklistItem item = new ChecklistItem
            {
                gameObject = itemObj,
                isChecked = itemData.isChecked
            };

            // Toggle 찾기
            Transform toggleTransform = itemObj.transform.Find(toggleObjectName);
            if (toggleTransform != null)
            {
                item.toggle = toggleTransform.GetComponent<Toggle>();
                if (item.toggle != null)
                {
                    item.toggle.isOn = itemData.isChecked;
                    
                    // 체크 변경 이벤트
                    item.toggle.onValueChanged.AddListener((bool value) => OnChecklistItemToggled(item, value));
                }
            }
            else
            {
                // 프리팹 자체가 Toggle일 수도 있음
                item.toggle = itemObj.GetComponent<Toggle>();
                if (item.toggle != null)
                {
                    item.toggle.isOn = itemData.isChecked;
                    item.toggle.onValueChanged.AddListener((bool value) => OnChecklistItemToggled(item, value));
                }
            }

            // Text 찾기
            Transform textTransform = itemObj.transform.Find(textObjectName);
            if (textTransform != null)
            {
                item.text = textTransform.GetComponent<TMP_Text>();
                if (item.text != null)
                {
                    item.text.text = itemData.text;
                }
            }
            else
            {
                // Toggle 하위의 Label이 Text일 수도 있음
                if (item.toggle != null)
                {
                    Transform labelTransform = item.toggle.transform.Find("Label");
                    if (labelTransform != null)
                    {
                        item.text = labelTransform.GetComponent<TMP_Text>();
                        if (item.text != null)
                        {
                            item.text.text = itemData.text;
                        }
                    }
                }
            }

            checklistItems.Add(item);

            if (verboseDebug)
            {
                Debug.Log($"[ChecklistMemoViewer] Created item: {itemData.text} (checked: {itemData.isChecked})");
            }
        }
    }

    /// <summary>
    /// 체크리스트 항목 체크 토글 시
    /// </summary>
    private void OnChecklistItemToggled(ChecklistItem item, bool isChecked)
    {
        item.isChecked = isChecked;
        UpdateProgress();

        if (verboseDebug)
        {
            Debug.Log($"[ChecklistMemoViewer] Item toggled: {isChecked}");
        }
    }

    /// <summary>
    /// 진행률 업데이트
    /// </summary>
    private void UpdateProgress()
    {
        if (checklistItems.Count == 0) return;

        int completedCount = 0;
        foreach (var item in checklistItems)
        {
            if (item.isChecked)
            {
                completedCount++;
            }
        }

        int totalCount = checklistItems.Count;
        float progress = (float)completedCount / totalCount;

        // 진행률 슬라이더
        if (progressSlider != null)
        {
            progressSlider.value = progress;
        }

        // 진행률 텍스트
        if (progressText != null)
        {
            progressText.text = $"{completedCount}/{totalCount} 완료 ({Mathf.RoundToInt(progress * 100)}%)";
        }

        // 완료 개수
        if (completedCountText != null)
        {
            completedCountText.text = $"{completedCount}개 완료";
        }

        if (verboseDebug)
        {
            Debug.Log($"[ChecklistMemoViewer] Progress updated: {completedCount}/{totalCount}");
        }
    }

    /// <summary>
    /// 모든 체크리스트 항목 삭제
    /// </summary>
    private void ClearChecklistItems()
    {
        foreach (var item in checklistItems)
        {
            if (item.gameObject != null)
            {
                Destroy(item.gameObject);
            }
        }
        checklistItems.Clear();
    }

    /// <summary>
    /// 모든 항목 체크
    /// </summary>
    public void CheckAll()
    {
        foreach (var item in checklistItems)
        {
            if (item.toggle != null)
            {
                item.toggle.isOn = true;
            }
        }
    }

    /// <summary>
    /// 모든 항목 체크 해제
    /// </summary>
    public void UncheckAll()
    {
        foreach (var item in checklistItems)
        {
            if (item.toggle != null)
            {
                item.toggle.isOn = false;
            }
        }
    }

    private void OnDestroy()
    {
        ClearChecklistItems();
    }
}
