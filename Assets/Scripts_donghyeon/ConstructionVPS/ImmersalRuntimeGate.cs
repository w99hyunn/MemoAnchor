
// AR트래킹이 안정적일 때만 Immersal 컴포넌트들을 켜두도록 관리
using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;



public class ImmersalRuntimeGate : MonoBehaviour
{
    [Header("AR Foundation")]
    [Tooltip("CPU 이미지가 정상적으로 들어오는지 확인하기 위해 ARCameraManager를 넣는 자리")]
    [SerializeField] private ARCameraManager arCameraManager;

    [Header("components to toggle")]
    [Tooltip("카메라 CPU 이미지가 안정적으로 준비되면 켜고, 아니면 꺼둘 컴포넌트들을 넣는 자리")]
    [SerializeField] private Behaviour[] componentsToToggle;

    [Header("Timing")]
    [Tooltip("CPU 이미지 상태를 얼마나 자주 검사할지 시간을 적는 자리")]
    [SerializeField] private float checkIntervalSeconds = 0.2f;

    [Header("Stability")]
    [Tooltip("연속 성공이 이 횟수 이상이면 ON")]
    [SerializeField] private int consecutiveSuccessToEnable = 3;

    [Tooltip("연속 실패가 이 횟수 이상이면 OFF")]
    [SerializeField] private int consecutiveFailToDisable = 3;


    private bool _paused;        // 앱 일시정지 상태 여부 판단 위함
    private Coroutine _loopCo;   // 코루틴 실행 추척, 필요시 중지 위함

    private bool _isOn;                 // 현재 토글 상태 파악 위함
    private int _successStreak;         // 연속 성공 횟수 파악 위함
    private int _failStreak;            // 연속 실패 횟수 파악 위함


    // 컴포넌트 켜짐 시 처리
    private void OnEnable()
    {
        if (_loopCo != null) StopCoroutine(_loopCo);
        _loopCo = StartCoroutine(GateLoop());
    }

    // 컴포넌트 꺼짐 시 처리
    private void OnDisable()
    {
        if (_loopCo != null)
        {
            StopCoroutine(_loopCo);
            _loopCo = null;
        }

        // 토글 상태를 OFF로 적용
        ApplyToggle(false);
    }

    // 화면 전환 시 OFF 처리
    private void OnApplicationPause(bool pauseStatus)
    {
        _paused = pauseStatus;
        if (_paused)
        {
            _successStreak = 0;
            _failStreak = 0;
            ApplyToggle(false);
        }
    }

    // CPU 이미지 상태를 주기적으로 검사 (누적 렌더링 방지)
    private IEnumerator GateLoop()
    {
        // ARCameraManager가 지정되지 않았으면 씬에서 찾아 넣기
        if (!arCameraManager)
            arCameraManager = FindFirstObjectByType<ARCameraManager>();

        // 시작은 OFF
        _successStreak = 0;
        _failStreak = 0;
        ApplyToggle(false);

        // 검사 주기 설정 객체 생성
        var wait = new WaitForSeconds(checkIntervalSeconds);


        while (true)
        {
            bool validCpuImage = false; // CPU 이미지가 정상인지 판단 위함

            if (!_paused &&                                             // 일시정지 상태가 아니고
                ARSession.state == ARSessionState.SessionTracking &&    // 트래킹이 정상 작동 중이며
                arCameraManager &&                                      // ARCameraManager가 지정되어 있고
                arCameraManager.enabled)                                // ARCameraManager가 켜져 있을 때
            {
                // CPU 이미지가 정상 크기로 들어오는지 확인
                if (arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
                {
                    try
                    {
                        validCpuImage = (image.width > 0 && image.height > 0);
                    }
                    finally
                    {
                        image.Dispose();
                    }
                }
            }

            // 히스테리시스(연속 성공/실패)로 토글 흔들림 방지
            if (validCpuImage)
            {
                _successStreak++;
                _failStreak = 0;

                if (!_isOn && _successStreak >= consecutiveSuccessToEnable)
                {
                    ApplyToggle(true);
                }
            }
            else
            {
                _failStreak++;
                _successStreak = 0;

                if (_isOn && _failStreak >= consecutiveFailToDisable)
                {
                    ApplyToggle(false);
                }
            }

            // 지정한 시간 간격으로 검사
            yield return wait;
        }
    }

    // 컴포넌트 토글 적용
    private void ApplyToggle(bool on)
    {
        if (_isOn == on) return; // 상태 변화 있을 때만 적용하기 위함
        _isOn = on;

        if (componentsToToggle == null) return;
        foreach (var c in componentsToToggle)
        {
            if (c) c.enabled = on;
        }
    }
}
