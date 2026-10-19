using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Ownership follows an actual control opening a native dialog; unrelated
    // windows and mod-defined window classes never inherit this presentation.
    internal static class BottomMenuChildren
    {
        internal sealed class State
        {
            internal Window window;
            internal Game game;
            internal bool shadow, prepared, painted;
            internal Rect bounds;
            internal string title;
            internal Rect? trigger;
            internal bool fromTop;
            internal readonly ToolsPanelMotion motion = new ToolsPanelMotion();
        }
        private static readonly Dictionary<Window, State> children = new Dictionary<Window, State>();
        private static readonly System.Reflection.FieldInfo MenuTitle = AccessTools.Field(typeof(FloatMenu), "title");
        internal static State Current;
        private static Rect? dropdownTrigger;
        internal static IDisposable AnchorDropdown(Rect trigger) => new AnchorScope(trigger);
        private sealed class AnchorScope : IDisposable
        {
            private readonly Rect? previous;
            internal AnchorScope(Rect rect) { previous = dropdownTrigger; dropdownTrigger = UI.GUIToScreenRect(rect); }
            public void Dispose() { dropdownTrigger = previous; }
        }
        internal static bool Drawing => Current != null && !(Current.window is Dialog_InfoCard);
        internal static bool Moving => Current != null && (!Current.motion.Settled || !Current.painted);
        private static double Now => Time.realtimeSinceStartup;
        internal static void Register(Window window)
        {
            if (!(BottomMenuShell.Drawing || Current != null || WorldMenus.WorldDrawing || WorldMenus.CommandDrawing)) return;
            RegisterOwned(window);
        }
        internal static void RegisterWorldTool(Window window)
        {
            if (RimWorld.Planet.WorldRendererUtility.WorldSelected && ReframedMod.Settings.CompactTools) RegisterOwned(window);
        }
        private static void RegisterOwned(Window window)
        {
            if (!BottomMenuShell.Enabled) return;
            if (window == null || window is MainTabWindow || window is Page || window.GetType().Assembly != typeof(Window).Assembly) return;
            if (!(window is FloatMenu) && !window.GetType().Name.StartsWith("Dialog_", StringComparison.Ordinal)) return;
            var state = new State { window = window, game = Verse.Current.Game,
                trigger = window is FloatMenu ? dropdownTrigger : null, fromTop = window is FloatMenu,
                title = window is FloatMenu ? (string)MenuTitle.GetValue(window) : null };
            state.motion.SetOpen(true, Now); children[window] = state;
            if (window is FloatMenu menu) ArchitectPopup.RegisterBottomMenu(menu);
        }
        internal static void PositionDropdown(Window window)
        {
            // WindowStack.Add has now run native PreOpen/positioning. Move only
            // owned dropdowns with an explicit trigger; keep native size/options.
            if (!(window is FloatMenu) || !children.TryGetValue(window, out State state) || !state.trigger.HasValue) return;
            Rect trigger = state.trigger.Value, bounds = window.windowRect;
            float titleHeight = state.title.NullOrEmpty() ? 0 : 25;
            bool above = BottomMenuPolicy.PopupAbove(trigger.y, trigger.yMax, bounds.height + titleHeight, UI.screenHeight);
            bounds.x = BottomMenuPolicy.PopupLeft(trigger.x, bounds.width, UI.screenWidth);
            bounds.y = BottomMenuPolicy.PopupTop(trigger.y, trigger.yMax, bounds.height + titleHeight, UI.screenHeight, above) + titleHeight;
            state.fromTop = !above; state.bounds = window.windowRect = bounds;
        }
        internal static void Prepare(WindowStack stack)
        {
            foreach (State state in children.Values.ToArray())
            {
                state.motion.Tick(Now);
                if (!BottomMenuShell.Enabled || state.game != Verse.Current.Game || !state.motion.Open && state.motion.Shell == 0)
                {
                    if (state.prepared) state.window.drawShadow = state.shadow;
                    children.Remove(state.window); continue;
                }
                if (!state.motion.Open || !stack.Windows.Contains(state.window)) continue;
                if (!state.prepared) { state.shadow = state.window.drawShadow; state.prepared = true; }
                state.window.drawShadow = false;
                state.bounds = state.window.windowRect;
            }
        }
        internal static void Close(Window window)
        {
            if (!children.TryGetValue(window, out State state)) return;
            state.motion.SetOpen(false, Now); state.painted = false;
            if (state.prepared) window.drawShadow = state.shadow;
        }
        internal static Rect Reveal(Rect rect, float value, bool fromTop = false)
        {
            float progress = ToolsPanelMotion.Ease(value);
            return new Rect(rect.x, BottomMenuPolicy.RevealTop(rect.y, rect.height, progress, fromTop), rect.width, rect.height * progress);
        }
        private static Rect RevealBody(State state, Rect rect)
        {
            BottomMenuPolicy.RevealBody(rect.y, rect.height, state.title.NullOrEmpty() ? 0 : 25,
                ToolsPanelMotion.Ease(state.motion.Shell), state.fromTop, out float top, out float height);
            return new Rect(rect.x, top, rect.width, height);
        }
        internal static bool Title(FloatMenu window, string title)
        {
            if (!BottomMenuShell.Enabled || !children.TryGetValue(window, out State state)) return false;
            state.title = title; state.bounds = window.windowRect;
            if (title.NullOrEmpty() || Event.current.type != EventType.Repaint) return true;
            Rect rect = TitleRect(state);
            Find.WindowStack.ImmediateWindow(6830963, rect, WindowLayer.Super,
                () => PaintTitle(state, rect.AtZero()), doBackground: false, absorbInputAroundWindow: false, shadowAlpha: 0);
            return true;
        }
        private static Rect TitleRect(State state) => new Rect(state.bounds.x, Math.Max(0, state.bounds.y - 25), state.bounds.width, 23);
        private static void PaintTitle(State state, Rect rect)
        {
            // The title follows the same vertical reveal as its dropdown.
            Rect union = new Rect(0, 0, state.bounds.width, state.bounds.height + 25);
            Rect reveal = Reveal(union, state.motion.Shell, state.fromTop);
            float top = Math.Max(0, reveal.y), bottom = Math.Min(23, reveal.yMax);
            if (bottom <= top || reveal.width <= 0) return;
            Rect clip = new Rect(rect.x, rect.y + top, reveal.width, bottom - top);
            using (new Theme.GuiScope(true))
            {
                GUI.enabled = false; GUI.color = Color.white;
                GUI.BeginGroup(clip);
                try
                {
                    Rect local = new Rect(rect.x - clip.x, rect.y - clip.y, rect.width, rect.height);
                    Theme.Panel(local); PawnInspectorView.Label(local.ContractedBy(8, 0), state.title, Theme.Muted);
                }
                finally { GUI.EndGroup(); }
            }
        }
        internal sealed class Scope : IDisposable
        {
            private State previous;
            private Theme.GuiScope gui;
            private Vector2 mouse;
            private bool groups, active;
            internal bool Run = true;
            internal Scope(Window window)
            {
                if (!children.TryGetValue(window, out State state) || !state.motion.Open || !BottomMenuShell.Enabled) return;
                active = true; previous = Current; Current = state; gui = new Theme.GuiScope(true);
                state.bounds = window.windowRect;
                bool interactive = BottomMenuPolicy.Interactive(state.motion.Open, state.motion.Settled, state.painted,
                    GUI.enabled && Find.WindowStack.GetsInput(window));
                if (!interactive && Event.current.type != EventType.Repaint)
                {
                    if (Find.WindowStack.GetsInput(window) && KeyBindingDefOf.Cancel.KeyDownEvent)
                    { window.OnCancelKeyPressed(); Event.current.Use(); }
                    Run = false; return;
                }
                if (interactive) return;
                Rect bounds = window.windowRect.AtZero(), reveal = RevealBody(state, bounds);
                if (reveal.width <= 0 || reveal.height <= 0) { Run = false; return; }
                GUI.enabled = false; mouse = Event.current.mousePosition;
                Event.current.mousePosition = new Vector2(-100000, -100000);
                GUI.BeginGroup(reveal); GUI.BeginGroup(new Rect(-reveal.x, -reveal.y, bounds.width, bounds.height)); groups = true;
            }
            public void Dispose()
            {
                if (!active) return;
                if (groups) { GUI.EndGroup(); GUI.EndGroup(); Event.current.mousePosition = mouse; }
                if (Run && Event.current.type == EventType.Repaint && Current.motion.Settled) Current.painted = true;
                Current = previous; gui.Dispose(); active = false;
            }
        }
        internal static void DrawClosing()
        {
            if (Event.current.type != EventType.Repaint || !BottomMenuShell.Enabled || Find.WindowStack.AnyWindowAbsorbingAllInput) return;
            using (new Theme.GuiScope(true))
            {
                GUI.enabled = false; GUI.color = Color.white;
                foreach (State state in children.Values)
                    if (!state.motion.Open && state.motion.Shell > 0)
                    {
                        Theme.Panel(RevealBody(state, state.bounds));
                        if (!state.title.NullOrEmpty()) PaintTitle(state, TitleRect(state));
                    }
            }
        }
    }
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Add))]
    internal static class BottomMenuChildOwnerPatch
    {
        private static void Prefix(Window __0) => BottomMenuChildren.Register(__0);
        private static void Postfix(Window __0) => BottomMenuChildren.PositionDropdown(__0);
    }
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.WindowStackOnGUI))]
    internal static class BottomMenuChildPreparePatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(WindowStack __instance) => BottomMenuChildren.Prepare(__instance);
        private static void Postfix() => BottomMenuChildren.DrawClosing();
    }
    [HarmonyPatch(typeof(Window), "InnerWindowOnGUI")]
    internal static class BottomMenuChildDrawPatch
    {
        private static bool Prefix(Window __instance, out BottomMenuChildren.Scope __state)
        { __state = new BottomMenuChildren.Scope(__instance); return __state.Run; }
        private static Exception Finalizer(Exception __exception, BottomMenuChildren.Scope __state)
        { __state?.Dispose(); return __exception; }
    }
    [HarmonyPatch(typeof(Window), nameof(Window.PostClose))]
    internal static class BottomMenuChildClosePatch
    { private static void Prefix(Window __instance) => BottomMenuChildren.Close(__instance); }
    [HarmonyPatch(typeof(FloatMenu), "UpdateBaseColor")]
    internal static class BottomMenuChildFadePatch
    {
        private static bool Prefix(FloatMenu __instance, ref Color ___baseColor)
        {
            if (BottomMenuChildren.Current?.window != __instance || !BottomMenuChildren.Moving) return true;
            // The inert reveal deliberately has no pointer; it must not trigger
            // native distance-based dismissal from that paint-only mouse position.
            ___baseColor = Color.white; return false;
        }
    }
    [HarmonyPatch(typeof(FloatMenu), nameof(FloatMenu.ExtraOnGUI))]
    internal static class BottomMenuChildTitlePatch
    { private static bool Prefix(FloatMenu __instance, string ___title) => !BottomMenuChildren.Title(__instance, ___title); }
    [HarmonyPatch(typeof(Widgets), nameof(Widgets.CloseButtonFor))]
    internal static class BottomMenuChildCloseButtonPatch
    {
        private static bool Prefix(Rect __0, ref bool __result)
        {
            if (!BottomMenuChildren.Drawing) return true;
            Rect button = new Rect(__0.xMax - 30, __0.y + 6, 24, 24);
            Theme.ButtonFace(button, GUI.enabled && Mouse.IsOver(button) ? Theme.Hover : Theme.Surface, !GUI.enabled);
            using (new Theme.GuiScope(true))
            { GUI.color = Theme.Muted; GUI.DrawTexture(button.ContractedBy(6), ToolsIcons.Get("clear")); }
            __result = GUI.enabled && Widgets.ButtonInvisible(button); return false;
        }
    }
}
