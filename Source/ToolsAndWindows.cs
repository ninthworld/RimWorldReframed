using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    [HarmonyPatch(typeof(GlobalControlsUtility), nameof(GlobalControlsUtility.DoPlaySettings))]
    internal static class ToolsMenu
    {
        private static KeyBindingDef shortcut;
        private static KeyBindingDef Shortcut => shortcut ?? (shortcut = DefDatabase<KeyBindingDef>.GetNamedSilentFail("Reframed_ToggleTools"));
        internal static bool Expanded => ToolsPanel.IsOpen;
        internal static string ShortcutLabel => Shortcut == null || Shortcut.MainKey == KeyCode.None ? "" : Shortcut.MainKeyLabel;
        private static bool HotkeyContext(bool worldView) => GUIUtility.keyboardControl == 0 &&
            Find.MainTabsRoot.OpenTab == null && (worldView ? !Find.WorldTargeter.IsTargeting && !Find.WorldRoutePlanner.Active :
            Find.DesignatorManager.SelectedDesignator == null && Find.Selector.SelectedObjects.Count == 0) && !Find.WindowStack.MouseObscuredNow &&
            !Find.WindowStack.AnyWindowAbsorbingAllInput;
        private static bool Prefix(WidgetRow rowVisibility, bool worldView, ref float curBaseY)
        {
            bool eligible = ReframedMod.Active && ReframedMod.Settings.CompactTools && !TutorSystem.TutorialMode &&
                Current.ProgramState == ProgramState.Playing && (worldView || Find.CurrentMap != null);
            ToolsPanel.Prepare(eligible);
            if (!eligible) return true;
            ToolsPanel.Draw(rowVisibility, worldView, HotkeyContext(worldView), Shortcut, ShortcutLabel, ref curBaseY);
            return false;
        }

        internal static System.Collections.Generic.Dictionary<string, object> AnimationAuditState() => ToolsPanel.AnimationAuditState();
        internal static void DiagnosticSetOpen(bool value) => ToolsPanel.DiagnosticSetOpen(value);
        internal static void DiagnosticNavigate(string page) => ToolsPanel.DiagnosticNavigate(page);
        internal static void DiagnosticSetQuery(string query) => ToolsPanel.DiagnosticSetQuery(query);
    }

    [HarmonyPatch(typeof(Widgets), nameof(Widgets.DrawWindowBackground), new[] { typeof(Rect) })]
    internal static class WindowTheme
    {
        private static bool Prefix(Rect rect)
        {
            if (InfoCardPanel.DrawBackground(rect)) return false;
            if (!BottomMenuChildren.Drawing && (!ReframedMod.Active || !ReframedMod.Settings.WindowTheme)) return true;
            using (new Theme.GuiScope(true)) { GUI.color = Color.white; Theme.Panel(rect); }
            return false;
        }
    }

    [HarmonyPatch(typeof(Widgets), nameof(Widgets.DrawWindowBackgroundTutor))]
    internal static class TutorWindowTheme
    {
        private static bool Prefix(Rect rect)
        {
            if (!ReframedMod.Active || !ReframedMod.Settings.WindowTheme) return true;
            using (new Theme.GuiScope(true)) { GUI.color = Color.white; Theme.Panel(rect); }
            return false;
        }
    }
}
