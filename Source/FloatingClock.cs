using System;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    [HarmonyPatch(typeof(GlobalControls), nameof(GlobalControls.GlobalControlsOnGUI))]
    internal static class FloatingClock
    {
        private const float ClockWidth = 154f;
        private const float ClockHeight = 77f;
        private static readonly Func<string> Temperature = (Func<string>)Delegate.CreateDelegate(
            typeof(Func<string>), AccessTools.Method(typeof(GlobalControls), "TemperatureString"));
        private static readonly Action<Rect, TimedDetectionRaids> Countdown =
            (Action<Rect, TimedDetectionRaids>)Delegate.CreateDelegate(typeof(Action<Rect, TimedDetectionRaids>),
            AccessTools.Method(typeof(GlobalControls), "DoCountdownTimer"));

        private static bool Eligible => ReframedMod.Active && ReframedMod.Settings.FloatingClock
            && Find.CurrentMap != null && !WorldRendererUtility.WorldSelected
            && !TutorSystem.TutorialMode;

        internal static bool Active => Eligible && HudLayout.TryClockBounds(ClockWidth, ClockHeight, out _);

        internal static Rect Bounds => Eligible && HudLayout.TryClockBounds(ClockWidth, ClockHeight, out Rect bounds) ? bounds : default;

        private static bool Prefix(WidgetRow ___rowVisibility)
        {
            if (!Eligible || !HudLayout.TryClockBounds(ClockWidth, ClockHeight, out Rect bounds)) return true;
            if (Event.current.type == EventType.Layout) return false;
            // HUD is drawn before modal windows. Disabled GUI alone does not block native speed key handling.
            bool blocked = Find.WindowStack.AnyWindowAbsorbingAllInput;
            if (blocked && Event.current.type != EventType.Repaint) return false;
            using (new Theme.GuiScope(true))
            {
                GUI.color = Color.white;
                GUI.enabled = GUI.enabled && !blocked;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                DrawClock(bounds);
                DrawAncillary(___rowVisibility);
            }
            return false;
        }

        private static void DrawAncillary(WidgetRow rowVisibility)
        {
            // Retain every ancillary GlobalControls branch and its native order. Date/weather/temperature
            // information is in the compact card; no second time-control call or duplicate readout block.
            float x = UI.screenWidth - 200f;
            float y = UI.screenHeight - 39f;
            GlobalControlsUtility.DoPlaySettings(rowVisibility, false, ref y);
            y -= 8f;
            float conditionHeight = Find.CurrentMap.gameConditionManager.TotalHeightAt(154f);
            Find.CurrentMap.gameConditionManager.DoConditionsUI(new Rect(UI.screenWidth - 154f,
                y - conditionHeight, 154f, conditionHeight));
            y -= conditionHeight;
            if (DebugViewSettings.showMemoryInfo) GlobalControlsUtility.DrawMemoryInfo(x, 200f, ref y);
            if (DebugViewSettings.showTpsCounter) GlobalControlsUtility.DrawTpsCounter(x, 200f, ref y);
            if (DebugViewSettings.showFpsCounter) GlobalControlsUtility.DrawFpsCounter(x, 200f, ref y);
            if (Prefs.ShowRealtimeClock) GlobalControlsUtility.DoRealtimeClock(x, 200f, ref y);
            var raids = Find.CurrentMap.Parent?.GetComponent<TimedDetectionRaids>();
            if (raids != null && raids.NextRaidCountdownActiveAndVisible)
            {
                Text.Anchor = TextAnchor.MiddleRight;
                Countdown(new Rect(x, y - 26f, 193f, 26f), raids);
                Text.Anchor = TextAnchor.UpperLeft;
                y -= 26f;
            }
            // Exactly once: the native letter stack updates alert anchors and bundling here.
            Find.LetterStack.LettersOnGUI(y - 10f);
        }

        private static void DrawClock(Rect bounds)
        {
            Map map = Find.CurrentMap;
            Rect card = new Rect(bounds.x, bounds.y, bounds.width, 41f);
            // The visible tray has its own padding around the four original
            // 32x24 native input regions. It stays narrower than the readout card.
            Rect speedTray = new Rect(bounds.xMax - 136f, bounds.y + 45f, 136f, 32f);
            Rect speedStrip = speedTray.ContractedBy(4f);
            Theme.Panel(card);
            Theme.Panel(speedTray);
            Vector2 longLat = Find.WorldGrid.LongLatOf(map.Tile);
            int hour = GenDate.HourInteger(Find.TickManager.TicksAbs, longLat.x);
            string time = Prefs.TwelveHourClockMode
                ? ((hour + 11) % 12 + 1) + " " + (hour >= 12 ? "PM".Translate() : "AM".Translate())
                : hour.ToString("00") + ":00";
            Rect timeRect = new Rect(card.x + 6f, card.y + 3f, 64f, 18f);
            ClockPresentation.Label(timeRect, time, Theme.Ink, 13, bold: true);
            // The short numeric readout is explicitly outdoor/map temperature. Full native cursor/room
            // context remains available in its tooltip, including unroofed-room and temperature preferences.
            string outdoor = ClockPresentation.Temperature(map.mapTemperature.OutdoorTemp);
            Rect temperatureRect = new Rect(card.x + 74f, card.y + 4f, 54f, 17f);
            ClockPresentation.Label(temperatureRect, outdoor, Theme.Ink, 11, anchor: TextAnchor.MiddleRight);
            TooltipHandler.TipRegion(temperatureRect, "Outdoors".Translate() + ": " + outdoor + "\n" + Temperature());

            Rect weatherRect = new Rect(card.xMax - 21f, card.y + 5f, 15f, 15f);
            if (!map.IsPocketMap)
            {
                // Native WeatherDef has no UI icon field. The inferred glyph family
                // follows its actual perceived precipitation/events/overlays; the
                // full native label/description remains the authority in the tip.
                var weather = map.weatherManager.CurWeatherPerceived;
                ClockPresentation.Weather(weatherRect, weather, hour);
                if (weather != null) TooltipHandler.TipRegion(weatherRect,
                    weather.LabelCap + (weather.description.NullOrEmpty() ? "" : "\n\n" + weather.description));
            }
            Rect dateRect = new Rect(card.x + 6f, card.y + 23f, card.width - 12f, 14f);
            string date = GenDate.DateReadoutStringAt(Find.TickManager.TicksAbs, longLat);
            ClockPresentation.Label(dateRect, date, Theme.Muted, 11);
            if (Mouse.IsOver(dateRect)) TooltipHandler.TipRegion(dateRect, DateTip(date, longLat));
            TooltipHandler.TipRegion(timeRect, time + "\n" + GenDate.Season(Find.TickManager.TicksAbs, longLat).LabelCap());

            // Keep the full native control, not a reimplementation of its superficially simple buttons.
            // This owns mouse/key restrictions, forced-state lines, sounds, knowledge and dev shortcuts.
            ClockPresentation.TimeControls(speedStrip);
            GenUI.AbsorbClicksInRect(card);
            GenUI.AbsorbClicksInRect(speedTray);
        }

        private static string DateTip(string date, Vector2 longLat)
        {
            var seasons = new StringBuilder();
            for (int i = 0; i < 4; i++)
            {
                Quadrum quadrum = (Quadrum)i;
                seasons.AppendLine(quadrum.Label() + " - " + quadrum.GetSeason(longLat.y).LabelCap());
            }
            Season season = GenDate.Season(Find.TickManager.TicksAbs, longLat);
            return date + "\n\n" + "DateReadoutTip".Translate(GenDate.DaysPassed, 15, season.LabelCap(),
                15, GenDate.Quadrum(GenTicks.TicksAbs, longLat.x).Label(), seasons.ToString());
        }

    }

    [HarmonyPatch(typeof(LearningReadout), nameof(LearningReadout.LearningReadoutOnGUI))]
    internal static class LearningPanelBounds
    {
        internal static float Bottom;
        private static bool Prefix(LearningReadout __instance, float ___contentHeight)
        {
            // Scenario configuration creates a Game before leaving the entry scene.
            // Its empty adaptive-learning HUD does not belong above the setup panels.
            if (EntryScene.Active && ReframedMod.Settings.StorytellerSetup && !TutorSystem.TutorialMode)
            { Bottom = 0; return false; }
            bool visible = !TutorSystem.TutorialMode && TutorSystem.AdaptiveTrainingEnabled
                && (Find.PlaySettings.showLearningHelper || __instance.ActiveConceptsCount != 0)
                && !Find.WindowStack.IsOpen<Screen_Credits>();
            Bottom = visible ? 16f + Mathf.Min(___contentHeight + 14f, UI.screenHeight / 2f) : 0f;
            return true;
        }
    }
}
