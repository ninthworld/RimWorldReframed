using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ReframedUI
{
    internal static class PawnHealthPanel
    {
        internal static Action OpenOptions = null;
        internal static bool OperationsReady;
        internal static bool Ready => PawnHealthNative.Ready;
        private static Pawn subject;
        private static Vector2 summaryScroll, conditionsScroll, billsScroll;
        private static Rect operationsBounds;
        private static Pawn operationPawn;
        private static ITab_Pawn_Health operationTab;
        internal static void Reset()
        {
            subject = null; summaryScroll = conditionsScroll = billsScroll = Vector2.zero;
            operationPawn = null; operationTab = null;
            PawnHealthOperations.Reset();
        }
        internal static void Draw(Rect rect, Pawn pawn, ITab_Pawn_Health tab)
        {
            if (!Ready || !PawnHealthNative.Valid(pawn, tab)) return;
            if (subject != pawn) { Reset(); subject = pawn; }
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white; Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                bool allow = PawnHealthNative.CanOperate(pawn, tab);
                if (!allow) PawnHealthNative.OperationSelected = false;
                DrawTabs(rect, pawn, allow);
                Rect body = new Rect(rect.x, rect.y + PawnHealthLayout.Header, rect.width, Math.Max(1, rect.height - PawnHealthLayout.Header));
                if (PawnHealthNative.OperationSelected) DrawOperations(body, pawn, tab);
                else
                {
                    float leftWidth = PawnHealthLayout.SummaryWidth(rect.width);
                    DrawSummary(new Rect(body.x, body.y, leftWidth, body.height), pawn, tab);
                    DrawConditions(new Rect(body.x + leftWidth + PawnHealthLayout.Gap, body.y,
                        body.width - leftWidth - PawnHealthLayout.Gap, body.height), pawn, tab);
                }
            }
        }
        private static void DrawTabs(Rect rect, Pawn pawn, bool allow)
        {
            float cog = OpenOptions != null ? 30 : 0;
            float width = Math.Min(138, (rect.width - cog - 6) / 2);
            if (allow)
            {
                if (PawnInspectorWidgets.Button(new Rect(rect.x, rect.y, width, 24), "HealthOverview".Translate(), selected: !PawnHealthNative.OperationSelected))
                    PawnHealthNative.OperationSelected = false;
                string label = (pawn.RaceProps.IsMechanoid ? "MedicalOperationsMechanoidsShort" : "MedicalOperationsShort").Translate();
                if (pawn.BillStack.Count > 0) label += " (" + pawn.BillStack.Count + ")";
                if (PawnInspectorWidgets.Button(new Rect(rect.x + width + 4, rect.y, width, 24), label, selected: PawnHealthNative.OperationSelected))
                    PawnHealthNative.OperationSelected = true;
            }
            else PawnInspectorView.Heading(new Rect(rect.x, rect.y, rect.width - cog, 24), "HealthOverview".Translate().ToString().ToUpperInvariant());
            if (OpenOptions != null && PawnInspectorWidgets.IconButton(new Rect(rect.xMax - 24, rect.y, 24, 24), "settings", "Health options")) OpenOptions();
            PawnInspectorWidgets.Divider(new Rect(rect.x, rect.y + 27, rect.width, 1));
        }
        private static void DrawSummary(Rect viewport, Pawn pawn, ITab_Pawn_Health tab)
        {
            bool food = PawnHealthNative.FoodVisible(pawn), care = PawnHealthNative.CareVisible(pawn), self = PawnHealthNative.SelfTendVisible(pawn);
            List<PawnCapacityDef> capacities = PawnHealthNative.Capacities(pawn).ToList();
            float controls = (food ? 25 : 0) + (care ? 25 : 0) + (self ? 25 : 0);
            float height = controls + (controls > 0 ? 10 : 0) + 19 + (pawn.RaceProps.IsFlesh ? 25 : 0) + capacities.Count * PawnHealthLayout.Row;
            float width = PawnInspectorWidgets.BeginScroll(viewport, ref summaryScroll, height);
            try
            {
                float y = 0;
                if (food)
                {
                    Rect row = new Rect(0, y, width, 23);
                    PawnInspectorView.Label(new Rect(0, y, width * .39f, 23), "AllowFood".Translate(), Theme.Ink);
                    Rect button = new Rect(width * .4f, y, width * .6f, 23);
                    bool full = PawnHealthLayout.FullyVisible(y, 23, summaryScroll.y, viewport.height);
                    if (PawnInspectorWidgets.Button(button, pawn.foodRestriction.CurrentFoodPolicy.label, enabled: full)) OpenFoodPolicy(pawn, tab);
                    TooltipHandler.TipRegion(row, "FoodRestrictionDescription".Translate()); y += 25;
                }
                if (care)
                {
                    Rect row = new Rect(0, y, width, 23), button = new Rect(width * .4f, y, width * .6f, 23);
                    PawnInspectorView.Label(new Rect(0, y, width * .39f, 23), "AllowMedicine".Translate(), Theme.Ink);
                    using (new Theme.GuiScope(true))
                    {
                        GUI.enabled &= PawnHealthLayout.FullyVisible(y, 23, summaryScroll.y, viewport.height);
                        // Keep the native dropdown and drag painting behavior, not only a copied preference assignment.
                        if (GUI.enabled || Event.current.type == EventType.Repaint) MedicalCareUtility.MedicalCareSelectButton(button, pawn);
                        // Repaint its icon with our compact face after native input processing.
                        // The entire category button keeps the native dropdown and drag target.
                        Theme.ButtonFace(button, GUI.enabled && Mouse.IsOver(button) ? Theme.Hover : Theme.Surface, !GUI.enabled);
                        GUI.color = Color.white;
                        GUI.DrawTexture(new Rect(button.x + 4, button.y + 3, 17, 17), PawnHealthNative.MedicalIcon(pawn.playerSettings.medCare));
                    }
                    PawnInspectorView.SmallLabel(new Rect(button.x + 26, button.y, button.width - 29, 23), pawn.playerSettings.medCare.GetLabel(), Theme.Ink);
                    TooltipHandler.TipRegion(row, "MedicineQualityDescription".Translate()); y += 25;
                }
                if (self)
                {
                    Rect row = new Rect(0, y, width, 23);
                    PawnInspectorView.Label(new Rect(0, y, width - 45, 23), "AllowSelfTend".Translate(), Theme.Ink);
                    bool enabled = pawn.playerSettings.selfTend;
                    if (PawnInspectorWidgets.Button(new Rect(width - 43, y, 43, 22), (enabled ? "On" : "Off").Translate(), enabled:
                        PawnHealthLayout.FullyVisible(y, 23, summaryScroll.y, viewport.height)))
                    {
                        (pawn.playerSettings.selfTend ? SoundDefOf.Checkbox_TurnedOff : SoundDefOf.Checkbox_TurnedOn).PlayOneShotOnCamera();
                        PawnHealthNative.ToggleSelfTend(pawn, tab);
                    }
                    TooltipHandler.TipRegion(row, "AllowSelfTendTip".Translate(Faction.OfPlayer.def.pawnsPlural, .7f.ToStringPercent()).CapitalizeFirst()); y += 25;
                }
                if (controls > 0) { PawnInspectorWidgets.Divider(new Rect(0, y + 3, width, 1)); y += 10; }
                PawnInspectorView.Heading(new Rect(0, y, width, 17), "CAPACITIES"); y += 19;
                if (pawn.RaceProps.IsFlesh)
                {
                    Pair<string, Color> pain = HealthCardUtility.GetPainLabel(pawn);
                    StatusRow(new Rect(0, y, width, 23), "PainLevel".Translate(), pain.First, pain.Second,
                        pawn.health.hediffSet.PainTotal, () => HealthCardUtility.GetPainTip(pawn), 17934 + pawn.thingIDNumber); y += 25;
                }
                foreach (PawnCapacityDef capacity in capacities)
                {
                    Pair<string, Color> value = HealthCardUtility.GetEfficiencyLabel(pawn, capacity);
                    StatusRow(new Rect(0, y, width, PawnHealthLayout.Row), capacity.GetLabelFor(pawn).CapitalizeFirst(), value.First, value.Second,
                        pawn.health.capacities.GetLevel(capacity), () => HealthCardUtility.GetPawnCapacityTip(pawn, capacity), pawn.thingIDNumber ^ capacity.index);
                    y += PawnHealthLayout.Row;
                }
            }
            finally { PawnInspectorWidgets.EndScroll(viewport, ref summaryScroll, height, 17931); }
        }
        private static void StatusRow(Rect row, string label, string value, Color color, float amount, Func<string> tip, int tipId)
        {
            float labelWidth = row.width * .5f;
            PawnInspectorView.Label(new Rect(row.x, row.y, labelWidth, row.height), label, Theme.Ink);
            PawnInspectorView.SmallLabel(new Rect(row.x + labelWidth, row.y, row.width - labelWidth, row.height), value, color, true);
            PawnInspectorView.Bar(new Rect(row.x, row.yMax - 2, row.width, 2), amount, color);
            TooltipHandler.TipRegion(row, new TipSignal(tip, tipId));
        }
        private static void OpenFoodPolicy(Pawn pawn, ITab_Pawn_Health tab)
        {
            if (!PawnHealthNative.Valid(pawn, tab) || !PawnHealthNative.FoodVisible(pawn)) return;
            var options = Current.Game.foodRestrictionDatabase.AllFoodRestrictions.Select(policy => new FloatMenuOption(policy.label, () =>
            { if (PawnHealthNative.Valid(pawn, tab) && PawnHealthNative.FoodVisible(pawn)) pawn.foodRestriction.CurrentFoodPolicy = policy; })).ToList();
            options.Add(new FloatMenuOption("ManageFoodPolicies".Translate() + "…", () =>
            { if (PawnHealthNative.Valid(pawn, tab)) Find.WindowStack.Add(new Dialog_ManageFoodPolicies(pawn.foodRestriction.CurrentFoodPolicy)); }));
            Find.WindowStack.Add(new FloatMenu(options));
        }
        private static void DrawConditions(Rect viewport, Pawn pawn, ITab_Pawn_Health tab)
        {
            List<IGrouping<BodyPartRecord, Hediff>> groups = PawnHealthNative.ConditionGroups(pawn);
            int entries = groups.Sum(group => group.Select(hediff => hediff.UIGroupKey).Distinct().Count());
            float bleed = pawn.health.hediffSet.BleedRateTotal;
            float footer = bleed > .01f ? 25 : 0;
            Rect list = new Rect(viewport.x, viewport.y, viewport.width, Math.Max(1, viewport.height - footer));
            float height = PawnHealthLayout.ConditionsHeight(groups.Count, entries);
            float width = PawnInspectorWidgets.BeginScroll(list, ref conditionsScroll, height);
            try
            {
                float y = 0;
                if (groups.Count == 0) PawnInspectorView.SmallLabel(new Rect(0, 3, width, 23), "NoHealthConditions".Translate(), Theme.Muted);
                foreach (IGrouping<BodyPartRecord, Hediff> part in groups)
                {
                    string label = part.Key?.LabelCap ?? "WholeBody".Translate().ToString();
                    Color partColor = part.Key == null ? Theme.Muted : HealthUtility.GetPartConditionLabel(pawn, part.Key).Second;
                    PawnInspectorView.Label(new Rect(0, y, width - 45, 19), label, partColor, true);
                    if (part.Key != null)
                    {
                        float health = pawn.health.hediffSet.GetPartHealth(part.Key), max = part.Key.def.GetMaxHealth(pawn);
                        PawnInspectorView.SmallLabel(new Rect(width - 45, y, 45, 19), health.ToString("0.#") + "/" + max.ToString("0.#"), Theme.Muted, true);
                        TooltipHandler.TipRegion(new Rect(0, y, width, 19), PawnHealthNative.PartTooltip(pawn, part.Key));
                    }
                    y += PawnHealthLayout.GroupHeader;
                    foreach (IGrouping<int, Hediff> entry in part.GroupBy(hediff => hediff.UIGroupKey))
                    {
                        DrawCondition(new Rect(0, y, width, PawnHealthLayout.Row), pawn, tab, entry.ToList(),
                            PawnHealthLayout.FullyVisible(y, PawnHealthLayout.Row, conditionsScroll.y, list.height));
                        y += PawnHealthLayout.Row;
                    }
                    PawnInspectorWidgets.Divider(new Rect(0, y + 2, width, 1)); y += 5;
                }
            }
            finally { PawnInspectorWidgets.EndScroll(list, ref conditionsScroll, height, 17932); }
            if (footer > 0)
            {
                Rect row = new Rect(viewport.x, viewport.yMax - 22, viewport.width, 22);
                string value = "BleedingRate".Translate() + ": " + bleed.ToStringPercent() + "/" + "LetterDay".Translate();
                int ticks = HealthUtility.TicksUntilDeathDueToBloodLoss(pawn);
                bool deathless = ModsConfig.BiotechActive && pawn.genes?.HasActiveGene(GeneDefOf.Deathless) == true;
                string prognosis = deathless ? "Deathless".Translate().ToString() : ticks < 60000 ? "TimeToDeath".Translate(ticks.ToStringTicksToPeriod()).ToString() : "WontBleedOutSoon".Translate().ToString();
                PawnInspectorView.SmallLabel(row, value, Theme.Danger);
                TooltipHandler.TipRegion(row, value + "\n" + prognosis);
            }
        }
        private static void DrawCondition(Rect row, Pawn pawn, ITab_Pawn_Health tab, List<Hediff> group, bool fullyVisible)
        {
            Hediff first = group[0];
            if (Mouse.IsOver(row)) Theme.RoundRect(row, Theme.Hover, 2);
            bool bleeding = group.Any(hediff => hediff.Bleeding);
            if (bleeding) Theme.RoundRect(new Rect(row.x + 1, row.center.y - 2, 3, 4), Theme.Danger, 1);
            Rect info = new Rect(row.xMax - 19, row.y + 1, 18, 18);
            TextureAndColor state = group[group.Count - 1].StateIcon;
            float reserve = state.HasValue ? 50 : 31;
            PawnInspectorView.Label(new Rect(row.x + 8, row.y, Math.Max(1, row.width - reserve), row.height), first.LabelCap + (group.Count > 1 ? " ×" + group.Count : ""), first.LabelColor);
            if (state.HasValue)
            {
                using (new Theme.GuiScope(true))
                { GUI.color = state.Color; GUI.DrawTexture(new Rect(row.xMax - 37, row.center.y - 7, 14, 14), state.Texture); }
            }
            if (fullyVisible)
            {
                if (DebugViewSettings.drawWoundAnchorsOnHover && Mouse.IsOver(row) &&
                    pawn.Drawer.renderer.WoundOverlays.debugDrawPart != first.Part)
                {
                    pawn.Drawer.renderer.WoundOverlays.debugDrawPart = first.Part;
                    pawn.Drawer.renderer.WoundOverlays.ClearCache();
                    PortraitsCache.SetDirty(pawn);
                    GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
                }
                TooltipHandler.TipRegion(row, new TipSignal(() => PawnHealthNative.ConditionTooltip(pawn, group), 82731 + first.loadID));
                if (PawnInspectorWidgets.IconButton(info, "info", "ThingInfo".Translate()))
                {
                    if (group.Count == 1) Find.WindowStack.Add(new Dialog_InfoCard(first));
                    else Find.WindowStack.Add(new FloatMenu(group.Select(hediff => new FloatMenuOption(hediff.LabelCap,
                        () => Find.WindowStack.Add(new Dialog_InfoCard(hediff)))).ToList()));
                }
                Rect link = new Rect(row.x, row.y, row.width - 21, row.height);
                if (PawnHealthNative.CanClick(pawn, group) && Widgets.ButtonInvisible(link)) PawnHealthNative.EntryClicked(pawn, tab, group);
            }
        }
        private static void DrawOperations(Rect rect, Pawn pawn, ITab_Pawn_Health tab)
        {
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.MedicalOperations, KnowledgeAmount.Total);
            Pawn previous = operationPawn; ITab_Pawn_Health previousTab = operationTab; Rect previousBounds = operationsBounds;
            operationPawn = pawn; operationTab = tab; operationsBounds = rect;
            try { PawnHealthNative.Operations.Invoke(null, new object[] { rect, pawn, Find.Selector.SingleSelectedThing, 0f }); }
            finally { operationPawn = previous; operationTab = previousTab; operationsBounds = previousBounds; }
        }
        internal static Bill DrawBillListing(BillStack stack, Rect rect, Func<List<FloatMenuOption>> options, ref Vector2 nativeScroll, ref float nativeHeight)
        {
            if (operationPawn == null || operationTab == null) return stack.DoListing(rect, options, ref nativeScroll, ref nativeHeight);
            return PawnHealthOperations.Draw(operationsBounds, operationPawn, operationTab, stack, options, ref billsScroll);
        }
    }
}
