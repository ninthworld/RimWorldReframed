using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Keep the real inspect pane and its high/low-priority string, conditional
    // controls, storage-group dispatch and mod additions. Only paint is scoped.
    internal static class ObjectInspectorDescription
    {
        internal static bool StringReady, HealthReady, BarReady;
        internal static bool Ready => StringReady && HealthReady && BarReady;
        internal static bool Drawing { get; private set; }
        private static bool suppressBar, sawString;
        private static Vector2 position;
        private static GUIStyle body, meta;

        internal static void Draw(Rect rect, MainTabWindow_Inspect pane, Thing thing, ref Vector2 scroll)
        {
            if (!Ready || pane == null || pane.GetType() != typeof(MainTabWindow_Inspect) || thing == null || thing.Destroyed ||
                Find.Selector.SingleSelectedThing != thing)
            { pane?.DoPaneContents(rect); scroll = Vector2.zero; return; }
            bool previousDrawing = Drawing, previousSaw = sawString;
            Vector2 previousPosition = position;
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                Drawing = true; sawString = false; position = scroll;
                try
                {
                    // No estimated outer scroll. The native filler supplies its
                    // exact remaining text rectangle after the real widget row.
                    pane.DoPaneContents(rect);
                    scroll = sawString ? position : Vector2.zero;
                }
                finally { Drawing = previousDrawing; sawString = previousSaw; position = previousPosition; }
            }
        }

        internal static GUIStyle Style(bool wrap)
        {
            if (body == null)
            {
                using (new Theme.GuiScope(true))
                {
                    Text.Font = GameFont.Small;
                    body = new GUIStyle(Text.CurFontStyle) { fontSize = 11, richText = true,
                        padding = new RectOffset(), margin = new RectOffset(), clipping = TextClipping.Clip };
                    body.normal.textColor = Color.white;
                    meta = new GUIStyle(body) { fontSize = 10 };
                }
            }
            GUIStyle style = Text.Font == GameFont.Tiny ? meta : body;
            style.wordWrap = wrap; style.alignment = Text.Anchor;
            return style;
        }
        internal static void Label(Rect rect, string text) => GUI.Label(rect, text ?? "", Style(Text.WordWrap));

        internal static void InspectString(Rect rect, string text, ref Vector2 nativePosition,
            bool dontConsumeScrollEventsIfNoScrollbar, bool takeScrollbarSpaceEvenIfNoScrollbar, bool longLabel)
        {
            if (!Drawing || longLabel || !dontConsumeScrollEventsIfNoScrollbar)
            {
                Widgets.LabelScrollable(rect, text, ref nativePosition, dontConsumeScrollEventsIfNoScrollbar,
                    takeScrollbarSpaceEvenIfNoScrollbar, longLabel);
                return;
            }
            sawString = true;
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                text = text ?? "";
                float width = Math.Max(1, rect.width);
                float height = Style(true).CalcHeight(new GUIContent(text), width);
                if (!ObjectInspectorDescriptionLayout.Overflow(height, rect.height))
                {
                    // Short content gets the entire width, consumes no wheel
                    // event, and cannot retain a previous object's scroll offset.
                    position = nativePosition = Vector2.zero;
                    GUI.BeginGroup(rect);
                    try { Label(new Rect(0, 0, width, height), text); }
                    finally { GUI.EndGroup(); }
                    return;
                }
                width = Math.Max(1, rect.width - 10);
                height = Style(true).CalcHeight(new GUIContent(text), width);
                ObjectInspectorDescriptionLayout.Clamp(ref position.x, ref position.y, height, rect.height);
                float actualWidth = PawnInspectorWidgets.BeginScroll(rect, ref position, height);
                try { Label(new Rect(0, 0, actualWidth, height), text); }
                finally { PawnInspectorWidgets.EndScroll(rect, ref position, height, 190301); }
                nativePosition = position;
            }
        }

        internal static Rect HealthMeter(WidgetRow row, float width, float height, float fraction, string value,
            Texture2D foreground, Texture2D background)
        {
            if (!Drawing) return row.FillableBar(width, height, fraction, value, foreground, background);
            Rect slot;
            bool previous = suppressBar;
            suppressBar = true;
            try
            {
                // Let WidgetRow allocate and advance the EXACT native slot.
                // Preserve custom siblings and the area selector's click geometry.
                slot = row.FillableBar(width, height, fraction, null, foreground, background);
            }
            finally { suppressBar = previous; }
            using (new Theme.GuiScope(true))
            {
                Color numericColor = GUI.color;
                GUI.color = GUI.contentColor = Color.white;
                float valueWidth = Math.Min(width * .58f, Math.Max(36, PawnInspectorView.MeasureSmall(value ?? "") + 3));
                float barWidth = Math.Max(1, width - valueWidth - 5);
                PawnInspectorView.Bar(new Rect(slot.x, slot.center.y - 5.5f, barWidth, 11),
                    ObjectInspectorDescriptionLayout.Ratio(fraction), Theme.Danger);
                PawnInspectorView.SmallLabel(new Rect(slot.xMax - valueWidth, slot.y, valueWidth, slot.height), value, numericColor, true);
                TooltipHandler.TipRegion(slot, "Health".Translate() + ": " + value);
            }
            return slot;
        }
        internal static Rect Fill(Rect rect, float fraction, Texture2D foreground, Texture2D background, bool drawBorder)
        {
            if (Drawing && suppressBar) return rect;
            return Widgets.FillableBar(rect, fraction, foreground, background, drawBorder);
        }
    }
}
