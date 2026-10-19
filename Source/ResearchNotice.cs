using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Owns the actual completion dialog, with its original pause, input deadline,
    // node and option callbacks. Discovery letters are a separate native channel.
    internal static class ResearchNotice
    {
        internal static Func<bool> Enabled = () => ReframedMod.Active && ReframedMod.Settings?.AlertCards == true;
        internal static Func<float> ProtectedTop = () => Math.Max(140, LearningPanelBounds.Bottom + 8);
        internal static bool SeamReady;
        private static readonly FieldInfo CurrentNode = AccessTools.DeclaredField(typeof(Dialog_NodeTree), "curNode");
        private static readonly MethodInfo Interactive = AccessTools.DeclaredPropertyGetter(typeof(Dialog_NodeTree), "InteractiveNow");
        private static readonly MethodInfo Activate = AccessTools.DeclaredMethod(typeof(DiaOption), "Activate", Type.EmptyTypes);
        private static readonly MethodInfo Reposition = AccessTools.Method(typeof(Window), "SetInitialSizeAndPosition");
        private static readonly ConditionalWeakTable<Dialog_NodeTree, State> owners = new ConditionalWeakTable<Dialog_NodeTree, State>();
        private static readonly List<Dialog_NodeTree> active = new List<Dialog_NodeTree>();
        private static readonly List<Ghost> ghosts = new List<Ghost>();
        private static GUIStyle label, title, time;
        private static Texture2D glow;
        private static int frame = -1;
        private static double now;
        private static double Now { get { if (frame != Time.frameCount) { frame = Time.frameCount; now = Time.realtimeSinceStartup; } return now; } }

        private sealed class State
        {
            internal ResearchProjectDef Project;
            internal DiaNode Node;
            internal DiaOption Okay, Research;
            internal Action ResearchAction;
            internal TaggedString Text;
            internal Game Game;
            internal readonly ToolsPanelMotion Motion = new ToolsPanelMotion();
            internal bool Owned, Failed, Background, Shadow, CloseX, CloseButton;
            internal Rect Bounds;
            internal double Created;
        }
        private sealed class Ghost
        {
            internal ToolsPanelMotion Motion;
            internal Rect Bounds;
            internal Game Game;
            internal int Width, Height;
        }
        internal static bool Ready => SeamReady && CurrentNode?.FieldType == typeof(DiaNode) &&
            Interactive?.ReturnType == typeof(bool) && Activate?.ReturnType == typeof(void) && Reposition != null;

        // Called solely at the exact native constructor site; all other constructors
        // and WindowStack.Add calls, including discovered-letter dialogs, are intact.
        internal static Dialog_NodeTree Create(DiaNode node, bool delay, bool radio, string nativeTitle, ResearchProjectDef project)
        {
            var window = new Dialog_NodeTree(node, delay, radio, nativeTitle);
            if (!Ready || Enabled?.Invoke() != true || project == null || !delay || radio || nativeTitle != null || node?.options?.Count != 2) return window;
            var state = new State { Project = project, Node = node, Okay = node.options[0], Research = node.options[1],
                ResearchAction = node.options[1]?.action, Text = node.text, Game = Current.Game, Created = Now };
            if (!Valid(window, state)) return window;
            state.Motion.SetOpen(true, Now);
            owners.Add(window, state); active.Add(window);
            return window;
        }
        private static bool Valid(Dialog_NodeTree window, State state)
        {
            if (state.Failed || state.Okay == null || state.Research == null || state.Game != Current.Game || window.GetType() != typeof(Dialog_NodeTree) ||
                window.screenFillColor != Color.clear ||
                !ReferenceEquals(CurrentNode.GetValue(window), state.Node) || state.Node.options == null || state.Node.options.Count != 2 ||
                !ReferenceEquals(state.Node.options[0], state.Okay) || !ReferenceEquals(state.Node.options[1], state.Research) ||
                !state.Node.text.Equals(state.Text) || !ReferenceEquals(state.Research.action, state.ResearchAction)) return false;
            return ValidOption(state.Okay, window) && ValidOption(state.Research, window) && state.Okay.action == null && state.Research.action != null;
        }
        private static bool ValidOption(DiaOption option, Dialog_NodeTree window) => option?.GetType() == typeof(DiaOption) &&
            ReferenceEquals(option.dialog, window) && option.resolveTree && !option.disabled && option.link == null && option.linkLateBind == null &&
            option.hyperlink.Equals(default(Dialog_InfoCard.Hyperlink));
        internal static bool Eligible(Window window) => Ready && Enabled?.Invoke() == true && Current.ProgramState == ProgramState.Playing &&
            ResearchNoticePolicy.Supported(UI.screenWidth, UI.screenHeight) && ResearchNoticePolicy.CanPlace(UI.screenHeight, ProtectedTop?.Invoke() ?? 140) && window is Dialog_NodeTree tree &&
            owners.TryGetValue(tree, out State state) && Valid(tree, state);
        internal static Vector2 Size => new Vector2(ResearchNoticePolicy.Width, ResearchNoticePolicy.Height);
        internal static void Prepare(Dialog_NodeTree window)
        {
            if (!owners.TryGetValue(window, out State state)) return;
            if (!Eligible(window))
            {
                if (state.Owned) { Restore(window, state); state.Failed = true; Reposition.Invoke(window, null); }
                return;
            }
            if (!state.Owned)
            {
                state.Background = window.doWindowBackground; state.Shadow = window.drawShadow;
                state.CloseX = window.doCloseX; state.CloseButton = window.doCloseButton; state.Owned = true;
                state.Motion.Reset(); state.Motion.SetOpen(true, Now);
            }
            window.doWindowBackground = window.drawShadow = window.doCloseX = window.doCloseButton = false;
            state.Bounds = new Rect(ResearchNoticePolicy.Left(UI.screenWidth), ResearchNoticePolicy.Top(ProtectedTop?.Invoke() ?? 140), Size.x, Size.y);
            window.windowRect = state.Bounds;
            state.Motion.Tick(Now);
        }
        private static void Restore(Dialog_NodeTree window, State state)
        {
            if (!state.Owned) return;
            window.doWindowBackground = state.Background; window.drawShadow = state.Shadow;
            window.doCloseX = state.CloseX; window.doCloseButton = state.CloseButton; state.Owned = false;
        }
        internal static void Tick()
        {
            // WindowStack teardown/load and another patch suppressing Add must
            // not retain old windows/games through our active owner index.
            foreach (Dialog_NodeTree window in active.ToArray())
                if (owners.TryGetValue(window, out State state) &&
                    (state.Game != Current.Game || (!Find.WindowStack.Windows.Contains(window) && (state.Owned || Now - state.Created > 1))))
                    Release(window);
            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                Ghost ghost = ghosts[i]; ghost.Motion.Tick(Now);
                if (ghost.Game != Current.Game || ghost.Width != UI.screenWidth || ghost.Height != UI.screenHeight ||
                    Enabled?.Invoke() != true || ghost.Motion.Shell <= 0) ghosts.RemoveAt(i);
            }
        }
        internal static void Closing(Dialog_NodeTree window)
        {
            if (!owners.TryGetValue(window, out State state) || !state.Owned) return;
            state.Motion.SetOpen(false, Now);
            ghosts.Add(new Ghost { Motion = state.Motion, Bounds = state.Bounds, Game = state.Game, Width = UI.screenWidth, Height = UI.screenHeight });
        }
        internal static void Release(Dialog_NodeTree window)
        {
            if (!owners.TryGetValue(window, out State state)) return;
            Restore(window, state); active.Remove(window); owners.Remove(window);
        }
        internal static void Reset()
        {
            foreach (Dialog_NodeTree window in active.ToArray())
            {
                if (owners.TryGetValue(window, out State state)) { Restore(window, state); state.Failed = true; }
                if (Find.WindowStack.Windows.Contains(window)) Reposition.Invoke(window, null);
                owners.Remove(window);
            }
            active.Clear(); ghosts.Clear();
        }
        private static Rect Reveal(Rect rect, float t) => new Rect(rect.x, rect.yMax - rect.height * t, rect.width * t, rect.height * t);
        internal static void Draw(Dialog_NodeTree window, Rect rect)
        {
            if (!owners.TryGetValue(window, out State state)) return;
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                float t = ToolsPanelMotion.Ease(state.Motion.Shell);
                bool settled = state.Motion.Settled;
                if (!settled && Event.current.type != EventType.Repaint) return;
                Rect visible = Reveal(new Rect(0, 0, rect.width, rect.height), t);
                Shell(visible);
                if (visible.width <= 0 || visible.height <= 0) return;
                GUI.enabled &= settled && (bool)Interactive.Invoke(window, null) && Find.WindowStack.Windows.Contains(window) && Find.WindowStack.GetsInput(window);
                GUI.BeginGroup(visible);
                try
                {
                    GUI.BeginGroup(new Rect(-visible.x, -visible.y, rect.width, rect.height));
                    try { Contents(window, state, rect); }
                    finally { GUI.EndGroup(); }
                }
                finally { GUI.EndGroup(); }
            }
        }
        private static void Styles()
        {
            if (label != null) return;
            Text.Font = GameFont.Small;
            label = new GUIStyle(Text.CurFontStyle) { fontSize = 11, wordWrap = false, alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(), margin = new RectOffset(), clipping = TextClipping.Clip };
            label.normal.textColor = Color.white;
            title = new GUIStyle(label) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
            time = new GUIStyle(label) { fontSize = 10, alignment = TextAnchor.MiddleRight };
        }
        private static void Contents(Dialog_NodeTree window, State state, Rect rect)
        {
            Styles();
            Color green = Theme.Rgb(0x25D977);
            GUI.color = green; GUI.DrawTexture(new Rect(12, 13, 17, 17), RightHudIcons.Get("check"));
            GUI.Label(new Rect(37, 7, rect.width - 140, 28), "Research finished", title);
            GUI.color = Theme.Muted; GUI.Label(new Rect(rect.width - 102, 7, 65, 28), ResearchNoticePolicy.Age(Math.Max(0, Now - state.Created)), time);
            Rect close = new Rect(rect.width - 29, 11, 18, 18);
            GUI.color = GUI.enabled && Mouse.IsOver(close) ? Theme.Ink : Theme.Muted;
            GUI.DrawTexture(close.ContractedBy(3), RightHudIcons.Get("close")); GUI.color = Color.white;
            if (GUI.enabled)
            {
                TooltipHandler.TipRegion(new Rect(8, 4, rect.width - 42, 34), state.Node.text.Resolve());
                TooltipHandler.TipRegion(close, "Dismiss");
            }
            if (GUI.enabled && Widgets.ButtonInvisible(close)) { Activate.Invoke(state.Okay, null); return; }
            Rect action = new Rect(8, 44, rect.width - 16, 30);
            Theme.RoundRect(action, GUI.enabled && Mouse.IsOver(action) ? Theme.Hover : Theme.Surface.ToTransparent(.75f), 5);
            GUI.color = GUI.enabled ? Theme.Ink : Theme.Muted;
            GUI.Label(action, "Open research screen", label); GUI.color = Color.white;
            if (GUI.enabled && Widgets.ButtonInvisible(action)) Activate.Invoke(state.Research, null);
        }
        private static void Shell(Rect rect)
        {
            Theme.RoundRect(rect, Theme.PanelFill, 8);
            if (Event.current.type != EventType.Repaint || rect.width <= 0 || rect.height <= 0) return;
            if (glow == null)
            {
                glow = new Texture2D(32, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                var colors = new Color[32];
                for (int i = 0; i < colors.Length; i++) colors[i] = Theme.Rgb(0x25D977).ToTransparent(.10f * (1 - i / 31f));
                glow.SetPixels(colors); glow.Apply();
            }
            GUI.DrawTexture(rect, glow, ScaleMode.StretchToFill, true, 0, Color.white, 0, 8);
        }
        internal static void DrawClosing()
        {
            if (Event.current.type != EventType.Repaint || Find.WindowStack.AnyWindowAbsorbingAllInput) return;
            using (new Theme.GuiScope(true))
            {
                GUI.color = Color.white; GUI.enabled = false;
                foreach (Ghost ghost in ghosts) Shell(Reveal(ghost.Bounds, ToolsPanelMotion.Ease(ghost.Motion.Shell)));
            }
        }
    }
}
