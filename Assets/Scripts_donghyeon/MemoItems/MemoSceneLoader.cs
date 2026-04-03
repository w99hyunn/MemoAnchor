using UnityEngine;
using UnityEngine.UI;

public class MemoSceneLoader : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button toScanBtn;
    [SerializeField] private Button toHomeBtn;
    [SerializeField] private Button to3DMapBtn;

    [Header("Scene Name")]
    [SerializeField] private string scanSceneName = "Scan";
    [SerializeField] private string homeSceneName = "Home";
    [SerializeField] private string map3DSceneName = "Map3D";
    void Start()
    {
        if (toScanBtn != null)
        {
            toScanBtn.onClick.AddListener(OnToScanButtonClicked);
            Debug.Log("[ScanSceneLoader] ToScanBtn listener registered");
        }
        else
        {
            Debug.LogError("[ScanSceneLoader] ToScanBtn이 할당되지 않았습니다!");
        }

        if (toHomeBtn != null)
        {
            toHomeBtn.onClick.AddListener(OnToHomeButtonClicked);
            Debug.Log("[ScanSceneLoader] ToHomeBtn listener registered");
        }
        else
        {
            Debug.LogError("[ScanSceneLoader] ToHomeBtn이 할당되지 않았습니다!");
        }

        if (to3DMapBtn != null)
        {
            to3DMapBtn.onClick.AddListener(OnTo3DMapButtonClicked);
            Debug.Log("[ScanSceneLoader] To3DMapBtn listener registered");
        }
        else
        {
            Debug.LogError("[ScanSceneLoader] To3DMapBtn이 할당되지 않았습니다!");
        }
    }

    private void OnToScanButtonClicked()
    {
        Debug.Log($"[ScanSceneLoader] ToScanBtn clicked, loading scene: {scanSceneName}");

        // SceneTransitionFade를 사용하면 페이드 효과와 함께 씬 전환
        SceneTransitionFade.LoadScene(scanSceneName);
    }

    private void OnToHomeButtonClicked()
    {
        Debug.Log($"[ScanSceneLoader] ToHomeBtn clicked, loading scene: {homeSceneName}");

        // SceneTransitionFade를 사용하면 페이드 효과와 함께 씬 전환
        SceneTransitionFade.LoadScene(homeSceneName);
    }

    private void OnTo3DMapButtonClicked()
    {
        Debug.Log($"[ScanSceneLoader] To3DMapBtn clicked, loading scene: {map3DSceneName}");

        // SceneTransitionFade를 사용하면 페이드 효과와 함께 씬 전환
        SceneTransitionFade.LoadScene(map3DSceneName);
    }

    private void OnDestroy()
    {
        // 메모리 누수 방지를 위해 리스너 제거
        if (toScanBtn != null)
        {
            toScanBtn.onClick.RemoveListener(OnToScanButtonClicked);
        }

    }

    private void OnHomeDestroy()
    {
        // 메모리 누수 방지를 위해 리스너 제거
        if (toHomeBtn != null)
        {
            toHomeBtn.onClick.RemoveListener(OnToHomeButtonClicked);
        }
    }

    private void On3DMapDestroy()
    {
        // 메모리 누수 방지를 위해 리스너 제거
        if (to3DMapBtn != null)
        {
            to3DMapBtn.onClick.RemoveListener(OnTo3DMapButtonClicked);
        }
    }
}