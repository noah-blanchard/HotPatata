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

        public enum Shape { Box, Sweep, Tube, Lathe }

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
        Tags current = new Tags { group = "Timber" };

        // ------------------------------------------------------------------ tags

        /// <summary>Tags the parts drawn until the scope ends (null fields keep the outer scope's); a no-op when nothing records.</summary>
        public static TagScope Tag(string group = null, TimberElementKind? kind = null, TimberWalk? walk = null, int? run = null, bool? solid = null)
        {
            var r = Active;
            if (r == null) return default;
            var saved = r.current;
            if (group != null) r.current.group = group;
            if (kind != null) r.current.kind = kind;
            if (walk != null) r.current.walk = walk.Value;
            if (run != null) r.current.run = run.Value;
            if (solid != null) r.current.solid = solid.Value;
            return new TagScope(r, saved);
        }

        /// <summary>Where along its run the next parts lie (parts of one order make one point of the walking surface).</summary>
        public static void Order(int order)
        {
            if (Active != null) Active.current.order = order;
        }

        /// <summary>A run's walking surface: as wide as <paramref name="width"/>, <paramref name="depth"/> deep.</summary>
        public static void RunSurface(int run, float width, float depth)
        {
            Active?.Runs.Add(new FrozenStructure.RunSurface { run = run, width = width, depth = depth });
        }

        public readonly struct TagScope : IDisposable
        {
            readonly ElementRecorder recorder;
            readonly Tags saved;

            public TagScope(ElementRecorder recorder, Tags saved)
            {
                this.recorder = recorder;
                this.saved = saved;
            }

            public void Dispose()
            {
                if (recorder != null) recorder.current = saved;
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

        /// <summary>An invisible wall the kit raised (a rail's, a bridge side's): it becomes a <see cref="TimberWall"/>.</summary>
        public static void RecordWall(GameObject made, IList<Vector3> line, float height)
        {
            var r = Active;
            if (r == null) return;
            r.replaced.Add(made);
            r.Walls.Add(new Wall { group = r.current.group, line = new List<Vector3>(line), height = height });
        }

        /// <summary>A collider the frozen elements stand for (a wall's box, now each board's own): it is not kept.</summary>
        public static void Replaced(GameObject made) => Active?.replaced.Add(made);

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
            if (r.tags.kind != null) return r.tags.kind.Value;
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
