using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HotPatata
{
    /// <summary>
    /// Presentation only (ARCHITECTURE §25.3, PatataCanopy): the sun's leaf cookie (soft dappled light on the decks) sways a
    /// little with the plants' wind (the global _HP_Wind set by <see cref="TimeOfDayBlender"/>). It moves the cookie's offset
    /// only, a few centimetres a second: it never flashes and never touches a rule. Set up by PatataCanopyBuilder.ApplyDapple.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class LeafDapple : MonoBehaviour
    {
        static readonly int Wind = Shader.PropertyToID("_HP_Wind");

        [SerializeField, Tooltip("How far the pattern sways at full wind (m).")] float sway = 0.35f;
        [SerializeField, Tooltip("Slow drift along the wind (m/s at full wind).")] float drift = 0.04f;

        UniversalAdditionalLightData data;
        Vector2 origin;

        void OnEnable()
        {
            data = GetComponent<UniversalAdditionalLightData>();
            if (data != null) origin = data.lightCookieOffset;
        }

        void OnDisable()
        {
            if (data != null) data.lightCookieOffset = origin;
        }

        void Update()
        {
            if (data == null) return;
            var wind = Shader.GetGlobalVector(Wind);
            var dir = new Vector2(wind.x, wind.y);
            float strength = wind.z, t = Time.time;
            float gust = Mathf.Sin(t * 0.7f) * 0.6f + Mathf.Sin(t * 1.9f + 1.3f) * 0.4f;
            float along = Mathf.Repeat(t * drift * strength, Mathf.Max(1f, data.lightCookieSize.x));   // the pattern tiles
            data.lightCookieOffset = origin + dir * (gust * sway * strength + along);
        }
    }
}
