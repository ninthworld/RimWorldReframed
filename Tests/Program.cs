using System;
using System.Linq;
using ReframedUI;

int cases = 0;
foreach (int available in new[] { 0, 28, 40, 56, 120, 220, 400, 716, 916, 1036, 1836, 3356 })
foreach (int buttonCount in new[] { 0, 1, 5, 12, 20, 60, 100 })
foreach (float buttonWidth in new[] { 27f, 45f, 58f, 100f, 140f, 164f, 240f, 600f })
{
    var widths = Enumerable.Repeat(buttonWidth, buttonCount).ToArray();
    int count = DockLayout.VisibleCount(widths, available, 3f, 40f, out float used, out bool overflow);
    if (count < 0 || count > buttonCount) throw new Exception("Visible count is outside the tab range");
    if (overflow != (count < buttonCount)) throw new Exception("Overflow does not describe all hidden actions");
    if (count == buttonCount && overflow) throw new Exception("Overflow should be absent when every tab fits");
    if (count < buttonCount && buttonCount > 0 && !overflow) throw new Exception("A hidden tab has no overflow access");
    if (used < 0f || (used > available && used != 0f)) throw new Exception("Returned row width exceeds its available area");
    if (overflow && available < 40f && (count != 0 || used != 0f))
        throw new Exception("Caller must fall back when the overflow control itself cannot fit");

    float expected = count == 0 ? (overflow && available >= 40f ? 40f : 0f) : count * buttonWidth + (count - 1) * 3f;
    if (overflow && count > 0) expected += 3f + 40f;
    if (Math.Abs(used - expected) > .01f) throw new Exception("Visible buttons and overflow width are inconsistent");
    cases++;
}

var mixed = new[] { 61f, 72f, 130f, 164f, 58f, 99f, 140f };
foreach (int available in new[] { 39, 40, 80, 180, 300, 500, 740 })
{
    int count = DockLayout.VisibleCount(mixed, available, 3f, 40f, out float used, out bool overflow);
    if (used > available) throw new Exception("Mixed-width row overlaps its reserved area");
    if (overflow != (count < mixed.Length)) throw new Exception("Mixed-width hidden action is unreachable");
    cases++;
}

var groupedWidths = new[] { 50f, 50f, 50f, 50f };
var groupedIds = new[] { 0, 0, 1, 1 };
int groupedCount = DockLayout.VisibleCount(groupedWidths, groupedIds, 197f, 3f, 9f, 32f,
    out float groupedUsed, out bool groupedOverflow);
if (groupedCount != 3 || !groupedOverflow || Math.Abs(groupedUsed - 197f) > .01f)
    throw new Exception("Overflow must account for a wider gap between source groups");
groupedCount = DockLayout.VisibleCount(groupedWidths, groupedIds, 215f, 3f, 9f, 32f,
    out groupedUsed, out groupedOverflow);
if (groupedCount != 4 || groupedOverflow || Math.Abs(groupedUsed - 215f) > .01f)
    throw new Exception("All source groups must fit with their wider separators");
groupedCount = DockLayout.VisibleCount(groupedWidths, groupedIds, 196f, 3f, 9f, 32f,
    out groupedUsed, out groupedOverflow);
if (groupedCount != 2 || !groupedOverflow || Math.Abs(groupedUsed - 138f) > .01f)
    throw new Exception("The dock must keep overflow and the visible group boundary within the available width");
cases += 3;

Console.WriteLine($"Passed {cases} dock geometry cases across 0–100 tabs, 27–600 UI-unit widths, 0–3356 available areas, and grouped source gaps.");
