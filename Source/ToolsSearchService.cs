using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ReframedUI
{
    // Search only revealed, spawned, selectable things on the current map. Keep
    // real Thing identities; never route a click through a stale display index.
    internal sealed class ToolsSearchService
    {
        internal readonly List<Thing> Results = new List<Thing>();
        internal string Query { get; private set; } = "";
        private Map map;
        private double nextRefresh;
        private bool dirty;
        internal void Reset()
        { Query = ""; Results.Clear(); map = null; nextRefresh = 0; dirty = false; }
        internal void SetQuery(string value, double now)
        {
            value = value ?? "";
            if (Query == value) return;
            Query = value;
            Results.Clear();
            dirty = true;
            nextRefresh = now + .12;
        }
        internal void Refresh(Map current, double now)
        {
            if (current != map) { map = current; dirty = true; nextRefresh = now; Results.Clear(); }
            if (now < nextRefresh) return;
            nextRefresh = now + .75;
            dirty = false;
            Results.Clear();
            string query = Query.Trim();
            if (map == null || query.Length == 0) return;
            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (!Available(thing, map)) continue;
                if (Matches(thing.LabelNoCount, query) || Matches(thing.def.label, query) ||
                    Matches(thing.def.defName, query) || Matches(thing.Stuff?.label, query)) Results.Add(thing);
            }
            Results.Sort((a, b) =>
            {
                int label = StringComparer.CurrentCultureIgnoreCase.Compare(a.LabelNoCount, b.LabelNoCount);
                return label != 0 ? label : a.thingIDNumber.CompareTo(b.thingIDNumber);
            });
        }
        internal bool Pending => dirty;
        internal static bool Available(Thing thing, Map current) => thing != null && !thing.Destroyed && thing.Spawned &&
            thing.Map == current && thing.def.selectable && !thing.Position.Fogged(current);
        private static bool Matches(string text, string query) => text != null && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        internal static string Detail(Thing thing)
        {
            string detail = thing.stackCount > 1 ? "×" + thing.stackCount : "";
            if (thing.TryGetQuality(out QualityCategory quality)) detail += (detail.Length > 0 ? " · " : "") + quality.GetLabel();
            if (thing.def.useHitPoints)
                detail += (detail.Length > 0 ? " · " : "") + (100f * thing.HitPoints / Math.Max(1, thing.MaxHitPoints)).ToString("0") + "%";
            return detail;
        }
    }
}
