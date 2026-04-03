using UnityEngine;

// 씬 전환 시에도 선택된 메모 ID를 유지하는 정적 클래스
public static class SelectedMemoManager
{
    public static string selectedMemoId = "";

    // 디버그용
    public static void SetSelectedMemo(string memoId)
    {
        selectedMemoId = memoId;
        Debug.Log($"[SelectedMemoManager] 메모 ID 저장: {memoId}");
    }

    public static string GetSelectedMemo()
    {
        Debug.Log($"[SelectedMemoManager] 메모 ID 로드: {selectedMemoId}");
        return selectedMemoId;
    }

    public static void Clear()
    {
        selectedMemoId = "";
    }
}