using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using static HotPatata.Editor.CourseKit;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// The fine detail of the industrial look (ARCHITECTURE §25.2): mouldings (baseboards, dado rails, wall panels, cornices,
    /// pilasters with bases and capitals, steel angles on slab edges, bolts), door and window frames, and props along the walls
    /// (pipes with flanges and valves, cabinets, cable trays, conduits, signs, zone numbers, barrels, pallets, workbenches).
    /// <para>
    /// Everything is a collider-free <see cref="CourseDecoration"/>. A room is dressed after it is built, from what its colliders
    /// say: a wall run is a stretch of static wall with a static floor at its foot and nothing in front of it, so a detail never
    /// sits on a mover, in a trigger, against an obstacle or in mid-air. Nothing comes near an intended pass
    /// (<see cref="PassCorridor.DecorationClearance"/> plus a margin). Seeded per room: a rebuild gives the same plant.
    /// </para>
    /// </summary>
    public static class IndustrialDetails
    {
        const float Step = 0.5f;            // wall sampling step (m)
        const float Band = 0.92f;           // depth in front of a wall that props may use (m)
        const float PassMargin = 0.35f;     // on top of PassCorridor.DecorationClearance

        /// <summary>A stretch of wall in a frame where the wall's face is the plane x = side * half, the room on the other side.</summary>
        public struct Run
        {
            public int side;
            public float half, z0, z1, floor, top;   // top: the first thing above the floor at the wall (a catwalk, the ceiling)
            public bool upper;                       // the wall is clear up to floor + 7.6 m (windows, zone numbers)
            public float Length => z1 - z0;
        }

        /// <summary>What a frame may hold: the plant dresses everything, the menu hall has its own columns.</summary>
        public struct Options
        {
            public bool pilasters, windows, zoneNumbers;
            public int zone;
            public float density;                    // 0..1: share of wall spans that get a prop
        }

        static Transform parent;
        static Random rng;
        static List<Bounds> passBoxes;
        static List<List<Vector3>> passPoints;
        static int count;
        static readonly HashSet<int> zoneSides = new HashSet<int>();   // one zone number per wall of a room

        // ------------------------------------------------------------------ passes

        /// <summary>The sampled arcs of every intended pass in the open scenes (set once before dressing).</summary>
        public static void CollectPasses()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/ScriptableObjects/Tuning/GameTuning.asset");
            passBoxes = new List<Bounds>();
            passPoints = new List<List<Vector3>>();
            foreach (var pass in Object.FindObjectsByType<PassCorridor>(FindObjectsSortMode.None))
            {
                var points = new List<Vector3>();
                if (tuning == null || !pass.TrySample(tuning, points) || points.Count == 0) continue;
                var box = new Bounds(points[0], Vector3.zero);
                foreach (var p in points) box.Encapsulate(p);
                box.Expand(2f * (PassCorridor.DecorationClearance + PassMargin));
                passBoxes.Add(box);
                passPoints.Add(points);
            }
        }

        public static void ClearPasses() { passBoxes = null; passPoints = null; }

        static bool NearPass(Bounds world)
        {
            if (passBoxes == null) return false;
            float limit = PassCorridor.DecorationClearance + PassMargin;
            for (int i = 0; i < passBoxes.Count; i++)
            {
                if (!passBoxes[i].Intersects(world)) continue;
                foreach (var p in passPoints[i])
                    if ((world.ClosestPoint(p) - p).sqrMagnitude < limit * limit) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ pieces

        static Bounds WorldBounds(Vector3 center, Vector3 size, Quaternion rotation)
        {
            var b = new Bounds(parent.TransformPoint(center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
                b.Encapsulate(parent.TransformPoint(center + rotation * Vector3.Scale(corner, size)));
            }
            return b;
        }

        static GameObject Box(string name, Vector3 center, Vector3 size, KitRole role, Quaternion? rotation = null)
        {
            if (NearPass(WorldBounds(center, size, rotation ?? Quaternion.identity))) return null;
            count++;
            return IndustrialKit.Detail(parent, name, center, size, role, rotation);
        }

        static GameObject Box(string name, Vector3 center, Vector3 size, Material mat)
        {
            if (NearPass(WorldBounds(center, size, Quaternion.identity))) return null;
            var go = Cube(name, parent, center, size, mat, "Default");
            Skin(go, KitShape.BevelBox, KitColor.Neutral, mat);
            go.AddComponent<CourseDecoration>();
            count++;
            return go;
        }

        /// <summary>A cylinder of <paramref name="radius"/> and <paramref name="length"/> along <paramref name="axis"/> (0 = x, 1 = y, 2 = z).</summary>
        static GameObject Cylinder(string name, Vector3 center, float radius, float length, int axis, KitRole role)
        {
            var rotation = axis == 0 ? Quaternion.Euler(0, 0, 90) : axis == 2 ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
            var scale = new Vector3(2 * radius, length / 2, 2 * radius);
            if (NearPass(WorldBounds(center, new Vector3(2 * radius, length, 2 * radius), rotation))) return null;
            var go = Shape(name, PrimitiveType.Cylinder, parent, center, rotation, scale, Mat(Look(role).material), "Default");
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            go.AddComponent<CourseDecoration>();
            count++;
            return go;
        }

        /// <summary>A sign or a painted number on a wall: an alpha quad just off the wall's face.</summary>
        static void WallSign(Run run, string name, Material mat, float z, float y, float width, float aspect, bool pointsForward = false)
        {
            var normal = new Vector3(-run.side, 0, 0);
            var center = new Vector3(run.side * run.half, y, z) + normal * 0.008f;
            float height = width / aspect;
            if (NearPass(WorldBounds(center, new Vector3(0.02f, height, width), Quaternion.identity))) return;
            // a quad faces its -Z; an arrow is drawn pointing to the quad's +X: on the +x wall turn it upside down so it still points +z
            var up = pointsForward && run.side > 0 ? Vector3.down : Vector3.up;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = center;
            quad.transform.localRotation = Quaternion.LookRotation(-normal, up);
            quad.transform.localScale = new Vector3(width, height, 1f);
            var r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quad.AddComponent<CourseDecoration>();
            count++;
        }

        // ------------------------------------------------------------------ finding the walls (plant rooms)

        /// <summary>
        /// Dresses one built room of the plant (its transform final, colliders synced): wall runs on both side walls, cornices under
        /// its ceiling, angles on its open slab edges. Returns the number of pieces.
        /// </summary>
        public static int DressRoom(Transform room, float half, float z0, float z1, float ceiling, float lowest, Options options, int seed)
        {
            var group = new GameObject("Details").transform;
            group.SetParent(room, false);
            group.gameObject.AddComponent<CourseDecoration>();
            parent = group;
            rng = new Random(seed ^ IndustrialKit.StableHash(room.name));
            count = 0;
            zoneSides.Clear();
            foreach (int side in new[] { -1, 1 })
            {
                foreach (var run in FindRuns(room, side, half, z0, z1, ceiling, lowest)) DressRun(run, options);
                foreach (var (a, b) in FindCornice(room, side, half, z0, z1, ceiling)) Cornice(side, half, a, b, ceiling);
            }
            SlabAngles(room, half, z0, z1, lowest);
            return count;
        }

        /// <summary>
        /// Dresses walls described by hand (the menu hall, which has no gameplay colliders): each run in <paramref name="frame"/>'s
        /// space, plus a cornice over each (side, from, to) stretch under <paramref name="ceiling"/>.
        /// </summary>
        public static int DressFrame(Transform frame, IEnumerable<Run> runs, IEnumerable<(int side, float half, float a, float b)> cornices, float ceiling,
                                     Options options, int seed)
        {
            parent = frame;
            rng = new Random(seed);
            count = 0;
            zoneSides.Clear();
            foreach (var run in runs) DressRun(run, options);
            foreach (var (side, half, a, b) in cornices) Cornice(side, half, a, b, ceiling);
            return count;
        }

        static int Environment => LayerMask.GetMask("Environment");

        static IEnumerable<Run> FindRuns(Transform room, int side, float half, float z0, float z1, float ceiling, float lowest)
        {
            Vector3 W(float x, float y, float z) => room.TransformPoint(new Vector3(x, y, z));
            float X(float depth) => side * (half - depth);
            var down = -room.up;
            Run? open = null;
            float lastFloor = 0;
            for (float z = z0 + Step / 2; z <= z1; z += Step)
            {
                bool ok = false;
                float floor = 0, top = ceiling;
                bool upper = false;
                if (Physics.Raycast(W(X(0.35f), ceiling - 0.3f, z), down, out var hit, ceiling - lowest + 1f, Environment, QueryTriggerInteraction.Ignore)
                    && Vector3.Dot(hit.normal, room.up) > 0.97f && IndustrialKit.IsStatic(hit.collider))
                {
                    floor = room.InverseTransformPoint(hit.point).y;
                    ok = floor >= lowest
                         // the floor reaches past the props' band
                         && Physics.Raycast(W(X(Band), floor + 0.3f, z), down, out var h2, 0.5f, Environment, QueryTriggerInteraction.Ignore)
                         && Mathf.Abs(room.InverseTransformPoint(h2.point).y - floor) < 0.03f
                         // a static wall right there
                         && Physics.Raycast(W(X(1f), floor + 0.4f, z), room.TransformDirection(side, 0, 0), out var wall, 1.2f, Environment, QueryTriggerInteraction.Ignore)
                         && Mathf.Abs(wall.distance - 1f) < 0.05f && IndustrialKit.IsStatic(wall.collider)
                         // nothing at all in front of it up to head height (obstacles, triggers, machines)
                         && !Physics.CheckBox(W(X(Band / 2 + 0.04f), floor + 1.4f, z), new Vector3(Band / 2, 1.25f, Step / 2 - 0.01f), room.rotation,
                                              Physics.AllLayers, QueryTriggerInteraction.Collide);
                    if (ok)
                    {
                        top = Physics.Raycast(W(X(0.3f), floor + 0.2f, z), room.up, out var up, 80f, Environment, QueryTriggerInteraction.Ignore)
                            ? room.InverseTransformPoint(up.point).y : ceiling;
                        upper = top - floor > 8.6f && !Physics.CheckBox(W(X(Band / 2 + 0.04f), floor + 5.1f, z), new Vector3(Band / 2, 2.4f, Step / 2 - 0.01f),
                                                                        room.rotation, Physics.AllLayers, QueryTriggerInteraction.Collide);
                    }
                }
                if (open.HasValue && (!ok || Mathf.Abs(floor - lastFloor) > 0.05f))
                {
                    if (open.Value.Length >= 1.5f) yield return open.Value;
                    open = null;
                }
                if (!ok) continue;
                if (!open.HasValue) open = new Run { side = side, half = half, z0 = z - Step / 2, z1 = z + Step / 2, floor = floor, top = top, upper = upper };
                else
                {
                    var r = open.Value;
                    r.z1 = z + Step / 2;
                    r.top = Mathf.Min(r.top, top);
                    r.upper &= upper;
                    open = r;
                }
                lastFloor = floor;
            }
            if (open.HasValue && open.Value.Length >= 1.5f) yield return open.Value;
        }

        static IEnumerable<(float, float)> FindCornice(Transform room, int side, float half, float z0, float z1, float ceiling)
        {
            Vector3 W(float x, float y, float z) => room.TransformPoint(new Vector3(x, y, z));
            float? start = null;
            float z = z0 + Step / 2;
            for (; z <= z1; z += Step)
            {
                bool ok = Physics.Raycast(W(side * (half - 1f), ceiling - 0.5f, z), room.TransformDirection(side, 0, 0), out var wall, 1.2f, Environment, QueryTriggerInteraction.Ignore)
                          && Mathf.Abs(wall.distance - 1f) < 0.05f
                          && Physics.Raycast(W(side * (half - 0.3f), ceiling - 0.5f, z), room.up, out var up, 0.6f, Environment, QueryTriggerInteraction.Ignore)
                          && up.collider.GetComponentInParent<CourseCeiling>() != null;
                if (ok && !start.HasValue) start = z - Step / 2;
                if (!ok && start.HasValue) { if (z - Step / 2 - start.Value >= 2f) yield return (start.Value, z - Step / 2); start = null; }
            }
            if (start.HasValue && z - Step / 2 - start.Value >= 2f) yield return (start.Value, z - Step / 2);
        }

        // ------------------------------------------------------------------ mouldings

        /// <summary>A two-step cornice along the top of a wall, from <paramref name="a"/> to <paramref name="b"/>.</summary>
        public static void Cornice(int side, float half, float a, float b, float ceiling)
        {
            float mid = (a + b) / 2, len = b - a;
            Box("Cornice", new Vector3(side * (half - 0.16f), ceiling - 0.13f, mid), new Vector3(0.32f, 0.26f, len), KitRole.Wall);
            Box("Cornice moulding", new Vector3(side * (half - 0.1f), ceiling - 0.32f, mid), new Vector3(0.2f, 0.12f, len), KitRole.Truss);
        }

        /// <summary>Dresses one wall run: pilasters split it into bays; each bay gets mouldings and, by the seed, a prop.</summary>
        public static void DressRun(Run run, Options options)
        {
            float X(float depth) => run.side * (run.half - depth);
            float f = run.floor, height = run.top - run.floor;
            int bays = Mathf.Max(1, Mathf.RoundToInt(run.Length / 6.5f));
            float bay = run.Length / bays;
            bool pipes = height > 3.4f && run.Length >= 6f && rng.NextDouble() < 0.55;
            bool tray = height > 4.2f && run.Length >= 6f && rng.NextDouble() < 0.6;
            var pipeRole = rng.NextDouble() < 0.5 ? KitRole.Grating : (rng.NextDouble() < 0.5 ? KitRole.Rust : KitRole.Frame);

            // the pipe run and the cable tray span the whole run
            if (pipes)
            {
                float y = f + 2.8f, a = run.z0 + 0.3f, b = run.z1 - 0.3f;
                Cylinder("Wall pipe", new Vector3(X(0.24f), y, (a + b) / 2), 0.11f, b - a, 2, pipeRole);
                for (float z = a + 1.2f; z < b - 0.5f; z += 3f)
                    Cylinder("Pipe flange", new Vector3(X(0.24f), y, z), 0.16f, 0.06f, 2, KitRole.Truss);
                for (float z = a + 0.6f; z < b; z += 2.4f)
                    Box("Pipe bracket", new Vector3(X(0.1f), y - 0.12f, z), new Vector3(0.22f, 0.04f, 0.08f), KitRole.Truss);
            }
            if (tray)
            {
                float y = f + 3.6f, mid = (run.z0 + run.z1) / 2, len = run.Length - 0.4f;
                Box("Cable tray", new Vector3(X(0.24f), y, mid), new Vector3(0.38f, 0.03f, len), KitRole.Grating);
                Box("Cable tray lip", new Vector3(X(0.06f), y + 0.05f, mid), new Vector3(0.02f, 0.1f, len), KitRole.Grating);
                Box("Cable tray lip", new Vector3(X(0.42f), y + 0.05f, mid), new Vector3(0.02f, 0.1f, len), KitRole.Grating);
                Box("Cables", new Vector3(X(0.24f), y + 0.04f, mid), new Vector3(0.3f, 0.05f, len), KitRole.Rubber);
                for (float z = run.z0 + 0.8f; z < run.z1 - 0.3f; z += 3f)
                    Box("Tray support", new Vector3(X(0.25f), y - 0.04f, z), new Vector3(0.5f, 0.05f, 0.05f), KitRole.Truss);
            }

            for (int i = 0; i < bays; i++)
            {
                float a = run.z0 + i * bay, b = a + bay, mid = (a + b) / 2;
                bool pilasterA = options.pilasters && height > 2.6f && (i > 0 || run.Length > 4f);
                if (pilasterA) Pilaster(run, a + (i == 0 ? 0.45f : 0f));
                if (options.pilasters && i == bays - 1 && height > 2.6f && run.Length > 4f) Pilaster(run, b - 0.45f);
                float inA = a + (options.pilasters ? 0.55f : 0.1f), inB = b - (options.pilasters ? 0.55f : 0.1f);
                if (inB - inA < 1f) continue;

                // what this bay holds
                int kind = rng.NextDouble() < options.density ? rng.Next(7) : -1;
                bool door = kind == 0 && inB - inA >= 2.2f && height > 3f;
                if (!door)
                {
                    Box("Baseboard", new Vector3(X(0.05f), f + 0.12f, mid), new Vector3(0.1f, 0.24f, b - a), KitRole.Rubber);
                    Box("Baseboard lip", new Vector3(X(0.065f), f + 0.255f, mid), new Vector3(0.13f, 0.03f, b - a), KitRole.Truss);
                    if (height > 3f)
                    {
                        Box("Dado rail", new Vector3(X(0.04f), f + 1.2f, mid), new Vector3(0.08f, 0.07f, b - a), KitRole.Truss);
                        PanelFrame(run, inA + 0.15f, inB - 0.15f, f + 0.42f, f + 1.02f);
                    }
                }
                switch (kind)
                {
                    case 0 when door: Door(run, mid); break;
                    case 1: Electrical(run, mid, tray); break;
                    case 2: PipeStation(run, mid, pipes, pipeRole); break;
                    case 3: Barrels(run, mid); break;
                    case 4: Pallet(run, mid); break;
                    case 5 when inB - inA >= 2f: Workbench(run, mid); break;
                    case 6: WallSign(run, "Sign", IndustrialSigns.For(rng.NextDouble() < 0.5 ? IndustrialSigns.Sign.Warning : IndustrialSigns.Sign.Info), mid, f + 1.9f, 0.5f, 1f); break;
                }
                if (options.windows && run.upper && rng.NextDouble() < 0.4) Window(run, mid, Mathf.Min(inB - inA - 0.4f, 2.6f));
            }

            // the zone number at the start of the room, and a direction arrow
            if (options.zoneNumbers && run.upper && run.z0 < 14f && run.Length >= 4f && zoneSides.Add(run.side))
            {
                float z = run.z0 + Mathf.Min(run.Length / 2, 3.2f);
                WallSign(run, "Zone number", IndustrialSigns.Zone(options.zone), z, f + 5.1f, 2.2f, 2f);
                WallSign(run, "Direction arrow", IndustrialSigns.For(IndustrialSigns.Sign.Arrow), z, f + 3.95f, 1.1f, 1f, true);
            }
        }

        static void Pilaster(Run run, float z)
        {
            float X(float depth) => run.side * (run.half - depth);
            float f = run.floor, t = run.top;
            Box("Pilaster", new Vector3(X(0.17f), (f + t) / 2, z), new Vector3(0.34f, t - f, 0.62f), KitRole.Pillar);
            Box("Pilaster base", new Vector3(X(0.24f), f + 0.3f, z), new Vector3(0.48f, 0.6f, 0.86f), KitRole.Truss);
            Box("Pilaster base band", new Vector3(X(0.245f), f + 0.82f, z), new Vector3(0.47f, 0.44f, 0.84f), Mat(IndustrialDir + IndustrialMaterialBuilder.CautionName));
            Box("Pilaster capital", new Vector3(X(0.22f), t - 0.26f, z), new Vector3(0.44f, 0.16f, 0.8f), KitRole.Truss);
            Box("Pilaster capital top", new Vector3(X(0.29f), t - 0.09f, z), new Vector3(0.58f, 0.18f, 1.02f), KitRole.Truss);
            foreach (float dz in new[] { -0.3f, 0.3f })
            {
                Box("Bolt", new Vector3(X(0.49f), f + 0.18f, z + dz), new Vector3(0.03f, 0.06f, 0.06f), KitRole.Grating);
            }
        }

        /// <summary>A raised rectangular frame on the wall (wainscot panel).</summary>
        static void PanelFrame(Run run, float a, float b, float y0, float y1)
        {
            float x = run.side * (run.half - 0.02f);
            const float w = 0.05f, d = 0.04f;
            Box("Panel frame", new Vector3(x, y1, (a + b) / 2), new Vector3(d, w, b - a), KitRole.Truss);
            Box("Panel frame", new Vector3(x, y0, (a + b) / 2), new Vector3(d, w, b - a), KitRole.Truss);
            Box("Panel frame", new Vector3(x, (y0 + y1) / 2, a), new Vector3(d, y1 - y0, w), KitRole.Truss);
            Box("Panel frame", new Vector3(x, (y0 + y1) / 2, b), new Vector3(d, y1 - y0, w), KitRole.Truss);
        }

        // ------------------------------------------------------------------ frames

        static void Door(Run run, float z)
        {
            float X(float depth) => run.side * (run.half - depth);
            float f = run.floor;
            Box("Door leaf", new Vector3(X(0.025f), f + 1.15f, z), new Vector3(0.05f, 2.3f, 1.1f), KitRole.Truss);
            Box("Door kick plate", new Vector3(X(0.055f), f + 0.15f, z), new Vector3(0.02f, 0.3f, 1.04f), KitRole.Grating);
            Box("Door handle", new Vector3(X(0.08f), f + 1.05f, z + 0.38f * run.side), new Vector3(0.05f, 0.04f, 0.16f), KitRole.Grating);
            Box("Door window", new Vector3(X(0.052f), f + 1.75f, z), new Vector3(0.01f, 0.4f, 0.3f), KitRole.Rubber);
            foreach (float s in new[] { -1f, 1f })
                Box("Door jamb", new Vector3(X(0.07f), f + 1.2f, z + s * 0.62f), new Vector3(0.14f, 2.4f, 0.14f), KitRole.Frame);
            Box("Door lintel", new Vector3(X(0.07f), f + 2.47f, z), new Vector3(0.14f, 0.14f, 1.38f), KitRole.Frame);
            Box("Door lamp", new Vector3(X(0.08f), f + 2.72f, z), new Vector3(0.1f, 0.12f, 0.24f), KitRole.Lamp);
        }

        static void Window(Run run, float z, float width)
        {
            if (width < 1.2f) return;
            float X(float depth) => run.side * (run.half - depth);
            float y0 = run.floor + 6.1f, y1 = run.floor + 7.4f;
            Box("Window glass", new Vector3(X(0.02f), (y0 + y1) / 2, z), new Vector3(0.04f, y1 - y0, width), KitRole.Rubber);
            Box("Window frame", new Vector3(X(0.06f), y1, z), new Vector3(0.12f, 0.1f, width + 0.1f), KitRole.Frame);
            Box("Window sill", new Vector3(X(0.1f), y0 - 0.03f, z), new Vector3(0.2f, 0.08f, width + 0.3f), KitRole.Frame);
            foreach (float s in new[] { -1f, 1f })
                Box("Window frame", new Vector3(X(0.06f), (y0 + y1) / 2, z + s * width / 2), new Vector3(0.12f, y1 - y0, 0.1f), KitRole.Frame);
            Box("Window mullion", new Vector3(X(0.05f), (y0 + y1) / 2, z), new Vector3(0.06f, y1 - y0, 0.05f), KitRole.Frame);
            Box("Window transom", new Vector3(X(0.05f), y0 + (y1 - y0) * 0.62f, z), new Vector3(0.06f, 0.05f, width), KitRole.Frame);
        }

        // ------------------------------------------------------------------ props

        static void Electrical(Run run, float z, bool tray)
        {
            float X(float depth) => run.side * (run.half - depth);
            float f = run.floor;
            int cabinets = rng.Next(1, 3);
            for (int i = 0; i < cabinets; i++)
            {
                float cz = z + (i - (cabinets - 1) / 2f) * 0.95f;
                Box("Cabinet", new Vector3(X(0.22f), f + 1.0f, cz), new Vector3(0.44f, 1.7f, 0.85f), KitRole.Truss);
                Box("Cabinet plinth", new Vector3(X(0.2f), f + 0.07f, cz), new Vector3(0.4f, 0.14f, 0.8f), KitRole.Rubber);
                Box("Cabinet door seam", new Vector3(X(0.445f), f + 1.0f, cz), new Vector3(0.012f, 1.5f, 0.015f), KitRole.Rubber);
                Box("Cabinet handle", new Vector3(X(0.455f), f + 1.05f, cz + 0.08f), new Vector3(0.03f, 0.18f, 0.03f), KitRole.Grating);
                Box("Cabinet lamp", new Vector3(X(0.45f), f + 1.65f, cz - 0.25f), new Vector3(0.02f, 0.05f, 0.05f), rng.NextDouble() < 0.5 ? KitRole.Lamp : KitRole.Glow);
                Box("Cabinet vent", new Vector3(X(0.445f), f + 0.4f, cz), new Vector3(0.01f, 0.2f, 0.5f), KitRole.Grating);
                float top = tray ? f + 3.55f : f + 2.6f;
                Box("Conduit", new Vector3(X(0.08f), (f + 1.85f + top) / 2, cz + 0.25f), new Vector3(0.06f, top - f - 1.85f, 0.06f), KitRole.Grating);
            }
            WallSign(run, "Electric sign", IndustrialSigns.For(IndustrialSigns.Sign.Electric), z, f + 2.15f, 0.4f, 1f);
        }

        static void PipeStation(Run run, float z, bool pipes, KitRole role)
        {
            float X(float depth) => run.side * (run.half - depth);
            float f = run.floor, top = pipes ? f + 2.8f : f + 2.5f;
            Cylinder("Riser", new Vector3(X(0.24f), (f + top) / 2, z), 0.09f, top - f, 1, role);
            Cylinder("Riser flange", new Vector3(X(0.24f), f + 0.5f, z), 0.14f, 0.05f, 1, KitRole.Truss);
            Cylinder("Riser flange", new Vector3(X(0.24f), f + 1.9f, z), 0.14f, 0.05f, 1, KitRole.Truss);
            // a hand wheel on a stem, facing the room
            Cylinder("Valve body", new Vector3(X(0.24f), f + 1.2f, z), 0.15f, 0.26f, 1, KitRole.Truss);
            Cylinder("Valve stem", new Vector3(X(0.45f), f + 1.2f, z), 0.025f, 0.3f, 0, KitRole.Grating);
            Cylinder("Valve wheel", new Vector3(X(0.6f), f + 1.2f, z), 0.2f, 0.035f, 0, KitRole.Frame);
            Box("Valve spoke", new Vector3(X(0.6f), f + 1.2f, z), new Vector3(0.03f, 0.36f, 0.03f), KitRole.Frame);
            Box("Valve spoke", new Vector3(X(0.6f), f + 1.2f, z), new Vector3(0.03f, 0.03f, 0.36f), KitRole.Frame);
            // a gauge on a short neck
            Cylinder("Gauge neck", new Vector3(X(0.24f), f + 1.6f, z + 0.16f), 0.02f, 0.18f, 2, KitRole.Grating);
            Cylinder("Gauge", new Vector3(X(0.24f), f + 1.6f, z + 0.29f), 0.09f, 0.06f, 2, KitRole.Truss);
            Box("Gauge face", new Vector3(X(0.24f), f + 1.6f, z + 0.325f), new Vector3(0.1f, 0.1f, 0.01f), KitRole.Accent);
        }

        static void Barrels(Run run, float z)
        {
            float X(float depth) => run.side * (run.half - depth);
            float f = run.floor;
            int n = rng.Next(2, 4);
            var roles = new[] { KitRole.Rust, KitRole.Frame, KitRole.Pillar, KitRole.Rubber };
            for (int i = 0; i < n; i++)
            {
                float bz = z + (i - (n - 1) / 2f) * 0.68f;
                float depth = 0.36f + (i % 2) * 0.12f;
                var role = roles[rng.Next(roles.Length)];
                Cylinder("Barrel", new Vector3(X(depth), f + 0.45f, bz), 0.29f, 0.88f, 1, role);
                foreach (float y in new[] { 0.28f, 0.62f })
                    Cylinder("Barrel ring", new Vector3(X(depth), f + y, bz), 0.3f, 0.035f, 1, role == KitRole.Rubber ? KitRole.Grating : role);
                Cylinder("Barrel bung", new Vector3(X(depth) + 0.12f, f + 0.9f, bz + 0.1f), 0.035f, 0.02f, 1, KitRole.Grating);
            }
        }

        static void Pallet(Run run, float z)
        {
            float X(float depth) => run.side * (run.half - depth);
            float f = run.floor;
            const float d = 0.84f, len = 1.15f;
            Box("Pallet deck", new Vector3(X(0.47f), f + 0.125f, z), new Vector3(d, 0.03f, len), KitRole.Rust);
            foreach (float s in new[] { -0.36f, 0f, 0.36f })
                Box("Pallet runner", new Vector3(X(0.47f + s), f + 0.055f, z), new Vector3(0.1f, 0.11f, len), KitRole.Rust);
            float size = 0.72f;
            Box("Crate", new Vector3(X(0.47f), f + 0.14f + size / 2, z - 0.18f), new Vector3(size, size, size), KitRole.Frame);
            Box("Crate band", new Vector3(X(0.47f), f + 0.14f + size / 2, z - 0.18f), new Vector3(size + 0.03f, 0.08f, size + 0.03f), KitRole.Truss);
            if (rng.NextDouble() < 0.6)
                Box("Crate", new Vector3(X(0.45f), f + 0.14f + size + 0.25f, z - 0.12f), new Vector3(0.5f, 0.5f, 0.5f), KitRole.Pillar);
            Box("Shrink wrap", new Vector3(X(0.47f), f + 0.36f, z + 0.38f), new Vector3(0.6f, 0.44f, 0.36f), KitRole.Rubber);
        }

        static void Workbench(Run run, float z)
        {
            float X(float depth) => run.side * (run.half - depth);
            float f = run.floor;
            Box("Bench top", new Vector3(X(0.4f), f + 0.9f, z), new Vector3(0.74f, 0.06f, 1.8f), KitRole.Rubber);
            Box("Bench shelf", new Vector3(X(0.4f), f + 0.25f, z), new Vector3(0.68f, 0.03f, 1.7f), KitRole.Truss);
            foreach (float dx in new[] { 0.08f, 0.72f })
                foreach (float dz in new[] { -0.85f, 0.85f })
                    Box("Bench leg", new Vector3(X(dx), f + 0.435f, z + dz), new Vector3(0.05f, 0.87f, 0.05f), KitRole.Truss);
            Box("Toolbox", new Vector3(X(0.38f), f + 1.04f, z - 0.45f), new Vector3(0.28f, 0.22f, 0.5f), KitRole.Frame);
            Box("Toolbox handle", new Vector3(X(0.38f), f + 1.17f, z - 0.45f), new Vector3(0.04f, 0.04f, 0.3f), KitRole.Rubber);
            Box("Vice", new Vector3(X(0.62f), f + 0.99f, z + 0.6f), new Vector3(0.16f, 0.12f, 0.2f), KitRole.Grating);
            Box("Pegboard", new Vector3(X(0.015f), f + 1.65f, z), new Vector3(0.03f, 0.8f, 1.6f), KitRole.Grating);
            for (int i = 0; i < 5; i++)
                Box("Tool", new Vector3(X(0.045f), f + 1.5f + (i % 2) * 0.25f, z - 0.6f + i * 0.3f), new Vector3(0.03f, 0.3f - (i % 3) * 0.06f, 0.05f), i % 2 == 0 ? KitRole.Frame : KitRole.Rubber);
            Box("Bench lamp", new Vector3(X(0.1f), f + 2.15f, z), new Vector3(0.2f, 0.06f, 0.9f), KitRole.Lamp);
        }

        // ------------------------------------------------------------------ slab edges

        /// <summary>Steel angles along the open top edges of static slabs (catwalks, landings, platforms), with a bolt now and then.</summary>
        static void SlabAngles(Transform room, float half, float z0, float z1, float lowest)
        {
            foreach (var col in room.GetComponentsInChildren<BoxCollider>())
            {
                if (col.isTrigger || col.gameObject.layer != LayerMask.NameToLayer("Environment") || !IndustrialKit.IsStatic(col)) continue;
                if (col.GetComponentInParent<CourseCeiling>() != null || col.GetComponentInParent<CourseDecoration>() != null) continue;
                if (Quaternion.Angle(Quaternion.Inverse(room.rotation) * col.transform.rotation, Quaternion.identity) > 0.5f) continue;   // ramps, slides
                var center = room.InverseTransformPoint(col.bounds.center);
                var size = room.InverseTransformVector(col.bounds.size);
                size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                if (size.y > 2.5f || size.x < 0.8f || size.z < 0.8f) continue;   // walls, foundations, posts
                float top = center.y + size.y / 2;
                if (top < lowest + 0.5f || center.z + size.z / 2 < z0 || center.z - size.z / 2 > z1) continue;
                // the four top edges: (outward direction, position of the edge, axis along it)
                for (int e = 0; e < 4; e++)
                {
                    bool alongZ = e < 2;
                    float s = e % 2 == 0 ? -1 : 1;
                    var outward = alongZ ? new Vector3(s, 0, 0) : new Vector3(0, 0, s);
                    float edge = alongZ ? center.x + s * size.x / 2 : center.z + s * size.z / 2;
                    if (alongZ && Mathf.Abs(edge) > half - 0.2f) continue;   // against the room's wall
                    float a = alongZ ? center.z - size.z / 2 : center.x - size.x / 2, b = alongZ ? center.z + size.z / 2 : center.x + size.x / 2;
                    float? start = null;
                    for (float t = a + 0.25f; t <= b + 0.01f; t += Step)
                    {
                        var at = alongZ ? new Vector3(edge, top, Mathf.Min(t, b - 0.05f)) : new Vector3(Mathf.Min(t, b - 0.05f), top, edge);
                        bool open = !Physics.CheckBox(room.TransformPoint(at + outward * 0.14f + Vector3.down * 0.16f), new Vector3(0.06f, 0.1f, 0.06f), room.rotation,
                                                      Physics.AllLayers, QueryTriggerInteraction.Ignore);
                        bool last = t + Step > b + 0.01f;
                        if (open && !start.HasValue) start = t - 0.25f;
                        if (start.HasValue && (!open || last))
                        {
                            float end = open ? b : t - 0.25f;
                            if (end - start.Value >= 0.9f) Angle(outward, edge, top, Mathf.Max(a, start.Value), Mathf.Min(b, end), alongZ);
                            start = null;
                        }
                    }
                }
            }
        }

        static void Angle(Vector3 outward, float edge, float top, float a, float b, bool alongZ)
        {
            float mid = (a + b) / 2, len = b - a;
            Vector3 P(float along, float offset, float y) => alongZ ? new Vector3(edge + outward.x * offset, y, along) : new Vector3(along, y, edge + outward.z * offset);
            var legSize = alongZ ? new Vector3(0.03f, 0.15f, len) : new Vector3(len, 0.15f, 0.03f);
            Box("Edge angle", P(mid, 0.015f, top - 0.075f), legSize, KitRole.Grating);
            for (float t = a + 0.5f; t < b - 0.3f; t += 3f)
                Box("Edge bolt", P(t, 0.035f, top - 0.08f), alongZ ? new Vector3(0.02f, 0.045f, 0.045f) : new Vector3(0.045f, 0.045f, 0.02f), KitRole.Truss);
        }
    }
}
