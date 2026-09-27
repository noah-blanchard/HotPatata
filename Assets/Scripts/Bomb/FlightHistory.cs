using UnityEngine;

namespace Beep
{
    /// <summary>
    /// A short record of where a flying bomb was, and where every player's catch centre was, on the host.
    /// It answers "was the bomb within reach of slot N at some point since time T?", which is how the host
    /// validates a catch that a lagging client saw happen a moment ago (see <see cref="CatchResolver"/>).
    /// Plain C# with no scene dependency, so it is unit tested directly.
    /// </summary>
    public sealed class FlightHistory
    {
        readonly int capacity;
        readonly float[] times;
        readonly Vector3[] bomb;
        readonly Vector3[,] centers;
        readonly bool[,] hasCenter;
        int head, count;

        public FlightHistory(int capacity = 64)
        {
            this.capacity = capacity;
            times = new float[capacity];
            bomb = new Vector3[capacity];
            centers = new Vector3[capacity, Player.MaxSlots];
            hasCenter = new bool[capacity, Player.MaxSlots];
        }

        public int Count => count;

        public void Clear() => head = count = 0;

        /// <summary>Starts a new sample; add the players' catch centres to it with <see cref="AddCenter"/>.</summary>
        public void Begin(float time, Vector3 bombPosition)
        {
            head = (head + 1) % capacity;
            if (count < capacity) count++;
            times[head] = time;
            bomb[head] = bombPosition;
            for (int s = 0; s < Player.MaxSlots; s++) hasCenter[head, s] = false;
        }

        /// <summary>Adds a player's catch centre to the sample started by the last <see cref="Begin"/>.</summary>
        public void AddCenter(int slot, Vector3 center)
        {
            if (count == 0 || slot < 0 || slot >= Player.MaxSlots) return;
            centers[head, slot] = center;
            hasCenter[head, slot] = true;
        }

        /// <summary>
        /// The latest time, not earlier than <paramref name="since"/>, at which the bomb was within
        /// <paramref name="reach"/> metres of slot <paramref name="slot"/>'s catch centre; -1 if never.
        /// </summary>
        public float LastReach(int slot, float since, float reach)
        {
            if (slot < 0 || slot >= Player.MaxSlots) return -1f;
            float reachSqr = reach * reach;
            for (int n = 0; n < count; n++)   // newest first
            {
                int i = (head - n + capacity) % capacity;
                if (times[i] < since) break;
                if (hasCenter[i, slot] && (bomb[i] - centers[i, slot]).sqrMagnitude <= reachSqr) return times[i];
            }
            return -1f;
        }
    }
}
