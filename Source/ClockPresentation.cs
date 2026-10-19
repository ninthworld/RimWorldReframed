using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Presentation only. Native TimeControls remains the single behavior owner.
    internal static class ClockPresentation
    {
        private enum WeatherGlyph { Clear, Night, Cloud, Rain, Snow, Fog, Storm, Sand, Toxic }
        private static readonly Dictionary<WeatherGlyph, Texture2D> weatherTextures = new Dictionary<WeatherGlyph, Texture2D>();
        private static readonly Dictionary<int, GUIStyle> styles = new Dictionary<int, GUIStyle>();
        private static Font nativeFont;

        internal static void Label(Rect rect, string value, Color color, int size, bool bold = false, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            if (Event.current.type != EventType.Repaint) return;
            using (new Theme.GuiScope(true))
            {
                Text.Font = GameFont.Small;
                GUIStyle native = Text.CurFontStyle;
                if (nativeFont != native.font) { styles.Clear(); nativeFont = native.font; }
                int key = size * 100 + (bold ? 50 : 0) + (int)anchor;
                if (!styles.TryGetValue(key, out GUIStyle style))
                {
                    style = new GUIStyle(native) { fontSize = size, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                        alignment = anchor, wordWrap = false, richText = false, clipping = TextClipping.Clip,
                        padding = new RectOffset(), margin = new RectOffset() };
                    style.normal.textColor = Color.white;
                    styles.Add(key, style);
                }
                string text = value ?? "";
                if (style.CalcSize(new GUIContent(text)).x > rect.width)
                {
                    // Measure with the actual compact style, preserving text
                    // elements rather than clipping half of a translated glyph.
                    int[] starts = StringInfo.ParseCombiningCharacters(text);
                    int count = starts.Length;
                    while (count > 0 && style.CalcSize(new GUIContent(text.Substring(0, starts[--count]) + "…")).x > rect.width) { }
                    text = count > 0 ? text.Substring(0, starts[count]) + "…" : "…";
                }
                GUI.color = color;
                GUI.contentColor = Color.white;
                GUI.Label(rect, text, style);
            }
        }

        internal static string Temperature(float celsius)
        {
            // Keep the native conversion/precision/localization. Only enrich its
            // native C/F suffix with the degree mark; Kelvin does not use one.
            string native = celsius.ToStringTemperature("F0");
            if ((Prefs.TemperatureMode == TemperatureDisplayMode.Celsius && native.EndsWith("C", StringComparison.Ordinal) ||
                Prefs.TemperatureMode == TemperatureDisplayMode.Fahrenheit && native.EndsWith("F", StringComparison.Ordinal)) &&
                (native.Length < 2 || native[native.Length - 2] != '°'))
                return native.Insert(native.Length - 1, "°");
            return native;
        }

        internal static void TimeControls(Rect strip)
        {
            // Original 32x24 native rectangles, no matrix/input remapping and no
            // replacement GUI controls. Only paint is added, in every GUI event's
            // same control order. Repaint helpers allocate no GUI control IDs.
            for (int i = 0; i < 4; i++)
            {
                Rect native = new Rect(strip.x + i * 32f, strip.y, 32f, 24f);
                Rect face = native.ContractedBy(2f);
                bool hover = GUI.enabled && Mouse.IsOver(native);
                Color rim = Theme.Rgb(0x303A44);
                Theme.RoundRect(face, rim, 2.5f);
                Theme.RoundRect(face.ContractedBy(.65f), hover ? Theme.Hover : Theme.Surface, 2f);
                Color ink = GUI.enabled ? Theme.Ink : Theme.Muted;
                if (i == 0 && Event.current.type == EventType.Repaint)
                {
                    Widgets.DrawBoxSolid(new Rect(native.center.x - 4f, native.y + 7f, 3f, 10f), ink);
                    Widgets.DrawBoxSolid(new Rect(native.center.x + 1f, native.y + 7f, 3f, 10f), ink);
                }
                else if (i > 0) Label(face, i + "x", ink, 11, anchor: TextAnchor.MiddleCenter);
            }

            // Widgets.ButtonImage sets its own GUI.color, so tinting an entire
            // native call transparent would fail on hover. Substitute just the
            // four image slots, scoped to this call and restored even on failure.
            // Native highlight is a 10%-white overlay; native forced-speed lines
            // draw last and remain visible over the compact text. Ultrafast slot
            // is untouched, as are the complete native mouse/key/dev paths.
            Texture2D[] textures = TexButton.SpeedButtonTextures;
            Texture2D pause = textures[0], normal = textures[1], fast = textures[2], superfast = textures[3];
            try
            {
                textures[0] = textures[1] = textures[2] = textures[3] = BaseContent.ClearTex;
                RimWorld.TimeControls.DoTimeControlsGUI(strip);
            }
            finally
            {
                textures[0] = pause; textures[1] = normal; textures[2] = fast; textures[3] = superfast;
            }
            // 1x/2x/3x name the native Normal/Fast/Superfast modes. They make no
            // claim about achieved TPS or the game's context-dependent multiplier.
        }

        internal static void Weather(Rect rect, WeatherDef weather, int hour)
        {
            if (weather == null || Event.current.type != EventType.Repaint) return;
            WeatherGlyph glyph;
            if (weather.doToxicBuildup) glyph = WeatherGlyph.Toxic;
            else if (weather.sandRate > 0) glyph = WeatherGlyph.Sand;
            else if (weather.eventMakers?.Any(m => m.eventClass != null && typeof(WeatherEvent_LightningStrike).IsAssignableFrom(m.eventClass)) == true) glyph = WeatherGlyph.Storm;
            else if (weather.snowRate > 0) glyph = WeatherGlyph.Snow;
            else if (weather.rainRate > 0) glyph = WeatherGlyph.Rain;
            else if (weather.overlayClasses?.Any(type => typeof(WeatherOverlay_Fog).IsAssignableFrom(type)) == true) glyph = WeatherGlyph.Fog;
            else if (weather.defName == "Clear") glyph = hour < 6 || hour >= 20 ? WeatherGlyph.Night : WeatherGlyph.Clear;
            else glyph = WeatherGlyph.Cloud; // Neutral fallback; actual label stays in the full native tooltip.
            if (!weatherTextures.TryGetValue(glyph, out Texture2D texture))
            {
                texture = MakeWeather(glyph);
                weatherTextures.Add(glyph, texture);
            }
            using (new Theme.GuiScope(true)) { GUI.color = Theme.Muted; GUI.DrawTexture(rect, texture); }
        }

        private static Texture2D MakeWeather(WeatherGlyph glyph)
        {
            // Cached antialiased vector silhouettes, inferred for states absent
            // from the reference. No source pixels, game texture edits or assets.
            const int size = 64, supersample = 3;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int coverage = 0;
                for (int sy = 0; sy < supersample; sy++) for (int sx = 0; sx < supersample; sx++)
                    if (WeatherInk(glyph, x + (sx + .5f) / supersample, size - y - (sy + .5f) / supersample)) coverage++;
                pixels[y * size + x] = new Color(1, 1, 1, coverage / (float)(supersample * supersample));
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = "ReframedClock_" + glyph, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(pixels); texture.Apply(false, true);
            return texture;
        }

        private static bool WeatherInk(WeatherGlyph glyph, float x, float y)
        {
            if (glyph == WeatherGlyph.Clear)
            {
                if (Circle(x, y, 32, 31, 11)) return true;
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4f;
                    if (Line(x, y, 32 + Mathf.Cos(angle) * 16, 31 + Mathf.Sin(angle) * 16,
                        32 + Mathf.Cos(angle) * 21, 31 + Mathf.Sin(angle) * 21, 2.2f)) return true;
                }
                return false;
            }
            if (glyph == WeatherGlyph.Night) return Circle(x, y, 31, 31, 19) && !Circle(x, y, 40, 23, 18);
            if (glyph == WeatherGlyph.Sand)
                return Line(x, y, 9, 21, 46, 21, 3) || Line(x, y, 17, 32, 55, 32, 3) || Line(x, y, 9, 43, 42, 43, 3) || Circle(x, y, 52, 44, 2);
            bool cloud = Circle(x, y, 19, 29, 9) || Circle(x, y, 31, 24, 12) || Circle(x, y, 44, 30, 9) ||
                x >= 18 && x <= 44 && y >= 28 && y <= 39;
            if (cloud) return true;
            if (glyph == WeatherGlyph.Fog) return Line(x, y, 12, 47, 52, 47, 3) || Line(x, y, 19, 55, 45, 55, 3);
            if (glyph == WeatherGlyph.Rain)
                return Line(x, y, 21, 45, 17, 53, 2.8f) || Line(x, y, 34, 45, 30, 53, 2.8f) || Line(x, y, 47, 45, 43, 53, 2.8f);
            if (glyph == WeatherGlyph.Snow)
            {
                for (int i = 0; i < 3; i++)
                {
                    float cx = 20 + i * 13;
                    if (Line(x, y, cx - 3, 46, cx + 3, 52, 1.8f) || Line(x, y, cx - 3, 52, cx + 3, 46, 1.8f) || Line(x, y, cx, 45, cx, 53, 1.8f)) return true;
                }
            }
            if (glyph == WeatherGlyph.Storm) return Line(x, y, 34, 41, 26, 49, 3.2f) || Line(x, y, 26, 49, 36, 49, 3.2f) || Line(x, y, 36, 49, 29, 58, 3.2f);
            if (glyph == WeatherGlyph.Toxic) return Circle(x, y, 25, 49, 3) || Circle(x, y, 39, 49, 3) || Line(x, y, 29, 55, 35, 55, 2.5f);
            return false;
        }

        private static bool Circle(float x, float y, float cx, float cy, float radius)
            => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius;
        private static bool Line(float x, float y, float ax, float ay, float bx, float by, float width)
        {
            float dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy));
            return Circle(x, y, ax + t * dx, ay + t * dy, width / 2f);
        }
    }
}
