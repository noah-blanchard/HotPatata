using System;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The kit in the palette (ARCHITECTURE §27.3, docs/OBSTACLES.md): one entry per system, its looks as chips (the KayKit look, the forest
    /// looks of <see cref="ForestKitBuilder"/>). Every kit prefab is placed the way the level's own pieces are: on the floor clicked (or where
    /// the Scene view looks), raised where it is meant to hang, in the level's Kit group, selected and ready to size
    /// (<see cref="KitSceneTools"/>) and link. The prefabs stay as they are.
    /// </summary>
    public static class PaletteKitEntries
    {
        const PaletteKind Kit = PaletteKind.Kit;
        const PaletteSnap Floor = PaletteSnap.Floor | PaletteSnap.Ground;
        const PaletteSnap Opening = Floor | PaletteSnap.WallOpening;
        const string Forest = "Forest look";

        /// <summary>A door's default size and how far a sliding door slides (its width and a little more).</summary>
        static readonly Vector3 DoorSize = new Vector3(4f, 3.6f, 0.5f);
        const float SlideMargin = 0.3f;

        // ------------------------------------------------------------------ doors and bridges

        [PaletteItem("kit.door", "Kit/Doors and bridges", "Door (rises)", Kind = Kit, Snap = Opening, Prefab = BombObstacleKitBuilder.Door + ".prefab", Tags = "door gate actuator")]
        static GameObject Door(PlaceContext c) => Place(c, BombObstacleKitBuilder.Door, "Door");
        [PaletteLook("kit.door", "forest", "Palisade Gate", Prefab = ForestKitBuilder.Gate, Tooltip = Forest)]
        static GameObject DoorForest(PlaceContext c) => Place(c, ForestKitBuilder.Gate, "Palisade gate");

        [PaletteItem("kit.sliding-door", "Kit/Doors and bridges", "Sliding Door", Kind = Kit, Snap = Opening, Prefab = BombObstacleKitBuilder.Door + ".prefab", Tags = "door slide")]
        static GameObject SlidingDoor(PlaceContext c) =>
            Place(c, BombObstacleKitBuilder.Door, "Sliding door", setup: go => BombObstacleKitBuilder.ResizeActuator(go, DoorSize, new Vector3(DoorSize.x + SlideMargin, 0f, 0f)));

        [PaletteItem("kit.bridge", "Kit/Doors and bridges", "Bridge (slides out)", Kind = Kit, Snap = Floor, Prefab = BombObstacleKitBuilder.Bridge + ".prefab", Tags = "bridge slide")]
        static GameObject Bridge(PlaceContext c) => Place(c, BombObstacleKitBuilder.Bridge, "Bridge", 3f);
        [PaletteLook("kit.bridge", "forest", "Gangway", Prefab = ForestKitBuilder.Gangway, Tooltip = Forest)]
        static GameObject BridgeForest(PlaceContext c) => Place(c, ForestKitBuilder.Gangway, "Gangway", 3f);

        [PaletteItem("kit.drawbridge", "Kit/Doors and bridges", "Drawbridge", Kind = Kit, Snap = Floor, Prefab = KitVariantBuilder.DrawbridgePath, Tags = "drawbridge pivot")]
        static GameObject Drawbridge(PlaceContext c) => Place(c, KitVariantBuilder.DrawbridgePath, "Drawbridge", 3f);
        [PaletteLook("kit.drawbridge", "forest", "Drawbridge (forest)", Prefab = ForestKitBuilder.Drawbridge, Tooltip = Forest)]
        static GameObject DrawbridgeForest(PlaceContext c) => Place(c, ForestKitBuilder.Drawbridge, "Drawbridge", 3f);

        [PaletteItem("kit.pivot", "Kit/Doors and bridges", "Pivot", Kind = Kit, Snap = Floor, Prefab = BombObstacleKitBuilder.Pivot + ".prefab", Tags = "pivot turn")]
        static GameObject Pivot(PlaceContext c) => Place(c, BombObstacleKitBuilder.Pivot, "Pivot", 3f);

        [PaletteItem("kit.lift", "Kit/Doors and bridges", "Lift", Kind = Kit, Snap = Floor, Prefab = BombObstacleKitBuilder.Lift + ".prefab", Tags = "lift raise")]
        static GameObject Lift(PlaceContext c) => Place(c, BombObstacleKitBuilder.Lift, "Lift");
        [PaletteLook("kit.lift", "forest", "Cage", Prefab = ForestKitBuilder.Cage, Tooltip = Forest)]
        static GameObject LiftForest(PlaceContext c) => Place(c, ForestKitBuilder.Cage, "Cage");

        // ------------------------------------------------------------------ platforms

        [PaletteItem("kit.platform", "Kit/Platforms", "Platform", Kind = Kit, Snap = Floor, Prefab = CourseKit.PlatformsDir + "Platform_Basic.prefab", Tags = "platform")]
        static GameObject Platform(PlaceContext c) => Place(c, CourseKit.PlatformsDir + "Platform_Basic", "Platform");

        [PaletteItem("kit.narrow-platform", "Kit/Platforms", "Narrow Platform", Kind = Kit, Snap = Floor, Prefab = CourseKit.PlatformsDir + "Platform_Narrow.prefab", Tags = "platform narrow")]
        static GameObject Narrow(PlaceContext c) => Place(c, CourseKit.PlatformsDir + "Platform_Narrow", "Narrow platform");

        [PaletteItem("kit.moving-platform", "Kit/Platforms", "Moving Platform", Kind = Kit, Snap = Floor, Prefab = CourseKit.PlatformsDir + "Platform_Moving.prefab", Tags = "platform moving carrier")]
        static GameObject Moving(PlaceContext c) => Place(c, CourseKit.PlatformsDir + "Platform_Moving", "Moving platform", 1f);
        [PaletteLook("kit.moving-platform", "forest", "Log Raft", Prefab = ForestKitBuilder.Raft, Tooltip = Forest)]
        static GameObject MovingForest(PlaceContext c) => Place(c, ForestKitBuilder.Raft, "Log raft", 1f);

        [PaletteItem("kit.moving-boulder", "Kit/Platforms", "Moving Boulder", Kind = Kit, Snap = Floor, Prefab = KitVariantBuilder.BoulderPath, Tags = "boulder moving rock")]
        static GameObject Boulder(PlaceContext c) => Place(c, KitVariantBuilder.BoulderPath, "Moving boulder", 1f);

        [PaletteItem("kit.elevator", "Kit/Platforms", "Elevator", Kind = Kit, Snap = Floor, Prefab = CourseKit.PlatformsDir + "Platform_Elevator.prefab", Tags = "elevator up")]
        static GameObject Elevator(PlaceContext c) => Place(c, CourseKit.PlatformsDir + "Platform_Elevator", "Elevator");

        [PaletteItem("kit.falling-platform", "Kit/Platforms", "Falling Platform", Kind = Kit, Snap = Floor, Prefab = CourseKit.PlatformsDir + "Platform_Falling.prefab", Tags = "falling crumble")]
        static GameObject Falling(PlaceContext c) => Place(c, CourseKit.PlatformsDir + "Platform_Falling", "Falling platform", 1f);
        [PaletteLook("kit.falling-platform", "forest", "Rotten Planks", Prefab = ForestKitBuilder.RottenPlank, Tooltip = Forest)]
        static GameObject FallingForest(PlaceContext c) => Place(c, ForestKitBuilder.RottenPlank, "Rotten planks", 1f);

        [PaletteItem("kit.conveyor", "Kit/Platforms", "Conveyor", Kind = Kit, Snap = Floor, Prefab = CourseKit.PlatformsDir + "Platform_Conveyor.prefab", Tags = "conveyor belt")]
        static GameObject Conveyor(PlaceContext c) => Place(c, CourseKit.PlatformsDir + "Platform_Conveyor", "Conveyor");

        [PaletteItem("kit.launch-pad", "Kit/Platforms", "Launch Pad", Kind = Kit, Snap = Floor, Prefab = CourseKit.ObstaclesDir + "LaunchPad.prefab", Tags = "launch pad jump spring")]
        static GameObject LaunchPad(PlaceContext c) => Place(c, CourseKit.ObstaclesDir + "LaunchPad", "Launch pad");

        // ------------------------------------------------------------------ signals

        [PaletteItem("kit.plate", "Kit/Signals", "Plate", Kind = Kit, Snap = Floor, Prefab = BombObstacleKitBuilder.Plate + ".prefab", Tags = "plate pressure source")]
        static GameObject Plate(PlaceContext c) => Place(c, BombObstacleKitBuilder.Plate, "Plate");

        [PaletteItem("kit.hands-free-plate", "Kit/Signals", "Hands-Free Plate", Kind = Kit, Snap = Floor, Prefab = BombObstacleKitBuilder.PlateHandsFree + ".prefab", Tags = "plate hands free source")]
        static GameObject HandsFree(PlaceContext c) => Place(c, BombObstacleKitBuilder.PlateHandsFree, "Hands-free plate");
        [PaletteLook("kit.hands-free-plate", "forest", "Lever", Prefab = ForestKitBuilder.Lever, Tooltip = Forest)]
        static GameObject HandsFreeForest(PlaceContext c) => Place(c, ForestKitBuilder.Lever, "Lever");

        [PaletteItem("kit.heavy-plate", "Kit/Signals", "Heavy Plate", Kind = Kit, Snap = Floor, Prefab = BombObstacleKitBuilder.PlateHeavy + ".prefab", Tags = "plate heavy source")]
        static GameObject Heavy(PlaceContext c) => Place(c, BombObstacleKitBuilder.PlateHeavy, "Heavy plate");

        [PaletteItem("kit.hourglass-plate", "Kit/Signals", "Hourglass Plate", Kind = Kit, Snap = Floor, Prefab = BombObstacleKitBuilder.PlateHourglass + ".prefab", Tags = "plate hourglass timer source")]
        static GameObject Hourglass(PlaceContext c) => Place(c, BombObstacleKitBuilder.PlateHourglass, "Hourglass plate");
        [PaletteLook("kit.hourglass-plate", "forest", "Winch", Prefab = ForestKitBuilder.Winch, Tooltip = Forest)]
        static GameObject HourglassForest(PlaceContext c) => Place(c, ForestKitBuilder.Winch, "Winch");

        [PaletteItem("kit.hoop", "Kit/Signals", "Hoop (ring)", Kind = Kit, Snap = Floor, Prefab = BombObstacleKitBuilder.GateRing + ".prefab", Tags = "hoop ring gate source")]
        static GameObject Ring(PlaceContext c) => Place(c, BombObstacleKitBuilder.GateRing, "Hoop", 2f);
        [PaletteLook("kit.hoop", "forest", "Signal Ring", Prefab = ForestKitBuilder.SignalRing, Tooltip = Forest)]
        static GameObject RingForest(PlaceContext c) => Place(c, ForestKitBuilder.SignalRing, "Signal ring", 2f);

        [PaletteItem("kit.arch", "Kit/Signals", "Arch", Kind = Kit, Snap = Floor, Prefab = BombObstacleKitBuilder.GateArch + ".prefab", Tags = "arch gate source")]
        static GameObject Arch(PlaceContext c) => Place(c, BombObstacleKitBuilder.GateArch, "Arch");
        [PaletteLook("kit.arch", "forest", "Bell Arch", Prefab = ForestKitBuilder.Bell, Tooltip = Forest)]
        static GameObject ArchForest(PlaceContext c) => Place(c, ForestKitBuilder.Bell, "Bell arch");

        [PaletteItem("kit.sun-beam", "Kit/Signals", "Sun Beam", Kind = Kit, Snap = Floor, Prefab = BombObstacleKitBuilder.Beam + ".prefab", Tags = "sun beam light source")]
        static GameObject Beam(PlaceContext c) => Place(c, BombObstacleKitBuilder.Beam, "Sun beam", 1f);

        [PaletteItem("kit.switch", "Kit/Signals", "Switch", Kind = Kit, Snap = Floor, Prefab = BombObstacleKitBuilder.Switch + ".prefab", Tags = "switch toggle")]
        static GameObject Switch(PlaceContext c) => Place(c, BombObstacleKitBuilder.Switch, "Switch");

        // ------------------------------------------------------------------ bomb zones

        [PaletteItem("kit.hot-zone", "Kit/Bomb zones", "Hot Zone", Kind = Kit, Snap = Floor, Prefab = BombObstacleKitBuilder.ZoneHot + ".prefab", Tags = "hot zone fuse")]
        static GameObject Hot(PlaceContext c) => Place(c, BombObstacleKitBuilder.ZoneHot, "Hot zone");
        [PaletteLook("kit.hot-zone", "forest", "Embers", Prefab = ForestKitBuilder.Embers, Tooltip = Forest)]
        static GameObject HotForest(PlaceContext c) => Place(c, ForestKitBuilder.Embers, "Embers");

        [PaletteItem("kit.cold-zone", "Kit/Bomb zones", "Cold Zone", Kind = Kit, Snap = Floor, Prefab = BombObstacleKitBuilder.ZoneCold + ".prefab", Tags = "cold zone fuse")]
        static GameObject Cold(PlaceContext c) => Place(c, BombObstacleKitBuilder.ZoneCold, "Cold zone");

        [PaletteItem("kit.forbidden-zone", "Kit/Bomb zones", "Forbidden Zone", Kind = Kit, Snap = Floor, Prefab = BombObstacleKitBuilder.ZoneForbidden + ".prefab", Tags = "forbidden zone")]
        static GameObject Forbidden(PlaceContext c) => Place(c, BombObstacleKitBuilder.ZoneForbidden, "Forbidden zone");

        [PaletteItem("kit.laser-curtain", "Kit/Bomb zones", "Laser Curtain", Kind = Kit, Snap = Opening, Prefab = BombObstacleKitBuilder.LaserCurtain + ".prefab", Tags = "laser curtain runners only")]
        static GameObject Curtain(PlaceContext c) => Place(c, BombObstacleKitBuilder.LaserCurtain, "Laser curtain");
        [PaletteLook("kit.laser-curtain", "forest", "Sparks", Prefab = ForestKitBuilder.Sparks, Tooltip = Forest)]
        static GameObject CurtainForest(PlaceContext c) => Place(c, ForestKitBuilder.Sparks, "Sparks");

        [PaletteItem("kit.body-screen", "Kit/Bomb zones", "Body Screen", Kind = Kit, Snap = Opening, Prefab = BombObstacleKitBuilder.Screen + ".prefab", Tags = "body screen net bomb only")]
        static GameObject BodyScreen(PlaceContext c) => Place(c, BombObstacleKitBuilder.Screen, "Body screen");
        [PaletteLook("kit.body-screen", "forest", "Rope Net", Prefab = ForestKitBuilder.Net, Tooltip = Forest)]
        static GameObject BodyScreenForest(PlaceContext c) => Place(c, ForestKitBuilder.Net, "Rope net");

        // ------------------------------------------------------------------ hazards

        [PaletteItem("kit.rotating-bar", "Kit/Hazards", "Rotating Bar", Kind = Kit, Snap = Floor, Prefab = CourseKit.ObstaclesDir + "Obstacle_RotatingBar.prefab", Tags = "rotating bar hazard")]
        static GameObject Bar(PlaceContext c) => Place(c, CourseKit.ObstaclesDir + "Obstacle_RotatingBar", "Rotating bar");

        [PaletteItem("kit.sweeper", "Kit/Hazards", "Sweeper", Kind = Kit, Snap = Floor, Prefab = CourseKit.ObstaclesDir + "Obstacle_Sweeper.prefab", Tags = "sweeper hazard")]
        static GameObject Sweeper(PlaceContext c) => Place(c, CourseKit.ObstaclesDir + "Obstacle_Sweeper", "Sweeper");
        [PaletteLook("kit.sweeper", "forest", "Swinging Log", Prefab = ForestKitBuilder.SwingingLog, Tooltip = Forest)]
        static GameObject SweeperForest(PlaceContext c) => Place(c, ForestKitBuilder.SwingingLog, "Swinging log");

        [PaletteItem("kit.piston", "Kit/Hazards", "Piston", Kind = Kit, Snap = Floor, Prefab = CourseKit.ObstaclesDir + "Obstacle_Piston.prefab", Tags = "piston hazard")]
        static GameObject Piston(PlaceContext c) => Place(c, CourseKit.ObstaclesDir + "Obstacle_Piston", "Piston");

        [PaletteItem("kit.crusher", "Kit/Hazards", "Crusher", Kind = Kit, Snap = Floor, Prefab = CourseKit.ObstaclesDir + "Obstacle_Crusher.prefab", Tags = "crusher hazard")]
        static GameObject Crusher(PlaceContext c) => Place(c, CourseKit.ObstaclesDir + "Obstacle_Crusher", "Crusher");

        [PaletteItem("kit.windmill", "Kit/Hazards", "Windmill", Kind = Kit, Snap = Floor, Prefab = CourseKit.ObstaclesDir + "Obstacle_Windmill.prefab", Tags = "windmill hazard blades")]
        static GameObject Windmill(PlaceContext c) => Place(c, CourseKit.ObstaclesDir + "Obstacle_Windmill", "Windmill");

        [PaletteItem("kit.hoop-obstacle", "Kit/Hazards", "Hoop Obstacle", Kind = Kit, Snap = Floor, Prefab = CourseKit.ObstaclesDir + "Obstacle_Hoop.prefab", Tags = "hoop obstacle")]
        static GameObject HoopObstacle(PlaceContext c) => Place(c, CourseKit.ObstaclesDir + "Obstacle_Hoop", "Hoop obstacle", 2f);

        // ------------------------------------------------------------------ transit

        [PaletteItem("kit.tube", "Kit/Transit", "Tube", Kind = Kit, Snap = Floor, Prefab = BombObstacleKitBuilder.Tube + ".prefab", Tags = "tube transit pipe")]
        static GameObject Tube(PlaceContext c) => Place(c, BombObstacleKitBuilder.Tube, "Tube");
        [PaletteLook("kit.tube", "forest", "Log Chute", Prefab = ForestKitBuilder.Chute, Tooltip = Forest)]
        static GameObject TubeForest(PlaceContext c) => Place(c, ForestKitBuilder.Chute, "Log chute");

        [PaletteItem("kit.cannon", "Kit/Transit", "Cannon", Kind = Kit, Snap = Floor, Prefab = BombObstacleKitBuilder.Cannon + ".prefab", Tags = "cannon transit")]
        static GameObject Cannon(PlaceContext c) => Place(c, BombObstacleKitBuilder.Cannon, "Cannon");

        // ------------------------------------------------------------------ placing

        /// <summary>
        /// Places the kit prefab <paramref name="prefabPathNoExt"/> on the floor of the context: its colliders' bottom on the floor,
        /// <paramref name="raise"/> metres higher for what hangs (a bridge, a hoop), in the level's Kit group, selected. Returns it.
        /// </summary>
        public static GameObject Place(PlaceContext c, string prefabPathNoExt, string name, float raise = 0f, Action<GameObject> setup = null)
        {
            if (prefabPathNoExt.EndsWith(".prefab")) prefabPathNoExt = prefabPathNoExt.Substring(0, prefabPathNoExt.Length - ".prefab".Length);
            var parent = c.Group(LevelGroup.Kit);
            // the floor clicked or looked at (a deck, a walkway, the ground), found before the new element stands there
            var at = c.Floor();
            var siblings = parent != null ? parent.Cast<Transform>().Select(t => t.name).ToArray() : Array.Empty<string>();
            var go = CourseKit.Place(parent, prefabPathNoExt, ObjectNames.GetUniqueName(siblings, name), at, Quaternion.identity);
            Undo.RegisterCreatedObjectUndo(go, "Add " + name);
            setup?.Invoke(go);
            // stand it on the floor: the bottom of its solid colliders (else its renderers) at the floor, then raised
            Physics.SyncTransforms();
            var bottom = Bottom(go);
            if (bottom.HasValue) go.transform.position += Vector3.up * (at.y - bottom.Value + raise);
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
