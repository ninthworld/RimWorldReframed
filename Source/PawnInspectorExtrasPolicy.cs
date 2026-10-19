using System;

namespace ReframedUI
{
    internal static class PawnInspectorExtrasPolicy
    {
        internal static bool FullyVisible(float top, float height, float scroll, float viewport) =>
            height > 0 && top >= scroll - .01f && top + height <= scroll + viewport + .01f;
        internal static bool SeverityAvailable(float maxSeverity, float lethalSeverity) => maxSeverity < float.MaxValue || lethalSeverity > 0;
        internal static bool HealthDeveloper(bool dev, bool playing) => dev && playing;
        internal static bool ConditionDeveloper(bool showDevGizmos, bool playing) => showDevGizmos && playing;
    }
}
