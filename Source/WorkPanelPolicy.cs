using System;

namespace ReframedUI
{
    // Deliberate, explicitly requested UI actions; native scheduling remains unchanged.
    internal static class WorkPanelPolicy
    {
        internal static int AutoPriority(int current, float? skill, int passion, bool disabled, bool incapable)
        {
            if (current < 1 || current > 4 || !skill.HasValue || disabled || incapable ||
                float.IsNaN(skill.Value) || float.IsInfinity(skill.Value)) return current;
            float score = skill.Value + 2 * Math.Max(0, Math.Min(2, passion));
            return score >= 14 ? 1 : score >= 9 ? 2 : score >= 4 ? 3 : 4;
        }

        internal static int AssignPriority(string name, int current, float? skill, int passion, bool disabled, bool incapable)
        {
            if (current < 0 || current > 4 || disabled || incapable) return current;
            switch (name)
            {
                case "Firefighter": case "Patient": case "PatientBedRest": return 1;
                case "BasicWorker": return 2;
                case "Hauling": case "Cleaning": return 4;
            }
            // Unknown unskilled definitions keep their existing assignment.
            if (!skill.HasValue || float.IsNaN(skill.Value) || float.IsInfinity(skill.Value)) return current;
            passion = Math.Max(0, Math.Min(2, passion));
            float score = skill.Value + 2 * passion;
            if (score < 4 && passion == 0) return 0;
            return AutoPriority(3, skill, passion, false, false);
        }

        internal static int ClearPriority(int current, bool disabled)
            => !disabled && current >= 1 && current <= 4 ? 0 : current;

        // Exact stock key, including mods whose naturalPriority exceeds 100000.
        internal static int NativeOrderKey(int naturalPriority, int priority)
            => unchecked(naturalPriority + (4 - priority) * 100000);

        internal static int TogglePriority(int current, int remembered)
            => current < 0 || current > 4 ? current : current > 0 ? 0 :
                remembered >= 1 && remembered <= 4 ? remembered : 3;

        internal static bool CanSetEnabledPriority(int current, int requested, bool manual)
            => current >= 1 && current <= 4 && requested >= 0 && requested <= 4 &&
                (requested == 0 || manual);

        internal static int NextPriority(int displayed)
            => displayed >= 1 && displayed <= 4 ? (displayed == 1 ? 4 : displayed - 1) : displayed;

        internal static bool CanCycleEnabledPriority(int stored, bool editable, bool manual)
            => manual && editable && stored >= 1 && stored <= 4;
    }

    // Raw native values are kept even while checkbox mode masks enabled priorities to3.
    // The same transaction code is used by assignment, Clear and Undo.
    internal sealed class WorkPanelPriorityDelta
    {
        internal readonly int[] Before, After;
        internal WorkPanelPriorityDelta(int[] before)
        { Before = (int[])before.Clone(); After = (int[])before.Clone(); }
        internal bool Matches(Func<int, int?> read, int[] expected)
        {
            for (int i = 0; i < expected.Length; i++) if (read(i) != expected[i]) return false;
            return true;
        }
        internal int CountDifferent(Func<int, int?> read, int[] expected)
        {
            int count = 0;
            for (int i = 0; i < expected.Length; i++) if (read(i) != expected[i]) count++;
            return count;
        }
        internal void Apply(bool restore, Func<int, int?> read, Func<bool> validContext, Action<int, int> write)
        {
            int[] expected = (int[])(restore ? After : Before).Clone();
            int[] target = restore ? Before : After;
            for (int i = 0; i < target.Length; i++)
            {
                if (Before[i] == After[i]) continue;
                // Stop further writes if a native/mod callback changes other work or the context.
                if (!validContext() || !Matches(read, expected)) break;
                write(i, target[i]);
                expected[i] = target[i];
            }
        }
    }

    // Session-only UI memory; no pawn or game serialization and no inferred assignments.
    internal sealed class WorkPanelPriorityMemory<TSettings, TWork>
    {
        private readonly System.Collections.Generic.Dictionary<TSettings, System.Collections.Generic.Dictionary<TWork, int>> values
            = new System.Collections.Generic.Dictionary<TSettings, System.Collections.Generic.Dictionary<TWork, int>>();
        internal void Remember(TSettings settings, TWork work, int priority)
        {
            if (priority < 1 || priority > 4) return;
            if (!values.TryGetValue(settings, out var jobs))
                values.Add(settings, jobs = new System.Collections.Generic.Dictionary<TWork, int>());
            jobs[work] = priority;
        }
        internal int Recall(TSettings settings, TWork work)
            => values.TryGetValue(settings, out var jobs) && jobs.TryGetValue(work, out int priority) ? priority : 3;
        internal void Clear() => values.Clear();
    }

    internal static class WorkPanelAuditPolicy
    {
        internal const int MaxRecordBytes = 1024 * 1024;
        internal const int MaxFileBytes = 8 * 1024 * 1024;
        internal static bool CanAppend(long current, long next)
            => current >= 0 && next > 0 && next <= MaxRecordBytes && current <= MaxFileBytes - next;
    }
}
