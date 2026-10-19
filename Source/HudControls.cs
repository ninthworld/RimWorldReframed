using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Paint-only utility buttons; actions and key routing stay with their native owners.
    internal static class HudControls
    {
        private static GUIStyle shortcutStyle;
        internal static void Face(Rect rect, bool hover, bool selected, bool disabled)
        {
            Color face = selected ? Theme.Selected : hover ? Theme.Hover : Theme.Surface;
            // Standalone map controls use the same lit rim as Architect.
            Theme.ButtonFace(rect, face, disabled, radius: 5f,
                rim: selected ? Theme.Rgb(0x92947B) : Theme.Rgb(0x465B6B));
        }

        internal static void ToolsGlyph(Rect rect, bool disabled, bool selected = false)
        {
            Color ink = selected ? Theme.Ink : Theme.Muted;
            if (disabled) ink.a = .45f;
            for (int i = 0; i < 4; i++)
                Theme.RoundRect(new Rect(rect.x + 7.8f + (i % 2) * 7.8f,
                    rect.y + 6.6f + (i / 2) * 7.8f, 5.2f, 5.2f), ink, 2.6f);
        }

        internal static void MenuGlyph(Rect rect, bool disabled, bool selected = false)
        {
            Color ink = selected ? Theme.Ink : Theme.Muted;
            if (disabled) ink.a = .45f;
            for (int i = 0; i < 3; i++)
            {
                float y = rect.y + 7.2f + i * 3.8f;
                Theme.RoundRect(new Rect(rect.x + 8f, y, 1.8f, 1.8f), ink, .65f);
                Theme.RoundRect(new Rect(rect.x + 11.4f, y, 9.6f, 1.6f), ink, .5f);
            }
        }

        internal static void Shortcut(Rect button, string label, bool disabled)
        {
            if (string.IsNullOrEmpty(label) || Event.current.type != EventType.Repaint) return;
            using (new Theme.GuiScope(true))
            {
                Text.Font = GameFont.Small;
                if (shortcutStyle == null || shortcutStyle.font != Text.CurFontStyle.font)
                    shortcutStyle = new GUIStyle(Text.CurFontStyle) { fontSize = 8,
                        alignment = TextAnchor.MiddleCenter, wordWrap = false, richText = false,
                        clipping = TextClipping.Clip, padding = new RectOffset(), margin = new RectOffset() };
                float width = Mathf.Clamp(shortcutStyle.CalcSize(new GUIContent(label)).x + 3f, 7f, button.width - 3f);
                Rect badge = new Rect(button.xMax - width - 1f, button.yMax - 10f, width, 9.5f);
                Theme.RoundRect(badge, Theme.PanelFill, 1.5f);
                Color ink = Theme.Muted; if (disabled) ink.a = .45f;
                GUI.color = ink;
                GUI.Label(badge, label, shortcutStyle);
            }
        }
    }
}
