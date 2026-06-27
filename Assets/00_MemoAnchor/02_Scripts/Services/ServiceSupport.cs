using UnityEngine;
using UnityEngine.SceneManagement;

namespace MemoAnchor
{
    public static class ServiceSupport
    {
        private const string SERVICES_MANAGER_SCENE_NAME = "ServicesManager";
        private const string SERVICES_MANAGER_SCENE_PATH = "Assets/00_MemoAnchor/01_Scenes/ServicesManager.unity";

        private static bool _hasLoggedMissingServicesManagerInBuildSettings;
        private static bool _isLoadingServicesManager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            _isLoadingServicesManager = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureServicesManagerLoaded();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == SERVICES_MANAGER_SCENE_NAME)
            {
                _isLoadingServicesManager = false;
                UnloadDuplicateServicesManagerScenes();
                return;
            }

            EnsureServicesManagerLoaded();
        }

        private static void EnsureServicesManagerLoaded()
        {
            if (_isLoadingServicesManager)
            {
                return;
            }

            Scene servicesManagerScene = SceneManager.GetSceneByName(SERVICES_MANAGER_SCENE_NAME);
            if (servicesManagerScene.isLoaded)
            {
                UnloadDuplicateServicesManagerScenes();
                return;
            }

            if (SceneUtility.GetBuildIndexByScenePath(SERVICES_MANAGER_SCENE_PATH) < 0)
            {
                if (_hasLoggedMissingServicesManagerInBuildSettings)
                {
                    return;
                }

                _hasLoggedMissingServicesManagerInBuildSettings = true;
                return;
            }

            _hasLoggedMissingServicesManagerInBuildSettings = false;
            _isLoadingServicesManager = true;
            SceneManager.LoadSceneAsync(SERVICES_MANAGER_SCENE_NAME, LoadSceneMode.Additive);
        }

        private static void UnloadDuplicateServicesManagerScenes()
        {
            bool hasPrimaryServicesManagerScene = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.name != SERVICES_MANAGER_SCENE_NAME)
                {
                    continue;
                }

                if (!hasPrimaryServicesManagerScene)
                {
                    hasPrimaryServicesManagerScene = true;
                    continue;
                }

                SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
