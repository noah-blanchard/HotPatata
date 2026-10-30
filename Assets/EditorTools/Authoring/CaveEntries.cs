using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// Caves v2 in the palette (ARCHITECTURE §27.6, docs/LEVEL_AUTHORING.md §29): a tunnel that changes along its way, a shaped chamber, a
    /// shaft, a cave network of two chambers and a tunnel, a cave mouth on a hillside, and the Mine Frames stair (the way up a shaft).
    /// </summary>
    public static class CaveEntries
    {
        [PaletteLook("rock.tunnel", "varied", "Varied", Tooltip = "Round, then a tall rift, then a low gallery; dressed")]
        static GameObject VariedTunnel(PlaceContext c)
        {
            var tunnel = (RockShellShape)PaletteCatalog.NewPiece(c, typeof(RockShellShape), "Cave tunnel", c.Ground(0f), LevelGroup.Props);
            Undo.RecordObject(tunnel, "Cave tunnel");
            tunnel.Points.Clear();
            tunnel.Points.AddRange(new[] { Vector3.zero, new Vector3(1f, 0f, 12f), new Vector3(-1f, 0.5f, 24f), new Vector3(0f, 0.5f, 36f) });
            Varied(tunnel);
            LevelPieceBuilder.Rebuild(tunnel);
            return tunnel.gameObject;
        }

        /// <summary>Round, a tall rift, a low wide gallery, round again: one section per point; dressed.</summary>
        public static void Varied(RockShellShape tunnel)
        {
            tunnel.Sections.Clear();
            var sections = new[]
            {
                new CaveSection(8f, 6f, CaveProfile.Round), new CaveSection(3f, 7f, CaveProfile.Rift, 8f),
                new CaveSection(10f, 3f, CaveProfile.Gallery), new CaveSection(6f, 5f, CaveProfile.Arch),
            };
            for (int i = 0; i < tunnel.Points.Count; i++) tunnel.Sections.Add(sections[Mathf.Min(i, sections.Length - 1)]);
            tunnel.Dressing = new CaveDressing { stalactites = 0.6f, stalagmites = 0.4f, puddles = 0.4f, props = 0.4f, lanternSpacing = 12f };
        }

        [PaletteLook("rock.cavern", "shaped", "Shaped", Tooltip = "An offset crown over walls, two alcoves, a pillar and a shelf; dressed")]
        static GameObject ShapedCavern(PlaceContext c)
        {
            var piece = (RockShellShape)PaletteCatalog.NewPiece(c, typeof(RockShellShape), "Cavern", c.Ground(0f), LevelGroup.Props);
            Undo.RecordObject(piece, "Cavern");
            Shape(piece);
            LevelPieceBuilder.Rebuild(piece);
            return piece.gameObject;
        }

        /// <summary>A chamber with an offset crown over 5 m walls, two alcoves, a pillar and a shelf 3 m up; dressed.</summary>
        public static void Shape(RockShellShape piece)
        {
            piece.Kind = RockShellKind.Cavern;
            piece.Height = 5f;
            piece.ApexHeight = 12f;
            piece.Apex = new Vector2(3f, -2f);
            piece.Points.Clear();
            piece.Points.AddRange(DeckShapes.Oval(30f, 24f, 18));
            piece.Alcoves.Clear();
            piece.Alcoves.Add(new CaveAlcove { along = 0.15f, width = 8f, depth = 3.5f });
            piece.Alcoves.Add(new CaveAlcove { along = 0.6f, width = 6f, depth = 2.5f });
            piece.Pillars.Clear();
            piece.Pillars.Add(new CavePillar { at = new Vector2(-5f, 3f), radius = 1.4f, waist = 0.6f });
            piece.Floors.Clear();
            piece.Floors.Add(new CaveFloor
            {
                outline = new[] { new Vector2(6f, 2f), new Vector2(14.5f, 2f), new Vector2(14.5f, 9f), new Vector2(6f, 9f) }.ToList(),
                height = 3f, thickness = 0.8f,
            });
            piece.Dressing = new CaveDressing { stalactites = 0.5f, stalagmites = 0.4f, columns = 0.5f, puddles = 0.3f, props = 0.3f, lanternSpacing = 12f };
        }

        [PaletteItem("rock.shaft", "Rock/Caves", "Shaft", Piece = typeof(RockShellShape), Tags = "shaft well pit vertical cave rock",
                     Tooltip = "Straight walls up from an outline, open at the top; the way up is a Mine Frames stair, a walkway or a lift (never a ladder).")]
        static GameObject Shaft(PlaceContext c)
        {
            var piece = (RockShellShape)PaletteCatalog.NewPiece(c, typeof(RockShellShape), "Shaft", c.Ground(0f), LevelGroup.Props);
            Undo.RecordObject(piece, "Shaft");
            piece.Kind = RockShellKind.Shaft;
            piece.Height = 20f;
            piece.Points.Clear();
            piece.Points.AddRange(DeckShapes.Oval(12f, 12f, 14));
            piece.Ledges.Add(new ShaftLedge { height = 10f, inset = 2f, from = 0.1f, to = 0.35f });
            LevelPieceBuilder.Rebuild(piece);
            return piece.gameObject;
        }

        [PaletteItem("rock.cave-network", "Rock/Caves", "Cave Network", Piece = typeof(CaveNetwork), Icon = "d_UnityEditor.Graphs.AnimatorControllerTool", Tags = "cave network chambers tunnels",
                     Tooltip = "Chambers linked by tunnels whose ends follow them: two chambers and a tunnel to start.")]
        static GameObject Network(PlaceContext c)
        {
            var network = (CaveNetwork)PaletteCatalog.NewPiece(c, typeof(CaveNetwork), "Cave network", c.Ground(0f), LevelGroup.Props);
            var a = CaveNetworks.AddNode(network, network.transform.position);
            var b = CaveNetworks.AddNode(network, network.transform.position + network.transform.forward * 40f);
            CaveNetworks.Link(network, a, b);
            Selection.activeGameObject = network.gameObject;
            return network.gameObject;
        }

        [PaletteItem("rock.cave-mouth", "Rock/Caves", "Cave Mouth", Icon = "d_SceneViewCamera", Tags = "cave mouth entrance hillside tunnel",
                     Tooltip = "Click a hillside: a tunnel into the hill, the rock over its mouth (scree, talus), the cave's ambience and two lanterns.")]
        static GameObject Mouth(PlaceContext c)
        {
            var at = c.Ground(0f);
            var into = c.Explicit ? -c.Normal : (SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera.transform.forward : Vector3.forward);
            var mouth = CaveMouths.Make(c.Group(LevelGroup.Props), at, into);
            Selection.activeGameObject = mouth.gameObject;
            return mouth.gameObject;
        }

        [PaletteItem("timber.mine-stair", "Timber/Mine", "Mine Stair", Piece = typeof(MineFrameShape), Tags = "mine stair steps shaft frames timber",
                     Tooltip = "Mine Frames holding a stair of treads along their line: the way up a shaft.")]
        static GameObject MineStair(PlaceContext c)
        {
            var frames = (MineFrameShape)PaletteCatalog.NewPiece(c, typeof(MineFrameShape), "Mine stair", c.Ground(0f), LevelGroup.Timber);
            Undo.RecordObject(frames, "Mine stair");
            frames.Stair = true;
            frames.Width = 2.2f;
            frames.Height = 2.6f;
            frames.Spacing = 2f;
            frames.Lagging = false;
            frames.Points.Clear();
            frames.Points.AddRange(new[] { Vector3.zero, new Vector3(0f, 3f, 6f), new Vector3(0f, 6f, 12f) });
            LevelPieceBuilder.Rebuild(frames);
            return frames.gameObject;
        }
    }
}
