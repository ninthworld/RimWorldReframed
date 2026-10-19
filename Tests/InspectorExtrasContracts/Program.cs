using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ReframedUI;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string description) { checks++; if (!condition) throw new Exception(description); }
    public static void Main(string[] args)
    {
        Check(!PawnInspectorExtrasPolicy.HealthDeveloper(false, true), "normal player never gains native Health dev actions");
        Check(!PawnInspectorExtrasPolicy.HealthDeveloper(true, false), "Health dev tools do not run outside native Playing context");
        Check(PawnInspectorExtrasPolicy.HealthDeveloper(true, true), "native developer in Playing retains global Health tools");
        Check(!PawnInspectorExtrasPolicy.ConditionDeveloper(false, true), "per-condition actions respect actual ShowDevGizmos, not generic DevMode alone");
        Check(!PawnInspectorExtrasPolicy.ConditionDeveloper(true, false), "per-condition mutations are not exposed in creation menus");
        Check(PawnInspectorExtrasPolicy.ConditionDeveloper(true, true), "native per-condition debug context is retained");
        Check(!PawnInspectorExtrasPolicy.SeverityAvailable(float.MaxValue, 0), "unbounded nonlethal hediff has no native severity button");
        Check(PawnInspectorExtrasPolicy.SeverityAvailable(1, 0), "bounded severity retains native dialog");
        Check(PawnInspectorExtrasPolicy.SeverityAvailable(float.MaxValue, 1), "positive lethal severity retains native dialog");
        Check(!PawnInspectorExtrasPolicy.SeverityAvailable(float.PositiveInfinity, -1), "invalid/unbounded nonlethal severity does not invent a dialog");
        Check(PawnInspectorExtrasPolicy.FullyVisible(0, 43, 0, 43), "exactly visible pregnancy row remains interactive");
        Check(!PawnInspectorExtrasPolicy.FullyVisible(0, 43, 1, 43), "top-clipped native pregnancy control receives no input");
        Check(!PawnInspectorExtrasPolicy.FullyVisible(258, 43, 0, 284), "bottom-clipped pregnancy row receives no input");
        Check(PawnInspectorExtrasPolicy.FullyVisible(258, 43, 17, 284), "scrolling a whole row into view restores native control");
        Check(!PawnInspectorExtrasPolicy.FullyVisible(0, 0, 0, 43), "empty hit region cannot activate");
        if (args.Length != 1) throw new ArgumentException("Supply freshly extracted extras-native/native-generics.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(args[0]));
        var types = document.RootElement.GetProperty("focused_types").EnumerateArray().ToArray();
        JsonElement Type(string type) => types.Single(t => t.GetProperty("type").GetString() == type);
        JsonElement Method(string type, string name) => Type(type).GetProperty("methods").EnumerateArray().Single(m => m.GetProperty("method").GetString() == name);
        string[] Calls(string type, string name) => Method(type, name).GetProperty("instructions").EnumerateArray()
            .Where(i => i.GetProperty("opcode").GetString() is "call" or "callvirt").Select(i => i.GetProperty("operand").GetString()).ToArray();
        string[] Operands(string type, string name) => Method(type, name).GetProperty("instructions").EnumerateArray().Select(i => i.GetProperty("operand").GetString()).ToArray();
        const string health = "RimWorld.HealthCardUtility", social = "RimWorld.SocialCardUtility";
        Check(Method(health, "DoDebugOptions").GetProperty("parameterTypes").EnumerateArray().Select(p => p.GetString()).SequenceEqual(new[] { "UnityEngine.Rect", "Verse.Pawn" }),
            "native Health developer entry signature is exact");
        Check(Calls(health, "DoDebugOptions").Count(call => call == "Verse.Widgets.ButtonText") == 1, "Health developer factory has exactly one button adapter seam");
        string[] dev = Operands(health, "DoDebugOptions");
        Check(new[] { "Add hediff", "Hediff debug tooltips", "Show hidden Hediffs", "Table: BodyPartRecord info", "Table: BodyPartGroupDef info", "Table: HediffGiver_Birthday", "Table: Immunities" }.All(dev.Contains),
            "delegated native global developer menu retains complete actual options");
        Check(Calls(social, "DrawPregnancyApproach").Contains("RimWorld.PregnancyUtility.CanEverProduceChild") &&
            Calls(social, "DrawPregnancyApproach").Contains("RimWorld.Planet.WorldPawnsUtility.IsWorldPawn"), "native pregnancy control owns fertility and world-away acceptance");
        Check(Calls(social, "DrawPregnancyApproach").Contains("RimWorld.Difficulty.get_ChildrenAllowed") &&
            Operands(social, "DrawPregnancyApproach").Contains("PregnancyNotPossible"), "native pregnancy settings retain difficulty and rejected-click explanation");
        Check(Calls("RimWorld.SocialCardUtility+<>c__DisplayClass59_1", "<DrawPregnancyApproach>b__0").Contains("RimWorld.Pawn_RelationsTracker.SetPregnancyApproach"),
            "native pregnancy option callback remains actual tracker mutation");
        string[] cache = Operands(social, "RecacheEntryInt");
        Check(cache.Contains("RimWorld.LovePartnerRelationUtility.IsLovePartnerRelation") && cache.Contains("RimWorld.MutantDef.incapableOfSocialInteractions") &&
            cache.Contains("Verse.ModsConfig.get_BiotechActive"), "native cache owns partner/mutant/DLC eligibility rather than independent inference");
        Check(Type(social + "+CachedSocialTabEntry").GetProperty("fields").EnumerateArray().Any(f => f.GetProperty("name").GetString() == "pregnancyApproach" &&
            f.GetProperty("type").GetString() == "System.Nullable`1<RimWorld.PregnancyApproach>"), "nullable native approach remains the entry eligibility marker");
        string[] certainty = Calls(social, "DrawPawnCertainty");
        Check(new[] { "RimWorld.Ideo.DrawIcon", "Verse.Widgets.Label", "Verse.Widgets.DrawHighlight", "Verse.Widgets.FillableBar" }.All(call => certainty.Count(c => c == call) == 1),
            "compact certainty has four exact single-call paint substitutions");
        Check(certainty.Contains("RimWorld.Pawn_IdeoTracker.get_CertaintyChangePerDay") && certainty.Contains("RimWorld.ConversionUtility.GetCertaintyReductionFactorsDescription") &&
            certainty.Contains("RimWorld.Trait.MultiplierOfStat") && certainty.Contains("RimWorld.IdeoUIUtility.OpenIdeoInfo"),
            "native certainty component preserves detailed daily/factor breakdown and actual info link");
        string[] row = Operands(health, "DrawHediffRow");
        Check(row.Contains("Verse.HediffUtility.TryGetComp<Verse.HediffComp_Disappears>"), "remaining-time dialog gate uses actual native Disappears comp");
        Check(Calls("RimWorld.HealthCardUtility+<>c__DisplayClass32_2", "<DrawHediffRow>b__5").Contains("Verse.Pawn_HealthTracker.RemoveHediff") &&
            Operands("RimWorld.HealthCardUtility+<>c__DisplayClass32_2", "<DrawHediffRow>b__7").Contains("Verse.Dialog_DebugSetSeverity..ctor") &&
            Operands("RimWorld.HealthCardUtility+<>c__DisplayClass32_2", "<DrawHediffRow>b__9").Contains("Verse.Dialog_DebugSetHediffRemaining..ctor"),
            "condition tools reuse exact native remove/severity/remaining actions");
        Console.WriteLine("PASS " + checks + " inspector extras policy and installed native delegation contracts. No live acceptance claimed.");
    }
}
