using System;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Shared inspector controls use the same quiet four-pixel rail as Architect.
    internal static class PawnInspectorWidgets
    {
        private static GUIStyle track, thumb;
        internal static float BeginScroll(Rect viewport, ref Vector2 position, float contentHeight)
        {
            float width = Math.Max(1, viewport.width - (contentHeight > viewport.height + .01f ? 10 : 0));
            position.x = 0;
            position.y = Mathf.Clamp(position.y, 0, Math.Max(0, contentHeight - viewport.height));
            Widgets.BeginScrollView(viewport, ref position, new Rect(0, 0, width, Math.Max(viewport.height, contentHeight)), false);
            return width;
        }
        internal static void EndScroll(Rect viewport, ref Vector2 position, float contentHeight, int controlHint)
        {
            Widgets.EndScrollView();
            if (contentHeight <= viewport.height + .01f) return;
            Rail(new Rect(viewport.xMax - 5, viewport.y, 4, viewport.height), ref position.y, contentHeight, viewport.height, false);
        }
        internal static void Rail(Rect rail, ref float position, float total, float visible, bool horizontal)
        {
            if (track == null)
            {
                track = new GUIStyle { margin = new RectOffset(), padding = new RectOffset() };
                thumb = new GUIStyle { margin = new RectOffset(), padding = new RectOffset() };
                thumb.normal.background = thumb.hover.background = thumb.active.background = BaseContent.WhiteTex;
            }
            Theme.RoundRect(rail, Theme.Surface, 2);
            using (new Theme.GuiScope(true))
            {
                GUI.color = Mouse.IsOver(rail.ExpandedBy(3)) ? Theme.Ink : Theme.Muted;
                float length = horizontal ? rail.width : rail.height;
                float extent = Math.Min(length, Math.Max(20, length * visible / total));
                thumb.fixedWidth = horizontal ? extent : 4;
                thumb.fixedHeight = horizontal ? 4 : extent;
                position = horizontal ? GUI.HorizontalSlider(rail, position, 0, Math.Max(0, total - visible), track, thumb) :
                    GUI.VerticalSlider(rail, position, 0, Math.Max(0, total - visible), track, thumb);
            }
        }
        internal static void Divider(Rect rect) => Theme.RoundRect(rect, Theme.Border.ToTransparent(.55f), 0);
        internal static bool IconButton(Rect rect, string icon, string tooltip, bool enabled = true)
        {
            if (!ArchitectPopup.SuppressLauncherTooltip) TooltipHandler.TipRegion(rect, tooltip);
            return Button(rect, "", icon, enabled);
        }
        internal static bool Button(Rect rect, string label, string icon = null, bool enabled = true, bool selected = false)
        {
            using (new Theme.GuiScope(true))
            {
                bool available = enabled && GUI.enabled;
                Theme.ButtonFace(rect, available && selected ? Theme.Selected : available && Mouse.IsOver(rect) ? Theme.Hover : Theme.Surface, !available);
                GUI.enabled = available;
                Color ink = available ? Theme.Ink : Theme.Muted;
                float iconSize = Math.Min(13, rect.height - 8);
                if (icon != null)
                {
                    GUI.color = ink;
                    float iconX = string.IsNullOrEmpty(label) ? rect.center.x - iconSize / 2 : rect.x + 7;
                    GUI.DrawTexture(new Rect(iconX, rect.center.y - iconSize / 2, iconSize, iconSize), InspectorIcons.Get(icon));
                }
                Rect text = rect.ContractedBy(7, 0);
                if (icon != null) text.xMin += iconSize + 5;
                if (!string.IsNullOrEmpty(label)) PawnInspectorView.Label(text, label, ink);
                return available && Widgets.ButtonInvisible(rect);
            }
        }
    }
}
