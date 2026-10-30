using System;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The rules that paint the layered ground (ARCHITECTURE §27.4): per layer, a score from the slope, the height over the level's floor,
    /// the curvature (hollow or ridge), the distance to water, to a walk line and to rock, the north faces and a noise; each texel takes
    /// the layers by score (sharpened, normalised). Art settings, not gameplay tuning: <see cref="Path"/>, made with its defaults when
    /// missing. Baked into the splat by <see cref="GroundBaker"/>.
    /// </summary>
    public sealed class GroundRules : ScriptableObject
    {
        public const string Path = "Assets/Settings/Look/GroundRules.asset";

        /// <summary>A band of a measure (slope in degrees, height in metres): 1 inside [min, max], fading to 0 over <see cref="soft"/>.</summary>
        [Serializable]
        public struct Band
        {
            public float boost, min, max, soft;

            public Band(float boost, float min, float max, float soft)
            {
                this.boost = boost;
                this.min = min;
                this.max = max;
                this.soft = soft;
            }

            public float Of(float v)
            {
                if (boost == 0f) return 0f;
                float s = Mathf.Max(1e-3f, soft);
                float inside = Mathf.Min(Mathf.Clamp01((v - min) / s + 1f), Mathf.Clamp01((max - v) / s + 1f));
                return boost * inside;
            }
        }

        /// <summary>Near something (water, a walk line, rock): <see cref="boost"/> at it, fading to 0 at <see cref="reach"/> metres.</summary>
        [Serializable]
        public struct Near
        {
            public float boost, reach;

            public Near(float boost, float reach)
            {
                this.boost = boost;
                this.reach = reach;
            }

            public float Of(float distance) => boost == 0f || reach <= 0f ? 0f : boost * Mathf.Clamp01(1f - distance / reach);
        }

        [Serializable]
        public sealed class LayerRule
        {
            public string name;
            [Tooltip("The score everywhere.")] public float baseScore;
            [Tooltip("By the ground's slope (degrees).")] public Band slope;
            [Tooltip("By the height over the level's floor (m).")] public Band height;
            [Tooltip("Hollows (+) score this much per unit of concavity; ridges (-) per unit of convexity when negative.")] public float concave;
            [Tooltip("Ridges score this much per unit of convexity.")] public float convex;
            [Tooltip("Under water.")] public float underWater;
            public Near nearWater, nearWalk, nearRock;
            [Tooltip("Faces looking north (+Z).")] public float north;
            [Tooltip("A world noise breaks its edges (score ± this).")] public float noise;
        }

        [Tooltip("How sharply the best scores win (1 = soft blends, 6 = crisp patches).")] public float sharpness = 4f;
        [Tooltip("The size of the noise that breaks the edges (m).")] public float noiseScale = 9f;
        [Tooltip("The curvature is measured over this many metres (a hollow, a ridge).")] public float curvatureRadius = 4f;
        [Tooltip("How far above the ground a walk line still paints a trail (a walkway on posts does not).")] public float walkLineReach = 1.5f;
        public LayerRule[] layers = Defaults();

        /// <summary>The default rules: litter on the forest flat and in hollows, grass in patches on the open and the knolls, trail on the walk lines, gravel on ridges and by rock, mud on shores, riverbed under water, scree at the foot of rock and on 35-50° slopes, rock over 45°.</summary>
        public static LayerRule[] Defaults() => new[]
        {
            new LayerRule { name = "Leaf litter", baseScore = 0.55f, slope = new Band(0.1f, 0f, 25f, 10f), concave = 0.6f, noise = 0.25f },
            new LayerRule { name = "Grass", baseScore = 0.22f, slope = new Band(0.25f, 0f, 18f, 8f), height = new Band(0.2f, 4f, 400f, 4f), convex = 0.35f, noise = 0.5f },
            new LayerRule { name = "Trail", nearWalk = new Near(2.2f, 2.2f), noise = 0.15f },
            new LayerRule { name = "Gravel", convex = 0.9f, nearRock = new Near(0.6f, 7f), nearWalk = new Near(0.35f, 3.5f), noise = 0.3f },
            new LayerRule { name = "Mud", concave = 0.35f, nearWater = new Near(2.2f, 5f), noise = 0.3f },
            new LayerRule { name = "Riverbed", underWater = 3f },
            new LayerRule { name = "Scree", slope = new Band(1.1f, 35f, 50f, 5f), nearRock = new Near(1.4f, 4.5f), noise = 0.25f },
            new LayerRule { name = "Rock", slope = new Band(2.6f, 46f, 90f, 4f) },
        };

        /// <summary>The rules asset (made with the defaults when missing).</summary>
        public static GroundRules Load()
        {
            var rules = AssetDatabase.LoadAssetAtPath<GroundRules>(Path);
            if (rules != null) return rules;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            rules = CreateInstance<GroundRules>();
            AssetDatabase.CreateAsset(rules, Path);
            AssetDatabase.SaveAssets();
            return rules;
        }

        /// <summary>The inputs of one texel.</summary>
        public struct Texel
        {
            public float slope, height, curvature, water, walk, rock, north;
            public bool underWater;
        }

        /// <summary>The weights of the eight layers at a texel, summing to 1 (<paramref name="noise"/>: each layer's own noise there, 0..1).</summary>
        public void Weigh(in Texel t, float[] weights, float[] noise)
        {
            float sum = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                var r = i < layers.Length ? layers[i] : null;
                float s = 0f;
                if (r != null)
                {
                    s = r.baseScore + r.slope.Of(t.slope) + r.height.Of(t.height)
                        + r.concave * Mathf.Max(0f, t.curvature) + r.convex * Mathf.Max(0f, -t.curvature)
                        + (t.underWater ? r.underWater : 0f) + r.nearWater.Of(t.water) + r.nearWalk.Of(t.walk) + r.nearRock.Of(t.rock)
                        + r.north * Mathf.Max(0f, t.north) + r.noise * (noise[i] - 0.5f) * 2f;
                }
                float w = Mathf.Pow(Mathf.Max(0f, s), Mathf.Max(0.5f, sharpness));
                weights[i] = w;
                sum += w;
            }
            if (sum <= 1e-6f)
            {
                for (int i = 0; i < weights.Length; i++) weights[i] = i == 0 ? 1f : 0f;
                return;
            }
            for (int i = 0; i < weights.Length; i++) weights[i] /= sum;
        }
    }
}
