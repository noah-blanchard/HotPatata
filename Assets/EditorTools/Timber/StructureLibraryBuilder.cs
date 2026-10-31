using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// Fills the structure library (ARCHITECTURE §27.1, docs/LEVEL_AUTHORING.md §19) with its first structures: each drawn from a piece
    /// (<see cref="ShelterShape"/>, <see cref="GantryShape"/>, <see cref="SignShape"/>, <see cref="PulleyBeamShape"/>) in a scene of its
    /// own, frozen, and saved as a prefab in <see cref="StructureLibrary.Folder"/>. Only the missing ones are made: a library prefab
    /// belongs to the author once it exists. Regenerate one explicitly (Assets/HotPatata/Regenerate Library Structure) to start it over.
    /// Menu HotPatata/Level/Build Structure Library.
    /// </summary>
    public static class StructureLibraryBuilder
    {
        /// <summary>One structure of the library: its folder, its name and the piece it is drawn from.</summary>
        public sealed class Entry
        {
            public string category;
            public string name;
            public Type type;
            public Action<LevelPiece> setup;

            public string Path => StructureLibrary.Folder + category + "/" + name + ".prefab";
        }

        public static readonly Entry[] Entries =
        {
            Make<ShelterShape>("Shelters", "Winch Booth", s => { s.Size = new Vector2(3.8f, 3.2f); s.Height = 2.7f; s.Roof = ShelterRoof.Gable; s.RoofRise = 0.9f; s.Drum = true; }),
            Make<ShelterShape>("Shelters", "Woodshed", s => { s.Size = new Vector2(4.4f, 2.2f); s.Height = 2.3f; s.Roof = ShelterRoof.Lean; s.RoofRise = 0.6f; }),
            Make<ShelterShape>("Shelters", "Lean-to", s => { s.Size = new Vector2(3f, 2f); s.Height = 2.2f; s.Roof = ShelterRoof.Lean; s.RoofRise = 0.5f; s.LowWalls = 1f; }),
            Make<GantryShape>("Gantries", "Gate Tower", g => { g.Span = 5f; g.Depth = 1.5f; g.Height = 9.6f; g.Pulley = true; g.RopeDrop = 4.7f; g.Roof = true; }),
            Make<GantryShape>("Gantries", "Lift Gantry", g => { g.Span = 3f; g.Depth = 1.2f; g.Height = 6f; g.Pulley = true; g.RopeDrop = 2.5f; g.Roof = false; }),
            Make<SignShape>("Signs", "Clue Board Owl", s => { s.Glyph = SignGlyph.Owl; }),
            Make<SignShape>("Signs", "Clue Board Leaf", s => { s.Glyph = SignGlyph.Leaf; }),
            Make<SignShape>("Signs", "Clue Board Axe", s => { s.Glyph = SignGlyph.Axe; }),
            Make<SignShape>("Signs", "Signpost", s => { s.Posts = 1; s.Board = new Vector2(0.9f, 0.5f); s.BoardHeight = 1.6f; s.Glyph = SignGlyph.None; s.Roof = false; }),
            Make<PulleyBeamShape>("Pulleys", "Pulley Beam", p => { p.Post = false; p.Reach = 3.5f; p.Height = 5f; }),
            Make<PulleyBeamShape>("Pulleys", "Pulley Post", p => { p.Post = true; p.Reach = 2.5f; p.Height = 4f; }),
            // the two lanes' markers (PROJECT_SPEC §15g) and the old mine (ARCHITECTURE §27.2)
            Make<LaneMarkerShape>("Lanes", "Sun Post", m => { m.Lane = Lane.Sun; m.Form = LaneMarkerForm.Post; }),
            Make<LaneMarkerShape>("Lanes", "Moon Post", m => { m.Lane = Lane.Moon; m.Form = LaneMarkerForm.Post; }),
            Make<LaneMarkerShape>("Lanes", "Sun Arch", m => { m.Lane = Lane.Sun; m.Form = LaneMarkerForm.Arch; }),
            Make<LaneMarkerShape>("Lanes", "Moon Arch", m => { m.Lane = Lane.Moon; m.Form = LaneMarkerForm.Arch; }),
            Make<MineFrameShape>("Mine", "Mine Entrance", f => { f.Points.Clear(); f.Points.AddRange(new[] { Vector3.zero, new Vector3(0f, 0f, 3f) }); f.Spacing = 3f; f.LanternEvery = 2; }),
            Make<MineFrameShape>("Mine", "Mine Gallery 9 m", f => { f.Points.Clear(); f.Points.AddRange(new[] { Vector3.zero, new Vector3(0f, 0f, 9f) }); }),
            Make<MineRailShape>("Mine", "Mine Track 8 m", r => { r.Points.Clear(); r.Points.AddRange(new[] { Vector3.zero, new Vector3(0f, 0f, 8f) }); }),
            // masonry (M21, ARCHITECTURE §27.7): walls freeze into stones and timber; the stone pieces are baked as they are built
            Make<WallShape>("Walls", "Dry-stone Run", w => { WallEntries.Dress(w, WallStyle.DryStone); w.Points.Clear(); w.Points.AddRange(new[] { new Vector3(-4f, 0f, 0f), new Vector3(4f, 0f, 0f) }); }),
            Make<WallShape>("Walls", "Palisade Gate-wall", w =>
            {
                WallEntries.Dress(w, WallStyle.Palisade);
                w.Points.Clear();
                w.Points.AddRange(new[] { new Vector3(-6f, 0f, 0f), new Vector3(6f, 0f, 0f) });
                // its gate is left open: the kit door is placed in the level (a wall opening's passage, or the palette's Door)
                w.Openings.Add(new WallOpening(6f, 3.4f, 3f, OpeningPassage.Free) { frame = OpeningFrame.Timber });
            }),
            Make<WallShape>("Walls", "Ruin Corner", w =>
            {
                WallEntries.Dress(w, WallStyle.Ruined);
                w.Points.Clear();
                w.Points.AddRange(new[] { new Vector3(-3.5f, 0f, 0f), Vector3.zero, new Vector3(0f, 0f, 3f) });
                w.Height = 3.6f;
                w.Crest = 2.6f;
                w.Breaches.Clear();
            }),
            Make<ArchShape>("Arches", "Stone Arch", a => { a.Kind = ArchKind.StoneRound; }),
            Make<ArchShape>("Arches", "Pointed Arch", a => { a.Kind = ArchKind.StonePointed; a.Rise = 2.2f; }),
            Make<ArchShape>("Arches", "Log Portal", a => { a.Kind = ArchKind.LogPortal; }),
            Make<StoneStairShape>("Stone", "Stone Stair", s => { s.Kind = StoneStairKind.Built; }),
            Make<PavingShape>("Stone", "Terrace", p => { p.Kind = PavingKind.Slabs; p.Raised = 1.5f; }),
            Make<StoneBridgeShape>("Stone", "Stone Bridge", b => { b.Start = new Vector3(0f, 2f, -9f); b.End = new Vector3(0f, 2f, 9f); }),
            Make<TowerShape>("Towers", "Watchtower", t => { t.Openings.Add(new WallOpening(2f, 2.2f, 2.8f, OpeningPassage.Free) { shape = OpeningShape.RoundArch, frame = OpeningFrame.Stone }); }),
            Make<TowerShape>("Towers", "Ruined Tower", t =>
            {
                t.Top = TowerTop.Ruined;
                t.WallStyle = WallStyle.DryStone;
                t.Floors.Clear();
                t.Openings.Add(new WallOpening(2f, 2.2f, 2.8f, OpeningPassage.Free) { shape = OpeningShape.Breach });
            }),
        };

        static Entry Make<T>(string category, string name, Action<T> setup) where T : LevelPiece =>
            new Entry { category = category, name = name, type = typeof(T), setup = p => setup((T)p) };

        [MenuItem("HotPatata/Libraries/Build Structure Library", priority = 1)]
        static void BuildMenu()
        {
            var made = Build(Entries.Where(e => AssetDatabase.LoadAssetAtPath<GameObject>(e.Path) == null));
            Debug.Log(made.Count == 0 ? "[Timber] the structure library is complete: nothing to make"
                                      : $"[Timber] structure library: {made.Count} made ({string.Join(", ", made)})");
        }

        [MenuItem("Assets/HotPatata/Regenerate Library Structure")]
        static void RegenerateMenu()
        {
            var entry = EntryOf(Selection.activeObject);
            if (entry == null) return;
            if (!EditorUtility.DisplayDialog("Regenerate library structure",
                    $"{entry.name} is drawn again from its piece: every change made to the prefab by hand is lost.", "Regenerate", "Cancel")) return;
            Build(new[] { entry });
        }

        [MenuItem("Assets/HotPatata/Regenerate Library Structure", true)]
        static bool RegenerateValidate() => EntryOf(Selection.activeObject) != null;

        static Entry EntryOf(Object asset)
        {
            string path = asset != null ? AssetDatabase.GetAssetPath(asset) : null;
            return string.IsNullOrEmpty(path) ? null : Entries.FirstOrDefault(e => e.Path == path);
        }

        /// <summary>
        /// A piece that does not freeze (a stone piece) as it was built: its output's objects under a root of its name, every mesh in the
        /// catalogue, the piece gone (M21).
        /// </summary>
        static GameObject Bake(LevelPiece piece)
        {
            if (piece.Output == null) return null;
            var root = new GameObject(piece.name);
            root.transform.SetPositionAndRotation(piece.transform.position, Quaternion.Euler(0f, piece.transform.eulerAngles.y, 0f));
            foreach (var filter in piece.Output.GetComponentsInChildren<MeshFilter>(true)) filter.sharedMesh = MeshCatalog.Store(filter.sharedMesh);
            foreach (var collider in piece.Output.GetComponentsInChildren<MeshCollider>(true)) collider.sharedMesh = MeshCatalog.Store(collider.sharedMesh);
            foreach (Transform child in piece.Output.transform.Cast<Transform>().ToList()) child.SetParent(root.transform, true);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.None;
            Object.DestroyImmediate(piece.gameObject);
            return root;
        }

        /// <summary>Draws, freezes and saves <paramref name="entries"/> (overwriting); returns the names made.</summary>
        public static List<string> Build(IEnumerable<Entry> entries)
        {
            var list = entries.ToList();
            var made = new List<string>();
            if (list.Count == 0) return made;
            var active = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            bool pieces = LevelPieceWatcher.Enabled, elements = TimberElementWatcher.Enabled;
            LevelPieceWatcher.Enabled = false;
            TimberElementWatcher.Enabled = false;
            try
            {
                SceneManager.SetActiveScene(scene);
                foreach (var entry in list)
                {
                    var go = new GameObject(entry.name);
                    var piece = (LevelPiece)go.AddComponent(entry.type);
                    entry.setup(piece);
                    piece.EnsureId();
                    LevelPieceBuilder.Rebuild(piece);
                    var frozen = TimberFreezer.CanFreeze(piece) ? TimberFreezer.Freeze(piece)?.gameObject : Bake(piece);
                    if (frozen == null) continue;
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(entry.Path));
                    MeshCatalog.Flush();
                    PrefabUtility.SaveAsPrefabAsset(frozen, entry.Path, out bool saved);
                    if (saved) made.Add(entry.name);
                    Object.DestroyImmediate(frozen);
                }
            }
            finally
            {
                if (active.IsValid()) SceneManager.SetActiveScene(active);
                EditorSceneManager.CloseScene(scene, true);
                LevelPieceWatcher.Enabled = pieces;
                TimberElementWatcher.Enabled = elements;
            }
            AssetDatabase.SaveAssets();
            return made;
        }
    }
}
