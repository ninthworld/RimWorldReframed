using System;

namespace ReframedUI
{
    internal static class ObjectInspectorDescriptionLayout
    {
        internal static bool Overflow(float actualHeight, float availableHeight) => actualHeight > availableHeight + .01f;
        internal static float Ratio(float fraction) => float.IsNaN(fraction) || float.IsInfinity(fraction) ? 0 : Math.Max(0, Math.Min(1, fraction));
        internal static void Clamp(ref float x, ref float y, float contentHeight, float availableHeight)
        {
            x = 0;
            y = Math.Max(0, Math.Min(y, Math.Max(0, contentHeight - availableHeight)));
        }
    }
}
