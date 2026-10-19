using System;
using System.Collections.Generic;
using System.Linq;
using ReframedUI;

int checks = 0;
void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
int Sort(int current, float? skill, int passion = 0, bool disabled = false, bool incapable = false)
    => WorkPanelPolicy.AutoPriority(current, skill, passion, disabled, incapable);

for (int passion = 0; passion <= 2; passion++)
for (int priority = 1; priority <= 4; priority++)
{
    Check(Sort(0, 20, passion) == 0, "Never enable a disabled job");
    Check(Sort(priority, null, passion) == priority, "Preserve no-skill care/emergency/support priorities");
    Check(Sort(priority, 20, passion, disabled: true) == priority, "Never alter native disabled work");
    Check(Sort(priority, 20, passion, incapable: true) == priority, "Preserve temporary total incapacity");
    Check(Sort(priority, float.NaN, passion) == priority, "Invalid skill cannot invent a priority");
    for (int skill = 0; skill <= 20; skill++)
    {
        int score = skill + 2 * passion;
        int expected = score >= 14 ? 1 : score >= 9 ? 2 : score >= 4 ? 3 : 4;
        Check(Sort(priority, skill, passion) == expected, "Exact threshold/passion weighting across native skill range");
        Check(Sort(Sort(priority, skill, passion), skill, passion) == expected, "Autosort is idempotent");
    }
}
Check(Sort(2, 13.999f) == 2 && Sort(2, 14) == 1, "Fractional mean boundary at14");
Check(Sort(2, 8.999f) == 3 && Sort(2, 9) == 2, "Fractional mean boundary at9");
Check(Sort(2, 3.999f) == 4 && Sort(2, 4) == 3, "Fractional mean boundary at4");
Check(Sort(2, 0, 2) == 3, "Major passion at zero skill does not become highest priority");
Check(Sort(2, 10, 2) == 1, "Major passion contributes exactly four points");
Check(Sort(2, 12, 1) == 1, "Minor passion contributes exactly two points");
Check(Sort(-1, 20) == -1 && Sort(5, 20) == 5, "Unknown mod priority domain is left alone");
Check(Sort(2, float.PositiveInfinity) == 2, "Nonfinite mod skill is left alone");

var jobs = new[] {
    (id: "firstTie", priority: 2, natural: 110), (id: "secondTie", priority: 2, natural: 110),
    (id: "normalHigh", priority: 1, natural: 0), (id: "modNatural", priority: 2, natural: 200000)
};
var ordered = jobs.OrderByDescending(j => WorkPanelPolicy.NativeOrderKey(j.natural, j.priority)).ToArray();
Check(ordered[0].id == "modNatural", "Exact native combined key preserves unusual mod naturalPriority");
Check(ordered[2].id == "firstTie" && ordered[3].id == "secondTie", "Stable native ties preserve original work order");
Check(WorkPanelPolicy.NativeOrderKey(110, 1) - WorkPanelPolicy.NativeOrderKey(110, 2) == 100000,
    "Each manual priority steps native scheduling key by100000");
var memory = new WorkPanelPriorityMemory<object, string>();
var pawnA = new object(); var pawnB = new object();
for (int p = 1; p <= 4; p++)
{
    memory.Remember(pawnA, "Construct", p);
    Check(WorkPanelPolicy.TogglePriority(p, memory.Recall(pawnA, "Construct")) == 0,
        "One click disables every native enabled priority, without cycling");
    Check(WorkPanelPolicy.TogglePriority(0, memory.Recall(pawnA, "Construct")) == p,
        "One subsequent click restores exact priority1..4");
    Check(WorkPanelPolicy.CanSetEnabledPriority(p, 0, true), "Dropdown Off accepts every enabled priority");
    Check(WorkPanelPolicy.CanSetEnabledPriority(p, 0, false), "Dropdown Off remains available in checkbox mode");
    for (int requested = 1; requested <= 4; requested++)
    {
        Check(WorkPanelPolicy.CanSetEnabledPriority(p, requested, true), "Explicit priority uses native1..4 domain");
        Check(!WorkPanelPolicy.CanSetEnabledPriority(0, requested, true), "Priority dropdown never enables an Off job");
        Check(!WorkPanelPolicy.CanSetEnabledPriority(p, requested, false), "Numbered priorities require manual mode");
    }
}
memory.Remember(pawnA, "Doctor", 2);
memory.Remember(pawnB, "Construct", 1);
Check(memory.Recall(pawnA, "Construct") == 4 && memory.Recall(pawnA, "Doctor") == 2,
    "Work priority memory is isolated between jobs");
Check(memory.Recall(pawnB, "Construct") == 1, "Work priority memory is isolated between pawns/settings");
memory.Remember(pawnA, "Construct", 0);
memory.Remember(pawnA, "Construct", 5);
Check(memory.Recall(pawnA, "Construct") == 4, "Off or unknown priority cannot overwrite remembered enabled priority");
memory.Clear();
Check(memory.Recall(pawnA, "Construct") == 3 && memory.Recall(pawnB, "Construct") == 3,
    "Full lifecycle reset clears session memory and restores ordinary default3");
Check(WorkPanelPolicy.TogglePriority(-1, 2) == -1 && WorkPanelPolicy.TogglePriority(5, 2) == 5,
    "Unknown mod priority domains are not clamped by toggle");
Check(!WorkPanelPolicy.CanSetEnabledPriority(3, -1, true) && !WorkPanelPolicy.CanSetEnabledPriority(3, 5, true),
    "Explicit priority refuses out-of-domain requests");
Check(WorkPanelAuditPolicy.CanAppend(0, 1), "Audit accepts a first nonempty record");
Check(WorkPanelAuditPolicy.CanAppend(WorkPanelAuditPolicy.MaxFileBytes - 1, 1), "Audit accepts exact file boundary");
Check(!WorkPanelAuditPolicy.CanAppend(WorkPanelAuditPolicy.MaxFileBytes, 1), "Audit never exceeds file bound");
Check(!WorkPanelAuditPolicy.CanAppend(0, WorkPanelAuditPolicy.MaxRecordBytes + 1), "Oversized snapshots are refused intact");
Check(!WorkPanelAuditPolicy.CanAppend(0, long.MaxValue), "Audit bound handles oversized inputs without overflow");
Check(!WorkPanelAuditPolicy.CanAppend(-1, 1) && !WorkPanelAuditPolicy.CanAppend(0, 0), "Invalid audit byte lengths rejected");
var expectedCycle = new[] { 2, 1, 4, 3, 2 };
for (int i = 0; i < expectedCycle.Length - 1; i++)
    Check(WorkPanelPolicy.NextPriority(expectedCycle[i]) == expectedCycle[i + 1], "Exact displayed cycle2→1→4→3→2");
for (int start = 1; start <= 4; start++)
{
    int value = start;
    var visited = new HashSet<int>();
    for (int step = 0; step < 4; step++)
    {
        Check(WorkPanelPolicy.CanCycleEnabledPriority(value, true, true), "Every native enabled priority is cycleable while On");
        Check(!WorkPanelPolicy.CanCycleEnabledPriority(value, true, false), "Off keeps saved numbers read-only and never activates manual mode");
        Check(!WorkPanelPolicy.CanCycleEnabledPriority(value, false, true), "Permanent/age-disabled work cannot edit numbers");
        visited.Add(value); value = WorkPanelPolicy.NextPriority(value);
        Check(value >= 1 && value <= 4, "Cycle never selects Off0 or an invalid native priority");
    }
    Check(value == start && visited.Count == 4, "Four clicks visit every priority and return to initial value");
}
foreach (int value in new[] { int.MinValue, -1, 0, 5, int.MaxValue })
{
    Check(!WorkPanelPolicy.CanCycleEnabledPriority(value, true, true), "Off/invalid assignment cannot cycle");
    Check(WorkPanelPolicy.NextPriority(value) == value, "Invalid/Off domain is preserved without fabricated assignment");
}
Check(!WorkPanelPolicy.CanCycleEnabledPriority(3, true, false), "Even native effective3 cannot be cycled while Priorities is Off");
for (int p = 1; p <= 4; p++)
{
    memory.Remember(pawnA, "Construct", p);
    bool mode = false;
    int raw = p;
    int effective = mode || raw == 0 ? raw : 3;
    Check(effective == 3 && memory.Recall(pawnA, "Construct") == p, "Masked native effective3 does not replace saved1/2/4 in UI memory");
    raw = WorkPanelPolicy.TogglePriority(raw, memory.Recall(pawnA, "Construct"));
    Check(raw == 0 && !mode, "Turning a job off while priorities are Off leaves global mode unchanged");
    mode = true; mode = false; // Mode notifications expire Undo, not this saved-number memory.
    raw = WorkPanelPolicy.TogglePriority(raw, memory.Recall(pawnA, "Construct"));
    Check(raw == p && !mode, "Off→On restores exact number even after priority-mode changes");
}
Check(WorkPanelPolicy.TogglePriority(0, memory.Recall(new object(), "NeverAssigned")) == 3,
    "A job without remembered assignment keeps ordinary default3");
int Assign(string name, int current, float? skill, int passion = 0, bool disabled = false, bool incapable = false)
    => WorkPanelPolicy.AssignPriority(name, current, skill, passion, disabled, incapable);
for (int current = 0; current <= 4; current++)
{
    foreach (string name in new[] { "Firefighter", "Patient", "PatientBedRest", "BasicWorker", "Hauling", "Cleaning", "Construction", "ModdedJob" })
    {
        Check(Assign(name, current, 20, 2, disabled: true) == current, "Auto assign preserves disabled/age-locked work");
        Check(Assign(name, current, 20, 2, incapable: true) == current, "Auto assign skips temporarily incapable work");
    }
    foreach (string essential in new[] { "Firefighter", "Patient", "PatientBedRest" })
        Check(Assign(essential, current, null) == 1, "Essential care and emergency jobs are enabled at highest priority");
    Check(Assign("BasicWorker", current, null) == 2, "Basic work is enabled at high priority");
    Check(Assign("Hauling", current, null) == 4 && Assign("Cleaning", current, null) == 4, "Support jobs stay enabled at low priority");
    Check(Assign("ModdedJob", current, null) == current, "Unknown no-skill work is preserved");
    foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        Check(Assign("Construction", current, invalid) == current, "Nonfinite skill does not change assignments");
    for (int passion = 0; passion <= 2; passion++)
    for (int skill = 0; skill <= 20; skill++)
    {
        int value = Assign("Construction", current, skill, passion);
        Check((value > 0) == (skill + 2 * passion >= 4 || passion > 0), "Auto assign chooses enabled work independently of its previous assignment");
        Check(value == Assign("Construction", value, skill, passion), "Auto assign is idempotent");
        if (value > 0) Check(value == Sort(3, skill, passion), "Enabled automatic work uses the declared skill/passion priority thresholds");
    }
    Check(WorkPanelPolicy.ClearPriority(current, false) == 0, "Clear turns every normal enabled priority off");
    Check(WorkPanelPolicy.ClearPriority(current, true) == current, "Clear leaves unavailable work alone");
}
Check(Assign("Doctor", 0, 14) == 1 && Assign("Doctor", 0, 9) == 2 && Assign("Doctor", 0, 4) == 3, "Threshold boundaries enable new skilled work");
Check(Assign("Doctor", 2, 3.999f) == 0 && Assign("Doctor", 0, 0, 1) == 4, "Low unmotivated jobs switch off; low-skill passions remain training opportunities");
Check(Assign("Doctor", 0, 0, 2) == 3, "Major passion boosts a newly enabled job");
foreach (int unknown in new[] { -1, 5, int.MaxValue })
{
    Check(Assign("Firefighter", unknown, 20) == unknown, "Unknown native priority domains are not clamped by Auto assign");
    Check(WorkPanelPolicy.ClearPriority(unknown, false) == unknown, "Unknown priority domains are not cleared");
}
// Execute the raw-delta path used by the actual native adapter, not a duplicate test implementation.
foreach (bool manual in new[] { false, true })
{
    int[] raw = { 0, 1, 2, 3, 4, 2, 1 };
    int[] original = (int[])raw.Clone();
    bool currentMode = manual;
    var delta = new WorkPanelPriorityDelta(raw);
    // Last two stand for a hidden definition and a permanently disabled job.
    for (int i = 0; i < 5; i++) delta.After[i] = WorkPanelPolicy.ClearPriority(raw[i], false);
    int writes = 0;
    delta.Apply(false, i => raw[i], () => currentMode == manual, (i, value) => { raw[i] = value; writes++; });
    Check(writes == 4 && raw.Take(5).All(p => p == 0), "Clear writes only enabled available deltas in either mode");
    Check(raw[5] == 2 && raw[6] == 1, "Hidden and disabled raw assignments survive Clear");
    Check(delta.Matches(i => raw[i], delta.After) && delta.CountDifferent(i => raw[i], delta.Before) == 4, "Poststate validates the full raw snapshot");
    delta.Apply(true, i => raw[i], () => currentMode == manual, (i, value) => raw[i] = value);
    Check(raw.SequenceEqual(original) && currentMode == manual, "Undo restores exact latent1/2/4 and never changes global priority mode");
    Check(delta.Matches(i => raw[i], delta.Before), "Restoration is exact across all loaded definitions");

    var assignment = new WorkPanelPriorityDelta(raw);
    assignment.After[0] = 2; assignment.After[1] = 0; assignment.After[3] = 1;
    assignment.Apply(false, i => raw[i], () => currentMode == manual, (i, value) => raw[i] = value);
    Check(raw[0] == 2 && raw[1] == 0 && raw[3] == 1, "Assignment can enable, disable and reprioritize in one native delta");
    assignment.Apply(true, i => raw[i], () => currentMode == manual, (i, value) => raw[i] = value);
    Check(raw.SequenceEqual(original), "Auto assignment undo restores previously Off jobs too");

    raw[5] = 4; writes = 0;
    Check(!delta.Matches(i => raw[i], delta.Before), "Outside raw2→4 edit invalidates snapshot even if checkbox mode still displays3");
    delta.Apply(false, i => raw[i], () => true, (i, value) => { raw[i] = value; writes++; });
    Check(writes == 0, "Conflicting outside edit prevents every transaction write");
    Array.Copy(original, raw, raw.Length);
    currentMode = !manual;
    delta.Apply(false, i => raw[i], () => currentMode == manual, (i, value) => { raw[i] = value; writes++; });
    Check(writes == 0 && raw.SequenceEqual(original), "Mode/context mismatch refuses mutation");
    Check(!delta.Matches(i => i == 2 ? null : (int?)raw[i], delta.Before), "Missing raw data fails closed without effective-priority fallback");

    // Plan through the actual assignment policy, then execute the same raw transaction used by AutoAssign.
    string[] names = { "Firefighter", "BasicWorker", "Construction", "Doctor", "Hauling", "ModdedJob", "LockedJob", "UnfitJob" };
    int[] saved = { 0, 4, 1, 2, 0, 4, 2, 1 };
    float?[] skills = { null, null, 12, 0, null, null, 20, 20 };
    int[] passions = { 0, 0, 1, 0, 0, 0, 2, 2 };
    currentMode = manual;
    var auto = new WorkPanelPriorityDelta(saved);
    for (int i = 0; i < names.Length; i++)
        auto.After[i] = Assign(names[i], saved[i], skills[i], passions[i], disabled: i == 6, incapable: i == 7);
    auto.Apply(false, i => saved[i], () => currentMode == manual, (i, value) => saved[i] = value);
    Check(saved.SequenceEqual(new[] { 1, 2, 1, 0, 4, 4, 2, 1 }), "Auto assign enables, clears and sets exact raw priorities independently of mode");
    Check(currentMode == manual, "Auto assign never switches global priority mode");
    Check(auto.Matches(i => saved[i], auto.After), "Either mode has the exact raw poststate required by Undo");
    auto.Apply(true, i => saved[i], () => currentMode == manual, (i, value) => saved[i] = value);
    Check(saved.SequenceEqual(auto.Before) && currentMode == manual, "Auto assign Undo restores exact raw0/1/2/4 and leaves either mode unchanged");
}
{
    int[] raw = { 1, 2, 3, 4 };
    var delta = new WorkPanelPriorityDelta(raw);
    delta.After[0] = delta.After[1] = 0;
    int writes = 0;
    delta.Apply(false, i => raw[i], () => true, (i, value) => { raw[i] = value; raw[3] = 1; writes++; });
    Check(writes == 1 && raw[1] == 2 && !delta.Matches(i => raw[i], delta.After), "Callback side effects stop subsequent writes and prevent exact Undo eligibility");
    var noOp = new WorkPanelPriorityDelta(raw);
    noOp.Apply(false, i => raw[i], () => true, (i, value) => writes++);
    Check(writes == 1 && noOp.CountDifferent(i => raw[i], noOp.Before) == 0, "No-op performs no setters");
    bool valid = true;
    delta = new WorkPanelPriorityDelta(raw); delta.After[1] = delta.After[2] = 0; writes = 0;
    delta.Apply(false, i => raw[i], () => valid, (i, value) => { raw[i] = value; valid = false; writes++; });
    Check(writes == 1 && raw[2] == 3, "Context change during a callback stops later writes");
}
Console.WriteLine($"PASS {checks} Work policy, raw assignment/Clear/Undo delta, toggle/memory, priority cycle and audit contracts.");
