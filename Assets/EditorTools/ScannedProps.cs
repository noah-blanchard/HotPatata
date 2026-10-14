using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// Places the scanned props (Poly Haven models and CC0 museum scans, ARCHITECTURE §25.3) for the course builders: a model fitted
    /// into a box (a relief inlaid flush in a wall face, a carved slab drawn inside a mover's collider), a model stood at a height
    /// under any rotation (a statue, a colossus in the jungle), or one piece of a kit (a pachira of the four in its file). Visuals
    /// only: every collider a file brings is removed; a prop that must collide gets its own box from its builder.
    /// </summary>
    public static class ScannedProps
    {
        /// <summary>The material of each placed model (NatureMaterialBuilder.Library).</summary>
        static readonly Dictionary<string, string> Materials = new Dictionary<string, string>
        {
            { "maya_stela_fragment", "Nature_Prop_MayaStela" },
            { "maya_relief_pillar", "Nature_Prop_MayaRelief" },
            { "palenque_relief_wall", "Nature_Prop_PalenqueWall" },
            { "maya_head_helmet", "Nature_Prop_MayaHead" },
            { "olmec_standing_figure", "Nature_Prop_OlmecFigure" },
            { "aztec_chalchiuhtlicue", "Nature_Prop_Chalchiuhtlicue" },
            { "olmec_face_fragment", "Nature_Prop_OlmecFace" },
            { "ballgame_palma", "Nature_Prop_Palma" },
            { "precolumbian_bowl", "Nature_Prop_OfferingBowl" },
            { "face_urn", "Nature_Prop_FaceUrn" },
            { "antique_ceramic_vase_01", "Nature_Prop_AntiqueVase" },
            { "ceramic_vase_01", "Nature_Prop_Vase1" },
            { "ceramic_vase_03", "Nature_Prop_Vase3" },
            { "planter_pot_clay", "Nature_Prop_ClayPot" },
            { "brass_diya_lantern", "Nature_Prop_DiyaLantern" },
            { "brass_pot_01", "Nature_Prop_BrassPot" },
            { "carved_wooden_plate", "Nature_Prop_CarvedPlate" },
            { "duck_nest_eggs", "Nature_Prop_Nest" },
            { "tree_frog", "Nature_Prop_Frog" },
            { "wooden_crate_01", "Nature_Prop_Crate1" },
            { "wooden_crate_02", "Nature_Prop_Crate2" },
            { "wooden_bucket_01", "Nature_Prop_Bucket" },
            { "wicker_basket_01", "Nature_Prop_Basket1" },
            { "wicker_basket_02", "Nature_Prop_Basket2" },
            { "wooden_ladder_02", "Nature_Prop_Ladder" },
            { "wine_barrel_01", "Nature_Prop_WineBarrel" },
            { "hatchet", "Nature_Prop_Hatchet" },
            { "wooden_stool_01", "Nature_Prop_Stool" },
            { "jug_01", "Nature_Prop_Jug" },
            { "wooden_bowl_01", "Nature_Prop_Bowl" },
            { "root_cluster_02", "Nature_Prop_Roots2" },
            { "single_root", "Nature_Prop_Root" },
        };

        public static string MaterialOf(string model) =>
            Materials.TryGetValue(model, out var m) ? m : throw new System.ArgumentException("no material for " + model);

        static GameObject Asset(string model) =>
            (NatureAssetImporter.ModelPath(model) is string path ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null)
            ?? throw new System.InvalidOperationException("missing model " + model + " (run tools/Fetch-PolyHaven.ps1 or tools/Fetch-Sketchfab.ps1)");

        /// <summary>
        /// A model fitted exactly into a box: <paramref name="center"/> and <paramref name="rotation"/> give the box's frame (world),
        /// <paramref name="size"/> its extent in that frame; <paramref name="orient"/> turns the model in the frame first (a model faces -Z
        /// and stands along +Y as imported: <c>Quaternion.Euler(90, 0, 0)</c> lays its face up). Only quarter turns keep the fit exact: a
        /// box is scaled per axis, so the model is stretched to fill it, never sheared. The result stays inside the box.
        /// </summary>
        public static GameObject Fit(Transform parent, string model, Vector3 center, Quaternion rotation, Quaternion orient, Vector3 size,
                                     bool shadows = true, string material = null)
        {
            var holder = new GameObject(model).transform;
            holder.SetParent(parent, false);
            holder.SetPositionAndRotation(center, rotation);
            var scaler = new GameObject("Fit").transform;
            scaler.SetParent(holder, false);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Asset(model), scaler);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = orient * go.transform.localRotation;   // keep the file's axis conversion
            var b = BoundsIn(holder, go);
            var k = new Vector3(size.x / Mathf.Max(1e-4f, b.size.x), size.y / Mathf.Max(1e-4f, b.size.y), size.z / Mathf.Max(1e-4f, b.size.z));
            scaler.localScale = k;
            scaler.localPosition = -Vector3.Scale(b.center, k);
            Finish(go, material ?? MaterialOf(model), shadows);
            return holder.gameObject;
        }

        /// <summary>The extent of a model turned by <paramref name="orient"/>, at its file's scale (for its proportions).</summary>
        public static Vector3 Extent(string model, Quaternion orient)
        {
            var probe = new GameObject("probe").transform;
            try
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(Asset(model), probe);
                go.transform.localRotation = orient * go.transform.localRotation;
                return BoundsIn(probe, go).size;
            }
            finally { Object.DestroyImmediate(probe.gameObject); }
        }

        /// <summary>
        /// A model under any rotation, scaled uniformly to <paramref name="height"/> (along world up once turned), its lowest point
        /// <paramref name="sink"/> metres under <paramref name="foot"/> (a statue half buried in the jungle floor).
        /// </summary>
        public static GameObject Stand(Transform parent, string model, Vector3 foot, Quaternion rotation, float height, float sink = 0f,
                                       bool shadows = true, string material = null)
        {
            var holder = new GameObject(model).transform;
            holder.SetParent(parent, false);
            holder.position = foot;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Asset(model), holder);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = rotation * go.transform.localRotation;
            var b = WorldBounds(go);
            float s = height / Mathf.Max(1e-4f, b.size.y);
            go.transform.localScale *= s;
            b = WorldBounds(go);
            go.transform.position += new Vector3(foot.x - b.center.x, foot.y - sink - b.min.y, foot.z - b.center.z);
            Finish(go, material ?? MaterialOf(model), shadows);
            return holder.gameObject;
        }

        /// <summary>
        /// One mesh of a kit file (a pachira of the four in <c>pachira_aquatica_01</c>, a barrel of the set): the meshes named in
        /// <paramref name="meshes"/>, each slot drawn by <paramref name="materialFor"/>(mesh name, slot), scaled uniformly to
        /// <paramref name="height"/> and stood on <paramref name="foot"/>.
        /// </summary>
        public static GameObject KitPiece(Transform parent, string model, string[] meshes, System.Func<string, int, string> materialFor, Vector3 foot,
                                          float yaw, float height, bool shadows = true)
        {
            var asset = Asset(model);
            var holder = new GameObject(model + " " + meshes[0]).transform;
            holder.SetParent(parent, false);
            holder.position = foot;
            var body = new GameObject("Piece").transform;
            body.SetParent(holder, false);
            foreach (var filter in asset.GetComponentsInChildren<MeshFilter>(true).Where(f => meshes.Contains(f.sharedMesh.name)))
            {
                var part = new GameObject(filter.sharedMesh.name);
                part.transform.SetParent(body, false);
                // the file's own transform for this piece (its axis conversion), relative to the file's root
                var m = asset.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                part.transform.localPosition = m.GetColumn(3);
                part.transform.localRotation = m.rotation;
                part.transform.localScale = m.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var r = part.AddComponent<MeshRenderer>();
                r.sharedMaterials = Enumerable.Range(0, filter.sharedMesh.subMeshCount)
                    .Select(i => NatureMaterialBuilder.Load(materialFor(filter.sharedMesh.name, i))).ToArray();
                r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (body.childCount == 0) throw new System.ArgumentException($"{model} has no mesh named {string.Join(", ", meshes)}");
            body.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var b = WorldBounds(body.gameObject);
            body.localScale = Vector3.one * (height / Mathf.Max(1e-4f, b.size.y));
            b = WorldBounds(body.gameObject);
            body.position += new Vector3(foot.x - b.center.x, foot.y - b.min.y, foot.z - b.center.z);
            return holder.gameObject;
        }

        static void Finish(GameObject go, string material, bool shadows)
        {
            var mat = NatureMaterialBuilder.Load(material);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }

        /// <summary>The box of every mesh under <paramref name="go"/>, in <paramref name="frame"/>'s space (tight for quarter turns).</summary>
        static Bounds BoundsIn(Transform frame, GameObject go)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var f in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null) continue;
                var mb = f.sharedMesh.bounds;
                var m = frame.worldToLocalMatrix * f.transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        public static Bounds WorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
