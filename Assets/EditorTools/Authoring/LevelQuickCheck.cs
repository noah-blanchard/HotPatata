using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata.Editor
{
    /// <summary>
    /// The quick checks run on every save of an authored level (ARCHITECTURE §27): what a piece cannot be built as drawn. The full
    /// check (contracts, walk probe, passes, kill zones) is Verify Level.
    /// </summary>
    public static class LevelQuickCheck
    {
        public static List<string> Run(Scene scene)
        {
            var findings = new List<string>();
            foreach (var piece in TimberLinks.Pieces(scene))
            {
                var output = piece.Output != null ? piece.Output.GetComponent<LevelPieceOutput>() : null;
                if (output == null) { findings.Add($"{piece.name}: not built (too few points?)"); continue; }
                if (output.Unheld > 0) findings.Add($"{piece.name}: {output.Unheld} hanger(s) with nothing above to tie to (add a giant tree's limb, or force posts)");
                var scale = piece.transform.lossyScale;
                if ((scale - Vector3.one).sqrMagnitude > 1e-4f) findings.Add($"{piece.name}: scaled {scale} (draw its size with its points instead)");
                switch (piece)
                {
                    case TimberDeckShape deck when deck.Outline.Count < 3:
                        findings.Add($"{piece.name}: a deck needs three points or more");
                        break;
                    case TimberWalkShape walk when walk.Points.Count < 2:
                        findings.Add($"{piece.name}: a walkway needs two points or more");
                        break;
                }
            }
            if (scene.GetRootGameObjects().All(g => g.GetComponentInChildren<LevelSettings>(true) == null) && TimberLinks.Pieces(scene).Any())
                findings.Add("no LevelSettings in the scene (make levels with HotPatata/Level/New Level)");
            return findings;
        }
    }
}
