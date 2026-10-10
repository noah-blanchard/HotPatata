using System;
using System.Linq;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Level-design metadata (PROJECT_SPEC §13.20): what a section FORCES the team to do and the obvious shortcuts it LOCKS.
    /// A course builder declares one per section; <c>CourseContractTests</c> checks every declared shortcut against the
    /// player's and the bomb's reach from <see cref="GameTuning"/> (gaps, climbs, lobs over a wall) and warns when one can
    /// be taken, so an intended mechanic cannot quietly become optional. Gameplay never reads it.
    /// </summary>
    public class SectionContract : MonoBehaviour
    {
        public enum ShortcutKind
        {
            Gap,     // jump from From (take-off edge) to To (landing edge): it must be out of a slide-jump's reach, mantle included
            Climb,   // from the floor at From up to the ledge at To: it must be higher than a jump plus a mantle
            Lob,     // throw over a wall whose top is To, from a thrower standing at From: a roof must seal it
            Spread   // two sources at From and To that one body cannot hold in turn: the walk between them (through Via) outlasts From's hourglass
        }

        [Serializable]
        public struct Shortcut
        {
            public string name;
            public ShortcutKind kind;
            [Tooltip("In this object's space (so it turns with its section).")] public Vector3 from;
            [Tooltip("In this object's space (so it turns with its section).")] public Vector3 to;
            [Tooltip("Spread only: the walk between the two sources goes through these points (in this object's space).")] public Vector3[] via;
        }

        /// <summary>Above the wall's top, a roof closer than this seals the wall against any lob (the bomb with a margin).</summary>
        public const float SealGap = 0.5f;
        /// <summary>Half the width a body spans past an edge on take-off and before one on landing.</summary>
        public const float BodyOverhang = 0.35f;

        [SerializeField, TextArea, Tooltip("What the team must do here (the section's question).")]
        string force;
        [SerializeField, Min(1), Tooltip("Players the section needs (3 in a course for three, PROJECT_SPEC §15e).")]
        int minPlayers = 2;
        [SerializeField, TextArea, Tooltip("Why fewer players fail here (\"With two: ...\"); required when minPlayers is 3 or more.")]
        string twoFail;
        [SerializeField] Shortcut[] shortcuts = new Shortcut[0];

        public string Force => force;
        public int MinPlayers => minPlayers;
        public string TwoFail => twoFail;
        public Shortcut[] Shortcuts => shortcuts;

        /// <summary>Editor builders: sets the contract from shortcuts given in world space (converted to this object's space).</summary>
        public void Configure(string forceText, params Shortcut[] locks) => Configure(forceText, 2, null, locks);

        /// <summary>Editor builders of a course for <paramref name="players"/>: the force, why fewer fail, and the locks.</summary>
        public void Configure(string forceText, int players, string twoFailText, params Shortcut[] locks)
        {
            force = forceText;
            minPlayers = Mathf.Max(1, players);
            twoFail = twoFailText;
            shortcuts = new Shortcut[locks.Length];
            for (int i = 0; i < locks.Length; i++)
                shortcuts[i] = new Shortcut
                {
                    name = locks[i].name, kind = locks[i].kind,
                    from = transform.InverseTransformPoint(locks[i].from), to = transform.InverseTransformPoint(locks[i].to),
                    via = (locks[i].via ?? new Vector3[0]).Select(transform.InverseTransformPoint).ToArray()
                };
        }

        public static Shortcut Lock(string name, ShortcutKind kind, Vector3 from, Vector3 to, params Vector3[] via) =>
            new Shortcut { name = name, kind = kind, from = from, to = to, via = via };

        public Vector3 From(Shortcut s) => transform.TransformPoint(s.from);
        public Vector3 To(Shortcut s) => transform.TransformPoint(s.to);

        /// <summary>The walk from a shortcut's start to its end through its <c>via</c> points (world metres).</summary>
        public float WalkLength(Shortcut s)
        {
            float length = 0f;
            var at = From(s);
            foreach (var v in s.via ?? new Vector3[0])
            {
                var next = transform.TransformPoint(v);
                length += Vector3.Distance(at, next);
                at = next;
            }
            return length + Vector3.Distance(at, To(s));
        }

        // ------------------------------------------------------------------ reach (pure, EditMode tested)

        /// <summary>The fastest a player leaves the ground on flat floor: a slide's boost on top of a sprint.</summary>
        public static float TakeOffSpeed(GameTuning t) => Mathf.Min(t.sprintSpeed + t.slideBoost, t.slideMaxSpeed);

        /// <summary>
        /// The farthest horizontal edge-to-edge gap a player crosses onto a ledge <paramref name="rise"/> metres above the take-off
        /// (negative: below), counting the slide-jump, the coyote time, the drag above sprint speed, the mantle at the far edge and
        /// the body hanging past both edges. 0 when the ledge is out of reach upward.
        /// </summary>
        public static float MaxGap(GameTuning t, float rise)
        {
            float up = Mathf.Sqrt(2f * t.gravity * t.jumpHeight);
            float lowest = rise - t.mantleMaxHeight;   // the feet may arrive this far under the ledge and still mantle onto it
            float disc = up * up - 2f * t.gravity * lowest;
            if (disc < 0f) return 0f;
            float air = (up + Mathf.Sqrt(disc)) / t.gravity + t.coyoteTime;
            float v = TakeOffSpeed(t);
            float slowing = Mathf.Min(air, Mathf.Max(0f, v - t.sprintSpeed) / Mathf.Max(0.01f, t.airOverspeedDrag));
            float distance = v * air - 0.5f * t.airOverspeedDrag * slowing * slowing;
            return distance + 2f * BodyOverhang;
        }

        /// <summary>The highest ledge above the floor a player reaches: a jump, then a mantle.</summary>
        public static float MaxClimb(GameTuning t) => t.jumpHeight + t.mantleMaxHeight;

        /// <summary>
        /// The highest a thrown bomb reaches at <paramref name="distance"/> metres horizontally from the hand (the envelope of
        /// every launch angle at the fastest throw, run speed added), relative to the hand.
        /// </summary>
        public static float MaxLobHeight(GameTuning t, float distance)
        {
            float v = t.throwSpeedMax + t.throwInheritForward * t.throwInheritMaxSpeed;
            float g = ThrowBallistics.Gravity(t);
            return v * v / (2f * g) - g * distance * distance / (2f * v * v);
        }

        /// <summary>Beyond a sprint through the hourglass, the room a body needs to step off one plate and onto the other.</summary>
        public const float SpreadMargin = 4f;

        /// <summary>
        /// The farthest apart (straight line) two sources may stand for one body to hold the first, then reach the second before
        /// the first's hourglass of <paramref name="memorySeconds"/> runs out (0: no hourglass, only the step between them).
        /// </summary>
        public static float MaxSpread(GameTuning t, float memorySeconds) => t.sprintSpeed * Mathf.Max(0f, memorySeconds) + SpreadMargin;

        /// <summary>Hand height above the feet when throwing (eye 1.6 m less the hand offset, PROJECT_SPEC §9).</summary>
        public const float HandHeight = 1.32f;
    }
}
