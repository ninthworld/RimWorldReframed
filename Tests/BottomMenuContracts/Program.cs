using System;
using ReframedUI;

int checks = 0;
void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
foreach (int width in new[] { 640, 799, 800, 900, 1280, 1920, 2560 })
foreach (int height in new[] { 480, 599, 600, 720, 1080, 1440 })
foreach (var preferred in new[] { (780f, 248f), (660f, 214f), (620f, 146f), (680f, 214f), (900f, 520f), (1000f, 650f), (780f, 500f), (780f, 460f) })
{
    if (!BottomMenuPolicy.Supported(width, height))
    { Check(width < 800 || height < 600, "unsupported viewport must retain native view"); continue; }
    float w = BottomMenuPolicy.Width(preferred.Item1, width), h = BottomMenuPolicy.Height(preferred.Item2, height);
    float bottom = height - BottomMenuPolicy.Dock - BottomMenuPolicy.Gap;
    Check(w <= width - 16 && w <= preferred.Item1, "shell fits horizontal screen edges");
    Check(bottom - h >= 8 && bottom < height - 35, "shell clears top and native bottom bar");
    Check(w - BottomMenuPolicy.Padding * 2 >= 530 && h - BottomMenuPolicy.Header - BottomMenuPolicy.Padding >= 92,
        "all supported shells retain the actual renderer minimum content area");
}
foreach (bool open in new[] { false, true })
foreach (bool settled in new[] { false, true })
foreach (bool painted in new[] { false, true })
foreach (bool owns in new[] { false, true })
    Check(BottomMenuPolicy.Interactive(open, settled, painted, owns) == (open && settled && painted && owns),
        "closed, moving, never-painted, or obscured controls must be inert");
var motion = new ToolsPanelMotion();
motion.SetOpen(true, 0); motion.Tick(.11);
Check(!BottomMenuPolicy.Interactive(motion.Open, motion.Settled, true, true), "opening shell cannot execute native delegates");
float middle = motion.Shell;
motion.SetOpen(false, .11);
Check(motion.Shell == middle && !motion.Open, "close reverses presentation while native ownership ends immediately");
motion.Tick(.22); Check(motion.Shell == 0, "closing shell expires");
motion.SetOpen(true, .22); motion.Tick(.44);
Check(!BottomMenuPolicy.Interactive(motion.Open, motion.Settled, false, true), "timer completion cannot enable unseen controls");
Check(BottomMenuPolicy.Interactive(motion.Open, motion.Settled, true, true), "final repaint and input ownership allow delegates");
// No lateral resizing: the visible top moves while the bottom stays anchored.
foreach (float height in new[] { 100f, 320f, 650f })
foreach (float step in new[] { 0f, .1f, .5f, .9f, 1f })
{
    float t = ToolsPanelMotion.Ease(step);
    Check(Math.Abs(BottomMenuPolicy.RevealTop(40, height, t) + height * t - (40 + height)) < .001f,
        "bottom reveal keeps its lower edge fixed at every animation step");
    Check(BottomMenuPolicy.RevealTop(40, height, t, true) == 40, "downward dropdown keeps trigger-adjacent top fixed");
}
foreach (float screen in new[] { 600f, 720f, 1080f })
foreach (float top in new[] { 8f, 140f, 450f })
foreach (float menuHeight in new[] { 80f, 220f, 480f })
{
    float bottom = top + 28;
    bool above = BottomMenuPolicy.PopupAbove(top, bottom, menuHeight, screen);
    float y = BottomMenuPolicy.PopupTop(top, bottom, menuHeight, screen, above);
    Check(y >= 8 && y + menuHeight <= screen - 8, "dropdown remains within vertical screen edges");
    if (bottom + 3 + menuHeight <= screen - 8)
        Check(!above && y == bottom + 3, "dropdown opens directly below trigger when space permits");
    if (screen - bottom - 8 < menuHeight && top > screen - bottom && top - menuHeight - 3 >= 8)
        Check(above && y + menuHeight == top - 3, "bottom-edge trigger flips popup above without lateral expansion");
}
Check(BottomMenuPolicy.PopupLeft(740, 180, 800) == 612, "right-edge trigger keeps popup visible");
Check(BottomMenuPolicy.PopupLeft(120, 180, 800) == 120, "ordinary popup aligns to trigger left");
foreach (bool fromTop in new[] { true, false })
foreach (float p in new[] { 0f, .05f, .1f, .5f, .95f, 1f })
{
    BottomMenuPolicy.RevealBody(100, 200, 25, p, fromTop, out float top, out float height);
    Check(height >= 0 && top >= 100 && top + height <= 300.001f, "title/body union stays within original body");
    if (fromTop && 225 * p < 25) Check(height == 0, "body does not appear while title region remains unrevealed");
    if (fromTop && height > 0) Check(top == 100 && Math.Abs(height - (225 * p - 25)) < .001f, "title/body share one continuous reveal edge");
    if (!fromTop && height > 0) Check(Math.Abs(top + height - 300) < .001f, "titled popup above trigger remains bottom anchored");
}
Console.WriteLine($"PASS {checks} shared menu viewport, vertical motion, anchored dropdown and input contracts.");
