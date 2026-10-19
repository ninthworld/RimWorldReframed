using System;

namespace ReframedUI
{
    internal static class EscMenuPolicy
    {
        internal const float Width = 238, Height = 330, Padding = 12, Row = 26, Gap = 4, Footer = 43;
        internal const float NavigationWidth = 80, NavigationHeight = 24;
        internal static float NavigationTop(float panelHeight) => panelHeight - (31 + NavigationHeight) / 2;
        internal static bool BlocksTools(bool selected, bool enabled, float reveal) => selected || enabled && reveal > 0;
        internal static bool NavigationInput(bool open, bool settled, bool ownsInput, bool destinationPainted)
            => open && settled && ownsInput && destinationPainted;
        internal static bool Supported(int width, int height) => width >= 640 && height >= 480;
        internal static bool RowInput(bool open, bool settled, bool ownsInput, bool samePaintedOptions,
            float top, float bottom, float scroll, float height)
            => open && settled && ownsInput && samePaintedOptions && top >= scroll && bottom <= scroll + height;
        internal static float Reveal(float closed, float open, float progress)
            => closed + (open - closed) * ToolsPanelMotion.Ease(Math.Max(0, Math.Min(1, progress)));
        internal static bool ConsumeOutside(bool open, bool ownsInput, bool mouseDown, bool inside)
            => open && ownsInput && mouseDown && !inside;
        internal static string[] ActionKeys(bool savingDisabled, bool commitment, bool anyFiles)
        {
            var keys = new System.Collections.Generic.List<string>();
            if (!savingDisabled && !commitment) keys.Add("Save");
            if (anyFiles && !commitment) keys.Add("LoadGame");
            keys.Add("ReviewScenario"); keys.Add("Options");
            bool saveQuit = commitment && !savingDisabled;
            keys.Add(saveQuit ? "SaveAndQuitToMainMenu" : "QuitToMainMenu");
            keys.Add(saveQuit ? "SaveAndQuitToOS" : "QuitToOS");
            return keys.ToArray();
        }
    }
}
