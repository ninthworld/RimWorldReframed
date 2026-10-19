using System;

namespace ReframedUI
{
    internal enum ColonyIssueGroup { Colonists, Resources, Environment }

    internal readonly struct ColonyIssueKind
    {
        internal readonly ColonyIssueGroup Group;
        internal readonly string Icon;
        internal ColonyIssueKind(ColonyIssueGroup group, string icon) { Group = group; Icon = icon; }
    }

    // Names identify actual alert types, never translated labels or fabricated examples.
    internal static class ColonyIssuePolicy
    {
        internal const float Padding = 8, Heading = 20, Row = 24, RowGap = 2, GroupGap = 4;
        // Installed AlertPriority enum: Medium0, High1, Critical2. Runtime Ready checks that seam.
        internal const int HighPriority = 1, CriticalPriority = 2;
        internal static ColonyIssueKind Kind(string name)
        {
            switch (name)
            {
                case "Alert_NeedColonistBeds": case "Alert_NeedSlaveBeds": case "Alert_NeedBabyCribs": case "Alert_NeedSlaveCribs":
                    return new ColonyIssueKind(ColonyIssueGroup.Colonists, "bed");
                case "Alert_NeedJoySources": case "Alert_Boredom": case "Alert_JoyBuildingNoChairs":
                case "Alert_BilliardsTableOnWall": case "Alert_ChessTableNoChairs": case "Alert_PokerTableNoChairs":
                    return new ColonyIssueKind(ColonyIssueGroup.Colonists, "joy");
                case "Alert_ColonistsIdle": case "Alert_NeedWarden": case "Alert_NeedMiner": case "Alert_RolesEmpty":
                case "Alert_SlavesUnsuppressed": case "Alert_SlavesUnattended": case "Alert_NoBabyFeeders":
                case "Alert_AbandonedBaby": case "Alert_Exhaustion": case "Alert_BrawlerHasRangedWeapon":
                case "Alert_ShieldUserHasRangedWeapon": case "Alert_HunterHasShieldAndRangedWeapon":
                case "Alert_TatteredApparel": case "Alert_UnhappyNudity": case "Alert_NeedWarmClothes":
                case "Alert_PsychicBondedSeparated": case "Alert_NeedMeditationSpot": case "Alert_UnusableMeditationFocus":
                case "Alert_RoyalNoThroneAssigned": case "Alert_UndignifiedThroneroom": case "Alert_UndignifiedBedroom":
                case "Alert_TitleRequiresBedroom": case "Alert_ThroneroomInvalidConfiguration":
                    return new ColonyIssueKind(ColonyIssueGroup.Colonists, "person");
                case "Alert_NeedDoctor": case "Alert_ColonistNeedsRescuing": case "Alert_ColonistNeedsTend":
                case "Alert_EntityNeedsTend": case "Alert_LifeThreateningHediff": case "Alert_AwaitingMedicalOperation":
                case "Alert_LowHemogen": case "Alert_LowDeathrest": case "Alert_DeathrestComplete":
                case "Alert_MechDamaged": case "Alert_MechMissingBodyPart": case "Alert_ColonistLeftUnburied":
                    return new ColonyIssueKind(ColonyIssueGroup.Colonists, "health");
                case "Alert_MinorBreakRisk": case "Alert_MajorOrExtremeBreakRisk": case "Alert_Thought":
                    return new ColonyIssueKind(ColonyIssueGroup.Colonists, "person");
                case "Alert_StarvationColonists": case "Alert_Biostarvation": case "Alert_MeatHunger":
                    return new ColonyIssueKind(ColonyIssueGroup.Colonists, "food");
                case "Alert_LowMedicine": return new ColonyIssueKind(ColonyIssueGroup.Resources, "medicine");
                case "Alert_LowFood": case "Alert_LowBabyFood": case "Alert_NoBabyFoodCaravan":
                case "Alert_NeedMealSource": case "Alert_PasteDispenserNeedsHopper": case "Alert_RoyalNoAcceptableFood":
                    return new ColonyIssueKind(ColonyIssueGroup.Resources, "food");
                case "Alert_NeedBatteries": case "Alert_GenebankUnpowered": case "Alert_ToxifierGeneratorStopped":
                case "Alert_MechChargerFull": case "Alert_NeedMechChargers": case "Alert_WarqueenHasLowResources":
                    return new ColonyIssueKind(ColonyIssueGroup.Resources, "power");
                case "Alert_NeedResearchProject": case "Alert_NeedResearchBench": case "Alert_NeedAnomalyProject":
                    return new ColonyIssueKind(ColonyIssueGroup.Resources, "research");
                case "Alert_Heatstroke": return new ColonyIssueKind(ColonyIssueGroup.Environment, "heat");
                case "Alert_Hypothermia": case "Alert_GhoulHypothermia": case "Alert_HypothermicAnimals":
                    return new ColonyIssueKind(ColonyIssueGroup.Environment, "cold");
                case "Alert_AnimalFilth": case "Alert_AnimalPenNeeded": case "Alert_AnimalPenNotEnclosed":
                case "Alert_PredatorInPen": case "Alert_AnimalRoaming": case "Alert_PennedAnimalHungry":
                case "Alert_HitchedAnimalHungryNoFood": case "Alert_StarvationAnimals":
                    return new ColonyIssueKind(ColonyIssueGroup.Environment, "animal");
                case "Alert_QuestExpiresSoon": case "Alert_BestowerWaiting":
                    return new ColonyIssueKind(ColonyIssueGroup.Environment, "quest");
                case "Alert_FireInHomeArea": case "Alert_MechThreat": case "Alert_TimedRaidsArriving":
                case "Alert_DangerousActivity": case "Alert_UndercaveUnstable": case "Alert_CultistPsychicRitual":
                case "Alert_LowOxygen": case "Alert_SlaveRebellionLikely": case "Alert_EmergingPitGate":
                    return new ColonyIssueKind(ColonyIssueGroup.Environment, "danger");
                default: return new ColonyIssueKind(ColonyIssueGroup.Environment, "warning");
            }
        }

        internal static bool SeparateAlert(int priority) => priority >= CriticalPriority;
        internal static float HeadingHeight(float presence) => Heading * Clamp01(presence);
        internal static float GroupSpacing(float precedingPresence, float presence)
            => GroupGap * Math.Min(Clamp01(precedingPresence), Clamp01(presence));
        internal static float RowReveal(float life, float expanded, int priority)
            => Clamp01(life) * Clamp01(expanded);
        internal static float Clamp01(float value) => float.IsNaN(value) ? 0 : Math.Max(0, Math.Min(1, value));
        internal static float Occupied(float natural, float maximum, float reveal)
            => float.IsNaN(natural) || float.IsInfinity(natural) || float.IsNaN(maximum) || float.IsInfinity(maximum) ? 0 :
                Math.Max(0, Math.Min(Math.Max(0, natural), Math.Max(0, maximum))) * Clamp01(reveal);
        internal static bool FullyVisible(float top, float bottom, float viewportTop, float viewportBottom)
            => top >= viewportTop - .01f && bottom <= viewportBottom + .01f;
    }
}
