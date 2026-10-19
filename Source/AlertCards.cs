using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal static class AlertCards
    {
        internal static bool Active => ReframedMod.Active && ReframedMod.Settings.AlertCards &&
            !TutorSystem.TutorialMode && !WorldRendererUtility.WorldSelected && Find.CurrentMap != null;
    }

    // One owner for the stack. Native updates, archive, sounds, expiry and
    // actions are untouched; unsupported renderers retain the full native stack.
    internal static class RightHud
    {
        internal const float Width = 220;
        private static readonly FieldInfo ActiveAlerts = AccessTools.Field(typeof(AlertsReadout), "activeAlerts");
        private static readonly MethodInfo Recheck = AccessTools.Method(typeof(AlertsReadout), "CheckAddOrRemoveAlert", new[] { typeof(Alert), typeof(bool) });
        private static Game game;
        private static Map map;
        private static int screenWidth, screenHeight, frame = -1;
        private static double now;
        private static bool prepared;
        internal static bool DrawingInfo { get; private set; }
        private static Rect issues, notices, critical;
        private static double Now
        { get { if (frame != Time.frameCount) { frame = Time.frameCount; now = Time.realtimeSinceStartup; } return now; } }
        private static List<Alert> Alerts => Find.Alerts == null ? null : (List<Alert>)ActiveAlerts.GetValue(Find.Alerts);
        internal static bool CanOwn => AlertCards.Active && UI.screenWidth >= 700 && UI.screenHeight >= 480 &&
            ColonyIssues.CanHandle(Alerts) && NotificationCards.CanHandle(Find.LetterStack.LettersListForReading);
        internal static Rect Occupied => issues.height + notices.height + critical.height > 0 ?
            Rect.MinMaxRect(UI.screenWidth - Width - 12, notices.height > 0 ? notices.y : critical.height > 0 ? critical.y : issues.y,
                UI.screenWidth - 12, issues.height > 0 ? issues.yMax : critical.height > 0 ? critical.yMax : notices.yMax) : default;
        private static void Reset()
        { ColonyIssues.Reset(); CriticalIssueCards.Reset(); NotificationCards.Reset(); ColonyConditions.Reset(); prepared = false; issues = notices = critical = default; }
        internal static float ProtectedTop(float width = Width)
        {
            float top = Math.Max(12 + (Prefs.DevMode ? 30 : 0), LearningPanelBounds.Bottom + 8);
            float x = UI.screenWidth - width - 12;
            if (ColonistGroupsHud.TryGetOccupiedBounds(out Rect colonists) && colonists.xMax > x)
                top = Math.Max(top, colonists.yMax + 8);
            else if (Find.PlaySettings.showColonistBar)
                foreach (Vector2 pos in Find.ColonistBar.DrawLocs)
                    if (pos.x + Find.ColonistBar.Size.x > x)
                        top = Math.Max(top, pos.y + Find.ColonistBar.Size.y + 12);
            if (ReframedMod.Settings.FloatingClock && HudLayout.TryClockBounds(154, 77, out Rect clock))
                top = Math.Max(top, clock.yMax + 10);
            return top;
        }
        internal static bool Prepare(float baseY)
        {
            prepared = false;
            if (!CanOwn) { Reset(); return false; }
            if (game != Current.Game || map != Find.CurrentMap || screenWidth != UI.screenWidth || screenHeight != UI.screenHeight)
            {
                Reset(); game = Current.Game; map = Find.CurrentMap;
                screenWidth = UI.screenWidth; screenHeight = UI.screenHeight;
            }
            float top = ProtectedTop();
            float bottom = Math.Min(baseY, HudLayout.ToolsButtonBounds.y - 8);
            float available = bottom - top;
            if (!RightHudGeometry.Supported(available)) { Reset(); return false; }
            bool hasLetters = Find.LetterStack.LettersListForReading.Count > 0;
            List<Alert> combined = ColonyConditions.Append(Alerts);
            bool hasIssues = combined.Any(a => a.Active && !ColonyIssuePolicy.SeparateAlert((int)a.Priority));
            bool hasCritical = combined.Any(a => a.Active && ColonyIssuePolicy.SeparateAlert((int)a.Priority));
            if (!RightHudGeometry.Supported(available, hasCritical, hasIssues)) { Reset(); return false; }
            float criticalHeight = CriticalIssueCards.Prepare(combined, RightHudGeometry.CriticalLimit(available, hasIssues || hasLetters), Now);
            float remaining = available - criticalHeight - (criticalHeight > 0 ? 8 : 0);
            float issueHeight = ColonyIssues.Prepare(combined, Width, RightHudGeometry.IssueLimit(remaining, hasLetters), Now);
            float letterSpace = Math.Max(30, remaining - issueHeight - 8);
            if (!NotificationCards.CanHandle(Find.LetterStack.LettersListForReading, Width, letterSpace))
            { Reset(); return false; }
            float letterHeight = NotificationCards.Prepare(Find.LetterStack.LettersListForReading, Width, letterSpace, Now);
            float x = UI.screenWidth - Width - 12;
            issues = new Rect(x, bottom - issueHeight, Width, issueHeight);
            critical = new Rect(x, issues.y - (issueHeight > 0 && criticalHeight > 0 ? 8 : 0) - criticalHeight, Width, criticalHeight);
            notices = new Rect(x, critical.y - ((issueHeight > 0 || criticalHeight > 0) && letterHeight > 0 ? 8 : 0) - letterHeight, Width, letterHeight);
            prepared = true;
            return true;
        }
        internal static bool Draw(AlertsReadout readout, List<Alert> active, ref int hoveredIndex)
        {
            if (WorldRendererUtility.WorldSelected) return false;
            if (!prepared || !CanOwn) { Reset(); return false; }
            hoveredIndex = -1;
            bool input = GUI.enabled && !Find.WindowStack.AnyWindowAbsorbingAllInput && !ToolsPanel.OccludesMapPointer;
            if (!input && Event.current.type != EventType.Repaint) return true;
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white; GUI.enabled &= input;
                Vector2 mouse = Event.current.mousePosition;
                if (!input) Event.current.mousePosition = new Vector2(-100000, -100000);
                try
                {
                    NotificationCards.Draw(notices, input && !ColonyIssues.Moving && !CriticalIssueCards.Moving);
                    CriticalIssueCards.Draw(critical, input && !ColonyIssues.Moving, active);
                    ColonyIssues.Draw(issues, input && !CriticalIssueCards.Moving);
                }
                finally { Event.current.mousePosition = mouse; }
            }
            Alert hovered = input ? CriticalIssueCards.HoveredAlert ?? ColonyIssues.HoveredAlert : null;
            if (hovered != null && (active.Contains(hovered) || ColonyConditions.Contains(hovered)))
            {
                hoveredIndex = active.IndexOf(hovered);
                using (new Theme.GuiScope(true))
                {
                    // The native pane queues an absolute ImmediateWindow. Scope
                    // its rectangle adaptation; a GUI group cannot reposition it.
                    DrawingInfo = true;
                    try { hovered.DrawInfoPane(); }
                    finally { DrawingInfo = false; }
                }
                PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Alerts, KnowledgeAmount.FrameDisplayed);
                if (hoveredIndex >= 0) Recheck.Invoke(readout, new object[] { hovered, false });
            }
            if (input)
            {
                if (notices.height > 0) GenUI.AbsorbClicksInRect(notices);
                if (critical.height > 0) GenUI.AbsorbClicksInRect(critical);
                if (issues.height > 0) GenUI.AbsorbClicksInRect(issues);
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.ImmediateWindow))]
    internal static class ColonyIssueInfoPatch
    {
        private static void Prefix(int ID, ref Rect rect)
        {
            if (RightHud.DrawingInfo && ID == 138956)
                rect.x = Math.Max(0, UI.screenWidth - RightHud.Width - 12 - 8 - rect.width);
        }
    }
    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.LettersOnGUI))]
    internal static class RightHudLettersPatch
    {
        private static bool Prefix(float baseY, ref float ___lastTopYInt)
        {
            if (WorldRendererUtility.WorldSelected) return !WorldHud.TryLetters(baseY, ref ___lastTopYInt);
            if (!RightHud.Prepare(baseY)) return true;
            ___lastTopYInt = RightHud.Occupied.height > 0 ? RightHud.Occupied.y : baseY;
            return false;
        }
    }
    [HarmonyPatch(typeof(AlertsReadout), nameof(AlertsReadout.AlertsReadoutOnGUI))]
    internal static class AlertsPanel
    {
        private static bool Prefix(AlertsReadout __instance, List<Alert> ___activeAlerts, ref int ___mouseoverAlertIndex)
            => !RightHud.Draw(__instance, ___activeAlerts, ref ___mouseoverAlertIndex);
    }
}
