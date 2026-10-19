using System;

namespace ReframedUI
{
    internal sealed class RosterMenusMotion
    {
        internal const double Seconds = .22;
        private bool initialized;
        private float from, target;
        private double previous, progress = 1;
        internal bool Settled => progress == 1;
        internal float Value => from + (target - from) * (float)(progress * progress * (3 - 2 * progress));
        internal void Set(float next, double now)
        {
            if (float.IsNaN(next) || float.IsInfinity(next) || double.IsNaN(now) || double.IsInfinity(now)) return;
            if (!initialized) { initialized = true; from = target = next; previous = now; return; }
            double elapsed = Math.Max(0, now - previous); previous = Math.Max(previous, now);
            progress = Math.Min(1, progress + elapsed / Seconds);
            if (1 - progress < .000000001) progress = 1;
            if (Math.Abs(next - target) <= .01f) return;
            from = Value; target = next; progress = 0;
        }
    }
}
