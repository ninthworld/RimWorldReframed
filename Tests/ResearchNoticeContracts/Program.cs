using System;
using ReframedUI;

int checks = 0;
void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
foreach (float width in new[] { 344f, 640, 900, 1920, 3840 })
foreach (float height in new[] { 182f, 480, 600, 1080, 2160 })
foreach (float top in new[] { -50f, 0, 8, 140, 500, 5000 })
{
    Check(ResearchNoticePolicy.Supported(width, height), "supported fixtures");
    float x = ResearchNoticePolicy.Left(width), y = ResearchNoticePolicy.Top(top);
    Check(x >= 8 && x + ResearchNoticePolicy.Width <= width - 8, "pill stays horizontally onscreen");
    bool fits = y + ResearchNoticePolicy.Height <= height - 45;
    Check(ResearchNoticePolicy.CanPlace(height, top) == fits, "insufficient room falls back instead of overlapping protected HUD");
    if (fits) Check(y >= 8 && y >= top && y + ResearchNoticePolicy.Height <= height - 45, "protected top and bottom bounds both honored");
}
Check(!ResearchNoticePolicy.CanPlace(1080, float.NaN) && !ResearchNoticePolicy.CanPlace(1080, float.PositiveInfinity), "invalid occupied geometry refuses custom positioning");
Check(!ResearchNoticePolicy.Supported(343, 600) && !ResearchNoticePolicy.Supported(900, 181), "unsupported small viewports retain native fallback");
Check(ResearchNoticePolicy.Age(0) == "Just now" && ResearchNoticePolicy.Age(59.99) == "Just now", "new completion timestamp");
Check(ResearchNoticePolicy.Age(60) == "1m ago" && ResearchNoticePolicy.Age(3599) == "59m ago", "minute boundaries");
Check(ResearchNoticePolicy.Age(3600) == "1h ago", "hour boundary");
var motion = new ToolsPanelMotion();
motion.SetOpen(true, 100);
Check(motion.Shell == 0 && !motion.Settled, "first open is clipped and noninteractive");
float last = 0;
for (int i = 1; i <= 22; i++)
{
    motion.Tick(100 + i * .01);
    float eased = ToolsPanelMotion.Ease(motion.Shell);
    Check(eased >= last && eased <= 1, "monotonic full-size reveal"); last = eased;
}
Check(motion.Settled && motion.Shell == 1, "settled after220ms real time");
motion.SetOpen(false, 100.22);
for (int i = 1; i <= 11; i++)
{
    motion.Tick(100.22 + i * .01);
    float eased = ToolsPanelMotion.Ease(motion.Shell);
    Check(eased <= last && eased >= 0, "noninteractive shell contracts monotonically"); last = eased;
}
float midway = motion.Shell;
motion.SetOpen(true, 100.33);
Check(Math.Abs(motion.Shell - midway) < .0001, "reversal preserves current geometry");
motion.Tick(100.44); Check(motion.Settled && motion.Shell == 1, "reverse covers remaining distance only");
motion.SetOpen(false, 100.44); motion.Tick(100.66);
Check(motion.Settled && motion.Shell == 0, "close cleanup shell expires independently of native callbacks");
motion.Reset(); Check(motion.Shell == 0 && !motion.Open, "context reset removes outgoing geometry");
Console.WriteLine($"PASS {checks} research notice geometry, timestamp and real-time reveal contracts.");
