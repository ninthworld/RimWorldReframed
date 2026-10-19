using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal static class PawnInspectorBodies
    {
        internal static void SelectionChanged(Pawn pawn)
        {
            PawnNeedsPanel.Reset(); PawnGearPanel.Reset(); PawnSocialPanel.Reset();
            PawnBioPanel.Reset(); PawnHealthPanel.Reset(); PawnLogPanel.Reset();
        }
        internal static bool Custom(InspectTabBase tab) => tab?.GetType() == typeof(ITab_Pawn_Needs) ||
            tab?.GetType() == typeof(ITab_Pawn_Gear) || tab?.GetType() == typeof(ITab_Pawn_Social) ||
            tab?.GetType() == typeof(ITab_Pawn_Character) || tab?.GetType() == typeof(ITab_Pawn_Health) ||
            tab?.GetType() == typeof(ITab_Pawn_Log);
        internal static float NativeBodyOffset(InspectTabBase tab) => 0;
        internal static bool Draw(Rect rect, Pawn pawn, InspectTabBase tab)
        {
            if (!Custom(tab)) return false;
            Type type = tab.GetType();
            if ((type == typeof(ITab_Pawn_Social) && !PawnSocialPanel.Ready) ||
                (type == typeof(ITab_Pawn_Character) && (!PawnBioPanel.Ready || !PawnBioActionsNative.Ready || !PawnBioActionsNaming.Ready || pawn.story == null)) ||
                (type == typeof(ITab_Pawn_Health) && !PawnHealthPanel.Ready) ||
                (type == typeof(ITab_Pawn_Log) && !PawnLogPanel.Ready))
            { SelectionInspector.DrawNativeTab(rect, tab); return true; }
            if (tab.GetType() == typeof(ITab_Pawn_Needs)) PawnNeedsPanel.Draw(rect, pawn);
            else if (tab.GetType() == typeof(ITab_Pawn_Gear)) PawnGearPanel.Draw(rect, pawn, (ITab_Pawn_Gear)tab);
            else if (type == typeof(ITab_Pawn_Character))
            {
                PawnBioPanel.PopupScope = PawnInspectorPopup.Enter;
                PawnBioPanel.Draw(rect, pawn, (ITab_Pawn_Character)tab);
            }
            else if (type == typeof(ITab_Pawn_Health))
            {
                PawnHealthPanel.OpenOptions = () => PawnInspectorExtras.Show(pawn, tab);
                PawnHealthPanel.Draw(rect, pawn, (ITab_Pawn_Health)tab);
            }
            else if (type == typeof(ITab_Pawn_Log))
            {
                PawnLogPanel.OpenOptions = () => PawnInspectorExtras.Show(pawn, tab);
                PawnLogPanel.Draw(rect, pawn, (ITab_Pawn_Log)tab);
            }
            else
            {
                PawnSocialPanel.OpenOptions = () => PawnInspectorExtras.Show(pawn, tab);
                PawnSocialPanel.Draw(rect, pawn);
            }
            return true;
        }
    }
}
