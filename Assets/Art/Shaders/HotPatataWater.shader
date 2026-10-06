// PatataWilds water (ARCHITECTURE §25.3): the river, pools and the waterfall. Every water surface is lethal (a KillZone just
// under it, built by NatureRiverBuilder); this shader only shows it:
//  - two normal maps scrolling along the mesh's V (the flow direction), at _FlowSpeed (high on the waterfall);
//  - the scene seen through the water (opaque texture, nudged by the normals) and darkened and tinted with depth (depth
//    texture), so the stony bed shows in the shallows and the pools turn deep green;
//  - foam where the water is shallow against rocks and banks, plus the mesh's vertex colour R (rapids, the fall's foot);
//  - the sky's reflection (the act's HDRI) with a Fresnel, a sharp sun glint, and fog.
// Transparent queue, no depth write and no shadow casting: drawn after the opaque copy, so it can read it.
Shader "HotPatata/Water"
{
    Properties
    {
        _ShallowColor ("Shallow Colour", Color) = (0.32, 0.42, 0.36, 1)
        _DeepColor ("Deep Colour", Color) = (0.04, 0.12, 0.11, 1)
        _DepthRange ("Depth of Full Colour (m)", Float) = 2.5
        _Clarity ("Clarity (how much of the bed shows)", Range(0, 1)) = 0.75
        [NoScaleOffset][Normal] _WaveMap ("Wave Normal", 2D) = "bump" {}
        _WaveScale ("Wave Tile Size (m)", Float) = 3
        _WaveStrength ("Wave Strength", Range(0, 2)) = 0.6
        _FlowSpeed ("Flow Speed (m/s along V)", Float) = 1.2
        _Refraction ("Refraction", Range(0, 0.2)) = 0.04
        [NoScaleOffset] _FoamMap ("Foam Noise (R)", 2D) = "white" {}
        _FoamColor ("Foam Colour", Color) = (0.92, 0.95, 0.93, 1)
        _FoamDepth ("Shore Foam Depth (m)", Float) = 0.35
        _FoamScale ("Foam Tile Size (m)", Float) = 2
        _Reflection ("Sky Reflection", Range(0, 2)) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.92
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _FoamColor;
                float _DepthRange, _WaveScale, _FlowSpeed, _FoamDepth, _FoamScale;
                half _Clarity, _WaveStrength, _Refraction, _Reflection, _Smoothness;
            CBUFFER_END

            TEXTURE2D(_WaveMap); SAMPLER(sampler_WaveMap);
            TEXTURE2D(_FoamMap); SAMPLER(sampler_FoamMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;          // metres: U across the stream, V along it (the plane's local Z)
                half foam : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                float4 screenPos : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.tangentWS = half4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
                // metres: the shared unit plane is scaled to its area, so the waves keep one size everywhere
                float2 scale = float2(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m02_m12_m22));
                o.uv = input.uv * scale;
                o.foam = input.color.r;
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                o.screenPos = ComputeScreenPos(pos.positionCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float t = _Time.y * _FlowSpeed;
                float2 uv1 = (i.uv + float2(0.0, -t)) / _WaveScale;
                float2 uv2 = (i.uv * 0.63 + float2(0.37, -t * 0.71)) / _WaveScale + 0.5;
                half3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_WaveMap, sampler_WaveMap, uv1), _WaveStrength);
                half3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_WaveMap, sampler_WaveMap, uv2), _WaveStrength);
                half3 tn = normalize(half3(n1.xy + n2.xy, n1.z * n2.z));
                half3 nGeo = normalize(i.normalWS);
                half3 tg = normalize(i.tangentWS.xyz);
                half3 bt = cross(nGeo, tg) * i.tangentWS.w;
                half3 normalWS = normalize(tn.x * tg + tn.y * bt + tn.z * nGeo);

                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float surfaceEye = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float2 refractUV = screenUV + tn.xy * _Refraction;
                float refractEye = LinearEyeDepth(SampleSceneDepth(refractUV), _ZBufferParams);
                if (refractEye < surfaceEye) { refractUV = screenUV; refractEye = sceneEye; }   // never refract what is in front
                half depth = max(0.0, refractEye - surfaceEye);
                half shoreDepth = max(0.0, sceneEye - surfaceEye);

                half3 bed = SampleSceneColor(refractUV);
                half deep = saturate(depth / max(0.01, _DepthRange));
                half3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, deep);
                half3 color = lerp(waterColor, bed * waterColor * 2.2, _Clarity * (1.0 - deep));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 viewWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                color *= lerp(0.55, 1.0, mainLight.shadowAttenuation);
                color += SampleSH(normalWS) * waterColor * 0.35;

                // foam: shallow water against the banks and stones, and the rapids painted in the vertex colour
                half foamNoise = SAMPLE_TEXTURE2D(_FoamMap, sampler_FoamMap, (i.uv + float2(0.0, -t * 0.8)) / _FoamScale).r;
                half shore = 1.0 - saturate(shoreDepth / max(0.01, _FoamDepth));
                half foam = saturate((shore * 1.2 + i.foam) * foamNoise * 1.6 - 0.25);
                color = lerp(color, _FoamColor.rgb * (mainLight.color * mainLight.shadowAttenuation + SampleSH(half3(0, 1, 0))), foam);

                half nv = saturate(dot(normalWS, viewWS));
                half fresnel = 0.02 + 0.98 * pow(1.0 - nv, 5.0);
                half perceptualRoughness = 1.0 - _Smoothness;
                half3 reflection = GlossyEnvironmentReflection(reflect(-viewWS, normalWS), perceptualRoughness, 1.0) * _Reflection;
                color = lerp(color, reflection, fresnel * (1.0 - foam));

                half3 h = SafeNormalize(mainLight.direction + viewWS);
                half spec = pow(saturate(dot(normalWS, h)), 600.0 * _Smoothness) * 6.0 * (1.0 - foam);
                color += mainLight.color * spec * mainLight.shadowAttenuation;

                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
