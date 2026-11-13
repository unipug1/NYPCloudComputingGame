Shader "UI/Chevron Wipe"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture", 2D) = "white" {}
        _Progress ("Progress", Range(0, 2)) = 0
        _ChevronDepth ("Chevron Depth", Range(0, 0.35)) = 0.14
        _StripeWidth ("Transparent Gap Width", Range(0, 0.2)) = 0.08
        _EdgeWidth ("Outer Edge Width", Range(0, 0.2)) = 0.09
        _FillColor ("Fill Color", Color) = (0.18, 0.05, 0.32, 1)
        _DotColor ("Dot Color", Color) = (0.75, 0.36, 0.95, 1)
        _DotSpacing ("Dot Spacing At 1080p", Range(4, 64)) = 14
        _DotSize ("Dot Size", Range(0, 1)) = 0.45
        _WaveAmount ("Wave Amount", Range(0, 1)) = 0.55
        _WaveSpeed ("Wave Speed", Range(0, 3)) = 0.65
        _WaveLength ("Wave Length In Dots", Range(2, 24)) = 9
        _Drift ("Drift XY At 1080p", Vector) = (16, 8, 0, 0)
        _OutlineColor ("Outline Color", Color) = (0.96, 0.75, 1, 1)
        _OutlineWidth ("Outline Width At 1080p", Range(0, 16)) = 3
        [HideInInspector] _CanvasAspect ("Canvas Aspect", Float) = 0
        [HideInInspector] _WipeTime ("Wipe Time", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "CanUseSpriteAtlas" = "False" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Progress;
            float _ChevronDepth;
            float _StripeWidth;
            float _EdgeWidth;
            fixed4 _FillColor;
            fixed4 _DotColor;
            float _DotSpacing;
            float _DotSize;
            float _WaveAmount;
            float _WaveSpeed;
            float _WaveLength;
            float4 _Drift;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _CanvasAspect;
            float _WipeTime;

            struct VertexInput
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct FragmentInput
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            FragmentInput vert(VertexInput input)
            {
                FragmentInput output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed3 HalftoneColor(float2 uv, float referenceWidth)
            {
                float2 referencePosition = uv * float2(referenceWidth, 1080.0);
                float2 grid = (referencePosition - _Drift.xy * _WipeTime) / max(_DotSpacing, 4.0);

                // stagger the rows and give each dot its own wave phase
                float rowOffset = frac(floor(grid.y) * 0.5);
                grid.x += rowOffset;
                float2 cellCenter = floor(grid) + 0.5;
                float wavePosition = dot(cellCenter - float2(rowOffset, 0), float2(0.8944, 0.4472));
                float wave = sin(6.283185 * (wavePosition / _WaveLength - _WipeTime * _WaveSpeed));
                float radius = min(0.49, _DotSize * 0.5 * (1.0 + _WaveAmount * wave));
                float distanceToDot = length(grid - cellCenter);
                float dotMask = step(distanceToDot, radius) * step(0.001, radius);
                return lerp(_FillColor.rgb, _DotColor.rgb, dotMask * _DotColor.a);
            }

            fixed4 frag(FragmentInput input) : SV_Target
            {
                if (_Progress <= 0.0 || _Progress >= 2.0)
                    return fixed4(0, 0, 0, 0);

                float aspect = _CanvasAspect > 0.0 ? _CanvasAspect : _ScreenParams.x / _ScreenParams.y;
                float referenceWidth = aspect * 1080.0;
                fixed3 color = HalftoneColor(input.uv, referenceWidth);
                if (_Progress == 1.0)
                    return fixed4(color, 1);

                // push the middle of the edge farther right
                float bend = (1.0 - abs(input.uv.y * 2.0 - 1.0)) * _ChevronDepth;
                float bandWidth = _StripeWidth + _EdgeWidth;
                float slope = 2.0 * _ChevronDepth * aspect;
                float outlineWidth = _OutlineWidth * sqrt(1.0 + slope * slope) / referenceWidth;
                float edgeTravelPadding = bandWidth + outlineWidth;
                float distanceFromEdge;
                float coverage;

                if (_Progress < 1.0)
                {
                    float edge = lerp(-_ChevronDepth, 1.0 + edgeTravelPadding, _Progress) + bend;
                    distanceFromEdge = edge - input.uv.x;
                    coverage = step(0.0, distanceFromEdge);
                }
                else
                {
                    float edge = lerp(-_ChevronDepth - edgeTravelPadding, 1.0, _Progress - 1.0) + bend;
                    distanceFromEdge = input.uv.x - edge;
                    coverage = step(0.0, distanceFromEdge);
                }

                float gap = step(_EdgeWidth, distanceFromEdge) * (1.0 - step(bandWidth, distanceFromEdge));
                // draw the outlines inside the visible parts of the wipe
                float nearTip = 1.0 - step(outlineWidth, distanceFromEdge);
                float nearOuterGap = step(_EdgeWidth - outlineWidth, distanceFromEdge)
                    * (1.0 - step(_EdgeWidth, distanceFromEdge));
                float nearInnerGap = step(bandWidth, distanceFromEdge)
                    * (1.0 - step(bandWidth + outlineWidth, distanceFromEdge));
                float hasGap = step(0.0001, _StripeWidth);
                float outline = max(nearTip, hasGap * max(nearOuterGap, nearInnerGap));

                color = lerp(color, _OutlineColor.rgb, outline * _OutlineColor.a);
                return fixed4(color, coverage * (1.0 - gap));
            }
            ENDCG
        }
    }
}
