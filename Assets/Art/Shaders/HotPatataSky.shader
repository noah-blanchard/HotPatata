// Cartoon sky: a three-colour vertical gradient, a soft sun disc with a glow, and slow drifting cloud bands
// near the horizon. Cheap and flat, so the saturated level kit reads clearly against it.
Shader "HotPatata/Sky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.28, 0.55, 0.98, 1)
        _HorizonColor ("Horizon", Color) = (0.78, 0.9, 1, 1)
        _BottomColor ("Below Horizon", Color) = (0.55, 0.72, 0.95, 1)
        _GradientPower ("Gradient Curve", Range(0.2, 4)) = 1.2
        _SunDirection ("Sun Direction (towards the sun)", Vector) = (0.3, 0.6, -0.5, 0)
        _SunColor ("Sun", Color) = (1, 0.96, 0.82, 1)
        _SunSize ("Sun Size", Range(0.001, 0.1)) = 0.025
        _SunGlow ("Sun Glow", Range(0, 2)) = 0.5
        _CloudColor ("Clouds", Color) = (1, 1, 1, 1)
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.45
        _CloudHeight ("Cloud Band Height", Range(0.05, 0.8)) = 0.35
        _CloudSpeed ("Cloud Drift", Float) = 0.004
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor, _HorizonColor, _BottomColor, _SunColor, _CloudColor;
                float4 _SunDirection;
                half _GradientPower, _SunSize, _SunGlow, _CloudCoverage, _CloudHeight;
                float _CloudSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.dir = input.positionOS.xyz;
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 4; k++) { v += Noise(p) * a; p *= 2.03; a *= 0.5; }
                return v;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                half up = saturate(d.y);
                half3 sky = d.y >= 0.0
                    ? lerp(_HorizonColor.rgb, _TopColor.rgb, pow(up, 1.0 / _GradientPower))
                    : lerp(_HorizonColor.rgb, _BottomColor.rgb, saturate(-d.y * 4.0));

                // Puffy cloud band: noise on the sky dome, fading out overhead and below the horizon.
                float2 cuv = d.xz / max(0.08, d.y + 0.15) * 1.6 + _Time.y * _CloudSpeed * float2(1.0, 0.35) * 60.0;
                float n = Fbm(cuv);
                half band = smoothstep(0.0, 0.05, d.y) * (1.0 - smoothstep(_CloudHeight * 0.5, _CloudHeight, d.y));
                half cloud = smoothstep(1.0 - _CloudCoverage, 1.0 - _CloudCoverage + 0.08, n) * band;
                half shade = lerp(0.82, 1.0, smoothstep(0.35, 0.8, n));   // a soft cartoon underside
                sky = lerp(sky, _CloudColor.rgb * shade, cloud);

                float3 sunDir = normalize(_SunDirection.xyz);
                half s = dot(d, sunDir);
                half disc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.7, s);
                half glow = pow(saturate(s), 64.0) * _SunGlow;
                sky += _SunColor.rgb * (disc * (1.0 - cloud * 0.7) + glow);
                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
}
