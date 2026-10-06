using HotPatata;
using UnityEngine;
using static HotPatata.Editor.CourseKit;

namespace HotPatata.Editor
{
    /// <summary>
    /// Visual-only details for the industrial look (ARCHITECTURE §25.2): edge strips, column bases and caps, braces, seams.
    /// Every piece is a collider-free <see cref="CourseDecoration"/>, so it can never change a collision, and builders keep
    /// them out of the pass corridors (checked by <c>IndustrialLabTests</c>).
    /// </summary>
    public static class IndustrialKit
    {
        /// <summary>Strips sit this far above a surface they decorate: enough not to z-fight, far below the controller's skin.</summary>
        public const float SurfaceLift = 0.005f;

        /// <summary>
        /// A hash of <paramref name="text"/> that is the same on every machine and every run (FNV-1a), unlike
        /// <see cref="string.GetHashCode"/>, which a runtime may randomise: seeds and picks stay deterministic across rebuilds.
        /// </summary>
        public static int StableHash(string text)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char ch in text) { hash ^= ch; hash *= 16777619; }
                return (int)(hash & 0x7fffffff);
            }
        }

        /// <summary>True when nothing ever moves <paramref name="c"/>: a separate detail or decal laid on it would otherwise float.</summary>
        public static bool IsStatic(Collider c) => c.attachedRigidbody == null && c.GetComponentInParent<MovingPlatform>() == null
            && c.GetComponentInParent<FallingPlatform>() == null && c.GetComponentInParent<SignalActuator>() == null
            && c.GetComponentInParent<RotatingObstacle>() == null && c.GetComponentInParent<Conveyor>() == null
            && c.GetComponentInParent<BombTransit>() == null && c.GetComponentInParent<PressurePlate>() == null
            && c.GetComponentInParent<LaunchPad>() == null;

        /// <summary>A collider-free box drawn in <paramref name="role"/>'s look, marked as decoration.</summary>
        public static GameObject Detail(Transform parent, string name, Vector3 center, Vector3 size, KitRole role, Quaternion? rotation = null)
        {
            var go = Cube(name, parent, center, size, role, "Default");
            if (rotation.HasValue) go.transform.localRotation = rotation.Value;
            go.AddComponent<CourseDecoration>();
            return go;
        }

        /// <summary>A light strip along the top edge of a slab (a landing cue): flush with the top, never rising above the collider.</summary>
        public static void EdgeStrip(Transform parent, string name, float x, float topY, float z, float length, float width = 0.3f)
        {
            Detail(parent, name, new Vector3(x, topY + SurfaceLift - 0.02f, z), new Vector3(width, 0.04f, length), KitRole.Accent);
        }

        /// <summary>A strip across a slab (a joint): a thin raw-metal line every few metres.</summary>
        public static void Seam(Transform parent, string name, float centerX, float topY, float z, float width)
        {
            Detail(parent, name, new Vector3(centerX, topY + SurfaceLift - 0.015f, z), new Vector3(width, 0.03f, 0.06f), KitRole.Grating);
        }

        /// <summary>A base plate and a cap around a column that stands at <paramref name="foot"/> and is <paramref name="height"/> tall.</summary>
        public static void ColumnTrim(Transform parent, string name, Vector3 foot, float height, float thickness)
        {
            float plate = thickness + 0.4f;
            Detail(parent, name + " base", foot + Vector3.up * 0.06f, new Vector3(plate, 0.12f, plate), KitRole.Truss);
            Detail(parent, name + " cap", foot + Vector3.up * (height - 0.06f), new Vector3(plate, 0.12f, plate), KitRole.Truss);
            // anchor bolts on the base plate
            foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                Detail(parent, name + " bolt", foot + new Vector3(sx * plate * 0.36f, 0.15f, sz * plate * 0.36f), new Vector3(0.07f, 0.06f, 0.07f), KitRole.Grating);
        }

        /// <summary>A flat diagonal brace between two points (a column to a column, a column to a beam).</summary>
        public static void Brace(Transform parent, string name, Vector3 a, Vector3 b, float thickness = 0.12f)
        {
            var rotation = Quaternion.LookRotation(b - a, Vector3.up);
            Detail(parent, name, (a + b) / 2f, new Vector3(thickness, thickness * 2f, Vector3.Distance(a, b)), KitRole.Truss, rotation);
        }

        /// <summary>A horizontal I-beam (under a walkway, along a ceiling): a flange pair and a web, all decoration.</summary>
        public static void Beam(Transform parent, string name, Vector3 center, float length, float depth = 0.4f, float flange = 0.3f)
        {
            Detail(parent, name + " web", center, new Vector3(0.08f, depth, length), KitRole.Truss);
            Detail(parent, name + " top", center + Vector3.up * (depth / 2f), new Vector3(flange, 0.06f, length), KitRole.Truss);
            Detail(parent, name + " bottom", center - Vector3.up * (depth / 2f), new Vector3(flange, 0.06f, length), KitRole.Truss);
        }
    }
}
