using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A plank house of an authored level (ARCHITECTURE §27): a floor held by its walls, four plank walls that stop bodies and the
    /// bomb (their doors and windows left open), a pitched roof and, under it, a ceiling a lob never gets over (<see cref="CourseCeiling"/>).
    /// A rectangle centred on the piece, turned with it. Data only: the editor draws it with the timber kit.
    /// </summary>
    [SelectionBase]
    public sealed class HouseShape : LevelPiece
    {
        public enum Wall { South, East, North, West }

        /// <summary>An opening in a wall: which wall, where along it (0..1 from its first corner), its width and height.</summary>
        [Serializable]
        public struct Door
        {
            public Wall wall;
            [Range(0f, 1f)] public float along;
            [Min(0.6f)] public float width;
            [Min(0.4f)] public float height;
            [Tooltip("Above the floor (m): 0 for a door, more for a window.")] public float sill;
        }

        [SerializeField, Tooltip("Width (x) and depth (z), metres.")] Vector2 size = new Vector2(6f, 5f);
        [SerializeField, Min(2.2f)] float wallHeight = 2.8f;
        [SerializeField, Tooltip("A plank floor at the piece's height (off when the house stands on a deck).")] bool floor = true;
        [SerializeField, Min(0f)] float roofRise = 1.6f;
        [SerializeField, Tooltip("A ceiling slab under the roof: nothing is lobbed into the house.")] bool ceiling = true;
        [SerializeField] List<Door> doors = new List<Door> { new Door { wall = Wall.South, along = 0.5f, width = 1.4f, height = 2.3f } };
        [SerializeField] TimberWear wear;

        public Vector2 Size { get => size; set => size = new Vector2(Mathf.Max(2f, value.x), Mathf.Max(2f, value.y)); }
        public float WallHeight { get => wallHeight; set => wallHeight = Mathf.Max(2.2f, value); }
        public bool Floor { get => floor; set => floor = value; }
        public float RoofRise { get => roofRise; set => roofRise = Mathf.Max(0f, value); }
        public bool Ceiling { get => ceiling; set => ceiling = value; }
        public List<Door> Doors => doors;
        public TimberWear Wear { get => wear; set => wear = value; }

        /// <summary>A wall's two corners in the piece's space (counter-clockwise seen from above).</summary>
        public (Vector3 a, Vector3 b) WallEnds(Wall wall)
        {
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            var sw = new Vector3(-hx, 0f, -hz);
            var se = new Vector3(hx, 0f, -hz);
            var ne = new Vector3(hx, 0f, hz);
            var nw = new Vector3(-hx, 0f, hz);
            return wall switch
            {
                Wall.South => (sw, se),
                Wall.East => (se, ne),
                Wall.North => (ne, nw),
                _ => (nw, sw),
            };
        }
    }
}
