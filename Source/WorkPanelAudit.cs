using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ReframedUI
{
    // Passive developer evidence only. No commands, patches, jobs, pause, saves or synthetic actions.
    internal static class WorkPanelAudit
    {
        private static readonly FieldInfo PrioritiesField = AccessTools.DeclaredField(typeof(Pawn_WorkSettings), "priorities");
        private static bool stopped, warningLogged;
        internal static bool Enabled => PawnBarDiagnostics.Enabled && !stopped;
        internal static string PathName => Path.Combine(GenFilePaths.SaveDataFolderPath, "ReframedUI", "WorkAudit", "actions.jsonl");
        internal static int? RawPriority(Pawn_WorkSettings settings, WorkTypeDef type)
            => settings != null && settings.Initialized && PrioritiesField?.GetValue(settings) is DefMap<WorkTypeDef, int> map
                ? (int?)map[type] : null;

        internal static Operation Begin(MainTabWindow_Work window, Pawn pawn, string action, WorkTypeDef type = null)
        {
            if (!Enabled) return null;
            try { return new Operation(window, pawn, action, type); }
            catch (Exception ex) { Stop(ex.Message); return null; }
        }

        private static object Snapshot(MainTabWindow_Work window)
        {
            var definitions = DefDatabase<WorkTypeDef>.AllDefsListForReading;
            var names = new List<string>(definitions.Count);
            foreach (var definition in definitions) names.Add(definition.defName);
            var pawns = new List<object>();
            using (new Theme.GuiScope(true))
                foreach (var pawn in WorkPanelNative.Roster(window))
                {
                    var raw = new List<object>();
                    var effective = new List<object>();
                    bool initialized = pawn.workSettings != null && pawn.workSettings.Initialized;
                    var map = initialized ? PrioritiesField?.GetValue(pawn.workSettings) as DefMap<WorkTypeDef, int> : null;
                    foreach (var definition in definitions)
                    {
                        raw.Add(map == null ? null : (object)map[definition]);
                        effective.Add(initialized ? (object)pawn.workSettings.GetPriority(definition) : null);
                    }
                    pawns.Add(PawnBarDiagnostics.D("pawn", pawn.ThingID, "name", pawn.LabelShort,
                        "initialized", initialized, "raw", raw, "effective", effective));
                }
            return PawnBarDiagnostics.D("manual", WorkPanelNative.Manual, "map", Find.CurrentMap?.uniqueID,
                "tick", Find.TickManager?.TicksGame, "work_defs", names, "raw_available", PrioritiesField != null,
                "roster", pawns);
        }

        internal sealed class Operation : IDisposable
        {
            private readonly MainTabWindow_Work window;
            private readonly string pawn, action, type;
            private readonly object before;
            private bool completed, success, disposed;
            private int count;
            private string reason = "Action did not complete";
            internal Operation(MainTabWindow_Work window, Pawn pawn, string action, WorkTypeDef type)
            {
                this.window = window; this.pawn = pawn?.ThingID; this.action = action; this.type = type?.defName;
                before = Snapshot(window);
            }
            internal void Complete(bool success, int count, string reason)
            { completed = true; this.success = success; this.count = count; this.reason = reason; }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                if (!Enabled) return;
                try
                {
                    var record = PawnBarDiagnostics.D("schema", "reframed-work-actions-v1", "utc", DateTime.UtcNow.ToString("o"),
                        "module", typeof(WorkPanelAudit).Module.ModuleVersionId.ToString(), "action", action, "pawn", pawn,
                        "work_type", type, "completed", completed, "success", success, "reported_change_count", count,
                        "notice", reason, "before", before, "after", Snapshot(window));
                    string line = PawnBarDiagnostics.Json(record) + Environment.NewLine;
                    int bytes = Encoding.UTF8.GetByteCount(line);
                    string path = PathName;
                    long current = File.Exists(path) ? new FileInfo(path).Length : 0;
                    if (!WorkPanelAuditPolicy.CanAppend(current, bytes))
                    { Stop("Audit size limit reached; existing evidence was retained. Archive actions.jsonl before restarting to record more actions."); return; }
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.AppendAllText(path, line, new UTF8Encoding(false));
                }
                catch (Exception ex) { Stop(ex.Message); }
            }
        }

        private static void Stop(string reason)
        {
            stopped = true;
            if (warningLogged) return;
            warningLogged = true;
            Log.Warning("[Reframed UI] Work audit stopped: " + reason);
        }
    }
}
