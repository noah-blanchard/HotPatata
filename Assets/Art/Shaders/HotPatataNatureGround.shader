// HotPatata nature ground: the layered ground of the authored levels (ARCHITECTURE §27.4, M21).
// Eight layers painted in the terrain's two splat control maps (bound by GroundSplat as _HP_Control0 and _HP_Control1),
// drawn in one pass from two texture arrays (GroundLayerBuilder: albedo with height in A; normal XY, occlusion, roughness):
//  - per pixel the two strongest layers, their weights less the third's (no seam where the third takes over), height-blended
//    so gravel sits in the cracks of a path instead of cross-fading;
//  - planar from above for the flat layers, triplanar for the steep ones (scree, rock), in world metres per layer;
//  - anti-tiling (_ANTITILE): a second sample turned and offset, mixed by a world noise; a world-space variation;
//  - moss on what faces north (_MossNorth), the terrain's holes (_TERRAIN_HOLES: a cave's mouth in a hillside);
//  - HotPatata/Nature's light model (wrapped Lambert, shade tint, sky fill, GGX, Forward+ lights, rim) and the height mist
//    (HP_MixFog). The editor can tint the hand-painted texels (_HP_ShowPainted, GroundSplat.ShowPainted).
Shader "HotPatata/NatureGround"
{
    Properties
    {
        [Header(Layers)]
        [NoScaleOffset] _GroundAlbedo ("Albedo (RGB) and Height (A) array", 2DArray) = "" {}
        [NoScaleOffset] _GroundNormal ("Normal XY (RG), Occlusion (B), Roughness (A) array", 2DArray) = "" {}
        _LayerTile0 ("Tile Size (m), layers 0-3", Vector) = (3, 3, 3, 2.5)
        _LayerTile1 ("Tile Size (m), layers 4-7", Vector) = (3, 3, 4, 6)
        _LayerTriplanar0 ("Triplanar (1), layers 0-3", Vector) = (0, 0, 0, 0)
        _LayerTriplanar1 ("Triplanar (1), layers 4-7", Vector) = (0, 0, 1, 1)
        _LayerBright0 ("Brightness, layers 0-3", Vector) = (1, 1, 1, 1)
        _LayerBright1 ("Brightness, layers 4-7", Vector) = (1, 1, 1, 1)
        _BlendDepth ("Height Blend Depth (0 = cross-fade)", Range(0.01, 1)) = 0.2
        _HeightContrast ("Height Contrast", Range(0, 2)) = 1
        _TriplanarSharpness ("Triplanar Blend Sharpness", Range(1, 16)) = 6
        _BumpScale ("Normal Strength", Range(0, 2)) = 1
        _BaseBrightness ("Texture Brightness", Range(0, 3)) = 1
        _BaseSaturation ("Texture Saturation", Range(0, 1.5)) = 1

        [Header(Roughness and occlusion)]
        _SmoothnessMin ("Smoothness at roughness 1", Range(0, 1)) = 0.02
        _SmoothnessMax ("Smoothness at roughness 0", Range(0, 1)) = 0.45
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
        _EnvReflection ("Environment Reflection", Range(0, 2)) = 0.6

        [Header(Repetition)]
        [Toggle(_ANTITILE)] _AntiTile ("Anti-tiling (a second turned sample)", Float) = 1
        _AntiTileScale ("Anti-tiling Noise Size (tiles)", Float) = 3
        _VariationScale ("Variation Size (m)", Float) = 6
        _VariationStrength ("Variation Strength", Range(0, 0.6)) = 0.12

        [Header(Moss)]
        _MossColor ("Moss Colour", Color) = (0.32, 0.4, 0.16, 1)
        _MossNorth ("Moss on North Faces", Range(0, 1)) = 0.35
        _MossNoise ("Moss Edge Noise", Range(0, 1)) = 0.6

        [Toggle(_TERRAIN_HOLES)] _TerrainHoles ("Terrain Holes", Float) = 1
        [HideInInspector][NoScaleOffset] _TerrainHolesTexture ("Terrain Holes (bound by the terrain)", 2D) = "white" {}

        [Header(Light)]
        _ShadeColor ("Shadow Tint", Color) = (0.72, 0.76, 0.86, 1)
        _Wrap ("Light Wrap", Range(0, 1)) = 0.2
        _AmbientStrength ("Sky Fill Strength", Range(0, 2)) = 0.45

        [Header(Rim)]
        _RimColor ("Rim Colour", Color) = (1, 0.92, 0.8, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.5
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry-100" "TerrainCompatible" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _LayerTile0, _LayerTile1;
            float4 _LayerTriplanar0, _LayerTriplanar1;
            float4 _LayerBright0, _LayerBright1;
            half _BlendDepth;
            half _HeightContrast;
            half _TriplanarSharpness;
            half _BumpScale;
            half _BaseBrightness;
            half _BaseSaturation;
            half _SmoothnessMin;
            half _SmoothnessMax;
            half _OcclusionStrength;
            half _EnvReflection;
            float _AntiTileScale;
            float _VariationScale;
            half _VariationStrength;
            half4 _MossColor;
            half _MossNorth;
            half _MossNoise;
            half4 _ShadeColor;
            half _Wrap;
            half _AmbientStrength;
            half4 _RimColor;
            half _RimPower;
            half _RimStrength;
        CBUFFER_END

        // Bound per terrain by GroundSplat (a property block): the eight weights, and the editor's painted overlay.
        TEXTURE2D(_HP_Control0); SAMPLER(sampler_HP_Control0);
        TEXTURE2D(_HP_Control1);
        TEXTURE2D(_HP_PaintedMask);
        float _HP_ControlRes;
        float _HP_ShowPainted;

        TEXTURE2D(_TerrainHolesTexture); SAMPLER(sampler_TerrainHolesTexture);

        struct GroundAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            float2 uv         : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        void GroundHoles(float2 uv)
        {
            #if defined(_TERRAIN_HOLES)
                clip(SAMPLE_TEXTURE2D(_TerrainHolesTexture, sampler_TerrainHolesTexture, uv).r - 0.5);
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GroundVertex
            #pragma fragment GroundFragment

            #pragma shader_feature_local _ANTITILE
            #pragma shader_feature_local _TERRAIN_HOLES
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "HotPatataFog.hlsl"

            TEXTURE2D_ARRAY(_GroundAlbedo); SAMPLER(sampler_GroundAlbedo);
            TEXTURE2D_ARRAY(_GroundNormal); SAMPLER(sampler_GroundNormal);

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS    : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                half fogFactor    : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings GroundVertex(GroundAttributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Value noise in 0..1, three octaves.
            float Noise(float2 q)
            {
                float total = 0.0, amp = 0.5;
                [unroll] for (int i = 0; i < 3; i++)
                {
                    float2 f = frac(q), c = floor(q);
                    float2 w = f * f * (3.0 - 2.0 * f);
                    total += amp * lerp(lerp(Hash21(c), Hash21(c + float2(1, 0)), w.x), lerp(Hash21(c + float2(0, 1)), Hash21(c + float2(1, 1)), w.x), w.y);
                    q *= 2.3; amp *= 0.5;
                }
                return total / 0.875;
            }

            struct LayerSample { half3 albedo; half height; half3 normalWS; half occlusion; half roughness; };

            half3 UnpackXY(half2 xy)
            {
                half3 n;
                n.xy = (xy * 2.0 - 1.0) * _BumpScale;
                n.z = sqrt(saturate(1.0 - dot(n.xy, n.xy)));
                return n;
            }

            // One projection of layer i: albedo + height, and the tangent-space normal, occlusion and roughness.
            void SampleProjection(float2 uv, int i, out half4 albedoHeight, out half3 normalTS, out half occlusion, out half roughness)
            {
                albedoHeight = SAMPLE_TEXTURE2D_ARRAY(_GroundAlbedo, sampler_GroundAlbedo, uv, i);
                half4 n = SAMPLE_TEXTURE2D_ARRAY(_GroundNormal, sampler_GroundNormal, uv, i);
                normalTS = UnpackXY(n.rg);
                occlusion = n.b;
                roughness = n.a;
            }

            // A layer's own value of a per-layer setting (layers 0-3 in lo, 4-7 in hi), picked by component: a local array indexed
            // by the layer came out as its first element.
            float LayerParam(float4 lo, float4 hi, int i)
            {
                float4 v = i < 4 ? lo : hi;
                int k = i - (i < 4 ? 0 : 4);
                return k == 0 ? v.x : k == 1 ? v.y : k == 2 ? v.z : v.w;
            }

            LayerSample SampleLayer(int i, float3 positionWS, half3 n)
            {
                float3 p = positionWS / max(0.05, LayerParam(_LayerTile0, _LayerTile1, i));
                LayerSample s;
                [branch] if (LayerParam(_LayerTriplanar0, _LayerTriplanar1, i) > 0.5)
                {
                    half3 w = pow(abs(n), _TriplanarSharpness);
                    w /= (w.x + w.y + w.z);
                    half3 axisSign = half3(n.x < 0 ? -1 : 1, n.y < 0 ? -1 : 1, n.z < 0 ? -1 : 1);
                    float2 uvX = p.zy, uvY = p.xz, uvZ = p.xy;
                    uvX.x *= axisSign.x; uvY.x *= axisSign.y; uvZ.x *= -axisSign.z;
                    half4 ax = 0, ay = 0, az = 0;
                    half3 tx = half3(0, 0, 1), ty = half3(0, 0, 1), tz = half3(0, 0, 1);
                    half ox = 1, oy = 1, oz = 1, rx = 1, ry = 1, rz = 1;
                    [branch] if (w.x > 0.01) SampleProjection(uvX + 0.31, i, ax, tx, ox, rx);
                    [branch] if (w.y > 0.01) SampleProjection(uvY, i, ay, ty, oy, ry);
                    [branch] if (w.z > 0.01) SampleProjection(uvZ + 0.67, i, az, tz, oz, rz);
                    half4 a = ax * w.x + ay * w.y + az * w.z;
                    s.albedo = a.rgb;
                    s.height = a.a;
                    s.occlusion = ox * w.x + oy * w.y + oz * w.z;
                    s.roughness = rx * w.x + ry * w.y + rz * w.z;
                    tx.x *= axisSign.x; ty.x *= axisSign.y; tz.x *= -axisSign.z;
                    tx = half3(tx.xy + n.zy, abs(tx.z) * n.x);
                    ty = half3(ty.xy + n.xz, abs(ty.z) * n.y);
                    tz = half3(tz.xy + n.xy, abs(tz.z) * n.z);
                    s.normalWS = normalize(tx.zyx * w.x + ty.xzy * w.y + tz.xyz * w.z);
                }
                else
                {
                    float2 uv = p.xz;
                    half4 a; half3 t; half o, r;
                    SampleProjection(uv, i, a, t, o, r);
                    #if defined(_ANTITILE)
                    {
                        // a second sample turned and offset, mixed in by a world noise: no repeat reads across a meadow
                        const float c = 0.5403, sn = 0.8415;   // a turn of one radian
                        float2 uv2 = float2(uv.x * c - uv.y * sn, uv.x * sn + uv.y * c) + float2(0.37 + i * 0.13, 0.71);
                        half4 a2; half3 t2; half o2, r2;
                        SampleProjection(uv2, i, a2, t2, o2, r2);
                        t2.xy = float2(t2.x * c + t2.y * sn, -t2.x * sn + t2.y * c);
                        half mixT = Noise(uv / max(0.5, _AntiTileScale) + i * 7.1);
                        mixT = saturate((mixT - 0.5) * 3.0 + 0.5 + (a2.a - a.a) * 0.8);
                        a = lerp(a, a2, mixT);
                        t = normalize(lerp(t, t2, mixT));
                        o = lerp(o, o2, mixT);
                        r = lerp(r, r2, mixT);
                    }
                    #endif
                    s.albedo = a.rgb;
                    s.height = a.a;
                    s.occlusion = o;
                    s.roughness = r;
                    half3 tw = half3(t.xy + n.xz, abs(t.z) * n.y);
                    s.normalWS = normalize(tw.xzy);
                }
                s.albedo *= LayerParam(_LayerBright0, _LayerBright1, i);
                return s;
            }

            half Diffuse(half ndl) { return saturate((ndl + _Wrap) / (1.0 + _Wrap)); }

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

            half4 GroundFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                GroundHoles(input.uv);
                half3 geomNormalWS = normalize(input.normalWS);

                // the eight weights, then the two strongest less the third (continuous where the third takes over)
                float res = max(2.0, _HP_ControlRes);
                float2 cuv = (input.uv * (res - 1.0) + 0.5) / res;
                half4 c0 = SAMPLE_TEXTURE2D(_HP_Control0, sampler_HP_Control0, cuv);
                half4 c1 = SAMPLE_TEXTURE2D(_HP_Control1, sampler_HP_Control0, cuv);
                half weights[8] = { c0.r, c0.g, c0.b, c0.a, c1.r, c1.g, c1.b, c1.a };
                int i1 = 0, i2 = 1;
                half w1 = -1, w2 = -1, w3 = 0;
                [unroll] for (int k = 0; k < 8; k++)
                {
                    half w = weights[k];
                    if (w > w1) { w3 = w2; w2 = w1; i2 = i1; w1 = w; i1 = k; }
                    else if (w > w2) { w3 = w2; w2 = w; i2 = k; }
                    else if (w > w3) { w3 = w; }
                }
                w3 = max(w3, 0.0);
                half a = max(w1 - w3, 1e-4), b = max(w2 - w3, 0.0);

                LayerSample s1 = SampleLayer(i1, input.positionWS, geomNormalWS);
                LayerSample s2 = s1;
                [branch] if (b > 0.002) s2 = SampleLayer(i2, input.positionWS, geomNormalWS);
                // height blend: the higher texel wins near the border of two layers
                half sum = a + b;
                a /= sum; b /= sum;
                half ha = s1.height * _HeightContrast + a, hb = s2.height * _HeightContrast + b;
                half top = max(ha, hb) - _BlendDepth;
                half ba = max(ha - top, 0.0), bb = max(hb - top, 0.0);
                half bs = max(ba + bb, 1e-4);
                ba /= bs; bb /= bs;

                half3 albedoTex = s1.albedo * ba + s2.albedo * bb;
                half3 normalWS = normalize(s1.normalWS * ba + s2.normalWS * bb);
                half occlusionMap = s1.occlusion * ba + s2.occlusion * bb;
                half roughness = s1.roughness * ba + s2.roughness * bb;

                half3 albedo = lerp(dot(albedoTex, half3(0.299, 0.587, 0.114)).xxx, albedoTex, _BaseSaturation) * _BaseBrightness;
                float noise = Noise(input.positionWS.xz / max(0.1, _VariationScale));
                albedo *= 1.0 + (noise - 0.5) * 2.0 * _VariationStrength;

                // moss where the ground faces north (+Z), its edge broken by the noise
                half north = saturate(normalWS.z * 2.2 - 0.25 + (noise - 0.5) * _MossNoise) * _MossNorth;
                albedo = lerp(albedo, _MossColor.rgb * (0.55 + dot(albedo, half3(0.333, 0.333, 0.334))), north);
                roughness = lerp(roughness, 0.95, north);
                half occlusion = lerp(1.0, occlusionMap, _OcclusionStrength);

                half smoothness = lerp(_SmoothnessMax, _SmoothnessMin, roughness);
                half perceptualRoughness = 1.0 - smoothness;
                half3 f0 = half3(0.04, 0.04, 0.04);

                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half directAO = 1.0, indirectAO = 1.0;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = CreateAmbientOcclusionFactor(screenUV, 1.0);
                    directAO = ao.directAmbientOcclusion;
                    indirectAO = ao.indirectAmbientOcclusion;
                #endif

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, half4(1, 1, 1, 1));
                half shadow = mainLight.shadowAttenuation * directAO;
                half3 color = albedo * mainLight.color * Diffuse(dot(normalWS, mainLight.direction)) * shadow;
                color += mainLight.color * Specular(normalWS, mainLight.direction, viewWS, perceptualRoughness, f0) * shadow;

                half3 fillTint = lerp(half3(1, 1, 1), _ShadeColor.rgb / max(0.01, dot(_ShadeColor.rgb, half3(0.333, 0.333, 0.334))), 0.35);
                color += albedo * SampleSH(normalWS) * (_AmbientStrength + 0.8) * fillTint * indirectAO * occlusion;

                half nv = saturate(dot(normalWS, viewWS));
                half3 envFresnel = lerp(f0, half3(1, 1, 1), pow(1.0 - nv, 5.0) * smoothness);
                color += GlossyEnvironmentReflection(reflect(-viewWS, normalWS), perceptualRoughness, occlusion * indirectAO) * envFresnel * _EnvReflection;

                #if defined(_ADDITIONAL_LIGHTS)
                    uint lightCount = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                    {
                        Light l = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                        half la = l.distanceAttenuation * l.shadowAttenuation;
                        color += l.color * la * (albedo * Diffuse(dot(normalWS, l.direction)) + Specular(normalWS, l.direction, viewWS, perceptualRoughness, f0));
                    }
                    #endif
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = screenUV;
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light l = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                        half la = l.distanceAttenuation * l.shadowAttenuation;
                        color += l.color * la * (albedo * Diffuse(dot(normalWS, l.direction)) + Specular(normalWS, l.direction, viewWS, perceptualRoughness, f0));
                    LIGHT_LOOP_END
                #endif

                half fresnel = pow(saturate(1.0 - nv), _RimPower);
                half backlight = saturate(dot(-viewWS, mainLight.direction) * 0.5 + 0.5);
                color += _RimColor.rgb * mainLight.color * fresnel * _RimStrength * lerp(0.25, 1.4, backlight) * lerp(0.4, 1.0, shadow);

                if (_HP_ShowPainted > 0.5)
                {
                    half painted = SAMPLE_TEXTURE2D(_HP_PaintedMask, sampler_HP_Control0, cuv).r;
                    color = lerp(color, half3(1.0, 0.25, 0.2), painted * 0.5);
                }

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
            #pragma target 3.5
            #pragma vertex GroundShadowVertex
            #pragma fragment GroundShadowFragment
            #pragma shader_feature_local _TERRAIN_HOLES
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            ShadowVaryings GroundShadowVertex(GroundAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                output.uv = input.uv;
                return output;
            }

            half4 GroundShadowFragment(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                GroundHoles(input.uv);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GroundDepthVertex
            #pragma fragment GroundDepthFragment
            #pragma shader_feature_local _TERRAIN_HOLES
            #pragma multi_compile_instancing

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings GroundDepthVertex(GroundAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half GroundDepthFragment(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                GroundHoles(input.uv);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GroundDepthNormalsVertex
            #pragma fragment GroundDepthNormalsFragment
            #pragma shader_feature_local _TERRAIN_HOLES
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthNormalsVaryings GroundDepthNormalsVertex(GroundAttributes input)
            {
                DepthNormalsVaryings output = (DepthNormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            void GroundDepthNormalsFragment(DepthNormalsVaryings input
                , out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                GroundHoles(input.uv);
                outNormalWS = half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
                #ifdef _WRITE_RENDERING_LAYERS
                    outRenderingLayers = EncodeMeshRenderingLayer();
                #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
