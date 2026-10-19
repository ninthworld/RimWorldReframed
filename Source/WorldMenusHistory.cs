using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ReframedUI
{
    internal static class WorldMenusHistory
    {
        private sealed class State
        {
            internal int Page;
            internal Vector2 Entries, Details, Statistics;
            internal IArchivable Selected;
            internal bool Focused;
            internal Vector2 Legend;
            internal int Range = -1;
            internal readonly QuickSearchWidget Search = (QuickSearchWidget)NativeSearch.GetValue(null);
        }
        private static readonly ConditionalWeakTable<MainTabWindow_History, State> states = new ConditionalWeakTable<MainTabWindow_History, State>();
        private static readonly FieldInfo Group = AccessTools.Field(typeof(MainTabWindow_History), "historyAutoRecorderGroup");
        private static readonly FieldInfo Section = AccessTools.Field(typeof(MainTabWindow_History), "graphSection");
        private static readonly FieldInfo Page = AccessTools.Field(typeof(MainTabWindow_History), "curTab");
        private static readonly FieldInfo Letters = AccessTools.Field(typeof(MainTabWindow_History), "showLetters");
        private static readonly FieldInfo Messages = AccessTools.Field(typeof(MainTabWindow_History), "showMessages");
        private static readonly FieldInfo Tabs = AccessTools.Field(typeof(MainTabWindow_History), "tabs");
        private static readonly FieldInfo NativeSearch = AccessTools.Field(typeof(MainTabWindow_History), "quickSearchWidget");
        private static readonly List<CurveMark> marks = new List<CurveMark>();
        private static Texture2D pinTexture, pinOutline;
        private static MainTabWindow_History focusedOwner;
        internal static Func<Rect, IDisposable> PopupScope = null;
        internal static bool SearchFocused => BottomMenuShell.Enabled && focusedOwner != null &&
            Find.WindowStack.Windows.Contains(focusedOwner) && BottomMenuShell.Eligible(focusedOwner) &&
            !Find.WindowStack.AnyWindowAbsorbingAllInput && states.TryGetValue(focusedOwner, out State state) && state.Page == 1 && state.Focused;
        internal static bool Supported(MainTabWindow_History window) => ((System.Collections.ICollection)Tabs.GetValue(window))?.Count == 3 &&
            WorldMenus.Unmodified(typeof(HistoryAutoRecorderGroup), "DrawGraph") && WorldMenus.Unmodified(typeof(SimpleCurveDrawer), "DrawCurves");
        internal static void Reset(MainTabWindow_History window) { if (states.TryGetValue(window, out State state)) state.Search.Unfocus(); states.Remove(window); if (focusedOwner == window) focusedOwner = null; }
        internal static void Draw(MainTabWindow_History window, Rect body)
        {
            if (!states.TryGetValue(window, out State state))
            { state = new State { Page = Convert.ToInt32(Page.GetValue(null)) }; states.Add(window, state); }
            string[] titles = { "Graph", "Messages", "Statistics" };
            float x = body.x;
            float selectedX = body.x, selectedWidth = 0;
            for (int i = 0; i < titles.Length; i++)
            {
                float width = Math.Max(72, PawnInspectorView.Measure(titles[i].Translate()) + 22);
                if (state.Page == i) { selectedX = x; selectedWidth = width; }
                x += width + 4;
            }
            PawnInspectorView.TabBand(new Rect(body.x, body.y, body.width, 27), new Rect(selectedX, body.y, selectedWidth, 27));
            x = body.x;
            for (int i = 0; i < titles.Length; i++)
            {
                string label = titles[i].Translate(); float width = Math.Max(72, PawnInspectorView.Measure(label) + 22);
                if (PawnInspectorView.Tab(new Rect(x, body.y, width, 27), label, state.Page == i, label, bandPainted: true))
                { state.Page = i; Page.SetValue(null, Enum.ToObject(Page.FieldType, i)); state.Search.Unfocus(); state.Focused = false; if (focusedOwner == window) focusedOwner = null; }
                x += width + 4;
            }
            body.yMin += 34;
            if (state.Page == 0) DrawGraph(window, body, state);
            else if (state.Page == 1) { DrawArchive(body, state); if (state.Focused) focusedOwner = window; else if (focusedOwner == window) focusedOwner = null; }
            else DrawStatistics(body, state);
        }
        private static void DrawGraph(MainTabWindow_History window, Rect rect, State state)
        {
            HistoryAutoRecorderGroup group = (HistoryAutoRecorderGroup)Group.GetValue(window);
            float days = Find.TickManager.TicksGame / 60000f;
            Rect trigger = new Rect(rect.x, rect.y, Math.Min(210, rect.width * .32f), 25);
            if (WorldMenusHistoryChart.Dropdown(trigger, group?.def.LabelCap ?? "SelectGraph".Translate()))
            {
                var options = new List<FloatMenuOption>();
                foreach (HistoryAutoRecorderGroup candidate in Find.History.Groups())
                {
                    if (candidate.def.devModeOnly && !Prefs.DevMode) continue;
                    HistoryAutoRecorderGroup captured = candidate;
                    options.Add(new FloatMenuOption(candidate.def.LabelCap, () => { if (Find.WindowStack.Windows.Contains(window)) { Group.SetValue(window, captured); state.Legend = Vector2.zero; } }));
                }
                if (options.Count > 0)
                    using (PopupScope?.Invoke(trigger)) Find.WindowStack.Add(new FloatMenu(options));
                PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.HistoryTab, KnowledgeAmount.Total);
            }
            int[] spans = { 30, 100, 300, 0 };
            float start = trigger.xMax + 16;
            float each = Math.Max(35, (rect.xMax - start) / 4);
            Rect segments = new Rect(start, rect.y, rect.xMax - start, 25);
            WorldMenus.Surface(segments);
            FloatRange section = (FloatRange)Section.GetValue(window);
            if (state.Range < 0) state.Range = WorldMenusHistoryGraphPolicy.RangeIndex(section.min, section.max);
            for (int i = 0; i < spans.Length; i++)
            {
                int span = spans[i]; string key = span == 0 ? "AllDays" : "Last" + span + "Days";
                if (WorldMenusHistoryChart.Segment(new Rect(start + i * each, rect.y, each, 25), key.Translate(), state.Range == i))
                { state.Range = i; Section.SetValue(window, new FloatRange(WorldMenusPolicy.GraphMinimum(days, span), days)); SoundDefOf.Click.PlayOneShotOnCamera(); }
            }
            Rect chart = new Rect(rect.x, rect.y + 35, rect.width, Math.Max(1, rect.height - 35));
            if (group == null) { WorldMenus.Surface(chart); WorldMenus.Label(chart.ContractedBy(12), "No graph records"); return; }
            marks.Clear();
            foreach (Tale tale in Find.TaleManager.AllTalesListForReading)
                if (tale.def.type == TaleType.PermanentHistorical && !tale.hidden)
                    marks.Add(new CurveMark(GenDate.TickAbsToGame(tale.date) / 60000f, tale.ShortSummary, tale.def.historyGraphColor));
            WorldMenusHistoryChart.Draw(chart, group, (FloatRange)Section.GetValue(window), marks, ref state.Legend);
        }
        private static void DrawArchive(Rect rect, State state)
        {
            bool letters = (bool)Letters.GetValue(null), messages = (bool)Messages.GetValue(null);
            float searchWidth = Math.Min(190, rect.width * .3f);
            if (WorldMenusHistoryChart.Filter(new Rect(rect.x, rect.y, 132, 24), "ShowLetters".Translate(), letters)) { letters = !letters; Letters.SetValue(null, letters); }
            if (WorldMenusHistoryChart.Filter(new Rect(rect.x + 138, rect.y, 132, 24), "ShowMessages".Translate(), messages)) { messages = !messages; Messages.SetValue(null, messages); }
            state.Search.OnGUI(new Rect(rect.xMax - searchWidth, rect.y, searchWidth, 24), () => state.Entries = Vector2.zero);
            state.Focused = state.Search.CurrentlyFocused();
            rect.yMin += 32;
            List<IArchivable> archived = Find.Archive.ArchivablesListForReading;
            var entries = archived.AsEnumerable().Reverse().Where(entry => WorldMenusPolicy.ArchiveVisible(entry is Letter, entry is ArchivedDialog, entry is Message, letters, messages)).ToList();
            if (!entries.Contains(state.Selected)) state.Selected = entries.FirstOrDefault();
            state.Search.noResultsMatched = !entries.Any(entry => state.Search.filter.Matches(entry.ArchivedLabel));
            float listWidth = rect.width * .58f;
            Rect viewport = new Rect(rect.x, rect.y, listWidth, rect.height);
            Rect details = new Rect(viewport.xMax + 10, rect.y, rect.width - listWidth - 10, rect.height);
            WorldMenus.Surface(viewport); WorldMenus.Surface(details);
            viewport = viewport.ContractedBy(6);
            float height = entries.Count * 39;
            float width = PawnInspectorWidgets.BeginScroll(viewport, ref state.Entries, height);
            try
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    IArchivable entry = entries[i]; Rect row = new Rect(0, i * 39, width, 35);
                    if (row.yMax < state.Entries.y || row.y > state.Entries.y + viewport.height) continue;
                    bool match = state.Search.filter.Active && state.Search.filter.Matches(entry.ArchivedLabel);
                    using (new Theme.GuiScope(true))
                    {
                        GUI.enabled &= WorldMenusPolicy.RowInput(row.y, row.yMax, state.Entries.y, viewport.height, true);
                        bool hover = GUI.enabled && Mouse.IsOver(row);
                        Theme.RoundRect(row, state.Selected == entry ? Theme.Selected.ToTransparent(.8f) : hover || match ? Theme.Hover : Color.clear, 3);
                        Rect pin = new Rect(1, row.y + 6, 21, 22);
                        bool pinned = Find.Archive.IsPinned(entry);
                        if (pinTexture == null) { pinTexture = ContentFinder<Texture2D>.Get("UI/Icons/Pin"); pinOutline = ContentFinder<Texture2D>.Get("UI/Icons/Pin-Outline"); }
                        GUI.color = pinned ? Theme.Ink : Theme.Muted;
                        GUI.DrawTexture(pin, pinned ? pinTexture : pinOutline); GUI.color = Color.white;
                        if (GUI.enabled && Widgets.ButtonInvisible(pin))
                        {
                            if (pinned) { Find.Archive.Unpin(entry); SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera(); }
                            else { Find.Archive.Pin(entry); SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera(); }
                        }
                        if (GUI.enabled) TooltipHandler.TipRegionByKey(pin, "PinArchivableTip", 200);
                        Rect icon = new Rect(28, row.y + 7, 21, 21);
                        if (entry.ArchivedIcon != null) { GUI.color = entry.ArchivedIconColor; Widgets.DrawTextureFitted(icon, entry.ArchivedIcon, 1); GUI.color = Color.white; }
                        WorldMenus.Label(new Rect(57, row.y + 1, width - 63, 19), entry.ArchivedLabel, state.Selected == entry, match ? Theme.Ink : Theme.Muted);
                        Vector2 location = Find.CurrentMap == null ? default : Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile);
                        PawnInspectorView.SmallLabel(new Rect(57, row.y + 19, width - 63, 14), GenDate.DateShortStringAt(GenDate.TickGameToAbs(entry.CreatedTicksGame), location), Theme.Muted);
                        Rect hit = new Rect(25, row.y, width - 25, row.height);
                        if (GUI.enabled && Mouse.IsOver(hit)) { state.Selected = entry; TooltipHandler.TipRegion(hit, entry.ArchivedTooltip); }
                        if (GUI.enabled && Event.current.type == EventType.MouseDown && hit.Contains(Event.current.mousePosition) && Event.current.button <= 1)
                        {
                            int button = Event.current.button; Event.current.Use(); state.Selected = entry;
                            if (button == 1)
                            {
                                GlobalTargetInfo target = entry.LookTargets.TryGetPrimaryTarget();
                                if (CameraJumper.CanJump(target)) { CameraJumper.TryJumpAndSelect(target); Find.MainTabsRoot.EscapeCurrentTab(); }
                            }
                            else entry.OpenArchived();
                        }
                    }
                }
            }
            finally { PawnInspectorWidgets.EndScroll(viewport, ref state.Entries, height, 70233); }
            Rect detailContent = details.ContractedBy(10);
            string text = state.Selected?.ArchivedTooltip ?? "NoMessages".Translate();
            float detailHeight = WorldMenus.Paragraph(new Rect(0, 0, detailContent.width - 10, 0), text, false);
            float detailWidth = PawnInspectorWidgets.BeginScroll(detailContent, ref state.Details, detailHeight);
            try { WorldMenus.Paragraph(new Rect(0, 0, detailWidth, 0), text); }
            finally { PawnInspectorWidgets.EndScroll(detailContent, ref state.Details, detailHeight, 70234); }
        }
        private static void DrawStatistics(Rect rect, State state)
        {
            var values = new List<KeyValuePair<string, string>>();
            void Add(string key, string value) => values.Add(new KeyValuePair<string, string>(key.Translate(), value));
            TimeSpan time = TimeSpan.FromSeconds(Find.GameInfo.RealPlayTimeInteracting);
            Add("Playtime", time.Days + " " + "LetterDay".Translate() + " " + time.Hours + " " + "LetterHour".Translate() + " " + time.Minutes + " " + "LetterMinute".Translate() + " " + time.Seconds + " " + "LetterSecond".Translate());
            Add("Storyteller", Find.Storyteller.def.LabelCap); Add("Difficulty", Find.Storyteller.difficultyDef.LabelCap);
            Map map = Find.CurrentMap;
            if (map != null)
            {
                Add("ThisMapColonyWealthTotal", map.wealthWatcher.WealthTotal.ToString("F0"));
                Add("ThisMapColonyWealthItems", map.wealthWatcher.WealthItems.ToString("F0"));
                Add("ThisMapColonyWealthBuildings", map.wealthWatcher.WealthBuildings.ToString("F0"));
                Add("ThisMapColonyWealthColonistsAndTameAnimals", map.wealthWatcher.WealthPawns.ToString("F0"));
            }
            Add("NumThreatBigs", Find.StoryWatcher.statsRecord.numThreatBigs.ToString());
            Add("NumEnemyRaids", Find.StoryWatcher.statsRecord.numRaidsEnemy.ToString());
            if (map != null) Add("ThisMapDamageTaken", map.damageWatcher.DamageTakenEver.ToString());
            Add("ColonistsKilled", Find.StoryWatcher.statsRecord.colonistsKilled.ToString());
            Add("ColonistsLaunched", Find.StoryWatcher.statsRecord.colonistsLaunched.ToString());
            float total = values.Count * 31;
            WorldMenus.Surface(rect); Rect viewport = rect.ContractedBy(9);
            float width = PawnInspectorWidgets.BeginScroll(viewport, ref state.Statistics, total);
            try
            {
                for (int i = 0; i < values.Count; i++)
                {
                    WorldMenus.Label(new Rect(0, i * 31, width * .7f, 26), values[i].Key, false, Theme.Muted);
                    PawnInspectorView.Label(new Rect(width * .7f, i * 31, width * .3f, 26), values[i].Value, Theme.Ink, true, true);
                    PawnInspectorWidgets.Divider(new Rect(0, i * 31 + 30, width, 1));
                }
            }
            finally { PawnInspectorWidgets.EndScroll(viewport, ref state.Statistics, total, 70235); }
        }
    }
}
