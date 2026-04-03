using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 이미지 메모 씬에서 이미지들을 표시
/// </summary>
public class ImageMemoViewer : MemoViewerBase
{
    [Header("Image Memo Specific")]
    [Tooltip("1개일 때 사용할 Image 오브젝트")]
    [SerializeField] private Image image1Solo;

    [Tooltip("2개일 때 사용할 첫 번째 Image")]
    [SerializeField] private Image image2First;

    [Tooltip("2개일 때 사용할 두 번째 Image")]
    [SerializeField] private Image image2Second;

    [Tooltip("3개일 때 사용할 첫 번째 Image")]
    [SerializeField] private Image image3First;

    [Tooltip("3개일 때 사용할 두 번째 Image")]
    [SerializeField] private Image image3Second;

    [Tooltip("3개일 때 사용할 세 번째 Image")]
    [SerializeField] private Image image3Third;

    [Tooltip("이미지 개수를 표시할 텍스트")]
    [SerializeField] private TMP_Text imageCountText;

    private List<Texture2D> loadedImages = new List<Texture2D>();

    protected override void Start()
    {
        base.Start();

        // 이미지 메모 전용 데이터 표시
        DisplayImageMemoData();
    }

    /// <summary>
    /// 이미지 메모 전용 데이터 표시
    /// </summary>
    private void DisplayImageMemoData()
    {
        if (currentMemoData == null)
        {
            Debug.LogWarning("[ImageMemoViewer] No memo data to display!");
            return;
        }

        // 이미지 경로 목록 확인
        if (currentMemoData.imagePaths == null || currentMemoData.imagePaths.Count == 0)
        {
            Debug.LogWarning("[ImageMemoViewer] No images found in memo data!");

            if (imageCountText != null)
            {
                imageCountText.text = "이미지 없음";
            }

            // 모든 이미지 오브젝트 비활성화
            HideAllImages();
            return;
        }

        // 최대 3개까지만 처리
        int imageCount = Mathf.Min(currentMemoData.imagePaths.Count, 3);

        // 이미지 개수 표시
        if (imageCountText != null)
        {
            imageCountText.text = $"이미지: {imageCount}개";
        }

        // 이미지 로드
        LoadImages(imageCount);

        // 개수에 따라 적절한 위치에 배치
        DisplayImagesAtFixedPositions(imageCount);

        if (verboseDebug)
        {
            Debug.Log($"[ImageMemoViewer] Loaded and displayed {loadedImages.Count} images");
        }
    }

    /// <summary>
    /// 이미지 파일들을 로드 (최대 개수 제한)
    /// </summary>
    private void LoadImages(int maxCount)
    {
        loadedImages.Clear();

        int loadCount = Mathf.Min(maxCount, currentMemoData.imagePaths.Count);

        for (int i = 0; i < loadCount; i++)
        {
            string imagePath = currentMemoData.imagePaths[i];

            if (string.IsNullOrEmpty(imagePath))
            {
                Debug.LogWarning("[ImageMemoViewer] Empty image path, skipping");
                continue;
            }

            // 전체 경로 생성
            string fullPath = imagePath;

            // 상대 경로인 경우 persistentDataPath와 결합
            if (!Path.IsPathRooted(imagePath))
            {
                fullPath = Path.Combine(Application.persistentDataPath, imagePath);
            }

            if (verboseDebug)
            {
                Debug.Log($"[ImageMemoViewer] Loading image from: {fullPath}");
            }

            // 파일 존재 확인
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[ImageMemoViewer] Image file not found: {fullPath}");
                continue;
            }

            // 이미지 로드
            Texture2D texture = LoadImageFromFile(fullPath);
            if (texture != null)
            {
                loadedImages.Add(texture);
            }
        }

        if (verboseDebug)
        {
            Debug.Log($"[ImageMemoViewer] Successfully loaded {loadedImages.Count}/{loadCount} images");
        }
    }

    /// <summary>
    /// 파일에서 이미지 로드
    /// </summary>
    private Texture2D LoadImageFromFile(string path)
    {
        try
        {
            byte[] imageData = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2);

            if (texture.LoadImage(imageData))
            {
                return texture;
            }
            else
            {
                Debug.LogError($"[ImageMemoViewer] Failed to load image data from: {path}");
                return null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ImageMemoViewer] Exception loading image: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 모든 이미지 오브젝트 숨기기
    /// </summary>
    private void HideAllImages()
    {
        if (image1Solo != null) image1Solo.gameObject.SetActive(false);
        if (image2First != null) image2First.gameObject.SetActive(false);
        if (image2Second != null) image2Second.gameObject.SetActive(false);
        if (image3First != null) image3First.gameObject.SetActive(false);
        if (image3Second != null) image3Second.gameObject.SetActive(false);
        if (image3Third != null) image3Third.gameObject.SetActive(false);
    }

    /// <summary>
    /// 개수에 따라 고정된 위치에 이미지 표시
    /// </summary>
    private void DisplayImagesAtFixedPositions(int imageCount)
    {
        // 먼저 모든 이미지 숨기기
        HideAllImages();

        switch (imageCount)
        {
            case 1:
                // 1개일 때: image1Solo에만 표시
                if (image1Solo != null && loadedImages.Count > 0)
                {
                    SetImageSprite(image1Solo, loadedImages[0]);
                    image1Solo.gameObject.SetActive(true);

                    if (verboseDebug)
                    {
                        Debug.Log("[ImageMemoViewer] Displaying 1 image at solo position");
                    }
                }
                break;

            case 2:
                // 2개일 때: image2First, image2Second에 표시
                if (image2First != null && loadedImages.Count > 0)
                {
                    SetImageSprite(image2First, loadedImages[0]);
                    image2First.gameObject.SetActive(true);
                }
                if (image2Second != null && loadedImages.Count > 1)
                {
                    SetImageSprite(image2Second, loadedImages[1]);
                    image2Second.gameObject.SetActive(true);
                }

                if (verboseDebug)
                {
                    Debug.Log("[ImageMemoViewer] Displaying 2 images at paired positions");
                }
                break;

            case 3:
                // 3개일 때: image3First, image3Second, image3Third에 표시
                if (image3First != null && loadedImages.Count > 0)
                {
                    SetImageSprite(image3First, loadedImages[0]);
                    image3First.gameObject.SetActive(true);
                }
                if (image3Second != null && loadedImages.Count > 1)
                {
                    SetImageSprite(image3Second, loadedImages[1]);
                    image3Second.gameObject.SetActive(true);
                }
                if (image3Third != null && loadedImages.Count > 2)
                {
                    SetImageSprite(image3Third, loadedImages[2]);
                    image3Third.gameObject.SetActive(true);
                }

                if (verboseDebug)
                {
                    Debug.Log("[ImageMemoViewer] Displaying 3 images at triple positions");
                }
                break;

            default:
                Debug.LogWarning($"[ImageMemoViewer] Unsupported image count: {imageCount}");
                break;
        }
    }

    /// <summary>
    /// Image 컴포넌트에 Texture2D를 Sprite로 변환해서 할당
    /// </summary>
    private void SetImageSprite(Image imageComponent, Texture2D texture)
    {
        if (imageComponent == null || texture == null) return;

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f)
        );

        imageComponent.sprite = sprite;

        if (verboseDebug)
        {
            Debug.Log($"[ImageMemoViewer] Set sprite for {imageComponent.name}");
        }
    }

    private void OnDestroy()
    {
        // 메모리 해제
        foreach (var texture in loadedImages)
        {
            if (texture != null)
            {
                Destroy(texture);
            }
        }
        loadedImages.Clear();
    }
}
