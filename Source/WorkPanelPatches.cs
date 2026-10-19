using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal static class WorkPanelWindow
    {
        private static MainTabWindow_Work owner;
        private static bool background, shadow, closeX, closeButton;
        private static readonly MethodInfo Reposition = AccessTools.Method(typeof(Window), "SetInitialSizeAndPosition");
        internal static void Prepare(MainTabWindow_Work window)
        {
            if (!WorkPanel.Eligible(window))
            {
                if (owner == window) { Release(window); Reposition.Invoke(window, null); }
                return;
            }
            if (owner != window)
            {
                if (owner != null) Release(owner);
                owner = window; background = window.doWindowBackground; shadow = window.drawShadow;
                closeX = window.doCloseX; closeButton = window.doCloseButton;
            }
            window.doWindowBackground = window.drawShadow = window.doCloseX = window.doCloseButton = false;
            window.windowRect = WorkPanel.Bounds(window);
            WorkPanelTransition.Update(window, window.windowRect, WorkPanel.LastEditorHeight, WorkPanel.LastCardWidth, WorkPanel.HasSelection);
        }
        internal static void Release(Window window)
        {
            if (owner != window) return;
            window.doWindowBackground = background; window.drawShadow = shadow;
            window.doCloseX = closeX; window.doCloseButton = closeButton;
            owner = null; WorkPanel.Reset();
            WorkPanelTransition.Release(window);
        }
    }
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.WindowStackOnGUI))]
    internal static class WorkPanelPreparePatch
    {
        private static void Prefix(WindowStack __instance)
        {
            WorkPanelTransition.Tick();
            foreach (Window window in __instance.Windows) if (window is MainTabWindow_Work work) WorkPanelWindow.Prepare(work);
        }
        private static void Postfix() => WorkPanelTransition.DrawClosing();
    }
    [HarmonyPatch(typeof(MainTabWindow_PawnTable), nameof(MainTabWindow_PawnTable.PostOpen))]
    internal static class WorkPanelOpenPatch
    {
        private static void Postfix(MainTabWindow_PawnTable __instance)
        { if (__instance is MainTabWindow_Work work) { WorkPanel.Reset(); WorkPanelWindow.Prepare(work); } }
    }
    [HarmonyPatch(typeof(MainTabWindow_PawnTable), "get_RequestedTabSize")]
    internal static class WorkPanelSizePatch
    {
        private static bool Prefix(MainTabWindow_PawnTable __instance, ref Vector2 __result)
        { if (!WorkPanel.Eligible(__instance)) return true; __result = WorkPanel.Bounds((MainTabWindow_Work)__instance).size; return false; }
    }
    [HarmonyPatch(typeof(MainTabWindow_PawnTable), "get_Margin")]
    internal static class WorkPanelMarginPatch
    {
        private static bool Prefix(MainTabWindow_PawnTable __instance, ref float __result)
        { if (!WorkPanel.Eligible(__instance)) return true; __result = 0; return false; }
    }
    [HarmonyPatch(typeof(MainTabWindow_Work), nameof(MainTabWindow_Work.DoWindowContents))]
    internal static class WorkPanelContentsPatch
    {
        private static bool Prefix(MainTabWindow_Work __instance, Rect __0)
        { if (!WorkPanel.Eligible(__instance)) return true; WorkPanelTransition.DrawContents(__instance, __0); return false; }
    }
    [HarmonyPatch(typeof(Window), nameof(Window.PostClose))]
    internal static class WorkPanelClosePatch
    {
        private static void Prefix(Window __instance) => WorkPanelTransition.Close(__instance);
        private static void Postfix(Window __instance) => WorkPanelWindow.Release(__instance);
    }
}
