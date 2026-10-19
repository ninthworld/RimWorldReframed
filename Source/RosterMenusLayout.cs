using System;

namespace ReframedUI
{
    internal static class RosterMenusLayout
    {
        internal const float Identity = 154, Row = 32, Gap = 2, Rail = 10;
        internal static bool Visible(float top, float height, float scroll, float viewport)
            => top + height >= scroll && top <= scroll + viewport;
        internal static bool FullRow(float top, float height, float scroll, float viewport)
            => top >= scroll - .01f && top + height <= scroll + viewport + .01f;
        internal static float ClampScroll(float value, float content, float viewport)
            => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Math.Max(0, Math.Min(value, Math.Max(0, content - viewport)));
        internal static float Extent(int count, float row) => count <= 0 ? 0 : count * (row + Gap) - Gap;
        // Match PawnInspectorWidgets.BeginScroll's vertical-only reservation.
        // Both the fixed hour headings and the timeline use this same width.
        internal static float ContentWidth(float width, float height, float contentHeight)
            => Math.Max(1, width - (contentHeight > height + .01f ? Rail : 0));
    }
}
