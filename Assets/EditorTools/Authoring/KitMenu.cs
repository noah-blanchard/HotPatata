using System;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// GameObject > HotPatata Level > Kit (ARCHITECTURE §27, docs/OBSTACLES.md): every kit prefab placed the way the level's own pieces
    /// are: on the ground where the Scene view looks (raised where it is meant to hang), in the level's Kit group, selected and ready to
    /// size (<see cref="KitSceneTools"/>) and link. The prefabs stay as they are (the kit's own look until the forest looks, M18.3b).
    /// </summary>
    public static class KitMenu
    {
        const string Menu = "GameObject/HotPatata Level/Kit/";
        const int Doors = 100, Sources = 120, Zones = 140;

        /// <summary>A door's default size and how far a sliding door slides (its width and a little more).</summary>
        static readonly Vector3 DoorSize = new Vector3(4f, 3.6f, 0.5f);
        const float SlideMargin = 0.3f;

        // ------------------------------------------------------------------ doors and platforms

        [MenuItem(Menu + "Door (rises)", priority = Doors)] static void Door() => Place(BombObstacleKitBuilder.Door, "Door");
        [MenuItem(Menu + "Sliding Door", priority = Doors + 1)]
        static void SlidingDoor() => Place(BombObstacleKitBuilder.Door, "Sliding door", setup: go =>
            BombObstacleKitBuilder.ResizeActuator(go, DoorSize, new Vector3(DoorSize.x + SlideMargin, 0f, 0f)));
        [MenuItem(Menu + "Bridge (slides out)", priority = Doors + 2)] static void Bridge() => Place(BombObstacleKitBuilder.Bridge, "Bridge", 3f);
        [MenuItem(Menu + "Drawbridge", priority = Doors + 3)] static void Drawbridge() => Place(KitVariantBuilder.DrawbridgePath, "Drawbridge", 3f);
        [MenuItem(Menu + "Pivot", priority = Doors + 4)] static void Pivot() => Place(BombObstacleKitBuilder.Pivot, "Pivot", 3f);
        [MenuItem(Menu + "Lift", priority = Doors + 5)] static void Lift() => Place(BombObstacleKitBuilder.Lift, "Lift");
        [MenuItem(Menu + "Moving Platform", priority = Doors + 6)] static void Moving() => Place(CourseKit.PlatformsDir + "Platform_Moving", "Moving platform", 1f);
        [MenuItem(Menu + "Moving Boulder", priority = Doors + 7)] static void Boulder() => Place(KitVariantBuilder.BoulderPath, "Moving boulder", 1f);
        [MenuItem(Menu + "Elevator", priority = Doors + 8)] static void Elevator() => Place(CourseKit.PlatformsDir + "Platform_Elevator", "Elevator");
        [MenuItem(Menu + "Falling Platform", priority = Doors + 9)] static void Falling() => Place(CourseKit.PlatformsDir + "Platform_Falling", "Falling platform", 1f);
        [MenuItem(Menu + "Conveyor", priority = Doors + 10)] static void Conveyor() => Place(CourseKit.PlatformsDir + "Platform_Conveyor", "Conveyor");
        [MenuItem(Menu + "Platform", priority = Doors + 11)] static void Basic() => Place(CourseKit.PlatformsDir + "Platform_Basic", "Platform");
        [MenuItem(Menu + "Narrow Platform", priority = Doors + 12)] static void Narrow() => Place(CourseKit.PlatformsDir + "Platform_Narrow", "Narrow platform");

        // ------------------------------------------------------------------ sources

        [MenuItem(Menu + "Plate", priority = Sources)] static void Plate() => Place(BombObstacleKitBuilder.Plate, "Plate");
        [MenuItem(Menu + "Hands-Free Plate", priority = Sources + 1)] static void HandsFree() => Place(BombObstacleKitBuilder.PlateHandsFree, "Hands-free plate");
        [MenuItem(Menu + "Heavy Plate", priority = Sources + 2)] static void Heavy() => Place(BombObstacleKitBuilder.PlateHeavy, "Heavy plate");
        [MenuItem(Menu + "Hourglass Plate", priority = Sources + 3)] static void Hourglass() => Place(BombObstacleKitBuilder.PlateHourglass, "Hourglass plate");
        [MenuItem(Menu + "Hoop (ring)", priority = Sources + 4)] static void Ring() => Place(BombObstacleKitBuilder.GateRing, "Hoop", 2f);
        [MenuItem(Menu + "Arch", priority = Sources + 5)] static void Arch() => Place(BombObstacleKitBuilder.GateArch, "Arch");
        [MenuItem(Menu + "Sun Beam", priority = Sources + 6)] static void Beam() => Place(BombObstacleKitBuilder.Beam, "Sun beam", 1f);
        [MenuItem(Menu + "Switch", priority = Sources + 7)] static void Switch() => Place(BombObstacleKitBuilder.Switch, "Switch");

        // ------------------------------------------------------------------ zones and obstacles

        [MenuItem(Menu + "Hot Zone", priority = Zones)] static void Hot() => Place(BombObstacleKitBuilder.ZoneHot, "Hot zone");
        [MenuItem(Menu + "Cold Zone", priority = Zones + 1)] static void Cold() => Place(BombObstacleKitBuilder.ZoneCold, "Cold zone");
        [MenuItem(Menu + "Forbidden Zone", priority = Zones + 2)] static void Forbidden() => Place(BombObstacleKitBuilder.ZoneForbidden, "Forbidden zone");
        [MenuItem(Menu + "Laser Curtain", priority = Zones + 3)] static void Curtain() => Place(BombObstacleKitBuilder.LaserCurtain, "Laser curtain");
        [MenuItem(Menu + "Body Screen", priority = Zones + 4)] static void Screen() => Place(BombObstacleKitBuilder.Screen, "Body screen");
        [MenuItem(Menu + "Tube", priority = Zones + 5)] static void Tube() => Place(BombObstacleKitBuilder.Tube, "Tube");
        [MenuItem(Menu + "Cannon", priority = Zones + 6)] static void Cannon() => Place(BombObstacleKitBuilder.Cannon, "Cannon");
        [MenuItem(Menu + "Hoop Obstacle", priority = Zones + 7)] static void HoopObstacle() => Place(CourseKit.ObstaclesDir + "Obstacle_Hoop", "Hoop obstacle", 2f);
        [MenuItem(Menu + "Rotating Bar", priority = Zones + 8)] static void Bar() => Place(CourseKit.ObstaclesDir + "Obstacle_RotatingBar", "Rotating bar");
        [MenuItem(Menu + "Sweeper", priority = Zones + 9)] static void Sweeper() => Place(CourseKit.ObstaclesDir + "Obstacle_Sweeper", "Sweeper");
        [MenuItem(Menu + "Piston", priority = Zones + 10)] static void Piston() => Place(CourseKit.ObstaclesDir + "Obstacle_Piston", "Piston");
        [MenuItem(Menu + "Crusher", priority = Zones + 11)] static void Crusher() => Place(CourseKit.ObstaclesDir + "Obstacle_Crusher", "Crusher");
        [MenuItem(Menu + "Windmill", priority = Zones + 12)] static void Windmill() => Place(CourseKit.ObstaclesDir + "Obstacle_Windmill", "Windmill");
        [MenuItem(Menu + "Launch Pad", priority = Zones + 13)] static void LaunchPad() => Place(CourseKit.ObstaclesDir + "LaunchPad", "Launch pad");
        [MenuItem(Menu + "Kill Zone", priority = Zones + 14)] static void Kill() => Place(CourseKit.GameplayDir + "KillZone", "Kill zone");

        // ------------------------------------------------------------------ forest looks (ForestKitBuilder)

        const string Forest = Menu + "Forest/";
        const int Looks = 200;

        [MenuItem(Forest + "Winch (hourglass plate)", priority = Looks)] static void Winch() => Place(ForestKitBuilder.Winch, "Winch");
        [MenuItem(Forest + "Lever (hands-free plate)", priority = Looks + 1)] static void Lever() => Place(ForestKitBuilder.Lever, "Lever");
        [MenuItem(Forest + "Drawbridge", priority = Looks + 2)] static void ForestDrawbridge() => Place(ForestKitBuilder.Drawbridge, "Drawbridge", 3f);
        [MenuItem(Forest + "Cage (lift)", priority = Looks + 3)] static void Cage() => Place(ForestKitBuilder.Cage, "Cage");
        [MenuItem(Forest + "Signal Ring (hoop)", priority = Looks + 4)] static void SignalRing() => Place(ForestKitBuilder.SignalRing, "Signal ring", 2f);
        [MenuItem(Forest + "Bell Arch (arch)", priority = Looks + 5)] static void BellArch() => Place(ForestKitBuilder.Bell, "Bell arch");
        [MenuItem(Forest + "Log Chute (tube)", priority = Looks + 6)] static void Chute() => Place(ForestKitBuilder.Chute, "Log chute");
        [MenuItem(Forest + "Rope Net (body screen)", priority = Looks + 7)] static void Net() => Place(ForestKitBuilder.Net, "Rope net");
        [MenuItem(Forest + "Embers (hot zone)", priority = Looks + 8)] static void Embers() => Place(ForestKitBuilder.Embers, "Embers");
        [MenuItem(Forest + "Sparks (laser curtain)", priority = Looks + 9)] static void Sparks() => Place(ForestKitBuilder.Sparks, "Sparks");
        [MenuItem(Forest + "Palisade Gate (door)", priority = Looks + 10)] static void ForestGate() => Place(ForestKitBuilder.Gate, "Palisade gate");
        [MenuItem(Forest + "Gangway (bridge)", priority = Looks + 11)] static void ForestGangway() => Place(ForestKitBuilder.Gangway, "Gangway", 3f);
        [MenuItem(Forest + "Log Raft (moving platform)", priority = Looks + 12)] static void ForestRaft() => Place(ForestKitBuilder.Raft, "Log raft", 1f);
        [MenuItem(Forest + "Rotten Planks (falling platform)", priority = Looks + 13)] static void ForestRotten() => Place(ForestKitBuilder.RottenPlank, "Rotten planks", 1f);
        [MenuItem(Forest + "Swinging Log (sweeper)", priority = Looks + 14)] static void ForestLog() => Place(ForestKitBuilder.SwingingLog, "Swinging log");

        // ------------------------------------------------------------------ placing

        /// <summary>
        /// Places the kit prefab <paramref name="prefabPathNoExt"/> where the Scene view looks: its colliders' bottom on the ground,
        /// <paramref name="raise"/> metres higher for what hangs (a bridge, a hoop), in the level's Kit group, selected. Returns it.
        /// </summary>
        public static GameObject Place(string prefabPathNoExt, string name, float raise = 0f, Action<GameObject> setup = null)
        {
            if (prefabPathNoExt.EndsWith(".prefab")) prefabPathNoExt = prefabPathNoExt.Substring(0, prefabPathNoExt.Length - ".prefab".Length);
            var scene = EditorSceneManager.GetActiveScene();
            var settings = LevelScenes.SettingsOf(scene);
            var parent = settings != null ? settings.Group(LevelGroup.Kit) : null;
            // the floor the Scene view looks at (a deck, a walkway, the ground), found before the new element stands there
            var at = LevelNature.ViewFloor(scene);
            var siblings = parent != null ? parent.Cast<Transform>().Select(t => t.name).ToArray() : Array.Empty<string>();
            var go = CourseKit.Place(parent, prefabPathNoExt, ObjectNames.GetUniqueName(siblings, name), at, Quaternion.identity);
            Undo.RegisterCreatedObjectUndo(go, "Add " + name);
            setup?.Invoke(go);
            // stand it on the ground: the bottom of its solid colliders (else its renderers) at the ground, then raised
            Physics.SyncTransforms();
            var bounds = Bottom(go);
            if (bounds.HasValue) go.transform.position += Vector3.up * (at.y - bounds.Value + raise);
            Selection.activeGameObject = go;
            return go;
        }

        static float? Bottom(GameObject go)
        {
            var colliders = go.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger && c.enabled).ToList();
            if (colliders.Count > 0) return colliders.Min(c => c.bounds.min.y);
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            return renderers.Length > 0 ? renderers.Min(r => r.bounds.min.y) : (float?)null;
        }
    }
}
