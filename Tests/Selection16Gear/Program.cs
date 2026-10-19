using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ReframedUI;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
    private static bool Near(float a, float b) => Math.Abs(a - b) < .0001f;
    public static void Main(string[] args)
    {
        Check(!PawnGearLayout.DropVisible(false, true, true, true, false), "mental/downed/carried/native control gate blocks drop in every category");
        Check(PawnGearLayout.DropVisible(true, true, false, true, true), "native prisoner inventory can be dropped on home map");
        Check(!PawnGearLayout.DropVisible(true, false, false, true, true), "native prisoner worn gear stays protected on home map");
        Check(PawnGearLayout.DropVisible(true, false, false, true, false), "native worn gear permits control on a non-home map");
        Check(!PawnGearLayout.DropVisible(true, false, false, false, false), "unspawned non-colonist gear is not droppable");
        Check(PawnGearLayout.DropVisible(true, false, true, false, false), "native colonist control branch is retained");
        Check(PawnGearLayout.DropBlock(true, true, true) == 1, "locked apparel reason wins as in native tooltip order");
        Check(PawnGearLayout.DropBlock(false, true, true) == 2, "quest-lodger reason wins over destroy-gear policy");
        Check(PawnGearLayout.DropBlock(false, false, true) == 3, "destroy-gear policy remains disabled");
        Check(PawnGearLayout.DropBlock(false, false, false) == 0, "ordinary native permitted drop remains enabled");
        Check(Near(PawnGearLayout.StackMass(.03f, 5), .15f), "row mass includes stack quantity");
        Check(PawnGearLayout.StackMass(2.5f, 4) == 10f, "row mass matches native unit-mass times count");
        Check(PawnGearLayout.SectionHeight(0) == 42, "empty section includes compact visible None row");
        Check(PawnGearLayout.SectionHeight(2) == 73, "section extent includes headings, both rows and spacing");
        Check(PawnGearLayout.SectionHeight(0) * 2 + PawnGearLayout.SectionHeight(2) <= 189, "reference-like two-apparel list fits a300 logical shell body");
        Check(PawnGearLayout.SectionHeight(40) > 900, "long inventory reserves scroll content for every item");
        Check(PawnGearLayout.StatsHeight >= 19 + 3 * 22 + 9 + 21 + 29 + 25, "all stats fit the declared left scroll extent");
        PawnGearLayout.TemperatureScale(7, 30, out float low, out float high);
        Check(low == -50 && high == 100, "ordinary temperature uses stable inferred physical scale");
        Check(Near(PawnGearLayout.Position(7, low, high), .38f), "comfort interval uses actual Celsius endpoints");
        PawnGearLayout.TemperatureScale(-120, 240, out low, out high);
        Check(low == -120 && high == 240, "extreme comfort expands scale without clipping real endpoints");
        Check(PawnGearLayout.Position(-120, low, high) == 0 && PawnGearLayout.Position(240, low, high) == 1, "extreme interval fits exactly inside ramp");
        Check(PawnGearLayout.Position(0, 0, 0) == 0, "invalid temperature range cannot divide by zero");
        if (args.Length == 1) NativeEvidence(args[0]);
        Console.WriteLine("PASS " + checks + " Gear layout, native policy and optional installed IL contracts. No live UI acceptance claimed.");
    }
    private static void NativeEvidence(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement types = document.RootElement.GetProperty("focused_types");
        JsonElement gear = types.EnumerateArray().Single(type => type.GetProperty("type").GetString() == "RimWorld.ITab_Pawn_Gear");
        JsonElement row = gear.GetProperty("methods").EnumerateArray().Single(method => method.GetProperty("method").GetString() == "DrawThingRow");
        string[] operands = row.GetProperty("instructions").EnumerateArray().Select(instruction => instruction.GetProperty("operand").GetString()).ToArray();
        Check(operands.Contains("MechanitorUtility.TryConfirmBandwidthLossFromDroppingThing"), "installed drop requires Biotech bandwidth confirmation seam");
        Check(operands.Contains("RimWorld.EquipmentUtility.QuestLodgerCanUnequip") && operands.Contains("RimWorld.QuestUtility.IsQuestLodger"), "installed quest controls are still the audited predicates");
        Check(operands.Contains("RimWorld.Pawn_ApparelTracker.IsLocked"), "installed locked-apparel gate is retained");
        Check(operands.Contains("RimWorld.FoodUtility.WillIngestFromInventoryNow") && operands.Contains("RimWorld.FoodUtility.IngestFromInventoryNow"), "installed consume policy/action seams retained");
    }
}
