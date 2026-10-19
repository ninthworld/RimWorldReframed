using System;
using System.Collections.Generic;
using System.Linq;
using ReframedUI;

internal static class HistoryGraphTests
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
        double Eval(float[] data, int frequency, double day) => WorldMenusHistoryGraphPolicy.Evaluate(data, frequency, day);
        float[] source = { 0, 10, 30 };
        Check(Eval(source, 60000, .5) == 5 && Eval(source, 60000, 1.5) == 20, "native piecewise linear interpolation");
        Check(Eval(source, 60000, -10) == 0 && Eval(source, 60000, 100) == 30, "native first/last continuation across selected section");
        Check(Eval(source, 2500, 2500d / 60000) == 10, "recorder-specific sampling frequency maps to days");
        Check(Eval(new float[] { 17 }, 60000, 100) == 17, "single record is a real constant curve");
        Check(double.IsNaN(Eval(Array.Empty<float>(), 60000, 0)) && double.IsNaN(Eval(source, 0, 0)), "no fabricated empty or invalid-frequency samples");
        Check(double.IsNaN(Eval(new[] { 1f, float.NaN, 3 }, 60000, 1)), "nonfinite records remain gaps");
        var full = new IReadOnlyList<float>[] { new float[] { -30, 0, 1000 }, new float[] { 7, 22 } };
        var scale = WorldMenusHistoryGraphPolicy.Bounds(full, false, 0, 0, false);
        Check(scale.Min == -30 && scale.Max == 1000, "native auto Y scale uses all arrays/allhistory including off-section outlier");
        scale = WorldMenusHistoryGraphPolicy.Bounds(full, false, 0, 0, true);
        Check(scale.Min == 0 && scale.Max == 1000, "onlypositive flag clamps native negative minimum");
        scale = WorldMenusHistoryGraphPolicy.Bounds(full, true, -50, 50, false);
        Check(scale.Min == -50 && scale.Max == 50, "actual group fixed scale overrides all data");
        scale = WorldMenusHistoryGraphPolicy.Bounds(full, true, -50, 50, true);
        Check(scale.Min == 0 && scale.Max == 50, "native positive clamp occurs after fixed scale");
        foreach (float value in new[] { -100f, 0, 42, 1000000 })
        {
            scale = WorldMenusHistoryGraphPolicy.Bounds(new[] { (IReadOnlyList<float>)new[] { value, value } }, false, 0, 0, false);
            Check(scale.Min < value && scale.Max > value && scale.Span > 0, "flat series gets bounded visible inferred padding");
        }
        Check(WorldMenusHistoryGraphPolicy.Section(0, 0).Span > 0, "new colony zero-day view remains drawable");
        Check(WorldMenusHistoryGraphPolicy.RangeIndex(0, 15) == 3 && WorldMenusHistoryGraphPolicy.RangeIndex(5, 35) == 0 &&
            WorldMenusHistoryGraphPolicy.RangeIndex(5, 105) == 1 && WorldMenusHistoryGraphPolicy.RangeIndex(5, 305) == 2, "initial native section/range identity");
        Check(WorldMenusHistoryGraphPolicy.RangeIndex(5, 65) == -2, "external custom interval is not falsely called a preset");
        foreach (double span in new[] { .001, .2, 1, 8, 250, 10000, 1000000000 })
        {
            double tick = WorldMenusHistoryGraphPolicy.TickStep(new WorldMenusHistoryGraphPolicy.Scale(0, span), 4, false);
            Check(tick > 0 && !double.IsNaN(tick) && span / tick <= 4.0001, "readable finite chart tick density");
            Check(WorldMenusHistoryGraphPolicy.TickStep(new WorldMenusHistoryGraphPolicy.Scale(0, span), 4, true) >= 1, "native integer-only measure constraint");
        }
        var section = new WorldMenusHistoryGraphPolicy.Scale(.2, 1.2);
        var points = WorldMenusHistoryGraphPolicy.VisiblePoints(source, 60000, section, 700);
        Check(Math.Abs(points[0].Value - 2) < .0001 && Math.Abs(points[points.Count - 1].Value - 14) < .0001, "section boundary samples preserve native interpolation");
        Check(points.All(p => p.Day >= .2 && p.Day <= 1.2), "section clip never backtracks outside selected dates");
        var late = WorldMenusHistoryGraphPolicy.VisiblePoints(source, 60000, new WorldMenusHistoryGraphPolicy.Scale(20, 30), 300);
        Check(late.Count == 2 && late.All(p => p.Value == 30), "entirely future range uses constant endpoint without old out-of-range sample");
        float[] large = Enumerable.Repeat(0f, 10000).ToArray(); large[1729] = 9999; large[1730] = -500;
        var reduced = WorldMenusHistoryGraphPolicy.VisiblePoints(large, 60000, new WorldMenusHistoryGraphPolicy.Scale(0, 10000), 100);
        Check(reduced.Any(p => p.Value == 9999) && reduced.Any(p => p.Value == -500), "pixel reduction preserves positive and negative single-sample spikes");
        Check(reduced.Count <= 402, "large save chart work bounded by first/min/max/last per pixel");
        Check(reduced.Zip(reduced.Skip(1), (a, b) => a.Day <= b.Day).All(v => v), "reduced source samples stay chronological");
        var gap = WorldMenusHistoryGraphPolicy.VisiblePoints(new[] { 0f, 1, float.NaN, 3, 4 }, 60000, new WorldMenusHistoryGraphPolicy.Scale(0, 4), 1);
        Check(gap.Any(p => double.IsNaN(p.Value)), "decimation does not bridge missing data");
        double x1 = -1, y1 = -1, x2 = 2, y2 = 2;
        Check(WorldMenusHistoryGraphPolicy.Clip(ref x1, ref y1, ref x2, ref y2) && x1 == 0 && y1 == 0 && x2 == 1 && y2 == 1, "line clip is exact at both plot boundaries");
        x1 = 0; y1 = 2; x2 = 1; y2 = 3;
        Check(!WorldMenusHistoryGraphPolicy.Clip(ref x1, ref y1, ref x2, ref y2), "fixed-scale entirely out-of-range line is not painted");
        foreach (float value in new[] { float.MinValue, -1000, -1, 0, 1, 1000, float.MaxValue })
        {
            x1 = 0; x2 = 1; y1 = value; y2 = -value;
            if (WorldMenusHistoryGraphPolicy.Clip(ref x1, ref y1, ref x2, ref y2))
                Check(new[] { x1, x2, y1, y2 }.All(v => v >= 0 && v <= 1), "extreme actual values cannot escape plot or overflow GUI geometry");
        }
        foreach (int series in new[] { 0, 1, 3, 6, 30 })
        foreach (int columns in new[] { 1, 3, 4 })
        foreach (double cardHeight in new[] { 270d, 300, 337, 400 })
        {
            double legend = Math.Min(63, WorldMenusHistoryGraphPolicy.LegendTotal(series, columns));
            double plotBottom = 44 + WorldMenusHistoryGraphPolicy.PlotHeight(cardHeight, legend);
            Check(plotBottom + 33 <= cardHeight - legend - 7, "axis/day label and scrolling native-series legend do not overlap");
        }
        Console.WriteLine($"PASS {checks} recorder interpolation, native scale, compact chart, clipping and peak-preserving history contracts.");
    }
}
