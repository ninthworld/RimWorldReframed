using System;
using System.Globalization;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Drawing returns intents only. The controller owns selection, priority,
    // copy/paste and every native gameplay callback.
    internal static class WorkPanelView
    {
        private static GUIStyle heading, tagText, identityText;
        internal static Color CategoryColor(int category)
        {
            switch (category)
            {
                case 0: return Theme.Rgb(0x389BD5);
                case 1: return Theme.Rgb(0x22BDC5);
                case 2: return Theme.Rgb(0x42CF69);
                case 3: return Theme.Rgb(0xDACB38);
                case 4: return Theme.Rgb(0xDE9D68);
                case 5: return Theme.Rgb(0xE46573);
                default: return Theme.Rgb(0x96A5B5);
            }
        }
        internal static float MeasureTag(string label, int? skill, Passion passion)
            => (float)Math.Ceiling(WorkPanelViewLayout.MeasureTag(PawnInspectorView.MeasureSmall(label ?? ""),
                skill.HasValue ? PawnInspectorView.MeasureSmall(skill.Value.ToString(CultureInfo.InvariantCulture)) : 0,
                skill.HasValue, passion != Passion.None)) + 2;

        // Tooltips belong to the controller's fully visible hit targets. Painting
        // a clipped or truncated tag must not create an invisible tooltip region.
        private static void PaintTagLabel(Rect rect, string text, Color color, bool right = false, bool strong = false)
        {
            using (new Theme.GuiScope(true))
            {
                Text.Font = GameFont.Small;
                if (tagText == null || tagText.font != Text.CurFontStyle.font)
                {
                    tagText = new GUIStyle(Text.CurFontStyle) { fontSize = 10, wordWrap = false,
                        clipping = TextClipping.Clip, padding = new RectOffset(), margin = new RectOffset() };
                    tagText.normal.textColor = Color.white;
                    identityText = new GUIStyle(tagText) { fontSize = 11, fontStyle = FontStyle.Bold };
                }
                GUIStyle style = strong ? identityText : tagText;
                style.alignment = right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                string fitted = text ?? "";
                if (style.CalcSize(new GUIContent(fitted)).x > rect.width)
                {
                    int[] boundaries = StringInfo.ParseCombiningCharacters(fitted);
                    int low = 0, high = boundaries.Length;
                    while (low < high)
                    {
                        int mid = (low + high + 1) / 2;
                        string value = fitted.Substring(0, mid == boundaries.Length ? fitted.Length : boundaries[mid]) + "…";
                        if (style.CalcSize(new GUIContent(value)).x <= rect.width) low = mid; else high = mid - 1;
                    }
                    fitted = fitted.Substring(0, low == boundaries.Length ? fitted.Length : boundaries[low]) + "…";
                }
                GUI.color = color; GUI.Label(rect, fitted, style);
            }
        }

        internal static string PriorityName(int priority)
            => priority == 1 ? "Highest" : priority == 2 ? "High" : priority == 3 ? "Normal" : "Low";
        internal static bool PriorityToggle(Rect rect, bool value)
        {
            bool available = GUI.enabled;
            Color ink = available ? Theme.Ink : Theme.Muted;
            Theme.ButtonFace(rect, available && Mouse.IsOver(rect) ? Theme.Hover : Theme.Surface, !available);
            PawnInspectorView.Label(new Rect(rect.x + 7, rect.y, rect.width - 58, rect.height), "Priorities", ink);
            PawnInspectorView.SmallLabel(new Rect(rect.xMax - 51, rect.y, 21, rect.height), value ? "On" : "Off", ink);
            Rect track = new Rect(rect.xMax - 30, rect.center.y - 6, 24, 12);
            Theme.RoundRect(track, value && available ? Theme.Rgb(0x3D8063) : Theme.Border, 6);
            Theme.RoundRect(new Rect(value ? track.xMax - 11 : track.x + 1, track.y + 1, 10, 10), ink, 5);
            return available && Widgets.ButtonInvisible(rect);
        }
        private static Color PriorityColor(int priority)
            => Theme.Rgb(priority == 1 ? 0x71C99Au : priority == 2 ? 0x75BCE7u : priority == 3 ? 0xD9BD75u : 0xB39ACDu);
        internal static bool PriorityButton(Rect rect, int priority, bool enabled, string tooltip)
        {
            using (new Theme.GuiScope(true))
            {
                bool assigned = priority >= 1 && priority <= 4;
                bool live = assigned && enabled && GUI.enabled;
                Color accent = live ? PriorityColor(priority) : Theme.Muted;
                // A centered, outlined numeric badge distinguishes priority from
                // the work tag's rectangular face and category stripe.
                Theme.RoundRect(rect, assigned ? accent : Theme.Border, rect.height / 2);
                Theme.RoundRect(rect.ContractedBy(1), assigned
                    ? Color.Lerp(Theme.Rgb(0x0B1218), accent, live && Mouse.IsOver(rect) ? .28f : .12f)
                    : Theme.Rgb(0x11191F), rect.height / 2 - 1);
                string label = assigned ? priority.ToString(CultureInfo.InvariantCulture) : "-";
                float width = PawnInspectorView.MeasureStrong(label) + 2;
                PaintTagLabel(new Rect(rect.center.x - width / 2, rect.y, width, rect.height), label, accent, strong: true);
                if (GUI.enabled) TooltipHandler.TipRegion(rect, tooltip);
                return live && Event.current.type == EventType.MouseDown && Event.current.button == 0 && Mouse.IsOver(rect);
            }
        }

        internal static int Tag(Rect rect, string label, int category, int? skill, Passion passion,
            bool assigned, bool available, string tooltip, bool? activatable = null, bool incapable = false)
        {
            if (rect.width <= 0 || rect.height <= 0) return 0;
            using (new Theme.GuiScope(true))
            {
                Color color = CategoryColor(category);
                bool live = available && GUI.enabled;
                bool hover = live && Mouse.IsOver(rect);
                Theme.RoundRect(rect, assigned ? (hover ? Theme.Hover : Theme.Rgb(0x202B36)) : Theme.Border.ToTransparent(.75f), 2.5f);
                if (!assigned) Theme.RoundRect(rect.ContractedBy(.7f), hover ? Theme.Hover : Theme.Rgb(0x0D161E), 2);
                if (incapable)
                {
                    Theme.RoundRect(rect, Theme.Danger.ToTransparent(.75f), 2.5f);
                    Theme.RoundRect(rect.ContractedBy(.8f), assigned ? Theme.Rgb(0x202B36) : Theme.Rgb(0x0D161E), 2);
                }
                if (!live) color = Color.Lerp(color, Theme.Muted, .6f).ToTransparent(.6f);
                Theme.RoundRect(new Rect(rect.x, rect.y + 2, Math.Min(2, rect.width), Math.Max(0, rect.height - 4)), color, .7f);
                string number = skill?.ToString(CultureInfo.InvariantCulture) ?? "";
                var parts = WorkPanelViewLayout.Tag(rect.width, PawnInspectorView.MeasureSmall(number), skill.HasValue, passion != Passion.None);
                PaintTagLabel(new Rect(rect.x + parts.LabelX, rect.y, parts.LabelWidth, rect.height), label,
                    live ? Theme.Ink : Theme.Muted);
                if (skill.HasValue)
                {
                    Color metadata = live && passion != Passion.None ? Theme.Rgb(DesignTokens.Best) : Theme.Muted;
                    PaintTagLabel(new Rect(rect.x + parts.SkillX, rect.y, parts.SkillWidth, rect.height), number, metadata, right: true);
                    if (parts.FlameSize > 0)
                    {
                        GUI.color = metadata;
                        GUI.DrawTexture(new Rect(rect.x + parts.FlameX, rect.center.y - parts.FlameSize / 2, parts.FlameSize, parts.FlameSize),
                            InspectorIcons.Get(passion == Passion.Major ? "doublepassion" : "passion"));
                    }
                }
                if (GUI.enabled && !string.IsNullOrEmpty(tooltip)) TooltipHandler.TipRegion(rect, tooltip);
                Event current = Event.current;
                int click = WorkPanelViewLayout.Click(activatable ?? available, GUI.enabled, current != null && current.type == EventType.MouseDown,
                    Mouse.IsOver(rect), current?.button ?? -1);
                // The controller/native dispatcher consumes the original event.
                // Native work warnings and checkbox handling require MouseDown.
                return click;
            }
        }
        internal static void DrawRowFace(Rect rect, bool selected)
        {
            if (selected)
            {
                Theme.RoundRect(rect, Theme.Rgb(0x354956), 3);
                Theme.RoundRect(rect.ContractedBy(.8f), Theme.Rgb(0x18252E), 2.5f);
                Theme.RoundRect(new Rect(rect.x + 1, rect.y + 4, 2, Math.Max(0, rect.height - 8)), Theme.Rgb(0x9BB5C4), 1);
            }
            else if (GUI.enabled && Mouse.IsOver(rect)) Theme.RoundRect(rect, Theme.Hover.ToTransparent(.6f), 3);
        }
        internal static bool Identity(Rect rect, Pawn pawn, bool selected, bool showStatus = true)
        {
            if (pawn == null || rect.width < 45 || rect.height <= 0) return false;
            GUI.BeginClip(rect);
            try
            {
            rect = new Rect(0, 0, rect.width, rect.height);
            using (new Theme.GuiScope(true))
            {
                float middle = rect.center.y;
                Rect select = new Rect(rect.x + 5, middle - 3.5f, 7, 7);
                Theme.RoundRect(select, selected ? Theme.Rgb(0x9BB5C4) : Theme.Border, 1.5f);
                if (!selected) Theme.RoundRect(select.ContractedBy(1), Theme.Rgb(0x0D161E), .7f);
                PawnInspectorView.Portrait(new Rect(rect.x + 15, middle - 11, 18, 22), pawn);
                float right = rect.xMax - 4;
                if (showStatus)
                {
                    if (pawn.Ideo != null) Status("ideology", pawn.Ideo.name, Theme.Rgb(0x22B8D0), middle, ref right);
                    if (pawn.Dead) Status("dead", "Dead".Translate(), Theme.Ink, middle, ref right);
                    if (pawn.Drafted) Status("drafted", "Drafted".Translate(), Theme.Danger, middle, ref right);
                    if (pawn.Downed) Status("medical", "Downed".Translate(), Theme.Danger, middle, ref right);
                }
                string name = pawn.LabelShortCap, role = pawn.story?.TitleShortCap ?? pawn.KindLabel;
                float left = rect.x + 37;
                float textWidth = Math.Max(0, right - left - 3);
                WorkPanelViewLayout.IdentityText(textWidth, PawnInspectorView.MeasureStrong(name), PawnInspectorView.MeasureSmall(role), out float nameWidth, out float roleWidth);
                PaintTagLabel(new Rect(left, rect.y, nameWidth, rect.height), name, Theme.Ink, strong: true);
                if (roleWidth > 0) PaintTagLabel(new Rect(left + nameWidth + 4, rect.y, roleWidth, rect.height), role, Theme.Muted);
                if (GUI.enabled)
                {
                    TooltipHandler.TipRegion(new Rect(left, rect.y, Math.Max(0, right - left), rect.height), name + "\n" + role);
                    TooltipHandler.TipRegion(new Rect(rect.x, rect.y, 41, rect.height), "Select colonist to edit work assignments");
                }
                return GUI.enabled && Widgets.ButtonInvisible(rect);
            }
            }
            finally { GUI.EndClip(); }
        }
        private static void Status(string key, string tip, Color color, float middle, ref float right)
        {
            right -= 15;
            Rect icon = new Rect(right, middle - 6, 12, 12);
            using (new Theme.GuiScope(true)) { GUI.color = color; GUI.DrawTexture(icon, InspectorIcons.Get(key)); }
            TooltipHandler.TipRegion(icon, tip);
        }
        internal static float MeasureHeading(string label)
        {
            using (new Theme.GuiScope(true))
            {
                EnsureHeading();
                return (label ?? "").Sum(c => heading.CalcSize(new GUIContent(c.ToString())).x + .3f);
            }
        }
        private static void EnsureHeading()
        {
            Text.Font = GameFont.Small;
            if (heading != null && heading.font == Text.CurFontStyle.font) return;
            heading = new GUIStyle(Text.CurFontStyle) { fontSize = 8, fontStyle = FontStyle.Bold,
                wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(), margin = new RectOffset() };
            heading.normal.textColor = Color.white;
        }
        internal static void CategoryHeading(Rect rect, string label)
        {
            // Clip long translations to their column; the complete label stays
            // available in a tooltip. An eight-pixel face fits narrow columns.
            GUI.BeginClip(rect);
            try
            {
                using (new Theme.GuiScope(true))
                {
                    EnsureHeading();
                    GUI.color = Theme.Ink;
                    float x = 0;
                    foreach (char c in (label ?? "").ToUpperInvariant())
                    {
                        string glyph = c.ToString(); float width = heading.CalcSize(new GUIContent(glyph)).x;
                        GUI.Label(new Rect(x, 0, width + 1, rect.height), glyph, heading); x += width + .3f;
                    }
                }
            }
            finally { GUI.EndClip(); }
            if (GUI.enabled) TooltipHandler.TipRegion(rect, label ?? "");
        }
        internal static bool IconButton(Rect rect, string key, string tooltip, bool enabled = true)
        {
            if (key != "copy" && key != "paste") return PawnInspectorWidgets.IconButton(rect, key, tooltip, enabled);
            using (new Theme.GuiScope(true))
            {
                bool live = enabled && GUI.enabled;
                Theme.ButtonFace(rect, live && Mouse.IsOver(rect) ? Theme.Hover : Theme.Surface, !live);
                Color ink = live ? Theme.Ink : Theme.Muted;
                Rect glyph = new Rect(rect.center.x - 5, rect.center.y - 6, 10, 12);
                if (key == "copy")
                {
                    Outline(new Rect(glyph.x - 2, glyph.y - 1, 8, 10), ink);
                    Theme.RoundRect(new Rect(glyph.x + 1, glyph.y + 2, 8, 10), Theme.Surface, .3f);
                    Outline(new Rect(glyph.x + 1, glyph.y + 2, 8, 10), ink);
                }
                else
                {
                    Outline(glyph, ink);
                    Theme.RoundRect(new Rect(glyph.x + 2, glyph.y - 1, 6, 3), ink, .7f);
                    Theme.RoundRect(new Rect(glyph.x + 2, glyph.y + 5, 6, 1), ink, 0);
                    Theme.RoundRect(new Rect(glyph.x + 2, glyph.y + 8, 5, 1), ink, 0);
                }
                if (GUI.enabled) TooltipHandler.TipRegion(rect, tooltip);
                GUI.enabled = live;
                return live && Widgets.ButtonInvisible(rect);
            }
        }
        private static void Outline(Rect rect, Color color)
        {
            Theme.RoundRect(new Rect(rect.x, rect.y, rect.width, 1), color, 0);
            Theme.RoundRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color, 0);
            Theme.RoundRect(new Rect(rect.x, rect.y, 1, rect.height), color, 0);
            Theme.RoundRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), color, 0);
        }
    }
}
