using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// Freezing stone (ARCHITECTURE §27.7, docs/LEVEL_AUTHORING.md §30): while a wall is drawn for the freezer, its stones and its
    /// collision boxes are heard here instead of meshed; the freezer makes a <see cref="StoneBlock"/> per stone (or, past
    /// <see cref="StoneFreezeLimit"/>, one mesh per straight run: freeze per segment) and <see cref="MasonryWall"/>s for its collision,
    /// boxes in a row merged into one wall with points to drag. <see cref="OpenHere"/> takes stones out and cuts the wall round them.
    /// </summary>
    public static class StoneFreeze
    {
        /// <summary>Past this many stones a wall freezes per segment (a mesh per run), not per stone.</summary>
        public const int StoneFreezeLimit = 400;

        public struct StoneRecord
        {
            public Vector3 center, size;
            public Quaternion rotation;
            public float bevel;
            public int seed;
        }

        public struct SolidRecord
        {
            public Vector3 center, size;
            public Quaternion rotation;
        }

        static List<StoneRecord> stones;
        static List<SolidRecord> solids;

        public static bool Recording => stones != null;

        public static void Begin()
        {
            stones = new List<StoneRecord>();
            solids = new List<SolidRecord>();
        }

        public static (List<StoneRecord> stones, List<SolidRecord> solids) End()
        {
            var result = (stones ?? new List<StoneRecord>(), solids ?? new List<SolidRecord>());
            stones = null;
            solids = null;
            return result;
        }

        /// <summary>Hears a stone (true: it is not meshed).</summary>
        public static bool Stone(Vector3 center, Quaternion rotation, Vector3 size, float bevel, int seed)
        {
            if (stones == null) return false;
            stones.Add(new StoneRecord { center = center, rotation = rotation, size = size, bevel = bevel, seed = seed });
            return true;
        }

        public static void Solid(GameObject box)
        {
            if (solids == null) return;
            var c = box.GetComponent<BoxCollider>();
            solids.Add(new SolidRecord { center = box.transform.position, rotation = box.transform.rotation, size = c.size });
            ElementRecorder.Replaced(box);
        }

        public static StoneKind KindOf(string material) => material switch
        {
            "Nature_Cliff" => StoneKind.Sandstone,
            "Nature_Limestone" => StoneKind.Limestone,
            "Nature_MossyRock" => StoneKind.Mossy,
            _ => StoneKind.Granite,
        };

        public static string MaterialOf(StoneKind kind) => kind switch
        {
            StoneKind.Sandstone => "Nature_Cliff",
            StoneKind.Limestone => "Nature_Limestone",
            StoneKind.Mossy => "Nature_MossyRock",
            _ => "Nature_Granite",
        };

        /// <summary>The stones heard as elements under <paramref name="parent"/>, or (per segment) a mesh per straight run of the wall.</summary>
        public static void Make(List<StoneRecord> records, Transform parent, StoneKind kind, bool perSegment, IWallSpec wall)
        {
            if (!perSegment)
            {
                int n = 0;
                foreach (var r in records)
                {
                    var go = new GameObject("Stone " + ++n);
                    go.transform.SetParent(parent, false);
                    go.transform.SetPositionAndRotation(r.center, r.rotation);
                    var block = go.AddComponent<StoneBlock>();
                    block.Kind = TimberElementKind.Trim;
                    block.Size = r.size;
                    block.Bevel = r.bevel;
                    block.Variant = r.seed;
                    block.Stone = kind;
                    block.Solid = false;
                }
                return;
            }
            var runs = MasonryKit.Runs(wall);
            foreach (var group in records.GroupBy(r => Nearest(runs, r.center)))
            {
                var mb = new MeshBuilder(1);
                foreach (var r in group) MasonryKit.Block(mb, r.center, r.rotation, r.size, r.bevel, 0.03f, r.seed, 0);
                var mesh = MeshCatalog.Store(mb.ToMesh($"{wall.Owner.name} run {group.Key + 1}", out _));
                var go = new GameObject($"Run {group.Key + 1}");
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = NatureMaterialBuilder.Load(MaterialOf(kind));
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
        }

        static int Nearest(List<MasonryKit.Run> runs, Vector3 p)
        {
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < runs.Count; i++)
            {
                var r = runs[i];
                var rel = p - r.a;
                rel.y = 0f;
                float along = Mathf.Clamp(Vector3.Dot(rel, r.along), 0f, r.length);
                float d = (rel - r.along * along).magnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>The boxes heard as masonry walls: boxes in a row (end to end, the same bottom and top) make one wall of many points.</summary>
        public static void MakeWalls(List<SolidRecord> records, Transform parent)
        {
            var walls = new List<(List<Vector3> pts, float bottom, float top, float thick, Vector3 along)>();
            foreach (var r in records)
            {
                var along = r.rotation * Vector3.forward;
                float bottom = r.center.y - r.size.y * 0.5f, top = r.center.y + r.size.y * 0.5f;
                var a = r.center - along * r.size.z * 0.5f;
                var b = r.center + along * r.size.z * 0.5f;
                a.y = b.y = 0f;
                var last = walls.Count > 0 ? walls[walls.Count - 1] : default;
                if (last.pts != null && Mathf.Abs(last.bottom - bottom) < 0.05f && Mathf.Abs(last.top - top) < 0.05f && Mathf.Abs(last.thick - r.size.x) < 0.02f &&
                    (last.pts[last.pts.Count - 1] - a).sqrMagnitude < 0.01f)
                {
                    if (Vector3.Angle(last.along, along) < 1f) last.pts[last.pts.Count - 1] = b;
                    else last.pts.Add(b);
                    walls[walls.Count - 1] = (last.pts, bottom, top, last.thick, along);
                    continue;
                }
                walls.Add((new List<Vector3> { a, b }, bottom, top, r.size.x, along));
            }
            int n = 0;
            foreach (var (pts, bottom, top, thick, _) in walls)
            {
                var go = new GameObject("Wall " + ++n);
                go.transform.SetParent(parent, false);
                var mid = (pts[0] + pts[pts.Count - 1]) * 0.5f;
                go.transform.position = new Vector3(mid.x, bottom, mid.z);
                var w = go.AddComponent<MasonryWall>();
                w.Bottom = 0f;
                w.Top = top - bottom;
                w.Thickness = thick;
                w.Points.Clear();
                w.Points.AddRange(pts.Select(p => go.transform.InverseTransformPoint(new Vector3(p.x, bottom, p.z))));
                Refresh(w, true);
            }
        }

        /// <summary>Lays a masonry wall's boxes again if its data changed; true if it did.</summary>
        public static bool Refresh(MasonryWall wall, bool force = false)
        {
            if (wall == null) return false;
            string key = wall.ShapeKey();
            if (!force && wall.BuiltFrom == key) return false;
            foreach (Transform child in wall.transform.Cast<Transform>().ToList())
                if (child.GetComponent<BoxCollider>() != null && child.GetComponent<TimberElement>() == null) Object.DestroyImmediate(child.gameObject);
            wall.gameObject.layer = CourseKit.Layer("Environment");
            for (int i = 0; i + 1 < wall.Points.Count; i++)
            {
                var a = wall.Points[i];
                var b = wall.Points[i + 1];
                var flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
                if (flat.sqrMagnitude < 1e-4f) continue;
                var child = new GameObject("Wall " + i) { layer = wall.gameObject.layer, hideFlags = HideFlags.NotEditable };
                child.transform.SetParent(wall.transform, false);
                float baseY = Mathf.Min(a.y, b.y) + wall.Bottom, topY = Mathf.Max(a.y, b.y) + wall.Top;
                child.transform.localPosition = new Vector3((a.x + b.x) * 0.5f, (baseY + topY) * 0.5f, (a.z + b.z) * 0.5f);
                child.transform.localRotation = Quaternion.LookRotation(flat.normalized);
                child.AddComponent<BoxCollider>().size = new Vector3(wall.Thickness, topY - baseY, flat.magnitude);
                GameObjectUtility.SetStaticEditorFlags(child, StaticEditorFlags.BatchingStatic);
            }
            wall.BuiltFrom = key;
            EditorUtility.SetDirty(wall);
            return true;
        }

        // ------------------------------------------------------------------ open here

        [MenuItem("HotPatata/Level/Open Wall Here", true)]
        static bool CanOpen() => Selection.gameObjects.Any(g => g.GetComponent<StoneBlock>() != null);

        [MenuItem("HotPatata/Level/Open Wall Here", priority = 66)]
        static void OpenSelected() => OpenHere(Selection.gameObjects.Select(g => g.GetComponent<StoneBlock>()).Where(s => s != null).ToList());

        /// <summary>
        /// Takes <paramref name="taken"/> stones out of their frozen wall and cuts its masonry walls round where they were: the run split
        /// either side, the part under them (a sill) and over them (a lintel) kept. One undo step.
        /// </summary>
        public static int OpenHere(List<StoneBlock> taken)
        {
            if (taken.Count == 0) return 0;
            Undo.IncrementCurrentGroup();
            var bounds = new Bounds(taken[0].transform.position, Vector3.zero);
            foreach (var s in taken)
            {
                var half = s.transform.rotation * (s.Size * 0.5f);
                bounds.Encapsulate(s.transform.position + new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z)));
                bounds.Encapsulate(s.transform.position - new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z)));
            }
            var structure = taken[0].GetComponentInParent<FrozenStructure>();
            int cut = 0;
            if (structure != null)
            {
                foreach (var wall in structure.GetComponentsInChildren<MasonryWall>(true).ToList())
                    if (Cut(wall, bounds)) cut++;
                // what the stones were laid on (the core behind the joints) opens with them
                foreach (var filter in structure.GetComponentsInChildren<MeshFilter>(true).Where(f => f.GetComponent<TimberElement>() == null && f.sharedMesh != null && f.transform.parent != null && f.transform.parent.name == "Core"))
                    CutMesh(filter, bounds);
            }
            foreach (var s in taken) Undo.DestroyObjectImmediate(s.gameObject);
            Undo.SetCurrentGroupName("Open wall here");
            return cut;
        }

        /// <summary>Drops the triangles of a drawn mesh whose middle is in <paramref name="hole"/> (its copy goes to the catalogue).</summary>
        static void CutMesh(MeshFilter filter, Bounds hole)
        {
            var mesh = Object.Instantiate(filter.sharedMesh);
            var v = mesh.vertices;
            var t = filter.transform;
            bool any = false;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var tris = mesh.GetTriangles(sub);
                var kept = new List<int>(tris.Length);
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    var mid = t.TransformPoint((v[tris[i]] + v[tris[i + 1]] + v[tris[i + 2]]) / 3f);
                    if (hole.Contains(mid)) { any = true; continue; }
                    kept.Add(tris[i]);
                    kept.Add(tris[i + 1]);
                    kept.Add(tris[i + 2]);
                }
                mesh.SetTriangles(kept, sub);
            }
            if (!any) { Object.DestroyImmediate(mesh); return; }
            Undo.RecordObject(filter, "Open wall here");
            filter.sharedMesh = MeshCatalog.Store(mesh);
        }

        static bool Cut(MasonryWall wall, Bounds hole)
        {
            var t = wall.transform;
            for (int i = 0; i + 1 < wall.Points.Count; i++)
            {
                var a = t.TransformPoint(wall.Points[i]);
                var b = t.TransformPoint(wall.Points[i + 1]);
                var d = b - a;
                d.y = 0f;
                float len = d.magnitude;
                if (len < 1e-3f) continue;
                var along = d / len;
                var rel = hole.center - a;
                rel.y = 0f;
                float s = Vector3.Dot(rel, along), off = Mathf.Abs(Vector3.Dot(rel, Vector3.Cross(Vector3.up, along)));
                if (s < 0f || s > len || off > wall.Thickness) continue;
                float halfAlong = Mathf.Abs(Vector3.Dot(hole.extents, new Vector3(Mathf.Abs(along.x), 0f, Mathf.Abs(along.z))));
                float s0 = Mathf.Max(0f, s - halfAlong), s1 = Mathf.Min(len, s + halfAlong);
                float foot = Mathf.Min(a.y, b.y);
                float holeBottom = hole.min.y - foot, holeTop = hole.max.y - foot;
                Undo.RecordObject(wall, "Open wall here");
                var before = wall.Points.Take(i + 1).Select(p => t.TransformPoint(p)).ToList();
                var after = wall.Points.Skip(i + 1).Select(p => t.TransformPoint(p)).ToList();
                var p0 = a + along * s0;
                var p1 = a + along * s1;
                // this wall keeps the part before the hole; a new one the part after it, one under it and one over it
                wall.Points.Clear();
                wall.Points.AddRange(before.Append(p0).Select(t.InverseTransformPoint));
                Refresh(wall, true);
                void Part(string name, List<Vector3> pts, float bottom, float top)
                {
                    if (pts.Count < 2 || top - bottom < 0.1f) return;
                    var go = new GameObject(name);
                    Undo.RegisterCreatedObjectUndo(go, "Open wall here");
                    go.transform.SetParent(t.parent, false);
                    go.transform.SetPositionAndRotation(t.position, t.rotation);
                    var w = go.AddComponent<MasonryWall>();
                    w.Bottom = bottom;
                    w.Top = top;
                    w.Thickness = wall.Thickness;
                    w.Points.Clear();
                    w.Points.AddRange(pts.Select(go.transform.InverseTransformPoint));
                    Refresh(w, true);
                }
                Part(wall.name + " (after)", new List<Vector3> { p1 }.Concat(after).ToList(), wall.Bottom, wall.Top);
                Part(wall.name + " (sill)", new List<Vector3> { p0, p1 }, wall.Bottom, Mathf.Min(wall.Top, holeBottom));
                Part(wall.name + " (lintel)", new List<Vector3> { p0, p1 }, Mathf.Max(wall.Bottom, holeTop), wall.Top);
                return true;
            }
            return false;
        }
    }
}
