using System.Globalization;
using UnityEngine;

namespace HotPatata
{
    /// <summary>The stone a block is cut from: its material.</summary>
    public enum StoneKind { Granite, Sandstone, Limestone, Mossy }

    /// <summary>
    /// One stone an author can move by hand (ARCHITECTURE §27.7): a frozen stone wall's stones, or one placed on its own (Element →
    /// Stone). Its size is across (x), up (y) and along (z), its edges bevelled; its variant changes its roughness. Drawn from the shared
    /// mesh catalogue like timber; a wall's stones never collide (the wall's <see cref="MasonryWall"/> does), one on its own may.
    /// </summary>
    public sealed class StoneBlock : TimberElement
    {
        public const int Variants = 4;

        [SerializeField, Tooltip("Across (x), up (y), along (z), in metres.")] Vector3 size = new Vector3(0.7f, 0.3f, 0.6f);
        [SerializeField, Range(0f, 0.15f), Tooltip("How much its edges are rounded (m).")] float bevel = 0.05f;
        [SerializeField, Range(0, Variants - 1)] int variant;
        [SerializeField] StoneKind stone = StoneKind.Granite;

        public Vector3 Size { get => size; set => size = new Vector3(Mathf.Max(0.05f, value.x), Mathf.Max(0.05f, value.y), Mathf.Max(0.05f, value.z)); }
        public float Bevel { get => bevel; set => bevel = Mathf.Clamp(value, 0f, 0.15f); }
        public int Variant { get => variant; set => variant = ((value % Variants) + Variants) % Variants; }
        public StoneKind Stone { get => stone; set => stone = value; }

        /// <summary>
        /// The size its mesh is drawn at: across and up to 10 cm, along to 15 cm, so a wall's stones share a few dozen meshes (the stone keeps
        /// its exact size; a stone a few centimetres off shows no gap in a dry-stone wall).
        /// </summary>
        public Vector3 DrawnSize => new Vector3(Snap(size.x, 0.1f), Snap(size.y, 0.1f), Snap(size.z, 0.15f));

        /// <summary>The variant its mesh is drawn with: two roughnesses (with the sizes, enough to tell stones apart).</summary>
        public int DrawnVariant => variant % 2;

        static float Snap(float v, float step) => Mathf.Max(step, Mathf.Round(v / step) * step);

        public override string ShapeKey()
        {
            var d = DrawnSize;
            return string.Format(CultureInfo.InvariantCulture, "stone|{0}|{1}|{2}|{3}|{4}", Mathf.RoundToInt(d.x * 100f), Mathf.RoundToInt(d.y * 100f),
                                 Mathf.RoundToInt(d.z * 100f), Mathf.RoundToInt(bevel * 100f), DrawnVariant);
        }
    }
}
