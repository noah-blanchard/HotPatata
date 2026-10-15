using System;
using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// A saved setting of a piece (ARCHITECTURE §27): a "lookout deck", a "worn stair", its widths, rails, supports and wear, never its
    /// shape. Applied to a piece (its inspector), or dragged from the Project window into the Scene view to make a new piece there.
    /// </summary>
    [CreateAssetMenu(fileName = "TimberPreset", menuName = "HotPatata/Level/Timber Preset")]
    public sealed class TimberPreset : ScriptableObject
    {
        [SerializeField] string pieceType;
        [SerializeField, TextArea(3, 12)] string settings;

        /// <summary>The piece's own place and shape, kept when a preset is applied.</summary>
        static readonly HashSet<string> ShapeFields = new HashSet<string>
        {
            "m_Script", "pieceId", "output", "outline", "openings", "trunk", "points", "start", "end", "a", "b", "startAttach", "endAttach", "closed",
        };

        public Type PieceType => string.IsNullOrEmpty(pieceType) ? null : Type.GetType(pieceType);

        /// <summary>Stores <paramref name="piece"/>'s settings.</summary>
        public void Capture(LevelPiece piece)
        {
            pieceType = piece.GetType().AssemblyQualifiedName;
            settings = EditorJsonUtility.ToJson(piece);
            EditorUtility.SetDirty(this);
        }

        /// <summary>Gives <paramref name="piece"/> these settings (its shape and place stay); false when the preset is for another kind of piece.</summary>
        public bool ApplyTo(LevelPiece piece)
        {
            if (PieceType != piece.GetType() || string.IsNullOrEmpty(settings)) return false;
            var holder = new GameObject("preset") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var source = (LevelPiece)holder.AddComponent(piece.GetType());
                EditorJsonUtility.FromJsonOverwrite(settings, source);
                var from = new SerializedObject(source);
                var to = new SerializedObject(piece);
                Undo.RecordObject(piece, "Apply timber preset");
                var p = from.GetIterator();
                for (bool enter = true; p.NextVisible(enter); enter = false)
                    if (!ShapeFields.Contains(p.name)) to.CopyFromSerializedProperty(p);
                to.ApplyModifiedProperties();
            }
            finally { Object.DestroyImmediate(holder); }
            return true;
        }

        /// <summary>Saves a new preset of <paramref name="piece"/> where the author picks.</summary>
        public static TimberPreset SaveAs(LevelPiece piece)
        {
            string path = EditorUtility.SaveFilePanelInProject("Save timber preset", piece.name + " preset", "asset", "A preset keeps a piece's settings, not its shape.",
                                                               "Assets/ScriptableObjects");
            if (string.IsNullOrEmpty(path)) return null;
            var preset = CreateInstance<TimberPreset>();
            preset.Capture(piece);
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            return preset;
        }

        // ------------------------------------------------------------------ dragged into the Scene view

        [InitializeOnLoadMethod]
        static void HookDrop() => SceneView.duringSceneGui += OnSceneDrop;

        static void OnSceneDrop(SceneView view)
        {
            var e = Event.current;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;
            var preset = DragAndDrop.objectReferences.OfType<TimberPreset>().FirstOrDefault();
            if (preset == null || preset.PieceType == null) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                var at = Physics.Raycast(ray, out var hit, 2000f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : ray.GetPoint(20f);
                var piece = LevelMenu.CreatePiece(preset.PieceType, preset.name, at);
                preset.ApplyTo(piece);
            }
            e.Use();
        }
    }
}
