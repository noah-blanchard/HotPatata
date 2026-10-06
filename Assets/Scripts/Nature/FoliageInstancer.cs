using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HotPatata
{
    /// <summary>
    /// Presentation only (ARCHITECTURE §25.3): draws a <see cref="FoliageSet"/> (grass, ferns, bushes, trees and rocks beyond the
    /// course) with GPU instancing, no scene object per plant. For each camera, every 32 m cell outside the view is skipped, the
    /// others pick a level of detail by distance and are drawn in chunks; only near trees cast shadows. Nothing collides: the
    /// scatter stays out of the pass corridors and off anything players walk into (checked by <c>PatataWildsTests</c>).
    /// </summary>
    [ExecuteAlways]
    public class FoliageInstancer : MonoBehaviour
    {
        const int Chunk = 1023;

        [SerializeField] FoliageSet set;

        sealed class CellData
        {
            public Bounds bounds;
            public Matrix4x4[] matrices;
        }

        readonly List<(FoliageSet.Batch batch, List<CellData> cells)> data = new List<(FoliageSet.Batch, List<CellData>)>();
        readonly Plane[] planes = new Plane[6];

        public FoliageSet Set => set;

        public void Configure(FoliageSet foliage)
        {
            set = foliage;
            Build();
        }

        void OnEnable()
        {
            Build();
            RenderPipelineManager.beginCameraRendering += Draw;
        }

        void OnDisable() => RenderPipelineManager.beginCameraRendering -= Draw;

        void Build()
        {
            data.Clear();
            if (set == null) return;
            foreach (var batch in set.batches)
            {
                if (batch.lods == null || batch.lods.Length == 0 || batch.lods[0] == null) continue;
                var cells = new List<CellData>();
                float reach = batch.lods[0].bounds.extents.magnitude * batch.maxScale + 0.5f;
                foreach (var cell in batch.cells)
                {
                    int n = cell.Count;
                    if (n == 0) continue;
                    var matrices = new Matrix4x4[n];
                    var bounds = new Bounds();
                    for (int i = 0; i < n; i++)
                    {
                        var (position, yaw, scale) = FoliageSet.Unpack(batch, cell, i);
                        matrices[i] = Matrix4x4.TRS(position, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
                        var b = new Bounds(position + Vector3.up * batch.lods[0].bounds.center.y * scale, Vector3.one * reach * 2f);
                        if (i == 0) bounds = b; else bounds.Encapsulate(b);
                    }
                    cells.Add(new CellData { bounds = bounds, matrices = matrices });
                }
                data.Add((batch, cells));
            }
        }

        void Draw(ScriptableRenderContext context, Camera cam)
        {
            if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection || data.Count == 0) return;
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            var eye = cam.transform.position;
            foreach (var (batch, cells) in data)
            {
                foreach (var cell in cells)
                {
                    if (!GeometryUtility.TestPlanesAABB(planes, cell.bounds)) continue;
                    float d = Vector3.Distance(eye, cell.bounds.ClosestPoint(eye));
                    int lod = -1;
                    for (int i = 0; i < batch.lodDistances.Length && i < batch.lods.Length; i++)
                        if (d < batch.lodDistances[i]) { lod = i; break; }
                    if (lod < 0) continue;
                    var mesh = batch.lods[lod];
                    if (mesh == null) continue;
                    bool shadows = lod < batch.lodShadows.Length && batch.lodShadows[lod] && d < batch.shadowDistance;
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        var material = batch.materials[Mathf.Min(sub, batch.materials.Length - 1)];
                        if (material == null) continue;
                        var rp = new RenderParams(material)
                        {
                            camera = cam,
                            layer = gameObject.layer,
                            worldBounds = cell.bounds,
                            shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                            receiveShadows = true
                        };
                        for (int start = 0; start < cell.matrices.Length; start += Chunk)
                            Graphics.RenderMeshInstanced(rp, mesh, sub, cell.matrices, Mathf.Min(Chunk, cell.matrices.Length - start), start);
                    }
                }
            }
        }

        /// <summary>Tests: the world box of every instance (its nearest level's mesh, turned and scaled).</summary>
        public List<Bounds> InstanceBounds()
        {
            var result = new List<Bounds>();
            if (set == null) return result;
            foreach (var batch in set.batches)
            {
                if (batch.lods == null || batch.lods.Length == 0 || batch.lods[0] == null) continue;
                var local = batch.lods[0].bounds;
                foreach (var cell in batch.cells)
                    for (int i = 0; i < cell.Count; i++)
                    {
                        var (position, yaw, scale) = FoliageSet.Unpack(batch, cell, i);
                        var m = Matrix4x4.TRS(position, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
                        var b = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
                        for (int c = 0; c < 8; c++)
                        {
                            var corner = local.center + Vector3.Scale(local.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                            b.Encapsulate(m.MultiplyPoint3x4(corner));
                        }
                        result.Add(b);
                    }
            }
            return result;
        }
    }
}
