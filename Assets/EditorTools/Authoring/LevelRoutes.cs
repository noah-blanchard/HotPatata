using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata.Editor
{
    /// <summary>
    /// The walk lines of an authored level (ARCHITECTURE §27): every walkway and bridge the author laid, on its walking surface, gathered
    /// into the level's <see cref="RouteWalks"/> (the route runner and the walk probe read them) whenever the scene is saved.
    /// </summary>
    public static class LevelRoutes
    {
        /// <summary>Walk lines are resampled at this spacing (m), like the generated courses'.</summary>
        public const float Spacing = 0.5f;

        /// <summary>Writes the scene's walk lines into its RouteWalks (made on the section root if missing); returns how many.</summary>
        public static int Gather(Scene scene)
        {
            var lines = new List<RouteWalks.Line>();
            foreach (var piece in TimberLinks.Pieces(scene).OrderBy(p => p.name))
            {
                var output = piece.Output != null ? piece.Output.GetComponent<LevelPieceOutput>() : null;
                if (output == null || output.WalkLine.Length < 2) continue;
                lines.Add(new RouteWalks.Line { name = piece.name, points = Curves.Resample(output.WalkLine, Spacing).ToArray() });
            }
            var walks = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RouteWalks>(true)).FirstOrDefault();
            if (walks == null)
            {
                if (lines.Count == 0) return 0;
                var section = SectionRoot(scene);
                if (section == null) return 0;
                walks = section.gameObject.AddComponent<RouteWalks>();
            }
            walks.Configure(lines);
            EditorUtility.SetDirty(walks);
            return lines.Count;
        }

        /// <summary>The section root of a course scene: the root of its start spawns (PlayerSpawns outside any checkpoint).</summary>
        public static Transform SectionRoot(Scene scene)
        {
            var spawn = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlayerSpawn>(true))
                .FirstOrDefault(sp => sp.GetComponentInParent<Checkpoint>() == null);
            return spawn != null ? spawn.transform.root : null;
        }
    }
}
