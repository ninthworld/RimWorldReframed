using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal static partial class ToolsPanel
    {
        internal static void Prepare(bool eligible)
        {
            // DoPlaySettings also runs in UIRoot_Entry while choosing a world tile.
            // Find.MainTabsRoot casts to UIRoot_Play: even ?. cannot make it safe.
            if (!eligible || Current.ProgramState != ProgramState.Playing || !(Find.UIRoot is UIRoot_Play))
            {
                ResetContext();
                lastMap = null; lastTab = null; lastWorld = false;
                return;
            }

            Map currentMap = Find.CurrentMap;
            bool world = WorldRendererUtility.WorldSelected;
            MainButtonDef currentTab = Find.MainTabsRoot?.OpenTab;
            bool obstructed = Find.WindowStack != null && Find.WindowStack.AnyWindowAbsorbingAllInput;
            if (currentMap == lastMap && !obstructed && currentTab != lastTab && currentTab != null)
                Open(false, "native-tab-opened");
            else if (currentMap != lastMap || world != lastWorld || obstructed)
                ResetContext();
            lastMap = currentMap;
            lastWorld = world;
            lastTab = currentTab;
        }

        private static void ResetContext()
        {
            motion.Reset();
            search.Reset();
            focusSearch = releaseFocus = false;
            focusedControl = "";
            keyboardControl = 0;
            page = outgoingPage = "main";
            searchScroll = Vector2.zero;
            // Never steal focus from native entry pages or another mod.
            if (GUI.GetNameOfFocusedControl() == "Reframed.ToolsSearch") GUI.FocusControl(null);
            lastReason = "context-reset";
        }
    }
}
