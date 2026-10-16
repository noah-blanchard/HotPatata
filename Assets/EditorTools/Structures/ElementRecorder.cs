using System;
using System.Collections.Generic;
using HotPatata;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// Listens to the timber kit while it draws a piece (ARCHITECTURE §27.1): every board, beam, rope, turned part and invisible wall the
    /// kit lays is written down, with what it is (its tags: group, kind, walking role) and where it is, so the piece can be frozen into
    /// elements that look exactly like it. The kit draws as usual; only while <see cref="Active"/> is set does a
    /// <see cref="MeshBuilder"/> made by <see cref="TimberKit.Visual"/> report to it.
    /// </summary>
    public sealed class ElementRecorder
    {
        /// <summary>The recorder the kit reports to (null: nothing records, the kit only draws).</summary>
        public static ElementRecorder Active { get; set; }

        /// <summary>
        /// Every piece of timber the kit draws comes out frozen, in place: elements under the piece's own root (a structure of its own),
        /// its walls as <see cref="TimberWall"/>s, the boxes they stand for not made. A course drawn once this way is handed over to be
        /// edited by hand (PatataRanger, M18.4c).
        /// </summary>
        public static bool FreezeInPlace { get; set; }

        /// <summary>The recorder of the last timber the kit began drawing while freezing in place.</summary>
        public static ElementRecorder LastInPlace { get; set; }

        static bool Listening => Active != null || FreezeInPlace;

        public enum Shape { Box, Sweep, Tube, Lathe, Paint }

        /// <summary>What the kit says the next parts are.</summary>
        public struct Tags
        {
            public string group;
            public TimberElementKind? kind;
            public TimberWalk walk;
            public int run;
            public int order;
            public bool solid;
        }

        public sealed class Record
        {
            public Shape shape;
            public Tags tags;
            public int slot;
            public int grain;
            // a box
            public Vector3 center;
            public Quaternion rotation;
            public Vector3 size;
            // a sweep or a tube
            public List<Vector3> path;
            public float width, height;
            public MeshBuilder.Anchor anchor;
            public bool caps;
            public float radius;
            // a lathe
            public List<Vector2> profile;
            public int sides;
            // a painted shape (its outline in profile)
            public TimberPaintColor paint;
        }

        public sealed class Wall
        {
            public string group;
            public List<Vector3> line;
            public float height;
        }

        public readonly List<Record> Records = new List<Record>();
        public readonly List<Wall> Walls = new List<Wall>();
        public readonly List<FrozenStructure.RunSurface> Runs = new List<FrozenStructure.RunSurface>();
        readonly HashSet<GameObject> replaced = new HashSet<GameObject>();
        static Tags current = new Tags { group = "Timber" };

        // ------------------------------------------------------------------ tags

        /// <summary>Tags the parts drawn until the scope ends (null fields keep the outer scope's); a no-op when nothing records.</summary>
        public static TagScope Tag(string group = null, TimberElementKind? kind = null, TimberWalk? walk = null, int? run = null, bool? solid = null)
        {
            if (!Listening) return default;
            var saved = current;
            if (group != null) current.group = group;
            if (kind != null) current.kind = kind;
            if (walk != null) current.walk = walk.Value;
            if (run != null) current.run = run.Value;
            if (solid != null) current.solid = solid.Value;
            return new TagScope(true, saved);
        }

        /// <summary>Where along its run the next parts lie (parts of one order make one point of the walking surface).</summary>
        public static void Order(int order)
        {
            if (Listening) current.order = order;
        }

        /// <summary>A run's walking surface: as wide as <paramref name="width"/>, <paramref name="depth"/> deep.</summary>
        public static void RunSurface(int run, float width, float depth)
        {
            (Active ?? (FreezeInPlace ? LastInPlace : null))?.Runs.Add(new FrozenStructure.RunSurface { run = run, width = width, depth = depth });
        }

        public readonly struct TagScope : IDisposable
        {
            readonly bool live;
            readonly Tags saved;

            public TagScope(bool live, Tags saved)
            {
                this.live = live;
                this.saved = saved;
            }

            public void Dispose()
            {
                if (live) current = saved;
            }
        }

        // ------------------------------------------------------------------ what the kit lays

        public void Box(Vector3 center, Quaternion rotation, Vector3 size, int slot, Vector2 uvShift) =>
            Records.Add(new Record { shape = Shape.Box, tags = current, slot = slot, grain = Grain(uvShift), center = center, rotation = rotation, size = size });

        public void Sweep(IList<Vector3> path, float width, float height, int slot, MeshBuilder.Anchor anchor, bool caps, float uvShift) =>
            Records.Add(new Record
            {
                shape = Shape.Sweep, tags = current, slot = slot, grain = Grain(new Vector2(uvShift, 0f)), path = new List<Vector3>(path),
                width = width, height = height, anchor = anchor, caps = caps,
            });

        public void Tube(IList<Vector3> points, IList<float> radii, int sides, int slot, bool caps, float uvShift)
        {
            float radius = 0f;
            foreach (var r in radii) radius += r;
            Records.Add(new Record
            {
                shape = Shape.Tube, tags = current, slot = slot, grain = Grain(new Vector2(uvShift, 0f)), path = new List<Vector3>(points),
                radius = radii.Count > 0 ? radius / radii.Count : 0.03f, caps = caps, sides = sides,
            });
        }

        public void Lathe(Vector3 origin, Quaternion rotation, IList<Vector2> profile, int sides, int slot) =>
            Records.Add(new Record { shape = Shape.Lathe, tags = current, slot = slot, center = origin, rotation = rotation, profile = new List<Vector2>(profile), sides = sides });

        /// <summary>A painted shape: its outline in the plane of <paramref name="rotation"/>'s x and z, proud along its y.</summary>
        public static void Paint(Vector3 center, Quaternion rotation, IList<Vector2> outline, TimberPaintColor paint)
        {
            var r = Active;
            if (r == null) return;
            r.Records.Add(new Record { shape = Shape.Paint, tags = current, center = center, rotation = rotation, profile = new List<Vector2>(outline), paint = paint });
        }

        /// <summary>
        /// An invisible wall the kit raised (a rail's, a bridge side's): it becomes a <see cref="TimberWall"/>. True when the wall is
        /// already made (freezing in place: <paramref name="made"/> is the wall), so the kit lays no boxes of its own.
        /// </summary>
        public static bool RecordWall(GameObject made, IList<Vector3> line, float height)
        {
            var r = Active;
            if (r != null)
            {
                r.replaced.Add(made);
                r.Walls.Add(new Wall { group = current.group, line = new List<Vector3>(line), height = height });
                return false;
            }
            if (!FreezeInPlace) return false;
            made.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var wall = made.AddComponent<TimberWall>();
            wall.Height = height;
            wall.Points.Clear();
            foreach (var p in line) wall.Points.Add(made.transform.InverseTransformPoint(p));
            TimberElementBuilder.Refresh(wall, true);
            return true;
        }

        /// <summary>A collider the frozen elements stand for (a wall's box, now each board's own): not kept (freezing in place: not made).</summary>
        public static void Replaced(GameObject made)
        {
            if (Active != null) Active.replaced.Add(made);
            else if (FreezeInPlace && made != null) UnityEngine.Object.DestroyImmediate(made);
        }

        /// <summary>True if <paramref name="t"/> or a parent of it was replaced by elements.</summary>
        public bool IsReplaced(Transform t)
        {
            for (; t != null; t = t.parent)
                if (replaced.Contains(t.gameObject)) return true;
            return false;
        }

        /// <summary>The kind a part is when the kit did not say: by its material and shape.</summary>
        public static TimberElementKind KindOf(Record r)
        {
            if (r.tags.kind != null && r.shape != Shape.Paint) return r.tags.kind.Value;
            if (r.shape == Shape.Paint) return TimberElementKind.Trim;
            switch (r.slot)
            {
                case TimberKit.Plank:
                case TimberKit.PlankAged: return TimberElementKind.Plank;
                case TimberKit.Iron: return TimberElementKind.Iron;
                case TimberKit.Rope: return TimberElementKind.Rope;
            }
            if (r.shape == Shape.Tube) return TimberElementKind.Log;
            if (r.shape == Shape.Box && Vector3.Dot(r.rotation * Vector3.forward, Vector3.up) > 0.95f) return TimberElementKind.Post;
            return TimberElementKind.Beam;
        }

        /// <summary>A texture shift brought to one of the catalogue's grain variants.</summary>
        public static int Grain(Vector2 uvShift)
        {
            unchecked
            {
                int h = Mathf.RoundToInt(uvShift.x * 100f) * 73856093 ^ Mathf.RoundToInt(uvShift.y * 100f) * 19349663;
                return ((h % TimberElement.GrainVariants) + TimberElement.GrainVariants) % TimberElement.GrainVariants;
            }
        }

        /// <summary>The texture shift of a grain variant (what the catalogue draws it with).</summary>
        public static Vector2 Shift(int grain) => new Vector2(grain * 0.37f % 3f, grain * 1.13f % 3f);
    }
}
