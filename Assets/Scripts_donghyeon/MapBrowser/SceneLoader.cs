using UnityEngine;
using UnityEngine.UI;

public class SceneLoader : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button toScanBtn;
    [SerializeField] private Button toMemoListBtn;
    [SerializeField] private Button memoOvBtn;
    [SerializeField] private Button offsetAlarmBtn;
    [SerializeField] private Button offsetProfileBtn;
    [SerializeField] private Button offsetSetBtn;

    [Header("Scene Name")]
    [SerializeField] private string scanSceneName = "Scan";
    [SerializeField] private string memoListSceneName = "MemoList";
    [SerializeField] private string memoCheckSceneName = "MemoCheck";
    [SerializeField] private string alarmSceneName = "Alarm";
    [SerializeField] private string profileSceneName = "Profile";
    [SerializeField] private string setSceneName = "Set";

    private void Start()
    {
        // ToScanBtn 클릭 이벤트 연결
        if (toScanBtn != null)
        {
            toScanBtn.onClick.AddListener(OnToScanButtonClicked);
            Debug.Log("[ScanSceneLoader] ToScanBtn listener registered");
        }
        else
        {
            Debug.LogError("[ScanSceneLoader] ToScanBtn이 할당되지 않았습니다!");
        }

        // ToMemoListBtn 클릭 이벤트 연결
        if (toMemoListBtn != null)
        {
            toMemoListBtn.onClick.AddListener(OnToMemoListButtonClicked);
            Debug.Log("[ScanSceneLoader] ToMemoListBtn listener registered");
        }
        else
        {
            Debug.LogError("[ScanSceneLoader] ToMemoListBtn이 할당되지 않았습니다!");
        }

        // MemoOvBtn 클릭 이벤트 연결
        if (memoOvBtn != null)
        {
            memoOvBtn.onClick.AddListener(OnMemoOvButtonClicked);
            Debug.Log("[ScanSceneLoader] MemoOvBtn listener registered");
        }
        else
        {
            Debug.LogError("[ScanSceneLoader] MemoOvBtn이 할당되지 않았습니다!");
        }

        // Offset_Alarm 클릭 이벤트 연결
        if (offsetAlarmBtn != null)
        {
            offsetAlarmBtn.onClick.AddListener(OnOffsetAlarmButtonClicked);
            Debug.Log("[ScanSceneLoader] Offset_Alarm listener registered");
        }
        else
        {
            Debug.LogError("[ScanSceneLoader] Offset_Alarm이 할당되지 않았습니다!");
        }

        // Offset_Profile 클릭 이벤트 연결
        if (offsetProfileBtn != null)
        {
            offsetProfileBtn.onClick.AddListener(OnOffsetProfileButtonClicked);
            Debug.Log("[ScanSceneLoader] Offset_Profile listener registered");
        }
        else
        {
            Debug.LogError("[ScanSceneLoader] Offset_Profile이 할당되지 않았습니다!");
        }

        // Offset_Set 클릭 이벤트 연결
        if (offsetSetBtn != null)
        {
            offsetSetBtn.onClick.AddListener(OnOffsetSetButtonClicked);
            Debug.Log("[ScanSceneLoader] Offset_Set listener registered");
        }
        else
        {
            Debug.LogError("[ScanSceneLoader] Offset_Set이 할당되지 않았습니다!");
        }
    }

    private void OnToScanButtonClicked()
    {
        Debug.Log($"[ScanSceneLoader] ToScanBtn clicked, loading scene: {scanSceneName}");

        // SceneTransitionFade를 사용하면 페이드 효과와 함께 씬 전환
        SceneTransitionFade.LoadScene(scanSceneName);
    }

    private void OnToMemoListButtonClicked()
    {
        Debug.Log($"[ScanSceneLoader] ToMemoListBtn clicked, loading scene: {memoListSceneName}");

        // SceneTransitionFade를 사용하면 페이드 효과와 함께 씬 전환
        SceneTransitionFade.LoadScene(memoListSceneName);
    }

    private void OnMemoOvButtonClicked()
    {
        Debug.Log($"[ScanSceneLoader] MemoOvBtn clicked, loading scene: {memoCheckSceneName}");

        // SceneTransitionFade를 사용하면 페이드 효과와 함께 씬 전환
        SceneTransitionFade.LoadScene(memoCheckSceneName);
    }

    private void OnOffsetAlarmButtonClicked()
    {
        Debug.Log($"[ScanSceneLoader] Offset_Alarm clicked, loading scene: {alarmSceneName}");

        // SceneTransitionFade를 사용하면 페이드 효과와 함께 씬 전환
        SceneTransitionFade.LoadScene(alarmSceneName);
    }

    private void OnOffsetProfileButtonClicked()
    {
        Debug.Log($"[ScanSceneLoader] Offset_Profile clicked, loading scene: {profileSceneName}");

        // SceneTransitionFade를 사용하면 페이드 효과와 함께 씬 전환
        SceneTransitionFade.LoadScene(profileSceneName);
    }

    private void OnOffsetSetButtonClicked()
    {
        Debug.Log($"[ScanSceneLoader] Offset_Set clicked, loading scene: {setSceneName}");

        // SceneTransitionFade를 사용하면 페이드 효과와 함께 씬 전환
        SceneTransitionFade.LoadScene(setSceneName);
    }

    private void OnDestroy()
    {
        // 메모리 누수 방지를 위해 리스너 제거
        if (toScanBtn != null)
        {
            toScanBtn.onClick.RemoveListener(OnToScanButtonClicked);
        }

        if (toMemoListBtn != null)
        {
            toMemoListBtn.onClick.RemoveListener(OnToMemoListButtonClicked);
        }

        if (memoOvBtn != null)
        {
            memoOvBtn.onClick.RemoveListener(OnMemoOvButtonClicked);
        }

        if (offsetAlarmBtn != null)
        {
            offsetAlarmBtn.onClick.RemoveListener(OnOffsetAlarmButtonClicked);
        }

        if (offsetProfileBtn != null)
        {
            offsetProfileBtn.onClick.RemoveListener(OnOffsetProfileButtonClicked);
        }

        if (offsetSetBtn != null)
        {
            offsetSetBtn.onClick.RemoveListener(OnOffsetSetButtonClicked);
        }
    }
}
