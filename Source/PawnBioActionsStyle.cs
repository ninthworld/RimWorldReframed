using UnityEngine;
using Verse;
namespace ReframedUI
{
    internal static class PawnBioActionsStyle
    {
        private static GUIStyle input, close;
        private static void Styles()
        {
            if (input != null) return;
            Text.Font = GameFont.Small;
            input = new GUIStyle(GUI.skin.textField) { font = Text.CurFontStyle.font, fontSize = 11, alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(7, 7, 2, 2), margin = new RectOffset(), border = new RectOffset(), wordWrap = false };
            input.normal.background = input.hover.background = input.focused.background = input.active.background = null;
            input.normal.textColor = input.hover.textColor = input.focused.textColor = input.active.textColor = Color.white;
            close = new GUIStyle(Text.CurFontStyle) { fontSize = 16, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(), margin = new RectOffset() };
            close.normal.textColor = Color.white;
        }
        internal static bool Close(Rect rect)
        {
            using (new Theme.GuiScope(true))
            {
                Styles(); Theme.ButtonFace(rect, Mouse.IsOver(rect) ? Theme.Hover : Theme.Surface, false);
                GUI.color = Theme.Muted; GUI.Label(rect, "×", close); TooltipHandler.TipRegion(rect, "Close".Translate());
                return Widgets.ButtonInvisible(rect);
            }
        }
        internal static string Input(Rect rect, string text, bool focused)
        {
            using (new Theme.GuiScope(true))
            {
                Styles(); Theme.RoundRect(rect, focused ? Theme.Rgb(0x638399) : GUI.enabled ? Theme.Border : Theme.Border.ToTransparent(.4f), 3);
                Theme.RoundRect(rect.ContractedBy(1), Theme.Rgb(0x111922), 2); GUI.color = Theme.Ink;
                return GUI.TextField(rect, text ?? "", input);
            }
        }
    }
}
