using System;
using System.Collections.Generic;

namespace ReframedUI
{
    // Shared by the actual storage adapter and tests. Native counts always win,
    // including zero. Supplemental observations never introduce unknown definitions.
    internal sealed class ResourceCountMerge<T> where T : class
    {
        internal readonly Dictionary<T, int> Counts = new Dictionary<T, int>();
        private readonly HashSet<T> native = new HashSet<T>();
        private readonly HashSet<T> extra = new HashSet<T>();
        private readonly HashSet<T> invalid = new HashSet<T>();
        private readonly HashSet<int> seen = new HashSet<int>();

        internal ResourceCountMerge(IEnumerable<KeyValuePair<T, int>> nativeCounts, IEnumerable<T> knownExtras)
        {
            foreach (var pair in nativeCounts)
                if (pair.Key != null) { Counts[pair.Key] = pair.Value; native.Add(pair.Key); }
            foreach (T def in knownExtras)
                if (def != null && !native.Contains(def)) { extra.Add(def); Counts[def] = 0; }
        }

        // Call only for a native-accepted weapon encountered directly in a native storage slot group.
        internal void AddStored(int thingId, T def, int stackCount)
        {
            if (def == null || !extra.Contains(def) || invalid.Contains(def) || !seen.Add(thingId)) return;
            long total = (long)Counts[def] + stackCount;
            if (stackCount < 0 || total > int.MaxValue)
            {
                Counts.Remove(def); // Unrepresentable quantities are unknown, never clamped or wrapped.
                invalid.Add(def);
                return;
            }
            Counts[def] = (int)total;
        }
    }
}
