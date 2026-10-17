using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// The palette's pictures (ARCHITECTURE §27.3): a prefab's tile is its asset preview; a piece's is rendered once (a default instance in
    /// an empty scene opened beside the level, a 256² picture in <see cref="Folder"/>) and rendered again when its signature changes (the
    /// entry, its piece's script). What has no shape (a sound, a tool) shows an icon. Menu HotPatata/Libraries/Refresh Palette Thumbnails.
    /// </summary>
    public static class PaletteThumbnails
    {
        public const string Folder = "Library/HotPatataPalette/";
        public const int Size = 256;
        /// <summary>Raised to render every picture again (the renderer changed).</summary>
        const int Version = 1;
        /// <summary>Where the pictures are taken: far from any level.</summary>
        static readonly Vector3 Stage = new Vector3(24000f, 600f, 24000f);
        static readonly Color Backdrop = new Color(0.20f, 0.22f, 0.24f);

        static readonly Dictionary<string, Texture2D> loaded = new Dictionary<string, Texture2D>();

        /// <summary>The picture of <paramref name="entry"/>'s <paramref name="look"/>, or null while it loads or is not rendered yet.</summary>
        public static Texture Get(PaletteEntry entry, string look)
        {
            string prefab = entry.PrefabOf(look);
            if (prefab != null && !Rendered(entry))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
                if (asset == null) return Icon("d_console.warnicon");
                return AssetPreview.GetAssetPreview(asset);
            }
            if (!string.IsNullOrEmpty(entry.Icon)) return Icon(entry.Icon);
            string key = entry.ThumbnailKey(look);
            if (loaded.TryGetValue(key, out var tex) && tex != null) return tex;
            if (!Fresh(entry, look)) return null;
            tex = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
            tex.LoadImage(File.ReadAllBytes(PicturePath(key)));
            loaded[key] = tex;
            return tex;
        }

        /// <summary>
        /// Rendered by the palette rather than shown by its asset preview: the kit and the course (the KayKit pieces are drawn by
        /// <c>KitSkin</c>, which an asset preview never runs).
        /// </summary>
        static bool Rendered(PaletteEntry entry) => entry.Kind == PaletteKind.Kit || entry.Kind == PaletteKind.Course;

        /// <summary>True while a prefab's asset preview is still being made.</summary>
        public static bool Loading => AssetPreview.IsLoadingAssetPreviews();

        static Texture Icon(string name) => EditorGUIUtility.IconContent(name)?.image;

        /// <summary>The fallback icon of an entry with no picture yet.</summary>
        public static Texture Placeholder(PaletteEntry entry) => Icon(entry.Kind switch
        {
            PaletteKind.Kit => "d_Prefab Icon",
            PaletteKind.Prop => "d_Prefab Icon",
            PaletteKind.Structure => "d_PrefabVariant Icon",
            PaletteKind.Element => "d_MeshFilter Icon",
            PaletteKind.Air => "d_AudioSource Icon",
            PaletteKind.Tool => "d_CustomTool",
            _ => "d_Mesh Icon",
        });

        static string PicturePath(string key) => Folder + Safe(key) + ".png";

        static string Safe(string key) => new string(key.Select(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' ? c : '_').ToArray());

        /// <summary>True when the picture on disk was made from what the entry is now.</summary>
        public static bool Fresh(PaletteEntry entry, string look)
        {
            string key = entry.ThumbnailKey(look), path = PicturePath(key);
            return File.Exists(path) && File.Exists(path + ".sig") && File.ReadAllText(path + ".sig") == Signature(entry, look);
        }

        /// <summary>What a rendered picture depends on: the renderer, the entry and look, and the text of its piece's script.</summary>
        public static string Signature(PaletteEntry entry, string look)
        {
            string script = "";
            if (entry.PieceType != null)
            {
                var guid = AssetDatabase.FindAssets(entry.PieceType.Name + " t:MonoScript").FirstOrDefault(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == entry.PieceType.Name);
                if (guid != null) script = Hash128.Compute(File.ReadAllText(AssetDatabase.GUIDToAssetPath(guid))).ToString();
            }
            return $"{Version}|{entry.Id}|{look}|{entry.Label}|{script}|{(entry.Preset != null ? EditorJsonUtility.ToJson(entry.Preset) : "")}";
        }

        /// <summary>The entries (and looks) whose picture must be rendered.</summary>
        public static List<(PaletteEntry entry, string look)> Missing(IEnumerable<PaletteEntry> entries) =>
            entries.SelectMany(e => e.Looks.Select(l => (e, l.Id)))
                   .Where(x => (x.e.PrefabOf(x.Item2) == null || Rendered(x.e)) && string.IsNullOrEmpty(x.e.Icon) && x.e.Kind != PaletteKind.Tool && !Fresh(x.e, x.Item2))
                   .ToList();

        [MenuItem("HotPatata/Libraries/Refresh Palette Thumbnails", priority = 80)]
        public static void RefreshAll()
        {
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            loaded.Clear();
            Render(Missing(PaletteCatalog.All()));
        }

        /// <summary>
        /// Renders the pictures of <paramref name="todo"/> in an empty scene opened beside the level (closed after, nothing of it saved).
        /// Returns how many were rendered; none when the open scene is unsaved (an empty scene cannot open beside it).
        /// </summary>
        public static int Render(List<(PaletteEntry entry, string look)> todo)
        {
            if (todo.Count == 0 || EditorApplication.isPlayingOrWillChangePlaymode) return 0;
            var active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path))
            {
                Debug.LogWarning("[Palette] save the open scene first: the palette renders its pictures in a scene opened beside it.");
                return 0;
            }
            Directory.CreateDirectory(Folder);
            var selection = Selection.objects;
            bool watcher = LevelPieceWatcher.Enabled;
            LevelPieceWatcher.Enabled = false;
            var mist = Shader.GetGlobalVector("_HP_MistParams");
            Shader.SetGlobalVector("_HP_MistParams", Vector4.zero);
            Scene stage = default;
            int done = 0;
            RenderTexture rt = null;
            try
            {
                stage = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(stage);
                RenderSettings.fog = false;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.62f, 0.66f, 0.72f);
                RenderSettings.ambientEquatorColor = new Color(0.45f, 0.45f, 0.42f);
                RenderSettings.ambientGroundColor = new Color(0.25f, 0.23f, 0.2f);
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Stage ground";
                ground.layer = CourseKit.Layer("Environment");
                ground.transform.position = Stage + Vector3.down * 0.5f;
                ground.transform.localScale = new Vector3(400f, 1f, 400f);
                ground.GetComponent<Renderer>().enabled = false;
                var sun = new GameObject("Stage sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.3f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
                var cam = new GameObject("Stage camera").AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Backdrop;
                cam.fieldOfView = 30f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 2000f;
                rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                cam.targetTexture = rt;
                Physics.SyncTransforms();
                for (int i = 0; i < todo.Count; i++)
                {
                    var (entry, look) = todo[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Palette", "Rendering " + entry.Label, (float)i / todo.Count)) break;
                    GameObject made = null;
                    try
                    {
                        var c = PlaceContext.At(Stage, Vector3.up);
                        c.Scene = stage;
                        made = entry.Look(look).Create(c);
                        if (made == null) continue;
                        if (entry.Preset != null && made.TryGetComponent<LevelPiece>(out var piece))
                        {
                            entry.Preset.ApplyTo(piece);
                            LevelPieceBuilder.Rebuild(piece);
                        }
                        Shoot(cam, made, rt, PicturePath(entry.ThumbnailKey(look)));
                        File.WriteAllText(PicturePath(entry.ThumbnailKey(look)) + ".sig", Signature(entry, look));
                        loaded.Remove(entry.ThumbnailKey(look));
                        done++;
                    }
                    catch (Exception e) { Debug.LogWarning($"[Palette] no picture for {entry.Label}: {e.Message}"); }
                    finally
                    {
                        if (made != null)
                        {
                            Undo.ClearUndo(made);
                            Object.DestroyImmediate(made);
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c => c.targetTexture == rt && rt != null)) cam.targetTexture = null;
                if (rt != null) Object.DestroyImmediate(rt);
                if (stage.IsValid())
                {
                    SceneManager.SetActiveScene(active);
                    EditorSceneManager.CloseScene(stage, true);
                }
                Shader.SetGlobalVector("_HP_MistParams", mist);
                LevelPieceWatcher.Enabled = watcher;
                Selection.objects = selection;
            }
            return done;
        }

        /// <summary>Frames what <paramref name="made"/> draws from three quarters above and writes the picture.</summary>
        static void Shoot(Camera cam, GameObject made, RenderTexture rt, string path)
        {
            var renderers = made.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer)).ToList();
            var bounds = renderers.Count > 0 ? renderers[0].bounds : new Bounds(made.transform.position, Vector3.one * 2f);
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float radius = Mathf.Max(0.5f, bounds.extents.magnitude);
            var dir = new Vector3(-1f, 0.75f, -1.2f).normalized;
            float distance = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
            cam.transform.position = bounds.center + dir * distance;
            cam.transform.LookAt(bounds.center);
            cam.farClipPlane = distance + radius * 2f + 10f;
            cam.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
