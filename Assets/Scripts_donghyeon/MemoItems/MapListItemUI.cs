
// 맵 목록 리스트 UI프리팹의 텍스트 생성 및 저장
// MemoListManager 코드의 AddMapItemToUI 메서드를 UI 프리팹에 연결
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

public class MapListItemUI : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("맵 이름이 표시될 텍스트 컴포넌트를 넣는 자리")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subText;
    [SerializeField] private Button button;

    [Header("Expansion Indicator")]
    [Tooltip("펼침/접힘 상태를 나타낼 아이콘 (선택사항)")]
    [SerializeField] private GameObject expandIcon;
    [SerializeField] private GameObject collapseIcon;

    [Header("Layout Settings")]
    [Tooltip("아이템의 최소 높이")]
    [SerializeField] private float minHeight = 138f;

    [Header("Child Container")]
    [SerializeField] private RectTransform childContainer;
    [SerializeField] private Image backgroundImage;

    [Header("Background")]
    [Tooltip("아이템 배경 프리팹")]
    [SerializeField] private GameObject backgroundPrefab;

    public RectTransform GetChildContainer() => childContainer;
    // 생성된 프리팹에 저장되는 값
    private int _mapIdInt;
    private string _mapIdStr;
    private string _mapName;

    // 클릭시 실행할 함수를 저장
    private Action _onClickSimple;

    private int lastChildCount = -1;

    // 배경 인스턴스
    private GameObject backgroundInstance;

    private void Awake()
    {
        Debug.Log($"[MapListItemUI] Awake() - {gameObject.name}");
        // RectTransform 크기 설정 (Instantiate 직후에 호출되도록 Awake에서 실행)
        SetupRectTransform();
        SetupChildContainer();
        CreateBackground();
    }

    private void Start()
    {
        // 초기 높이 강제 설정
        UpdateHeight();
    }

    private void LateUpdate()
    {
        // ChildContainer의 자식 수가 변경되었을 때만 높이 업데이트
        if (childContainer != null && childContainer.childCount != lastChildCount)
        {
            lastChildCount = childContainer.childCount;
            UpdateHeight();
        }
    }

    // RectTransform 크기를 자동으로 설정
    private void SetupRectTransform()
    {
        // LayoutElement 추가하여 동적 높이 조정 가능하도록
        LayoutElement layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = gameObject.AddComponent<LayoutElement>();
        }
        layoutElement.minHeight = minHeight;
        layoutElement.preferredHeight = -1;  // -1로 설정하여 자동 계산되도록 함
        layoutElement.flexibleHeight = -1;

        // VerticalLayoutGroup 추가하여 Header와 ChildContainer를 수직 배치
        VerticalLayoutGroup verticalLayout = GetComponent<VerticalLayoutGroup>();
        if (verticalLayout == null)
        {
            verticalLayout = gameObject.AddComponent<VerticalLayoutGroup>();
        }
        verticalLayout.childControlHeight = false;
        verticalLayout.childControlWidth = true;
        verticalLayout.childForceExpandHeight = false;
        verticalLayout.childForceExpandWidth = true;
        verticalLayout.spacing = 0;
        verticalLayout.padding = new RectOffset(0, 0, 0, 0);
        verticalLayout.childAlignment = TextAnchor.UpperCenter;

        // ContentSizeFitter 추가하여 자식들의 높이에 맞춤
        ContentSizeFitter sizeFitter = GetComponent<ContentSizeFitter>();
        if (sizeFitter == null)
        {
            sizeFitter = gameObject.AddComponent<ContentSizeFitter>();
        }
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // BackgroundImage에 LayoutElement 추가하여 레이아웃에 영향주지 않도록
        if (backgroundImage != null)
        {
            LayoutElement bgLayout = backgroundImage.GetComponent<LayoutElement>();
            if (bgLayout == null)
            {
                bgLayout = backgroundImage.gameObject.AddComponent<LayoutElement>();
            }
            bgLayout.ignoreLayout = true;
        }

        // Header(button이 있는 GameObject)에 LayoutElement 추가
        if (button != null && button.gameObject != gameObject)
        {
            LayoutElement headerLayout = button.gameObject.GetComponent<LayoutElement>();
            if (headerLayout == null)
            {
                headerLayout = button.gameObject.AddComponent<LayoutElement>();
            }
            headerLayout.ignoreLayout = false;
            headerLayout.minHeight = minHeight;
            headerLayout.preferredHeight = minHeight;
        }
    }

    // ChildContainer 설정
    private void SetupChildContainer()
    {
        if (childContainer == null)
        {
            Debug.LogWarning($"[MapListItemUI] childContainer is null in {gameObject.name}");
            return;
        }

        Debug.Log($"[MapListItemUI] SetupChildContainer: {gameObject.name}");
        Debug.Log($"[MapListItemUI] Before - Header siblingIndex: {(button != null && button.gameObject != gameObject ? button.transform.GetSiblingIndex() : -1)}, ChildContainer siblingIndex: {childContainer.GetSiblingIndex()}");

        // ChildContainer가 Header 다음에 오도록 순서 강제
        if (button != null && button.gameObject != gameObject)
        {
            // Header가 첫 번째, ChildContainer가 두 번째
            button.transform.SetSiblingIndex(0);
            childContainer.SetSiblingIndex(1);

            Debug.Log($"[MapListItemUI] After - Header siblingIndex: {button.transform.GetSiblingIndex()}, ChildContainer siblingIndex: {childContainer.GetSiblingIndex()}");

            // RectTransform 상세 정보 출력
            RectTransform headerRect = button.transform as RectTransform;
            Debug.Log($"[MapListItemUI] Header RectTransform: anchoredPosition={headerRect.anchoredPosition}, sizeDelta={headerRect.sizeDelta}, localPosition={headerRect.localPosition}");
            Debug.Log($"[MapListItemUI] ChildContainer RectTransform: anchoredPosition={childContainer.anchoredPosition}, sizeDelta={childContainer.sizeDelta}, localPosition={childContainer.localPosition}");
        }
        else
        {
            Debug.LogWarning($"[MapListItemUI] button is null or same GameObject: button={(button != null ? button.name : "null")}");
        }

        // ChildContainer의 배경 이미지는 프리팹에서 설정됨 (코드에서는 설정하지 않음)

        // LayoutElement 추가하여 부모 레이아웃에 포함
        LayoutElement containerLayout = childContainer.GetComponent<LayoutElement>();
        if (containerLayout == null)
        {
            containerLayout = childContainer.gameObject.AddComponent<LayoutElement>();
        }
        containerLayout.ignoreLayout = false;  // false로 변경하여 레이아웃에 포함
        containerLayout.minHeight = 0;
        containerLayout.preferredHeight = -1;  // 자동 계산
        containerLayout.flexibleHeight = -1;

        // VerticalLayoutGroup 추가하여 자식들을 수직으로 배치
        VerticalLayoutGroup verticalLayout = childContainer.GetComponent<VerticalLayoutGroup>();
        if (verticalLayout == null)
        {
            verticalLayout = childContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        }
        verticalLayout.childControlHeight = false;
        verticalLayout.childControlWidth = true;
        verticalLayout.childForceExpandHeight = false;
        verticalLayout.childForceExpandWidth = true;
        verticalLayout.spacing = 5;
        verticalLayout.padding = new RectOffset(20, 20, 10, 10);

        // ContentSizeFitter 추가하여 자식들의 높이에 맞춤
        ContentSizeFitter sizeFitter = childContainer.GetComponent<ContentSizeFitter>();
        if (sizeFitter == null)
        {
            sizeFitter = childContainer.gameObject.AddComponent<ContentSizeFitter>();
        }
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // ChildContainer가 보이지 않도록 초기화 (자식이 없을 때)
        childContainer.gameObject.SetActive(childContainer.childCount > 0);
    }

    // 하위 목록에 맞춰 MapItemPrefab 높이 동적 조정
    private void UpdateHeight()
    {
        if (childContainer == null)
        {
            Debug.LogWarning($"[MapListItemUI] UpdateHeight: childContainer is null - {gameObject.name}");
            return;
        }

        // 자식이 있으면 ChildContainer 표시, 없으면 숨김
        bool hasChildren = childContainer.childCount > 0;
        bool activeStateChanged = childContainer.gameObject.activeSelf != hasChildren;

        if (activeStateChanged)
        {
            childContainer.gameObject.SetActive(hasChildren);
            Debug.Log($"[MapListItemUI] UpdateHeight: {gameObject.name} - ChildContainer active={hasChildren}, childCount={childContainer.childCount}");
        }

        // 순서 재확인 및 강제 설정
        if (button != null && button.gameObject != gameObject)
        {
            button.transform.SetSiblingIndex(0);
            childContainer.SetSiblingIndex(1);
        }

        // 레이아웃 강제 갱신 - 자식 수가 변경되면 항상 갱신
        RebuildLayoutHierarchy();
    }

    // 레이아웃 체인 전체 갱신
    private void RebuildLayoutHierarchy()
    {
        // ChildContainer 레이아웃 갱신
        if (childContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(childContainer);
        }

        // 자신의 레이아웃 갱신
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);

        // 부모 체인 전체 레이아웃 갱신
        Transform parent = transform.parent;
        while (parent != null)
        {
            RectTransform parentRect = parent as RectTransform;
            if (parentRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            }
            parent = parent.parent;
        }

        Canvas.ForceUpdateCanvases();

        // 배경 크기 업데이트
        UpdateBackground();
    }

    // 배경 생성
    private void CreateBackground()
    {
        if (backgroundPrefab == null)
        {
            Debug.LogWarning($"[MapListItemUI] backgroundPrefab이 설정되지 않았습니다 - {gameObject.name}");
            return;
        }

        // 배경 인스턴스를 자신의 자식으로 생성
        backgroundInstance = Instantiate(backgroundPrefab, transform);
        backgroundInstance.name = "Background";

        // 배경을 맨 처음에 배치 (다른 자식들 뒤에 렌더링)
        backgroundInstance.transform.SetAsFirstSibling();

        // LayoutElement 추가하여 레이아웃 계산에서 제외
        LayoutElement bgLayout = backgroundInstance.GetComponent<LayoutElement>();
        if (bgLayout == null)
        {
            bgLayout = backgroundInstance.AddComponent<LayoutElement>();
        }
        bgLayout.ignoreLayout = true;

        // RectTransform 설정 - 전체 영역을 덮도록
        RectTransform bgRect = backgroundInstance.GetComponent<RectTransform>();
        if (bgRect != null)
        {
            bgRect.anchorMin = new Vector2(0, 0);
            bgRect.anchorMax = new Vector2(1, 1);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
        }

        // 배경 Image 설정
        UnityEngine.UI.Image bgImage = backgroundInstance.GetComponent<UnityEngine.UI.Image>();
        if (bgImage != null)
        {
            bgImage.raycastTarget = false;
        }

        // Sibling Index 설정
        // Background(0) -> ChildContainer(1) -> Header(2)
        if (childContainer != null)
        {
            childContainer.SetSiblingIndex(1);
        }

        Transform headerTransform = button != null ? button.transform : null;
        if (headerTransform != null && headerTransform.parent == transform)
        {
            // Header를 맨 마지막으로 설정 (가장 앞에 렌더링)
            headerTransform.SetAsLastSibling();

            // Header에 Canvas 추가 (렌더링 순서 강제)
            Canvas headerCanvas = headerTransform.GetComponent<Canvas>();
            if (headerCanvas == null)
            {
                headerCanvas = headerTransform.gameObject.AddComponent<Canvas>();
                headerCanvas.overrideSorting = true;
                headerCanvas.sortingOrder = 10; // 배경(0)보다 훨씬 높게

                // GraphicRaycaster 추가 (버튼 클릭 유지)
                if (headerTransform.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                {
                    headerTransform.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                }

                Debug.Log($"[MapListItemUI] Header Canvas added: sortingOrder={headerCanvas.sortingOrder}");
            }
        }

        Debug.Log($"[MapListItemUI] 배경 생성 완료: {backgroundInstance.name}, siblingIndex={backgroundInstance.transform.GetSiblingIndex()}");
    }

    // 배경 크기 업데이트 (stretch 앵커를 사용하므로 자동으로 조정됨)
    private void UpdateBackground()
    {
        // stretch 앵커로 설정했으므로 별도 업데이트 불필요
        // 부모 크기가 변경되면 자동으로 따라감
    }

    // 한 프레임 뒤 레이아웃 재계산
    private System.Collections.IEnumerator DelayedLayoutRebuild()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }

    // 맵 데이터 바인딩 (int 버전)
    public void Bind(int mapId, string mapName, Action onClick)
    {
        _mapIdInt = mapId;
        _mapIdStr = mapId.ToString();
        _mapName = mapName ?? "";
        _onClickSimple = onClick;

        ApplyTexts(_mapName, _mapIdStr);
        WireButton(() =>
        {
            Debug.Log($"[MapListItemUI] Clicked id={_mapIdInt}, name={_mapName}");
            _onClickSimple?.Invoke();
        });

        SetupRectTransform();
    }

    // UI 텍스트 갱신
    private void ApplyTexts(string name, string idStr)
    {
        if (titleText) titleText.text = string.IsNullOrEmpty(name) ? "(Unnamed Map)" : name;
        if (subText) subText.text = $"Map ID: {idStr}";
    }

    // 버튼 클릭 동작 연결
    private void WireButton(Action clickAction)
    {
        if (!button) button = GetComponent<Button>();

        if (!button)
        {
            Debug.LogError("[MapListItemUI] Button reference is missing.");
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => clickAction?.Invoke());
    }

    // 펼침/접힘 상태 표시
    public void SetExpanded(bool expanded)
    {
        if (expandIcon != null)
            expandIcon.SetActive(!expanded);

        if (collapseIcon != null)
            collapseIcon.SetActive(expanded);
    }
}
