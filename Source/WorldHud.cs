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
    // Adapt only the two native time/date calls. World navigation, layers,
    // targeters, route planning, compass and native ancillary ordering stay intact.
    internal static class WorldHud
    {
        internal static bool ClockSeamReady;
        private static bool LetterContext => ReframedMod.Active && ReframedMod.Settings.AlertCards &&
            Current.ProgramState == ProgramState.Playing && WorldRendererUtility.WorldSelected && !TutorSystem.TutorialMode;
        internal static bool Clock => ClockSeamReady && ReframedMod.Active && ReframedMod.Settings.FloatingClock &&
            Current.ProgramState == ProgramState.Playing && WorldRendererUtility.WorldSelected && !TutorSystem.TutorialMode &&
            HudLayout.TryClockBounds(154, 77, out _);
        internal static void TimeControls(float left, float width, ref float bottom)
        {
            if (!Clock) { GlobalControlsUtility.DoTimespeedControls(left, width, ref bottom); return; }
            if (Find.WindowStack.AnyWindowAbsorbingAllInput && Event.current.type != EventType.Repaint) return;
            HudLayout.TryClockBounds(154, 77, out Rect bounds);
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                GUI.enabled &= !Find.WindowStack.AnyWindowAbsorbingAllInput;
                Rect card = new Rect(bounds.x, bounds.y, bounds.width, 41);
                Rect tray = new Rect(bounds.xMax - 136, bounds.y + 45, 136, 32);
                Theme.Panel(card); Theme.Panel(tray);
                PlanetTile tile = Find.WorldSelector.SelectedTile;
                if (!tile.Valid && Find.WorldSelector.NumSelectedObjects > 0) tile = Find.WorldSelector.FirstSelectedObject.Tile;
                if (!tile.Valid && Find.CurrentMap != null) tile = Find.CurrentMap.Tile;
                if (tile.Valid)
                {
                    Vector2 location = Find.WorldGrid.LongLatOf(tile);
                    int hour = GenDate.HourInteger(Find.TickManager.TicksAbs, location.x);
                    string time = Prefs.TwelveHourClockMode ? ((hour + 11) % 12 + 1) + " " + (hour >= 12 ? "PM".Translate() : "AM".Translate()) : hour.ToString("00") + ":00";
                    ClockPresentation.Label(new Rect(card.x + 6, card.y + 3, 80, 18), time, Theme.Ink, 13, bold: true);
                    ClockPresentation.Label(new Rect(card.x + 6, card.y + 23, card.width - 12, 14), GenDate.DateReadoutStringAt(Find.TickManager.TicksAbs, location), Theme.Muted, 11);
                    TooltipHandler.TipRegion(card, GenDate.DateReadoutStringAt(Find.TickManager.TicksAbs, location) + "\n" + GenDate.Season(Find.TickManager.TicksAbs, location).LabelCap());
                }
                else
                {
                    ClockPresentation.Label(new Rect(card.x + 6, card.y + 3, card.width - 12, 18), MainButtonDefOf.World.LabelCap, Theme.Ink, 13, bold: true);
                    ClockPresentation.Label(new Rect(card.x + 6, card.y + 23, card.width - 12, 14), "Local time unavailable", Theme.Muted, 10);
                }
                ClockPresentation.TimeControls(tray.ContractedBy(4));
                GenUI.AbsorbClicksInRect(card); GenUI.AbsorbClicksInRect(tray);
            }
        }
        internal static void Date(float left, float width, ref float bottom)
        { if (!Clock) GlobalControlsUtility.DoDate(left, width, ref bottom); }
        internal static bool TryLetters(float bottom, ref float top)
        {
            if (!LetterContext || UI.screenWidth < 900 || UI.screenHeight < 600) return false;
            float protectedTop = RightHud.ProtectedTop();
            float available = Math.Min(bottom, HudLayout.ToolsButtonBounds.y - 8) - protectedTop;
            var letters = Find.LetterStack.LettersListForReading;
            if (!NotificationCards.CanHandle(letters, RightHud.Width, available)) return false;
            float height = NotificationCards.Prepare(letters, RightHud.Width, available, Time.realtimeSinceStartup);
            Rect rect = new Rect(UI.screenWidth - RightHud.Width - 12, Math.Min(bottom, HudLayout.ToolsButtonBounds.y - 8) - height, RightHud.Width, height);
            bool input = GUI.enabled && !Find.WindowStack.AnyWindowAbsorbingAllInput && !ToolsPanel.OccludesMapPointer;
            if (input || Event.current.type == EventType.Repaint)
                using (new Theme.GuiScope(true))
                {
                    GUI.color = Color.white; GUI.enabled &= input;
                    NotificationCards.Draw(rect, input);
                    if (input && height > 0) GenUI.AbsorbClicksInRect(rect);
                }
            top = height > 0 ? rect.y : bottom; return true;
        }
    }
    [HarmonyPatch(typeof(WorldGlobalControls), nameof(WorldGlobalControls.WorldGlobalControlsOnGUI))]
    internal static class WorldHudClockPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var body = instructions.ToList();
            MethodInfo time = AccessTools.Method(typeof(GlobalControlsUtility), nameof(GlobalControlsUtility.DoTimespeedControls));
            MethodInfo date = AccessTools.Method(typeof(GlobalControlsUtility), nameof(GlobalControlsUtility.DoDate));
            WorldHud.ClockSeamReady = body.Count(code => code.Calls(time)) == 1 && body.Count(code => code.Calls(date)) == 1;
            if (!WorldHud.ClockSeamReady) { Log.Warning("[Reframed UI] World time/date seam changed; retaining native world clock."); return body; }
            foreach (var code in body)
            {
                if (code.Calls(time)) code.operand = AccessTools.Method(typeof(WorldHud), nameof(WorldHud.TimeControls));
                else if (code.Calls(date)) code.operand = AccessTools.Method(typeof(WorldHud), nameof(WorldHud.Date));
            }
            return body;
        }
    }
}
