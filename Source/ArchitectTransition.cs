using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Presentation follows the real native window. Closing never delays native
    // tab/designator cleanup; the outgoing shell is a noninteractive paint ghost.
    internal static class ArchitectTransition
    {
        private static readonly ToolsPanelMotion motion = new ToolsPanelMotion();
        private static MainTabWindow_Architect owner;
        private static Map map;
        private static int frame = -1;
        private static double now;
        private static double Now
        {
            get { if (frame != Time.frameCount) { frame = Time.frameCount; now = Time.realtimeSinceStartup; } return now; }
        }
        internal static bool Moving => owner != null && !motion.Settled;
        internal static float Progress => ToolsPanelMotion.Ease(motion.Shell);
        internal static Rect Shell
        {
            get
            {
                Rect target = ArchitectPanel.Bounds;
                float t = Progress;
                return new Rect(target.x, ArchitectLayout.RevealTop(target.yMax, target.height, t),
                    target.width * t, target.height * t);
            }
        }
        internal static void Open(MainTabWindow_Architect window)
        {
            if (owner != window || map != Find.CurrentMap) motion.Reset();
            owner = window; map = Find.CurrentMap;
            motion.SetOpen(true, Now);
        }
        internal static void Close(Window window)
        {
            if (owner != window) return;
            motion.SetOpen(false, Now);
        }
        internal static void Prepare()
        {
            if (owner == null) return;
            if (!ArchitectPanel.Eligible(owner) || map != Find.CurrentMap ||
                !motion.Open && ((Find.MainTabsRoot.OpenTab != null && Find.MainTabsRoot.OpenTab != MainButtonDefOf.Architect) ||
                    Find.WindowStack.AnyWindowAbsorbingAllInput))
            { owner = null; motion.Reset(); return; }
            motion.Tick(Now);
        }
        internal static void DrawOpeningShell()
        {
            if (motion.Open && Moving) DrawShell();
        }
        internal static void DrawClosingShell()
        {
            if (!motion.Open && motion.Shell > 0f && owner != null && Event.current.type == EventType.Repaint)
                DrawShell();
        }
        private static void DrawShell()
        {
            using (new Theme.GuiScope(true))
            {
                GUI.color = Color.white;
                Color fill = Theme.PanelFill; fill.a = System.Math.Min(fill.a, .84f);
                Theme.RoundRect(Shell, fill, 5f);
            }
        }
        internal static void DrawContents(MainTabWindow_Architect window, Rect rect)
        {
            if (!Moving) { ArchitectPanel.Draw(window, rect); return; }
            // Moving controls cannot dispatch input. Paint at final size inside
            // the growing clip; this avoids stretching text or thumbnail art.
            if (Event.current.type != EventType.Repaint) return;
            Rect visible = Shell, bounds = ArchitectPanel.Bounds;
            Rect clip = Rect.MinMaxRect(Mathf.Max(0f, visible.x - bounds.x), Mathf.Max(0f, visible.y - bounds.y),
                Mathf.Min(rect.width, visible.xMax - bounds.x), Mathf.Min(rect.height, visible.yMax - bounds.y));
            if (clip.width <= 0f || clip.height <= 0f) return;
            GUI.BeginGroup(clip);
            GUI.BeginGroup(new Rect(-clip.x, -clip.y, rect.width, rect.height));
            try { ArchitectPanel.Draw(window, rect); }
            finally { GUI.EndGroup(); GUI.EndGroup(); }
        }
        internal static Dictionary<string, object> Audit() => new Dictionary<string, object>
        {
            ["progress"] = motion.Shell, ["target"] = motion.Open ? 1f : 0f,
            ["shell"] = new[] { Shell.x, Shell.y, Shell.width, Shell.height },
            ["seconds"] = ToolsPanelMotion.ShellSeconds,
            ["origin"] = "panel-bottom-left",
            ["closing"] = owner != null && !motion.Open && motion.Shell > 0f
        };
        internal static Dictionary<string, object> AnimationAuditState() => Audit();
    }

    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.WindowStackOnGUI))]
    internal static class ArchitectTransitionPatch
    {
        private static void Prefix() => ArchitectTransition.Prepare();
        private static void Postfix() => ArchitectTransition.DrawClosingShell();
    }
}
