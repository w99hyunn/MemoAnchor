using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ParticipantDropdownController : MonoBehaviour
{
    [Header("Dropdown Reference")]
    public TMP_Dropdown dropdown;

    [Header("Add New Option UI")]
    public TMP_InputField newOptionInput;
    public Button addButton;

    [Header("Input Field (선택값 표시용)")]
    public TMP_InputField displayInputField; // 선택한 값을 보여줄 인풋필드 (선택사항)

    private List<string> currentOptions = new List<string>();

    void Start()
    {
        if (dropdown == null)
        {
            dropdown = GetComponent<TMP_Dropdown>();
        }

        // 초기 옵션 불러오기
        LoadInitialOptions();

        // 드롭다운 선택 이벤트
        dropdown.onValueChanged.AddListener(OnDropdownValueChanged);

        // 새 옵션 추가 버튼 이벤트
        if (addButton != null)
        {
            addButton.onClick.AddListener(AddNewOption);
        }

        // Enter 키로 옵션 추가
        if (newOptionInput != null)
        {
            newOptionInput.onSubmit.AddListener((string text) => AddNewOption());
        }
    }

    void LoadInitialOptions()
    {
        // 기존 드롭다운 옵션을 리스트에 저장
        currentOptions.Clear();
        foreach (TMP_Dropdown.OptionData option in dropdown.options)
        {
            currentOptions.Add(option.text);
        }
    }

    void OnDropdownValueChanged(int index)
    {
        string selectedValue = dropdown.options[index].text;
        Debug.Log($"선택된 값: {selectedValue}");

        // 선택값을 다른 InputField에 표시하고 싶다면
        if (displayInputField != null)
        {
            displayInputField.text = selectedValue;
        }
    }

    public void AddNewOption()
    {
        if (newOptionInput == null) return;

        string newOption = newOptionInput.text.Trim();

        // 빈 값 체크
        if (string.IsNullOrEmpty(newOption))
        {
            Debug.Log("옵션이 비어있습니다.");
            return;
        }

        // 중복 체크
        if (currentOptions.Contains(newOption))
        {
            Debug.Log("이미 존재하는 옵션입니다.");
            newOptionInput.text = "";
            return;
        }

        // 드롭다운에 옵션 추가
        dropdown.options.Add(new TMP_Dropdown.OptionData(newOption));
        currentOptions.Add(newOption);

        // 드롭다운 새로고침
        dropdown.RefreshShownValue();

        // 입력 필드 초기화
        newOptionInput.text = "";

        Debug.Log($"새 옵션 추가됨: {newOption}");
    }

    // 선택된 값을 가져오는 메서드
    public string GetSelectedValue()
    {
        if (dropdown.value >= 0 && dropdown.value < dropdown.options.Count)
        {
            return dropdown.options[dropdown.value].text;
        }
        return "";
    }

    // 프로그래밍 방식으로 옵션 설정
    public void SetDropdownValue(string value)
    {
        for (int i = 0; i < dropdown.options.Count; i++)
        {
            if (dropdown.options[i].text == value)
            {
                dropdown.value = i;
                return;
            }
        }
    }

    // 모든 옵션 초기화
    public void ClearAllOptions()
    {
        dropdown.ClearOptions();
        currentOptions.Clear();
    }

    // 특정 옵션 제거
    public void RemoveOption(string optionText)
    {
        for (int i = 0; i < dropdown.options.Count; i++)
        {
            if (dropdown.options[i].text == optionText)
            {
                dropdown.options.RemoveAt(i);
                currentOptions.Remove(optionText);
                dropdown.RefreshShownValue();
                break;
            }
        }
    }
}