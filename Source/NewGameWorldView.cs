using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ReframedUI
{
    // Source-absent extension of the storyteller split shell. Own presentation
    // only; the native page retains initialization, parameters and generation.
    internal sealed class NewGameWorldView
    {
        private static readonly AccessTools.FieldRef<Page_CreateWorldParams, string> Seed = AccessTools.FieldRefAccess<Page_CreateWorldParams, string>("seedString");
        private static readonly AccessTools.FieldRef<Page_CreateWorldParams, float> Coverage = AccessTools.FieldRefAccess<Page_CreateWorldParams, float>("planetCoverage");
        private static readonly AccessTools.FieldRef<Page_CreateWorldParams, OverallRainfall> Rainfall = AccessTools.FieldRefAccess<Page_CreateWorldParams, OverallRainfall>("rainfall");
        private static readonly AccessTools.FieldRef<Page_CreateWorldParams, OverallTemperature> Temperature = AccessTools.FieldRefAccess<Page_CreateWorldParams, OverallTemperature>("temperature");
        private static readonly AccessTools.FieldRef<Page_CreateWorldParams, OverallPopulation> Population = AccessTools.FieldRefAccess<Page_CreateWorldParams, OverallPopulation>("population");
        private static readonly AccessTools.FieldRef<Page_CreateWorldParams, LandmarkDensity> Landmarks = AccessTools.FieldRefAccess<Page_CreateWorldParams, LandmarkDensity>("landmarkDensity");
        private static readonly AccessTools.FieldRef<Page_CreateWorldParams, List<FactionDef>> Factions = AccessTools.FieldRefAccess<Page_CreateWorldParams, List<FactionDef>>("factions");
        private static readonly MethodInfo ResetFactions = AccessTools.Method(typeof(Page_CreateWorldParams), "ResetFactionCounts");
        private readonly NewGameWorldFactions factions = new NewGameWorldFactions();
        private Vector2 settingsScroll;
        private GUIStyle title, body, small, smallRight, section, button, input, slider, sliderThumb, scrollTrack, scrollThumb;
        private float scale, lastScale, opacity;
        private float lastSliderSound = -1;
        private Rect origin;

        internal void Draw(Rect rect, Page_CreateWorldParams page, float alpha)
        {
            origin = rect; scale = rect.width / 582f; opacity = alpha;
            Styles();
            Label(At(17, 16, 239, 32), page.PageTitle, title, Color.white);
            Label(At(17, 50, 239, 44), "Shape the world your colony will call home.", body, Theme.Muted);
            DrawSettings(At(17, 102, 239, 470), page);
            factions.Draw(At(289, 17, 276, 555), Factions(page), scale, opacity,
                () => ResetFactions.Invoke(page, null));
        }

        private void DrawSettings(Rect viewport, Page_CreateWorldParams page)
        {
            int rows = 3 + (ModsConfig.BiotechActive ? 1 : 0) + (ModsConfig.OdysseyActive ? 1 : 0);
            float contentHeight = (176 + rows * 60 + 44) * scale;
            bool overflow = contentHeight > viewport.height;
            float width = viewport.width - (overflow ? 10 * scale : 0);
            settingsScroll.x = 0;
            settingsScroll.y = Mathf.Clamp(settingsScroll.y, 0, Mathf.Max(0, contentHeight - viewport.height));
            settingsScroll = GUI.BeginScrollView(viewport, settingsScroll, new Rect(0, 0, viewport.width, Mathf.Max(viewport.height, contentHeight)), false, false, GUIStyle.none, GUIStyle.none);
            try
            {
                Section(new Rect(0, 0, width, 16 * scale), "WorldSeed".Translate());
                Rect field = new Rect(0, 22 * scale, width, 32 * scale);
                GUI.color = new Color(1, 1, 1, opacity);
                Theme.ButtonFace(field, Theme.Rgb(0x0B1116), false, 4 * scale);
                GUI.color = new Color(1, 1, 1, opacity);
                GUI.SetNextControlName("ReframedWorldSeed");
                Seed(page) = GUI.TextField(field.ContractedBy(8 * scale, 0), Seed(page) ?? string.Empty, input);
                GUI.color = Color.white;
                if (Action(new Rect(0, 62 * scale, width, 30 * scale), "RandomizeSeed".Translate()))
                {
                    Seed(page) = GenText.RandomSeedString();
                    GUI.FocusControl(null);
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                }
                Section(new Rect(0, 108 * scale, width, 16 * scale), "PlanetCoverage".Translate());
                Rect coverageRow = new Rect(0, 130 * scale, width, 30 * scale);
                if (GUI.enabled) TooltipHandler.TipRegionByKey(new Rect(0, 108 * scale, width, 52 * scale), "PlanetCoverageTip");
                float[] choices = Prefs.DevMode ? new[] { .3f, .5f, 1f, .05f } : new[] { .3f, .5f, 1f };
                float gap = 6 * scale, itemWidth = (width - gap * (choices.Length - 1)) / choices.Length;
                for (int i = 0; i < choices.Length; i++)
                {
                    float value = choices[i];
                    Rect choice = new Rect(i * (itemWidth + gap), coverageRow.y, itemWidth, coverageRow.height);
                    if (Action(choice, value.ToStringPercent(), Mathf.Approximately(value, Coverage(page)), false))
                    {
                        if (!Mathf.Approximately(Coverage(page), value))
                        {
                            Coverage(page) = value;
                            if (value == 1f) Messages.Message("MessageMaxPlanetCoveragePerformanceWarning".Translate(), MessageTypeDefOf.CautionInput, historical: false);
                        }
                        GUI.FocusControl(null);
                    }
                    if (value == .05f && GUI.enabled) TooltipHandler.TipRegion(choice, "5% (dev)");
                }
                float y = 176 * scale;
                Rainfall(page) = (OverallRainfall)EnumSlider(ref y, width, "PlanetRainfall", (int)Rainfall(page), OverallRainfallUtility.EnumValuesCount);
                Temperature(page) = (OverallTemperature)EnumSlider(ref y, width, "PlanetTemperature", (int)Temperature(page), OverallTemperatureUtility.EnumValuesCount);
                Population(page) = (OverallPopulation)EnumSlider(ref y, width, "PlanetPopulation", (int)Population(page), OverallPopulationUtility.EnumValuesCount);
                if (ModsConfig.OdysseyActive)
                    Landmarks(page) = (LandmarkDensity)EnumSlider(ref y, width, "PlanetLandmarkDensity", (int)Landmarks(page), LandmarkDensityUtility.EnumValuesCount);
                if (ModsConfig.BiotechActive)
                {
                    page.pollution = ValueSlider(y, width, "PlanetPollution".Translate(), page.pollution, 0, 1, .05f,
                        page.pollution.ToStringPercent(), "0%", "100%");
                    y += 60 * scale;
                }
                float advancedY = Mathf.Max(y + 8 * scale, viewport.height - 32 * scale);
                if (!TutorSystem.TutorialMode && Action(new Rect(0, advancedY, width, 32 * scale), "AdvancedSettings".Translate() + "…"))
                {
                    GUI.FocusControl(null);
                    Find.WindowStack.Add(new Dialog_AdvancedGameConfig());
                }
            }
            finally { GUI.EndScrollView(GUI.enabled); }
            if (overflow)
            {
                scrollThumb.fixedHeight = Mathf.Max(24 * scale, viewport.height * viewport.height / contentHeight);
                Color scrollColor = Theme.Rgb(0x344752); scrollColor.a = opacity;
                GUI.color = scrollColor;
                settingsScroll.y = GUI.VerticalSlider(new Rect(viewport.xMax - 5 * scale, viewport.y, 5 * scale, viewport.height), settingsScroll.y, 0, contentHeight - viewport.height, scrollTrack, scrollThumb);
                GUI.color = Color.white;
            }
        }

        private int EnumSlider(ref float y, float width, string key, int current, int count)
        {
            int middle = (count - 1) / 2;
            string level = current == middle ? (key + "_Normal").Translate().ToString()
                : current < middle ? (key + "_Low").Translate().ToString() : (key + "_High").Translate().ToString();
            int result = Mathf.RoundToInt(ValueSlider(y, width, key.Translate(), current, 0, count - 1, 1, level,
                (key + "_Low").Translate(), (key + "_High").Translate()));
            y += 60 * scale;
            return result;
        }

        private float ValueSlider(float y, float width, string text, float current, float min, float max, float step, string value, string low, string high)
        {
            Rect label = new Rect(0, y, width * .68f, 20 * scale);
            Label(label, Fit(text, body, label.width), body, Color.white);
            Label(new Rect(width * .68f, y, width * .32f, 20 * scale), value, smallRight, Theme.Muted);
            if (GUI.enabled) TooltipHandler.TipRegion(new Rect(0, y, width, 20 * scale), text + ": " + value);
            Rect track = new Rect(0, y + 24 * scale, width, 12 * scale);
            GUI.color = new Color(1, 1, 1, opacity);
            float raw = GUI.HorizontalSlider(track, current, min, max, slider, sliderThumb);
            float result = Mathf.Clamp(Mathf.Round(raw / step) * step, min, max);
            if (GUI.enabled && !Mathf.Approximately(current, result) && Time.realtimeSinceStartup - lastSliderSound > .075f)
            {
                SoundDefOf.DragSlider.PlayOneShotOnCamera();
                lastSliderSound = Time.realtimeSinceStartup;
            }
            // Native slider input/capture with the shared thin-track visual vocabulary.
            GUI.color = new Color(1, 1, 1, opacity);
            Theme.RoundRect(new Rect(track.x, track.center.y - 2 * scale, width, 4 * scale), Theme.Rgb(0x202D37), 2 * scale);
            float fraction = Mathf.InverseLerp(min, max, result);
            Theme.RoundRect(new Rect(track.x, track.center.y - 2 * scale, Mathf.Max(0, (width - 10 * scale) * fraction + 5 * scale), 4 * scale), Theme.Rgb(0x647985), 2 * scale);
            Theme.RoundRect(new Rect(track.x + (width - 10 * scale) * fraction, track.center.y - 5 * scale, 10 * scale, 10 * scale), Theme.Rgb(0xBCCBD1), 3 * scale);
            Label(new Rect(0, y + 38 * scale, width / 2, 16 * scale), low, small, Theme.Muted);
            Label(new Rect(width / 2, y + 38 * scale, width / 2, 16 * scale), high, smallRight, Theme.Muted);
            return result;
        }

        private bool Action(Rect rect, string text, bool selected = false, bool tooltip = true)
        {
            bool hover = GUI.enabled && Mouse.IsOver(rect);
            GUI.color = new Color(1, 1, 1, opacity);
            Theme.ButtonFace(rect, selected ? Theme.Selected : hover ? Theme.Hover : Theme.Surface, !GUI.enabled, 3 * scale);
            Label(rect.ContractedBy(5 * scale, 0), Fit(text, button, rect.width - 10 * scale), button, Color.white);
            if (GUI.enabled && tooltip) TooltipHandler.TipRegion(rect, text);
            return GUI.enabled && Widgets.ButtonInvisible(rect);
        }
        private void Section(Rect rect, string text)
        {
            float x = rect.x;
            foreach (char letter in text.ToUpperInvariant())
            {
                string glyph = letter.ToString();
                float width = section.CalcSize(new GUIContent(glyph)).x;
                Label(new Rect(x, rect.y, width + scale, rect.height), glyph, section, Color.white);
                x += width + .55f * scale;
            }
        }
        private void Label(Rect rect, string text, GUIStyle style, Color ink)
        {
            using (new Theme.GuiScope(true)) { ink.a *= opacity; GUI.enabled = true; GUI.color = ink; GUI.Label(rect, text, style); }
        }
        private static string Fit(string text, GUIStyle style, float width)
        {
            if (style.CalcSize(new GUIContent(text)).x <= width) return text;
            int length = text.Length;
            while (length > 0 && style.CalcSize(new GUIContent(text.Substring(0, length) + "…")).x > width) length--;
            return text.Substring(0, length) + "…";
        }
        private Rect At(float x, float y, float w, float h) => new Rect(origin.x + x * scale, origin.y + y * scale, w * scale, h * scale);
        private void Styles()
        {
            Text.Font = GameFont.Small;
            if (body != null && lastScale == scale && body.font == Text.CurFontStyle.font) return;
            lastScale = scale;
            GUIStyle Font(int size, TextAnchor anchor, bool wrap = false)
            {
                var result = new GUIStyle(Text.CurFontStyle) { fontSize = Mathf.RoundToInt(size * scale), alignment = anchor, wordWrap = wrap, padding = new RectOffset(), margin = new RectOffset() };
                result.normal.textColor = Color.white; return result;
            }
            title = Font(16, TextAnchor.MiddleLeft); body = Font(14, TextAnchor.MiddleLeft, true);
            small = Font(12, TextAnchor.MiddleLeft); smallRight = Font(12, TextAnchor.MiddleRight);
            section = Font(9, TextAnchor.MiddleLeft); section.fontStyle = FontStyle.Bold;
            button = Font(14, TextAnchor.MiddleCenter);
            input = Font(14, TextAnchor.MiddleLeft); input.normal.background = input.focused.background = input.hover.background = input.active.background = null;
            input.richText = false; input.wordWrap = false; input.clipping = TextClipping.Clip;
            input.focused.textColor = input.hover.textColor = input.active.textColor = Color.white;
            slider = new GUIStyle { fixedHeight = 12 * scale };
            sliderThumb = new GUIStyle { fixedWidth = 10 * scale, fixedHeight = 12 * scale };
            scrollTrack = new GUIStyle(); scrollThumb = new GUIStyle { normal = { background = BaseContent.WhiteTex } };
        }
    }
}
