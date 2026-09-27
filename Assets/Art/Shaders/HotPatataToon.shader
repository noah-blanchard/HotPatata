// HotPatata toon: soft, bright, party-game shading for URP (Forward / Forward+).
//  - a soft two-band ramp on the main light (with its shadows) and a tinted, never-black shade colour;
//  - ambient from the sky probe, rim light, optional specular blob, emission (MaterialPropertyBlock friendly);
//  - kit extras: fake bevel highlight on scaled unit cubes, world-space checker / stripe patterns.
// ShadowCaster, DepthOnly and DepthNormals (SSAO) reuse URP's own passes.
Shader "HotPatata/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade Tint (multiplies the base colour in shadow)", Color) = (0.62, 0.58, 0.82, 1)
        _TopColor ("Top Colour (upward faces)", Color) = (1, 0.94, 0.82, 1)
        _TopBlend ("Top Colour Blend (0 = off)", Range(0, 1)) = 0
        _RampThreshold ("Ramp Threshold", Range(-1, 1)) = 0.05
        _RampSmoothness ("Ramp Softness", Range(0.001, 1)) = 0.12
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.45

        [Header(Rim)]
        _RimColor ("Rim Colour", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.5
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.25

        [Header(Specular blob)]
        _SpecColor ("Specular Colour", Color) = (1, 1, 1, 1)
        _SpecSize ("Specular Size (0 = off)", Range(0, 0.2)) = 0

        [Header(Emission)]
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)

        [Header(Kit extras)]
        _EdgeWidth ("Bevel Highlight Width (m, unit cubes only, 0 = off)", Range(0, 0.3)) = 0
        _EdgeStrength ("Bevel Highlight Strength", Range(0, 1)) = 0.25
        [Enum(None, 0, Checker, 1, Stripes, 2)] _Pattern ("World Pattern", Float) = 0
        _PatternColor ("Pattern Colour (stripes)", Color) = (0.1, 0.1, 0.1, 1)
        _PatternScale ("Pattern Size (m)", Float) = 1
        _PatternStrength ("Pattern Strength", Range(0, 1)) = 0.08
        _PatternScroll ("Pattern Scroll (world m/s, conveyors)", Vector) = (0, 0, 0, 0)

        [Toggle(_VERTEX_COLOR)] _VertexColor ("Multiply By Vertex Colour (particles)", Float) = 0
        [HideInInspector] _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ToonVertex
            #pragma fragment ToonFragment

            #pragma shader_feature_local _VERTEX_COLOR

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "HotPatataToonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                half4 color       : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS    : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                half fogFactor    : TEXCOORD4;
                half4 color       : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ToonVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = nrm.normalWS;
                output.positionOS = input.positionOS.xyz;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                output.color = input.color;
                return output;
            }

            // Distance (m) to the nearest edge of the face this fragment is on, for a unit cube scaled by its transform.
            half BevelMask(float3 positionOS)
            {
                float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20),
                                      length(UNITY_MATRIX_M._m01_m11_m21),
                                      length(UNITY_MATRIX_M._m02_m12_m22));
                float3 d = (0.5 - abs(positionOS)) * scale;   // distance to each pair of faces, in metres
                // On a face one component is ~0; the next smallest is the distance to that face's nearest edge.
                float lo = min(d.x, min(d.y, d.z));
                float hi = max(d.x, max(d.y, d.z));
                float mid = d.x + d.y + d.z - lo - hi;
                return 1.0 - smoothstep(_EdgeWidth * 0.5, _EdgeWidth, mid);
            }

            half3 WorldPattern(half3 albedo, float3 positionWS, half3 normalWS)
            {
                half3 n = abs(normalWS);
                positionWS -= _PatternScroll.xyz * _Time.y;   // moving belts (cosmetic only)
                float2 uv = n.y > 0.5 ? positionWS.xz : (n.x > n.z ? positionWS.zy : positionWS.xy);
                uv /= max(0.01, _PatternScale);
                if (_Pattern < 1.5)
                {
                    float c = fmod(abs(floor(uv.x) + floor(uv.y)), 2.0);
                    return albedo * (1.0 - c * _PatternStrength);
                }
                float s = step(0.5, frac((uv.x + uv.y) * 0.5));
                return lerp(albedo, _PatternColor.rgb, s * _PatternStrength);
            }

            half Band(half ndl)
            {
                return smoothstep(_RampThreshold - _RampSmoothness, _RampThreshold + _RampSmoothness, ndl);
            }

            half4 ToonFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalWS = normalize(input.normalWS);
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);

                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                #if defined(_VERTEX_COLOR)
                    albedo *= input.color.rgb;
                #endif
                // Upward faces can take their own colour (a cartoon 'grass top' that separates walkable from sides).
                albedo = lerp(albedo, _TopColor.rgb, smoothstep(0.55, 0.8, normalWS.y) * _TopBlend);
                if (_Pattern > 0.5) albedo = WorldPattern(albedo, input.positionWS, normalWS);

                half directAO = 1.0, indirectAO = 1.0;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = CreateAmbientOcclusionFactor(screenUV, 1.0);
                    directAO = ao.directAmbientOcclusion;
                    indirectAO = ao.indirectAmbientOcclusion;
                #endif

                // --- main light: soft two-band ramp, shadowed side tinted (never black)
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, half4(1, 1, 1, 1));
                half ndl = dot(normalWS, mainLight.direction);
                half lit = Band(ndl) * lerp(0.0, 1.0, mainLight.shadowAttenuation) * directAO;
                half3 shade = albedo * _ShadeColor.rgb;
                half3 color = lerp(shade, albedo * mainLight.color, lit);

                // --- ambient from the sky
                color += albedo * SampleSH(normalWS) * _AmbientStrength * indirectAO;

                // --- specular blob (cartoon highlight)
                if (_SpecSize > 0.0)
                {
                    half3 h = normalize(mainLight.direction + viewWS);
                    half blob = smoothstep(1.0 - _SpecSize, 1.0 - _SpecSize * 0.6, dot(normalWS, h));
                    color += _SpecColor.rgb * mainLight.color * blob * lit;
                }

                // --- additional lights (point/spot), banded too
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = screenUV;
                    uint lightCount = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                    {
                        Light l = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                        color += albedo * l.color * Band(dot(normalWS, l.direction)) * l.distanceAttenuation * l.shadowAttenuation;
                    }
                    #endif
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light l = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                        color += albedo * l.color * Band(dot(normalWS, l.direction)) * l.distanceAttenuation * l.shadowAttenuation;
                    LIGHT_LOOP_END
                #endif

                // --- rim: a soft bright outline on the lit side, fainter in shadow
                half fresnel = pow(saturate(1.0 - dot(normalWS, viewWS)), _RimPower);
                color += _RimColor.rgb * fresnel * _RimStrength * lerp(0.35, 1.0, lit);

                // --- fake bevel: a light edge on kit boxes so shapes read at a glance
                if (_EdgeWidth > 0.0) color *= 1.0 + BevelMask(input.positionOS) * _EdgeStrength;

                color += _EmissionColor.rgb;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "HotPatataToonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "HotPatataToonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing
            #include "HotPatataToonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
