using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
#if !RUNTIME_CONTRACTS
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
#endif

namespace ReframedUI
{
    // Diagnostic classification only. This is not a presentation registry and never changes an event.
    internal static class RuntimeNotificationContract
    {
        internal static string Family(string channel, string id, string sourcePackage)
        {
            // A custom definition with a familiar name is not proof of the native semantic identity.
            if (sourcePackage != "ludeon.rimworld") return null;
            if (channel == "LetterDef")
            {
                switch (id)
                {
                    case "ThreatBig": case "NewQuest_ThreatBig": return "major-threat";
                    case "ThreatSmall": return "minor-threat";
                    case "NegativeEvent": case "Death": return "adverse";
                    case "NeutralEvent": return "neutral";
                    case "PositiveEvent": return "beneficial";
                    // Ordinary quest identity alone does not establish its urgency.
                    default: return null;
                }
            }
            if (channel == "MessageTypeDef")
            {
                switch (id)
                {
                    case "ThreatBig": return "major-threat";
                    case "ThreatSmall": return "minor-threat";
                    case "PawnDeath": case "NegativeHealthEvent": case "NegativeEvent": return "adverse";
                    case "NeutralEvent": return "neutral";
                    case "TaskCompletion": return "completion";
                    case "PositiveEvent": return "beneficial";
                    case "SituationResolved": return "resolved";
                    case "RejectInput": return "input-rejection";
                    case "CautionInput": return "input-caution";
                    case "SilentInput": return "input-silent";
                    default: return null;
                }
            }
            return null;
        }

        internal static string MappingStatus(string family) => family == null ? "native-unclassified" : "channel-family-fallback";
    }

#if !RUNTIME_CONTRACTS
    internal static class RuntimeDiagnostics
    {
        private static bool failureLogged;

        // Call on the main thread after loading finishes. No workers, textures, sounds or events are instantiated.
        internal static string WriteReport(string outputDirectory)
        {
            string temporary = null;
            try
            {
                string directory = Path.GetFullPath(outputDirectory);
                Directory.CreateDirectory(directory);
                string report = Path.Combine(directory, "runtime-report.xml");
                temporary = Path.Combine(directory, "runtime-report-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (var xml = XmlWriter.Create(temporary, new XmlWriterSettings { Indent = true, CheckCharacters = false }))
                {
                    xml.WriteStartDocument();
                    xml.WriteStartElement("reframedRuntimeReport");
                    Attr(xml, "schemaVersion", "1");
                    Attr(xml, "capturedUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                    Attr(xml, "evidence", "loaded-runtime-discovery; not in-game acceptance or bespoke mapping coverage");
                    Environment(xml);
                    Modules(xml);
                    Surfaces(xml);
                    Definitions(xml);
                    AlertTypes(xml);
                    xml.WriteElementString("coverageLimitations", "Definitions resolve XML inheritance and patches at capture time. " +
                        "Letter/message IDs do not enumerate event instances. Incident letterDef is only a declared relationship: workers, quests, " +
                        "health, pawn, caravan, world and custom code can select other letters or generate notifications directly. " +
                        "No title matching, color-to-severity inference, custom-event mapping, visual replacement, worker creation or gameplay mutation occurs. " +
                        "Native flags/colors/sounds/classes are recorded even for unclassified definitions. Fallback rows are not bespoke designs. " +
                        "Foreign patches on the same method are potential collisions, not proof of incompatibility; subclass overrides may bypass base patches.");
                    xml.WriteEndElement();
                    xml.WriteEndDocument();
                }
                File.Copy(temporary, report, true);
                File.Delete(temporary);
                return report;
            }
            catch (Exception exception)
            {
                if (!failureLogged)
                {
                    failureLogged = true;
                    Log.Warning("[Reframed UI] Runtime report unavailable; UI activation is unaffected. " + exception);
                }
                if (temporary != null) { try { File.Delete(temporary); } catch { /* best effort diagnostic cleanup */ } }
                return null;
            }
        }

        private static void Environment(XmlWriter xml)
        {
            xml.WriteStartElement("environment");
            Attr(xml, "loadedGameVersion", VersionControl.CurrentVersionStringWithRev);
            Attr(xml, "loadedBuild", VersionControl.CurrentBuild);
            Attr(xml, "loadedRevision", VersionControl.CurrentRevision);
            Attr(xml, "loadedBuildDate", VersionControl.CurrentBuildDate.ToString("O", CultureInfo.InvariantCulture));
            Attr(xml, "unityVersion", Application.unityVersion);
            Attr(xml, "programState", Current.ProgramState);
            Attr(xml, "developerMode", Prefs.DevMode);
            Assembly(xml, "game", typeof(Def).Assembly);
            Assembly(xml, "harmony", typeof(Harmony).Assembly);
            Assembly(xml, "mod", typeof(RuntimeDiagnostics).Assembly);
            // Version.txt is evidence, not an activation gate; known installations report different rev text here.
            string versionFile = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(Def).Assembly.Location), "..", "..", "Version.txt"));
            xml.WriteStartElement("versionFile");
            Attr(xml, "path", versionFile);
            Attr(xml, "text", File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : "unavailable");
            xml.WriteEndElement();
            xml.WriteStartElement("activeMods");
            int order = 0;
            foreach (var mod in LoadedModManager.RunningModsListForReading)
            {
                xml.WriteStartElement("mod");
                Attr(xml, "sequence", order++);
                Attr(xml, "loadOrder", mod.loadOrder);
                Attr(xml, "packageId", mod.PackageId);
                Attr(xml, "name", mod.Name);
                Attr(xml, "root", mod.RootDir);
                Attr(xml, "official", mod.IsOfficialMod);
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
            xml.WriteEndElement();
        }

        private static void Assembly(XmlWriter xml, string role, System.Reflection.Assembly assembly)
        {
            xml.WriteStartElement("assembly");
            Attr(xml, "role", role);
            Attr(xml, "identity", assembly.FullName);
            Attr(xml, "moduleVersionId", assembly.ManifestModule.ModuleVersionId);
            try
            {
                Attr(xml, "path", assembly.Location);
                using (var sha = SHA256.Create())
                using (var input = File.OpenRead(assembly.Location))
                    Attr(xml, "sha256", BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant());
            }
            catch (Exception exception) { Attr(xml, "hashUnavailable", exception.GetType().Name); }
            xml.WriteEndElement();
        }

        private static void Modules(XmlWriter xml)
        {
            xml.WriteStartElement("activation");
            Attr(xml, "bootstrapStatus", RuntimePatchBootstrap.Status);
            Attr(xml, "patchesReady", ReframedMod.PatchesReady);
            Attr(xml, "masterActiveNow", ReframedMod.Active);
            Attr(xml, "note", "Module switches do not alone imply drawing: game, tutorial, map, viewport and event guards still apply. Existing patches stay installed when switches turn off.");
            if (ReframedMod.Settings != null)
                foreach (var field in typeof(ReframedSettings).GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.Name))
                {
                    if (field.FieldType != typeof(bool) && field.FieldType != typeof(float)) continue;
                    xml.WriteStartElement("setting");
                    Attr(xml, "name", field.Name);
                    Attr(xml, "value", field.GetValue(ReframedMod.Settings));
                    xml.WriteEndElement();
                }
            foreach (string guard in RuntimePatchBootstrap.GuardResults) xml.WriteElementString("guard", guard);
            xml.WriteEndElement();
        }

        private static void Surfaces(XmlWriter xml)
        {
            xml.WriteStartElement("patchSurfaces");
            Attr(xml, "ownedHarmonyId", RuntimePatchBootstrap.OwnerId);
            Attr(xml, "guardedSurfaceCount", RuntimePatchBootstrap.OwnedSurfaces.Count);
            foreach (var surface in RuntimePatchBootstrap.OwnedSurfaces)
            {
                xml.WriteStartElement("surface");
                Attr(xml, "signature", surface.DeclaringType.FullName + "." + surface);
                string setting = SurfaceSetting(surface.DeclaringType);
                Attr(xml, "moduleSetting", setting);
                var field = typeof(ReframedSettings).GetField(setting);
                Attr(xml, "moduleSwitchEnabled", ReframedMod.Settings != null && field != null ? field.GetValue(ReframedMod.Settings) : null);
                var patches = Harmony.GetPatchInfo(surface);
                if (patches != null)
                {
                    Attr(xml, "foreignOwnerPresent", patches.Owners.Any(owner => owner != RuntimePatchBootstrap.OwnerId));
                    Patches(xml, "prefix", patches.Prefixes);
                    Patches(xml, "postfix", patches.Postfixes);
                    Patches(xml, "transpiler", patches.Transpilers);
                    Patches(xml, "finalizer", patches.Finalizers);
                }
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }

        private static string SurfaceSetting(Type type)
        {
            if (type == typeof(MainButtonsRoot)) return "CompactDock";
            if (type == typeof(GlobalControls) || type == typeof(LearningReadout)) return "FloatingClock";
            if (type == typeof(ColonistBar) || type == typeof(ColonistBarColonistDrawer)) return "ColonistCards";
            if (type == typeof(ResourceReadout)) return "ResourcePanel";
            if (type == typeof(Alert) || type == typeof(AlertsReadout)) return "AlertCards";
            if (type == typeof(GlobalControlsUtility)) return "CompactTools";
            return "WindowTheme";
        }

        private static void Patches(XmlWriter xml, string kind, IEnumerable<Patch> patches)
        {
            foreach (var patch in patches.OrderBy(patch => patch.index))
            {
                xml.WriteStartElement("patch");
                Attr(xml, "kind", kind);
                Attr(xml, "owner", patch.owner);
                Attr(xml, "priority", patch.priority);
                Attr(xml, "index", patch.index);
                Attr(xml, "before", string.Join(",", patch.before ?? new string[0]));
                Attr(xml, "after", string.Join(",", patch.after ?? new string[0]));
                Attr(xml, "method", patch.PatchMethod.DeclaringType.FullName + "." + patch.PatchMethod.Name);
                Attr(xml, "assembly", patch.PatchMethod.Module.Assembly.FullName);
                xml.WriteEndElement();
            }
        }

        private static void Definitions(XmlWriter xml)
        {
            xml.WriteStartElement("loadedDefinitions");
            Attr(xml, "values", "resolved runtime objects after XML inheritance/patches; original XML ancestry is not reconstructed");
            var letters = DefDatabase<LetterDef>.AllDefsListForReading.OrderBy(def => def.defName, StringComparer.Ordinal).ToList();
            var messages = DefDatabase<MessageTypeDef>.AllDefsListForReading.OrderBy(def => def.defName, StringComparer.Ordinal).ToList();
            var incidents = DefDatabase<IncidentDef>.AllDefsListForReading.OrderBy(def => def.defName, StringComparer.Ordinal).ToList();
            Attr(xml, "letters", letters.Count);
            Attr(xml, "messages", messages.Count);
            Attr(xml, "incidents", incidents.Count);
            Attr(xml, "bespokeMappings", 0);
            foreach (var letter in letters)
            {
                Definition(xml, "LetterDef", letter);
                Mapping(xml, "LetterDef", letter);
                Attr(xml, "letterClass", letter.letterClass?.AssemblyQualifiedName);
                Attr(xml, "colorRgba", ColorValue(letter.color));
                Attr(xml, "flashColorRgba", ColorValue(letter.flashColor));
                Attr(xml, "flashInterval", letter.flashInterval);
                Attr(xml, "bounce", letter.bounce);
                Attr(xml, "arriveSound", letter.arriveSound?.defName);
                Attr(xml, "iconPath", letter.icon);
                Attr(xml, "pauseMode", letter.pauseMode);
                Attr(xml, "forcedSlowdown", letter.forcedSlowdown);
                xml.WriteEndElement();
            }
            foreach (var message in messages)
            {
                Definition(xml, "MessageTypeDef", message);
                Mapping(xml, "MessageTypeDef", message);
                Attr(xml, "sound", message.sound?.defName);
                Attr(xml, "nativeSeverityField", "none; known native identity family only, otherwise unclassified");
                xml.WriteEndElement();
            }
            foreach (var incident in incidents)
            {
                Definition(xml, "IncidentDef", incident);
                Attr(xml, "workerClass", incident.workerClass?.AssemblyQualifiedName);
                Attr(xml, "category", incident.category?.defName);
                Attr(xml, "letterDef", incident.letterDef?.defName);
                Attr(xml, "declaredLetterFamily", incident.letterDef == null ? null : Family("LetterDef", incident.letterDef));
                Attr(xml, "mappingStatus", "event-context-unclassified; declared-letter-is-not-an-event-mapping");
                Attr(xml, "targetTags", incident.targetTags == null ? null : string.Join(",", incident.targetTags.Select(tag => tag.defName)));
                Attr(xml, "questScriptDef", incident.questScriptDef?.defName);
                Attr(xml, "gameCondition", incident.gameCondition?.defName);
                Attr(xml, "diseaseIncident", incident.diseaseIncident?.defName);
                Attr(xml, "populationEffect", incident.populationEffect);
                Attr(xml, "pointsScaleable", incident.pointsScaleable);
                Attr(xml, "hidden", incident.hidden);
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }

        private static void Definition(XmlWriter xml, string kind, Def def)
        {
            xml.WriteStartElement(kind);
            Attr(xml, "id", def.defName);
            Attr(xml, "runtimeType", def.GetType().AssemblyQualifiedName);
            Attr(xml, "sourcePackage", def.modContentPack?.PackageId);
            Attr(xml, "sourceFile", def.fileName);
            Attr(xml, "generated", def.generated);
            Attr(xml, "label", def.label);
        }

        private static string Family(string channel, Def def) => RuntimeNotificationContract.Family(channel, def.defName, def.modContentPack?.PackageId);

        private static void Mapping(XmlWriter xml, string channel, Def def)
        {
            string family = Family(channel, def);
            Attr(xml, "diagnosticFamily", family);
            Attr(xml, "mappingStatus", RuntimeNotificationContract.MappingStatus(family));
            Attr(xml, "semanticBasis", family == null ? "unknown; preserve native definition" : "known core definition identity; not inferred from title or color");
        }

        private static void AlertTypes(XmlWriter xml)
        {
            xml.WriteStartElement("loadedAlertTypes");
            Attr(xml, "coverage", "loaded concrete types, not active alert instances or exhaustive event coverage");
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(assembly => assembly.FullName, StringComparer.Ordinal))
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException exception) { types = exception.Types.Where(type => type != null).ToArray(); }
                catch (Exception) { continue; }
                foreach (var type in types.Where(type => !type.IsAbstract && typeof(Alert).IsAssignableFrom(type)).OrderBy(type => type.FullName, StringComparer.Ordinal))
                {
                    xml.WriteStartElement("alertType");
                    Attr(xml, "type", type.AssemblyQualifiedName);
                    var drawAt = type.GetMethod("DrawAt", new[] { typeof(float), typeof(bool) });
                    Attr(xml, "drawAtDeclaringType", drawAt?.DeclaringType?.FullName);
                    Attr(xml, "baseDrawPatchMayBeBypassed", drawAt != null && drawAt.DeclaringType != typeof(Alert));
                    Attr(xml, "priority", "instance-dependent; not instantiated by discovery");
                    Attr(xml, "mappingStatus", "native-unclassified");
                    xml.WriteEndElement();
                }
            }
            xml.WriteEndElement();
        }

        private static string ColorValue(Color color) => string.Join(",", new[] { color.r, color.g, color.b, color.a }.Select(value => value.ToString("R", CultureInfo.InvariantCulture)));
        private static void Attr(XmlWriter xml, string name, object value) => xml.WriteAttributeString(name, value == null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture));
    }
#endif
}
