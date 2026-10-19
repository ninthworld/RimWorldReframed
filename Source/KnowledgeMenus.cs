using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal static class KnowledgeMenus
    {
        private sealed class View
        {
            internal string Query = "";
            internal Vector2 ListScroll, DetailScroll;
            internal float DetailHeight = 1000;
            internal object Selection;
            internal bool Focused;
            internal string AppliedQuery;
            internal bool DeveloperView;
            internal readonly KnowledgeMenusGraph.State Graph = new KnowledgeMenusGraph.State();
        }
        private static readonly ConditionalWeakTable<Window, View> views = new ConditionalWeakTable<Window, View>();
        internal static Func<Rect, IDisposable> PopupScope = null;
        private static Window focusedOwner, graphOwner;
        private static GUIStyle paragraph, input;
        internal static bool Supports(Window window) => window != null &&
            (window.GetType() == typeof(MainTabWindow_Research) && KnowledgeMenusNative.ResearchReady && KnowledgeMenusNative.ContentsCompatible(true) ||
                window.GetType() == typeof(MainTabWindow_Quests) && KnowledgeMenusNative.QuestsReady && KnowledgeMenusNative.ContentsCompatible(false));
        internal static Vector2 PreferredSize(Window window) => window is MainTabWindow_Research ? new Vector2(900, 520) : new Vector2(1000, 650);
        internal static void Reset(Window window)
        {
            if (window == null) return;
            if (views.TryGetValue(window, out View view)) { view.Focused = false; KnowledgeMenusGraph.Release(view.Graph); }
            if (graphOwner == window) graphOwner = null;
            views.Remove(window);
            if (focusedOwner == window) focusedOwner = null;
        }
        internal static void PrepareCapture(WindowStack stack)
        {
            if (graphOwner == null || !views.TryGetValue(graphOwner, out View view)) return;
            if (!BottomMenuShell.Enabled || !BottomMenuShell.Eligible(graphOwner) || !stack.Windows.Contains(graphOwner) ||
                stack.AnyWindowAbsorbingAllInput || view.DeveloperView)
            { KnowledgeMenusGraph.Release(view.Graph); graphOwner = null; }
        }
        internal static bool SearchFocused => BottomMenuShell.Enabled && focusedOwner != null && BottomMenuShell.Eligible(focusedOwner) && Current.ProgramState == ProgramState.Playing &&
            Find.WindowStack.Windows.Contains(focusedOwner) && !Find.WindowStack.AnyWindowAbsorbingAllInput && views.TryGetValue(focusedOwner, out View view) && view.Focused;
        internal static void Draw(Window window, Rect body)
        {
            if (!Supports(window) || body.width < 530 || body.height < 240) return;
            View view = views.GetValue(window, ignored => new View());
            using (new Theme.GuiScope(true))
            {
                KnowledgeMenusNative.NativeGui(); Styles();
                if (window is MainTabWindow_Research developerOwner && Prefs.DevMode)
                {
                    if (PawnInspectorWidgets.Button(new Rect(body.x, body.y, 180, 24), view.DeveloperView ? "Back to graph" : "Developer tools"))
                    { KnowledgeMenusGraph.Release(view.Graph); view.DeveloperView = !view.DeveloperView; view.AppliedQuery = null; GUI.FocusControl(null); view.Focused = false; if (focusedOwner == window) focusedOwner = null; }
                    body.yMin += 32;
                    if (view.DeveloperView)
                    {
                        GUI.BeginGroup(body);
                        try
                        {
                            // Explicit authoring fallback: native Finish/Apply techprint, graph editing/export.
                            Rect native = body.AtZero(); float nativeLeft = Math.Min(380, native.width * .5f);
                            Rect search = new Rect(native.xMax - 200, 0, 200, 24);
                            KnowledgeMenusNative.ResearchCall(developerOwner, "DrawSearchRect", search);
                            KnowledgeMenusNative.ResearchCall(developerOwner, "DrawLeftRect", new Rect(0, 0, nativeLeft, native.height));
                            KnowledgeMenusNative.ResearchCall(developerOwner, "DrawRightRect", new Rect(nativeLeft + 10, 0, native.width - nativeLeft - 10, native.height), search.x - 4);
                        }
                        finally { GUI.EndGroup(); }
                        view.Focused = KnowledgeMenusNative.ResearchSearchFocused(developerOwner);
                        if (view.Focused) focusedOwner = window; else if (focusedOwner == window) focusedOwner = null;
                        return;
                    }
                }
                else view.DeveloperView = false;
                if (window is MainTabWindow_Research researchOwner)
                {
                    graphOwner = window;
                    DrawResearch(researchOwner, body, view);
                    view.Focused = GUI.GetNameOfFocusedControl() == "Reframed.KnowledgeSearch";
                    if (view.Focused) focusedOwner = window; else if (focusedOwner == window) focusedOwner = null;
                    return;
                }
                float leftWidth = KnowledgeMenusPolicy.LeftWidth(body.width);
                Rect left = new Rect(body.x, body.y, leftWidth, body.height);
                Rect detail = new Rect(left.xMax + 12, body.y, body.width - leftWidth - 12, body.height);
                Theme.RoundRect(left, Theme.Surface.ToTransparent(.28f), 4); Theme.RoundRect(detail, Theme.Surface.ToTransparent(.28f), 4);
                Search(new Rect(left.x + 8, left.y + 8, left.width - 16, 25), window, view);
                DrawQuests((MainTabWindow_Quests)window, left, detail, view);
                view.Focused = GUI.GetNameOfFocusedControl() == "Reframed.KnowledgeSearch";
                if (view.Focused) focusedOwner = window; else if (focusedOwner == window) focusedOwner = null;
            }
        }
        private static void Styles()
        {
            Text.Font = GameFont.Small;
            if (paragraph != null && paragraph.font == Text.CurFontStyle.font) return;
            paragraph = new GUIStyle(Text.CurFontStyle) { fontSize = 11, wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(), margin = new RectOffset() };
            paragraph.normal.textColor = Color.white;
            input = new GUIStyle(paragraph) { wordWrap = false, alignment = TextAnchor.MiddleLeft };
        }
        private static void Search(Rect rect, Window owner, View view)
        {
            Theme.RoundRect(rect, Theme.Border, 3); Theme.RoundRect(rect.ContractedBy(.8f), Theme.Rgb(0x0B141D).ToTransparent(.85f), 2.5f);
            GUI.SetNextControlName("Reframed.KnowledgeSearch");
            string next = GUI.TextField(new Rect(rect.x + 7, rect.y, rect.width - 33, rect.height), view.Query, input);
            if (next != view.Query) { view.Query = next; view.ListScroll = Vector2.zero; }
            if (view.Query.Length == 0 && GUI.GetNameOfFocusedControl() != "Reframed.KnowledgeSearch")
                PawnInspectorView.SmallLabel(rect.ContractedBy(7, 0), "Search…", Theme.Muted);
            if (view.Query.Length > 0 && PawnInspectorWidgets.Button(new Rect(rect.xMax - 24, rect.y + 2, 22, rect.height - 4), "×"))
            { view.Query = ""; view.ListScroll = Vector2.zero; GUI.FocusControl("Reframed.KnowledgeSearch"); }
        }
        private static bool Dropdown(Rect rect, string label)
        {
            bool click = PawnInspectorWidgets.Button(rect, "");
            PawnInspectorView.Label(new Rect(rect.x + 7, rect.y, rect.width - 28, rect.height), label, Theme.Ink);
            Vector2 center = new Vector2(rect.xMax - 11, rect.center.y);
            Widgets.DrawLine(center + new Vector2(-3, -1), center + new Vector2(0, 2), Theme.Muted, 1);
            Widgets.DrawLine(center + new Vector2(0, 2), center + new Vector2(3, -1), Theme.Muted, 1);
            return click;
        }
        private static void Section(Rect rect, string title, ref float y)
        { PawnInspectorWidgets.Divider(new Rect(0, y + 5, rect.width, .7f)); PawnInspectorView.Heading(new Rect(0, y + 12, rect.width, 14), title); y += 31; }
        private static void Paragraph(Rect rect, string value, ref float y)
        {
            float height = paragraph.CalcHeight(new GUIContent(value ?? ""), rect.width);
            GUI.Label(new Rect(0, y, rect.width, height), value ?? "", paragraph); y += height + 8;
        }
        private static bool PointerOutside(Rect viewport)
        {
            EventType type = Event.current.type;
            return (type == EventType.MouseDown || type == EventType.MouseUp || type == EventType.MouseDrag || type == EventType.ScrollWheel) && !Mouse.IsOver(viewport);
        }
        private static string ProjectStatus(ResearchProjectDef project)
            => project.IsHidden ? "UnknownResearch".Translate().ToString() : project.IsFinished ? "Finished".Translate().ToString() : Find.ResearchManager.IsCurrentProject(project) ? "InProgress".Translate().ToString() : project.CanStartNow ? "Available" : "Locked".Translate().ToString();
        private static void DrawResearch(MainTabWindow_Research owner, Rect body, View view)
        {
            List<ResearchProjectDef> all = owner.VisibleResearchProjects;
            float detailWidth = Math.Min(300, Math.Max(260, body.width * .33f));
            Rect graph = new Rect(body.x, body.y + 36, body.width - detailWidth - 12, body.height - 36);
            Rect detail = new Rect(graph.xMax + 12, body.y, detailWidth, body.height);
            Theme.RoundRect(graph, Theme.Surface.ToTransparent(.18f), 4);
            Theme.RoundRect(detail, Theme.Surface.ToTransparent(.28f), 4);
            Search(new Rect(body.x, body.y, Math.Max(120, graph.width - 150), 26), owner, view);
            Rect category = new Rect(graph.xMax - 142, body.y, 142, 26);
            if (Dropdown(category, owner.CurTab?.LabelCap.ToString() ?? "Research".Translate().ToString()))
            {
                var options = new List<FloatMenuOption>();
                foreach (ResearchTabDef tab in DefDatabase<ResearchTabDef>.AllDefsListForReading)
                {
                    ResearchTabDef captured = tab;
                    options.Add(new FloatMenuOption(tab.LabelCap, () =>
                    {
                        if (!Find.WindowStack.Windows.Contains(owner)) return;
                        KnowledgeMenusGraph.Release(view.Graph);
                        owner.CurTab = captured;
                        KnowledgeMenusNative.ResearchCall(owner, "UpdateSelectedProject", Find.ResearchManager);
                        view.Graph.Focus = true;
                    }));
                }
                using (PopupScope?.Invoke(category)) Find.WindowStack.Add(new FloatMenu(options));
            }
            HashSet<ResearchProjectDef> matches;
            if (KnowledgeMenusPolicy.RefreshSearch(view.AppliedQuery, view.Query))
            { matches = KnowledgeMenusNative.ResearchResults(owner, view.Query); view.AppliedQuery = view.Query; view.Graph.Focus = true; }
            else matches = KnowledgeMenusNative.ExistingResearchResults(owner);
            KnowledgeMenusGraph.Draw(owner, graph, view.Graph, all, matches, !string.IsNullOrEmpty(view.Query));
            ResearchProjectDef current = KnowledgeMenusNative.Project(owner);
            if (current == null || current.IsHidden || !all.Contains(current) || !Find.ResearchManager.TabInfoVisible(current.tab))
            {
                Rect help = detail.ContractedBy(14); float helpY = 0;
                ResearchTabDef tab = owner.CurTab;
                if (!ReferenceEquals(view.Selection, tab)) { view.Selection = tab; view.DetailScroll = Vector2.zero; view.DetailHeight = 500; }
                float helpWidth = PawnInspectorWidgets.BeginScroll(help, ref view.DetailScroll, view.DetailHeight);
                try
                {
                    if (tab != null)
                    {
                        PawnInspectorView.Label(new Rect(0, helpY, helpWidth, 25), tab.generalTitle.CapitalizeFirst(), Theme.Ink, strong: true); helpY += 31;
                        Paragraph(new Rect(0, 0, helpWidth, help.height), tab.generalDescription, ref helpY);
                    }
                    Paragraph(new Rect(0, 0, helpWidth, help.height), tab != null && !Find.ResearchManager.TabInfoVisible(tab) ? "ResearchNotDiscovered".Translate().ToString() : "Select a research project", ref helpY);
                    view.DetailHeight = helpY + 8;
                }
                finally { PawnInspectorWidgets.EndScroll(help, ref view.DetailScroll, view.DetailHeight, 300105); }
                return;
            }
            if (!ReferenceEquals(view.Selection, current)) { view.Selection = current; view.DetailScroll = Vector2.zero; view.DetailHeight = 1000; }
            Rect inner = detail.ContractedBy(12);
            PawnInspectorView.Label(new Rect(inner.x, inner.y, inner.width - 145, 25), current.LabelCap, Theme.Ink, strong: true);
            KnowledgeMenusNative.ResearchCall(owner, "DrawStartButton", new Rect(inner.xMax - 140, inner.y, 140, 25));
            PawnInspectorView.SmallLabel(new Rect(inner.x, inner.y + 29, inner.width, 18), current.ProgressApparentString + " / " + current.CostApparent.ToString("F0") + " · " + ProjectStatus(current), Theme.Muted);
            PawnInspectorView.Bar(new Rect(inner.x, inner.y + 51, inner.width, 9), current.ProgressPercent, Theme.Good);
            Rect viewport = new Rect(inner.x, inner.y + 70, inner.width, Math.Max(1, inner.height - 70));
            bool outside = PointerOutside(viewport);
            float contentWidth = PawnInspectorWidgets.BeginScroll(viewport, ref view.DetailScroll, view.DetailHeight);
            try
            {
                using (new Theme.GuiScope(true))
                {
                    GUI.enabled &= !outside;
                    Rect r = new Rect(0, 0, contentWidth, 500); float y = 0;
                    Paragraph(r, current.Description, ref y);
                    if (ModsConfig.AnomalyActive && current.knowledgeCategory != null)
                    {
                        Paragraph(r, "KnowledgeCategory".Translate() + ": " + current.knowledgeCategory.LabelCap, ref y);
                        Paragraph(r, "AnomalyResearchDescriptionHelpText".Translate(), ref y);
                    }
                    if ((int)current.techLevel > (int)Faction.OfPlayer.def.techLevel)
                        Paragraph(r, "TechLevelTooLow".Translate(Faction.OfPlayer.def.techLevel.ToStringHuman(), current.techLevel.ToStringHuman(), (1 / current.CostFactor(Faction.OfPlayer.def.techLevel)).ToStringPercent()) + " " + "ResearchCostComparison".Translate(current.Cost.ToString("F0"), current.CostApparent.ToString("F0")), ref y);
                    Section(r, "REQUIREMENTS", ref y);
                    if (current.TechprintCount > 0) Paragraph(r, "Techprints: " + current.TechprintsApplied + " / " + current.TechprintCount, ref y);
                    ResearchY(owner, "DrawTechprintInfo", r, ref y);
                    ResearchY(owner, "DrawResearchPrerequisites", r, ref y, current);
                    foreach (ResearchProjectDef prerequisite in (current.prerequisites ?? new List<ResearchProjectDef>()).Concat(current.hiddenPrerequisites ?? new List<ResearchProjectDef>()).Distinct())
                    {
                        if (prerequisite == null || prerequisite.IsHidden || !all.Contains(prerequisite)) continue;
                        if (PawnInspectorWidgets.Button(new Rect(0, y, contentWidth, 23), prerequisite.LabelCap + " ›"))
                        { owner.Select(prerequisite); view.Graph.Focus = true; }
                        y += 27;
                    }
                    foreach (string method in new[] { "DrawResearchBenchRequirements", "DrawStudyRequirements", "DrawInspectionRequirements" })
                        y += KnowledgeMenusNative.ResearchHeight(owner, method, current, new Rect(0, y, contentWidth, 500));
                    if (current.requiresMechanitor) Paragraph(r, (current.PlayerMechanitorRequirementMet ? "✓ " : "— ") + "Mechanitor requirement", ref y);
                    Section(r, "UNLOCKS", ref y);
                    y += KnowledgeMenusNative.ResearchHeight(owner, "DrawUnlockableHyperlinks", new Rect(0, y, contentWidth, 500), new Rect(0, view.DetailScroll.y, contentWidth, viewport.height), current);
                    y += KnowledgeMenusNative.ResearchHeight(owner, "DrawCustomUnlockables", new Rect(0, y, contentWidth, 500), current);
                    y += KnowledgeMenusNative.ResearchHeight(owner, "DrawContentSource", new Rect(0, y, contentWidth, 500), current);
                    view.DetailHeight = y + 12;
                }
            }
            finally { PawnInspectorWidgets.EndScroll(viewport, ref view.DetailScroll, view.DetailHeight, 300102); }
        }
        private static void ResearchY(MainTabWindow_Research owner, string method, Rect rect, ref float y, ResearchProjectDef project = null)
        {
            object[] args = project == null ? new object[] { new Rect(0, y, rect.width, 500), y } : new object[] { new Rect(0, y, rect.width, 500), y, project };
            KnowledgeMenusNative.ResearchCall(owner, method, args); y = (float)args[1];
        }

        private static void DrawQuests(MainTabWindow_Quests owner, Rect left, Rect detail, View view)
        {
            string[] pages = { "AvailableQuests", "ActiveQuests", "HistoricalQuests" };
            int page = (int)Convert.ToByte(KnowledgeMenusNative.QuestTab.GetValue(owner));
            Rect tabs = new Rect(left.x + 8, left.y + 39, left.width - 16, 25);
            if (Dropdown(tabs, pages[Math.Min(2, page)].Translate()))
                using (PopupScope?.Invoke(tabs)) Find.WindowStack.Add(new FloatMenu(pages.Select((key, index) => new FloatMenuOption(key.Translate(), () => { KnowledgeMenusNative.QuestPage(owner, index); view.ListScroll = Vector2.zero; })).ToList()));
            if (PawnInspectorWidgets.Button(new Rect(left.x + 8, left.y + 70, left.width - 16, 24), "ChooseRewards".Translate())) Find.WindowStack.Add(new Dialog_RewardPrefsConfig());
            if (Prefs.DevMode && !DebugSettings.godMode)
            {
                bool show = (bool)KnowledgeMenusNative.QuestShowAll.GetValue(owner);
                if (PawnInspectorWidgets.Button(new Rect(left.x + 8, left.y + 99, left.width - 16, 21), "DEV: Show hidden", selected: show)) KnowledgeMenusNative.QuestShowAll.SetValue(owner, !show);
            }
            KnowledgeMenusNative.QuestQuery(owner, view.Query);
            List<Quest> rows = KnowledgeMenusNative.QuestRows(owner);
            float listTop = Prefs.DevMode && !DebugSettings.godMode ? 128 : 103;
            Rect list = new Rect(left.x + 6, left.y + listTop, left.width - 12, left.height - listTop - 6);
            float contentWidth = PawnInspectorWidgets.BeginScroll(list, ref view.ListScroll, rows.Count * 50);
            try
            {
                if (rows.Count == 0) PawnInspectorView.Label(new Rect(5, 3, contentWidth - 10, 30), "No matching quests", Theme.Muted);
                for (int index = 0; index < rows.Count; index++)
                {
                    Quest quest = rows[index]; Rect row = new Rect(0, index * 50, contentWidth, 47);
                    bool selected = KnowledgeMenusNative.Selected(owner) == quest;
                    Theme.RoundRect(row, selected ? Theme.Selected.ToTransparent(.8f) : Theme.Surface.ToTransparent(.45f), 3);
                    float indent = quest.parent != null && rows.Contains(quest.parent) ? 9 : 0;
                    PawnInspectorView.Label(new Rect(7 + indent, row.y + 3, row.width - 14 - indent, 22), quest.name, Theme.Ink, strong: selected);
                    string time = KnowledgeMenusNative.QuestTimeText(owner, quest, out string timeTip, out Color timeColor);
                    string status = quest.dismissed ? "QuestDismissed".Translate().ToString() : QuestStatus(quest);
                    PawnInspectorView.SmallLabel(new Rect(7 + indent, row.y + 26, row.width - 14 - indent, 17), status + (string.IsNullOrEmpty(time) ? "" : " · " + time), quest.State == QuestState.EndedFailed ? Theme.Danger : timeColor);
                    if (KnowledgeMenusPolicy.FullyVisible(row.y, row.height, view.ListScroll.y, list.height))
                        TooltipHandler.TipRegion(row, quest.name + "\n" + status + "\n" + timeTip + "\nChallenge " + Math.Max(1, quest.challengeRating));
                    if (KnowledgeMenusPolicy.FullyVisible(row.y, row.height, view.ListScroll.y, list.height) && GUI.enabled && Widgets.ButtonInvisible(row)) owner.Select(quest);
                }
            }
            finally { PawnInspectorWidgets.EndScroll(list, ref view.ListScroll, rows.Count * 50, 300103); }
            Quest current = KnowledgeMenusNative.Selected(owner);
            if (current == null || !Find.QuestManager.questsInDisplayOrder.Contains(current)) { PawnInspectorView.Label(detail.ContractedBy(14), "NoQuestSelected".Translate(), Theme.Muted); return; }
            if (!ReferenceEquals(view.Selection, current)) { view.Selection = current; view.DetailScroll = Vector2.zero; view.DetailHeight = 1200; }
            Rect inner = detail.ContractedBy(12);
            PawnInspectorView.Label(new Rect(inner.x, inner.y, inner.width - 120, 30), current.name, Theme.Ink, strong: true);
            GUI.BeginGroup(new Rect(inner.xMax - 112, inner.y, 112, 34));
            try
            {
                float unused = 0; QuestY(owner, "DoDismissButton", new Rect(0, 0, 112, 34), ref unused);
                KnowledgeMenusNative.QuestCall(owner, "DoCharityIcon", new Rect(0, 0, 112, 34));
            }
            finally { GUI.EndGroup(); }
            if (KnowledgeMenusNative.Selected(owner) != current) return;
            Rect viewport = new Rect(inner.x, inner.y + 40, inner.width, Math.Max(1, inner.height - 40));
            bool outside = PointerOutside(viewport);
            float width = PawnInspectorWidgets.BeginScroll(viewport, ref view.DetailScroll, view.DetailHeight);
            try
            {
                using (new Theme.GuiScope(true))
                {
                    GUI.enabled &= !outside;
                    Rect r = new Rect(0, 0, width, 10000); float y = 0;
                    Section(r, "QUEST", ref y);
                    float beforeAccept = y;
                    QuestY(owner, "DoAcceptButton", r, ref y);
                    object[] dates = { r, y, beforeAccept }; KnowledgeMenusNative.QuestCall(owner, "DoRightAlignedInfo", dates); y = (float)dates[1];
                    QuestY(owner, "DoOutcomeInfo", r, ref y);
                    QuestY(owner, "DoDescription", r, ref y);
                    foreach (string method in new[] { "DoAcceptanceRequirementInfo", "DoIdeoCharityInfo" })
                    { object[] args = { r, view.DetailHeight > viewport.height, y }; KnowledgeMenusNative.QuestCall(owner, method, args); y = (float)args[2]; }
                    Section(r, "REWARDS & CHOICES", ref y); QuestY(owner, "DoRewards", r, ref y);
                    Section(r, "PLACES & PEOPLE", ref y);
                    foreach (string method in new[] { "DoLookTargets", "DoSelectTargets", "DoDefHyperlinks" }) QuestY(owner, method, r, ref y);
                    if (current.root != null && !current.root.hideInvolvedFactionsInfo) QuestY(owner, "DoFactionInfo", r, ref y);
                    QuestY(owner, "DoDebugInfoToggle", r, ref y); QuestY(owner, "DoDebugInfo", r, ref y);
                    view.DetailHeight = y + 12;
                }
            }
            finally { PawnInspectorWidgets.EndScroll(viewport, ref view.DetailScroll, view.DetailHeight, 300104); }
        }
        private static void QuestY(MainTabWindow_Quests owner, string method, Rect rect, ref float y)
        { object[] args = { rect, y }; KnowledgeMenusNative.QuestCall(owner, method, args); y = (float)args[1]; }
        private static string QuestStatus(Quest quest)
        {
            switch (quest.State)
            {
                case QuestState.NotYetAccepted: return "AvailableQuests".Translate();
                case QuestState.Ongoing: return "ActiveQuests".Translate();
                case QuestState.EndedSuccess: return "Completed";
                case QuestState.EndedFailed: return "Failed";
                default: return "HistoricalQuests".Translate();
            }
        }
    }
    [HarmonyPatch(typeof(WindowStack), "get_AnySearchWidgetFocused")]
    internal static class KnowledgeMenusSearchFocusPatch
    { private static void Postfix(ref bool __result) { if (KnowledgeMenus.SearchFocused) __result = true; } }
}
