using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Old mine timbering of an authored level (ARCHITECTURE §27.2): a set of frames (two posts and a cap beam, lagging boards over the
    /// cap if asked) every few metres along a line through its points, standing on the ground under each frame. Its posts stop players and
    /// the bomb; the rest is drawn only. A lantern can hang from every few caps. Freezes into elements like the rest of the timber.
    /// Data only: the editor draws it.
    /// </summary>
    [SelectionBase]
    public sealed class MineFrameShape : LevelPiece
    {
        [SerializeField, Tooltip("The line the frames stand along (piece space), at the floor.")]
        List<Vector3> points = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 12f) };
        [SerializeField, Min(1.6f), Tooltip("The clear width between the posts (m).")] float width = 4.5f;
        [SerializeField, Min(2.2f), Tooltip("The cap beam's underside above the floor (m).")] float height = 3.6f;
        [SerializeField, Min(1.2f), Tooltip("From one frame to the next (m).")] float spacing = 3f;
        [SerializeField, Tooltip("Boards laid over the caps (a lagged roof).")] bool lagging = true;
        [SerializeField, Min(0), Tooltip("A lantern under every this many caps (0: none).")] int lanternEvery = 3;
        [SerializeField] TimberWear wear = TimberWear.Aged;

        public List<Vector3> Points => points;
        public float Width { get => width; set => width = Mathf.Max(1.6f, value); }
        public float Height { get => height; set => height = Mathf.Max(2.2f, value); }
        public float Spacing { get => spacing; set => spacing = Mathf.Max(1.2f, value); }
        public bool Lagging { get => lagging; set => lagging = value; }
        public int LanternEvery { get => lanternEvery; set => lanternEvery = Mathf.Max(0, value); }
        public TimberWear Wear { get => wear; set => wear = value; }

        public List<Vector3> WorldPoints()
        {
            var list = new List<Vector3>(points.Count);
            foreach (var p in points) list.Add(transform.TransformPoint(p));
            return list;
        }
    }
}
