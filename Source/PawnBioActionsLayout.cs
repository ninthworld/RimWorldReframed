using System;
namespace ReframedUI
{
    internal static class PawnBioActionsLayout
    {
        internal const float NamingWidth = 480, FieldStart = 62, FieldStep = 34, FieldHeight = 28;
        internal static float NamingHeight(int fields) => 110 + Math.Max(1, Math.Min(4, fields)) * FieldStep;
        internal static float ActionsHeight(int count, bool information) => 83 + Math.Max(1, Math.Min(4, count)) * 31 + (information ? 37 : 0);
        internal static bool NamingAuthorized(bool patches, bool enabled, bool information, bool informationCards, bool active, bool selectionPanels) =>
            patches && enabled && (information ? informationCards : active && selectionPanels);
    }
}
