using System;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Presentation only. The native Page owns setup state, navigation and final validation.
    internal static class ReframedStoryteller
    {
        private static readonly ConditionalWeakTable<Listing_Standard, ViewState> Views =
            new ConditionalWeakTable<Listing_Standard, ViewState>();

        // true means the interface was drawn; false requires the original native interface.
        internal static bool Draw(Rect rect, ref StorytellerDef chosenStoryteller,
            ref DifficultyDef difficulty, ref Difficulty difficultyValues, Listing_Standard infoListing)
        {
            if (Current.ProgramState != ProgramState.Entry || Find.GameInitData == null ||
                TutorSystem.TutorialMode || infoListing == null || chosenStoryteller == null ||
                !chosenStoryteller.listVisible || chosenStoryteller.forcedDifficulty != null ||
                chosenStoryteller.disablePermadeath || difficultyValues == null ||
                (difficulty != null && difficulty.isCustom) || rect.width < 650f || rect.height < 300f ||
                float.IsNaN(rect.width) || float.IsNaN(rect.height) ||
                float.IsInfinity(rect.width) || float.IsInfinity(rect.height)) return false;

            ViewState view = Views.GetValue(infoListing, ignored => new ViewState());
            if (view.Storytellers.Length == 0 || view.Difficulties.Length == 0) return false;
            using (new Theme.GuiScope(true))
            {
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                if (view.LastStoryteller != chosenStoryteller)
                {
                    view.DetailsScroll = Vector2.zero;
                    view.LastStoryteller = chosenStoryteller;
                }
                float leftWidth = Mathf.Clamp(rect.width * .30f, 220f, 300f);
                Rect left = new Rect(rect.x, rect.y, leftWidth, rect.height);
                Rect right = new Rect(left.xMax + 14f, rect.y, rect.width - leftWidth - 14f, rect.height);
                DrawStorytellers(left, view, ref chosenStoryteller);
                DrawSettings(right, view, chosenStoryteller, ref difficulty, difficultyValues);
            }
            return true;
        }

        private static void DrawStorytellers(Rect bounds, ViewState view, ref StorytellerDef chosen)
        {
            Theme.Panel(bounds);
            Rect content = bounds.ContractedBy(12f);
            string introduction = "HowStorytellersWork".Translate();
            float introHeight = Text.CalcHeight(introduction, content.width);
            Wrapped(new Rect(content.x, content.y, content.width, introHeight), introduction, Theme.Muted);
            Rect viewport = new Rect(content.x, content.y + introHeight + 14f, content.width,
                Mathf.Max(20f, content.height - introHeight - 14f));
            Rect scrollContent = new Rect(0f, 0f, Mathf.Max(1f, viewport.width - 18f), view.Storytellers.Length * 116f);
            Widgets.BeginScrollView(viewport, ref view.StoryScroll, scrollContent);
            try
            {
                for (int i = 0; i < view.Storytellers.Length; i++)
                {
                    float y = i * 116f;
                    if (y + 116f < view.StoryScroll.y || y > view.StoryScroll.y + viewport.height) continue;
                    StorytellerDef storyteller = view.Storytellers[i];
                    Rect card = new Rect(0f, y, scrollContent.width, 108f);
                    ChoiceFace(card, storyteller == chosen);
                    Rect portrait = new Rect(card.x + 8f, card.y + 5f, card.width - 16f, 72f);
                    if (storyteller.portraitTinyTex != null)
                        GUI.DrawTexture(portrait, storyteller.portraitTinyTex, ScaleMode.ScaleToFit);
                    Label(new Rect(card.x + 8f, card.y + 77f, card.width - 16f, 27f), storyteller.LabelCap, Theme.Ink);
                    TooltipHandler.TipRegion(card, storyteller.description ?? storyteller.label);
                    if (Widgets.ButtonInvisible(card))
                    {
                        TutorSystem.Notify_Event("ChooseStoryteller");
                        chosen = storyteller;
                        view.DetailsScroll = Vector2.zero;
                        view.LastStoryteller = storyteller;
                    }
                }
            }
            finally { Widgets.EndScrollView(); }
        }

        private static void DrawSettings(Rect bounds, ViewState view, StorytellerDef chosen,
            ref DifficultyDef difficulty, Difficulty values)
        {
            Theme.Panel(bounds);
            Rect viewport = bounds.ContractedBy(16f);
            float width = Mathf.Max(1f, viewport.width - 18f);
            string description = chosen.description ?? "";
            float descriptionHeight = Text.CalcHeight(description, width);
            int rows = (view.Difficulties.Length + 1) / 2;
            string reloadInfo = "ReloadAnytimeModeInfo".Translate();
            string commitmentInfo = "PermadeathModeInfo".Translate();
            float cardWidth = (width - 10f) / 2f;
            float cardHeight = 42f + Mathf.Max(Text.CalcHeight(reloadInfo, cardWidth - 20f),
                Text.CalcHeight(commitmentInfo, cardWidth - 20f)) + 12f;
            float difficultyTop = 38f + descriptionHeight + 20f;
            float modeTop = difficultyTop + 28f + rows * 42f + 18f;
            float anomalyHeight = ModsConfig.AnomalyActive ? 48f : 0f;
            Rect scrollContent = new Rect(0, 0, width, modeTop + 28f + cardHeight + anomalyHeight + 16f);
            Widgets.BeginScrollView(viewport, ref view.DetailsScroll, scrollContent);
            try
            {
                Label(new Rect(0f, 0f, width, 30f), chosen.LabelCap, Theme.Ink);
                Wrapped(new Rect(0f, 38f, width, descriptionHeight), description, Theme.Muted);
                Label(new Rect(0f, difficultyTop, width, 25f), "Difficulty", Theme.Ink);
                float buttonWidth = (width - 10f) / 2f;
                for (int i = 0; i < view.Difficulties.Length; i++)
                {
                    DifficultyDef option = view.Difficulties[i];
                    Rect button = new Rect((i % 2) * (buttonWidth + 10f), difficultyTop + 28f + (i / 2) * 42f, buttonWidth, 34f);
                    ChoiceFace(button, option == difficulty);
                    Widgets.DrawBoxSolid(new Rect(button.x + 4f, button.y + 7f, 3f, button.height - 14f), option.isCustom ? Theme.Muted : Theme.Accent);
                    Label(new Rect(button.x + 14f, button.y, button.width - 20f, button.height),
                        option.LabelCap + (option.isCustom ? "…" : ""), Theme.Ink);
                    TooltipHandler.TipRegion(button, option.description.ResolveTags());
                    if (Widgets.ButtonInvisible(button))
                    {
                        // Exact native preset/custom initialization policy; no arbitrary reset on story/mode changes.
                        if (!option.isCustom) values.CopyFrom(option);
                        else if (option != difficulty) values.CopyFrom(DifficultyDefOf.Rough);
                        difficulty = option;
                    }
                }
                Label(new Rect(0, modeTop, width, 25f), "Playstyle", Theme.Ink);
                bool modeChosen = Find.GameInitData.permadeathChosen;
                bool commitment = Find.GameInitData.permadeath;
                Rect reload = new Rect(0f, modeTop + 28f, cardWidth, cardHeight);
                Rect committed = new Rect(cardWidth + 10f, modeTop + 28f, cardWidth, cardHeight);
                if (ModeCard(reload, "ReloadAnytimeMode".Translate(), reloadInfo, modeChosen && !commitment))
                {
                    Find.GameInitData.permadeathChosen = true;
                    Find.GameInitData.permadeath = false;
                }
                if (ModeCard(committed, "CommitmentMode".TranslateWithBackup("PermadeathMode"), commitmentInfo, modeChosen && commitment))
                {
                    Find.GameInitData.permadeathChosen = true;
                    Find.GameInitData.permadeath = true;
                }
                if (ModsConfig.AnomalyActive && Theme.ActionButton(new Rect(0f, reload.yMax + 14f, width, 34f), "AnomalySettings".Translate() + "…"))
                {
                    if (difficulty == null)
                        Messages.Message("MustChooseDifficulty".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                    else Find.WindowStack.Add(new Dialog_AnomalySettings(values));
                }
            }
            finally { Widgets.EndScrollView(); }
        }

        private static bool ModeCard(Rect rect, string title, string description, bool selected)
        {
            ChoiceFace(rect, selected);
            Label(new Rect(rect.x + 10f, rect.y + 5f, rect.width - 20f, 29f), title, Theme.Ink);
            Wrapped(new Rect(rect.x + 10f, rect.y + 40f, rect.width - 20f, rect.height - 48f), description, Theme.Muted);
            TooltipHandler.TipRegion(rect, title + "\n\n" + description);
            return Widgets.ButtonInvisible(rect);
        }

        private static void ChoiceFace(Rect rect, bool selected)
        {
            Theme.ButtonFace(rect, selected ? Theme.Selected : Mouse.IsOver(rect) ? Theme.Hover : Theme.Surface, !GUI.enabled, radius: 4f);
            if (selected) Widgets.DrawBoxSolid(new Rect(rect.x + 5f, rect.yMax - 3f, Mathf.Max(0f, rect.width - 10f), 2f), Theme.Good);
        }

        private static void Label(Rect rect, string text, Color color)
        {
            using (new Theme.GuiScope(true))
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                GUI.color = color;
                Widgets.Label(rect, text.Truncate(Mathf.Max(1f, rect.width)));
                TooltipHandler.TipRegion(rect, text);
            }
        }

        private static void Wrapped(Rect rect, string text, Color color)
        {
            using (new Theme.GuiScope(true))
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                GUI.color = color;
                Widgets.Label(rect, text);
            }
        }

        private sealed class ViewState
        {
            internal readonly StorytellerDef[] Storytellers = DefDatabase<StorytellerDef>.AllDefs
                .Where(storyteller => storyteller.listVisible).OrderBy(storyteller => storyteller.listOrder).ToArray();
            internal readonly DifficultyDef[] Difficulties = DefDatabase<DifficultyDef>.AllDefs.ToArray();
            internal Vector2 StoryScroll, DetailsScroll;
            internal StorytellerDef LastStoryteller;
        }
    }
}
