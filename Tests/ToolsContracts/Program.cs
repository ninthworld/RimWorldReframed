using System;
using ReframedUI;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string label)
    { checks++; if (!value) throw new Exception(label); }
    private static bool Near(float a, float b) => Math.Abs(a - b) < .0001f;
    public static void Main()
    {
        ContextTests.Run();
        var motion = new ToolsPanelMotion();
        motion.SetOpen(true, 10);
        motion.Tick(10.11);
        Check(Near(motion.Shell, .5f), "shell uses elapsed real time");
        Check(!motion.Settled, "moving shell blocks row input");
        motion.SetOpen(false, 10.11);
        Check(Near(motion.Shell, .5f), "reversal does not jump");
        motion.Tick(10.165);
        Check(Near(motion.Shell, .25f), "retraction uses same bounded velocity");
        motion.SetOpen(true, 10.165);
        motion.Tick(10.33);
        Check(motion.Shell == 1f && motion.Settled, "rapid reverse reaches exact target");
        motion.SetSubmenu(true, 10.33);
        motion.Tick(10.43);
        Check(Near(motion.Slide, .5f) && !motion.Settled, "submenu moves independently inside open shell");
        Check(motion.Shell == 1f, "submenu never retracts shell");
        motion.SetSubmenu(false, 10.43);
        motion.Tick(10.53);
        Check(motion.Slide == 0f && motion.Shell == 1f && motion.Settled, "back reverses slide to main");
        motion.Tick(11.09);
        Check(motion.Open, "open tools stay open until an explicit dismissal");
        motion.SetOpen(false, 11.09);
        motion.Tick(11.31);
        Check(!motion.Open && motion.Shell == 0f, "explicit dismissal reaches the closed endpoint");
        motion.SetOpen(true, 12);
        motion.Tick(100);
        Check(motion.Shell == 1f, "long frame cannot overshoot shell");
        Check(!ToolsPanelMotion.RowInteractive(false, 0, 40, 0, 80), "moving rows are inert");
        Check(!ToolsPanelMotion.RowInteractive(true, -1, 39, 0, 80), "top clipped row is inert");
        Check(!ToolsPanelMotion.RowInteractive(true, 41, 81, 0, 80), "bottom clipped row is inert");
        Check(ToolsPanelMotion.RowInteractive(true, 0, 40, 0, 80), "fully visible settled row is interactive");
        Check(ToolsPanelMotion.Ease(0) == 0 && ToolsPanelMotion.Ease(1) == 1, "easing preserves anchors");
        Check(ToolsPanelMotion.Ease(.25f) < ToolsPanelMotion.Ease(.5f) &&
            ToolsPanelMotion.Ease(.5f) < ToolsPanelMotion.Ease(.75f), "easing never reverses growth");
        float before = motion.Shell;
        motion.Tick(100);
        Check(motion.Shell == before, "repeated GUI events in one frame do not advance motion");
        var fine = new ToolsPanelMotion();
        var coarse = new ToolsPanelMotion();
        fine.SetOpen(true, 0); coarse.SetOpen(true, 0);
        for (int i = 1; i <= 10; i++) fine.Tick(i * .011);
        coarse.Tick(.11);
        Check(Near(fine.Shell, coarse.Shell), "frame subdivision cannot change opening duration");
        fine.SetSubmenu(true, .11); fine.Tick(.21);
        fine.SetOpen(false, .21); fine.Tick(1);
        Check(fine.Shell == 0, "closing from submenu reaches launcher");
        fine.ResetSubmenu();
        Check(fine.Slide == 0 && !fine.Submenu, "fresh opening has no stale hidden page offset");
        Console.WriteLine("PASS " + checks + " tools motion/input contracts; no live UI acceptance claimed.");
    }
}
