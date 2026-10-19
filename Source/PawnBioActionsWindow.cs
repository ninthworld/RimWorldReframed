using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // The original Character header owns destructive and conditional controls.
    // Keep its native confirmation/menu callbacks rather than simulating them.
    internal sealed class PawnBioActionsWindow : Window
    {
        private readonly Pawn pawn;
        private readonly Dialog_InfoCard informationOwner;
        internal PawnBioActionsWindow(Pawn pawn)
        {
            this.pawn = pawn;
            informationOwner = InfoCardPanel.CurrentCard;
            doCloseX = doCloseButton = doWindowBackground = drawShadow = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            forcePause = false;
        }
        protected override float Margin => 0;
        public override Vector2 InitialSize => new Vector2(380, 254);

        public override void DoWindowContents(Rect inRect)
        {
            bool authorized = PawnBioActionsLayout.NamingAuthorized(ReframedMod.PatchesReady, ReframedMod.Settings?.Enabled == true,
                informationOwner != null, ReframedMod.Settings?.InformationCards == true, ReframedMod.Active, ReframedMod.Settings?.SelectionPanels == true);
            if (!authorized || !PawnBioActionsNative.Ready || !PawnBioActionsNaming.Ready) { Close(); return; }
            bool valid;
            if (informationOwner != null) valid = InfoCardPanel.OwnsPawn(informationOwner, pawn);
            else
            {
                Thing selected = Find.Selector.SingleSelectedThing;
                valid = selected == pawn || selected is Corpse corpse && corpse.InnerPawn == pawn;
            }
            if (pawn == null || pawn.Destroyed || !valid)
            { Close(); return; }
            using (informationOwner != null ? InformationPopupScope.Enter() : PawnInspectorPopup.Enter())
            using (PawnBioActionsNaming.EnterOwner(informationOwner))
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                Theme.RoundRect(inRect, PawnInspectorView.BodyFill, 6);
                PawnInspectorView.Heading(new Rect(14, 9, inRect.width - 56, 20), "BIO ACTIONS");
                if (PawnBioActionsStyle.Close(new Rect(inRect.width - 31, 7, 24, 24))) Close();
                PawnInspectorView.Label(new Rect(14, 32, inRect.width - 28, 22), pawn.Name?.ToStringFull ?? pawn.LabelCap, Theme.Ink, true);
                PawnInspectorWidgets.Divider(new Rect(14, 60, inRect.width - 28, 1));
                float y = 69;
                var actions = PawnBioActionsNative.Actions(pawn);
                float height = PawnBioActionsLayout.ActionsHeight(actions.Count, informationOwner == null);
                if (Event.current.type == EventType.Repaint && Math.Abs(windowRect.height - height) > .1f)
                { Rect bounds = windowRect; bounds.y += (bounds.height - height) / 2; bounds.height = height; windowRect = bounds; }
                foreach (PawnBioActionsNative.Kind action in actions)
                {
                    Rect row = new Rect(14, y, inRect.width - 28, 26);
                    TooltipHandler.TipRegion(row, PawnBioActionsNative.Tooltip(action, pawn));
                    if (PawnInspectorWidgets.Button(row, PawnBioActionsNative.Label(action, pawn))) PawnBioActionsNative.Invoke(pawn, action);
                    y += 31;
                }
                if (actions.Count == 0) PawnInspectorView.SmallLabel(new Rect(14, y, inRect.width - 28, 24), "No character actions available.", Theme.Muted);
                if (informationOwner == null && PawnInspectorWidgets.Button(new Rect(14, inRect.height - 37, inRect.width - 28, 24), "Information", "info"))
                {
                    Close(); Find.WindowStack.Add(new Dialog_InfoCard(pawn));
                }
            }
        }
    }

}
