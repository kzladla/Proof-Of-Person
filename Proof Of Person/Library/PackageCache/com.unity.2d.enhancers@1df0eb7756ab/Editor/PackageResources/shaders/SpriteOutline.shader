Shader "Hidden/SpriteOutline"
{
    Properties
    {
        _MainTex("Sprite Texture", 2D) = "white" {}
        _OutlineSize("Outline Size", Float) = 1
        _OutlineColor("Outline Color", Color) = (1,0,1,1)
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"
        }
        LOD 100

        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            
            float _OutlineSize;
            fixed4 _OutlineColor;
            uniform bool _AdjustLinearForGamma;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);
                if (col.a == 0)
                    discard;

                const float width = _OutlineSize * _MainTex_TexelSize.x;
                const float height = _OutlineSize * _MainTex_TexelSize.y;

                float2 pos = i.uv + float2(-width, -height);
                const half a1 = pos.x >= 0 && pos.y >= 0 && pos.x <= 1 && pos.y <= 1 ? tex2D(_MainTex, pos).a : 0;
                pos = i.uv + float2(0, -height);
                const half a2 = pos.x >= 0 && pos.y >= 0 && pos.x <= 1 && pos.y <= 1 ? tex2D(_MainTex, pos).a : 0;
                pos = i.uv + float2(+width, -height);
                const half a3 = pos.x >= 0 && pos.y >= 0 && pos.x <= 1 && pos.y <= 1 ? tex2D(_MainTex, pos).a : 0;

                pos = i.uv + float2(-width, 0);
                const half a4 =pos.x >= 0 && pos.y >= 0 && pos.x <= 1 && pos.y <= 1 ? tex2D(_MainTex, pos).a : 0;
                pos = i.uv + float2(+width, 0);
                const half a6 = pos.x >= 0 && pos.y >= 0 && pos.x <= 1 && pos.y <= 1 ? tex2D(_MainTex, pos).a : 0;

                pos = i.uv + float2(-width, +height);
                const half a7 = pos.x >= 0 && pos.y >= 0 && pos.x <= 1 && pos.y <= 1 ? tex2D(_MainTex, pos).a : 0;
                pos = i.uv + float2(0, +height);
                const half a8 = pos.x >= 0 && pos.y >= 0 && pos.x <= 1 && pos.y <= 1 ? tex2D(_MainTex, pos).a : 0;
                pos = i.uv + float2(+width, +height);
                const half a9 = pos.x >= 0 && pos.y >= 0 && pos.x <= 1 && pos.y <= 1 ? tex2D(_MainTex, pos).a : 0;

                const half gx = -a1 - a2  - a3 + a7 + a8  + a9;
                const half gy = -a1 - a4  - a7 + a3 + a6  + a9;

                const half w = sqrt(gx * gx + gy * gy) * 1.25;

                float4 c = _OutlineColor;
                if (w >= 1)
                {
                    if (_AdjustLinearForGamma)
                        c.rgb = LinearToGammaSpace(c.rgb);
                }
                else
                    discard;

                return c;

            }
            ENDCG
        }
    }
}

