
// 핀이 표시에 필요한 요소(아이콘, 툴팁, 제목 텍스트)들 참조
using TMPro;
using UnityEngine;

public class PinVisualRefs : MonoBehaviour
{
    [Header("Visual Refs")]
    [Tooltip("IconCanvas 넣는 자리")]
    public GameObject iconCanvas;

    [Tooltip("TooltipCanvas 넣는 자리")]
    public GameObject tooltipCanvas;

    [Tooltip("TooltipCanvas 안의 타이틀 TMP_Text 넣는 자리 (없어도 자동으로 찾음)")]
    public TMP_Text titleText;
}
