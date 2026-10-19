using System;

namespace ReframedUI
{
    internal static class BottomMenuPolicy
    {
        internal const float Header = 42, Padding = 12, Gap = 8, Dock = 35;
        internal static bool Supported(int width, int height) => width >= 800 && height >= 600;
        internal static float Width(float preferred, int screen) => Math.Max(1, Math.Min(preferred, screen - Gap * 2));
        internal static float Height(float preferred, int screen) => Math.Max(1, Math.Min(preferred, screen - Dock - Gap * 2));
        internal static bool Interactive(bool open, bool settled, bool painted, bool ownsInput)
            => open && settled && painted && ownsInput;
        // A full-width vertical reveal has no sideways travel. Menus rise from
        // their bottom edge; dropdowns unfold from the edge next to the trigger.
        internal static float RevealTop(float top, float height, float progress, bool fromTop = false)
            => fromTop ? top : top + height * (1 - Math.Max(0, Math.Min(1, progress)));
        internal static bool PopupAbove(float triggerTop, float triggerBottom, float height, float screenHeight)
            => screenHeight - triggerBottom - Gap < height && triggerTop > screenHeight - triggerBottom;
        internal static float PopupLeft(float triggerLeft, float width, float screenWidth)
            => Math.Max(Gap, Math.Min(triggerLeft, screenWidth - Gap - width));
        internal static float PopupTop(float triggerTop, float triggerBottom, float height, float screenHeight, bool above)
            => Math.Max(Gap, Math.Min(above ? triggerTop - height - 3 : triggerBottom + 3, screenHeight - Gap - height));
        internal static void RevealBody(float top, float height, float titleHeight, float progress, bool fromTop, out float visibleTop, out float visibleHeight)
        {
            float unionHeight = height + titleHeight;
            float unionTop = RevealTop(top - titleHeight, unionHeight, progress, fromTop);
            visibleTop = Math.Max(top, unionTop);
            visibleHeight = Math.Max(0, Math.Min(top + height, unionTop + unionHeight * progress) - visibleTop);
        }
    }
}
