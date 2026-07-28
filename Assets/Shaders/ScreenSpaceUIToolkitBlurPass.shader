Shader "Hidden/MemoAnchor/ScreenSpaceUIToolkitBlurPass"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _BlurOffset;
        CBUFFER_END

        half3 SampleBlitTexture(float2 uv)
        {
            uv = UnityStereoTransformScreenSpaceTex(uv);
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
        }

        half3 BoxBlur(float2 uv)
        {
            float2 texel = _BlitTexture_TexelSize.xy;
            half3 color = SampleBlitTexture(uv) * 0.5h;
            color += SampleBlitTexture(uv + texel * float2(1.0, 1.0)) * 0.125h;
            color += SampleBlitTexture(uv + texel * float2(-1.0, 1.0)) * 0.125h;
            color += SampleBlitTexture(uv + texel * float2(1.0, -1.0)) * 0.125h;
            color += SampleBlitTexture(uv + texel * float2(-1.0, -1.0)) * 0.125h;
            return color;
        }

        half3 GaussianBlur(float2 uv, float2 direction)
        {
            float2 texel = _BlitTexture_TexelSize.xy * direction * _BlurOffset;
            half3 color = SampleBlitTexture(uv) * 0.227027h;
            color += SampleBlitTexture(uv + texel * 1.384615) * 0.316216h;
            color += SampleBlitTexture(uv - texel * 1.384615) * 0.316216h;
            color += SampleBlitTexture(uv + texel * 3.230769) * 0.070270h;
            color += SampleBlitTexture(uv - texel * 3.230769) * 0.070270h;
            return color;
        }
        ENDHLSL

        Pass
        {
            Name "Downsample"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ DISABLE_TEXTURE2D_X_ARRAY

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(BoxBlur(input.texcoord.xy), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Horizontal"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ DISABLE_TEXTURE2D_X_ARRAY

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(GaussianBlur(input.texcoord.xy, float2(1.0, 0.0)), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Vertical"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ DISABLE_TEXTURE2D_X_ARRAY

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(GaussianBlur(input.texcoord.xy, float2(0.0, 1.0)), 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
