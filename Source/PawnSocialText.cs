using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal static class PawnSocialText
    {
        private static GUIStyle paragraph, small;
        private static void Ensure()
        {
            using (new Theme.GuiScope(true))
            {
                Text.Font = GameFont.Small;
                if (paragraph != null && paragraph.font == Text.CurFontStyle.font) return;
                paragraph = new GUIStyle(Text.CurFontStyle) { fontSize = 10, alignment = TextAnchor.UpperLeft,
                    wordWrap = true, richText = true, clipping = TextClipping.Clip,
                    padding = new RectOffset(), margin = new RectOffset() };
                paragraph.normal.textColor = Color.white;
                small = new GUIStyle(paragraph) { wordWrap = false };
            }
        }
        internal static float Width(string value) { Ensure(); return small.CalcSize(new GUIContent(value ?? "")).x; }
        internal static float Height(string value, float width) { Ensure(); return paragraph.CalcHeight(new GUIContent(value ?? ""), width); }
        internal static void Paragraph(Rect rect, string value, Color color)
        {
            Ensure();
            using (new Theme.GuiScope(true)) { GUI.color = color; GUI.Label(rect, value, paragraph); }
        }
    }
}
