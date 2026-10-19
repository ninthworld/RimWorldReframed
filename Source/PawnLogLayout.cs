using System;

namespace ReframedUI
{
    // Inferred extension of the compact inspector feed. The native utility owns
    // ordering, categories, battle headings and the 300-entry cap.
    internal static class PawnLogLayout
    {
        internal const int EntryLimit = 300;
        internal const float ToolbarHeight = 24f, ToolbarGap = 9f, RowGap = 5f;
        internal const float HeadingHeight = 22f, BattleGap = 9f;
        internal static float RowHeight(float paragraphHeight) => Math.Max(48f, paragraphHeight + 31f);
        internal static float TimestampWidth(float measured, float width) => Math.Min(Math.Max(0f, measured + 3f), width * .4f);
        internal static float TitleWidth(float width, float timeWidth) => Math.Max(1f, width - 43f - timeWidth);
        internal static float FilterWidth(float width, float copyWidth, bool cog) => Math.Max(1f,
            (width - copyWidth - 6f - (cog ? 30f : 0f) - 12f) / 3f);
        internal static float CenteredScroll(float top, float rowHeight, float viewportHeight, float totalHeight) =>
            Math.Max(0f, Math.Min(Math.Max(0f, totalHeight - viewportHeight), top + (rowHeight - viewportHeight) / 2f));
        internal static bool Visible(float top, float height, float scroll, float viewport) =>
            top + height >= scroll && top <= scroll + viewport;
    }
}
