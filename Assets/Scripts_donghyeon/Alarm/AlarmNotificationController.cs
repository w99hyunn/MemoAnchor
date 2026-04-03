using System.Collections;
using UnityEngine;
using TMPro;

public class AlarmNotificationController : MonoBehaviour
{
    [Header("알림 이미지들 (순서대로)")]
    [SerializeField] private GameObject[] alarmImages;  // Alarm1, Alarm2, Alarm3, Alarm4
    
    [Header("StateText (첫 알림 시 사라짐)")]
    [SerializeField] private TMP_Text stateText;                 // StateText 연결
    [SerializeField] private float stateTextFadeDuration = 0.3f; // StateText 페이드 아웃 시간
    
    [Header("타이밍 설정")]
    [SerializeField] private float delayBeforeStart = 0.5f;      // 씬 시작 후 첫 알림까지 대기
    [SerializeField] private float delayBetweenAlarms = 0.8f;    // 알림 간 간격
    
    [Header("애니메이션 설정")]
    [SerializeField] private float animationDuration = 0.3f;     // 애니메이션 시간
    [SerializeField] private float startScale = 0.8f;            // 시작 크기 (1 = 100%)

    private void Start()
    {
        // 모든 알림 비활성화
        foreach (var alarm in alarmImages)
        {
            if (alarm != null)
                alarm.SetActive(false);
        }
        
        // 순차적으로 알림 표시
        StartCoroutine(ShowAlarmsSequentially());
    }

    private IEnumerator ShowAlarmsSequentially()
    {
        yield return new WaitForSeconds(delayBeforeStart);

        for (int i = 0; i < alarmImages.Length; i++)
        {
            if (alarmImages[i] != null)
            {
                // 첫 번째 알림일 때 StateText 페이드 아웃
                if (i == 0 && stateText != null)
                {
                    StartCoroutine(FadeOutStateText());
                }
                
                // 알림 활성화 및 애니메이션
                StartCoroutine(ShowWithAnimation(alarmImages[i]));
                
                // 다음 알림까지 대기
                yield return new WaitForSeconds(delayBetweenAlarms);
            }
        }
    }

    private IEnumerator FadeOutStateText()
    {
        float elapsed = 0f;
        Color originalColor = stateText.color;
        
        while (elapsed < stateTextFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / stateTextFadeDuration;
            
            // 알파값 감소
            stateText.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f - t);
            
            yield return null;
        }
        
        // 완전히 투명하게 하고 비활성화
        stateText.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
        stateText.gameObject.SetActive(false);
    }

    private IEnumerator ShowWithAnimation(GameObject alarm)
    {
        RectTransform rect = alarm.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = alarm.GetComponent<CanvasGroup>();
        
        // 시작 상태 설정
        if (rect != null)
            rect.localScale = Vector3.one * startScale;
        
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        
        // 활성화
        alarm.SetActive(true);
        
        // 애니메이션
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            
            // Ease Out Back (살짝 튀어나왔다가 들어가는 효과)
            float easeT = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);
            
            // 스케일 애니메이션
            if (rect != null)
            {
                float scale = Mathf.Lerp(startScale, 1f, easeT);
                rect.localScale = Vector3.one * scale;
            }
            
            // 페이드 인 (선형)
            if (canvasGroup != null)
                canvasGroup.alpha = t;
            
            yield return null;
        }
        
        // 최종 상태 확정
        if (rect != null)
            rect.localScale = Vector3.one;
        
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }
}
