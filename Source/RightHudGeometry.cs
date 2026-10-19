using System;

namespace ReframedUI
{
    internal static class RightHudGeometry
    {
        internal static bool Supported(float available) => available >= 128;
        internal static bool Supported(float available, bool critical, bool issues)
            => Supported(available) && (!(critical && issues) || available >= 180);
        internal static float CriticalLimit(float available, bool otherContent)
            => Math.Max(30, otherContent ? Math.Min(available * .35f, available - 136) : available - 38);
        internal static float IssueLimit(float available, bool letters)
            => Math.Max(0, letters ? Math.Min(available * .65f, available - 74) : available - 38);
    }
}
