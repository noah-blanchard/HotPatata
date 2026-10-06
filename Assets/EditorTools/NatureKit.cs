using System.Collections.Generic;
using System.IO;
using HotPatata;
using UnityEditor;
using UnityEngine;
using static HotPatata.Editor.CourseKit;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// Course pieces of the nature look (ARCHITECTURE §25.3), for <see cref="PatataWildsBuilder"/>: water (a surface with a
    /// <see cref="KillZone"/> just under it: every water surface is lethal), the campfire that stands for a checkpoint, mossy
    /// rocks, Poly Haven props, torches and ambient sound sources. Decoration is collider-free and marked
    /// <see cref="CourseDecoration"/>; nothing here carries a rule.
    /// </summary>
    public static class NatureKit
    {
        public const string WaterMeshPath = NatureTreeBuilder.MeshDir + "WaterPlane.asset";
        public const string WaterMaterialName = "Nature_Water";
        public const string WaterfallMaterialName = "Nature_Waterfall";
        /// <summary>How far under a water surface its kill zone starts (PROJECT_SPEC §20).</summary>
        public const float KillBelowWater = 0.3f;
        const string TuningPath = "Assets/ScriptableObjects/Tuning/GameTuning.asset";
        const string MixerPath = "Assets/Audio/HotPatataMixer.mixer";
        const string SoftDot = "Assets/Art/VFX/Textures/SoftDot.png";

        static GameTuning Tuning => AssetDatabase.LoadAssetAtPath<GameTuning>(TuningPath);

        static UnityEngine.Audio.AudioMixerGroup SfxGroup
        {
            get
            {
                var groups = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>(MixerPath)?.FindMatchingGroups("Master/SFX");
                return groups != null && groups.Length > 0 ? groups[0] : null;
            }
        }

        // ------------------------------------------------------------------ water

        /// <summary>The water materials (HotPatata/Water) and the shared unit plane they are drawn on.</summary>
        public static void EnsureWater()
        {
            NatureTextureFactory.EnsureTextures();
            var shader = Shader.Find("HotPatata/Water") ?? throw new System.InvalidOperationException("missing shader HotPatata/Water");
            foreach (var (name, fall) in new[] { (WaterMaterialName, false), (WaterfallMaterialName, true) })
            {
                string path = NatureMaterialBuilder.Path(name);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.SetTexture("_WaveMap", AssetDatabase.LoadAssetAtPath<Texture2D>(NatureTextureFactory.WaveNormal));
                mat.SetTexture("_FoamMap", AssetDatabase.LoadAssetAtPath<Texture2D>(NatureTextureFactory.FoamNoise));
                mat.SetFloat("_FlowSpeed", fall ? 6f : 1.1f);
                mat.SetFloat("_WaveScale", fall ? 2.5f : 3f);
                mat.SetFloat("_Clarity", fall ? 0.2f : 0.75f);
                mat.SetFloat("_FoamDepth", fall ? 3f : 0.4f);
                mat.SetColor("_ShallowColor", fall ? new Color(0.55f, 0.64f, 0.62f) : new Color(0.32f, 0.42f, 0.36f));
                EditorUtility.SetDirty(mat);
            }
            if (AssetDatabase.LoadAssetAtPath<Mesh>(WaterMeshPath) == null)
            {
                Directory.CreateDirectory(NatureTreeBuilder.MeshDir.TrimEnd('/'));
                AssetDatabase.CreateAsset(UnitPlane(16), WaterMeshPath);
            }
        }

        /// <summary>A 1 x 1 plane in XZ (centre at the origin), subdivided; UVs 0..1, normals up, tangents along +X.</summary>
        static Mesh UnitPlane(int cells)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int z = 0; z <= cells; z++)
                for (int x = 0; x <= cells; x++)
                {
                    v.Add(new Vector3(x / (float)cells - 0.5f, 0f, z / (float)cells - 0.5f));
                    uv.Add(new Vector2(x / (float)cells, z / (float)cells));
                }
            for (int z = 0; z < cells; z++)
                for (int x = 0; x < cells; x++)
                {
                    int a = z * (cells + 1) + x, b = a + cells + 1;
                    t.Add(a); t.Add(b); t.Add(a + 1);
                    t.Add(a + 1); t.Add(b); t.Add(b + 1);
                }
            var mesh = new Mesh { name = "WaterPlane" };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            var foam = new List<Color>();
            for (int i = 0; i < v.Count; i++) foam.Add(Color.black);   // no painted foam: only the shallows foam
            mesh.SetColors(foam);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A water surface over the area <paramref name="x0"/>..<paramref name="x1"/> x <paramref name="z0"/>..<paramref name="z1"/>
        /// (local), at <paramref name="surfaceY"/>, flowing along +Z, with its kill zone from just under the surface down to the bed.
        /// A player falling in, or a thrown bomb touching it, fails the section (KillZone).
        /// </summary>
        public static GameObject Water(Transform parent, string name, float x0, float x1, float z0, float z1, float surfaceY, float bedY)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3((x0 + x1) / 2f, surfaceY, (z0 + z1) / 2f);
            go.transform.localScale = new Vector3(x1 - x0, 1f, z1 - z0);
            go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(WaterMeshPath);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = NatureMaterialBuilder.Load(WaterMaterialName);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<NatureWater>();
            float top = surfaceY - KillBelowWater, bottom = Mathf.Min(bedY, top - 0.6f);
            var kill = Place(parent, GameplayDir + "KillZone", name + " (drowning)", Vector3.zero, Quaternion.identity);
            kill.transform.localPosition = new Vector3((x0 + x1) / 2f, (top + bottom) / 2f, (z0 + z1) / 2f);
            kill.transform.localScale = new Vector3(x1 - x0, top - bottom, z1 - z0);
            return go;
        }

        /// <summary>A waterfall: a vertical sheet of fast water at local x, from <paramref name="top"/> down to <paramref name="bottom"/>.</summary>
        public static GameObject Waterfall(Transform parent, string name, Vector3 center, float width, float top, float bottom, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(center.x, (top + bottom) / 2f, center.z);
            // the plane's +Z (flow) points down the fall; its normal faces the course
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(width, 1f, top - bottom);
            go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(WaterMeshPath);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = NatureMaterialBuilder.Load(WaterfallMaterialName);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<CourseDecoration>();
            return go;
        }

        // ------------------------------------------------------------------ rocks and props

        /// <summary>A solid rock (Environment collider, a rough slab drawn in <paramref name="material"/>, Nature_MossyRock by default).</summary>
        public static GameObject Rock(Transform parent, string name, Vector3 center, Vector3 size, string material = "Nature_MossyRock")
        {
            var go = Block(parent, name, center, size, KitRole.Wall);
            CourseKit.Skin(go.transform.Find("Visual").gameObject, KitShape.RoughBox, KitColor.Neutral, NatureMaterialBuilder.Load(material));
            return go;
        }

        /// <summary>A collider-free piece of the kit in a role's look (logs, planks), marked as decoration.</summary>
        public static GameObject Detail(Transform parent, string name, Vector3 center, Vector3 size, KitRole role, Quaternion? rotation = null) =>
            IndustrialKit.Detail(parent, name, center, size, role, rotation);

        /// <summary>A Poly Haven model as decoration: its material swapped for the library's, no collider, scaled to <paramref name="height"/> metres.</summary>
        public static GameObject Prop(Transform parent, string model, string material, Vector3 position, float yaw, float height, bool shadows = true)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{NatureAssetImporter.ModelFolder}{model}/{model}_1k.fbx")
                        ?? throw new System.InvalidOperationException("missing model " + model + " (run tools/Fetch-PolyHaven.ps1)");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.name = model;
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var bounds = WorldBounds(go);
            float scale = bounds.size.y > 0.001f ? height / bounds.size.y : 1f;
            go.transform.localScale = Vector3.one * scale;
            bounds = WorldBounds(go);
            go.transform.position += Vector3.up * (parent.TransformPoint(position).y - bounds.min.y);   // stand on the ground
            var mat = NatureMaterialBuilder.Load(material);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            go.AddComponent<CourseDecoration>();
            return go;
        }

        public static Bounds WorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>A torch on a pole: a warm practical light (no shadows) and a glowing ember head.</summary>
        public static void Torch(Transform parent, Vector3 foot, float intensity = 9f, float range = 14f)
        {
            Detail(parent, "Torch pole", foot + Vector3.up * 0.9f, new Vector3(0.12f, 1.8f, 0.12f), KitRole.Frame);
            Detail(parent, "Torch head", foot + Vector3.up * 1.9f, new Vector3(0.22f, 0.25f, 0.22f), KitRole.Glow);
            LookBuilder.PracticalLamp(parent, foot + Vector3.up * 2.3f, new Color(1f, 0.62f, 0.3f), intensity, range, "Torch light");
        }

        // ------------------------------------------------------------------ campfire (checkpoint)

        static Material ParticleMaterial(string name, Color color, bool additive) =>
            MakeUnlitMaterial(name, color, AssetDatabase.LoadAssetAtPath<Texture2D>(SoftDot), additive);

        /// <summary>
        /// A checkpoint's campfire (spec §12.3, §19): a ring of stones (Poly Haven's stone fire pit) with split logs, cold until the
        /// checkpoint is reached; then flames, embers, smoke, a warm light and a crackle (<see cref="CampfirePresentation"/>).
        /// Collider-free. <paramref name="checkpoint"/> null makes the summit beacon (lit at the finish, bigger).
        /// </summary>
        public static CampfirePresentation Campfire(Transform parent, Vector3 position, Checkpoint checkpoint, float scale = 1f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CampfirePrefabPath) ?? BuildCampfirePrefab();
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = checkpoint != null ? "Campfire " + checkpoint.name : "Summit beacon";
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * scale;
            var presentation = go.GetComponent<CampfirePresentation>();
            SetReference(presentation, "checkpoint", checkpoint);
            SetField(presentation, "size", p => p.floatValue = scale);
            return presentation;
        }

        public const string CampfirePrefabPath = NatureTreeBuilder.PrefabDir + "Campfire.prefab";

        /// <summary>
        /// The campfire every checkpoint shows (a prefab, so a scene holds 25 small instances rather than 75 particle systems):
        /// rebuilt by each course build. Its particles scale with the instance (the summit beacon is bigger).
        /// </summary>
        public static GameObject BuildCampfirePrefab()
        {
            System.IO.Directory.CreateDirectory(NatureTreeBuilder.PrefabDir.TrimEnd('/'));
            var holder = new GameObject("Campfire");
            try
            {
                BuildCampfire(holder.transform);
                return PrefabUtility.SaveAsPrefabAsset(holder, CampfirePrefabPath);
            }
            finally { Object.DestroyImmediate(holder); }
        }

        static void BuildCampfire(Transform root)
        {
            const float scale = 1f;
            Checkpoint checkpoint = null;
            root.gameObject.AddComponent<CourseDecoration>();
            Prop(root, "stone_fire_pit", "Nature_Prop_FirePit", Vector3.zero, 0f, 0.45f * scale);
            // split logs leaning into the centre
            for (int i = 0; i < 4; i++)
            {
                var rot = Quaternion.Euler(0f, i * 90f + 20f, 0f) * Quaternion.Euler(-35f, 0f, 0f);
                Detail(root, "Fire log", rot * new Vector3(0f, 0f, 0.28f * scale) + Vector3.up * 0.25f * scale, new Vector3(0.14f, 0.14f, 0.7f) * scale, KitRole.Truss, rot);
            }
            var fire = new GameObject("Fire").transform;
            fire.SetParent(root, false);
            fire.localPosition = Vector3.up * 0.3f * scale;
            var flames = Particles(fire, "Flames", ParticleMaterial("Nature_Flame", new Color(1f, 0.55f, 0.18f, 0.9f), true), scale,
                lifetime: new Vector2(0.5f, 0.9f), speed: new Vector2(0.6f, 1.4f), size: new Vector2(0.35f, 0.7f), rate: 34f, radius: 0.22f, gravity: -0.15f,
                start: new Color(1f, 0.75f, 0.3f, 0.95f), end: new Color(1f, 0.25f, 0.05f, 0f));
            var embers = Particles(fire, "Embers", ParticleMaterial("Nature_Spark", new Color(1f, 0.6f, 0.2f, 1f), true), scale,
                lifetime: new Vector2(1.2f, 2.4f), speed: new Vector2(1f, 2.6f), size: new Vector2(0.03f, 0.07f), rate: 9f, radius: 0.25f, gravity: -0.05f,
                start: new Color(1f, 0.7f, 0.3f, 1f), end: new Color(1f, 0.3f, 0.05f, 0f), noise: 0.6f);
            var smoke = Particles(fire, "Smoke", ParticleMaterial("Nature_Smoke", new Color(0.55f, 0.55f, 0.58f, 0.35f), false), scale,
                lifetime: new Vector2(5f, 8f), speed: new Vector2(0.6f, 1.1f), size: new Vector2(0.8f, 1.6f), rate: 5f, radius: 0.3f, gravity: -0.02f,
                start: new Color(0.45f, 0.45f, 0.48f, 0.4f), end: new Color(0.7f, 0.7f, 0.72f, 0f), noise: 0.35f, grow: 3.5f);
            smoke.transform.localPosition = Vector3.up * 0.8f * scale;
            var light = LookBuilder.PracticalLamp(fire, fire.position + Vector3.up * 0.6f * scale, new Color(1f, 0.6f, 0.28f), 0f, 14f * scale, "Fire light");
            var audio = fire.gameObject.AddComponent<AudioSource>();
            audio.clip = NatureAudioFactory.Clip(NatureAudioFactory.Ambience.Fire);
            audio.loop = true;
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Logarithmic;
            audio.minDistance = 2f;
            audio.maxDistance = 22f;
            audio.dopplerLevel = 0f;
            audio.priority = 200;
            audio.outputAudioMixerGroup = SfxGroup;
            var presentation = root.gameObject.AddComponent<CampfirePresentation>();
            presentation.Configure(Tuning, checkpoint, flames, embers, smoke, light, audio, scale);
        }

        static ParticleSystem Particles(Transform parent, string name, Material material, float scale, Vector2 lifetime, Vector2 speed, Vector2 size, float rate,
                                        float radius, float gravity, Color start, Color end, float noise = 0f, float grow = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // emit upwards
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x * scale, speed.y * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x * scale, size.y * scale);
            main.startColor = start;
            main.gravityModifier = gravity;
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // a bigger instance (the summit beacon) has bigger flames
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = radius * scale;
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(start.a, 0.12f), new GradientAlphaKey(end.a, 1f) });
            colour.color = g;
            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, grow));
            if (noise > 0f)
            {
                var n = ps.noise;
                n.enabled = true;
                n.strength = noise;
                n.frequency = 0.6f;
            }
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        // ------------------------------------------------------------------ ambient sound

        /// <summary>A fixed 3D ambient loop (river, falls, birds, wind): low priority, on the effects group, never a movement sound.</summary>
        public static AudioSource Ambient(Transform parent, string name, Vector3 position, NatureAudioFactory.Ambience kind, float volume, float maxDistance)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var audio = go.AddComponent<AudioSource>();
            audio.clip = NatureAudioFactory.Clip(kind);
            audio.loop = true;
            audio.playOnAwake = true;
            audio.volume = volume;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Logarithmic;
            audio.minDistance = Mathf.Min(6f, maxDistance * 0.2f);
            audio.maxDistance = maxDistance;
            audio.dopplerLevel = 0f;
            audio.priority = 200;   // under the bomb's beeps
            audio.outputAudioMixerGroup = SfxGroup;
            go.AddComponent<AmbientLoop>();
            return audio;
        }
    }
}
