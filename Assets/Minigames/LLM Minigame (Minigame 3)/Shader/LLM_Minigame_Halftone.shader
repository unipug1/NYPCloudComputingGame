Shader "UI/LLM_Minigame_Halftone"
{
    Properties
    {
        [Header(Colors)]
        _DotColor ("Dot Color", Color) = (0, 0, 0, 0.35)
        _BgColor ("Background Color (Transparent Allowed)", Color) = (0, 0, 0, 0)

        [Header(Grid Density)]
        _Density ("Dot Density", Float) = 45.0
        _AspectRatio ("Screen Aspect Ratio (e.g. 1.777)", Float) = 1.777778

        [Header(Scale Gradient)]
        _MinRadius ("Min Dot Radius (Bottom Right)", Range(0.0, 0.5)) = 0.05
        _MaxRadius ("Max Dot Radius (Top Left)", Range(0.0, 0.5)) = 0.42

        [Header(Animation)]
        _ScrollSpeed ("Scroll Speed", Float) = 0.15
        _PulseSpeed ("Pulse Speed", Float) = 2.0
        _PulseAmount ("Pulse Amount", Range(0.0, 0.1)) = 0.02
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

            fixed4 _DotColor;
            fixed4 _BgColor;
            float _Density;
            float _AspectRatio;
            float _MinRadius;
            float _MaxRadius;
            float _ScrollSpeed;
            float _PulseSpeed;
            float _PulseAmount;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = IN.texcoord;
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // calculate diagonal gradient from bottom right (1, 0) to top left (0, 1)
                // at bottom right (uv.x = 1, uv.y = 0), diag is 0
                // at top left (uv.x = 0, uv.y = 1), diag is 1
                float diag = saturate((1.0 - IN.uv.x) * 0.5 + IN.uv.y * 0.5);

                // calculate base dot radius scaling across that diagonal
                float baseRadius = lerp(_MinRadius, _MaxRadius, diag);

                // subtle breathing pulse wave over time
                float pulse = sin(_Time.y * _PulseSpeed + diag * 6.28) * _PulseAmount;
                float targetRadius = clamp(baseRadius + pulse, 0.0, 0.48);

                // adjust uv aspect ratio so dots remain perfect circles on wide screens
                float2 aspectUV = IN.uv;
                aspectUV.x *= _AspectRatio;

                // continuous diagonal scrolling
                float2 scrollOffset = float2(-1.0, 1.0) * (_Time.y * _ScrollSpeed);
                float2 animatedUV = aspectUV + scrollOffset;

                // create repeating grid cells
                float2 grid = animatedUV * _Density;
                float2 cellUV = frac(grid) - 0.5;

                // distance from the center of each cell
                float dist = length(cellUV);

                // smooth anti-aliased circle edge
                float edgeSoftness = 0.04;
                float circle = smoothstep(targetRadius, targetRadius - edgeSoftness, dist);

                // blend between background and dot color
                fixed4 finalColor = lerp(_BgColor, _DotColor, circle);

                // multiply with canvas UI tint
                finalColor *= IN.color;

                return finalColor;
            }
            ENDCG
        }
    }
}