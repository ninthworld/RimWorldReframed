using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ReframedUI
{
    internal static class ResourceHistoryCounts
    {
        private static readonly Func<ResourceCounter, Thing, bool> ShouldCount = ResolveShouldCount();
        private static List<ThingDef> weapons;
        private static int defCount = -1;

        private static IEnumerable<ThingDef> Weapons
        {
            get
            {
                int count = DefDatabase<ThingDef>.AllDefsListForReading.Count;
                if (weapons == null || count != defCount)
                {
                    weapons = DefDatabase<ThingDef>.AllDefs.Where(def => def.IsWeapon).ToList();
                    defCount = count;
                }
                return weapons;
            }
        }

        internal static HashSet<string> KnownNames(Map map)
        {
            var names = new HashSet<string>(map.resourceCounter.AllCountedAmounts.Keys
                .Where(def => def != null).Select(def => def.defName), StringComparer.Ordinal);
            if (ShouldCount != null) foreach (var def in Weapons) names.Add(def.defName);
            return names;
        }

        internal static Dictionary<ThingDef, int> Read(Map map)
        {
            var merge = new ResourceCountMerge<ThingDef>(map.resourceCounter.AllCountedAmounts,
                ShouldCount == null ? Enumerable.Empty<ThingDef>() : Weapons);
            if (ShouldCount == null) return merge.Counts;
            // Same direct stored-thing enumeration as ResourceCounter.UpdateResourceCounts.
            // No equipment, inventory or pawn/caravan scans; no recount/simulation mutation.
            foreach (var group in map.haulDestinationManager.AllGroupsListForReading)
                foreach (Thing held in group.HeldThings)
                {
                    Thing inner = held.GetInnerIfMinified();
                    if (inner != null && inner.def.IsWeapon && ShouldCount(map.resourceCounter, inner))
                        merge.AddStored(held.thingIDNumber, inner.def, inner.stackCount);
                }
            return merge.Counts;
        }

        private static Func<ResourceCounter, Thing, bool> ResolveShouldCount()
        {
            try
            {
                return (Func<ResourceCounter, Thing, bool>)Delegate.CreateDelegate(typeof(Func<ResourceCounter, Thing, bool>),
                    AccessTools.Method(typeof(ResourceCounter), "ShouldCount", new[] { typeof(Thing) }));
            }
            catch (Exception error)
            {
                Log.Warning("[Reframed UI] Stored-weapon observations unavailable; native resource counts retained. " + error.Message);
                return null;
            }
        }
    }
}
