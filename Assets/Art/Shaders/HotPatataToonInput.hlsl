#ifndef HOTPATATA_TOON_INPUT_INCLUDED
#define HOTPATATA_TOON_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

// Every material property lives in UnityPerMaterial (SRP Batcher compatible). The URP ShadowCaster /
// DepthOnly / DepthNormals passes are reused as-is: they only need _BaseMap, _BaseColor and _Cutoff.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half4 _ShadeColor;
    half4 _TopColor;
    half _TopBlend;
    half _RampThreshold;
    half _RampSmoothness;
    half _AmbientStrength;
    half4 _RimColor;
    half _RimPower;
    half _RimStrength;
    half4 _SpecColor;
    half _SpecSize;
    half4 _EmissionColor;
    half _EdgeWidth;
    half _EdgeStrength;
    half _Pattern;
    half4 _PatternColor;
    half _PatternScale;
    half _PatternStrength;
    half _Cutoff;
CBUFFER_END

#endif
