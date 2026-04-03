// 씬 이동 이력 관리 유틸리티 클래스
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneHistoryManager
{
    private const string PREF_PREVIOUS_SCENE = "PREVIOUS_SCENE_NAME";

    // 현재 씬을 이전 씬으로 저장
    public static void SaveCurrentScene()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        PlayerPrefs.SetString(PREF_PREVIOUS_SCENE, currentScene);
        PlayerPrefs.Save();
        Debug.Log($"[SceneHistoryManager] Saved current scene: {currentScene}");
    }

    // 이전 씬 이름 가져오기
    public static string GetPreviousScene(string fallback = "Home")
    {
        if (PlayerPrefs.HasKey(PREF_PREVIOUS_SCENE))
        {
            string previousScene = PlayerPrefs.GetString(PREF_PREVIOUS_SCENE);

            // 현재 씬과 같으면 fallback 반환 (무한 루프 방지)
            if (previousScene == SceneManager.GetActiveScene().name)
            {
                Debug.LogWarning($"[SceneHistoryManager] Previous scene is current scene. Using fallback: {fallback}");
                return fallback;
            }

            return previousScene;
        }

        Debug.LogWarning($"[SceneHistoryManager] No previous scene found. Using fallback: {fallback}");
        return fallback;
    }

    // 이전 씬 정보 삭제
    public static void Clear()
    {
        PlayerPrefs.DeleteKey(PREF_PREVIOUS_SCENE);
        PlayerPrefs.Save();
        Debug.Log("[SceneHistoryManager] Cleared previous scene history");
    }
}
