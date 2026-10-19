using HarmonyLib;
using RimWorld;
using Verse;

namespace ReframedUI
{
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Add))]
    internal static class SaveBrowserRouting
    {
        private static void Prefix(ref Window window)
        {
            // Keep foreign subclasses and disabled-mod behavior intact.
            if (ReframedMod.PatchesReady && ReframedMod.Settings.Enabled && ReframedMod.Settings.MainMenu &&
                window?.GetType() == typeof(Dialog_SaveFileList_Load))
                window = new ReframedSaveBrowser();
        }
    }
}
