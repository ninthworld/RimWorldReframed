using System;

namespace ReframedUI
{
    internal static class WorldMenusPolicy
    {
        internal static bool VisibleFaction(bool player, bool hidden, bool showAll) => !player && !hidden || showAll;
        internal static bool ArchiveVisible(bool letter, bool archivedDialog, bool message, bool showLetters, bool showMessages) =>
            (showLetters || !letter && !archivedDialog) && (showMessages || !message);
        internal static float GraphMinimum(float currentDays, int lastDays) => lastDays <= 0 ? 0 : Math.Max(0, currentDays - lastDays);
        internal static bool RowInput(float top, float bottom, float scroll, float height, bool enabled) => enabled && top >= scroll && bottom <= scroll + height;
        internal static float RevealTop(float bottom, float height, float progress) => bottom - height * Math.Max(0, Math.Min(1, progress));
    }
}
