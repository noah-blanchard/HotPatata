// HotPatata nature: realistic outdoor surfaces for PatataWilds (ARCHITECTURE §25.3, M12).
// A fork of HotPatata/Industrial's light model (wrapped Lambert with real shadows, sky fill times SSAO, GGX, Forward+
// additional lights, light-aware rim) for rock, earth, wood and foliage:
//  - two mappings: triplanar in metres (_MAPPING_TRIPLANAR: rough rock slabs, cliffs, ground, soft blend between the three
//    projections from the object-space position times the object's scale, so it sticks to moving platforms and never
//    stretches), or the mesh's own UVs and tangents divided by _TileSize (generated logs and planks, foliage atlases,
//    Poly Haven models);
//  - Poly Haven maps as they come: base colour, OpenGL normal, roughness (not gloss), ambient occlusion;
//  - a top layer (_TOPLAYER): moss or grass on the faces that look up, broken by a world-space noise;
//  - foliage (_ALPHATEST_ON): a cut-out from a separate alpha map, both faces (_Cull), back faces lit from their own
//    side, light through the leaves (_Translucency);
//  - wind (_WIND): a trunk bend and a leaf flutter driven by the global _HP_Wind (TimeOfDayBlender), applied in every
//    pass so shadows and SSAO follow the plants;
//  - a dithered distance fade (_FadeEnd) for instanced foliage (FoliageInstancer);
//  - hazard stripes (_Stripe*) and emission (_EmissionColor), driven by the same code as the other kit shaders;
//  - the height mist (HotPatataFog.hlsl) and the sun's leaf cookie (_LIGHT_COOKIES, PatataCanopy's dapple).
// Repetition: hex tiling (_ANTITILE) on the floor projection or the UVs, a macro layer and a world-space variation.
Shader "HotPatata/Nature"
{
    Properties
    {
        [Header(Base)]
        [MainTexture][NoScaleOffset] _BaseMap ("Base Color (sRGB)", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color Tint", Color) = (1, 1, 1, 1)
        _BaseSaturation ("Texture Saturation", Range(0, 1.5)) = 1
        _BaseBrightness ("Texture Brightness", Range(0, 3)) = 1
        _TileSize ("Tile Size (metres per texture repeat; 1 for an atlas)", Float) = 2
        [Toggle(_MAPPING_TRIPLANAR)] _Triplanar ("Triplanar Mapping (else mesh UVs)", Float) = 0
        _TriplanarSharpness ("Triplanar Blend Sharpness", Range(1, 16)) = 6

        [Header(Normal)]
        [NoScaleOffset][Normal] _BumpMap ("Normal Map (OpenGL, Y+)", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 1

        [Header(Roughness and occlusion)]
        [NoScaleOffset] _RoughnessMap ("Roughness Map (R, linear)", 2D) = "white" {}
        _SmoothnessMin ("Smoothness at roughness 1", Range(0, 1)) = 0.02
        _SmoothnessMax ("Smoothness at roughness 0", Range(0, 1)) = 0.55
        [NoScaleOffset] _OcclusionMap ("Occlusion Map (R, linear)", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
        _EnvReflection ("Environment Reflection", Range(0, 2)) = 1

        [Header(Repetition)]
        [Toggle(_ANTITILE)] _AntiTile ("Anti-tiling (hex tiling on the UVs or the floor projection)", Float) = 0
        _AntiTileSharpness ("Anti-tiling Blend Sharpness", Range(1, 12)) = 6
        _AntiTileRotation ("Anti-tiling Rotation", Range(0, 1)) = 0.6
        _MacroScale ("Macro Layer Scale", Range(0.02, 1)) = 0.13
        _MacroStrength ("Macro Layer Strength", Range(0, 1)) = 0.35
        _VariationScale ("Variation Size (m)", Float) = 4
        _VariationStrength ("Variation Strength", Range(0, 0.6)) = 0.12

        [Header(Top layer)]
        [Toggle(_TOPLAYER)] _TopLayer ("Top Layer (moss, grass on faces looking up)", Float) = 0
        [NoScaleOffset] _TopMap ("Top Base Color", 2D) = "white" {}
        [NoScaleOffset][Normal] _TopBumpMap ("Top Normal", 2D) = "bump" {}
        _TopColor ("Top Tint", Color) = (1, 1, 1, 1)
        _TopTileSize ("Top Tile Size (m)", Float) = 3
        _TopCoverage ("Top Coverage", Range(0, 1)) = 0.35
        _TopSharpness ("Top Edge Sharpness", Range(1, 30)) = 8
        _TopNoise ("Top Edge Noise", Range(0, 1)) = 0.5

        [Header(Foliage)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Cut-out (foliage)", Float) = 0
        [NoScaleOffset] _AlphaMap ("Alpha Map (A, from greyscale)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.45
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull (Off for leaves)", Float) = 2
        _Translucency ("Translucency (light through leaves)", Range(0, 2)) = 0
        _TranslucencyColor ("Translucency Tint", Color) = (0.75, 0.9, 0.35, 1)

        [Header(Wind)]
        [Toggle(_WIND)] _Wind ("Wind Sway", Float) = 0
        _WindStrength ("Wind Strength", Range(0, 3)) = 1
        _WindFlutter ("Leaf Flutter", Range(0, 3)) = 1
        [ToggleUI] _WindVertexColor ("Weights From Vertex Colour (else from height)", Float) = 0
        _WindHeight ("Height of Full Sway (m, height weights)", Float) = 1

        [Header(Distance fade)]
        _FadeStart ("Fade Start (m)", Float) = 0
        _FadeEnd ("Fade End (m, 0 = never)", Float) = 0

        [Header(Light)]
        _ShadeColor ("Shadow Tint", Color) = (0.72, 0.76, 0.86, 1)
        _Wrap ("Light Wrap", Range(0, 1)) = 0.2
        _AmbientStrength ("Sky Fill Strength", Range(0, 2)) = 0.45

        [Header(Rim)]
        _RimColor ("Rim Colour", Color) = (1, 0.92, 0.8, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.5
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.08

        [Header(Hazard stripes)]
        _StripeColor ("Stripe Colour", Color) = (0.05, 0.035, 0.025, 1)
        _StripeStrength ("Stripe Strength (0 = off)", Range(0, 1)) = 0
        _StripeScale ("Stripe Width (m)", Float) = 0.45

        [Header(Emission)]
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)

        [HideInInspector] _PatternScroll ("Belt Scroll (world m/s, set by Conveyor)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull [_Cull]

        HLSLINCLUDE
        #include "HotPatataNatureInput.hlsl"

        struct NatureAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            float4 tangentOS  : TANGENT;
            float2 uv         : TEXCOORD0;
            half4 color       : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float3 ObjectScale()
        {
            return float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
        }

        // The vertex in world space, wind applied.
        float3 NaturePositionWS(NatureAttributes input, float3 normalWS)
        {
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            #if defined(_WIND)
                positionWS += NatureWind(positionWS, normalWS, NatureWindWeights(input.positionOS.xyz, input.color));
            #endif
            return positionWS;
        }

        void NatureAlphaClip(float2 uv)
        {
            #if defined(_ALPHATEST_ON)
                clip(SAMPLE_TEXTURE2D(_AlphaMap, sampler_AlphaMap, uv).a * _BaseColor.a - _Cutoff);
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex NatureVertex
            #pragma fragment NatureFragment

            #pragma shader_feature_local _MAPPING_TRIPLANAR
            #pragma shader_feature_local _ANTITILE
            #pragma shader_feature_local _TOPLAYER
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _WIND
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

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS    : TEXCOORD1;
                half4 tangentWS   : TEXCOORD2;
                float3 positionM  : TEXCOORD3;   // object space in metres (scale applied, belt scroll removed)
                half3 normalM     : TEXCOORD4;   // the normal in that metric object space
                float2 uv         : TEXCOORD5;   // mesh UVs in tiles (belt scroll applied)
                float2 rawUV      : TEXCOORD6;   // mesh UVs as authored (the alpha map)
                half fogFactor    : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings NatureVertex(NatureAttributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 positionWS = NaturePositionWS(input, normalWS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.tangentWS = half4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w * GetOddNegativeScale());

                float3 scale = ObjectScale();
                // A conveyor scrolls its texture (cosmetic): the world scroll turned into the object's axes, in metres.
                float3 axisX = normalize(UNITY_MATRIX_M._m00_m10_m20), axisY = normalize(UNITY_MATRIX_M._m01_m11_m21), axisZ = normalize(UNITY_MATRIX_M._m02_m12_m22);
                float3 scrollM = float3(dot(axisX, _PatternScroll.xyz), dot(axisY, _PatternScroll.xyz), dot(axisZ, _PatternScroll.xyz)) * _Time.y;
                output.positionM = input.positionOS.xyz * scale - scrollM;
                output.normalM = normalize(input.normalOS / max(scale, 1e-4));
                output.uv = input.uv / max(0.01, _TileSize) - scrollM.xz / max(0.01, _TileSize);
                output.rawUV = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Value noise in 0..1, three octaves in WORLD metres: neighbouring rocks and slabs never look alike.
            float Variation(float2 worldMetres)
            {
                float2 q = worldMetres / max(0.1, _VariationScale);
                float total = 0.0, amp = 0.5;
                [unroll] for (int i = 0; i < 3; i++)
                {
                    float2 f = frac(q), c = floor(q);
                    float2 w = f * f * (3.0 - 2.0 * f);
                    total += amp * lerp(lerp(Hash21(c), Hash21(c + float2(1, 0)), w.x),
                                        lerp(Hash21(c + float2(0, 1)), Hash21(c + float2(1, 1)), w.x), w.y);
                    q /= 4.3; amp *= 0.6;
                }
                return total / 0.98;
            }

            struct SurfaceSample { half3 albedo; half3 normalTS; half roughness; half occlusion; };

            #if defined(_ANTITILE)
            // Hex tiling (Mikkelsen, "Practical Real-Time Hex-Tiling"), as in HotPatata/Industrial.
            struct HexData { float2 uv1, uv2, uv3; float2x2 rot1, rot2, rot3; float3 w; float2 dx, dy; };

            float2 HexHash(float2 p) { return frac(sin(mul(float2x2(127.1, 311.7, 269.5, 183.3), p)) * 43758.5453); }
            float2 HexCentre(int2 v) { return mul(float2x2(1.0, 0.5, 0.0, 1.0 / 1.15470054), float2(v)) / 3.464; }

            float2x2 HexRot(int2 v)
            {
                float a = fmod(abs(v.x * v.y) + abs(v.x + v.y) + PI, 2.0 * PI);
                if (a > PI) a -= 2.0 * PI;
                a *= _AntiTileRotation;
                float c = cos(a), s = sin(a);
                return float2x2(c, -s, s, c);
            }

            HexData MakeHex(float2 uv)
            {
                HexData h;
                float2 skew = mul(float2x2(1.0, 0.0, -0.57735027, 1.15470054), uv * 3.464);
                int2 baseId = int2(floor(skew));
                float3 t = float3(frac(skew), 0.0);
                t.z = 1.0 - t.x - t.y;
                float s = step(0.0, -t.z), s2 = 2.0 * s - 1.0;
                float3 w = float3(-t.z * s2, s - t.y * s2, s - t.x * s2);
                int2 v1 = baseId + int2(s, s), v2 = baseId + int2(s, 1.0 - s), v3 = baseId + int2(1.0 - s, s);
                h.rot1 = HexRot(v1); h.rot2 = HexRot(v2); h.rot3 = HexRot(v3);
                float2 c1 = HexCentre(v1), c2 = HexCentre(v2), c3 = HexCentre(v3);
                h.uv1 = mul(uv - c1, h.rot1) + c1 + HexHash(float2(v1));
                h.uv2 = mul(uv - c2, h.rot2) + c2 + HexHash(float2(v2));
                h.uv3 = mul(uv - c3, h.rot3) + c3 + HexHash(float2(v3));
                float3 ws = pow(max(w, 1e-4), _AntiTileSharpness);
                h.w = ws / (ws.x + ws.y + ws.z);
                h.dx = ddx(uv);
                h.dy = ddy(uv);
                return h;
            }

            half4 HexSample(TEXTURE2D_PARAM(tex, samp), HexData h)
            {
                return SAMPLE_TEXTURE2D_GRAD(tex, samp, h.uv1, mul(h.dx, h.rot1), mul(h.dy, h.rot1)) * h.w.x
                     + SAMPLE_TEXTURE2D_GRAD(tex, samp, h.uv2, mul(h.dx, h.rot2), mul(h.dy, h.rot2)) * h.w.y
                     + SAMPLE_TEXTURE2D_GRAD(tex, samp, h.uv3, mul(h.dx, h.rot3), mul(h.dy, h.rot3)) * h.w.z;
            }

            half3 HexNormal(TEXTURE2D_PARAM(tex, samp), HexData h, half scale)
            {
                half3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(tex, samp, h.uv1, mul(h.dx, h.rot1), mul(h.dy, h.rot1)), scale);
                half3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(tex, samp, h.uv2, mul(h.dx, h.rot2), mul(h.dy, h.rot2)), scale);
                half3 n3 = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(tex, samp, h.uv3, mul(h.dx, h.rot3), mul(h.dy, h.rot3)), scale);
                n1.xy = mul(h.rot1, n1.xy); n2.xy = mul(h.rot2, n2.xy); n3.xy = mul(h.rot3, n3.xy);
                return normalize(n1 * h.w.x + n2 * h.w.y + n3 * h.w.z);
            }
            #endif

            // One projection of the surface maps; hex tiled when asked (the floors, or the UV mapping).
            SurfaceSample SampleSurface(float2 uv, bool hex)
            {
                SurfaceSample s;
                #if defined(_ANTITILE)
                if (hex)
                {
                    HexData h = MakeHex(uv);
                    s.albedo = HexSample(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), h).rgb;
                    s.normalTS = HexNormal(TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), h, _BumpScale);
                    s.roughness = HexSample(TEXTURE2D_ARGS(_RoughnessMap, sampler_RoughnessMap), h).r;
                    s.occlusion = HexSample(TEXTURE2D_ARGS(_OcclusionMap, sampler_OcclusionMap), h).r;
                    return s;
                }
                #endif
                s.albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                s.normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                s.roughness = SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, uv).r;
                s.occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).r;
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

            half4 NatureFragment(Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                NatureDistanceClip(input.positionWS, input.positionCS);
                NatureAlphaClip(input.rawUV);

                half faceSign = frontFace ? 1.0 : -1.0;
                half3 geomNormalWS = normalize(input.normalWS) * faceSign;
                float3 axisX = normalize(UNITY_MATRIX_M._m00_m10_m20), axisY = normalize(UNITY_MATRIX_M._m01_m11_m21), axisZ = normalize(UNITY_MATRIX_M._m02_m12_m22);

                half3 albedoTex;
                half roughnessMap, occlusionMap;
                half3 normalWS;
                float2 stripeMetres;
                #if defined(_MAPPING_TRIPLANAR)
                {
                    float3 p = input.positionM / max(0.01, _TileSize);
                    half3 n = normalize(input.normalM) * faceSign;
                    half3 w = pow(abs(n), _TriplanarSharpness);
                    w /= (w.x + w.y + w.z);
                    half3 axisSign = half3(n.x < 0 ? -1 : 1, n.y < 0 ? -1 : 1, n.z < 0 ? -1 : 1);
                    float2 uvX = p.zy, uvY = p.xz, uvZ = p.xy;
                    uvX.x *= axisSign.x; uvY.x *= axisSign.y; uvZ.x *= -axisSign.z;
                    SurfaceSample sx = (SurfaceSample)0, sy = (SurfaceSample)0, sz = (SurfaceSample)0;
                    sx.normalTS = sy.normalTS = sz.normalTS = half3(0, 0, 1);
                    [branch] if (w.x > 0.01) sx = SampleSurface(uvX + 0.31, false);
                    [branch] if (w.y > 0.01) sy = SampleSurface(uvY, true);
                    [branch] if (w.z > 0.01) sz = SampleSurface(uvZ + 0.67, false);
                    albedoTex = sx.albedo * w.x + sy.albedo * w.y + sz.albedo * w.z;
                    roughnessMap = sx.roughness * w.x + sy.roughness * w.y + sz.roughness * w.z;
                    occlusionMap = sx.occlusion * w.x + sy.occlusion * w.y + sz.occlusion * w.z;
                    // whiteout blend of the three tangent-space normals (Golus, "Normal Mapping for a Triplanar Shader")
                    half3 tx = sx.normalTS, ty = sy.normalTS, tz = sz.normalTS;
                    tx.x *= axisSign.x; ty.x *= axisSign.y; tz.x *= -axisSign.z;
                    tx = half3(tx.xy + n.zy, abs(tx.z) * n.x);
                    ty = half3(ty.xy + n.xz, abs(ty.z) * n.y);
                    tz = half3(tz.xy + n.xy, abs(tz.z) * n.z);
                    half3 nM = normalize(tx.zyx * w.x + ty.xzy * w.y + tz.xyz * w.z);
                    normalWS = normalize(axisX * nM.x + axisY * nM.y + axisZ * nM.z);
                    stripeMetres = float2(input.positionM.x + input.positionM.z, input.positionM.y);
                }
                #else
                {
                    SurfaceSample s = SampleSurface(input.uv, true);
                    albedoTex = s.albedo;
                    roughnessMap = s.roughness;
                    occlusionMap = s.occlusion;
                    half3 t = normalize(input.tangentWS.xyz) * faceSign;
                    half3 b = cross(geomNormalWS, t) * input.tangentWS.w;
                    normalWS = normalize(s.normalTS.x * t + s.normalTS.y * b + s.normalTS.z * geomNormalWS);
                    stripeMetres = input.uv * max(0.01, _TileSize);
                }
                #endif

                half3 texel = lerp(dot(albedoTex, half3(0.299, 0.587, 0.114)).xxx, albedoTex, _BaseSaturation) * _BaseBrightness;
                half3 albedo = texel * _BaseColor.rgb;
                if (_MacroStrength > 0.0)
                {
                    float2 macroUV = input.positionWS.xz / max(0.01, _TileSize);
                    half macro = dot(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, macroUV * _MacroScale + 0.37).rgb, half3(0.333, 0.333, 0.334));
                    half average = dot(SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, macroUV, 10).rgb, half3(0.333, 0.333, 0.334));
                    albedo *= lerp(1.0, clamp(macro / max(0.05, average), 0.6, 1.5), _MacroStrength);
                }
                float noise = Variation(input.positionWS.xz + input.positionWS.y * 0.37);
                albedo *= 1.0 + (noise - 0.5) * 2.0 * _VariationStrength;
                half roughness = roughnessMap;
                half occlusion = lerp(1.0, occlusionMap, _OcclusionStrength);

                #if defined(_TOPLAYER)
                {
                    // moss or grass where the surface looks up, its edge broken by the world noise
                    float2 topUV = input.positionWS.xz / max(0.01, _TopTileSize);
                    half up = dot(normalWS, half3(0, 1, 0));
                    half mask = saturate((up - (1.0 - _TopCoverage) + (noise - 0.5) * _TopNoise) * _TopSharpness);
                    half3 topAlbedo = SAMPLE_TEXTURE2D(_TopMap, sampler_TopMap, topUV).rgb * _TopColor.rgb;
                    half3 topN = UnpackNormalScale(SAMPLE_TEXTURE2D(_TopBumpMap, sampler_TopBumpMap, topUV), _BumpScale);
                    half3 topNormalWS = normalize(half3(topN.x, 0, topN.y) + half3(0, topN.z, 0));
                    albedo = lerp(albedo, topAlbedo, mask);
                    normalWS = normalize(lerp(normalWS, topNormalWS, mask * 0.85));
                    roughness = lerp(roughness, 0.92, mask);
                }
                #endif

                if (_StripeStrength > 0.0)
                {
                    // Diagonal charred bands in metres (spec section 19: a hazard never relies on colour alone).
                    half stripe = step(0.5, frac((stripeMetres.x + stripeMetres.y) / max(0.05, _StripeScale) * 0.5));
                    albedo = lerp(albedo, _StripeColor.rgb, stripe * _StripeStrength);
                    roughness = lerp(roughness, 0.95, stripe * _StripeStrength);
                }

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

                // light through thin leaves and blades, strongest with the sun behind them
                if (_Translucency > 0.0)
                {
                    half back = pow(saturate(dot(viewWS, -mainLight.direction)), 3.0);
                    half through = saturate(dot(-geomNormalWS, mainLight.direction)) * 0.5 + 0.5;
                    color += albedo * _TranslucencyColor.rgb * mainLight.color * back * through * _Translucency * mainLight.shadowAttenuation;
                }

                // the sky fills the shadows, times the occlusion map
                half3 fillTint = lerp(half3(1, 1, 1), _ShadeColor.rgb / max(0.01, dot(_ShadeColor.rgb, half3(0.333, 0.333, 0.334))), 0.35);
                color += albedo * SampleSH(normalWS) * (_AmbientStrength + 0.8) * fillTint * indirectAO * occlusion;

                // glossy reflection of the sky (the act's HDRI, set as the scene's reflection by TimeOfDayBlender)
                half nv = saturate(dot(normalWS, viewWS));
                half3 envFresnel = lerp(f0, half3(1, 1, 1), pow(1.0 - nv, 5.0) * smoothness);
                color += GlossyEnvironmentReflection(reflect(-viewWS, normalWS), perceptualRoughness, occlusion * indirectAO) * envFresnel * _EnvReflection;

                #if defined(_ADDITIONAL_LIGHTS)
                    uint lightCount = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                    {
                        Light l = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                        half a = l.distanceAttenuation * l.shadowAttenuation;
                        color += l.color * a * (albedo * Diffuse(dot(normalWS, l.direction)) + Specular(normalWS, l.direction, viewWS, perceptualRoughness, f0));
                    }
                    #endif
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = screenUV;
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light l = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                        half a = l.distanceAttenuation * l.shadowAttenuation;
                        color += l.color * a * (albedo * Diffuse(dot(normalWS, l.direction)) + Specular(normalWS, l.direction, viewWS, perceptualRoughness, f0));
                    LIGHT_LOOP_END
                #endif

                half fresnel = pow(saturate(1.0 - nv), _RimPower);
                half backlight = saturate(dot(-viewWS, mainLight.direction) * 0.5 + 0.5);
                color += _RimColor.rgb * mainLight.color * fresnel * _RimStrength * lerp(0.25, 1.4, backlight) * lerp(0.4, 1.0, shadow);

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
            #pragma target 3.5
            #pragma vertex NatureShadowVertex
            #pragma fragment NatureShadowFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _WIND
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

            ShadowVaryings NatureShadowVertex(NatureAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 positionWS = NaturePositionWS(input, normalWS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                output.uv = input.uv;
                return output;
            }

            half4 NatureShadowFragment(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                NatureAlphaClip(input.uv);
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
            #pragma vertex NatureDepthVertex
            #pragma fragment NatureDepthFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _WIND
            #pragma multi_compile_instancing

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings NatureDepthVertex(NatureAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = NaturePositionWS(input, TransformObjectToWorldNormal(input.normalOS));
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.uv = input.uv;
                return output;
            }

            half NatureDepthFragment(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                NatureDistanceClip(input.positionWS, input.positionCS);
                NatureAlphaClip(input.uv);
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
            #pragma vertex NatureDepthNormalsVertex
            #pragma fragment NatureDepthNormalsFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _WIND
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthNormalsVaryings NatureDepthNormalsVertex(NatureAttributes input)
            {
                DepthNormalsVaryings output = (DepthNormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 positionWS = NaturePositionWS(input, normalWS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.uv = input.uv;
                return output;
            }

            void NatureDepthNormalsFragment(DepthNormalsVaryings input, bool frontFace : SV_IsFrontFace
                , out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                NatureDistanceClip(input.positionWS, input.positionCS);
                NatureAlphaClip(input.uv);
                outNormalWS = half4(NormalizeNormalPerPixel(input.normalWS * (frontFace ? 1.0 : -1.0)), 0.0);
                #ifdef _WRITE_RENDERING_LAYERS
                    outRenderingLayers = EncodeMeshRenderingLayer();
                #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
