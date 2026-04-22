using UnityEngine;
using UnityEngine.SceneManagement;
using MemoAnchor.UI;

namespace MemoAnchor
{
    public class Splash : MonoBehaviour
    {
        [SerializeField] private string mainScene = "Main";
        [SerializeField] private float minimumSplashTime = 3f;

        private FadeTransition fadeTransition;

        private void Awake()
        {
            TryGetComponent<FadeTransition>(out fadeTransition);
            ConfigureFrameRate();
        }

        private void Start()
        {
            _ = LoadMainSceneAsync();
        }

        private async Awaitable LoadMainSceneAsync()
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(mainScene);
            loadOperation.allowSceneActivation = false;

            float elapsed = 0f;
            while (elapsed < minimumSplashTime || loadOperation.progress < 0.9f)
            {
                elapsed += Time.deltaTime;
                await Awaitable.NextFrameAsync();
            }

            await fadeTransition.FadeOutAsync();
            loadOperation.allowSceneActivation = true;
        }

        private static void ConfigureFrameRate()
        {
            // Let mobile use the device refresh rate instead of default low-power cap.
            QualitySettings.vSyncCount = 0;
            int refreshRate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            Application.targetFrameRate = refreshRate > 0 ? refreshRate : 60;
        }
    }
}