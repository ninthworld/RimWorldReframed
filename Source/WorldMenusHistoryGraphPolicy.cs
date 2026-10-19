using System;
using System.Collections.Generic;

namespace ReframedUI
{
    // Pure numerical adapter for the actual recorder arrays. No game state writes.
    internal static class WorldMenusHistoryGraphPolicy
    {
        internal readonly struct Scale
        {
            internal readonly double Min, Max;
            internal Scale(double min, double max) { Min = min; Max = max; }
            internal double Span => Max - Min;
        }
        internal readonly struct Point
        {
            internal readonly double Day, Value;
            internal Point(double day, double value) { Day = day; Value = value; }
        }
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        internal static double LegendTotal(int series, int columns) => Math.Ceiling(Math.Max(0, series) / (double)Math.Max(1, columns)) * 21;
        internal static double PlotHeight(double card, double legend) => Math.Max(36, card - 94 - legend);
        internal static double Day(int index, int frequency) => index * (double)frequency / 60000d;
        internal static int RangeIndex(double min, double max)
        {
            if (min <= .00001) return 3; // Native initial graph is always All days.
            double span = max - min;
            return Math.Abs(span - 30) < .001 ? 0 : Math.Abs(span - 100) < .001 ? 1 : Math.Abs(span - 300) < .001 ? 2 : -2;
        }
        internal static Scale Bounds(IEnumerable<IReadOnlyList<float>> series, bool fixedScale, double fixedMin, double fixedMax, bool positive)
        {
            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            foreach (IReadOnlyList<float> records in series)
                foreach (float value in records) if (Finite(value)) { min = Math.Min(min, value); max = Math.Max(max, value); }
            if (!Finite(min)) { min = 0; max = 1; }
            if (fixedScale && Finite(fixedMin) && Finite(fixedMax)) { min = fixedMin; max = fixedMax; }
            if (positive) min = Math.Max(0, min);
            // Native flat ranges are blank. Preserve a fixed nondegenerate scale;
            // only degenerate/invalid ranges need a documented visible flat line.
            if (max <= min)
            {
                double padding = Math.Max(1, Math.Abs(min) * .05); max = min + padding;
                if (!fixedScale) min = positive ? Math.Max(0, min - padding) : min - padding;
            }
            return new Scale(min, max);
        }
        internal static Scale Section(double min, double max) => new Scale(Math.Max(0, Finite(min) ? min : 0),
            Math.Max(Math.Max(0, Finite(min) ? min : 0) + 1d / 60000d, Finite(max) ? max : 0));
        internal static double Evaluate(IReadOnlyList<float> records, int frequency, double day)
        {
            if (records == null || records.Count == 0 || frequency <= 0 || !Finite(day)) return double.NaN;
            double index = Math.Max(0, Math.Min(records.Count - 1, day * 60000d / frequency));
            int low = (int)Math.Floor(index), high = Math.Min(records.Count - 1, low + 1);
            double left = records[low], right = records[high];
            return Finite(left) && Finite(right) ? left + (right - left) * (index - low) : double.NaN;
        }
        internal static double Unit(double value, Scale scale) => (value - scale.Min) / scale.Span;
        internal static double TickStep(Scale scale, int wanted, bool integers)
        {
            double raw = scale.Span / Math.Max(1, wanted), power = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double normalized = raw / power;
            double step = (normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 2.5 ? 2.5 : normalized <= 5 ? 5 : 10) * power;
            return integers ? Math.Max(1, Math.Ceiling(step)) : step;
        }
        // Preserve spikes by retaining each pixel bucket's earliest, minimum,
        // maximum and latest source samples in chronological order.
        internal static List<Point> VisiblePoints(IReadOnlyList<float> values, int frequency, Scale section, int pixels)
        {
            var result = new List<Point>();
            if (values == null || values.Count == 0 || frequency <= 0 || pixels <= 0) return result;
            result.Add(new Point(section.Min, Evaluate(values, frequency, section.Min)));
            int begin = Math.Max(0, (int)Math.Min(values.Count, Math.Ceiling(section.Min * 60000d / frequency)));
            int end = Math.Min(values.Count - 1, (int)Math.Min(values.Count - 1, Math.Floor(section.Max * 60000d / frequency)));
            int i = begin;
            while (i <= end)
            {
                int bucket = (int)Math.Floor(Unit(Day(i, frequency), section) * pixels), last = i;
                int min = i, max = i;
                while (last + 1 <= end && (int)Math.Floor(Unit(Day(last + 1, frequency), section) * pixels) == bucket)
                {
                    last++;
                    if (!Finite(values[last])) break;
                    if (!Finite(values[min]) || values[last] < values[min]) min = last;
                    if (!Finite(values[max]) || values[last] > values[max]) max = last;
                }
                int[] indices = { i, min, max, last }; Array.Sort(indices);
                int previous = -1;
                foreach (int index in indices) if (index != previous) { result.Add(new Point(Day(index, frequency), values[index])); previous = index; }
                i = last + 1;
            }
            result.Add(new Point(section.Max, Evaluate(values, frequency, section.Max)));
            return result;
        }
        internal static bool Clip(ref double x1, ref double y1, ref double x2, ref double y2)
        {
            if (!Finite(x1) || !Finite(y1) || !Finite(x2) || !Finite(y2)) return false;
            double dx = x2 - x1, dy = y2 - y1, low = 0, high = 1;
            if (!Boundary(-dx, x1, ref low, ref high) || !Boundary(dx, 1 - x1, ref low, ref high) ||
                !Boundary(-dy, y1, ref low, ref high) || !Boundary(dy, 1 - y1, ref low, ref high)) return false;
            x2 = Math.Max(0, Math.Min(1, x1 + high * dx)); y2 = Math.Max(0, Math.Min(1, y1 + high * dy));
            x1 = Math.Max(0, Math.Min(1, x1 + low * dx)); y1 = Math.Max(0, Math.Min(1, y1 + low * dy));
            return true;
        }
        private static bool Boundary(double p, double q, ref double low, ref double high)
        {
            if (p == 0) return q >= 0;
            double r = q / p;
            if (p < 0) { if (r > high) return false; low = Math.Max(low, r); }
            else { if (r < low) return false; high = Math.Min(high, r); }
            return true;
        }
    }
}
