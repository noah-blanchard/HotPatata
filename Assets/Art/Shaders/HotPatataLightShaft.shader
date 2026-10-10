// God rays through the canopy (ARCHITECTURE §25.3, PatataCanopy): a soft additive shaft of sunlight hanging from a gap in the
// crowns. The mesh is a unit quad (u across, v down the shaft); the vertex shader lays it along the sun's direction (bent towards
// the vertical, so a low sun never sends a shaft across the course) and turns it to face the camera around that axis; the
// object's (uniform) scale is the shaft's length, the mesh's X extent its width as a fraction of it, its position the top. Strength = the moment's shaft strength
// (_HP_Shafts.x, TimeOfDayBlender through MistField) times the sun's colour: none at dusk. It fades near the camera, against
// geometry (depth) and far away, and is strongest looking towards the sun. Presentation only, never a flash.
Shader "HotPatata/LightShaft"
{
    Properties
    {
        [HDR] _BaseColor ("Tint", Color) = (1, 0.95, 0.82, 1)
        _Intensity ("Intensity", Range(0, 1)) = 0.08
        _Vertical ("Bend Towards Vertical", Range(0, 3)) = 1.2
        _FadeNear ("Fade Near Camera (m)", Float) = 6
        _Softness ("Soft Edge Against Geometry (m)", Float) = 3
        _FadeFar ("Fade Out By (m)", Float) = 240
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Intensity, _Vertical;
                float _FadeNear, _Softness, _FadeFar;
            CBUFFER_END

            float4 _HP_Shafts;   // x = the moment's shaft strength (0 = none)

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float seed : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 ShaftAxis()
            {
                return normalize(-_MainLightPosition.xyz + float3(0, -_Vertical, 0));
            }

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 top = UNITY_MATRIX_M._m03_m13_m23;
                float len = length(UNITY_MATRIX_M._m01_m11_m21);
                float3 axis = ShaftAxis();
                float3 mid = top + axis * len * 0.5;
                float3 toCamera = normalize(_WorldSpaceCameraPos - mid);
                float3 side = cross(axis, toCamera);
                side = dot(side, side) > 1e-4 ? normalize(side) : float3(1, 0, 0);
                float v = input.uv.y;
                float3 positionWS = top + axis * (v * len) + side * (input.positionOS.x * len * lerp(0.7, 1.35, v));
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.uv = input.uv;
                o.seed = frac(dot(top, float3(0.137, 0.271, 0.093)));
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float strength = _HP_Shafts.x * _Intensity;
                if (strength <= 0.0) return half4(0, 0, 0, 0);
                float u = i.uv.x, v = i.uv.y;
                float edge = sin(PI * u);
                edge *= edge;
                float along = smoothstep(0.0, 0.18, v) * (1.0 - smoothstep(0.55, 1.0, v));
                float t = _Time.y;
                float streak = 0.55 + 0.25 * sin(u * 13.0 + i.seed * 40.0 + t * 0.21) + 0.2 * sin(u * 5.3 - i.seed * 17.0 - t * 0.13);
                float dist = distance(i.positionWS, _WorldSpaceCameraPos);
                float near = saturate((dist - _FadeNear) / 6.0);
                float far = 1.0 - saturate(dist / max(1.0, _FadeFar));
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float soft = saturate((sceneEye - LinearEyeDepth(i.positionCS.z, _ZBufferParams)) / max(0.01, _Softness));
                float3 view = normalize(i.positionWS - _WorldSpaceCameraPos);
                float facing = 0.35 + 0.65 * pow(saturate(dot(view, _MainLightPosition.xyz) * 0.5 + 0.5), 3.0);
                half3 color = _MainLightColor.rgb * _BaseColor.rgb * (edge * along * streak * near * far * soft * facing * strength);
                return half4(color, 0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
