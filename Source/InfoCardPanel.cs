using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ReframedUI
{
    // The subject/cache/tab/history pipeline remains Dialog_InfoCard's own.
    // This scope applies only while that exact native window is being drawn.
    internal static class InfoCardPanel
    {
        internal static Func<bool> Enabled = () => ReframedMod.Active && ReframedMod.Settings?.WindowTheme == true;
        internal static Dialog_InfoCard CurrentCard { get; set; }
        internal static bool Drawing => CurrentCard != null;
        internal static bool ContentsReady, BioReady, HealthReady;
        internal static int TabCount = 1;
        private static readonly FieldInfo ThingField = AccessTools.Field(typeof(Dialog_InfoCard), "thing");
        private static readonly FieldInfo StuffField = AccessTools.Field(typeof(Dialog_InfoCard), "stuff");
        private static readonly FieldInfo DefField = AccessTools.Field(typeof(Dialog_InfoCard), "def");
        private static readonly FieldInfo TabField = AccessTools.Field(typeof(Dialog_InfoCard), "tab");
        private static readonly MethodInfo PawnGetter = AccessTools.PropertyGetter(typeof(Dialog_InfoCard), "ThingPawn");
        private static readonly FieldInfo SearchName = AccessTools.Field(typeof(QuickSearchWidget), "controlName");
        private static GUIStyle small, body, title, search;
        private static readonly Dictionary<Dialog_InfoCard, Vector2> tabPositions = new Dictionary<Dialog_InfoCard, Vector2>();
        private sealed class NativeFlags { internal bool Shadow, CloseButton; }
        private static readonly ConditionalWeakTable<Dialog_InfoCard, NativeFlags> nativeFlags = new ConditionalWeakTable<Dialog_InfoCard, NativeFlags>();

        internal static bool Ready => ContentsReady && BioReady && HealthReady && InfoCardStats.Ready && InfoCardPermits.Ready && ThingField != null && StuffField != null &&
            DefField != null && TabField != null && PawnGetter != null && SearchName != null;
        internal static bool Eligible(Window window) => Enabled?.Invoke() == true && Ready && InfoCardLayout.Supported(UI.screenWidth, UI.screenHeight) && window?.GetType() == typeof(Dialog_InfoCard) &&
            InfoCardLayout.KnownTab(Convert.ToInt32(TabField.GetValue(window)));
        internal static Pawn SubjectPawn(Dialog_InfoCard card) => card == null ? null : (Pawn)PawnGetter.Invoke(card, null);
        internal static bool OwnsPawn(Dialog_InfoCard card, Pawn pawn) => card != null && pawn != null && !pawn.Destroyed &&
            Find.WindowStack.Windows.Contains(card) && ReferenceEquals(SubjectPawn(card), pawn);
        internal static void Release(Dialog_InfoCard card)
        {
            if (card == null) return;
            tabPositions.Remove(card);
            if (nativeFlags.TryGetValue(card, out NativeFlags flags))
            { card.drawShadow = flags.Shadow; card.doCloseButton = flags.CloseButton; nativeFlags.Remove(card); }
        }
        internal static void Prepare(Dialog_InfoCard card)
        {
            if (card.GetType() != typeof(Dialog_InfoCard)) return;
            if (!Eligible(card)) { Release(card); return; }
            if (!nativeFlags.TryGetValue(card, out NativeFlags flags))
            { flags = new NativeFlags { Shadow = card.drawShadow, CloseButton = card.doCloseButton }; nativeFlags.Add(card, flags); }
            card.drawShadow = false;
            card.doCloseButton = false;
        }

        internal static GUIStyle Style(bool wrap = true)
        {
            EnsureStyles();
            GUIStyle style = Text.Font == GameFont.Medium ? title : Text.Font == GameFont.Tiny ? small : body;
            style.alignment = Text.Anchor;
            style.wordWrap = wrap;
            return style;
        }
        private static void EnsureStyles()
        {
            if (body != null) return;
            using (new Theme.GuiScope(true))
            {
                Text.Font = GameFont.Small;
                body = new GUIStyle(Text.CurFontStyle) { fontSize = 11, richText = true, wordWrap = true,
                    padding = new RectOffset(), margin = new RectOffset(), clipping = TextClipping.Clip };
                body.normal.textColor = Color.white;
                small = new GUIStyle(body) { fontSize = 10 };
                title = new GUIStyle(body) { fontSize = 12, fontStyle = FontStyle.Bold };
                search = new GUIStyle(GUI.skin.textField) { font = body.font, fontSize = 11, alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(), margin = new RectOffset(), border = new RectOffset() };
                foreach (GUIStyleState state in new[] { search.normal, search.hover, search.active, search.focused,
                    search.onNormal, search.onHover, search.onActive, search.onFocused })
                { state.background = null; state.textColor = Theme.Ink; }
            }
        }
        internal static void Label(Rect rect, string text)
        {
            GUI.Label(rect, text ?? "", Style(Text.WordWrap));
        }
        internal static void DrawTitle(Rect rect, string text)
        {
            if (!Drawing) { Widgets.Label(rect, text); return; }
            float x = (HasPortrait ? 68 : 46) + (HasHistory() ? 30 : 0);
            float end = HasMaterials ? MaterialsRect.x - 8 : SearchAvailable ? SearchRect.x - 8 : CurrentCard.windowRect.width - 40;
            rect = new Rect(x, 5, Math.Max(1, end - x), 30);
            using (new Theme.GuiScope(true))
            {
                Text.Anchor = TextAnchor.MiddleLeft; Text.Font = GameFont.Medium; Text.WordWrap = false;
                GUI.color = Theme.Ink;
                PawnInspectorView.Label(rect, text, Theme.Ink, true);
                TooltipHandler.TipRegion(rect, text);
            }
        }
        private static readonly FieldInfo HistoryField = AccessTools.Field(typeof(Dialog_InfoCard), "history");
        private static bool HasHistory() => (HistoryField?.GetValue(null) as System.Collections.ICollection)?.Count > 0;
        private static bool HasMaterials => DefField.GetValue(CurrentCard) is BuildableDef buildable && GenStuff.AllowedStuffsFor(buildable).Any();
        private static bool SearchAvailable => CurrentCard.CommonSearchWidget != null;
        private static bool HasPortrait
        {
            get
            {
                Pawn pawn = SubjectPawn(CurrentCard);
                return pawn?.RaceProps.Humanlike == true && pawn.UIIconOverride == null && pawn.StyleDef?.UIIcon == null && pawn.def.uiIconPath.NullOrEmpty();
            }
        }
        internal static Rect SearchRect => new Rect(InfoCardLayout.SearchLeft(CurrentCard.windowRect.width), InfoCardLayout.ControlTop, 170, InfoCardLayout.ControlHeight);
        internal static Rect CloseRect => new Rect(InfoCardLayout.CloseLeft(CurrentCard.windowRect.width), InfoCardLayout.ControlTop,
            InfoCardLayout.ControlHeight, InfoCardLayout.ControlHeight);
        internal static Rect MaterialsRect
        {
            get
            {
                float width = Math.Min(140, Math.Max(90, PawnInspectorView.Measure("ShowMaterials".Translate()) + 16));
                float right = SearchAvailable ? SearchRect.x - 8 : CurrentCard.windowRect.width - 40;
                return new Rect(right - width, 8, width, 24);
            }
        }
        internal static void CompactIcon(ref Rect result, float x, float y, float width, float height) => result = Drawing ?
            new Rect(10 + (HasHistory() ? 30 : 0), 5, 30, 30) : new Rect(x, y, width, height);

        internal static void DrawSubjectIcon(Rect rect, Thing thing, float alpha, Rot4? rot, bool stackOfOne, float scale, bool grayscale)
        {
            if (Drawing && HasPortrait) return;
            Widgets.ThingIcon(rect, thing, alpha, rot, stackOfOne, scale, grayscale);
        }
        internal static void DrawPortrait()
        {
            if (!Drawing || !HasPortrait) return;
            // The portrait uses its own tall lane and paints after the tab band.
            PawnInspectorView.Portrait(new Rect(8 + (HasHistory() ? 30 : 0), 2, 50, 56), SubjectPawn(CurrentCard));
        }

        internal static void SingleTab()
        {
            Rect band = new Rect(0, InfoCardLayout.Header, CurrentCard.windowRect.width, InfoCardLayout.Tabs);
            Rect slot = new Rect(InfoCardLayout.TabsLeft(HasPortrait, HasHistory()), band.y, Math.Max(64, PawnInspectorView.Measure("TabStats".Translate()) + 24), band.height);
            PawnInspectorView.TabBand(band, slot);
            PawnInspectorView.Tab(slot, "TabStats".Translate(), true, "TabStats".Translate(), bandPainted: true);
        }

        internal static TabRecord DrawTabs(Rect rect, List<TabRecord> tabs, float maxWidth)
        {
            if (!Drawing) return TabDrawer.DrawTabs(rect, tabs, maxWidth);
            TabCount = tabs.Count;
            float left = InfoCardLayout.TabsLeft(HasPortrait, HasHistory());
            Rect viewport = new Rect(left, InfoCardLayout.Header, Math.Max(1, CurrentCard.windowRect.width - left - 12), InfoCardLayout.Tabs);
            float total = tabs.Sum(tab => Math.Max(64, PawnInspectorView.Measure(tab.label) + 24)) + Math.Max(0, tabs.Count - 1) * 4;
            tabPositions.TryGetValue(CurrentCard, out Vector2 position);
            position.x = Mathf.Clamp(position.x, 0, Math.Max(0, total - viewport.width));
            float cursor = 0;
            Rect active = new Rect(viewport.x, viewport.y, 0, viewport.height);
            foreach (TabRecord tab in tabs)
            {
                float width = Math.Max(64, PawnInspectorView.Measure(tab.label) + 24);
                if (tab.Selected)
                {
                    float start = Mathf.Clamp(viewport.x + cursor - position.x, viewport.xMin, viewport.xMax);
                    float end = Mathf.Clamp(viewport.x + cursor + width - position.x, start, viewport.xMax);
                    active = new Rect(start, viewport.y, end - start, viewport.height);
                }
                cursor += width + 4;
            }
            PawnInspectorView.TabBand(new Rect(0, viewport.y, CurrentCard.windowRect.width, viewport.height), active);
            position = GUI.BeginScrollView(viewport, position, new Rect(0, 0, Math.Max(total, viewport.width), viewport.height), false, false, GUIStyle.none, GUIStyle.none);
            TabRecord clicked = null;
            try
            {
                float x = 0;
                foreach (TabRecord tab in tabs)
                {
                    float width = Math.Max(64, PawnInspectorView.Measure(tab.label) + 24);
                    Rect slot = new Rect(x, 0, width, viewport.height);
                    MouseoverSounds.DoRegion(slot, SoundDefOf.Mouseover_Tab);
                    if (!tab.TutorTag.NullOrEmpty()) UIHighlighter.HighlightOpportunity(slot, tab.TutorTag);
                    if (PawnInspectorView.Tab(slot, tab.label, tab.Selected, tab.GetTip(), bandPainted: true))
                    {
                        SoundDefOf.RowTabSelect.PlayOneShotOnCamera(); tab.clickedAction?.Invoke(); clicked = tab;
                    }
                    x += width + 4;
                }
            }
            finally { GUI.EndScrollView(); }
            tabPositions[CurrentCard] = position;
            if (total > viewport.width)
                PawnInspectorWidgets.Rail(new Rect(viewport.x, viewport.yMax - 3, viewport.width, 3), ref position.x, total, viewport.width, true);
            tabPositions[CurrentCard] = position;
            return clicked;
        }

        internal static void DrawBio(Rect rect, Pawn pawn, Action randomize, Rect creationRect, bool showName)
        {
            if (!Drawing || pawn?.story == null || !PawnBioPanel.Ready || !PawnBioActionsNative.Ready || !PawnBioActionsNaming.Ready)
            { CharacterCardUtility.DrawCharacterCard(rect, pawn, randomize, creationRect, showName); return; }
            PawnBioPanel.Draw(rect, pawn, null);
        }

        internal static void DrawHealth(Rect rect, Pawn pawn, bool allowOperations, bool showBloodLoss, Thing thingForMedBills)
        {
            // Native health reserves20px for its own navigation before drawing.
            // The card already has a tab row, so reclaim only that empty strip.
            if (Drawing) { rect.y -= 20; rect.height += 20; }
            HealthCardUtility.DrawPawnHealthCard(rect, pawn, allowOperations, showBloodLoss, thingForMedBills);
        }
        internal static TabRecord DrawHealthTabs(Rect rect, List<TabRecord> tabs, float maxWidth)
        {
            // InfoCard supplies allowOperations=false. Its single Overview
            // choice has no navigation purpose; native health data still draws.
            if (Drawing && tabs.Count == 1) return null;
            return TabDrawer.DrawTabs(rect, tabs, maxWidth);
        }

        internal static void DrawSearch(QuickSearchWidget widget, Rect rect, Action changed, Action clear)
        {
            EnsureStyles();
            if (widget.CurrentlyFocused() && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            { widget.Unfocus(); Event.current.Use(); }
            if (OriginalEventUtility.EventType == EventType.MouseDown && !rect.Contains(Event.current.mousePosition)) widget.Unfocus();
            Theme.RoundRect(rect, widget.CurrentlyFocused() ? Theme.Muted : Theme.Border, 3);
            Theme.RoundRect(rect.ContractedBy(1), Theme.PanelFill, 2);
            using (new Theme.GuiScope(true))
            {
                GUI.color = Theme.Muted;
                GUI.DrawTexture(new Rect(rect.x + 6, rect.y + 6, 12, 12), ToolsIcons.Get("search"));
            }
            bool active = widget.filter.Active;
            Rect input = new Rect(rect.x + 24, rect.y + 2, Math.Max(1, rect.width - 29 - (active ? 36 : 0)), rect.height - 4);
            string next;
            using (new Theme.GuiScope(true))
            {
                GUI.color = widget.noResultsMatched && active ? ColorLibrary.RedReadable : Theme.Ink;
                GUI.SetNextControlName((string)SearchName.GetValue(widget));
                next = GUI.TextField(input, widget.filter.Text, widget.maxSearchTextLength, search);
            }
            if (next != widget.filter.Text) { widget.filter.Text = next; changed?.Invoke(); }
            if (active)
            {
                Rect button = new Rect(rect.xMax - 36, rect.y + 2, 34, rect.height - 4);
                TooltipHandler.TipRegion(button, "Clear search");
                using (new Theme.GuiScope(true))
                {
                    PawnInspectorView.SmallLabel(button, "Clear", Mouse.IsOver(button) ? Theme.Ink : Theme.Muted);
                }
                if (Widgets.ButtonInvisible(button))
                {
                    widget.filter.Text = ""; SoundDefOf.CancelMode.PlayOneShotOnCamera(); changed?.Invoke(); clear?.Invoke();
                }
            }
        }

        // Root's WindowTheme calls this before its general WindowTheme setting.
        internal static bool DrawBackground(Rect rect)
        {
            if (!Drawing) return false;
            using (new Theme.GuiScope(true))
            {
                GUI.color = Color.white;
                GUI.BeginClip(new Rect(rect.x, rect.y, rect.width, InfoCardLayout.Header));
                try { Theme.RoundRect(new Rect(0, 0, rect.width, rect.height), PawnInspectorView.HeaderFill, 10); }
                finally { GUI.EndClip(); }
                float bodyTop = InfoCardLayout.Header + InfoCardLayout.Tabs;
                GUI.BeginClip(new Rect(rect.x, rect.y + bodyTop, rect.width, Math.Max(1, rect.height - bodyTop)));
                try { Theme.RoundRect(new Rect(0, -bodyTop, rect.width, rect.height), PawnInspectorView.BodyFill, 10); }
                finally { GUI.EndClip(); }
            }
            return true;
        }
    }
}
