using System;

namespace ReframedUI
{
    internal static class PrimitiveGeometry
    {
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool Drawable(float x, float y, float width, float height) =>
            Finite(x) && Finite(y) && Finite(width) && Finite(height) && width > 0 && height > 0;
        internal static float Radius(float requested, float width, float height) =>
            !Finite(requested) || !Finite(width) || !Finite(height) || width <= 0 || height <= 0
                ? 0 : Math.Max(0, Math.Min(requested, Math.Min(width, height) / 2));
        internal static float Fraction(float value) => !Finite(value) ? 0 : Math.Max(0, Math.Min(1, value));
    }
}
