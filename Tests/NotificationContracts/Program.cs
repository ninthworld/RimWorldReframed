using System;
using System.Collections.Generic;
using System.Linq;
using ReframedUI;

internal static class Program
{
    private sealed class Event
    {
        internal readonly string Label;
        internal Event(string label) { Label = label; }
        public override bool Equals(object other) => other is Event e && e.Label == Label;
        public override int GetHashCode() => Label.GetHashCode();
    }
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static bool Near(double a, double b) => Math.Abs(a - b) < .00001;
    public static void Main()
    {
        string[] discovered = { "BabyBirth", "BabyToChild", "Bossgroup", "HumanPregnancy", "ChildBirthday", "ChildToAdult", "RansomDemand", "ChoosePawn", "AcceptVisitors", "AcceptJoiner", "BetrayVisitors", "GameEnded", "Death", "NewQuest", "NewQuest_ThreatBig", "RitualOutcomePositive", "RitualOutcomeNegative", "BundleLetter", "ThreatBig", "ThreatSmall", "NegativeEvent", "NeutralEvent", "PositiveEvent", "RelicHuntInstallationFound" };
        foreach (string definition in discovered) Check(NotificationPolicy.Presentation(definition, false).Icon != null, "static discovered LetterDef has explicit justified family: " + definition);
        Check(NotificationPolicy.Presentation("NewQuest", false).Icon == "quest", "actual core quest definition maps even without live quest reference");
        Check(NotificationPolicy.Presentation("NewQuest_ThreatBig", true).Tint == 0xE45F66 && NotificationPolicy.Presentation("ThreatSmall", true).Tint == 0xEBA45A, "quest context never softens known threat urgency");
        Check(NotificationPolicy.Presentation("UnknownMod", true).Tint == null && NotificationPolicy.Presentation("UnknownMod", false).Icon == null, "unknown mod preserves actual native icon/color unless genuine quest context supplies icon");
        Check(NotificationPolicy.Presentation("NeutralEvent", false).Icon == "info", "generic discovered project letter never pretends to be research completion");
        Check(NotificationPolicy.Presentation("NegativeEvent", false).Icon == "warning", "generic adverse event never pretends to be electrical without actual incident context");
        Check(NotificationPolicy.IncidentIcon("ShortCircuit") == "power", "only exact electrical context supplies reference power icon");
        Check(NotificationPolicy.IncidentIcon("HeatWave") == "heat" && NotificationPolicy.IncidentIcon("ColdSnap") == "cold", "typed weather context distinguishes temperatures");
        Check(NotificationPolicy.IncidentIcon("RaidEnemy") == "danger" && NotificationPolicy.IncidentIcon("Disease_Plague") == "health", "typed hostile and disease incident families remain distinct");
        Check(NotificationPolicy.IncidentIcon("UnknownMod") == null, "unknown incidents preserve definition/native fallback");
        Check(NotificationPolicy.Capacity(29) == 0 && NotificationPolicy.Capacity(30) == 1, "too little space requires native fallback, never an inaccessible clipped row");
        Check(NotificationPolicy.Capacity(64) == 1 && NotificationPolicy.Capacity(65) == 2, "capacity includes row spacing exactly once");
        Check(!NotificationPolicy.Supported(float.NaN, 80) && !NotificationPolicy.Supported(220, float.PositiveInfinity), "nonfinite layout rejects safely");
        Check(NotificationPolicy.Capacity(float.MaxValue) > 0, "finite extreme capacity cannot overflow negative");
        foreach (int count in new[] { 0, 1, 2, 3, 20, 1000 })
            foreach (int capacity in new[] { 1, 2, 3, 20 })
            {
                int bundle = NotificationPolicy.Bundled(count, capacity);
                Check(bundle >= 0 && bundle <= count && count - bundle + (bundle > 0 ? 1 : 0) <= capacity,
                    "every native entry has an explicit visible or bundled route");
            }
        Check(NotificationPolicy.Bundled(20, 3) == 18, "overflow retains newest two and bundles oldest eighteen without reordering");
        Check(NotificationPolicy.AgeTicks(100, 120) == 0, "future or loaded timestamp never becomes negative");
        Check(NotificationPolicy.AgeTicks(int.MaxValue, int.MinValue) == int.MaxValue, "timestamp delta cannot overflow");
        Check(NotificationPolicy.Step(4, 4) == 0, "repeated OnGUI events at same real time do not advance motion");
        Check(NotificationPolicy.Step(3, 4) == 0 && NotificationPolicy.Step(double.NaN, 4) == 0, "clock regression and invalid sample do not run animation backward");
        var first = new Event("Same title"); var second = new Event("Same title");
        var burst = new NotificationMotion<Event>();
        Check(burst.Prepare(new[] { first, second }, 0) == 0, "simultaneous arrival starts with no occupied-space jump");
        Check(burst.Prepare(new[] { first, second }, .001) < 1, "simultaneous row spacing grows continuously with the reveal");
        Check(Near(burst.Prepare(new[] { first, second }, .251), 65), "settled burst occupies exact bounded stack geometry");
        var data = new Dictionary<Event, int>(NotificationIdentityComparer<Event>.Instance) { [first] = 1, [second] = 2 };
        Check(data.Count == 2, "even custom equal-label/equal-ID equality cannot merge event presentation");
        var motion = new NotificationMotion<Event>();
        Check(motion.Prepare(new[] { first }, 0) == 0 && !motion.Settled, "new arrival starts collapsed and inert");
        double halfway = motion.Prepare(new[] { first }, .11);
        Check(Near(halfway, 15) && !motion.Settled, "entry expands through real intermediate geometry");
        Check(Near(motion.Prepare(new[] { first }, .11), halfway), "paused rendering clock preserves exact geometry");
        motion.Prepare(new[] { first }, .22);
        Check(motion.Settled && Near(motion.Nodes[0].Progress, 1), "settled arrival can regain input");
        motion.Prepare(new[] { first, second }, .23);
        Check(motion.Nodes.Count == 2 && !motion.Settled, "simultaneous duplicate labels retain independent identities and block reflow input");
        motion.Prepare(new[] { first, second }, .48);
        Check(motion.Settled && motion.Nodes.Single(n => ReferenceEquals(n.Key, first)).Bottom == 35 && motion.Nodes.Single(n => ReferenceEquals(n.Key, second)).Bottom == 0,
            "native oldest-to-newest vertical order remains deterministic");
        motion.Prepare(new[] { second }, .54);
        var exiting = motion.Nodes.Single(n => ReferenceEquals(n.Key, first)); double beforeReverse = exiting.Progress;
        Check(!exiting.Live && !motion.Settled && beforeReverse > 0, "native removal retains only an inert shrinking visual");
        motion.Prepare(new[] { first, second }, .54);
        Check(exiting.Live && Near(exiting.Progress, beforeReverse), "reversal reuses same object and geometry without snapping");
        motion.Prepare(new[] { first, second }, .8);
        motion.Prepare(Array.Empty<Event>(), .85);
        Check(!motion.Settled && motion.Nodes.All(n => !n.Live), "exit keeps no input-owning node");
        motion.Prepare(Array.Empty<Event>(), 1.1);
        Check(motion.Nodes.Count == 0 && motion.Settled, "completed exits release all retained event references");
        motion.Prepare(new[] { first }, 2); motion.Prepare(new[] { first }, 1); motion.Prepare(new[] { first }, 2);
        Check(motion.Nodes[0].Progress == 1, "clock regression does not credit time twice");
        motion.Reset(); Check(motion.Nodes.Count == 0, "context reset removes old game objects");
        Check(NotificationPolicy.CanActivate(true, true, true, true, true, true, 0), "stable live left MouseDown may open native letter");
        Check(NotificationPolicy.CanActivate(true, true, true, true, true, true, 1), "stable live right MouseDown may request native dismissal");
        for (int gate = 0; gate < 6; gate++)
        {
            bool[] gates = { true, true, true, true, true, true }; gates[gate] = false;
            Check(!NotificationPolicy.CanActivate(gates[0], gates[1], gates[2], gates[3], gates[4], gates[5], 0), "each independent input/lifecycle guard rejects action: " + gate);
        }
        Check(!NotificationPolicy.CanActivate(true, true, true, true, true, true, 2), "middle mouse does not invent native actions");
        Console.WriteLine(checks + " notification lifecycle, overflow, identity and input contracts passed.");
    }
}
