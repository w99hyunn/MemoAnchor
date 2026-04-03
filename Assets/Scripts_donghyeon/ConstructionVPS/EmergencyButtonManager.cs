using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Emergency의 BtRoot 안 버튼들을 관리하는 매니저
/// 버튼을 클릭하면 색상이 변경되고, 1개만 선택 가능
/// </summary>
public class EmergencyButtonManager : MonoBehaviour
{
    [Header("Button References")]
    [Tooltip("BtRoot 안의 모든 긴급도 버튼들")]
    [SerializeField] private Button[] emergencyButtons;
    
    [Header("Colors")]
    [Tooltip("선택되지 않은 버튼 색상")]
    [SerializeField] private Color normalColor = Color.white;
    [Tooltip("선택된 버튼 색상 (#96CBE0)")]
    [SerializeField] private Color selectedColor = new Color(0x96 / 255f, 0xCB / 255f, 0xE0 / 255f); // #96CBE0
    
    [Header("Outline Colors")]
    [Tooltip("아무것도 선택하지 않았을 때 Outline 색상 (#96CBE0)")]
    [SerializeField] private Color outlineUnselectedColor = new Color(0x96 / 255f, 0xCB / 255f, 0xE0 / 255f); // #96CBE0
    [Tooltip("1개라도 선택되었을 때 Outline 색상 (#D9D9D9)")]
    [SerializeField] private Color outlineSelectedColor = new Color(0xD9 / 255f, 0xD9 / 255f, 0xD9 / 255f); // #D9D9D9
    
    [Header("Animation Settings")]
    [Tooltip("색상 전환 애니메이션 지속 시간")]
    [SerializeField] private float animationDuration = 0.2f;
    
    // 현재 선택된 버튼의 인덱스 (-1 = 선택 없음)
    private int selectedButtonIndex = -1;
    
    // 각 버튼의 Image 컴포넌트 캐싱
    private Image[] buttonImages;
    
    // 각 버튼의 Outline 컴포넌트 캐싱
    private Outline[] buttonOutlines;
    
    private void Start()
    {
        // 버튼들의 Image 및 Outline 컴포넌트 가져오기
        if (emergencyButtons != null && emergencyButtons.Length > 0)
        {
            buttonImages = new Image[emergencyButtons.Length];
            buttonOutlines = new Outline[emergencyButtons.Length];
            
            for (int i = 0; i < emergencyButtons.Length; i++)
            {
                if (emergencyButtons[i] != null)
                {
                    // 버튼의 자식 "Image" 오브젝트 찾기 (배경)
                    Transform imageTransform = emergencyButtons[i].transform.Find("Image");
                    if (imageTransform != null)
                    {
                        buttonImages[i] = imageTransform.GetComponent<Image>();
                        
                        // Outline 컴포넌트 가져오기 (없으면 추가)
                        buttonOutlines[i] = imageTransform.GetComponent<Outline>();
                        if (buttonOutlines[i] == null)
                        {
                            buttonOutlines[i] = imageTransform.gameObject.AddComponent<Outline>();
                            buttonOutlines[i].effectDistance = new Vector2(4, -4);
                        }
                    }
                    
                    // Image를 못 찾으면 버튼 자체의 Image 사용
                    if (buttonImages[i] == null)
                    {
                        buttonImages[i] = emergencyButtons[i].GetComponent<Image>();
                        
                        // Outline도 버튼 자체에서 가져오기
                        buttonOutlines[i] = emergencyButtons[i].GetComponent<Outline>();
                        if (buttonOutlines[i] == null)
                        {
                            buttonOutlines[i] = emergencyButtons[i].gameObject.AddComponent<Outline>();
                            buttonOutlines[i].effectDistance = new Vector2(4, -4);
                        }
                    }
                    
                    // 버튼 클릭 이벤트 연결
                    int index = i; // 클로저 문제 방지
                    emergencyButtons[i].onClick.AddListener(() => OnEmergencyButtonClicked(index));
                    
                    Debug.Log($"[EmergencyButtonManager] 버튼 {i} ({emergencyButtons[i].name}) 초기화 완료");
                }
            }
            
            // 모든 버튼을 초기 색상으로 설정
            UpdateAllButtonColors();
        }
        else
        {
            Debug.LogWarning("[EmergencyButtonManager] 긴급도 버튼이 할당되지 않았습니다!");
        }
    }
    
    // 긴급도 버튼 클릭 시
    private void OnEmergencyButtonClicked(int index)
    {
        if (index < 0 || index >= emergencyButtons.Length)
        {
            Debug.LogError($"[EmergencyButtonManager] 잘못된 버튼 인덱스: {index}");
            return;
        }
        
        // 같은 버튼을 다시 클릭하면 선택 해제
        if (selectedButtonIndex == index)
        {
            selectedButtonIndex = -1;
            Debug.Log($"[EmergencyButtonManager] 버튼 {index} 선택 해제");
        }
        else
        {
            selectedButtonIndex = index;
            Debug.Log($"[EmergencyButtonManager] 버튼 {index} 선택됨");
        }
        
        // 모든 버튼 색상 업데이트
        UpdateAllButtonColors();
    }
    
    // 모든 버튼의 색상 업데이트
    private void UpdateAllButtonColors()
    {
        if (buttonImages == null) return;
        
        // Outline 색상 결정: 아무것도 선택 안 됨 = #96CBE0, 1개라도 선택됨 = #D9D9D9
        Color targetOutlineColor = (selectedButtonIndex == -1) ? outlineUnselectedColor : outlineSelectedColor;
        
        for (int i = 0; i < buttonImages.Length; i++)
        {
            // 버튼 배경 색상 업데이트
            if (buttonImages[i] != null)
            {
                Color targetColor = (i == selectedButtonIndex) ? selectedColor : normalColor;
                
                // 애니메이션으로 색상 전환
                StartCoroutine(AnimateButtonColor(buttonImages[i], targetColor));
            }
            
            // Outline 색상 업데이트
            if (buttonOutlines != null && i < buttonOutlines.Length && buttonOutlines[i] != null)
            {
                StartCoroutine(AnimateOutlineColor(buttonOutlines[i], targetOutlineColor));
            }
        }
    }
    
    // 버튼 색상 애니메이션
    private System.Collections.IEnumerator AnimateButtonColor(Image image, Color targetColor)
    {
        Color fromColor = image.color;
        float elapsed = 0f;
        
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            // Ease-out 효과
            t = 1f - Mathf.Pow(1f - t, 2f);
            
            image.color = Color.Lerp(fromColor, targetColor, t);
            yield return null;
        }
        
        image.color = targetColor;
    }
    
    // Outline 색상 애니메이션
    private System.Collections.IEnumerator AnimateOutlineColor(Outline outline, Color targetColor)
    {
        Color fromColor = outline.effectColor;
        float elapsed = 0f;
        
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            // Ease-out 효과
            t = 1f - Mathf.Pow(1f - t, 2f);
            
            outline.effectColor = Color.Lerp(fromColor, targetColor, t);
            yield return null;
        }
        
        outline.effectColor = targetColor;
    }
    
    // 현재 선택된 버튼 인덱스 가져오기 (외부에서 호출 가능)
    public int GetSelectedButtonIndex()
    {
        return selectedButtonIndex;
    }
    
    // 특정 버튼을 선택 상태로 설정 (외부에서 호출 가능)
    public void SetSelectedButton(int index)
    {
        if (index < -1 || index >= emergencyButtons.Length)
        {
            Debug.LogError($"[EmergencyButtonManager] 잘못된 버튼 인덱스: {index}");
            return;
        }
        
        selectedButtonIndex = index;
        UpdateAllButtonColors();
        Debug.Log($"[EmergencyButtonManager] 버튼 {index} 강제 선택됨");
    }
    
    // 선택 해제 (외부에서 호출 가능)
    public void ClearSelection()
    {
        selectedButtonIndex = -1;
        UpdateAllButtonColors();
        Debug.Log($"[EmergencyButtonManager] 모든 선택 해제");
    }
    
    private void OnDestroy()
    {
        // 버튼 이벤트 정리
        if (emergencyButtons != null)
        {
            for (int i = 0; i < emergencyButtons.Length; i++)
            {
                if (emergencyButtons[i] != null)
                {
                    emergencyButtons[i].onClick.RemoveAllListeners();
                }
            }
        }
    }
}
