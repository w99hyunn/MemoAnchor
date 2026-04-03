
// URP렌더 환경 카메라를 지정한 색상과 깊이로 강제 맞춤
// 프레임 잔상/깜빡임 제거 위함
// ⚠️ 주의: AR 카메라에는 사용하지 마세요! (카메라 피드가 가려짐)
using UnityEngine;
using UnityEngine.Rendering;

// 해당 클래스가 가장 먼저 실행되도록 설정
[DefaultExecutionOrder(-10000)]

public class ForceCameraClearURP : MonoBehaviour
{
    [Header("Render Cam")]
    [Tooltip("강제 설정할 카메라를 넣는 자리")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("지정 카메라의 Depth값을 초기화 할지")]
    [SerializeField] private bool clearDepth = true;

    [Tooltip("지정 카메라의 색상값을 초기화 할지 (AR 카메라는 false 권장)")]
    [SerializeField] private bool clearColor = false;

    [Tooltip("초기화 시 기본 색")]
    [SerializeField] private Color clearTo = Color.black;

    // targetCamera를 현재 오브젝트의 카메라로 설정
    private void Reset()
    {
        targetCamera = GetComponent<Camera>();
    }

    // 렌더링 시작 시점에 강제 설정 등록   
    private void OnEnable()
    {
        if (!targetCamera) targetCamera = GetComponent<Camera>();
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    // 강제 설정 해제
    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    // 렌더링 시작 시점에 강제 설정 실행 함수
    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        // 대상 카메라인지 판단
        if (!targetCamera) return;
        if (cam != targetCamera) return;

        // (지정한 값으로) 강제 초기화
        var cmd = CommandBufferPool.Get("Force Clear");
        cmd.ClearRenderTarget(clearDepth, clearColor, clearTo);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}
