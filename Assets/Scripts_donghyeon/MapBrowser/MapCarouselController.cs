using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MapCarouselController : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    [Header("Carousel Settings")]
    [Tooltip("슬라이드할 맵 이미지들 (마지막에 첫 이미지 복제본 추가하면 무한 루프)")]
    [SerializeField] private List<RectTransform> mapItems;

    [Tooltip("자동 슬라이드 간격 (초)")]
    [SerializeField] private float autoSlideInterval = 3f;

    [Tooltip("슬라이드 애니메이션 시간 (초)")]
    [SerializeField] private float slideDuration = 0.5f;

    [Tooltip("자동 슬라이드 활성화")]
    [SerializeField] private bool enableAutoSlide = true;

    [Tooltip("무한 루프 모드 (마지막에 첫 이미지 복제본이 있을 때)")]
    [SerializeField] private bool infiniteLoop = false;

    [Header("Indicators")]
    [Tooltip("인디케이터 점들")]
    [SerializeField] private List<Image> indicators;

    [Tooltip("활성 인디케이터 색상")]
    [SerializeField] private Color activeColor = Color.white;

    [Tooltip("비활성 인디케이터 색상")]
    [SerializeField] private Color inactiveColor = new Color(1f, 1f, 1f, 0.3f);

    [Header("Wiring")]
    [SerializeField] private RectTransform content;
    [SerializeField] private RectTransform viewport;

    [Header("Swipe Settings")]
    [Tooltip("스와이프 인식 최소 거리")]
    [SerializeField] private float swipeThreshold = 50f;

    private int currentIndex = 0;
    private float itemWidth;
    private Vector2 dragStartPos;
    private bool isDragging = false;
    private Coroutine autoSlideCoroutine;

    private void Start()
    {
        // 초기화
        if (mapItems.Count > 0)
        {
            itemWidth = viewport.rect.width + 50;
            UpdateCarousel(0, false);
        }

        // 자동 슬라이드 시작
        if (enableAutoSlide && mapItems.Count > 1)
        {
            StartAutoSlide();
        }
    }

    // 다음 슬라이드
    public void NextSlide()
    {
        if (infiniteLoop && currentIndex == mapItems.Count - 2)
        {
            // 무한 루프 모드: 마지막 실제 이미지에서 복제본으로 이동
            StartCoroutine(InfiniteLoopToClone());
        }
        else if (currentIndex < mapItems.Count - 1)
        {
            UpdateCarousel(currentIndex + 1, true);
        }
        else
        {
            // 일반 모드: 처음으로 점프
            UpdateCarousel(0, true);
        }
    }

    // 이전 슬라이드
    public void PreviousSlide()
    {
        if (currentIndex > 0)
        {
            UpdateCarousel(currentIndex - 1, true);
        }
        else if (infiniteLoop)
        {
            // 무한 루프 모드: 첫 번째에서 마지막 실제 이미지로
            int lastRealIndex = mapItems.Count - 2; // 복제본 제외한 마지막
            UpdateCarousel(lastRealIndex, true);
        }
        else
        {
            // 일반 모드: 마지막으로 점프
            UpdateCarousel(mapItems.Count - 1, true);
        }
    }

    // 특정 인덱스로 이동
    public void GoToSlide(int index)
    {
        if (index >= 0 && index < mapItems.Count)
        {
            UpdateCarousel(index, true);
        }
    }

    // 캐러셀 업데이트
    private void UpdateCarousel(int newIndex, bool animate)
    {
        currentIndex = newIndex;
        float targetX = -currentIndex * itemWidth;

        if (animate)
        {
            StopAllCoroutines();
            StartCoroutine(AnimateToPosition(targetX));
        }
        else
        {
            content.anchoredPosition = new Vector2(targetX, content.anchoredPosition.y);
        }

        UpdateIndicators();
    }

    // 부드러운 이동 애니메이션
    private IEnumerator AnimateToPosition(float targetX)
    {
        float startX = content.anchoredPosition.x;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            t = Mathf.SmoothStep(0f, 1f, t); // 부드러운 이징

            float newX = Mathf.Lerp(startX, targetX, t);
            content.anchoredPosition = new Vector2(newX, content.anchoredPosition.y);

            yield return null;
        }

        content.anchoredPosition = new Vector2(targetX, content.anchoredPosition.y);

        // 자동 슬라이드 재시작
        if (enableAutoSlide && mapItems.Count > 1)
        {
            StartAutoSlide();
        }
    }

    // 무한 루프: 마지막 이미지에서 복제본으로 자연스럽게 이동한 후 첫 번째로 순간이동
    private IEnumerator InfiniteLoopToClone()
    {
        // 마지막 실제 이미지에서 복제본(마지막 인덱스)으로 애니메이션
        int cloneIndex = mapItems.Count - 1;
        float startX = content.anchoredPosition.x;
        float targetX = -cloneIndex * itemWidth;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            t = Mathf.SmoothStep(0f, 1f, t);

            float newX = Mathf.Lerp(startX, targetX, t);
            content.anchoredPosition = new Vector2(newX, content.anchoredPosition.y);

            yield return null;
        }

        content.anchoredPosition = new Vector2(targetX, content.anchoredPosition.y);

        // 짧은 대기 후 즉시 첫 번째 위치로 순간이동 (사용자가 알아차리지 못하게)
        yield return new WaitForSeconds(0.05f);

        currentIndex = 0;
        content.anchoredPosition = new Vector2(0, content.anchoredPosition.y);
        UpdateIndicators();

        // 자동 슬라이드 재시작
        if (enableAutoSlide && mapItems.Count > 1)
        {
            StartAutoSlide();
        }
    }

    // 인디케이터 업데이트
    private void UpdateIndicators()
    {
        // 무한 루프 모드에서는 실제 이미지 개수(indicators.Count)만큼만 표시
        int displayIndex = currentIndex;

        // 복제본 이미지를 보고 있을 때는 첫 번째 인디케이터를 활성화
        if (infiniteLoop && currentIndex >= indicators.Count)
        {
            displayIndex = 0;
        }

        for (int i = 0; i < indicators.Count; i++)
        {
            if (indicators[i] != null)
            {
                indicators[i].color = (i == displayIndex) ? activeColor : inactiveColor;
            }
        }
    }

    // 자동 슬라이드
    private void StartAutoSlide()
    {
        if (autoSlideCoroutine != null)
        {
            StopCoroutine(autoSlideCoroutine);
        }
        autoSlideCoroutine = StartCoroutine(AutoSlideRoutine());
    }

    private IEnumerator AutoSlideRoutine()
    {
        yield return new WaitForSeconds(autoSlideInterval);
        NextSlide();
    }

    // 드래그 이벤트 처리
    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        dragStartPos = eventData.position;

        // 자동 슬라이드 중지
        if (autoSlideCoroutine != null)
        {
            StopCoroutine(autoSlideCoroutine);
        }
        StopAllCoroutines();
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 실시간 드래그 반응 (선택사항)
        if (isDragging)
        {
            float dragDelta = eventData.position.x - dragStartPos.x;
            float targetX = -currentIndex * itemWidth + dragDelta;
            content.anchoredPosition = new Vector2(targetX, content.anchoredPosition.y);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        isDragging = false;
        float dragDistance = eventData.position.x - dragStartPos.x;

        // 스와이프 방향 판단
        if (Mathf.Abs(dragDistance) > swipeThreshold)
        {
            if (dragDistance > 0)
            {
                PreviousSlide(); // 오른쪽으로 스와이프 = 이전
            }
            else
            {
                NextSlide(); // 왼쪽으로 스와이프 = 다음
            }
        }
        else
        {
            // 스와이프 거리가 부족하면 원래 위치로
            UpdateCarousel(currentIndex, true);
        }
    }
}