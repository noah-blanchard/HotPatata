using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A forest of an authored level (ARCHITECTURE §27): inside its outline the editor plants giants in groves with clearings between
    /// them, an understory and ground cover (ferns, grass, bushes, rocks, logs, moss), drawn instanced and never colliding (like
    /// PatataRanger's). Nothing grows on a walkway, a deck, a pass arc, rock too steep to hold it or too near a giant tree.
    /// Data only: the editor plants it.
    /// </summary>
    [SelectionBase]
    public sealed class ForestArea : LevelPiece
    {
        [SerializeField, Tooltip("The area in the piece's space (x, z; y is ignored): plants stand on the ground under it.")]
        List<Vector3> outline = new List<Vector3>
        {
            new Vector3(-30f, 0f, -30f), new Vector3(30f, 0f, -30f), new Vector3(30f, 0f, 30f), new Vector3(-30f, 0f, 30f),
        };
        [SerializeField, Range(0f, 1f), Tooltip("Giant trees (45 to 65 m) in groves.")] float giants = 0.5f;
        [SerializeField, Range(0f, 1f), Tooltip("Younger trees and bushes.")] float understory = 0.6f;
        [SerializeField, Range(0f, 1f), Tooltip("Ferns, grass, rocks, logs and moss.")] float groundCover = 0.7f;
        [SerializeField, Range(0f, 1f), Tooltip("How much of the area is open clearings between the groves.")] float clearings = 0.3f;
        [SerializeField, Min(1f), Tooltip("Nothing taller than ground cover grows closer than this to a walkway, a bridge or a deck (m).")] float pathClearance = 3.5f;

        public List<Vector3> Outline => outline;
        public float Giants { get => giants; set => giants = Mathf.Clamp01(value); }
        public float Understory { get => understory; set => understory = Mathf.Clamp01(value); }
        public float GroundCover { get => groundCover; set => groundCover = Mathf.Clamp01(value); }
        public float Clearings { get => clearings; set => clearings = Mathf.Clamp01(value); }
        public float PathClearance { get => pathClearance; set => pathClearance = Mathf.Max(1f, value); }

        public List<Vector2> WorldOutline()
        {
            var list = new List<Vector2>(outline.Count);
            foreach (var p in outline)
            {
                var w = transform.TransformPoint(new Vector3(p.x, 0f, p.z));
                list.Add(new Vector2(w.x, w.z));
            }
            return list;
        }
    }
}
