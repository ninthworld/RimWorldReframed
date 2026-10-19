using System;
using System.Collections.Generic;
using System.Linq;
using ReframedUI;

internal static class Program
{
    private sealed class Quest
    {
        internal readonly string Name;
        internal Quest Parent;
        internal readonly List<Quest> Children = new List<Quest>();
        internal Quest(string name) { Name = name; }
    }
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static List<Quest> Order(params Quest[] quests) => KnowledgeMenusPolicy.Hierarchy(quests, q => q.Parent, q => q.Children);
    public static void Main()
    {
        Check(KnowledgeMenusPolicy.RefreshSearch(null, ""), "initial native search state is synchronized once");
        Check(KnowledgeMenusPolicy.RefreshSearch("steel", "wood"), "changed query requests original native matching");
        Check(!KnowledgeMenusPolicy.RefreshSearch("steel", "steel"), "unchanged query cannot reset manually selected matching project/tab");
        Check(!KnowledgeMenusPolicy.RefreshSearch("", ""), "idle empty query does not refresh native selection");
        Check(KnowledgeMenusPolicy.RefreshSearch("steel", ""), "clear requests original native search cleanup");
        Check(!KnowledgeMenusPolicy.OwnsGraphPan(0, 0), "idle control zero never owns capture");
        Check(KnowledgeMenusPolicy.OwnsGraphPan(27, 27), "only exact graph control owns capture");
        Check(!KnowledgeMenusPolicy.OwnsGraphPan(27, 28), "release must never clear another widget hotControl");
        Check(KnowledgeMenusPolicy.GraphPanEvent(27, 27, 1, 1), "captured right-button drag pans");
        Check(KnowledgeMenusPolicy.GraphPanEvent(27, 27, 2, 2), "captured middle-button drag pans");
        Check(!KnowledgeMenusPolicy.GraphPanEvent(27, 28, 1, 1), "stale drag cannot consume other control input");
        Check(!KnowledgeMenusPolicy.GraphPanEvent(27, 27, 1, 0), "left drag cannot continue right capture");
        Check(!KnowledgeMenusPolicy.GraphPanEvent(27, 27, 2, 1), "right drag cannot continue middle capture");
        Check(!KnowledgeMenusPolicy.GraphPanEvent(27, 27, 0, 0), "left-button gesture never becomes pan capture");
        Check(KnowledgeMenusPolicy.GraphNodeInput(true, false, true, true, true, 0), "original left press selects actual graph project");
        Check(!KnowledgeMenusPolicy.GraphNodeInput(false, false, true, true, true, 0), "moving/disabled shell cannot activate graph nodes");
        Check(!KnowledgeMenusPolicy.GraphNodeInput(true, true, true, true, true, 0), "hidden nodes remain unknown and inert");
        Check(!KnowledgeMenusPolicy.GraphNodeInput(true, false, false, true, true, 0), "removed project identity cannot act");
        Check(!KnowledgeMenusPolicy.GraphNodeInput(true, false, true, false, true, 0), "clipped outside-viewport node part cannot act");
        Check(!KnowledgeMenusPolicy.GraphNodeInput(true, false, true, true, false, 0), "release/repaint cannot duplicate press action");
        Check(!KnowledgeMenusPolicy.GraphNodeInput(true, false, true, true, true, 1), "right drag is navigation, not node selection");
        foreach (float zoom in new[] { .9f, 1f, 1.5f })
            Check(60 * zoom - KnowledgeMenusPolicy.GraphNodeHeight * zoom >= 3.59f, "observed60px same-column native pair retains gap at all supported zooms");
        foreach (float zoom in new[] { -5f, .2f, .9f, 1f, 1.5f, 8f })
        {
            float actual = KnowledgeMenusPolicy.GraphZoom(zoom);
            Check(actual >= .9f && actual <= 1.5f, "zoom preserves readable minimum and bounded maximum");
            Check(KnowledgeMenusPolicy.GraphZoom(actual) == actual, "zoom clamp is stable");
        }
        foreach (float zoom in new[] { .9f, 1f, 1.5f }) foreach (float pointer in new[] { 0f, 100f, 480f })
        {
            float before = (325 + pointer) / 1.1f;
            float pan = KnowledgeMenusPolicy.ZoomAnchor(325, pointer, 1.1f, zoom);
            Check(Math.Abs((pan + pointer) / zoom - before) < .0001f, "zoom keeps project beneath cursor at same screen point before edge clamp");
        }
        Check(KnowledgeMenusPolicy.GraphPan(-20, 1000, 400) == 0, "drag cannot lose first project edge");
        Check(KnowledgeMenusPolicy.GraphPan(2000, 1000, 400) == 600, "last project edge stays reachable");
        Check(KnowledgeMenusPolicy.GraphPan(20, 100, 400) == 0, "small graph has no imaginary rail range");
        Check(KnowledgeMenusPolicy.GraphPan(215, 1000, 400) == 215, "ordinary pan survives unchanged");
        Check(!KnowledgeMenusPolicy.GraphFinite(float.NaN) && !KnowledgeMenusPolicy.GraphFinite(float.PositiveInfinity), "invalid mod position cannot poison graph bounds");
        Check(KnowledgeMenusPolicy.GraphFinite(-500) && KnowledgeMenusPolicy.GraphFinite(123456), "negative and large finite mod coordinates remain reachable");
        for (int filter = 1; filter <= 4; filter++)
            foreach (bool done in new[] { false, true }) foreach (bool active in new[] { false, true }) foreach (bool possible in new[] { false, true })
                Check(!KnowledgeMenusPolicy.Status(filter, true, done, active, possible), "hidden projects cannot reveal state through a status filter");
        Check(KnowledgeMenusPolicy.Status(0, true, false, false, false), "all filter may retain native UnknownResearch placeholder");
        Check(KnowledgeMenusPolicy.Status(1, false, false, false, true), "available means native CanStartNow and not complete/current");
        Check(!KnowledgeMenusPolicy.Status(1, false, false, true, true), "active project not repeated under available");
        Check(KnowledgeMenusPolicy.Status(2, false, false, true, false), "current includes anomaly projects even if currently not startable");
        Check(KnowledgeMenusPolicy.Status(3, false, true, false, false), "completed projects remain reachable");
        Check(KnowledgeMenusPolicy.Status(4, false, false, false, false), "locked project details remain reachable");
        Check(!KnowledgeMenusPolicy.Status(4, false, true, false, false), "finished is not a locked project");
        Check(KnowledgeMenusPolicy.CanStart(true, false, false, true), "only visible native-startable project can request original action");
        Check(!KnowledgeMenusPolicy.CanStart(false, false, false, true), "removed definition cannot be started");
        Check(!KnowledgeMenusPolicy.CanStart(true, true, false, true), "hidden project cannot be started by stale control");
        Check(!KnowledgeMenusPolicy.CanStart(true, false, true, true), "current project routes to separate native stop");
        Check(!KnowledgeMenusPolicy.CanStart(true, false, false, false), "native unmet requirements are never bypassed");
        foreach (float width in new[] { 530f, 760f, 1000f, 1800f })
        {
            float left = KnowledgeMenusPolicy.LeftWidth(width);
            Check(left >= 180 && left <= 280 && width - left - 12 >= 330, "supported widths preserve useful detail area");
        }
        Check(KnowledgeMenusPolicy.FullyVisible(10, 43, 10, 43), "exact row can be selected");
        Check(!KnowledgeMenusPolicy.FullyVisible(9, 43, 10, 43) && !KnowledgeMenusPolicy.FullyVisible(11, 43, 10, 43), "partially clipped rows cannot activate");
        var parent = new Quest("parent"); var child = new Quest("child") { Parent = parent }; parent.Children.Add(child);
        var grandchild = new Quest("grandchild") { Parent = child }; child.Children.Add(grandchild);
        var other = new Quest("other");
        Check(Order(child, other, parent, grandchild).Select(q => q.Name).SequenceEqual(new[] { "other", "parent", "child", "grandchild" }), "native sorted roots precede own child subtree even when child sorts earlier");
        Check(Order(child).Single() == child, "eligible child is retained when hidden parent is absent from native candidates");
        Check(Order(parent, child, child, grandchild).Count == 3, "native visited set avoids duplicate recursive rows");
        Check(Order(parent, other).SequenceEqual(new[] { parent, other }), "native callbacks do not introduce children excluded by native filters");
        Check(Order().Count == 0, "empty native quest list remains empty");
        var a = new Quest("same"); var b = new Quest("same");
        Check(Order(a, b).Count == 2, "duplicate visible names retain independent actual quests");
        Console.WriteLine(checks + " knowledge browser/filter/hierarchy/input contracts passed.");
    }
}
