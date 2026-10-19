using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using NativeLine = RimWorld.ITab_Pawn_Log_Utility.LogLineDisplayable;
using NativeHeader = RimWorld.ITab_Pawn_Log_Utility.LogLineDisplayableHeader;
using NativeLog = RimWorld.ITab_Pawn_Log_Utility.LogLineDisplayableLog;
using NativeGap = RimWorld.ITab_Pawn_Log_Utility.LogLineDisplayableGap;

namespace ReframedUI
{
    internal static class PawnLogPanel
    {
        private static readonly FieldInfo HeaderText = AccessTools.Field(typeof(NativeHeader), "text");
        private static readonly FieldInfo EntryField = AccessTools.Field(typeof(NativeLog), "log");
        private static readonly FieldInfo PovField = AccessTools.Field(typeof(NativeLog), "pawn");
        private static readonly FieldInfo HighlightAlpha = AccessTools.Field(typeof(ITab_Pawn_Log_Utility), "HighlightAlpha");
        private static readonly FieldInfo HighlightDuration = AccessTools.Field(typeof(ITab_Pawn_Log_Utility), "HighlightDuration");
        private static readonly FieldInfo Interaction = AccessTools.Field(typeof(PlayLogEntry_Interaction), "intDef");
        private static readonly FieldInfo SingleInteraction = AccessTools.Field(typeof(PlayLogEntry_InteractionSinglePawn), "intDef");
        private static readonly List<Row> rows = new List<Row>();
        private static Pawn owner;
        private static ITab_Pawn_Log ownerTab;
        private static PlayLog playLog;
        private static BattleLog battleLog;
        private static int battleTick = -1, playTick = -1;
        private static bool all, combat, social;
        private static Vector2 scroll;
        internal static Action OpenOptions = null;
        internal static bool Ready => HeaderText?.FieldType == typeof(TaggedString) &&
            EntryField?.FieldType == typeof(LogEntry) && PovField?.FieldType == typeof(Pawn) &&
            HighlightAlpha?.FieldType == typeof(float) && HighlightDuration?.FieldType == typeof(float) &&
            Interaction?.FieldType == typeof(InteractionDef) && SingleInteraction?.FieldType == typeof(InteractionDef);

        private enum Kind { Header, Entry, Gap, Native }
        private sealed class Row
        {
            internal NativeLine Native;
            internal Kind Kind;
            internal LogEntry Entry;
            internal Pawn Pov;
            internal string Text, Title;
            private float measuredWidth = -1f, height;
            internal float Height(float width)
            {
                if (width != measuredWidth)
                {
                    measuredWidth = width;
                    switch (Kind)
                    {
                        case Kind.Header: height = PawnLogLayout.HeadingHeight; break;
                        case Kind.Gap: height = PawnLogLayout.BattleGap; break;
                        case Kind.Entry: height = PawnLogLayout.RowHeight(PawnLogText.Height(Text, width - 16f)); break;
                        default:
                            using (new Theme.GuiScope(true))
                            { TextDefaults(); height = Math.Max(0f, Native.GetHeight(width)); }
                            break;
                    }
                }
                return height;
            }
            internal float Extent(float width) => Height(width) + (Kind == Kind.Entry ? PawnLogLayout.RowGap : 0f);
        }

        internal static void Reset()
        {
            owner = null; ownerTab = null; playLog = null; battleLog = null;
            rows.Clear(); scroll = Vector2.zero; battleTick = playTick = -1;
        }

        internal static void Draw(Rect rect, Pawn pawn, ITab_Pawn_Log tab)
        {
            if (pawn == null || tab == null || !Ready) return;
            if (owner != pawn || ownerTab != tab)
            { Reset(); owner = pawn; ownerTab = tab; }
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                TextDefaults();
                bool changed = DrawToolbar(new Rect(rect.x, rect.y, rect.width, PawnLogLayout.ToolbarHeight), tab);
                Refresh(pawn, tab, changed);
                Rect viewport = new Rect(rect.x, rect.y + PawnLogLayout.ToolbarHeight + PawnLogLayout.ToolbarGap,
                    rect.width, Math.Max(1f, rect.height - PawnLogLayout.ToolbarHeight - PawnLogLayout.ToolbarGap));
                if (rows.Count == 0)
                {
                    PawnInspectorView.SmallLabel(new Rect(viewport.x + 8f, viewport.y + 8f, viewport.width - 16f, 20f),
                        "NoRecentEntries".Translate().CapitalizeFirst(), Theme.Muted);
                    tab.logSeek = null;
                    return;
                }
                float width = viewport.width;
                float height = Measure(width);
                if (height > viewport.height + .01f) { width -= 10f; height = Measure(width); }
                if (tab.logSeek != null)
                {
                    float top = 0;
                    foreach (Row row in rows)
                    {
                        if (row.Native.Matches(tab.logSeek))
                        { scroll.y = PawnLogLayout.CenteredScroll(top, row.Height(width), viewport.height, height); break; }
                        top += row.Extent(width);
                    }
                    tab.logSeek = null;
                }
                tab.data.StartNewDraw();
                float contentWidth = PawnInspectorWidgets.BeginScroll(viewport, ref scroll, height);
                try
                {
                    float top = 0;
                    foreach (Row row in rows)
                    {
                        float rowHeight = row.Height(contentWidth);
                        if (PawnLogLayout.Visible(top, rowHeight, scroll.y, viewport.height))
                            DrawRow(row, new Rect(0, top, contentWidth, rowHeight), tab);
                        top += row.Extent(contentWidth);
                    }
                }
                finally { PawnInspectorWidgets.EndScroll(viewport, ref scroll, height, 731017); }
            }
        }

        private static void TextDefaults()
        { Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true; }

        private static float Measure(float width)
        {
            float height = 0;
            foreach (Row row in rows) height += row.Extent(width);
            return Math.Max(0f, height - (rows.Count > 0 && rows[rows.Count - 1].Kind == Kind.Entry ? PawnLogLayout.RowGap : 0f));
        }

        private static bool DrawToolbar(Rect rect, ITab_Pawn_Log tab)
        {
            float copyWidth = Math.Max(46f, Math.Min(76f, PawnInspectorView.Measure("Copy".Translate()) + 16f));
            bool cog = OpenOptions != null;
            float filterWidth = PawnLogLayout.FilterWidth(rect.width, copyWidth, cog);
            Rect filter = new Rect(rect.x, rect.y, filterWidth, rect.height);
            bool changed = Toggle(filter, "ShowAll", ref tab.showAll,
                "Include every combat detail, including entries hidden in the compact native log.");
            filter.x += filter.width + 6f;
            changed |= Toggle(filter, "ShowCombat", ref tab.showCombat);
            filter.x += filter.width + 6f;
            changed |= Toggle(filter, "ShowSocial", ref tab.showSocial);
            Rect copy = new Rect(filter.xMax + 6f, rect.y, copyWidth, rect.height);
            TooltipHandler.TipRegionByKey(copy, "CopyLogTip");
            if (PawnInspectorWidgets.Button(copy, "Copy".Translate()))
            {
                // Copy the currently requested filters, including on the first draw.
                Refresh(owner, tab, changed);
                var output = new StringBuilder();
                foreach (Row row in rows) row.Native.AppendTo(output);
                GUIUtility.systemCopyBuffer = output.ToString();
            }
            if (cog && PawnInspectorWidgets.IconButton(new Rect(copy.xMax + 6f, rect.y, 24f, rect.height),
                "settings", "Log options")) OpenOptions?.Invoke();
            return changed;
        }

        private static bool Toggle(Rect rect, string key, ref bool value, string explanation = null)
        {
            Theme.RoundRect(rect, value ? Theme.Border : Theme.Border.ToTransparent(.5f), 3f);
            Theme.RoundRect(rect.ContractedBy(1f), Mouse.IsOver(rect) ? Theme.Hover : Theme.Surface, 2f);
            Rect indicator = new Rect(rect.x + 7f, rect.center.y - 3.5f, 7f, 7f);
            Theme.RoundRect(indicator, value ? Theme.Accent : Theme.Muted.ToTransparent(.3f), 1f);
            string label = key.Translate();
            PawnInspectorView.Label(new Rect(rect.x + 20f, rect.y, rect.width - 26f, rect.height), label,
                value ? Theme.Ink : Theme.Muted);
            TooltipHandler.TipRegion(rect, explanation == null ? label : label + "\n" + explanation);
            if (!Widgets.ButtonInvisible(rect)) return false;
            value = !value;
            return true;
        }

        private static void Refresh(Pawn pawn, ITab_Pawn_Log tab, bool force)
        {
            int currentBattle = pawn.records?.LastBattleTick ?? -1;
            int currentPlay = Find.PlayLog.LastTick;
            if (!force && owner == pawn && ownerTab == tab && playLog == Find.PlayLog && battleLog == Find.BattleLog &&
                battleTick == currentBattle && playTick == currentPlay && all == tab.showAll && combat == tab.showCombat && social == tab.showSocial) return;
            bool filtersChanged = all != tab.showAll || combat != tab.showCombat || social != tab.showSocial;
            all = tab.showAll; combat = tab.showCombat; social = tab.showSocial;
            playLog = Find.PlayLog; battleLog = Find.BattleLog; battleTick = currentBattle; playTick = currentPlay;
            if (filtersChanged) scroll = Vector2.zero;
            rows.Clear();
            // GenerateLogLinesFor owns native filtering, battle grouping, chronology
            // and actual per-entry POV. It returns a shared temporary list: copy now.
            var generated = new List<NativeLine>(ITab_Pawn_Log_Utility.GenerateLogLinesFor(
                pawn, tab.showAll, tab.showCombat, tab.showSocial, PawnLogLayout.EntryLimit));
            var playEntries = new HashSet<LogEntry>(playLog.AllEntries);
            string battleTitle = null;
            foreach (NativeLine native in generated)
            {
                var row = new Row { Native = native, Kind = Kind.Native };
                if (native.GetType() == typeof(NativeHeader))
                { row.Kind = Kind.Header; row.Text = ((TaggedString)HeaderText.GetValue(native)).ToString(); battleTitle = row.Text; }
                else if (native.GetType() == typeof(NativeGap)) { row.Kind = Kind.Gap; battleTitle = null; }
                else if (native.GetType() == typeof(NativeLog))
                {
                    row.Entry = (LogEntry)EntryField.GetValue(native); row.Pov = (Pawn)PovField.GetValue(native);
                    if (row.Entry != null)
                    {
                        row.Kind = Kind.Entry; row.Text = row.Entry.ToGameStringFromPOV(row.Pov);
                        // Titles describe real native data: the actual interaction,
                        // labeled entry definition or enclosing battle. Unknown play
                        // events retain the neutral Log title and their full narrative.
                        InteractionDef interaction = row.Entry is PlayLogEntry_Interaction ? (InteractionDef)Interaction.GetValue(row.Entry) :
                            row.Entry is PlayLogEntry_InteractionSinglePawn ? (InteractionDef)SingleInteraction.GetValue(row.Entry) : null;
                        row.Title = interaction != null ? interaction.LabelCap.ToString() : !string.IsNullOrEmpty(row.Entry.def?.label) ? row.Entry.def.LabelCap.ToString() :
                            !playEntries.Contains(row.Entry) && battleTitle != null ? battleTitle : "TabLog".Translate().ToString();
                    }
                }
                rows.Add(row);
            }
            // The native fallback must regenerate rather than reuse utility scratch
            // storage that another Social/Log panel may have changed meanwhile.
            tab.cachedLogDisplay = null;
        }

        private static void DrawRow(Row row, Rect rect, ITab_Pawn_Log tab)
        {
            if (row.Kind == Kind.Gap) return;
            if (row.Kind == Kind.Header)
            {
                Rect heading = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, 16f);
                GUI.BeginGroup(heading);
                try { PawnInspectorView.Heading(new Rect(0, 0, heading.width, heading.height), row.Text.ToUpperInvariant()); }
                finally { GUI.EndGroup(); }
                TooltipHandler.TipRegion(rect, row.Text);
                return;
            }
            if (row.Kind == Kind.Native)
            {
                // Unrecognized mod extensions keep their own rendering and input.
                using (new Theme.GuiScope(true)) { TextDefaults(); row.Native.Draw(rect.y, rect.width, tab.data); }
                return;
            }
            bool hover = Mouse.IsOver(rect);
            Theme.RoundRect(rect, Theme.Border.ToTransparent(.55f), 3f);
            Theme.RoundRect(rect.ContractedBy(1f), hover ? Theme.Hover : PawnInspectorView.Content, 2f);
            if (row.Entry == tab.data.highlightEntry)
            {
                float alpha = (float)HighlightAlpha.GetValue(null), duration = (float)HighlightDuration.GetValue(null);
                Theme.RoundRect(rect, Theme.Ink.ToTransparent(alpha * tab.data.highlightIntensity), 3f);
                tab.data.highlightIntensity = Math.Max(0f, tab.data.highlightIntensity - Time.deltaTime / Math.Max(.01f, duration));
            }
            tab.data.alternatingBackground = !tab.data.alternatingBackground;
            Texture2D icon = row.Entry.IconFromPOV(row.Pov);
            if (icon != null)
            {
                using (new Theme.GuiScope(true))
                {
                    GUI.color = row.Entry.IconColorFromPOV(row.Pov) ?? Color.white;
                    GUI.DrawTexture(new Rect(rect.x + 8f, rect.y + 7f, 15f, 15f), icon, ScaleMode.ScaleToFit);
                }
            }
            string age = row.Entry.Age.ToStringTicksToPeriod();
            float timeWidth = PawnLogLayout.TimestampWidth(PawnLogText.Width(age), rect.width);
            PawnInspectorView.Label(new Rect(rect.x + 30f, rect.y + 3f,
                PawnLogLayout.TitleWidth(rect.width, timeWidth), 21f), row.Title, Theme.Ink);
            PawnInspectorView.SmallLabel(new Rect(rect.xMax - timeWidth - 8f, rect.y + 4f, timeWidth, 20f), age, Theme.Muted, true);
            PawnLogText.Paragraph(new Rect(rect.x + 8f, rect.y + 25f, rect.width - 16f, rect.height - 30f), row.Text, Theme.Muted);
            if (hover)
            {
                TooltipHandler.TipRegion(rect, row.Entry.GetTipString, unchecked(613261 + row.Entry.LogID * 2063));
                if (DebugViewSettings.logCombatLogMouseover) row.Entry.ToGameStringFromPOV(row.Pov, true);
            }
            if (Widgets.ButtonInvisible(rect, row.Entry.CanBeClickedFromPOV(row.Pov))) row.Entry.ClickedFromPOV(row.Pov);
        }
    }
}
