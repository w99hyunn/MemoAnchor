Shader "MemoAnchor/Preview Vertex Colors"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _BackColor ("Back Color", Color) = (0.32, 0.36, 0.42, 1)
        _UseBackColor ("Use Back Color", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Cull [_Cull]

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _BackColor;
            float _UseBackColor;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                if (_UseBackColor > 0.5 && facing < 0)
                {
                    return _BackColor;
                }
                return fixed4(i.color.rgb, 1);
            }
            ENDCG
        }
    }
}
