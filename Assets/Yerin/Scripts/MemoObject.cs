// Assets/Scripts/MemoObject.cs
using UnityEngine;
using TMPro;

public class MemoObject : MonoBehaviour
{
    public TextMeshProUGUI contentText;
    private MemoData2 data;  // MemoData2 유지

    public void SetData(MemoData2 memoData2)  // MemoData → MemoData2
    {
        this.data = memoData2;
        if (contentText != null)
            contentText.text = memoData2.content;
    }

    // 클릭 시 (나중에 UI에서 사용)
    void OnMouseDown()
    {
        Debug.Log($"메모 클릭: {data.content}");
        // 상세보기 UI 열기 등
    }
}