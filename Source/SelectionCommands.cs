using System;
#if !SELECTION_CONTRACTS
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
#endif

namespace ReframedUI
{
    // Paint ownership is a nested scope, never a latch across GUI events. The native
    // dispatcher and native Command.GizmoOnGUIInt continue to own every interaction.
    internal sealed class SelectionPaintContext
    {
        internal readonly struct Snapshot
        {
            internal readonly bool Map, Command;
            internal Snapshot(bool map, bool command) { Map = map; Command = command; }
        }

        private bool map, command;
        internal bool Drawing => map && command;

        internal Snapshot EnterMap(bool eligible)
        {
            var previous = new Snapshot(map, command);
            map = eligible;
            command = false;
            return previous;
        }

        internal Snapshot EnterCommand(bool supported)
        {
            var previous = new Snapshot(map, command);
            command = map && supported;
            return previous;
        }

        internal void Restore(Snapshot previous) { map = previous.Map; command = previous.Command; }
    }

#if !SELECTION_CONTRACTS
    internal static class SelectionCommands
    {
        private static readonly SelectionPaintContext context = new SelectionPaintContext();
        internal static bool Drawing => context.Drawing;

        private static bool Eligible => ReframedMod.Active && ReframedMod.Settings.SelectionPanels &&
            ArchitectSeams.FaceReady && Current.ProgramState == ProgramState.Playing &&
            Find.CurrentMap != null && !WorldRendererUtility.WorldSelected && !TutorSystem.TutorialMode;

        internal static SelectionPaintContext.Snapshot EnterMap() => context.EnterMap(Eligible);
        internal static SelectionPaintContext.Snapshot EnterCommand(Command command)
            => context.EnterCommand(Supports(command));
        internal static void Restore(SelectionPaintContext.Snapshot state) => context.Restore(state);

        internal static bool Supports(Command command)
        {
            // A mod-provided subclass may call the base painter while depending on
            // native chrome/label geometry. Its complete drawing remains native.
            // Non-Command gizmos never enter this method. Native Command_Toggle
            // keeps its own checkbox overlay after the native base call returns.
            return command != null && SupportsType(command.GetType());
        }

        internal static bool SupportsType(Type commandType) => commandType != null &&
            commandType.Assembly == typeof(Command).Assembly && typeof(Command).IsAssignableFrom(commandType);
    }

    // Installed metadata audit: MapUIOnGUI is the sole native map-selection caller of
    // GizmoGridDrawer.DrawGizmoGridFor. WorldGizmoUtility has its separate caller.
    // The native method itself rejects non-Inspect main tabs. Do not rebuild its
    // selected-object cache, reverse-designation commands, grouping or input routing.
    [HarmonyPatch(typeof(MapGizmoUtility), nameof(MapGizmoUtility.MapUIOnGUI))]
    internal static class SelectionMapCommandsPatch
    {
        private static void Prefix(out SelectionPaintContext.Snapshot __state)
            => __state = SelectionCommands.EnterMap();

        private static void Finalizer(SelectionPaintContext.Snapshot __state)
            => SelectionCommands.Restore(__state);
    }

    // Add ownership around the existing Architect face/label/icon substitutions;
    // this is a prefix/finalizer only, with no second Command transpiler. Native
    // 75-pixel tiles retain bottom 10-pixel gradient labels through the shared
    // painter; native shrunk commands retain their icon-only rendering/tooltips.
    [HarmonyPatch(typeof(Command), "GizmoOnGUIInt")]
    internal static class SelectionCommandPaintPatch
    {
        private static void Prefix(Command __instance, out SelectionPaintContext.Snapshot __state)
            => __state = SelectionCommands.EnterCommand(__instance);

        private static void Finalizer(SelectionPaintContext.Snapshot __state)
            => SelectionCommands.Restore(__state);
    }
#endif
}
