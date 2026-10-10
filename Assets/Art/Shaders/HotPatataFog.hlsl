#ifndef HOTPATATA_FOG_INCLUDED
// The height mist of the nature courses (ARCHITECTURE §25.3): an exponential mist that pools on a floor and thins with height,
// on top of URP's linear haze. The floor follows the ground (a height texture over the course, MistField), so the mist lies in
// the valleys and under the canopy wherever the course climbs. The globals are set by MistField and TimeOfDayBlender
// (presentation) and are all zero by default: a scene without a MistField is drawn exactly as before.
#define HOTPATATA_FOG_INCLUDED

TEXTURE2D(_HP_MistFloor); SAMPLER(sampler_HP_MistFloor);
float4 _HP_MistFloorRect;   // xy = world XZ of the floor texture's corner, zw = 1 / its world size
float4 _HP_MistParams;      // x = density at the floor (1/m, 0 = no mist), y = falloff height (m), z = most opacity, w = glow towards the sun
half4 _HP_MistColor;

// The mist floor's height under a world position.
float HP_MistFloor(float3 positionWS)
{
    float2 uv = saturate((positionWS.xz - _HP_MistFloorRect.xy) * _HP_MistFloorRect.zw);
    return SAMPLE_TEXTURE2D_LOD(_HP_MistFloor, sampler_HP_MistFloor, uv, 0).r;
}

// How much of a surface at positionWS the mist hides from the camera (0..most opacity): the density exp(-height / falloff)
// integrated in closed form along the view ray, both ends measured from the floor under the surface.
half HP_MistAmount(float3 positionWS)
{
    float density = _HP_MistParams.x;
    if (density <= 0.0) return 0.0;
    float falloff = max(0.5, _HP_MistParams.y);
    float floorY = HP_MistFloor(positionWS);
    float hc = max(-4.0, (_WorldSpaceCameraPos.y - floorY) / falloff);
    float hp = max(-4.0, (positionWS.y - floorY) / falloff);
    float ec = exp(-hc), ep = exp(-hp);
    float dh = hp - hc;
    float mean = abs(dh) > 1e-3 ? (ec - ep) / dh : ep;   // the mean of exp(-h) along the ray
    float depth = density * distance(positionWS, _WorldSpaceCameraPos) * mean;
    return (1.0 - exp(-depth)) * _HP_MistParams.z;
}

// The mist's colour seen towards positionWS: its own colour, brighter looking into the sun.
half3 HP_MistTint(float3 positionWS)
{
    float3 view = normalize(positionWS - _WorldSpaceCameraPos);
    half glow = pow(saturate(dot(view, _MainLightPosition.xyz)), 6.0) * _HP_MistParams.w;
    return _HP_MistColor.rgb + _MainLightColor.rgb * glow;
}

// URP's linear haze (MixFog) with the height mist under it.
half3 HP_MixFog(half3 color, half fogFactor, float3 positionWS)
{
    if (_HP_MistParams.x > 0.0) color = lerp(color, HP_MistTint(positionWS), HP_MistAmount(positionWS));
    return MixFog(color, fogFactor);
}

#endif
