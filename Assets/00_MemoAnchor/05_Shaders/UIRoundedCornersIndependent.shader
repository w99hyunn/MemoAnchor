Shader "UI/RoundedCornersIndependent"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        // 각 모서리별 반경 (TopLeft, TopRight, BottomRight, BottomLeft)
        _RadiusTL ("Radius Top Left", Float) = 0
        _RadiusTR ("Radius Top Right", Float) = 0
        _RadiusBR ("Radius Bottom Right", Float) = 0
        _RadiusBL ("Radius Bottom Left", Float) = 0
        
        // 사이즈 (RectTransform에서 가져옴)
        _Width ("Width", Float) = 100
        _Height ("Height", Float) = 100
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        
        _ColorMask ("Color Mask", Float) = 15
        
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        
        Pass
        {
            Name "Default"
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            
            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float2 localPos : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            
            float _RadiusTL;
            float _RadiusTR;
            float _RadiusBR;
            float _RadiusBL;
            float _Width;
            float _Height;
            
            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                // UV를 실제 픽셀 좌표로 변환 (0,0이 왼쪽 하단)
                OUT.localPos = v.texcoord * float2(_Width, _Height);
                return OUT;
            }
            
            // 정확한 둥근 사각형 SDF
            float roundedRectSDF(float2 pixelPos, float2 size, float4 radii)
            {
                // radii: x=TL, y=TR, z=BR, w=BL
                // 중심 기준 좌표로 변환
                float2 halfSize = size * 0.5;
                float2 centerPos = pixelPos - halfSize;
                
                // 어떤 사분면에 있는지 결정하여 해당 반경 선택
                float radius;
                if (centerPos.x < 0.0)
                {
                    // 왼쪽
                    if (centerPos.y > 0.0)
                        radius = radii.x; // Top Left
                    else
                        radius = radii.w; // Bottom Left
                }
                else
                {
                    // 오른쪽
                    if (centerPos.y > 0.0)
                        radius = radii.y; // Top Right
                    else
                        radius = radii.z; // Bottom Right
                }
                
                // 코너까지의 거리 계산
                float2 q = abs(centerPos) - halfSize + radius;
                float dist = min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius;
                
                return dist;
            }
            
            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                
                float2 size = float2(_Width, _Height);
                float4 radii = float4(_RadiusTL, _RadiusTR, _RadiusBR, _RadiusBL);
                
                float dist = roundedRectSDF(IN.localPos, size, radii);
                
                // 부드러운 안티앨리어싱 (픽셀 단위)
                float aa = 1.5;
                float alpha = 1.0 - smoothstep(-aa, aa, dist);
                color.a *= alpha;
                
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                
                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif
                
                return color;
            }
            ENDCG
        }
    }
}
