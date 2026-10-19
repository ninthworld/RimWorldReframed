using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ReframedUI;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    public static void Main(string[] args)
    {
        Check(PawnHealthLayout.SummaryWidth(536) + PawnHealthLayout.Gap < 536, "standard shell reserves a positive separate conditions column");
        Check(PawnHealthLayout.SummaryWidth(536) > 220 && 536 - PawnHealthLayout.SummaryWidth(536) - PawnHealthLayout.Gap > 290,
            "standard split leaves readable label/value summary and condition action lanes");
        Check(PawnHealthLayout.SummaryWidth(8) > 0, "constrained calculation never emits a negative summary width");
        Check(PawnHealthLayout.ConditionsHeight(2, 3) == 113, "group extent includes every group title, all rows and both separators");
        Check(PawnHealthLayout.ConditionsHeight(25, 40) > 1200, "long conditions reserve every entry for scrolling");
        Check(PawnHealthLayout.QueueHeight(15) > 297, "full native 15-operation queue overflows honestly instead of truncating");
        Check(PawnHealthLayout.FullyVisible(0, 21, 0, 21), "exact first-row fit is interactive");
        Check(!PawnHealthLayout.FullyVisible(0, 21, 1, 21), "partially clipped top row cannot activate treatment or operations");
        Check(!PawnHealthLayout.FullyVisible(280, 21, 0, 297), "partially clipped bottom row cannot activate");
        Check(PawnHealthLayout.FullyVisible(280, 21, 4, 297), "same bottom row activates only after wholly revealed");
        Check(PawnHealthLayout.Intersects(0, 600, 0, 297) && !PawnHealthLayout.FullyVisible(0, 600, 0, 297),
            "tall custom native bill keeps visible clipped controls accessible without requiring an impossible whole-row fit");
        Check(PawnHealthLayout.Intersects(0, 600, 400, 297), "lower portion of tall native bill remains accessible after scrolling");
        Check(!PawnHealthLayout.Intersects(0, 33, 33, 297) && !PawnHealthLayout.Intersects(297, 33, 0, 297),
            "native rows exactly outside the clip receive no input");
        Check(PawnHealthLayout.ClampScroll(900, 500, 297) == 203, "removing conditions clamps scroll to remaining content");
        Check(PawnHealthLayout.ClampScroll(20, 100, 297) == 0, "short list cannot stay scrolled into empty space");
        Check(PawnHealthLayout.ClampScroll(-10, 500, 297) == 0, "negative wheel/drag positions clamp safely");
        Check(PawnHealthLayout.SelfTendOutcome(true, true, 1) == 1, "disabled Doctor work rejects enabling self-tend");
        Check(PawnHealthLayout.SelfTendOutcome(true, false, 0) == 2, "unassigned Doctor work cautions while allowing self-tend");
        Check(PawnHealthLayout.SelfTendOutcome(false, true, 0) == 0, "turning self-tend off is always allowed");
        Check(PawnHealthLayout.SelfTendOutcome(true, false, 1) == 0, "eligible assigned doctor can self-tend");
        Check(PawnHealthLayout.CareVisible(true, false, true, true, true, false, true), "nonhuman guest in player bed keeps native medical care");
        Check(!PawnHealthLayout.CareVisible(true, true, false, false, true, false, true), "mutant medical-care entitlement remains authoritative");
        Check(!PawnHealthLayout.CareVisible(true, true, false, true, true, true, true), "dead pawn cannot change care");
        Check(!PawnHealthLayout.CareVisible(false, true, false, true, true, false, true), "non-flesh pawn does not acquire fabricated medicine controls");
        if (args.Length != 1) throw new ArgumentException("Supply freshly extracted installed health-native/native.json");
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(args[0]));
        var types = doc.RootElement.GetProperty("focused_types").EnumerateArray().ToArray();
        JsonElement Method(string type, string name) => types.Single(t => t.GetProperty("type").GetString() == type).GetProperty("methods").EnumerateArray()
            .Single(m => m.GetProperty("method").GetString() == name);
        string[] Calls(string type, string name) => Method(type, name).GetProperty("instructions").EnumerateArray()
            .Where(i => i.GetProperty("opcode").GetString() is "call" or "callvirt").Select(i => i.GetProperty("operand").GetString()).ToArray();
        var operations = Method("RimWorld.HealthCardUtility", "DrawMedOperationsTab");
        Check(operations.GetProperty("returnType").GetString() == "Single" && operations.GetProperty("parameterTypes").EnumerateArray().Select(p => p.GetString())
            .SequenceEqual(new[] { "UnityEngine.Rect", "Verse.Pawn", "Verse.Thing", "Single" }), "native operations entry signature remains exact");
        Check(Calls("RimWorld.HealthCardUtility", "DrawMedOperationsTab").Count(call => call == "RimWorld.BillStack.DoListing") == 1,
            "transpiler has exactly one guarded native queue seam");
        Check(Method("RimWorld.BillStack", "DoListing").GetProperty("parameterTypes").EnumerateArray().Select(p => p.GetString()).SequenceEqual(new[] {
            "UnityEngine.Rect", "System.Func`1<System.Collections.Generic.List`1<Verse.FloatMenuOption>>", "UnityEngine.Vector2&", "Single&" }), "queue adapter stack shape matches native ref arguments");
        string[] factory = Calls("RimWorld.HealthCardUtility+<>c__DisplayClass27_0", "<DrawMedOperationsTab>b__0");
        Check(factory.Contains("Verse.RecipeWorker.AvailableReport") && factory.Contains("Verse.RecipeDef.PotentiallyMissingIngredients") &&
            factory.Contains("Verse.RecipeWorker.GetPartsToApplyOn") && factory.Contains("Verse.RecipeDef.AvailableOnNow") &&
            factory.Contains("RimWorld.HealthCardUtility.GenerateSurgeryOption"), "native menu factory retains recipe, ingredient, body-part and callback semantics");
        string[] bill = Calls("RimWorld.Bill", "DoInterface");
        Check(bill.Contains("RimWorld.BillStack.Reorder") && bill.Contains("RimWorld.BillStack.Delete") && bill.Contains("RimWorld.Bill.DoConfigInterface"),
            "native operation controls remain the audited reorder/delete/config seams");
        Check(Calls("RimWorld.HealthCardUtility", "DrawOverviewTab").Contains("Verse.Pawn.WorkTypeIsDisabled") &&
            Calls("RimWorld.HealthCardUtility", "DrawOverviewTab").Contains("RimWorld.Pawn_WorkSettings.GetPriority"), "self-tend policy matches native Doctor rejection and caution");
        Check(Calls("RimWorld.HealthCardUtility", "EntryClicked").Contains("RimWorld.ITab_Pawn_Log.SeekTo"), "condition click remains native combat-log navigation");
        Check(Calls("RimWorld.HealthCardUtility+<>c", "<DrawHediffRow>b__32_1").Contains("Verse.Hediff.get_UIGroupKey"),
            "condition entries use native UI group identity rather than def-only merging");
        Check(Calls("Verse.PawnCapacityDef", "CanShowOnPawn").Contains("Verse.RaceProperties.get_IsDrone"), "capacity filtering includes installed drone category");
        Console.WriteLine("PASS " + checks + " Health layout, eligibility and freshly extracted installed native seam contracts. Live acceptance pending.");
    }
}
