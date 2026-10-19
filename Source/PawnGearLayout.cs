using System;

namespace ReframedUI
{
    internal static class PawnGearLayout
    {
        internal const float LeftFraction = .46f, Gap = 18f, RowHeight = 24f;
        internal const float HeadingHeight = 18f, EmptyHeight = 17f, SectionGap = 7f, StatsHeight = 172f;
        internal static float SectionHeight(int count) => HeadingHeight + (count == 0 ? EmptyHeight : count * RowHeight) + SectionGap;
        internal static float StackMass(float unitMass, int count) => unitMass * count;
        internal static bool DropVisible(bool canControl, bool inventory, bool canControlColonist, bool spawned, bool homeMap) =>
            canControl && (inventory || canControlColonist || spawned && !homeMap);
        internal static int DropBlock(bool locked, bool quest, bool destroysGear) => locked ? 1 : quest ? 2 : destroysGear ? 3 : 0;
        internal static void TemperatureScale(float minimum, float maximum, out float start, out float end)
        { start = Math.Min(-50, minimum); end = Math.Max(100, maximum); }
        internal static float Position(float temperature, float minimum, float maximum) =>
            maximum <= minimum ? 0 : Math.Max(0, Math.Min(1, (temperature - minimum) / (maximum - minimum)));
    }
}
