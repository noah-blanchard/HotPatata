// HotPatata industrial: semi-realistic PBR-ish surfaces for the kit boxes (ARCHITECTURE §25.2, issue #87).
// A fork of HotPatata/Stylized (same light model: wrapped Lambert with real shadows, sky fill times SSAO, GGX,
// Forward+ additional lights, light-aware rim) with what tiling PBR textures need:
//  - box mapping in metres: the texture is projected on the dominant axis of the face from the object-space position
//    times the object's scale, so a 12 m slab and a 1 m cube share one texel density and nothing stretches. The mesh
//    UVs (KayKit's atlas) and tangents are never used; the texture sticks to moving platforms (object space);
//  - base colour, normal (OpenGL), gloss or roughness, metallic and occlusion maps, all always sampled (the defaults
//    are neutral), so dropping a texture in the Inspector is enough: no keyword to tick;
//  - metals: F0 from the albedo, no diffuse, and a cheap environment reflection from the ambient SH (no probe);
//  - belts: _PatternScroll (set by Conveyor) slides the texture along the belt;
//  - anti-repetition: a macro layer of the same texture at a larger scale, and a faint procedural variation that also
//    gives texture-less placeholder materials some life.
// ShadowCaster, DepthOnly and DepthNormals (SSAO) reuse URP's own passes.
Shader "HotPatata/Industrial"
{
    Properties
    {
        [Header(Base)]
        [MainTexture][NoScaleOffset] _BaseMap ("Base Color (sRGB)", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color Tint", Color) = (0.6, 0.6, 0.6, 1)
        _BaseSaturation ("Texture Saturation (0 = grey, to recolour a painted texture with the tint)", Range(0, 1)) = 1
        _BaseBrightness ("Texture Brightness", Range(0, 3)) = 1
        _TileSize ("Tile Size (metres per texture repeat)", Float) = 2

        [Header(Normal)]
        [NoScaleOffset][Normal] _BumpMap ("Normal Map (OpenGL, Y+)", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 1
        [Toggle] _FlipNormalY ("Flip Normal Y (for DirectX normal maps)", Float) = 0

        [Header(Smoothness)]
        [NoScaleOffset] _GlossMap ("Gloss or Roughness Map (R, linear)", 2D) = "gray" {}
        [Toggle] _GlossIsRoughness ("Map Is Roughness (inverted in the shader)", Float) = 0
        _SmoothnessMin ("Smoothness at map 0", Range(0, 1)) = 0.05
        _SmoothnessMax ("Smoothness at map 1", Range(0, 1)) = 0.5

        [Header(Metallic)]
        [NoScaleOffset] _MetallicMap ("Metallic Map (R, linear)", 2D) = "white" {}
        _Metallic ("Metallic (multiplies the map)", Range(0, 1)) = 0

        [Header(Occlusion)]
        [NoScaleOffset] _OcclusionMap ("Occlusion Map (R, linear)", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1

        [Header(Repetition)]
        _MacroScale ("Macro Layer Scale (1 = same as tile)", Range(0.02, 1)) = 0.14
        _MacroStrength ("Macro Layer Strength (0 = off)", Range(0, 1)) = 0.35
        _VariationScale ("Variation Size (m)", Float) = 3
        _VariationStrength ("Variation Strength (placeholder life)", Range(0, 0.5)) = 0.08

        [Header(Light)]
        _ShadeColor ("Shadow Tint (colours the sky fill in shadow)", Color) = (0.72, 0.72, 0.84, 1)
        _Wrap ("Light Wrap (soft terminator)", Range(0, 1)) = 0.15
        _Stylize ("Stylize (0 = soft light, 1 = two-band ramp)", Range(0, 1)) = 0
        _RampThreshold ("Ramp Threshold (stylize)", Range(-1, 1)) = 0.05
        _RampSmoothness ("Ramp Softness (stylize)", Range(0.001, 1)) = 0.12
        _AmbientStrength ("Sky Fill Strength", Range(0, 2)) = 0.45
        _EnvReflection ("Environment Reflection (from ambient SH)", Range(0, 2)) = 1

        [Header(Rim)]
        _RimColor ("Rim Colour", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.5
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.1

        [Header(Emission)]
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)

        [HideInInspector] _PatternScroll ("Belt Scroll (world m/s, set by Conveyor)", Vector) = (0, 0, 0, 0)
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
            #pragma vertex IndustrialVertex
            #pragma fragment IndustrialFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "HotPatataIndustrialInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS    : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                half3 normalOS    : TEXCOORD3;
                half fogFactor    : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings IndustrialVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            // The object's scale: the box mapping measures the surface in metres whatever the object is stretched to.
            float3 ObjectScale()
            {
                return float3(length(UNITY_MATRIX_M._m00_m10_m20),
                              length(UNITY_MATRIX_M._m01_m11_m21),
                              length(UNITY_MATRIX_M._m02_m12_m22));
            }

            // Box mapping: UV in tiles from the face's dominant axis. T x B = the face normal, so the texture is never
            // mirrored on the opposite faces and the normal map's frame is right-handed.
            void BoxMap(float3 positionOS, float3 normalOS, out float2 uv, out float3 tangentOS, out float3 bitangentOS)
            {
                float3 p = positionOS * ObjectScale();
                // A conveyor scrolls its texture (cosmetic): the world scroll, turned into the object's axes, in metres.
                float3 axisX = normalize(UNITY_MATRIX_M._m00_m10_m20), axisY = normalize(UNITY_MATRIX_M._m01_m11_m21), axisZ = normalize(UNITY_MATRIX_M._m02_m12_m22);
                p -= float3(dot(axisX, _PatternScroll.xyz), dot(axisY, _PatternScroll.xyz), dot(axisZ, _PatternScroll.xyz)) * _Time.y;
                float3 a = abs(normalOS);
                float3 s = step(0.0, normalOS) * 2.0 - 1.0;
                if (a.y >= a.x && a.y >= a.z)      { tangentOS = float3(0, 0, 1); bitangentOS = float3(s.y, 0, 0); }
                else if (a.x >= a.z)               { tangentOS = float3(0, 1, 0); bitangentOS = float3(0, 0, s.x); }
                else                               { tangentOS = float3(1, 0, 0); bitangentOS = float3(0, s.z, 0); }
                uv = float2(dot(tangentOS, p), dot(bitangentOS, p)) / max(0.01, _TileSize);
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Two octaves of value noise in 0..1: a faint blotchy variation, in metres, no texture needed.
            float Variation(float2 uvMetres)
            {
                float2 q = uvMetres / max(0.1, _VariationScale);
                float total = 0.0, amp = 0.6;
                [unroll] for (int i = 0; i < 2; i++)
                {
                    float2 f = frac(q), c = floor(q);
                    float2 w = f * f * (3.0 - 2.0 * f);
                    total += amp * lerp(lerp(Hash21(c), Hash21(c + float2(1, 0)), w.x),
                                        lerp(Hash21(c + float2(0, 1)), Hash21(c + float2(1, 1)), w.x), w.y);
                    q *= 2.3; amp *= 0.4;
                }
                return total / 0.84;
            }

            // How much a light reaches a face: a soft wrapped Lambert, nudged towards a two-band ramp by _Stylize.
            half Diffuse(half ndl)
            {
                half soft = saturate((ndl + _Wrap) / (1.0 + _Wrap));
                half band = smoothstep(_RampThreshold - _RampSmoothness, _RampThreshold + _RampSmoothness, ndl);
                return lerp(soft, band, _Stylize);
            }

            // GGX specular (normalised) with a Schlick Fresnel from f0 (0.04 dielectric, the albedo for a metal), times N.L.
            half3 Specular(half3 normalWS, half3 lightDir, half3 viewWS, half roughness, half3 f0)
            {
                half3 h = SafeNormalize(lightDir + viewWS);
                half nh = saturate(dot(normalWS, h));
                half lh = saturate(dot(lightDir, h));
                half a2 = max(roughness * roughness, 0.002);
                a2 *= a2;
                half d = nh * nh * (a2 - 1.0) + 1.0;
                half spec = a2 / (4.0 * PI * d * d * max(0.1, lh * lh) * (roughness + 0.5));
                half3 fresnel = f0 + (1.0 - f0) * pow(1.0 - lh, 5.0);
                return spec * fresnel * saturate(dot(normalWS, lightDir));
            }

            half4 IndustrialFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv; float3 tangentOS, bitangentOS;
                BoxMap(input.positionOS, normalize(input.normalOS), uv, tangentOS, bitangentOS);
                float2 metres = uv * max(0.01, _TileSize);

                half3 normalWS = normalize(input.normalWS);
                half3 tn = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                tn.y = _FlipNormalY > 0.5 ? -tn.y : tn.y;
                normalWS = normalize(normalWS * tn.z + TransformObjectToWorldDir(tangentOS) * tn.x + TransformObjectToWorldDir(bitangentOS) * tn.y);

                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);

                half3 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                texel = lerp(dot(texel, half3(0.299, 0.587, 0.114)).xxx, texel, _BaseSaturation) * _BaseBrightness;
                half3 albedo = texel * _BaseColor.rgb;
                if (_MacroStrength > 0.0)
                {
                    // The same texture at a larger scale, as a ratio to its own average: breaks the repetition, keeps the brightness.
                    half macro = dot(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv * _MacroScale + 0.37).rgb, half3(0.333, 0.333, 0.334));
                    half average = dot(SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, 10).rgb, half3(0.333, 0.333, 0.334));
                    albedo *= lerp(1.0, clamp(macro / max(0.05, average), 0.6, 1.5), _MacroStrength);
                }
                albedo *= 1.0 + (Variation(metres) - 0.5) * 2.0 * _VariationStrength;

                half gloss = SAMPLE_TEXTURE2D(_GlossMap, sampler_GlossMap, uv).r;
                gloss = _GlossIsRoughness > 0.5 ? 1.0 - gloss : gloss;
                half smoothness = lerp(_SmoothnessMin, _SmoothnessMax, gloss);
                half roughness = 1.0 - smoothness;
                half metal = saturate(SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, uv).r * _Metallic);
                half occlusion = lerp(1.0, SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).r, _OcclusionStrength);
                half3 diffuseColor = albedo * (1.0 - metal);
                half3 f0 = lerp(half3(0.04, 0.04, 0.04), albedo, metal);

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
                half3 color = diffuseColor * mainLight.color * lit;
                color += mainLight.color * Specular(normalWS, mainLight.direction, viewWS, roughness, f0) * shadow;

                // --- the sky fills the shadows (cool, deep, never black), times the baked-in occlusion map
                half3 fillTint = lerp(half3(1, 1, 1), _ShadeColor.rgb / max(0.01, dot(_ShadeColor.rgb, half3(0.333, 0.333, 0.334))), 0.35);
                half3 skyFill = SampleSH(normalWS) * (_AmbientStrength + 0.8) * fillTint * indirectAO * occlusion;
                color += diffuseColor * skyFill;

                // --- a cheap environment reflection from the ambient SH (no probe): what makes a metal read as metal
                half nv = saturate(dot(normalWS, viewWS));
                half3 envFresnel = lerp(f0, half3(1, 1, 1), pow(1.0 - nv, 5.0) * smoothness);   // roughness keeps a floor of reflection: a rough metal is dull, not black
                color += SampleSH(reflect(-viewWS, normalWS)) * (_AmbientStrength + 0.8) * fillTint * envFresnel * lerp(0.35, 1.0, smoothness)
                         * _EnvReflection * indirectAO * occlusion;

                // --- additional lights (point/spot)
                #if defined(_ADDITIONAL_LIGHTS)
                    uint lightCount = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                    {
                        Light l = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                        half a = l.distanceAttenuation * l.shadowAttenuation;
                        color += l.color * a * (diffuseColor * Diffuse(dot(normalWS, l.direction)) + Specular(normalWS, l.direction, viewWS, roughness, f0));
                    }
                    #endif
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = screenUV;
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light l = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                        half a = l.distanceAttenuation * l.shadowAttenuation;
                        color += l.color * a * (diffuseColor * Diffuse(dot(normalWS, l.direction)) + Specular(normalWS, l.direction, viewWS, roughness, f0));
                    LIGHT_LOOP_END
                #endif

                // --- rim: light-aware, strongest with the low sun behind the subject
                half fresnel = pow(saturate(1.0 - nv), _RimPower);
                half backlight = saturate(dot(-viewWS, mainLight.direction) * 0.5 + 0.5);
                color += _RimColor.rgb * mainLight.color * fresnel * _RimStrength * lerp(0.25, 1.4, backlight) * lerp(0.4, 1.0, shadow);

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
            #include "HotPatataIndustrialInput.hlsl"
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
            #include "HotPatataIndustrialInput.hlsl"
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
            #include "HotPatataIndustrialInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
