using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal static class WorldMenusFactions
    {
        private sealed class State { internal Faction Selected; internal Vector2 List, Details; internal bool ShowAll; }
        private static readonly ConditionalWeakTable<MainTabWindow_Factions, State> states = new ConditionalWeakTable<MainTabWindow_Factions, State>();
        private static readonly FieldInfo ScrollTo = AccessTools.Field(typeof(MainTabWindow_Factions), "scrollToFaction");
        private static readonly MethodInfo Ongoing = AccessTools.Method(typeof(FactionUIUtility), "GetOngoingEvents");
        private static readonly MethodInfo Recent = AccessTools.Method(typeof(FactionUIUtility), "GetRecentEvents");
        private static readonly MethodInfo Natural = AccessTools.Method(typeof(FactionUIUtility), "GetNaturalGoodwillExplanation");
        internal static void Reset(MainTabWindow_Factions window) => states.Remove(window);
        internal static void Draw(MainTabWindow_Factions window, Rect body)
        {
            State state = states.GetOrCreateValue(window);
            if (!Prefs.DevMode) state.ShowAll = false;
            Rect toolbar = new Rect(body.x, body.y, body.width, 24);
            var all = Find.FactionManager.AllFactionsInViewOrder.ToList();
            var factions = all.Where(f => WorldMenusPolicy.VisibleFaction(f.IsPlayer, f.Hidden, state.ShowAll)).ToList();
            Faction requested = (Faction)ScrollTo.GetValue(window);
            if (requested != null) { state.Selected = requested; state.List.y = Math.Max(0, factions.IndexOf(requested)) * 53; ScrollTo.SetValue(window, null); }
            if (!factions.Contains(state.Selected)) state.Selected = factions.FirstOrDefault();
            WorldMenus.Label(toolbar, factions.Count + " " + "Factions".Translate(), true);
            if (Prefs.DevMode && WorldMenus.Button(new Rect(body.xMax - 130, body.y, 130, 23), "DEV: Show all", state.ShowAll)) state.ShowAll = !state.ShowAll;
            body.yMin += 31;
            float listWidth = Mathf.Clamp(body.width * .38f, 210, 310);
            Rect list = new Rect(body.x, body.y, listWidth, body.height);
            Rect details = new Rect(list.xMax + 12, body.y, Math.Max(1, body.width - listWidth - 12), body.height);
            WorldMenus.Surface(list); WorldMenus.Surface(details);
            Rect listView = list.ContractedBy(6);
            float content = factions.Count * 53;
            float width = PawnInspectorWidgets.BeginScroll(listView, ref state.List, content);
            try
            {
                for (int i = 0; i < factions.Count; i++)
                {
                    Rect row = new Rect(0, i * 53, width, 48);
                    if (row.yMax < state.List.y || row.y > state.List.y + listView.height) continue;
                    Faction faction = factions[i];
                    bool visible = row.y >= state.List.y && row.yMax <= state.List.y + listView.height;
                    using (new Theme.GuiScope(true))
                    {
                        GUI.enabled &= visible;
                        Theme.RoundRect(row, faction == state.Selected ? Theme.Selected.ToTransparent(.8f) : Mouse.IsOver(row) && GUI.enabled ? Theme.Hover : Color.clear, 3);
                        GUI.color = faction.Color; GUI.DrawTexture(new Rect(8, row.y + 9, 27, 27), faction.def.FactionIcon); GUI.color = Color.white;
                        WorldMenus.Label(new Rect(44, row.y + 4, width - 51, 20), faction.Name.CapitalizeFirst(), true);
                        WorldMenus.Label(new Rect(44, row.y + 24, width - 51, 18), faction.defeated ? "Defeated".Translate().ToString() : faction.PlayerRelationKind.GetLabelCap(), false, faction.defeated ? Theme.Muted : faction.PlayerRelationKind.GetColor());
                        if (GUI.enabled && Widgets.ButtonInvisible(row)) { state.Selected = faction; state.Details = Vector2.zero; }
                    }
                }
            }
            finally { PawnInspectorWidgets.EndScroll(listView, ref state.List, content, 70231); }
            if (state.Selected == null) { WorldMenus.Label(details.ContractedBy(12), "NoFactions".Translate()); return; }
            DrawDetails(details.ContractedBy(12), state, all);
        }
        private static void DrawDetails(Rect rect, State state, List<Faction> all)
        {
            Faction faction = state.Selected;
            float inner = rect.width - 10;
            string description = faction.def.Description;
            string goodwill = Goodwill(faction);
            var enemies = all.Where(f => f != faction && f.HostileTo(faction) && WorldMenusPolicy.VisibleFaction(f.IsPlayer, f.Hidden, state.ShowAll)).ToList();
            var ideos = new List<Ideo>();
            if (ModsConfig.IdeologyActive && !Find.IdeoManager.classicMode && faction.ideos != null)
            {
                if (faction.ideos.PrimaryIdeo != null) ideos.Add(faction.ideos.PrimaryIdeo);
                ideos.AddRange(faction.ideos.IdeosMinorListForReading);
            }
            float descriptionHeight = WorldMenus.Paragraph(new Rect(0, 0, inner, 0), description, false);
            float goodwillHeight = WorldMenus.Paragraph(new Rect(0, 0, inner, 0), goodwill, false);
            float total = 110 + descriptionHeight + goodwillHeight + (ideos.Count > 0 ? 26 + ideos.Count * 29 : 0) + (enemies.Count > 0 ? 26 + enemies.Count * 28 : 0);
            float width = PawnInspectorWidgets.BeginScroll(rect, ref state.Details, total);
            try
            {
                WorldMenus.Label(new Rect(0, 0, width - 32, 23), faction.Name.CapitalizeFirst(), true);
                if (PawnInspectorWidgets.IconButton(new Rect(width - 23, 0, 23, 23), "info", "Information")) Find.WindowStack.Add(new Dialog_InfoCard(faction));
                WorldMenus.Label(new Rect(0, 25, width, 18), faction.def.LabelCap, false, Theme.Muted);
                string leader = faction.leader == null ? "" : faction.LeaderTitle.CapitalizeFirst() + ": " + faction.leader.Name.ToStringFull;
                WorldMenus.Label(new Rect(0, 46, width, 18), leader, false, Theme.Muted);
                if (faction.leader != null && GUI.enabled) TooltipHandler.TipRegion(new Rect(0, 46, width, 18), leader);
                float y = 74;
                y += WorldMenus.Paragraph(new Rect(0, y, width, 0), description) + 14;
                y += WorldMenus.Paragraph(new Rect(0, y, width, 0), goodwill) + 14;
                if (ideos.Count > 0)
                {
                    PawnInspectorView.Heading(new Rect(0, y, width, 18), "Ideoligions".Translate().ToString().ToUpperInvariant()); y += 24;
                    foreach (Ideo ideo in ideos)
                    {
                        IdeoUIUtility.DoIdeoIcon(new Rect(0, y, 22, 22), ideo, true, () => IdeoUIUtility.OpenIdeoInfo(ideo));
                        WorldMenus.Label(new Rect(30, y, width - 30, 22), ideo.name); y += 29;
                    }
                }
                if (enemies.Count > 0)
                {
                    PawnInspectorView.Heading(new Rect(0, y, width, 18), "EnemyOf".Translate().ToString().ToUpperInvariant()); y += 24;
                    foreach (Faction enemy in enemies)
                    {
                        FactionUIUtility.DrawFactionIconWithTooltip(new Rect(0, y, 21, 21), enemy);
                        WorldMenus.Label(new Rect(29, y, width - 29, 22), enemy.Name); y += 28;
                    }
                }
            }
            finally { PawnInspectorWidgets.EndScroll(rect, ref state.Details, total, 70232); }
        }
        private static string Goodwill(Faction faction)
        {
            if (faction.IsPlayer) return "";
            if (faction.def.permanentEnemy) return "CurrentGoodwillTip_PermanentEnemy".Translate();
            if (!faction.HasGoodwill) return faction.PlayerRelationKind.GetLabelCap();
            string result = "Goodwill".Translate() + ": " + faction.PlayerGoodwill.ToStringWithSign() + " · " + faction.PlayerRelationKind.GetLabelCap() +
                "\n" + "NaturalGoodwill".Translate() + ": " + faction.NaturalGoodwill.ToStringWithSign();
            string natural = ((TaggedString)Natural.Invoke(null, new object[] { faction })).ToString();
            string ongoing = ((TaggedString)Ongoing.Invoke(null, new object[] { faction })).ToString();
            string recent = ((TaggedString)Recent.Invoke(null, new object[] { faction })).ToString();
            if (!natural.NullOrEmpty()) result += "\n\n" + natural;
            if (!ongoing.NullOrEmpty()) result += "\n\n" + "OngoingEvents".Translate() + ":\n" + ongoing;
            if (!recent.NullOrEmpty()) result += "\n\n" + "RecentEvents".Translate() + ":\n" + recent;
            string key = faction.PlayerRelationKind == FactionRelationKind.Ally ? "CurrentGoodwillTip_Ally" : faction.PlayerRelationKind == FactionRelationKind.Neutral ? "CurrentGoodwillTip_Neutral" : "CurrentGoodwillTip_Hostile";
            result += "\n\n" + (faction.PlayerRelationKind == FactionRelationKind.Neutral ? key.Translate("-75", "75") : key.Translate("0"));
            result += "\n\n" + "NaturalGoodwillRange".Translate() + ": " + Mathf.Clamp(faction.NaturalGoodwill - 50, -100, 100) + " " + "RangeTo".Translate() + " " + Mathf.Clamp(faction.NaturalGoodwill + 50, -100, 100);
            result += "\n" + "NaturalGoodwillDescription".Translate(1.25f.ToStringPercent());
            return result;
        }
    }
}
