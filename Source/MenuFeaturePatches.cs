using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Keep the native pages and their validation; replace only these presentation surfaces.
    [HarmonyPatch(typeof(StorytellerUI), nameof(StorytellerUI.DrawStorytellerSelectionInterface))]
    internal static class StorytellerSetupPatch
    {
        private static bool failed;

        private static bool Prefix(Rect __0, ref StorytellerDef __1, ref DifficultyDef __2,
            ref Difficulty __3, Listing_Standard __4)
        {
            if (NewGameSetup.NativeEditorDepth > 0 || failed || !ReframedMod.PatchesReady || ReframedMod.Settings == null ||
                !ReframedMod.Settings.Enabled || !ReframedMod.Settings.StorytellerSetup ||
                Current.ProgramState != ProgramState.Entry || !GenScene.InEntryScene) return true;
            try { return !ReframedStoryteller.Draw(__0, ref __1, ref __2, ref __3, __4); }
            catch (Exception error)
            {
                failed = true;
                Log.Error("[Reframed UI] Storyteller presentation failed; native interface resumes next frame. " + error);
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.DoExpansionIcons))]
    internal static class DlcPanelPatch
    {
        private static bool Prefix()
        {
            if (!ReframedMod.PatchesReady || ReframedMod.Settings == null ||
                !ReframedMod.Settings.Enabled || !ReframedMod.Settings.DlcPanel) return true;
            return !ReframedDlcPanel.Draw();
        }
    }
}
