using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata.Editor
{
    /// <summary>
    /// Checks a built course against its section contracts (PROJECT_SPEC §13.20): every shortcut a <see cref="SectionContract"/>
    /// declares locked must be out of the player's or the bomb's reach (<see cref="SectionContract.MaxGap"/>,
    /// <see cref="SectionContract.MaxClimb"/>, <see cref="SectionContract.MaxLobHeight"/>), and two scans run on every course:
    /// no player can step round the end of a body screen or hop over it, and no bomb can fly round or over a laser curtain.
    /// The findings are warnings (<c>CourseContractTests</c>, menu <b>HotPatata/Course/Check Section Contracts</b>): a design
    /// review aid, never a gate.
    /// </summary>
    public static class CourseContractCheck
    {
        const string TuningPath = "Assets/ScriptableObjects/Tuning/GameTuning.asset";
        const float BodyRadius = 0.35f, BodyHeight = 1.8f, BombProbe = 0.25f, FramePost = 0.35f, CurtainPost = 0.3f;

        public static GameTuning Tuning => AssetDatabase.LoadAssetAtPath<GameTuning>(TuningPath);

        /// <summary>Every finding in <paramref name="scene"/> (empty: every contract holds).</summary>
        public static List<string> Problems(Scene scene, GameTuning tuning)
        {
            Physics.SyncTransforms();
            var problems = new List<string>();
            int solid = LayerMask.GetMask("Environment", "Hazard");
            int body = LayerMask.GetMask("Environment", "Hazard", BodyScreen.LayerName);
            var roots = scene.GetRootGameObjects();

            foreach (var contract in roots.SelectMany(r => r.GetComponentsInChildren<SectionContract>(true)))
                foreach (var s in contract.Shortcuts)
                {
                    string where = $"{Path(contract.transform)} '{s.name}'";
                    Vector3 from = contract.From(s), to = contract.To(s);
                    switch (s.kind)
                    {
                        case SectionContract.ShortcutKind.Gap:
                        {
                            float gap = Flat(to - from), rise = to.y - from.y, reach = SectionContract.MaxGap(tuning, rise);
                            if (gap <= reach) problems.Add($"{where}: a {gap:F1} m gap (rise {rise:F1} m) is jumpable, a slide-jump with a mantle reaches {reach:F1} m");
                            break;
                        }
                        case SectionContract.ShortcutKind.Climb:
                        {
                            float rise = to.y - from.y, reach = SectionContract.MaxClimb(tuning);
                            if (rise <= reach) problems.Add($"{where}: a {rise:F1} m ledge is climbable, a jump and a mantle reach {reach:F1} m");
                            break;
                        }
                        case SectionContract.ShortcutKind.Lob:
                        {
                            // from just inside the wall's top (a ray ignores the collider it starts in), so a roof flush with the top counts
                            if (Ray(scene, to - Vector3.up * 0.3f, Vector3.up, SectionContract.SealGap + 0.3f, solid)) break;
                            float need = to.y - (from.y + SectionContract.HandHeight) + BombProbe;
                            float reach = SectionContract.MaxLobHeight(tuning, Flat(to - from));
                            if (reach >= need) problems.Add($"{where}: the wall top is not sealed by a roof, a lob clears it by {reach - need:F1} m");
                            break;
                        }
                    }
                }

            foreach (var screen in roots.SelectMany(r => r.GetComponentsInChildren<BodyScreen>(false)))
                foreach (var box in screen.GetComponentsInChildren<BoxCollider>(false).Where(b => b.gameObject.layer == LayerMask.NameToLayer(BodyScreen.LayerName)))
                {
                    Frame(box, out var center, out var right, out var half, out float bottom, out float height);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 beside = center + right * side * (half.x + FramePost + BodyRadius + 0.1f);   // past the frame's post
                        beside.y = bottom;
                        bool floor = Ray(scene, beside + Vector3.up * 0.5f, Vector3.down, 1.5f, solid);
                        bool free = !Capsule(scene, beside + Vector3.up * (BodyRadius + 0.05f), beside + Vector3.up * (BodyHeight - BodyRadius),
                                             BodyRadius - 0.05f, body);
                        if (floor && free) problems.Add($"{Path(screen.transform)}: a player walks round its {(side < 0 ? "left" : "right")} end");
                    }
                    if (height < tuning.jumpHeight + 0.4f) problems.Add($"{Path(screen.transform)}: {height:F1} m tall, a player jumps onto it");
                }

            foreach (var zone in roots.SelectMany(r => r.GetComponentsInChildren<Zone>(false)).Where(z => z.GetComponent<BombBarrier>() != null))
            {
                Frame(zone.Volume, out var center, out var right, out var half, out float bottom, out float height);
                Vector3 mid = center;
                mid.y = bottom + height / 2f;
                for (int side = -1; side <= 1; side += 2)
                    if (!Sphere(scene, mid + right * side * (half.x + CurtainPost + BombProbe + 0.05f), BombProbe, solid))   // past the post
                        problems.Add($"{Path(zone.transform)}: the bomb flies round its {(side < 0 ? "left" : "right")} end");
                Vector3 top = center;
                top.y = bottom + height + CurtainPost + BombProbe + 0.05f;   // above the lintel
                if (!Sphere(scene, top, BombProbe, solid))
                    problems.Add($"{Path(zone.transform)}: the bomb flies over it");
            }
            return problems;
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        // Queries that only see the course under test, so another scene open in the Editor never answers for it.
        static readonly Collider[] Buffer = new Collider[64];

        static bool Ray(Scene scene, Vector3 from, Vector3 dir, float length, int mask) =>
            Physics.RaycastAll(from, dir, length, mask, QueryTriggerInteraction.Ignore).Any(h => h.collider.gameObject.scene == scene);

        static bool Capsule(Scene scene, Vector3 a, Vector3 b, float radius, int mask)
        {
            int n = Physics.OverlapCapsuleNonAlloc(a, b, radius, Buffer, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (Buffer[i].gameObject.scene == scene) return true;
            return false;
        }

        static bool Sphere(Scene scene, Vector3 c, float radius, int mask)
        {
            int n = Physics.OverlapSphereNonAlloc(c, radius, Buffer, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (Buffer[i].gameObject.scene == scene) return true;
            return false;
        }

        /// <summary>A box collider's world centre, across axis, half size, bottom and height.</summary>
        static void Frame(BoxCollider box, out Vector3 center, out Vector3 right, out Vector3 half, out float bottom, out float height)
        {
            var t = box.transform;
            Vector3 s = t.lossyScale;
            half = Vector3.Scale(box.size * 0.5f, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
            center = t.TransformPoint(box.center);
            right = t.right;
            height = half.y * 2f;
            bottom = center.y - half.y;
        }

        static string Path(Transform t)
        {
            var names = new List<string>();
            for (; t != null; t = t.parent) names.Insert(0, t.name);
            return string.Join("/", names);
        }

        [MenuItem("HotPatata/Course/Check Section Contracts")]
        static void CheckOpenScenes()
        {
            var tuning = Tuning;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                var problems = Problems(scene, tuning);
                foreach (var p in problems) Debug.LogWarning($"[CourseContract] {scene.name}: {p}");
                Debug.Log($"[CourseContract] {scene.name}: {problems.Count} finding(s)");
            }
        }
    }
}
