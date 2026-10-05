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
//  - anti-repetition, four layers: hex tiling (_ANTITILE: each texture is sampled three times at random offsets and
//    rotations and blended, so no tile is visibly repeated), a macro layer of the same texture at a larger scale, a
//    multi-scale procedural variation in world metres (no repeat inside a room), and an optional grunge mask (dirt,
//    streaks) at a large world scale that darkens and roughens;
//  - decals (_Decal): the same lighting on an alpha-blended quad with its own UVs, drawn just above a surface.
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
        [ToggleUI] _FlipNormalY ("Flip Normal Y (for DirectX normal maps)", Float) = 0

        [Header(Smoothness)]
        [NoScaleOffset] _GlossMap ("Gloss or Roughness Map (R, linear)", 2D) = "gray" {}
        [ToggleUI] _GlossIsRoughness ("Map Is Roughness (inverted in the shader)", Float) = 0
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
        _VariationScale ("Variation Size (m, three octaves of 1x, 4.3x and 17x)", Float) = 3
        _VariationStrength ("Variation Strength (world-space, no repeat)", Range(0, 0.6)) = 0.08
        [Toggle(_ANTITILE)] _AntiTile ("Anti-tiling (hex tiling: 3 samples per map; keep off for bricks, tiles and plates)", Float) = 0
        _AntiTileSharpness ("Anti-tiling Blend Sharpness", Range(1, 12)) = 6
        _AntiTileRotation ("Anti-tiling Rotation (0 = offsets only, 1 = random turns)", Range(0, 1)) = 0.6

        [Header(Grunge)]
        [NoScaleOffset] _GrungeMap ("Grunge Mask (R, linear: 0 = dirt, 1 = clean)", 2D) = "white" {}
        _GrungeSize ("Grunge Size (world metres)", Float) = 9
        _GrungeStrength ("Grunge Darkening", Range(0, 1)) = 0
        _GrungeRoughness ("Grunge Roughness (dirt is less glossy)", Range(0, 1)) = 0.5

        [Header(Decal)]
        [ToggleUI] _Decal ("Decal (mesh UVs, no normal map; the blending is set by IndustrialDecals, not by this box)", Float) = 0

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

        [Header(Hazard stripes)]
        _StripeColor ("Stripe Colour", Color) = (0.03, 0.03, 0.03, 1)
        _StripeStrength ("Stripe Strength (0 = off)", Range(0, 1)) = 0
        _StripeScale ("Stripe Width (m)", Float) = 0.5

        [Header(Emission)]
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)

        [HideInInspector] _SrcBlend ("Src Blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 0
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
        [HideInInspector] _OffsetFactor ("Offset Factor", Float) = 0
        [HideInInspector] _OffsetUnits ("Offset Units", Float) = 0
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
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Offset [_OffsetFactor], [_OffsetUnits]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex IndustrialVertex
            #pragma fragment IndustrialFragment

            #pragma shader_feature_local _ANTITILE
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
                float2 uv         : TEXCOORD0;
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
                float2 uv         : TEXCOORD5;
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
                output.uv = input.uv;
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

            // Value noise in 0..1, three octaves (1x, 4.3x and 17x the size) in WORLD metres, so neighbouring slabs and rooms
            // differ and nothing repeats inside a room. Texture-less materials get their life from this alone.
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
                return total / 0.98;   // 0.5 + 0.3 + 0.18: back to 0..1
            }

            // The face's plane in world metres (dominant axis of the world normal).
            float2 WorldPlane(float3 p, float3 n)
            {
                float3 a = abs(n);
                return a.y >= a.x && a.y >= a.z ? p.xz : (a.x >= a.z ? p.zy : p.xy);
            }

            #if defined(_ANTITILE)
            // Hex tiling (Mikkelsen, "Practical Real-Time Hex-Tiling"): the plane is cut into a triangle grid; each corner turns
            // and shifts the texture by its own random amount, and the three samples are blended by the triangle's weights.
            struct HexData { float2 uv1, uv2, uv3; float2x2 rot1, rot2, rot3; float3 w; float2 dx, dy; };

            float2 HexHash(float2 p)
            {
                float2 r = mul(float2x2(127.1, 311.7, 269.5, 183.3), p);
                return frac(sin(r) * 43758.5453);
            }

            float2 HexCentre(int2 v)
            {
                return mul(float2x2(1.0, 0.5, 0.0, 1.0 / 1.15470054), float2(v)) / 3.464;
            }

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

            // A normal map under hex tiling: each sample's xy is turned back by its corner's rotation before blending.
            half3 HexNormal(TEXTURE2D_PARAM(tex, samp), HexData h, half scale)
            {
                half3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(tex, samp, h.uv1, mul(h.dx, h.rot1), mul(h.dy, h.rot1)), scale);
                half3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(tex, samp, h.uv2, mul(h.dx, h.rot2), mul(h.dy, h.rot2)), scale);
                half3 n3 = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(tex, samp, h.uv3, mul(h.dx, h.rot3), mul(h.dy, h.rot3)), scale);
                n1.xy = mul(h.rot1, n1.xy); n2.xy = mul(h.rot2, n2.xy); n3.xy = mul(h.rot3, n3.xy);
                return normalize(n1 * h.w.x + n2 * h.w.y + n3 * h.w.z);
            }
            #endif

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

                half3 normalWS = normalize(input.normalWS);
                bool decal = _Decal > 0.5;
                float2 mapUV = decal ? input.uv : uv;   // a decal is drawn with its own UVs, not box mapped
                half4 texel4;
                half3 tn;
                half gloss, metalMap, occlusionMap;
                #if defined(_ANTITILE)
                    if (!decal)
                    {
                        HexData hex = MakeHex(uv);
                        texel4 = HexSample(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), hex);
                        tn = HexNormal(TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), hex, _BumpScale);
                        gloss = HexSample(TEXTURE2D_ARGS(_GlossMap, sampler_GlossMap), hex).r;
                        metalMap = HexSample(TEXTURE2D_ARGS(_MetallicMap, sampler_MetallicMap), hex).r;
                        occlusionMap = HexSample(TEXTURE2D_ARGS(_OcclusionMap, sampler_OcclusionMap), hex).r;
                    }
                    else
                #endif
                    {
                        texel4 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, mapUV);
                        tn = decal ? half3(0, 0, 1) : UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, mapUV), _BumpScale);
                        gloss = SAMPLE_TEXTURE2D(_GlossMap, sampler_GlossMap, mapUV).r;
                        metalMap = SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, mapUV).r;
                        occlusionMap = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, mapUV).r;
                    }
                tn.y = _FlipNormalY > 0.5 ? -tn.y : tn.y;
                normalWS = normalize(normalWS * tn.z + TransformObjectToWorldDir(tangentOS) * tn.x + TransformObjectToWorldDir(bitangentOS) * tn.y);

                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);

                half3 texel = texel4.rgb;
                texel = lerp(dot(texel, half3(0.299, 0.587, 0.114)).xxx, texel, _BaseSaturation) * _BaseBrightness;
                half3 albedo = texel * _BaseColor.rgb;
                if (_MacroStrength > 0.0 && !decal)
                {
                    // The same texture at a larger scale, as a ratio to its own average: breaks the repetition, keeps the brightness.
                    half macro = dot(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv * _MacroScale + 0.37).rgb, half3(0.333, 0.333, 0.334));
                    half average = dot(SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, 10).rgb, half3(0.333, 0.333, 0.334));
                    albedo *= lerp(1.0, clamp(macro / max(0.05, average), 0.6, 1.5), _MacroStrength);
                }
                // the geometric normal picks the plane (the mapped normal would flip it back and forth on bevels); world space,
                // so on a moving platform the variation and the grunge slide, which their low contrast hides
                float2 worldMetres = WorldPlane(input.positionWS, normalize(input.normalWS));
                if (!decal) albedo *= 1.0 + (Variation(worldMetres) - 0.5) * 2.0 * _VariationStrength;
                half dirt = 0.0;
                if (_GrungeStrength > 0.0 && !decal)
                {
                    half clean = SAMPLE_TEXTURE2D(_GrungeMap, sampler_GrungeMap, worldMetres / max(0.5, _GrungeSize)).r;
                    dirt = (1.0 - clean) * _GrungeStrength;
                    albedo *= 1.0 - dirt * 0.75;
                }
                float2 metres = uv * max(0.01, _TileSize);
                if (_StripeStrength > 0.0)
                {
                    // Diagonal hazard stripes in metres (spec section 19: a hazard never relies on colour alone).
                    half stripe = step(0.5, frac((metres.x + metres.y) / max(0.05, _StripeScale) * 0.5));
                    albedo = lerp(albedo, _StripeColor.rgb, stripe * _StripeStrength);
                }

                gloss = _GlossIsRoughness > 0.5 ? 1.0 - gloss : gloss;
                half smoothness = lerp(_SmoothnessMin, _SmoothnessMax, gloss) * (1.0 - dirt * _GrungeRoughness);
                half roughness = 1.0 - smoothness;
                half metal = saturate(metalMap * _Metallic);
                half occlusion = lerp(1.0, occlusionMap, _OcclusionStrength);
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
                return half4(color, decal ? saturate(texel4.a * _BaseColor.a) : 1.0);
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
