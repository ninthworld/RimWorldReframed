using System;

namespace ReframedUI
{
    internal static class PawnSocialLayout
    {
        internal const float HeaderHeight = 40f, Gap = 10f, CardGap = 7f, CardHeight = 94f;
        internal const int Columns = 3;
        internal static float RelationsWidth(float width) => width * .42f;
        internal static float CardWidth(float contentWidth) => Math.Max(1f, (contentWidth - CardGap * (Columns - 1)) / Columns);
        internal static float RelationsHeight(int count) => count <= 0 ? 0f :
            ((count + Columns - 1) / Columns) * (CardHeight + CardGap) - CardGap;
        internal static float TimestampWidth(float measured, float cardWidth) => Math.Max(0f, Math.Min(measured + 3f, cardWidth * .48f));
    }
}
