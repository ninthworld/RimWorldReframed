using System;

namespace ReframedUI
{
    // Source crop ratios, adapted to readable logical text. This is an inferred
    // layout, not a declaration that source pixels are Unity logical units.
    internal static class PawnInspectorLayout
    {
        internal const float Width = 560f, Header = 43f, Tabs = 30f, Padding = 12f;
        internal const float SummaryHeight = 234f, DetailHeight = 420f;
        internal const float NeedsHeight = DetailHeight, SocialHeight = DetailHeight, GearHeight = DetailHeight;
        internal const float BioHeight = DetailHeight, HealthHeight = DetailHeight, LogHeight = DetailHeight;
        internal const float TabGap = 3f, TabSidePadding = 7f, TabIconLane = 18f;
        internal static float TabWidth(float labelWidth, bool icon) => Math.Max(56f,
            (float)Math.Ceiling(labelWidth) + TabSidePadding * 2 + (icon ? TabIconLane : 0) + 6f);
        internal static float NameWidth(float boldWidth, float available) => Math.Min(Math.Max(0, available), (float)Math.Ceiling(boldWidth) + 4f);
        internal static bool Supported(float width, float height) => width >= 900f && height >= 600f;
        internal static float BoundedHeight(float requested, float screenHeight) => Math.Max(150f, Math.Min(requested, screenHeight - 100f));
        internal static float Ratio(float value, float maximum) => maximum <= 0 || float.IsNaN(value) ? 0 : Math.Max(0, Math.Min(1, value / maximum));
        internal static float Armor(float baseArmor, float[] coverage, float[][] layers)
        {
            float total = 0;
            for (int i = 0; i < coverage.Length; i++)
            {
                float remaining = 1 - Ratio(baseArmor, 2f);
                foreach (float layer in layers[i]) remaining *= 1 - Ratio(layer, 2f);
                total += coverage[i] * (1 - remaining);
            }
            return Math.Max(0, Math.Min(2, total * 2));
        }
    }
}
