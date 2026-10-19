using System;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Native close and selection cleanup stay immediate. Only inert shells survive
    // dismissal; no pawn, table, scroll, tooltip or command callbacks run for ghosts.
    internal static class WorkPanelTransition
    {
        private static readonly WorkPanelMotion motion = new WorkPanelMotion();
        private static MainTabWindow_Work owner;
        private static Game game;
        private static Map map;
        private static Rect roster, editor;
        private static int width, height, frame = -1;
        private static double now;
        private static double Now
        {
            get { if (frame != Time.frameCount) { frame = Time.frameCount; now = Time.realtimeSinceStartup; } return now; }
        }
        internal static Color RosterFill => Theme.PanelFill;
        internal static Color EditorFill => Theme.Rgb(0x0B1218).ToTransparent(Theme.PanelFill.a);
        // Keep the outgoing card's space until it has contracted, including in
        // height-capped rosters. Its inert shell must never cover clickable rows.
        internal static float ReservedEditorHeight => motion.ReserveEditor ? editor.height + 6 : 0;
        private static Rect Reveal(Rect rect, float progress) => new Rect(rect.x,
            rect.yMax - rect.height * progress, rect.width * progress, rect.height * progress);
        private static bool Context => owner != null && game == Current.Game && map == Find.CurrentMap &&
            width == UI.screenWidth && height == UI.screenHeight && ReframedMod.Active && ReframedMod.Settings.WorkPanel &&
            !WorldRendererUtility.WorldSelected && !TutorSystem.TutorialMode && (!motion.Open || WorkPanel.Eligible(owner));

        internal static void Update(MainTabWindow_Work window, Rect bounds, float editorHeight, float cardWidth, bool editorSelected)
        {
            if (owner != window || !Context)
            {
                Reset(); owner = window; game = Current.Game; map = Find.CurrentMap;
                width = UI.screenWidth; height = UI.screenHeight;
            }
            motion.SetOpen(true, Now);
            roster = new Rect(bounds.x, bounds.y + editorHeight, bounds.width, bounds.height - editorHeight);
            if (editorSelected && editorHeight > 0)
                editor = new Rect(bounds.xMax - cardWidth - 8, bounds.y, cardWidth, editorHeight - 6);
            motion.SetEditor(editorSelected, Now);
        }
        internal static void Deselect() => motion.SetEditor(false, Now);
        internal static void Close(Window window)
        { if (owner == window) motion.SetOpen(false, Now); }
        internal static void Release(Window window)
        { if (owner == window && motion.Open) Reset(); }
        private static void Reset()
        { owner = null; game = null; map = null; roster = editor = default; motion.Reset(); }
        internal static void Tick()
        {
            if (owner == null) return;
            bool otherTab = Find.MainTabsRoot.OpenTab != null && Find.MainTabsRoot.OpenTab.tabWindowClass != typeof(MainTabWindow_Work);
            if (!Context || !motion.Open && (otherTab || Find.WindowStack.AnyWindowAbsorbingAllInput))
            { Reset(); return; }
            motion.Tick(Now);
            if (!motion.Open && motion.Panel == 0 && motion.Editor == 0) Reset();
        }
        internal static void DrawContents(MainTabWindow_Work window, Rect rect)
        {
            if (owner != window || motion.PanelInteractive) { WorkPanel.Draw(window, rect); return; }
            if (Event.current.type != EventType.Repaint || motion.Panel <= 0) return;
            PaintOnly(() => Clip(Reveal(rect, motion.Panel), rect.size, () => WorkPanel.Draw(window, rect)));
        }
        internal static void DrawEditor(Rect card, Action draw)
        {
            if (motion.EditorInteractive) { draw(); return; }
            if (!motion.EditorOpen || Event.current.type != EventType.Repaint || motion.Editor <= 0 || owner == null) return;
            PaintOnly(() => Clip(Reveal(card, motion.Editor), owner.windowRect.size, draw));
        }
        private static void PaintOnly(Action draw)
        {
            using (new Theme.GuiScope(true))
            {
                GUI.enabled = false;
                // Repaint only: native status cells and shared labels may register
                // tooltips despite GUI.enabled. Give this paint pass no hover,
                // then restore the event before the rest of the UI draws.
                Vector2 mouse = Event.current.mousePosition;
                Event.current.mousePosition = new Vector2(-100000, -100000);
                try { draw(); }
                finally { Event.current.mousePosition = mouse; }
            }
        }
        private static void Clip(Rect visible, Vector2 canvas, Action draw)
        {
            if (visible.width <= 0 || visible.height <= 0) return;
            GUI.BeginGroup(visible);
            try
            {
                GUI.BeginGroup(new Rect(-visible.x, -visible.y, canvas.x, canvas.y));
                try { draw(); }
                finally { GUI.EndGroup(); }
            }
            finally { GUI.EndGroup(); }
        }
        internal static void DrawClosing()
        {
            if (owner == null || Event.current.type != EventType.Repaint || !Context) return;
            // Post-stack paint must not put an outgoing editor above a newer
            // menu/modal. Keep its reservation and timing, only suppress paint.
            if (motion.Open && (!Find.WindowStack.Windows.Contains(owner) || !Find.WindowStack.GetsInput(owner))) return;
            using (new Theme.GuiScope(true))
            {
                GUI.color = Color.white; GUI.enabled = false;
                if (!motion.Open && motion.Panel > 0) Theme.RoundRect(Reveal(roster, motion.Panel), RosterFill, 6);
                if (!motion.EditorOpen && motion.Editor > 0) PaintEditorShell(Reveal(editor, motion.Editor));
            }
        }
        internal static void PaintEditorShell(Rect rect)
        {
            Theme.RoundRect(rect, EditorFill, 5);
            if (Event.current.type == EventType.Repaint)
                using (new Theme.GuiScope(true))
                {
                    Color rim = Theme.Border.ToTransparent(.5f) * GUI.color; GUI.color = Color.white;
                    GUI.DrawTexture(rect, BaseContent.WhiteTex, ScaleMode.StretchToFill, true, 0, rim, .6f, 5f);
                }
        }
    }
}
