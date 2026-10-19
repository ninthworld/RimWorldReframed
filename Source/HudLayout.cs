using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal static class HudLayout
    {
        internal const float DockLeft = 8f;
        internal const float DockHeight = 30f;
        internal static float DockY => UI.screenHeight - 35f;
        internal static float DockAvailableWidth => Mathf.Max(0f, UI.screenWidth - 67f);
        internal static bool DockEnabled => ReframedMod.Active && ReframedMod.Settings.CompactDock
            && !TutorSystem.TutorialMode && UI.screenWidth >= 640 && UI.screenHeight >= 480;
        internal static Rect MenuButtonBounds => new Rect(UI.screenWidth - 43f, DockY, 31f, 31f);
        internal static Rect ToolsButtonBounds => new Rect(UI.screenWidth - 43f,
            UI.screenHeight - (DockEnabled ? 70f : 35f), 31f, 31f);
        private static readonly List<HudArea> Occupied = new List<HudArea>();

        internal static bool TryClockBounds(float width, float height, out Rect bounds)
        {
            bounds = default(Rect);
            if (!ReframedMod.Active || (Find.CurrentMap == null && !WorldRendererUtility.WorldSelected) ||
                TutorSystem.TutorialMode || UI.screenWidth < 900 || UI.screenHeight < 600) return false;
            float x = UI.screenWidth - width - 12f;
            float y = ReframedMod.Settings.ClockTop + (Prefs.DevMode ? 30f : 0f);
            Occupied.Clear();
            if (LearningPanelBounds.Bottom > 0)
                Occupied.Add(new HudArea(UI.screenWidth - 260f, 0f, 260f, LearningPanelBounds.Bottom));
            if (ColonistGroupsHud.TryGetOccupiedBounds(out Rect groupedColonists))
                Occupied.Add(new HudArea(groupedColonists.x, groupedColonists.y, groupedColonists.width, groupedColonists.height));
            else if (Find.PlaySettings.showColonistBar)
                foreach (var pos in Find.ColonistBar.DrawLocs)
                    Occupied.Add(new HudArea(pos.x, pos.y, Find.ColonistBar.Size.x, Find.ColonistBar.Size.y + 12f));
            // Preserve the lower right ancillary controls and the actual letter/alert stack.
            float bottomLimit = UI.screenHeight - 220f;
            float lettersTop = Find.LetterStack.LastTopY;
            // Grouped right-HUD placement is below the clock; do not create a
            // height feedback loop with its independently animated stack.
            if (!WorldRendererUtility.WorldSelected && !RightHud.CanOwn && lettersTop > 0 && lettersTop <= UI.screenHeight)
            {
                var root = Find.UIRoot as UIRoot_Play;
                float alertsHeight = root?.alerts.AlertsHeight ?? 0f;
                bottomLimit = Mathf.Min(bottomLimit, lettersTop - alertsHeight - 32f);
            }
            if (!HudGeometry.TryPlace(x, y, width, height, bottomLimit, Occupied, out float top)) return false;
            bounds = new Rect(x, top, width, height);
            return true;
        }

        internal static void DrawMenuButton()
        {
            if (!DockEnabled) return;
            // The native Menu window paints and handles its own launcher last.
            if (EscMenuPanel.OwnsWindow) return;
            using (new Theme.GuiScope(true))
            {
                Rect rect = MenuButtonBounds;
                bool blocked = Find.WindowStack.AnyWindowAbsorbingAllInput;
                GUI.enabled = GUI.enabled && !blocked;
                HudControls.Face(rect, !blocked && Mouse.IsOver(rect), EscMenuPanel.Selected, blocked);
                HudControls.MenuGlyph(rect, blocked, EscMenuPanel.Selected);
                string menuKey = KeyBindingDefOf.Cancel.MainKey == KeyCode.None ? "" : KeyBindingDefOf.Cancel.MainKeyLabel;
                HudControls.Shortcut(rect, menuKey == "Escape" ? "Esc" : menuKey, blocked);
                if (!blocked && Mouse.IsOver(rect)) TooltipHandler.TipRegion(rect, "Menu".Translate() +
                    (string.IsNullOrEmpty(menuKey) ? "" : " (" + menuKey + ")"));
                if (!blocked && Widgets.ButtonInvisible(rect))
                    Find.MainTabsRoot.ToggleTab(MainButtonDefOf.Menu);
                GenUI.AbsorbClicksInRect(rect);
            }
        }
    }
}
