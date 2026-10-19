using System;

namespace ReframedUI
{
    // Real-time, reversible distance-based motion. No Unity dependency: timing and
    // launcher/input policy can be exercised without launching the game.
    internal sealed class ToolsPanelMotion
    {
        internal const double ShellSeconds = .22;
        internal const double SlideSeconds = .20;
        internal float Shell { get; private set; }
        internal float Slide { get; private set; }
        internal bool Open { get; private set; }
        internal bool Submenu { get; private set; }
        private double previous = double.NaN;
        internal bool Settled => Shell == (Open ? 1f : 0f) && Slide == (Submenu ? 1f : 0f);
        internal void SetOpen(bool value, double now)
        {
            Tick(now);
            Open = value;
        }
        internal void SetSubmenu(bool value, double now) { Tick(now); Submenu = value; }
        internal void ResetSubmenu() { Slide = 0f; Submenu = false; }
        internal void Reset()
        {
            Shell = Slide = 0f;
            Open = Submenu = false;
            previous = double.NaN;
        }
        internal void Tick(double now)
        {
            if (double.IsNaN(previous)) previous = now;
            double delta = Math.Max(0, now - previous);
            previous = now;
            Shell = Move(Shell, Open ? 1f : 0f, delta / ShellSeconds);
            Slide = Move(Slide, Submenu ? 1f : 0f, delta / SlideSeconds);
        }
        internal static float Ease(float value) => value * value * (3f - 2f * value);
        private static float Move(float value, float target, double step) => Math.Abs(target - value) <= step + .000001 ? target :
            target > value ? (float)Math.Min(target, value + step) : (float)Math.Max(target, value - step);
        internal static bool RowInteractive(bool settled, float rowTop, float rowBottom, float viewportTop, float viewportBottom) =>
            settled && rowTop >= viewportTop && rowBottom <= viewportBottom;
    }
}
