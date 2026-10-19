using HarmonyLib;
using Verse;

namespace ReframedUI
{
    // Native keybindings consult this getter before the HUD is drawn. A plain
    // GUI.TextField is not one of Window.CommonSearchWidget's native instances,
    // so report its cached focus through the same native keyboard gate.
    [HarmonyPatch(typeof(WindowStack), "get_AnySearchWidgetFocused")]
    internal static class ToolsSearchFocusPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (ReframedMod.Active && ReframedMod.Settings.CompactTools && ToolsPanel.OwnSearchFocused)
                __result = true;
        }
    }
}
