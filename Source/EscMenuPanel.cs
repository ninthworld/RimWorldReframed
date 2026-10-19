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
    // The actual native Menu window owns pause, Escape and child actions. Only
    // its presentation changes; outgoing visuals never retain native input.
    internal static class EscMenuPanel
    {
        private static readonly ToolsPanelMotion motion = new ToolsPanelMotion();
        private static readonly MethodInfo Reposition = AccessTools.Method(typeof(Window), "SetInitialSizeAndPosition");
        private static MainTabWindow_Menu owner, failed;
        private static Game game;
        private static Map map;
        private static int screenWidth, screenHeight;
        private static bool background, shadow, closeX, closeButton, links;
        private static Rect panel, launcher, shell;
        private static Vector2 actionScroll, linkScroll;
        private static GUIStyle label, heading;
        private static EscMenuOptions options;
        private static string paintedActions = "", paintedLinks = "";
        private static int paintedNavigation = -1;
        private static double Now => Time.realtimeSinceStartup;
        internal static bool Enabled => ReframedMod.Active && ReframedMod.Settings.EscapeMenu && HudLayout.DockEnabled &&
            !TutorSystem.TutorialMode && (WorldRendererUtility.WorldSelected || Find.CurrentMap != null) &&
            EscMenuPolicy.Supported(UI.screenWidth, UI.screenHeight) && EscMenuNative.Ready;
        internal static bool Eligible(Window window) => Enabled && window?.GetType() == typeof(MainTabWindow_Menu) && window != failed;
        internal static bool OwnsWindow => owner != null && Eligible(owner) && Find.WindowStack.Windows.Contains(owner);
        internal static bool OccludesMapPointer => Enabled && motion.Shell > 0 && Mouse.IsOver(shell);
        internal static bool Selected => Find.MainTabsRoot?.OpenTab == MainButtonDefOf.Menu;
        internal static bool BlocksTools => EscMenuPolicy.BlocksTools(Selected, Enabled, motion.Shell);
        internal static Rect Bounds => new Rect(HudLayout.MenuButtonBounds.xMax - EscMenuPolicy.Width,
            HudLayout.MenuButtonBounds.yMax - EscMenuPolicy.Height, EscMenuPolicy.Width, EscMenuPolicy.Height);

        internal static void Prepare(MainTabWindow_Menu window)
        {
            if (!Eligible(window))
            {
                if (owner == window) Fallback(window);
                return;
            }
            if (owner != window)
            {
                if (owner != null) Release(owner);
                bool resume = game == Current.Game && map == Find.CurrentMap && screenWidth == UI.screenWidth && screenHeight == UI.screenHeight;
                if (!resume || motion.Shell <= 0) { motion.Reset(); links = false; actionScroll = linkScroll = Vector2.zero; }
                game = Current.Game; map = Find.CurrentMap; screenWidth = UI.screenWidth; screenHeight = UI.screenHeight;
                owner = window; background = window.doWindowBackground; shadow = window.drawShadow;
                closeX = window.doCloseX; closeButton = window.doCloseButton;
                paintedActions = paintedLinks = "";
                paintedNavigation = -1;
                motion.SetOpen(true, Now);
            }
            panel = Bounds; launcher = HudLayout.MenuButtonBounds;
            window.doWindowBackground = window.drawShadow = window.doCloseX = window.doCloseButton = false;
            window.windowRect = panel;
            motion.Tick(Now); UpdateShell();
        }
        private static void UpdateShell()
        {
            float w = EscMenuPolicy.Reveal(launcher.width, panel.width, motion.Shell);
            float h = EscMenuPolicy.Reveal(launcher.height, panel.height, motion.Shell);
            shell = new Rect(launcher.xMax - w, launcher.yMax - h, w, h);
        }
        internal static void Tick()
        {
            if (game != Current.Game || map != Find.CurrentMap || screenWidth != UI.screenWidth || screenHeight != UI.screenHeight || !Enabled)
            {
                MainTabWindow_Menu old = owner;
                if (old != null) { Release(old); if (Find.WindowStack.Windows.Contains(old)) Reposition.Invoke(old, null); }
                motion.Reset(); options = null; shell = default; return;
            }
            if (owner != null && !Find.WindowStack.Windows.Contains(owner)) Release(owner);
            motion.Tick(Now); UpdateShell();
            if (owner == null && motion.Shell <= 0) options = null;
        }
        internal static void HandleOutside()
        {
            if (!OwnsWindow || Event.current == null) return;
            // The covered Tools launcher is reserved even before the reveal reaches it.
            bool inside = Mouse.IsOver(shell.ExpandedBy(5)) || Mouse.IsOver(HudLayout.ToolsButtonBounds);
            if (Find.WindowStack.GetsInput(owner) && !motion.Settled && Event.current.type == EventType.MouseDown &&
                Mouse.IsOver(HudLayout.ToolsButtonBounds)) { Event.current.Use(); return; }
            if (EscMenuPolicy.ConsumeOutside(motion.Open, Find.WindowStack.GetsInput(owner),
                Event.current.type == EventType.MouseDown, inside))
            { Find.MainTabsRoot.ToggleTab(MainButtonDefOf.Menu); Event.current.Use(); }
        }
        internal static void Closing(Window window)
        { if (window == owner) { motion.SetOpen(false, Now); UpdateShell(); } }
        internal static bool Cancel(Window window)
        {
            if (window != owner || !links || !Find.WindowStack.GetsInput(window)) return false;
            Navigate(false); Event.current.Use(); return true;
        }
        internal static void Release(Window window)
        {
            if (window == failed) failed = null;
            if (window != owner) return;
            window.doWindowBackground = background; window.drawShadow = shadow;
            window.doCloseX = closeX; window.doCloseButton = closeButton;
            owner = null;
        }
        private static void Fallback(MainTabWindow_Menu window)
        {
            Release(window); failed = window; motion.Reset(); options = null;
            Reposition.Invoke(window, null);
            // The current compact rectangle must never dispatch native buttons.
            if (Event.current.type == EventType.MouseDown) Event.current.Use();
        }
        internal static void Draw(MainTabWindow_Menu window, Rect rect)
        {
            Prepare(window);
            if (owner != window || Event.current.type == EventType.Layout) return;
            if (!EscMenuNative.TryCollect(window, out EscMenuOptions current)) { Fallback(window); return; }
            options = current;
            bool input = GUI.enabled && Find.WindowStack.GetsInput(window);
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                Rect localShell = shell; localShell.position -= panel.position;
                Paint(localShell, input, false);
                Rect button = launcher; button.position -= panel.position;
                PaintLauncher(button, input, motion.Open);
                if (input && Widgets.ButtonInvisible(button)) Find.MainTabsRoot.ToggleTab(MainButtonDefOf.Menu);
            }
        }
        internal static void DrawClosing()
        {
            if (owner != null || motion.Open || motion.Shell <= 0 || Event.current.type != EventType.Repaint ||
                Find.WindowStack.AnyWindowAbsorbingAllInput || Find.MainTabsRoot.OpenTab != null || ToolsPanel.IsOpen) return;
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white; GUI.enabled = false;
                GUI.BeginGroup(panel);
                try
                {
                    Rect localShell = shell; localShell.position -= panel.position;
                    Paint(localShell, false, true);
                    Rect button = launcher; button.position -= panel.position;
                    PaintLauncher(button, false, false, false);
                }
                finally { GUI.EndGroup(); }
            }
        }
        private static void PaintLauncher(Rect button, bool input, bool selected, bool disabled = true)
        {
            HudControls.Face(button, input && Mouse.IsOver(button), selected, disabled && !input);
            HudControls.MenuGlyph(button, disabled && !input, selected);
            string key = KeyBindingDefOf.Cancel.MainKey == KeyCode.None ? "" : KeyBindingDefOf.Cancel.MainKeyLabel;
            HudControls.Shortcut(button, key == "Escape" ? "Esc" : key, disabled && !input);
            if (input) TooltipHandler.TipRegion(button, selected ? "Close menu" : "Open menu");
        }
        private static void Navigate(bool showLinks)
        {
            if (!motion.Settled || links == showLinks) return;
            links = showLinks; motion.SetSubmenu(showLinks, Now);
            paintedNavigation = -1;
            if (showLinks) paintedLinks = ""; else paintedActions = "";
        }
        private static void Paint(Rect visibleShell, bool input, bool closing)
        {
            if (motion.Shell <= 0 || options == null) return;
            Styles(); Theme.Panel(visibleShell);
            Rect body = new Rect(EscMenuPolicy.Padding, EscMenuPolicy.Padding, panel.width - EscMenuPolicy.Padding * 2,
                panel.height - EscMenuPolicy.Padding - EscMenuPolicy.Footer);
            Rect clip = Intersect(visibleShell.ContractedBy(1), body);
            if (clip.width <= 0 || clip.height <= 0) return;
            GUI.BeginClip(clip);
            Vector2 mouse = Event.current.mousePosition;
            try
            {
                bool active = input && motion.Open && motion.Settled && !closing;
                if (!active) Event.current.mousePosition = new Vector2(-100000, -100000);
                Rect local = new Rect(body.x - clip.x, body.y - clip.y, body.width, body.height);
                float slide = ToolsPanelMotion.Ease(motion.Slide);
                if (slide < 1) DrawPage(new Rect(local.x - local.width * slide, local.y, local.width, local.height), false, active);
                if (slide > 0) DrawPage(new Rect(local.x + local.width * (1 - slide), local.y, local.width, local.height), true, active);
            }
            finally { Event.current.mousePosition = mouse; GUI.EndClip(); }
            PaintNavigation(visibleShell, input && !closing);
        }
        private static void PaintNavigation(Rect visibleShell, bool input)
        {
            Rect nav = new Rect(EscMenuPolicy.Padding, EscMenuPolicy.NavigationTop(panel.height),
                EscMenuPolicy.NavigationWidth, EscMenuPolicy.NavigationHeight);
            Rect clip = Intersect(visibleShell.ContractedBy(1), nav);
            if (clip.width <= 0 || clip.height <= 0) return;
            bool available = EscMenuPolicy.NavigationInput(motion.Open, motion.Settled, input,
                paintedNavigation == (links ? 1 : 0)) && clip == nav;
            GUI.BeginClip(clip);
            try
            {
                Rect local = new Rect(nav.x - clip.x, nav.y - clip.y, nav.width, nav.height);
                if (Button(local, links ? "Back".Translate().ToString() : "Links", available,
                    links ? SaveBrowserIcons.Back : MainMenuIcons.Get("ui.external-link"))) Navigate(!links);
            }
            finally { GUI.EndClip(); }
            if (Event.current.type == EventType.Repaint && motion.Open && motion.Settled)
                paintedNavigation = links ? 1 : 0;
        }
        private static void DrawPage(Rect body, bool web, bool input)
        {
            string title = web ? "Links" : MainButtonDefOf.Menu.LabelCap;
            IReadOnlyList<EscMenuOption> rows = web ? options.Links : options.Actions;
            string signature = string.Join("\u001f", rows.Select(row => row.Label));
            bool same = signature == (web ? paintedLinks : paintedActions);
            bool available = input && same;
            Label(new Rect(body.x + 2, body.y, body.width, 26), title.ToUpperInvariant(), Theme.Ink, true);
            Rect viewport = new Rect(body.x, body.y + 34, body.width, body.height - 34);
            Vector2 scroll = web ? linkScroll : actionScroll;
            float contentHeight = Math.Max(0, rows.Count * (EscMenuPolicy.Row + EscMenuPolicy.Gap) - EscMenuPolicy.Gap);
            using (new Theme.GuiScope(true))
            {
                GUI.enabled &= input;
                float width = PawnInspectorWidgets.BeginScroll(viewport, ref scroll, contentHeight);
                try
                {
                    float y = 0;
                    foreach (EscMenuOption row in rows)
                    {
                        Rect rect = new Rect(0, y, width, EscMenuPolicy.Row);
                        bool rowInput = EscMenuPolicy.RowInput(motion.Open, motion.Settled, available, same, rect.y, rect.yMax, scroll.y, viewport.height);
                        if (Button(rect, row.Label, rowInput, row.Icon)) { row.Activate(); break; }
                        if (rowInput) { TooltipHandler.TipRegion(rect, row.Tooltip); row.Highlight(rect); }
                        y += EscMenuPolicy.Row + EscMenuPolicy.Gap;
                    }
                }
                finally { PawnInspectorWidgets.EndScroll(viewport, ref scroll, contentHeight, web ? 280002 : 280001); }
            }
            if (web) linkScroll = scroll; else actionScroll = scroll;
            if (Event.current.type == EventType.Repaint && motion.Open && motion.Settled)
            { if (web) paintedLinks = signature; else paintedActions = signature; }
        }
        private static bool Button(Rect rect, string text, bool input, Texture2D icon = null)
        {
            bool live = input && GUI.enabled;
            Theme.ButtonFace(rect, live && Mouse.IsOver(rect) ? Theme.Hover : Theme.Surface, false);
            float start = rect.x + 8;
            if (icon != null)
            {
                GUI.color = Color.white; GUI.DrawTexture(new Rect(start, rect.center.y - 7, 14, 14), icon, ScaleMode.ScaleToFit); start += 20;
            }
            Label(new Rect(start, rect.y, Math.Max(1, rect.xMax - start - 8), rect.height), text, Theme.Ink);
            return live && Widgets.ButtonInvisible(rect);
        }
        private static void Styles()
        {
            Text.Font = GameFont.Small;
            if (label != null && label.font == Text.CurFontStyle.font) return;
            label = new GUIStyle(Text.CurFontStyle) { fontSize = 11, wordWrap = false, alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip, padding = new RectOffset(), margin = new RectOffset() };
            label.normal.textColor = Color.white;
            heading = new GUIStyle(label) { fontSize = 10, fontStyle = FontStyle.Bold };
        }
        private static void Label(Rect rect, string text, Color color, bool strong = false)
        { GUI.color = color; GUI.Label(rect, text, strong ? heading : label); GUI.color = Color.white; }
        private static Rect Intersect(Rect a, Rect b) => Rect.MinMaxRect(Math.Max(a.xMin, b.xMin), Math.Max(a.yMin, b.yMin),
            Math.Min(a.xMax, b.xMax), Math.Min(a.yMax, b.yMax));
    }
}
