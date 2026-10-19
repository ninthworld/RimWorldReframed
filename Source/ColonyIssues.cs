using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // A presentation of the native active list. Native updates, reports and actions remain authoritative.
    internal static class ColonyIssues
    {
        private sealed class Entry
        {
            internal Alert Alert;
            internal ColonyIssueKind Kind;
            internal string Label = "", Fitted = "";
            internal float FittedWidth = -1;
            internal int Priority, Index;
            internal bool Current, WasCurrent;
            internal int PreviousOrder;
            internal ColonyIssueGroup PreviousGroup;
            internal readonly ColonyIssueMotion Life = new ColonyIssueMotion();
            internal readonly ColonyIssuePositionMotion Position = new ColonyIssuePositionMotion();
        }

        private static readonly MethodInfo NativeDraw = AccessTools.Method(typeof(Alert), nameof(Alert.DrawAt), new[] { typeof(float), typeof(bool) });
        private static readonly MethodInfo NativeClick = AccessTools.Method(typeof(Alert), "OnClick", Type.EmptyTypes);
        private static readonly Action<Alert> Click = CreateClick();
        private static readonly Dictionary<Type, bool> compatible = new Dictionary<Type, bool>();
        private static readonly Dictionary<Alert, Entry> entries = new Dictionary<Alert, Entry>();
        private static readonly List<Entry> ordered = new List<Entry>();
        private static readonly List<Alert> expired = new List<Alert>();
        private static readonly ColonyIssueMotion shell = new ColonyIssueMotion();
        private static readonly ColonyIssueMotion[] groups = { new ColonyIssueMotion(1), new ColonyIssueMotion(1), new ColonyIssueMotion(1) };
        private static readonly ColonyIssueMotion[] presence = { new ColonyIssueMotion(), new ColonyIssueMotion(), new ColonyIssueMotion() };
        private static readonly string[] headings = { "COLONISTS", "RESOURCES", "ENVIRONMENT" };
        private static readonly int[] counts = new int[3];
        private static readonly float[] groupReveal = new float[3];
        private static Vector2 scroll;
        private static float contentHeight, occupiedHeight, width;
        private static GUIStyle bodyStyle, smallStyle;
        private static double clock;
        private static bool moving;
        private static int toggledGroup = -1;
        private static Rect toggledHeading;

        internal static Func<Alert, ColonyIssueKind?> ExternalKind { get; set; }
        internal static Alert HoveredAlert { get; private set; }
        internal static Rect HoveredRect { get; private set; }
        internal static bool Moving => moving;
        internal static ColonyIssueKind KindFor(Alert alert) => ExternalKind?.Invoke(alert) ?? Describe(alert.GetType());
        internal static void Activate(Alert alert) => Click(alert);
        internal static bool Ready => NativeDraw?.ReturnType == typeof(Rect) && NativeClick?.ReturnType == typeof(void) && Click != null &&
            (int)AlertPriority.Critical == ColonyIssuePolicy.CriticalPriority && (int)AlertPriority.High == ColonyIssuePolicy.HighPriority;

        private static Action<Alert> CreateClick()
        {
            if (NativeClick == null || !NativeClick.IsVirtual || NativeClick.IsStatic) return null;
            try { return (Action<Alert>)Delegate.CreateDelegate(typeof(Action<Alert>), NativeClick); }
            catch (ArgumentException) { return null; }
        }

        internal static bool CanHandle(List<Alert> active)
        {
            if (!Ready || active == null) return false;
            foreach (var alert in active)
            {
                if (alert == null) return false;
                Type type = alert.GetType();
                if (!compatible.TryGetValue(type, out bool supported))
                {
                    MethodInfo draw = AccessTools.Method(type, nameof(Alert.DrawAt), new[] { typeof(float), typeof(bool) });
                    compatible[type] = supported = draw?.DeclaringType == typeof(Alert);
                }
                // A subclass's drawing can carry arbitrary controls. Preserve its whole native stack.
                if (!supported) return false;
            }
            return true;
        }

        private static ColonyIssueKind Describe(Type type)
        {
            for (Type current = type; current != null && current != typeof(Alert); current = current.BaseType)
            {
                if (current.Assembly != typeof(Alert).Assembly) continue;
                ColonyIssueKind kind = ColonyIssuePolicy.Kind(current.Name);
                if (kind.Icon != "warning") return kind;
            }
            return ColonyIssuePolicy.Kind(null);
        }

        internal static float Prepare(List<Alert> active, float availableWidth, float maxHeight, double now)
        {
            HoveredAlert = null; HoveredRect = default(Rect);
            if (!CanHandle(active)) return occupiedHeight = 0;
            clock = now;
            width = Math.Max(1, availableWidth);
            Array.Clear(counts, 0, counts.Length);
            Array.Clear(groupReveal, 0, groupReveal.Length);
            foreach (var entry in entries.Values)
            { entry.WasCurrent = entry.Current; entry.PreviousGroup = entry.Kind.Group; entry.Current = false; }
            for (int index = 0; index < ordered.Count; index++) ordered[index].PreviousOrder = index;
            int currentCount = 0;
            for (int index = 0; index < active.Count; index++)
            {
                Alert alert = active[index];
                if (!alert.Active || ColonyIssuePolicy.SeparateAlert((int)alert.Priority)) continue;
                currentCount++;
                ColonyIssueKind? external = ExternalKind?.Invoke(alert);
                if (!entries.TryGetValue(alert, out Entry entry))
                    entries.Add(alert, entry = new Entry { Alert = alert, Kind = external ?? Describe(alert.GetType()) });
                else if (external.HasValue) entry.Kind = external.Value;
                entry.Current = true; entry.Priority = (int)alert.Priority; entry.Index = index;
                counts[(int)entry.Kind.Group]++;
                string label = alert.Label ?? "";
                if (entry.Label != label) { entry.Label = label; entry.FittedWidth = -1; }
            }
            shell.Set(currentCount > 0, now);
            moving = !shell.Settled;
            foreach (var group in groups) { group.Tick(now); moving |= !group.Settled; }
            for (int group = 0; group < presence.Length; group++)
            {
                presence[group].Set(counts[group] > 0, now);
                groupReveal[group] = presence[group].Value;
                moving |= !presence[group].Settled;
            }
            if (toggledGroup >= 0 && groups[toggledGroup].Settled) toggledGroup = -1;
            expired.Clear(); ordered.Clear();
            foreach (var pair in entries)
            {
                Entry entry = pair.Value;
                entry.Life.Set(entry.Current, now);
                moving |= !entry.Life.Settled;
                if (!entry.Current && entry.Life.Value <= 0) expired.Add(pair.Key);
                else ordered.Add(entry);
            }
            foreach (var alert in expired) entries.Remove(alert);
            ordered.Sort((a, b) =>
            {
                int group = a.Kind.Group.CompareTo(b.Kind.Group);
                if (group != 0) return group;
                int urgency = b.Priority.CompareTo(a.Priority);
                return urgency != 0 ? urgency : a.Index.CompareTo(b.Index);
            });
            // Ignore index shifts caused only by arrivals/removals. A relative
            // inversion among surviving rows is a real priority/order change.
            bool reordered = false;
            int previousOrder = -1;
            foreach (Entry entry in ordered)
            {
                if (!entry.Current || !entry.WasCurrent) continue;
                reordered |= entry.PreviousOrder < previousOrder || entry.PreviousGroup != entry.Kind.Group;
                previousOrder = entry.PreviousOrder;
            }
            float y = 0;
            float precedingPresence = 0;
            for (int group = 0; group < 3; group++)
            {
                if (groupReveal[group] <= 0) continue;
                y += ColonyIssuePolicy.GroupSpacing(precedingPresence, groupReveal[group]);
                precedingPresence = Math.Max(precedingPresence, groupReveal[group]);
                y += ColonyIssuePolicy.HeadingHeight(groupReveal[group]);
                foreach (Entry entry in ordered)
                {
                    if ((int)entry.Kind.Group != group) continue;
                    entry.Position.Follow(y, now, reordered);
                    moving |= !entry.Position.Settled;
                    y += Slot(entry);
                }
            }
            contentHeight = y;
            occupiedHeight = ColonyIssuePolicy.Occupied(contentHeight + 2 * ColonyIssuePolicy.Padding, maxHeight, shell.Value);
            scroll.x = 0;
            scroll.y = Math.Min(scroll.y, Math.Max(0, contentHeight - Math.Max(1, occupiedHeight - 2 * ColonyIssuePolicy.Padding)));
            return occupiedHeight;
        }

        private static float Slot(Entry entry) => (ColonyIssuePolicy.Row + ColonyIssuePolicy.RowGap) *
            ColonyIssuePolicy.RowReveal(entry.Life.Value, groups[(int)entry.Kind.Group].Value, entry.Priority);

        internal static void Draw(Rect bounds, bool input)
        {
            HoveredAlert = null; HoveredRect = default(Rect);
            if (bounds.width <= 1 || bounds.height <= 1 || width <= 1 || occupiedHeight <= 0 || Event.current.type == EventType.Layout) return;
            bool available = input && shell.Open && shell.Settled;
            // Paint-only passes cannot acquire controls, register delayed tips or consume wheel events.
            if (!available && Event.current.type != EventType.Repaint) return;
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = false;
                Theme.RoundRect(bounds, Theme.PanelFill, 6);
                GUI.BeginGroup(bounds);
                try
                {
                    Rect viewport = new Rect(ColonyIssuePolicy.Padding, ColonyIssuePolicy.Padding,
                        Math.Max(1, bounds.width - 2 * ColonyIssuePolicy.Padding),
                        Math.Max(1, bounds.height - 2 * ColonyIssuePolicy.Padding));
                    float bodyWidth = PawnInspectorWidgets.BeginScroll(viewport, ref scroll, contentHeight);
                    try
                    {
                        float y = 0;
                        float precedingPresence = 0;
                        for (int group = 0; group < 3; group++)
                        {
                            if (groupReveal[group] <= 0) continue;
                            y += ColonyIssuePolicy.GroupSpacing(precedingPresence, groupReveal[group]);
                            precedingPresence = Math.Max(precedingPresence, groupReveal[group]);
                            Rect heading = new Rect(0, y, bodyWidth, ColonyIssuePolicy.Heading);
                            Rect screenHeading = new Rect(bounds.x + viewport.x, bounds.y + viewport.y + y - scroll.y,
                                heading.width, heading.height);
                            bool stableReversal = group == toggledGroup && SameRect(screenHeading, toggledHeading);
                            bool headingInput = available && counts[group] > 0 && presence[group].Settled &&
                                (!moving || stableReversal) && FullyVisible(heading, viewport.height);
                            float headingHeight = ColonyIssuePolicy.HeadingHeight(groupReveal[group]);
                            GUI.BeginClip(new Rect(heading.x, heading.y, heading.width, headingHeight));
                            try { DrawHeading(new Rect(0, 0, heading.width, heading.height), group, headingInput, screenHeading); }
                            finally { GUI.EndClip(); }
                            y += headingHeight;
                            foreach (var entry in ordered)
                            {
                                if ((int)entry.Kind.Group != group) continue;
                                float slot = Slot(entry);
                                if (slot <= .001f) continue;
                                float visible = Math.Min(ColonyIssuePolicy.Row, slot);
                                Rect row = new Rect(0, entry.Position.Value, bodyWidth, ColonyIssuePolicy.Row);
                                bool clickable = available && !moving && entry.Current && visible >= ColonyIssuePolicy.Row && FullyVisible(row, viewport.height);
                                if (row.yMax >= scroll.y && row.y <= scroll.y + viewport.height)
                                {
                                    GUI.BeginClip(new Rect(row.x, row.y, row.width, visible));
                                    try { DrawEntry(new Rect(0, 0, row.width, row.height), entry, clickable, bounds, viewport, row); }
                                    finally { GUI.EndClip(); }
                                }
                                y += slot;
                            }
                        }
                    }
                    finally { PawnInspectorWidgets.EndScroll(viewport, ref scroll, contentHeight, 270081); }
                }
                finally { GUI.EndGroup(); }
            }
        }

        private static bool FullyVisible(Rect rect, float height)
            => ColonyIssuePolicy.FullyVisible(rect.y, rect.yMax, scroll.y, scroll.y + height);

        private static bool SameRect(Rect a, Rect b) => Math.Abs(a.x - b.x) < .01f && Math.Abs(a.y - b.y) < .01f &&
            Math.Abs(a.width - b.width) < .01f && Math.Abs(a.height - b.height) < .01f;

        private static void DrawHeading(Rect rect, int group, bool input, Rect screenHeading)
        {
            string count = counts[group].ToString(CultureInfo.InvariantCulture);
            float countWidth = Math.Max(18, PawnInspectorView.Measure(count) + 4);
            float countStart = rect.xMax - 20 - countWidth;
            HudTypography.Heading(new Rect(rect.x, rect.y, countStart - rect.x - 5, rect.height), headings[group], Theme.Ink);
            float ruleEnd = countStart - 5;
            float ruleStart = Math.Min(ruleEnd, rect.x + HudTypography.MeasureHeading(headings[group]) + 6);
            Theme.RoundRect(new Rect(ruleStart, rect.center.y, Math.Max(0, ruleEnd - ruleStart), 1), Theme.Border.ToTransparent(.4f), 0);
            PaintLabel(new Rect(countStart, rect.y, countWidth, rect.height), count, Theme.Muted, true);
            Rect arrow = new Rect(rect.xMax - 13, rect.center.y - 5, 10, 10);
            using (new Theme.GuiScope(true))
            {
                GUI.color = input && Mouse.IsOver(rect) ? Theme.Ink : Theme.Muted.ToTransparent(.75f);
                UI.RotateAroundPivot(180 * groups[group].Value, arrow.center);
                GUI.DrawTexture(arrow, RightHudIcons.Get("chevron"));
            }
            if (!input) return;
            string summary = count + (counts[group] == 1 ? " issue. " : " issues. ");
            TooltipHandler.TipRegion(rect, summary + "Click to expand or collapse.");
            if (Widgets.ButtonInvisible(rect))
            {
                groups[group].Set(!groups[group].Open, clock);
                toggledGroup = group; toggledHeading = screenHeading;
                moving = true;
            }
        }

        private static void DrawEntry(Rect rect, Entry entry, bool input, Rect bounds, Rect viewport, Rect contentRect)
        {
            bool hovered = input && Mouse.IsOver(rect);
            Color urgency = entry.Priority >= (int)AlertPriority.Critical ? Theme.Danger : entry.Priority == (int)AlertPriority.High ? Theme.Accent : Theme.Muted;
            Color face = hovered ? Theme.Hover.ToTransparent(.66f) : Theme.Rgb(DesignTokens.Deep).ToTransparent(.46f);
            Theme.RoundRect(rect, face, 3);
            if (entry.Priority >= (int)AlertPriority.Critical) Theme.RoundRect(new Rect(rect.x, rect.y + 4, 2, rect.height - 8), Theme.Danger, 1);
            using (new Theme.GuiScope(true))
            {
                GUI.color = urgency;
                GUI.DrawTexture(new Rect(6, rect.center.y - 7, 14, 14), RightHudIcons.Get(entry.Kind.Icon));
            }
            Rect label = new Rect(27, 0, Math.Max(1, rect.width - 33), rect.height);
            if (Math.Abs(entry.FittedWidth - label.width) > .01f)
            {
                entry.Fitted = Fit(entry.Label, label.width);
                entry.FittedWidth = label.width;
            }
            // Paint-only labels never register tooltips through a modal or an animated clip.
            PaintLabel(label, entry.Fitted, entry.Current ? Theme.Ink : Theme.Muted);
            if (!input) return;
            if (hovered)
            {
                HoveredAlert = entry.Alert;
                HoveredRect = new Rect(bounds.x + viewport.x + contentRect.x,
                    bounds.y + viewport.y + contentRect.y - scroll.y, contentRect.width, contentRect.height);
                if (entry.Fitted != entry.Label || PawnInspectorView.Measure(entry.Label) > label.width)
                    TooltipHandler.TipRegion(rect, entry.Label);
            }
            if (Widgets.ButtonInvisible(rect)) Click(entry.Alert);
        }

        private static string Fit(string value, float width)
        {
            if (PawnInspectorView.Measure(value) <= width) return value;
            // Do not cut native/mod rich-text tags in half; the row clip and full tooltip retain its content.
            if (value.IndexOf('<') >= 0) return value;
            if (PawnInspectorView.Measure("…") > width) return "";
            int[] elements = StringInfo.ParseCombiningCharacters(value);
            int low = 0, high = elements.Length;
            while (low < high)
            {
                int count = (low + high + 1) / 2;
                string candidate = value.Substring(0, count == elements.Length ? value.Length : elements[count]) + "…";
                if (PawnInspectorView.Measure(candidate) <= width) low = count; else high = count - 1;
            }
            return value.Substring(0, low == elements.Length ? value.Length : elements[low]) + "…";
        }

        private static void PaintLabel(Rect rect, string value, Color color, bool small = false)
        {
            using (new Theme.GuiScope(true))
            {
                Text.Font = GameFont.Small;
                if (bodyStyle == null || bodyStyle.font != Text.CurFontStyle.font)
                {
                    bodyStyle = new GUIStyle(Text.CurFontStyle) { fontSize = HudTypography.BodySize, alignment = TextAnchor.MiddleLeft,
                        wordWrap = false, clipping = TextClipping.Clip, padding = new RectOffset(), margin = new RectOffset() };
                    bodyStyle.normal.textColor = Color.white;
                    smallStyle = new GUIStyle(bodyStyle) { fontSize = 10, alignment = TextAnchor.MiddleRight };
                }
                GUI.color = color;
                GUI.Label(rect, value, small ? smallStyle : bodyStyle);
            }
        }

        internal static void Reset()
        {
            entries.Clear(); ordered.Clear(); expired.Clear(); shell.Reset();
            foreach (var group in groups) group.Reset(1);
            foreach (var group in presence) group.Reset();
            Array.Clear(counts, 0, counts.Length);
            Array.Clear(groupReveal, 0, groupReveal.Length);
            scroll = Vector2.zero; contentHeight = occupiedHeight = width = 0; moving = false;
            toggledGroup = -1; toggledHeading = default(Rect);
            HoveredAlert = null; HoveredRect = default(Rect);
        }
    }
}
