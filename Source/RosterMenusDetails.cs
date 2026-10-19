using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal static partial class RosterMenus
    {
        private static string[] Groups(Window owner) => owner is MainTabWindow_Assign ? new[] { "Policies", "Care & identity" } :
            owner is MainTabWindow_Animals ? new[] { "Care", "Training", "Details" } : owner is MainTabWindow_Schedule ? new[] { "Restrictions & identity" } : new[] { "Wildlife details" };
        private static int GroupOf(Window owner, PawnColumnDef column)
        {
            if (owner is MainTabWindow_Assign) return column.Worker is PawnColumnWorker_Outfit || column.Worker is PawnColumnWorker_FoodRestriction || column.Worker is PawnColumnWorker_DrugPolicy || column.Worker is PawnColumnWorker_Reading || column.Worker is PawnColumnWorker_Carry ? 0 : 1;
            if (owner is MainTabWindow_Animals)
            {
                string name = column.defName;
                if (column.trainable != null || name.StartsWith("Follow") || name.StartsWith("Animal") || name == "Master" || name == "SpecialTrainable") return 1;
                if (name == "MedicalCare" || name == "Slaughter" || name == "Sterile" || name == "ReleaseAnimalToWild" || name.StartsWith("AllowedArea")) return 0;
                return 2;
            }
            return 0;
        }
        private static List<PawnColumnDef> DetailColumns(Window owner, View view, PawnTable table)
        {
            var result = new List<PawnColumnDef>();
            foreach (PawnColumnDef column in table.Columns)
            {
                if (column.Worker is PawnColumnWorker_Label || column.Worker is PawnColumnWorker_Gap || column.Worker is PawnColumnWorker_RemainingSpace || column.Worker is PawnColumnWorker_Timetable) continue;
                if (GroupOf(owner, column) == view.Group) result.Add(column);
            }
            return result;
        }
        private static bool Management(PawnColumnDef column) => column.Worker is PawnColumnWorker_Outfit || column.Worker is PawnColumnWorker_FoodRestriction ||
            column.Worker is PawnColumnWorker_DrugPolicy || column.Worker is PawnColumnWorker_Reading || column.Worker is PawnColumnWorker_AllowedArea;
        private static float ControlHeight(PawnColumnDef column, Pawn pawn) => 17 + Math.Max(30, column.Worker.GetMinCellHeight(pawn)) + (Management(column) ? 36 : 0);
        private static float DetailContent(Window owner, View view, PawnTable table)
        {
            List<PawnColumnDef> columns = DetailColumns(owner, view, table); float total = 0;
            for (int i = 0; i < columns.Count; i += 2)
                total += Math.Max(ControlHeight(columns[i], view.Selected), i + 1 < columns.Count ? ControlHeight(columns[i + 1], view.Selected) : 0) + 6;
            return Math.Max(32, total);
        }
        private static float DetailHeight(Window owner, View view, PawnTable table) => 42 + Math.Min(252, DetailContent(owner, view, table));
        private static void Details(Window owner, Rect rect, View view, PawnTable table)
        {
            Theme.RoundRect(rect, Theme.Surface.ToTransparent(.32f), 4);
            GUI.BeginClip(rect);
            try
            {
                float width = rect.width;
                PawnInspectorView.Label(new Rect(8, 3, width * .40f, 27), view.Selected.LabelShortCap, Theme.Ink, true);
                string[] groups = Groups(owner);
                float groupWidth = Math.Min(170, width * .42f);
                Rect group = new Rect(width - groupWidth - 8, 4, groupWidth, 25);
                using (PopupScope?.Invoke(group))
                    if (MenuButton(group, groups[Math.Min(groups.Length - 1, view.Group)]))
                    {
                        var options = new List<FloatMenuOption>();
                        for (int i = 0; i < groups.Length; i++)
                        { int captured = i; options.Add(new FloatMenuOption(groups[i], () => { view.Group = captured; view.DetailScroll = Vector2.zero; })); }
                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                Rect viewport = new Rect(8, 36, width - 16, Math.Max(1, rect.height - 42));
                List<PawnColumnDef> columns = DetailColumns(owner, view, table);
                float total = DetailContent(owner, view, table);
                float contentWidth = PawnInspectorWidgets.BeginScroll(viewport, ref view.DetailScroll, total);
                try
                {
                    float y = 0;
                    for (int i = 0; i < columns.Count; i += 2)
                    {
                        float height = Math.Max(ControlHeight(columns[i], view.Selected), i + 1 < columns.Count ? ControlHeight(columns[i + 1], view.Selected) : 0);
                        for (int offset = 0; offset < 2 && i + offset < columns.Count; offset++)
                        {
                            PawnColumnDef column = columns[i + offset];
                            float cardWidth = (contentWidth - 10) / 2;
                            Rect card = new Rect(offset * (cardWidth + 10), y, cardWidth, ControlHeight(column, view.Selected));
                            bool input = GUI.enabled && view.Open && view.Height.Settled;
                            Rect heading = new Rect(card.x + 2, card.y, card.width - 4, 16);
                            Native(() => HeaderLabel(heading, column, table), input && RosterMenusLayout.FullRow(heading.y, heading.height, view.DetailScroll.y, viewport.height));
                            Rect cell = new Rect(card.x, card.y + 17, card.width, Math.Max(30, column.Worker.GetMinCellHeight(view.Selected)));
                            Cell(cell, column, view.Selected, table, input && RosterMenusLayout.FullRow(cell.y, cell.height, view.DetailScroll.y, viewport.height), Dropdown(column, view.Selected));
                            if (Management(column))
                            {
                                Rect manage = new Rect(card.x, cell.yMax + 4, card.width, 32);
                                Native(() =>
                                {
                                    GUI.BeginClip(manage);
                                    try
                                    {
                                        using (PopupScope?.Invoke(new Rect(0, 0, manage.width, 32)))
                                        {
                                            Vector2 mouse = Event.current.mousePosition;
                                            if (!Mouse.IsOver(new Rect(0, 0, manage.width, 32))) Event.current.mousePosition = new Vector2(-100000, -100000);
                                            try { column.Worker.DoHeader(new Rect(0, 0, manage.width, 65), table); }
                                            finally { Event.current.mousePosition = mouse; }
                                        }
                                    }
                                    finally { GUI.EndClip(); }
                                }, input && RosterMenusLayout.FullRow(manage.y, manage.height, view.DetailScroll.y, viewport.height));
                            }
                        }
                        y += height + 6;
                    }
                    if (columns.Count == 0) PawnInspectorView.Label(new Rect(0, 0, contentWidth, 30), "None".Translate(), Theme.Muted);
                }
                finally { PawnInspectorWidgets.EndScroll(viewport, ref view.DetailScroll, total, 310292); }
            }
            finally { GUI.EndClip(); }
        }
    }
}
