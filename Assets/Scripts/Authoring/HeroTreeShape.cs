using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A giant tree of an authored level (ARCHITECTURE §27): a hero tree (the kind PatataRanger's post is built round) shaped by its
    /// data: kind, height, girth, lean, crown, and the limbs placed by hand that timber can hang from. Its trunk and limbs always
    /// collide (a body stops at them, a thrown bomb explodes). Decks that hug it saw its crown off round them. It is the level's
    /// <see cref="ITrunkSource"/>: decks lean on its trunk, hangers tie to its limbs. Data only: the editor grows it.
    /// </summary>
    [SelectionBase]
    public sealed class HeroTreeShape : LevelPiece, ITrunkSource
    {
        public enum Kind { Broadleaf, Conifer, Dead }

        /// <summary>A limb placed by hand: where it leaves the trunk (m above the foot), its heading (degrees, in the tree's own turn),
        /// length, rise (degrees above level), radius at the trunk, and whether it carries leaves.</summary>
        [Serializable]
        public struct Limb
        {
            public float height, yaw, length, rise, radius;
            public bool leaves;
        }

        /// <summary>Timber is held by the trunk up to this share of the tree's height (above, it is too thin).</summary>
        public const float HoldingShare = 0.85f;

        [SerializeField] Kind kind = Kind.Broadleaf;
        [SerializeField, Min(12f)] float height = 50f;
        [SerializeField, Min(0.4f), Tooltip("The trunk's radius at its foot, above the roots' flare.")] float baseRadius = 2.2f;
        [SerializeField, Min(0.15f), Tooltip("Where the trunk ends in the crown.")] float topRadius = 0.9f;
        [SerializeField, Tooltip("Where the top stands from the foot (x, z, metres, in the tree's own turn): the trunk curves into it.")] Vector2 lean = new Vector2(1.5f, 0.5f);
        [SerializeField, Min(4f), Tooltip("Where the crown's own limbs start (m above the foot).")] float crownStart = 24f;
        [SerializeField, Range(0, 40), Tooltip("How many limbs the crown has (broadleaf); conifers grow whorls all the way up.")] int crownLimbs = 14;
        [SerializeField] List<Limb> limbs = new List<Limb>();

        // what the last growth measured (local to the foot, unturned): read by the decks and hangers without growing the tree again
        [SerializeField, HideInInspector] Vector3[] axis = Array.Empty<Vector3>();
        [SerializeField, HideInInspector] float[] radii = Array.Empty<float>();
        [SerializeField, HideInInspector] Vector3[] anchors = Array.Empty<Vector3>();

        public Kind TreeKind { get => kind; set => kind = value; }
        public float Height { get => height; set => height = Mathf.Max(12f, value); }
        public float BaseRadius { get => baseRadius; set => baseRadius = Mathf.Max(0.4f, value); }
        public float TopRadius { get => topRadius; set => topRadius = Mathf.Max(0.15f, value); }
        public Vector2 Lean { get => lean; set => lean = value; }
        public float CrownStart { get => crownStart; set => crownStart = Mathf.Clamp(value, 4f, height - 2f); }
        public int CrownLimbs { get => crownLimbs; set => crownLimbs = Mathf.Clamp(value, 0, 40); }
        public List<Limb> Limbs => limbs;

        Quaternion Turn => Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        /// <summary>Editor: what the growth measured (axis and radii up the trunk, anchor points on the limbs), local to the foot.</summary>
        public void Measure(Vector3[] trunkAxis, float[] trunkRadii, Vector3[] limbAnchors)
        {
            axis = trunkAxis ?? Array.Empty<Vector3>();
            radii = trunkRadii ?? Array.Empty<float>();
            anchors = limbAnchors ?? Array.Empty<Vector3>();
        }

        /// <summary>The trunk's axis at <paramref name="h"/> metres above the foot (local, unturned): measured, else the shape it is grown to.</summary>
        public Vector3 AxisAt(float h)
        {
            float t = Mathf.Clamp01(h / height);
            if (axis.Length > 1)
            {
                float f = t * (axis.Length - 1);
                int i = Mathf.Min(Mathf.FloorToInt(f), axis.Length - 2);
                return Vector3.Lerp(axis[i], axis[i + 1], f - i);
            }
            float bend = Mathf.Sin(t * Mathf.PI * 0.5f);
            return new Vector3(lean.x * bend, h, lean.y * bend);
        }

        public float RadiusAt(float h)
        {
            float t = Mathf.Clamp01(h / height);
            if (radii.Length > 1)
            {
                float f = t * (radii.Length - 1);
                int i = Mathf.Min(Mathf.FloorToInt(f), radii.Length - 2);
                return Mathf.Lerp(radii[i], radii[i + 1], f - i);
            }
            return Mathf.Lerp(baseRadius, topRadius, Mathf.Pow(t, 0.85f));
        }

        public bool TryTrunkAt(float y, out Vector3 center, out float radius)
        {
            float h = y - transform.position.y;
            center = default;
            radius = 0f;
            if (h <= 0f || h > height * HoldingShare) return false;
            center = transform.position + Turn * AxisAt(h);
            radius = RadiusAt(h);
            return true;
        }

        public IEnumerable<Vector3> Anchors
        {
            get
            {
                var foot = transform.position;
                var turn = Turn;
                foreach (var a in anchors) yield return foot + turn * a;
                // the trunk itself high up: a rope round it holds as well as a limb
                yield return foot + turn * AxisAt(Mathf.Max(crownStart, height * 0.6f));
                yield return foot + turn * AxisAt(height * 0.8f);
            }
        }
    }
}
