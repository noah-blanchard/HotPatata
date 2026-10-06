using HotPatata;
using UnityEditor;
using UnityEngine;
using static HotPatata.Editor.CourseKit;

namespace HotPatata.Editor
{
    /// <summary>
    /// Redraws the KayKit prefabs placed in PatataWilds (obstacles, platforms, tubes, kit boxes) in the nature look, on the scene's
    /// instances only (ARCHITECTURE §25.3), as <see cref="IndustrialRestyle"/> does for the plant: a mover becomes a log raft, a
    /// hazard a stained log with charred bands, a falling platform rotten planks, a gate's frame mossy logs, a tube's pipes blazed
    /// hollow logs in the slot colour. Gameplay cues keep their own materials on purpose (fuse zones, laser beams, pads, icons,
    /// plates, launch pads). Colliders, sizes and layers are never touched: a <see cref="KitSkin"/> only gets another shape and material.
    /// </summary>
    public static class NatureRestyle
    {
        /// <summary>The nature shape and material that replace <paramref name="material"/> on <paramref name="skin"/> (material null: keep it).</summary>
        public static (KitShape shape, string material) Target(string material, KitSkin skin, SurfaceTheme theme)
        {
            const string D = NatureDir;
            if (material.StartsWith("Nature_")) return (KitShape.Logs, null);
            switch (material)
            {
                case "KayKit_Hazard": return (KitShape.Logs, D + NatureMaterialBuilder.HazardName);
                case "KayKit_Belt": return (KitShape.Logs, D + NatureMaterialBuilder.BeltName);
                case "KayKit_Brick": return (KitShape.RoughBox, theme.Wall);
                case "Tube_Pipe_1": return (KitShape.Logs, D + NatureMaterialBuilder.PipeName(1));
                case "Tube_Pipe_2": return (KitShape.Logs, D + NatureMaterialBuilder.PipeName(2));
                case "Tube_Pipe_3": return (KitShape.Logs, D + NatureMaterialBuilder.PipeName(3));
                case "KayKit_Toon":
                    if (skin == null) return (KitShape.Logs, D + "Nature_RoughWood");
                    switch (skin.Shape)
                    {
                        case KitShape.Platform:
                            return skin.Color == KitColor.Blue ? (KitShape.Logs, D + "Nature_BarkBrown")
                                 : skin.Color == KitColor.Yellow ? (KitShape.Planks, D + NatureMaterialBuilder.FallingName)
                                 : (KitShape.RoughBox, theme.Floor);
                        case KitShape.Barrier:
                            return skin.Color == KitColor.Yellow ? (KitShape.Logs, D + "Nature_RoughWood")
                                 : skin.Color == KitColor.Blue ? (KitShape.Logs, D + "Nature_BarkBrown")
                                 : skin.Color == KitColor.Red ? (KitShape.Logs, D + NatureMaterialBuilder.HazardName)
                                 : (KitShape.RoughBox, theme.Wall);
                        case KitShape.Arrow: return (KitShape.RoughBox, D + "Nature_Mud");
                        case KitShape.Floor: return (KitShape.Planks, D + "Nature_Planks");
                        case KitShape.Pipe: return (KitShape.Logs, D + "Nature_BarkDark");
                        default: return (KitShape.Logs, D + "Nature_PineBark");   // pillars, struts
                    }
                default: return (KitShape.Logs, null);
            }
        }

        /// <summary>Restyles every renderer under <paramref name="root"/> with the section's theme; returns how many it redrew.</summary>
        public static int Apply(Transform root, SurfaceTheme theme)
        {
            int changed = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var current = r.sharedMaterial;
                if (current == null || r.GetComponentInParent<LaunchPad>() != null) continue;   // the launch pad's spring is its cue
                var skin = r.GetComponent<KitSkin>();
                var (shape, target) = Target(current.name, skin, theme);
                if (target == null) continue;   // nature already, or a gameplay cue
                var material = Mat(target);
                if (skin != null)
                    CourseKit.Skin(r.gameObject, shape, KitColor.Neutral, material, skin.UnitBox, skin.Flip);
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
