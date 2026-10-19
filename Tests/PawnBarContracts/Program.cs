using System;
using System.Collections.Generic;
using ReframedUI;

static class Program
{
    static int checks;
    static void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
    static void Main()
    {
        Check(PawnBarGeometry.TryBuild(3, 1000, 400, 0, out var sparse) && sparse.Width == 244 && sparse.Height == 112,
            "Three-pawn layout must reserve only three cards and two group gaps.");
        Check(PawnBarGeometry.TryBuild(9, 1000, 400, 0, out var reference) && reference.Width == 652 && reference.Height == 112,
            "Reference density must retain 60x78 cards, 8px intra-group gaps and 24px group gaps without a plus tile.");
        Check(PawnBarGeometry.TryBuild(1, 76, 112, 0, out var single) && single.Width == 76 && single.Height == 112 && single.RowCount == 1,
            "An exactly filled one-card row must not create an empty plus row.");
        foreach (int count in new[] {1,2,3,8,9,10,19,20,21,22,23,39,40,41,79,80,81,200,511,512})
        foreach (float width in new[] {76f,159f,160f,244f,327f,328f,651f,652f,653f,1100f,1461f})
        foreach (float weapon in new[] {0f,28f})
        {
            Check(PawnBarGeometry.TryBuild(count,width,1e6f,weapon,out var layout), "A bounded full roster must fit a scrollable layout.");
            Check(layout.Cards.Count == count, "Full layout must retain the entire roster.");
            Check(layout.Width <= width+.01f && layout.Height <= 1e6f, "Container exceeds available area.");
            Check(layout.RowPitch == 18+78+weapon+10, "Row pitch must reserve heading, portrait, weapon and inter-row gap.");
            Check(layout.Height == layout.RowCount*layout.RowPitch-10+16, "Height must contain only occupied rows and shell padding.");
            var indices = new HashSet<int>();
            var rows = new HashSet<float>();
            int expectedIndex = 0;
            foreach (var card in layout.Cards)
            {
                Check(indices.Add(card.Index) && card.Index == expectedIndex++, "Native pawn indices must remain unique and in order.");
                int groups = Math.Min(3,count), firstCount = count/groups;
                int expectedGroup = card.Index < firstCount+(count%groups>0?1:0) ? 0 :
                    card.Index < firstCount*2+Math.Min(count%groups,2) ? 1 : 2;
                Check(card.Group == expectedGroup, "Wrapping must preserve contiguous balanced group membership.");
                Check(card.X >= 0 && card.Y >= 18 && card.X+60 <= layout.Width-16+.01f &&
                    card.Y+78+weapon <= layout.Height-16+.01f, "Card or weapon extends outside container.");
                rows.Add(card.Y);
            }
            Check(rows.Count == layout.RowCount, "Every reported row must contain a real pawn.");
            foreach (var section in layout.Sections)
                Check(section.X >= 0 && section.Width > 0 && section.X+section.Width <= layout.Width-16+.01f &&
                    rows.Contains(section.Y+18), "Section heading must align with an occupied card row.");
            for (int a=0;a<layout.Cards.Count;a++) for(int b=a+1;b<layout.Cards.Count;b++)
            {
                var left=layout.Cards[a]; var right=layout.Cards[b];
                Check(left.X+60 <= right.X || right.X+60 <= left.X ||
                    left.Y+78+weapon <= right.Y || right.Y+78+weapon <= left.Y,
                    "Card and weapon hit regions must not overlap.");
            }
            foreach (int cap in new[] {2,4})
            {
                int visible = Math.Min(layout.RowCount,cap);
                Check(PawnBarGeometry.ViewportContentHeight(layout,cap,1e6f) == visible*layout.RowPitch-10,
                    "Collapsed and expanded viewport caps must expose complete rows.");
                float oneAndHalf = layout.RowPitch*1.5f-10;
                Check(PawnBarGeometry.ViewportContentHeight(layout,cap,oneAndHalf) == layout.RowPitch-10,
                    "A constrained viewport must round down to whole rows.");
                Check(PawnBarGeometry.ViewportContentHeight(layout,cap,layout.RowPitch-11) == 0,
                    "Insufficient space for a complete row must be reported to the caller.");
            }
            Check(!PawnBarGeometry.TryBuild(count,width,layout.Height-1,weapon,out _), "Height bounds must not silently discard final rows.");
        }
        Check(!PawnBarGeometry.TryBuild(0,1000,400,0,out _), "Empty bar should fall back.");
        Check(!PawnBarGeometry.TryBuild(3,75,400,0,out _), "Too-narrow layouts should fall back.");
        Check(!PawnBarGeometry.TryBuild(3,float.NaN,400,0,out _), "Invalid dimensions should fall back.");
        Check(!PawnBarGeometry.TryBuild(3,1000,400,-1,out _), "Negative weapon reservation should fall back.");
        Check(PawnBarGeometry.ViewportContentHeight(reference,0,1000) == 0, "A zero-row viewport must be empty.");
        Check(PawnBarGeometry.ViewportContentHeight(reference,2,float.NaN) == 0, "Invalid viewport bounds must be rejected.");
        var thirteenGroups = new int[13];
        for (int i=0;i<thirteenGroups.Length;i++) thirteenGroups[i] = i==0 ? 44 : 39;
        var smallGroups = new int[27];
        for (int i=0;i<smallGroups.Length;i++) smallGroups[i] = 1+i%3;
        foreach (var counts in new[] {thirteenGroups,smallGroups})
        foreach (float width in new[] {76f,244f,652f,1461f})
        foreach (float weapon in new[] {0f,28f})
        {
            Check(PawnBarGeometry.TryBuild(counts,width,1e6f,weapon,out var native), "Explicit native groups must all fit the full scroll layout.");
            int expectedTotal = 0;
            foreach (int groupSize in counts) expectedTotal += groupSize;
            Check(native.Cards.Count == expectedTotal, "Explicit groups must retain every native pawn.");
            Check(native.Width <= width+.01f, "Explicit native groups must stay within available width.");
            Check(native.Height == native.RowCount*native.RowPitch-10+16, "Explicit groups must not reserve an unused plus row.");
            var seen = new int[counts.Length];
            int expectedIndex = 0, expectedGroup = 0;
            foreach (var card in native.Cards)
            {
                while (seen[expectedGroup] == counts[expectedGroup]) expectedGroup++;
                Check(card.Index == expectedIndex++ && card.Group == expectedGroup, "Explicit group and native index identity must survive wrapping.");
                seen[card.Group]++;
                Check(card.X >= 0 && card.Y >= 18 && card.X+60 <= native.Width-16+.01f &&
                    card.Y+78+weapon <= native.Height-16+.01f, "Explicit group card/weapon regions exceed bounds.");
            }
            for (int group=0;group<counts.Length;group++)
                Check(seen[group] == counts[group] && native.Sections.Exists(section => section.Group == group),
                    "Every supplied group needs all of its cards and a heading section.");
            for (int a=0;a<native.Sections.Count;a++) for(int b=a+1;b<native.Sections.Count;b++)
            {
                var left=native.Sections[a]; var right=native.Sections[b];
                Check(left.Y != right.Y || left.X+left.Width+24 <= right.X || right.X+right.Width+24 <= left.X,
                    "Explicit group headings must retain non-overlapping separator regions.");
            }
            foreach (var card in native.Cards)
                Check(native.Sections.Exists(section => section.Group == card.Group && section.Y+18 == card.Y &&
                    section.X <= card.X && section.X+section.Width >= card.X+60),
                    "Every wrapped explicit-group card must remain below its own heading section.");
            for (int a=0;a<native.Cards.Count;a++) for(int b=a+1;b<native.Cards.Count;b++)
            {
                var left=native.Cards[a]; var right=native.Cards[b];
                Check(left.X+60 <= right.X || right.X+60 <= left.X ||
                    left.Y+78+weapon <= right.Y || right.Y+78+weapon <= left.Y,
                    "Explicit native groups must not overlap card or weapon targets.");
            }
        }
        Check(PawnBarGeometry.TryBuild(new[] {1,1,1,1},1000,400,0,out var four) && four.Width == 328 && four.RowCount == 1,
            "Four native groups must retain four headings with only three group gaps.");
        Check(!PawnBarGeometry.TryBuild((IReadOnlyList<int>)null,1000,400,0,out _), "Null native group list must be rejected.");
        Check(!PawnBarGeometry.TryBuild(Array.Empty<int>(),1000,400,0,out _), "Empty native group list must be rejected.");
        Check(!PawnBarGeometry.TryBuild(new[] {1,0,2},1000,400,0,out _), "Empty native group must be rejected.");
        Check(!PawnBarGeometry.TryBuild(new[] {1,-1,2},1000,400,0,out _), "Negative native group count must be rejected.");
        Check(!PawnBarGeometry.TryBuild(new[] {10000,1},1000,1e6f,0,out _), "Oversized total native roster must be rejected.");
        Check(!PawnBarGeometry.TryBuild(new[] {int.MaxValue,int.MaxValue},1000,1e6f,0,out _), "Overflowing native group counts must be rejected safely.");
        foreach (float width in new[] {1088f,1100f})
        {
            Check(PawnBarGeometry.TryBuild(65,width,1e6f,0,out var packed), "A large roster must retain its complete layout.");
            int firstTwoRows = packed.Cards.FindAll(card => card.Y < packed.RowPitch*2).Count;
            Check(firstTwoRows == (width == 1100f ? 31 : 30), "The first two rows must use remaining capacity across group boundaries.");
            Check(packed.Cards.Count == 65, "Dense packing must retain every pawn.");
            for (int i=0;i<packed.Cards.Count;i++)
            {
                var card = packed.Cards[i];
                int expectedGroup = i < 22 ? 0 : i < 44 ? 1 : 2;
                Check(card.Index == i && card.Group == expectedGroup, "Dense packing must preserve native order and contiguous group membership.");
            }
            Check(packed.Sections.Exists(section => section.Group == 1 && section.Y == packed.RowPitch && section.DividerBefore),
                "The next group's partial chunk needs a heading and divider in the remaining second-row space.");
            for (int a=0;a<packed.Sections.Count;a++) for(int b=a+1;b<packed.Sections.Count;b++)
            {
                var left=packed.Sections[a]; var right=packed.Sections[b];
                Check(left.Y != right.Y || left.X+left.Width+24 <= right.X || right.X+right.Width+24 <= left.X,
                    "Adjacent group headings must retain the 24px separator gap.");
            }
        }
        Console.WriteLine($"{checks} pawn-bar geometry checks passed.");
    }
}
