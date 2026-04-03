
// 메모 아이템 UI 프리팹의 텍스트 생성 및 표시
// MemoListManager에서 메모 리스트를 생성할 때 사용
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class MemoItemUI : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("메모 제목이 표시될 텍스트 컴포넌트를 넣는 자리")]
    [SerializeField] private TMP_Text titleText;

    [SerializeField] private Button button;

    [Header("Color Elements")]
    [Tooltip("Dot 이미지 (색상 변경 대상)")]
    [SerializeField] private Image dotImage;

    [Header("Layout Settings")]
    [Tooltip("아이템의 최소 높이")]
    [SerializeField] private float minHeight = 138f;  // MapItem, LocationItem과 동일한 높이

    [Header("Separator")]
    [Tooltip("하단 구분선 (짧은 선) - 선택사항")]
    [SerializeField] private GameObject separatorShort;

    // 색상 정의
    private static readonly Color CHECKED_COLOR = new Color(0x4C / 255f, 0x96 / 255f, 0xB3 / 255f); // #4C96B3
    private static readonly Color UNCHECKED_DOT_COLOR = new Color(0xED / 255f, 0xF5 / 255f, 0xFA / 255f); // #EDF5FA
    private static readonly Color UNCHECKED_TEXT_COLOR = new Color(0x79 / 255f, 0x79 / 255f, 0x79 / 255f); // #797979

    // 생성된 프리팹에 저장되는 값
    private string _memoId;        // 메모 고유 ID
    private string _memoTitle;     // 메모 제목
    private bool _isAssigned;      // Assignee 체크 상태

    // 클릭 시 실행할 함수를 저장
    private Action _onClick;

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
                    Debug.Log($"★★★ [ASSIGNEE] [MemoItemUI] Awake: Dot 자동 검색 성공 (path: {GetGameObjectPath(dotTransform)})");
                }
                else
                {
                    Debug.LogWarning($"★★★ [ASSIGNEE] [MemoItemUI] Awake: Dot 오브젝트를 찾았지만 Image 컴포넌트가 없습니다.");
                }
            }
            else
            {
                Debug.LogWarning($"★★★ [ASSIGNEE] [MemoItemUI] Awake: 'Dot' 오브젝트를 찾을 수 없습니다. transform={transform.name}");
            }
        }

        // TitleText 자동 검색
        if (titleText == null)
        {
            titleText = GetComponentInChildren<TMP_Text>();
            if (titleText != null)
            {
                Debug.Log($"★★★ [ASSIGNEE] [MemoItemUI] Awake: TitleText 자동 검색 성공");
            }
            else
            {
                Debug.LogWarning($"★★★ [ASSIGNEE] [MemoItemUI] Awake: TMP_Text를 찾을 수 없습니다. transform={transform.name}");
            }
        }

        // RectTransform 크기 설정 (Instantiate 직후에 호출되도록 Awake에서 실행)
        SetupRectTransform();

        // Awake에서는 isAssigned가 아직 설정되지 않았으므로 색상 적용하지 않음
        // Bind()에서 색상 적용됨
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

    // RectTransform 크기를 자동으로 설정
    private void SetupRectTransform()
    {
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            // 최소 높이 설정
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, minHeight);
        }

        // LayoutElement 추가하여 높이 보장
        LayoutElement layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = gameObject.AddComponent<LayoutElement>();
        }
        layoutElement.minHeight = minHeight;
        layoutElement.preferredHeight = minHeight;
    }

    // 메모 타이틀 바인딩 (간단한 버전)
    public void Bind(string memoId, string title, bool isAssigned, Action onClick)
    {
        _memoId = memoId ?? "";
        _memoTitle = title ?? "";
        _isAssigned = isAssigned;
        _onClick = onClick;

        // Bind 시점에 dotImage가 null이면 다시 검색 (Awake보다 먼저 호출될 수 있음)
        if (dotImage == null)
        {
            Transform dotTransform = transform.Find("Dot");
            if (dotTransform == null)
            {
                dotTransform = FindChildRecursive(transform, "Dot");
            }
            if (dotTransform != null)
            {
                dotImage = dotTransform.GetComponent<Image>();
                Debug.Log($"★★★ [ASSIGNEE] [MemoItemUI] Bind: Dot 재검색 성공");
            }
        }

        ApplyText(_memoTitle);
        WireButton(() =>
        {
            Debug.Log($"[MemoItemUI] Clicked id={_memoId}, title={_memoTitle}");
            _onClick?.Invoke();
        });

        // Bind 호출 시에도 크기 재설정
        SetupRectTransform();

        // isAssigned 상태에 따라 색상 적용
        ApplyColorsBasedOnAssigned();
    }

    // UI 텍스트 갱신 함수
    private void ApplyText(string title)
    {
        if (titleText)
        {
            titleText.text = string.IsNullOrWhiteSpace(title) ? "(제목 없음)" : title;
        }
    }

    // isAssigned 상태에 따라 색상 적용
    private void ApplyColorsBasedOnAssigned()
    {
        Debug.Log($"★★★ [ASSIGNEE] [MemoItemUI] 색상 적용 시작: id={_memoId}, title={_memoTitle}, isAssigned={_isAssigned}");

        // Dot과 Text가 있는지 확인 (없으면 검색)
        EnsureDotImageRenderable();
        EnsureTitleTextFound();

        Debug.Log($"★★★ [ASSIGNEE] [MemoItemUI]   dotImage={dotImage != null}, titleText={titleText != null}");

        if (_isAssigned)
        {
            // 체크 O: Dot과 Text 모두 4C96B3, Outline 비활성화
            if (dotImage != null)
            {
                dotImage.color = CHECKED_COLOR;
                Debug.Log($"★★★ [ASSIGNEE] [MemoItemUI]   ✓ Dot 색상 변경: {CHECKED_COLOR}");

                // 체크 시 Outline 비활성화
                var outline = dotImage.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.enabled = false;
                    Debug.Log($"★★★ [ASSIGNEE] [MemoItemUI]   ✓ Outline 비활성화");
                }
            }
            else
            {
                Debug.LogWarning($"★★★ [ASSIGNEE] [MemoItemUI]   ✗ dotImage가 null입니다!");
            }

            if (titleText != null)
            {
                titleText.color = CHECKED_COLOR;
                Debug.Log($"★★★ [ASSIGNEE] [MemoItemUI]   ✓ Text 색상 변경: {CHECKED_COLOR}");
            }
            else
            {
                Debug.LogWarning($"★★★ [ASSIGNEE] [MemoItemUI]   ✗ titleText가 null입니다!");
            }
        }
        else
        {
            // 체크 X: Dot은 EDF5FA (배경) + 797979 (Outline), Text는 797979
            if (dotImage != null)
            {
                dotImage.color = UNCHECKED_DOT_COLOR;
                Debug.Log($"★★★ [ASSIGNEE] [MemoItemUI]   ✓ Dot 색상 변경: {UNCHECKED_DOT_COLOR}");

                // Outline 컴포넌트가 없으면 추가
                var outline = dotImage.GetComponent<Outline>();
                if (outline == null)
                {
                    outline = dotImage.gameObject.AddComponent<Outline>();
                    outline.effectDistance = new Vector2(1.5f, -1.5f);
                    Debug.Log($"★★★ [ASSIGNEE] [MemoItemUI]   ✓ Outline 컴포넌트 생성됨");
                }

                // Outline 색상 설정
                outline.effectColor = UNCHECKED_TEXT_COLOR;
                outline.enabled = true;
                Debug.Log($"★★★ [ASSIGNEE] [MemoItemUI]   ✓ Outline 색상 변경: {UNCHECKED_TEXT_COLOR}");
            }
            else
            {
                Debug.LogWarning($"★★★ [ASSIGNEE] [MemoItemUI]   ✗ dotImage가 null입니다!");
            }

            if (titleText != null)
            {
                titleText.color = UNCHECKED_TEXT_COLOR;
                Debug.Log($"★★★ [ASSIGNEE] [MemoItemUI]   ✓ Text 색상 변경: {UNCHECKED_TEXT_COLOR}");
            }
            else
            {
                Debug.LogWarning($"★★★ [ASSIGNEE] [MemoItemUI]   ✗ titleText가 null입니다!");
            }
        }

        Debug.Log($"★★★ [ASSIGNEE] [MemoItemUI] 색상 적용 완료: id={_memoId}, isAssigned={_isAssigned}");
    }

    // dotImage가 null이면 다시 검색
    private void EnsureDotImageFound()
    {
        if (dotImage != null) return;

        Transform dotTransform = transform.Find("Dot");
        if (dotTransform == null)
        {
            dotTransform = FindChildRecursive(transform, "Dot");
        }

        if (dotTransform != null)
        {
            dotImage = dotTransform.GetComponent<Image>();
            if (dotImage != null)
            {
                Debug.Log($"★★★ [ASSIGNEE] [MemoItemUI] EnsureDotImageFound: Dot 검색 성공");
            }
        }
    }

    // titleText가 null이면 다시 검색
    private void EnsureTitleTextFound()
    {
        if (titleText != null) return;

        titleText = GetComponentInChildren<TMP_Text>();
        if (titleText != null)
        {
            Debug.Log($"★★★ [ASSIGNEE] [MemoItemUI] EnsureTitleTextFound: TitleText 검색 성공");
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
            // Unity 내장 스프라이트 로드 시도
            Sprite knobSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            if (knobSprite != null)
            {
                dotImage.sprite = knobSprite;
                Debug.Log($"★★★ [ASSIGNEE] [MemoItemUI]   ✓ Dot에 Knob 스프라이트 설정됨");
            }
            else
            {
                // Knob이 없으면 Background 스프라이트 시도
                Sprite bgSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
                if (bgSprite != null)
                {
                    dotImage.sprite = bgSprite;
                    Debug.Log($"★★★ [ASSIGNEE] [MemoItemUI]   ✓ Dot에 Background 스프라이트 설정됨");
                }
                else
                {
                    Debug.LogWarning($"★★★ [ASSIGNEE] [MemoItemUI]   ✗ 내장 스프라이트를 찾을 수 없습니다!");
                }
            }
        }
    }

    // 버튼 클릭 동작 연결 함수
    private void WireButton(Action clickAction)
    {
        if (!button) button = GetComponent<Button>(); // 버튼 컴포넌트 자동 참조 위함

        if (!button)
        {
            Debug.LogError("[MemoItemUI] Button reference is missing. Inspector에 Button을 넣거나 같은 오브젝트에 Button 컴포넌트를 추가하세요.");
            return;
        }

        // 클릭 리스너 초기화 후 새 동작 연결
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => clickAction?.Invoke());
    }

    // 외부에서 메모 정보를 가져올 수 있도록
    public string GetMemoId() => _memoId;
    public string GetMemoTitle() => _memoTitle;
    public bool GetIsAssigned() => _isAssigned;
}
