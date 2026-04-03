using UnityEngine;
using TMPro;

/// <summary>
/// 텍스트 메모 씬에서 텍스트 내용을 표시
/// </summary>
public class TextMemoViewer : MemoViewerBase
{
    [Header("Text Memo Specific")]
    [Tooltip("메모 본문 내용을 표시할 텍스트")]
    [SerializeField] private TMP_Text bodyText;

    [Tooltip("카테고리를 표시할 텍스트")]
    [SerializeField] private TMP_Text categoryText;

    [Tooltip("위치를 표시할 텍스트")]
    [SerializeField] private TMP_Text locationText;

    protected override void Start()
    {
        base.Start();

        // 텍스트 메모 전용 데이터 표시
        DisplayTextMemoData();
    }

    /// <summary>
    /// 텍스트 메모 전용 데이터 표시
    /// </summary>
    private void DisplayTextMemoData()
    {
        if (currentMemoData == null)
        {
            Debug.LogWarning("[TextMemoViewer] No memo data to display!");
            return;
        }

        // 본문 내용
        if (bodyText != null)
        {
            bodyText.text = !string.IsNullOrEmpty(currentMemoData.body) 
                ? currentMemoData.body 
                : "내용 없음";

            if (verboseDebug)
            {
                Debug.Log($"[TextMemoViewer] Body text: {currentMemoData.body}");
            }
        }

        // 카테고리
        if (categoryText != null)
        {
            if (!string.IsNullOrEmpty(currentMemoData.category))
            {
                categoryText.text = $"카테고리: {currentMemoData.category}";
            }
            else
            {
                categoryText.text = "";
            }
        }

        // 위치
        if (locationText != null)
        {
            if (!string.IsNullOrEmpty(currentMemoData.location))
            {
                locationText.text = $"위치: {currentMemoData.location}";
            }
            else
            {
                locationText.text = "";
            }
        }

        if (verboseDebug)
        {
            Debug.Log("[TextMemoViewer] Text memo data displayed successfully");
        }
    }
}
