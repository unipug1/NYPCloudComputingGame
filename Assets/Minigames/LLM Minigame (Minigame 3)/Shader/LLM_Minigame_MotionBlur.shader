Shader "UI/LLM_Minigame_MotionBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BlurAngle ("Blur Angle (Degrees)", Float) = 45.0
        _BlurAmount ("Blur Amount", Range(0, 0.05)) = 0.0
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

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _BlurAngle;
            float _BlurAmount;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                if (_BlurAmount <= 0.0001)
                {
                    return tex2D(_MainTex, IN.uv) * IN.color;
                }

                float rad = _BlurAngle * 0.01745329;
                float2 dir = float2(cos(rad), sin(rad)) * _BlurAmount;

                // 5 point directional smear
                fixed4 col = fixed4(0, 0, 0, 0);
                col += tex2D(_MainTex, IN.uv - dir * 2.0) * 0.1;
                col += tex2D(_MainTex, IN.uv - dir * 1.0) * 0.2;
                col += tex2D(_MainTex, IN.uv) * 0.4;
                col += tex2D(_MainTex, IN.uv + dir * 1.0) * 0.2;
                col += tex2D(_MainTex, IN.uv + dir * 2.0) * 0.1;

                return col * IN.color;
            }
            ENDCG
        }
    }
}