#ifndef HOTPATATA_NATURE_INPUT_INCLUDED
// Material inputs and shared vertex code of HotPatata/Nature (ARCHITECTURE §25.3, PatataWilds).
#define HOTPATATA_NATURE_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

// Every material property lives in UnityPerMaterial (SRP Batcher compatible).
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _Cutoff;
    half _BumpScale;
    half _BaseSaturation;
    half _BaseBrightness;
    float _TileSize;
    half _TriplanarSharpness;
    half _SmoothnessMin;
    half _SmoothnessMax;
    half _OcclusionStrength;
    half _EnvReflection;
    float _MacroScale;
    half _MacroStrength;
    float _VariationScale;
    half _VariationStrength;
    half _AntiTileSharpness;
    half _AntiTileRotation;
    half4 _TopColor;
    float _TopTileSize;
    half _TopCoverage;
    half _TopSharpness;
    half _TopNoise;
    half _Translucency;
    half4 _TranslucencyColor;
    half _WindStrength;
    half _WindFlutter;
    half _WindVertexColor;
    float _WindHeight;
    half4 _ShadeColor;
    half _Wrap;
    half _AmbientStrength;
    half4 _RimColor;
    half _RimPower;
    half _RimStrength;
    half4 _EmissionColor;
    float4 _PatternScroll;
    half4 _StripeColor;
    half _StripeStrength;
    float _StripeScale;
    half _Cull;
    float _FadeStart;
    float _FadeEnd;
CBUFFER_END

// _BaseMap and _BumpMap come from URP's SurfaceInput.hlsl.
TEXTURE2D(_RoughnessMap);   SAMPLER(sampler_RoughnessMap);
TEXTURE2D(_OcclusionMap);   SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_AlphaMap);       SAMPLER(sampler_AlphaMap);
TEXTURE2D(_TopMap);         SAMPLER(sampler_TopMap);
TEXTURE2D(_TopBumpMap);     SAMPLER(sampler_TopBumpMap);

// Set once per frame by TimeOfDayBlender (presentation): xy = wind direction (world XZ), z = strength (0 = still), w = gust speed.
float4 _HP_Wind;

// Wind bend, in world space, for a vertex at positionWS whose sway weights are (trunk, branch, leaf). The phase comes from the
// object's position, so neighbouring plants never sway in step; nothing moves when _HP_Wind.z is 0 (the default).
float3 NatureWind(float3 positionWS, float3 normalWS, float3 weights)
{
    float strength = _HP_Wind.z * _WindStrength;
    if (strength <= 0.0) return float3(0, 0, 0);
    float3 objectWS = UNITY_MATRIX_M._m03_m13_m23;
    float t = _Time.y * max(0.1, _HP_Wind.w);
    float phase = dot(objectWS, float3(0.37, 0.0, 0.53));
    float gust = sin(t * 0.9 + phase) * 0.6 + sin(t * 2.3 + phase * 1.7) * 0.25 + 0.55;
    float3 dir = float3(_HP_Wind.x, 0, _HP_Wind.y);
    float3 bend = dir * gust * (weights.x * 0.35 + weights.y * 0.2);
    float flutter = sin(t * 9.0 + dot(positionWS, float3(3.1, 2.3, 2.7))) * weights.z * 0.04 * _WindFlutter;
    return (bend + normalWS * flutter) * strength;
}

// The sway weights of a vertex: its vertex colour (generated trees and grass: r trunk, g branch, b leaf) or, for plain meshes,
// its height above the object's origin.
float3 NatureWindWeights(float3 positionOS, half4 color)
{
    float h = saturate(positionOS.y / max(0.05, _WindHeight));
    float3 byHeight = float3(h * h, h, h);
    return lerp(byHeight, color.rgb, _WindVertexColor);
}

// The distance fade of instanced foliage: a dither that thins a plant out between _FadeStart and _FadeEnd (0 = off), so a
// level-of-detail switch or the end of the grass never pops.
void NatureDistanceClip(float3 positionWS, float4 positionCS)
{
    if (_FadeEnd <= 0.0) return;
    float d = distance(positionWS, _WorldSpaceCameraPos);
    float keep = 1.0 - saturate((d - _FadeStart) / max(0.01, _FadeEnd - _FadeStart));
    float2 p = floor(positionCS.xy);
    float dither = frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
    clip(keep - dither - 0.001);
}

#endif
