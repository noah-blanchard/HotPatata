using UnityEngine;

namespace HotPatata
{
    /// <summary>A shelter's roof: two slopes from a ridge, one slope (a lean-to), or none.</summary>
    public enum ShelterRoof { Gable, Lean, None }

    /// <summary>
    /// A shelter of an authored level (ARCHITECTURE §27.1): four posts, a frame, a board roof, low plank walls if asked, a winch drum
    /// against its back if asked. A winch booth, a woodshed, a lean-to over a bench. Its posts and walls collide. Data only: the editor
    /// draws it (HotPatata.Editor.TimberStructures); freeze it to move its boards by hand.
    /// </summary>
    [SelectionBase]
    public sealed class ShelterShape : LevelPiece
    {
        [SerializeField, Tooltip("Width (x) and depth (z) between the posts, metres.")] Vector2 size = new Vector2(3.8f, 3.2f);
        [SerializeField, Min(1.8f), Tooltip("The eaves' height above the piece.")] float height = 2.7f;
        [SerializeField] ShelterRoof roof = ShelterRoof.Gable;
        [SerializeField, Min(0f), Tooltip("How far the roof rises above the eaves (m).")] float roofRise = 0.9f;
        [SerializeField, Min(0f), Tooltip("Plank walls this high on the back and the sides (0: open).")] float lowWalls;
        [SerializeField, Tooltip("A winch drum and its crank against the back.")] bool drum;
        [SerializeField] TimberWear wear = TimberWear.Aged;

        public Vector2 Size { get => size; set => size = new Vector2(Mathf.Max(1f, value.x), Mathf.Max(1f, value.y)); }
        public float Height { get => height; set => height = Mathf.Max(1.8f, value); }
        public ShelterRoof Roof { get => roof; set => roof = value; }
        public float RoofRise { get => roofRise; set => roofRise = Mathf.Max(0f, value); }
        public float LowWalls { get => lowWalls; set => lowWalls = Mathf.Clamp(value, 0f, height); }
        public bool Drum { get => drum; set => drum = value; }
        public TimberWear Wear { get => wear; set => wear = value; }
    }
}
