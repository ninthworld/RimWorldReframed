using System;
using ReframedUI;

int assertions = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); assertions++; }
Check(!ArchitectLayout.Supported(899, 824), "Narrow screens keep native Architect");
Check(!ArchitectLayout.Supported(1374, 599), "Short screens keep native Architect");
Check(ArchitectLayout.Supported(1374, 824), "Canonical UI1.5 logical canvas supported");
Check(ArchitectLayout.Height - 2f * ArchitectLayout.Padding - ArchitectLayout.HeaderHeight >= 14f * ArchitectLayout.CategoryRow,
    "Full-height sidebar fits all fourteen native categories at canonical height");
foreach (float height in new[] { 600f, 720f, 824f, 1080f, 2160f })
{
    float bottom = ArchitectLayout.Bottom(height), panel = ArchitectLayout.PanelHeight(height);
    Check(bottom < height - 35f && bottom - panel >= 0f, "Panel remains above launcher and on screen");
    foreach (float progress in new[] { 0f, .25f, .5f, .75f, 1f })
        Check(ArchitectLayout.RevealTop(bottom, panel, progress) + panel * progress == bottom,
            "Opening and closing keep the panel bottom edge fixed at every sampled size");
}
Check(ArchitectLayout.ContentHeight(260, 900, 480) == 420, "Native grouping removes unused space");
Check(ArchitectLayout.ContentHeight(260, 900, -180) == 1080, "Extra native rows grow scroll content");
Check(ArchitectLayout.ContentHeight(260, 900, 850) == 260, "Short content fills viewport");
Check(ArchitectLayout.ReanchorScroll(640, 900, 420, 260) == 160, "Grouping retains native first-row view");
Check(ArchitectLayout.ReanchorScroll(160, 420, 1080, 260) == 820, "New rows preserve bottom anchor");
Check(ArchitectLayout.ClampScroll(-4, 420, 260) == 0, "Top scroll clamped");
Check(ArchitectLayout.ClampScroll(999, 420, 260) == 160, "Bottom scroll clamped");
Check(ArchitectLayout.ClampScroll(99, 20, 260) == 0, "Empty content has no scroll");
float oneRow = ArchitectLayout.MeasuredBlockHeight(900, 813);
Check(oneRow == 95, "One native row compacts to its measured height plus top padding");
Check(ArchitectLayout.MeasuredBlockHeight(oneRow, 8) == oneRow, "Compact block is stable across repaints");
Check(ArchitectLayout.MeasuredBlockHeight(900, -180) == 1088, "Overflow retains every native row");
Console.WriteLine($"PASS {assertions} Architect geometry/overflow contracts; no live acceptance claimed.");
