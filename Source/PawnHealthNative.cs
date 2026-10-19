using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ReframedUI
{
    internal static class PawnHealthNative
    {
        internal static readonly MethodInfo Operations = AccessTools.Method(typeof(HealthCardUtility), "DrawMedOperationsTab");
        private static readonly MethodInfo AllowOperations = AccessTools.Method(typeof(ITab_Pawn_Health), "ShouldAllowOperations");
        private static readonly MethodInfo Subject = AccessTools.PropertyGetter(typeof(ITab_Pawn_Health), "PawnForHealth");
        private static readonly MethodInfo Groups = AccessTools.Method(typeof(HealthCardUtility), "VisibleHediffGroupsInOrder");
        private static readonly MethodInfo PartTip = AccessTools.Method(typeof(HealthCardUtility), "GetTooltip");
        private static readonly MethodInfo Clickable = AccessTools.Method(typeof(HealthCardUtility), "CanEntryBeClicked");
        private static readonly MethodInfo Click = AccessTools.Method(typeof(HealthCardUtility), "EntryClicked");
        private static readonly FieldInfo OnOperations = AccessTools.Field(typeof(HealthCardUtility), "onOperationTab");
        private static readonly FieldInfo DebugTips = AccessTools.Field(typeof(HealthCardUtility), "showHediffsDebugInfo");
        private static readonly MethodInfo CareIcon = AccessTools.Method(typeof(MedicalCareUtility), "MedicalCareIcon");
        internal static bool Ready => Operations != null && AllowOperations != null && Subject != null && Groups != null && PartTip != null &&
            Clickable != null && Click != null && OnOperations != null && DebugTips != null && CareIcon != null && PawnHealthPanel.OperationsReady;
        internal static UnityEngine.Texture2D MedicalIcon(MedicalCareCategory care) => (UnityEngine.Texture2D)CareIcon.Invoke(null, new object[] { care });
        internal static bool OperationSelected { get => (bool)OnOperations.GetValue(null); set => OnOperations.SetValue(null, value); }
        internal static bool Valid(Pawn pawn, ITab_Pawn_Health tab) => pawn != null && tab != null && !pawn.Destroyed &&
            (Find.Selector.SingleSelectedThing == pawn || Find.Selector.SingleSelectedThing is Corpse corpse && corpse.InnerPawn == pawn) &&
            ReferenceEquals(Subject.Invoke(tab, null), pawn);
        internal static bool CanOperate(Pawn pawn, ITab_Pawn_Health tab) => Valid(pawn, tab) && !pawn.Dead && (bool)AllowOperations.Invoke(tab, null);
        internal static List<IGrouping<BodyPartRecord, Hediff>> ConditionGroups(Pawn pawn) =>
            ((IEnumerable<IGrouping<BodyPartRecord, Hediff>>)Groups.Invoke(null, new object[] { pawn,
                HealthCardUtility.ShowBloodLoss(Find.Selector.SingleSelectedThing) })).ToList();
        internal static string PartTooltip(Pawn pawn, BodyPartRecord part) => part == null ? "" : (string)PartTip.Invoke(null, new object[] { pawn, part });
        internal static string ConditionTooltip(Pawn pawn, IEnumerable<Hediff> group) =>
            string.Join("\n\n", group.Select(hediff => hediff.GetTooltip(pawn, Prefs.DevMode && (bool)DebugTips.GetValue(null))));
        internal static bool CanClick(Pawn pawn, IEnumerable<Hediff> group) => (bool)Clickable.Invoke(null, new object[] { group, pawn });
        internal static void EntryClicked(Pawn pawn, ITab_Pawn_Health tab, IEnumerable<Hediff> group)
        {
            if (Valid(pawn, tab) && CanClick(pawn, group)) Click.Invoke(null, new object[] { group, pawn });
        }
        internal static bool FoodVisible(Pawn pawn) => pawn.foodRestriction?.Configurable == true && !pawn.DevelopmentalStage.Baby() && pawn.needs?.food != null &&
            (!pawn.IsMutant || !pawn.mutant.Def.disablePolicies);
        internal static bool CareVisible(Pawn pawn)
        {
            bool ours = pawn.Faction == Faction.OfPlayer || pawn.HostFaction == Faction.OfPlayer;
            bool guestBed = WildManUtility.NonHumanlikeOrWildMan(pawn) && pawn.InBed() && pawn.CurrentBed()?.Faction == Faction.OfPlayer;
            return PawnHealthLayout.CareVisible(pawn.RaceProps.IsFlesh, ours, guestBed,
                !pawn.IsMutant || pawn.mutant.Def.entitledToMedicalCare, pawn.playerSettings != null, pawn.Dead, Current.ProgramState == ProgramState.Playing);
        }
        internal static bool SelfTendVisible(Pawn pawn) => Current.ProgramState == ProgramState.Playing && pawn.IsColonist && !pawn.Dead &&
            !pawn.DevelopmentalStage.Baby() && pawn.playerSettings != null;
        internal static void ToggleSelfTend(Pawn pawn, ITab_Pawn_Health tab)
        {
            if (!Valid(pawn, tab) || !SelfTendVisible(pawn)) return;
            bool next = !pawn.playerSettings.selfTend;
            int result = PawnHealthLayout.SelfTendOutcome(next, pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor), pawn.workSettings?.GetPriority(WorkTypeDefOf.Doctor) ?? 0);
            pawn.playerSettings.selfTend = next && result != 1;
            if (result != 0) Messages.Message((result == 1 ? "MessageCannotSelfTendEver" : "MessageSelfTendUnsatisfied").Translate(pawn.LabelShort, pawn),
                result == 1 ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.CautionInput, false);
        }
        internal static IEnumerable<PawnCapacityDef> Capacities(Pawn pawn) => pawn.Dead ? Enumerable.Empty<PawnCapacityDef>() :
            DefDatabase<PawnCapacityDef>.AllDefs.Where(def => def.CanShowOnPawn(pawn) && PawnCapacityUtility.BodyCanEverDoCapacity(pawn.RaceProps.body, def)).OrderBy(def => def.listOrder);
    }
}
