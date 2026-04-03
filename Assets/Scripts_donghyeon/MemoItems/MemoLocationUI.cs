
// 메모 위치 아이템 UI 컴포넌트
// 맵 아이템과 메모 타이틀 사이의 중간 계층
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

public class MemoLocationUI : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("메모 위치가 표시될 텍스트 컴포넌트를 넣는 자리")]
    [SerializeField] private TMP_Text locationText;

    [SerializeField] private Button button;

    [Header("Color Elements")]
    [Tooltip("Dot 이미지 (색상 변경 대상)")]
    [SerializeField] private Image dotImage;

    [Header("Expansion Indicator")]
    [Tooltip("펼침/접힘 상태를 나타낼 아이콘 (선택사항)")]
    [SerializeField] private GameObject expandIcon;
    [SerializeField] private GameObject collapseIcon;

    [Header("Layout Settings")]
    [Tooltip("아이템의 최소 높이")]
    [SerializeField] private float minHeight = 138f;  // MapItem과 동일한 높이

    [Header("Child Container")]
    [Tooltip("메모 아이템들이 추가될 컨테이너 - 'ChildContainer' 이름으로 자동 검색")]
    [SerializeField] private GameObject childContainerObj;

    // 런타임에 캐시
    private RectTransform _childContainerCache;

    [Header("Separator")]
    [Tooltip("하단 구분선 (긴 선) - 선택사항")]
    [SerializeField] private GameObject separatorLong;

    // 색상 정의
    private static readonly Color CHECKED_COLOR = new Color(0x4C / 255f, 0x96 / 255f, 0xB3 / 255f); // #4C96B3
    private static readonly Color UNCHECKED_DOT_COLOR = new Color(0xED / 255f, 0xF5 / 255f, 0xFA / 255f); // #EDF5FA (Dot 배경)
    private static readonly Color UNCHECKED_TEXT_COLOR = new Color(0x79 / 255f, 0x79 / 255f, 0x79 / 255f); // #797979 (Text 및 Outline)

    // 생성된 프리팹에 저장되는 값
    private string _locationKey;    // 위치 그룹 키 (예: "위치_1", "위치_2")
    private string _locationDisplay; // 화면에 표시될 위치 정보

    // 클릭 시 실행할 함수를 저장
    private Action _onClick;

    // 자식 수 변화 감지용
    private int lastChildCount = -1;

    private void Awake()
    {
        // Dot 자동 검색 (재귀적으로 모든 하위 검색)
        if (dotImage == null)
        {
            // 먼저 직접 자식에서 찾기
            Transform dotTransform = transform.Find("Dot");

            // 직접 자식에 없으면 모든 하위에서 재귀적으로 찾기
            if (dotTransform == null)
            {
                dotTransform = FindChildRecursive(transform, "Dot");
            }

            if (dotTransform != null)
            {
                dotImage = dotTransform.GetComponent<Image>();
                if (dotImage != null)
                {
                    Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI] Awake: Dot 자동 검색 성공 (path: {GetGameObjectPath(dotTransform)})");
                }
                else
                {
                    Debug.LogWarning($"★★★ [ASSIGNEE] [MemoLocationUI] Awake: Dot 오브젝트를 찾았지만 Image 컴포넌트가 없습니다.");
                }
            }
            else
            {
                Debug.LogWarning($"★★★ [ASSIGNEE] [MemoLocationUI] Awake: 'Dot' 오브젝트를 찾을 수 없습니다. transform={transform.name}");
            }
        }

        // LocationText 자동 검색
        if (locationText == null)
        {
            locationText = GetComponentInChildren<TMP_Text>();
            if (locationText != null)
            {
                Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI] Awake: LocationText 자동 검색 성공");
            }
            else
            {
                Debug.LogWarning($"★★★ [ASSIGNEE] [MemoLocationUI] Awake: TMP_Text를 찾을 수 없습니다. transform={transform.name}");
            }
        }

        // ChildContainer 자동 검색 (Awake에서 먼저 실행)
        AutoFindChildContainer();

        // RectTransform 크기 설정 (Instantiate 직후에 호출되도록 Awake에서 실행)
        SetupRectTransform();
        SetupChildContainer();

        // Awake에서는 색상을 적용하지 않음 (Bind()에서 적용)
    }

    // 재귀적으로 자식 오브젝트 찾기
    private Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform found = FindChildRecursive(child, childName);
            if (found != null)
                return found;
        }
        return null;
    }

    // GameObject의 전체 경로 가져오기 (디버깅용)
    private string GetGameObjectPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }

    private void Start()
    {
        // 초기 높이 강제 설정
        UpdateHeight();
    }

    private void LateUpdate()
    {
        // ChildContainer의 자식 수가 변경되었을 때만 높이 업데이트
        RectTransform childContainer = GetChildContainer();
        if (childContainer != null && childContainer.childCount != lastChildCount)
        {
            lastChildCount = childContainer.childCount;
            UpdateHeight();
        }
    }

    // ChildContainer를 자동으로 찾는 함수
    private void AutoFindChildContainer()
    {
        // Inspector에서 설정되지 않았으면 자동 검색
        if (childContainerObj == null)
        {
            // "ChildContainer" 이름으로 자식 찾기
            Transform found = transform.Find("ChildContainer");
            if (found != null)
            {
                childContainerObj = found.gameObject;
                Debug.Log($"[MemoLocationUI] ChildContainer 자동 검색 성공: {gameObject.name}");
            }
            else
            {
                Debug.LogWarning($"[MemoLocationUI] ChildContainer를 찾을 수 없습니다: {gameObject.name}");
            }
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

        // Header를 찾아서 LayoutElement 설정
        Transform headerTransform = transform.Find("Header");
        if (headerTransform != null)
        {
            LayoutElement headerLayout = headerTransform.GetComponent<LayoutElement>();
            if (headerLayout == null)
            {
                headerLayout = headerTransform.gameObject.AddComponent<LayoutElement>();
            }
            headerLayout.ignoreLayout = false;
            headerLayout.minHeight = minHeight;
            headerLayout.preferredHeight = minHeight;
        }
    }

    // ChildContainer 설정
    private void SetupChildContainer()
    {
        if (childContainerObj == null)
        {
            Debug.LogWarning($"[MemoLocationUI] childContainerObj is null in {gameObject.name}");
            return;
        }

        RectTransform childContainer = childContainerObj.GetComponent<RectTransform>();
        if (childContainer == null)
        {
            Debug.LogError($"[MemoLocationUI] ChildContainer에 RectTransform이 없습니다: {gameObject.name}");
            return;
        }

        Debug.Log($"[MemoLocationUI] SetupChildContainer: {gameObject.name}");

        // Header를 찾아서 순서 설정
        Transform headerTransform = transform.Find("Header");
        if (headerTransform != null)
        {
            // Header가 첫 번째, ChildContainer가 두 번째
            headerTransform.SetSiblingIndex(0);
            childContainer.SetSiblingIndex(1);
            Debug.Log($"[MemoLocationUI] Header/ChildContainer 순서 설정 완료");

            // Header RectTransform 설정
            RectTransform headerRect = headerTransform as RectTransform;
            if (headerRect != null)
            {
                // Header LayoutElement 설정
                LayoutElement headerLayout = headerTransform.GetComponent<LayoutElement>();
                if (headerLayout == null)
                {
                    headerLayout = headerTransform.gameObject.AddComponent<LayoutElement>();
                }
                headerLayout.minHeight = minHeight;
                headerLayout.preferredHeight = minHeight;
            }
        }
        else
        {
            Debug.LogWarning($"[MemoLocationUI] Header를 찾을 수 없습니다: {gameObject.name}");
        }

        // LayoutElement 추가하여 부모 레이아웃에 포함
        LayoutElement containerLayout = childContainer.GetComponent<LayoutElement>();
        if (containerLayout == null)
        {
            containerLayout = childContainer.gameObject.AddComponent<LayoutElement>();
        }
        containerLayout.ignoreLayout = false;  // false로 설정하여 레이아웃에 포함
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
        verticalLayout.spacing = 0;  // 간격 제거
        verticalLayout.padding = new RectOffset(0, 0, 0, 0);  // 패딩 제거

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

    // 위치 정보를 바인딩
    public void Bind(string locationKey, string locationDisplay, Action onClick)
    {
        _locationKey = locationKey ?? "";
        _locationDisplay = locationDisplay ?? "";
        _onClick = onClick;

        ApplyText(_locationDisplay);
        WireButton(() =>
        {
            Debug.Log($"[MemoLocationUI] Clicked locationKey={_locationKey}");
            _onClick?.Invoke();
        });

        // Bind 호출 시에도 크기 재설정
        SetupRectTransform();
    }

    // UI 텍스트 갱신 함수
    private void ApplyText(string location)
    {
        if (locationText)
        {
            locationText.text = string.IsNullOrWhiteSpace(location) ? "(위치 정보 없음)" : location;
        }
    }

    // Toggle 상태에 따라 색상 적용 (하위 메모들의 isAssigned 확인)
    private void ApplyColorsBasedOnToggle()
    {
        Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI] 색상 적용 시작: locationKey={_locationKey}");

        // Dot과 Text가 있는지 확인 (없으면 검색)
        EnsureDotImageRenderable();
        EnsureLocationTextFound();

        // 하위 ChildContainer에서 MemoItemUI를 찾아서 isAssigned 확인
        // 하나라도 true이면 CHECKED_COLOR, 모두 false이면 UNCHECKED_COLOR
        bool hasAssigned = false;

        RectTransform childContainer = GetChildContainer();
        if (childContainer != null)
        {
            MemoItemUI[] memoItems = childContainer.GetComponentsInChildren<MemoItemUI>(true);
            Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI]   찾은 MemoItemUI 개수: {memoItems.Length}");

            foreach (var memoItem in memoItems)
            {
                bool itemAssigned = memoItem.GetIsAssigned();
                Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI]     MemoItem isAssigned: {itemAssigned}");

                if (itemAssigned)
                {
                    hasAssigned = true;
                    break;
                }
            }
        }
        else
        {
            Debug.LogWarning($"★★★ [ASSIGNEE] [MemoLocationUI]   ✗ childContainer가 null입니다!");
        }

        Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI]   hasAssigned={hasAssigned}");

        if (hasAssigned)
        {
            // 체크 O: Dot과 Text 모두 4C96B3, Outline 비활성화
            if (dotImage != null)
            {
                dotImage.color = CHECKED_COLOR;
                Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI]   ✓ Dot 색상 변경: {CHECKED_COLOR}");

                // 체크 시 Outline 비활성화
                var outline = dotImage.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.enabled = false;
                    Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI]   ✓ Outline 비활성화");
                }
            }
            else
            {
                Debug.LogWarning($"★★★ [ASSIGNEE] [MemoLocationUI]   ✗ dotImage가 null입니다!");
            }

            if (locationText != null)
            {
                locationText.color = CHECKED_COLOR;
                Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI]   ✓ Text 색상 변경: {CHECKED_COLOR}");
            }
            else
            {
                Debug.LogWarning($"★★★ [ASSIGNEE] [MemoLocationUI]   ✗ locationText가 null입니다!");
            }
        }
        else
        {
            // 체크 X: Dot은 EDF5FA (배경) + 797979 (Outline), Text는 797979
            if (dotImage != null)
            {
                dotImage.color = UNCHECKED_DOT_COLOR;
                Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI]   ✓ Dot 색상 변경: {UNCHECKED_DOT_COLOR}");

                // Outline 컴포넌트가 없으면 추가
                var outline = dotImage.GetComponent<Outline>();
                if (outline == null)
                {
                    outline = dotImage.gameObject.AddComponent<Outline>();
                    outline.effectDistance = new Vector2(1.5f, -1.5f);
                    Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI]   ✓ Outline 컴포넌트 생성됨");
                }

                // Outline 색상 설정
                outline.effectColor = UNCHECKED_TEXT_COLOR;
                outline.enabled = true;
                Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI]   ✓ Outline 색상 변경: {UNCHECKED_TEXT_COLOR}");
            }
            else
            {
                Debug.LogWarning($"★★★ [ASSIGNEE] [MemoLocationUI]   ✗ dotImage가 null입니다!");
            }

            if (locationText != null)
            {
                locationText.color = UNCHECKED_TEXT_COLOR;
                Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI]   ✓ Text 색상 변경: {UNCHECKED_TEXT_COLOR}");
            }
            else
            {
                Debug.LogWarning($"★★★ [ASSIGNEE] [MemoLocationUI]   ✗ locationText가 null입니다!");
            }
        }

        Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI] 색상 적용 완료: hasAssigned={hasAssigned}");
    }

    // dotImage가 null이면 다시 검색
    private void EnsureDotImageFound()
    {
        if (dotImage != null) return;

        // 직접 자식에서 찾기
        Transform dotTransform = transform.Find("Dot");

        // 직접 자식에 없으면 모든 하위에서 재귀적으로 찾기
        if (dotTransform == null)
        {
            dotTransform = FindChildRecursive(transform, "Dot");
        }

        if (dotTransform != null)
        {
            dotImage = dotTransform.GetComponent<Image>();
            if (dotImage != null)
            {
                Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI] EnsureDotImageFound: Dot 검색 성공");
            }
        }
    }

    // locationText가 null이면 다시 검색
    private void EnsureLocationTextFound()
    {
        if (locationText != null) return;

        locationText = GetComponentInChildren<TMP_Text>();
        if (locationText != null)
        {
            Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI] EnsureLocationTextFound: LocationText 검색 성공");
        }
    }

    // Dot Image가 렌더링 가능하도록 설정 (스프라이트 없이도 색상 표시)
    private void EnsureDotImageRenderable()
    {
        // 먼저 dotImage가 있는지 확인
        EnsureDotImageFound();

        if (dotImage == null) return;

        // 스프라이트가 없으면 Unity 내장 Knob 스프라이트 사용 (원형)
        if (dotImage.sprite == null)
        {
            Sprite knobSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            if (knobSprite != null)
            {
                dotImage.sprite = knobSprite;
                Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI]   ✓ Dot에 Knob 스프라이트 설정됨");
            }
            else
            {
                Sprite bgSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
                if (bgSprite != null)
                {
                    dotImage.sprite = bgSprite;
                    Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI]   ✓ Dot에 Background 스프라이트 설정됨");
                }
            }
        }
    }

    // 외부에서 색상을 업데이트할 때 사용 (ChildContainer에 메모가 추가된 후)
    public void UpdateColorsBasedOnChildren()
    {
        ApplyColorsBasedOnToggle();
    }

    // 초기 색상 설정 (메모가 아직 로드되지 않았을 때, 외부에서 isAssigned 여부를 전달)
    public void SetInitialColors(bool hasAssigned)
    {
        Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI] SetInitialColors: locationKey={_locationKey}, hasAssigned={hasAssigned}");

        // Dot과 Text가 있는지 확인 (없으면 검색)
        EnsureDotImageRenderable();
        EnsureLocationTextFound();

        if (hasAssigned)
        {
            // 체크 O: Dot과 Text 모두 4C96B3, Outline 비활성화
            if (dotImage != null)
            {
                dotImage.color = CHECKED_COLOR;
                var outline = dotImage.GetComponent<Outline>();
                if (outline != null) outline.enabled = false;
                Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI]   ✓ Dot 초기 색상 설정: {CHECKED_COLOR}");
            }

            if (locationText != null)
            {
                locationText.color = CHECKED_COLOR;
                Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI]   ✓ Text 초기 색상 설정: {CHECKED_COLOR}");
            }
        }
        else
        {
            // 체크 X: Dot은 EDF5FA (배경) + 797979 (Outline), Text는 797979
            if (dotImage != null)
            {
                dotImage.color = UNCHECKED_DOT_COLOR;

                // Outline 컴포넌트가 없으면 추가
                var outline = dotImage.GetComponent<Outline>();
                if (outline == null)
                {
                    outline = dotImage.gameObject.AddComponent<Outline>();
                    outline.effectDistance = new Vector2(1.5f, -1.5f);
                }
                outline.effectColor = UNCHECKED_TEXT_COLOR;
                outline.enabled = true;
                Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI]   ✓ Dot 초기 색상 설정: {UNCHECKED_DOT_COLOR} + Outline");
            }

            if (locationText != null)
            {
                locationText.color = UNCHECKED_TEXT_COLOR;
                Debug.Log($"★★★ [ASSIGNEE] [MemoLocationUI]   ✓ Text 초기 색상 설정: {UNCHECKED_TEXT_COLOR}");
            }
        }
    }

    // 버튼 클릭 동작 연결 함수
    private void WireButton(Action clickAction)
    {
        if (!button) button = GetComponent<Button>();

        if (!button)
        {
            Debug.LogError("[MemoLocationUI] Button reference is missing. Inspector에 Button을 넣거나 같은 오브젝트에 Button 컴포넌트를 추가하세요.");
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => clickAction?.Invoke());
    }

    // 펼침/접힘 상태 표시 (외부에서 호출)
    public void SetExpanded(bool expanded)
    {
        if (expandIcon != null)
            expandIcon.SetActive(!expanded);

        if (collapseIcon != null)
            collapseIcon.SetActive(expanded);

        // ChildContainer 표시/숨김
        if (childContainerObj != null)
        {
            RectTransform childContainer = childContainerObj.GetComponent<RectTransform>();
            if (childContainer != null)
            {
                bool hasChildren = childContainer.childCount > 0;
                childContainerObj.SetActive(expanded && hasChildren);

                // 표시 상태가 변경되면 높이 업데이트
                UpdateHeight();
            }
        }
    }

    // 하위 목록에 맞춰 LocationItem 높이 동적 조정
    private void UpdateHeight()
    {
        RectTransform childContainer = GetChildContainer();
        if (childContainer == null)
        {
            return;
        }

        // 자식이 있으면 ChildContainer 표시, 없으면 숨김
        bool hasChildren = childContainer.childCount > 0;
        bool activeStateChanged = childContainerObj != null && childContainerObj.activeSelf != hasChildren;

        if (activeStateChanged)
        {
            childContainerObj.SetActive(hasChildren);
        }

        // 순서 재확인 및 강제 설정 - Header 찾아서 설정
        Transform headerTransform = transform.Find("Header");
        if (headerTransform != null)
        {
            headerTransform.SetSiblingIndex(0);
            childContainer.SetSiblingIndex(1);
        }

        // 레이아웃 강제 갱신 - 자식 수가 변경되면 항상 갱신
        RebuildLayoutHierarchy();
    }

    // 레이아웃 체인 전체 갱신
    private void RebuildLayoutHierarchy()
    {
        // ChildContainer 레이아웃 갱신
        RectTransform childContainer = GetChildContainer();
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
    }

    // 한 프레임 뒤 레이아웃 재계산
    private System.Collections.IEnumerator DelayedLayoutRebuild()
    {
        yield return null;
        RebuildLayoutHierarchy();
    }

    // Button이 같은 GameObject에 있을 때 Header를 별도로 생성
    private void CreateHeaderSeparately()
    {
        if (button == null || button.gameObject != gameObject)
        {
            return; // 이미 분리되어 있음
        }

        Debug.Log($"[MemoLocationUI] CreateHeaderSeparately 시작: {gameObject.name}");

        // 새 Header GameObject 생성
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(transform, false);
        headerObj.transform.SetSiblingIndex(0); // 첫 번째로

        // RectTransform 설정
        RectTransform headerRect = headerObj.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.pivot = new Vector2(0.5f, 1);
        headerRect.sizeDelta = new Vector2(0, minHeight);
        headerRect.anchoredPosition = Vector2.zero;

        // Button 컴포넌트를 Header로 이동
        Button oldButton = button;
        Button newButton = headerObj.AddComponent<Button>();
        newButton.transition = oldButton.transition;
        newButton.colors = oldButton.colors;
        newButton.targetGraphic = oldButton.targetGraphic;

        // LocationText를 Header로 이동
        if (locationText != null && locationText.transform.parent == transform)
        {
            locationText.transform.SetParent(headerRect, false);
        }

        // 아이콘들도 Header로 이동
        if (expandIcon != null && expandIcon.transform.parent == transform)
        {
            expandIcon.transform.SetParent(headerRect, false);
        }
        if (collapseIcon != null && collapseIcon.transform.parent == transform)
        {
            collapseIcon.transform.SetParent(headerRect, false);
        }

        // 기존 Button 제거
        DestroyImmediate(oldButton);

        // 새 Button 참조
        button = newButton;

        Debug.Log($"[MemoLocationUI] CreateHeaderSeparately 완료: Header={headerObj.name}");
    }

    // 외부에서 정보를 가져올 수 있도록
    public string GetLocationKey() => _locationKey;
    public string GetLocationDisplay() => _locationDisplay;

    // 메모 아이템들이 추가될 childContainer 반환 (캐싱)
    public RectTransform GetChildContainer()
    {
        if (_childContainerCache != null)
            return _childContainerCache;

        // ChildContainer 자동 검색 (필요시)
        if (childContainerObj == null)
        {
            AutoFindChildContainer();
        }

        if (childContainerObj == null)
            return null;

        _childContainerCache = childContainerObj.GetComponent<RectTransform>();
        return _childContainerCache;
    }
}
