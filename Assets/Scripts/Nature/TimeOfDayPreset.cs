using UnityEngine;
using UnityEngine.Rendering;

namespace HotPatata
{
    /// <summary>
    /// One moment of PatataWilds' day (ARCHITECTURE §25.3): the sun, the sky fill, the haze, the HDRI sky and a grade on top of
    /// the shared look. Written by <c>LookBuilder</c> (never by hand); <see cref="TimeOfDayBlender"/> blends between consecutive
    /// presets as the team reaches checkpoints. Presentation data only.
    /// </summary>
    [CreateAssetMenu(menuName = "HotPatata/Time Of Day Preset")]
    public class TimeOfDayPreset : ScriptableObject
    {
        [Header("Sun")]
        [Range(-10f, 90f)] public float sunElevation = 25f;
        public float sunYaw = 40f;
        public Color sunColor = Color.white;
        [Min(0f)] public float sunIntensity = 1.5f;
        [Range(0f, 1f)] public float shadowStrength = 0.9f;

        [Header("Sky fill (trilight)")]
        public Color ambientSky = new Color(0.42f, 0.5f, 0.72f);
        public Color ambientEquator = new Color(0.7f, 0.56f, 0.48f);
        public Color ambientGround = new Color(0.26f, 0.22f, 0.24f);

        [Header("Haze")]
        public Color fogColor = new Color(0.98f, 0.72f, 0.5f);
        [Min(0f)] public float fogStart = 55f;
        [Min(0f)] public float fogEnd = 300f;

        [Header("Mist (MistField, HotPatataFog.hlsl)")]
        public Color mistColor = new Color(0.7f, 0.78f, 0.76f);
        [Tooltip("Density at the mist floor (1/m, 0 = no mist).")]
        [Min(0f)] public float mistDensity;
        [Tooltip("Height over which the mist thins by e (m).")]
        [Min(0.5f)] public float mistFalloff = 10f;
        [Tooltip("How much brighter the mist looks towards the sun.")]
        [Min(0f)] public float mistGlow = 0.3f;
        [Tooltip("Strength of the light shafts (god rays) through the crowns.")]
        [Min(0f)] public float shaftStrength;

        [Header("Sky")]
        public Cubemap sky;
        [Min(0f)] public float skyExposure = 1f;
        [Tooltip("Degrees: turns the photographed sky so its sun is where this preset's sun is (measured by LookBuilder).")]
        public float skyRotation;

        [Header("Grade and wind")]
        [Tooltip("A profile overriding the shared look's exposure and white balance for this moment (blended by weight).")]
        public VolumeProfile grade;
        [Tooltip("Wind strength for the swaying plants (0 = still).")]
        [Range(0f, 2f)] public float wind = 0.4f;

        /// <summary>The direction the sunlight travels (the directional light's forward).</summary>
        public Quaternion SunRotation => Quaternion.Euler(sunElevation, sunYaw, 0f);
    }
}
