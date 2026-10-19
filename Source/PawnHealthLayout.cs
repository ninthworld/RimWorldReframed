using System;

namespace ReframedUI
{
    // Inferred compact Health geometry; native data and action eligibility remain authoritative.
    internal static class PawnHealthLayout
    {
        internal const float Header = 30, Gap = 14, Row = 21, GroupHeader = 20, BillRow = 33;
        internal static float SummaryWidth(float width) => Math.Max(1, (width - Gap) * .43f);
        internal static float ConditionsHeight(int groups, int entries) => groups * (GroupHeader + 5) + entries * Row;
        internal static float QueueHeight(int count) => Math.Max(25, count * BillRow);
        internal static bool FullyVisible(float y, float height, float scroll, float viewport) =>
            y >= scroll - .01f && y + height <= scroll + viewport + .01f;
        internal static bool Intersects(float y, float height, float scroll, float viewport) =>
            height > 0 && viewport > 0 && y + height > scroll && y < scroll + viewport;
        internal static float ClampScroll(float value, float content, float viewport) =>
            Math.Max(0, Math.Min(value, Math.Max(0, content - viewport)));
        internal static int SelfTendOutcome(bool enabling, bool doctorDisabled, int doctorPriority) =>
            !enabling ? 0 : doctorDisabled ? 1 : doctorPriority == 0 ? 2 : 0;
        internal static bool CareVisible(bool flesh, bool playerOrHost, bool guestInPlayerBed, bool entitled, bool settings, bool dead, bool playing) =>
            flesh && (playerOrHost || guestInPlayerBed) && entitled && settings && !dead && playing;
    }
}
