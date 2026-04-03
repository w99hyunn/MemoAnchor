// 이전 씬으로 돌아가는 간단한 뒤로가기 기능
using UnityEngine;
using UnityEngine.SceneManagement;

public class BackToPreviousScene : MonoBehaviour
{
    private const string PREF_PREVIOUS_SCENE = "PREVIOUS_SCENE_NAME";

    [Header("Fallback Scene")]
    [Tooltip("이전 씬 정보가 없을 때 이동할 기본 씬 이름")]
    [SerializeField] private string fallbackSceneName = "Home";

    // 이전 씬 이름을 저장하는 정적 메서드 (씬 전환 전에 호출)
    public static void SaveCurrentSceneAsPrevious()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        PlayerPrefs.SetString(PREF_PREVIOUS_SCENE, currentScene);
        PlayerPrefs.Save();
        Debug.Log($"[BackToPreviousScene] Saved current scene as previous: {currentScene}");
    }

    // 뒤로 가기 처리 함수 (버튼 클릭 시 호출)
    public void GoBack()
    {
        string previousScene = GetPreviousSceneName();
        Debug.Log($"[BackToPreviousScene] GoBack > LoadScene({previousScene})");

        SceneManager.LoadScene(previousScene);
    }

    // 이전 씬 이름 가져오기
    private string GetPreviousSceneName()
    {
        if (PlayerPrefs.HasKey(PREF_PREVIOUS_SCENE))
        {
            string previousScene = PlayerPrefs.GetString(PREF_PREVIOUS_SCENE);

            // 현재 씬과 같으면 fallback 사용 (무한 루프 방지)
            if (previousScene == SceneManager.GetActiveScene().name)
            {
                Debug.LogWarning($"[BackToPreviousScene] Previous scene is same as current. Using fallback: {fallbackSceneName}");
                return fallbackSceneName;
            }

            return previousScene;
        }
        else
        {
            Debug.LogWarning($"[BackToPreviousScene] No previous scene saved. Using fallback: {fallbackSceneName}");
            return fallbackSceneName;
        }
    }
}
