using UnityEngine;
using UnityEngine.UI;

public class PopupManager : MonoBehaviour
{
    [Header("팝업 오브젝트들")]
    public GameObject[] popups; // 띄울 팝업창들을 리스트로 넣어주세요

    [Header("이미지를 띄울 타겟 UI")]
    public Image targetDisplayImage; // 팝업 안에 이미지가 나타날 곳

    // 모든 팝업을 닫는 함수
    public void CloseAllPopups()
    {
        foreach (GameObject popup in popups)
        {
            if (popup != null) popup.SetActive(false);
        }
    }

    // 특정 인덱스의 팝업을 띄우는 함수
    public void OpenPopup(int index)
    {
        CloseAllPopups(); // 다른 팝업이 열려있다면 닫기

        if (index >= 0 && index < popups.Length)
        {
            popups[index].SetActive(true);
        }
        else
        {
            Debug.LogWarning("팝업 인덱스 범위를 벗어났습니다!");
        }
    }

    public void OpenPopupWithButtonImage(GameObject clickedObj)
    {
        // 버튼 오브젝트에서 Image 컴포넌트를 찾고 스프라이트를 가져옵니다.
        Image btnImage = clickedObj.GetComponent<Image>();
        if (btnImage != null && targetDisplayImage != null)
        {
            targetDisplayImage.sprite = btnImage.sprite;
        }

    }
}
