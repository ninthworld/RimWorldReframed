using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    [StaticConstructorOnStartup]
    internal static class PawnGearPanel
    {
        private static Vector2 itemScroll, statsScroll;
        private static Pawn selectedPawn;
        private static Texture2D temperatureRamp;
        private static float itemViewportHeight;
        private static readonly Color BarColor = Theme.Rgb(0x536F7B);
        internal static void Reset() { itemScroll = statsScroll = Vector2.zero; selectedPawn = null; }

        internal static void Draw(Rect rect, Pawn pawn, ITab_Pawn_Gear tab)
        {
            if (pawn == null || tab == null) return;
            if (pawn != selectedPawn) { Reset(); selectedPawn = pawn; }
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = false;
                Rect left = new Rect(rect.x, rect.y, rect.width * PawnGearLayout.LeftFraction, rect.height);
                Rect right = new Rect(left.xMax + PawnGearLayout.Gap, rect.y, rect.xMax - left.xMax - PawnGearLayout.Gap, rect.height);
                DrawStatistics(left, pawn, tab);
                DrawItems(right, pawn, tab);
            }
        }

        private static void DrawStatistics(Rect rect, Pawn pawn, ITab_Pawn_Gear tab)
        {
            float width = PawnInspectorWidgets.BeginScroll(rect, ref statsScroll, PawnGearLayout.StatsHeight);
            try
            {
                float y = 0;
                if (PawnGearNative.Show(tab, "OverallArmor", pawn))
                {
                    PawnInspectorView.Heading(new Rect(0, y, width, 15), "OVERALL ARMOR"); y += 19;
                    foreach (var pair in new[] { Tuple.Create(StatDefOf.ArmorRating_Sharp, "Sharp"), Tuple.Create(StatDefOf.ArmorRating_Blunt, "Blunt"), Tuple.Create(StatDefOf.ArmorRating_Heat, "Heat") })
                    {
                        float armor = Armor(pawn, pair.Item1);
                        PawnInspectorView.Meter(new Rect(0, y, width, 22), pair.Item2, armor / 2, BarColor,
                            armor.ToStringPercent() + " overall armor (native body coverage)", armor.ToStringPercent("F0")); y += 22;
                    }
                    y += 9;
                }
                if (!pawn.Dead)
                {
                    PawnInspectorView.Heading(new Rect(0, y, width, 15), "CORE STATS"); y += 21;
                    if (PawnGearNative.Show(tab, "Inventory", pawn))
                    {
                        float mass = MassUtility.GearAndInventoryMass(pawn), capacity = MassUtility.Capacity(pawn);
                        string formatted = mass.ToString("0.##") + " / " + capacity.ToStringMass();
                        CompactStat(new Rect(0, y, width, 22), "Mass carried", PawnInspectorLayout.Ratio(mass, capacity), formatted); y += 29;
                    }
                    Temperature(new Rect(0, y, width, 25), pawn);
                }
            }
            finally { PawnInspectorWidgets.EndScroll(rect, ref statsScroll, PawnGearLayout.StatsHeight, 1); }
        }
        private static void CompactStat(Rect row, string label, float fill, string formatted)
        {
            const float nameWidth = 72, valueWidth = 82;
            PawnInspectorView.Label(new Rect(row.x, row.y, nameWidth, row.height), label, Theme.Ink);
            Rect bar = new Rect(row.x + nameWidth + 2, row.y + 7, Math.Max(12, row.width - nameWidth - valueWidth - 9), 10);
            PawnInspectorView.Bar(bar, fill, BarColor);
            PawnInspectorView.SmallLabel(new Rect(row.xMax - valueWidth, row.y, valueWidth, row.height), formatted, Theme.Muted, right: true);
            TooltipHandler.TipRegion(row, label + ": " + formatted);
        }

        private static void Temperature(Rect row, Pawn pawn)
        {
            float low = pawn.GetStatValue(StatDefOf.ComfyTemperatureMin), high = pawn.GetStatValue(StatDefOf.ComfyTemperatureMax);
            PawnInspectorView.Label(new Rect(row.x, row.y, 72, row.height), "Temp range", Theme.Ink);
            Rect ramp = new Rect(row.x + 74, row.y + 7, Math.Max(12, row.width - 163), 10);
            PawnGearLayout.TemperatureScale(low, high, out float scaleLow, out float scaleHigh);
            if (Event.current.type == EventType.Repaint)
            {
                EnsureRamp(); GUI.DrawTexture(ramp, temperatureRamp);
                float first = PawnGearLayout.Position(low, scaleLow, scaleHigh), last = PawnGearLayout.Position(high, scaleLow, scaleHigh);
                using (new Theme.GuiScope(true))
                {
                    GUI.color = new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, .65f);
                    Widgets.DrawBox(new Rect(ramp.x + first * ramp.width, ramp.y - .5f, Math.Max(1, (last - first) * ramp.width), ramp.height + 1), 1);
                }
            }
            string formatted = ClockPresentation.Temperature(low) + " ~ " + ClockPresentation.Temperature(high);
            PawnInspectorView.SmallLabel(new Rect(row.xMax - 82, row.y, 82, row.height), formatted, Theme.Muted, right: true);
            TooltipHandler.TipRegion(row, "Comfortable temperature: " + formatted + "\n" +
                ClockPresentation.Temperature(scaleLow) + " — " + ClockPresentation.Temperature(scaleHigh));
        }
        private static void EnsureRamp()
        {
            if (temperatureRamp != null) return;
            temperatureRamp = new Texture2D(128, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Color[] stops = { Theme.Rgb(0x334CBD), Theme.Rgb(0x408967), Theme.Rgb(0xB5A82D), Theme.Rgb(0xB92638) };
            var pixels = new Color[128];
            for (int i = 0; i < pixels.Length; i++)
            {
                float position = i / 127f * 3; int from = Math.Min(2, (int)position);
                pixels[i] = Color.Lerp(stops[from], stops[from + 1], position - from);
            }
            temperatureRamp.SetPixels(pixels); temperatureRamp.Apply(false, true);
        }

        private static void DrawItems(Rect rect, Pawn pawn, ITab_Pawn_Gear tab)
        {
            bool equipmentVisible = PawnGearNative.Show(tab, "Equipment", pawn), apparelVisible = PawnGearNative.Show(tab, "Apparel", pawn), inventoryVisible = PawnGearNative.Show(tab, "Inventory", pawn);
            var equipment = equipmentVisible ? pawn.equipment.AllEquipmentListForReading.Cast<Thing>().ToList() : new List<Thing>();
            if (equipmentVisible && pawn.apparel != null) equipment.AddRange(pawn.apparel.WornApparel.Where(item => item.def.apparel.layers.Contains(ApparelLayerDefOf.Belt)));
            var apparel = apparelVisible ? pawn.apparel.WornApparel.Where(item => !item.def.apparel.layers.Contains(ApparelLayerDefOf.Belt))
                .OrderByDescending(item => item.def.apparel.bodyPartGroups.Count > 0 ? item.def.apparel.bodyPartGroups[0].listOrder : 0).Cast<Thing>().ToList() : new List<Thing>();
            var inventory = inventoryVisible ? pawn.inventory.innerContainer.Cast<Thing>().ToList() : new List<Thing>();
            float height = (equipmentVisible ? PawnGearLayout.SectionHeight(equipment.Count) : 0) + (apparelVisible ? PawnGearLayout.SectionHeight(apparel.Count) : 0) + (inventoryVisible ? PawnGearLayout.SectionHeight(inventory.Count) : 0);
            float width = PawnInspectorWidgets.BeginScroll(rect, ref itemScroll, height);
            itemViewportHeight = rect.height;
            try
            {
                float y = 0;
                if (equipmentVisible) Section("EQUIPMENT", equipment, pawn, tab, width, ref y, false);
                if (apparelVisible) Section("APPAREL", apparel, pawn, tab, width, ref y, false);
                if (inventoryVisible) Section("INVENTORY", inventory, pawn, tab, width, ref y, true);
            }
            finally { PawnInspectorWidgets.EndScroll(rect, ref itemScroll, height, 2); }
        }
        private static void Section(string heading, List<Thing> things, Pawn pawn, ITab_Pawn_Gear tab, float width, ref float y, bool inventory)
        {
            PawnInspectorView.Heading(new Rect(0, y, width, 14), heading); y += PawnGearLayout.HeadingHeight;
            if (things.Count == 0)
            { PawnInspectorView.SmallLabel(new Rect(0, y, width, PawnGearLayout.EmptyHeight), "None".Translate(), Theme.Muted); y += PawnGearLayout.EmptyHeight; }
            foreach (Thing thing in things)
            {
                Rect row = new Rect(0, y, width, PawnGearLayout.RowHeight);
                if (thing != null && !thing.Destroyed) ItemRow(row, thing, pawn, tab, inventory);
                y += PawnGearLayout.RowHeight;
                PawnInspectorWidgets.Divider(new Rect(0, y - 1, width, 1));
            }
            y += PawnGearLayout.SectionGap;
        }
        private static void ItemRow(Rect row, Thing thing, Pawn pawn, ITab_Pawn_Gear tab, bool inventory)
        {
            bool interactive = GUI.enabled && row.yMin >= itemScroll.y && row.yMax <= itemScroll.y + itemViewportHeight;
            bool hover = interactive && Mouse.IsOver(row);
            if (hover) Theme.RoundRect(row, Theme.Hover.ToTransparent(.55f), 3);
            using (new Theme.GuiScope(true))
            { GUI.color = Color.white; Widgets.ThingIcon(new Rect(row.x + 1, row.y + 2, 20, 20), thing); }
            Rect mass = new Rect(row.xMax - 49, row.y, 49, row.height), action = new Rect(mass.x - 20, row.y + 3, 17, 17);
            float textX = row.x + 27, available = Math.Max(1, action.x - textX - 4);
            string name = GenLabel.ThingLabel(thing, 1, false, false).CapitalizeFirst(), detail = PawnGearNative.Condition(thing, pawn);
            float detailWidth = detail.Length == 0 ? 0 : Math.Min(available * .55f, PawnInspectorView.Measure(detail) * (10f / 11f) + 2);
            float nameWidth = Math.Min(PawnInspectorView.Measure(name) + 1, Math.Max(1, available - detailWidth - (detailWidth > 0 ? 5 : 0)));
            PawnInspectorView.Label(new Rect(textX, row.y, nameWidth, row.height), name, Theme.Ink);
            if (detailWidth > 0) PawnInspectorView.SmallLabel(new Rect(textX + nameWidth + 5, row.y, detailWidth, row.height), detail, Theme.Muted);
            PawnInspectorView.SmallLabel(mass, PawnGearLayout.StackMass(thing.GetStatValue(StatDefOf.Mass), thing.stackCount).ToStringMass(), Theme.Muted, right: true);
            if (hover)
            { using (new Theme.GuiScope(true)) { GUI.color = Theme.Muted; GUI.DrawTexture(action, InspectorIcons.Get("settings")); } }
            if (!interactive) return;
            TooltipHandler.TipRegion(row, thing.GetTooltip());
            TooltipHandler.TipRegion(new Rect(textX, row.y, available, row.height), thing.LabelCap + "\n" + detail);
            TooltipHandler.TipRegion(action, "Item actions");
            if (Widgets.ButtonInvisible(action)) { PawnGearNative.OpenActions(pawn, thing, tab, inventory); return; }
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && hover)
            { Event.current.Use(); PawnGearNative.OpenActions(pawn, thing, tab, inventory); return; }
            if (Widgets.ButtonInvisible(row)) Find.WindowStack.Add(new Dialog_InfoCard(thing));
        }
        private static float Armor(Pawn pawn, StatDef stat)
        {
            var parts = pawn.RaceProps.body.AllParts;
            return PawnInspectorLayout.Armor(pawn.GetStatValue(stat), parts.Select(part => part.coverageAbs).ToArray(),
                parts.Select(part => pawn.apparel?.WornApparel.Where(item => item.def.apparel.CoversBodyPart(part)).Select(item => item.GetStatValue(stat)).ToArray() ?? Array.Empty<float>()).ToArray());
        }
    }
}
