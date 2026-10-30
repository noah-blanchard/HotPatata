using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The rock in the palette (ARCHITECTURE §27.5, docs/LEVEL_AUTHORING.md §28): a cliff in each of its six rocks (looks of the Cliff
    /// entry), each with the detail and the transitions that suit it, and the Rock Outcrop (a small, many-sided, fractured cliff: the rock
    /// out of the hill).
    /// </summary>
    public static class RockEntries
    {
        /// <summary>Gives <paramref name="cliff"/> the detail and transitions of its rock (strata for sandstone and limestone, columns for basalt...).</summary>
        public static void Dress(CliffShape cliff, CliffRock rock)
        {
            cliff.Rock = rock;
            cliff.Octaves = 4;
            cliff.Lacunarity = 2.1f;
            cliff.Strata = rock == CliffRock.Sandstone ? 0.8f : rock == CliffRock.Limestone ? 0.5f : rock == CliffRock.Earth ? 0.6f : 0.15f;
            cliff.Fracture = rock == CliffRock.Granite ? 0.7f : rock == CliffRock.Earth || rock == CliffRock.Basalt ? 0f : 0.45f;
            cliff.FootBlend = 0.9f;
            cliff.RockScatter = rock == CliffRock.Earth || rock == CliffRock.Basalt ? 0f : 0.35f;
            cliff.Scree = rock == CliffRock.Earth ? 0.2f : 0.6f;
            cliff.ScreeWidth = 4f;
            cliff.Fuse = 2f;
            cliff.Decals = 0.4f;
            if (rock == CliffRock.Earth)
            {
                // an earth bank stays under a lock's height: it is never a lock (Verify warns if a Climb lock targets one)
                cliff.Height = Mathf.Min(cliff.Height, 3f);
                cliff.Roughness = 0.4f;
            }
        }

        static GameObject Cliff(PlaceContext c, CliffRock rock, string name)
        {
            var cliff = (CliffShape)PaletteCatalog.NewPiece(c, typeof(CliffShape), name, c.Ground(-0.5f), LevelGroup.Props);
            Undo.RecordObject(cliff, name);
            Dress(cliff, rock);
            LevelPieceBuilder.Rebuild(cliff);
            return cliff.gameObject;
        }

        [PaletteLook("rock.cliff", "sandstone", "Sandstone", Tooltip = "Layered ochre, strong strata")]
        static GameObject Sandstone(PlaceContext c) => Cliff(c, CliffRock.Sandstone, "Cliff");

        [PaletteLook("rock.cliff", "mossy", "Mossy", Tooltip = "Granite under moss")]
        static GameObject Mossy(PlaceContext c) => Cliff(c, CliffRock.Mossy, "Cliff");

        [PaletteLook("rock.cliff", "limestone", "Limestone", Tooltip = "Pale karst, banded")]
        static GameObject Limestone(PlaceContext c) => Cliff(c, CliffRock.Limestone, "Cliff");

        [PaletteLook("rock.cliff", "basalt", "Basalt", Tooltip = "Dark, in columns")]
        static GameObject Basalt(PlaceContext c) => Cliff(c, CliffRock.Basalt, "Cliff");

        [PaletteLook("rock.cliff", "earth", "Earth Bank", Tooltip = "Soil and roots, under 3.4 m: never a lock")]
        static GameObject Earth(PlaceContext c) => Cliff(c, CliffRock.Earth, "Earth bank");

        [PaletteItem("rock.outcrop", "Rock/Cliffs", "Rock Outcrop", Piece = typeof(CliffShape), Tags = "outcrop rock boulder hill",
                     Tooltip = "A small fractured cliff of many sides, half buried: the rock out of the hill.")]
        static GameObject Outcrop(PlaceContext c)
        {
            var cliff = (CliffShape)PaletteCatalog.NewPiece(c, typeof(CliffShape), "Outcrop", c.Ground(-0.8f), LevelGroup.Props);
            Undo.RecordObject(cliff, "Outcrop");
            Dress(cliff, CliffRock.Granite);
            cliff.Height = 3.5f;
            cliff.Sink = 1.5f;
            cliff.Fracture = 0.85f;
            cliff.Crest = 1.2f;
            cliff.Outline.Clear();
            cliff.Outline.AddRange(Enumerable.Range(0, 9).Select(i =>
            {
                float a = i * Mathf.PI * 2f / 9f, r = 3.2f * (1f + 0.28f * Mathf.Sin(a * 2.7f + 1.3f));
                return new Vector3(Mathf.Cos(a) * r * 1.3f, 0f, Mathf.Sin(a) * r);
            }));
            LevelPieceBuilder.Rebuild(cliff);
            return cliff.gameObject;
        }
    }
}
