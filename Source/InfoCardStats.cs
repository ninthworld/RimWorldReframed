using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal static class InfoCardStats
    {
        private static readonly FieldInfo Entries = Field("cachedDrawEntries"), Values = Field("cachedEntryValues"),
            Selected = Field("selectedEntry"), Hovered = Field("mousedOverEntry"), Scroll = Field("scrollPosition"),
            RightScroll = Field("scrollPositionRightPanel"), Positioner = Field("scrollPositioner"),
            ListHeight = Field("listHeight"), RightHeight = Field("rightPanelHeight");
        internal static bool Ready => new[] { Entries, Values, Selected, Hovered, Scroll, RightScroll, Positioner, ListHeight, RightHeight }.All(member => member != null);
        internal static string MeasuringLabel;
        internal static float MeasuringWidth;
        private static FieldInfo Field(string name) => AccessTools.Field(typeof(StatsReportUtility), name);

        internal static void Draw(Rect rect, Thing thing, WorldObject worldObject)
        {
            var entries = (List<StatDrawEntry>)Entries.GetValue(null);
            var values = (List<string>)Values.GetValue(null);
            var positioner = (ScrollPositioner)Positioner.GetValue(null);
            Vector2 scroll = (Vector2)Scroll.GetValue(null), rightScroll = (Vector2)RightScroll.GetValue(null);
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white; Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                float leftWidth = InfoCardLayout.LeftWidth(rect.width);
                Rect left = new Rect(rect.x, rect.y, leftWidth, rect.height);
                Rect right = new Rect(left.xMax + 18, rect.y, Math.Max(1, rect.width - leftWidth - 18), rect.height);
                PawnInspectorWidgets.Divider(new Rect(left.xMax + 8, rect.y + 3, 1, rect.height - 6));
                positioner.ClearInterestRects();
                Hovered.SetValue(null, null);
                // Measure using the same native entry labels and cached values that
                // DrawStatsReport finalized. Search deliberately keeps every row.
                float MeasureList(float width)
                {
                    float measured = 0; string group = null;
                    for (int i = 0; i < entries.Count; i++)
                    {
                        string next = entries[i].category.LabelCap;
                        if (next != group) { measured += 26; group = next; }
                        measured += EntryHeight(entries[i].LabelCap, Value(entries[i], values, i), width - 8);
                    }
                    return measured;
                }
                float height = MeasureList(left.width);
                if (InfoCardLayout.Overflow(height, left.height)) height = MeasureList(left.width - 10);
                float contentWidth = PawnInspectorWidgets.BeginScroll(left, ref scroll, height);
                try
                {
                    float y = 0; string category = null;
                    for (int i = 0; i < entries.Count; i++)
                    {
                        StatDrawEntry entry = entries[i];
                        string next = entry.category.LabelCap;
                        if (next != category)
                        {
                            if (y > 0) PawnInspectorWidgets.Divider(new Rect(0, y + 2, contentWidth, 1));
                            PawnInspectorView.Heading(new Rect(8, y + 5, Math.Max(1, contentWidth - 8), 18), next.ToUpperInvariant());
                            y += 26; category = next;
                        }
                        bool selected = ReferenceEquals(Selected.GetValue(null), entry);
                        bool active = StatsReportUtility.QuickSearchWidget.filter.Active;
                        bool match = active && StatsReportUtility.QuickSearchWidget.filter.Matches(entry.LabelCap);
                        float start = y;
                        int index = i;
                        MeasuringLabel = entry.LabelCap;
                        MeasuringWidth = (contentWidth - 8) * .55f;
                        try
                        {
                            // Native entry.Draw retains its real click, hover, stat
                            // tooltip, lowlight, search and cached-value semantics.
                            y += entry.Draw(8, y, Math.Max(1, contentWidth - 8), selected, match, active && !match,
                                () => StatsReportUtility.SelectEntry(index), () => Hovered.SetValue(null, entry),
                                scroll, left, Value(entry, values, i));
                        }
                        finally { MeasuringLabel = null; }
                        if (selected || match) positioner.RegisterInterestRect(new Rect(8, start, Math.Max(1, contentWidth - 8), y - start));
                    }
                }
                finally { PawnInspectorWidgets.EndScroll(left, ref scroll, height, 180801); }
                positioner.ScrollVertically(ref scroll, left.size);
                Scroll.SetValue(null, scroll); ListHeight.SetValue(null, height);

                StatDrawEntry current = (StatDrawEntry)Selected.GetValue(null) ?? (StatDrawEntry)Hovered.GetValue(null) ?? entries.FirstOrDefault();
                if (current == null) return;
                StatRequest request = current.hasOptionalReq ? current.optionalReq : thing != null ? StatRequest.For(thing) : StatRequest.ForEmpty();
                string explanation = current.GetExplanationText(request) ?? "";
                List<Dialog_InfoCard.Hyperlink> links = current.GetHyperlinks(request)?.ToList();
                float MeasureDetail(float width)
                {
                    float measured = InfoCardPanel.Style().CalcHeight(new GUIContent(explanation), width) + 12;
                    if (links != null) foreach (var link in links) measured += Math.Max(24, InfoCardPanel.Style().CalcHeight(new GUIContent(link.Label), width));
                    return measured;
                }
                float textWidth = Math.Max(1, right.width);
                float detailHeight = MeasureDetail(textWidth);
                // Reserve the slim rail only if content actually overflows, then
                // remeasure text with the resulting exact width.
                if (InfoCardLayout.Overflow(detailHeight, right.height)) { textWidth = Math.Max(1, right.width - 10); detailHeight = MeasureDetail(textWidth); }
                float explanationHeight = InfoCardPanel.Style().CalcHeight(new GUIContent(explanation), textWidth);
                float width = PawnInspectorWidgets.BeginScroll(right, ref rightScroll, detailHeight);
                try
                {
                    InfoCardPanel.Label(new Rect(0, 0, width, explanationHeight), explanation);
                    float y = explanationHeight + 12;
                    if (links != null)
                    {
                        foreach (Dialog_InfoCard.Hyperlink link in links)
                        {
                            float rowHeight = Math.Max(24, InfoCardPanel.Style().CalcHeight(new GUIContent(link.Label), width));
                            Rect row = new Rect(0, y, width, rowHeight);
                            string tooltip = link.HasGeneOwnerThing ? link.Label : "ViewHyperlink".Translate(link.Label).ToString();
                            // Includes Def/Thing/Hediff/Royalty/quest/research/ideo/
                            // gene-owner callbacks, plus native history behavior.
                            Widgets.HyperlinkWithIcon(row, link, tooltip, 2, 6);
                            y += rowHeight;
                        }
                    }
                }
                finally { PawnInspectorWidgets.EndScroll(right, ref rightScroll, detailHeight, 180802); }
                RightScroll.SetValue(null, rightScroll); RightHeight.SetValue(null, detailHeight);
            }
        }

        private static string Value(StatDrawEntry entry, List<string> values, int index) => index < values.Count ? values[index] : entry.ValueString;
        private static float EntryHeight(string label, string value, float width)
        {
            float effective = Math.Max(1, width);
            return Math.Max(21, Math.Max(InfoCardPanel.Style().CalcHeight(new GUIContent(label), effective * .55f),
                InfoCardPanel.Style().CalcHeight(new GUIContent(value), effective * .45f)));
        }
        internal static float MeasuredEntryHeight(string text, float width)
        {
            float measured = InfoCardPanel.Style(Text.WordWrap).CalcHeight(new GUIContent(text ?? ""), Math.Max(1, width));
            if (MeasuringLabel != null) measured = Math.Max(21, Math.Max(measured,
                InfoCardPanel.Style(Text.WordWrap).CalcHeight(new GUIContent(MeasuringLabel), Math.Max(1, MeasuringWidth))));
            return measured;
        }
    }
}
