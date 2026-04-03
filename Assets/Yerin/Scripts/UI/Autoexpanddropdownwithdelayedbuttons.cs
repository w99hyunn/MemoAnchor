using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class AutoExpandDropdownWithDelayedButtons : MonoBehaviour
{
    [Header("Dropdown Reference")]
    public TMP_Dropdown dropdown;

    [Header("Auto-Expand UI")]
    public GameObject addNewPanel; // 새 옵션 추가 UI (처음엔 숨김)
    public TMP_InputField newOptionInput;
    public GameObject buttonGroup; // 확인/취소 버튼 그룹 (처음엔 숨김) ⭐
    public Button confirmButton;
    public Button cancelButton;

    [Header("Settings")]
    public string addNewOptionText = "+ 새 항목 추가"; // 드롭다운에 표시될 텍스트

    private List<string> userOptions = new List<string>(); // 사용자가 추가한 옵션들
    private int addNewOptionIndex; // "+ 새 항목 추가"의 인덱스
    private bool isAddingNew = false;

    void Start()
    {
        if (dropdown == null)
        {
            dropdown = GetComponent<TMP_Dropdown>();
        }

        // 처음에 추가 UI 숨기기
        if (addNewPanel != null)
        {
            addNewPanel.SetActive(false);
        }

        // 처음에 버튼 그룹 숨기기
        if (buttonGroup != null)
        {
            buttonGroup.SetActive(false);
        }

        // 초기 옵션 로드
        LoadInitialOptions();

        // 마지막에 "+ 새 항목 추가" 옵션 추가
        AddNewOptionButton();

        // 드롭다운 선택 이벤트
        dropdown.onValueChanged.AddListener(OnDropdownValueChanged);

        // 인풋 필드 이벤트 - 클릭하거나 입력 시작하면 버튼 표시 ⭐
        if (newOptionInput != null)
        {
            // 클릭 시
            newOptionInput.onSelect.AddListener((string text) => ShowButtons());

            // 텍스트 변경 시
            newOptionInput.onValueChanged.AddListener((string text) =>
            {
                if (!string.IsNullOrEmpty(text))
                {
                    ShowButtons();
                }
            });

            // Enter 키로 확인
            newOptionInput.onSubmit.AddListener((string text) => ConfirmNewOption());
        }

        // 확인/취소 버튼 이벤트
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(ConfirmNewOption);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(CancelNewOption);
        }
    }

    void LoadInitialOptions()
    {
        // 기존 드롭다운 옵션을 사용자 옵션으로 저장
        userOptions.Clear();
        foreach (TMP_Dropdown.OptionData option in dropdown.options)
        {
            userOptions.Add(option.text);
        }
    }

    void AddNewOptionButton()
    {
        // 마지막에 "+ 새 항목 추가" 옵션 추가
        dropdown.options.Add(new TMP_Dropdown.OptionData(addNewOptionText));
        addNewOptionIndex = dropdown.options.Count - 1;
        dropdown.RefreshShownValue();
    }

    void OnDropdownValueChanged(int index)
    {
        // "+ 새 항목 추가"를 선택했을 때
        if (index == addNewOptionIndex && !isAddingNew)
        {
            isAddingNew = true;
            ShowAddNewPanel();
        }
        else if (index < addNewOptionIndex)
        {
            // 일반 옵션 선택
            string selectedValue = dropdown.options[index].text;
            Debug.Log($"선택된 값: {selectedValue}");
        }
    }

    void ShowAddNewPanel()
    {
        if (addNewPanel != null)
        {
            addNewPanel.SetActive(true);

            // 입력 필드 초기화 및 포커스
            if (newOptionInput != null)
            {
                newOptionInput.text = "";
                newOptionInput.Select();
                newOptionInput.ActivateInputField();
            }

            // 버튼은 아직 숨김 ⭐
            if (buttonGroup != null)
            {
                buttonGroup.SetActive(false);
            }
        }
    }

    void ShowButtons()
    {
        // 인풋 클릭하거나 입력 시작하면 버튼 표시 ⭐
        if (buttonGroup != null && !buttonGroup.activeSelf)
        {
            buttonGroup.SetActive(true);
        }
    }

    void HideAddNewPanel()
    {
        if (addNewPanel != null)
        {
            addNewPanel.SetActive(false);
        }

        if (buttonGroup != null)
        {
            buttonGroup.SetActive(false);
        }

        isAddingNew = false;
    }

    void ConfirmNewOption()
    {
        if (newOptionInput == null) return;

        string newOption = newOptionInput.text.Trim();

        // 빈 값 체크
        if (string.IsNullOrEmpty(newOption))
        {
            Debug.Log("옵션이 비어있습니다.");
            CancelNewOption();
            return;
        }

        // 중복 체크
        if (userOptions.Contains(newOption))
        {
            Debug.Log("이미 존재하는 옵션입니다.");
            newOptionInput.text = "";
            return;
        }

        // 새 옵션을 "+ 새 항목 추가" 바로 앞에 삽입
        dropdown.options.Insert(addNewOptionIndex, new TMP_Dropdown.OptionData(newOption));
        userOptions.Add(newOption);

        // "+ 새 항목 추가"의 인덱스 업데이트
        addNewOptionIndex++;

        // 방금 추가한 옵션으로 선택 변경
        dropdown.value = addNewOptionIndex - 1;
        dropdown.RefreshShownValue();

        Debug.Log($"새 옵션 추가됨: {newOption}");

        // UI 숨기기
        HideAddNewPanel();
    }

    void CancelNewOption()
    {
        // 이전 선택값으로 되돌리기 (첫 번째 옵션)
        dropdown.value = 0;
        dropdown.RefreshShownValue();

        // UI 숨기기
        HideAddNewPanel();
    }

    // 선택된 값을 가져오는 메서드
    public string GetSelectedValue()
    {
        if (dropdown.value >= 0 && dropdown.value < addNewOptionIndex)
        {
            return dropdown.options[dropdown.value].text;
        }
        return "";
    }

    // 특정 값으로 설정
    public void SetDropdownValue(string value)
    {
        for (int i = 0; i < dropdown.options.Count; i++)
        {
            if (dropdown.options[i].text == value)
            {
                dropdown.value = i;
                dropdown.RefreshShownValue();
                return;
            }
        }
    }

    // 모든 사용자 옵션 가져오기
    public List<string> GetAllUserOptions()
    {
        return new List<string>(userOptions);
    }

    // 옵션 제거
    public void RemoveOption(string optionText)
    {
        for (int i = 0; i < dropdown.options.Count; i++)
        {
            if (i < addNewOptionIndex && dropdown.options[i].text == optionText)
            {
                dropdown.options.RemoveAt(i);
                userOptions.Remove(optionText);
                addNewOptionIndex--;
                dropdown.RefreshShownValue();
                break;
            }
        }
    }
}