using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ReframedUI
{
    // Context is attached to the actual object, never reconstructed from a title.
    // Weak keys neither own expiry nor retain letters from an old game.
    internal static class NotificationContext
    {
        private sealed class Identity { internal readonly string Incident; internal Identity(string incident) { Incident = incident; } }
        private static readonly ConditionalWeakTable<Letter, Identity> identities = new ConditionalWeakTable<Letter, Identity>();
        [ThreadStatic] private static ElectricalScope current;
        private sealed class ElectricalScope : IDisposable
        {
            private readonly ElectricalScope parent;
            internal readonly LetterDef Definition;
            internal readonly LookTargets Targets;
            internal readonly string Label, Text;
            internal bool Captured;
            internal ElectricalScope(TaggedString label, TaggedString text, LetterDef definition, LookTargets targets)
            {
                // Match the actual native Label/Text setter normalization. This
                // validates a typed call's arguments; it never classifies words.
                Label = label.CapitalizeFirst().RawText; Text = text.CapitalizeFirst().RawText;
                Definition = definition; Targets = targets;
                parent = current; current = this;
            }
            public void Dispose() { current = parent; }
        }
        internal static string Icon(Letter letter)
            => identities.TryGetValue(letter, out Identity identity) ? NotificationPolicy.IncidentIcon(identity.Incident) : null;
        private static void Bind(Letter letter, string incident)
        {
            if (letter == null || string.IsNullOrEmpty(incident)) return;
            identities.Remove(letter); identities.Add(letter, new Identity(incident));
        }
        // Called only at the exact SendIncidentLetter receive call. No ambient
        // scope leaks into the incident's explosions, mod callbacks or children.
        internal static void ReceiveIncident(LetterStack stack, Letter letter, string debug, int delay, bool sound, IncidentDef incident)
        { Bind(letter, incident?.defName); stack.ReceiveLetter(letter, debug, delay, sound); }

        // Calls the original complete overload, preserving any other patches.
        // Scope includes construction+receipt only, and restores even on throw.
        internal static void ReceiveElectrical(LetterStack stack, TaggedString label, TaggedString text, LetterDef definition,
            LookTargets targets, Faction faction, Quest quest, List<ThingDef> hyperlinks, string debug, int delay, bool sound)
        {
            using (new ElectricalScope(label, text, definition, targets)) stack.ReceiveLetter(label, text, definition, targets, faction, quest, hyperlinks, debug, delay, sound);
        }
        internal static void CaptureElectrical(Letter letter)
        {
            // Native LetterMaker assigns the supplied LookTargets reference
            // directly. A nested unrelated receipt must not consume this scope.
            if (current == null || current.Captured || letter == null || letter.GetType() != typeof(StandardLetter) ||
                !ReferenceEquals(letter.def, current.Definition) || !ReferenceEquals(letter.lookTargets, current.Targets) ||
                !string.Equals(letter.Label.RawText, current.Label, StringComparison.Ordinal) ||
                !string.Equals(((ChoiceLetter)letter).Text.RawText, current.Text, StringComparison.Ordinal)) return;
            current.Captured = true; Bind(letter, "ShortCircuit");
        }

        internal static readonly Type[] ObjectReceive = { typeof(Letter), typeof(string), typeof(int), typeof(bool) };
        internal static readonly Type[] TextReceive = { typeof(TaggedString), typeof(TaggedString), typeof(LetterDef), typeof(LookTargets),
            typeof(Faction), typeof(Quest), typeof(List<ThingDef>), typeof(string), typeof(int), typeof(bool) };
        internal static IEnumerable<CodeInstruction> Adapt(IEnumerable<CodeInstruction> instructions, bool incident)
        {
            var original = new List<CodeInstruction>(instructions);
            MethodInfo target = AccessTools.Method(typeof(LetterStack), nameof(LetterStack.ReceiveLetter), incident ? ObjectReceive : TextReceive);
            int calls = 0; foreach (CodeInstruction instruction in original) if (instruction.Calls(target)) calls++;
            if (target == null || calls != 1) return original;
            // Do not move exception boundaries around an injected argument.
            if (incident && original.Exists(instruction => instruction.Calls(target) && instruction.blocks.Count != 0)) return original;
            var result = new List<CodeInstruction>();
            foreach (CodeInstruction source in original)
            {
                var copy = new CodeInstruction(source);
                if (copy.Calls(target))
                {
                    if (incident)
                    {
                        // Branch labels must land before the newly loaded arg.
                        var load = new CodeInstruction(OpCodes.Ldarg_S, (byte)5);
                        load.labels.AddRange(copy.labels); copy.labels.Clear();
                        load.blocks.AddRange(copy.blocks); copy.blocks.Clear();
                        result.Add(load);
                    }
                    copy.opcode = OpCodes.Call;
                    copy.operand = AccessTools.Method(typeof(NotificationContext), incident ? nameof(ReceiveIncident) : nameof(ReceiveElectrical));
                }
                result.Add(copy);
            }
            return result;
        }
    }
    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.SendIncidentLetter))]
    internal static class NotificationIncidentContextPatch
    { internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => NotificationContext.Adapt(instructions, true); }
    [HarmonyPatch(typeof(ShortCircuitUtility), nameof(ShortCircuitUtility.DoShortCircuit), new[] { typeof(Building) })]
    internal static class NotificationShortCircuitContextPatch
    { internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => NotificationContext.Adapt(instructions, false); }
    [HarmonyPatch(typeof(ShortCircuitUtility), nameof(ShortCircuitUtility.TryShortCircuitInRain), new[] { typeof(Thing) })]
    internal static class NotificationRainShortCircuitContextPatch
    { internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => NotificationContext.Adapt(instructions, false); }
    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter), new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) })]
    internal static class NotificationReceiveContextPatch
    { internal static void Prefix(Letter let) => NotificationContext.CaptureElectrical(let); }
}
