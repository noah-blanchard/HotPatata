using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata.Editor
{
    /// <summary>
    /// A retaining wall's terrace (ARCHITECTURE §27.7): the ground on its held side raised to its top over <see cref="WallShape.Terrace"/>
    /// metres, falling back to the ground beyond (a contribution to the height stack, §27.4: only ever up, the sculpt kept).
    /// </summary>
    [InitializeOnLoad]
    public static class WallGround
    {
        static WallGround()
        {
            GroundShapes.Extra.Add(Terraces);
        }

        static IEnumerable<GroundShapes.Contribution> Terraces(Scene scene)
        {
            foreach (var wall in TimberLinks.Pieces(scene).OfType<WallShape>())
            {
                if (wall.Style != WallStyle.Retaining || wall.Terrace <= 0f || !wall.isActiveAndEnabled) continue;
                var runs = MasonryKit.Runs(wall);
                if (runs.Count == 0) continue;
                float reach = wall.Terrace + 1f;
                var pts = runs.SelectMany(r => new[] { r.a, r.b }).ToList();
                var box = Rect.MinMaxRect(pts.Min(p => p.x) - reach, pts.Min(p => p.z) - reach, pts.Max(p => p.x) + reach, pts.Max(p => p.z) + reach);
                float side = wall.RetainRight ? 1f : -1f;
                var c = new GroundShapes.Contribution { Id = wall.PieceId + "#terrace", Signature = LevelPieceBuilder.Signature(wall), Bounds = box };
                c.Apply = (running, cells) =>
                {
                    foreach (var (x, z) in GroundShapes.Cells(running, cells, box))
                    {
                        var q = running.World(x, z);
                        // the nearest run, how far behind it (on the held side) and how far along
                        float best = float.MaxValue, behind = 0f, top = 0f;
                        foreach (var run in runs)
                        {
                            var rel = new Vector3(q.x, 0f, q.y) - new Vector3(run.a.x, 0f, run.a.z);
                            float along = Mathf.Clamp(Vector3.Dot(rel, run.along), 0f, run.length);
                            float across = Vector3.Dot(rel, run.right) * side;
                            float d = (rel - run.along * along).magnitude;
                            if (d >= best) continue;
                            best = d;
                            behind = across;
                            top = run.At(run.start + along, MasonryKit.Top(wall, run.start + along) - 0.15f).y;
                        }
                        if (behind < wall.Thickness * 0.5f - 0.05f || behind > wall.Terrace) continue;
                        float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((wall.Terrace - behind) / (wall.Terrace * 0.35f)));
                        running[x, z] += Mathf.Max(0f, top - running[x, z]) * w;
                    }
                };
                yield return c;
            }
        }
    }
}
