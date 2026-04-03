using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class MemoViewerController : MonoBehaviour
{
    [Header("TabPinCreate")]
    [SerializeField] private TabPinCreate pinStore;

    [Header("씬3 전용 UI 요소들")]
    [SerializeField] private TMP_Text titleText;        // 제목 표시 텍스트
    [SerializeField] private TMP_Text bodyText;         // 본문 표시 텍스트
    [SerializeField] private TMP_Text locationText;     // 위치 표시 텍스트
    [SerializeField] private TMP_Text dueDateText;      // 마감일 표시 텍스트
    [SerializeField] private TMP_Text dueTimeText;      // 마감 시간 표시 텍스트

    [Header("이미지 표시")]
    [SerializeField] private Transform imageContainer;  // 이미지들이 들어갈 부모
    [SerializeField] private GameObject imagePrefab;    // 이미지 표시용 프리팹

    [Header("음성 표시")]
    [SerializeField] private Transform voiceContainer;  // 음성 목록이 들어갈 부모
    [SerializeField] private GameObject voiceItemPrefab; // 음성 아이템 프리팹

    [Header("체크리스트 표시")]
    [SerializeField] private Transform checklistContainer;  // 체크리스트 항목들이 들어갈 부모
    [SerializeField] private GameObject checklistItemPrefab; // 체크리스트 아이템 프리팹

    [Header("디버그")]
    [SerializeField] private bool showDebugLog = true;

    void Start()
    {
        LoadAndDisplaySelectedMemo();
    }

    void LoadAndDisplaySelectedMemo()
    {
        // 1. 선택된 메모 ID 가져오기
        string selectedMemoId = SelectedMemoManager.selectedMemoId;

        if (string.IsNullOrEmpty(selectedMemoId))
        {
            Debug.Log("[MemoViewer] 선택된 메모가 없습니다.");
            return;
        }

        if (showDebugLog)
            Debug.Log($"[MemoViewer] 선택된 메모 ID: {selectedMemoId}");

        // 2. TabPinCreate 찾기
        if (pinStore == null)
        {
            pinStore = FindObjectOfType<TabPinCreate>();
            if (pinStore == null)
            {
                Debug.LogError("[MemoViewer] TabPinCreate를 찾을 수 없습니다!");
                return;
            }
        }

        // 3. 메모 데이터 로드
        MemoData memo = pinStore.GetMemoById(selectedMemoId);

        if (memo == null)
        {
            Debug.LogWarning($"[MemoViewer] ID {selectedMemoId}에 해당하는 메모를 찾을 수 없습니다.");
            return;
        }

        if (showDebugLog)
            Debug.Log($"[MemoViewer] 메모 찾음 - 제목: '{memo.title}', 타입: {memo.memoType}");

        // 4. UI에 데이터 표시
        DisplayMemoData(memo);
    }

    void DisplayMemoData(MemoData memo)
    {
        // 텍스트 정보 표시
        if (titleText) titleText.text = memo.title ?? "";
        if (bodyText) bodyText.text = memo.body ?? "";
        if (locationText) locationText.text = memo.location ?? "";
        if (dueDateText) dueDateText.text = memo.dueDate ?? "";
        if (dueTimeText) dueTimeText.text = memo.dueTime ?? "";

        if (showDebugLog)
            Debug.Log($"[MemoViewer] 텍스트 표시 완료");

        // 메모 타입별 표시
        switch (memo.memoType)
        {
            case "image":
                if (memo.imagePaths != null && memo.imagePaths.Count > 0)
                    DisplayImages(memo.imagePaths);
                break;

            case "voice":
            case "voicememo":
                if (memo.voiceRecordingPaths != null && memo.voiceRecordingPaths.Count > 0)
                    DisplayVoiceRecordings(memo.voiceRecordingPaths);
                break;

            case "checklist":
                // body에 체크리스트 항목들이 줄바꿈으로 저장되어 있음
                if (!string.IsNullOrEmpty(memo.body))
                    DisplayChecklist(memo.body);
                break;

            case "text":
            default:
                // 텍스트는 이미 위에서 표시됨
                break;
        }

        if (showDebugLog)
            Debug.Log($"[MemoViewer] ✅ 메모 표시 완료!");
    }

    void DisplayImages(List<string> imagePaths)
    {
        foreach (Transform child in imageContainer) Destroy(child.gameObject);

        foreach (string path in imagePaths)
        {
            Texture2D texture = LoadImageFromFile(path);
            if (texture != null)
            {
                GameObject imgObj = Instantiate(imagePrefab, imageContainer);

                // 이미지 설정
                Image imgComponent = imgObj.GetComponent<Image>();
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                if (imgComponent) imgComponent.sprite = sprite;

            }
        }
    }

    void DisplayVoiceRecordings(List<string> voicePaths)
    {
        if (voiceContainer == null || voiceItemPrefab == null)
        {
            Debug.LogWarning("[MemoViewer] voiceContainer 또는 voiceItemPrefab이 할당되지 않았습니다.");
            return;
        }

        // 기존 음성 아이템 삭제
        foreach (Transform child in voiceContainer)
        {
            Destroy(child.gameObject);
        }

        // 음성 아이템 생성
        foreach (string voicePath in voicePaths)
        {
            GameObject voiceObj = Instantiate(voiceItemPrefab, voiceContainer);

            // voiceItemPrefab 안에 재생 버튼 등을 구성
            TMP_Text voiceText = voiceObj.GetComponentInChildren<TMP_Text>();
            if (voiceText != null)
            {
                voiceText.text = $"🎤 {System.IO.Path.GetFileName(voicePath)}";
            }
        }

        if (showDebugLog)
            Debug.Log($"[MemoViewer] 음성 {voicePaths.Count}개 표시 완료");
    }

    void DisplayChecklist(string checklistData)
    {
        if (checklistContainer == null || checklistItemPrefab == null)
        {
            Debug.LogWarning("[MemoViewer] checklistContainer 또는 checklistItemPrefab이 할당되지 않았습니다.");
            return;
        }

        // 기존 체크리스트 아이템 삭제
        foreach (Transform child in checklistContainer)
        {
            Destroy(child.gameObject);
        }

        // 줄바꿈으로 분리
        string[] items = checklistData.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

        foreach (string item in items)
        {
            GameObject checkObj = Instantiate(checklistItemPrefab, checklistContainer);

            // 체크박스 + 텍스트 구성
            Toggle toggle = checkObj.GetComponentInChildren<Toggle>();
            TMP_Text itemText = checkObj.GetComponentInChildren<TMP_Text>();

            if (itemText != null)
            {
                // "✓ " 또는 "☐ " 같은 체크 표시 제거하고 순수 텍스트만
                string cleanText = item.TrimStart('✓', '☐', ' ');
                itemText.text = cleanText;
            }

            if (toggle != null)
            {
                // 항목이 ✓로 시작하면 체크됨
                toggle.isOn = item.TrimStart().StartsWith("✓");
            }
        }

        if (showDebugLog)
            Debug.Log($"[MemoViewer] 체크리스트 {items.Length}개 항목 표시 완료");
    }

    Texture2D LoadImageFromFile(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return null;

        try
        {
            string fullPath = System.IO.Path.Combine(Application.persistentDataPath, "MemoImages", fileName);

            if (!System.IO.File.Exists(fullPath))
            {
                Debug.LogWarning($"[MemoViewer] 이미지 파일을 찾을 수 없습니다: {fileName}");
                return null;
            }

            byte[] data = System.IO.File.ReadAllBytes(fullPath);
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(data);

            return texture;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MemoViewer] 이미지 로드 실패 ({fileName}): {e.Message}");
            return null;
        }
    }
}
