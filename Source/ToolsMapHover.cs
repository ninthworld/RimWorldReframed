using HarmonyLib;
using Verse;

namespace ReframedUI
{
    // These native mouse readouts otherwise paint over our non-window HUD.
    // Keep their settings and map rendering intact; only occlude UI hover.
    [HarmonyPatch(typeof(BeautyDrawer), nameof(BeautyDrawer.BeautyDrawerOnGUI))]
    internal static class ToolsBeautyHoverPatch
    {
        private static bool Prefix() => !ToolsPanel.OccludesMapPointer;
    }

    [HarmonyPatch(typeof(EnvironmentStatsDrawer), nameof(EnvironmentStatsDrawer.EnvironmentStatsOnGUI))]
    internal static class ToolsRoomHoverPatch
    {
        private static bool Prefix() => !ToolsPanel.OccludesMapPointer;
    }
}
