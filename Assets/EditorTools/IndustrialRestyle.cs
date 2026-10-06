using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using static HotPatata.Editor.CourseKit;

namespace HotPatata.Editor
{
    /// <summary>
    /// Redraws the KayKit prefabs placed in a scene (obstacles, platforms, tubes, kit boxes) in the industrial look, on the scene's
    /// instances only (ARCHITECTURE §25.2): the prefab assets, and every other course, keep their KayKit look. Gameplay cues keep
    /// their own materials on purpose (fuse zones, laser beams, pads, icons, plates, checkpoints). Colliders, sizes and layers are
    /// never touched: a <see cref="KitSkin"/> is only given another shape and material.
    /// </summary>
    public static class IndustrialRestyle
    {
        /// <summary>The industrial material that replaces <paramref name="material"/> on <paramref name="skin"/> (null: keep it).</summary>
        public static string Target(string material, KitSkin skin, CourseKit.SurfaceTheme theme)
        {
            if (material.StartsWith("Industrial_")) return null;
            switch (material)
            {
                case "KayKit_Hazard": return "Industrial/Industrial_Hazard";
                case "KayKit_Belt": return CourseKit.RubberMaterial;
                case "KayKit_Brick": return theme.Wall;
                case "Tube_Pipe_1": return "Industrial/" + IndustrialMaterialBuilder.PipeName(1);
                case "Tube_Pipe_2": return "Industrial/" + IndustrialMaterialBuilder.PipeName(2);
                case "Tube_Pipe_3": return "Industrial/" + IndustrialMaterialBuilder.PipeName(3);
                case "KayKit_Toon":
                    if (skin == null) return CourseKit.PaintedMetalMaterial;
                    switch (skin.Shape)
                    {
                        case KitShape.Platform:
                            return skin.Color == KitColor.Blue ? CourseKit.PaintedMetalMaterial
                                 : skin.Color == KitColor.Yellow ? CourseKit.PaintedMetalYellowMaterial : theme.Floor;
                        case KitShape.Barrier:
                            return skin.Color == KitColor.Yellow ? CourseKit.PaintedMetalYellowMaterial
                                 : skin.Color == KitColor.Blue ? CourseKit.PaintedMetalMaterial
                                 : skin.Color == KitColor.Red ? "Industrial/Industrial_Hazard" : theme.Wall;
                        case KitShape.Arrow: return CourseKit.RawMetalMaterial;
                        case KitShape.Floor: return theme.Floor;
                        default: return CourseKit.PaintedMetalMaterial;   // pipes, pillars, struts
                    }
                default: return null;
            }
        }

        /// <summary>Restyles every renderer under <paramref name="root"/> with the room's theme; returns how many it redrew.</summary>
        public static int Apply(Transform root, CourseKit.SurfaceTheme theme)
        {
            int changed = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var current = r.sharedMaterial;
                if (current == null) continue;
                var skin = r.GetComponent<KitSkin>();
                string target = Target(current.name, skin, theme);
                if (target == null) continue;   // industrial already, or a gameplay cue
                var material = Mat(target);
                if (skin != null && skin.Shape != KitShape.Pipe && skin.Shape != KitShape.BevelBox)
                    CourseKit.Skin(r.gameObject, KitShape.BevelBox, KitColor.Neutral, material, skin.UnitBox, skin.Flip);
                else
                {
                    r.sharedMaterial = material;
                    if (PrefabUtility.IsPartOfPrefabInstance(r)) PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                }
                changed++;
            }
            return changed;
        }
    }
}
