using System;
using ReframedUI;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    public static void Main()
    {
        foreach (float width in new[] { 536f, 420f, 300f })
        {
            foreach (bool cog in new[] { false, true })
            {
                float filter = PawnLogLayout.FilterWidth(width, 76f, cog);
                Check(filter > 35f, "three native filters retain input space at narrow sizes");
                Check(Math.Abs(filter * 3 + 12 + 6 + 76 + (cog ? 30 : 0) - width) < .001f,
                    "last toolbar action ends at panel edge without overlapping a filter");
            }
            float time = PawnLogLayout.TimestampWidth(300f, width - 10f);
            Check(PawnLogLayout.TitleWidth(width - 10f, time) > 100f, "long elapsed time leaves a readable title lane with rail");
            Check(PawnLogLayout.RowHeight(250f) >= 280f, "long generated narrative remains wrapped and fully reachable");
        }
        Check(PawnLogLayout.EntryLimit == 300, "native log cap is preserved rather than the Social feed cap");
        float viewport = 307 - PawnLogLayout.ToolbarHeight - PawnLogLayout.ToolbarGap;
        Check(viewport >= 270, "compact toolbar leaves room for several event cards");
        Check(PawnLogLayout.CenteredScroll(0, 48, viewport, 2000) == 0, "seek first entry clamps above top");
        Check(PawnLogLayout.CenteredScroll(1952, 48, viewport, 2000) == 2000 - viewport,
            "seek last entry exposes its bottom without overscrolling");
        Check(Math.Abs(PawnLogLayout.CenteredScroll(1000, 48, viewport, 2000) - (1024 - viewport / 2)) < .001f,
            "interior seek centers the matched actual entry");
        Check(PawnLogLayout.Visible(200, 100, 250, 100), "partially scrolled event keeps input and painting");
        Check(!PawnLogLayout.Visible(20, 48, 250, 100), "offscreen event cannot receive invisible clicks");
        float total = 300 * (PawnLogLayout.RowHeight(26) + PawnLogLayout.RowGap);
        Check(total > viewport && PawnLogLayout.CenteredScroll(total - 62, 57, viewport, total) > total - viewport - 63,
            "full native 300-event history remains reachable");
        Console.WriteLine("PASS " + checks + " Log layout, seek and overflow contracts; no in-game acceptance claimed.");
    }
}
