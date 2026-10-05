#ifndef HOTPATATA_INDUSTRIAL_INPUT_INCLUDED
// Material inputs of HotPatata/Industrial (ARCHITECTURE §25.2, issue #87).
#define HOTPATATA_INDUSTRIAL_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

// Every material property lives in UnityPerMaterial (SRP Batcher compatible). The URP ShadowCaster / DepthOnly /
// DepthNormals passes are reused as-is: they only need _BaseMap(_ST), _BaseColor, _Cutoff and _BumpScale.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _Cutoff;
    half _BumpScale;
    half _BaseSaturation;
    half _BaseBrightness;
    float _TileSize;
    float _MacroScale;
    half _MacroStrength;
    float _VariationScale;
    half _VariationStrength;
    half _FlipNormalY;
    half _GlossIsRoughness;
    half _SmoothnessMin;
    half _SmoothnessMax;
    half _Metallic;
    half _OcclusionStrength;
    half _EnvReflection;
    half4 _ShadeColor;
    half _Wrap;
    half _Stylize;
    half _RampThreshold;
    half _RampSmoothness;
    half _AmbientStrength;
    half4 _RimColor;
    half _RimPower;
    half _RimStrength;
    half4 _EmissionColor;
    float4 _PatternScroll;
    half4 _StripeColor;
    half _StripeStrength;
    float _StripeScale;
    half _AntiTile;
    half _AntiTileSharpness;
    half _AntiTileRotation;
    float _GrungeSize;
    half _GrungeStrength;
    half _GrungeRoughness;
    half _Decal;
    half _SrcBlend;
    half _DstBlend;
    half _ZWrite;
    half _OffsetFactor;
    half _OffsetUnits;
CBUFFER_END

// _BaseMap and _BumpMap come from URP's SurfaceInput.hlsl.
TEXTURE2D(_GlossMap);       SAMPLER(sampler_GlossMap);
TEXTURE2D(_MetallicMap);    SAMPLER(sampler_MetallicMap);
TEXTURE2D(_OcclusionMap);   SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_GrungeMap);      SAMPLER(sampler_GrungeMap);

#endif
