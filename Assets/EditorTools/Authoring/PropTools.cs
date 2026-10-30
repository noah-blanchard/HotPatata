using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>A prop's inspector (ARCHITECTURE §27): Solid gives it a box round what it draws (on Environment), or takes it away.</summary>
    [CustomEditor(typeof(PropSolid)), CanEditMultipleObjects]
    public sealed class PropSolidEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            bool solid = targets.Cast<PropSolid>().All(p => p.Solid);
            EditorGUI.showMixedValue = targets.Cast<PropSolid>().Select(p => p.Solid).Distinct().Count() > 1;
            EditorGUI.BeginChangeCheck();
            bool wanted = EditorGUILayout.Toggle(new GUIContent("Solid", "A box round the prop that players and the bomb collide with. Off: decoration (walk through it)."), solid);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
                foreach (var prop in targets.Cast<PropSolid>()) SetSolid(prop, wanted);
            EditorGUILayout.HelpBox("A prop that looks walkable or in the way should be solid; small things (bowls, stones, plants) stay decoration.", MessageType.None);
        }

        /// <summary>Makes <paramref name="prop"/> solid (a box child on Environment fitted to its renderers) or decoration.</summary>
        public static void SetSolid(PropSolid prop, bool solid, bool undo = true)
        {
            if (undo) Undo.RecordObject(prop, solid ? "Solid prop" : "Decoration prop");
            if (prop.Box != null)
            {
                if (undo) Undo.DestroyObjectImmediate(prop.Box.gameObject);
                else Object.DestroyImmediate(prop.Box.gameObject);
            }
            BoxCollider box = null;
            if (solid)
            {
                var renderers = prop.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    var t = prop.transform;
                    var min = Vector3.positiveInfinity;
                    var max = Vector3.negativeInfinity;
                    foreach (var r in renderers)
                    {
                        var b = r.bounds;
                        for (int c = 0; c < 8; c++)
                        {
                            var corner = t.InverseTransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                            min = Vector3.Min(min, corner);
                            max = Vector3.Max(max, corner);
                        }
                    }
                    var go = new GameObject("Solid") { layer = CourseKit.Layer("Environment") };
                    if (undo) Undo.RegisterCreatedObjectUndo(go, "Solid prop");
                    go.transform.SetParent(t, false);
                    box = go.AddComponent<BoxCollider>();
                    box.center = (min + max) / 2f;
                    box.size = max - min;
                }
            }
            prop.Configure(solid && box != null, box);
            EditorUtility.SetDirty(prop);
            if (undo && prop.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(prop.gameObject.scene);
        }
    }
}
