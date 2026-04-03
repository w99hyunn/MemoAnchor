// 씬 전환 및 페이드 효과 관리
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
    [Header("Fade Transition")]
    [Tooltip("씬 전환 시 페이드 효과 사용")]
    [SerializeField] private bool useFadeTransition = true;

    [Tooltip("페이드 대기 시간 (초)")]
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Previous Scene Tracking")]
    [Tooltip("씬 전환 시 자동으로 이전 씬 저장 (BackButton용)")]
    [SerializeField] private bool autoSavePreviousScene = true;

    // 특정 씬으로 전환
    public void LoadScene(string sceneName)
    {
        Debug.Log($"[SceneTransitionManager] LoadScene({sceneName})");

        // 자동으로 현재 씬을 이전 씬으로 저장
        if (autoSavePreviousScene)
        {
            SceneHistoryManager.SaveCurrentScene();
        }

        if (useFadeTransition)
        {
            StartCoroutine(LoadSceneWithFade(sceneName));
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    // 페이드 효과와 함께 씬 전환
    private IEnumerator LoadSceneWithFade(string sceneName)
    {
        // 페이드 아웃 (흰색으로)
        SceneTransitionFade.DoFadeOut();
        yield return new WaitForSeconds(fadeDuration);

        // 씬 전환
        SceneTransitionFade.LoadScene(sceneName);
    }

    // 즉시 씬 전환 (페이드 없이)
    public void LoadSceneImmediately(string sceneName)
    {
        Debug.Log($"[SceneTransitionManager] LoadSceneImmediately({sceneName})");

        // 자동으로 현재 씬을 이전 씬으로 저장
        if (autoSavePreviousScene)
        {
            SceneHistoryManager.SaveCurrentScene();
        }

        SceneManager.LoadScene(sceneName);
    }
}
