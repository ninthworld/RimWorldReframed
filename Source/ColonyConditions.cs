using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace ReframedUI
{
    // Alert_Custom descendants are excluded from native alert discovery. These
    // adapters never enter native activeAlerts, send bells, or create conditions.
    internal static class ColonyConditions
    {
        private sealed class ConditionIssue : Alert_Custom
        {
            internal readonly GameCondition Condition;
            internal readonly GameConditionManager Manager;
            private readonly Map map;
            internal ConditionIssue(GameCondition condition, GameConditionManager manager)
            { Condition = condition; Manager = manager; map = Find.CurrentMap; }
            internal bool Visible => map == Find.CurrentMap && Manager.ActiveConditions.Contains(Condition) &&
                Condition.def.displayOnUI && (Manager.ownerMap == null ||
                Condition.CanApplyOnMap(Manager.ownerMap) && !Condition.HiddenByOtherCondition(Manager.ownerMap));
            public override string GetLabel() => Condition.LabelCap;
            public override TaggedString GetExplanation() => Condition.TooltipString;
            public override AlertReport GetReport() => Visible;
            public override string GetJumpToTargetsText => "";
            protected override void OnClick()
            {
                if (!Visible) return;
                if (Condition.conditionCauser != null && !Condition.hideSource && CameraJumper.CanJump(Condition.conditionCauser))
                    CameraJumper.TryJumpAndSelect(Condition.conditionCauser);
                else if (Condition.quest != null)
                {
                    Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Quests);
                    ((MainTabWindow_Quests)MainButtonDefOf.Quests.TabWindow).Select(Condition.quest);
                }
            }
        }
        private static readonly Dictionary<GameCondition, ConditionIssue> cache = new Dictionary<GameCondition, ConditionIssue>();
        private static readonly List<Alert> combined = new List<Alert>();
        internal static void Reset() { cache.Clear(); combined.Clear(); }
        internal static bool Contains(Alert alert) => alert is ConditionIssue issue && issue.Visible;
        internal static List<Alert> Append(List<Alert> alerts)
        {
            ColonyIssues.ExternalKind = Kind;
            combined.Clear(); combined.AddRange(alerts);
            for (GameConditionManager manager = Find.CurrentMap.gameConditionManager; manager != null; manager = manager.Parent)
                foreach (GameCondition condition in manager.ActiveConditions)
                {
                    if (!cache.TryGetValue(condition, out ConditionIssue entry))
                        cache[condition] = entry = new ConditionIssue(condition, manager);
                    entry.Recalculate();
                    if (entry.Active && !combined.Contains(entry)) combined.Add(entry);
                }
            foreach (GameCondition stale in cache.Keys.Where(key => !combined.Contains(cache[key])).ToArray()) cache.Remove(stale);
            return combined;
        }
        private static ColonyIssueKind? Kind(Alert alert)
        {
            if (!(alert is ConditionIssue issue)) return null;
            string name = issue.Condition.def.defName;
            string icon = name == "HeatWave" ? "heat" : name == "ColdSnap" || name == "VolcanicWinter" ? "cold" :
                name == "SolarFlare" || name == "EMIField" ? "power" : name == "ToxicFallout" ? "danger" : "warning";
            return new ColonyIssueKind(ColonyIssueGroup.Environment, icon);
        }
    }
    [HarmonyPatch(typeof(GameConditionManager), nameof(GameConditionManager.DoConditionsUI))]
    internal static class ColonyConditionsPatch
    {
        private static bool Prefix(GameConditionManager __instance, Rect rect)
        {
            // Native code reserves this exact condition height before calling
            // LettersOnGUI. A too-short/unsupported stack keeps the native rows.
            return __instance != Find.CurrentMap?.gameConditionManager || !RightHud.Prepare(rect.y - 10);
        }
    }
}
