using System;

namespace ReframedUI
{
    internal static class InfoCardLayout
    {
        internal static bool KnownTab(int tab) => tab >= 0 && tab <= 4;
        internal const float Header = 40, Tabs = 28, Padding = 12;
        internal const float ControlTop = 8, ControlHeight = 24;
        internal static float CloseLeft(float width) => width - Padding - ControlHeight;
        internal static float SearchLeft(float width) => CloseLeft(width) - 8 - 170;
        internal static float TabsLeft(bool portrait, bool history) => portrait ? 66 + (history ? 30 : 0) : Padding;
        internal static bool Supported(float width, float height) => width >= 560 && height >= 300;
        internal static float WindowWidth(float available) => Math.Max(1, Math.Min(640, available - 24));
        internal static float WindowHeight(float available) => Math.Max(1, Math.Min(420, available - 24));
        internal static bool Overflow(float content, float visible) => content > visible + .01f;
        internal static float LeftWidth(float total) => Math.Max(1, (total - 18) * .46f);
        internal static float PermitHeight(float minimum, float labelBottom) => Math.Max(minimum, labelBottom + 58);
        internal static void ScrollSpace(float width, float height, float contentWidth, float contentHeight,
            out float innerWidth, out float innerHeight, out bool vertical, out bool horizontal)
        {
            vertical = Overflow(contentHeight, height);
            horizontal = Overflow(contentWidth, width - (vertical ? 9 : 0));
            vertical = Overflow(contentHeight, height - (horizontal ? 9 : 0));
            horizontal = Overflow(contentWidth, width - (vertical ? 9 : 0));
            innerWidth = Math.Max(1, width - (vertical ? 9 : 0));
            innerHeight = Math.Max(1, height - (horizontal ? 9 : 0));
        }
    }
}
