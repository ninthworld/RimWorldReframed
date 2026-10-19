using System;
using ReframedUI;

int checks = 0;
void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
foreach (bool player in new[] { false, true })
foreach (bool hidden in new[] { false, true })
foreach (bool all in new[] { false, true })
    Check(WorldMenusPolicy.VisibleFaction(player, hidden, all) == (all || !(player || hidden)), "native faction visibility, including DEV override");
foreach (bool letter in new[] { false, true })
foreach (bool dialog in new[] { false, true })
foreach (bool message in new[] { false, true })
foreach (bool showLetters in new[] { false, true })
foreach (bool showMessages in new[] { false, true })
{
    bool visible = WorldMenusPolicy.ArchiveVisible(letter, dialog, message, showLetters, showMessages);
    Check(visible == ((!(letter || dialog) || showLetters) && (!message || showMessages)), "native archive type filters retain archived dialogs in Letters");
}
foreach (float day in new[] { 0, 1, 29, 30, 100, 301, 10000 })
foreach (int span in new[] { 0, 30, 100, 300 })
{
    float minimum = WorldMenusPolicy.GraphMinimum(day, span);
    Check(minimum >= 0 && minimum <= day, "graph bounds preserve real history range");
    Check(minimum == (span == 0 ? 0 : Math.Max(0, day - span)), "native graph period semantics");
}
Check(WorldMenusPolicy.RowInput(30, 65, 30, 35, true), "fully visible row active");
Check(!WorldMenusPolicy.RowInput(29, 64, 30, 35, true), "partially clipped top row inert");
Check(!WorldMenusPolicy.RowInput(31, 66, 30, 35, true), "partially clipped bottom row inert");
Check(!WorldMenusPolicy.RowInput(30, 65, 30, 35, false), "disabled parent cannot activate row");
var motion = new ToolsPanelMotion();
motion.SetOpen(true, 0);
float previous = 1000;
for (int i = 0; i <= 22; i++)
{
    motion.Tick(i * .01);
    float progress = ToolsPanelMotion.Ease(motion.Shell);
    float top = WorldMenusPolicy.RevealTop(1000, 360, progress);
    Check(top <= previous && top >= 640, "opening keeps fixed bottom with monotonically rising top"); previous = top;
    Check(progress == 1 || !motion.Settled, "moving controls remain disabled");
}
Check(motion.Settled && motion.Shell == 1, "220ms reveal finishes");
motion.SetOpen(false, .22);
motion.Tick(.33);
float halfway = motion.Shell;
Check(halfway > 0 && halfway < 1, "native close leaves bounded inert shell interval");
motion.SetOpen(true, .33);
Check(motion.Shell == halfway, "reversal has no size discontinuity");
motion.Tick(.44); Check(motion.Settled && motion.Shell == 1, "reopen traverses remaining distance only");
motion.SetOpen(false, .44); motion.Tick(.66);
Check(motion.Shell == 0 && motion.Settled, "close ghost expires after220ms");
motion.Reset(); Check(motion.Shell == 0 && !motion.Open, "game teardown clears motion");
Console.WriteLine($"PASS {checks} native-filter, graph-range, clipping and bidirectional world motion contracts.");
HistoryGraphTests.Run();
