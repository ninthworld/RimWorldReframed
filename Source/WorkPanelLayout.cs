using System;

namespace ReframedUI
{
    internal static class WorkPanelLayout
    {
        internal const float Row = 30, Header = 30, Padding = 7, Tag = 18, TagPitch = 22, PrioritySpace = 22;
        internal const float EditorFooter = 30;
        internal static bool Supported(float width, float height) => width >= 700 && height >= 480;
        internal static float Width(float screen, float editor = 0) => Math.Min(Math.Max(780, editor), Math.Min(850, screen - 16));
        internal static float Bottom(float screen) => screen - 43;
        internal static float Height(float content, float screen) => Math.Min(screen - 83, Header + Padding + Math.Max(Row, content));
        internal static float EditorHeight(float content, float screen) => Math.Min(content + EditorFooter + 12, (screen - 83) * .55f);
        internal static float EditorViewport(float height) => height - EditorFooter - 12;
        internal static float EditorTagWidth(float measured, float column) => Math.Min(measured, Math.Max(1, column - PrioritySpace));
        internal static float Reveal(float scroll, float top, float height, float viewport)
        {
            if (height > viewport || top < scroll) return Math.Max(0, top);
            return top + height > scroll + viewport ? Math.Max(0, top + height - viewport) : scroll;
        }
        internal static bool FullyVisible(float top, float height, float scroll, float viewport) => top >= scroll - .01f && top + height <= scroll + viewport + .01f;
        internal static int Category(string name)
        {
            switch (name)
            {
                case "Patient": case "Doctor": case "PatientBedRest": case "Childcare": return 0;
                case "DarkStudy": case "Research": return 1;
                case "Growing": case "PlantCutting": case "Mining": case "Hunting": return 2;
                case "Cooking": case "Smithing": case "Tailoring": case "Crafting": case "Art": return 3;
                case "Firefighter": return 4;
                case "Construction": case "Hauling": case "Cleaning": case "Warden": case "Handling": return 5;
                default: return 6;
            }
        }
        private static readonly string[] ConceptOrder = { "Patient", "Doctor", "PatientBedRest", "Childcare", "DarkStudy", "Research",
            "Growing", "PlantCutting", "Mining", "Hunting", "Cooking", "Smithing", "Tailoring", "Crafting", "Art", "Firefighter",
            "Construction", "Hauling", "Cleaning", "Warden", "Handling" };
        internal static int EditorOrder(string name) { int index = Array.IndexOf(ConceptOrder, name); return index < 0 ? int.MaxValue : index; }
    }
}
