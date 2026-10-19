using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ReframedUI
{
    // Disposable, opt-in developer fixtures only. Call Update before the rail's
    // Active/fallback check so "native on" can also be consumed in native mode.
    // No tick hook, save, player-prefs write, arbitrary paths or executable input.
    internal static class PawnBarDiagnostics
    {
        private static readonly bool optedIn = Environment.GetCommandLineArgs().Any(
            a => string.Equals(a, "-reframed-pawn-bar-audit", StringComparison.OrdinalIgnoreCase));
        private static readonly Dictionary<int, DrawRecord> draws = new Dictionary<int, DrawRecord>();
        private static readonly Dictionary<int, Building_Bed> medicalBeds = new Dictionary<int, Building_Bed>();
        private static float nextPoll;
        private static bool busy;
        private static int sequence;
        private static bool ioErrorLogged;
        internal static bool Enabled => optedIn && Prefs.DevMode;
        private static string DirectoryPath => Path.Combine(GenFilePaths.SaveDataFolderPath, "ReframedUI", "PawnBarAudit");

        internal static void Update()
        {
            if (!Enabled || busy || Time.realtimeSinceStartup < nextPoll) return;
            nextPoll = Time.realtimeSinceStartup + .25f;
            string command = null, receipt = null;
            try
            {
                string path = Path.Combine(DirectoryPath, "command.txt");
                if (!File.Exists(path)) return;
                Directory.CreateDirectory(DirectoryPath);
                receipt = Path.Combine(DirectoryPath, DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture)
                    + "-" + (++sequence).ToString("D4", CultureInfo.InvariantCulture));
                // Claim before executing; a failed command cannot run repeatedly.
                // The consumed text remains beside its result for provenance.
                File.Move(path, receipt + ".command.txt");
                if (new FileInfo(receipt + ".command.txt").Length > 512)
                    throw new ArgumentException("Command must be one line, at most 512 UTF-8 bytes.");
                command = File.ReadAllText(receipt + ".command.txt", Encoding.UTF8).Trim();
                if (command.Length == 0 || command.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                    throw new ArgumentException("Expected one explicit command per file.");
                Log.Message("[Reframed UI] Pawn-bar audit command: " + command);
                if (Current.ProgramState != ProgramState.Playing || Current.Game == null || Find.CurrentMap == null)
                    throw new InvalidOperationException("A playing game and current map are required.");
                string[] words = command.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                // Explicit read-only capture permits observing native speed controls without
                // turning the act of measurement into a pause. All fixture commands still pause.
                if (words[0] != "observe") Find.TickManager.Pause();
                if (words[0] == "motion")
                {
                    RequireCount(words, 3);
                    busy = true;
                    MaterialsDiagnostics.StartMotion(words[1], words[2], receipt, (success, message) =>
                    {
                        busy = false;
                        WriteResult(receipt, command, success, message);
                    });
                    return;
                }
                if (words[0] == "capture" || words[0] == "observe")
                {
                    RequireCount(words, 2);
                    if (words[1].Length > 64 || words[1].Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
                        throw new ArgumentException("Capture tag must contain 1-64 letters, digits, hyphens or underscores.");
                    Root root = UnityEngine.Object.FindObjectOfType<Root>();
                    if (root == null) throw new InvalidOperationException("No Unity game root.");
                    busy = true;
                    root.StartCoroutine(Capture(receipt, command, words[1]));
                    return;
                }
                string result = Execute(words);
                WriteResult(receipt, command, true, result);
            }
            catch (Exception ex)
            {
                busy = false;
                try { if (receipt != null) WriteResult(receipt, command, false, ex.GetType().Name + ": " + ex.Message); }
                catch (Exception io) { if (!ioErrorLogged) { ioErrorLogged = true; Log.Warning("[Reframed UI] Pawn-bar audit receipt failed: " + io.Message); } }
                Log.Warning("[Reframed UI] Pawn-bar audit command failed: " + ex.Message);
            }
        }

        private static string Execute(string[] args)
        {
            string verb = args[0];
            if (verb == "resources") return MaterialsDiagnostics.Execute(args);
            if (verb == "weather")
            {
                RequireCount(args, 2);
                // Bounded native visual fixture in the already opted-in disposable map.
                if (args[1] != "Clear" && args[1] != "Rain" && args[1] != "Fog" && args[1] != "SnowHard")
                    throw new ArgumentException("Allowed weather fixture: Clear, Rain, Fog, SnowHard.");
                var weather = DefDatabase<WeatherDef>.GetNamed(args[1]);
                Find.CurrentMap.weatherManager.TransitionTo(weather);
                return "Native weather transition to " + weather.defName + "; paused, unsaved visual fixture.";
            }
            if (verb == "rows")
            {
                RequireCount(args, 2);
                ReframedMod.Settings.CollapsedPawnRows = Integer(args[1], 1, 3);
                return "Collapsed pawn rows = " + ReframedMod.Settings.CollapsedPawnRows + "; mod setting changed in memory only.";
            }
            if (verb == "view")
            {
                RequireCount(args, 3);
                ColonistGroupsHud.SetAuditView(OnOff(args[1]), Integer(args[2], 0, 10000));
                return "Pawn-bar expanded = " + args[1] + "; requested first row = " + args[2] + "; clamped by viewport.";
            }
            if (verb == "baseline") { RequireCount(args, 1); return "Paused only; no pawn state reset or save write."; }
            if (verb == "native")
            {
                RequireCount(args, 2);
                ReframedMod.Settings.ColonistCards = !OnOff(args[1]);
                return "Native drawer " + args[1] + "; mod setting changed in memory only.";
            }
            if (verb == "visiblemood")
            {
                RequireCount(args, 2);
                SetMemoryPref("visibleMood", OnOff(args[1]));
                return "Native visibleMood " + args[1] + "; PrefsData changed in memory without Apply/Save.";
            }
            if (verb == "weapons")
            {
                RequireCount(args, 2);
                ShowWeaponsUnderPortraitMode mode;
                if (args[1] == "never") mode = ShowWeaponsUnderPortraitMode.Never;
                else if (args[1] == "drafted") mode = ShowWeaponsUnderPortraitMode.WhileDrafted;
                else if (args[1] == "always") mode = ShowWeaponsUnderPortraitMode.Always;
                else throw new ArgumentException("Weapons mode must be never, drafted or always.");
                SetMemoryPref("showWeaponsUnderPortraitMode", mode);
                return "Weapon preference " + mode + "; in memory only.";
            }
            if (verb == "roster")
            {
                RequireCount(args, 2);
                int target = Integer(args[1], 1, 256);
                Map map = Find.CurrentMap;
                int before = map.mapPawns.FreeColonistsSpawned.Count;
                int count = before;
                while (count < target)
                {
                    IntVec3 cell = CellFinder.RandomClosewalkCellNear(map.Center, map, 20);
                    Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
                        PawnGenerationContext.PlayerStarter, forceGenerateNewPawn: true, allowDead: false,
                        allowDowned: false, canGeneratePawnRelations: false, allowPregnant: false));
                    GenSpawn.Spawn(pawn, cell, map);
                    count++;
                }
                Find.ColonistBar.MarkColonistsDirty();
                return "Current-map free colonists " + before + " -> " + count + "; generated native identities; no removals.";
            }
            if (verb == "caravans")
            {
                RequireCount(args, 2);
                int target = Integer(args[1], 1, 12);
                if (!Find.CurrentMap.Tile.Valid || !Find.CurrentMap.Tile.LayerDef.canFormCaravans)
                    throw new InvalidOperationException("Current map requires a valid caravan-capable world tile.");
                int before = Find.WorldObjects.Caravans.Count(c => c.IsPlayerControlled);
                for (int count = before; count < target; count++)
                {
                    Pawn member = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
                        PawnGenerationContext.PlayerStarter, forceGenerateNewPawn: true, allowDead: false,
                        allowDowned: false, canGeneratePawnRelations: false, allowPregnant: false));
                    CaravanMaker.MakeCaravan(new[] { member }, Faction.OfPlayer, Find.CurrentMap.Tile, true);
                }
                Find.ColonistBar.MarkColonistsDirty();
                return "Player caravans " + before + " -> " + Find.WorldObjects.Caravans.Count(c => c.IsPlayerControlled) +
                    "; one newly generated colonist per added native caravan, no map pawns moved; native entry group count = " +
                    Find.ColonistBar.Entries.Select(e => e.group).Distinct().Count() + ". Paused; no save write.";
            }
            if (verb == "mood" || verb == "draft" || verb == "mental") RequireCount(args, 3);
            else if (verb == "sleep" || verb == "injure" || verb == "down" || verb == "dead" || verb == "inspire" || verb == "burn" ||
                verb == "equip" || verb == "medical" || verb == "flee" || verb == "fight") RequireCount(args, 2);
            else throw new ArgumentException("Unknown command. Allowed: capture TAG; motion materials/pawns/tools TAG; resources populate; resources trends on/off; baseline; native on/off; visiblemood on/off; weapons never/drafted/always; roster 1..256; caravans 1..12; mood INDEX 0..1; draft INDEX on/off; mental INDEX aggressive/nonaggressive; sleep/injure/down/dead/inspire/burn/equip/medical/flee/fight INDEX.");
            Pawn p = PawnAt(args[1]);
            if (p.Dead) throw new InvalidOperationException("Pawn is already dead.");
            string identity = p.ThingID + " " + p.LabelShortCap;
            switch (verb)
            {
                case "equip":
                    EquipPistol(p);
                    return identity + ": primary = " + p.equipment.Primary?.def.defName + "; existing primary preserved on map if replaced.";
                case "medical":
                    RequireMobile(p);
                    Building_Bed bed = p.CurrentBed();
                    if (bed == null && medicalBeds.TryGetValue(p.thingIDNumber, out Building_Bed previous) && previous.Spawned && previous.Map == p.Map)
                        bed = previous;
                    if (bed == null || bed.def.defName != "HospitalBed")
                    {
                        ThingDef hospital = DefDatabase<ThingDef>.GetNamed("HospitalBed");
                        IntVec3 spot = GenRadial.RadialCellsAround(p.Position, 20f, true).FirstOrDefault(cell =>
                            GenAdj.OccupiedRect(cell, Rot4.North, hospital.size).Cells.All(c => c.InBounds(p.Map) && c.Standable(p.Map) &&
                            c.GetThingList(p.Map).Count == 0));
                        if (!spot.IsValid || !GenAdj.OccupiedRect(spot, Rot4.North, hospital.size).Cells.All(c => c.InBounds(p.Map) && c.Standable(p.Map) && c.GetThingList(p.Map).Count == 0))
                            throw new InvalidOperationException("No empty hospital-bed footprint within 20 cells.");
                        bed = (Building_Bed)ThingMaker.MakeThing(hospital, ThingDefOf.Steel);
                        bed.SetFaction(Faction.OfPlayer);
                        GenSpawn.Spawn(bed, spot, p.Map, Rot4.North);
                    }
                    if (medicalBeds.Count > 512) medicalBeds.Clear();
                    medicalBeds[p.thingIDNumber] = bed;
                    bed.Medical = true;
                    bool addedMedicalCut = false;
                    // Native CanUseBedNow rejects medical beds for healthy pawns.
                    // Give the disposable fixture a genuine small wound rather
                    // than bypassing the native medical-rest predicate.
                    if (!HealthAIUtility.ShouldSeekMedicalRest(p))
                    {
                        BodyPartRecord healthyArm = p.health.hediffSet.GetNotMissingParts().FirstOrDefault(part =>
                            part.def.defName == "Arm" && p.health.hediffSet.GetPartHealth(part) > 5f);
                        if (healthyArm == null) throw new InvalidOperationException("No healthy arm for bounded medical-rest fixture cut.");
                        Hediff medicalCut = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Cut"), p, healthyArm);
                        medicalCut.Severity = 1f;
                        p.health.AddHediff(medicalCut, healthyArm);
                        addedMedicalCut = true;
                    }
                    if (p.playerSettings != null && p.playerSettings.medCare == MedicalCareCategory.NoCare)
                        p.playerSettings.medCare = MedicalCareCategory.NoMeds;
                    if (!HealthAIUtility.ShouldSeekMedicalRest(p) || !RestUtility.CanUseBedNow(bed, p, checkSocialProperness: false))
                        throw new InvalidOperationException("Native medical-bed eligibility failed: needsRest=" + HealthAIUtility.ShouldSeekMedicalRest(p) +
                            ", care=" + HealthAIUtility.ShouldEverReceiveMedicalCareFromPlayer(p) + ", usable=" + RestUtility.CanUseBedNow(bed, p, false));
                    if (p.drafter != null) p.drafter.Drafted = false;
                    // Explicit fixture teleport to a real bed, then start the real
                    // native LayDown job without advancing simulation while paused.
                    p.Position = bed.GetSleepingSlotPos(0);
                    p.Notify_Teleported();
                    Job rest = JobMaker.MakeJob(JobDefOf.LayDown, bed);
                    rest.playerForced = true;
                    p.jobs.StartJob(rest, JobCondition.InterruptForced);
                    if (p.jobs.curDriver != null) p.jobs.curDriver.asleep = false;
                    if (p.CurJob?.def != JobDefOf.LayDown || !p.InBed() || p.CurrentBed() != bed || !bed.Medical || p.jobs.curDriver?.asleep != false)
                        throw new InvalidOperationException("Medical-rest fixture failed actual native readback: inBed=" + p.InBed() +
                            ", bed=" + p.CurrentBed()?.ThingID + ", job=" + p.CurJob?.def.defName + ", asleep=" + p.jobs.curDriver?.asleep);
                    return identity + ": medical_rest = " + (p.InBed() && p.CurrentBed().Medical) +
                        "; asleep = " + p.jobs.curDriver?.asleep + "; job = " + p.CurJob?.def.defName + "; bed = " + bed.ThingID +
                        "; needs_medical_rest = " + HealthAIUtility.ShouldSeekMedicalRest(p) + "; added_1point_arm_cut = " + addedMedicalCut + "; explicit fixture teleport.";
                case "flee":
                    RequireMobile(p);
                    if (p.CurJob?.def != JobDefOf.FleeAndCower)
                    {
                        if (p.drafter != null) p.drafter.Drafted = false;
                        p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.FleeAndCower, p.Position), JobCondition.InterruptForced);
                    }
                    return identity + ": fleeing = " + (p.CurJob?.def == JobDefOf.FleeAndCower) + "; job = " + p.CurJob?.def.defName;
                case "fight":
                    RequireMobile(p);
                    EquipPistol(p);
                    if (p.drafter == null) throw new InvalidOperationException("Pawn has no drafter.");
                    p.drafter.Drafted = true;
                    p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait_Combat), JobCondition.InterruptForced);
                    IntVec3 aim = GenRadial.RadialCellsAround(p.Position, 5f, false).First(c => c.InBounds(p.Map) && c.Standable(p.Map) && c.GetThingList(p.Map).Count == 0);
                    p.stances.SetStance(new Stance_Warmup(600, new LocalTargetInfo(aim), p.equipment.PrimaryEq.PrimaryVerb));
                    return identity + ": attacking = " + (p.CurJob?.def == JobDefOf.Wait_Combat && p.stances.curStance is Stance_Busy busyStance && busyStance.focusTarg.IsValid) +
                        "; native Wait_Combat and explicit native warmup stance aimed at empty ground " + aim + "; paused; no projectile fired.";
                case "mood":
                    if (p.needs?.mood == null) throw new InvalidOperationException("Pawn has no mood need.");
                    if (!float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float level) ||
                        float.IsNaN(level) || level < 0 || level > 1) throw new ArgumentException("Mood must be finite, 0..1.");
                    p.needs.mood.CurLevel = level;
                    return identity + ": native mood = " + Number(level) + "; no private counters forced (installed imminent getters use current mood/blocked state).";
                case "draft":
                    if (p.drafter == null) throw new InvalidOperationException("Pawn has no drafter.");
                    p.drafter.Drafted = OnOff(args[2]);
                    return identity + ": drafted = " + p.Drafted;
                case "mental":
                    string def = args[2] == "aggressive" ? "Berserk" : args[2] == "nonaggressive" ? "Wander_Psychotic" : null;
                    if (def == null) throw new ArgumentException("Mental mode must be aggressive or nonaggressive.");
                    if (!p.mindState.mentalStateHandler.TryStartMentalState(DefDatabase<MentalStateDef>.GetNamed(def),
                        "Explicit disposable pawn-bar developer fixture", forced: true, forceWake: true))
                        throw new InvalidOperationException("Native mental-state handler rejected " + def + ".");
                    return identity + ": native mental state " + p.MentalStateDef?.defName;
                case "sleep":
                    if (p.Downed || p.InMentalState || p.jobs == null) throw new InvalidOperationException("Sleep requires an awake-capable, non-mental pawn with jobs.");
                    if (p.drafter != null) p.drafter.Drafted = false;
                    Job job = JobMaker.MakeJob(JobDefOf.LayDown, p.Position);
                    job.forceSleep = true;
                    p.jobs.StartJob(job, JobCondition.InterruptForced);
                    if (p.jobs.curDriver == null || p.CurJob?.def != JobDefOf.LayDown)
                        throw new InvalidOperationException("Native LayDown job did not start.");
                    // Explicit fixture forcing: the native LayDown driver is real;
                    // set its public asleep flag while paused, without advancing time.
                    p.jobs.curDriver.asleep = true;
                    return identity + ": native LayDown job; driver.asleep explicitly forced true while paused.";
                case "injure":
                    BodyPartRecord arm = p.health.hediffSet.GetNotMissingParts().FirstOrDefault(part => part.def.defName == "Arm" && p.health.hediffSet.GetPartHealth(part) > 5f);
                    if (arm == null) throw new InvalidOperationException("No healthy arm available for a bounded 1-point cut.");
                    Hediff cut = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Cut"), p, arm);
                    cut.Severity = 1f;
                    p.health.AddHediff(cut, arm);
                    return identity + ": added real 1-point Cut on " + arm.Label + ".";
                case "down":
                    p.health.AddHediff(DefDatabase<HediffDef>.GetNamed("Anesthetic"));
                    return identity + ": native Anesthetic; downed = " + p.Downed;
                case "dead":
                    p.Kill(null);
                    Find.ColonistBar.MarkColonistsDirty();
                    return identity + ": explicitly killed in disposable developer fixture; no save write.";
                case "inspire":
                    InspirationDef inspiration = p.mindState.inspirationHandler.GetRandomAvailableInspirationDef();
                    if (inspiration == null || !p.mindState.inspirationHandler.TryStartInspiration(inspiration,
                        "Explicit disposable pawn-bar developer fixture", sendLetter: false))
                        throw new InvalidOperationException("Native inspiration handler found no eligible inspiration.");
                    return identity + ": native inspiration " + inspiration.defName;
                case "burn":
                    p.TryAttachFire(.1f, null);
                    return identity + ": native attached fire; burning = " + p.IsBurning();
            }
            throw new InvalidOperationException("Unreachable command.");
        }

        private static Pawn PawnAt(string index)
        {
            List<ColonistBar.Entry> entries = Find.ColonistBar.Entries;
            int i = Integer(index, 0, entries.Count - 1);
            Pawn p = entries[i].pawn;
            if (p == null || !p.Spawned || p.Map != Find.CurrentMap || p.Faction != Faction.OfPlayer)
                throw new InvalidOperationException("Entry must be a spawned player pawn on the current map; indices are zero-based native ColonistBar.Entries identities.");
            return p;
        }

        private static void RequireMobile(Pawn pawn)
        {
            if (pawn.Downed || pawn.InMentalState || pawn.jobs == null)
                throw new InvalidOperationException("Fixture job requires a non-downed, non-mental pawn with native jobs.");
        }

        private static void EquipPistol(Pawn pawn)
        {
            if (pawn.equipment == null) throw new InvalidOperationException("Pawn has no native equipment tracker.");
            if (pawn.equipment.Primary?.def.defName == "Gun_Autopistol") return;
            ThingWithComps existing = pawn.equipment.Primary;
            if (existing != null && !pawn.equipment.TryDropEquipment(existing, out _, pawn.Position, forbid: false))
                throw new InvalidOperationException("Could not preserve existing equipment on the map; pistol not created.");
            pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_Autopistol")));
        }

        private static void SetMemoryPref(string fieldName, object value)
        {
            // Fixed internal call sites only, no user-provided member names. Avoid
            // native property Apply side effects and any preference serialization.
            object data = AccessTools.Field(typeof(Prefs), "data").GetValue(null);
            var field = AccessTools.Field(data.GetType(), fieldName);
            if (field == null) throw new MissingFieldException(data.GetType().FullName, fieldName);
            field.SetValue(data, value);
        }

        private static IEnumerator Capture(string receipt, string command, string tag)
        {
            yield return new WaitForEndOfFrame();
            Texture2D texture = null;
            try
            {
                if (!Enabled) throw new InvalidOperationException("Developer opt-in was disabled before capture.");
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (texture == null) throw new InvalidOperationException("Unity returned no screenshot texture.");
                string png = receipt + "-" + tag + ".png";
                File.WriteAllBytes(png, texture.EncodeToPNG());
                string manifest = Json(D("schema", "reframed-pawn-bar-audit-v1", "tag", tag,
                    "utc", DateTime.UtcNow.ToString("o"), "capture_phase", "end_of_frame_render_target",
                    "acceptance", "Unreviewed developer evidence; not a source golden or performance measurement.",
                    "command", command, "png", png, "png_sha256", Hash(png),
                    "assembly", typeof(PawnBarDiagnostics).Assembly.Location,
                    "assembly_sha256", Hash(typeof(PawnBarDiagnostics).Assembly.Location),
                    "version", typeof(PawnBarDiagnostics).Assembly.GetName().Version.ToString(),
                    "state", State()));
                File.WriteAllText(receipt + "-" + tag + ".json", manifest, new UTF8Encoding(false));
                WriteResult(receipt, command, true, "Captured " + png);
            }
            catch (Exception ex)
            {
                try { WriteResult(receipt, command, false, ex.GetType().Name + ": " + ex.Message); }
                catch (Exception io) { Log.Warning("[Reframed UI] Pawn-bar audit capture receipt failed: " + io.Message); }
                Log.Warning("[Reframed UI] Pawn-bar audit capture failed: " + ex.Message);
            }
            finally
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                busy = false;
            }
        }

        internal static object State()
        {
            var entries = new List<object>();
            var native = Find.ColonistBar.Entries;
            for (int i = 0; i < native.Count; i++)
            {
                Pawn p = native[i].pawn;
                if (p == null) { entries.Add(D("index", i, "pawn", null)); continue; }
                MentalBreaker breaker = p.mindState?.mentalBreaker;
                bool asleep = p.CurJob != null && p.jobs?.curDriver?.asleep == true;
                bool caravanResting = p.GetCaravan() != null && p.needs?.rest?.Resting == true;
                bool medical = p.InBed() && p.CurrentBed().Medical;
                bool attacking = p.CurJob?.def == JobDefOf.AttackMelee || p.CurJob?.def == JobDefOf.AttackStatic ||
                    p.CurJob?.def == JobDefOf.Wait_Combat && p.stances?.curStance is Stance_Busy stance && stance.focusTarg.IsValid;
                var hediffs = new List<object>();
                if (p.health != null) foreach (Hediff h in p.health.hediffSet.hediffs)
                    hediffs.Add(D("def", h.def.defName, "severity", h.Severity, "part", h.Part?.def.defName));
                object drawn = null;
                if (draws.TryGetValue(p.thingIDNumber, out DrawRecord record) && record.Frame == Time.frameCount)
                    drawn = D("rect", RectData(record.Bounds), "units", "root GUI screen coordinates", "frame", record.Frame);
                entries.Add(D("index", i, "id", p.ThingID, "name", p.Name?.ToStringFull,
                    "label", p.LabelShortCap.ToString(), "map", native[i].map?.uniqueID, "group", native[i].group,
                    "dead", p.Dead, "downed", p.Downed, "health", p.health?.summaryHealth.SummaryHealthPercent,
                    "hediffs", hediffs, "mood", p.needs?.mood?.CurLevelPercentage,
                    "risk", MoodThresholdExtensions.CurrentMoodThresholdFor(p).ToString(),
                    "can_random_break", breaker?.CanDoRandomMentalBreaks, "break_blocked", breaker?.Blocked,
                    "minor_threshold", breaker?.BreakThresholdMinor, "major_threshold", breaker?.BreakThresholdMajor,
                    "extreme_threshold", breaker?.BreakThresholdExtreme, "mental_state", p.MentalStateDef?.defName,
                    "aggressive_mental_state", p.InAggroMentalState, "asleep", asleep, "caravan_resting", caravanResting, "medical_rest", medical,
                    "drafted", p.Drafted, "job", p.CurJob?.def.defName, "idle", p.mindState?.IsIdle,
                    "idle_icon_day_gate", GenDate.DaysPassed >= 1, "forming_caravan", p.IsFormingCaravan(),
                    "fleeing", p.CurJob?.def == JobDefOf.FleeAndCower, "attacking", attacking,
                    "burning", p.IsBurning(), "inspired", p.mindState?.inspirationHandler?.Inspired,
                    "inspiration", p.mindState?.inspirationHandler?.CurStateDef?.defName,
                    "selected", Find.Selector.SelectedObjects.Contains(p.Dead ? (object)p.Corpse : p),
                    "weapon", p.equipment?.Primary?.def.defName,
                    "mood_visual_eligible", Prefs.VisibleMood && p.needs?.mood != null && breaker?.CanDoRandomMentalBreaks == true && !p.Dead && !p.Downed,
                    "drawn_card", drawn));
            }
            return D("frame", Time.frameCount, "game_tick", Find.TickManager.TicksGame,
                "paused", Find.TickManager.Paused, "time_speed", Find.TickManager.CurTimeSpeed.ToString(), "ui_scale", Prefs.UIScale,
                "main_tab", Find.MainTabsRoot.OpenTab?.defName, "tools_expanded", ToolsMenu.Expanded,
                "tools_diagnostics", ToolsDiagnostics.State(),
                "architect_diagnostics", ArchitectDiagnostics.State(),
                "selection_inspector", SelectionInspector.AuditState(),
                "tools_key", ToolsMenu.ShortcutLabel, "clock_bounds", RectData(FloatingClock.Bounds),
                "menu_bounds", RectData(HudLayout.MenuButtonBounds), "tools_bounds", RectData(HudLayout.ToolsButtonBounds),
                "weather", Find.CurrentMap.weatherManager.CurWeatherPerceived?.defName,
                "client_width", Screen.width, "client_height", Screen.height,
                "ui_width", UI.screenWidth, "ui_height", UI.screenHeight,
                "colonist_cards_setting", ReframedMod.Settings.ColonistCards,
                "custom_layout_active", ColonistGroupsHud.TryGetOccupiedBounds(out _),
                "expanded", ColonistGroupsHud.Expanded, "first_row", ColonistGroupsHud.FirstRow,
                "collapsed_rows", ReframedMod.Settings.CollapsedPawnRows,
                "visible_mood", Prefs.VisibleMood, "weapons", Prefs.ShowWeaponsUnderPortraitMode.ToString(),
                "dev_mode", Prefs.DevMode, "materials_diagnostics", MaterialsDiagnostics.AuditSnapshot(), "entries", entries);
        }

        internal static void RecordDraw(Pawn pawn, Rect rect)
        {
            if (!Enabled || pawn == null || Event.current?.type != EventType.Repaint) return;
            // Bound retained records across reloads without retaining Pawn objects.
            if (draws.Count > 1024) draws.Clear();
            draws[pawn.thingIDNumber] = new DrawRecord { Frame = Time.frameCount, Bounds = new Rect(UI.GUIToScreenPoint(rect.position), rect.size) };
        }

        private sealed class DrawRecord { internal int Frame; internal Rect Bounds; }
        private static object RectData(Rect r) => D("x", r.x, "y", r.y, "width", r.width, "height", r.height);
        private static int Integer(string text, int min, int max)
        {
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < min || value > max)
                throw new ArgumentException("Integer must be in " + min + ".." + max + ".");
            return value;
        }
        private static bool OnOff(string text) => text == "on" ? true : text == "off" ? false : throw new ArgumentException("Expected on or off.");
        private static void RequireCount(string[] args, int count)
        { if (args.Length != count) throw new ArgumentException("Wrong number of arguments for " + args[0] + "."); }
        private static void WriteResult(string receipt, string command, bool success, string message)
        {
            File.WriteAllText(receipt + ".result.json", Json(D("utc", DateTime.UtcNow.ToString("o"), "command", command,
                "success", success, "message", message, "paused", Current.ProgramState == ProgramState.Playing && Find.TickManager.Paused)), new UTF8Encoding(false));
            Log.Message("[Reframed UI] Pawn-bar audit " + (success ? "success: " : "failure: ") + message);
        }
        internal static Dictionary<string, object> D(params object[] pairs)
        {
            var result = new Dictionary<string, object>();
            for (int i = 0; i < pairs.Length; i += 2) result.Add((string)pairs[i], pairs[i + 1]);
            return result;
        }
        internal static string Json(object value)
        {
            if (value == null) return "null";
            if (value is string s)
            {
                var b = new StringBuilder("\"");
                foreach (char c in s)
                    if (c == '"' || c == '\\') b.Append('\\').Append(c);
                    else if (c < 32 || char.IsSurrogate(c)) b.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else b.Append(c);
                return b.Append('"').ToString();
            }
            if (value is bool flag) return flag ? "true" : "false";
            if (value is Rect rect) return Json(RectData(rect));
            if (value is Vector2 vector) return Json(D("x", vector.x, "y", vector.y));
            if (value is Vector3 vector3) return Json(D("x", vector3.x, "y", vector3.y, "z", vector3.z));
            if (value is Color color) return Json(D("r", color.r, "g", color.g, "b", color.b, "a", color.a));
            if (value is Enum) return Json(value.ToString());
            if (value is IDictionary<string, object> dict)
                return "{" + string.Join(",", dict.Select(pair => Json(pair.Key) + ":" + Json(pair.Value))) + "}";
            if (value is IDictionary general)
            {
                var fields = new List<string>();
                foreach (DictionaryEntry pair in general) fields.Add(Json(Convert.ToString(pair.Key, CultureInfo.InvariantCulture)) + ":" + Json(pair.Value));
                return "{" + string.Join(",", fields) + "}";
            }
            if (value is IEnumerable list) return "[" + string.Join(",", list.Cast<object>().Select(Json)) + "]";
            if (value is float number) return Number(number);
            if (value is double d && (double.IsNaN(d) || double.IsInfinity(d))) return "null";
            if (value is byte || value is short || value is int || value is long || value is double || value is decimal || value is uint || value is ulong)
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            return Json(Convert.ToString(value, CultureInfo.InvariantCulture));
        }
        private static string Number(float value) => float.IsNaN(value) || float.IsInfinity(value) ? "null" : value.ToString("R", CultureInfo.InvariantCulture);
        internal static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }

}
