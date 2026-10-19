using System;

namespace ReframedUI
{
    // UI-coordinate geometry only; also compiled by the dependency-free contracts.
    internal static class ArchitectLayout
    {
        internal const float Width = 740f, Height = 520f, CategoryWidth = 142f;
        internal const float Padding = 12f, HeaderHeight = 38f, InfoHeight = 104f;
        internal const float NativeCardHeight = 75f, CategoryRow = 28f;

        internal static bool Supported(float width, float height) => width >= 900f && height >= 600f;
        internal static float PanelHeight(float screenHeight) => Math.Min(Height, screenHeight - 100f);
        internal static float Bottom(float screenHeight) => screenHeight - 42f;
        // The bottom-left corner is stationary throughout both motion directions.
        internal static float RevealTop(float bottom, float height, float progress) => bottom - height * progress;
        internal static float ContentHeight(float viewport, float previous, float minimumY)
            => Math.Max(viewport, previous - minimumY);
        // Keep native bottom-to-top ordering, but top-align the complete block when
        // it fits. Measure relative to its own baseline so viewport padding cannot oscillate.
        internal static float MeasuredBlockHeight(float previous, float minimumY)
            => Math.Max(NativeCardHeight + 12f, previous - minimumY + 8f);
        internal static float ClampScroll(float scroll, float content, float viewport)
            => Math.Max(0f, Math.Min(scroll, Math.Max(0f, content - viewport)));
        internal static float ReanchorScroll(float scroll, float previous, float content, float viewport)
            => ClampScroll(scroll + content - previous, content, viewport);
    }
}
