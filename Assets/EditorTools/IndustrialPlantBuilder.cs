using System;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static HotPatata.Editor.CourseKit;
using static HotPatata.Editor.IndustrialKit;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// The industrial showcase map (ARCHITECTURE §25.2, issue #87): a closed, dark plant under one roof, about 190 m along +Z.
    /// Three rooms linked by two bridges over a deep pit: a low entry hall with a belt, a double-height machine hall with a crane
    /// platform over a void and a ramp up, an elevated bridge, and a furnace room with the finish. Concrete walls and roof (two
    /// slits over the hall let a few sun shafts in), warm practical lights on a very low ambient. The shell is solid: the plant
    /// has no exit but the finish. Everything is the industrial look; gameplay dimensions follow PatataWorks.
    /// </summary>
    public static class IndustrialPlantBuilder
    {
        public const string ScenePath = "Assets/Scenes/IndustrialPlant.unity";
        const string GroupName = "IndustrialPlant";
        const float HalfWidth = 11.5f;                  // inside face of the outer walls
        const float PitFloor = -16f, KillY = -12f;       // the plant's foundation, and the recovery zone above it
        const float RoofY = 22f;                         // underside of the outer roof
        const float Z0 = -12f, Z1 = 192f;                // inside faces of the end walls

        static readonly Color Warm = new Color(1f, 0.66f, 0.34f);
        static readonly Color Furnace = new Color(1f, 0.34f, 0.1f);
        static readonly Color Cold = new Color(0.62f, 0.78f, 1f);

        [MenuItem("HotPatata/Course/Build Industrial Plant")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            IndustrialMaterialBuilder.Ensure(false);
            var scene = IndustrialLabBuilder.PrepareScene(ScenePath, out var section);
            RebuildGroup(section, GroupName, root =>
            {
                using (UseLookSet(LookSet.Industrial))
                {
                    Shell(root);
                    EntryHall(root);
                    BridgeA(root);
                    MachineHall(root);
                    RampAndLanding(root);
                    BridgeB(root);
                    FurnaceRoom(root);
                }
            });
            LookBuilder.ApplyToScene(scene);
            Physics.SyncTransforms();
            PatataWorksBuilder.ValidatePasses();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[IndustrialPlantBuilder] Industrial plant built: closed, three checkpoints, two bridges, crane, furnace room, finish.");
        }

        // ------------------------------------------------------------------ helpers

        static void Column(Transform p, string name, float x, float z, float footY, float topY, float thickness) => IndustrialLabBuilder.Column(p, name, x, z, footY, topY, thickness);
        static void Pass(Transform p, string name, Vector3 from, Vector3 to) => IndustrialLabBuilder.Pass(p, name, from, to);
        static void Cp(Transform p, int id, Vector3 position) => IndustrialLabBuilder.Checkpoint(p, id, position);
        static void Kill(Transform p, Vector3 position, Vector3 size) => IndustrialLabBuilder.Kill(p, position, size);

        /// <summary>A solid concrete mass from the pit floor up to <paramref name="top"/> (no floating slab, nothing to see under it).</summary>
        static void Solid(Transform p, string name, float zA, float zB, float top, float width = 2 * HalfWidth, float x = 0, KitRole role = KitRole.Floor)
        {
            float height = top - PitFloor;
            Block(p, name, new Vector3(x, PitFloor + height / 2, (zA + zB) / 2), new Vector3(width, height, zB - zA), role);
        }

        /// <summary>A hanging lamp: chain, shade, glowing bulb, and the light itself. Kept clear of the throw lanes by the caller.</summary>
        static void Lamp(Transform p, Vector3 position, float dropFrom, Color color, float intensity, float range = 24f)
        {
            if (dropFrom > position.y + 0.3f)
                Detail(p, "Lamp chain", new Vector3(position.x, (position.y + dropFrom) / 2, position.z), new Vector3(0.06f, dropFrom - position.y, 0.06f), KitRole.Truss);
            Detail(p, "Lamp shade", position + Vector3.up * 0.18f, new Vector3(1.1f, 0.22f, 1.1f), KitRole.Truss);
            Detail(p, "Lamp bulb", position, new Vector3(0.7f, 0.14f, 0.7f), KitRole.Lamp);
            LookBuilder.PracticalLamp(p, position - Vector3.up * 0.6f, color, intensity, range, "Lamp light");
        }

        /// <summary>A wall-mounted lamp (a cage light on a bracket) facing the floor.</summary>
        static void WallLamp(Transform p, float x, float y, float z, Color color, float intensity)
        {
            float side = Mathf.Sign(x);
            Detail(p, "Wall lamp bracket", new Vector3(x - side * 0.25f, y, z), new Vector3(0.5f, 0.1f, 0.1f), KitRole.Truss);
            Detail(p, "Wall lamp bulb", new Vector3(x - side * 0.55f, y, z), new Vector3(0.22f, 0.22f, 0.22f), KitRole.Lamp);
            LookBuilder.PracticalLamp(p, new Vector3(x - side * 1.2f, y - 0.3f, z), color, intensity, 16f, "Wall lamp light");
        }

        /// <summary>A duct or pipe run along a wall (decoration).</summary>
        static void Duct(Transform p, float x, float y, float zA, float zB, float size = 0.5f)
        {
            Detail(p, "Duct", new Vector3(x, y, (zA + zB) / 2), new Vector3(size, size, zB - zA), KitRole.Grating);
            for (float z = zA + 2; z < zB; z += 6) Detail(p, "Duct clamp", new Vector3(x, y, z), new Vector3(size + 0.12f, size + 0.12f, 0.12f), KitRole.Truss);
        }

        /// <summary>A roof truss across the room (decoration), under the slab it hangs from.</summary>
        static void CrossTruss(Transform p, float y, float z, float width = 2 * HalfWidth - 0.4f)
        {
            Detail(p, "Roof truss", new Vector3(0, y, z), new Vector3(width, 0.5f, 0.35f), KitRole.Truss);
            Detail(p, "Roof truss flange", new Vector3(0, y + 0.3f, z), new Vector3(width, 0.06f, 0.7f), KitRole.Truss);
        }

        // ------------------------------------------------------------------ shell

        /// <summary>The closed building: foundation, outer walls and end walls up to the roof, the roof with two slits over the hall, wall pilasters.</summary>
        static void Shell(Transform p)
        {
            float length = Z1 - Z0, mid = (Z0 + Z1) / 2;
            Block(p, "Foundation", new Vector3(0, PitFloor - 0.5f, mid), new Vector3(2 * HalfWidth + 2, 1, length + 2), KitRole.Floor);
            float wallBottom = PitFloor - 1, wallTop = RoofY + 1;
            foreach (float x in new[] { -HalfWidth - 0.5f, HalfWidth + 0.5f })
                Block(p, "Outer wall", new Vector3(x, (wallBottom + wallTop) / 2, mid), new Vector3(1, wallTop - wallBottom, length + 2), KitRole.Brick);
            foreach (float z in new[] { Z0 - 0.5f, Z1 + 0.5f })
                Block(p, "End wall", new Vector3(0, (wallBottom + wallTop) / 2, z), new Vector3(2 * HalfWidth + 2, wallTop - wallBottom, 1), KitRole.Brick);
            // Roof: solid except two long slits over the hall (z 60..124) where the low sun comes in as shafts.
            Roof(p, Z0, 60, -HalfWidth, HalfWidth);
            Roof(p, 124, Z1, -HalfWidth, HalfWidth);
            foreach (var (xa, xb) in new[] { (-HalfWidth, -6.5f), (-3.5f, 3.5f), (6.5f, HalfWidth) })
                Roof(p, 60, 124, xa, xb);
            // Wall pilasters on the inside faces (collidable, out of the throw lanes), and cable ducts.
            for (float z = 6; z < Z1; z += 14)
                foreach (float x in new[] { -HalfWidth + 0.5f, HalfWidth - 0.5f })
                    if (z < 36 || z > 56) Block(p, "Wall pilaster", new Vector3(x, (PitFloor + RoofY) / 2, z), new Vector3(1, RoofY - PitFloor, 1), KitRole.Pillar);
            foreach (float x in new[] { -HalfWidth + 0.45f, HalfWidth - 0.45f })
            {
                Duct(p, x, 7.4f, 0, 34);
                Duct(p, x, 12.5f, 64, 120, 0.7f);
                Duct(p, x, 9.4f, 130, 188, 0.6f);
            }
        }

        static void Roof(Transform p, float zA, float zB, float xA, float xB) =>
            Block(p, "Roof", new Vector3((xA + xB) / 2, RoofY + 0.5f, (zA + zB) / 2), new Vector3(xB - xA, 1, zB - zA), KitRole.Ceiling).AddComponent<CourseCeiling>();

        // ------------------------------------------------------------------ rooms

        /// <summary>Low entry hall: concrete floor, a rubber belt along one side, steel columns, a false ceiling hung with warm lamps.</summary>
        static void EntryHall(Transform p)
        {
            const float ceiling = 9f;
            Solid(p, "Entry floor", Z0, 36, 0, 13.5f, 4.75f);                          // x -2..11.5: the spawn lane
            Solid(p, "Entry west floor", Z0, 36, 0, 3.5f, -9.75f);                      // x -11.5..-8
            Solid(p, "Entry belt start", Z0, 2, 0, 6, -5f);
            Solid(p, "Entry belt end", 32, 36, 0, 6, -5f);
            Solid(p, "Belt bed", 2, 32, -1, 6, -5f);
            AddConveyor(p, "Entry belt", new Vector3(-5, -0.5f, 17), new Vector3(6, 1, 30), 2f);   // x -8..-2
            Block(p, "Entry ceiling", new Vector3(0, ceiling + 0.5f, 12), new Vector3(2 * HalfWidth, 1, 48), KitRole.Ceiling).AddComponent<CourseCeiling>();
            foreach (float z in new[] { 0f, 12f, 24f })
                foreach (float x in new[] { -9.5f, 9.5f })
                    Column(p, "Entry column", x, z, 0, ceiling, 0.7f);
            foreach (float z in new[] { 0f, 8f, 16f, 24f, 32f })
            {
                CrossTruss(p, ceiling - 0.4f, z);
                Lamp(p, new Vector3(z % 16 == 0 ? -5f : 5f, ceiling - 1.6f, z), ceiling - 0.4f, Warm, 15f, 24f);
            }
            for (float z = -6; z < 36; z += 6) Seam(p, "Entry seam", 4.75f, 0, z, 13.5f);
            EdgeStrip(p, "Bridge edge strip", 0, 0, 35.85f, 0.3f, 2 * HalfWidth);
            Pass(p, "Entry warmup", new Vector3(3, 1.5f, 4), new Vector3(8, 1.5f, 14));
            Cp(p, 1, new Vector3(5, 0, 24));
        }

        /// <summary>Bridge A: 24 m of grated deck over the pit, painted legs and braces, a pass along it.</summary>
        static void BridgeA(Transform p)
        {
            Block(p, "Bridge A deck", new Vector3(0, -0.2f, 48), new Vector3(5, 0.4f, 24), KitRole.Grating);
            foreach (float x in new[] { -2.4f, 2.4f })
                Block(p, "Bridge A railing", new Vector3(x, 0.55f, 48), new Vector3(0.15f, 1.1f, 24), KitRole.Railing);
            foreach (float z in new[] { 40f, 56f })
            {
                foreach (float x in new[] { -2.9f, 2.9f }) Column(p, "Bridge A leg", x, z, PitFloor, -0.4f, 0.45f);
                Detail(p, "Bridge A crossbeam", new Vector3(0, -0.55f, z), new Vector3(6f, 0.3f, 0.4f), KitRole.Truss);
            }
            foreach (float x in new[] { -2.9f, 2.9f })
            {
                Brace(p, "Bridge A brace", new Vector3(x, PitFloor + 1, 40), new Vector3(x, -1f, 56));
                Brace(p, "Bridge A brace", new Vector3(x, PitFloor + 1, 56), new Vector3(x, -1f, 40));
            }
            Beam(p, "Bridge A beam", new Vector3(-1.7f, -0.6f, 48), 24);
            Beam(p, "Bridge A beam", new Vector3(1.7f, -0.6f, 48), 24);
            for (float z = 38; z <= 58; z += 5) Lamp(p, new Vector3(z % 10 == 8 ? -3f : 3f, 4.5f, z), 8.6f, Warm, 11f, 20f);
            Kill(p, new Vector3(0, KillY, 82), new Vector3(2 * HalfWidth, 2, 200));
            Pass(p, "Bridge A relay", new Vector3(0, 1.5f, 40), new Vector3(0, 1.5f, 50));
        }

        /// <summary>Machine hall: a fixed floor, a crane platform across a void, a machinery floor with a belt and big machines along the walls.</summary>
        static void MachineHall(Transform p)
        {
            const float ceiling = RoofY - 0.5f;
            Solid(p, "Hall landing", 60, 72, 0);
            var crane = AddMover(p, "Platform_Moving", "Hall crane", new Vector3(0, -0.5f, 74.5f), new Vector3(0, -0.5f, 81.5f), new Vector3(6, 1, 5), 2f, 0f, 0.5f);
            Skin(crane.transform.Find("Platform/Visual").gameObject, KitRole.Mover);   // painted steel, not the KayKit blue
            Solid(p, "Hall machinery floor", 84, 112, 0);
            foreach (float x in new[] { -HalfWidth + 3.5f, HalfWidth - 3.5f })
            {
                foreach (float z in new[] { 90f, 104f })
                {
                    Block(p, "Machine", new Vector3(x, 3, z), new Vector3(6, 6, 8), KitRole.Pillar);
                    Detail(p, "Machine hatch", new Vector3(x - Mathf.Sign(x) * 0.0f, 3.8f, z), new Vector3(6.04f, 1.6f, 4f), KitRole.Rubber);
                    Detail(p, "Machine lamp", new Vector3(x + (x < 0 ? 3.1f : -3.1f), 5.2f, z), new Vector3(0.2f, 0.2f, 0.5f), KitRole.Lamp);
                }
                Column(p, "Hall column", x, 66, 0, ceiling, 1f);
                Column(p, "Hall column", x, 78, PitFloor, ceiling, 1f);
                Column(p, "Hall column", x, 118, PitFloor, ceiling, 1f);
            }
            for (float z = 62; z < 124; z += 10)
            {
                CrossTruss(p, ceiling - 0.4f, z);
                Lamp(p, new Vector3(z % 20 == 2 ? -6f : 6f, 14.5f, z), ceiling - 0.4f, Warm, 12f, 30f);
            }
            for (float z = 66; z < 124; z += 14)
                foreach (float x in new[] { -HalfWidth, HalfWidth })
                    WallLamp(p, x, 6.5f, z, Cold, 5f);
            for (float z = 62; z < 112; z += 6) Seam(p, "Hall seam", 0, 0, z, 2 * HalfWidth);
            EdgeStrip(p, "Hall landing strip", 0, 0, 71.85f, 0.3f, 2 * HalfWidth);
            EdgeStrip(p, "Hall floor strip", 0, 0, 84.15f, 0.3f, 2 * HalfWidth);
            Pass(p, "Hall crane relay", new Vector3(0, 1.5f, 68), new Vector3(0, 1.5f, 78));
            Pass(p, "Hall machinery relay", new Vector3(-2, 1.5f, 92), new Vector3(2, 1.5f, 102));
            Cp(p, 2, new Vector3(0, 0, 88));
        }

        /// <summary>A raw-metal ramp up to the bridge landing (4 m), on painted legs over the pit.</summary>
        static void RampAndLanding(Transform p)
        {
            var a = new Vector3(0, 0, 112);
            var b = new Vector3(0, 4, 128);
            var rotation = Quaternion.LookRotation(b - a, Vector3.up);
            Block(p, "Hall ramp", (a + b) / 2 - rotation * Vector3.up * 0.5f, new Vector3(7, 1, Vector3.Distance(a, b)), KitRole.Grating, rotation);
            foreach (var (z, top) in new[] { (118f, 1.2f), (124f, 2.8f) })
                foreach (float x in new[] { -3.2f, 3.2f }) Column(p, "Ramp leg", x, z, PitFloor, top - 0.9f, 0.45f);
            foreach (float x in new[] { -3.7f, 3.7f })
                Block(p, "Ramp curb", (a + b) / 2 + Vector3.up * 0.2f + Vector3.right * x, new Vector3(0.12f, 0.5f, Vector3.Distance(a, b)), KitRole.Railing, rotation);
            Solid(p, "Bridge landing", 128, 134, 4);
            Lamp(p, new Vector3(0, 11.5f, 120), 21.6f, Warm, 10f, 26f);
            Cp(p, 3, new Vector3(-1, 4, 131));
        }

        /// <summary>Bridge B: an elevated truss bridge (24 m) between the machine hall and the furnace room, with a pass along it.</summary>
        static void BridgeB(Transform p)
        {
            Block(p, "Bridge B deck", new Vector3(0, 3.8f, 146), new Vector3(6, 0.4f, 24), KitRole.Grating);
            foreach (float x in new[] { -2.9f, 2.9f })
                Block(p, "Bridge B railing", new Vector3(x, 4.55f, 146), new Vector3(0.15f, 1.1f, 24), KitRole.Railing);
            foreach (float z in new[] { 138f, 154f })
            {
                foreach (float x in new[] { -3.4f, 3.4f }) Column(p, "Bridge B leg", x, z, PitFloor, 3.4f, 0.5f);
                Detail(p, "Bridge B crossbeam", new Vector3(0, 3.45f, z), new Vector3(7.2f, 0.34f, 0.45f), KitRole.Truss);
            }
            foreach (float x in new[] { -3.4f, 3.4f })
            {
                Brace(p, "Bridge B brace", new Vector3(x, PitFloor + 1, 138), new Vector3(x, 3f, 154));
                Brace(p, "Bridge B brace", new Vector3(x, PitFloor + 1, 154), new Vector3(x, 3f, 138));
            }
            Beam(p, "Bridge B beam", new Vector3(-2f, 3.3f, 146), 24, 0.5f);
            Beam(p, "Bridge B beam", new Vector3(2f, 3.3f, 146), 24, 0.5f);
            foreach (float z in new[] { 136f, 146f, 156f })
                Lamp(p, new Vector3(z % 20 == 6 ? -4f : 4f, 11f, z), 21.6f, Warm, 10f, 24f);
            Pass(p, "Bridge B relay", new Vector3(0, 5.5f, 136), new Vector3(0, 5.5f, 146));
        }

        /// <summary>Furnace room: low ceiling, orange glow, a steel gantry; the finish at the far end.</summary>
        static void FurnaceRoom(Transform p)
        {
            const float floor = 4f, ceiling = 14f;
            Solid(p, "Furnace floor", 158, Z1, floor);
            Block(p, "Furnace ceiling", new Vector3(0, ceiling + 0.5f, 175), new Vector3(2 * HalfWidth, 1, 34), KitRole.Ceiling).AddComponent<CourseCeiling>();
            foreach (float z in new[] { 166f, 180f })
                foreach (float x in new[] { -HalfWidth + 3f, HalfWidth - 3f })
                {
                    Block(p, "Furnace casing", new Vector3(x, floor + 3, z), new Vector3(5, 6, 7), KitRole.Pillar);
                    LookBuilder.PracticalLamp(p, new Vector3(x + (x < 0 ? 3.2f : -3.2f), floor + 1.6f, z), Furnace, 16f, 22f, "Furnace glow");
                    Detail(p, "Furnace mouth", new Vector3(x + (x < 0 ? 2.52f : -2.52f), floor + 1.6f, z), new Vector3(0.05f, 1.2f, 1.8f), KitRole.Glow);
                }
            for (float z = 160; z < Z1; z += 8) CrossTruss(p, ceiling - 0.4f, z);
            foreach (float z in new[] { 162f, 172f, 184f }) Lamp(p, new Vector3(0, ceiling - 1.8f, z), ceiling - 0.4f, Warm, 9f, 20f);
            foreach (float z in new[] { 162f, 174f, 186f })
                foreach (float x in new[] { -9.5f, 9.5f }) Column(p, "Furnace column", x, z, floor, ceiling, 0.7f);
            for (float z = 160; z < Z1; z += 6) Seam(p, "Furnace seam", 0, floor, z, 2 * HalfWidth);
            EdgeStrip(p, "Furnace edge strip", 0, floor, 158.15f, 0.3f, 2 * HalfWidth);
            Pass(p, "Furnace relay", new Vector3(-2, 5.5f, 162), new Vector3(2, 5.5f, 172));
            Place(p, GameplayDir + "FinishZone", "FinishZone", new Vector3(0, floor, 184), Quaternion.identity);
        }
    }
}
