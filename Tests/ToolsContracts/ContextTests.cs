using System;
using ReframedUI;
using Verse;
using UnityEngine;

// Compile the actual production Prepare/reset path against throwing entry-root
// seams, so a regression in access ordering fails without launching the game.
internal static class ContextTests
{
    internal static void Run()
    {
        foreach (bool eligible in new[] { false, true })
        foreach (ProgramState state in new[] { ProgramState.Entry, ProgramState.Playing })
        {
            Current.ProgramState = state; Find.UIRoot = new UIRoot_Entry();
            Find.Reads = 0; ToolsPanel.Seed(); GUI.Focus = "NativeSeedField";
            ToolsPanel.Prepare(eligible);
            Require(Find.Reads == 0 && ToolsPanel.Cleared, "Entry root must not access play services or retain input state");
            Require(GUI.Focus == "NativeSeedField", "Native focus must survive reset");
        }
        Current.ProgramState = ProgramState.Playing; Find.UIRoot = new UIRoot_Play();
        Find.Reads = 0; ToolsPanel.Seed(); GUI.Focus = "Reframed.ToolsSearch";
        ToolsPanel.Prepare(false);
        Require(Find.Reads == 0 && ToolsPanel.Cleared && GUI.Focus == null, "Disabled mod releases only its own state before play reads");
        Find.Map = new Map(); ToolsPanel.Prepare(true);
        Require(Find.Reads > 0, "Eligible play root reads native context");
        ToolsPanel.Seed(); Find.UIRoot = new UIRoot_Entry(); Current.ProgramState = ProgramState.Entry;
        Find.Reads = 0; GUI.Focus = "Reframed.ToolsSearch"; ToolsPanel.Prepare(false);
        Require(Find.Reads == 0 && ToolsPanel.Cleared && GUI.Focus == null, "Returning to entry clears stale Tools state safely");
        Console.WriteLine("PASS Tools production context reset: entry, transitional root, disabled play, enabled play, return-to-menu and focus ownership.");
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
namespace UnityEngine
{
    internal struct Vector2 { internal static Vector2 zero => default; }
    internal static class GUI
    {
        internal static string Focus;
        internal static string GetNameOfFocusedControl() => Focus;
        internal static void FocusControl(string value) => Focus = value;
    }
}
namespace RimWorld
{
    internal class MainButtonDef { }
    internal class MainTabsRoot { internal MainButtonDef OpenTab = null; }
}
namespace RimWorld.Planet
{
    internal static class WorldRendererUtility { internal static bool WorldSelected => false; }
}
namespace Verse
{
    internal enum ProgramState { Entry, Playing }
    internal static class Current { internal static ProgramState ProgramState; }
    internal class UIRoot { }
    internal class UIRoot_Play : UIRoot { }
    internal class UIRoot_Entry : UIRoot { }
    internal class Map { }
    internal class WindowStack { internal bool AnyWindowAbsorbingAllInput = false; }
    internal static class Find
    {
        internal static UIRoot UIRoot;
        internal static int Reads;
        internal static Map Map;
        private static void Access() { Reads++; if (!(UIRoot is UIRoot_Play)) throw new InvalidCastException("Entry root has no play services"); }
        internal static Map CurrentMap { get { Access(); return Map; } }
        internal static RimWorld.MainTabsRoot MainTabsRoot { get { Access(); return new RimWorld.MainTabsRoot(); } }
        internal static WindowStack WindowStack { get { Access(); return new WindowStack(); } }
    }
}
namespace ReframedUI
{
    internal static partial class ToolsPanel
    {
        private static readonly ToolsPanelMotion motion = new ToolsPanelMotion();
        private sealed class Search { internal string Query; internal void Reset() => Query = ""; }
        private static readonly Search search = new Search();
        private static bool focusSearch, releaseFocus, lastWorld;
        private static string focusedControl, page, outgoingPage, lastReason;
        private static int keyboardControl;
        private static Vector2 searchScroll;
        private static Map lastMap;
        private static RimWorld.MainButtonDef lastTab;
        private static void Open(bool value, string reason) { motion.SetOpen(value, 1); lastReason = reason; }
        internal static void Seed()
        {
            motion.SetOpen(true, 0); motion.Tick(1); search.Query = "test";
            focusSearch = releaseFocus = lastWorld = true; focusedControl = "Reframed.ToolsSearch";
            keyboardControl = 12; page = outgoingPage = "search"; lastMap = new Map(); lastTab = new RimWorld.MainButtonDef();
        }
        internal static bool Cleared => !motion.Open && motion.Shell == 0 && search.Query == "" &&
            !focusSearch && !releaseFocus && !lastWorld && focusedControl == "" && keyboardControl == 0 &&
            page == "main" && outgoingPage == "main" && lastMap == null && lastTab == null;
    }
}
