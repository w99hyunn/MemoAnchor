using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CustomDropdown : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField inputField;
    public GameObject dropdownPanel;
    public GameObject dropdownItemPrefab;
    public Transform dropdownContent;
    public Button toggleButton;

    [Header("Add New Option")]
    public TMP_InputField newOptionInput;
    public Button addOptionButton;

    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color hoverColor = new Color(0.3f, 0.6f, 1f, 1f); // 파란색
    public Color selectedColor = new Color(0.2f, 0.5f, 0.9f, 1f);

    private List<string> options = new List<string> { "건물", "공간" };
    private bool isOpen = false;
    private List<GameObject> dropdownItems = new List<GameObject>();

    void Start()
    {
        // 드롭다운 패널 초기 상태: 닫힘
        dropdownPanel.SetActive(false);

        // 토글 버튼 이벤트
        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(ToggleDropdown);
        }

        // 인풋 필드 클릭 이벤트
        if (inputField != null)
        {
            inputField.onSelect.AddListener((string text) => OpenDropdown());
        }

        // 새 옵션 추가 버튼 이벤트
        if (addOptionButton != null)
        {
            addOptionButton.onClick.AddListener(AddNewOption);
        }

        // Enter 키로 옵션 추가
        if (newOptionInput != null)
        {
            newOptionInput.onSubmit.AddListener((string text) => AddNewOption());
        }

        // 초기 드롭다운 아이템 생성
        RefreshDropdownItems();
    }

    void ToggleDropdown()
    {
        if (isOpen)
        {
            CloseDropdown();
        }
        else
        {
            OpenDropdown();
        }
    }

    void OpenDropdown()
    {
        isOpen = true;
        dropdownPanel.SetActive(true);
        RefreshDropdownItems();
    }

    void CloseDropdown()
    {
        isOpen = false;
        dropdownPanel.SetActive(false);
    }

    void RefreshDropdownItems()
    {
        // 기존 아이템 삭제
        foreach (GameObject item in dropdownItems)
        {
            Destroy(item);
        }
        dropdownItems.Clear();

        // 새 아이템 생성
        foreach (string option in options)
        {
            GameObject item = Instantiate(dropdownItemPrefab, dropdownContent);
            dropdownItems.Add(item);

            // 텍스트 설정
            TMP_Text itemText = item.GetComponentInChildren<TMP_Text>();
            if (itemText != null)
            {
                itemText.text = option;
            }

            // 버튼 설정
            Button itemButton = item.GetComponent<Button>();
            if (itemButton != null)
            {
                string optionValue = option; // 로컬 변수로 복사
                itemButton.onClick.AddListener(() => SelectOption(optionValue));

                // 호버 효과
                AddHoverEffect(itemButton);
            }
        }
    }

    void AddHoverEffect(Button button)
    {
        Image buttonImage = button.GetComponent<Image>();
        if (buttonImage == null) return;

        UnityEngine.EventSystems.EventTrigger trigger = button.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (trigger == null)
        {
            trigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        }

        // 마우스 올렸을 때
        UnityEngine.EventSystems.EventTrigger.Entry pointerEnter = new UnityEngine.EventSystems.EventTrigger.Entry();
        pointerEnter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
        pointerEnter.callback.AddListener((data) => { buttonImage.color = hoverColor; });
        trigger.triggers.Add(pointerEnter);

        // 마우스 나갔을 때
        UnityEngine.EventSystems.EventTrigger.Entry pointerExit = new UnityEngine.EventSystems.EventTrigger.Entry();
        pointerExit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
        pointerExit.callback.AddListener((data) => { buttonImage.color = normalColor; });
        trigger.triggers.Add(pointerExit);
    }

    void SelectOption(string option)
    {
        inputField.text = option;
        CloseDropdown();
    }

    void AddNewOption()
    {
        string newOption = newOptionInput.text.Trim();

        if (string.IsNullOrEmpty(newOption))
        {
            Debug.Log("옵션이 비어있습니다.");
            return;
        }

        if (options.Contains(newOption))
        {
            Debug.Log("이미 존재하는 옵션입니다.");
            return;
        }

        options.Add(newOption);
        newOptionInput.text = "";
        RefreshDropdownItems();

        Debug.Log($"새 옵션 추가됨: {newOption}");
    }

    // 외부에서 드롭다운을 닫기 위한 메서드 (배경 클릭 등)
    public void OnBackgroundClick()
    {
        CloseDropdown();
    }
}