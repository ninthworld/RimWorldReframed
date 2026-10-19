using System;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    public sealed class ReframedSettings : ModSettings
    {
        public bool Enabled = true;
        public bool MainMenu = true;
        public bool UseOriginalMenuBackground = true;
        public bool StorytellerSetup = true;
        public bool DlcPanel = true;
        public bool CompactDock = true;
        public bool FloatingClock = true;
        public bool ColonistCards = true;
        public int CollapsedPawnRows = 2;
        public bool ResourcePanel = true;
        public bool AlertCards = true;
        public bool CompactTools = true;
        public bool EscapeMenu = true;
        public bool BuildMenu = true;
        public bool SelectionPanels = true;
        public bool InformationCards = true;
        public bool WorkPanel = true;
        public bool BottomMenus = true;
        public bool WindowTheme = true;
        public float ClockTop = 12f;
        public float PanelOpacity = 0.9f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref Enabled, "enabled", true);
            Scribe_Values.Look(ref MainMenu, "mainMenu", true);
            Scribe_Values.Look(ref UseOriginalMenuBackground, "useOriginalMenuBackground", true);
            Scribe_Values.Look(ref StorytellerSetup, "storytellerSetup", true);
            Scribe_Values.Look(ref DlcPanel, "dlcPanel", true);
            Scribe_Values.Look(ref CompactDock, "compactDock", true);
            Scribe_Values.Look(ref FloatingClock, "floatingClock", true);
            Scribe_Values.Look(ref ColonistCards, "colonistCards", true);
            Scribe_Values.Look(ref CollapsedPawnRows, "collapsedPawnRows", 2);
            Scribe_Values.Look(ref ResourcePanel, "resourcePanel", true);
            Scribe_Values.Look(ref AlertCards, "alertCards", true);
            Scribe_Values.Look(ref CompactTools, "compactTools", true);
            Scribe_Values.Look(ref EscapeMenu, "escapeMenu", true);
            Scribe_Values.Look(ref BuildMenu, "buildMenu", true);
            Scribe_Values.Look(ref SelectionPanels, "selectionPanels", true);
            Scribe_Values.Look(ref InformationCards, "informationCards", true);
            Scribe_Values.Look(ref WorkPanel, "workPanel", true);
            Scribe_Values.Look(ref BottomMenus, "bottomMenus", true);
            Scribe_Values.Look(ref WindowTheme, "windowTheme", true);
            Scribe_Values.Look(ref ClockTop, "clockTop", 12f);
            Scribe_Values.Look(ref PanelOpacity, "panelOpacity", 0.9f);
            ClockTop = Mathf.Clamp(ClockTop, 12f, 240f);
            PanelOpacity = Mathf.Clamp(PanelOpacity, 0.65f, .9f);
            CollapsedPawnRows = Mathf.Clamp(CollapsedPawnRows, 1, 3);
        }
    }

    public sealed class ReframedMod : Mod
    {
        internal static ReframedSettings Settings;
        internal static bool PatchesReady;
        private Vector2 settingsScroll;
        private float settingsHeight = 720f;

        public ReframedMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ReframedSettings>();
            InfoCardPanel.Enabled = () => PatchesReady && Settings != null && Settings.Enabled && Settings.InformationCards;
            ResearchNotice.Enabled = () => AlertCards.Active;
            ResearchNotice.ProtectedTop = () => RightHud.ProtectedTop(320);
            var harmony = new Harmony("ninthworld.reframedui");
            PatchesReady = RuntimePatchBootstrap.TryInitialize(harmony);
            if (PatchesReady)
            {
                SavePreviewStore.Initialize();
                Log.Message("[Reframed UI] v0.23.2 Compact colony menus and research graph loaded.");
            }
        }

        internal static bool Active => PatchesReady && Settings != null && Settings.Enabled
            && Current.ProgramState == ProgramState.Playing;

        public override string SettingsCategory() => "Reframed UI";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect content = new Rect(0f, 0f, Mathf.Max(1f, inRect.width - 20f), settingsHeight);
            Widgets.BeginScrollView(inRect, ref settingsScroll, content);
            try { DrawSettings(content); }
            finally { Widgets.EndScrollView(); }
        }

        private void DrawSettings(Rect inRect)
        {
            var list = new Listing_Standard();
            list.Begin(inRect);
            list.Label("Reframed UI / 0.23.2");
            if (Prefs.DevMode && list.ButtonText("Open foundation lab")) FoundationPreview.Open();
            list.GapLine();
            list.CheckboxLabeled("Enable Reframed UI", ref Settings.Enabled);
            list.CheckboxLabeled("Reworked main menu and save browser", ref Settings.MainMenu);
            list.CheckboxLabeled("Use original main-menu background", ref Settings.UseOriginalMenuBackground);
            list.CheckboxLabeled("New-colony setup menus", ref Settings.StorytellerSetup);
            list.CheckboxLabeled("Expansion selector and information", ref Settings.DlcPanel);
            list.CheckboxLabeled("Compact bottom navigation", ref Settings.CompactDock);
            list.CheckboxLabeled("Floating date and speed panel", ref Settings.FloatingClock);
            list.CheckboxLabeled("Colonist cards and groups", ref Settings.ColonistCards);
            list.Label("Collapsed pawn rows: " + Settings.CollapsedPawnRows);
            Settings.CollapsedPawnRows = Mathf.RoundToInt(list.Slider(Settings.CollapsedPawnRows, 1f, 3f));
            list.Label("Larger rosters scroll; expand shows up to four rows when screen space permits.");
            list.CheckboxLabeled("Resource counts and weekly trends", ref Settings.ResourcePanel);
            list.CheckboxLabeled("Colony issues and notifications", ref Settings.AlertCards);
            list.CheckboxLabeled("Collapsible tools menu", ref Settings.CompactTools);
            list.CheckboxLabeled("Animated Esc menu", ref Settings.EscapeMenu);
            list.CheckboxLabeled("Reworked Architect build menu", ref Settings.BuildMenu);
            list.CheckboxLabeled("Selection panels and action buttons", ref Settings.SelectionPanels);
            list.CheckboxLabeled("Information windows", ref Settings.InformationCards);
            list.CheckboxLabeled("Work tags and priorities", ref Settings.WorkPanel);
            list.CheckboxLabeled("Colony menus and world panels", ref Settings.BottomMenus);
            list.CheckboxLabeled("Dark rounded windows", ref Settings.WindowTheme);
            list.Gap();
            list.Label("Clock distance from top: " + Mathf.RoundToInt(Settings.ClockTop) + " UI pixels");
            Settings.ClockTop = list.Slider(Settings.ClockTop, 12f, 240f);
            list.Label("Panel opacity: " + Mathf.RoundToInt(Settings.PanelOpacity * 100) + "%");
            Settings.PanelOpacity = list.Slider(Settings.PanelOpacity, 0.65f, .9f);
            list.Gap();
            list.Label("Changes apply immediately. Move the clock down if your colonist bar has several rows. " +
                "The floating panel falls back to vanilla in world view, tutorials, and narrow UI layouts. " +
                "Other UI mods may require disabling the feature they also replace.");
            if (!PatchesReady) list.Label("Patches failed to load. See the RimWorld log for details.");
            if (list.ButtonText("Restore defaults"))
            {
                Settings.Enabled = Settings.MainMenu = Settings.CompactDock = Settings.FloatingClock = true;
                Settings.UseOriginalMenuBackground = true;
                Settings.StorytellerSetup = Settings.DlcPanel = true;
                Settings.ColonistCards = Settings.ResourcePanel = Settings.AlertCards = true;
                Settings.CollapsedPawnRows = 2;
                Settings.CompactTools = Settings.EscapeMenu = Settings.WindowTheme = true;
                Settings.BuildMenu = true;
                Settings.BottomMenus = true;
                Settings.SelectionPanels = true;
                Settings.InformationCards = true;
                Settings.WorkPanel = true;
                Settings.ClockTop = 12f;
                Settings.PanelOpacity = 0.9f;
            }
            settingsHeight = list.CurHeight + 12f;
            list.End();
        }
    }
}
