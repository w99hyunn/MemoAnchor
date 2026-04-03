
// AR 카메라 해상도를 720p로 강제 설정 (안정성 향상 목적)
using System.Collections;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ForceCameraConfig : MonoBehaviour
{
    [Header("ARCameraManager Cam")]
    [Tooltip("ARCameraManager컴포넌트를 넣는 자리")]
    [SerializeField] private ARCameraManager cam;

    // ARCameraManager 준비되면 해상도 강제 설정 시도
    private void OnEnable()
    {
        if (!cam)
        {
            // ArCameraManager컴포넌트 탐색
            cam = GetComponent<ARCameraManager>();   // 오브젝트에 있으면 그걸 우선 위함
            if (!cam) cam = FindObjectOfType<ARCameraManager>();
        }

        // AR 카메라 서브시스템이 준비되는 시점까지 기다렸다가 적용
        if (cam) StartCoroutine(ApplyWhenReady());
    }

    // AR 카메라 서브시스템이 준비될 때까지 대기 및 적용 함수
    private IEnumerator ApplyWhenReady()
    {
        // subsystem 준비될 때까지 대기
        while (cam.subsystem == null || !cam.subsystem.running)
            yield return null;

        // (구성 목록이 한두 프레임 뒤에 채워지는 경우가 있어) 재시도
        for (int tries = 0; tries < 30; tries++)
        {
            var configs = cam.GetConfigurations(Allocator.Temp);
            if (configs.IsCreated && configs.Length > 0)
            {
                TrySet720p(configs);
                configs.Dispose();
                yield break;
            }

            if (configs.IsCreated) configs.Dispose();
            yield return null;
        }
    }

    // 720p 해상도로 설정 시도 함수
    private void TrySet720p(NativeArray<XRCameraConfiguration> configs)
    {
        // 1280x720(또는 720x1280) 우선, 없으면 가장 낮은 해상도
        XRCameraConfiguration best = configs[0];
        bool found720 = false;

        for (int i = 0; i < configs.Length; i++)
        {
            var c = configs[i];

            bool is720p =
                (c.width == 1280 && c.height == 720) ||
                (c.width == 720 && c.height == 1280);

            if (is720p)
            {
                best = c;
                found720 = true;
                break;
            }

            // 720p 없으면 가장 낮은 해상도로 선택
            if (!found720 && (c.width * c.height) < (best.width * best.height))
                best = c;
        }

        // ARCameraManager에 선택한 해상도 적용
        cam.currentConfiguration = best;
    }
}
