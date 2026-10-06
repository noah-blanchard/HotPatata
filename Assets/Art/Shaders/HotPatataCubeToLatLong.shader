// Editor helper (LookBuilder, ARCHITECTURE §25.3): unwraps a cubemap into a latitude-longitude image so the brightest
// direction (the photographed sun of a Poly Haven sky) can be found. Same direction convention as HotPatata/SkyBlend.
Shader "Hidden/HotPatata/CubeToLatLong"
{
    Properties { [NoScaleOffset] _Cube ("Cubemap", Cube) = "grey" {} }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            TEXTURECUBE(_Cube); SAMPLER(sampler_Cube);
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes i)
            {
                // Graphics.Blit draws a full-screen quad: the clip position is the UV, so pixel columns and rows are longitude and latitude
                Varyings o;
                o.positionCS = float4(i.uv * 2.0 - 1.0, 0.5, 1.0);
                o.uv = i.uv;
                return o;
            }
            float4 frag(Varyings i) : SV_Target
            {
                float phi = i.uv.x * 2.0 * PI - PI;
                float theta = (i.uv.y - 0.5) * PI;
                float3 d = float3(cos(theta) * cos(phi), sin(theta), cos(theta) * sin(phi));
                return float4(SAMPLE_TEXTURECUBE_LOD(_Cube, sampler_Cube, d, 0).rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
