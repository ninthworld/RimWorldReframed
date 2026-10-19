using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal static partial class RosterMenus
    {
        private static readonly FieldInfo NativeTable = AccessTools.DeclaredField(typeof(MainTabWindow_PawnTable), "table");
        private static readonly MethodInfo NativeLabel = AccessTools.DeclaredMethod(typeof(PawnColumnWorker_Label), "GetLabel", new[] { typeof(Pawn) });
        private static readonly MethodInfo HeaderClick = AccessTools.DeclaredMethod(typeof(PawnColumnWorker), "HeaderClicked", new[] { typeof(Rect), typeof(PawnTable) });
        private static readonly MethodInfo HeaderTip = AccessTools.DeclaredMethod(typeof(PawnColumnWorker), "GetHeaderTip", new[] { typeof(PawnTable) });
        private static readonly MethodInfo PaletteFor = AccessTools.DeclaredMethod(typeof(TimeAssignmentSelector), "DrawTimeAssignmentSelectorFor", new[] { typeof(Rect), typeof(TimeAssignmentDef) });
        internal static bool Ready => NativeTable?.FieldType == typeof(PawnTable) && NativeLabel?.ReturnType == typeof(TaggedString) &&
            HeaderClick?.ReturnType == typeof(void) && HeaderTip?.ReturnType == typeof(string) && PaletteFor?.ReturnType == typeof(void);
        private static PawnTable Table(Window owner) => NativeTable?.GetValue(owner) as PawnTable;
        internal static bool Supports(Window owner)
        {
            if (!Ready || owner == null || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null) return false;
            Type type = owner.GetType();
            if (type != typeof(MainTabWindow_Schedule) && type != typeof(MainTabWindow_Assign) && type != typeof(MainTabWindow_Animals) && type != typeof(MainTabWindow_Wildlife)) return false;
            PawnTable table = Table(owner);
            if (table == null || table.GetType().Assembly != typeof(PawnTable).Assembly) return false;
            bool label = false;
            foreach (PawnColumnDef column in table.Columns)
            {
                if (column?.Worker == null || column.Worker.GetType().Assembly != typeof(PawnColumnWorker).Assembly) return false;
                // A foreign patch may add actual header controls, not just paint.
                // Compact headers cannot safely infer such additions.
                for (Type worker = column.Worker.GetType(); worker != null && typeof(PawnColumnWorker).IsAssignableFrom(worker); worker = worker.BaseType)
                    if (ForeignPatch(AccessTools.DeclaredMethod(worker, "DoHeader", new[] { typeof(Rect), typeof(PawnTable) }))) return false;
                if (column.Worker is PawnColumnWorker_Timetable)
                {
                    if (!(owner is MainTabWindow_Schedule) || column.defName != "Timetable") return false;
                    if (ForeignPatch(AccessTools.Method(column.Worker.GetType(), "DoCell", new[] { typeof(Rect), typeof(Pawn), typeof(PawnTable) })) || ForeignPatch(PaletteFor)) return false;
                }
                if (column.Worker is PawnColumnWorker_Label)
                { if (column.Worker.GetType() != typeof(PawnColumnWorker_Label) || label) return false; label = true; }
            }
            return label;
        }
        private static bool ForeignPatch(MethodBase method)
        {
            if (method == null) return false;
            Patches patches = Harmony.GetPatchInfo(method);
            if (patches == null) return false;
            foreach (string owner in patches.Owners) if (owner != "ninthworld.reframedui") return true;
            return false;
        }
        private static PawnColumnDef Column(PawnTable table, string name)
        { foreach (PawnColumnDef column in table.Columns) if (column.defName == name) return column; return null; }
        private static PawnColumnDef LabelColumn(PawnTable table)
        { foreach (PawnColumnDef column in table.Columns) if (column.Worker is PawnColumnWorker_Label) return column; return null; }
        private static string Caption(PawnColumnDef column)
        {
            if (!column.label.NullOrEmpty()) return column.LabelCap;
            if (column.trainable != null) return column.trainable.LabelCap;
            switch (column.defName)
            {
                case "Gender": return "Sex";
                case "MedicalCare": return "Medicine";
                case "HostilityResponse": return "Response";
                case "Hunt": return "Hunt";
                case "Tame": return "Tame";
                case "ManhunterOnDamageChance": return "Risk";
                case "ManhunterOnTameFailChance": return "Risk";
                case "Pregnant": return "Pregnancy";
                case "Predator": return "Predator";
                case "MentalState": return "Mental state";
                case "CopyPasteTimetable": return "Copy / paste";
                default: return column.defName;
            }
        }
        private static void HeaderLabel(Rect rect, PawnColumnDef column, PawnTable table)
        {
            if (column == null) return;
            // Icon-sized primary columns retain their own native semantic icon.
            // Long explanatory header tips belong only in tooltips.
            if (rect.width <= 56 && column.HeaderIcon != null)
            {
                using (new Theme.GuiScope(true))
                {
                    GUI.color = Theme.Muted;
                    GUI.DrawTexture(new Rect(rect.center.x - 7, rect.center.y - 7, 14, 14), column.HeaderIcon, ScaleMode.ScaleToFit);
                }
            }
            else PawnInspectorView.SmallLabel(rect, Caption(column), Theme.Muted);
            using (PopupScope?.Invoke(rect))
            {
                if (GUI.enabled) TooltipHandler.TipRegion(rect, (string)HeaderTip.Invoke(column.Worker, new object[] { table }));
                if (column.HeaderInteractable && Widgets.ButtonInvisible(rect)) HeaderClick.Invoke(column.Worker, new object[] { rect, table });
            }
        }
        private static void SortMenu(PawnTable table)
        {
            var options = new List<FloatMenuOption> { new FloatMenuOption("Default order", () => table.SortBy(null, false)) };
            foreach (PawnColumnDef column in table.Columns)
            {
                if (!column.sortable) continue;
                PawnColumnDef captured = column;
                options.Add(new FloatMenuOption(Caption(column) + " ↑", () => table.SortBy(captured, true)));
                options.Add(new FloatMenuOption(Caption(column) + " ↓", () => table.SortBy(captured, false)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
        private static bool Dropdown(PawnColumnDef column, Pawn pawn) => column.Worker is PawnColumnWorker_FoodRestriction ||
            column.Worker is PawnColumnWorker_DrugPolicy || column.Worker is PawnColumnWorker_Reading || column.Worker is PawnColumnWorker_Master ||
            column.Worker is PawnColumnWorker_Outfit && pawn.outfits?.forcedHandler.SomethingIsForced != true;
        private static void Cell(Rect rect, PawnColumnDef column, Pawn pawn, PawnTable table, bool input, bool dropdown)
        {
            using (PopupScope?.Invoke(rect))
            {
                if (dropdown)
                {
                    Theme.ButtonFace(rect.ContractedBy(0, 2), GUI.enabled && input && Mouse.IsOver(rect) ? Theme.Hover : Theme.Surface, !input);
                    Rect native = rect; native.width = Math.Max(20, native.width - 16);
                    Native(() =>
                    {
                        Vector2 mouse = Event.current.mousePosition;
                        // The attached chevron is part of this same real control.
                        // Map its trailing16px to the adjacent native button;
                        // preserve the original event/type/button and native GUI capture.
                        if (rect.Contains(mouse) && mouse.x >= native.xMax) Event.current.mousePosition = new Vector2(native.xMax - 2, mouse.y);
                        try { column.Worker.DoCell(native, pawn, table); }
                        finally { Event.current.mousePosition = mouse; }
                    }, input);
                    Arrow(new Rect(rect.xMax - 12, rect.center.y - 4, 8, 8));
                }
                else Native(() => column.Worker.DoCell(rect, pawn, table), input);
            }
        }
        private static void Palette(Rect rect, bool input)
        {
            TimeAssignmentDef[] choices = ModsConfig.RoyaltyActive ? new[] { TimeAssignmentDefOf.Anything, TimeAssignmentDefOf.Work, TimeAssignmentDefOf.Joy, TimeAssignmentDefOf.Sleep, TimeAssignmentDefOf.Meditate } :
                new[] { TimeAssignmentDefOf.Anything, TimeAssignmentDefOf.Work, TimeAssignmentDefOf.Joy, TimeAssignmentDefOf.Sleep };
            float width = Math.Min(110, (rect.width - (choices.Length - 1) * 6) / choices.Length);
            for (int i = 0; i < choices.Length; i++)
            {
                TimeAssignmentDef choice = choices[i]; Rect chip = new Rect(rect.x + i * (width + 6), rect.y, width, rect.height);
                Native(() => { GUI.color = Color.clear; PaletteFor.Invoke(null, new object[] { chip, choice }); }, input);
                Theme.ButtonFace(chip, TimeAssignmentSelector.selectedAssignment == choice ? Theme.Selected : Theme.Surface, !input);
                GUI.DrawTexture(new Rect(chip.x + 7, chip.center.y - 3, 6, 6), choice.ColorTexture);
                PawnInspectorView.Label(new Rect(chip.x + 19, chip.y, chip.width - 23, chip.height), choice.LabelCap, Theme.Ink);
            }
        }
        private static void Timeline(Rect rect, Pawn pawn, PawnTable table, bool input)
        {
            PawnColumnDef column = Column(table, "Timetable");
            if (column == null || pawn.timetable == null || pawn.IsSubhuman) return;
            Native(() => { GUI.color = Color.clear; column.Worker.DoCell(rect, pawn, table); }, input);
            for (int hour = 0; hour < 24; hour++)
            {
                Rect tile = new Rect(rect.x + hour * rect.width / 24, rect.y, rect.width / 24, rect.height).ContractedBy(1);
                TimeAssignmentDef assignment = pawn.timetable.GetAssignment(hour);
                GUI.DrawTexture(tile, assignment.ColorTexture, ScaleMode.StretchToFill, true, 0, Color.white, 0, 2);
                if (input && Mouse.IsOver(tile))
                {
                    Theme.RoundRect(new Rect(tile.x, tile.yMax - 2, tile.width, 2), Theme.Ink, 1);
                    TooltipHandler.TipRegion(tile, hour.ToString("00") + ":00–" + (hour + 1).ToString("00") + ":00 · " + assignment.LabelCap);
                }
            }
        }
        private static void Native(Action draw, bool input)
        {
            using (new Theme.GuiScope(true))
            {
                bool prior = Drawing; Drawing = true; Vector2 mouse = Event.current.mousePosition;
                try { if (!input) { GUI.enabled = false; Event.current.mousePosition = new Vector2(-100000, -100000); } draw(); }
                finally { Event.current.mousePosition = mouse; Drawing = prior; }
            }
        }
    }
}

