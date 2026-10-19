using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal static class PawnInspectorExtrasSocial
    {
        internal static readonly Type EntryType = AccessTools.Inner(typeof(SocialCardUtility), "CachedSocialTabEntry");
        private static readonly MethodInfo Check = AccessTools.Method(typeof(SocialCardUtility), "CheckRecache");
        private static readonly MethodInfo DrawApproach = AccessTools.Method(typeof(SocialCardUtility), "DrawPregnancyApproach");
        private static readonly FieldInfo Entries = AccessTools.Field(typeof(SocialCardUtility), "cachedEntries");
        private static readonly FieldInfo Other = EntryType == null ? null : AccessTools.Field(EntryType, "otherPawn");
        private static readonly FieldInfo Approach = EntryType == null ? null : AccessTools.Field(EntryType, "pregnancyApproach");
        private static readonly FieldInfo ShowAll = AccessTools.Field(typeof(SocialCardUtility), "showAllRelations");
        internal static bool Ready => EntryType != null && Check != null && DrawApproach != null && Entries != null && Other != null && Approach != null && ShowAll != null;
        internal static bool AllRelations => (bool)ShowAll.GetValue(null);
        internal static void ToggleAllRelations()
        {
            ShowAll.SetValue(null, !AllRelations);
            SocialCardUtility.ClearCaches(); PawnSocialPanel.Reset();
        }
        internal static bool PregnancyAvailable(Pawn pawn) => Ready && ModsConfig.BiotechActive &&
            Current.ProgramState == ProgramState.Playing && Find.Storyteller.difficulty.ChildrenAllowed && Partners(pawn).Count > 0;
        internal sealed class Partner
        {
            internal object Native;
            internal Pawn Other;
            internal PregnancyApproach Approach;
        }
        internal static List<Partner> Partners(Pawn pawn)
        {
            var result = new List<Partner>();
            if (!Ready || pawn == null) return result;
            // Native cache controls love-partner, mutant and DLC eligibility.
            Check.Invoke(null, new object[] { pawn });
            foreach (object entry in (IEnumerable)Entries.GetValue(null))
                if (Approach.GetValue(entry) is PregnancyApproach approach && Other.GetValue(entry) is Pawn other)
                    result.Add(new Partner { Native = entry, Other = other, Approach = approach });
            return result;
        }
        internal static void DrawNativeApproach(Rect rect, Pawn pawn, Partner partner)
        {
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white; Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                // The small native control owns fertility/world-away acceptance,
                // muted rejection, tooltip, option icons and bilateral callbacks.
                DrawApproach.Invoke(null, new object[] { partner.Native, rect, pawn });
            }
        }
    }

    internal sealed class PawnInspectorExtrasPartnersWindow : Window
    {
        private readonly Pawn pawn;
        private readonly InspectTabBase tab;
        private Vector2 scroll;
        private const float Row = 43;
        internal PawnInspectorExtrasPartnersWindow(Pawn pawn, InspectTabBase tab)
        {
            this.pawn = pawn; this.tab = tab;
            doWindowBackground = drawShadow = false; doCloseX = true; closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }
        public override Vector2 InitialSize => new Vector2(440, Math.Min(360, 95 + PawnInspectorExtrasSocial.Partners(pawn).Count * Row));
        public override void DoWindowContents(Rect inRect)
        {
            if (!PawnInspectorExtras.Valid(pawn, tab) || !PawnInspectorExtrasSocial.PregnancyAvailable(pawn)) { Close(); return; }
            using (PawnInspectorPopup.Enter())
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white; Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                Theme.RoundRect(inRect, PawnInspectorView.BodyFill, 5);
                PawnInspectorView.Heading(new Rect(10, 7, inRect.width - 42, 18), "PregnancyApproach".Translate().ToString().ToUpperInvariant());
                var partners = PawnInspectorExtrasSocial.Partners(pawn);
                Rect viewport = new Rect(10, 31, inRect.width - 20, Math.Max(1, inRect.height - 40));
                float height = partners.Count * Row;
                float width = PawnInspectorWidgets.BeginScroll(viewport, ref scroll, height);
                try
                {
                    float y = 0;
                    foreach (var partner in partners)
                    {
                        PawnInspectorView.Label(new Rect(2, y, width - 42, 20), partner.Other.LabelShortCap, Theme.Ink);
                        PawnInspectorView.SmallLabel(new Rect(2, y + 18, width - 42, 19), PregnancyUtility.GetDescription(partner.Approach), Theme.Muted);
                        Rect icon = new Rect(width - 29, y + 7, 24, 24);
                        Theme.RoundRect(icon.ExpandedBy(2), Mouse.IsOver(icon) ? Theme.Hover : Theme.Surface, 3);
                        bool visible = PawnInspectorExtrasPolicy.FullyVisible(y, Row, scroll.y, viewport.height);
                        if (visible) PawnInspectorExtrasSocial.DrawNativeApproach(icon, pawn, partner);
                        else if (Event.current.type == EventType.Repaint)
                        { GUI.color = Theme.Muted; GUI.DrawTexture(icon, PregnancyUtility.GetIcon(partner.Approach)); GUI.color = Color.white; }
                        PawnInspectorWidgets.Divider(new Rect(0, y + Row - 2, width, 1)); y += Row;
                    }
                }
                finally { PawnInspectorWidgets.EndScroll(viewport, ref scroll, height, 18211); }
            }
        }
    }
}
