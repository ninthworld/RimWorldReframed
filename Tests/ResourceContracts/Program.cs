using System;
using System.Collections.Generic;
using System.Linq;
using ReframedUI;

int passed = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    passed++;
}
Dictionary<string, int> Counts(int steel = 100, int? wood = 0)
{
    var values = new Dictionary<string, int> { ["Steel"] = steel };
    if (wood.HasValue) values["Wood"] = wood.Value;
    return values;
}
ResourceHistoryLedger Week(int initial = 100)
{
    var ledger = new ResourceHistoryLedger();
    for (int tick = 0; tick <= 420000; tick += 2500) ledger.Observe(tick, Counts(initial));
    return ledger;
}
bool Change(ResourceHistoryLedger ledger, int tick, int current, long expected)
    => ledger.TryChange("Steel", tick, current, out long delta, out _) && delta == expected;

var fresh = new ResourceHistoryLedger();
Check(!fresh.TryChange("Steel", 800000, 100, out _, out _), "No first-load fabricated zero baseline");
fresh.Observe(800000, Counts());
Check(!fresh.TryChange("Steel", 800000, 100, out _, out _), "One late first observation is not seven days");
Check(!fresh.Observe(802499, Counts(999)), "No sub-hour sample");
Check(fresh.Observe(802500, Counts(999)), "Next hourly boundary sampled");

var week = Week();
Check(!week.TryChange("Steel", 419999, 150, out _, out _), "One tick before seven days is unavailable");
Check(Change(week, 420000, 150, 50), "Exact seven-day positive delta");
Check(Change(week, 420000, 50, -50), "Signed loss");
Check(Change(week, 420000, 100, 0), "Observed stable count has neutral zero delta");
Check(week.TryChange("Wood", 420000, 0, out long zero, out _) && zero == 0, "Explicit sampled zeros are genuine");
Check(!week.TryChange("NewModResource", 420000, 0, out _, out _), "Unknown resource is not sampled zero");
Check(!week.TryChange("Steel", 422501, 150, out _, out _), "Latest observation too old");
Check(!week.TryChange("Steel", 420000, -1, out _, out _), "Corrupt current native count is unavailable");
Check(Change(Week(int.MaxValue), 420000, 0, -2147483647L), "Large negative quantity change does not overflow");
Check(Change(Week(0), 420000, int.MaxValue, 2147483647L), "Large positive quantity change does not overflow");

var precise = new ResourceHistoryLedger();
for (int tick = 0; tick <= 420000; tick += 2500) precise.Observe(tick, Counts(tick / 2500));
Check(Change(precise, 422499, 1000, 1000), "Use at-or-before target, not nearest/future sample");
precise.Observe(422500, Counts(169)); precise.Observe(425000, Counts(170));
Check(Change(precise, 425000, 1000, 998), "Target advances to the next exact old observation");

var gap = new ResourceHistoryLedger();
for (int tick = 0; tick <= 420000; tick += 2500) if (tick != 200000) gap.Observe(tick, Counts());
Check(!gap.TryChange("Steel", 420000, 150, out _, out _), "Missing middle hour invalidates continuous seven-day history");
gap.Observe(1000000, Counts());
Check(!gap.TryChange("Steel", 1000000, 150, out _, out _), "Long inactive gap never filled or interpolated");

var missingDef = new ResourceHistoryLedger();
for (int tick = 0; tick <= 420000; tick += 2500)
    missingDef.Observe(tick, tick == 200000 ? new Dictionary<string, int> { ["Wood"] = 1 } : Counts());
Check(!missingDef.TryChange("Steel", 420000, 150, out _, out _), "A def missing in any middle snapshot is unavailable");

var mapA = Week(100);
var mapB = Week(400);
Check(Change(mapA, 420000, 150, 50) && Change(mapB, 420000, 150, -250), "Independent map histories");
var exported = mapA.Export();
var restored = new ResourceHistoryLedger();
Check(restored.Restore(exported, 420000, new HashSet<string> { "Steel", "Wood" }), "Valid saved records restore");
Check(Change(restored, 420000, 150, 50), "Restore preserves exact seven-day delta");
exported[0].Counts["Steel"] = 999;
Check(Change(restored, 420000, 150, 50) && Change(mapA, 420000, 150, 50), "Save/export/restore record ownership is isolated");
var inputCounts = Counts();
var immutable = new ResourceHistoryLedger(); immutable.Observe(0, inputCounts); inputCounts["Steel"] = 999;
Check(immutable.Export()[0].Counts["Steel"] == 100, "Capture copies native count dictionary");

var dirty = Week().Export();
dirty.Insert(0, null);
dirty.Add(new ResourceHistorySample()); // Exactly the missing Scribe fields' defaults.
dirty.First(s => s?.Tick == 200000).Counts = null;
Check(!restored.Restore(dirty, 420000, null), "Missing/null saved fields are marked corrupt");
Check(!restored.TryChange("Steel", 420000, 150, out _, out _), "Discarded corrupt observation leaves a visible history gap");

dirty = Week().Export();
dirty.Add(new ResourceHistorySample { Tick = 200000, Counts = Counts(999) });
Check(!restored.Restore(dirty, 420000, null), "Duplicate timestamp saved records are rejected");
Check(!restored.TryChange("Steel", 420000, 150, out _, out _), "Neither ambiguous duplicate becomes a false baseline/continuous observation");

dirty = Week().Export(); dirty[80].Counts["Steel"] = -1;
Check(!restored.Restore(dirty, 420000, null), "Negative saved count is corrupt");
Check(!restored.TryChange("Steel", 420000, 150, out _, out _), "Corrupt saved count cannot become zero");
Check(restored.Restore(Week().Export(), 420000, new HashSet<string> { "Wood" }), "Removed definitions safely filtered on restore");
Check(!restored.TryChange("Steel", 420000, 150, out _, out _), "Missing mod definition does not regain historical zeros");
Check(restored.Restore(null, 420000, null) && restored.Count == 0, "Absent saved history starts empty");
Check(!restored.Restore(Week().Export(), 300000, null) && restored.Count == 0, "Future data after load rewind clears timeline");
week.Observe(100000, Counts(900));
Check(week.Count == 1 && week.LastTick == 100000, "Backward sampling starts a new timeline immediately");
Check(!week.TryChange("Steel", 100000, 900, out _, out _), "Backward timeline cannot borrow future history");
Check(week.Observe(102500, Counts(950)), "Hourly sampling resumes after rewind");

var longRun = new ResourceHistoryLedger();
for (int tick = 0; tick <= 2500000; tick += 2500) longRun.Observe(tick, Counts(tick / 2500));
Check(longRun.Count == 176, "Long runs remain bounded at 176 snapshots");
Check(Change(longRun, 2500000, 1000, 168), "Pruning retains the required seven-day baseline");
var outOfOrder = longRun.Export(); outOfOrder.Reverse();
Check(restored.Restore(outOfOrder, 2500000, null) && Change(restored, 2500000, 1000, 168), "Out-of-order saved records normalize without changing values");
Check(!restored.Restore(Enumerable.Repeat(new ResourceHistorySample(), 705).ToList(), 2500000, null), "Oversized corrupt restore cannot grow unbounded history");

Check(ResourceHistoryLedger.SanitizeCategories(null).Count == 0, "Missing category state restores empty");
var categories = ResourceHistoryLedger.SanitizeCategories(new[] { null, "", " ", "category:Food", "category:Food", "category:Modded", new string('x', 513) });
Check(categories.SequenceEqual(new[] { "category:Food", "category:Modded" }), "Scribe category state drops nulls/duplicates/invalid strings, retains stable mod IDs");
Check(ResourceHistoryLedger.SanitizeCategories(Enumerable.Range(0, 2000).Select(i => "category:" + i)).Count == 1024, "Restored category state is bounded");

var merged = new ResourceCountMerge<string>(new Dictionary<string, int> { ["Steel"] = 100, ["NativeWeapon"] = 2, ["NativeZeroWeapon"] = 0 },
    new[] { "Sword", "NativeWeapon", "NativeZeroWeapon", "UnusedWeapon" });
merged.AddStored(1, "Sword", 1); merged.AddStored(1, "Sword", 1); merged.AddStored(2, "Sword", 3);
Check(merged.Counts["Sword"] == 4, "Supplemental stored weapon stacks counted once per stored thing identity");
merged.AddStored(3, "NativeWeapon", 100); merged.AddStored(4, "NativeZeroWeapon", 100);
Check(merged.Counts["NativeWeapon"] == 2 && merged.Counts["NativeZeroWeapon"] == 0, "Native counter authoritative even for zero weapon quantity");
merged.AddStored(5, "UnknownWeapon", 1);
Check(!merged.Counts.ContainsKey("UnknownWeapon"), "Storage adapter cannot fabricate a quantity for unknown def");
Check(merged.Counts["UnusedWeapon"] == 0 && merged.Counts["Steel"] == 100, "Known supplemental zeros explicit; native material unchanged");
var overflow = new ResourceCountMerge<string>(new Dictionary<string, int>(), new[] { "Sword", "Bad" });
overflow.AddStored(1, "Sword", int.MaxValue); overflow.AddStored(2, "Sword", 1); overflow.AddStored(3, "Sword", 1);
Check(!overflow.Counts.ContainsKey("Sword"), "Overflowed stored count unavailable instead of wrapped/clamped");
overflow.AddStored(4, "Bad", -1);
Check(!overflow.Counts.ContainsKey("Bad"), "Negative corrupt stored stack cannot become a genuine zero");
var noPrivateApi = new ResourceCountMerge<string>(Counts(), Array.Empty<string>());
Check(!noPrivateApi.Counts.ContainsKey("Sword"), "Native-only fallback does not invent weapon zeros");
var weaponLedger = new ResourceHistoryLedger();
for (int tick = 0; tick <= 420000; tick += 2500) weaponLedger.Observe(tick, merged.Counts);
Check(weaponLedger.TryChange("Sword", 420000, 1, out long weaponChange, out _) && weaponChange == -3,
    "Same merged weapon quantity source yields correct seven-day decrease");

Console.WriteLine($"Resource history contracts: {passed} checks passed. Pure post-Scribe-state/time/merge rules only; no game acceptance claim.");

