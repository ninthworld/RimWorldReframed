using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal static partial class RosterMenus
    {
        private sealed class View
        {
            internal Vector2 Scroll, DetailScroll;
            internal Pawn Selected;
            internal bool Open;
            internal int Group;
            internal readonly RosterMenusMotion Height = new RosterMenusMotion();
        }
        private static readonly ConditionalWeakTable<Window, View> views = new ConditionalWeakTable<Window, View>();
        internal static Func<Rect, IDisposable> PopupScope;
        internal static bool Drawing { get; private set; }
        internal static void Reset(Window owner) { if (owner != null) views.Remove(owner); }
        private static float ListHeight(PawnTable table) => RosterMenusLayout.Extent(Math.Min(8, table.PawnsListForReading.Count), RosterMenusLayout.Row);
        private static float MainHeight(Window owner, PawnTable table) => 28 + (owner is MainTabWindow_Schedule ? 34 : 0) + 20 + Math.Max(32, ListHeight(table));
        private static void UpdateHeight(Window owner, View view, PawnTable table)
        {
            if (view.Selected != null && !table.PawnsListForReading.Contains(view.Selected)) view.Open = false;
            float detail = view.Open && view.Selected != null ? DetailHeight(owner, view, table) + 8 : 0;
            view.Height.Set(66 + MainHeight(owner, table) + detail, Time.realtimeSinceStartup);
            if (!view.Open && view.Height.Settled) view.Selected = null;
        }
        internal static Vector2 PreferredSize(Window owner)
        {
            if (!Supports(owner)) return new Vector2(680, 200);
            View view = views.GetOrCreateValue(owner); PawnTable table = Table(owner);
            UpdateHeight(owner, view, table);
            return new Vector2(owner is MainTabWindow_Schedule ? 780 : owner is MainTabWindow_Animals ? 680 : owner is MainTabWindow_Assign ? 660 : 620, view.Height.Value);
        }
        internal static void Draw(Window owner, Rect body)
        {
            if (!Supports(owner) || Event.current.type == EventType.Layout) return;
            View view = views.GetOrCreateValue(owner); PawnTable table = Table(owner);
            UpdateHeight(owner, view, table);
            using (new Theme.GuiScope(true))
            {
                Vector2 mouse = Event.current.mousePosition;
                if (!view.Height.Settled) { GUI.enabled = false; Event.current.mousePosition = new Vector2(-100000, -100000); }
                try
                {
                    GUI.color = GUI.contentColor = Color.white; Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = false;
                    Rect inner = body.ContractedBy(6);
                    Toolbar(owner, new Rect(inner.x, inner.y, inner.width, 24), table);
                    float y = inner.y + 28;
                    if (owner is MainTabWindow_Schedule) { Palette(new Rect(inner.x, y, inner.width, 28), GUI.enabled); y += 34; }
                    List<PawnColumnDef> primary = Primary(owner, table);
                    float controls = 0; foreach (PawnColumnDef column in primary) controls += PrimaryWidth(column) + 6;
                    float identity = owner is MainTabWindow_Schedule ? 154 : Math.Max(146, inner.width - controls - 32);
                    List<Pawn> pawns = table.PawnsListForReading;
                    float rosterHeight = Math.Min(Math.Max(32, ListHeight(table)), Math.Max(32, inner.yMax - (y + 20)));
                    float total = RosterMenusLayout.Extent(pawns.Count, RosterMenusLayout.Row);
                    float usableWidth = RosterMenusLayout.ContentWidth(inner.width, rosterHeight, total);
                    Rect header = new Rect(inner.x, y, inner.width, 18);
                    HeaderLabel(new Rect(header.x + 30, y, identity - 30, 18), LabelColumn(table), table);
                    float x = header.x + identity;
                    if (owner is MainTabWindow_Schedule)
                    {
                        float timeline = usableWidth - identity - 82;
                        for (int hour = 0; hour < 24; hour++) PawnInspectorView.SmallLabel(new Rect(x + hour * timeline / 24, y, timeline / 24, 18), hour % 3 == 0 ? hour.ToString() : "", Theme.Muted);
                    }
                    else foreach (PawnColumnDef column in primary)
                    { HeaderLabel(new Rect(x, y, PrimaryWidth(column), 18), column, table); x += PrimaryWidth(column) + 6; }
                    y += 20;
                    Rect viewport = new Rect(inner.x, y, inner.width, rosterHeight);
                    float width = PawnInspectorWidgets.BeginScroll(viewport, ref view.Scroll, total);
                    try
                    {
                        for (int index = 0; index < pawns.Count; index++)
                        {
                            Pawn pawn = pawns[index]; Rect row = new Rect(0, index * (RosterMenusLayout.Row + RosterMenusLayout.Gap), width, RosterMenusLayout.Row);
                            if (!RosterMenusLayout.Visible(row.y, row.height, view.Scroll.y, viewport.height)) continue;
                            bool input = GUI.enabled && RosterMenusLayout.FullRow(row.y, row.height, view.Scroll.y, viewport.height);
                            Theme.RoundRect(row, view.Selected == pawn ? Theme.Selected.ToTransparent(.7f) : Theme.Surface.ToTransparent(.28f), 3);
                            Identity(new Rect(0, row.y, identity - 6, row.height), pawn, LabelColumn(table), view, input);
                            float cellX = identity;
                            if (owner is MainTabWindow_Schedule)
                            {
                                float timelineWidth = width - identity - 82;
                                Timeline(new Rect(cellX, row.y + 6, timelineWidth, 20), pawn, table, input);
                                PawnColumnDef copy = Column(table, "CopyPasteTimetable");
                                if (copy != null) Cell(new Rect(width - 74, row.y + 1, 42, 30), copy, pawn, table, input, false);
                            }
                            else foreach (PawnColumnDef column in primary)
                            { Cell(new Rect(cellX, row.y + 1, PrimaryWidth(column), 30), column, pawn, table, input, Dropdown(column, pawn)); cellX += PrimaryWidth(column) + 6; }
                            Rect more = new Rect(width - 26, row.y + 3, 22, 26);
                            Native(() =>
                            {
                                Theme.ButtonFace(more, GUI.enabled && Mouse.IsOver(more) ? Theme.Hover : Theme.Surface, !GUI.enabled);
                                Arrow(more.ContractedBy(7), view.Selected == pawn && view.Open);
                                if (GUI.enabled) TooltipHandler.TipRegion(more, "View all settings and bulk controls");
                                if (Widgets.ButtonInvisible(more)) Toggle(view, pawn);
                            }, input);
                        }
                        if (pawns.Count == 0) PawnInspectorView.Label(new Rect(8, 2, width - 16, 30), "None".Translate(), Theme.Muted);
                    }
                    finally { PawnInspectorWidgets.EndScroll(viewport, ref view.Scroll, total, 310291); }
                    y = viewport.yMax + 8;
                    if (view.Selected != null && inner.yMax > y) Details(owner, new Rect(inner.x, y, inner.width, inner.yMax - y), view, table);
                }
                finally { Event.current.mousePosition = mouse; }
            }
        }
        private static void Toggle(View view, Pawn pawn)
        {
            if (view.Selected == pawn) view.Open = !view.Open;
            else { view.Selected = pawn; view.Open = true; view.Group = 0; view.DetailScroll = Vector2.zero; }
        }
        private static List<PawnColumnDef> Primary(Window owner, PawnTable table)
        {
            var result = new List<PawnColumnDef>();
            string[] names = owner is MainTabWindow_Assign ? new[] { "FoodRestriction", "DrugPolicy", "MedicalCare", "HostilityResponse" } :
                owner is MainTabWindow_Animals ? new[] { "Master", "Bond", "MedicalCare", "Slaughter" } :
                owner is MainTabWindow_Wildlife ? new[] { "Hunt", "ManhunterOnDamageChance", "Tame", "ManhunterOnTameFailChance", "Predator", "Info" } : new string[0];
            foreach (string name in names) { PawnColumnDef column = Column(table, name); if (column != null) result.Add(column); }
            return result;
        }
        private static float PrimaryWidth(PawnColumnDef column)
        {
            switch (column.defName)
            {
                case "FoodRestriction": case "DrugPolicy": return 130;
                case "Master": return 120;
                case "ManhunterOnDamageChance": case "ManhunterOnTameFailChance": return 50;
                default: return 30;
            }
        }
        private static void Toolbar(Window owner, Rect rect, PawnTable table)
        {
            PawnInspectorView.SmallLabel(new Rect(rect.x, rect.y, rect.width - 270, 24), table.PawnsListForReading.Count + (owner is MainTabWindow_Animals || owner is MainTabWindow_Wildlife ? " animals" : " colonists"), Theme.Muted);
            if (owner is MainTabWindow_Animals)
            {
                Rect slaughter = new Rect(rect.xMax - 258, rect.y, 164, 24);
                using (PopupScope?.Invoke(slaughter)) if (PawnInspectorWidgets.Button(slaughter, "ManageAutoSlaughter".Translate())) Find.WindowStack.Add(new Dialog_AutoSlaughter(Find.CurrentMap));
            }
            Rect sort = new Rect(rect.xMax - 86, rect.y, 86, 24);
            using (PopupScope?.Invoke(sort)) if (MenuButton(sort, "Sort")) SortMenu(table);
        }
        private static void Identity(Rect rect, Pawn pawn, PawnColumnDef labelColumn, View view, bool input)
        {
            Native(() =>
            {
                Rect portrait = new Rect(rect.x + 2, rect.y + 2, 26, rect.height - 4);
                PawnInspectorView.Portrait(portrait, pawn);
                string name = ((TaggedString)NativeLabel.Invoke(labelColumn.Worker, new object[] { pawn })).Resolve();
                Color color = pawn.IsSlave || pawn.IsColonyMech ? PawnNameColorUtility.PawnNameColorOf(pawn) : Theme.Ink;
                PawnInspectorView.Label(new Rect(rect.x + 32, rect.y, rect.width - 34, rect.height), name, color, view.Selected == pawn);
                if (pawn.health.summaryHealth.SummaryHealthPercent < .99f) PawnInspectorView.Bar(new Rect(rect.x + 32, rect.yMax - 3, rect.width - 36, 2), pawn.health.summaryHealth.SummaryHealthPercent, Theme.Danger);
                if (GUI.enabled) TooltipHandler.TipRegion(rect, pawn.GetTooltip());
                if (Widgets.ButtonInvisible(portrait)) { CameraJumper.TryJumpAndSelect(pawn); if (Event.current.button == 0) Find.MainTabsRoot.EscapeCurrentTab(playSound: false); }
                if (Widgets.ButtonInvisible(new Rect(rect.x + 30, rect.y, rect.width - 30, rect.height))) Toggle(view, pawn);
            }, input);
        }
        private static void Arrow(Rect rect, bool up = false)
        {
            using (new Theme.GuiScope(true)) { GUI.color = Theme.Muted; if (up) UI.RotateAroundPivot(180, rect.center); GUI.DrawTexture(rect, RightHudIcons.Get("chevron")); }
        }
        private static bool MenuButton(Rect rect, string label)
        {
            Theme.ButtonFace(rect, GUI.enabled && Mouse.IsOver(rect) ? Theme.Hover : Theme.Surface, !GUI.enabled);
            PawnInspectorView.Label(new Rect(rect.x + 7, rect.y, Math.Max(1, rect.width - 26), rect.height), label, Theme.Ink);
            Arrow(new Rect(rect.xMax - 15, rect.center.y - 4, 8, 8));
            return Widgets.ButtonInvisible(rect);
        }
    }
}
