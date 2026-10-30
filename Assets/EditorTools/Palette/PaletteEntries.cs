using System;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// The level's placeable things made from code (ARCHITECTURE §27.3, docs/LEVEL_AUTHORING.md §25): pieces, nature, the course, air and
    /// elements. Each is a palette tile and a GameObject/HotPatata Level menu item (written by <see cref="PaletteMenuWriter"/>). The kit is in
    /// <see cref="PaletteKitEntries"/>.
    /// </summary>
    public static class PaletteEntries
    {
        const PaletteSnap Line = PaletteSnap.Ground | PaletteSnap.Floor | PaletteSnap.DeckEdge;

        // ------------------------------------------------------------------ ground

        [PaletteItem("ground.water", "Ground/Water", "Water", Piece = typeof(WaterShape), Tags = "lake pond pool kill", Tooltip = "A water surface with its kill zone under it.")]
        static GameObject Water(PlaceContext c) => Piece<WaterShape>(c, "Water", 0.5f, LevelGroup.Props);

        [PaletteItem("ground.valley-walls", "Ground/Tools", "Valley Walls", Kind = PaletteKind.Tool, Icon = "TerrainInspector.TerrainToolRaise",
                     Tooltip = "Raises the terrain's edge into valley walls (30 m wide, 35 m high: the Level inspector sets other sizes). Ctrl+Z undoes it.")]
        static GameObject ValleyWalls(PlaceContext c)
        {
            var terrain = LevelTerrain.Terrains(c.Scene).FirstOrDefault();
            if (terrain == null)
            {
                Debug.LogWarning("[Palette] the level has no terrain.");
                return null;
            }
            int n = LevelTerrain.RaiseBorder(terrain, 30f, 35f);
            Debug.Log($"[Level] valley walls: {n} ground samples raised (Ctrl+Z undoes it). Rebuild All Pieces if timber stands near them.");
            return null;
        }

        // ------------------------------------------------------------------ rock

        [PaletteItem("rock.cliff", "Rock/Cliffs", "Cliff", Piece = typeof(CliffShape), Tags = "rock face wall granite sandstone mossy lock")]
        static GameObject Cliff(PlaceContext c) => Piece<CliffShape>(c, "Cliff", -0.5f, LevelGroup.Props);

        [PaletteItem("rock.tunnel", "Rock/Caves", "Cave Tunnel", Piece = typeof(RockShellShape), Tags = "cave tunnel rock")]
        static GameObject Tunnel(PlaceContext c) => Piece<RockShellShape>(c, "Cave tunnel", 0f, LevelGroup.Props);

        [PaletteItem("rock.cavern", "Rock/Caves", "Cavern", Piece = typeof(RockShellShape), Tags = "cave dome chamber rock")]
        static GameObject Cavern(PlaceContext c)
        {
            var piece = (RockShellShape)PaletteCatalog.NewPiece(c, typeof(RockShellShape), "Cavern", c.Ground(0f), LevelGroup.Props);
            Undo.RecordObject(piece, "Cavern");
            piece.Kind = RockShellKind.Cavern;
            piece.Height = 10f;
            piece.Points.Clear();
            piece.Points.AddRange(DeckShapes.Oval(24f, 20f, 16));
            LevelPieceBuilder.Rebuild(piece);
            return piece.gameObject;
        }

        // ------------------------------------------------------------------ nature

        [PaletteItem("nature.giant-tree", "Nature/Trees", "Giant Tree", Piece = typeof(HeroTreeShape), Tags = "tree trunk oak pine dead")]
        static GameObject GiantTree(PlaceContext c) => Piece<HeroTreeShape>(c, "Giant tree", -0.4f, LevelGroup.Trees);

        [PaletteItem("nature.forest", "Nature/Trees", "Forest Area", Piece = typeof(ForestArea), Icon = "TerrainInspector.TerrainToolTrees", Tags = "forest trees plants planting")]
        static GameObject Forest(PlaceContext c) => Piece<ForestArea>(c, "Forest", 0f, LevelGroup.Trees);

        [PaletteItem("nature.forest-walked", "Nature/Trees", "Forest Area (Walked)", Piece = typeof(ForestArea), Icon = "TerrainInspector.TerrainToolPlants", Tags = "forest clearing trail ferns grass walked",
                     Tooltip = "A forest the team walks across: only what a player walks through grows (ferns, grass, moss, low bushes).")]
        static GameObject ForestWalked(PlaceContext c)
        {
            var go = PaletteCatalog.NewObject(c, "Forest (walked)", c.Ground(0f), LevelGroup.Trees);
            var forest = Undo.AddComponent<ForestArea>(go);
            forest.Walked = true;
            forest.EnsureId();
            LevelPieceBuilder.Rebuild(forest);
            return go;
        }

        [PaletteItem("nature.mist-zone", "Nature/Air", "Mist Zone", Kind = PaletteKind.Air, Icon = "d_WindZone Icon", Tags = "mist fog cave", Also = "Air")]
        static GameObject MistZone(PlaceContext c)
        {
            var go = PaletteCatalog.NewObject(c, "Mist zone", c.Ground(0f) + Vector3.up * 2f, LevelGroup.Props);
            Undo.AddComponent<MistZone>(go);
            return go;
        }

        // ------------------------------------------------------------------ timber

        [PaletteItem("timber.deck", "Timber/Timber", "Deck", Piece = typeof(TimberDeckShape), Tags = "platform floor boards")]
        static GameObject Deck(PlaceContext c) => Piece<TimberDeckShape>(c, "Deck", 3f);

        [PaletteItem("timber.walkway", "Timber/Timber", "Walkway or Stair", Piece = typeof(TimberWalkShape), Snap = Line, Tags = "walk path stair steps boardwalk")]
        static GameObject Walkway(PlaceContext c)
        {
            var walk = (TimberWalkShape)PaletteCatalog.NewPiece(c, typeof(TimberWalkShape), "Walkway", Start(c), LevelGroup.Timber);
            if (c.Snapped.Kind == PaletteSnap.DeckEdge && c.Snapped.Attach.Attached)
            {
                Undo.RecordObject(walk, "Attach walkway");
                Face(walk.transform, c.Snapped.Direction);
                walk.Start = c.Snapped.Attach;
                LevelPieceBuilder.Refresh(TimberLinks.Pieces(c.Scene));
            }
            return walk.gameObject;
        }

        [PaletteItem("timber.rope-bridge", "Timber/Timber", "Rope Bridge", Piece = typeof(RopeBridgeShape), Snap = Line, Tags = "bridge rope span gap")]
        static GameObject RopeBridge(PlaceContext c)
        {
            var bridge = (RopeBridgeShape)PaletteCatalog.NewPiece(c, typeof(RopeBridgeShape), "Rope bridge", c.Snapped.Kind == PaletteSnap.DeckEdge ? Start(c) : c.Ground(3f),
                                                                 LevelGroup.Timber);
            if (c.Snapped.Kind == PaletteSnap.DeckEdge && c.Snapped.Attach.Attached)
            {
                Undo.RecordObject(bridge, "Attach bridge");
                Face(bridge.transform, c.Snapped.Direction);
                bridge.StartAttach = c.Snapped.Attach;
                LevelPieceBuilder.Refresh(TimberLinks.Pieces(c.Scene));
            }
            return bridge.gameObject;
        }

        [PaletteItem("timber.railing", "Timber/Timber", "Railing", Piece = typeof(RailingShape), Snap = Line, Tags = "rail fence handrail")]
        static GameObject Railing(PlaceContext c)
        {
            var railing = (RailingShape)PaletteCatalog.NewPiece(c, typeof(RailingShape), "Railing", Start(c), LevelGroup.Timber);
            if (c.Snapped.Found && c.Snapped.Direction.sqrMagnitude > 0f)
            {
                Undo.RecordObject(railing.transform, "Railing");
                Face(railing.transform, Vector3.Cross(Vector3.up, c.Snapped.Direction));
                LevelPieceBuilder.Rebuild(railing);
            }
            return railing.gameObject;
        }

        [PaletteItem("timber.house", "Timber/Timber", "House", Piece = typeof(HouseShape), Tags = "house hut cabin walls roof building")]
        static GameObject House(PlaceContext c) => Piece<HouseShape>(c, "House", 0f);

        [PaletteItem("timber.trestle", "Timber/Timber", "Trestle Tower", Piece = typeof(TrestleShape), Tags = "tower trestle support")]
        static GameObject Trestle(PlaceContext c) => Piece<TrestleShape>(c, "Trestle", 6f);

        [PaletteItem("timber.mine-frames", "Timber/Mine", "Mine Frames", Piece = typeof(MineFrameShape), Tags = "mine frames gallery tunnel timber")]
        static GameObject MineFrames(PlaceContext c) => Piece<MineFrameShape>(c, "Mine frames", 0f);

        [PaletteItem("timber.mine-track", "Timber/Mine", "Mine Track", Piece = typeof(MineRailShape), Tags = "mine rails track")]
        static GameObject MineTrack(PlaceContext c) => Piece<MineRailShape>(c, "Mine track", 0f);

        // ------------------------------------------------------------------ structures

        [PaletteItem("structure.shelter", "Structures/Structures", "Shelter", Piece = typeof(ShelterShape), Tags = "shelter roof hut")]
        static GameObject Shelter(PlaceContext c) => Piece<ShelterShape>(c, "Shelter", 0f);

        [PaletteItem("structure.gantry", "Structures/Structures", "Gantry", Piece = typeof(GantryShape), Tags = "gantry frame pulley")]
        static GameObject Gantry(PlaceContext c) => Piece<GantryShape>(c, "Gantry", 0f);

        [PaletteItem("structure.sign", "Structures/Structures", "Sign", Piece = typeof(SignShape), Tags = "sign board glyph clue")]
        static GameObject Sign(PlaceContext c) => Piece<SignShape>(c, "Sign", 0f);

        [PaletteItem("structure.pulley-beam", "Structures/Structures", "Pulley Beam", Piece = typeof(PulleyBeamShape), Tags = "pulley beam rope")]
        static GameObject PulleyBeam(PlaceContext c) => Piece<PulleyBeamShape>(c, "Pulley beam", 0f);

        [PaletteItem("structure.lane-marker", "Structures/Structures", "Lane Marker", Piece = typeof(LaneMarkerShape), Tags = "lane sun moon marker")]
        static GameObject LaneMarker(PlaceContext c) => Piece<LaneMarkerShape>(c, "Lane marker", 0f);

        // ------------------------------------------------------------------ elements (ARCHITECTURE §27.1)

        [PaletteItem("element.plank", "Elements/Timber", "Plank", Kind = PaletteKind.Element, Tags = "element board")]
        static GameObject Plank(PlaceContext c) =>
            Board(c, "Plank", TimberElementKind.Plank, TimberWood.Plank, new Vector3(0.2f, 0.05f, 2f), Quaternion.identity, 0.05f, false);

        [PaletteItem("element.post", "Elements/Timber", "Post", Kind = PaletteKind.Element, Tags = "element post")]
        static GameObject Post(PlaceContext c) =>
            Board(c, "Post", TimberElementKind.Post, TimberWood.Timber, new Vector3(0.2f, 0.2f, 2.5f), Quaternion.Euler(-90f, 0f, 0f), 1.25f, true);

        [PaletteItem("element.beam", "Elements/Timber", "Beam", Kind = PaletteKind.Element, Tags = "element beam")]
        static GameObject Beam(PlaceContext c) =>
            Board(c, "Beam", TimberElementKind.Beam, TimberWood.Timber, new Vector3(0.16f, 0.2f, 3f), Quaternion.identity, 2f, false);

        [PaletteItem("element.rope", "Elements/Timber", "Rope", Kind = PaletteKind.Element, Tags = "element rope")]
        static GameObject Rope(PlaceContext c)
        {
            var go = NewElement(c, "Rope", 2f);
            var rope = Undo.AddComponent<TimberPath>(go);
            rope.Kind = TimberElementKind.Rope;
            rope.Wood = TimberWood.Rope;
            rope.Shape = TimberPath.Profile.Round;
            rope.Radius = 0.028f;
            rope.Sag = 0.2f;
            rope.Points.Clear();
            rope.Points.AddRange(new[] { Vector3.zero, Vector3.forward * 3f });
            Done(rope);
            return go;
        }

        [PaletteItem("element.iron-strap", "Elements/Timber", "Iron Strap", Kind = PaletteKind.Element, Tags = "element iron strap metal")]
        static GameObject IronStrap(PlaceContext c) =>
            Board(c, "Iron strap", TimberElementKind.Iron, TimberWood.Iron, new Vector3(0.06f, 0.01f, 0.4f), Quaternion.identity, 1f, false);

        // ------------------------------------------------------------------ course

        [PaletteItem("course.checkpoint", "Course/Course", "Checkpoint (campfire)", Kind = PaletteKind.Course, Snap = PaletteSnap.Floor, Tags = "checkpoint campfire respawn",
                     Prefab = CourseKit.GameplayDir + "Campfire.prefab")]
        static GameObject Checkpoint(PlaceContext c) => LevelCourse.AddCheckpoint(c.Scene, c.Group(LevelGroup.Kit), c.Floor()).gameObject;

        [PaletteItem("course.finish", "Course/Course", "Finish", Kind = PaletteKind.Course, Snap = PaletteSnap.Floor, Tags = "finish end goal", Icon = "d_FilterSelectedOnly")]
        static GameObject Finish(PlaceContext c) => LevelCourse.AddFinish(c.Group(LevelGroup.Kit), c.Floor()).gameObject;

        [PaletteItem("course.kill-zone", "Course/Course", "Kill Zone", Kind = PaletteKind.Course, Snap = PaletteSnap.Floor, Tags = "kill zone fall death",
                     Icon = "d_console.erroricon")]
        static GameObject KillZone(PlaceContext c) => PaletteKitEntries.Place(c, CourseKit.GameplayDir + "KillZone", "Kill zone");

        // ------------------------------------------------------------------ air

        [PaletteItem("air.sound-source", "Air/Air", "Sound Source", Kind = PaletteKind.Air, Icon = "d_AudioSource Icon", Tags = "sound river waterfall wind audio")]
        static GameObject SoundSource(PlaceContext c)
        {
            var parent = c.Group(LevelGroup.Props);
            var at = c.Ground(1f);
            var audio = NatureKit.Ambient(parent, "Sound source", parent != null ? parent.InverseTransformPoint(at) : at, NatureAudioFactory.Ambience.River, 0.8f, 40f);
            audio.GetComponent<AmbientLoop>().Configure("");
            Undo.RegisterCreatedObjectUndo(audio.gameObject, "Add sound source");
            return audio.gameObject;
        }

        [PaletteItem("air.ambience-zone", "Air/Air", "Ambience Zone", Kind = PaletteKind.Air, Icon = "d_AudioReverbZone Icon", Tags = "ambience sound cave zone")]
        static GameObject AmbienceZone(PlaceContext c)
        {
            var go = PaletteCatalog.NewObject(c, "Ambience zone", c.Ground(4f), LevelGroup.Props);
            Undo.AddComponent<AmbienceZone>(go).Configure("Cave", new Vector3(20f, 10f, 20f), 1);
            return go;
        }

        // ------------------------------------------------------------------ helpers

        static GameObject Piece<T>(PlaceContext c, string name, float above, LevelGroup group = LevelGroup.Timber) where T : LevelPiece =>
            PaletteCatalog.NewPiece(c, typeof(T), name, c.Ground(above), group).gameObject;

        /// <summary>Where a line piece starts: on the snapped edge or end, else on the ground.</summary>
        static Vector3 Start(PlaceContext c) => c.Snapped.Found ? c.Snapped.Point : c.Ground(0f);

        /// <summary>Turns <paramref name="t"/> so its forward (a line piece's run) points along <paramref name="direction"/> on the flat.</summary>
        static void Face(Transform t, Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) return;
            Undo.RecordObject(t, "Face");
            t.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        static GameObject Board(PlaceContext c, string name, TimberElementKind kind, TimberWood wood, Vector3 size, Quaternion rotation, float above, bool solid)
        {
            var go = NewElement(c, name, above);
            go.transform.rotation = rotation;
            var board = Undo.AddComponent<TimberBoard>(go);
            board.Kind = kind;
            board.Wood = wood;
            board.Size = size;
            board.Solid = solid;
            board.Grain = Random.Range(0, TimberElement.GrainVariants);
            // a plank laid in a structure is part of its walking surface
            if (kind == TimberElementKind.Plank && board.Structure != null) board.Walk = TimberWalk.Slab;
            Done(board);
            return go;
        }

        /// <summary>
        /// A new element: made while an element is selected, it goes beside it (in its structure: a plank there is walkable); else into the
        /// level's timber group.
        /// </summary>
        static GameObject NewElement(PlaceContext c, string name, float above)
        {
            var near = Selection.activeTransform;
            Transform parent;
            if (near != null && (near.GetComponent<TimberElement>() != null || near.GetComponentInParent<FrozenStructure>() != null))
                parent = near.GetComponent<TimberElement>() != null ? near.parent : near;
            else parent = c.Group(LevelGroup.Timber);
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Add " + name);
            if (parent != null) Undo.SetTransformParent(go.transform, parent, "Add " + name);
            go.transform.position = c.Ground(above);
            return go;
        }

        static void Done(TimberElement element)
        {
            TimberElementBuilder.Refresh(element, true);
            MeshCatalog.Flush();
            EditorUtility.SetDirty(element);
        }
    }
}
