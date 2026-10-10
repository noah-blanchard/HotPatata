using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Host only, online: judges a REMOTE player against moving hazards (rotating bars, crushers, closing doors) at the time
    /// that player saw them (docs/netcode-deterministic-plan.md §2.4). The host's own copy of a remote player is drawn about
    /// a round trip late, so a trigger on it would kill a player who dodged on their screen (or spare one who did not).
    /// Instead, each tick-stamped state the owner sends (<see cref="NetworkPlayer.PlayerStamp"/>: the time it drew the level
    /// at and its feet) is tested against every moving lethal zone posed at that time. Every mover is a pure function of
    /// time (<see cref="ITimePosed"/>), so rewinding the level costs nothing and needs no history.
    ///
    /// The single decision point stays on the host (AGENTS.md); the client only says when it was where. Anti-abuse: the
    /// time is clamped to [now - <see cref="GameTuning.hazardRewindCap"/>, now], so a client can only make itself older.
    /// </summary>
    public static class HazardRewind
    {
        const float MidpointMaxStep = 2f;   // metres: farther apart, two stamps are a teleport, not a path

        /// <summary>A box in world space.</summary>
        public struct OrientedBox
        {
            public Vector3 Center;
            public Quaternion Rotation;
            public Vector3 HalfExtents;
        }

        static bool warnedNonBox;

        /// <summary>
        /// A new stamp arrived from <paramref name="player"/>'s owner. Fails the section if, at the stamp's time (and halfway
        /// from the previous stamp), the player's capsule was inside an armed moving lethal zone.
        /// </summary>
        public static void Judge(Player player, NetworkPlayer.PlayerStamp previous, NetworkPlayer.PlayerStamp current)
        {
            var run = RunManager.Instance;
            if (!NetMode.IsAuthority || run == null || run.State != RunState.Playing || player == null || player.ControlLocked) return;
            double start = SimulationClock.SectionStart;
            if (!current.Valid || current.Time < start) return;   // produced before the current section attempt

            var controller = player.GetComponent<CharacterController>();
            if (controller == null) return;

            double now = SimulationClock.ServerNow;
            float cap = player.Tuning.hazardRewindCap;
            if (Check(player, controller, current.Position, current.Time, now, cap)) return;

            bool path = previous.Valid && previous.Time >= start && previous.Time < current.Time &&
                        (current.Position - previous.Position).sqrMagnitude < MidpointMaxStep * MidpointMaxStep;
            if (path) Check(player, controller, (previous.Position + current.Position) * 0.5f, (previous.Time + current.Time) * 0.5, now, cap);
        }

        static bool Check(Player player, CharacterController controller, Vector3 feet, double claimed, double now, float cap)
        {
            double t = ClampClaim(claimed, now, cap);
            Capsule(controller, feet, out Vector3 bottom, out Vector3 top, out float radius);
            foreach (var zone in KillZone.Moving)
            {
                if (zone == null || !zone.isActiveAndEnabled || zone.Posed == null) continue;
                if (!zone.Posed.LethalAt(t, zone.Zone)) continue;
                if (!TryBoxAt(zone, t, out var box)) continue;
                if (!CapsuleHitsBox(bottom, top, radius, box)) continue;

                SyncStats.HazardRewindHits++;
                PatataLog.Sync($"hazard rewound HIT zone={zone.name} player={player} {(now - t) * 1000.0:F0}ms back");
                RunManager.Instance.FailSection("PlayerFell", $"player={player} zone={zone.name} (judged {(now - t) * 1000.0:F0} ms back)");
                return true;
            }
            return false;
        }

        /// <summary>The time a claim is judged at: never in the future, never older than the cap. Pure (EditMode tested).</summary>
        public static double ClampClaim(double claimed, double now, float cap) =>
            claimed > now ? now : claimed < now - cap ? now - cap : claimed;

        /// <summary>The player's capsule (segment ends and radius) with its feet at <paramref name="feet"/>, as its controller is now (posture mirrored).</summary>
        static void Capsule(CharacterController c, Vector3 feet, out Vector3 bottom, out Vector3 top, out float radius)
        {
            Vector3 scale = c.transform.lossyScale;
            radius = c.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float height = Mathf.Max(c.height * Mathf.Abs(scale.y), 2f * radius);
            Vector3 center = feet + c.transform.rotation * Vector3.Scale(c.center, scale);
            Vector3 half = Vector3.up * (height * 0.5f - radius);
            bottom = center - half;
            top = center + half;
        }

        /// <summary>
        /// The zone's box at server time <paramref name="t"/>: its pose now, carried by how its mover moves between now and t
        /// (M_box(t) = M_mover(t) · M_mover(now)⁻¹ · M_box(now)). A zone that is not a box is approximated by its bounds.
        /// </summary>
        public static bool TryBoxAt(KillZone zone, double t, out OrientedBox box)
        {
            box = default;
            var mover = zone.Posed.PosedTransform;
            if (mover == null || !zone.Posed.TryPoseAt(t, out Vector3 thenPosition, out Quaternion thenRotation)) return false;

            OrientedBox now;
            if (zone.Zone is BoxCollider b)
            {
                var tr = b.transform;
                Vector3 s = tr.lossyScale;
                now = new OrientedBox
                {
                    Center = tr.TransformPoint(b.center),
                    Rotation = tr.rotation,
                    HalfExtents = new Vector3(Mathf.Abs(b.size.x * s.x), Mathf.Abs(b.size.y * s.y), Mathf.Abs(b.size.z * s.z)) * 0.5f
                };
            }
            else
            {
                if (!warnedNonBox)
                {
                    warnedNonBox = true;
                    Debug.LogWarning($"HazardRewind: {zone.name} is not a BoxCollider; its bounds are used", zone);
                }
                var bounds = zone.Zone.bounds;
                now = new OrientedBox { Center = bounds.center, Rotation = Quaternion.identity, HalfExtents = bounds.extents };
            }

            box = Carry(now, mover.position, mover.rotation, thenPosition, thenRotation);
            return true;
        }

        /// <summary>Moves a box rigidly with its mover, from the mover's pose now to its pose then. Pure (EditMode tested).</summary>
        public static OrientedBox Carry(OrientedBox box, Vector3 moverNow, Quaternion moverNowRotation, Vector3 moverThen, Quaternion moverThenRotation)
        {
            Quaternion delta = moverThenRotation * Quaternion.Inverse(moverNowRotation);
            return new OrientedBox
            {
                Center = moverThen + delta * (box.Center - moverNow),
                Rotation = delta * box.Rotation,
                HalfExtents = box.HalfExtents
            };
        }

        /// <summary>Does the capsule (segment a-b, radius r) touch the box? Pure (EditMode tested).</summary>
        public static bool CapsuleHitsBox(Vector3 a, Vector3 b, float radius, OrientedBox box)
        {
            Quaternion toLocal = Quaternion.Inverse(box.Rotation);
            Vector3 la = toLocal * (a - box.Center), lb = toLocal * (b - box.Center);
            // The distance from a point of the segment to the box is convex along the segment: a ternary search finds its minimum.
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 40; i++)
            {
                float m1 = lo + (hi - lo) / 3f, m2 = hi - (hi - lo) / 3f;
                if (SqrDistanceToBox(Vector3.Lerp(la, lb, m1), box.HalfExtents) <= SqrDistanceToBox(Vector3.Lerp(la, lb, m2), box.HalfExtents)) hi = m2;
                else lo = m1;
            }
            return SqrDistanceToBox(Vector3.Lerp(la, lb, (lo + hi) * 0.5f), box.HalfExtents) <= radius * radius;
        }

        static float SqrDistanceToBox(Vector3 p, Vector3 half)
        {
            float dx = Mathf.Max(Mathf.Abs(p.x) - half.x, 0f);
            float dy = Mathf.Max(Mathf.Abs(p.y) - half.y, 0f);
            float dz = Mathf.Max(Mathf.Abs(p.z) - half.z, 0f);
            return dx * dx + dy * dy + dz * dz;
        }
    }
}
