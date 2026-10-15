using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HotPatata;
using UnityEngine.Rendering.Universal;

namespace HotPatata.Editor
{
    /// <summary>
    /// Review renders of a course from fixed, named views (ARCHITECTURE §25.5): an offscreen camera through the scene's own look
    /// (post-processing on), so the same view can be shot before and after a change. <c>grey</c> draws every renderer in one plain
    /// grey (the blockout check: do the shapes still read without textures?). Editor-only; nothing is saved in the scene.
    /// </summary>
    public static class CourseShots
    {
        public struct View
        {
            public string name;
            public Vector3 from, at;
            public float fov;
            /// <summary>Where in the course's day the view is seen (a <see cref="TimeOfDayBlender"/> position), or -1: as the scene stands.</summary>
            public float time;

            public View(string name, Vector3 from, Vector3 at, float fov = 70f, float time = -1f)
            {
                this.name = name;
                this.from = from;
                this.at = at;
                this.fov = fov;
                this.time = time;
            }
        }

        /// <summary>Renders each view to <c>&lt;folder&gt;/&lt;prefix&gt;&lt;name&gt;.png</c>; returns the files written.</summary>
        public static List<string> Shoot(string folder, string prefix, IEnumerable<View> views, bool grey = false, int width = 1600, int height = 900, bool jpg = false)
        {
            Directory.CreateDirectory(folder);
            var written = new List<string>();
            var swapped = new List<(Renderer r, Material[] mats)>();
            Material plain = null;
            if (grey)
            {
                plain = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.62f, 0.62f, 0.6f) };
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (r is ParticleSystemRenderer) continue;
                    swapped.Add((r, r.sharedMaterials));
                    r.sharedMaterials = Enumerable.Repeat(plain, r.sharedMaterials.Length).ToArray();
                }
            }
            var go = new GameObject("CourseShotsCamera") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var cam = go.AddComponent<Camera>();
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = !grey;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cam.nearClipPlane = 0.08f;
                cam.farClipPlane = 2500f;
                var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                bool warmed = false;
                foreach (var v in views)
                {
                    if (v.time >= 0f) SetDay(v.time);
                    cam.transform.position = v.from;
                    cam.transform.LookAt(v.at);
                    cam.fieldOfView = v.fov;
                    if (!warmed) { cam.Render(); warmed = true; }   // the first render after a scene change can come out empty
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    string file = Path.Combine(folder, prefix + v.name.Replace(' ', '_') + (jpg ? ".jpg" : ".png"));
                    File.WriteAllBytes(file, jpg ? tex.EncodeToJPG(86) : tex.EncodeToPNG());
                    written.Add(file);
                }
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
            }
            finally
            {
                SetDay(0f);
                Object.DestroyImmediate(go);
                foreach (var (r, mats) in swapped)
                    if (r != null) r.sharedMaterials = mats;
                if (plain != null) Object.DestroyImmediate(plain);
            }
            return written;
        }

        /// <summary>Shows the scene at <paramref name="time"/> of its day (sun, ambient, haze, sky, mist), as the blender does in play.</summary>
        static void SetDay(float time)
        {
            var blender = Object.FindFirstObjectByType<TimeOfDayBlender>();
            if (blender == null) return;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(TimeOfDayBlender).GetMethod("Apply", flags)?.Invoke(blender, new object[] { time });
            typeof(TimeOfDayBlender).GetMethod("ApplyMist", flags)?.Invoke(blender, new object[] { time });
        }
    }
}
