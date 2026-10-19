using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Sound;

namespace ReframedUI
{
    // Installed DrawThingRow is the behavior contract. Reuse its actual owners
    // and callbacks; do not issue new jobs, change apparel or move inventory here.
    internal static class PawnGearNative
    {
        private static readonly MethodInfo SelectedPawn = AccessTools.PropertyGetter(typeof(ITab_Pawn_Gear), "SelPawnForGear");
        private static readonly MethodInfo Control = AccessTools.PropertyGetter(typeof(ITab_Pawn_Gear), "CanControl");
        private static readonly MethodInfo ColonistControl = AccessTools.PropertyGetter(typeof(ITab_Pawn_Gear), "CanControlColonist");
        private static readonly MethodInfo Drop = AccessTools.Method(typeof(ITab_Pawn_Gear), "InterfaceDrop");
        private static readonly MethodInfo Ingest = AccessTools.Method(typeof(ITab_Pawn_Gear), "InterfaceIngest");
        internal static bool Show(ITab_Pawn_Gear tab, string section, Pawn pawn) =>
            (bool)AccessTools.Method(typeof(ITab_Pawn_Gear), "ShouldShow" + section).Invoke(tab, new object[] { pawn });
        internal static bool Valid(Pawn pawn, Thing thing, ITab_Pawn_Gear tab) => pawn != null && thing != null && !thing.Destroyed && tab != null &&
            (Find.Selector.SingleSelectedThing == pawn || Find.Selector.SingleSelectedThing is Corpse corpse && corpse.InnerPawn == pawn) &&
            ReferenceEquals(SelectedPawn.Invoke(tab, null), pawn) && (pawn.equipment?.AllEquipmentListForReading.Any(item => item == thing) == true ||
            pawn.apparel?.WornApparel.Any(item => item == thing) == true || pawn.inventory?.innerContainer.Contains(thing) == true);

        private static bool DropAvailable(Pawn pawn, Thing thing, ITab_Pawn_Gear tab, bool inventory, out int blocked)
        {
            blocked = 0;
            if (!Valid(pawn, thing, tab)) return false;
            if (inventory != (pawn.inventory?.innerContainer.Contains(thing) == true)) return false;
            bool visible = PawnGearLayout.DropVisible((bool)Control.Invoke(tab, null), inventory,
                (bool)ColonistControl.Invoke(tab, null), pawn.Spawned, pawn.Spawned && pawn.Map.IsPlayerHome);
            if (!visible) return false;
            bool quest = QuestUtility.IsQuestLodger(pawn) && (inventory || !EquipmentUtility.QuestLodgerCanUnequip(thing, pawn));
            bool locked = thing is Apparel apparel && pawn.apparel?.IsLocked(apparel) == true;
            blocked = PawnGearLayout.DropBlock(locked, quest, !inventory && pawn.kindDef.destroyGearOnDrop);
            return true;
        }
        private static bool IngestAvailable(Pawn pawn, Thing thing, ITab_Pawn_Gear tab) => Valid(pawn, thing, tab) &&
            (bool)ColonistControl.Invoke(tab, null) && FoodUtility.WillIngestFromInventoryNow(pawn, thing);

        internal static void OpenActions(Pawn pawn, Thing thing, ITab_Pawn_Gear tab, bool inventory)
        {
            if (!Valid(pawn, thing, tab)) return;
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("ThingInfo".Translate(), () =>
                { if (Valid(pawn, thing, tab)) Find.WindowStack.Add(new Dialog_InfoCard(thing)); })
            };
            if (DropAvailable(pawn, thing, tab, inventory, out int blocked))
            {
                string reason = blocked == 1 ? "DropThingLocked".Translate().ToString() : blocked == 2 ? "DropThingLodger".Translate().ToString() :
                    blocked == 3 ? "ApparelLockedLower".Translate().ToString() : null;
                options.Add(new FloatMenuOption(blocked == 0 ? "DropThing".Translate().ToString() : "DropThing".Translate() + " (" + reason + ")",
                    blocked != 0 ? (Action)null : () =>
                    {
                        if (!DropAvailable(pawn, thing, tab, inventory, out int currentBlock) || currentBlock != 0) return;
                        Action callback = () =>
                        {
                            if (!DropAvailable(pawn, thing, tab, inventory, out int confirmedBlock) || confirmedBlock != 0) return;
                            SoundDefOf.Tick_High.PlayOneShotOnCamera();
                            Drop.Invoke(tab, new object[] { thing });
                        };
                        if (!ModsConfig.BiotechActive || !MechanitorUtility.TryConfirmBandwidthLossFromDroppingThing(pawn, thing, callback)) callback();
                    }));
            }
            if (IngestAvailable(pawn, thing, tab))
                options.Add(new FloatMenuOption("ConsumeThing".Translate(thing.LabelNoCount, thing), () =>
                {
                    if (!IngestAvailable(pawn, thing, tab)) return;
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    Ingest.Invoke(tab, new object[] { thing });
                }));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        internal static string Condition(Thing thing, Pawn pawn)
        {
            var parts = new List<string>();
            if (thing.TryGetQuality(out QualityCategory quality)) parts.Add(quality.GetLabel());
            if (thing.def.useHitPoints) parts.Add(((float)thing.HitPoints / Math.Max(1, thing.MaxHitPoints)).ToStringPercent("F0"));
            if (thing.stackCount > 1) parts.Add("×" + thing.stackCount);
            if (thing is Apparel apparel && pawn.outfits?.forcedHandler.IsForced(apparel) == true) parts.Add("ApparelForcedLower".Translate());
            if (thing is Apparel worn && pawn.apparel?.IsLocked(worn) == true) parts.Add("ApparelLockedLower".Translate());
            return string.Join(" · ", parts);
        }
    }
}
