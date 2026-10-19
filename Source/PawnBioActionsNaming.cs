using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace ReframedUI
{
    internal static class PawnBioActionsNaming
    {
        internal static readonly Type Context = AccessTools.Inner(typeof(Dialog_NamePawn), "NameContext");
        internal static readonly MethodInfo MakeRow = AccessTools.Method(Context, "MakeRow");
        private static readonly MethodInfo FocusNext = AccessTools.Method(typeof(Dialog_NamePawn), "FocusNextControl");
        private static readonly MethodInfo Contents = AccessTools.Method(typeof(Dialog_NamePawn), "DoWindowContents");
        internal static readonly FieldInfo PawnField = AccessTools.Field(typeof(Dialog_NamePawn), "pawn");
        private static readonly FieldInfo Names = AccessTools.Field(typeof(Dialog_NamePawn), "names");
        private static readonly FieldInfo First = AccessTools.Field(typeof(Dialog_NamePawn), "firstCall");
        private static readonly FieldInfo FocusOverride = AccessTools.Field(typeof(Dialog_NamePawn), "focusControlOverride");
        private static readonly FieldInfo LabelWidth = AccessTools.Field(Context, "labelWidth"), TextWidth = AccessTools.Field(Context, "textboxWidth");
        private static readonly FieldInfo TextboxName = AccessTools.Field(Context, "textboxName");
        private static readonly FieldInfo Size = AccessTools.Field(typeof(Dialog_NamePawn), "size");
        private static readonly FieldInfo Description = AccessTools.Field(typeof(Dialog_NamePawn), "descriptionText");
        private sealed class Owner
        {
            internal Pawn Pawn; internal Dialog_InfoCard Card; internal bool Information;
            internal bool Background, Shadow, CloseX, CloseButton; internal Vector2 NativeSize;
        }
        private static readonly ConditionalWeakTable<Dialog_NamePawn, Owner> Owners = new ConditionalWeakTable<Dialog_NamePawn, Owner>();
        private static Dialog_InfoCard actionOwner;
        internal static Dialog_NamePawn Current;
        internal static bool ForceAccept, RowsReady, InputReady, ButtonsReady;
        private static int nativeButtonIndex;
        private static string inputName;
        internal static bool Ready => RowsReady && InputReady && ButtonsReady && Names != null && MakeRow != null && FocusNext != null;
        internal static IDisposable EnterOwner(Dialog_InfoCard card) => new ActionOwner(card);
        private sealed class ActionOwner : IDisposable
        {
            private readonly Dialog_InfoCard previous;
            internal ActionOwner(Dialog_InfoCard card) { previous = actionOwner; actionOwner = card; }
            public void Dispose() { actionOwner = previous; }
        }
        internal static void Register(Window window)
        {
            if (!Ready || window?.GetType() != typeof(Dialog_NamePawn) ||
                !(PawnInspectorPopup.Active || InformationPopupScope.Active || InfoCardPanel.Drawing)) return;
            var dialog = (Dialog_NamePawn)window;
            if (Owners.TryGetValue(dialog, out _)) return;
            var owner = new Owner { Pawn = (Pawn)PawnField.GetValue(dialog), Card = actionOwner ?? InfoCardPanel.CurrentCard,
                Information = InformationPopupScope.Active || InfoCardPanel.Drawing,
                Background = dialog.doWindowBackground, Shadow = dialog.drawShadow, CloseX = dialog.doCloseX, CloseButton = dialog.doCloseButton,
                NativeSize = (Vector2)Size.GetValue(dialog) };
            int count = ((IList)Names.GetValue(dialog)).Count;
            if (!Authorized(owner.Information) || count < 1 || count > 4 || !(owner.Pawn.Name is NameTriple || owner.Pawn.Name is NameSingle)) return;
            Owners.Add(dialog, owner); dialog.doWindowBackground = dialog.drawShadow = dialog.doCloseX = dialog.doCloseButton = false;
        }
        private static bool Authorized(bool information) =>
            PawnBioActionsLayout.NamingAuthorized(ReframedMod.PatchesReady, ReframedMod.Settings?.Enabled == true, information,
                ReframedMod.Settings?.InformationCards == true, ReframedMod.Active, ReframedMod.Settings?.SelectionPanels == true);
        internal static bool Owned(Dialog_NamePawn dialog) => dialog != null && Owners.TryGetValue(dialog, out Owner owner) && Authorized(owner.Information);
        internal static void Prepare()
        {
            foreach (Window window in Find.WindowStack.Windows)
                if (window is Dialog_NamePawn dialog && Owners.TryGetValue(dialog, out Owner owner) && !Owned(dialog))
                {
                    dialog.doWindowBackground = owner.Background; dialog.drawShadow = owner.Shadow;
                    dialog.doCloseX = owner.CloseX; dialog.doCloseButton = owner.CloseButton;
                    Size.SetValue(dialog, owner.NativeSize); Owners.Remove(dialog);
                }
        }
        internal static Vector2 Dimensions(Dialog_NamePawn dialog) => new Vector2(PawnBioActionsLayout.NamingWidth, PawnBioActionsLayout.NamingHeight(((IList)Names.GetValue(dialog)).Count));
        internal static bool Draw(Dialog_NamePawn dialog, Rect rect)
        {
            if (!Owned(dialog) || ForceAccept) return true;
            Owners.TryGetValue(dialog, out Owner owner);
            bool valid = owner.Pawn != null && !owner.Pawn.Destroyed && (owner.Card != null ? InfoCardPanel.OwnsPawn(owner.Card, owner.Pawn) :
                Find.Selector.SingleSelectedThing == owner.Pawn || Find.Selector.SingleSelectedThing is Corpse corpse && corpse.InnerPawn == owner.Pawn);
            if (!valid) { dialog.Close(); return false; }
            using (owner.Information ? InformationPopupScope.Enter() : PawnInspectorPopup.Enter())
            using (new Theme.GuiScope(true))
            {
                var previous = Current; Current = dialog;
                try
                {
                    GUI.color = GUI.contentColor = Color.white; Text.Font = GameFont.Small; Text.Anchor = TextAnchor.MiddleLeft; Text.WordWrap = false;
                    bool accept = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
                    if (accept) Event.current.Use();
                    if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab)
                    { FocusNext.Invoke(dialog, new object[] { GUI.GetNameOfFocusedControl(), !Event.current.shift }); Event.current.Use(); }
                    Theme.RoundRect(rect, PawnInspectorView.BodyFill, 6);
                    PawnInspectorView.Heading(new Rect(14, 10, rect.width - 56, 20), "RenameColonist".Translate().ToString().ToUpperInvariant());
                    if (PawnBioActionsStyle.Close(new Rect(rect.width - 31, 7, 24, 24))) dialog.Close();
                    PawnInspectorView.SmallLabel(new Rect(14, 32, rect.width - 28, 20), owner.Pawn.Name.ToStringFull, Theme.Muted);
                    TooltipHandler.TipRegion(new Rect(14, 30, rect.width - 28, 23), ((TaggedString)Description.GetValue(dialog)).ToString());
                    float y = 62;
                    foreach (object context in (IList)Names.GetValue(dialog))
                    {
                        float oldLabel = (float)LabelWidth.GetValue(context), oldText = (float)TextWidth.GetValue(context);
                        LabelWidth.SetValue(context, 90f); TextWidth.SetValue(context, rect.width - 90f - 88f - 46f);
                        var divider = new RectDivider(new Rect(14, y, rect.width - 28, 28), 19031, new Vector2(6, 0));
                        object[] args = { owner.Pawn, 88f, "Randomize".Translate(), "Suggested".Translate() + "…", divider, (string)FocusOverride.GetValue(dialog) };
                        string previousInput = inputName; inputName = (string)TextboxName.GetValue(context);
                        try { MakeRow.Invoke(context, args); FocusOverride.SetValue(dialog, args[5]); }
                        finally { inputName = previousInput; LabelWidth.SetValue(context, oldLabel); TextWidth.SetValue(context, oldText); }
                        y += 34;
                    }
                    if ((bool)First.GetValue(dialog)) { FocusNext.Invoke(dialog, new object[] { null, true }); First.SetValue(dialog, false); }
                    string focus = (string)FocusOverride.GetValue(dialog);
                    if (!string.IsNullOrEmpty(focus) && Event.current.type == EventType.Repaint) { GUI.FocusControl(focus); FocusOverride.SetValue(dialog, null); }
                    if (PawnInspectorWidgets.Button(new Rect(14, rect.height - 39, 100, 25), "Cancel".Translate())) dialog.Close();
                    accept |= PawnInspectorWidgets.Button(new Rect(rect.width - 114, rect.height - 39, 100, 25), "Accept".Translate());
                    if (accept) Accept(dialog);
                }
                finally { Current = previous; }
            }
            return false;
        }
        private static void Accept(Dialog_NamePawn dialog)
        {
            // Native BuildName, IsValid, title setter, messages and baby deadline
            // remain the complete original acceptance path. No custom name mutation.
            Rect oldRect = dialog.windowRect; Vector2 oldSize = (Vector2)Size.GetValue(dialog);
            bool previous = ForceAccept; ForceAccept = true; nativeButtonIndex = 0; Event.current.Use();
            using (new Theme.GuiScope(true))
            {
                GUI.enabled = false; GUI.BeginGroup(new Rect(-10000, -10000, 1, 1));
                try { Contents.Invoke(dialog, new object[] { new Rect(0, 0, oldSize.x, oldSize.y) }); }
                finally { GUI.EndGroup(); ForceAccept = previous; Size.SetValue(dialog, oldSize); dialog.windowRect = oldRect; }
            }
        }
        internal static bool Footer(Rect rect, string label, bool background, bool sound, bool active, TextAnchor? anchor) =>
            ForceAccept ? ++nativeButtonIndex == 2 : Widgets.ButtonText(rect, label, background, sound, active, anchor);
        internal static bool RowButton(Rect rect, string label, bool background, bool sound, bool active, TextAnchor? anchor) =>
            Current != null ? !ForceAccept && PawnInspectorWidgets.Button(rect, label, enabled: active) : Widgets.ButtonText(rect, label, background, sound, active, anchor);
        internal static string TextField(Rect rect, string value) => Current != null && !ForceAccept ?
            PawnBioActionsStyle.Input(rect, value, inputName != null && GUI.GetNameOfFocusedControl() == inputName) : Widgets.TextField(rect, value);
        internal static void RowLabel(Rect rect, string value)
        { if (Current != null && !ForceAccept) PawnInspectorView.Label(rect, value, GUI.enabled ? Theme.Ink : Theme.Muted); else Widgets.Label(rect, value); }
        internal static void TaggedLabel(Rect rect, TaggedString value) => RowLabel(rect, value.ToString());
    }
}
