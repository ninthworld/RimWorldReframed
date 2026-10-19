using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal static class PawnInspectorView
    {
        private static GUIStyle body, bold, small, tiny;
        internal static readonly Color Content = Theme.Rgb(DesignTokens.Deep).ToTransparent(.96f);
        private static readonly Dictionary<Color32, Texture2D> meterGradients = new Dictionary<Color32, Texture2D>();
        internal static Color BodyFill => Theme.Rgb(DesignTokens.Content).ToTransparent(Mathf.Clamp((ReframedMod.Settings?.PanelOpacity ?? .9f) + .04f, .8f, .96f));
        internal static Color HeaderFill => Theme.Rgb(DesignTokens.Panel).ToTransparent(Mathf.Clamp((ReframedMod.Settings?.PanelOpacity ?? .9f) + .06f, .82f, .97f));
        internal static void Styles()
        {
            using (new Theme.GuiScope(true))
            {
            Text.Font = GameFont.Small;
            if (body != null && body.font == Text.CurFontStyle.font) return;
            body = new GUIStyle(Text.CurFontStyle) { fontSize = 11, alignment = TextAnchor.MiddleLeft, wordWrap = false,
                clipping = TextClipping.Clip, padding = new RectOffset(), margin = new RectOffset() };
            body.normal.textColor = Color.white;
            bold = new GUIStyle(body) { fontStyle = FontStyle.Bold };
            small = new GUIStyle(body) { fontSize = 10 };
            tiny = new GUIStyle(body) { fontSize = 8, fontStyle = FontStyle.Bold };
            }
        }
        internal static float Measure(string value) { Styles(); return body.CalcSize(new GUIContent(value)).x; }
        internal static float MeasureStrong(string value) { Styles(); return bold.CalcSize(new GUIContent(value)).x; }
        internal static float MeasureSmall(string value) { Styles(); return small.CalcSize(new GUIContent(value)).x; }
        internal static void Label(Rect rect, string value, Color color, bool strong = false, bool right = false)
        {
            Styles();
            StyledLabel(rect, value, color, strong ? bold : body, right);
        }
        internal static void SmallLabel(Rect rect, string value, Color color, bool right = false)
        {
            Styles(); StyledLabel(rect, value, color, small, right);
        }
        private static void StyledLabel(Rect rect, string value, Color color, GUIStyle style, bool right)
        {
            using (new Theme.GuiScope(true))
            {
                GUI.color = color;
                TextAnchor old = style.alignment;
                style.alignment = right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                try
                {
                    value = value ?? "";
                    string fitted = value;
                    if (style.CalcSize(new GUIContent(value)).x > rect.width)
                    {
                        int[] boundaries = StringInfo.ParseCombiningCharacters(value);
                        int low = 0, high = boundaries.Length;
                        while (low < high)
                        {
                            int mid = (low + high + 1) / 2;
                            string candidate = value.Substring(0, mid == boundaries.Length ? value.Length : boundaries[mid]) + "…";
                            if (style.CalcSize(new GUIContent(candidate)).x <= rect.width) low = mid; else high = mid - 1;
                        }
                        fitted = value.Substring(0, low == boundaries.Length ? value.Length : boundaries[low]) + "…";
                        TooltipHandler.TipRegion(rect, value);
                    }
                    GUI.Label(rect, fitted, style);
                }
                finally { style.alignment = old; }
            }
        }
        internal static void Heading(Rect rect, string value)
        {
            Styles();
            using (new Theme.GuiScope(true))
            {
                GUI.color = Theme.Ink;
                float x = rect.x;
                foreach (char letter in value)
                {
                    string glyph = letter.ToString(); float width = tiny.CalcSize(new GUIContent(glyph)).x;
                    GUI.Label(new Rect(x, rect.y, width + 1, rect.height), glyph, tiny); x += width + .65f;
                }
            }
        }
        // Paint adjoining surfaces once, so translucent tabs do not become
        // darker than the body. The lighter fill belongs only to the active tab.
        internal static void TabBand(Rect band, Rect active)
        {
            float left = Mathf.Clamp(active.xMin, band.xMin, band.xMax);
            float right = Mathf.Clamp(active.xMax, left, band.xMax);
            Theme.RoundRect(new Rect(band.x, band.y, left - band.x, band.height), HeaderFill, 0);
            Theme.RoundRect(new Rect(left, band.y, right - left, band.height), BodyFill, 0);
            Theme.RoundRect(new Rect(right, band.y, band.xMax - right, band.height), HeaderFill, 0);
        }
        internal static bool Tab(Rect rect, string text, bool selected, string tooltip, string icon = null, bool bandPainted = false)
        {
            // The outline extends below its clip: no lower border or rounded
            // bottom corners can separate the selected tab from its content.
            GUI.BeginClip(rect);
            try
            {
                Rect outer = new Rect(0, 0, rect.width, rect.height + 5);
                if (!bandPainted && !selected) Theme.RoundRect(outer, HeaderFill, 3f);
                Color rim = selected ? Theme.Border : Theme.Border.ToTransparent(.55f);
                if (Event.current.type == EventType.Repaint)
                    using (new Theme.GuiScope(true))
                    {
                        Color tint = rim * GUI.color; GUI.color = Color.white;
                        GUI.DrawTexture(outer, BaseContent.WhiteTex, ScaleMode.StretchToFill, true, 0, tint, 1f, 3f);
                    }
            }
            finally { GUI.EndClip(); }
            using (new Theme.GuiScope(true))
            {
                GUI.color = selected ? Theme.Ink : Theme.Muted;
                if (icon != null) GUI.DrawTexture(new Rect(rect.x + 7, rect.center.y - 6.5f, 13, 13), InspectorIcons.Get(icon));
            }
            Rect textRect = rect.ContractedBy(7, 0); if (icon != null) textRect.xMin += 18;
            Label(textRect, text, selected ? Theme.Ink : Theme.Muted);
            TooltipHandler.TipRegion(rect, tooltip);
            return Widgets.ButtonInvisible(rect);
        }
        internal static void Header(Rect rect, Pawn pawn)
        {
            Portrait(new Rect(8, 2, 50, 56), pawn);
            // Only real state indicators are shown. Never invent statuses to fill a source layout.
            float statusX = rect.width - 64;
            if (pawn.Ideo != null) Status("ideology", pawn.Ideo.name, Theme.Rgb(0x22B8D0), ref statusX);
            if (pawn.Dead) Status("dead", "Dead", Theme.Ink, ref statusX);
            if (pawn.Drafted) Status("drafted", "Drafted", Theme.Danger, ref statusX);
            if (pawn.Downed) Status("medical", "Downed", Theme.Danger, ref statusX);
            string age = pawn.gender.GetLabel().CapitalizeFirst() + ", " + pawn.ageTracker.AgeBiologicalYears + " years";
            float ageWidth = Math.Min(153, MeasureSmall(age) + 4);
            SmallLabel(new Rect(statusX - ageWidth, 8, ageWidth, 20), age, Theme.Muted, right: true);
            float textWidth = Math.Max(35, statusX - ageWidth - 80);
            string name = pawn.LabelShortCap;
            float nameWidth = PawnInspectorLayout.NameWidth(MeasureStrong(name), textWidth);
            Label(new Rect(68, 8, nameWidth, 20), name, Theme.Ink, true);
            string role = pawn.story?.TitleShortCap ?? pawn.KindLabel;
            float roleWidth = textWidth - nameWidth - 3;
            if (roleWidth >= Math.Min(40, MeasureSmall(role)))
                SmallLabel(new Rect(68 + nameWidth + 3, 8, roleWidth, 20), role, Theme.Muted);
            else SmallLabel(new Rect(68, 26, textWidth, 15), role, Theme.Muted);
        }
        internal static void Portrait(Rect rect, Pawn pawn)
        {
            using (new Theme.GuiScope(true))
            {
                GUI.color = Color.white;
                GUI.DrawTexture(rect, PortraitsCache.Get(pawn, new Vector2(64, 72), Rot4.South,
                    ColonistBarColonistDrawer.PawnTextureCameraOffset, 1.65f), ScaleMode.ScaleToFit);
            }
        }
        private static void Status(string key, string tip, Color color, ref float x)
        {
            x -= 17;
            Rect rect = new Rect(x, 12, 12, 12);
            using (new Theme.GuiScope(true)) { GUI.color = color; GUI.DrawTexture(rect, InspectorIcons.Get(key)); }
            TooltipHandler.TipRegion(rect, tip);
        }
        internal static void Bar(Rect bar, float value, Color color)
        {
            Theme.RoundRect(bar, Theme.Border.ToTransparent(.6f), 2.5f);
            Rect inside = bar.ContractedBy(.6f);
            Theme.RoundRect(inside, Theme.Rgb(0x0C131B), 2);
            value = Mathf.Clamp01(value);
            if (value <= 0 || Event.current.type != EventType.Repaint) return;
            Color32 key = color;
            if (!meterGradients.TryGetValue(key, out Texture2D gradient))
            {
                gradient = new Texture2D(256, 1, TextureFormat.RGBA32, false)
                    { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                Color[] pixels = new Color[256];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.Lerp(color, color * new Color(.55f, .55f, .55f, 1), i / 255f);
                gradient.SetPixels(pixels); gradient.Apply(false, true); meterGradients.Add(key, gradient);
            }
            Rect filled = new Rect(inside.x, inside.y, inside.width * value, inside.height);
            using (new Theme.GuiScope(true))
            {
                GUI.color = Color.white;
                GUI.DrawTexture(filled, gradient, ScaleMode.StretchToFill, true, 0, Color.white, 0, Math.Min(2, filled.width / 2));
            }
        }
        internal static void Meter(Rect row, string label, float? amount, Color color, string tooltip = null, string formattedValue = null)
        {
            float nameWidth = Math.Min(77f, row.width * .32f), valueWidth = 38;
            Label(new Rect(row.x, row.y, nameWidth, row.height), label, Theme.Ink);
            Rect bar = new Rect(row.x + nameWidth, row.y + (row.height - 11) / 2, Math.Max(1, row.width - nameWidth - valueWidth - 4), 11);
            float value = amount.HasValue ? PawnInspectorLayout.Ratio(amount.Value, 1) : 0;
            Bar(bar, value, color);
            SmallLabel(new Rect(row.xMax - valueWidth, row.y, valueWidth, row.height), formattedValue ?? (amount.HasValue ? value.ToStringPercent("F0") : "—"), Theme.Muted, right: true);
            if (!string.IsNullOrEmpty(tooltip)) TooltipHandler.TipRegion(row, tooltip);
        }
        internal static void Summary(Rect rect, Pawn pawn, Action details)
        {
            float leftWidth = rect.width * .45f;
            Rect left = new Rect(rect.x, rect.y, leftWidth, 86), right = new Rect(left.xMax + 7, rect.y, rect.width - leftWidth - 7, rect.height);
            Theme.RoundRect(left, Content, 3f); Theme.RoundRect(right, Content, 3f);
            Rect line = new Rect(left.x + 6, left.y + 2, left.width - 12, 20);
            Meter(line, "Health", pawn.health?.summaryHealth.SummaryHealthPercent, Theme.Danger); line.y += 20;
            Meter(line, "Mood", pawn.needs?.mood?.CurLevelPercentage, Theme.Rgb(0xD6B11A)); line.y += 20;
            Meter(line, "Food", pawn.needs?.food?.CurLevelPercentage, Theme.Rgb(0x83AF7F)); line.y += 20;
            Meter(line, "Recreation", pawn.needs?.joy?.CurLevelPercentage, Theme.Rgb(0x687BB7));
            float split = left.width * .41f;
            Rect activity = new Rect(left.x, left.yMax + 7, split - 3, Math.Max(44, rect.yMax - left.yMax - 7));
            Rect equipment = new Rect(activity.xMax + 7, activity.y, left.width - split - 4, activity.height);
            Theme.RoundRect(activity, Content, 3); Theme.RoundRect(equipment, Content, 3);
            Heading(new Rect(activity.x + 6, activity.y + 4, activity.width - 12, 12), "ACTIVITY");
            string job = pawn.CurJob?.GetReport(pawn) ?? (pawn.Dead ? "Dead" : pawn.Downed ? "Downed" : "Standing");
            Label(new Rect(activity.x + 6, activity.y + 18, activity.width - 12, 20), job.CapitalizeFirst(), Theme.Ink);
            TooltipHandler.TipRegion(activity, job);
            if (Widgets.ButtonInvisible(activity)) details();
            Heading(new Rect(equipment.x + 6, equipment.y + 4, equipment.width - 12, 12), "EQUIPMENT");
            ThingWithComps item = pawn.equipment?.Primary;
            if (item == null) Label(new Rect(equipment.x + 6, equipment.y + 18, equipment.width - 12, 20), "None", Theme.Muted);
            else
            {
                Widgets.ThingIcon(new Rect(equipment.x + 5, equipment.y + 20, 14, 14), item);
                float conditionWidth = item.def.useHitPoints ? 30 : 0;
                SmallLabel(new Rect(equipment.x + 23, equipment.y + 18, equipment.width - 29 - conditionWidth, 20), item.LabelShortCap, Theme.Ink);
                if (conditionWidth > 0) SmallLabel(new Rect(equipment.xMax - 34, equipment.y + 18, 28, 20), PawnInspectorLayout.Ratio(item.HitPoints, item.MaxHitPoints).ToStringPercent("F0"), Theme.Muted, right: true);
                TooltipHandler.TipRegion(equipment, item.GetTooltip());
                if (Widgets.ButtonInvisible(equipment)) Find.WindowStack.Add(new Dialog_InfoCard(item));
            }
            Heading(new Rect(right.x + 7, right.y + 4, right.width - 14, 12), "SKILLS");
            if (pawn.skills == null) { Label(right.ContractedBy(8), "No skills", Theme.Muted); return; }
            string[] order = { "Shooting", "Construction", "Cooking", "Animals", "Artistic", "Social", "Melee", "Mining", "Plants", "Crafting", "Medicine", "Intellectual" };
            var skills = pawn.skills.skills.OrderBy(skill => { int n = Array.IndexOf(order, skill.def.defName); return n < 0 ? 99 : n; }).ToList();
            int rows = Math.Max(6, (skills.Count + 1) / 2);
            float column = (right.width - 20) / 2f, rowHeight = Math.Min(20, (right.height - 22) / rows);
            for (int i = 0; i < skills.Count; i++)
            {
                SkillRecord skill = skills[i]; int col = i / rows, row = i % rows;
                Rect slot = new Rect(right.x + 7 + col * (column + 6), right.y + 20 + row * rowHeight, column, rowHeight);
                SkillLine(slot, skill);
                TooltipHandler.TipRegion(slot, skill.def.LabelCap + "\n" + skill.def.description + "\n" + (skill.TotallyDisabled ? "Disabled" : "Level " + skill.Level + " · " + skill.passion));
            }
        }
        internal static void SkillLine(Rect slot, SkillRecord skill)
        {
            Color color = skill.TotallyDisabled || skill.passion == Passion.None ? Theme.Muted : Theme.Rgb(DesignTokens.Best);
            Label(new Rect(slot.x, slot.y, slot.width - 35, slot.height), skill.def.skillLabel.CapitalizeFirst(),
                skill.TotallyDisabled ? Theme.Muted : Theme.Ink);
            SmallLabel(new Rect(slot.xMax - 35, slot.y, 20, slot.height), skill.TotallyDisabled ? "—" : skill.Level.ToString(), color, right: true);
            if (!skill.TotallyDisabled && skill.passion != Passion.None)
            {
                using (new Theme.GuiScope(true))
                {
                    GUI.color = Theme.Rgb(DesignTokens.Best);
                    GUI.DrawTexture(new Rect(slot.xMax - 13, slot.y + (slot.height - 12) / 2, 12, 12),
                        InspectorIcons.Get(skill.passion == Passion.Major ? "doublepassion" : "passion"));
                }
            }
        }
    }
}
