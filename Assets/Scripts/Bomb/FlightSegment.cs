using Unity.Netcode;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A thrown bomb's whole flight as data (#117 P2, ARCHITECTURE §13.3): it left <see cref="Origin"/> with
    /// <see cref="Velocity"/> at server time <see cref="T0"/>, and from then on it is exactly
    /// x(t) = origin + v·(t − t0) + ½·g·(t − t0)². No machine integrates it: the host sweeps this arc to decide how the flight
    /// ends, and every machine draws it from its own clock, so the throw is sent once and never streamed.
    /// <see cref="Id"/> numbers the flights (0 = none).
    /// </summary>
    public struct FlightSegment : INetworkSerializable, System.IEquatable<FlightSegment>
    {
        public double T0;
        public Vector3 Origin;
        public Vector3 Velocity;
        public int Id;

        public bool Valid => Id != 0;

        public FlightSegment(double t0, Vector3 origin, Vector3 velocity, int id)
        {
            T0 = t0;
            Origin = origin;
            Velocity = velocity;
            Id = id;
        }

        /// <summary>Seconds of flight at server time <paramref name="time"/> (never negative).</summary>
        public float Elapsed(double time) => time > T0 ? (float)(time - T0) : 0f;

        public Vector3 PositionAt(double time, float gravity) => ThrowBallistics.PositionAt(Origin, Velocity, gravity, Elapsed(time));

        public Vector3 VelocityAt(double time, float gravity) => ThrowBallistics.VelocityAt(Velocity, gravity, Elapsed(time));

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref T0);
            serializer.SerializeValue(ref Origin);
            serializer.SerializeValue(ref Velocity);
            serializer.SerializeValue(ref Id);
        }

        public bool Equals(FlightSegment o) => T0 == o.T0 && Origin == o.Origin && Velocity == o.Velocity && Id == o.Id;
    }
}
