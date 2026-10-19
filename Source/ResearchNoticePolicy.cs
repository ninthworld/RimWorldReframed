using System;

namespace ReframedUI
{
    internal static class ResearchNoticePolicy
    {
        internal const float Width = 320, Height = 82;
        internal static float Left(float screen) => Math.Max(8, screen - Width - 12);
        internal static float Top(float protectedTop) => Math.Max(8, protectedTop);
        internal static bool CanPlace(float screenHeight, float protectedTop)
            => !float.IsNaN(protectedTop) && !float.IsInfinity(protectedTop) && Top(protectedTop) + Height <= screenHeight - 45;
        internal static bool Supported(float width, float height) => width >= Width + 24 && height >= Height + 100;
        internal static string Age(double seconds) => seconds < 60 ? "Just now" : seconds < 3600
            ? ((int)(seconds / 60)) + "m ago" : ((int)(seconds / 3600)) + "h ago";
    }
}
