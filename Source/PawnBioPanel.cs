using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Bio has no source crop. Reuse Overview's skills/type on the common tab
    // background; biography and skills retain independent narrow scroll rails.
    internal static class PawnBioPanel
    {
        private sealed class Row
        {
            internal string Label, Detail;
            internal Func<string> Tip;
            internal Action Click;
            internal Texture2D Icon;
            internal Color Ink = Theme.Ink;
            internal Color IconColor = Color.white;
            internal float Height = 20;
            internal bool Heading;
        }

        private static readonly Func<WorkTags, IEnumerable<WorkTags>> NativeTags = Bind<Func<WorkTags, IEnumerable<WorkTags>>>("WorkTagsFrom", typeof(WorkTags));
        private static readonly Func<Pawn, WorkTags, string> NativeCauses = Bind<Func<Pawn, WorkTags, string>>("GetWorkTypeDisabledCausedBy", typeof(Pawn), typeof(WorkTags));
        private static readonly Func<WorkTags, string> NativeDisabledTypes = Bind<Func<WorkTags, string>>("GetWorkTypesDisabledByWorkTag", typeof(WorkTags));
        private static readonly Func<Pawn, WorkTags, Color> NativeTagColor = Bind<Func<Pawn, WorkTags, Color>>("GetDisabledWorkTagLabelColor", typeof(Pawn), typeof(WorkTags));
        private static readonly Func<Pawn, Faction, RoyalTitle, int, string> NativeTitleTip = Bind<Func<Pawn, Faction, RoyalTitle, int, string>>("GetTitleTipString", typeof(Pawn), typeof(Faction), typeof(RoyalTitle), typeof(int));
        private static readonly Func<SkillRecord, string> NativeSkillTip = BindSkillTip();
        private static readonly List<Row> rows = new List<Row>();
        private static readonly List<ExtraFaction> extraFactions = new List<ExtraFaction>();
        private static Vector2 biographyScroll, skillsScroll;
        // The integrator supplies the same ownership scope used for Bio's body.
        internal static Func<IDisposable> PopupScope = null;
        internal static bool Ready => NativeTags != null && NativeCauses != null && NativeDisabledTypes != null && NativeTagColor != null && NativeTitleTip != null && NativeSkillTip != null;

        private static Func<SkillRecord, string> BindSkillTip()
        {
            var method = AccessTools.Method(typeof(SkillUI), "GetSkillDescription", new[] { typeof(SkillRecord) });
            return method == null ? null : Delegate.CreateDelegate(typeof(Func<SkillRecord, string>), method, false) as Func<SkillRecord, string>;
        }

        private static T Bind<T>(string name, params Type[] args) where T : class
        {
            var method = AccessTools.Method(typeof(CharacterCardUtility), name, args);
            return method == null ? null : Delegate.CreateDelegate(typeof(T), method, false) as T;
        }

        internal static void Reset() { biographyScroll = skillsScroll = Vector2.zero; }

        internal static void Draw(Rect rect, Pawn pawn, ITab_Pawn_Character nativeTab)
        {
            if (pawn?.story == null || !Ready) return;
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                float leftWidth = Mathf.Floor((rect.width - 7) * .47f);
                Rect left = new Rect(rect.x, rect.y, leftWidth, rect.height);
                Rect right = new Rect(left.xMax + 7, rect.y, rect.width - leftWidth - 7, rect.height);
                BuildRows(pawn);
                DrawBiography(left, pawn);
                DrawSkills(right, pawn);
            }
        }

        private static void Section(string key)
        {
            rows.Add(new Row { Label = key.Translate().ToString().ToUpperInvariant(), Heading = true, Height = rows.Count == 0 ? 23 : 29 });
        }
        private static void Add(string label, Func<string> tip = null, Action click = null, string detail = null,
            Texture2D icon = null, Color? ink = null, Color? iconColor = null)
        {
            rows.Add(new Row { Label = label ?? "", Detail = detail, Tip = tip, Click = click, Icon = icon,
                Ink = ink ?? Theme.Ink, IconColor = iconColor ?? Color.white, Height = detail == null ? 20 : 34 });
        }

        private static void BuildRows(Pawn pawn)
        {
            rows.Clear();
            Section("TabCharacter");
            Add(pawn.Name?.ToStringFull ?? pawn.LabelCap, () => pawn.Name?.ToStringFull ?? pawn.LabelCap,
                CanRename(pawn) ? (Action)(() => Find.WindowStack.Add(PawnNamingUtility.NamePawnDialog(pawn, null))) : null);
            Add(pawn.MainDesc(false, true), () => pawn.ageTracker.AgeTooltipString, ink: Theme.Muted);
            if (pawn.guilt != null && pawn.guilt.IsGuilty)
                Add("Guilty", () => pawn.guilt.Tip, icon: TexUI.GuiltyTex, ink: Theme.Muted);
            foreach (BackstorySlot slot in Enum.GetValues(typeof(BackstorySlot)))
            {
                BackstoryDef backstory = pawn.story.GetBackstory(slot);
                if (backstory == null) continue;
                Add(backstory.TitleCapFor(pawn.gender), () => backstory.FullDescriptionFor(pawn).Resolve(),
                    detail: (slot == BackstorySlot.Childhood ? "Childhood" : "Adulthood").Translate());
            }
            if (!pawn.story.title.NullOrEmpty()) Add(pawn.story.title, detail: "BackstoryTitle".Translate());

            Section("Traits");
            List<Trait> traits = pawn.story.traits?.TraitsSorted;
            if (traits == null || traits.Count == 0)
                Add((pawn.DevelopmentalStage.Baby() ? "TraitsDevelopLaterBaby" : "None").Translate(), ink: Theme.Muted);
            else foreach (Trait trait in traits)
                Add(trait.LabelCap, () => trait.TipString(pawn), ink: trait.Suppressed ? Theme.Muted :
                    trait.sourceGene != null ? Color.Lerp(Theme.Ink, ColoredText.GeneColor, .6f) : Theme.Ink);

            rows.Add(new Row { Label = "IncapableOf".Translate(pawn).ToString().ToUpperInvariant(), Heading = true, Height = 29 });
            List<WorkTags> tags = NativeTags(pawn.CombinedDisabledWorkTags).ToList();
            if (tags.Count == 0) Add("None".Translate(), ink: Theme.Muted);
            else foreach (WorkTags tag in tags)
                Add(tag.LabelTranslated().CapitalizeFirst(), () => NativeCauses(pawn, tag) + "\n" + NativeDisabledTypes(tag),
                    ink: Color.Lerp(Theme.Muted, NativeTagColor(pawn, tag), .55f));

            // The native card filters and orders this same collection. These are
            // information links, never ability-casting controls.
            var abilities = pawn.abilities?.AllAbilitiesForReading?.Where(a => a.def.showOnCharacterCard)
                .OrderBy(a => a.def.level).ThenBy(a => a.def.EntropyGain).ToList();
            if (abilities != null && abilities.Count > 0)
            {
                Section("Abilities");
                foreach (Ability ability in abilities)
                    Add(ability.def.LabelCap, () => ability.Tooltip + "\n\n" + "ClickToLearnMore".Translate(),
                        () => Find.WindowStack.Add(new Dialog_InfoCard(ability.def)), icon: ability.def.uiIcon);
            }

            Section("Faction");
            Faction faction = pawn.Faction;
            if (faction != null && !faction.Hidden) Add(faction.Name, () => "FactionDesc".Translate(pawn.Named("PAWN")) + "\n\n" + "ClickToViewFactions".Translate(),
                () => Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Factions), icon: faction.def.FactionIcon, iconColor: faction.Color);
            extraFactions.Clear();
            QuestUtility.GetExtraFactionsFromQuestParts(pawn, extraFactions, null);
            GuestUtility.GetExtraFactionsFromGuestStatus(pawn, extraFactions);
            foreach (ExtraFaction extra in extraFactions)
            {
                if (extra.faction == pawn.Faction || extra.faction == null) continue;
                ExtraFaction captured = extra;
                Add(extra.faction.Name, () => captured.factionType.GetLabel().CapitalizeFirst() + "\n\n" +
                    "ExtraFactionDesc".Translate(pawn.Named("PAWN")) + "\n\n" + "ClickToViewFactions".Translate(),
                    () => Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Factions),
                    detail: extra.factionType.GetLabel().CapitalizeFirst(), icon: extra.faction.def.FactionIcon, iconColor: extra.faction.Color);
            }
            if (faction == null || faction.Hidden) Add("None".Translate(), ink: Theme.Muted);

            if (ModsConfig.BiotechActive && pawn.genes != null && pawn.genes.GenesListForReading.Any())
            {
                Section("Xenotype");
                Add(pawn.genes.XenotypeLabelCap, () => pawn.genes.XenotypeDescShort + "\n\n" + "ViewGenesDesc".Translate(pawn.Named("PAWN")),
                    () => OpenGenes(pawn), icon: pawn.genes.XenotypeIcon, iconColor: XenotypeDef.IconColor);
            }
            if (ModsConfig.IdeologyActive && pawn.Ideo != null && !Find.IdeoManager.classicMode)
            {
                Section("StatsReport_Ideoligion");
                Add(pawn.Ideo.name, () => pawn.Ideo.description, () => IdeoUIUtility.OpenIdeoInfo(pawn.Ideo),
                    icon: pawn.Ideo.Icon, iconColor: pawn.Ideo.Color);
                Precept_Role role = pawn.Ideo.GetRole(pawn);
                if (role != null) Add(role.LabelForPawn(pawn), () => role.GetTip(),
                    () => InspectPaneUtility.OpenTab(typeof(ITab_Pawn_Social)), icon: role.Icon, iconColor: pawn.Ideo.Color);
            }
            if (pawn.royalty != null && pawn.royalty.AllTitlesInEffectForReading.Count > 0)
            {
                Section("Title");
                foreach (RoyalTitle title in pawn.royalty.AllTitlesInEffectForReading)
                    Add(title.def.GetLabelCapFor(pawn) + " (" + pawn.royalty.GetFavor(title.faction) + ")",
                        () => NativeTitleTip(pawn, title.faction, title, pawn.royalty.GetFavor(title.faction)),
                        () => Find.WindowStack.Add(new Dialog_InfoCard(title.def, title.faction, pawn)),
                        detail: title.faction.Name, icon: title.faction.def.FactionIcon, iconColor: title.faction.Color);
            }
            if (ModsConfig.IdeologyActive && !pawn.DevelopmentalStage.Baby() && pawn.story.favoriteColor != null)
            {
                string orIdeo = pawn.Ideo != null && !pawn.Ideo.classicMode ? "OrIdeoColor".Translate(pawn.Named("PAWN")).ToString() : "";
                Add(pawn.story.favoriteColor.LabelCap, () => "FavoriteColorTooltip".Translate(pawn.Named("PAWN"),
                    pawn.story.favoriteColor.label.Named("COLOR"), 0.6f.ToStringPercent().Named("PERCENTAGE"), orIdeo.Named("ORIDEO")),
                    detail: "Favorite color", icon: BaseContent.WhiteTex, iconColor: pawn.story.favoriteColor.color);
            }
            if (pawn.guest != null && !pawn.guest.Recruitable)
                Add("Unrecruitable".Translate(), () => "UnrecruitableDesc".Translate(pawn.Named("PAWN")), ink: Theme.Muted);
            int questLines = 0;
            QuestUtility.AppendInspectStringsFromQuestParts((text, quest) =>
            {
                Add(text, () => text + "\n\n" + "ClickToViewInQuestsTab".Translate(), quest == null ? null : (Action)(() =>
                {
                    Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Quests);
                    ((MainTabWindow_Quests)MainButtonDefOf.Quests.TabWindow).Select(quest);
                }));
            }, pawn, out questLines);
        }

        private static bool CanRename(Pawn pawn) => !pawn.Dead && (pawn.IsColonist || pawn.IsColonySubhuman || DebugSettings.ShowDevGizmos);

        private static void OpenGenes(Pawn pawn)
        {
            // Same live-tab versus dialog routing as CharacterCardUtility.
            if (Current.ProgramState == ProgramState.Playing && Find.WindowStack.WindowOfType<Dialog_InfoCard>() == null &&
                Find.WindowStack.WindowOfType<Dialog_GrowthMomentChoices>() == null)
                InspectPaneUtility.OpenTab(typeof(ITab_Genes));
            else Find.WindowStack.Add(new Dialog_ViewGenes(pawn));
        }

        private static void DrawBiography(Rect viewport, Pawn pawn)
        {
            Rect area = viewport;
            float height = rows.Sum(r => r.Height);
            float width = PawnInspectorWidgets.BeginScroll(area, ref biographyScroll, height);
            try
            {
                float y = 0;
                for (int i = 0; i < rows.Count; i++)
                {
                    Row row = rows[i];
                    Rect line = new Rect(0, y, width, row.Height);
                    if (row.Heading)
                    {
                        if (i > 0) PawnInspectorWidgets.Divider(new Rect(0, y + 3, width, 1));
                        PawnInspectorView.Heading(new Rect(0, y + (i == 0 ? 0 : 8), width, 20), row.Label);
                        if (i == 0 && PawnInspectorWidgets.IconButton(new Rect(width - 20, y, 20, 20), "settings", "Bio actions"))
                            Find.WindowStack.Add(new PawnBioActionsWindow(pawn));
                    }
                    else if (line.yMax >= biographyScroll.y && line.y < biographyScroll.y + area.height)
                    {
                        if (row.Click != null && Mouse.IsOver(line)) Theme.RoundRect(line, Theme.Hover.ToTransparent(.65f), 2);
                        float x = 0;
                        if (row.Icon != null)
                        {
                            using (new Theme.GuiScope(true)) { GUI.color = row.IconColor; GUI.DrawTexture(new Rect(0, y + (row.Height - 14) / 2, 14, 14), row.Icon); }
                            x = 19;
                        }
                        if (row.Detail != null) PawnInspectorView.SmallLabel(new Rect(x, y, width - x, 14), row.Detail, Theme.Muted);
                        PawnInspectorView.Label(new Rect(x, y + (row.Detail == null ? 0 : 13), width - x, 20), row.Label, row.Ink);
                        if (Mouse.IsOver(line) && row.Tip != null) TooltipHandler.TipRegion(line, new TipSignal(row.Tip, unchecked(pawn.thingIDNumber * 397 ^ (i + 173031))));
                        if (row.Click != null && Widgets.ButtonInvisible(line)) row.Click();
                    }
                    y += row.Height;
                }
            }
            finally { PawnInspectorWidgets.EndScroll(area, ref biographyScroll, height, 17031); }
        }

        private static void DrawSkills(Rect viewport, Pawn pawn)
        {
            Rect area = viewport;
            bool baby = pawn.DevelopmentalStage.Baby();
            var skills = baby ? null : pawn.skills?.skills.OrderByDescending(s => s.def.listOrder).ToList();
            float height = 24 + (skills == null || skills.Count == 0 ? 20 : skills.Count * 22);
            float width = PawnInspectorWidgets.BeginScroll(area, ref skillsScroll, height);
            try
            {
                PawnInspectorView.Heading(new Rect(0, 0, width, 21), "Skills".Translate().ToString().ToUpperInvariant());
                if (skills == null || skills.Count == 0) PawnInspectorView.Label(new Rect(0, 24, width, 20),
                    (baby ? "SkillsDevelopLaterBaby" : "None").Translate(), Theme.Muted);
                else for (int i = 0; i < skills.Count; i++)
                {
                    SkillRecord skill = skills[i];
                    Rect row = new Rect(0, 24 + i * 22, width, 21);
                    if (row.yMax < skillsScroll.y || row.y > skillsScroll.y + area.height) continue;
                    PawnInspectorView.SkillLine(row, skill);
                    if (Mouse.IsOver(row)) TooltipHandler.TipRegion(row, new TipSignal(() => NativeSkillTip(skill), unchecked(pawn.thingIDNumber * 397 ^ skill.def.shortHash ^ 17032)));
                }
            }
            finally { PawnInspectorWidgets.EndScroll(area, ref skillsScroll, height, 17032); }
        }
    }
}
