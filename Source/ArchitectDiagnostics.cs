using System.Linq;
using RimWorld;
using Verse;

namespace ReframedUI
{
    // Read-only state for the existing DevMode + command-line gated fixture.
    // Does not choose a designator, place a blueprint or alter native search.
    internal static class ArchitectDiagnostics
    {
        internal static object State()
        {
            if (!PawnBarDiagnostics.Enabled) return null;
            var window = MainButtonDefOf.Architect.TabWindow as MainTabWindow_Architect;
            Designator selected = Find.DesignatorManager.SelectedDesignator;
            var blueprints = Find.CurrentMap.listerThings.AllThings.OfType<Blueprint>().ToList();
            return PawnBarDiagnostics.D(
                "enabled", ReframedMod.Settings.BuildMenu,
                "window_type", window?.GetType().FullName,
                "open", Find.MainTabsRoot.OpenTab == MainButtonDefOf.Architect,
                "category", window?.selectedDesPanel?.def?.defName,
                "selected_designator", selected?.GetType().FullName,
                "selected_label", selected?.LabelCap.ToString(),
                "blueprint_count", blueprints.Count,
                "blueprints", blueprints.Take(64).Select(blueprint => PawnBarDiagnostics.D(
                    "id", blueprint.ThingID, "def", blueprint.def.defName,
                    "buildable", blueprint.def.entityDefToBuild?.defName,
                    "position", blueprint.Position.ToString(), "rotation", blueprint.Rotation.AsInt,
                    "stuff", (blueprint as Blueprint_Build)?.stuffToUse?.defName ?? blueprint.Stuff?.defName)).ToArray(),
                "layout", ArchitectPanel.AnimationAuditState());
        }
    }
}
