
// UI 상단 Safe Area 확보 (또는 전체 화면 사용)
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SafeAreaFitter : MonoBehaviour
{
    [Header("Safe Area Settings")]
    [Tooltip("Safe Area 적용 (false면 전체 화면 사용)")]
    [SerializeField] private bool useSafeArea = true;

    [Tooltip("Safe Area 무시하고 항상 전체 화면 사용")]
    [SerializeField] private bool forceFullScreen = false;

    // 화면 데이터 저장 장소
    private RectTransform rt;
    private Rect lastSafeArea;
    private ScreenOrientation lastOrientation;

    // 초기 Safe Area 적용
    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    private void Start()
    {
        // Canvas 리빌드가 완료된 후 적용
        Apply();
    }

    // 변화 감지 > Safe Area 적용
    private void Update()
    {
        // 전체 화면 모드면 업데이트 불필요
        if (forceFullScreen || !useSafeArea)
            return;

        if (Screen.safeArea != lastSafeArea || Screen.orientation != lastOrientation)
            Apply();
    }

    // Safe Area 적용 함수
    private void Apply()
    {
        // 전체 화면 모드 또는 Safe Area 비활성화
        if (forceFullScreen || !useSafeArea)
        {
            ApplyFullScreen();
            return;
        }

        // 기기의 Safe Area 가져오기
        Rect sa = Screen.safeArea;        // OS에서 제공하는 Safe Area 정보 얻기 위함

        // 현재 화면 정보 저장 (변화 감지용)
        lastSafeArea = sa;
        lastOrientation = Screen.orientation;

        // Safe Area를 화면 비율로 변환
        Vector2 anchorMin = sa.position;
        Vector2 anchorMax = sa.position + sa.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        // RectTransform에 적용
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;

        // 오프셋 제거 (앵커 변경으로 인한 위치 변화 방지)
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // 전체 화면 적용 함수
    private void ApplyFullScreen()
    {
        // 전체 화면으로 설정 (0,0 ~ 1,1)
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;

        // 오프셋 제거
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Inspector에서 값 변경 시 즉시 적용 (에디터 전용)
    private void OnValidate()
    {
#if UNITY_EDITOR
        // 에디터 모드에서만 실행
        if (!Application.isPlaying)
        {
            if (rt == null)
                rt = GetComponent<RectTransform>();

            // Canvas 리빌드 루프 중이 아닐 때만 적용
            if (rt != null)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null && rt != null)
                        Apply();
                };
            }
        }
#endif
    }
}
