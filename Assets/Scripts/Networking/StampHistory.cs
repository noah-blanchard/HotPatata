using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The last second or so of a remote player's stamps (<see cref="NetworkPlayer.PlayerStamp"/>), oldest to newest, as this
    /// machine received them. It answers "where were this player's feet at server time t, as their owner saw it?", which is
    /// how the host checks a time-stamped claim (a catch, #117 P3) against the claimer's own reports. Plain C#, EditMode tested.
    /// </summary>
    public sealed class StampHistory
    {
        readonly NetworkPlayer.PlayerStamp[] ring;
        int head, count;

        public StampHistory(int capacity = 48)
        {
            ring = new NetworkPlayer.PlayerStamp[Mathf.Max(2, capacity)];
        }

        public int Count => count;

        public NetworkPlayer.PlayerStamp Newest => count > 0 ? At(count - 1) : default;

        /// <summary>The i-th stamp, 0 = oldest.</summary>
        public NetworkPlayer.PlayerStamp At(int i) => ring[(head - count + i + ring.Length * 2) % ring.Length];

        public void Clear() => head = count = 0;

        /// <summary>Adds a stamp newer than every stamp so far (older ones are ignored: they are already superseded).</summary>
        public void Add(NetworkPlayer.PlayerStamp stamp)
        {
            if (count > 0 && stamp.Time < Newest.Time) return;
            ring[head] = stamp;
            head = (head + 1) % ring.Length;
            if (count < ring.Length) count++;
        }

        /// <summary>
        /// The feet at server time <paramref name="time"/>: between the two stamps around it (never across a teleport), or the
        /// newest one carried by its velocity for at most <paramref name="maxExtrapolation"/> seconds. False when the time is
        /// older than the history, or too far past it.
        /// </summary>
        public bool TryFeetAt(double time, float maxExtrapolation, out Vector3 feet)
        {
            feet = default;
            if (count == 0) return false;
            var newest = Newest;
            if (time >= newest.Time)
            {
                double ahead = time - newest.Time;
                if (ahead > maxExtrapolation) return false;
                feet = newest.Position + newest.Velocity * (float)ahead;
                return true;
            }
            for (int i = count - 1; i > 0; i--)
            {
                var b = At(i);
                var a = At(i - 1);
                if (time < a.Time) continue;
                if (a.Epoch != b.Epoch)
                {
                    feet = b.Position;   // a teleport between them: the owner was already at the new place
                    return true;
                }
                double span = b.Time - a.Time;
                float k = span > 1e-6 ? (float)((time - a.Time) / span) : 1f;
                feet = Vector3.Lerp(a.Position, b.Position, k);
                return true;
            }
            return false;
        }
    }
}
