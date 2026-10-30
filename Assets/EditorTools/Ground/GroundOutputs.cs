using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HotPatata.Editor
{
    /// <summary>
    /// What the land pieces put in their outputs (ARCHITECTURE §27.4), once the height stack has applied them: a trail's walk line (on its
    /// profile, joined to the level's walk lines when it is walked), a river's water ribbon (the nature water, its flow along the bed) and
    /// its kill zones (boxes every <see cref="KillStep"/> metres or less, from under the surface down to a metre under the bed). A ravine has
    /// neither water nor kill zone. Their bands go into the ground's bake (<see cref="GroundBaker.Sources"/>).
    /// </summary>
    [InitializeOnLoad]
    public static class GroundOutputs
    {
        /// <summary>The longest kill zone along a river (m).</summary>
        public const float KillStep = 8f;
        /// <summary>The water ribbon reaches this far into the banks (m), so no gap shows at its edge.</summary>
        public const float RibbonOverlap = 0.35f;

        static GroundOutputs()
        {
            GroundBaker.Sources.Add(Bake);
            PalettePaths.Lines.Add(scene => TimberLinks.Pieces(scene).OfType<TrailShape>().SelectMany(t => Segments(TrailLine(t))));
        }

        /// <summary>The terrain under a piece (the first that covers its position).</summary>
        public static Terrain TerrainUnder(LevelPiece piece) =>
            LevelTerrain.Terrains(piece.gameObject.scene).FirstOrDefault(t => LevelTerrain.Covers(t, new Vector2(piece.transform.position.x, piece.transform.position.z)))
            ?? LevelTerrain.Terrains(piece.gameObject.scene).FirstOrDefault();

        static void EnsureComposed(LevelPiece piece, bool known)
        {
            if (known) return;
            var terrain = TerrainUnder(piece);
            if (terrain != null) TerrainStack.Recompose(terrain, LevelTerrain.Area(terrain));
        }

        static IEnumerable<(Vector2, Vector2)> Segments(IList<Vector3> line)
        {
            for (int i = 0; i + 1 < line.Count; i++) yield return (new Vector2(line[i].x, line[i].z), new Vector2(line[i + 1].x, line[i + 1].z));
        }

        /// <summary>A trail's line (its last profile), or its points on the flat when it was never composed.</summary>
        public static Vector3[] TrailLine(TrailShape trail) =>
            GroundShapes.Trails.TryGetValue(trail.PieceId ?? "", out var p) ? p.line : trail.WorldPoints().Select(q => new Vector3(q.x, trail.transform.position.y, q.y)).ToArray();

        // ------------------------------------------------------------------ trail

        /// <summary>The trail's walk line (a little over its profile), or null when it is not walked.</summary>
        public static Vector3[] BuildTrail(TrailShape trail)
        {
            EnsureComposed(trail, GroundShapes.Trails.ContainsKey(trail.PieceId));
            if (!trail.Walked || !GroundShapes.Trails.TryGetValue(trail.PieceId, out var profile)) return null;
            return Curves.Resample(profile.line, 1f).Select(p => p + Vector3.up * 0.05f).ToArray();
        }

        // ------------------------------------------------------------------ river

        public static void BuildRiver(RiverShape river, Transform root, string name)
        {
            EnsureComposed(river, GroundShapes.Rivers.ContainsKey(river.PieceId));
            if (!river.Water || !GroundShapes.Rivers.TryGetValue(river.PieceId, out var bed) || bed.bed.Length < 2) return;
            NatureKit.EnsureWater();
            // the ribbon: every metre along the bed, across the water's width, at the surface
            var line = new List<int>();
            float since = 0f;
            for (int i = 0; i < bed.bed.Length; i++)
            {
                if (i > 0) since += Vector2.Distance(new Vector2(bed.bed[i].x, bed.bed[i].z), new Vector2(bed.bed[i - 1].x, bed.bed[i - 1].z));
                if (i == 0 || i == bed.bed.Length - 1 || since >= 1f)
                {
                    line.Add(i);
                    since = 0f;
                }
            }
            float half = bed.waterHalf + RibbonOverlap;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float along = 0f;
            for (int k = 0; k < line.Count; k++)
            {
                int i = line[k];
                var p = bed.bed[i];
                var prev = bed.bed[line[Mathf.Max(0, k - 1)]];
                var next = bed.bed[line[Mathf.Min(line.Count - 1, k + 1)]];
                var dir = new Vector3(next.x - prev.x, 0f, next.z - prev.z).normalized;
                var side = new Vector3(dir.z, 0f, -dir.x);
                if (k > 0) along += Vector2.Distance(new Vector2(p.x, p.z), new Vector2(bed.bed[line[k - 1]].x, bed.bed[line[k - 1]].z));
                var c = new Vector3(p.x, bed.surface[i], p.z);
                verts.Add(c - side * half);
                verts.Add(c + side * half);
                uvs.Add(new Vector2(-half, along));
                uvs.Add(new Vector2(half, along));
                if (k == 0) continue;
                int b = verts.Count - 4;
                tris.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
            }
            var mesh = new Mesh { name = name + " water" };
            mesh.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.SetNormals(Enumerable.Repeat(Vector3.up, verts.Count).ToList());
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            var water = new GameObject(name + " water");
            water.transform.SetParent(root, false);
            water.AddComponent<MeshFilter>().sharedMesh = OrganicAssets.Save(mesh);
            var r = water.AddComponent<MeshRenderer>();
            r.sharedMaterial = NatureMaterialBuilder.Load(NatureKit.WaterMaterialName);
            r.shadowCastingMode = ShadowCastingMode.Off;
            water.AddComponent<NatureWater>();

            // kill zones: a box every few metres along the water, from just under its surface to a metre under the bed
            float run = 0f;
            int from = 0;
            for (int i = 1; i < bed.bed.Length; i++)
            {
                run += Vector2.Distance(new Vector2(bed.bed[i].x, bed.bed[i].z), new Vector2(bed.bed[i - 1].x, bed.bed[i - 1].z));
                if (run < KillStep && i < bed.bed.Length - 1) continue;
                var a = bed.bed[from];
                var e = bed.bed[i];
                var mid = (a + e) * 0.5f;
                var dir = new Vector3(e.x - a.x, 0f, e.z - a.z);
                float len = dir.magnitude;
                float top = Mathf.Max(bed.surface[from], bed.surface[i]) - NatureKit.KillBelowWater;
                float bottom = Mathf.Min(a.y, e.y) - 1f;
                var kill = CourseKit.Place(root, CourseKit.GameplayDir + "KillZone", $"{name} (drowning {from})", new Vector3(mid.x, (top + bottom) * 0.5f, mid.z),
                                           Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir : Vector3.forward, Vector3.up));
                kill.transform.localScale = new Vector3(half * 2f, Mathf.Max(0.6f, top - bottom), len + 1f);
                from = i;
                run = 0f;
            }
        }

        // ------------------------------------------------------------------ the bake's bands

        static void Bake(UnityEngine.SceneManagement.Scene scene, Terrain terrain, GroundBaker.Inputs inputs)
        {
            var h = inputs.heights;
            foreach (var trail in TimberLinks.Pieces(scene).OfType<TrailShape>())
            {
                if (trail.Paint == TrailPaint.None || !GroundShapes.Trails.TryGetValue(trail.PieceId, out var p)) continue;
                int layer = trail.Paint == TrailPaint.Gravel ? GroundLayerBuilder.Gravel : trail.Paint == TrailPaint.Mud ? GroundLayerBuilder.Mud : GroundLayerBuilder.Trail;
                var force = new GroundField(h.Size, h.Area);
                float half = trail.Width * 0.5f;
                for (int i = 0; i + 1 < p.line.Length; i++)
                {
                    Vector2 a = new Vector2(p.line[i].x, p.line[i].z), b = new Vector2(p.line[i + 1].x, p.line[i + 1].z);
                    force.Segment(a, b, half + 1f, (x, z, t) =>
                    {
                        float d = Vector2.Distance(force.World(x, z), Vector2.Lerp(a, b, t));
                        float w = d <= half ? 0.95f : 0.95f * Mathf.Clamp01(1f - (d - half));
                        force[x, z] = Mathf.Max(force[x, z], w);
                    });
                }
                inputs.force.Add((force, layer));
            }
            foreach (var river in TimberLinks.Pieces(scene).OfType<RiverShape>())
            {
                if (!GroundShapes.Rivers.TryGetValue(river.PieceId, out var bed) || bed.bed.Length < 2) continue;
                var force = river.Water ? null : new GroundField(h.Size, h.Area);
                for (int i = 0; i + 1 < bed.bed.Length; i++)
                {
                    Vector2 a = new Vector2(bed.bed[i].x, bed.bed[i].z), b = new Vector2(bed.bed[i + 1].x, bed.bed[i + 1].z);
                    float surface = bed.surface[i];
                    if (river.Water)
                        inputs.water.Segment(a, b, bed.waterHalf, (x, z, _) =>
                        {
                            inputs.water[x, z] = 1f;
                            if (h[x, z] < surface) inputs.underWater[x, z] = 1f;
                        });
                    else
                        force.Segment(a, b, bed.half, (x, z, _) => force[x, z] = 0.85f);
                }
                if (force != null) inputs.force.Add((force, GroundLayerBuilder.Scree));
            }
        }
    }
}
