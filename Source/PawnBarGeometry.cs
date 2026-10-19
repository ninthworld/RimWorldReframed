using System;
using System.Collections.Generic;

namespace ReframedUI
{
    // Reference-pixel geometry. All drawing and native selection adapters consume
    // the same full roster layout. The caller owns viewport clipping and scrolling.
    internal static class PawnBarGeometry
    {
        internal const float CardWidth = 60, CardHeight = 78, HeaderHeight = 18;
        internal const float Padding = 8, Gap = 8, GroupGap = 24, RowGap = 10;
        internal sealed class Slot
        {
            internal int Index, Group;
            internal float X, Y;
        }
        internal sealed class Section
        {
            internal int Group;
            internal float X, Y, Width;
            internal bool DividerBefore;
        }
        internal sealed class Layout
        {
            internal float Width, Height, RowPitch;
            internal int RowCount;
            internal readonly List<Slot> Cards = new List<Slot>();
            internal readonly List<Section> Sections = new List<Section>();
        }
        internal static bool TryBuild(int count, float maxWidth, float maxHeight, float weaponHeight, out Layout result)
        {
            result = null;
            if (count <= 0 || count > 10000) return false;
            int groups = Math.Min(3, count);
            var counts = new int[groups];
            for (int group = 0; group < groups; group++)
                counts[group] = count / groups + (group < count % groups ? 1 : 0);
            return TryBuild(counts, maxWidth, maxHeight, weaponHeight, out result);
        }

        // Group numbers correspond directly to caller-supplied native groups;
        // wrapping creates additional sections without changing that membership.
        internal static bool TryBuild(IReadOnlyList<int> groupCounts, float maxWidth, float maxHeight, float weaponHeight, out Layout result)
        {
            result = null;
            if (groupCounts == null || groupCounts.Count == 0 || groupCounts.Count > 10000 ||
                float.IsNaN(maxWidth + maxHeight + weaponHeight) ||
                float.IsInfinity(maxWidth + maxHeight + weaponHeight) || weaponHeight < 0) return false;
            int total = 0;
            for (int group = 0; group < groupCounts.Count; group++)
            {
                if (groupCounts[group] <= 0 || groupCounts[group] > 10000 - total) return false;
                total += groupCounts[group];
            }
            float inner = maxWidth - Padding * 2;
            int capacity = (int)Math.Floor((inner + Gap) / (CardWidth + Gap));
            if (capacity < 1) return false;
            var layout = new Layout();
            int index = 0;
            float x = 0, rowY = 0, used = 0;
            float pitch = HeaderHeight + CardHeight + weaponHeight + RowGap;
            for (int group = 0; group < groupCounts.Count; group++)
            {
                int remaining = groupCounts[group];
                while (remaining > 0)
                {
                    // Consume the available part of this row before continuing a
                    // group below. x already includes the preceding group gap.
                    int available = (int)Math.Floor((inner - x + Gap + .01f) / (CardWidth + Gap));
                    if (available < 1) { x = 0; rowY += pitch; available = capacity; }
                    int chunk = Math.Min(remaining, available);
                    float width = chunk * (CardWidth + Gap) - Gap;
                    if (rowY + HeaderHeight + CardHeight + weaponHeight + Padding * 2 > maxHeight) return false;
                    layout.Sections.Add(new Section { Group = group, X = x, Y = rowY, Width = width, DividerBefore = x > 0 });
                    for (int col = 0; col < chunk; col++)
                        layout.Cards.Add(new Slot { Index = index++, Group = group, X = x + col * (CardWidth + Gap), Y = rowY + HeaderHeight });
                    used = Math.Max(used, x + width);
                    x += width + GroupGap;
                    remaining -= chunk;
                    if (remaining > 0) { x = 0; rowY += pitch; }
                }
            }
            layout.Width = used + Padding * 2;
            layout.RowPitch = pitch;
            layout.RowCount = (int)Math.Round(rowY / pitch) + 1;
            layout.Height = rowY + HeaderHeight + CardHeight + weaponHeight + Padding * 2;
            if (layout.Height > maxHeight) return false;
            result = layout;
            return true;
        }

        // Both maxHeight and the result describe the content viewport, excluding
        // the shell's top/bottom padding. Only complete rows are exposed.
        internal static float ViewportContentHeight(Layout fullLayout, int maxRows, float maxHeight)
        {
            if (fullLayout == null || maxRows <= 0 || float.IsNaN(maxHeight) || maxHeight <= 0) return 0;
            float availableRows = (float)Math.Floor((maxHeight + RowGap) / fullLayout.RowPitch);
            int rows = (int)Math.Min(Math.Min(fullLayout.RowCount, maxRows), availableRows);
            return rows > 0 ? rows * fullLayout.RowPitch - RowGap : 0;
        }
    }
}
