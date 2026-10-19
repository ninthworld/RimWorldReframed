using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Uses the existing dual-gated developer command channel. All mutations are
    // explicit disposable fixtures; no saves, tick changes or live ledger writes.
    internal static class MaterialsDiagnostics
    {
        private const string StockpileLabel = "Reframed materials audit fixture";
        private const int Frames = 16;
        private const float Duration = .8f;
        private static readonly string[] DefNames = { "Steel", "WoodLog", "Cloth", "Leather_Plain", "ComponentIndustrial",
            "MedicineIndustrial", "Gun_AssaultRifle", "Gun_Autopistol", "Gun_LMG", "RawRice", "RawPotatoes", "MealSimple", "MealFine" };
        private static readonly int[] Targets = { 713, 247, 133, 108, 25, 17, 4, 3, 2, 1375, 193, 49, 12 };
        private static ResourceHistoryLedger trendFixture;
        private static Map trendMap;
        private static bool syntheticTrends;
        private static readonly Dictionary<string, int> trendBaselines = new Dictionary<string, int>();

        internal static string Execute(string[] args)
        {
            Guard();
            if (args.Length == 2 && args[1] == "populate") return Populate();
            if (args.Length == 2 && args[1] == "overflow") return Populate(overflow: true);
            if (args.Length == 3 && args[1] == "trends" && (args[2] == "on" || args[2] == "off"))
            {
                if (args[2] == "off") { syntheticTrends = false; trendFixture = null; trendMap = null; return "Synthetic visual trends disabled; native live ledger retained."; }
                InitializeTrends();
                return PawnBarDiagnostics.Json(PawnBarDiagnostics.D("fixture", "SYNTHETIC VISUAL FIXTURE",
                    "notice", "Separate ledger and virtual 420000-tick comparison clock; actual game ticks and live observations unchanged.",
                    "readback", TrendCases(ResourceHistoryCounts.Read(Find.CurrentMap))));
            }
            throw new ArgumentException("Allowed: resources populate; resources overflow; resources trends on/off.");
        }

        private static string Populate(bool overflow = false)
        {
            Map map = Find.CurrentMap;
            var defs = DefNames.Select(name => DefDatabase<ThingDef>.GetNamed(name)).ToList();
            var targets = Targets.ToList();
            int extraDefinitions = 0;
            if (overflow)
            {
                targets[Array.IndexOf(DefNames, "RawRice")] = 12950;
                // Round-robin native item categories for a varied, deterministic
                // long list. Never generate pawns/buildings or items needing a
                // material. Definition/name input is not accepted from commands.
                var categories = DefDatabase<ThingDef>.AllDefsListForReading.Where(def =>
                    def.category == ThingCategory.Item && def.CountAsResource && !def.MadeFromStuff &&
                    def.stackLimit > 0 && !DefNames.Contains(def.defName))
                    .GroupBy(def => def.thingCategories?.FirstOrDefault()?.defName ?? "other")
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => group.OrderBy(def => def.defName, StringComparer.Ordinal).ToList()).ToList();
                for (int row = 0; extraDefinitions < 45; row++)
                {
                    bool any = false;
                    foreach (var category in categories)
                    {
                        if (row >= category.Count) continue;
                        any = true;
                        defs.Add(category[row]);
                        targets.Add(15 + extraDefinitions * 3);
                        if (++extraDefinitions == 45) break;
                    }
                    if (!any) break;
                }
                if (extraDefinitions == 0) throw new InvalidOperationException("No eligible extra native counted Item definitions for overflow fixture.");
            }
            map.resourceCounter.UpdateResourceCounts();
            Dictionary<ThingDef, int> before = ResourceHistoryCounts.Read(map);
            Zone_Stockpile stockpile = map.zoneManager.AllZones.OfType<Zone_Stockpile>().FirstOrDefault(z => z.label == StockpileLabel);
            if (stockpile == null)
            {
                stockpile = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager) { label = StockpileLabel };
                stockpile.settings.filter.SetDisallowAll();
                map.zoneManager.RegisterZone(stockpile);
            }
            foreach (ThingDef def in defs) stockpile.settings.filter.SetAllow(def, true);
            var added = new Dictionary<ThingDef, int>();
            for (int i = 0; i < defs.Count; i++)
            {
                ThingDef def = defs[i];
                int present = stockpile.slotGroup.HeldThings.Where(t => t.def == def).Sum(t => t.stackCount);
                int deficit = Math.Max(0, targets[i] - present);
                added[def] = deficit;
                while (deficit > 0)
                {
                    // Only empty unfogged cells, never overwrite other zones,
                    // pawns, items, buildings or plants. Existing stockpile items
                    // are retained; repeat commands only fill remaining deficits.
                    IntVec3? spot = GenRadial.RadialCellsAround(map.Center, 60f, true).Where(c =>
                        c.InBounds(map) && c.Standable(map) && !c.Fogged(map) && c.GetThingList(map).Count == 0 &&
                        (map.zoneManager.ZoneAt(c) == null || map.zoneManager.ZoneAt(c) == stockpile)).Select(c => (IntVec3?)c).FirstOrDefault();
                    if (!spot.HasValue) throw new InvalidOperationException("No empty unfogged stockpile cell within 60 cells of map center; existing fixture contents retained.");
                    if (map.zoneManager.ZoneAt(spot.Value) == null) stockpile.AddCell(spot.Value);
                    Thing item = ThingMaker.MakeThing(def);
                    item.stackCount = Math.Min(Math.Max(1, def.stackLimit), deficit);
                    GenSpawn.Spawn(item, spot.Value, map);
                    if (!stockpile.settings.AllowedToAccept(item) || !stockpile.slotGroup.HeldThings.Contains(item))
                        throw new InvalidOperationException("Native stockpile did not accept fixture item " + item.ThingID + ".");
                    deficit -= item.stackCount;
                }
            }
            map.resourceCounter.UpdateResourceCounts();
            ResourceHistoryComponent history = map.GetComponent<ResourceHistoryComponent>();
            if (history == null) throw new InvalidOperationException("Current map has no native resource history component.");
            // Refresh the production adapter's cache; no Observe/Restore or ticks.
            AccessTools.Method(typeof(ResourceHistoryComponent), "RefreshCounts").Invoke(history, null);
            Dictionary<ThingDef, int> current = history.CurrentCounts;
            var rows = new List<object>();
            foreach (ThingDef def in defs)
            {
                int earlier = before.TryGetValue(def, out int oldCount) ? oldCount : 0;
                bool available = current.TryGetValue(def, out int observed);
                if (!available || observed < (long)earlier + added[def])
                    throw new InvalidOperationException("Native/storage adapter readback failed for " + def.defName + ": before=" + earlier + ", added=" + added[def] + ", current=" + (available ? observed.ToString() : "unavailable"));
                rows.Add(PawnBarDiagnostics.D("def", def.defName, "before", earlier, "added", added[def], "current", observed,
                    "native_counter_key", map.resourceCounter.AllCountedAmounts.ContainsKey(def),
                    "fixture_stored", stockpile.slotGroup.HeldThings.Where(t => t.def == def).Sum(t => t.stackCount)));
            }
            return PawnBarDiagnostics.Json(PawnBarDiagnostics.D("fixture", "real spawned native stockpile items", "zone", stockpile.label,
                "overflow", overflow, "extra_definitions", extraDefinitions, "fixture_definitions", defs.Count,
                "count_semantics", "ResourceCounter.AllCountedAmounts plus existing stored-weapon adapter using native ShouldCount; fresh, unfogged, actual HeldThings; equipped weapons excluded",
                "game_tick", Find.TickManager.TicksGame, "live_history_changed", false, "rows", rows));
        }

        private static void InitializeTrends()
        {
            Dictionary<ThingDef, int> current = ResourceHistoryCounts.Read(Find.CurrentMap);
            trendBaselines.Clear();
            foreach (var pair in current) if (pair.Value >= 0 && pair.Key.defName != "RawRice") trendBaselines[pair.Key.defName] = pair.Value;
            int steel = current.TryGetValue(DefDatabase<ThingDef>.GetNamed("Steel"), out int s) ? s : 0;
            int wood = current.TryGetValue(DefDatabase<ThingDef>.GetNamed("WoodLog"), out int w) ? w : 0;
            if (steel < 1) throw new InvalidOperationException("Populate actual Steel first so the synthetic positive case can use a nonnegative baseline.");
            trendBaselines["Steel"] = Math.Max(0, steel - 121);
            trendBaselines["WoodLog"] = checked(wood + 64);
            trendFixture = new ResourceHistoryLedger();
            for (int tick = 0; tick <= ResourceHistoryLedger.SevenDays; tick += ResourceHistoryLedger.Interval)
                if (!trendFixture.Observe(tick, trendBaselines)) throw new InvalidOperationException("Separate synthetic ledger refused its bounded observations.");
            trendMap = Find.CurrentMap;
            syntheticTrends = true;
            foreach (string name in new[] { "Steel", "WoodLog", "Cloth", "RawRice" })
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamed(name);
                int count = current.TryGetValue(def, out int value) ? value : 0;
                TryTrend(def, count, out bool available, out long delta, out _);
                if (name == "Steel" && (!available || delta <= 0) || name == "WoodLog" && (!available || delta >= 0) ||
                    name == "Cloth" && (!available || delta != 0) || name == "RawRice" && available)
                    throw new InvalidOperationException("Synthetic fixture readback failed for " + name + ".");
            }
        }

        internal static bool TryTrend(ThingDef def, int currentCount, out bool available, out long change, out string reason)
        {
            available = false; change = 0; reason = null;
            if (!PawnBarDiagnostics.Enabled || !syntheticTrends || trendMap != Find.CurrentMap || trendFixture == null) return false;
            available = trendFixture.TryChange(def?.defName, ResourceHistoryLedger.SevenDays, currentCount, out change, out string nativeReason);
            reason = "SYNTHETIC VISUAL FIXTURE: separate generated observations and virtual seven-day clock; no seven-day simulation or live historical evidence. " + nativeReason;
            return true;
        }

        internal static object AuditSnapshot()
        {
            var counts = new Dictionary<string, object>();
            Dictionary<ThingDef, int> native = null;
            if (Current.ProgramState == ProgramState.Playing && Find.CurrentMap != null)
            {
                native = ResourceHistoryCounts.Read(Find.CurrentMap);
                foreach (var pair in native) if (pair.Value > 0) counts[pair.Key.defName] = pair.Value;
            }
            return PawnBarDiagnostics.D("synthetic_visual_trends", PawnBarDiagnostics.Enabled && syntheticTrends && trendMap == Find.CurrentMap,
                "synthetic_notice", "If enabled, trend arrows use a separate fixture ledger and virtual seven-day comparison clock. Actual resource counts are native; game ticks/live history unchanged.",
                "synthetic_baselines", syntheticTrends ? trendBaselines : null,
                "synthetic_cases", native == null ? null : TrendCases(native),
                "native_stored_counts", counts, "materials_animation", ReadAnimation("materials", false), "pawns_animation", ReadAnimation("pawns", false));
        }

        private static object TrendCases(Dictionary<ThingDef, int> current)
        {
            var result = new List<object>();
            foreach (string name in new[] { "Steel", "WoodLog", "Cloth", "RawRice" })
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamed(name);
                int count = current.TryGetValue(def, out int observed) ? observed : 0;
                if (TryTrend(def, count, out bool available, out long change, out string reason))
                    result.Add(PawnBarDiagnostics.D("def", name, "actual_count", count, "synthetic_available", available, "synthetic_change", change, "reason", reason));
            }
            return result;
        }

        internal static void StartMotion(string surface, string tag, string receipt, Action<bool, string> complete)
        {
            Guard();
            if (surface != "materials" && surface != "pawns" && surface != "tools" && surface != "architect")
                throw new ArgumentException("Motion surface must be materials, pawns, tools or architect.");
            if (tag.Length == 0 || tag.Length > 64 || tag.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
                throw new ArgumentException("Motion tag requires 1-64 letters, digits, hyphens or underscores.");
            if ((long)Screen.width * Screen.height * Frames > 96000000L)
                throw new InvalidOperationException("Motion capture exceeds bounded 96-million-pixel texture budget.");
            Dictionary<string, object> initial = ReadAnimation(surface, true);
            Signature(initial); // Validate the trigger contract before starting a coroutine.
            Root root = UnityEngine.Object.FindObjectOfType<Root>();
            if (root == null) throw new InvalidOperationException("No Unity game root for motion capture.");
            File.WriteAllText(receipt + ".armed.json", PawnBarDiagnostics.Json(PawnBarDiagnostics.D("armed", true,
                "surface", surface, "tag", tag, "utc", DateTime.UtcNow.ToString("o"), "state", initial,
                "trigger", "first changed progress or target; 30-second timeout; does not synthesize mouse input")), new UTF8Encoding(false));
            root.StartCoroutine(Motion(surface, tag, receipt, initial, complete));
            Log.Message("[Reframed UI] Armed " + surface + " motion capture " + tag + "; move/click naturally within 30 seconds.");
        }

        private sealed class Sample
        {
            internal Texture2D Texture;
            internal int Frame;
            internal float Realtime, Elapsed;
            internal object Animation, State;
        }

        private static IEnumerator Motion(string surface, string tag, string receipt, Dictionary<string, object> initial, Action<bool, string> complete)
        {
            var samples = new List<Sample>();
            float armedAt = Time.realtimeSinceStartup, started = -1f;
            string signature = Signature(initial), failure = null;
            try
            {
                while (samples.Count < Frames)
                {
                    yield return new WaitForEndOfFrame();
                    Dictionary<string, object> animation = null;
                    try
                    {
                        Guard();
                        animation = ReadAnimation(surface, true);
                        float now = Time.realtimeSinceStartup;
                        if (started < 0 && Signature(animation) == signature)
                        {
                            if (now - armedAt >= 30f) throw new TimeoutException("No progress/target change observed within 30 seconds.");
                        }
                        else
                        {
                            if (started < 0) started = now;
                            float threshold = samples.Count * Duration / (Frames - 1);
                            if (now - started >= threshold && (samples.Count == 0 || samples[samples.Count - 1].Frame != Time.frameCount))
                            {
                                var sample = new Sample { Frame = Time.frameCount, Realtime = now, Elapsed = now - started,
                                    Animation = Freeze(animation), State = Freeze(PawnBarDiagnostics.State()) };
                                sample.Texture = ScreenCapture.CaptureScreenshotAsTexture();
                                if (sample.Texture == null) throw new InvalidOperationException("Unity returned no end-of-frame render texture.");
                                samples.Add(sample);
                            }
                        }
                    }
                    catch (Exception ex) { failure = ex.GetType().Name + ": " + ex.Message; break; }
                }
                try
                {
                    var manifest = new List<object>();
                    for (int i = 0; i < samples.Count; i++)
                    {
                        Sample sample = samples[i];
                        string png = receipt + "-" + tag + "-" + (i + 1).ToString("D2", CultureInfo.InvariantCulture) + ".png";
                        File.WriteAllBytes(png, sample.Texture.EncodeToPNG());
                        manifest.Add(PawnBarDiagnostics.D("index", i + 1, "frame", sample.Frame, "realtime", sample.Realtime,
                            "elapsed_seconds", sample.Elapsed, "animation", sample.Animation, "same_frame_state", sample.State,
                            "png", png, "png_sha256", PawnBarDiagnostics.Hash(png), "width", sample.Texture.width, "height", sample.Texture.height));
                    }
                    File.WriteAllText(receipt + "-" + tag + ".motion.json", PawnBarDiagnostics.Json(PawnBarDiagnostics.D(
                        "schema", "reframed-native-motion-v1", "surface", surface, "tag", tag,
                        "capture_phase", "end_of_frame_render_target", "frames_requested", Frames, "target_duration_seconds", Duration,
                        "timing_limit", "Readback affects frame timing; no performance claim. PNG encoding happens after final sample. Actual timestamps are authoritative.",
                        "utc", DateTime.UtcNow.ToString("o"), "assembly_sha256", PawnBarDiagnostics.Hash(typeof(MaterialsDiagnostics).Assembly.Location),
                        "initial_animation", initial, "error", failure, "samples", manifest)), new UTF8Encoding(false));
                    complete(failure == null, failure ?? "Captured " + samples.Count + " native " + surface + " motion frames: " + receipt + "-" + tag + ".motion.json");
                }
                catch (Exception ex) { complete(false, "Motion write failed: " + ex.Message); }
            }
            finally { foreach (Sample sample in samples) if (sample.Texture != null) UnityEngine.Object.Destroy(sample.Texture); }
        }

        private static Dictionary<string, object> ReadAnimation(string surface, bool required)
        {
            Type type = surface == "materials" ? typeof(Resources) : surface == "tools" ? typeof(ToolsMenu) :
                surface == "architect" ? typeof(ArchitectTransition) : typeof(ColonistGroupsHud);
            MethodInfo method = AccessTools.DeclaredMethod(type, "AnimationAuditState", Type.EmptyTypes);
            if (method == null)
            {
                if (required) throw new MissingMethodException(type.Name, "AnimationAuditState");
                return PawnBarDiagnostics.D("available", false, "reason", "AnimationAuditState not present in this build");
            }
            object value = method.Invoke(null, null);
            if (value is Dictionary<string, object> result) return (Dictionary<string, object>)Freeze(result);
            if (required) throw new InvalidOperationException(type.Name + ".AnimationAuditState must return Dictionary<string,object>.");
            return PawnBarDiagnostics.D("available", false, "reason", "No animation state returned");
        }

        private static string Signature(Dictionary<string, object> state)
        {
            // Deliberately exclude timestamps and non-animation state; natural
            // hover/click or reversal changes target/progress without automation.
            var fields = state.Where(pair => pair.Key.IndexOf("progress", StringComparison.OrdinalIgnoreCase) >= 0 ||
                pair.Key.IndexOf("target", StringComparison.OrdinalIgnoreCase) >= 0).ToDictionary(pair => pair.Key, pair => pair.Value);
            if (fields.Count == 0) throw new InvalidOperationException("Animation audit state requires progress/target fields to arm motion capture.");
            return PawnBarDiagnostics.Json(fields);
        }

        private static object Freeze(object value)
        {
            if (value is IDictionary dictionary)
            {
                var copy = new Dictionary<string, object>();
                foreach (DictionaryEntry pair in dictionary)
                    copy[Convert.ToString(pair.Key, CultureInfo.InvariantCulture)] = Freeze(pair.Value);
                return copy;
            }
            if (!(value is string) && value is IEnumerable sequence) return sequence.Cast<object>().Select(Freeze).ToList();
            return value;
        }

        private static void Guard()
        {
            if (!PawnBarDiagnostics.Enabled || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null)
                throw new InvalidOperationException("Requires explicit CLI flag, DevMode, a playing disposable game and current map.");
        }
    }
}
