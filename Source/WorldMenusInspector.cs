using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // The real WorldInspectPane remains the owner; selection, WITab instances,
    // native update/on-open/close/extra callbacks and world gizmos remain native.
    internal static class WorldMenusInspector
    {
        private static readonly ToolsPanelMotion motion = new ToolsPanelMotion();
        private static readonly FieldInfo Size = AccessTools.Field(typeof(InspectTabBase), "size");
        private static readonly MethodInfo Valid = AccessTools.PropertyGetter(typeof(InspectTabBase), "StillValid");
        private static readonly MethodInfo Fill = AccessTools.Method(typeof(InspectTabBase), "FillTab");
        private static readonly MethodInfo Extra = AccessTools.Method(typeof(InspectTabBase), "ExtraOnGUI");
        private static readonly MethodInfo UpdateSize = AccessTools.Method(typeof(InspectTabBase), "UpdateSize");
        private static readonly MethodInfo Toggle = AccessTools.Method(typeof(InspectPaneUtility), "InterfaceToggleTab");
        private static readonly MethodInfo Reposition = AccessTools.Method(typeof(Window), "SetInitialSizeAndPosition");
        private static WorldInspectPane owner;
        private static Game game;
        private static bool background, shadow, restoring, painted;
        private static Rect nativeRect;
        private static Vector2 bodyScroll, tabScroll;
        private static InspectTabBase lastTab;
        private static string selection;
        internal static bool Drawing { get; private set; }
        internal static InspectTabBase DrawingTab { get; private set; }
        internal static Rect NativeBody { get; private set; }
        internal static Rect Bounds { get; private set; }
        internal static bool Eligible(WorldInspectPane pane) => !restoring && pane != null && pane.GetType() == typeof(WorldInspectPane) &&
            WorldMenus.Enabled?.Invoke() == true && Current.ProgramState == ProgramState.Playing && Current.Game != null &&
            Find.World != null && WorldRendererUtility.WorldSelected && !TutorSystem.TutorialMode && UI.screenWidth >= 760 && UI.screenHeight >= 480 &&
            Find.MainTabsRoot.OpenTab == null && pane.AnythingSelected &&
            WorldMenus.Unmodified(typeof(WorldInspectPane), "DoWindowContents", "ExtraOnGUI") &&
            WorldMenus.Unmodified(typeof(InspectPaneUtility), "InspectPaneOnGUI", "DoTabs", "ExtraOnGUI");
        private static InspectTabBase Selected(WorldInspectPane pane) => pane.CurTabs?.FirstOrDefault(t => t.GetType() == pane.OpenTabType && t.IsVisible);
        private static Rect Calculate()
        {
            float width = Math.Min(560, UI.screenWidth - 280);
            float height = Math.Min(360, UI.screenHeight - 140);
            return new Rect(8, UI.screenHeight - 42 - height, width, height);
        }
        private static double Now => Time.realtimeSinceStartup;
        internal static void Prepare()
        {
            if (Current.ProgramState != ProgramState.Playing || Current.Game == null || Find.World == null || Find.WindowStack == null)
            { Release(); motion.Reset(); return; }
            WorldInspectPane pane = Find.WorldInterface?.inspectPane;
            bool present = pane != null && Find.WindowStack.Windows.Contains(pane);
            if (game != null && game != Current.Game) { Release(); motion.Reset(); }
            if (present && Eligible(pane))
            {
                if (owner != pane)
                {
                    Release(); owner = pane; game = Current.Game; background = pane.doWindowBackground; shadow = pane.drawShadow; nativeRect = pane.windowRect;
                    motion.Reset(); motion.SetOpen(true, Now); painted = false;
                }
                else if (!motion.Open) { motion.SetOpen(true, Now); painted = false; }
                Rect nextBounds = Calculate();
                if (nextBounds != Bounds) painted = false;
                Bounds = nextBounds; pane.windowRect = Bounds;
                pane.doWindowBackground = pane.drawShadow = false;
                string current = Find.WorldSelector.SelectedTile.ToString() + ":" + string.Join(",", Find.WorldSelector.SelectedObjects.Select(o => o.ID.ToString()));
                InspectTabBase selected = Selected(pane);
                if (selection != current || selected != lastTab) { selection = current; lastTab = selected; bodyScroll = Vector2.zero; }
            }
            else if (owner != null && motion.Open)
            {
                if (!present || (WorldMenus.Enabled?.Invoke() == true &&
                    (!WorldRendererUtility.WorldSelected || !owner.AnythingSelected || Find.MainTabsRoot.OpenTab != null))) Close(owner);
                else { Release(); motion.Reset(); }
            }
            motion.Tick(Now);
            if (!motion.Open && motion.Shell == 0) Release();
        }
        internal static void Close(Window window)
        {
            if (window != owner) return;
            motion.SetOpen(false, Now); painted = false;
            // Restore flags immediately; an outgoing ghost is paint only.
            if (!Find.WindowStack.Windows.Contains(owner))
            { owner.doWindowBackground = background; owner.drawShadow = shadow; }
        }
        private static void Release()
        {
            if (owner != null)
            {
                owner.doWindowBackground = background; owner.drawShadow = shadow; owner.windowRect = nativeRect;
                if (Find.WindowStack != null && Find.WindowStack.Windows.Contains(owner))
                { restoring = true; try { Reposition.Invoke(owner, null); } finally { restoring = false; } }
            }
            owner = null; game = null; selection = null; lastTab = null; painted = false; bodyScroll = tabScroll = Vector2.zero;
        }
        private static Rect Shell
        {
            get { float t = ToolsPanelMotion.Ease(motion.Shell); return new Rect(Bounds.x, WorldMenusPolicy.RevealTop(Bounds.yMax, Bounds.height, t), Bounds.width, Bounds.height * t); }
        }
        internal static void DrawClosing()
        {
            if (owner == null || motion.Open || motion.Shell == 0 || Event.current.type != EventType.Repaint || game != Current.Game ||
                Find.WindowStack.AnyWindowAbsorbingAllInput || Find.MainTabsRoot.OpenTab != null || Find.WorldRoutePlanner.Active || WorldMenus.Enabled?.Invoke() != true) return;
            Theme.RoundRect(Shell, Theme.PanelFill, 8);
        }
        internal static bool Owns(WorldInspectPane pane) => pane == owner && Eligible(pane) && Find.WindowStack.Windows.Contains(pane);
        internal static bool Closing(WorldInspectPane pane) => pane != null && pane == owner && !motion.Open && game == Current.Game;
        internal static void Extras(WorldInspectPane pane)
        {
            if (!Owns(pane) || !motion.Settled || !painted) return;
            bool input = GUI.enabled && Find.WindowStack.GetsInput(pane);
            if (!input && Event.current.type != EventType.Repaint) return;
            if (input && KeyBindingDefOf.SelectNextInCell.KeyDownEvent) pane.SelectNextInCell();
            InspectTabBase tab = Selected(pane);
            if (tab == null) return;
            NativeRect(tab, BodyRect(Bounds.AtZero()));
            using (new Theme.GuiScope(true))
            using (PawnInspectorPopup.Enter())
            {
                Vector2 mouse = Event.current.mousePosition;
                if (!input) Event.current.mousePosition = new Vector2(-100000, -100000);
                GUI.enabled &= input;
                Drawing = true; DrawingTab = tab;
                try { Extra.Invoke(tab, null); }
                catch (Exception error) { Log.ErrorOnce("[Reframed UI] World inspector tab extras: " + error, 300305); }
                finally { Drawing = false; DrawingTab = null; Event.current.mousePosition = mouse; }
            }
        }
        private static Rect BodyRect(Rect rect) => new Rect(12, 80, rect.width - 24, rect.height - 92);
        internal static void Draw(WorldInspectPane pane, Rect rect)
        {
            if (Event.current.type == EventType.Layout) return;
            bool input = motion.Settled && painted && GUI.enabled && Find.WindowStack.GetsInput(pane);
            if (!input && Event.current.type != EventType.Repaint) { GenUI.AbsorbClicksInRect(rect); return; }
            Rect clip = motion.Settled ? rect : new Rect(0, Shell.y - Bounds.y, Shell.width, Shell.height);
            Vector2 mouse = Event.current.mousePosition;
            using (new Theme.GuiScope(true))
            using (PawnInspectorPopup.Enter())
            {
                GUI.color = GUI.contentColor = Color.white;
                GUI.enabled &= input;
                if (!input) Event.current.mousePosition = new Vector2(-100000, -100000);
                GUI.BeginGroup(clip); GUI.BeginGroup(new Rect(-clip.x, -clip.y, rect.width, rect.height));
                try
                {
                    Drawing = true;
                    GUI.BeginClip(new Rect(0, 0, rect.width, 43));
                    try { Theme.RoundRect(rect, PawnInspectorView.HeaderFill, 10); }
                    finally { GUI.EndClip(); }
                    GUI.BeginClip(new Rect(0, 70, rect.width, rect.height - 70));
                    try { Theme.RoundRect(new Rect(0, -70, rect.width, rect.height), PawnInspectorView.BodyFill, 10); }
                    finally { GUI.EndClip(); }
                    DrawTabs(new Rect(12, 43, rect.width - 24, 27), pane);
                    Rect title = new Rect(14, 7, rect.width - 92, 27);
                    WorldMenus.Label(title, pane.GetLabel(title), true);
                    WorldObject selectedObject = Find.WorldSelector.SingleSelectedObject;
                    var selectedTile = Find.WorldSelector.SelectedTile;
                    if ((selectedObject != null || selectedTile.Valid) &&
                        PawnInspectorWidgets.IconButton(new Rect(rect.width - 57, 7, 22, 22), "info", "Information"))
                    {
                        if (selectedObject != null) Find.WindowStack.Add(new Dialog_InfoCard(selectedObject));
                        else if (Find.WorldGrid[selectedTile].PrimaryBiome != null)
                            Find.WindowStack.Add(new Dialog_InfoCard(Find.WorldGrid[selectedTile].PrimaryBiome));
                    }
                    if (pane.ShouldShowSelectNextInCellButton && PawnInspectorWidgets.IconButton(new Rect(rect.width - 31, 7, 22, 22), "plus", "SelectNextInSquareTip".Translate())) pane.SelectNextInCell();
                    Rect body = BodyRect(rect);
                    InspectTabBase selected = Selected(pane);
                    if (selected == null && pane.ShouldShowPaneContents)
                    {
                        // Native full description includes mod WorldObject inspect-string additions.
                        GUI.BeginGroup(body);
                        try { using (new Theme.GuiScope(true)) { Text.Font = GameFont.Small; Text.WordWrap = true; pane.DoPaneContents(body.AtZero()); } }
                        finally { GUI.EndGroup(); }
                    }
                    else if (selected != null) DrawTab(body, selected);
                    pane.RecentHeight = rect.height;
                    if (Event.current.type == EventType.Repaint && motion.Settled) painted = true;
                }
                finally { Drawing = false; DrawingTab = null; GUI.EndGroup(); GUI.EndGroup(); Event.current.mousePosition = mouse; }
                GenUI.AbsorbClicksInRect(rect);
            }
        }
        private static void DrawTabs(Rect rect, WorldInspectPane pane)
        {
            List<InspectTabBase> tabs = pane.CurTabs?.Where(t => t.IsVisible && !t.Hidden).ToList() ?? new List<InspectTabBase>();
            InspectTabBase activeHidden = Selected(pane);
            if (activeHidden != null && !tabs.Contains(activeHidden)) tabs.Add(activeHidden);
            float total = 38 + tabs.Sum(t => Math.Max(64, PawnInspectorView.Measure(t.labelKey.Translate()) + 18) + 4);
            if (Event.current.type == EventType.ScrollWheel && Mouse.IsOver(rect)) { tabScroll.x += Event.current.delta.y * 28; Event.current.Use(); }
            tabScroll.x = Mathf.Clamp(tabScroll.x, 0, Math.Max(0, total - rect.width));
            float selectedX = 0, selectedWidth = pane.OpenTabType == null ? 34 : 0, cursor = 38;
            foreach (InspectTabBase tab in tabs)
            {
                float width = Math.Max(64, PawnInspectorView.Measure(tab.labelKey.Translate()) + 18);
                if (pane.OpenTabType == tab.GetType()) { selectedX = cursor; selectedWidth = width; }
                cursor += width + 4;
            }
            float activeLeft = Mathf.Clamp(rect.x + selectedX - tabScroll.x, rect.xMin, rect.xMax);
            float activeRight = Mathf.Clamp(rect.x + selectedX + selectedWidth - tabScroll.x, activeLeft, rect.xMax);
            PawnInspectorView.TabBand(new Rect(0, rect.y, Bounds.width, rect.height), new Rect(activeLeft, rect.y, activeRight - activeLeft, rect.height));
            tabScroll = GUI.BeginScrollView(rect, tabScroll, new Rect(0, 0, Math.Max(total, rect.width), rect.height), false, false, GUIStyle.none, GUIStyle.none);
            try
            {
                float x = 0;
                if (PawnInspectorView.Tab(new Rect(x, 0, 34, rect.height), "", pane.OpenTabType == null, "Summary", "summary", true)) pane.CloseOpenTab();
                x += 38;
                foreach (InspectTabBase tab in tabs)
                {
                    string label = tab.labelKey.Translate(); float width = Math.Max(64, PawnInspectorView.Measure(label) + 18);
                    if (PawnInspectorView.Tab(new Rect(x, 0, width, rect.height), label, pane.OpenTabType == tab.GetType(), label, bandPainted: true)) Toggle.Invoke(null, new object[] { tab, pane });
                    if (GUI.enabled && pane.OpenTabType != tab.GetType() && !tab.TutorHighlightTagClosed.NullOrEmpty()) UIHighlighter.HighlightOpportunity(new Rect(x, 0, width, 25), tab.TutorHighlightTagClosed);
                    x += width + 4;
                }
            }
            finally { GUI.EndScrollView(); }
        }
        private static Vector2 NativeRect(InspectTabBase tab, Rect body)
        {
            UpdateSize.Invoke(tab, null); Vector2 size = (Vector2)Size.GetValue(tab);
            size.x = Math.Max(body.width - 10, size.x); size.y = Math.Max(body.height - 10, size.y);
            NativeBody = new Rect(Bounds.x + body.x - bodyScroll.x, Bounds.y + body.y - bodyScroll.y, size.x, size.y);
            return size;
        }
        private static void DrawTab(Rect body, InspectTabBase tab)
        {
            if (!(bool)Valid.Invoke(tab, null) || !tab.IsVisible) return;
            Vector2 size = NativeRect(tab, body);
            bool horizontal = size.x > body.width - 10, vertical = size.y > body.height - 10;
            Rect viewport = body; if (horizontal) viewport.height -= 9; if (vertical) viewport.width -= 9;
            bodyScroll.x = Mathf.Clamp(bodyScroll.x, 0, Math.Max(0, size.x - viewport.width));
            bodyScroll.y = Mathf.Clamp(bodyScroll.y, 0, Math.Max(0, size.y - viewport.height));
            bodyScroll = GUI.BeginScrollView(viewport, bodyScroll, new Rect(0, 0, size.x, size.y), false, false, GUIStyle.none, GUIStyle.none);
            try
            {
                DrawingTab = tab; NativeRect(tab, body);
                using (new Theme.GuiScope(true))
                {
                    Text.Font = GameFont.Small; Text.WordWrap = true;
                    try { Fill.Invoke(tab, null); }
                    catch (Exception error) { Log.ErrorOnce("[Reframed UI] World inspector tab: " + error, 300304); }
                }
            }
            finally { DrawingTab = null; GUI.EndScrollView(); }
            if (vertical) PawnInspectorWidgets.Rail(new Rect(body.xMax - 4, body.y, 4, viewport.height), ref bodyScroll.y, size.y, viewport.height, false);
            if (horizontal) PawnInspectorWidgets.Rail(new Rect(body.x, body.yMax - 4, viewport.width, 4), ref bodyScroll.x, size.x, viewport.width, true);
        }
    }
}
