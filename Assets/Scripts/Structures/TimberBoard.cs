using System.Globalization;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A straight piece of sawn timber (ARCHITECTURE §27.1): a board, a post, a beam, a brace, an iron strap. Its size is across (x),
    /// thick (y) and long (z), centred on the element; its two ends may be cut on a slant (degrees round its local up).
    /// </summary>
    public sealed class TimberBoard : TimberElement
    {
        [SerializeField, Tooltip("Across (x), thickness (y), length (z), in metres.")] Vector3 size = new Vector3(0.2f, 0.05f, 2f);
        [SerializeField, Range(-60f, 60f), Tooltip("The slant of the cut at the back end (degrees).")] float cutStart;
        [SerializeField, Range(-60f, 60f), Tooltip("The slant of the cut at the front end (degrees).")] float cutEnd;

        public const float MinSize = 0.01f;

        public Vector3 Size
        {
            get => size;
            set => size = new Vector3(Mathf.Max(MinSize, value.x), Mathf.Max(MinSize, value.y), Mathf.Max(MinSize, value.z));
        }

        public float CutStart { get => cutStart; set => cutStart = Mathf.Clamp(value, -60f, 60f); }
        public float CutEnd { get => cutEnd; set => cutEnd = Mathf.Clamp(value, -60f, 60f); }

        /// <summary>The middle of the board's top face, its back end and its front end (local).</summary>
        public Vector3 TopCenter => Vector3.up * size.y * 0.5f;
        public Vector3 BackEnd => Vector3.back * size.z * 0.5f;
        public Vector3 FrontEnd => Vector3.forward * size.z * 0.5f;

        void OnValidate() => Size = size;

        /// <summary>Millimetres for the size, tenths of a degree for the cuts: what makes two boards share a mesh.</summary>
        public override string ShapeKey() =>
            string.Format(CultureInfo.InvariantCulture, "board|{0}|{1}|{2}|{3}|{4}|{5}", Mm(size.x), Mm(size.y), Mm(size.z),
                          Mathf.RoundToInt(cutStart * 10f), Mathf.RoundToInt(cutEnd * 10f), Grain);

        static int Mm(float v) => Mathf.RoundToInt(v * 1000f);
    }
}
