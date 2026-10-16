using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// One file of the mesh catalogue (ARCHITECTURE §27.1): its meshes are sub-assets, each named after what it draws, shared by
    /// everything that draws it in every level and prefab. Written by the editor (HotPatata.Editor.MeshCatalog), never by hand.
    /// </summary>
    public sealed class MeshBank : ScriptableObject
    {
    }
}
