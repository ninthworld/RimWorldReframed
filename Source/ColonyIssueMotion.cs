using System;

namespace ReframedUI
{
    // Reversible real-time reveal; independent of ticks, pause, frame rate and Unity.
    internal sealed class ColonyIssueMotion
    {
        internal const double Seconds = .22;
        private float value, target;
        private double previous = double.NaN;
        internal ColonyIssueMotion(float initial = 0) { value = target = ColonyIssuePolicy.Clamp01(initial); }
        internal float Value => value * value * (3 - 2 * value);
        internal bool Open => target > 0;
        internal bool Settled => value == target;
        internal void Set(bool open, double now) { Tick(now); target = open ? 1 : 0; }
        internal void Tick(double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now)) return;
            if (double.IsNaN(previous)) previous = now;
            double elapsed = Math.Max(0, now - previous);
            previous = Math.Max(previous, now);
            float step = (float)(elapsed / Seconds);
            value = Math.Abs(target - value) <= step + .000001f ? target :
                target > value ? Math.Min(target, value + step) : Math.Max(target, value - step);
        }
        internal void Reset(float initial = 0)
        { value = target = ColonyIssuePolicy.Clamp01(initial); previous = double.NaN; }
    }

    // Normal disclosure keeps its existing layout flow. An actual order change
    // adds a decaying offset from the prior rendered position to the new slot.
    internal sealed class ColonyIssuePositionMotion
    {
        internal const double Seconds = .18;
        private bool initialized;
        private float layout, startOffset;
        private double progress = 1;
        private double previous = double.NaN;
        private float Offset => startOffset * (float)(1 - progress * progress * (3 - 2 * progress));
        internal float Value => layout + Offset;
        internal bool Settled => progress == 1;
        internal void Follow(float nextLayout, double now, bool reordered)
        {
            if (float.IsNaN(nextLayout) || float.IsInfinity(nextLayout) || double.IsNaN(now) || double.IsInfinity(now)) return;
            double elapsed = double.IsNaN(previous) ? 0 : Math.Max(0, now - previous);
            previous = double.IsNaN(previous) ? now : Math.Max(previous, now);
            double step = elapsed / Seconds;
            progress = 1 - progress <= step + .000000001 ? 1 : progress + step;
            if (initialized && reordered)
            {
                startOffset = Value - nextLayout;
                progress = Math.Abs(startOffset) <= .01f ? 1 : 0;
            }
            layout = nextLayout; initialized = true;
        }
    }
}
