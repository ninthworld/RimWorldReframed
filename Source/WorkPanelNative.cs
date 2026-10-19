using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ReframedUI
{
    internal readonly struct WorkPanelCell
    {
        internal readonly int Priority, StoredPriority;
        internal readonly float? Skill;
        internal readonly Passion Passion;
        internal readonly bool Enabled, Editable, Activatable, Incapable;
        internal readonly string DisabledReason, Tooltip;

        internal WorkPanelCell(int priority, int storedPriority, float? skill, Passion passion, bool editable, bool activatable,
            bool incapable, string reason, string tooltip)
        {
            Priority = priority; StoredPriority = storedPriority; Skill = skill; Passion = passion; Enabled = priority > 0;
            Editable = editable; Activatable = activatable; Incapable = incapable; DisabledReason = reason; Tooltip = tooltip;
        }
    }

    internal static class WorkPanelNative
    {
        private static readonly FieldInfo TableField = AccessTools.DeclaredField(typeof(MainTabWindow_PawnTable), "table");
        private static readonly MethodInfo IncapableMethod = AccessTools.DeclaredMethod(typeof(PawnColumnWorker_WorkPriority),
            "IsIncapableOfWholeWorkType", new[] { typeof(Pawn), typeof(WorkTypeDef) });
        private static readonly MethodInfo CopyMethod = AccessTools.DeclaredMethod(typeof(PawnColumnWorker_CopyPasteWorkPriorities),
            "CopyFrom", new[] { typeof(Pawn) });
        private static readonly MethodInfo PasteMethod = AccessTools.DeclaredMethod(typeof(PawnColumnWorker_CopyPasteWorkPriorities),
            "PasteTo", new[] { typeof(Pawn) });
        private static readonly MethodInfo ClipboardMethod = AccessTools.DeclaredMethod(typeof(PawnColumnWorker_CopyPasteWorkPriorities),
            "get_AnythingInClipboard", Type.EmptyTypes);
        private static readonly MethodInfo HeaderMethod = AccessTools.DeclaredMethod(typeof(PawnColumnWorker_WorkPriority),
            "HeaderClicked", new[] { typeof(Rect), typeof(PawnTable) });
        private static readonly PawnColumnWorker_WorkPriority CapacityWorker = new PawnColumnWorker_WorkPriority();
        private static readonly PawnColumnWorker_CopyPasteWorkPriorities DefaultCopyWorker = new PawnColumnWorker_CopyPasteWorkPriorities();
        private static bool checkboxActivation, ownMutation;
        private static Pawn_WorkSettings setterSettings;
        private static WorkTypeDef setterType;
        private static int setterPriority;
        private static readonly WorkPanelPriorityMemory<Pawn_WorkSettings, WorkTypeDef> priorities
            = new WorkPanelPriorityMemory<Pawn_WorkSettings, WorkTypeDef>();
        private static Game sessionGame;
        private static Map sessionMap;
        private static bool sessionManual;
        internal static bool CheckboxSeamReady;
        private static UndoState undo;
        internal static int LastChangeCount { get; private set; }
        internal static string LastNotice { get; private set; } = "";

        internal static bool Ready => TableField?.FieldType == typeof(PawnTable) && IncapableMethod?.ReturnType == typeof(bool)
            && CopyMethod != null && PasteMethod != null && HeaderMethod != null && ClipboardMethod?.ReturnType == typeof(bool) && CheckboxSeamReady;
        internal static bool Manual => Current.Game != null && Current.Game.playSettings.useWorkPriorities;

        private static PawnTable Table(MainTabWindow_Work window) => window == null ? null : TableField?.GetValue(window) as PawnTable;

        // These are the table's actual cached roster and visible columns, including mod ordering.
        // Do not call EnableAndInitializeIfNotAlreadyInitialized from any read path.
        internal static List<Pawn> Roster(MainTabWindow_Work window)
            => Table(window)?.PawnsListForReading ?? new List<Pawn>();

        internal static List<WorkTypeDef> WorkTypes(MainTabWindow_Work window)
        {
            var result = new List<WorkTypeDef>();
            var columns = Table(window)?.Columns;
            if (columns != null)
                foreach (var column in columns)
                    if (column.workType != null && !result.Contains(column.workType)) result.Add(column.workType);
            return result;
        }

        private static bool StatusColumn(PawnColumnDef column)
        {
            string expected;
            switch (column.defName)
            {
                case "Guest": expected = "RimWorld.PawnColumnWorker_Guest"; break;
                case "Faction": expected = "RimWorld.PawnColumnWorker_Faction"; break;
                case "Ideo": expected = "RimWorld.PawnColumnWorker_Ideo"; break;
                case "Xenotype": expected = "RimWorld.PawnColumnWorker_Xenotype"; break;
                default: return false;
            }
            return column.Worker.GetType().FullName == expected;
        }

        internal static bool Compatible(MainTabWindow_Work window)
        {
            if (!Ready) return false;
            var columns = Table(window)?.Columns;
            if (columns == null) return false;
            foreach (var column in columns)
            {
                Type worker = column.Worker.GetType();
                if (column.workType != null)
                {
                    if (worker != typeof(PawnColumnWorker_WorkPriority)) return false;
                    continue;
                }
                if (StatusColumn(column)) continue;
                if (column.defName == "LabelWithIcon" && worker == typeof(PawnColumnWorker_Label)) continue;
                if (column.defName == "CopyPasteWorkPriorities" && worker == typeof(PawnColumnWorker_CopyPasteWorkPriorities)) continue;
                if (column.defName == "RemainingSpace" && worker == typeof(PawnColumnWorker_RemainingSpace)) continue;
                return false;
            }
            return true;
        }

        internal static List<PawnColumnDef> StatusColumns(MainTabWindow_Work window)
        {
            var result = new List<PawnColumnDef>();
            var columns = Table(window)?.Columns;
            if (columns != null) foreach (var column in columns) if (StatusColumn(column)) result.Add(column);
            return result;
        }

        internal static float StatusWidth(MainTabWindow_Work window, PawnColumnDef column)
        {
            if (column == null || Table(window) == null) return 26;
            using (new Theme.GuiScope(true)) return Math.Max(26, column.Worker.GetMinWidth(Table(window)));
        }

        internal static void DrawStatus(Rect rect, Pawn pawn, PawnColumnDef column, MainTabWindow_Work window)
        {
            var table = Table(window);
            if (table == null || pawn == null || column == null || !StatusColumn(column)) return;
            using (new Theme.GuiScope(true)) column.Worker.DoCell(rect, pawn, table);
        }

        // Sort keeps the table's actual comparers and exact native header state cycle.
        // No Event modifiers are replayed here: choosing a menu item can never invoke bulk edits.
        internal static bool Sort(MainTabWindow_Work window, WorkTypeDef type = null)
        {
            var columns = Table(window)?.Columns;
            if (columns == null) return false;
            foreach (var column in columns)
                if (type == null ? column.defName == "LabelWithIcon" : column.workType == type)
                    return SortColumn(window, column);
            return false;
        }

        internal static bool SortColumn(MainTabWindow_Work window, PawnColumnDef column, bool reverse = false)
        {
            var table = Table(window);
            if (!Ready || table == null || column == null || !column.sortable || !table.Columns.Contains(column)) return false;
            if (table.SortingBy != column) table.SortBy(column, !reverse);
            else if (table.SortingDescending != reverse) table.SortBy(column, reverse);
            else { table.SortBy(null, false); SoundDefOf.Tick_Low.PlayOneShotOnCamera(); return true; }
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
            return true;
        }

        internal static bool ActivateHeader(MainTabWindow_Work window, WorkTypeDef type, int mouseButton)
        {
            var ev = Event.current;
            var table = Table(window);
            if (!Ready || table == null || type == null || ev == null || ev.type != EventType.MouseDown ||
                !ev.shift || ev.button != mouseButton || mouseButton < 0 || mouseButton > 1) return false;
            foreach (var column in table.Columns)
            {
                if (column.workType != type || column.Worker.GetType() != typeof(PawnColumnWorker_WorkPriority)) continue;
                Reset();
                using (new Theme.GuiScope(true))
                    HeaderMethod.Invoke(column.Worker, new object[] { new Rect(ev.mousePosition.x, ev.mousePosition.y, 25, 25), table });
                if (ev.type != EventType.Used) ev.Use();
                Invalidate(window);
                return true;
            }
            return false;
        }

        private static bool Usable(Pawn pawn) => pawn != null && !pawn.Destroyed && !pawn.Dead &&
            pawn.workSettings != null && pawn.workSettings.Initialized && pawn.workSettings.EverWork;
        private static bool Available(MainTabWindow_Work window, Pawn pawn) => Usable(pawn) && Roster(window).Contains(pawn);

        private static bool Incapable(Pawn pawn, WorkTypeDef type)
            => (bool)IncapableMethod.Invoke(CapacityWorker, new object[] { pawn, type });

        // Saved native number, including while checkbox mode masks an enabled job's effective priority to3.
        // Never initialize settings or use the effective value as a fallback for missing raw data.
        internal static int StoredPriority(Pawn pawn, WorkTypeDef type)
            => Usable(pawn) && type != null ? WorkPanelAudit.RawPriority(pawn.workSettings, type) ?? 0 : 0;

        internal static WorkPanelCell Cell(Pawn pawn, WorkTypeDef type)
        {
            if (!Usable(pawn) || type == null)
                return new WorkPanelCell(0, 0, null, Passion.None, false, false, false, "Work settings unavailable", "");
            bool disabled = pawn.WorkTypeIsDisabled(type);
            bool incapable = Incapable(pawn, type);
            bool skilled = pawn.skills != null && type.relevantSkills != null && type.relevantSkills.Count > 0;
            float? skill = skilled ? (float?)pawn.skills.AverageOfRelevantSkillsFor(type) : null;
            Passion passion = skilled ? pawn.skills.MaxPassionOfRelevantSkillsFor(type) : Passion.None;
            string reason = disabled ? string.Join("; ", pawn.GetReasonsForDisabledWorkType(type)) :
                incapable ? "IncapableOfWorkTypeBecauseOfCapacities".Translate().ToString() : "";
            bool activatable = !disabled || PawnUtility.IsWorkTypeDisabledByAge(pawn, type, out _);
            return new WorkPanelCell(pawn.workSettings.GetPriority(type), StoredPriority(pawn, type), skill, passion, !disabled, activatable, incapable,
                reason, WidgetsWork.TipForPawnWorker(pawn, type, incapable));
        }

        internal static void Invalidate(MainTabWindow_Work window) => Table(window)?.SetDirty();
        internal static void Reset() { undo = null; LastChangeCount = 0; LastNotice = ""; }
        internal static void ResetSession()
        {
            Reset(); priorities.Clear(); sessionGame = null; sessionMap = null;
        }
        private static void EnsureSession()
        {
            if (sessionGame != Current.Game || sessionMap != Find.CurrentMap)
            { ResetSession(); sessionGame = Current.Game; sessionMap = Find.CurrentMap; }
            // Mode changes invalidate Undo but keep remembered enabled numbers for currently Off jobs.
            if (sessionManual != Manual) { Reset(); sessionManual = Manual; }
        }
        private static bool Result(bool success, int count, string reason)
        { LastChangeCount = count; LastNotice = reason; return success; }

        internal static bool SetManual(MainTabWindow_Work window, bool value)
        {
            if (!Ready || Current.Game == null || Manual == value) return false;
            Reset();
            Current.Game.playSettings.useWorkPriorities = value;
            // Exact native checkbox scope: all player pawns, including world and temporary pawns.
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_Alive)
                if (pawn.Faction == Faction.OfPlayer && pawn.workSettings != null)
                    pawn.workSettings.Notify_UseWorkPrioritiesChanged();
            Invalidate(window);
            return true;
        }

        // Caller invokes on the ORIGINAL MouseDown before Event.Use; no synthetic events or reimplemented actions.
        // Native input painting is inert on MouseDown. Scope restores fonts/anchors/tints changed by its draw path.
        internal static bool Activate(MainTabWindow_Work window, Pawn pawn, WorkTypeDef type, int mouseButton)
        {
            Event ev = Event.current;
            if (!Ready || !Available(window, pawn) || type == null || !WorkTypes(window).Contains(type) || ev == null || ev.type != EventType.MouseDown ||
                ev.button != mouseButton || mouseButton < 0 || mouseButton > 1 || (!Manual && mouseButton != 0)) return false;
            int before = pawn.workSettings.GetPriority(type);
            using (new Theme.GuiScope(true))
            {
                checkboxActivation = true;
                try
                {
                    Vector2 pointer = ev.mousePosition;
                    WidgetsWork.DrawWorkBoxFor(pointer.x - 12.5f, pointer.y - 12.5f, pawn, type, Incapable(pawn, type));
                }
                finally { checkboxActivation = false; }
            }
            if (ev.type != EventType.Used) ev.Use();
            bool changed = before != pawn.workSettings.GetPriority(type);
            if (changed) { Reset(); Invalidate(window); }
            return changed;
        }

        internal static bool NativeCheckbox(Rect rect, bool sound) => checkboxActivation || Widgets.ButtonInvisible(rect, sound);

        // Exact four native setter sites; outside this action the helper delegates unchanged.
        internal static void NativeSetPriority(Pawn_WorkSettings settings, WorkTypeDef type, int priority)
            => settings.SetPriority(type, settings == setterSettings && type == setterType ? setterPriority : priority);

        internal static bool Toggle(MainTabWindow_Work window, Pawn pawn, WorkTypeDef type)
        {
            using (var audit = WorkPanelAudit.Begin(window, pawn, "toggle", type))
            {
                EnsureSession();
                var ev = Event.current;
                if (!Ready || !Available(window, pawn) || type == null || !WorkTypes(window).Contains(type) ||
                    ev == null || ev.type != EventType.MouseDown || ev.button != 0)
                    return Finish(audit, false, 0, "Work assignment unavailable");
                int? stored = WorkPanelAudit.RawPriority(pawn.workSettings, type);
                if (!stored.HasValue)
                { ev.Use(); return Finish(audit, false, 0, "Native work priority unavailable"); }
                int before = stored.Value;
                int target = WorkPanelPolicy.TogglePriority(before, priorities.Recall(pawn.workSettings, type));
                if (target == before)
                { ev.Use(); return Finish(audit, false, 0, "Unsupported native priority value; no assignment changed"); }
                if (before > 0) priorities.Remember(pawn.workSettings, type, before);
                // Original native WorkIsActive checks, age rejection, warnings, sounds and knowledge remain in this body.
                using (new Theme.GuiScope(true))
                {
                    checkboxActivation = true; setterSettings = pawn.workSettings; setterType = type; setterPriority = target;
                    try
                    {
                        Vector2 pointer = ev.mousePosition;
                        WidgetsWork.DrawWorkBoxFor(pointer.x - 12.5f, pointer.y - 12.5f, pawn, type, Incapable(pawn, type));
                    }
                    finally { checkboxActivation = false; setterSettings = null; setterType = null; }
                }
                if (ev.type != EventType.Used) ev.Use();
                int? actual = WorkPanelAudit.RawPriority(pawn.workSettings, type);
                if (!actual.HasValue) return Finish(audit, false, 0, "Native work priority unavailable after assignment");
                int after = actual.Value;
                if (after == before) return Finish(audit, false, 0, "Work assignment unchanged");
                priorities.Remember(pawn.workSettings, type, after);
                Reset(); Invalidate(window);
                return Finish(audit, true, 1, after == 0 ? "Work disabled." :
                    Manual ? "Work enabled at priority " + after + "." : "Work enabled.");
            }
        }

        // Separate numbered button: editable only while priorities are On, using the preserved native number.
        // Caller supplies the original left MouseDown and consumes it after this explicit action.
        internal static bool CycleEnabledPriority(MainTabWindow_Work window, Pawn pawn, WorkTypeDef type)
        {
            using (var audit = WorkPanelAudit.Begin(window, pawn, "cycle-priority", type))
            {
                EnsureSession();
                if (!Manual) return Finish(audit, false, 0, "Turn priorities On to edit priority numbers");
                var ev = Event.current;
                if (!Ready || !Available(window, pawn) || type == null || !WorkTypes(window).Contains(type) ||
                    ev == null || ev.type != EventType.MouseDown || ev.button != 0)
                    return Finish(audit, false, 0, "Work priority unavailable");
                var cell = Cell(pawn, type);
                if (!WorkPanelPolicy.CanCycleEnabledPriority(cell.StoredPriority, cell.Editable, Manual))
                    return Finish(audit, false, 0, "Enable an available job before choosing its priority");
                int target = WorkPanelPolicy.NextPriority(cell.StoredPriority);
                bool changed = SetEnabledPriority(window, pawn, type, target);
                string notice = LastNotice;
                int count = LastChangeCount;
                return Finish(audit, changed, count, notice);
            }
        }

        internal static bool SetEnabledPriority(MainTabWindow_Work window, Pawn pawn, WorkTypeDef type, int priority)
        {
            using (var audit = WorkPanelAudit.Begin(window, pawn, "set-priority", type))
            {
                EnsureSession();
                if (!Ready || !Available(window, pawn) || type == null || !WorkTypes(window).Contains(type) || pawn.WorkTypeIsDisabled(type))
                    return Finish(audit, false, 0, "Work priority unavailable");
                int? stored = WorkPanelAudit.RawPriority(pawn.workSettings, type);
                if (!stored.HasValue) return Finish(audit, false, 0, "Native work priority unavailable");
                int before = stored.Value;
                if (before < 0 || before > 4 || priority < 0 || priority > 4)
                    return Finish(audit, false, 0, "Priority must use the native Off or1–4 values");
                if (!WorkPanelPolicy.CanSetEnabledPriority(before, priority, Manual))
                    return Finish(audit, false, 0, before == 0 ? "Enable this job before choosing its priority" : "Enable manual priorities to choose a numbered priority");
                if (before == priority) return Finish(audit, false, 0, "Work priority unchanged");
                priorities.Remember(pawn.workSettings, type, before);
                Reset();
                pawn.workSettings.SetPriority(type, priority);
                int? actual = WorkPanelAudit.RawPriority(pawn.workSettings, type);
                if (!actual.HasValue) return Finish(audit, false, 0, "Native work priority unavailable after assignment");
                int after = actual.Value;
                if (after == before) return Finish(audit, false, 0, "Work priority unchanged");
                priorities.Remember(pawn.workSettings, type, after);
                if (Manual) SoundDefOf.DragSlider.PlayOneShotOnCamera();
                else SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
                Invalidate(window);
                return Finish(audit, true, 1, after == 0 ? "Work disabled." : "Work priority set to " + after + ".");
            }
        }

        private static bool Finish(WorkPanelAudit.Operation audit, bool success, int count, string reason)
        {
            audit?.Complete(success, count, reason);
            return Result(success, count, reason);
        }

        private static PawnColumnWorker_CopyPasteWorkPriorities CopyWorker(MainTabWindow_Work window)
        {
            var columns = Table(window)?.Columns;
            if (columns != null)
                foreach (var column in columns)
                    if (column.Worker is PawnColumnWorker_CopyPasteWorkPriorities worker) return worker;
            return DefaultCopyWorker;
        }

        private static MethodInfo WorkerMethod(object worker, string name, MethodInfo fallback)
            => AccessTools.Method(worker.GetType(), name, name.StartsWith("get_", StringComparison.Ordinal)
                ? Type.EmptyTypes : new[] { typeof(Pawn) }) ?? fallback;

        internal static bool CanPaste => ClipboardMethod != null && (bool)ClipboardMethod.Invoke(DefaultCopyWorker, null);
        internal static bool CanPasteFor(MainTabWindow_Work window)
        {
            if (!Ready) return false;
            var worker = CopyWorker(window);
            return (bool)WorkerMethod(worker, "get_AnythingInClipboard", ClipboardMethod).Invoke(worker, null);
        }
        internal static bool Copy(MainTabWindow_Work window, Pawn pawn)
        {
            if (!Ready || !Available(window, pawn)) return false;
            var worker = CopyWorker(window);
            WorkerMethod(worker, "CopyFrom", CopyMethod).Invoke(worker, new object[] { pawn });
            return true;
        }

        internal static bool Paste(MainTabWindow_Work window, Pawn pawn)
        {
            if (!Ready || !Available(window, pawn)) return false;
            var worker = CopyWorker(window);
            if (!(bool)WorkerMethod(worker, "get_AnythingInClipboard", ClipboardMethod).Invoke(worker, null)) return false;
            Reset();
            // Single native callback. It owns all-def capability masks and native invalidation/notifications.
            WorkerMethod(worker, "PasteTo", PasteMethod).Invoke(worker, new object[] { pawn });
            Invalidate(window);
            return true;
        }

        private sealed class UndoState
        {
            internal Game Game;
            internal Map Map;
            internal Pawn Pawn;
            internal Pawn_WorkSettings Settings;
            internal WorkTypeDef[] Defs;
            internal WorkPanelPriorityDelta Values;
            internal int[] Before => Values.Before;
            internal int[] After => Values.After;
            internal bool[] Disabled, Incapable;
            internal bool Manual;
            internal string Action;
        }

        private static UndoState Snapshot(Pawn pawn, string action)
        {
            List<WorkTypeDef> defs = DefDatabase<WorkTypeDef>.AllDefsListForReading;
            var state = new UndoState { Game = Current.Game, Map = Find.CurrentMap, Pawn = pawn,
                Settings = pawn.workSettings, Defs = defs.ToArray(),
                Disabled = new bool[defs.Count], Incapable = new bool[defs.Count],
                Manual = Manual, Action = action };
            int[] before = new int[defs.Count];
            for (int i = 0; i < state.Defs.Length; i++)
            {
                var type = state.Defs[i];
                int? raw = WorkPanelAudit.RawPriority(pawn.workSettings, type);
                if (!raw.HasValue) return null;
                before[i] = raw.Value;
                state.Disabled[i] = pawn.WorkTypeIsDisabled(type);
                state.Incapable[i] = Incapable(pawn, type);
            }
            state.Values = new WorkPanelPriorityDelta(before);
            return state;
        }

        internal static bool AutoAssign(MainTabWindow_Work window, Pawn pawn, out string reason)
        {
            using (var audit = WorkPanelAudit.Begin(window, pawn, "auto-assign"))
            {
                EnsureSession();
                bool success = ChangeAssignments(window, pawn, false, out reason);
                audit?.Complete(success, LastChangeCount, reason);
                return success;
            }
        }

        internal static bool CanClear(MainTabWindow_Work window, Pawn pawn)
        {
            if (!Ready || !Available(window, pawn)) return false;
            foreach (var type in WorkTypes(window))
            {
                int? raw = WorkPanelAudit.RawPriority(pawn.workSettings, type);
                if (raw.HasValue && WorkPanelPolicy.ClearPriority(raw.Value, pawn.WorkTypeIsDisabled(type)) != raw.Value) return true;
            }
            return false;
        }

        internal static bool Clear(MainTabWindow_Work window, Pawn pawn, out string reason)
        {
            using (var audit = WorkPanelAudit.Begin(window, pawn, "clear"))
            {
                EnsureSession();
                bool success = ChangeAssignments(window, pawn, true, out reason);
                audit?.Complete(success, LastChangeCount, reason);
                return success;
            }
        }

        private static bool ChangeAssignments(MainTabWindow_Work window, Pawn pawn, bool clear, out string reason)
        {
            reason = "";
            if (!Ready || !Usable(pawn)) { reason = "Work settings unavailable"; return Result(false, 0, reason); }
            if (!Roster(window).Contains(pawn)) { reason = "Colonist is no longer in this work roster"; return Result(false, 0, reason); }
            UndoState next = Snapshot(pawn, clear ? "Clear" : "Auto assign");
            if (next == null) { reason = "Native work priorities unavailable; no changes made"; return Result(false, 0, reason); }
            int changes = 0;
            // Only jobs shown by this native table are part of this explicit operation.
            foreach (var type in WorkTypes(window))
            {
                int index = Array.IndexOf(next.Defs, type);
                if (index < 0) continue;
                var cell = Cell(pawn, type);
                int priority = clear ? WorkPanelPolicy.ClearPriority(next.Before[index], !cell.Editable) :
                    WorkPanelPolicy.AssignPriority(type.defName, next.Before[index], cell.Skill, (int)cell.Passion, !cell.Editable, cell.Incapable);
                next.After[index] = priority;
                if (priority != next.Before[index]) changes++;
            }
            if (changes == 0) { reason = clear ? "No available work to clear." : "Assignments already match skills and passions. No changes made."; return Result(false, 0, reason); }
            if (!Matches(next, next.Before)) { reason = "Work settings changed before the action; no changes made."; return Result(false, 0, reason); }
            Reset();
            ownMutation = true;
            try
            {
                next.Values.Apply(false, i => WorkPanelAudit.RawPriority(next.Settings, next.Defs[i]),
                    () => SameContext(next) && Available(window, pawn), (i, value) =>
                    {
                        priorities.Remember(next.Settings, next.Defs[i], next.Before[i]);
                        next.Settings.SetPriority(next.Defs[i], value);
                        priorities.Remember(next.Settings, next.Defs[i], next.After[i]);
                    });
            }
            finally { ownMutation = false; }
            // Other mods may alter additional priorities in their SetPriority callbacks. Never claim an undo for that.
            bool canUndo = Matches(next) && Available(window, pawn);
            if (canUndo) undo = next;
            int actualChanges = CountDifferent(next, next.Before);
            reason = next.Action + " changed " + actualChanges + " work " + (actualChanges == 1 ? "assignment." : "assignments.") +
                (canUndo ? " Undo available." : " Undo unavailable because work settings changed during the action.");
            Invalidate(window);
            return Result(actualChanges > 0, actualChanges, reason);
        }

        private static int CountDifferent(UndoState state, int[] expected)
            => state.Values.CountDifferent(i => WorkPanelAudit.RawPriority(state.Settings, state.Defs[i]), expected);

        private static bool SameContext(UndoState state) => state != null && Manual == state.Manual &&
            Current.Game == state.Game && Find.CurrentMap == state.Map && Usable(state.Pawn) && state.Pawn.workSettings == state.Settings;

        private static bool Matches(UndoState state, int[] expected = null)
        {
            if (!SameContext(state)) return false;
            var defs = DefDatabase<WorkTypeDef>.AllDefsListForReading;
            if (defs.Count != state.Defs.Length || !state.Values.Matches(i => WorkPanelAudit.RawPriority(state.Settings, state.Defs[i]), expected ?? state.After)) return false;
            for (int i = 0; i < state.Defs.Length; i++)
                if (defs[i] != state.Defs[i] ||
                    state.Pawn.WorkTypeIsDisabled(defs[i]) != state.Disabled[i] ||
                    Incapable(state.Pawn, defs[i]) != state.Incapable[i]) return false;
            return true;
        }

        internal static bool CanUndo(MainTabWindow_Work window, Pawn pawn, out string reason)
        {
            reason = "No assignment change to undo";
            if (undo == null) return false;
            if (!Ready) { Reset(); return false; }
            if (pawn != undo.Pawn || !Matches(undo) || !Roster(window).Contains(pawn))
            {
                Reset(); reason = "Undo expired after the pawn, work settings, or work context changed"; return false;
            }
            reason = "Undo " + undo.Action + " for " + pawn.LabelShortCap; return true;
        }

        internal static bool Undo(MainTabWindow_Work window, Pawn pawn, out string reason)
        {
            using (var audit = WorkPanelAudit.Begin(window, pawn, "undo"))
            {
                bool success = UndoCore(window, pawn, out reason);
                audit?.Complete(success, LastChangeCount, reason);
                return success;
            }
        }

        private static bool UndoCore(MainTabWindow_Work window, Pawn pawn, out string reason)
        {
            if (!CanUndo(window, pawn, out reason)) return Result(false, 0, reason);
            UndoState previous = undo;
            Reset();
            ownMutation = true;
            try
            {
                previous.Values.Apply(true, i => WorkPanelAudit.RawPriority(previous.Settings, previous.Defs[i]),
                    () => SameContext(previous) && Available(window, pawn), (i, value) =>
                    {
                        previous.Settings.SetPriority(previous.Defs[i], value);
                        priorities.Remember(previous.Settings, previous.Defs[i], previous.Before[i]);
                    });
            }
            finally { ownMutation = false; }
            int changes = CountDifferent(previous, previous.After);
            bool exact = Matches(previous, previous.Before);
            reason = exact ? "Undo restored " + changes + " work " + (changes == 1 ? "priority." : "priorities.") :
                "Undo changed " + changes + " work priorities; other work changes prevented exact restoration.";
            Invalidate(window);
            return Result(changes > 0, changes, reason);
        }

        internal static void ObservePriorityChange(Pawn_WorkSettings settings, WorkTypeDef type, int? before)
        {
            if (!ownMutation && undo != null && settings == undo.Settings && before.HasValue &&
                WorkPanelAudit.RawPriority(settings, type) != before.Value) Reset();
        }
        internal static int? PriorityBefore(Pawn_WorkSettings settings, WorkTypeDef type)
            => !ownMutation && undo != null && settings == undo.Settings ? WorkPanelAudit.RawPriority(settings, type) : null;
        internal static void ObserveModeChange() { if (!ownMutation) { Reset(); sessionManual = Manual; } }
    }

    [HarmonyPatch(typeof(WidgetsWork), nameof(WidgetsWork.DrawWorkBoxFor))]
    internal static class WorkPanelCheckboxActivationPatch
    {
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = new List<CodeInstruction>(instructions);
            var target = AccessTools.Method(typeof(Widgets), nameof(Widgets.ButtonInvisible), new[] { typeof(Rect), typeof(bool) });
            var setter = AccessTools.Method(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.SetPriority), new[] { typeof(WorkTypeDef), typeof(int) });
            int count = 0, setters = 0;
            foreach (var instruction in original)
            { if (instruction.Calls(target)) count++; if (instruction.Calls(setter)) setters++; }
            WorkPanelNative.CheckboxSeamReady = count == 1 && setters == 4;
            if (!WorkPanelNative.CheckboxSeamReady) return original;
            var result = new List<CodeInstruction>(original.Count);
            foreach (var instruction in original)
            {
                var copy = new CodeInstruction(instruction);
                if (copy.Calls(target)) copy.operand = AccessTools.Method(typeof(WorkPanelNative), nameof(WorkPanelNative.NativeCheckbox));
                if (copy.Calls(setter))
                {
                    copy.opcode = OpCodes.Call;
                    copy.operand = AccessTools.Method(typeof(WorkPanelNative), nameof(WorkPanelNative.NativeSetPriority));
                }
                result.Add(copy);
            }
            return result;
        }
    }

    [HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.SetPriority))]
    internal static class WorkPanelPriorityObservationPatch
    {
        internal static void Prefix(Pawn_WorkSettings __instance, WorkTypeDef w, out int? __state)
            => __state = WorkPanelNative.PriorityBefore(__instance, w);
        internal static void Postfix(Pawn_WorkSettings __instance, WorkTypeDef w, int? __state)
            => WorkPanelNative.ObservePriorityChange(__instance, w, __state);
    }

    [HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.Notify_UseWorkPrioritiesChanged))]
    internal static class WorkPanelModeObservationPatch
    {
        internal static void Postfix() => WorkPanelNative.ObserveModeChange();
    }
}
