using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.WindowStackOnGUI))]
    internal static class EscMenuPreparePatch
    {
        private static void Prefix(WindowStack __instance)
        {
            EscMenuPanel.Tick();
            foreach (Window window in __instance.Windows.ToArray())
                if (window is MainTabWindow_Menu menu) EscMenuPanel.Prepare(menu);
            EscMenuPanel.HandleOutside();
        }
        private static void Postfix() => EscMenuPanel.DrawClosing();
    }
    [HarmonyPatch(typeof(MainTabWindow_Menu), "get_RequestedTabSize")]
    internal static class EscMenuSizePatch
    {
        private static bool Prefix(MainTabWindow_Menu __instance, ref Vector2 __result)
        { if (!EscMenuPanel.Eligible(__instance)) return true; __result = EscMenuPanel.Bounds.size; return false; }
    }
    [HarmonyPatch(typeof(Window), "get_Margin")]
    internal static class EscMenuMarginPatch
    {
        private static void Postfix(Window __instance, ref float __result)
        { if (EscMenuPanel.Eligible(__instance)) __result = 0; }
    }
    [HarmonyPatch(typeof(MainTabWindow_Menu), nameof(MainTabWindow_Menu.DoWindowContents))]
    internal static class EscMenuContentsPatch
    {
        private static bool Prefix(MainTabWindow_Menu __instance, Rect __0)
        { if (!EscMenuPanel.Eligible(__instance)) return true; EscMenuPanel.Draw(__instance, __0); return false; }
    }
    [HarmonyPatch(typeof(Window), nameof(Window.PostClose))]
    internal static class EscMenuClosePatch
    {
        private static void Prefix(Window __instance) => EscMenuPanel.Closing(__instance);
        private static void Postfix(Window __instance) => EscMenuPanel.Release(__instance);
    }
    [HarmonyPatch(typeof(Window), nameof(Window.OnCancelKeyPressed))]
    internal static class EscMenuCancelPatch
    {
        private static bool Prefix(Window __instance) => !EscMenuPanel.Cancel(__instance);
    }
}
