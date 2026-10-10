using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Presentation only (ARCHITECTURE §25.3): the height mist of a nature course. It holds the mist floor (a height texture over
    /// the course, baked by the course builder from its ground) and the mist shown while no <see cref="TimeOfDayBlender"/> runs (the
    /// editor, written by <c>LookBuilder</c> from the first moment of the day); in play the blender pushes the blended moment
    /// every frame. Sets the shader globals of HotPatataFog.hlsl and clears them when disabled, so a scene without one (the
    /// menu, the plant) never shows mist. Never set by hand: change the builders and rebuild.
    /// </summary>
    [ExecuteAlways]
    public class MistField : MonoBehaviour
    {
        static readonly int FloorTex = Shader.PropertyToID("_HP_MistFloor"), FloorRect = Shader.PropertyToID("_HP_MistFloorRect"),
            Params = Shader.PropertyToID("_HP_MistParams"), ColorId = Shader.PropertyToID("_HP_MistColor"), Shafts = Shader.PropertyToID("_HP_Shafts");

        [SerializeField, Tooltip("The mist floor's height (R, metres, world) over the course.")] Texture2D floor;
        [SerializeField, Tooltip("World XZ of the texture's corner (x, y) and its size (z, w).")] Vector4 rect;
        [SerializeField, Range(0f, 1f), Tooltip("The most the mist ever hides.")] float maxOpacity = 0.9f;
        [Header("Mist without a running day (the editor)")]
        [SerializeField] Color color = new Color(0.7f, 0.78f, 0.76f);
        [SerializeField, Min(0f)] float density;
        [SerializeField, Min(0.5f)] float falloff = 10f;
        [SerializeField, Min(0f)] float glow;
        [SerializeField, Min(0f)] float shafts;

        /// <summary>The enabled field, if any (one per nature scene).</summary>
        public static MistField Active { get; private set; }

        public Texture2D Floor => floor;
        public Vector4 Rect => rect;
        public float Density => density;
        public float Falloff => falloff;

        void OnEnable()
        {
            Active = this;
            Push(color, density, falloff, glow, shafts);
        }

        // In the editor (no day running) the still mist is pushed every frame: a rebuilt floor texture or a reload never leaves the
        // shaders pointing at a texture that is gone. In play the blender pushes the blended moment instead.
        void Update()
        {
            if (!Application.isPlaying) Push(color, density, falloff, glow, shafts);
        }

        void OnDisable()
        {
            if (Active == this) Active = null;
            Shader.SetGlobalVector(Params, Vector4.zero);
            Shader.SetGlobalVector(Shafts, Vector4.zero);
        }

        /// <summary>Shows a mist (TimeOfDayBlender: the blended moment of the day).</summary>
        public void Push(Color mistColor, float mistDensity, float mistFalloff, float mistGlow, float shaftStrength)
        {
            if (floor != null) Shader.SetGlobalTexture(FloorTex, floor);
            Shader.SetGlobalVector(FloorRect, new Vector4(rect.x, rect.y, 1f / Mathf.Max(1f, rect.z), 1f / Mathf.Max(1f, rect.w)));
            Shader.SetGlobalVector(Params, new Vector4(floor != null ? mistDensity : 0f, mistFalloff, maxOpacity, mistGlow));
            Shader.SetGlobalVector(ColorId, (Vector4)mistColor.linear);
            Shader.SetGlobalVector(Shafts, new Vector4(shaftStrength, 0f, 0f, 0f));
        }

        /// <summary>Editor builders: the floor and the mist the editor shows.</summary>
        public void Configure(Texture2D floorHeights, Vector4 worldRect, float opacity)
        {
            floor = floorHeights;
            rect = worldRect;
            maxOpacity = opacity;
            if (isActiveAndEnabled) OnEnable();
        }

        /// <summary>LookBuilder: the mist shown while no day runs (the first moment).</summary>
        public void ConfigureStill(Color mistColor, float mistDensity, float mistFalloff, float mistGlow, float shaftStrength)
        {
            color = mistColor;
            density = mistDensity;
            falloff = mistFalloff;
            glow = mistGlow;
            shafts = shaftStrength;
            if (isActiveAndEnabled) OnEnable();
        }

        /// <summary>The floor height under a world position (tests and builders; the shader samples the same texture).</summary>
        public float FloorAt(Vector3 world)
        {
            if (floor == null) return 0f;
            float u = Mathf.Clamp01((world.x - rect.x) / Mathf.Max(1f, rect.z)), v = Mathf.Clamp01((world.z - rect.y) / Mathf.Max(1f, rect.w));
            return floor.GetPixelBilinear(u, v).r;
        }
    }
}
