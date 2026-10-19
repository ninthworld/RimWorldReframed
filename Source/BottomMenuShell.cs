using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Native windows retain their lifecycle and data. This owns only their frame,
    // deliberate body renderers and real-time presentation; closure is immediate.
    internal static class BottomMenuShell
    {
        private sealed class State
        {
            internal MainTabWindow window;
            internal Game game;
            internal bool background, shadow, closeX, closeButton, painted;
            internal Rect bounds;
            internal readonly ToolsPanelMotion motion = new ToolsPanelMotion();
        }
        private static readonly Dictionary<Window, State> states = new Dictionary<Window, State>();
        private static readonly HashSet<Window> failed = new HashSet<Window>();
        private static readonly MethodInfo Reposition = AccessTools.Method(typeof(MainTabWindow), "SetInitialSizeAndPosition");
        internal static Window Current { get; private set; }
        internal static bool Drawing => Current != null;
        internal static bool Enabled => ReframedMod.Active && ReframedMod.Settings.BottomMenus &&
            CurrentGame() && !TutorSystem.TutorialMode && BottomMenuPolicy.Supported(UI.screenWidth, UI.screenHeight);
        private static bool CurrentGame() => Verse.Current.ProgramState == ProgramState.Playing && Verse.Current.Game != null;
        private static double Now => Time.realtimeSinceStartup;
        internal static bool Eligible(Window window) => Enabled && window is MainTabWindow && !failed.Contains(window) &&
            (RosterMenus.Supports(window) || KnowledgeMenus.Supports(window) || WorldMenus.Supports(window));
        private static Vector2 Preferred(Window window) => RosterMenus.Supports(window) ? RosterMenus.PreferredSize(window) :
            KnowledgeMenus.Supports(window) ? KnowledgeMenus.PreferredSize(window) : WorldMenus.PreferredSize(window);
        internal static Rect Bounds(Window window)
        {
            Vector2 preferred = Preferred(window);
            float width = BottomMenuPolicy.Width(preferred.x, UI.screenWidth);
            float height = BottomMenuPolicy.Height(preferred.y, UI.screenHeight);
            return new Rect(BottomMenuPolicy.Gap, UI.screenHeight - BottomMenuPolicy.Dock - BottomMenuPolicy.Gap - height, width, height);
        }
        internal static void Prepare(WindowStack stack)
        {
            WorldMenus.Enabled = () => Enabled;
            RosterMenus.PopupScope = BottomMenuChildren.AnchorDropdown;
            KnowledgeMenus.PopupScope = BottomMenuChildren.AnchorDropdown;
            WorldMenusHistory.PopupScope = BottomMenuChildren.AnchorDropdown;
            KnowledgeMenus.PrepareCapture(stack);
            foreach (var pair in states.ToArray())
            {
                State state = pair.Value;
                state.motion.Tick(Now);
                if (state.game != Verse.Current.Game || !Enabled || (!state.motion.Open && state.motion.Shell == 0))
                {
                    Restore(state); Reset(pair.Key); states.Remove(pair.Key);
                    if (!Enabled && state.game == Verse.Current.Game && state.motion.Open && stack.Windows.Contains(state.window))
                        Reposition.Invoke(state.window, null);
                }
            }
            foreach (Window item in stack.Windows.ToArray())
            {
                if (!Eligible(item))
                {
                    if (states.TryGetValue(item, out State old) && old.motion.Open)
                    { Restore(old); Reset(item); states.Remove(item); Reposition.Invoke(item, null); }
                    continue;
                }
                var window = (MainTabWindow)item;
                if (!states.TryGetValue(window, out State state))
                {
                    state = new State { window = window, game = Verse.Current.Game };
                    states.Add(window, state);
                }
                if (!state.motion.Open)
                {
                    state.background = window.doWindowBackground; state.shadow = window.drawShadow;
                    state.closeX = window.doCloseX; state.closeButton = window.doCloseButton;
                    state.painted = false; state.motion.SetOpen(true, Now);
                }
                Rect bounds = Bounds(window);
                if (state.bounds != bounds) state.painted = false;
                state.bounds = bounds; window.windowRect = bounds;
                window.doWindowBackground = window.drawShadow = window.doCloseX = window.doCloseButton = false;
            }
        }
        private static void Restore(State state)
        {
            state.window.doWindowBackground = state.background; state.window.drawShadow = state.shadow;
            state.window.doCloseX = state.closeX; state.window.doCloseButton = state.closeButton;
        }
        internal static void Close(Window window)
        {
            failed.Remove(window);
            if (!states.TryGetValue(window, out State state)) return;
            state.motion.SetOpen(false, Now); state.painted = false; Restore(state);
            Reset(window);
        }
        private static void Reset(Window window)
        { RosterMenus.Reset(window); KnowledgeMenus.Reset(window); WorldMenus.Reset(window); }
        private static Rect Reveal(Rect rect, float value)
        {
            float progress = ToolsPanelMotion.Ease(value);
            return new Rect(rect.x, BottomMenuPolicy.RevealTop(rect.y, rect.height, progress), rect.width, rect.height * progress);
        }
        internal static bool Draw(MainTabWindow window, Rect rect)
        {
            if (!Eligible(window) || !states.TryGetValue(window, out State state)) return false;
            bool input = BottomMenuPolicy.Interactive(state.motion.Open, state.motion.Settled, state.painted,
                GUI.enabled && Find.WindowStack.GetsInput(window));
            if (!input && Event.current.type != EventType.Repaint) return true;
            Rect visible = Reveal(rect, state.motion.Shell);
            if (visible.width <= 0 || visible.height <= 0) return true;
            using (new Theme.GuiScope(true))
            {
                Window previous = Current; Current = window;
                Vector2 mouse = Event.current.mousePosition;
                GUI.enabled &= input;
                GUI.color = GUI.contentColor = Color.white;
                if (!input) Event.current.mousePosition = new Vector2(-100000, -100000);
                GUI.BeginGroup(visible);
                GUI.BeginGroup(new Rect(-visible.x, -visible.y, rect.width, rect.height));
                try
                {
                    Theme.Panel(rect);
                    if (Header(window, rect)) return true;
                    Rect body = new Rect(BottomMenuPolicy.Padding, BottomMenuPolicy.Header,
                        rect.width - BottomMenuPolicy.Padding * 2, rect.height - BottomMenuPolicy.Header - BottomMenuPolicy.Padding);
                    if (RosterMenus.Supports(window)) RosterMenus.Draw(window, body);
                    else if (KnowledgeMenus.Supports(window)) KnowledgeMenus.Draw(window, body);
                    else WorldMenus.Draw(window, body);
                    if (Event.current.type == EventType.Repaint && state.motion.Settled) state.painted = true;
                }
                catch (Exception error)
                {
                    // Never leave a broken replacement swallowing the native
                    // menu indefinitely, or replay the failing action this event.
                    failed.Add(window); state.painted = false;
                    Log.Error("[Reframed UI] Retaining native " + window.GetType().Name + " after replacement error: " + error);
                }
                finally
                { GUI.EndGroup(); GUI.EndGroup(); Event.current.mousePosition = mouse; Current = previous; }
            }
            return true;
        }
        private static bool Header(MainTabWindow window, Rect rect)
        {
            if (window.def != null) DockIcons.Draw(window.def, new Rect(14, 13, 16, 16), Theme.Muted);
            PawnInspectorView.Label(new Rect(38, 7, Math.Max(1, rect.width - 88), 28), window.def?.LabelCap ?? "", Theme.Ink, true);
            Rect close = new Rect(rect.xMax - 36, 8, 26, 26);
            Theme.ButtonFace(close, GUI.enabled && Mouse.IsOver(close) ? Theme.Hover : Theme.Surface, !GUI.enabled);
            GUI.color = Theme.Muted; GUI.DrawTexture(close.ContractedBy(7), ToolsIcons.Get("clear")); GUI.color = Color.white;
            if (GUI.enabled && Widgets.ButtonInvisible(close))
            { Find.MainTabsRoot.ToggleTab(window.def); return true; }
            return false;
        }
        internal static void DrawClosing()
        {
            if (Event.current.type != EventType.Repaint || !Enabled || Find.MainTabsRoot.OpenTab != null || Find.WindowStack.AnyWindowAbsorbingAllInput) return;
            using (new Theme.GuiScope(true))
            {
                GUI.enabled = false; GUI.color = Color.white;
                foreach (State state in states.Values)
                    if (!state.motion.Open && state.motion.Shell > 0 && state.game == Verse.Current.Game)
                        Theme.Panel(Reveal(state.bounds, state.motion.Shell));
            }
        }
        internal static readonly Type[] Types = {
            typeof(MainTabWindow_Schedule), typeof(MainTabWindow_Assign), typeof(MainTabWindow_Animals), typeof(MainTabWindow_Wildlife),
            typeof(MainTabWindow_Research), typeof(MainTabWindow_Quests), typeof(MainTabWindow_Factions), typeof(MainTabWindow_History)
        };
        private static MethodInfo Declared(MethodInfo method) => method == null ? null : AccessTools.DeclaredMethod(method.DeclaringType,
            method.Name, method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
        internal static IEnumerable<MethodBase> ContentsMethods => Types.Select(type => Declared(AccessTools.Method(type, "DoWindowContents", new[] { typeof(Rect) }))).Distinct();
        internal static IEnumerable<MethodBase> MarginMethods => Types.Select(type => Declared(AccessTools.PropertyGetter(type, "Margin"))).Distinct();
    }
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.WindowStackOnGUI))]
    internal static class BottomMenuPreparePatch
    {
        private static void Prefix(WindowStack __instance) => BottomMenuShell.Prepare(__instance);
        private static void Postfix() => BottomMenuShell.DrawClosing();
    }
    [HarmonyPatch]
    internal static class BottomMenuContentsPatch
    {
        private static IEnumerable<MethodBase> TargetMethods() => BottomMenuShell.ContentsMethods;
        private static bool Prefix(MainTabWindow __instance, Rect __0) => !BottomMenuShell.Draw(__instance, __0);
    }
    [HarmonyPatch]
    internal static class BottomMenuMarginPatch
    {
        private static IEnumerable<MethodBase> TargetMethods() => BottomMenuShell.MarginMethods;
        private static void Postfix(Window __instance, ref float __result) { if (BottomMenuShell.Eligible(__instance)) __result = 0; }
    }
    [HarmonyPatch(typeof(Window), nameof(Window.PostClose))]
    internal static class BottomMenuClosePatch
    { private static void Prefix(Window __instance) => BottomMenuShell.Close(__instance); }
}
