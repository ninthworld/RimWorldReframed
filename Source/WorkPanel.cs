using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal static class WorkPanel
    {
        private static Pawn selected;
        private static Map map;
        private static MainTabWindow_Work owner;
        private static Vector2 rosterScroll, editorScroll;
        private static readonly Dictionary<string, Vector2> laneScroll = new Dictionary<string, Vector2>();
        private static Rect activeViewport;
        private static bool revealSelection;
        private static string notice = "";
        internal static bool Drawing { get; private set; }
        internal static float LastEditorHeight { get; private set; }
        internal static float LastCardWidth { get; private set; }
        internal static bool HasSelection => selected != null;
        private static readonly string[] Categories = { "MEDICAL & CARE", "LEARNING", "FARMING", "PRODUCTION", "COMBAT", "CONSTRUCTION" };
        internal static bool Eligible(Window window) => window?.GetType() == typeof(MainTabWindow_Work) && ReframedMod.Active &&
            ReframedMod.Settings.WorkPanel && WorkPanelNative.Ready && WorkPanelNative.Compatible((MainTabWindow_Work)window) && Find.CurrentMap != null && !WorldRendererUtility.WorldSelected &&
            !TutorSystem.TutorialMode && WorkPanelLayout.Supported(UI.screenWidth, UI.screenHeight);
        private static List<Pawn> Roster(MainTabWindow_Work window)
        {
            if (owner != window || map != Find.CurrentMap) { Reset(); owner = window; map = Find.CurrentMap; }
            List<Pawn> pawns = WorkPanelNative.Roster(window);
            if (selected != null && !pawns.Contains(selected)) Select(null);
            return pawns;
        }
        internal static void Reset()
        { selected = null; owner = null; map = null; rosterScroll = editorScroll = Vector2.zero; laneScroll.Clear(); notice = ""; revealSelection = false; WorkPanelNative.ResetSession(); }
        private static void Select(Pawn pawn)
        {
            if (selected == pawn) return;
            selected = pawn; notice = ""; revealSelection = pawn != null; editorScroll = Vector2.zero; WorkPanelNative.Reset();
            if (pawn == null) WorkPanelTransition.Deselect();
        }
        private static string Label(WorkTypeDef work) => work.labelShort.CapitalizeFirst();
        private static int? Skill(WorkPanelCell cell) => cell.Skill.HasValue ? Mathf.RoundToInt(cell.Skill.Value) : (int?)null;
        private static List<WorkTypeDef>[] Groups(List<WorkTypeDef> types) => Enumerable.Range(0, 7)
            .Select(category => types.Where(type => WorkPanelLayout.Category(type.defName) == category)
                .OrderBy(type => WorkPanelLayout.EditorOrder(type.defName)).ToList()).ToArray();
        private static float[] ColumnWidths(List<WorkTypeDef>[] groups)
        {
            return Enumerable.Range(0, 6).Select(category => Math.Min(170, Math.Max(72,
                Math.Max(WorkPanelView.MeasureHeading(Categories[category]) + 2, groups[category].Select(type =>
                {
                    bool skilled = type.relevantSkills != null && type.relevantSkills.Count > 0;
                    return WorkPanelView.MeasureTag(Label(type), skilled ? (int?)20 : null, skilled ? Passion.Major : Passion.None) + WorkPanelLayout.PrioritySpace;
                }).DefaultIfEmpty(0).Max())))).ToArray();
        }
        private static float CardWidth(float windowWidth, float[] widths) => Math.Min(windowWidth - 20, widths.Sum() + 5 * 8 + 26);
        private static int Columns(float width, float[] widths) => widths.Sum() + 5 * 8 + 26 <= width + .1f ? 6 : 3;
        private static float GridHeight(List<WorkTypeDef>[] groups, int columns)
        {
            float height = 8;
            for (int first = 0; first < 6; first += columns) height += 18 + groups.Skip(first).Take(columns).Max(group => group.Count) * WorkPanelLayout.TagPitch;
            int extra = groups[6].Count(type => type.defName != "BasicWorker");
            if (extra > 0) height += 8 + (float)Math.Ceiling(extra / (float)columns) * WorkPanelLayout.TagPitch;
            return height + 8;
        }
        private static float EditorHeight(List<WorkTypeDef>[] groups, float width, float[] columns)
            => selected == null ? WorkPanelTransition.ReservedEditorHeight : WorkPanelLayout.EditorHeight(GridHeight(groups, Columns(width, columns)), UI.screenHeight);
        internal static Rect Bounds(MainTabWindow_Work window)
        {
            List<Pawn> pawns = Roster(window);
            var groups = Groups(WorkPanelNative.WorkTypes(window));
            float[] columns = ColumnWidths(groups);
            float width = WorkPanelLayout.Width(UI.screenWidth, columns.Sum() + 5 * 8 + 50);
            float editor = EditorHeight(groups, CardWidth(width, columns), columns);
            LastEditorHeight = editor; LastCardWidth = CardWidth(width, columns);
            float height = WorkPanelLayout.Height(editor + pawns.Count * WorkPanelLayout.Row, UI.screenHeight);
            return new Rect(8, WorkPanelLayout.Bottom(UI.screenHeight) - height, width, height);
        }
        internal static void Draw(MainTabWindow_Work window, Rect rect)
        {
            if (Event.current.type == EventType.Layout) return;
            using (new Theme.GuiScope(true))
            {
                Drawing = true;
                try
                {
                    GUI.color = GUI.contentColor = Color.white; Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = false;
                    List<Pawn> pawns = Roster(window);
                    List<WorkTypeDef> types = WorkPanelNative.WorkTypes(window);
                    var groups = Groups(types); float[] columns = ColumnWidths(groups);
                    float cardWidth = CardWidth(rect.width, columns);
                    float editor = EditorHeight(groups, cardWidth, columns);
                    if (selected != null)
                    {
                        Rect card = new Rect(rect.width - cardWidth - 8, 0, cardWidth, editor - 6);
                        WorkPanelTransition.DrawEditor(card, () => DrawEditor(window, card, groups, columns));
                    }
                    Rect roster = new Rect(0, editor, rect.width, rect.height - editor);
                    Theme.RoundRect(roster, WorkPanelTransition.RosterFill, 6);
                    if (DrawToolbar(window, roster, types)) return;
                    Rect viewport = new Rect(7, roster.y + WorkPanelLayout.Header, rect.width - 14, Math.Max(1, roster.height - WorkPanelLayout.Header - 7));
                    float content = Math.Max(WorkPanelLayout.Row, pawns.Count * WorkPanelLayout.Row);
                    int selectedIndex = pawns.IndexOf(selected);
                    if (revealSelection && selectedIndex >= 0)
                    { rosterScroll.y = WorkPanelLayout.Reveal(rosterScroll.y, selectedIndex * WorkPanelLayout.Row, WorkPanelLayout.Row, viewport.height); revealSelection = false; }
                    PawnInspectorWidgets.BeginScroll(viewport, ref rosterScroll, content);
                    activeViewport = new Rect(0, rosterScroll.y, viewport.width - 10, viewport.height);
                    try
                    {
                        if (pawns.Count == 0) PawnInspectorView.SmallLabel(new Rect(8, 0, activeViewport.width - 16, 30), "No colonists available for work.", Theme.Muted);
                        for (int index = 0; index < pawns.Count; index++)
                            DrawRow(window, new Rect(0, index * WorkPanelLayout.Row, activeViewport.width, WorkPanelLayout.Row), pawns[index], types);
                    }
                    finally { PawnInspectorWidgets.EndScroll(viewport, ref rosterScroll, content, 200001); }
                }
                finally { Drawing = false; }
            }
        }
        private static bool DrawToolbar(MainTabWindow_Work window, Rect rect, List<WorkTypeDef> types)
        {
            MainButtonDef workButton = DefDatabase<MainButtonDef>.GetNamedSilentFail("Work");
            string title = workButton != null ? workButton.LabelCap.ToString() : "Work";
            float titleWidth = Math.Min(120, PawnInspectorView.MeasureStrong(title) + 3);
            PawnInspectorView.Label(new Rect(12, rect.y + 4, titleWidth, 22), title, Theme.Ink, strong: true);
            float sortWidth = Math.Max(52, PawnInspectorView.Measure("Sort…") + 18);
            Rect sort = new Rect(rect.xMax - 32 - sortWidth, rect.y + 5, sortWidth, 20);
            float modeWidth = PawnInspectorView.Measure("Priorities") + PawnInspectorView.MeasureSmall("Off") + 56;
            Rect mode = new Rect(sort.x - 6 - modeWidth, rect.y + 5, modeWidth, 20);
            string hint = selected == null ? "Select a colonist to assign work" : "Assign work · " + selected.LabelShortCap;
            Rect message = new Rect(18 + titleWidth, rect.y + 4, Math.Max(1, mode.x - titleWidth - 26), 22);
            PawnInspectorView.SmallLabel(message, hint, Theme.Muted);
            if (GUI.enabled) TooltipHandler.TipRegion(message, hint + "\nClick a tag to toggle work. Turn Priorities on to edit its number.");
            if (WorkPanelView.PriorityToggle(mode, WorkPanelNative.Manual))
            { WorkPanelNative.SetManual(window, !WorkPanelNative.Manual); notice = ""; }
            if (GUI.enabled) TooltipHandler.TipRegion(mode, "Use numbered priorities for the colony and enable the priority buttons. When off, stored numbers remain gray and cannot be edited. Auto assign works in either mode.");
            if (PawnInspectorWidgets.Button(sort, "Sort…")) ShowSort(window, types);
            if (PawnBioActionsStyle.Close(new Rect(rect.xMax - 26, rect.y + 5, 20, 20))) { window.Close(); return true; }
            return false;
        }
        private static List<WorkTypeDef> Assigned(Pawn pawn, List<WorkTypeDef> types) => types
            .Where(type => !pawn.Destroyed && !pawn.Dead && pawn.workSettings != null && pawn.workSettings.Initialized && pawn.workSettings.EverWork && pawn.workSettings.GetPriority(type) > 0)
            .OrderByDescending(type => WorkPanelPolicy.NativeOrderKey(type.naturalPriority, pawn.workSettings.GetPriority(type)))
            .ThenBy(type => DefDatabase<WorkTypeDef>.AllDefsListForReading.IndexOf(type)).ToList();
        private static bool Visible(Rect rect) => rect.y >= activeViewport.yMin && rect.yMax <= activeViewport.yMax;
        private static void DrawRow(MainTabWindow_Work window, Rect row, Pawn pawn, List<WorkTypeDef> types)
        {
            if (row.yMax < activeViewport.yMin || row.y > activeViewport.yMax) return;
            WorkPanelView.DrawRowFace(row, pawn == selected);
            float statusWidth = WorkPanelNative.StatusColumns(window).Sum(column => WorkPanelNative.StatusWidth(window, column));
            Rect identity = new Rect(row.x + 2, row.y, 150, row.height);
            using (new Theme.GuiScope(true))
            {
                GUI.enabled &= Visible(row);
                if (WorkPanelView.Identity(identity, pawn, pawn == selected, false)) Select(pawn == selected ? null : pawn);
                float x = identity.xMax;
                foreach (PawnColumnDef status in WorkPanelNative.StatusColumns(window))
                {
                    float width = WorkPanelNative.StatusWidth(window, status);
                    if (Visible(row))
                    {
                        GUI.BeginGroup(new Rect(x, row.y, width, 30));
                        try { WorkPanelNative.DrawStatus(new Rect(0, 0, width, 30), pawn, status, window); }
                        finally { GUI.EndGroup(); }
                    }
                    x += width;
                }
                Rect actions = new Rect(row.xMax - 46, row.y + 5, 44, 20);
                if (WorkPanelView.IconButton(new Rect(actions.x, actions.y, 20, 20), "copy", "Copy " + pawn.LabelShortCap + "'s work priorities"))
                { Select(pawn); WorkPanelNative.Copy(window, pawn); notice = ""; }
                if (WorkPanelView.IconButton(new Rect(actions.x + 24, actions.y, 20, 20), "paste", "Paste work priorities", WorkPanelNative.CanPasteFor(window)))
                { Select(pawn); WorkPanelNative.Paste(window, pawn); notice = ""; }
            }
            Rect lane = new Rect(identity.xMax + statusWidth + 4, row.y + 5, Math.Max(1, row.xMax - 52 - identity.xMax - statusWidth - 4), WorkPanelLayout.Tag);
            Theme.RoundRect(lane.ExpandedBy(2, 1), PawnInspectorView.Content, 2);
            var assigned = Assigned(pawn, types);
            float[] widths = assigned.Select(type => Math.Min(lane.width, WorkPanelView.MeasureTag(Label(type), null, Passion.None))).ToArray();
            float total = widths.Sum() + Math.Max(0, assigned.Count - 1) * 3;
            laneScroll.TryGetValue(pawn.ThingID, out Vector2 scroll);
            scroll.x = Mathf.Clamp(scroll.x, 0, Math.Max(0, total - lane.width)); scroll.y = 0;
            bool laneVisible = Visible(lane);
            if (laneVisible && Event.current.type == EventType.ScrollWheel && Mouse.IsOver(lane) && total > lane.width)
            { scroll.x = Mathf.Clamp(scroll.x + Event.current.delta.y * 24, 0, total - lane.width); Event.current.Use(); }
            scroll = GUI.BeginScrollView(lane, scroll, new Rect(0, 0, Math.Max(lane.width, total), lane.height), false, false, GUIStyle.none, GUIStyle.none);
            try
            {
                float x = 0;
                for (int index = 0; index < assigned.Count; index++)
                {
                    WorkTypeDef type = assigned[index]; Rect tag = new Rect(x, 0, widths[index], lane.height);
                    using (new Theme.GuiScope(true))
                    {
                        GUI.enabled &= laneVisible && x >= scroll.x - .01f && x + tag.width <= scroll.x + lane.width + .01f;
                        var cell = GUI.enabled ? WorkPanelNative.Cell(pawn, type) : default;
                        int click = WorkPanelView.Tag(tag, Label(type), WorkPanelLayout.Category(type.defName), null, Passion.None, true, true,
                            GUI.enabled ? cell.Tooltip + "\nSelect this colonist to change work above." : null, incapable: cell.Incapable);
                        if (click > 0) { Select(pawn); Event.current.Use(); }
                    }
                    x += widths[index] + 3;
                }
            }
            finally { GUI.EndScrollView(); }
            if (total > lane.width && Visible(new Rect(lane.x, row.yMax - 3, lane.width, 2)))
                PawnInspectorWidgets.Rail(new Rect(lane.x, row.yMax - 3, lane.width, 2), ref scroll.x, total, lane.width, true);
            laneScroll[pawn.ThingID] = scroll;
        }
        private static void DrawEditor(MainTabWindow_Work window, Rect card, List<WorkTypeDef>[] groups, float[] widths)
        {
            WorkPanelTransition.PaintEditorShell(card);
            int columns = Columns(card.width, widths);
            float content = GridHeight(groups, columns);
            Rect viewport = new Rect(card.x + 5, card.y + 3, card.width - 10, card.height - WorkPanelLayout.EditorFooter - 6);
            PawnInspectorWidgets.BeginScroll(viewport, ref editorScroll, content);
            float available = viewport.width - 10;
            activeViewport = new Rect(0, editorScroll.y, available, viewport.height);
            try
            {
                float y = 5;
                for (int first = 0; first < 6; first += columns)
                {
                    float x = 3;
                    float block = 18 + groups.Skip(first).Take(columns).Max(group => group.Count) * WorkPanelLayout.TagPitch;
                    for (int offset = 0; offset < columns; offset++)
                    {
                        int category = first + offset;
                        float column = columns == 6 ? widths[category] : (available - 6 - (columns - 1) * 8) / columns;
                        if (offset > 0) Theme.RoundRect(new Rect(x - 4, y + 2, .7f, block - 4), Theme.Border.ToTransparent(.35f), 0);
                        Rect heading = new Rect(x, y, column, 14);
                        if (Visible(heading)) WorkPanelView.CategoryHeading(heading, Categories[category]);
                        for (int item = 0; item < groups[category].Count; item++)
                            EditorTag(window, new Rect(x, y + 18 + item * WorkPanelLayout.TagPitch, column, WorkPanelLayout.Tag), groups[category][item], category);
                        x += column + 8;
                    }
                    y += block;
                }
                var extra = groups[6].Where(type => type.defName != "BasicWorker").ToList();
                if (extra.Count > 0)
                {
                    float column = (available - 6 - (columns - 1) * 8) / columns;
                    Theme.RoundRect(new Rect(3, y + 2, available - 6, .7f), Theme.Border.ToTransparent(.35f), 0);
                    for (int item = 0; item < extra.Count; item++)
                        EditorTag(window, new Rect(3 + (item % columns) * (column + 8), y + 8 + (item / columns) * WorkPanelLayout.TagPitch, column, WorkPanelLayout.Tag), extra[item], 6);
                }
            }
            finally { PawnInspectorWidgets.EndScroll(viewport, ref editorScroll, content, 200002); }
            Rect footer = new Rect(card.x + 5, card.yMax - WorkPanelLayout.EditorFooter, card.width - 10, WorkPanelLayout.EditorFooter);
            Theme.RoundRect(new Rect(footer.x + 3, footer.y, footer.width - 6, .7f), Theme.Border.ToTransparent(.35f), 0);
            activeViewport = footer;
            WorkTypeDef basic = groups[6].FirstOrDefault(type => type.defName == "BasicWorker");
            if (basic != null) EditorTag(window, new Rect(footer.x + 3, footer.y + 5, Math.Min(170, footer.width - 184), WorkPanelLayout.Tag), basic, 6);
            DrawEditorActions(window, footer);
        }
        private static void DrawEditorActions(MainTabWindow_Work window, Rect card)
        {
            Rect clear = new Rect(card.xMax - 8 - Math.Max(42, PawnInspectorView.Measure("Clear") + 18), card.y + 5, Math.Max(42, PawnInspectorView.Measure("Clear") + 18), 20);
            float undoWidth = Math.Max(42, PawnInspectorView.Measure("Undo") + 18);
            Rect undo = new Rect(clear.x - 6 - undoWidth, card.y + 5, undoWidth, 20);
            float autoWidth = Math.Max(76, PawnInspectorView.Measure("Auto assign") + 18);
            Rect auto = new Rect(undo.x - 6 - autoWidth, card.y + 5, autoWidth, 20);
            if (GUI.enabled) TooltipHandler.TipRegion(auto, "Assign work for " + selected.LabelShortCap + " by skill and passion. Keeps basic care and emergency work enabled, with hauling and cleaning at low priority. Skips unavailable work; Undo restores previous assignments." +
                (string.IsNullOrEmpty(notice) ? "" : "\n" + notice));
            if (PawnInspectorWidgets.Button(auto, "Auto assign"))
                WorkPanelNative.AutoAssign(window, selected, out notice);
            bool canUndo = WorkPanelNative.CanUndo(window, selected, out string undoTip);
            if (GUI.enabled) TooltipHandler.TipRegion(undo, undoTip + (string.IsNullOrEmpty(notice) ? "" : "\n" + notice));
            if (PawnInspectorWidgets.Button(undo, "Undo", enabled: canUndo)) WorkPanelNative.Undo(window, selected, out notice);
            bool canClear = WorkPanelNative.CanClear(window, selected);
            if (GUI.enabled) TooltipHandler.TipRegion(clear, "Turn off all available work shown for " + selected.LabelShortCap + ". Undo restores these assignments.");
            if (PawnInspectorWidgets.Button(clear, "Clear", enabled: canClear)) WorkPanelNative.Clear(window, selected, out notice);
        }
        private static void EditorTag(MainTabWindow_Work window, Rect rect, WorkTypeDef type, int category)
        {
            if (!Visible(rect) || selected == null) return;
            WorkPanelCell cell = WorkPanelNative.Cell(selected, type);
            float measured = WorkPanelView.MeasureTag(Label(type), Skill(cell), cell.Passion);
            float tagWidth = WorkPanelLayout.EditorTagWidth(measured, rect.width);
            Rect priority = new Rect(rect.x, rect.y, WorkPanelLayout.PrioritySpace - 3, rect.height);
            Rect tag = new Rect(rect.x + WorkPanelLayout.PrioritySpace, rect.y, tagWidth, rect.height);
            bool assigned = cell.StoredPriority >= 1 && cell.StoredPriority <= 4;
            bool hasPriority = assigned && cell.Editable && WorkPanelNative.Manual;
            string priorityTip = assigned && cell.Editable
                ? WorkPanelView.PriorityName(cell.StoredPriority) + (WorkPanelNative.Manual ? " priority. Click to change." : " stored priority. Turn Priorities on to edit.")
                : cell.Editable ? "Enable this work to set its priority." : "This work is unavailable.";
            if (WorkPanelView.PriorityButton(priority, cell.StoredPriority, hasPriority, priorityTip))
            { WorkPanelNative.CycleEnabledPriority(window, selected, type); notice = ""; Event.current.Use(); return; }
            int click = WorkPanelView.Tag(tag, Label(type), category, Skill(cell), cell.Passion, cell.Priority > 0, cell.Editable,
                cell.Tooltip + "\nClick the tag to " + (cell.Priority > 0 ? "turn off." : "turn on.") + " Numbers inside tags show skill; the separate button sets priority.", cell.Activatable, cell.Incapable);
            if (click > 0) { WorkPanelNative.Toggle(window, selected, type); notice = ""; Event.current.Use(); }
        }
        private static void ShowSort(MainTabWindow_Work window, List<WorkTypeDef> types)
        {
            var options = new List<FloatMenuOption> { new FloatMenuOption("Name", () => { if (Eligible(window)) WorkPanelNative.Sort(window, null); }) };
            foreach (PawnColumnDef status in WorkPanelNative.StatusColumns(window).Where(column => column.sortable))
            {
                PawnColumnDef captured = status; string label = status.LabelCap;
                if (string.IsNullOrEmpty(label)) label = string.IsNullOrEmpty(status.headerTip) ? status.defName : status.headerTip.Translate().ToString();
                options.Add(new FloatMenuOption(label, () => { if (Eligible(window)) WorkPanelNative.SortColumn(window, captured); }));
            }
            foreach (WorkTypeDef type in types)
            {
                WorkTypeDef captured = type;
                options.Add(new FloatMenuOption(Label(type), () => { if (Eligible(window)) WorkPanelNative.Sort(window, captured); }));
            }
            var menu = new FloatMenu(options); ArchitectPopup.RegisterWork(menu); Find.WindowStack.Add(menu);
        }
    }
}
