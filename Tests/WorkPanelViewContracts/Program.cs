using System;
using ReframedUI;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    public static void Main()
    {
        Check(WorkPanelViewLayout.MeasureTag(40, 10, false, false) == 51, "unskilled work has no invented numeric metadata");
        Check(WorkPanelViewLayout.MeasureTag(40, 10, false, true) == 51, "passion without a relevant skill has no flame");
        float measured = WorkPanelViewLayout.MeasureTag(63.25f, 13.5f, true, true);
        var exact = WorkPanelViewLayout.Tag(measured, 13.5f, true, true);
        Check(exact.LabelWidth == 63.25f, "font-measured fractional task names fit exactly");
        Check(exact.SkillWidth == 13.5f && exact.FlameSize == 10, "two-digit skill and actual passion each retain reserved space");
        Check(exact.SkillX >= exact.LabelX + exact.LabelWidth + 4, "name never overlaps skill");
        Check(exact.FlameX >= exact.SkillX + exact.SkillWidth + 2, "skill never overlaps passion");
        Check(exact.FlameX + exact.FlameSize <= measured - 5, "metadata retains right inset");
        foreach (float width in new[] { 0, 1, 7, 8, 15, 25, 41, 100, 500 })
        {
            var part = WorkPanelViewLayout.Tag(width, 14, true, true);
            Check(part.LabelX >= 0 && part.LabelWidth >= 0 && part.LabelX + part.LabelWidth <= width &&
                part.SkillX >= 0 && part.SkillWidth >= 0 && part.SkillX + part.SkillWidth <= width &&
                part.FlameX >= 0 && part.FlameX + part.FlameSize <= width, "narrow layouts never escape their allotted bounds: " + width);
        }
        Check(WorkPanelViewLayout.Tag(35, 14, true, true).FlameSize == 0, "narrow tags sacrifice flame before numeric data");
        Check(WorkPanelViewLayout.MeasureTag(float.NaN, float.PositiveInfinity, true, true) == 27, "nonfinite metrics cannot poison panel geometry");
        Check(WorkPanelViewLayout.Click(true, true, true, true, 0) == 1, "left click returns assignment intent");
        Check(WorkPanelViewLayout.Click(true, true, true, true, 1) == -1, "right click returns reverse intent");
        Check(WorkPanelViewLayout.Click(true, true, true, true, 2) == 0, "middle mouse does not change work");
        Check(WorkPanelViewLayout.Click(false, true, true, true, 0) == 0, "incapable work cannot produce intent");
        Check(WorkPanelViewLayout.Click(true, false, true, true, 0) == 0, "disabled clipped rows cannot produce intent");
        Check(WorkPanelViewLayout.Click(true, true, false, true, 0) == 0, "repaint or mouse-up cannot repeat work click");
        Check(WorkPanelViewLayout.Click(true, true, true, false, 0) == 0, "outside clicks cannot activate tags");
        WorkPanelViewLayout.IdentityText(130, 120, 95, out float name, out float role);
        Check(name == 120 && role == 0, "long names keep their space before optional role text; full role stays in tooltip");
        WorkPanelViewLayout.IdentityText(40, 120, 95, out name, out role);
        Check(name == 40 && role == 0, "very narrow identities prioritize name, preserving role in tooltip");
        WorkPanelViewLayout.IdentityText(130, 33, 20, out name, out role);
        Check(name == 33 && role >= 20, "short identity leaves natural inline role space");
        Console.WriteLine(checks + " Work view layout/input contracts passed.");
    }
}
