
// 토글의 켜짐/꺼짐 상태에 따라 박스 및 아이콘 색상 변환
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AssigneeCheckToggleVisual : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("토글 넣는 자리")]
    [SerializeField] private Toggle toggle;

    [Tooltip("체크박스 배경(박스) Image 넣는 자리")]
    [SerializeField] private Image boxImage;

    [Tooltip("체크 아이콘 Image 넣는 자리")]
    [SerializeField] private Image checkIconImage;

    [Header("Colors")]
    // 토글 꺼졌을 때 색상
    [Tooltip("기본(OFF) 박스 색상 넣는 자리")]
    [SerializeField] private Color boxOffColor = Color.white;
    [Tooltip("기본(OFF) 아이콘 색상(검정) 넣는 자리")]
    [SerializeField] private Color iconOffColor = Color.black;

    // 토글 켜졌을 때 색상
    [Tooltip("ON일 때 박스 색상(하늘색) 넣는 자리")]
    [SerializeField] private Color boxOnColor = new Color(0.45f, 0.80f, 1.00f, 1.00f);
    [Tooltip("ON일 때 아이콘 색상(흰색) 넣는 자리")]
    [SerializeField] private Color iconOnColor = Color.white;

    // 토글 초기화 함수
    private void Reset()
    {
        toggle = GetComponent<Toggle>();
        if (toggle)
        {
            // 필요 컴포넌트 자동 찾기기
            var bg = toggle.transform.Find("Background");
            if (bg) boxImage = bg.GetComponent<Image>();
            var ck = toggle.transform.Find("Background/Checkmark");
            if (ck) checkIconImage = ck.GetComponent<Image>();
        }
    }

    // 이벤트 등록 및 초기 색상 적용
    private void Awake()
    {
        if (!toggle) toggle = GetComponent<Toggle>();
        if (toggle)
        {
            toggle.onValueChanged.RemoveListener(Apply);
            toggle.onValueChanged.AddListener(Apply);
        }
    }

    // Unity 2022.3+ Canvas 리빌드 호환성
    private void Start()
    {
        StartCoroutine(ApplyInitialStateAfterCanvasReady());
    }

    private IEnumerator ApplyInitialStateAfterCanvasReady()
    {
        // Canvas 리빌드 완전 완료 대기
        yield return null;
        yield return new WaitForEndOfFrame();

        Apply(toggle != null && toggle.isOn);
    }

    // 토글 상태에 따라 박스 및 아이콘 색상 변환 함수
    private void Apply(bool isOn)
    {
        if (boxImage) boxImage.color = isOn ? boxOnColor : boxOffColor;
        if (checkIconImage)
        {
            checkIconImage.color = isOn ? iconOnColor : iconOffColor;

            // 아이콘 항상 보여줌줌
            checkIconImage.enabled = true;
        }
    }
}
