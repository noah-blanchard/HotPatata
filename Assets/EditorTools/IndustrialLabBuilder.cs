using System;
using System.Linq;
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
    /// The test map of the industrial look (ARCHITECTURE §25.2, issue #87): about 150 m along +Z with a covered entry, a pit to
    /// jump, a catwalk with a pass, a raw-metal ramp, a rubber pad, a lit covered hall and an open finish, plus a calibration
    /// wall on each side (|x| = 32 m, no colliders) showing one box of 1, 4 and 12 m per material, in KayKit and in the
    /// industrial look, to judge texel density, stretching and repetition. Isolated: not in the menu, the network scene list or
    /// the build settings. Gameplay dimensions follow PatataWorks (10 m floors, 1.5 m pass height, jumps well inside the run).
    /// </summary>
    public static class IndustrialLabBuilder
    {
        public const string ScenePath = "Assets/Scenes/IndustrialLab.unity";
        const string TemplatePath = "Assets/Scenes/PlaytestCourse.unity";   // the template PatataWorks is generated from
        const string GroupName = "IndustrialLab";
        const float Width = 10f;

        [MenuItem("HotPatata/Course/Build Industrial Lab")]
        public static void Build() => Build(LookSet.Industrial);

        [MenuItem("HotPatata/Course/Build Industrial Lab (KayKit baseline)")]
        public static void BuildBaseline() => Build(LookSet.KayKit);

        public static void Build(LookSet set)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            IndustrialMaterialBuilder.Ensure(false);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null && !AssetDatabase.CopyAsset(TemplatePath, ScenePath))
                throw new InvalidOperationException("Could not create " + ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var run = Object.FindFirstObjectByType<RunManager>();
            if (run == null) throw new InvalidOperationException("Course template has no RunManager");
            // The template's run, spawns, bomb, camera and networking are retained; generated geometry is replaced.
            var section = Object.FindObjectsByType<PlayerSpawn>(FindObjectsSortMode.None)
                .First(s => s.GetComponentInParent<Checkpoint>() == null).transform.parent;
            while (section.parent != null) section = section.parent;
            foreach (var child in section.Cast<Transform>().ToArray())
                if (child.GetComponentsInChildren<Checkpoint>(true).Length > 0 || child.GetComponent<KillZone>() != null)
                    Object.DestroyImmediate(child.gameObject);
            foreach (var decoration in scene.GetRootGameObjects().Where(g => g.GetComponentsInChildren<Renderer>(true).Length > 0 &&
                         g.GetComponentsInChildren<MonoBehaviour>(true).Length == 0).ToArray())
                Object.DestroyImmediate(decoration);
            var previous = section.GetComponent<CourseRoute>();
            if (previous != null) Object.DestroyImmediate(previous);

            RebuildGroup(section, GroupName, root =>
            {
                using (UseLookSet(set))
                {
                    Entry(root);
                    Pit(root);
                    Catwalk(root);
                    RampAndPad(root);
                    Hall(root);
                    Finish(root);
                }
                Calibration(root, 32f, LookSet.Industrial);
                Calibration(root, -32f, LookSet.KayKit);
            });
            LookBuilder.ApplyToScene(scene);
            Physics.SyncTransforms();
            PatataWorksBuilder.ValidatePasses();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[IndustrialLabBuilder] Industrial lab built in the {set} look: two checkpoints, covered hall, finish.");
        }

        // ------------------------------------------------------------------ helpers

        static void Floor(Transform p, string name, float a, float b, float y, float width = Width, float x = 0, KitRole role = KitRole.Floor) =>
            Block(p, name, new Vector3(x, y - 0.5f, (a + b) / 2), new Vector3(width, 1, b - a), role);

        static void Pass(Transform p, string name, Vector3 from, Vector3 to, PassCorridor.ArcKind kind = PassCorridor.ArcKind.Normal, float opening = 0)
        {
            var go = new GameObject("Pass " + name);
            go.transform.SetParent(p, false);
            go.transform.position = from;
            go.AddComponent<PassCorridor>().Configure(to, kind, 0, false, opening);
        }

        static void Checkpoint(Transform p, int id, Vector3 position)
        {
            var go = AddCheckpoint(p, $"CP_{id:00}", id, position, 0);
            go.transform.rotation = Quaternion.identity;
        }

        static void Kill(Transform p, Vector3 position, Vector3 size)
        {
            var go = Place(p, GameplayDir + "KillZone", "Pit recovery", position, Quaternion.identity);
            go.transform.localScale = size;
        }

        static void Column(Transform p, string name, float x, float z, float footY, float topY, float thickness)
        {
            float height = topY - footY;
            Block(p, name, new Vector3(x, footY + height / 2, z), new Vector3(thickness, height, thickness), KitRole.Pillar);
            ColumnTrim(p, name + " trim", new Vector3(x, footY, z), height, thickness);
        }

        // ------------------------------------------------------------------ route

        /// <summary>Covered entry: a concrete hall with walls and painted columns, the spawn and the first checkpoint.</summary>
        static void Entry(Transform p)
        {
            Floor(p, "Entry floor", -12, 24, 0);
            foreach (float x in new[] { -5.5f, 5.5f })
                Block(p, "Entry wall", new Vector3(x, 3, 6), new Vector3(1, 6, 36), KitRole.Brick);
            foreach (float z in new[] { 0f, 12f })
                foreach (float x in new[] { -4.7f, 4.7f })
                    Column(p, "Entry column", x, z, 0, 6, 0.6f);
            for (float z = -6; z < 24; z += 6) Seam(p, "Entry seam", 0, 0, z, Width);
            EdgeStrip(p, "Pit edge strip", 0, 0, 23.85f, 0.3f, Width);
            Pass(p, "Entry warmup", new Vector3(-3, 1.5f, 6), new Vector3(3, 1.5f, 14));
            Checkpoint(p, 1, new Vector3(0, 0, 18));
        }

        /// <summary>A 4 m pit between the entry and a concrete deck, over a floor 14 m down.</summary>
        static void Pit(Transform p)
        {
            Floor(p, "Deck", 28, 40, 0);
            EdgeStrip(p, "Deck edge strip", 0, 0, 28.15f, 0.3f, Width);
            Block(p, "Entry pier", new Vector3(0, -7.5f, 19), new Vector3(8, 13, 8), KitRole.Brick);
            Floor(p, "Pit floor", 20, 76, -14, 30, 0);
            Block(p, "Deck pier", new Vector3(0, -7.5f, 34), new Vector3(8, 13, 10), KitRole.Brick);
            Kill(p, new Vector3(0, -9, 48), new Vector3(30, 2, 56));
        }

        /// <summary>A grated catwalk on painted legs, with yellow railings: the pass runs along it.</summary>
        static void Catwalk(Transform p)
        {
            Block(p, "Catwalk deck", new Vector3(0, -0.2f, 56), new Vector3(6, 0.4f, 32), KitRole.Grating);
            foreach (float x in new[] { -2.9f, 2.9f })
                Block(p, "Catwalk railing", new Vector3(x, 0.55f, 56), new Vector3(0.15f, 1.1f, 32), KitRole.Railing);
            foreach (float z in new[] { 44f, 64f })
            {
                foreach (float x in new[] { -3.3f, 3.3f }) Column(p, "Catwalk leg", x, z, -14, -0.4f, 0.4f);
                Detail(p, "Catwalk crossbeam", new Vector3(0, -0.55f, z), new Vector3(7f, 0.3f, 0.4f), KitRole.Truss);
            }
            foreach (float x in new[] { -3.3f, 3.3f })
            {
                Brace(p, "Catwalk brace", new Vector3(x, -13f, 44), new Vector3(x, -1f, 64));
                Brace(p, "Catwalk brace", new Vector3(x, -13f, 64), new Vector3(x, -1f, 44));
            }
            Beam(p, "Catwalk beam", new Vector3(-2.2f, -0.6f, 56), 32);
            Beam(p, "Catwalk beam", new Vector3(2.2f, -0.6f, 56), 32);
            Pass(p, "Catwalk handoff", new Vector3(0, 1.5f, 44), new Vector3(0, 1.5f, 56));
        }

        /// <summary>A raw-metal ramp up two metres, then a rubber slab.</summary>
        static void RampAndPad(Transform p)
        {
            var a = new Vector3(0, 0, 72);
            var b = new Vector3(0, 2, 84);
            var rotation = Quaternion.LookRotation(b - a, Vector3.up);
            Block(p, "Metal ramp", (a + b) / 2 - rotation * Vector3.up * 0.5f, new Vector3(6, 1, Vector3.Distance(a, b)), KitRole.Grating, rotation);
            Block(p, "Rubber pad", new Vector3(0, 1.5f, 90), new Vector3(8, 1, 12), KitRole.Rubber);
            EdgeStrip(p, "Rubber pad strip", -3.85f, 2, 90, 12, 0.2f);
            EdgeStrip(p, "Rubber pad strip", 3.85f, 2, 90, 12, 0.2f);
        }

        /// <summary>The covered hall (a ceiling, warm lamps, painted columns and beams): the indoor light test.</summary>
        static void Hall(Transform p)
        {
            const float y = 2f, ceiling = 11.5f;
            Floor(p, "Hall floor", 96, 130, y);
            foreach (float x in new[] { -5.5f, 5.5f })
                Block(p, "Hall wall", new Vector3(x, y + 6, 113), new Vector3(1, 12, 34), KitRole.Brick);
            Block(p, "Hall ceiling", new Vector3(0, y + ceiling, 113), new Vector3(12, 1, 36), KitRole.Ceiling).AddComponent<CourseCeiling>();
            foreach (float z in new[] { 100f, 112f, 124f })
            {
                foreach (float x in new[] { -4.6f, 4.6f }) Column(p, "Hall column", x, z, y, y + ceiling - 0.5f, 0.6f);
                LookBuilder.FactoryLamp(p, new Vector3(0, y + 8, z));
            }
            Beam(p, "Hall beam", new Vector3(-4.6f, y + ceiling - 0.9f, 113), 34, 0.5f);
            Beam(p, "Hall beam", new Vector3(4.6f, y + ceiling - 0.9f, 113), 34, 0.5f);
            for (float z = 100; z < 130; z += 6) Seam(p, "Hall seam", 0, y, z, Width);
            Pass(p, "Hall relay", new Vector3(-3, 3.5f, 102), new Vector3(3, 3.5f, 114));
            Checkpoint(p, 2, new Vector3(0, y, 100));
        }

        /// <summary>The open finish under the sun.</summary>
        static void Finish(Transform p)
        {
            const float y = 2f;
            Floor(p, "Finish floor", 130, 152, y);
            foreach (float x in new[] { -4.9f, 4.9f })
                Block(p, "Finish railing", new Vector3(x, y + 0.55f, 141), new Vector3(0.15f, 1.1f, 20), KitRole.Railing);
            EdgeStrip(p, "Finish edge strip", 0, y, 151.85f, 0.3f, Width);
            Place(p, GameplayDir + "FinishZone", "FinishZone", new Vector3(0, y, 144), Quaternion.identity);
        }

        // ------------------------------------------------------------------ calibration wall

        /// <summary>
        /// One panel of 1 m, one of 4 m and one of 12 m per material along a collider-free wall facing the route: the same
        /// surface at three sizes shows at a glance whether the texture keeps one density and never stretches.
        /// </summary>
        static void Calibration(Transform root, float x, LookSet set)
        {
            var wall = new GameObject("Calibration wall " + set).transform;
            wall.SetParent(root, false);
            wall.gameObject.AddComponent<CourseDecoration>();
            float side = Mathf.Sign(x);
            float z = 0;
            using (UseLookSet(set))
            {
                Detail(wall, "Calibration floor", new Vector3(x, -0.5f, 46), new Vector3(8, 1, 96), KitRole.Floor);
                foreach (var role in new[] { KitRole.Floor, KitRole.Truss, KitRole.Grating, KitRole.Rubber })
                {
                    foreach (var (w, h) in new[] { (1f, 1f), (4f, 4f), (12f, 6f) })
                    {
                        Detail(wall, $"Calibration {role} {w:0}m", new Vector3(x + side * 1f, h / 2, z + w / 2), new Vector3(1, h, w), role);
                        z += w + 1.5f;
                    }
                    z += 1.5f;
                }
            }
        }
    }
}
