using System;
using System.Collections.Generic;

namespace ReframedUI
{
    // Game-independent occupied-area placement, shared with focused collision tests.
    internal struct HudArea
    {
        internal float X, Y, Width, Height;
        internal HudArea(float x, float y, float width, float height)
        { X = x; Y = y; Width = width; Height = height; }
    }

    internal static class HudGeometry
    {
        internal static bool TryPlace(float x, float initialY, float width, float height,
            float bottomLimit, IList<HudArea> occupied, out float top)
        {
            top = initialY;
            if (float.IsNaN(x + initialY + width + height + bottomLimit) ||
                float.IsInfinity(x + initialY + width + height + bottomLimit) ||
                width <= 0 || height <= 0) return false;
            // Each collision moves below an obstacle; a second pass handles unsorted areas.
            for (int pass = 0; pass <= occupied.Count; pass++)
            {
                bool moved = false;
                foreach (var area in occupied)
                {
                    if (area.Width <= 0 || area.Height <= 0 || x >= area.X + area.Width ||
                        x + width <= area.X || top >= area.Y + area.Height + 8f ||
                        top + height + 8f <= area.Y) continue;
                    top = area.Y + area.Height + 8f;
                    moved = true;
                }
                if (top + height > bottomLimit) return false;
                if (!moved) return true;
            }
            return false;
        }
    }
}
