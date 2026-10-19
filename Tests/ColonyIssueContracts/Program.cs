using System;
using System.Reflection;
using ReframedUI;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Near(float actual, float expected, string message) => Check(Math.Abs(actual - expected) < .00002f, message);

    private class NativeLike
    {
        internal int Clicks;
        protected virtual void OnClick() { Clicks++; }
    }
    private sealed class CustomLike : NativeLike
    {
        protected override void OnClick() { Clicks += 10; }
    }

    private static void Main()
    {
        foreach (var expected in new[] {
            ("Alert_NeedColonistBeds", ColonyIssueGroup.Colonists, "bed"),
            ("Alert_NeedJoySources", ColonyIssueGroup.Colonists, "joy"),
            ("Alert_ColonistsIdle", ColonyIssueGroup.Colonists, "person"),
            ("Alert_LowMedicine", ColonyIssueGroup.Resources, "medicine"),
            ("Alert_LowFood", ColonyIssueGroup.Resources, "food"),
            ("Alert_Heatstroke", ColonyIssueGroup.Environment, "heat"),
            ("Alert_Hypothermia", ColonyIssueGroup.Environment, "cold"),
            ("AnUnknownModAlert", ColonyIssueGroup.Environment, "warning") })
        {
            var kind = ColonyIssuePolicy.Kind(expected.Item1);
            Check(kind.Group == expected.Item2 && kind.Icon == expected.Item3, expected.Item1);
        }
        Check(ColonyIssuePolicy.Kind(null).Icon == "warning", "Null names retain fallback row");
        Check(!ColonyIssuePolicy.SeparateAlert(0), "Medium remains in collapsible group");
        Check(!ColonyIssuePolicy.SeparateAlert(1), "High remains in collapsible group");
        Check(ColonyIssuePolicy.SeparateAlert(2), "Critical belongs in independent alert cards");
        for (int life = 0; life <= 20; life++)
        for (int reveal = 0; reveal <= 20; reveal++)
        {
            float l = life / 20f, r = reveal / 20f;
            Near(ColonyIssuePolicy.RowReveal(l, r, 0), l * r, "ordinary disclosure");
            Near(ColonyIssuePolicy.RowReveal(l, r, 2), l * r, "no priority overrides group collapse");
            float occupied = ColonyIssuePolicy.Occupied(900, 300, r);
            Check(occupied >= 0 && occupied <= 300, "bounded overflow viewport");
        }
        Near(ColonyIssuePolicy.RowReveal(float.NaN, 1, 2), 0, "invalid reveal is closed");
        Near(ColonyIssuePolicy.Occupied(100, 300, 1), 100, "short stack takes actual height");
        Near(ColonyIssuePolicy.Occupied(100, -1, 1), 0, "no available space");
        Near(ColonyIssuePolicy.Occupied(float.PositiveInfinity, 300, 1), 0, "invalid extent");
        Near(ColonyIssuePolicy.Occupied(100, float.NaN, 1), 0, "invalid maximum");
        Check(ColonyIssuePolicy.FullyVisible(20, 44, 20, 44), "exact row fits");
        Check(!ColonyIssuePolicy.FullyVisible(19, 43, 20, 44), "top clip disables input");
        Check(!ColonyIssuePolicy.FullyVisible(21, 45, 20, 44), "bottom clip disables input");

        // Empty sections reserve neither a heading nor a gap. A nonempty
        // collapsed section retains its heading independently of row disclosure.
        for (int mask = 0; mask < 8; mask++)
        {
            float extent = 0; int visibleGroups = 0;
            for (int group = 0; group < 3; group++)
            {
                float presence = (mask & (1 << group)) != 0 ? 1 : 0;
                if (presence == 0) continue;
                extent += ColonyIssuePolicy.GroupSpacing(visibleGroups > 0 ? 1 : 0, presence) + ColonyIssuePolicy.HeadingHeight(presence);
                visibleGroups++;
            }
            Near(extent, visibleGroups * 20 + Math.Max(0, visibleGroups - 1) * 4, "all sparse section combinations have no orphan gaps");
        }
        Near(ColonyIssuePolicy.HeadingHeight(0), 0, "empty header gone");
        Near(ColonyIssuePolicy.GroupSpacing(1, 0), 0, "empty section gap gone");
        Near(ColonyIssuePolicy.GroupSpacing(0, 1), 0, "first visible section has no leading gap");
        Near(ColonyIssuePolicy.GroupSpacing(.5f, 1), 2, "first section disappearing also contracts following gap");
        Near(ColonyIssuePolicy.GroupSpacing(1, .5f) + ColonyIssuePolicy.GroupSpacing(1, 1), 6, "middle section disappearance retains final section gap");
        Near(ColonyIssuePolicy.HeadingHeight(1) + ColonyIssuePolicy.RowReveal(1, 0, 0) * 26, 20, "collapsed ordinary section retains count heading");
        Near(ColonyIssuePolicy.HeadingHeight(1) + ColonyIssuePolicy.RowReveal(1, 0, 2) * 26, 20, "no uncollapsible rows remain in a group");
        var section = new ColonyIssueMotion(1);
        section.Set(false, 0); section.Tick(.11);
        Near(ColonyIssuePolicy.HeadingHeight(section.Value), 10, "last issue leaving contracts heading");
        Near(ColonyIssuePolicy.GroupSpacing(1, section.Value), 2, "last issue leaving contracts gap");
        section.Set(true, .11);
        Near(ColonyIssuePolicy.HeadingHeight(section.Value), 10, "different arriving issue reverses existing section without jumping");
        section.Tick(.22); Near(ColonyIssuePolicy.HeadingHeight(section.Value), 20, "replacement issue restores section");
        section.Set(false, .22); section.Tick(.44);
        Near(ColonyIssuePolicy.HeadingHeight(section.Value), 0, "final removal leaves no heading");
        Near(ColonyIssuePolicy.GroupSpacing(1, section.Value), 0, "final removal leaves no gap");

        var motion = new ColonyIssueMotion();
        motion.Set(true, 10); Near(motion.Value, 0, "open has no first-frame jump");
        motion.Tick(10.11); Near(motion.Value, .5f, "halfway open");
        float beforeReverse = motion.Value;
        motion.Set(false, 10.11); Near(motion.Value, beforeReverse, "reversal is continuous");
        motion.Tick(10.22); Near(motion.Value, 0, "reversal closes from current value");
        Check(motion.Settled && !motion.Open, "closed lifecycle");
        motion.Set(true, 10.22); motion.Tick(10.44); Near(motion.Value, 1, "full open");
        motion.Set(false, 10.44); motion.Tick(10.66); Near(motion.Value, 0, "full close");
        motion.Reset(); motion.Set(true, 0); motion.Tick(.055);
        float quarter = motion.Value;
        motion.Tick(.055); Near(motion.Value, quarter, "same timestamp cannot progress");
        motion.Tick(-10); Near(motion.Value, quarter, "backward clock cannot progress");
        motion.Tick(double.NaN); motion.Tick(double.PositiveInfinity);
        Near(motion.Value, quarter, "invalid clock ignored");
        motion.Tick(.11); Near(motion.Value, .5f, "backward clock does not add time");
        motion.Tick(100); Near(motion.Value, 1, "large frame cannot overshoot");
        foreach (int frames in new[] { 1, 2, 4, 11, 22, 60, 144 })
        {
            motion.Reset(); motion.Set(true, 0);
            for (int i = 1; i <= frames; i++) motion.Tick(.22 * i / frames);
            Near(motion.Value, 1, "frame-independent opening");
            motion.Set(false, .22);
            for (int i = 1; i <= frames; i++) motion.Tick(.22 + .22 * i / frames);
            Near(motion.Value, 0, "frame-independent closing");
        }

        var upper = new ColonyIssuePositionMotion(); var lower = new ColonyIssuePositionMotion();
        upper.Follow(20, 0, false); lower.Follow(46, 0, false);
        upper.Follow(46, 1, true); lower.Follow(20, 1, true);
        Near(upper.Value, 20, "priority inversion starts at existing rendered upper row");
        Near(lower.Value, 46, "priority inversion starts at existing rendered lower row");
        Check(!upper.Settled && !lower.Settled, "reordering rows cannot regain input immediately");
        upper.Follow(46, 1.09, false); lower.Follow(20, 1.09, false);
        Near(upper.Value, 33, "upper row crosses smoothly"); Near(lower.Value, 33, "lower row crosses smoothly");
        upper.Follow(20, 1.09, true); lower.Follow(46, 1.09, true);
        Near(upper.Value, 33, "second order change retains current upper paint position");
        Near(lower.Value, 33, "second order change retains current lower paint position");
        upper.Follow(20, 1.27, false); lower.Follow(46, 1.27, false);
        Near(upper.Value, 20, "reversed row reaches exact new slot"); Near(lower.Value, 46, "reversed partner reaches exact new slot");
        Check(upper.Settled && lower.Settled, "reorder input returns only after exact settlement");
        upper.Follow(21, 1.27, false); Near(upper.Value, 21, "normal group disclosure follows continuous layout without lag");
        upper.Follow(47, 2, true); upper.Follow(47, 2.045, false);
        float reorderedQuarter = upper.Value;
        upper.Follow(47, 2.045, false); Near(upper.Value, reorderedQuarter, "reorder repeated timestamp does not advance");
        upper.Follow(47, 1, false); Near(upper.Value, reorderedQuarter, "reorder clock regression ignored");
        upper.Follow(float.NaN, 2.09, false); upper.Follow(47, double.NaN, false);
        Near(upper.Value, reorderedQuarter, "invalid position samples ignored");
        upper.Follow(47, 2.18, false); Near(upper.Value, 47, "reorder completes without overshoot");
        Check(upper.Settled, "reorder survives invalid clock samples");
        foreach (int frames in new[] { 1, 2, 18, 60, 144 })
        {
            var position = new ColonyIssuePositionMotion(); position.Follow(20, 0, false); position.Follow(72, 0, true);
            for (int i = 1; i <= frames; i++) position.Follow(72, .18 * i / frames, false);
            Near(position.Value, 72, "position tween frame independence");
            Check(position.Settled, "frame-independent position settlement gates input");
        }

        MethodInfo clickMethod = typeof(NativeLike).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance);
        var click = (Action<NativeLike>)Delegate.CreateDelegate(typeof(Action<NativeLike>), clickMethod);
        var ordinary = new NativeLike(); var custom = new CustomLike();
        click(ordinary); click(custom);
        Check(ordinary.Clicks == 1 && custom.Clicks == 10, "open protected virtual delegate preserves override dispatch");
        Console.WriteLine($"PASS {checks} policy/motion/callback contracts; no game objects constructed or actions invoked.");
    }
}
