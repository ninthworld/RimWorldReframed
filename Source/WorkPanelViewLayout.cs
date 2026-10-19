using System;

namespace ReframedUI
{
    // Pure geometry, shared by measurement and painting. All text widths come
    // from the actual inspector font; no character-count width estimates.
    internal static class WorkPanelViewLayout
    {
        internal const float TagHeight = 18, RowHeight = 30;
        internal const float TagLeft = 6, TagRight = 5, MetadataGap = 4, FlameWidth = 10, FlameGap = 2;
        private static float Width(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Math.Max(0, value);
        internal static float MeasureTag(float label, float skill, bool hasSkill, bool flame)
            => TagLeft + Width(label) + (hasSkill ? MetadataGap + Width(skill) + (flame ? FlameGap + FlameWidth : 0) : 0) + TagRight;

        internal readonly struct TagParts
        {
            internal readonly float LabelX, LabelWidth, SkillX, SkillWidth, FlameX, FlameSize;
            internal TagParts(float labelX, float labelWidth, float skillX, float skillWidth, float flameX, float flameSize)
            { LabelX = labelX; LabelWidth = labelWidth; SkillX = skillX; SkillWidth = skillWidth; FlameX = flameX; FlameSize = flameSize; }
        }
        internal static TagParts Tag(float width, float skill, bool hasSkill, bool flame)
        {
            width = Width(width);
            float left = Math.Min(TagLeft, width), right = Math.Max(left, width - TagRight);
            // Narrow compatibility layouts sacrifice the flame before the skill,
            // then ellipsize the task name. Never paint outside the supplied rect.
            float flameSize = hasSkill && flame && right - left >= Width(skill) + MetadataGap + FlameWidth + FlameGap ? FlameWidth : 0;
            float flameX = right - flameSize;
            float skillRight = flameX - (flameSize > 0 ? FlameGap : 0);
            float skillWidth = hasSkill ? Math.Min(Width(skill), Math.Max(0, skillRight - left)) : 0;
            float skillX = skillRight - skillWidth;
            return new TagParts(left, Math.Max(0, (hasSkill ? skillX - MetadataGap : right) - left), skillX, skillWidth, flameX, flameSize);
        }
        internal static int Click(bool available, bool enabled, bool mouseDown, bool inside, int button)
            => available && enabled && mouseDown && inside ? button == 0 ? 1 : button == 1 ? -1 : 0 : 0;

        internal static void IdentityText(float available, float measuredName, float measuredRole, out float name, out float role)
        {
            available = Width(available);
            name = Math.Min(Width(measuredName), available);
            float remaining = Math.Max(0, available - name - 4);
            role = measuredRole > 0 && remaining >= 20 ? remaining : 0;
        }
    }
}
