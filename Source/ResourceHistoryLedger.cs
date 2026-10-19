using System;
using System.Collections.Generic;
using System.Linq;

namespace ReframedUI
{
    // Plain persisted data and time rules are independent of the game/UI.
    public sealed partial class ResourceHistorySample
    {
        // Missing saved fields must not become a plausible tick-zero/zero-count sample.
        public int Tick = -1;
        public Dictionary<string, int> Counts;
    }

    internal sealed class ResourceHistoryLedger
    {
        internal const int Interval = 2500;
        internal const int SevenDays = 420000;
        internal const int Capacity = 176;
        private const int MaxEntries = 32768;
        private readonly List<ResourceHistorySample> samples = new List<ResourceHistorySample>();
        internal int Count => samples.Count;
        internal int LastTick => samples.Count == 0 ? -1 : samples[samples.Count - 1].Tick;

        internal bool Due(int tick) => tick >= 0 && (LastTick < 0 || tick < LastTick || (long)tick - LastTick >= Interval);

        internal bool Observe(int tick, IDictionary<string, int> counts)
        {
            if (tick < 0 || !ValidCounts(counts)) return false;
            if (tick < LastTick) samples.Clear(); // A new timeline cannot inherit future observations.
            if (!Due(tick)) return false;
            samples.Add(new ResourceHistorySample { Tick = tick, Counts = Copy(counts, null) });
            Prune();
            return true;
        }

        // This is the exact sanitizer called after Scribe has restored the plain records.
        // Invalid records are discarded, leaving gaps rather than interpolated/zero observations.
        internal bool Restore(IList<ResourceHistorySample> saved, int now, ISet<string> knownDefs)
        {
            samples.Clear();
            if (saved == null || saved.Count == 0) return true;
            if (now < 0 || saved.Count > Capacity * 4 || saved.Any(s => s != null && s.Tick > now)) return false;
            bool clean = true;
            var duplicateTicks = new HashSet<int>(saved.Where(s => s != null).GroupBy(s => s.Tick)
                .Where(g => g.Count() > 1).Select(g => g.Key));
            foreach (var record in saved.OrderBy(s => s?.Tick ?? -1))
            {
                if (record == null || record.Tick < 0 || duplicateTicks.Contains(record.Tick) || !ValidCounts(record.Counts))
                { clean = false; continue; }
                samples.Add(new ResourceHistorySample { Tick = record.Tick, Counts = Copy(record.Counts, knownDefs) });
            }
            Prune();
            return clean;
        }

        internal List<ResourceHistorySample> Export() => samples.Select(s => new ResourceHistorySample
            { Tick = s.Tick, Counts = Copy(s.Counts, null) }).ToList();

        internal bool TryChange(string defName, int now, int currentCount, out long change, out string reason)
        {
            change = 0;
            reason = "Seven days of continuous observations are not available.";
            if (now < 0 || currentCount < 0 || string.IsNullOrEmpty(defName))
            { reason = "The current native count or time is unavailable."; return false; }
            if (samples.Count == 0 || LastTick > now) return false;
            if ((long)now - LastTick > Interval)
            { reason = "Recent resource observations are missing."; return false; }
            long target = (long)now - SevenDays;
            int baseline = samples.FindLastIndex(s => s.Tick <= target);
            if (baseline < 0) return false;
            if (target - samples[baseline].Tick > Interval)
            { reason = "The seven-day comparison observation is missing."; return false; }
            for (int i = baseline; i < samples.Count; i++)
            {
                if (!samples[i].Counts.ContainsKey(defName))
                { reason = "This resource was not recorded throughout the comparison period."; return false; }
                if (i > baseline && (long)samples[i].Tick - samples[i - 1].Tick > Interval)
                { reason = "Resource history contains a sampling gap."; return false; }
            }
            change = (long)currentCount - samples[baseline].Counts[defName];
            reason = "Quantity change from the latest observation at or before seven days ago (at most one hour earlier).";
            return true;
        }

        internal bool RecentlyPositive(string name)
            => samples.Any(s => s.Counts.TryGetValue(name, out int value) && value > 0);

        internal static List<string> SanitizeCategories(IEnumerable<string> categories)
            => categories == null ? new List<string>() : categories
                .Where(value => !string.IsNullOrWhiteSpace(value) && value.Length <= 512)
                .Distinct(StringComparer.Ordinal).Take(1024).ToList();

        private void Prune()
        {
            if (samples.Count > Capacity) samples.RemoveRange(0, samples.Count - Capacity);
        }

        private static bool ValidCounts(IDictionary<string, int> values)
            => values != null && values.Count <= MaxEntries && values.All(pair =>
                !string.IsNullOrWhiteSpace(pair.Key) && pair.Key.Length <= 512 && pair.Value >= 0);

        private static Dictionary<string, int> Copy(IDictionary<string, int> values, ISet<string> known)
        {
            var copy = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var pair in values) if (known == null || known.Contains(pair.Key)) copy.Add(pair.Key, pair.Value);
            return copy;
        }
    }
}
