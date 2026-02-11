Shader "Custom/Outline"
{
    Properties
    {
        _FillColor ("Fill Color", Color) = (1,1,1,0.3)
        _OutlineBrightColor ("Bright Outline Color", Color) = (1,1,1,1)
        _OutlineDarkColor ("Dark Outline Color", Color) = (0,0,0,1)
        _Thickness ("Outline Thickness", Range(0.001, 0.05)) = 0.01
        _DashSpacing ("Dash Spacing", Range(5, 50)) = 20
        _Speed ("Dash Speed", Range(0.1, 5)) = 1.0
        _MainTex ("Main Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord  : TEXCOORD0;
                float2 clipUV : TEXCOORD1;
                float2 screenpos : TEXCOORD2;
            };

            float4 _FillColor;
            float4 _OutlineBrightColor;
            float4 _OutlineDarkColor;
            float _Thickness;
            float _DashSpacing;
            float _Speed;
            float _EditorTime;

            sampler2D _MainTex;
            uniform float4 _MainTex_ST;
            uniform float4x4 unity_GUIClipTextureMatrix;
            sampler2D _GUIClipTexture;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color = IN.color;
                float3 eyePos = UnityObjectToViewPos(IN.vertex);
                OUT.clipUV = mul(unity_GUIClipTextureMatrix, float4(eyePos.xy, 0, 1.0));
                OUT.screenpos = ComputeScreenPos(OUT.vertex);

                return OUT;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                if (tex2D(_GUIClipTexture, i.clipUV).a == 0)
                    discard;

                float4 col = tex2D(_MainTex, i.texcoord);
                const float alpha = col.a;
                const float dx = ddx(alpha);
                const float dy = ddy(alpha);
                float edgeFactor = length(dx) + length(dy);
                edgeFactor = smoothstep(_Thickness * 0.5, _Thickness, edgeFactor);

                const float linePattern = step(0.0, sin((-i.texcoord.x + i.texcoord.y) * _DashSpacing + _EditorTime * _Speed));
                const float4 finalMask = edgeFactor * lerp(_OutlineBrightColor, _OutlineDarkColor, linePattern);
                const float4 finalColor = lerp(finalMask, _FillColor, alpha);

                return finalColor;
            }
            ENDHLSL
        }
    }
}
