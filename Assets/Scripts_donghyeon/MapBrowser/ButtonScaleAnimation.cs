
// 버튼 클릭 시 스케일 애니메이션 스크립트
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonScaleAnimation : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Scale Settings")]
    [Tooltip("버튼을 눌렀을 때 크기 (1.0이 원래 크기)")]
    [SerializeField] private float pressedScale = 0.95f;

    [Tooltip("버튼에 마우스를 올렸을 때 크기")]
    [SerializeField] private float hoverScale = 1.05f;

    [Tooltip("원래 크기")]
    [SerializeField] private float normalScale = 1.0f;

    [Header("Animation Settings")]
    [Tooltip("애니메이션 속도")]
    [SerializeField] private float animationSpeed = 10f;

    [Tooltip("마우스 오버 효과 사용")]
    [SerializeField] private bool useHoverEffect = true;

    [Tooltip("클릭 효과 사용")]
    [SerializeField] private bool useClickEffect = true;

    private Button button;
    private RectTransform rectTransform;
    private Vector3 targetScale;
    private Coroutine scaleCoroutine;
    private bool isHovering = false;

    private void Awake()
    {
        button = GetComponent<Button>();
        rectTransform = GetComponent<RectTransform>();
        targetScale = Vector3.one * normalScale;
    }

    private void OnEnable()
    {
        // Canvas 리빌드 후 초기 스케일 설정 (Unity 2022.3+ 호환성)
        StartCoroutine(InitializeScaleAfterCanvasReady());
    }

    private IEnumerator InitializeScaleAfterCanvasReady()
    {
        // Canvas 리빌드 완전 완료 대기
        yield return null;
        yield return new WaitForEndOfFrame();

        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one * normalScale;
            targetScale = Vector3.one * normalScale;
        }
    }

    private void Update()
    {
        // 부드럽게 타겟 스케일로 이동
        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.Lerp(
                rectTransform.localScale,
                targetScale,
                Time.deltaTime * animationSpeed
            );
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!useClickEffect || button == null || !button.interactable) return;

        // 클릭 시 작아지는 효과
        targetScale = Vector3.one * pressedScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!useClickEffect || button == null || !button.interactable) return;

        // 마우스를 떼면 원래 크기로 (또는 호버 크기로)
        targetScale = Vector3.one * (isHovering && useHoverEffect ? hoverScale : normalScale);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!useHoverEffect || button == null || !button.interactable) return;

        isHovering = true;
        // 마우스가 버튼 위에 있을 때 약간 커지는 효과
        targetScale = Vector3.one * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!useHoverEffect || button == null) return;

        isHovering = false;
        // 마우스가 벗어나면 원래 크기로
        targetScale = Vector3.one * normalScale;
    }

    // 외부에서 호출할 수 있는 펄스 효과 (선택적)
    public void PlayPulseEffect()
    {
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(PulseEffect());
    }

    private IEnumerator PulseEffect()
    {
        float duration = 0.2f;
        float elapsed = 0f;
        Vector3 originalScale = rectTransform.localScale;

        // 커지기
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float scale = Mathf.Lerp(normalScale, hoverScale, elapsed / duration);
            rectTransform.localScale = Vector3.one * scale;
            yield return null;
        }

        elapsed = 0f;

        // 작아지기
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float scale = Mathf.Lerp(hoverScale, normalScale, elapsed / duration);
            rectTransform.localScale = Vector3.one * scale;
            yield return null;
        }

        rectTransform.localScale = Vector3.one * normalScale;
        targetScale = Vector3.one * normalScale;
    }
}
