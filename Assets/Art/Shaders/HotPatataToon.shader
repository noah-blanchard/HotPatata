// HotPatata stylized: cinematic, golden-hour shading for URP (Forward / Forward+), ARCHITECTURE §25.
//  - soft wrapped Lambert on the main light with its real shadows (a touch of the old ramp kept by _Stylize), the
//    shadowed side filled only by the sky (SH ambient times SSAO, tinted by _ShadeColor): deep, never black;
//  - GGX specular (_Smoothness, optional gloss map), optional normal map, a light-aware rim (strongest against the sun);
//  - emission always added (MaterialPropertyBlock friendly);
//  - kit extras: fake bevel highlight on scaled unit cubes, world-space checker / stripe patterns (hazard stripes:
//    never red alone), top colour on upward faces;
//  - suit tint: the texture's coloured areas take the base colour, greys and whites stay as painted.
// The file and every property name are kept from the old HotPatata/Toon, so no material or builder had to change.
// ShadowCaster, DepthOnly and DepthNormals (SSAO) reuse URP's own passes.
Shader "HotPatata/Stylized"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shadow Tint (colours the sky fill in shadow)", Color) = (0.62, 0.58, 0.82, 1)
        _TopColor ("Top Colour (upward faces)", Color) = (1, 0.94, 0.82, 1)
        _TopBlend ("Top Colour Blend (0 = off)", Range(0, 1)) = 0
        _Wrap ("Light Wrap (soft terminator)", Range(0, 1)) = 0.25
        _Stylize ("Stylize (0 = soft light, 1 = the old two-band ramp)", Range(0, 1)) = 0.15
        _RampThreshold ("Ramp Threshold (stylize)", Range(-1, 1)) = 0.05
        _RampSmoothness ("Ramp Softness (stylize)", Range(0.001, 1)) = 0.12
        _AmbientStrength ("Sky Fill Strength", Range(0, 2)) = 0.45
        _SuitTint ("Suit Tint (coloured texels take Base Color, greys stay; 0 = multiply as usual)", Range(0, 1)) = 0

        [Header(Surface)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.2
        [NoScaleOffset] _GlossMap ("Gloss Map (R)", 2D) = "white" {}
        [Toggle(_GLOSSMAP)] _UseGlossMap ("Use Gloss Map", Float) = 0
        [NoScaleOffset][Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 1
        [Toggle(_NORMALMAP)] _UseNormalMap ("Use Normal Map", Float) = 0

        [Header(Rim)]
        _RimColor ("Rim Colour", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.5
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.25

        [Header(Specular blob (stylized extra))]
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
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _GLOSSMAP

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "HotPatataToonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "HotPatataFog.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
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
                half4 tangentWS   : TEXCOORD5;
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
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = nrm.normalWS;
                output.tangentWS = half4(nrm.tangentWS, input.tangentOS.w * GetOddNegativeScale());
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

            // How much a light reaches a face: a soft wrapped Lambert, nudged towards the old two-band ramp by _Stylize.
            half Diffuse(half ndl)
            {
                half soft = saturate((ndl + _Wrap) / (1.0 + _Wrap));
                half band = smoothstep(_RampThreshold - _RampSmoothness, _RampThreshold + _RampSmoothness, ndl);
                return lerp(soft, band, _Stylize);
            }

            // GGX specular (normalised, with a Schlick Fresnel on a dielectric), times N.L.
            half3 Specular(half3 normalWS, half3 lightDir, half3 viewWS, half roughness)
            {
                half3 h = SafeNormalize(lightDir + viewWS);
                half nh = saturate(dot(normalWS, h));
                half lh = saturate(dot(lightDir, h));
                half a2 = max(roughness * roughness, 0.002);
                a2 *= a2;
                half d = nh * nh * (a2 - 1.0) + 1.0;
                half spec = a2 / (4.0 * PI * d * d * max(0.1, lh * lh) * (roughness + 0.5));
                half fresnel = 0.04 + 0.96 * pow(1.0 - lh, 5.0);
                return spec * fresnel * saturate(dot(normalWS, lightDir));
            }

            half4 ToonFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalWS = normalize(input.normalWS);
                #if defined(_NORMALMAP)
                    half3 tangentNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                    half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                    normalWS = normalize(TransformTangentToWorld(tangentNormal, half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));
                #endif
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);

                half3 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half3 albedo = texel * _BaseColor.rgb;
                if (_SuitTint > 0.0)
                {
                    // Suit tint (ARCHITECTURE §25): the coloured swatches of the atlas become the base colour,
                    // keeping a little of their light-to-dark gradient; greys, whites and faces stay as painted.
                    half hi = max(texel.r, max(texel.g, texel.b));
                    half chroma = hi - min(texel.r, min(texel.g, texel.b));
                    half3 suit = _BaseColor.rgb * lerp(0.7, 1.0, hi);
                    albedo = lerp(texel, suit, smoothstep(0.15, 0.35, chroma) * _SuitTint);
                }
                #if defined(_VERTEX_COLOR)
                    albedo *= input.color.rgb;
                #endif
                // Upward faces can take their own colour (a 'grass top' that separates walkable from sides).
                albedo = lerp(albedo, _TopColor.rgb, smoothstep(0.55, 0.8, input.normalWS.y) * _TopBlend);
                if (_Pattern > 0.5) albedo = WorldPattern(albedo, input.positionWS, normalWS);

                half smoothness = _Smoothness;
                #if defined(_GLOSSMAP)
                    smoothness *= SAMPLE_TEXTURE2D(_GlossMap, sampler_GlossMap, input.uv).r;
                #endif
                half roughness = 1.0 - smoothness;

                half directAO = 1.0, indirectAO = 1.0;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = CreateAmbientOcclusionFactor(screenUV, 1.0);
                    directAO = ao.directAmbientOcclusion;
                    indirectAO = ao.indirectAmbientOcclusion;
                #endif

                // --- main light: soft, with its real shadows
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, half4(1, 1, 1, 1));
                half shadow = mainLight.shadowAttenuation * directAO;
                half lit = Diffuse(dot(normalWS, mainLight.direction)) * shadow;
                half3 color = albedo * mainLight.color * lit;
                color += mainLight.color * Specular(normalWS, mainLight.direction, viewWS, roughness) * shadow;

                // --- the sky fills the shadows: cool, deep, never black (tinted by _ShadeColor, a little)
                half3 fillTint = lerp(half3(1, 1, 1), _ShadeColor.rgb / max(0.01, dot(_ShadeColor.rgb, half3(0.333, 0.333, 0.334))), 0.35);
                color += albedo * SampleSH(normalWS) * (_AmbientStrength + 0.8) * fillTint * indirectAO;

                // --- specular blob (an optional cartoon highlight, kept for materials that use it)
                if (_SpecSize > 0.0)
                {
                    half3 h = normalize(mainLight.direction + viewWS);
                    half blob = smoothstep(1.0 - _SpecSize, 1.0 - _SpecSize * 0.6, dot(normalWS, h));
                    color += _SpecColor.rgb * mainLight.color * blob * lit;
                }

                // --- additional lights (point/spot)
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = screenUV;
                    uint lightCount = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                    {
                        Light l = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                        half a = l.distanceAttenuation * l.shadowAttenuation;
                        color += l.color * a * (albedo * Diffuse(dot(normalWS, l.direction)) + Specular(normalWS, l.direction, viewWS, roughness));
                    }
                    #endif
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light l = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                        half a = l.distanceAttenuation * l.shadowAttenuation;
                        color += l.color * a * (albedo * Diffuse(dot(normalWS, l.direction)) + Specular(normalWS, l.direction, viewWS, roughness));
                    LIGHT_LOOP_END
                #endif

                // --- rim: light-aware, strongest with the low sun behind the subject (a warm cinematic edge)
                half fresnel = pow(saturate(1.0 - dot(normalWS, viewWS)), _RimPower);
                half backlight = saturate(dot(-viewWS, mainLight.direction) * 0.5 + 0.5);
                color += _RimColor.rgb * mainLight.color * fresnel * _RimStrength * lerp(0.25, 1.4, backlight) * lerp(0.4, 1.0, shadow);

                // --- fake bevel: a light edge on kit boxes so shapes read at a glance
                if (_EdgeWidth > 0.0) color *= 1.0 + BevelMask(input.positionOS) * _EdgeStrength;

                color += _EmissionColor.rgb;
                color = HP_MixFog(color, input.fogFactor, input.positionWS);
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
