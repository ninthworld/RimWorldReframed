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
    internal static class SelectionInspector
    {
        private static readonly FieldInfo TabSize = AccessTools.Field(typeof(InspectTabBase), "size");
        private static readonly FieldInfo TabLabelKey = AccessTools.Field(typeof(InspectTabBase), "labelKey");
        private static readonly MethodInfo ToggleTab = AccessTools.Method(typeof(InspectPaneUtility), "InterfaceToggleTab");
        private static readonly MethodInfo StillValid = AccessTools.PropertyGetter(typeof(InspectTabBase), "StillValid");
        private static readonly MethodInfo Reposition = AccessTools.Method(typeof(Window), "SetInitialSizeAndPosition");
        private static Thing SelectedThing => Find.Selector.SingleSelectedThing;
        private static MainTabWindow_Inspect owner;
        private static bool nativeBackground, nativeShadow;
        private static Rect nativeBounds;
        private static Thing previousThing;
        private static Vector2 bodyScroll, tabScroll;
        private static bool details;
        private static bool restoring;
        private static readonly List<InspectTabBase> tabs = new List<InspectTabBase>();
        internal static bool DrawingNative { get; private set; }
        internal static InspectTabBase NativeTab { get; private set; }
        internal static Rect NativeBodyBounds { get; private set; }
        internal static Rect Bounds { get; private set; }
        internal static bool Active => owner != null && Eligible(owner);

        internal static bool Eligible(MainTabWindow_Inspect pane) => !restoring && pane != null && ReframedMod.Active && ReframedMod.Settings.SelectionPanels &&
            !TutorSystem.TutorialMode && !WorldRendererUtility.WorldSelected && Find.CurrentMap != null &&
            PawnInspectorLayout.Supported(UI.screenWidth, UI.screenHeight) && Find.Selector.SelectedObjects.Count == 1 && SelectedThing != null &&
            !SelectedThing.Destroyed && Find.MainTabsRoot.OpenTab == MainButtonDefOf.Inspect;

        internal static InspectTabBase SelectedTab(MainTabWindow_Inspect pane) => pane.CurTabs?.FirstOrDefault(tab =>
            tab.GetType() == pane.OpenTabType && tab.IsVisible && !tab.Hidden);

        internal static Rect CalculateBounds(MainTabWindow_Inspect pane)
        {
            Pawn pawn = SelectedThing as Pawn;
            float width = pawn != null ? PawnInspectorLayout.Width : 420f;
            float height = pawn != null ? PawnInspectorLayout.SummaryHeight : 200f;
            InspectTabBase tab = SelectedTab(pane);
            if (tab != null)
            {
                if (pawn != null && tab.GetType() == typeof(ITab_Pawn_Needs)) height = PawnInspectorLayout.NeedsHeight;
                else if (pawn != null && tab.GetType() == typeof(ITab_Pawn_Gear)) height = PawnInspectorLayout.GearHeight;
                else if (pawn != null && tab.GetType() == typeof(ITab_Pawn_Social)) height = PawnInspectorLayout.SocialHeight;
                else if (pawn != null && tab.GetType() == typeof(ITab_Pawn_Character)) height = PawnInspectorLayout.BioHeight;
                else if (pawn != null && tab.GetType() == typeof(ITab_Pawn_Health)) height = PawnInspectorLayout.HealthHeight;
                else if (pawn != null && tab.GetType() == typeof(ITab_Pawn_Log)) height = PawnInspectorLayout.LogHeight;
                else
                {
                    AccessTools.Method(tab.GetType(), "UpdateSize").Invoke(tab, null);
                    Vector2 requested = (Vector2)TabSize.GetValue(tab);
                    width = Math.Max(width, Math.Min(requested.x + 24f, UI.screenWidth - 80f));
                    height = pawn != null ? PawnInspectorLayout.DetailHeight : requested.y + PawnInspectorLayout.Header + PawnInspectorLayout.Tabs + 24f;
                }
            }
            height = PawnInspectorLayout.BoundedHeight(height, UI.screenHeight);
            return new Rect(8f, UI.screenHeight - 40f - height, width, height);
        }

        internal static void Prepare(MainTabWindow_Inspect pane)
        {
            if (!Eligible(pane)) { Release(); return; }
            if (owner != pane)
            {
                Release(); owner = pane;
                nativeBackground = pane.doWindowBackground; nativeShadow = pane.drawShadow; nativeBounds = pane.windowRect;
            }
            Bounds = CalculateBounds(pane);
            pane.windowRect = Bounds;
            // ExtraOnGUI precedes the WindowStack shadow pass. Keep these false
            // through the complete native window callback, including deferred GUI.
            pane.doWindowBackground = pane.drawShadow = false;
            if (previousThing != SelectedThing)
            {
                previousThing = SelectedThing;
                bodyScroll = tabScroll = Vector2.zero; details = false;
                PawnInspectorBodies.SelectionChanged(previousThing as Pawn);
            }
            InspectTabBase selected = SelectedTab(pane);
            if (selected != null)
            {
                using (PawnInspectorPopup.Enter())
                using (new Theme.GuiScope(true))
                {
                    GUI.color = GUI.contentColor = Color.white; Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                    AccessTools.Method(selected.GetType(), "UpdateSize").Invoke(selected, null);
                    Vector2 size = (Vector2)TabSize.GetValue(selected);
                    float offset = PawnInspectorBodies.NativeBodyOffset(selected);
                    NativeBodyBounds = new Rect(Bounds.x + 12 - bodyScroll.x,
                        Bounds.y + PawnInspectorLayout.Header + PawnInspectorLayout.Tabs + 10 + offset - bodyScroll.y,
                        Math.Max(Bounds.width - 40, size.x), Math.Max(Bounds.height - PawnInspectorLayout.Header - PawnInspectorLayout.Tabs - 20 - offset, size.y));
                    DrawingNative = true; NativeTab = selected;
                    try { AccessTools.Method(selected.GetType(), "ExtraOnGUI").Invoke(selected, null); }
                    catch (Exception error) { Log.ErrorOnce("[Reframed UI] Exception drawing inspector tab extras: " + error, 754292); }
                    finally { DrawingNative = false; NativeTab = null; }
                }
            }
        }

        private static void Release()
        {
            if (owner != null)
            {
                owner.doWindowBackground = nativeBackground; owner.drawShadow = nativeShadow;
                owner.windowRect = nativeBounds;
                restoring = true;
                try { Reposition.Invoke(owner, null); }
                finally { restoring = false; }
            }
            owner = null; Bounds = default; previousThing = null;
        }

        internal static void Draw(Rect rect, MainTabWindow_Inspect pane)
        {
            if (Event.current.type == EventType.Layout) return;
            using (PawnInspectorPopup.Enter())
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                Text.Font = GameFont.Small; Text.WordWrap = false;
                float headerBottom = PawnInspectorLayout.Header;
                float bodyTop = headerBottom + PawnInspectorLayout.Tabs;
                // The tab band is painted separately, dark behind inactive
                // tabs and continuous with the body only at the selected tab.
                GUI.BeginClip(new Rect(0, 0, rect.width, headerBottom));
                try { Theme.RoundRect(rect, PawnInspectorView.HeaderFill, 10f); }
                finally { GUI.EndClip(); }
                GUI.BeginClip(new Rect(0, bodyTop, rect.width, rect.height - bodyTop));
                try { Theme.RoundRect(new Rect(0, -bodyTop, rect.width, rect.height), PawnInspectorView.BodyFill, 10f); }
                finally { GUI.EndClip(); }
                Thing thing = SelectedThing;
                Pawn pawn = thing as Pawn;
                // Paint the band before the portrait, whose bust extends into
                // the reserved lane to the left of the tabs.
                DrawTabs(new Rect(pawn != null ? 66f : 10f, PawnInspectorLayout.Header, rect.width - (pawn != null ? 76f : 20f), PawnInspectorLayout.Tabs), pane);
                Rect header = new Rect(0, 0, rect.width, PawnInspectorLayout.Header);
                if (pawn != null) PawnInspectorView.Header(header, pawn);
                else
                {
                    Widgets.ThingIcon(new Rect(12, 5, 36, 36), thing);
                    PawnInspectorView.Label(new Rect(57, 8, rect.width - 260, 26), thing.LabelCap, Theme.Ink, true);
                }
                if (PawnInspectorWidgets.IconButton(new Rect(rect.width - 52, 7, 21, 21), "info", "Information"))
                    Find.WindowStack.Add(new Dialog_InfoCard(thing));
                if (PawnInspectorWidgets.IconButton(new Rect(rect.width - 28, 7, 21, 21), "settings", "Selection options"))
                    ShowOptions(pane, thing);
                Rect body = new Rect(0, PawnInspectorLayout.Header + PawnInspectorLayout.Tabs, rect.width,
                    rect.height - PawnInspectorLayout.Header - PawnInspectorLayout.Tabs);
                InspectTabBase selected = SelectedTab(pane);
                GUI.BeginGroup(body);
                try
                {
                    Rect local = new Rect(12, 10, body.width - 24, body.height - 20);
                    if (selected == null)
                    {
                        if (pawn != null && !details) PawnInspectorView.Summary(local, pawn, () => details = true);
                        else DrawDescription(local, pane, pawn != null);
                    }
                    else if (pawn != null && PawnInspectorBodies.Draw(local, pawn, selected)) { }
                    else DrawNativeTab(local, selected);
                }
                finally { GUI.EndGroup(); }
                GenUI.AbsorbClicksInRect(rect);
            }
        }

        private static void ShowOptions(MainTabWindow_Inspect pane, Thing thing)
        {
            InspectTabBase tab = SelectedTab(pane);
            bool Current() => Eligible(pane) && SelectedThing == thing;
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("Selection controls", () => { if (Current()) Find.WindowStack.Add(new InspectorControlsWindow(pane, thing)); }),
                new FloatMenuOption("Selection description", () => { if (Current()) { pane.CloseOpenTab(); details = true; bodyScroll = Vector2.zero; } })
            };
            if (pane.ShouldShowSelectNextInCellButton)
                options.Add(new FloatMenuOption("SelectNextInSquareTip".Translate(), () => { if (Current() && pane.ShouldShowSelectNextInCellButton) pane.SelectNextInCell(); }));
            if (thing is Pawn pawn && PawnInspectorBodies.Custom(tab))
                options.Insert(0, new FloatMenuOption("Tab options", () =>
                {
                    if (!Current() || SelectedTab(pane) != tab) return;
                    PawnInspectorExtras.Show(pawn, tab);
                }));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        // The native corner controls include pawn/DLC/mod-specific actions.
        // Keep that complete callback available in a compact overflow window.
        private sealed class InspectorControlsWindow : Window
        {
            private readonly MainTabWindow_Inspect pane;
            private readonly Thing thing;
            internal InspectorControlsWindow(MainTabWindow_Inspect pane, Thing thing)
            {
                this.pane = pane; this.thing = thing;
                doCloseX = true; closeOnClickedOutside = true; doWindowBackground = false; drawShadow = false;
            }
            public override Vector2 InitialSize => new Vector2(290, 100);
            public override void DoWindowContents(Rect inRect)
            {
                if (!Eligible(pane) || SelectedThing != thing) { Close(); return; }
                using (PawnInspectorPopup.Enter())
                using (new Theme.GuiScope(true))
                {
                    GUI.color = GUI.contentColor = Color.white;
                    Theme.RoundRect(inRect, PawnInspectorView.HeaderFill, 6);
                    PawnInspectorView.Heading(new Rect(10, 4, inRect.width - 30, 16), "SELECTION CONTROLS");
                    Text.Font = GameFont.Small; Text.WordWrap = true; Text.Anchor = TextAnchor.UpperLeft;
                    GUI.BeginGroup(new Rect(8, 28, inRect.width - 20, 30));
                    try { float used = 0; pane.DoInspectPaneButtons(new Rect(0, 0, inRect.width - 20, 30), ref used); }
                    finally { GUI.EndGroup(); }
                }
            }
        }

        private static void DrawDescription(Rect rect, MainTabWindow_Inspect pane, bool returnSummary)
        {
            using (new Theme.GuiScope(true))
            {
            GUI.color = GUI.contentColor = Color.white; Text.Font = GameFont.Small; Text.WordWrap = true; Text.Anchor = TextAnchor.UpperLeft;
            if (returnSummary)
            {
                if (Theme.ActionButton(new Rect(rect.xMax - 88, rect.y, 88, 23), "Summary")) details = false;
                rect.yMin += 28;
            }
            ObjectInspectorDescription.Draw(rect, pane, SelectedThing, ref bodyScroll);
            }
        }

        private static void DrawTabs(Rect rect, MainTabWindow_Inspect pane)
        {
            Rect band = new Rect(0, rect.y, Bounds.width, rect.height);
            tabs.Clear();
            if (pane.CurTabs != null) tabs.AddRange(pane.CurTabs.Where(tab => tab.IsVisible && !tab.Hidden));
            var ordered = tabs.OrderBy(Rank).ToList(); // stable for native extra/DLC/mod tabs
            tabs.Clear(); tabs.AddRange(ordered);
            float total = 34f + PawnInspectorLayout.TabGap + tabs.Sum(tab =>
                PawnInspectorLayout.TabWidth(PawnInspectorView.Measure(TabLabel(tab)), TabIcon(tab) != null) + PawnInspectorLayout.TabGap);
            bool overflow = total > rect.width;
            Rect previous = new Rect(rect.x, rect.y, 20, rect.height);
            Rect next = new Rect(rect.xMax - 20, rect.y, 20, rect.height);
            if (overflow)
            {
                rect.xMin += 23; rect.xMax -= 23;
                if (Event.current.type == EventType.ScrollWheel && Mouse.IsOver(rect))
                { tabScroll.x += Event.current.delta.y * 28; Event.current.Use(); }
            }
            tabScroll.x = Mathf.Clamp(tabScroll.x, 0, Math.Max(0, total - rect.width));
            float selectedX = 0, selectedWidth = pane.OpenTabType == null ? 34 : 0;
            float cursor = 34 + PawnInspectorLayout.TabGap;
            foreach (InspectTabBase tab in tabs)
            {
                float width = PawnInspectorLayout.TabWidth(PawnInspectorView.Measure(TabLabel(tab)), TabIcon(tab) != null);
                if (pane.OpenTabType == tab.GetType()) { selectedX = cursor; selectedWidth = width; }
                cursor += width + PawnInspectorLayout.TabGap;
            }
            float activeLeft = Mathf.Clamp(rect.x + selectedX - tabScroll.x, rect.xMin, rect.xMax);
            float activeRight = Mathf.Clamp(rect.x + selectedX + selectedWidth - tabScroll.x, activeLeft, rect.xMax);
            PawnInspectorView.TabBand(band, new Rect(activeLeft, band.y, activeRight - activeLeft, band.height));
            if (overflow)
            {
                if (Theme.ActionButton(previous, "‹")) tabScroll.x -= 100;
                if (Theme.ActionButton(next, "›")) tabScroll.x += 100;
            }
            tabScroll = GUI.BeginScrollView(rect, tabScroll, new Rect(0, 0, Math.Max(total, rect.width), rect.height), false, false, GUIStyle.none, GUIStyle.none);
            try
            {
                float x = 0;
                if (PawnInspectorView.Tab(new Rect(x, 0, 34, rect.height), "", pane.OpenTabType == null, "Summary", "summary", true))
                { pane.CloseOpenTab(); details = false; bodyScroll = Vector2.zero; }
                x += 34 + PawnInspectorLayout.TabGap;
                foreach (InspectTabBase tab in tabs)
                {
                    string label = TabLabel(tab);
                    string icon = TabIcon(tab);
                    float width = PawnInspectorLayout.TabWidth(PawnInspectorView.Measure(label), icon != null);
                    if (PawnInspectorView.Tab(new Rect(x, 0, width, rect.height), label, pane.OpenTabType == tab.GetType(), label, icon, true))
                    { ToggleTab.Invoke(null, new object[] { tab, pane }); bodyScroll = Vector2.zero; details = false; }
                    x += width + PawnInspectorLayout.TabGap;
                }
            }
            finally { GUI.EndScrollView(); }
        }
        private static int Rank(InspectTabBase tab)
        {
            Type type = tab.GetType();
            if (type == typeof(ITab_Pawn_Character)) return 0;
            if (type == typeof(ITab_Pawn_Needs)) return 1;
            if (type == typeof(ITab_Pawn_Gear)) return 2;
            if (type == typeof(ITab_Pawn_Health)) return 3;
            if (type == typeof(ITab_Pawn_Social)) return 4;
            if (type == typeof(ITab_Pawn_Log)) return 5;
            return 10;
        }
        private static string TabLabel(InspectTabBase tab) => tab.GetType() == typeof(ITab_Pawn_Character) ? "Bio" : ((string)TabLabelKey.GetValue(tab)).Translate().ToString();
        private static string TabIcon(InspectTabBase tab)
        {
            string[] names = { "bio", "needs", "gear", "health", "social", "log" };
            int rank = Rank(tab); return rank < names.Length ? names[rank] : null;
        }

        internal static void NotifyClosed(Window window) { if (owner == window) Release(); }

        internal static void DrawNativeTab(Rect rect, InspectTabBase tab)
        {
            if (owner == null || SelectedThing != previousThing || SelectedTab(owner) != tab || !tab.IsVisible || !(bool)StillValid.Invoke(tab, null)) return;
            AccessTools.Method(tab.GetType(), "UpdateSize").Invoke(tab, null);
            Vector2 size = (Vector2)TabSize.GetValue(tab);
            float width = Math.Max(rect.width - 16f, size.x), height = Math.Max(rect.height, size.y);
            bool vertical = height > rect.height + .01f;
            bool horizontal = width > rect.width - (vertical ? 9 : 0) + .01f;
            vertical = height > rect.height - (horizontal ? 9 : 0) + .01f;
            horizontal = width > rect.width - (vertical ? 9 : 0) + .01f;
            Rect viewport = rect;
            if (horizontal) viewport.height -= 9;
            if (vertical) viewport.width -= 9;
            bodyScroll.x = Mathf.Clamp(bodyScroll.x, 0, Math.Max(0, width - viewport.width));
            bodyScroll.y = Mathf.Clamp(bodyScroll.y, 0, Math.Max(0, height - viewport.height));
            bodyScroll = GUI.BeginScrollView(viewport, bodyScroll, new Rect(0, 0, width, height), false, false, GUIStyle.none, GUIStyle.none);
            try
            {
                DrawingNative = true; NativeTab = tab;
                NativeBodyBounds = new Rect(Bounds.x + rect.x - bodyScroll.x, Bounds.y + PawnInspectorLayout.Header + PawnInspectorLayout.Tabs + rect.y - bodyScroll.y, width, height);
                try
                {
                    using (new Theme.GuiScope(true))
                    {
                        GUI.color = GUI.contentColor = Color.white; Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                        AccessTools.Method(tab.GetType(), "FillTab").Invoke(tab, null);
                    }
                }
                catch (Exception error) { Log.ErrorOnce("[Reframed UI] Exception filling inspector tab " + tab.GetType() + ": " + error, 754291); }
            }
            finally { DrawingNative = false; NativeTab = null; GUI.EndScrollView(); }
            if (height > viewport.height + .01f) PawnInspectorWidgets.Rail(new Rect(rect.xMax - 5, rect.y, 4, viewport.height), ref bodyScroll.y, height, viewport.height, false);
            if (horizontal) PawnInspectorWidgets.Rail(new Rect(rect.x, rect.yMax - 5, viewport.width, 4), ref bodyScroll.x, width, viewport.width, true);
        }

        internal static Dictionary<string, object> AuditState() => new Dictionary<string, object>
        { ["active"] = Active, ["bounds"] = new[] { Bounds.x, Bounds.y, Bounds.width, Bounds.height },
            ["thing"] = previousThing?.ThingID, ["tab"] = owner?.OpenTabType?.FullName ?? "Summary", ["details"] = details,
            ["tabs"] = tabs.Select(tab => tab.GetType().FullName).ToArray() };
    }

    [HarmonyPatch(typeof(MainTabWindow_Inspect), nameof(MainTabWindow_Inspect.ExtraOnGUI))]
    internal static class SelectionInspectorPreparePatch
    { private static void Prefix(MainTabWindow_Inspect __instance) => SelectionInspector.Prepare(__instance); }

    [HarmonyPatch(typeof(MainTabWindow_Inspect), "get_RequestedTabSize")]
    internal static class SelectionInspectorSizePatch
    {
        private static bool Prefix(MainTabWindow_Inspect __instance, ref Vector2 __result)
        { if (!SelectionInspector.Eligible(__instance)) return true; __result = SelectionInspector.CalculateBounds(__instance).size; return false; }
    }
    [HarmonyPatch(typeof(MainTabWindow_Inspect), "get_Margin")]
    internal static class SelectionInspectorMarginPatch
    {
        private static bool Prefix(MainTabWindow_Inspect __instance, ref float __result)
        { if (!SelectionInspector.Eligible(__instance)) return true; __result = 0; return false; }
    }
    [HarmonyPatch(typeof(InspectPaneUtility), nameof(InspectPaneUtility.InspectPaneOnGUI))]
    internal static class SelectionInspectorContentsPatch
    {
        private static bool Prefix(Rect __0, IInspectPane __1)
        {
            var inspect = __1 as MainTabWindow_Inspect;
            if (!SelectionInspector.Eligible(inspect)) return true;
            SelectionInspector.Draw(__0, inspect); return false;
        }
    }
    [HarmonyPatch(typeof(InspectPaneUtility), "DoTabs")]
    internal static class SelectionInspectorTabsPatch
    { private static bool Prefix(IInspectPane __0) => !SelectionInspector.Eligible(__0 as MainTabWindow_Inspect); }
    [HarmonyPatch(typeof(InspectPaneUtility), nameof(InspectPaneUtility.PaneWidthFor))]
    internal static class SelectionInspectorPaneWidthPatch
    {
        private static bool Prefix(IInspectPane __0, ref float __result)
        {
            var pane = __0 as MainTabWindow_Inspect;
            if (!SelectionInspector.Eligible(pane)) return true;
            // Native actions begin14px after this edge, clearing the inset shell.
            __result = SelectionInspector.CalculateBounds(pane).xMax; return false;
        }
    }
    [HarmonyPatch(typeof(InspectTabBase), "get_TabRect")]
    internal static class SelectionInspectorNativeTabRectPatch
    {
        private static bool Prefix(InspectTabBase __instance, ref Rect __result)
        { if (!SelectionInspector.DrawingNative || SelectionInspector.NativeTab != __instance) return true; __result = SelectionInspector.NativeBodyBounds; return false; }
    }
    [HarmonyPatch(typeof(Window), nameof(Window.PostClose))]
    internal static class SelectionInspectorClosePatch
    { private static void Postfix(Window __instance) => SelectionInspector.NotifyClosed(__instance); }
}

