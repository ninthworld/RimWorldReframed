using System;
using ReframedUI;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    private static bool Near(float value, float expected) => Math.Abs(value - expected) < .0001f;
    public static void Main()
    {
        Check(!PawnInspectorPopup.Active, "unrelated menus start outside inspector ownership");
        var outer = PawnInspectorPopup.Enter();
        Check(PawnInspectorPopup.Active, "inspector callback owns menus it opens");
        using (PawnInspectorPopup.Enter()) Check(PawnInspectorPopup.Active, "nested callback inherits ownership");
        Check(PawnInspectorPopup.Active, "nested scope cannot release outer ownership");
        outer.Dispose(); outer.Dispose();
        Check(!PawnInspectorPopup.Active, "double dispose cannot leak ownership to unrelated menus");
        try { using (PawnInspectorPopup.Enter()) throw new InvalidOperationException(); }
        catch (InvalidOperationException) { }
        Check(!PawnInspectorPopup.Active, "failed callback still releases popup ownership");
        Check(!PawnInspectorLayout.Supported(899, 824), "narrow viewport keeps native inspector");
        Check(!PawnInspectorLayout.Supported(1375, 599), "short viewport keeps native inspector");
        Check(PawnInspectorLayout.Supported(900, 600), "bounded layout starts at supported logical dimensions");
        Check(PawnInspectorLayout.BoundedHeight(1000, 600) == 500, "oversized native body leaves map and dock space");
        Check(PawnInspectorLayout.BoundedHeight(236, 824) == 236, "normal summary preserves source-derived proportions");
        Check(PawnInspectorLayout.GearHeight - PawnInspectorLayout.Header - PawnInspectorLayout.Tabs - 20 >= 172,
            "compact armor, mass and temperature fit the normalized concept body");
        foreach (float height in new[] { PawnInspectorLayout.NeedsHeight, PawnInspectorLayout.GearHeight, PawnInspectorLayout.SocialHeight,
            PawnInspectorLayout.BioHeight, PawnInspectorLayout.HealthHeight, PawnInspectorLayout.LogHeight })
            Check(height == PawnInspectorLayout.DetailHeight && height > PawnInspectorLayout.SummaryHeight,
                "Only Overview is shorter; every main detail tab keeps the same outer height");
        foreach (float measured in new[] { 17.2f, 27f, 43.8f, 75.01f, 180.6f })
        {
            Check(PawnInspectorLayout.TabWidth(measured, true) - 32 >= measured + 6,
                "icon tab retains complete measured label plus rounding space");
            Check(PawnInspectorLayout.TabWidth(measured, false) - 14 >= measured + 6,
                "extra text-only tab retains complete measured label");
        }
        Check(PawnInspectorLayout.NameWidth(110.5f, 150) >= 110.5f,
            "name is not truncated by a proportional cap when it fits");
        Check(PawnInspectorLayout.NameWidth(220, 150) == 150,
            "long name uses all space before its tooltip is needed");
        Check(PawnInspectorLayout.NameWidth(80, -4) == 0,
            "crowded name lane cannot create a negative rectangle");
        Check(PawnInspectorLayout.Ratio(1, 0) == 0, "missing capacity is not division by zero");
        Check(PawnInspectorLayout.Ratio(float.NaN, 1) == 0, "invalid readout cannot create invalid geometry");
        Check(PawnInspectorLayout.Ratio(-1, 1) == 0 && PawnInspectorLayout.Ratio(3, 1) == 1, "outlying need/stat values remain clipped");
        Check(Near(PawnInspectorLayout.Armor(0, new[] { 1f }, new[] { Array.Empty<float>() }), 0), "no armor stays zero");
        Check(Near(PawnInspectorLayout.Armor(.4f, new[] { .3f, .7f }, new[] { Array.Empty<float>(), Array.Empty<float>() }), .4f), "natural armor applies across all body coverage");
        Check(Near(PawnInspectorLayout.Armor(0, new[] { .5f, .5f }, new[] { new[] { .6f }, Array.Empty<float>() }), .3f), "partial apparel coverage does not protect other parts");
        Check(Near(PawnInspectorLayout.Armor(.4f, new[] { 1f }, new[] { new[] { .6f } }), .88f), "natural and worn armor combine by native remaining-chance product");
        Check(Near(PawnInspectorLayout.Armor(.4f, new[] { 1f }, new[] { new[] { .6f, .6f } }), 1.216f), "multiple layers retain native nonlinear calculation");
        Check(PawnInspectorLayout.Armor(5, new[] { 1f }, new[] { new[] { 9f } }) == 2, "native 200percent upper limit retained");
        Check(PawnInspectorLayout.Armor(-1, new[] { 1f }, new[] { new[] { -1f } }) == 0, "negative armor cannot reverse the bar");
        Console.WriteLine("PASS " + checks + " inspector geometry/native-armor contracts. No live UI acceptance claimed.");
    }
}
