using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    public sealed partial class ResourceHistorySample : IExposable
    {
        public void ExposeData()
        {
            Scribe_Values.Look(ref Tick, "tick", -1);
            Scribe_Collections.Look(ref Counts, "counts", LookMode.Value, LookMode.Value);
        }
    }

    // RimWorld discovers MapComponents and constructs one for each map. No global patch/tick hook.
    public sealed class ResourceHistoryComponent : MapComponent
    {
        private List<ResourceHistorySample> saved = new List<ResourceHistorySample>();
        private List<string> closedCategories = new List<string>();
        private readonly ResourceHistoryLedger ledger = new ResourceHistoryLedger();
        private bool initialized;
        private int lastAttemptTick = -1;
        private Dictionary<ThingDef, int> current;
        private float refreshAt;
        internal ResourceHistoryLedger Ledger => ledger;
        internal Dictionary<ThingDef, int> CurrentCounts
        {
            get
            {
                if (current == null || Time.realtimeSinceStartup >= refreshAt) RefreshCounts();
                return current;
            }
        }

        public ResourceHistoryComponent(Map map) : base(map) { }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && initialized) saved = ledger.Export();
            Scribe_Collections.Look(ref saved, "reframedResourceSnapshots", LookMode.Deep);
            Scribe_Collections.Look(ref closedCategories, "reframedResourceClosedCategories", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                closedCategories = ResourceHistoryLedger.SanitizeCategories(closedCategories);
                initialized = false;
            }
        }

        public override void FinalizeInit()
        {
            Initialize();
            SampleIfDue();
        }

        public override void MapComponentTick()
        {
            if (!initialized) Initialize();
            SampleIfDue();
        }

        private void Initialize()
        {
            int tick = Find.TickManager.TicksGame;
            var known = ResourceHistoryCounts.KnownNames(map);
            if (!ledger.Restore(saved, tick, known))
                Log.Warning("[Reframed UI] Invalid resource-history observations discarded for map " + map.uniqueID + ". Missing trends remain unavailable.");
            saved = null;
            closedCategories = ResourceHistoryLedger.SanitizeCategories(closedCategories);
            initialized = true;
        }

        private void SampleIfDue()
        {
            int tick = Find.TickManager.TicksGame;
            if (!ledger.Due(tick)) return;
            if (lastAttemptTick >= 0 && tick >= lastAttemptTick && (long)tick - lastAttemptTick < ResourceHistoryLedger.Interval) return;
            lastAttemptTick = tick;
            RefreshCounts();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var pair in current)
                if (pair.Key != null) counts[pair.Key.defName] = pair.Value;
            ledger.Observe(tick, counts);
        }

        private void RefreshCounts()
        {
            current = ResourceHistoryCounts.Read(map);
            refreshAt = Time.realtimeSinceStartup + .75f;
        }

        internal bool Closed(string category) => closedCategories.Contains(category);
        internal void Toggle(string category)
        {
            if (!closedCategories.Remove(category)) closedCategories.Add(category);
        }

    }
}
