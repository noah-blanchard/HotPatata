// PatataWilds sky (ARCHITECTURE §25.3): two HDRI cubemaps blended by TimeOfDayBlender as the run moves from act to act
// (dawn, noon, late afternoon, sunset, dusk). Each has its own exposure and rotation, so the photographed sun lines up with
// the scene's sun; the band just above and below the horizon melts into the fog colour, so the far hills and the sky meet,
// and below the horizon into the height mist's colour where the nature courses have one (HotPatataFog.hlsl).
Shader "HotPatata/SkyBlend"
{
    Properties
    {
        [NoScaleOffset] _SkyA ("Sky A (HDR cubemap)", Cube) = "grey" {}
        _ExposureA ("Exposure A", Range(0, 8)) = 1
        _RotationA ("Rotation A (degrees)", Range(0, 360)) = 0
        [NoScaleOffset] _SkyB ("Sky B (HDR cubemap)", Cube) = "grey" {}
        _ExposureB ("Exposure B", Range(0, 8)) = 1
        _RotationB ("Rotation B (degrees)", Range(0, 360)) = 0
        _Blend ("Blend (0 = A, 1 = B)", Range(0, 1)) = 0
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _HorizonFog ("Horizon Fog Band", Range(0, 1)) = 0.6
        _HorizonHeight ("Horizon Band Height", Range(0.01, 0.5)) = 0.12
        _Saturation ("Saturation", Range(0, 1.5)) = 1
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
            #include "HotPatataFog.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _ExposureA, _ExposureB, _Blend, _HorizonFog, _HorizonHeight, _Saturation;
                float _RotationA, _RotationB;
                half4 _Tint;
            CBUFFER_END

            TEXTURECUBE(_SkyA); SAMPLER(sampler_SkyA);
            TEXTURECUBE(_SkyB); SAMPLER(sampler_SkyB);

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

            float3 RotateY(float3 d, float degrees)
            {
                float a = radians(degrees), c = cos(a), s = sin(a);
                return float3(c * d.x - s * d.z, d.y, s * d.x + c * d.z);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                half3 a = SAMPLE_TEXTURECUBE_LOD(_SkyA, sampler_SkyA, RotateY(d, _RotationA), 0).rgb * _ExposureA;
                half3 b = SAMPLE_TEXTURECUBE_LOD(_SkyB, sampler_SkyB, RotateY(d, _RotationB), 0).rgb * _ExposureB;
                half3 sky = lerp(a, b, _Blend) * _Tint.rgb;
                sky = lerp(dot(sky, half3(0.2126, 0.7152, 0.0722)).xxx, sky, _Saturation);
                half band = 1.0 - saturate(abs(d.y) / _HorizonHeight);
                half below = saturate(-d.y * 6.0);
                half3 under = _HP_MistParams.x > 0.0 ? lerp(unity_FogColor.rgb, _HP_MistColor.rgb, _HP_MistParams.z) : unity_FogColor.rgb;
                sky = lerp(sky, lerp(unity_FogColor.rgb, under, below), saturate(band * band * _HorizonFog + below));
                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
