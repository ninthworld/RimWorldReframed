using System;
using ReframedUI;

int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
Check(InfoCardLayout.KnownTab(0) && InfoCardLayout.KnownTab(4), "All native Stats through Permits tabs supported");
Check(!InfoCardLayout.KnownTab(-1) && !InfoCardLayout.KnownTab(5), "Unfamiliar tab values fail open");
Check(InfoCardLayout.WindowWidth(1920) == 640 && InfoCardLayout.WindowHeight(1080) == 420, "Ordinary screen uses compact card dimensions");
Check(InfoCardLayout.WindowWidth(640) == 616 && InfoCardLayout.WindowHeight(400) == 376, "Small screens retain outer margins");
Check(InfoCardLayout.WindowWidth(0) > 0 && InfoCardLayout.WindowHeight(0) > 0, "Degenerate bounds stay drawable");
Check(InfoCardLayout.Supported(560, 300), "Minimum supported screen keeps header actions and body reachable");
Check(!InfoCardLayout.Supported(559, 300) && !InfoCardLayout.Supported(560, 299), "Undersized screens use native fallback rather than overlapping controls");
foreach (float width in new[] { 536f, 640f })
{
    Check(InfoCardLayout.SearchLeft(width) + 170 + 8 == InfoCardLayout.CloseLeft(width), "Search and close retain their common gutter");
    Check(InfoCardLayout.CloseLeft(width) + InfoCardLayout.ControlHeight == width - InfoCardLayout.Padding, "Close control shares header height and right padding");
}
Check(InfoCardLayout.TabsLeft(true, false) >= 8 + 50 + 8 && InfoCardLayout.TabsLeft(true, true) >= 38 + 50 + 8,
    "Tall pawn portrait owns its lane even when history adds a back control");
void Space(float cw, float ch, bool wantVertical, bool wantHorizontal, float wantWidth, float wantHeight)
{
    InfoCardLayout.ScrollSpace(100, 100, cw, ch, out float w, out float h, out bool v, out bool x);
    Check(v == wantVertical && x == wantHorizontal && w == wantWidth && h == wantHeight,
        $"Rail geometry for content {cw}x{ch}: got {w}x{h} v={v} h={x}");
}
Space(100, 100, false, false, 100, 100);
Space(90, 120, true, false, 91, 100);
Space(120, 90, false, true, 100, 91);
Space(120, 120, true, true, 91, 91);
Space(95, 120, true, true, 91, 91);
Space(120, 95, true, true, 91, 91);
Check(InfoCardLayout.PermitHeight(400, 200) == 400, "Short permit description retains ordinary body extent");
Check(InfoCardLayout.PermitHeight(400, 900) - 50 >= 908, "Native Accept stays at least eight pixels below long permit text");
Check(InfoCardLayout.PermitHeight(400, 300) < InfoCardLayout.PermitHeight(400, 900), "Next measured permit extent can shrink after a shorter selection");
for (int vw = 30; vw <= 120; vw += 10)
for (int vh = 30; vh <= 120; vh += 10)
for (int cw = 20; cw <= 140; cw += 20)
for (int ch = 20; ch <= 140; ch += 20)
{
    InfoCardLayout.ScrollSpace(vw, vh, cw, ch, out float w, out float h, out bool v, out bool x);
    Check(w > 0 && h > 0 && w <= vw && h <= vh, "Both rail reservations remain inside viewport");
    Check(v == InfoCardLayout.Overflow(ch, h) && x == InfoCardLayout.Overflow(cw, w), "Rail dependency settles to a consistent overflow state");
}
Console.WriteLine($"PASS {checks} InfoCard viewport, fallback and coupled-rail contracts.");
