using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // A presentation-only chart built from actual recorder arrays. No native
    // recorder, tale, graph-selection or game-history state is replaced.
    internal static class WorldMenusHistoryChart
    {
        internal static bool Dropdown(Rect rect, string label)
        {
            bool hover = GUI.enabled && Mouse.IsOver(rect);
            Theme.ButtonFace(rect, hover ? Theme.Hover : Theme.Surface, !GUI.enabled);
            PawnInspectorView.Label(new Rect(rect.x + 9, rect.y, rect.width - 33, rect.height), label, GUI.enabled ? Theme.Ink : Theme.Muted);
            if (Event.current.type == EventType.Repaint)
            {
                Color ink = GUI.enabled ? Theme.Ink : Theme.Muted;
                Vector2 center = new Vector2(rect.xMax - 13, rect.center.y);
                Widgets.DrawLine(new Vector2(center.x - 3, center.y - 1.5f), new Vector2(center.x, center.y + 1.5f), ink, 1.2f);
                Widgets.DrawLine(new Vector2(center.x, center.y + 1.5f), new Vector2(center.x + 3, center.y - 1.5f), ink, 1.2f);
            }
            return GUI.enabled && Widgets.ButtonInvisible(rect);
        }
        internal static bool Segment(Rect rect, string label, bool selected)
        {
            bool hover = GUI.enabled && Mouse.IsOver(rect);
            if (selected || hover) Theme.RoundRect(rect.ContractedBy(2), selected ? Theme.Selected.ToTransparent(.75f) : Theme.Hover, 3);
            if (selected) Theme.RoundRect(new Rect(rect.x + 9, rect.yMax - 3, Math.Max(1, rect.width - 18), 1), Theme.Accent.ToTransparent(.8f), 0);
            PawnInspectorView.Label(rect.ContractedBy(8, 0), label, selected ? Theme.Ink : Theme.Muted, selected);
            return GUI.enabled && Widgets.ButtonInvisible(rect);
        }
        internal static bool Filter(Rect rect, string label, bool selected)
        {
            bool hover = GUI.enabled && Mouse.IsOver(rect);
            Theme.RoundRect(rect, hover ? Theme.Hover : Theme.Surface.ToTransparent(.6f), 4);
            Rect box = new Rect(rect.x + 8, rect.center.y - 5, 10, 10);
            Theme.RoundRect(box, selected ? Theme.Accent : Theme.Muted.ToTransparent(.7f), 2);
            Theme.RoundRect(box.ContractedBy(1), Theme.Surface, 1);
            if (selected && Event.current.type == EventType.Repaint)
            {
                Widgets.DrawLine(new Vector2(box.x + 2, box.y + 5), new Vector2(box.x + 4, box.y + 7), Theme.Ink, 1.2f);
                Widgets.DrawLine(new Vector2(box.x + 4, box.y + 7), new Vector2(box.x + 8, box.y + 3), Theme.Ink, 1.2f);
            }
            PawnInspectorView.Label(new Rect(rect.x + 26, rect.y, rect.width - 32, rect.height), label, selected ? Theme.Ink : Theme.Muted);
            return GUI.enabled && Widgets.ButtonInvisible(rect);
        }
        internal static void Draw(Rect rect, HistoryAutoRecorderGroup group, FloatRange nativeSection, List<CurveMark> marks, ref Vector2 legendPosition)
        {
            WorldMenus.Surface(rect);
            List<HistoryAutoRecorder> series = group.recorders.Where(recorder => recorder?.def != null && recorder.records != null).ToList();
            int columns = Math.Max(1, (int)((rect.width - 24) / 170));
            float legendTotal = (float)WorldMenusHistoryGraphPolicy.LegendTotal(series.Count, columns);
            float legendHeight = Math.Min(63, legendTotal);
            Rect legend = new Rect(rect.x + 12, rect.yMax - legendHeight - 7, rect.width - 24, legendHeight);
            Rect plot = new Rect(rect.x + 61, rect.y + 44, Math.Max(1, rect.width - 78), (float)WorldMenusHistoryGraphPolicy.PlotHeight(rect.height, legendHeight));
            var xRange = WorldMenusHistoryGraphPolicy.Section(nativeSection.min, nativeSection.max);
            var yRange = WorldMenusHistoryGraphPolicy.Bounds(series.Select(s => (IReadOnlyList<float>)s.records), group.def.useFixedScale,
                group.def.fixedScale.x, group.def.fixedScale.y, group.def.onlyPositiveValues);
            if (Event.current.type == EventType.Repaint)
            {
                using (new Theme.GuiScope(true))
                {
                    GUI.color = GUI.contentColor = Color.white;
                    DrawGrid(plot, xRange, yRange, group.def.integersOnly);
                    DrawMarks(plot, xRange, marks);
                    foreach (HistoryAutoRecorder recorder in series) DrawSeries(plot, xRange, yRange, recorder);
                    DrawReadout(rect, plot, xRange, yRange, series);
                    string day = Find.History.curveDrawerStyle.LabelX ?? "Day";
                    PawnInspectorView.SmallLabel(new Rect(plot.x, plot.yMax + 19, plot.width, 14), day, Theme.Muted, true);
                    if (!series.Any(s => s.records.Count > 0))
                        PawnInspectorView.Label(new Rect(plot.x + 12, plot.center.y - 10, plot.width - 24, 20), "No graph records", Theme.Muted);
                }
            }
            if (legendHeight > 0) DrawLegend(legend, legendTotal, columns, series, ref legendPosition);
        }
        private static void DrawGrid(Rect plot, WorldMenusHistoryGraphPolicy.Scale xRange, WorldMenusHistoryGraphPolicy.Scale yRange, bool integers)
        {
            Color grid = Theme.Border.ToTransparent(.42f), ink = Theme.Muted;
            double stepX = WorldMenusHistoryGraphPolicy.TickStep(xRange, 5, true);
            double startX = Math.Ceiling(xRange.Min / stepX) * stepX;
            for (int i = 0; i < 20; i++)
            {
                double day = startX + i * stepX; if (day > xRange.Max + stepX * .0001) break;
                float x = plot.x + (float)WorldMenusHistoryGraphPolicy.Unit(day, xRange) * plot.width;
                Theme.RoundRect(new Rect(x, plot.y, 1, plot.height), grid, 0);
                Label(new Rect(x - 28, plot.yMax + 4, 56, 14), day.ToString("0.##"), ink, TextAnchor.UpperCenter);
            }
            double stepY = WorldMenusHistoryGraphPolicy.TickStep(yRange, 4, integers);
            double startY = Math.Ceiling(yRange.Min / stepY) * stepY;
            for (int i = 0; i < 20; i++)
            {
                double value = startY + i * stepY; if (value > yRange.Max + stepY * .0001) break;
                float y = plot.yMax - (float)WorldMenusHistoryGraphPolicy.Unit(value, yRange) * plot.height;
                Theme.RoundRect(new Rect(plot.x, y, plot.width, 1), grid, 0);
                Label(new Rect(plot.x - 56, y - 7, 48, 14), Axis(value), ink, TextAnchor.MiddleRight);
            }
            Theme.RoundRect(new Rect(plot.x, plot.y, 1, plot.height), Theme.Border.ToTransparent(.8f), 0);
            Theme.RoundRect(new Rect(plot.x, plot.yMax, plot.width, 1), Theme.Border.ToTransparent(.8f), 0);
        }
        private static string Axis(double value)
        {
            double absolute = Math.Abs(value);
            if (absolute >= 1000000000) return (value / 1000000000d).ToString("0.##") + "G";
            if (absolute >= 1000000) return (value / 1000000d).ToString("0.##") + "M";
            if (absolute >= 10000) return (value / 1000d).ToString("0.##") + "k";
            return value.ToString(absolute > 0 && absolute < .01 ? "0.####" : "0.##");
        }
        private static void DrawSeries(Rect plot, WorldMenusHistoryGraphPolicy.Scale xRange, WorldMenusHistoryGraphPolicy.Scale yRange, HistoryAutoRecorder recorder)
        {
            var points = WorldMenusHistoryGraphPolicy.VisiblePoints(recorder.records, recorder.def.recordTicksFrequency, xRange, Math.Max(1, (int)plot.width));
            for (int i = 1; i < points.Count; i++)
            {
                double x1 = WorldMenusHistoryGraphPolicy.Unit(points[i - 1].Day, xRange), x2 = WorldMenusHistoryGraphPolicy.Unit(points[i].Day, xRange);
                double y1 = WorldMenusHistoryGraphPolicy.Unit(points[i - 1].Value, yRange), y2 = WorldMenusHistoryGraphPolicy.Unit(points[i].Value, yRange);
                if (!WorldMenusHistoryGraphPolicy.Clip(ref x1, ref y1, ref x2, ref y2)) continue;
                if (x1 == x2 && y1 == y2) continue;
                Widgets.DrawLine(new Vector2(plot.x + (float)x1 * plot.width, plot.yMax - (float)y1 * plot.height),
                    new Vector2(plot.x + (float)x2 * plot.width, plot.yMax - (float)y2 * plot.height), recorder.def.graphColor, 1.5f);
            }
        }
        private static void DrawMarks(Rect plot, WorldMenusHistoryGraphPolicy.Scale xRange, List<CurveMark> marks)
        {
            foreach (CurveMark mark in marks)
            {
                if (mark.X < xRange.Min || mark.X > xRange.Max) continue;
                float x = plot.x + (float)WorldMenusHistoryGraphPolicy.Unit(mark.X, xRange) * plot.width;
                Rect hit = new Rect(x - 5, plot.y - 14, 10, 12);
                Theme.RoundRect(new Rect(x - 2.5f, plot.y - 10, 5, 5), mark.Color, 2);
                Theme.RoundRect(new Rect(x, plot.y, 1, plot.height), mark.Color.ToTransparent(.13f), 0);
                if (GUI.enabled && Mouse.IsOver(hit)) TooltipHandler.TipRegion(hit, mark.Message);
            }
        }
        private static void DrawReadout(Rect rect, Rect plot, WorldMenusHistoryGraphPolicy.Scale xRange, WorldMenusHistoryGraphPolicy.Scale yRange, List<HistoryAutoRecorder> series)
        {
            bool hover = GUI.enabled && Mouse.IsOver(plot);
            string dayLabel = Find.History.curveDrawerStyle.LabelX ?? "Day";
            string caption = dayLabel + " " + xRange.Min.ToString("0.##") + " – " + xRange.Max.ToString("0.##");
            Color color = Theme.Muted;
            if (hover)
            {
                double day = xRange.Min + (Event.current.mousePosition.x - plot.x) / plot.width * xRange.Span;
                HistoryAutoRecorder nearest = null; double value = 0, distance = double.PositiveInfinity;
                foreach (HistoryAutoRecorder recorder in series)
                {
                    double candidate = WorldMenusHistoryGraphPolicy.Evaluate(recorder.records, recorder.def.recordTicksFrequency, day);
                    if (!WorldMenusHistoryGraphPolicy.Finite(candidate)) continue;
                    double y = plot.yMax - WorldMenusHistoryGraphPolicy.Unit(candidate, yRange) * plot.height;
                    double currentDistance = Math.Abs(y - Event.current.mousePosition.y);
                    if (currentDistance < distance) { nearest = recorder; value = candidate; distance = currentDistance; }
                }
                if (nearest != null)
                {
                    color = nearest.def.graphColor;
                    caption = nearest.def.LabelCap + "  ·  " + dayLabel + " " + day.ToString("0.##") + "  ·  " + Format(nearest, value);
                    float x = Event.current.mousePosition.x;
                    Theme.RoundRect(new Rect(x, plot.y, 1, plot.height), Theme.Ink.ToTransparent(.28f), 0);
                    double unit = WorldMenusHistoryGraphPolicy.Unit(value, yRange);
                    if (unit >= 0 && unit <= 1)
                    {
                        float y = plot.yMax - (float)unit * plot.height;
                        Theme.RoundRect(new Rect(x - 3, y - 3, 6, 6), color, 3);
                    }
                    TooltipHandler.TipRegion(plot, caption);
                }
            }
            PawnInspectorView.Label(new Rect(rect.x + 12, rect.y + 5, rect.width - 24, 20), caption, color);
        }
        private static string Format(HistoryAutoRecorder recorder, double value)
        {
            string number = value.ToString("0.##");
            try { return recorder.def.valueFormat.NullOrEmpty() ? number : string.Format(recorder.def.valueFormat, number); }
            catch (FormatException) { return number; }
        }
        private static void DrawLegend(Rect rect, float total, int columns, List<HistoryAutoRecorder> series, ref Vector2 position)
        {
            float width = PawnInspectorWidgets.BeginScroll(rect, ref position, total);
            try
            {
                float cell = width / columns;
                for (int i = 0; i < series.Count; i++)
                {
                    HistoryAutoRecorder recorder = series[i];
                    float x = i % columns * cell, y = i / columns * 21;
                    Rect entry = new Rect(x, y, cell - 4, 20);
                    bool input = GUI.enabled && y >= position.y && y + 20 <= position.y + rect.height;
                    Vector2 mouse = Event.current.mousePosition;
                    if (!input) Event.current.mousePosition = new Vector2(-100000, -100000);
                    try
                    {
                        Theme.RoundRect(new Rect(x + 1, y + 8, 13, 2), recorder.def.graphColor, 1);
                        PawnInspectorView.SmallLabel(new Rect(x + 21, y, Math.Max(1, cell - 25), 20), recorder.def.LabelCap, Theme.Ink);
                        if (input) TooltipHandler.TipRegion(entry, recorder.def.LabelCap + (recorder.def.description.NullOrEmpty() ? "" : "\n\n" + recorder.def.description));
                    }
                    finally { Event.current.mousePosition = mouse; }
                }
            }
            finally { PawnInspectorWidgets.EndScroll(rect, ref position, total, 310341); }
        }
        private static GUIStyle axis;
        private static void Label(Rect rect, string text, Color color, TextAnchor anchor)
        {
            if (axis == null || axis.font != Text.CurFontStyle.font)
                axis = new GUIStyle(Text.CurFontStyle) { fontSize = 10, wordWrap = false, clipping = TextClipping.Clip, richText = false,
                    padding = new RectOffset(), margin = new RectOffset() };
            using (new Theme.GuiScope(true))
            { GUI.color = color; GUI.contentColor = Color.white; axis.alignment = anchor; GUI.Label(rect, text, axis); }
        }
    }
}
