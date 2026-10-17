using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Dev only: the name of a showcase item, drawn over it in the Scene view (the LevelToolsShowcase scene, docs/LEVEL_AUTHORING.md §25).
    /// Never in a level: the showcase is a dev scene, never registered in the menu.
    /// </summary>
    public sealed class ShowcaseLabel : MonoBehaviour
    {
        [SerializeField] string text;

        public string Text { get => text; set => text = value; }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (string.IsNullOrEmpty(text)) return;
            var view = UnityEditor.SceneView.currentDrawingSceneView;
            if (view != null && Vector3.Distance(view.camera.transform.position, transform.position) > 150f) return;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 3f, text, UnityEditor.EditorStyles.whiteBoldLabel);
        }
#endif
    }
}
