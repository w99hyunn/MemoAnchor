using UnityEngine;

public class AudioPlayer : MonoBehaviour
{
    // 스피커 역할을 할 컴포넌트
    public AudioSource audioSource;

    // 재생할 음성 파일들을 담는 리스트 (인스펙터에서 할당)
    public AudioClip[] audioClips;

    // 버튼에서 호출할 함수 (인덱스 번호로 실행)
    public void PlaySound(int index)
    {
        if (index >= 0 && index < audioClips.Length)
        {
            // 현재 재생 중인 소리를 멈추고 새 소리 재생
            audioSource.clip = audioClips[index];
            audioSource.Play();

            // 만약 소리가 겹치게 재생하고 싶다면 아래 코드를 쓰세요.
            // audioSource.PlayOneShot(audioClips[index]);
        }
        else
        {
            Debug.LogWarning("음성 파일 인덱스 범위를 확인해주세요!");
        }
    }
}