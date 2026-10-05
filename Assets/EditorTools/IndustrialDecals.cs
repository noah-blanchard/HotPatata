using System.Collections.Generic;
using System.IO;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// Random surface detail for the industrial look (ARCHITECTURE §25.2): stains, oil, cracks, scuffs, drips, stencils... as alpha
    /// quads lying 6 mm above a floor or a wall, lit like everything else (<c>HotPatata/Industrial</c> in decal mode), scattered with a
    /// fixed seed so a rebuild gives the same plant. Every PNG of <c>Assets/Art/Textures/Industrial/Decals</c> is used; a name with
    /// drip, leak or streak goes on walls only, arrow, stencil, number or line on floors only. Purely visual (no collider, marked
    /// <see cref="CourseDecoration"/>), never over a trigger zone, a moving platform or the edge of a floor.
    /// </summary>
    public static class IndustrialDecals
    {
        const string DecalDir = IndustrialTextureImporter.Folder + "Decals/";
        const float Lift = 0.006f;

        public struct Kind
        {
            public Material material;
            public float aspect;      // width / height of the texture
            public bool floors, walls;
        }

        /// <summary>One material per decal texture (created when missing): alpha blended, drawn after the opaque geometry.</summary>
        public static Material MaterialFor(Texture2D texture)
        {
            string path = IndustrialMaterialBuilder.Dir + "Decals/Decal_" + texture.name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && mat.GetTexture("_BaseMap") == texture && mat.GetFloat("_Decal") > 0.5f) return mat;   // already a decal of this texture
            if (mat == null)
            {
                Directory.CreateDirectory(IndustrialMaterialBuilder.Dir + "Decals");
                mat = new Material(Shader.Find(IndustrialMaterialBuilder.ShaderName));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = Shader.Find(IndustrialMaterialBuilder.ShaderName);
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Decal", 1f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_OffsetFactor", -1f);
            mat.SetFloat("_OffsetUnits", -1f);
            mat.SetFloat("_SmoothnessMin", 0.1f);
            mat.SetFloat("_SmoothnessMax", 0.1f);
            mat.SetFloat("_VariationStrength", 0f);
            mat.SetFloat("_MacroStrength", 0f);
            mat.SetFloat("_GrungeStrength", 0f);
            mat.SetFloat("_AntiTile", 0f);
            mat.SetColor("_ShadeColor", new Color(0.95f, 0.92f, 0.9f));
            mat.DisableKeyword("_ANTITILE");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 2501;   // after every opaque surface, before the real transparents
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static List<Kind> Available()
        {
            var kinds = new List<Kind>();
            if (!AssetDatabase.IsValidFolder(DecalDir.TrimEnd('/'))) return kinds;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { DecalDir.TrimEnd('/') }).OrderBy(g => g))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                string n = tex.name.ToLowerInvariant();
                bool wallOnly = n.Contains("drip") || n.Contains("leak") || n.Contains("streak");
                bool floorOnly = n.Contains("arrow") || n.Contains("stencil") || n.Contains("number") || n.Contains("line");
                kinds.Add(new Kind { material = MaterialFor(tex), aspect = tex.width / (float)tex.height, floors = !wallOnly, walls = !floorOnly });
            }
            return kinds;
        }

        /// <summary>Scatters decals on the floors and walls of one built room (its transform must be final and its colliders synced).</summary>
        public static int Scatter(Transform room, float length, float ceiling, int seed, float density = 1f)
        {
            var kinds = Available();
            if (kinds.Count == 0) return 0;
            var rng = new Random(seed ^ room.name.GetHashCode());
            var parent = new GameObject("Decals").transform;
            parent.SetParent(room, false);
            parent.gameObject.AddComponent<CourseDecoration>();
            int environment = LayerMask.GetMask("Environment");
            int cues = LayerMask.GetMask("Trigger", "Hazard");
            int placed = 0;

            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            bool Flat(Vector3 point, Vector3 normal, float width, float height, Vector3 right, Vector3 up)
            {
                // the four corners must rest on the same surface, and nothing gameplay-related may be under the decal
                foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                {
                    var corner = point + right * (sx * width / 2f) + up * (sy * height / 2f);
                    if (!Physics.Raycast(corner + normal * 0.4f, -normal, out var h, 0.6f, environment, QueryTriggerInteraction.Ignore)) return false;
                    if (Mathf.Abs(Vector3.Dot(h.point - point, normal)) > 0.03f) return false;
                    if (h.collider.GetComponentInParent<MovingPlatform>() != null || h.collider.GetComponentInParent<Conveyor>() != null) return false;
                }
                return !Physics.CheckSphere(point, Mathf.Max(width, height) * 0.5f + 0.4f, cues, QueryTriggerInteraction.Collide);
            }

            void Place(Kind kind, Vector3 point, Vector3 normal, Vector3 upHint, float width)
            {
                float height = width / kind.aspect;
                var rotation = Quaternion.LookRotation(-normal, upHint);
                var right = rotation * Vector3.right; var up = rotation * Vector3.up;
                if (!Flat(point, normal, width, height, right, up)) return;
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.name = "Decal " + kind.material.name.Replace("Decal_", "");
                quad.transform.SetParent(parent, true);
                quad.transform.SetPositionAndRotation(point + normal * (Lift + 0.0006f * (placed % 7)), rotation);
                quad.transform.localScale = new Vector3(width, height, 1f);
                var r = quad.GetComponent<MeshRenderer>();
                r.sharedMaterial = kind.material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                placed++;
            }

            var floorKinds = kinds.Where(k => k.floors).ToList();
            var wallKinds = kinds.Where(k => k.walls).ToList();
            int floorTarget = Mathf.RoundToInt(length / 6f * density), wallTarget = Mathf.RoundToInt(length / 3.5f * density);
            for (int attempt = 0, done = 0; done < floorTarget && attempt < floorTarget * 6 && floorKinds.Count > 0; attempt++)
            {
                var from = room.TransformPoint(new Vector3(R(-10.5f, 10.5f), ceiling - 1.5f, R(0f, length)));
                if (!Physics.Raycast(from, Vector3.down, out var hit, 90f, environment, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.97f) continue;
                int before = placed;
                var kind = floorKinds[rng.Next(floorKinds.Count)];
                Place(kind, hit.point, hit.normal, Quaternion.AngleAxis(R(0f, 360f), hit.normal) * room.forward, R(1.5f, 4.5f));
                if (placed > before) done++;
            }
            for (int attempt = 0, done = 0; done < wallTarget && attempt < wallTarget * 8 && wallKinds.Count > 0; attempt++)
            {
                var column = room.TransformPoint(new Vector3(R(-9f, 9f), ceiling - 1.5f, R(0f, length)));
                if (!Physics.Raycast(column, Vector3.down, out var floor, 90f, environment, QueryTriggerInteraction.Ignore) || floor.normal.y < 0.9f) continue;
                var side = (rng.Next(2) == 0 ? 1 : -1) * room.right;
                var origin = floor.point + Vector3.up * R(0.8f, 5.5f);
                if (!Physics.Raycast(origin, side, out var wall, 20f, environment, QueryTriggerInteraction.Ignore) || Mathf.Abs(wall.normal.y) > 0.05f) continue;
                int before = placed;
                var kind = wallKinds[rng.Next(wallKinds.Count)];
                Place(kind, wall.point, wall.normal, Vector3.up, R(1.5f, 4.2f));
                if (placed > before) done++;
            }
            return placed;
        }
    }
}
