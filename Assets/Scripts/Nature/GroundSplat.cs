using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The layered ground of an authored level (ARCHITECTURE §27.4): hands the terrain's two splat control maps (eight layers) to its
    /// material, <c>HotPatata/NatureGround</c>, which draws every layer in one pass. Presentation only: it reads the terrain and never
    /// changes it. The editor can tint the texels the author painted by hand (<see cref="ShowPainted"/>).
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(Terrain)), DisallowMultipleComponent]
    public sealed class GroundSplat : MonoBehaviour
    {
        static readonly int Control0 = Shader.PropertyToID("_HP_Control0"), Control1 = Shader.PropertyToID("_HP_Control1"),
            ControlRes = Shader.PropertyToID("_HP_ControlRes"), Painted = Shader.PropertyToID("_HP_PaintedMask"), PaintedOn = Shader.PropertyToID("_HP_ShowPainted");

        Terrain terrain;
        MaterialPropertyBlock block;
        Texture2D paintedMask;
        bool showPainted;

        /// <summary>Editor: shows the hand-painted texels in red over the ground (<paramref name="mask"/> white where painted), or hides them.</summary>
        public void ShowPainted(Texture2D mask, bool on)
        {
            paintedMask = mask;
            showPainted = on && mask != null;
            Apply();
        }

        public bool ShowingPainted => showPainted;

        void OnEnable() => Apply();

        /// <summary>Binds the control maps again (the alphamaps were resized or replaced).</summary>
        public void Apply()
        {
            if (terrain == null) terrain = GetComponent<Terrain>();
            var data = terrain != null ? terrain.terrainData : null;
            if (data == null) return;
            block ??= new MaterialPropertyBlock();
            terrain.GetSplatMaterialPropertyBlock(block);
            var maps = data.alphamapTextures;
            block.SetTexture(Control0, maps.Length > 0 ? maps[0] : Texture2D.blackTexture);
            block.SetTexture(Control1, maps.Length > 1 ? maps[1] : Texture2D.blackTexture);
            block.SetFloat(ControlRes, data.alphamapResolution);
            block.SetTexture(Painted, paintedMask != null ? paintedMask : Texture2D.blackTexture);
            block.SetFloat(PaintedOn, showPainted ? 1f : 0f);
            terrain.SetSplatMaterialPropertyBlock(block);
        }
    }
}
