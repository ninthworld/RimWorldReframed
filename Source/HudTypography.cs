using UnityEngine;
using Verse;

namespace ReframedUI
{
    // The resource readout establishes the typography for the compact HUD panels.
    internal static class HudTypography
    {
        internal const int BodySize = 11, HeadingSize = 7;
        private const float Tracking = .65f;
        private static GUIStyle heading;

        private static void EnsureHeading()
        {
            Text.Font = GameFont.Small;
            if (heading != null && heading.font == Text.CurFontStyle.font) return;
            heading = new GUIStyle(Text.CurFontStyle) { fontSize = HeadingSize, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft, wordWrap = false, richText = false,
                clipping = TextClipping.Clip, padding = new RectOffset(), margin = new RectOffset() };
            heading.normal.textColor = Color.white;
        }

        internal static float MeasureHeading(string value)
        {
            using (new Theme.GuiScope(true))
            {
                EnsureHeading();
                float width = 0;
                foreach (char c in value.ToUpperInvariant())
                    width += heading.CalcSize(new GUIContent(c.ToString())).x + Tracking;
                return Mathf.Max(0, width - Tracking);
            }
        }

        internal static void Heading(Rect rect, string value, Color color)
        {
            using (new Theme.GuiScope(true))
            {
                EnsureHeading();
                // A modal blocks controls, not the readout's text contrast.
                GUI.enabled = true; GUI.contentColor = Color.white; GUI.color = color;
                GUI.BeginGroup(rect);
                try
                {
                    float x = 0;
                    foreach (char c in value.ToUpperInvariant())
                    {
                        string glyph = c.ToString();
                        float advance = heading.CalcSize(new GUIContent(glyph)).x;
                        if (x + advance > rect.width) break;
                        GUI.Label(new Rect(x, 0, advance + 1, rect.height), glyph, heading);
                        x += advance + Tracking;
                    }
                }
                finally { GUI.EndGroup(); }
            }
        }
    }
}
