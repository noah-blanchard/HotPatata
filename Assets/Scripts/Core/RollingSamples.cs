using System;

namespace HotPatata
{
    /// <summary>
    /// The last N values of a measurement (a ring), with percentiles over them. Plain C#, EditMode tested; used by
    /// <see cref="NetQuality"/> for round trips, jitter, arrival gaps and frame times.
    /// </summary>
    public sealed class RollingSamples
    {
        readonly double[] values;
        readonly double[] sorted;
        int head, count;
        bool dirty;

        public RollingSamples(int capacity = 128)
        {
            values = new double[Math.Max(1, capacity)];
            sorted = new double[values.Length];
        }

        public int Count => count;

        public void Clear()
        {
            head = count = 0;
            dirty = true;
        }

        public void Add(double value)
        {
            values[head] = value;
            head = (head + 1) % values.Length;
            if (count < values.Length) count++;
            dirty = true;
        }

        /// <summary>The value below which <paramref name="p"/> (0..1) of the samples fall (nearest rank); 0 when empty.</summary>
        public double Percentile(double p)
        {
            if (count == 0) return 0.0;
            if (dirty)
            {
                Array.Copy(values, sorted, count);
                Array.Sort(sorted, 0, count);
                dirty = false;
            }
            int rank = (int)Math.Ceiling(Math.Clamp(p, 0.0, 1.0) * count) - 1;
            return sorted[Math.Clamp(rank, 0, count - 1)];
        }

        public double Min => Percentile(0.0);
        public double Max => Percentile(1.0);
    }
}
