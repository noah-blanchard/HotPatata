using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// Draws the pieces of an authored level (ARCHITECTURE §27): each piece's data goes through the timber kit (<see cref="TimberKit"/>,
    /// <see cref="TimberBuildings"/>) on its <see cref="SceneSite"/>, into a fresh <see cref="LevelPieceOutput"/> under the piece. The
    /// meshes stay in memory until the scene is saved (<see cref="LevelMeshStore"/>). A piece is rebuilt only when what it is built
    /// from changed (<see cref="Signature"/>): its data, its place, the decks its ends sit on, the walks that open its rails.
    /// </summary>
    public static class LevelPieceBuilder
    {
        /// <summary>The share of aged boards of a worn piece (a new one keeps the kit's own).</summary>
        public const float AgedDeck = 0.6f, AgedWalk = 0.55f, AgedBridge = 0.65f;
        /// <summary>Two outline edges closer than this (not neighbours) are a seam: no rail along it (a ring round a trunk).</summary>
        public const float SeamGap = TimberDeckShape.SeamGap;
        /// <summary>
        /// A piece wider or longer than this (m), or with this many points, is not built: a point thrown far away (a click near the
        /// horizon) would make the kit lay boards for minutes and freeze the Editor.
        /// </summary>
        public const float MaxSpan = 300f;
        public const int MaxPoints = 400;
        /// <summary>A forest area may be wider (its planting is a few loops over its cells, nothing laid board by board).</summary>
        public const float MaxForestSpan = 600f;
        /// <summary>A tree trunk or a deck's trunk within this (m) of a piece is part of what it is built from.</summary>
        public const float TrunkReach = 20f;
        /// <summary>A forest is replanted when a piece within this (m) of its area changes.</summary>
        public const float ForestReach = 40f;

        /// <summary>The last rebuild's time (ms), for the log and the tests.</summary>
        public static double LastMilliseconds { get; private set; }

        /// <summary>Rebuilds the pieces whose inputs changed (or all with <paramref name="force"/>); returns how many were rebuilt.</summary>
        public static int Refresh(IEnumerable<LevelPiece> pieces, bool force = false)
        {
            var all = pieces.Where(p => p != null).Distinct().ToList();
            // trees first: decks lean on their measured trunks, and a tree's sawn bands follow the decks round it; then the timber;
            // forests last (they keep off everything else)
            int count = 0;
            foreach (var stage in new Func<LevelPiece, bool>[] { p => p is HeroTreeShape, p => !(p is HeroTreeShape) && !(p is ForestArea), p => p is ForestArea })
            {
                var stale = all.Where(stage).Where(p => force || NeedsRebuild(p)).ToList();
                if (stale.Count == 0) continue;
                Rebuild(stale);
                count += stale.Count;
            }
            return count;
        }

        public static bool NeedsRebuild(LevelPiece piece)
        {
            var output = piece.Output != null ? piece.Output.GetComponent<LevelPieceOutput>() : null;
            return output == null || output.Signature != Signature(piece);
        }

        public static void Rebuild(LevelPiece piece) => Rebuild(new[] { piece });

        public static void Rebuild(IList<LevelPiece> pieces)
        {
            var clock = Stopwatch.StartNew();
            EnsureMaterials();
            OrganicAssets.BeginInMemory();
            try
            {
                foreach (var piece in pieces)
                {
                    if (piece == null) continue;
                    try { BuildOne(piece); }
                    catch (Exception e) { Debug.LogException(e, piece); }
                }
            }
            finally { OrganicAssets.End(); }
            Physics.SyncTransforms();
            LastMilliseconds = clock.Elapsed.TotalMilliseconds;
        }

        static bool materialsChecked;

        static void EnsureMaterials()
        {
            if (materialsChecked) return;
            if (AssetDatabase.LoadAssetAtPath<Material>(NatureMaterialBuilder.Dir + TimberKit.PlankMaterial + ".mat") == null) NatureMaterialBuilder.Ensure(false);
            materialsChecked = true;
        }

        // ------------------------------------------------------------------ one piece

        static void BuildOne(LevelPiece piece)
        {
            piece.EnsureId();
            Clear(piece);
            var root = new GameObject("Generated");
            root.transform.SetParent(piece.transform, false);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var scale = piece.transform.lossyScale;
            root.transform.localScale = new Vector3(1f / NonZero(scale.x), 1f / NonZero(scale.y), 1f / NonZero(scale.z));
            var output = root.AddComponent<LevelPieceOutput>();
            TimberKit.ClearReport();
            string tooBig = TooBig(piece);
            if (tooBig != null)
            {
                Debug.LogWarning($"[Level] {piece.name} is not built: {tooBig}. Move its far points back (or Ctrl+Z).", piece);
                output.Configure(Signature(piece), null, 0);
                root.hideFlags = HideFlags.NotEditable;
                piece.SetOutput(root);
                return;
            }
            var line = BuildGeometry(piece, root.transform);
            output.Configure(Signature(piece), line, TimberKit.Unheld.Count);
            TimberKit.ClearReport();
            root.hideFlags = HideFlags.NotEditable;
            piece.SetOutput(root);
            EditorUtility.SetDirty(piece);
            if (piece.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(piece.gameObject.scene);
        }

        /// <summary>
        /// Lays a piece's geometry under <paramref name="root"/> (world space): what a rebuild puts in its output, and what freezing a piece
        /// records (<see cref="TimberFreezer"/>). Returns the line a walker treads on it, or null.
        /// </summary>
        public static Vector3[] BuildGeometry(LevelPiece piece, Transform root)
        {
            string name = piece.name;
            switch (piece)
            {
                case TimberDeckShape deck: BuildDeck(deck, root, name); break;
                case TimberWalkShape walk: return BuildWalk(walk, root, name);
                case RopeBridgeShape bridge: return BuildBridge(bridge, root, name);
                case RailingShape railing: TimberKit.Railing(root, name, railing.WorldPoints(), railing.Seed); break;
                case HouseShape house: BuildHouse(house, root, name); break;
                case TrestleShape trestle: BuildTrestle(trestle, root, name); break;
                case ShelterShape shelter:
                    TimberStructures.Shelter(root, name, shelter.transform.position, Yaw(shelter), shelter.Size, shelter.Height, shelter.Roof, shelter.RoofRise,
                                             shelter.LowWalls, shelter.Drum, shelter.Wear == TimberWear.Aged);
                    break;
                case GantryShape gantry:
                    TimberStructures.Gantry(root, name, gantry.transform.position, Yaw(gantry), gantry.Span, gantry.Depth, gantry.Height, gantry.Pulley, gantry.RopeDrop, gantry.Roof);
                    break;
                case SignShape sign:
                    TimberStructures.Sign(root, name, sign.transform.position, Yaw(sign), sign.Posts, sign.Board, sign.BoardHeight, sign.Glyph, sign.Roof);
                    break;
                case PulleyBeamShape beam:
                    TimberStructures.PulleyBeam(root, name, beam.transform.position, Yaw(beam), beam.Reach, beam.Height, beam.Post, beam.RopeDrop);
                    break;
                case HeroTreeShape tree: LevelNature.BuildTree(tree, root, name); break;
                case WaterShape water: LevelNature.BuildWater(water, root, name); break;
                case ForestArea forest: LevelForest.Plant(forest, root); break;
            }
            return null;
        }

        /// <summary>A piece's turn round the vertical (structures stand upright whatever the piece's tilt).</summary>
        static Quaternion Yaw(LevelPiece piece) => Quaternion.Euler(0f, piece.transform.eulerAngles.y, 0f);

        static float NonZero(float v) => Mathf.Abs(v) < 1e-4f ? 1f : v;

        /// <summary>Why a piece is too big to build (<see cref="MaxSpan"/>, <see cref="MaxPoints"/>), or null.</summary>
        public static string TooBig(LevelPiece piece)
        {
            IList<Vector3> points = piece switch
            {
                TimberDeckShape d => d.WorldOutline().Select(p => new Vector3(p.x, 0f, p.y)).ToList(),
                TimberWalkShape w => w.WorldControl(),
                RopeBridgeShape b => new[] { b.WorldEnds().a, b.WorldEnds().b },
                RailingShape r => r.WorldPoints(),
                HouseShape h => new[] { Vector3.zero, new Vector3(h.Size.x, h.WallHeight, h.Size.y) },
                TrestleShape t => new[] { Vector3.zero, new Vector3(t.Half.x * 2f, 0f, t.Half.y * 2f) },
                ShelterShape sh => new[] { Vector3.zero, new Vector3(sh.Size.x, sh.Height + sh.RoofRise, sh.Size.y) },
                GantryShape g => new[] { Vector3.zero, new Vector3(g.Span, g.Height, g.Depth) },
                SignShape sg => new[] { Vector3.zero, new Vector3(sg.Board.x, sg.BoardHeight + sg.Board.y, 0f) },
                PulleyBeamShape pb => new[] { Vector3.zero, new Vector3(0f, pb.Height, pb.Reach) },
                HeroTreeShape tree => new[] { Vector3.zero, new Vector3(tree.Lean.x, tree.Height, tree.Lean.y) },
                WaterShape water => new[] { Vector3.zero, new Vector3(water.Size.x, water.Depth, water.Size.y) },
                ForestArea forest => forest.WorldOutline().Select(p => new Vector3(p.x, 0f, p.y)).ToList(),
                _ => Array.Empty<Vector3>(),
            };
            if (points.Count > MaxPoints) return $"{points.Count} points (at most {MaxPoints})";
            if (points.Any(p => float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || p.magnitude > 1e5f)) return "a point is out of the world";
            if (points.Count == 0) return null;
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var p in points) bounds.Encapsulate(p);
            float span = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (piece is TimberWalkShape || piece is RailingShape) span = Mathf.Max(span, Curves.Length(points));
            float max = piece is ForestArea ? MaxForestSpan : MaxSpan;
            return span > max ? $"it spans {span:F0} m (at most {max:F0} m)" : null;
        }

        /// <summary>Removes a piece's generated geometry (every output under it, and the meshes only it held).</summary>
        public static void Clear(LevelPiece piece)
        {
            foreach (var old in piece.GetComponentsInChildren<LevelPieceOutput>(true).Where(o => o.transform.parent == piece.transform).ToArray())
            {
                foreach (var mesh in Meshes(old.gameObject).Where(m => !EditorUtility.IsPersistent(m)).ToArray()) Object.DestroyImmediate(mesh);
                foreach (var foliage in old.GetComponentsInChildren<FoliageInstancer>(true).Select(f => f.Set).Where(s => s != null && !EditorUtility.IsPersistent(s)).ToArray())
                    Object.DestroyImmediate(foliage);
                Object.DestroyImmediate(old.gameObject);
            }
            piece.SetOutput(null);
        }

        /// <summary>The meshes a piece's output draws and collides with.</summary>
        public static IEnumerable<Mesh> Meshes(GameObject output) =>
            output.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)
                .Concat(output.GetComponentsInChildren<MeshCollider>(true).Select(c => c.sharedMesh))
                .Where(m => m != null).Distinct();

        // ------------------------------------------------------------------ the pieces

        static void BuildDeck(TimberDeckShape deck, Transform root, string name)
        {
            var poly = deck.WorldOutline();
            if (poly.Count < 3) return;
            float y = deck.transform.position.y;
            var spec = new TimberKit.DeckSpec
            {
                grain = deck.Grain + deck.transform.eulerAngles.y,
                rails = deck.Rails,
                openings = TimberLinks.OpeningsOf(deck),
                railSkip = SeamSkip(poly),
                seed = deck.Seed,
                support = deck.Support,
                agedShare = deck.Wear == TimberWear.Aged ? AgedDeck : 0f,
            };
            TimberKit.Deck(root, name, poly, deck.TopAt, spec, SceneSite.At(deck.gameObject.scene, y, y - 0.3f, deck.MaxPost));
        }

        /// <summary>The rail samples on a seam: where the outline doubles back on itself (two edges closer than <see cref="SeamGap"/>).</summary>
        public static Func<Vector2, bool> SeamSkip(IList<Vector2> poly)
        {
            int n = poly.Count;
            return q =>
            {
                int nearest = -1;
                float best = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    float d = SegmentDistance(q, poly[i], poly[(i + 1) % n]);
                    if (d < best) { best = d; nearest = i; }
                }
                for (int j = 0; j < n; j++)
                {
                    int gap = Mathf.Abs(j - nearest);
                    if (gap <= 1 || gap >= n - 1) continue;
                    if (SegmentDistance(q, poly[j], poly[(j + 1) % n]) < SeamGap) return true;
                }
                return false;
            };
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        static Vector3[] BuildWalk(TimberWalkShape walk, Transform root, string name)
        {
            var control = walk.WorldControl();
            if (control.Count < 2 || Curves.Length(control) < 0.5f) return null;
            var path = Curves.Spline(control, 0.4f);
            float y = control.Average(c => c.y), top = control.Max(c => c.y);
            var spec = new TimberKit.WalkSpec
            {
                railLeft = walk.RailLeft,
                railRight = walk.RailRight,
                seed = walk.Seed,
                onGround = walk.OnGround,
                support = walk.Support,
                agedShare = walk.Wear == TimberWear.Aged ? AgedWalk : 0f,
            };
            TimberKit.Walkway(root, name, path, walk.Width, spec, SceneSite.At(walk.gameObject.scene, y, top + 0.2f, walk.MaxPost));
            return TimberKit.LastWalkSurface.ToArray();
        }

        static Vector3[] BuildBridge(RopeBridgeShape bridge, Transform root, string name)
        {
            var (a, b) = bridge.WorldEnds();
            if (Vector3.Distance(a, b) < 1f) return null;
            float sag = bridge.SagFor(a, b);
            var spec = new TimberKit.BridgeSpec
            {
                sideWalls = bridge.SideWalls,
                seed = bridge.Seed,
                agedShare = bridge.Wear == TimberWear.Aged ? AgedBridge : 0f,
            };
            TimberKit.RopeBridge(root, name, a, b, bridge.Width, sag, spec);
            return Curves.Sag(a, b, sag, 24).ToArray();
        }

        static void BuildHouse(HouseShape house, Transform root, string name)
        {
            var t = house.transform;
            var origin = t.position;
            var rot = Quaternion.Euler(0f, t.eulerAngles.y, 0f);
            Vector3 W(Vector3 local) => origin + rot * local;
            Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);
            float hx = house.Size.x * 0.5f, hz = house.Size.y * 0.5f, y = origin.y, top = y + house.WallHeight;
            if (house.Floor)
            {
                var poly = new List<Vector2>
                {
                    Flat(W(new Vector3(-hx, 0f, -hz))), Flat(W(new Vector3(hx, 0f, -hz))), Flat(W(new Vector3(hx, 0f, hz))), Flat(W(new Vector3(-hx, 0f, hz))),
                };
                TimberKit.Deck(root, name + " floor", poly, _ => y,
                               new TimberKit.DeckSpec { grain = t.eulerAngles.y, held = true, seed = house.Seed, agedShare = house.Wear == TimberWear.Aged ? AgedDeck : 0f },
                               SceneSite.At(house.gameObject.scene, y, y - 0.3f, 4f));
            }
            foreach (HouseShape.Wall wall in Enum.GetValues(typeof(HouseShape.Wall)))
            {
                var (a, b) = house.WallEnds(wall);
                var wa = W(a);
                var wb = W(b);
                float length = Vector3.Distance(wa, wb);
                var doors = house.Doors.Where(d => d.wall == wall).Select(d =>
                {
                    float width = Mathf.Min(d.width, length - 0.6f);
                    float at = Mathf.Clamp(d.along * length, width / 2f + 0.3f, length - width / 2f - 0.3f);
                    float sill = Mathf.Clamp(d.sill, 0f, house.WallHeight - 0.5f);
                    return (at, width, Mathf.Min(d.height, house.WallHeight - sill - 0.25f), sill);
                }).ToArray();
                TimberBuildings.PlankWall(root, $"{name} {wall} wall", Flat(wa), Flat(wb), y, top, doors, house.Wear == TimberWear.Aged ? 2 : 4);
            }
            bool ridgeAlongX = house.Size.x >= house.Size.y;
            using var roof = ElementRecorder.Tag("Roof", walk: TimberWalk.None, solid: false);
            TimberBuildings.Frame(root, name + " roof", mb =>
            {
                var eave = W(Vector3.zero) + Vector3.up * (house.WallHeight + 0.1f);
                if (ridgeAlongX) TimberBuildings.Roof(mb, eave, hz, hx, house.RoofRise, rot * Quaternion.Euler(0f, 90f, 0f), TimberKit.PlankAged, 0.45f);
                else TimberBuildings.Roof(mb, eave, hx, hz, house.RoofRise, rot, TimberKit.PlankAged, 0.45f);
            });
            if (house.Ceiling)
            {
                var ceiling = TimberBuildings.SolidInvisible(root, name + " ceiling", W(Vector3.zero) + Vector3.up * (house.WallHeight + 0.2f), rot,
                                                             new Vector3(house.Size.x + 0.6f, 0.4f, house.Size.y + 0.6f));
                ceiling.AddComponent<CourseCeiling>();
            }
        }

        static void BuildTrestle(TrestleShape trestle, Transform root, string name)
        {
            var at = trestle.transform.position;
            TimberBuildings.Trestle(root, name, at, at.y, trestle.Half, trestle.transform.eulerAngles.y, p => SceneSite.GroundAt(p, at.y - 0.3f));
        }

        // ------------------------------------------------------------------ what a piece is built from

        /// <summary>
        /// Everything a piece's geometry depends on, as text: its name (the kit seeds its randomness with it), its data, its place, and
        /// what it meets (an end's deck edge, the openings attached ends cut in a deck). The ground under it is not in it (Rebuild All).
        /// </summary>
        public static string Signature(LevelPiece piece)
        {
            var s = new StringBuilder();
            s.Append(piece.GetType().Name).Append('|').Append(piece.name).Append('|').Append(piece.Seed).Append('|');
            V(s, piece.transform.position);
            V(s, piece.transform.eulerAngles);
            V(s, piece.transform.lossyScale);
            switch (piece)
            {
                case TimberDeckShape d:
                    Trunks(s, d, d.transform.position.y);
                    foreach (var p in d.Outline) V(s, p);
                    s.Append(d.Slope.x.ToString("F3", CultureInfo.InvariantCulture)).Append(',').Append(d.Slope.y.ToString("F3", CultureInfo.InvariantCulture))
                     .Append('|').Append(d.Grain.ToString("F2", CultureInfo.InvariantCulture)).Append(d.Rails).Append(d.Support).Append(d.Wear)
                     .Append(d.MaxPost.ToString("F2", CultureInfo.InvariantCulture)).Append('|');
                    foreach (var o in TimberLinks.OpeningsOf(d)) { V(s, new Vector3(o.at.x, o.width, o.at.y)); }
                    if (d.TrunkComponent != null) V(s, d.TrunkComponent.transform.position);
                    break;
                case TimberWalkShape w:
                    var control = w.WorldControl();
                    if (control.Count > 0) Trunks(s, w, control.Average(c => c.y));
                    foreach (var p in control) V(s, p);
                    s.Append(w.Width.ToString("F2", CultureInfo.InvariantCulture)).Append(w.RailLeft).Append(w.RailRight).Append(w.OnGround).Append(w.Support).Append(w.Wear)
                     .Append(w.MaxPost.ToString("F2", CultureInfo.InvariantCulture));
                    break;
                case RopeBridgeShape b:
                    var (a, e) = b.WorldEnds();
                    V(s, a);
                    V(s, e);
                    s.Append(b.Width.ToString("F2", CultureInfo.InvariantCulture)).Append(b.Sag.ToString("F2", CultureInfo.InvariantCulture)).Append(b.SideWalls).Append(b.Wear);
                    break;
                case RailingShape r:
                    foreach (var p in r.Points) V(s, p);
                    s.Append(r.Closed);
                    break;
                case HouseShape h:
                    V(s, new Vector3(h.Size.x, h.WallHeight, h.Size.y));
                    s.Append(h.Floor).Append(h.RoofRise.ToString("F2", CultureInfo.InvariantCulture)).Append(h.Ceiling).Append(h.Wear).Append('|');
                    foreach (var door in h.Doors) s.Append(door.wall).Append(':').Append(JsonUtility.ToJson(door)).Append(';');
                    break;
                case TrestleShape tr:
                    V(s, tr.Half);
                    break;
                case ShelterShape sh:
                    V(s, new Vector3(sh.Size.x, sh.Height, sh.Size.y));
                    V(s, new Vector3(sh.RoofRise, sh.LowWalls, sh.Drum ? 1f : 0f));
                    s.Append(sh.Roof).Append(sh.Wear).Append('|');
                    break;
                case GantryShape g:
                    V(s, new Vector3(g.Span, g.Height, g.Depth));
                    V(s, new Vector3(g.Pulley ? 1f : 0f, g.RopeDrop, g.Roof ? 1f : 0f));
                    break;
                case SignShape sg:
                    V(s, new Vector3(sg.Board.x, sg.BoardHeight, sg.Board.y));
                    s.Append(sg.Posts).Append(sg.Glyph).Append(sg.Roof).Append('|');
                    break;
                case PulleyBeamShape pb:
                    V(s, new Vector3(pb.Reach, pb.Height, pb.RopeDrop));
                    s.Append(pb.Post).Append('|');
                    break;
                case HeroTreeShape tree:
                    s.Append(tree.TreeKind).Append('|');
                    V(s, new Vector3(tree.Height, tree.BaseRadius, tree.TopRadius));
                    V(s, new Vector3(tree.Lean.x, tree.CrownStart, tree.Lean.y));
                    s.Append(tree.CrownLimbs).Append('|');
                    foreach (var limb in tree.Limbs) s.Append(JsonUtility.ToJson(limb)).Append(';');
                    foreach (var band in LevelNature.SawnBands(tree)) V(s, band);
                    break;
                case WaterShape water:
                    V(s, new Vector3(water.Size.x, water.Depth, water.Size.y));
                    break;
                case ForestArea forest:
                    foreach (var p in forest.Outline) V(s, p);
                    V(s, new Vector3(forest.Giants, forest.Understory, forest.GroundCover));
                    V(s, new Vector2(forest.Clearings, forest.PathClearance));
                    // what it keeps off: every other piece near its area (a deck moved into it replants it)
                    var area = forest.WorldOutline();
                    if (area.Count > 0)
                    {
                        var rect = Rect.MinMaxRect(area.Min(p => p.x) - ForestReach, area.Min(p => p.y) - ForestReach, area.Max(p => p.x) + ForestReach, area.Max(p => p.y) + ForestReach);
                        foreach (var other in TimberLinks.Pieces(forest.gameObject.scene).Where(o => !(o is ForestArea) && rect.Contains(new Vector2(o.transform.position.x, o.transform.position.z))).OrderBy(o => o.PieceId))
                            s.Append(TimberKit.Seed(Signature(other), 0)).Append(',');
                    }
                    break;
            }
            return s.ToString();
        }

        /// <summary>The trunks within <see cref="TrunkReach"/> of a piece at height <paramref name="y"/> (where they stand and how thick).</summary>
        static void Trunks(StringBuilder s, LevelPiece piece, float y)
        {
            var at = piece.transform.position;
            foreach (var source in SceneSite.TrunkSources(piece.gameObject.scene))
            {
                if (!source.TryTrunkAt(y, out var center, out float radius)) continue;
                if (new Vector2(center.x - at.x, center.z - at.z).magnitude > TrunkReach + 30f) continue;
                V(s, new Vector3(center.x, radius, center.z));
            }
            s.Append('|');
        }

        static void V(StringBuilder s, Vector3 v) =>
            s.Append(v.x.ToString("F3", CultureInfo.InvariantCulture)).Append(',').Append(v.y.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
             .Append(v.z.ToString("F3", CultureInfo.InvariantCulture)).Append(';');
    }
}
