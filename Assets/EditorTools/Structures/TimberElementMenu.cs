using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// GameObject/HotPatata Level/Element (ARCHITECTURE §27.1, docs/LEVEL_AUTHORING.md §17): one plank, post, beam, rope or iron strap,
    /// where the Scene view looks. Made while an element is selected, it goes beside it (in its structure: a plank there is walkable);
    /// else into the level's timber group.
    /// </summary>
    public static class TimberElementMenu
    {
        const string Add = "GameObject/HotPatata Level/Element/";

        [MenuItem(Add + "Plank", priority = 40)]
        static void AddPlank() => Board("Plank", TimberElementKind.Plank, TimberWood.Plank, new Vector3(0.2f, 0.05f, 2f), Quaternion.identity, 0.05f, false);

        [MenuItem(Add + "Post", priority = 41)]
        static void AddPost() => Board("Post", TimberElementKind.Post, TimberWood.Timber, new Vector3(0.2f, 0.2f, 2.5f), Quaternion.Euler(-90f, 0f, 0f), 1.25f, true);

        [MenuItem(Add + "Beam", priority = 42)]
        static void AddBeam() => Board("Beam", TimberElementKind.Beam, TimberWood.Timber, new Vector3(0.16f, 0.2f, 3f), Quaternion.identity, 2f, false);

        [MenuItem(Add + "Iron Strap", priority = 44)]
        static void AddIron() => Board("Iron strap", TimberElementKind.Iron, TimberWood.Iron, new Vector3(0.06f, 0.01f, 0.4f), Quaternion.identity, 1f, false);

        [MenuItem(Add + "Rope", priority = 43)]
        static void AddRope()
        {
            var go = New("Rope", 2f);
            var rope = Undo.AddComponent<TimberPath>(go);
            rope.Kind = TimberElementKind.Rope;
            rope.Wood = TimberWood.Rope;
            rope.Shape = TimberPath.Profile.Round;
            rope.Radius = 0.028f;
            rope.Sag = 0.2f;
            rope.Points.Clear();
            rope.Points.AddRange(new[] { Vector3.zero, Vector3.forward * 3f });
            Done(rope);
        }

        static void Board(string name, TimberElementKind kind, TimberWood wood, Vector3 size, Quaternion rotation, float above, bool solid)
        {
            var go = New(name, above);
            go.transform.rotation = rotation;
            var board = Undo.AddComponent<TimberBoard>(go);
            board.Kind = kind;
            board.Wood = wood;
            board.Size = size;
            board.Solid = solid;
            board.Grain = Random.Range(0, TimberElement.GrainVariants);
            // a plank laid in a structure is part of its walking surface
            if (kind == TimberElementKind.Plank && board.Structure != null) board.Walk = TimberWalk.Slab;
            Done(board);
        }

        static GameObject New(string name, float above)
        {
            var near = Selection.activeTransform;
            Transform parent = null;
            if (near != null && (near.GetComponent<TimberElement>() != null || near.GetComponentInParent<FrozenStructure>() != null))
                parent = near.GetComponent<TimberElement>() != null ? near.parent : near;
            else
            {
                var settings = LevelScenes.SettingsOf(EditorSceneManager.GetActiveScene());
                if (settings != null) parent = settings.Group(LevelGroup.Timber);
            }
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Add " + name);
            if (parent != null) Undo.SetTransformParent(go.transform, parent, "Add " + name);
            go.transform.position = Ground() + Vector3.up * above;
            Selection.activeGameObject = go;
            return go;
        }

        static void Done(TimberElement element)
        {
            TimberElementBuilder.Refresh(element, true);
            TimberCatalog.Flush();
            EditorUtility.SetDirty(element);
        }

        /// <summary>Where the Scene view looks, on the ground.</summary>
        static Vector3 Ground()
        {
            var view = SceneView.lastActiveSceneView;
            var pivot = view != null ? view.pivot : Vector3.zero;
            float ground = LevelNature.GroundAt(EditorSceneManager.GetActiveScene(), new Vector2(pivot.x, pivot.z));
            return new Vector3(pivot.x, float.IsNaN(ground) ? pivot.y : ground, pivot.z);
        }
    }
}
