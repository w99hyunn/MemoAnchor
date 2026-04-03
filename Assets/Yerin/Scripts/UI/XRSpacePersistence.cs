using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// XR Space (3D 맵)를 씬 전환 시에도 유지하고 각 씬의 지정된 위치로 이동
/// XR Space_1 오브젝트에 붙여서 사용
/// </summary>
public class XRSpacePersistence : MonoBehaviour
{
    [Header("XR Space Settings")]
    [Tooltip("각 씬에서 XR Space가 이동할 위치의 오브젝트 이름")]
    [SerializeField] private string targetPositionObjectName = "XRSpaceTarget";

    [Tooltip("이 씬들에서는 위치 이동하지 않음 (AR 씬 등)")]
    [SerializeField] private string[] doNotMoveInScenes = new string[] { "MeetingScene" };

    [Tooltip("이 씬들에서는 XR Space 완전히 제거 (Home, 메뉴 씬 등)")]
    [SerializeField] private string[] destroyInScenes = new string[] { "Home" };

    [Header("Debug")]
    [Tooltip("디버그 로그 출력")]
    [SerializeField] private bool verboseDebug = true;

    private static XRSpacePersistence instance;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Transform originalParent;

    private void Awake()
    {
        // 싱글톤 패턴
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            // 원래 위치 저장 (AR 씬 위치)
            originalPosition = transform.position;
            originalRotation = transform.rotation;
            originalParent = transform.parent;

            if (verboseDebug)
            {
                Debug.Log($"[XRSpacePersistence] XR Space will persist across scenes");
                Debug.Log($"[XRSpacePersistence] Original position saved: {originalPosition}");
            }

            // 씬 로드 이벤트 구독
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            if (verboseDebug)
            {
                Debug.Log("[XRSpacePersistence] Duplicate XR Space destroyed");
            }
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 씬이 로드될 때마다 호출
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (verboseDebug)
        {
            Debug.Log($"[XRSpacePersistence] Scene loaded: {scene.name}, mode: {mode}");
        }

        // 삭제해야 할 씬인지 체크
        if (destroyInScenes != null)
        {
            foreach (string sceneName in destroyInScenes)
            {
                if (scene.name == sceneName)
                {
                    if (verboseDebug)
                    {
                        Debug.Log($"[XRSpacePersistence] '{scene.name}' is in destroy list. Destroying XR Space.");
                    }
                    Destroy(gameObject); // XR Space 완전히 제거
                    return;
                }
            }
        }

        // 이동하지 않을 씬인지 체크
        if (doNotMoveInScenes != null)
        {
            foreach (string sceneName in doNotMoveInScenes)
            {
                if (scene.name == sceneName)
                {
                    if (verboseDebug)
                    {
                        Debug.Log($"[XRSpacePersistence] '{scene.name}' is in do-not-move list. Staying at current position.");
                    }
                    return; // 이동하지 않음
                }
            }
        }

        // 새 씬에서 타겟 위치 찾기
        // 짧은 지연 후 실행 (씬이 완전히 로드되도록)
        StartCoroutine(MoveToTargetPositionDelayed(scene));
    }

    /// <summary>
    /// 씬 로드 완료 후 위치 이동 (코루틴)
    /// </summary>
    private System.Collections.IEnumerator MoveToTargetPositionDelayed(Scene scene)
    {
        // 한 프레임 대기 (씬이 완전히 활성화되도록)
        yield return null;

        MoveToTargetPosition(scene);
    }

    /// <summary>
    /// 각 씬의 지정된 위치로 XR Space 이동
    /// </summary>
    private void MoveToTargetPosition(Scene scene)
    {
        // 씬의 모든 오브젝트에서 타겟 찾기 (자식 포함)
        GameObject[] rootObjects = scene.GetRootGameObjects();
        GameObject targetObject = null;

        foreach (GameObject root in rootObjects)
        {
            // 각 루트에서 이름으로 자식까지 재귀 검색
            Transform found = FindInChildren(root.transform, targetPositionObjectName);
            if (found != null)
            {
                targetObject = found.gameObject;
                break;
            }
        }

        if (targetObject != null)
        {
            // 타겟 위치로 이동
            transform.position = targetObject.transform.position;
            transform.rotation = targetObject.transform.rotation;

            // 타겟의 부모가 있으면 같은 부모 사용
            if (targetObject.transform.parent != null)
            {
                transform.SetParent(targetObject.transform.parent);
            }

            // 타겟 오브젝트는 비활성화 (placeholder였으므로)
            targetObject.SetActive(false);

            if (verboseDebug)
            {
                Debug.Log($"[XRSpacePersistence] Moved to target position in scene: {scene.name}");
                Debug.Log($"[XRSpacePersistence] New position: {transform.position}");
            }
        }
        else
        {
            if (verboseDebug)
            {
                Debug.LogWarning($"[XRSpacePersistence] Target '{targetPositionObjectName}' not found in scene: {scene.name}");
                Debug.Log("[XRSpacePersistence] XR Space will stay at current position");
            }
        }
    }

    /// <summary>
    /// 자식 오브젝트까지 재귀적으로 검색
    /// </summary>
    private Transform FindInChildren(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;

        foreach (Transform child in parent)
        {
            Transform found = FindInChildren(child, name);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// AR 씬으로 돌아갈 때 원래 위치로 복원
    /// </summary>
    public void ResetToOriginalPosition()
    {
        transform.position = originalPosition;
        transform.rotation = originalRotation;
        transform.SetParent(originalParent);

        if (verboseDebug)
        {
            Debug.Log("[XRSpacePersistence] Reset to original AR scene position");
        }
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}