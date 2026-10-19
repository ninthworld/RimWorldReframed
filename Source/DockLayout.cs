using System.Collections.Generic;

namespace ReframedUI
{
    internal static class DockLayout
    {
        internal static int VisibleCount(IReadOnlyList<float> widths, float available,
            float gap, float overflowWidth, out float width, out bool overflow)
        {
            return VisibleCount(widths, null, available, gap, gap, overflowWidth, out width, out overflow);
        }

        internal static int VisibleCount(IReadOnlyList<float> widths, IReadOnlyList<int> groups,
            float available, float gap, float groupGap, float overflowWidth, out float width, out bool overflow)
        {
            width = 0f;
            overflow = false;
            if (widths == null || widths.Count == 0) return 0;
            if (available <= 0f)
            {
                overflow = true;
                return 0;
            }

            if (groups != null && groups.Count != widths.Count) return 0;
            float all = 0f;
            for (int i = 0; i < widths.Count; i++)
            {
                if (widths[i] <= 0f || float.IsNaN(widths[i]) || float.IsInfinity(widths[i])) return 0;
                all += widths[i];
                if (i > 0) all += GroupGap(groups, i, gap, groupGap);
            }
            if (all <= available)
            {
                width = all;
                return widths.Count;
            }

            overflow = true;
            if (overflowWidth <= 0f || overflowWidth > available) return 0;

            int visible = 0;
            float tabsWidth = 0f;
            while (visible < widths.Count)
            {
                float candidateTabs = tabsWidth + (visible == 0 ? 0f : GroupGap(groups, visible, gap, groupGap)) + widths[visible];
                float candidateTotal = candidateTabs + gap + overflowWidth;
                if (candidateTotal > available) break;
                tabsWidth = candidateTabs;
                visible++;
            }
            width = visible == 0 ? overflowWidth : tabsWidth + gap + overflowWidth;
            return visible;
        }

        private static float GroupGap(IReadOnlyList<int> groups, int index, float gap, float groupGap)
        {
            return groups != null && groups[index] != groups[index - 1] ? groupGap : gap;
        }
    }
}
