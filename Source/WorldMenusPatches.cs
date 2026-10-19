using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.WindowStackOnGUI))]
    internal static class WorldMenusPreparePatch
    { private static void Prefix() => WorldMenusInspector.Prepare(); private static void Postfix() => WorldMenusInspector.DrawClosing(); }
    [HarmonyPatch(typeof(WorldInspectPane), nameof(WorldInspectPane.DoWindowContents))]
    internal static class WorldMenusContentsPatch
    {
        private static bool Prefix(WorldInspectPane __instance, Rect __0)
        { if (WorldMenusInspector.Closing(__instance)) return false; if (!WorldMenusInspector.Owns(__instance)) return true; WorldMenusInspector.Draw(__instance, __0); return false; }
    }
    [HarmonyPatch(typeof(InspectPaneUtility), nameof(InspectPaneUtility.ExtraOnGUI))]
    internal static class WorldMenusExtraPatch
    {
        private static bool Prefix(IInspectPane __0)
        { var pane = __0 as WorldInspectPane; if (WorldMenusInspector.Closing(pane)) return false; if (!WorldMenusInspector.Owns(pane)) return true; WorldMenusInspector.Extras(pane); return false; }
    }
    [HarmonyPatch(typeof(InspectPaneUtility), nameof(InspectPaneUtility.PaneWidthFor))]
    internal static class WorldMenusWidthPatch
    {
        private static bool Prefix(IInspectPane __0, ref float __result)
        { if (!WorldMenusInspector.Owns(__0 as WorldInspectPane)) return true; __result = WorldMenusInspector.Bounds.xMax; return false; }
    }
    [HarmonyPatch(typeof(Window), nameof(Window.PostClose))]
    internal static class WorldMenusClosePatch
    { private static void Prefix(Window __instance) => WorldMenusInspector.Close(__instance); }
    [HarmonyPatch(typeof(InspectTabBase), "get_TabRect")]
    internal static class WorldMenusTabRectPatch
    {
        private static bool Prefix(InspectTabBase __instance, ref Rect __result)
        { if (!WorldMenusInspector.Drawing || WorldMenusInspector.DrawingTab != __instance) return true; __result = WorldMenusInspector.NativeBody; return false; }
    }
    // Reuse existing guarded Command painters; the original native grid and all
    // command hotkeys, tutorial/disabled checks and action callbacks still run.
    internal static class WorldMenusCommands
    {
        private static bool world, command;
        internal static bool Drawing => world && command;
        internal readonly struct State { internal readonly bool World, Command; internal State(bool world, bool command) { World = world; Command = command; } }
        internal static State EnterWorld() { var previous = new State(world, command); world = WorldMenus.Enabled?.Invoke() == true && WorldRendererUtility.WorldSelected && Current.ProgramState == ProgramState.Playing && !TutorSystem.TutorialMode; command = false; return previous; }
        internal static State EnterCommand(Command instance) { var previous = new State(world, command); command = world && SelectionCommands.Supports(instance); return previous; }
        internal static void Restore(State previous) { world = previous.World; command = previous.Command; }
    }
    [HarmonyPatch(typeof(WorldGizmoUtility), nameof(WorldGizmoUtility.WorldUIOnGUI))]
    internal static class WorldMenusCommandsPatch
    { private static void Prefix(out WorldMenusCommands.State __state) => __state = WorldMenusCommands.EnterWorld(); private static void Finalizer(WorldMenusCommands.State __state) => WorldMenusCommands.Restore(__state); }
    [HarmonyPatch(typeof(Command), "GizmoOnGUIInt")]
    internal static class WorldMenusCommandPaintPatch
    { private static void Prefix(Command __instance, out WorldMenusCommands.State __state) => __state = WorldMenusCommands.EnterCommand(__instance); private static void Finalizer(WorldMenusCommands.State __state) => WorldMenusCommands.Restore(__state); }
}
