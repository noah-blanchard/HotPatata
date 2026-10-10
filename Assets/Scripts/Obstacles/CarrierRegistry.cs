using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Gives every carrier (<see cref="IPlatformCarrier"/>) an id that is the same on every machine, so a rider can be
    /// referenced on the wire without making platforms NetworkObjects (docs/netcode-deterministic-plan.md §2.2).
    /// The id is derived when the carrier is enabled: a hash of its scene name and its sibling-index path from the scene
    /// root. Every machine loads the same scene, so they all derive the same ids; never names (AGENTS.md). A clash is
    /// reported, and <c>CarrierRegistryTests</c> checks that every course scene is clash-free.
    /// </summary>
    public static class CarrierRegistry
    {
        static readonly Dictionary<int, IPlatformCarrier> Carriers = new Dictionary<int, IPlatformCarrier>();
        static readonly StringBuilder Path = new StringBuilder();

        /// <summary>The id <paramref name="carrier"/> gets: never 0.</summary>
        public static int IdFor(Component carrier)
        {
            Path.Clear();
            Path.Append(carrier.gameObject.scene.name);
            AppendSiblingPath(carrier.transform);
            return Hash(Path.ToString());
        }

        static void AppendSiblingPath(Transform t)
        {
            if (t.parent != null) AppendSiblingPath(t.parent);
            Path.Append('/').Append(t.GetSiblingIndex());
        }

        /// <summary>FNV-1a, 32 bits: stable across runtimes and platforms (string.GetHashCode is not). 0 is reserved for "none".</summary>
        public static int Hash(string text)
        {
            uint h = 2166136261;
            foreach (char c in text)
            {
                h ^= c;
                h *= 16777619;
            }
            return h == 0 ? 1 : (int)h;
        }

        /// <summary>Called by a carrier when it is enabled; returns its id.</summary>
        public static int Register<T>(T carrier) where T : Component, IPlatformCarrier
        {
            int id = IdFor(carrier);
            if (Carriers.TryGetValue(id, out var other) && other is Object o && o != null && !ReferenceEquals(other, carrier))
                Debug.LogError($"CarrierRegistry: {carrier.name} and {o.name} share carrier id {id}; riders on them cannot be synced", carrier);
            Carriers[id] = carrier;
            return id;
        }

        /// <summary>Called by a carrier when it is disabled.</summary>
        public static void Unregister(int id, IPlatformCarrier carrier)
        {
            if (Carriers.TryGetValue(id, out var current) && ReferenceEquals(current, carrier)) Carriers.Remove(id);
        }

        /// <summary>The live carrier with this id on this machine, or null.</summary>
        public static IPlatformCarrier Get(int id) =>
            id != 0 && Carriers.TryGetValue(id, out var c) && c is Object o && o != null ? c : null;

        /// <summary>Every live carrier (diagnostics, bots).</summary>
        public static IEnumerable<IPlatformCarrier> All
        {
            get
            {
                foreach (var c in Carriers.Values)
                    if (c is Object o && o != null) yield return c;
            }
        }
    }
}
