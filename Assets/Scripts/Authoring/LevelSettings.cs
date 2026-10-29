using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>The day an authored level borrows (ARCHITECTURE §27): one of the nature courses' days, applied by the editor's look.</summary>
    public enum LevelDay { Wilds, Canopy, Temple, Ranger }

    /// <summary>
    /// How an authored level's terrain is drawn (ARCHITECTURE §27.4): the valley ground of M18 (one triplanar material: litter on the flat,
    /// rock on the slopes), or the layered ground of M21 (eight layers baked from rules and painted by hand).
    /// </summary>
    public enum GroundLook { Valley, Layered }

    /// <summary>The groups an authored level keeps its pieces in.</summary>
    public enum LevelGroup { Timber, Trees, Kit, Props }

    /// <summary>
    /// An authored level's settings (ARCHITECTURE §27, docs/LEVEL_AUTHORING.md): the name the menu shows, the players it needs
    /// online and the day it is lit by, and the groups new pieces go in. Read by the editor tools (Register In Menu, Apply Look);
    /// gameplay never reads it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelSettings : MonoBehaviour
    {
        [SerializeField, Tooltip("The level's name in the menu (the scene's name when empty).")] string displayName;
        [SerializeField, Range(1, 4), Tooltip("Players the level needs online (PROJECT_SPEC §15e).")] int minPlayers = 2;
        [SerializeField, Range(1, 4), Tooltip("Players the level allows online, at most (PROJECT_SPEC §16: a level for two is for exactly two).")] int maxPlayers = 4;
        [SerializeField, Tooltip("The day that lights the level, from dawn at the start to its last moment at the last checkpoint.")] LevelDay day = LevelDay.Ranger;
        [Header("Mist (baked from the terrain on every save)")]
        [SerializeField, Range(0f, 3f), Tooltip("The day's mist, thinner (under 1) or thicker (over 1).")] float mistDensity = 1f;
        [SerializeField, Range(0f, 10f), Tooltip("How far under the ground the mist floor lies (m): the team walks in its veil.")] float mistDepth = 2.5f;
        [Header("Sound")]
        [SerializeField, Tooltip("The ambience (Assets/Audio/Ambience/Environments) where a section says nothing.")] string defaultAmbience = "Forest";
        [SerializeField, Tooltip("The ambience of each section: from the start (0), then from each checkpoint id. Empty = the default.")]
        List<string> sectionAmbience = new List<string>();
        [SerializeField, Tooltip("The music's act of each section: from the start (0), then from each checkpoint id. Empty = act 1.")]
        List<int> sectionActs = new List<int>();
        [Header("Ground")]
        [SerializeField, Tooltip("Valley: one material (levels made before M21). Layered: eight layers baked from rules and painted by hand.")]
        GroundLook ground;
        [SerializeField, Tooltip("Bake the ground rules on every save (off: the Level inspector's button bakes them).")] bool bakeGroundOnSave;
        [SerializeField, HideInInspector] Transform timber, trees, kit, props;

        public string DisplayName { get => displayName; set => displayName = value; }
        public int MinPlayers { get => minPlayers; set => minPlayers = Mathf.Clamp(value, 1, 4); }
        /// <summary>Players the level allows online, never under <see cref="MinPlayers"/>.</summary>
        public int MaxPlayers { get => Mathf.Max(minPlayers, maxPlayers); set => maxPlayers = Mathf.Clamp(value, 1, 4); }
        public LevelDay Day { get => day; set => day = value; }
        public float MistDensity { get => mistDensity; set => mistDensity = Mathf.Clamp(value, 0f, 3f); }
        public float MistDepth { get => mistDepth; set => mistDepth = Mathf.Clamp(value, 0f, 10f); }
        public string DefaultAmbience { get => defaultAmbience; set => defaultAmbience = value ?? ""; }
        public List<string> SectionAmbience => sectionAmbience;
        public List<int> SectionActs => sectionActs;
        public GroundLook Ground { get => ground; set => ground = value; }
        public bool BakeGroundOnSave { get => bakeGroundOnSave; set => bakeGroundOnSave = value; }

        /// <summary>Where new pieces of a kind go (the level itself when the group is gone).</summary>
        public Transform Group(LevelGroup group)
        {
            var t = group switch { LevelGroup.Timber => timber, LevelGroup.Trees => trees, LevelGroup.Kit => kit, _ => props };
            return t != null ? t : transform;
        }

        /// <summary>Editor: the level's groups.</summary>
        public void SetGroups(Transform timberGroup, Transform treesGroup, Transform kitGroup, Transform propsGroup)
        {
            timber = timberGroup;
            trees = treesGroup;
            kit = kitGroup;
            props = propsGroup;
        }
    }
}
