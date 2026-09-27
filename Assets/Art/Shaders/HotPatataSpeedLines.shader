// Anime-style speed lines: thin white streaks radiating from the screen centre, only at the edges, scrolling
// outward. Drawn by a Full Screen Pass renderer feature (alpha blended, no colour copy needed). Intensity comes
// from the global _HotPatataSpeedLines (0 = invisible), set each frame by SpeedEffects.
Shader "HotPatata/SpeedLines"
{
    Properties
    {
        _LineColor ("Line Colour", Color) = (1, 1, 1, 1)
        _LineCount ("Line Count", Float) = 90
        _InnerRadius ("Clear Centre Radius", Range(0, 1)) = 0.38
        _Thickness ("Line Thinness", Range(0.5, 0.99)) = 0.86
        _ScrollSpeed ("Scroll Speed", Float) = 3.2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "SpeedLines"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 _LineColor;
            float _LineCount, _InnerRadius, _Thickness, _ScrollSpeed;
            float _HotPatataSpeedLines;

            float Hash(float x) { return frac(sin(x * 12.9898) * 43758.5453); }

            half4 frag(Varyings input) : SV_Target
            {
                float k = saturate(_HotPatataSpeedLines);
                if (k <= 0.001) return 0;

                float2 p = input.texcoord - 0.5;
                p.x *= _ScreenParams.x / _ScreenParams.y;
                float r = length(p);
                float a = atan2(p.y, p.x) / (2.0 * PI) + 0.5;

                float cell = floor(a * _LineCount);
                float seed = Hash(cell);
                float within = frac(a * _LineCount);
                // Only some angular cells carry a line, and more of them as the speed builds.
                float present = step(1.0 - k * 0.85, seed);
                float thin = smoothstep(_Thickness, 1.0, 1.0 - abs(within - 0.5) * 2.0);
                // Dashes streaming outward, each line at its own pace.
                float dash = step(0.45, frac(r * 2.5 - _Time.y * _ScrollSpeed * (0.6 + seed) + seed * 7.0));
                float edge = smoothstep(_InnerRadius, _InnerRadius + 0.35, r);

                float alpha = present * thin * dash * edge * k * _LineColor.a;
                return half4(_LineColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
