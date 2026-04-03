// AR 세션 및 관련 컴포넌트 정리
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ARSessionCleaner : MonoBehaviour
{
    [Header("Reset Element")]
    [Tooltip("정리할 ARSession 컴포넌트")]
    [SerializeField] private ARSession arSession;

    [Tooltip("TabPinCreate 상태 초기화")]
    [SerializeField] private TabPinCreate tabPinCreate;

    [Tooltip("정리 시 비활성화할 컴포넌트들")]
    [SerializeField] private MonoBehaviour[] componentsToDisable;

    [Header("Debug")]
    [Tooltip("디버그 로그 출력")]
    [SerializeField] private bool showDebugLogs = true;

    // 모든 정리 작업 실행
    public void CleanupAll()
    {
        Debug.Log("★★★ [ARSessionCleaner] CleanupAll 시작 ★★★");

        if (showDebugLogs)
        {
            Debug.Log("[ARSessionCleaner] CleanupAll() 시작");
            Debug.Log($"[ARSessionCleaner] arSession={(arSession != null ? "연결됨" : "NULL")}, tabPinCreate={(tabPinCreate != null ? "연결됨" : "NULL")}");
        }

        DisableComponents();
        ResetARSession();
        ResetPinState();

        Debug.Log("★★★ [ARSessionCleaner] CleanupAll 완료 - 다음 씬 진입 시 메모 복원 준비됨 ★★★");

        if (showDebugLogs)
            Debug.Log("[ARSessionCleaner] CleanupAll() 완료");
    }

    // AR 세션 리셋
    public void ResetARSession()
    {
        if (arSession)
        {
            arSession.Reset();
            Debug.Log("[ARSessionCleaner] ARSession.Reset()");
        }
    }

    // 핀/메모 복원 상태 초기화
    public void ResetPinState()
    {
        if (tabPinCreate)
        {
            tabPinCreate.ResetRestorationState();
            Debug.Log("[ARSessionCleaner] TabPinCreate.ResetRestorationState()");
        }
    }

    // 지정된 컴포넌트들 비활성화
    public void DisableComponents()
    {
        if (componentsToDisable != null)
        {
            foreach (var mb in componentsToDisable)
            {
                if (mb)
                {
                    mb.enabled = false;
                    Debug.Log($"[ARSessionCleaner] Disabled: {mb.GetType().Name}");
                }
            }
        }
    }
}
