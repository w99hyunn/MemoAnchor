using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 각 모서리별로 독립적인 반경을 설정할 수 있는 UI Image 컴포넌트
/// </summary>
[RequireComponent(typeof(Image))]
[ExecuteAlways]
public class RoundedCornersImage : MonoBehaviour
{
    [Header("Corner Radius")]
    [SerializeField] private float radiusTopLeft = 0f;
    [SerializeField] private float radiusTopRight = 0f;
    [SerializeField] private float radiusBottomRight = 0f;
    [SerializeField] private float radiusBottomLeft = 0f;
    
    [Header("Shader Reference (Optional)")]
    [Tooltip("셰이더를 직접 할당하면 빌드에서 더 안정적으로 동작합니다")]
    [SerializeField] private Shader roundedCornersShader;
    
    private Image image;
    private Material materialInstance;
    private RectTransform rectTransform;
    
    // 셰이더 프로퍼티 ID 캐싱
    private static readonly int RadiusTLId = Shader.PropertyToID("_RadiusTL");
    private static readonly int RadiusTRId = Shader.PropertyToID("_RadiusTR");
    private static readonly int RadiusBRId = Shader.PropertyToID("_RadiusBR");
    private static readonly int RadiusBLId = Shader.PropertyToID("_RadiusBL");
    private static readonly int WidthId = Shader.PropertyToID("_Width");
    private static readonly int HeightId = Shader.PropertyToID("_Height");
    
    // 전역 셰이더 캐시 (AddComponent로 생성된 인스턴스용)
    private static Shader cachedShader = null;
    
    private void Awake()
    {
        Initialize();
    }
    
    private void OnEnable()
    {
        Initialize();
        UpdateMaterial();
    }
    
    private void OnDisable()
    {
        CleanupMaterial();
    }
    
    private void OnDestroy()
    {
        CleanupMaterial();
    }
    
    private void OnRectTransformDimensionsChange()
    {
        UpdateMaterial();
    }
    
    private void OnValidate()
    {
        // 에디터에서 값 변경 시 업데이트
        if (Application.isPlaying || !Application.isEditor)
            return;
            
        Initialize();
        UpdateMaterial();
    }
    
    private void Initialize()
    {
        if (image == null)
            image = GetComponent<Image>();
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();
    }
    
    private void CleanupMaterial()
    {
        if (materialInstance != null)
        {
            if (Application.isPlaying)
                Destroy(materialInstance);
            else
                DestroyImmediate(materialInstance);
            materialInstance = null;
        }
        
        if (image != null)
            image.material = null;
    }
    
    /// <summary>
    /// 셰이더를 찾는 함수 - 여러 방법으로 시도
    /// </summary>
    private Shader FindShader()
    {
        // 1. SerializeField로 직접 할당된 셰이더
        if (roundedCornersShader != null)
            return roundedCornersShader;
        
        // 2. 전역 캐시된 셰이더
        if (cachedShader != null)
            return cachedShader;
        
        // 3. Shader.Find로 검색
        Shader shader = Shader.Find("UI/RoundedCornersIndependent");
        if (shader != null)
        {
            cachedShader = shader;
            return shader;
        }
        
        // 4. Resources 폴더에서 로드 시도
        shader = Resources.Load<Shader>("Shaders/UIRoundedCornersIndependent");
        if (shader != null)
        {
            cachedShader = shader;
            return shader;
        }
        
        return null;
    }
    
    /// <summary>
    /// 외부에서 셰이더를 설정 (AssigneeDropdownManager에서 사용)
    /// </summary>
    public static void SetGlobalShader(Shader shader)
    {
        if (shader != null)
            cachedShader = shader;
    }
    
    private void UpdateMaterial()
    {
        Initialize();
        
        if (image == null || rectTransform == null)
            return;
        
        // 셰이더 로드
        Shader shader = FindShader();
        if (shader == null)
        {
            Debug.LogWarning("[RoundedCornersImage] 셰이더를 찾을 수 없습니다. 빌드에 포함되지 않았을 수 있습니다. " +
                           "Project Settings > Graphics > Always Included Shaders에 'UI/RoundedCornersIndependent' 셰이더를 추가하세요. " +
                           "기본 UI 머티리얼로 폴백합니다.");
            // 셰이더를 찾지 못하면 기본 UI 머티리얼 사용 (흰색 화면 방지)
            if (image.material != null && image.material != image.defaultMaterial)
            {
                image.material = null; // 기본 머티리얼로 복원
            }
            return;
        }
        
        // 머티리얼 인스턴스 생성
        if (materialInstance == null)
        {
            materialInstance = new Material(shader);
            materialInstance.name = "RoundedCorners (Instance)";
        }
        
        // 프로퍼티 설정
        Rect rect = rectTransform.rect;
        materialInstance.SetFloat(RadiusTLId, radiusTopLeft);
        materialInstance.SetFloat(RadiusTRId, radiusTopRight);
        materialInstance.SetFloat(RadiusBRId, radiusBottomRight);
        materialInstance.SetFloat(RadiusBLId, radiusBottomLeft);
        materialInstance.SetFloat(WidthId, rect.width);
        materialInstance.SetFloat(HeightId, rect.height);
        
        // Image에 머티리얼 적용
        image.material = materialInstance;
    }
    
    /// <summary>
    /// 모든 모서리의 반경을 동일하게 설정
    /// </summary>
    public void SetRadius(float radius)
    {
        SetRadius(radius, radius, radius, radius);
    }
    
    /// <summary>
    /// 각 모서리의 반경을 개별적으로 설정
    /// </summary>
    public void SetRadius(float topLeft, float topRight, float bottomRight, float bottomLeft)
    {
        radiusTopLeft = topLeft;
        radiusTopRight = topRight;
        radiusBottomRight = bottomRight;
        radiusBottomLeft = bottomLeft;
        UpdateMaterial();
    }
    
    /// <summary>
    /// 위쪽 모서리만 둥글게 설정 (드롭다운 첫 번째 아이템용)
    /// </summary>
    public void SetTopCornersRadius(float radius)
    {
        SetRadius(radius, radius, 0, 0);
    }
    
    /// <summary>
    /// 아래쪽 모서리만 둥글게 설정 (드롭다운 마지막 아이템용)
    /// </summary>
    public void SetBottomCornersRadius(float radius)
    {
        SetRadius(0, 0, radius, radius);
    }
    
    /// <summary>
    /// 현재 반경 값들 반환
    /// </summary>
    public (float topLeft, float topRight, float bottomRight, float bottomLeft) GetRadius()
    {
        return (radiusTopLeft, radiusTopRight, radiusBottomRight, radiusBottomLeft);
    }
}
