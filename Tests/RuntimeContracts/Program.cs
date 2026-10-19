using System;
using ReframedUI;

internal static class Program
{
    private static int checks;

    private static void Check(bool condition, string name)
    {
        checks++;
        if (!condition) throw new Exception("FAIL " + name);
    }

    public static void Main()
    {
        // Ordinary contract fixtures, not invented RimWorld APIs or runtime integration stubs.
        Type fixture = typeof(SignatureFixture);
        Check(RuntimeMemberContract.MethodMatches(fixture, "Paint", typeof(void), false,
            new[] { typeof(float), typeof(bool) }, new[] { "topY", "minimized" }), "exact private method");
        Check(!RuntimeMemberContract.MethodMatches(fixture, "Paint", typeof(void), false,
            new[] { typeof(float) }), "removed/added parameter rejected");
        Check(!RuntimeMemberContract.MethodMatches(fixture, "Paint", typeof(void), true,
            new[] { typeof(float), typeof(bool) }), "static drift rejected");
        Check(!RuntimeMemberContract.MethodMatches(fixture, "Paint", typeof(int), false,
            new[] { typeof(float), typeof(bool) }), "return drift rejected");
        Check(!RuntimeMemberContract.MethodMatches(fixture, "Paint", typeof(void), false,
            new[] { typeof(float), typeof(bool) }, new[] { "y", "minimized" }), "Harmony-bound parameter rename rejected");
        Check(RuntimeMemberContract.MethodMatches(fixture, "Position", typeof(void), true,
            new[] { typeof(float).MakeByRefType() }, new[] { "curBaseY" }), "by-reference exact match");
        Check(!RuntimeMemberContract.MethodMatches(fixture, "Position", typeof(void), true,
            new[] { typeof(float) }), "by-value drift rejected");
        Check(!RuntimeMemberContract.MethodMatches(typeof(DerivedFixture), "Paint", typeof(void), false,
            new[] { typeof(float), typeof(bool) }), "inherited member does not hide target relocation");
        Check(RuntimeMemberContract.FieldMatches(fixture, "height", typeof(float), false), "private injected field exact match");
        Check(!RuntimeMemberContract.FieldMatches(fixture, "height", typeof(double), false), "injected field type drift rejected");
        Check(!RuntimeMemberContract.FieldMatches(fixture, "height", typeof(float), true), "injected field static drift rejected");
        Check(!RuntimeMemberContract.FieldMatches(fixture, "missing", typeof(float), false), "missing field rejected");
        Check(!RuntimeMemberContract.MethodMatches(fixture, "Missing", typeof(void), false, Type.EmptyTypes), "missing method rejected");

        Check(RuntimeNotificationContract.Family("LetterDef", "ThreatBig", "ludeon.rimworld") == "major-threat", "native threat family");
        Check(RuntimeNotificationContract.Family("LetterDef", "NewQuest_ThreatBig", "ludeon.rimworld") == "major-threat", "quest does not downgrade native threat");
        Check(RuntimeNotificationContract.Family("LetterDef", "NewQuest", "ludeon.rimworld") == null, "ordinary quest identity does not invent severity");
        Check(RuntimeNotificationContract.Family("LetterDef", "Bzzzt", "ludeon.rimworld") == null, "English event title is not classification");
        Check(RuntimeNotificationContract.Family("LetterDef", "ThreatBig", "custom.mod") == null, "custom source is not silently classified as core");
        Check(RuntimeNotificationContract.Family("LetterDef", "CustomCritical", "custom.mod") == null, "unknown identity remains unclassified");
        Check(RuntimeNotificationContract.Family("LetterDef", "ThreatBig", null) == null, "missing origin remains unclassified");
        Check(RuntimeNotificationContract.Family("IncidentDef", "ThreatBig", "ludeon.rimworld") == null, "incident identity is not a letter relationship");
        Check(RuntimeNotificationContract.Family("MessageTypeDef", "TaskCompletion", "ludeon.rimworld") == "completion", "message completion family");
        Check(RuntimeNotificationContract.Family("MessageTypeDef", "SilentInput", "ludeon.rimworld") == "input-silent", "transient channel distinctions retained");
        Check(RuntimeNotificationContract.MappingStatus(null) == "native-unclassified", "unknown is visibly unclassified");
        Check(RuntimeNotificationContract.MappingStatus("major-threat") == "channel-family-fallback", "family is not counted as bespoke");
        Console.WriteLine("PASS " + checks + " pure runtime member and notification contracts. No game/Harmony integration or live UI acceptance claimed.");
    }

    private class SignatureFixture
    {
#pragma warning disable CS0169
        private float height;
#pragma warning restore CS0169
        private void Paint(float topY, bool minimized) { }
        private static void Position(ref float curBaseY) { }
    }
    private sealed class DerivedFixture : SignatureFixture { }
}
