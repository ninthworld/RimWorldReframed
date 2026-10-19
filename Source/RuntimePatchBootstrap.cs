using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
#if !RUNTIME_CONTRACTS
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
#endif

namespace ReframedUI
{
    // Shared by the runtime and dependency-free contract tests. Never executes a game member.
    internal static class RuntimeMemberContract
    {
        internal static bool MethodMatches(Type type, string name, Type result, bool isStatic,
            Type[] arguments, string[] argumentNames = null)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            return methods.Any(method => method.Name == name && method.ReturnType == result &&
                method.IsStatic == isStatic && !method.ContainsGenericParameters &&
                method.GetParameters().Select(p => p.ParameterType).SequenceEqual(arguments) &&
                (argumentNames == null || method.GetParameters().Select(p => p.Name).SequenceEqual(argumentNames)));
        }

        internal static bool FieldMatches(Type type, string name, Type fieldType, bool isStatic)
        {
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            return field != null && field.FieldType == fieldType && field.IsStatic == isStatic;
        }
    }

#if !RUNTIME_CONTRACTS
    internal static partial class RuntimePatchBootstrap
    {
        internal const string OwnerId = "ninthworld.reframedui";
        internal static string Status { get; private set; } = "not-attempted";
        internal static readonly List<string> GuardResults = new List<string>();
        internal static readonly List<MethodBase> OwnedSurfaces = new List<MethodBase>();

        // Deliberate allow-list: adding a module also requires a reviewed surface/guard entry.
        private static readonly Type[] PatchTypes = {
            typeof(Dock), typeof(FloatingClock), typeof(LearningPanelBounds), typeof(ColonistCards),
            typeof(ColonistRail), typeof(ColonistGroupHitTest), typeof(ColonistGroupScreenRect), typeof(ColonistGroupAlpha),
            typeof(ColonistCardSelectionOverlay),
            typeof(Resources), typeof(AlertsPanel), typeof(RightHudLettersPatch), typeof(ColonyConditionsPatch),
            typeof(ColonyIssueInfoPatch),
            typeof(NotificationIncidentContextPatch), typeof(NotificationShortCircuitContextPatch),
            typeof(NotificationRainShortCircuitContextPatch), typeof(NotificationReceiveContextPatch),
            typeof(ResearchNoticeCreationPatch), typeof(ResearchNoticePreparePatch), typeof(ResearchNoticeSizePatch),
            typeof(ResearchNoticeMarginPatch), typeof(ResearchNoticeContentsPatch), typeof(ResearchNoticeClosePatch),
            typeof(ToolsMenu), typeof(ToolsSearchFocusPatch), typeof(ToolsBeautyHoverPatch), typeof(ToolsRoomHoverPatch),
            typeof(EscMenuOptionListingPatch), typeof(EscMenuPreparePatch), typeof(EscMenuSizePatch),
            typeof(EscMenuMarginPatch), typeof(EscMenuContentsPatch), typeof(EscMenuClosePatch), typeof(EscMenuCancelPatch),
            typeof(BottomMenuPreparePatch), typeof(BottomMenuContentsPatch), typeof(BottomMenuMarginPatch), typeof(BottomMenuClosePatch),
            typeof(BottomMenuLabelPatch), typeof(BottomMenuTextHeightPatch), typeof(BottomMenuTextSizePatch), typeof(BottomMenuLineHeightPatch),
            typeof(BottomMenuButtonPatch), typeof(BottomMenuSectionPatch), typeof(BottomMenuSelectedPatch), typeof(BottomMenuScrollPatch),
            typeof(BottomMenuNativeSearchPatch), typeof(BottomMenuRewardSurfacePatch), typeof(BottomMenuSearchFocusPatch),
            typeof(BottomMenuChildOwnerPatch), typeof(BottomMenuChildPreparePatch), typeof(BottomMenuChildDrawPatch),
            typeof(BottomMenuChildClosePatch), typeof(BottomMenuChildFadePatch), typeof(BottomMenuChildCloseButtonPatch), typeof(BottomMenuChildTitlePatch),
            typeof(KnowledgeMenusSearchFocusPatch),
            typeof(WorldMenusPreparePatch), typeof(WorldMenusContentsPatch), typeof(WorldMenusExtraPatch), typeof(WorldMenusWidthPatch),
            typeof(WorldMenusClosePatch), typeof(WorldMenusTabRectPatch), typeof(WorldMenusCommandsPatch), typeof(WorldMenusCommandPaintPatch),
            typeof(WorldHudClockPatch),
            typeof(ArchitectContentsPatch), typeof(ArchitectExtrasPatch), typeof(ArchitectMarginPatch),
            typeof(ArchitectWindowPatch), typeof(ArchitectGridPatch), typeof(ArchitectCommandFacePatch), typeof(ArchitectRotationPatch),
            typeof(ArchitectDrawStylePatch), typeof(ArchitectClosePatch), typeof(ArchitectTransitionPatch),
            typeof(ArchitectPopupOwnerPatch), typeof(ArchitectPopupContentsPatch), typeof(ArchitectPopupWidthPatch), typeof(ArchitectPopupOptionPatch),
            typeof(SelectionMapCommandsPatch), typeof(SelectionCommandPaintPatch),
            typeof(SelectionInspectorPreparePatch), typeof(SelectionInspectorSizePatch), typeof(SelectionInspectorMarginPatch),
            typeof(SelectionInspectorContentsPatch), typeof(SelectionInspectorTabsPatch), typeof(SelectionInspectorNativeTabRectPatch),
            typeof(SelectionInspectorPaneWidthPatch), typeof(SelectionInspectorClosePatch),
            typeof(ObjectInspectorDescriptionStringPatch), typeof(ObjectInspectorDescriptionHealthPatch),
            typeof(ObjectInspectorDescriptionBarPatch), typeof(ObjectInspectorDescriptionLabelPatch),
            typeof(ObjectInspectorDescriptionHeightPatch), typeof(ObjectInspectorDescriptionSizePatch),
            typeof(ObjectInspectorDescriptionButtonPatch),
            typeof(PawnSocialRoleButtonPatch), typeof(PawnSocialRomanceButtonPatch),
            typeof(WorkPanelPreparePatch), typeof(WorkPanelOpenPatch), typeof(WorkPanelSizePatch),
            typeof(WorkPanelMarginPatch), typeof(WorkPanelContentsPatch), typeof(WorkPanelClosePatch),
            typeof(WorkPanelCheckboxActivationPatch), typeof(WorkPanelPriorityObservationPatch), typeof(WorkPanelModeObservationPatch),
            typeof(PawnHealthOperationsPatch),
            typeof(PawnBioActionsHeaderPatch), typeof(PawnBioActionsNameOwnerPatch),
            typeof(PawnBioActionsNamePreparePatch), typeof(PawnBioActionsNameSizePatch), typeof(PawnBioActionsNameMarginPatch),
            typeof(PawnBioActionsNameContentsPatch), typeof(PawnBioActionsNameRowsPatch), typeof(PawnBioActionsNameInputPatch),
            typeof(PawnInspectorExtrasHealthMenuPatch), typeof(PawnInspectorExtrasCertaintyPatch),
            typeof(InfoCardWindowPatch), typeof(InfoCardPreparePatch), typeof(InfoCardSizePatch),
            typeof(InfoCardContentsPatch), typeof(InfoCardFillPatch), typeof(InfoCardStatsPatch), typeof(InfoCardHealthSummaryPatch),
            typeof(InfoCardLabelPatch), typeof(InfoCardTextHeightPatch), typeof(InfoCardTextSizePatch),
            typeof(InfoCardLineHeightPatch), typeof(InfoCardButtonFacePatch), typeof(InfoCardSectionPatch),
            typeof(InfoCardSeparatorPatch), typeof(InfoCardSelectedPatch), typeof(InfoCardScrollPatch),
            typeof(InfoCardSearchPatch), typeof(InfoCardSearchRectPatch), typeof(InfoCardMaterialsPatch),
            typeof(InfoCardBackPatch), typeof(InfoCardClosePatch), typeof(InfoCardCloseButtonPatch),
            typeof(InfoCardPermitDispatchPatch), typeof(InfoCardPermitDetailPatch),
            typeof(WindowTheme), typeof(TutorWindowTheme), typeof(ReframedMainMenu),
            typeof(StorytellerSetupPatch), typeof(DlcPanelPatch), typeof(SaveBrowserRouting), typeof(EntryBackgroundPatch),
            typeof(NewGameRegisterPatch), typeof(NewGameCancelPatch), typeof(NewGameSizePatch), typeof(NewGameLayoutPatch), typeof(NewGameMarginPatch), typeof(NewGameUpdatePatch), typeof(NewGameContentsPatch),
            typeof(MenuTooltipMeasurePatch), typeof(MenuTooltipDrawPatch)
        };

        internal static bool TryInitialize(Harmony harmony)
        {
            GuardResults.Clear();
            OwnedSurfaces.Clear();
            if (harmony == null || harmony.Id != OwnerId)
            {
                Status = "rejected-owner-id";
                Log.Error("[Reframed UI] Patch initialization requires owned Harmony ID " + OwnerId + ". Vanilla UI retained.");
                return false;
            }
            try
            {
                // A repeated initialization replaces only our patches; no foreign owner's patches are removed.
                harmony.UnpatchAll(OwnerId);
                VerifyRequiredMembers();
                if (GuardResults.Any(row => row.StartsWith("FAIL ", StringComparison.Ordinal)))
                    throw new MissingMemberException(string.Join("; ", GuardResults.Where(row => row.StartsWith("FAIL ", StringComparison.Ordinal))));

                foreach (var patchType in PatchTypes) harmony.CreateClassProcessor(patchType).Patch();
                foreach (var surface in OwnedSurfaces)
                {
                    var info = Harmony.GetPatchInfo(surface);
                    if (info == null || !info.Owners.Contains(OwnerId))
                        throw new InvalidOperationException("Owned patch absent after initialization: " + surface);
                }
                Status = "ready";
                Log.Message("[Reframed UI] Runtime contracts passed; " + OwnedSurfaces.Count + " owned UI surfaces patched.");
                return true;
            }
            catch (Exception exception)
            {
                Status = "failed-open";
                try { harmony.UnpatchAll(OwnerId); }
                catch (Exception cleanupException)
                {
                    Status = "failed-cleanup";
                    Log.Error("[Reframed UI] Owned patch cleanup failed; master activation remains false. " + cleanupException);
                }
                Log.Error("[Reframed UI] Runtime initialization failed; replacement UI remains inactive. " + exception);
                return false;
            }
        }

        private static void VerifyRequiredMembers()
        {
            Surface(typeof(MainMenuDrawer), "MainMenuOnGUI", typeof(void), true, Type.EmptyTypes);
            Surface(typeof(UI_BackgroundMain), "BackgroundOnGUI", typeof(void), false, Type.EmptyTypes);
            Surface(typeof(MainMenuDrawer), "DoExpansionIcons", typeof(void), true, Type.EmptyTypes);
            Surface(typeof(WindowStack), "Add", typeof(void), false, new[] { typeof(Window) });
            Surface(typeof(WindowStack), "get_AnySearchWidgetFocused", typeof(bool), false, Type.EmptyTypes);
            Surface(typeof(BeautyDrawer), "BeautyDrawerOnGUI", typeof(void), true, Type.EmptyTypes);
            Surface(typeof(EnvironmentStatsDrawer), "EnvironmentStatsOnGUI", typeof(void), true, Type.EmptyTypes);
            Surface(typeof(StorytellerUI), "DrawStorytellerSelectionInterface", typeof(void), true,
                new[] { typeof(Rect), typeof(StorytellerDef).MakeByRefType(), typeof(DifficultyDef).MakeByRefType(),
                    typeof(Difficulty).MakeByRefType(), typeof(Listing_Standard) });
            Method(typeof(MainMenuDrawer), "InitLearnToPlay", typeof(void), true, Type.EmptyTypes);
            Method(typeof(MainMenuDrawer), "DoMainMenuControls", typeof(void), true, new[] { typeof(Rect), typeof(bool) });
            Surface(typeof(OptionListingUtility), "DrawOptionListing", typeof(float), true,
                new[] { typeof(Rect), typeof(List<ListableOption>) });
            Surface(typeof(MainTabWindow_Menu), "get_RequestedTabSize", typeof(Vector2), false, Type.EmptyTypes);
            Surface(typeof(MainTabWindow_Menu), "DoWindowContents", typeof(void), false, new[] { typeof(Rect) });
            Surface(typeof(Window), "OnCancelKeyPressed", typeof(void), false, Type.EmptyTypes);
            Method(typeof(MainTabWindow_Menu), "PreOpen", typeof(void), false, Type.EmptyTypes);
            Method(typeof(MainTabsRoot), "ToggleTab", typeof(void), false, new[] { typeof(MainButtonDef), typeof(bool) });
            Method(typeof(ListableOption), "DrawOption", typeof(float), false, new[] { typeof(Vector2), typeof(float) });
            Method(typeof(ListableOption_WebLink), "DrawOption", typeof(float), false, new[] { typeof(Vector2), typeof(float) });
            Field(typeof(MainTabWindow_Menu), "anyGameFiles", typeof(bool), false);
            Field(typeof(MainMenuDrawer), "webRect", typeof(Rect), true);
            Field(typeof(MainMenuDrawer), "webBackgroundYMax", typeof(float), true);
            Field(typeof(ListableOption), "uiHighlightTag", typeof(string), false);
            Field(typeof(ListableOption), "label", typeof(string), false);
            Field(typeof(ListableOption), "action", typeof(Action), false);
            Field(typeof(ListableOption_WebLink), "image", typeof(Texture2D), false);
            Field(typeof(ListableOption_WebLink), "url", typeof(string), false);
            Surface(typeof(Page), "OnCancelKeyPressed", typeof(void), false, Type.EmptyTypes);
            Surface(typeof(Window), "SetInitialSizeAndPosition", typeof(void), false, Type.EmptyTypes);
            Surface(typeof(Window), "get_Margin", typeof(float), false, Type.EmptyTypes);
            Surface(typeof(Window), "WindowUpdate", typeof(void), false, Type.EmptyTypes);
            Surface(typeof(Window), "WindowOnGUI", typeof(void), false, Type.EmptyTypes);
            Surface(typeof(Page_SelectScenario), "DoWindowContents", typeof(void), false, new[] { typeof(Rect) });
            Surface(typeof(Page_SelectStoryteller), "DoWindowContents", typeof(void), false, new[] { typeof(Rect) });
            Surface(typeof(Page_CreateWorldParams), "DoWindowContents", typeof(void), false, new[] { typeof(Rect) });
            Field(typeof(Scenario), "parts", typeof(List<ScenPart>), false);
            Field(typeof(Page_CreateWorldParams), "seedString", typeof(string), false);
            Field(typeof(Page_CreateWorldParams), "planetCoverage", typeof(float), false);
            Field(typeof(Page_CreateWorldParams), "rainfall", typeof(OverallRainfall), false);
            Field(typeof(Page_CreateWorldParams), "temperature", typeof(OverallTemperature), false);
            Field(typeof(Page_CreateWorldParams), "population", typeof(OverallPopulation), false);
            Field(typeof(Page_CreateWorldParams), "landmarkDensity", typeof(LandmarkDensity), false);
            Field(typeof(Page_CreateWorldParams), "factions", typeof(List<FactionDef>), false);
            Field(typeof(Page_CreateWorldParams), "pollution", typeof(float), false);
            Method(typeof(Page_CreateWorldParams), "Reset", typeof(void), false, Type.EmptyTypes);
            Method(typeof(Page_CreateWorldParams), "ResetFactionCounts", typeof(void), false, Type.EmptyTypes);
            Method(typeof(Page_CreateWorldParams), "CanDoNext", typeof(bool), false, Type.EmptyTypes);
            Field(typeof(Page_SelectScenario), "curScen", typeof(Scenario), false);
            Field(typeof(Page_SelectScenario), "infoScrollPosition", typeof(Vector2), false);
            Field(typeof(Page_SelectStoryteller), "storyteller", typeof(StorytellerDef), false);
            Field(typeof(Page_SelectStoryteller), "difficulty", typeof(DifficultyDef), false);
            Field(typeof(Page_SelectStoryteller), "difficultyValues", typeof(Difficulty), false);
            Field(typeof(Page_SelectStoryteller), "selectedStorytellerInfoListing", typeof(Listing_Standard), false);
            Method(typeof(Page_SelectScenario), "DoScenarioSelectionList", typeof(void), false, new[] { typeof(Rect) });
            Method(typeof(Page_SelectScenario), "GoToScenarioEditor", typeof(void), false, Type.EmptyTypes);
            Method(typeof(Page_SelectScenario), "EnsureValidSelection", typeof(void), false, Type.EmptyTypes);
            Method(typeof(Page_SelectScenario), "CanDoNext", typeof(bool), false, Type.EmptyTypes);
            Method(typeof(Page_SelectStoryteller), "CanDoNext", typeof(bool), false, Type.EmptyTypes);
            Method(typeof(Page), "CanDoBack", typeof(bool), false, Type.EmptyTypes);
            Method(typeof(Page), "DoBack", typeof(void), false, Type.EmptyTypes);
            Method(typeof(Page), "DoNext", typeof(void), false, Type.EmptyTypes);
            Surface(typeof(MainButtonsRoot), "DoButtons", typeof(void), false, Type.EmptyTypes);
            Surface(typeof(GlobalControls), "GlobalControlsOnGUI", typeof(void), false, Type.EmptyTypes);
            Surface(typeof(LearningReadout), "LearningReadoutOnGUI", typeof(void), false, Type.EmptyTypes);
            Surface(typeof(ColonistBarColonistDrawer), "DrawColonist", typeof(void), false,
                new[] { typeof(Rect), typeof(Pawn), typeof(Map), typeof(bool), typeof(bool) },
                new[] { "rect", "colonist", "pawnMap", "highlight", "reordering" });
            Surface(typeof(ColonistBar), "ColonistBarOnGUI", typeof(void), false, Type.EmptyTypes);
            Surface(typeof(ColonistBarColonistDrawer), "DrawSelectionOverlayOnGUI", typeof(void), false,
                new[] { typeof(Pawn), typeof(Rect) }, new[] { "colonist", "rect" });
            Method(typeof(SelectionDrawerUtility), "DrawSelectionOverlayOnGUI", typeof(void), true,
                new[] { typeof(object), typeof(Rect), typeof(float), typeof(float) });
            Surface(typeof(ColonistBar), "TryGetEntryAt", typeof(bool), false,
                new[] { typeof(Vector2), typeof(ColonistBar.Entry).MakeByRefType() });
            Surface(typeof(ColonistBar), "ColonistsOrCorpsesInScreenRect", typeof(List<Thing>), false,
                new[] { typeof(Rect) });
            Surface(typeof(ColonistBar), "GetEntryRectAlpha", typeof(float), false, new[] { typeof(Rect) });
            Surface(typeof(ResourceReadout), "ResourceReadoutOnGUI", typeof(void), false, Type.EmptyTypes);
            Method(typeof(Alert), "DrawAt", typeof(Rect), false, new[] { typeof(float), typeof(bool) }, new[] { "topY", "minimized" });
            Surface(typeof(AlertsReadout), "AlertsReadoutOnGUI", typeof(void), false, Type.EmptyTypes);
            Surface(typeof(LetterStack), "LettersOnGUI", typeof(void), false, new[] { typeof(float) });
            Surface(typeof(GameConditionManager), "DoConditionsUI", typeof(void), false, new[] { typeof(Rect) });
            Surface(typeof(WindowStack), "ImmediateWindow", typeof(void), false,
                new[] { typeof(int), typeof(Rect), typeof(WindowLayer), typeof(Action), typeof(bool), typeof(bool), typeof(float), typeof(Action), typeof(bool) });
            Surface(typeof(IncidentWorker), "SendIncidentLetter", typeof(void), true,
                new[] { typeof(TaggedString), typeof(TaggedString), typeof(LetterDef), typeof(IncidentParms), typeof(LookTargets), typeof(IncidentDef), typeof(NamedArgument[]) });
            Surface(typeof(ShortCircuitUtility), "DoShortCircuit", typeof(void), true, new[] { typeof(Building) });
            Surface(typeof(ShortCircuitUtility), "TryShortCircuitInRain", typeof(bool), true, new[] { typeof(Thing) });
            Surface(typeof(LetterStack), "ReceiveLetter", typeof(void), false,
                new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) });
            Method(typeof(LetterStack), "ReceiveLetter", typeof(void), false,
                new[] { typeof(TaggedString), typeof(TaggedString), typeof(LetterDef), typeof(LookTargets), typeof(Faction), typeof(Quest), typeof(List<ThingDef>), typeof(string), typeof(int), typeof(bool) });
            Method(typeof(LetterStack), "get_LettersListForReading", typeof(List<Letter>), false, Type.EmptyTypes);
            Field(typeof(AlertsReadout), "mouseoverAlertIndex", typeof(int), false);
            Method(typeof(AlertsReadout), "CheckAddOrRemoveAlert", typeof(void), false, new[] { typeof(Alert), typeof(bool) });
            Method(typeof(Alert), "DrawInfoPane", typeof(void), false, Type.EmptyTypes);
            Method(typeof(Alert), "Recalculate", typeof(void), false, Type.EmptyTypes);
            Method(typeof(Alert), "get_Active", typeof(bool), false, Type.EmptyTypes);
            Method(typeof(Alert), "get_Priority", typeof(AlertPriority), false, Type.EmptyTypes);
            Method(typeof(Alert), "get_Label", typeof(string), false, Type.EmptyTypes);
            Field(typeof(LetterStack), "lastTopYInt", typeof(float), false);
            Method(typeof(Letter), "PostProcessedLabel", typeof(string), false, Type.EmptyTypes);
            Method(typeof(Letter), "DrawButtonAt", typeof(void), false, new[] { typeof(float) });
            Method(typeof(Letter), "CheckForMouseOverTextAt", typeof(void), false, new[] { typeof(float) });
            Method(typeof(Letter), "OpenLetter", typeof(void), false, Type.EmptyTypes);
            Method(typeof(Letter), "get_CanDismissWithRightClick", typeof(bool), false, Type.EmptyTypes);
            Method(typeof(IArchivable), "get_ArchivedTooltip", typeof(string), false, Type.EmptyTypes);
            Method(typeof(LetterStack), "get_BundleLetter", typeof(BundleLetter), false, Type.EmptyTypes);
            Method(typeof(BundleLetter), "SetLetters", typeof(void), false, new[] { typeof(List<Letter>) });
            Method(typeof(LetterStack), "RemoveLetter", typeof(void), false, new[] { typeof(Letter) });
            Method(typeof(LetterStack), "Notify_LetterMouseover", typeof(void), false, new[] { typeof(Letter) });
            Field(typeof(Letter), "def", typeof(LetterDef), false);
            Field(typeof(Letter), "arrivalTick", typeof(int), false);
            Field(typeof(Letter), "lookTargets", typeof(LookTargets), false);
            Method(typeof(Letter), "get_Label", typeof(TaggedString), false, Type.EmptyTypes);
            Method(typeof(ChoiceLetter), "get_Text", typeof(TaggedString), false, Type.EmptyTypes);
            Method(typeof(TaggedString), "CapitalizeFirst", typeof(TaggedString), false, Type.EmptyTypes);
            Method(typeof(TaggedString), "get_RawText", typeof(string), false, Type.EmptyTypes);
            Field(typeof(LetterDef), "color", typeof(Color), false);
            Method(typeof(LetterDef), "get_Icon", typeof(Texture2D), false, Type.EmptyTypes);
            Field(typeof(ChoiceLetter), "quest", typeof(Quest), false);
            Method(typeof(GameConditionManager), "get_Parent", typeof(GameConditionManager), false, Type.EmptyTypes);
            Method(typeof(GameConditionManager), "get_ActiveConditions", typeof(List<GameCondition>), false, Type.EmptyTypes);
            Field(typeof(GameConditionManager), "ownerMap", typeof(Map), false);
            Method(typeof(GameCondition), "CanApplyOnMap", typeof(bool), false, new[] { typeof(Map) });
            Method(typeof(GameCondition), "HiddenByOtherCondition", typeof(bool), false, new[] { typeof(Map) });
            Method(typeof(GameCondition), "get_LabelCap", typeof(string), false, Type.EmptyTypes);
            Method(typeof(GameCondition), "get_TooltipString", typeof(string), false, Type.EmptyTypes);
            Surface(typeof(ResearchManager), "FinishProject", typeof(void), false,
                new[] { typeof(ResearchProjectDef), typeof(bool), typeof(Pawn), typeof(bool) });
            Surface(typeof(Dialog_NodeTree), "get_InitialSize", typeof(Vector2), false, Type.EmptyTypes);
            Surface(typeof(Dialog_NodeTree), "DoWindowContents", typeof(void), false, new[] { typeof(Rect) });
            Surface(typeof(Dialog_NodeTree), "PostClose", typeof(void), false, Type.EmptyTypes);
            Method(typeof(Dialog_NodeTree), "get_InteractiveNow", typeof(bool), false, Type.EmptyTypes);
            Method(typeof(DiaOption), "Activate", typeof(void), false, Type.EmptyTypes);
            Field(typeof(Dialog_NodeTree), "curNode", typeof(DiaNode), false);
            Field(typeof(Dialog_NodeTree), "screenFillColor", typeof(Color), false);
            Field(typeof(DiaNode), "options", typeof(List<DiaOption>), false);
            Field(typeof(DiaNode), "text", typeof(TaggedString), false);
            Field(typeof(DiaOption), "dialog", typeof(Window), false);
            Field(typeof(DiaOption), "action", typeof(Action), false);
            Field(typeof(DiaOption), "resolveTree", typeof(bool), false);
            Field(typeof(DiaOption), "disabled", typeof(bool), false);
            Field(typeof(DiaOption), "link", typeof(DiaNode), false);
            Field(typeof(DiaOption), "linkLateBind", typeof(Func<DiaNode>), false);
            Field(typeof(DiaOption), "hyperlink", typeof(Dialog_InfoCard.Hyperlink), false);
            Field(typeof(Window), "doWindowBackground", typeof(bool), false);
            Field(typeof(Window), "doCloseX", typeof(bool), false);
            Field(typeof(Window), "doCloseButton", typeof(bool), false);
            Field(typeof(Window), "windowRect", typeof(Rect), false);
            Surface(typeof(GlobalControlsUtility), "DoPlaySettings", typeof(void), true,
                new[] { typeof(WidgetRow), typeof(bool), typeof(float).MakeByRefType() },
                new[] { "rowVisibility", "worldView", "curBaseY" });
            Surface(typeof(Widgets), "DrawWindowBackground", typeof(void), true, new[] { typeof(Rect) }, new[] { "rect" });
            Surface(typeof(Widgets), "DrawWindowBackgroundTutor", typeof(void), true, new[] { typeof(Rect) }, new[] { "rect" });
            Surface(typeof(ActiveTip), "get_TipRect", typeof(Rect), false, Type.EmptyTypes);
            Surface(typeof(ActiveTip), "DrawInner", typeof(void), false, new[] { typeof(Rect), typeof(string) }, new[] { "bgRect", "label" });
            Method(typeof(ActiveTip), "get_FinalText", typeof(string), false, Type.EmptyTypes);

            Method(typeof(Alert), "OnClick", typeof(void), false, Type.EmptyTypes);
            Method(typeof(ColonistBarColonistDrawer), "DrawIcons", typeof(void), false, new[] { typeof(Rect), typeof(Pawn) });
            Method(typeof(ResourceCounter), "get_AllCountedAmounts", typeof(Dictionary<ThingDef, int>), false, Type.EmptyTypes);
            Method(typeof(ResourceCounter), "ShouldCount", typeof(bool), false, new[] { typeof(Thing) });
            Field(typeof(ColonistBar), "cachedScale", typeof(float), false);
            Field(typeof(ColonistBar), "colonistsToHighlight", typeof(List<Pawn>), false);
            Method(typeof(GlobalControls), "TemperatureString", typeof(string), true, Type.EmptyTypes);
            Method(typeof(GlobalControls), "DoCountdownTimer", typeof(void), true, new[] { typeof(Rect), typeof(TimedDetectionRaids) });
            Method(typeof(TimeControls), "DoTimeControlsGUI", typeof(void), true, new[] { typeof(Rect) });
            Method(typeof(WeatherManager), "DoWeatherGUI", typeof(void), false, new[] { typeof(Rect) });
            Method(typeof(WeatherManager), "get_CurWeatherPerceived", typeof(WeatherDef), false, Type.EmptyTypes);
            Method(typeof(WindowStack), "get_AnyWindowAbsorbingAllInput", typeof(bool), false, Type.EmptyTypes);
            Field(typeof(MainButtonsRoot), "allButtonsInOrder", typeof(List<MainButtonDef>), false);
            Field(typeof(GlobalControls), "rowVisibility", typeof(WidgetRow), false);
            Field(typeof(LearningReadout), "contentHeight", typeof(float), false);
            Field(typeof(AlertsReadout), "activeAlerts", typeof(List<Alert>), false);

            // SELECTION15 owns presentation; native inspect tabs and map commands retain authority.
            Surface(typeof(MapGizmoUtility), nameof(MapGizmoUtility.MapUIOnGUI), typeof(void), true, Type.EmptyTypes);
            Surface(typeof(MainTabWindow_Inspect), "ExtraOnGUI", typeof(void), false, Type.EmptyTypes);
            Surface(typeof(MainTabWindow_Inspect), "get_RequestedTabSize", typeof(Vector2), false, Type.EmptyTypes);
            Surface(typeof(MainTabWindow_Inspect), "get_Margin", typeof(float), false, Type.EmptyTypes);
            Surface(typeof(InspectPaneUtility), "InspectPaneOnGUI", typeof(void), true, new[] { typeof(Rect), typeof(IInspectPane) });
            Surface(typeof(InspectPaneUtility), "DoTabs", typeof(void), true, new[] { typeof(IInspectPane) });
            Surface(typeof(InspectPaneUtility), "PaneWidthFor", typeof(float), true, new[] { typeof(IInspectPane) });
            Surface(typeof(InspectTabBase), "get_TabRect", typeof(Rect), false, Type.EmptyTypes);
            Field(typeof(InspectTabBase), "size", typeof(Vector2), false);
            Field(typeof(InspectTabBase), "labelKey", typeof(string), false);
            Method(typeof(InspectTabBase), "UpdateSize", typeof(void), false, Type.EmptyTypes);
            Method(typeof(InspectTabBase), "FillTab", typeof(void), false, Type.EmptyTypes);
            Method(typeof(InspectTabBase), "ExtraOnGUI", typeof(void), false, Type.EmptyTypes);
            Method(typeof(InspectTabBase), "get_StillValid", typeof(bool), false, Type.EmptyTypes);
            Method(typeof(InspectPaneUtility), "InterfaceToggleTab", typeof(void), true, new[] { typeof(InspectTabBase), typeof(IInspectPane) });
            Method(typeof(MainTabWindow_Inspect), "get_ShouldShowSelectNextInCellButton", typeof(bool), false, Type.EmptyTypes);
            Method(typeof(MainTabWindow_Inspect), "SelectNextInCell", typeof(void), false, Type.EmptyTypes);
            Method(typeof(PawnNeedsUIUtility), "GetThoughtGroupsInDisplayOrder", typeof(void), true, new[] { typeof(Need_Mood), typeof(List<Thought>) });
            Method(typeof(PawnNeedsUIUtility), "GetLeadingThoughtInGroup", typeof(Thought), true, new[] { typeof(List<Thought>) });
            Method(typeof(PawnNeedsUIUtility), "SortInDisplayOrder", typeof(void), true, new[] { typeof(List<Need>) });
            Method(typeof(ThoughtHandler), "GetMoodThoughts", typeof(void), false, new[] { typeof(Thought), typeof(List<Thought>) });
            Method(typeof(ThoughtHandler), "MoodOffsetOfGroup", typeof(float), false, new[] { typeof(Thought) });
            Method(typeof(Thought), "GroupsWith", typeof(bool), false, new[] { typeof(Thought) });
            Method(typeof(Thought), "get_CurStageIndex", typeof(int), false, Type.EmptyTypes);
            Method(typeof(Thought), "get_VisibleInNeedsTab", typeof(bool), false, Type.EmptyTypes);
            Method(typeof(Thought), "get_CausedByBeliefInPrecept", typeof(string), false, Type.EmptyTypes);
            Method(typeof(Need), "get_ShowOnNeedList", typeof(bool), false, Type.EmptyTypes);
            Method(typeof(IdeoUIUtility), "OpenIdeoInfo", typeof(void), true, new[] { typeof(Ideo) });
            Method(typeof(IdeoUtility), "IdeoCausesHumanMeatCravings", typeof(bool), true, new[] { typeof(Ideo) });
            Field(typeof(Thought), "sourcePrecept", typeof(Precept), false);
            Field(typeof(ThoughtDef), "stagesStack", typeof(bool), false);
            Field(typeof(Thought_Memory), "age", typeof(int), false);
            Field(typeof(Verse.AI.Pawn_MindState), "lastHumanMeatIngestedTick", typeof(int), false);
            Method(typeof(NeedsCardUtility), "DrawThoughtListing", typeof(void), true, new[] { typeof(Rect), typeof(Pawn), typeof(Vector2).MakeByRefType() });
            Method(typeof(ITab_Pawn_Gear), "get_SelPawnForGear", typeof(Pawn), false, Type.EmptyTypes);
            Method(typeof(ITab_Pawn_Gear), "get_CanControl", typeof(bool), false, Type.EmptyTypes);
            Method(typeof(ITab_Pawn_Gear), "get_CanControlColonist", typeof(bool), false, Type.EmptyTypes);
            Method(typeof(ITab_Pawn_Gear), "InterfaceDrop", typeof(void), false, new[] { typeof(Thing) });
            Method(typeof(ITab_Pawn_Gear), "InterfaceIngest", typeof(void), false, new[] { typeof(Thing) });
            Method(typeof(ITab_Pawn_Gear), "ShouldShowEquipment", typeof(bool), false, new[] { typeof(Pawn) });
            Method(typeof(ITab_Pawn_Gear), "ShouldShowApparel", typeof(bool), false, new[] { typeof(Pawn) });
            Method(typeof(ITab_Pawn_Gear), "ShouldShowInventory", typeof(bool), false, new[] { typeof(Pawn) });
            Method(typeof(ITab_Pawn_Gear), "ShouldShowOverallArmor", typeof(bool), false, new[] { typeof(Pawn) });
            Method(typeof(MechanitorUtility), "TryConfirmBandwidthLossFromDroppingThing", typeof(bool), true, new[] { typeof(Pawn), typeof(Thing), typeof(Action) });
            Method(typeof(EquipmentUtility), "QuestLodgerCanUnequip", typeof(bool), true, new[] { typeof(Thing), typeof(Pawn) });
            Method(typeof(QuestUtility), "IsQuestLodger", typeof(bool), true, new[] { typeof(Pawn) });
            Method(typeof(FoodUtility), "WillIngestFromInventoryNow", typeof(bool), true, new[] { typeof(Pawn), typeof(Thing) });
            Method(typeof(GenLabel), "ThingLabel", typeof(string), true, new[] { typeof(Thing), typeof(int), typeof(bool), typeof(bool) });
            Method(typeof(ITab_Pawn_Gear), "DrawThingRow", typeof(void), false, new[] { typeof(float).MakeByRefType(), typeof(float), typeof(Thing), typeof(bool) });
            Method(typeof(SocialCardUtility), "PawnsForSocialInfo", typeof(List<Pawn>), true, new[] { typeof(Pawn) });
            Method(typeof(SocialCardUtility), "CheckRecache", typeof(void), true, new[] { typeof(Pawn) });
            Method(typeof(SocialCardUtility), "ShouldShowPawnRelations", typeof(bool), true, new[] { typeof(Pawn), typeof(Pawn) });
            Surface(typeof(SocialCardUtility), "DrawPawnRoleSelection", typeof(void), true, new[] { typeof(Pawn), typeof(Rect) });
            Surface(typeof(SocialCardUtility), "DrawTryRomance", typeof(void), true, new[] { typeof(Rect), typeof(Pawn) });
            Method(typeof(Ideo), "GetRole", typeof(Precept_Role), false, new[] { typeof(Pawn) });
            Method(typeof(Precept_Role), "LabelForPawn", typeof(string), false, new[] { typeof(Pawn) });
            Method(typeof(SocialCardUtility), "RomanceExplanation", typeof(string), true, new[] { typeof(Pawn), typeof(Pawn) });
            Method(typeof(SocialCardUtility), "GetPawnSituationLabel", typeof(string), true, new[] { typeof(Pawn), typeof(Pawn) });
            Method(typeof(SocialCardUtility), "CanDrawTryRomance", typeof(bool), true, new[] { typeof(Pawn) });
            Method(typeof(Widgets), "ButtonText", typeof(bool), true, new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?) });
            Field(typeof(PlayLogEntry_Interaction), "intDef", typeof(InteractionDef), false);
            Field(typeof(PlayLogEntry_InteractionSinglePawn), "intDef", typeof(InteractionDef), false);
            // Inferred character/health/log bodies retain the installed native data and action seams.
            Method(typeof(CharacterCardUtility), "WorkTagsFrom", typeof(IEnumerable<WorkTags>), true, new[] { typeof(WorkTags) });
            Method(typeof(CharacterCardUtility), "GetWorkTypeDisabledCausedBy", typeof(string), true, new[] { typeof(Pawn), typeof(WorkTags) });
            Method(typeof(CharacterCardUtility), "GetWorkTypesDisabledByWorkTag", typeof(string), true, new[] { typeof(WorkTags) });
            Method(typeof(CharacterCardUtility), "GetDisabledWorkTagLabelColor", typeof(Color), true, new[] { typeof(Pawn), typeof(WorkTags) });
            Method(typeof(CharacterCardUtility), "GetTitleTipString", typeof(string), true, new[] { typeof(Pawn), typeof(Faction), typeof(RoyalTitle), typeof(int) });
            Method(typeof(SkillUI), "GetSkillDescription", typeof(string), true, new[] { typeof(SkillRecord) });
            Surface(typeof(CharacterCardUtility), "DrawCharacterCard", typeof(void), true, new[] { typeof(Rect), typeof(Pawn), typeof(Action), typeof(Rect), typeof(bool) });
            Method(typeof(CharacterCardUtility), "PawnCardSize", typeof(Vector2), true, new[] { typeof(Pawn) });
            Surface(typeof(HealthCardUtility), "DrawMedOperationsTab", typeof(float), true, new[] { typeof(Rect), typeof(Pawn), typeof(Thing), typeof(float) });
            Method(typeof(BillStack), "DoListing", typeof(Bill), false, new[] { typeof(Rect), typeof(Func<List<FloatMenuOption>>), typeof(Vector2).MakeByRefType(), typeof(float).MakeByRefType() });
            Method(typeof(ITab_Pawn_Health), "get_PawnForHealth", typeof(Pawn), false, Type.EmptyTypes);
            Method(typeof(ITab_Pawn_Health), "ShouldAllowOperations", typeof(bool), false, Type.EmptyTypes);
            Method(typeof(HealthCardUtility), "VisibleHediffGroupsInOrder", typeof(IEnumerable<IGrouping<BodyPartRecord, Hediff>>), true, new[] { typeof(Pawn), typeof(bool) });
            Method(typeof(HealthCardUtility), "GetTooltip", typeof(string), true, new[] { typeof(Pawn), typeof(BodyPartRecord) });
            Method(typeof(HealthCardUtility), "CanEntryBeClicked", typeof(bool), true, new[] { typeof(IEnumerable<Hediff>), typeof(Pawn) });
            Method(typeof(HealthCardUtility), "EntryClicked", typeof(void), true, new[] { typeof(IEnumerable<Hediff>), typeof(Pawn) });
            Field(typeof(HealthCardUtility), "onOperationTab", typeof(bool), true);
            Field(typeof(HealthCardUtility), "showHediffsDebugInfo", typeof(bool), true);
            Method(typeof(MedicalCareUtility), "MedicalCareIcon", typeof(Texture2D), true, new[] { typeof(MedicalCareCategory) });
            Method(typeof(HealthCardUtility), "ShowBloodLoss", typeof(bool), true, new[] { typeof(Thing) });
            Method(typeof(HealthCardUtility), "GetPainLabel", typeof(Pair<string, Color>), true, new[] { typeof(Pawn) });
            Method(typeof(HealthCardUtility), "GetPainTip", typeof(string), true, new[] { typeof(Pawn) });
            Method(typeof(HealthCardUtility), "GetEfficiencyLabel", typeof(Pair<string, Color>), true, new[] { typeof(Pawn), typeof(PawnCapacityDef) });
            Method(typeof(HealthCardUtility), "GetPawnCapacityTip", typeof(string), true, new[] { typeof(Pawn), typeof(PawnCapacityDef) });
            Method(typeof(PawnCapacityDef), "CanShowOnPawn", typeof(bool), false, new[] { typeof(Pawn) });
            Method(typeof(Hediff), "GetTooltip", typeof(string), false, new[] { typeof(Pawn), typeof(bool) });
            Method(typeof(MedicalCareUtility), "MedicalCareSelectButton", typeof(void), true, new[] { typeof(Rect), typeof(Pawn) });
            Method(typeof(Bill), "DoInterface", typeof(Rect), false, new[] { typeof(float), typeof(float), typeof(float), typeof(int) });
            Method(typeof(BillStack), "Reorder", typeof(void), false, new[] { typeof(Bill), typeof(int) });
            Method(typeof(BillStack), "Delete", typeof(void), false, new[] { typeof(Bill) });
            Field(typeof(ITab_Pawn_Log_Utility.LogLineDisplayableHeader), "text", typeof(TaggedString), false);
            Field(typeof(ITab_Pawn_Log_Utility.LogLineDisplayableLog), "log", typeof(LogEntry), false);
            Field(typeof(ITab_Pawn_Log_Utility.LogLineDisplayableLog), "pawn", typeof(Pawn), false);
            Field(typeof(ITab_Pawn_Log_Utility), "HighlightAlpha", typeof(float), true);
            Field(typeof(ITab_Pawn_Log_Utility), "HighlightDuration", typeof(float), true);
            Method(typeof(ITab_Pawn_Log_Utility), "GenerateLogLinesFor", typeof(List<ITab_Pawn_Log_Utility.LogLineDisplayable>), true,
                new[] { typeof(Pawn), typeof(bool), typeof(bool), typeof(bool), typeof(int) });
            // Window.SetInitialSizeAndPosition and PostClose are already guarded owned surfaces.

            // WORK20 native scheduling, roster, click dispatch and exact Undo observation.
            Method(typeof(RimWorld.PawnColumnWorker_WorkPriority), "IsIncapableOfWholeWorkType", typeof(bool), false, new[] { typeof(Verse.Pawn), typeof(Verse.WorkTypeDef) });
            Method(typeof(RimWorld.PawnColumnWorker_WorkPriority), "HeaderClicked", typeof(void), false, new[] { typeof(UnityEngine.Rect), typeof(RimWorld.PawnTable) });
            Method(typeof(RimWorld.PawnColumnWorker_CopyPasteWorkPriorities), "CopyFrom", typeof(void), false, new[] { typeof(Verse.Pawn) });
            Method(typeof(RimWorld.PawnColumnWorker_CopyPasteWorkPriorities), "PasteTo", typeof(void), false, new[] { typeof(Verse.Pawn) });
            Method(typeof(RimWorld.PawnColumnWorker_CopyPasteWorkPriorities), "get_AnythingInClipboard", typeof(bool), false, Type.EmptyTypes);
            Surface(typeof(RimWorld.WidgetsWork), "DrawWorkBoxFor", typeof(void), true, new[] { typeof(float), typeof(float), typeof(Verse.Pawn), typeof(Verse.WorkTypeDef), typeof(bool) });
            Method(typeof(RimWorld.WidgetsWork), "TipForPawnWorker", typeof(string), true, new[] { typeof(Verse.Pawn), typeof(Verse.WorkTypeDef), typeof(bool) });
            Method(typeof(Verse.Widgets), "ButtonInvisible", typeof(bool), true, new[] { typeof(UnityEngine.Rect), typeof(bool) });
            Surface(typeof(RimWorld.Pawn_WorkSettings), "SetPriority", typeof(void), false, new[] { typeof(Verse.WorkTypeDef), typeof(int) });
            Method(typeof(RimWorld.Pawn_WorkSettings), "GetPriority", typeof(int), false, new[] { typeof(Verse.WorkTypeDef) });
            Method(typeof(RimWorld.Pawn_WorkSettings), "get_Initialized", typeof(bool), false, Type.EmptyTypes);
            Method(typeof(RimWorld.Pawn_WorkSettings), "get_EverWork", typeof(bool), false, Type.EmptyTypes);
            Surface(typeof(RimWorld.Pawn_WorkSettings), "Notify_UseWorkPrioritiesChanged", typeof(void), false, Type.EmptyTypes);
            Method(typeof(RimWorld.PawnTable), "get_PawnsListForReading", typeof(List<>).MakeGenericType(typeof(Verse.Pawn)), false, Type.EmptyTypes);
            Method(typeof(RimWorld.PawnTable), "get_Columns", typeof(List<>).MakeGenericType(typeof(RimWorld.PawnColumnDef)), false, Type.EmptyTypes);
            Method(typeof(RimWorld.PawnTable), "SetDirty", typeof(void), false, Type.EmptyTypes);
            Method(typeof(RimWorld.PawnTable), "SortBy", typeof(void), false, new[] { typeof(RimWorld.PawnColumnDef), typeof(bool) });
            Method(typeof(RimWorld.PawnTable), "get_SortingBy", typeof(RimWorld.PawnColumnDef), false, Type.EmptyTypes);
            Method(typeof(RimWorld.PawnTable), "get_SortingDescending", typeof(bool), false, Type.EmptyTypes);
            Method(typeof(RimWorld.PawnColumnWorker), "DoCell", typeof(void), false, new[] { typeof(UnityEngine.Rect), typeof(Verse.Pawn), typeof(RimWorld.PawnTable) });
            Method(typeof(RimWorld.PawnColumnWorker), "GetMinWidth", typeof(int), false, new[] { typeof(RimWorld.PawnTable) });
            Method(typeof(RimWorld.PawnColumnDef), "get_Worker", typeof(RimWorld.PawnColumnWorker), false, Type.EmptyTypes);
            Method(typeof(RimWorld.Pawn_SkillTracker), "AverageOfRelevantSkillsFor", typeof(float), false, new[] { typeof(Verse.WorkTypeDef) });
            Method(typeof(RimWorld.Pawn_SkillTracker), "MaxPassionOfRelevantSkillsFor", typeof(RimWorld.Passion), false, new[] { typeof(Verse.WorkTypeDef) });
            Method(typeof(Verse.Pawn), "WorkTypeIsDisabled", typeof(bool), false, new[] { typeof(Verse.WorkTypeDef) });
            Method(typeof(Verse.Pawn), "GetReasonsForDisabledWorkType", typeof(List<>).MakeGenericType(typeof(string)), false, new[] { typeof(Verse.WorkTypeDef) });
            Method(typeof(RimWorld.PawnUtility), "IsWorkTypeDisabledByAge", typeof(bool), true, new[] { typeof(Verse.Pawn), typeof(Verse.WorkTypeDef), typeof(int).MakeByRefType() });
            Method(typeof(RimWorld.PawnsFinder), "get_AllMapsWorldAndTemporary_Alive", typeof(List<>).MakeGenericType(typeof(Verse.Pawn)), true, Type.EmptyTypes);
            Field(typeof(RimWorld.MainTabWindow_PawnTable), "table", typeof(RimWorld.PawnTable), false);
            Field(typeof(RimWorld.PawnColumnDef), "workType", typeof(Verse.WorkTypeDef), false);
            Field(typeof(RimWorld.PawnColumnDef), "sortable", typeof(bool), false);
            Field(typeof(RimWorld.PawnColumnDef), "headerTip", typeof(string), false);
            Field(typeof(RimWorld.Pawn_WorkSettings), "priorities", typeof(DefMap<WorkTypeDef, int>), false);
            Field(typeof(Verse.WorkTypeDef), "relevantSkills", typeof(List<>).MakeGenericType(typeof(RimWorld.SkillDef)), false);
            Field(typeof(RimWorld.PlaySettings), "useWorkPriorities", typeof(bool), false);
            Field(typeof(Verse.Game), "playSettings", typeof(RimWorld.PlaySettings), false);
            Field(typeof(Verse.Pawn), "workSettings", typeof(RimWorld.Pawn_WorkSettings), false);
            Field(typeof(Verse.Pawn), "skills", typeof(RimWorld.Pawn_SkillTracker), false);
            Surface(typeof(MainTabWindow_Work), "DoWindowContents", typeof(void), false, new[] { typeof(Rect) });
            Surface(typeof(MainTabWindow_PawnTable), "PostOpen", typeof(void), false, Type.EmptyTypes);
            Surface(typeof(MainTabWindow_PawnTable), "get_RequestedTabSize", typeof(Vector2), false, Type.EmptyTypes);
            Surface(typeof(MainTabWindow_PawnTable), "get_Margin", typeof(float), false, Type.EmptyTypes);
            Field(typeof(WorkTypeDef), "naturalPriority", typeof(int), false);
            Field(typeof(WorkTypeDef), "labelShort", typeof(string), false);

            // INFO19 Bio actions and naming retain native eligibility, callbacks and validation.
            Surface(typeof(Dialog_NamePawn), "DoWindowContents", typeof(void), false, new[] { typeof(Rect) });
            Surface(typeof(Dialog_NamePawn), "get_InitialSize", typeof(Vector2), false, Type.EmptyTypes);
            Surface(typeof(CharacterCardUtility), "DoNameInputRect", typeof(void), true, new[] { typeof(Rect), typeof(string).MakeByRefType(), typeof(int) });
            Type nameContext = PawnBioActionsNaming.Context;
            if (nameContext == null) GuardResults.Add("FAIL Dialog_NamePawn.NameContext absent");
            else
            {
                Surface(nameContext, "MakeRow", typeof(void), false, new[] { typeof(Pawn), typeof(float), typeof(TaggedString), typeof(TaggedString), typeof(RectDivider).MakeByRefType(), typeof(string).MakeByRefType() });
                Field(typeof(Dialog_NamePawn), "names", typeof(List<>).MakeGenericType(nameContext), false);
                Field(nameContext, "labelWidth", typeof(float), false);
                Field(nameContext, "textboxWidth", typeof(float), false);
                Field(nameContext, "textboxName", typeof(string), false);
            }
            Method(typeof(Dialog_NamePawn), "FocusNextControl", typeof(void), false, new[] { typeof(string), typeof(bool) });
            Field(typeof(Dialog_NamePawn), "pawn", typeof(Pawn), false);
            Field(typeof(Dialog_NamePawn), "firstCall", typeof(bool), false);
            Field(typeof(Dialog_NamePawn), "focusControlOverride", typeof(string), false);
            Field(typeof(Dialog_NamePawn), "size", typeof(Vector2), false);
            Field(typeof(Dialog_NamePawn), "descriptionText", typeof(TaggedString), false);
            Method(typeof(CharacterCardUtility), "DoTopStack", typeof(float), true, new[] { typeof(Pawn), typeof(Rect), typeof(bool), typeof(float) });
            Method(typeof(Widgets), "ButtonImage", typeof(bool), true, new[] { typeof(Rect), typeof(Texture2D), typeof(bool), typeof(string) });
            Method(typeof(Widgets), "Label", typeof(void), true, new[] { typeof(Rect), typeof(TaggedString) });
            Method(typeof(Widgets), "TextField", typeof(string), true, new[] { typeof(Rect), typeof(string) });
            // ButtonText, string Label and shared Window/WindowStack surfaces are guarded below/above.

            // INFO19 object descriptions keep the native pane, controls and composed string.
            Surface(typeof(InspectPaneFiller), "DrawInspectString", typeof(void), true, new[] { typeof(string), typeof(Rect) });
            Surface(typeof(InspectPaneFiller), "DrawHealth", typeof(void), true, new[] { typeof(WidgetRow), typeof(Thing) });
            Surface(typeof(WidgetRow), "FillableBar", typeof(Rect), false, new[] { typeof(float), typeof(float), typeof(float), typeof(string), typeof(Texture2D), typeof(Texture2D) });
            Method(typeof(MainTabWindow_Inspect), "DoPaneContents", typeof(void), false, new[] { typeof(Rect) });
            Method(typeof(Widgets), "LabelScrollable", typeof(void), true, new[] { typeof(Rect), typeof(string), typeof(Vector2).MakeByRefType(), typeof(bool), typeof(bool), typeof(bool) });
            Method(typeof(Widgets), "FillableBar", typeof(Rect), true, new[] { typeof(Rect), typeof(float), typeof(Texture2D), typeof(Texture2D), typeof(bool) });
            // Label, CalcHeight, CalcSize and DrawButtonGraphic share guarded surfaces below.

            // INFO18 global information cards: exact owned seams and native data cache.
            Surface(typeof(Window), "InnerWindowOnGUI", typeof(void), false, new[] { typeof(int) });
            Surface(typeof(HealthCardUtility), "DrawHealthSummary", typeof(void), true, new[] { typeof(Rect), typeof(Pawn), typeof(bool), typeof(Thing) });
            Method(typeof(HealthCardUtility), "DrawPawnHealthCard", typeof(void), true, new[] { typeof(Rect), typeof(Pawn), typeof(bool), typeof(bool), typeof(Thing) });
            Method(typeof(Widgets), "ThingIcon", typeof(void), true, new[] { typeof(Rect), typeof(Thing), typeof(float), typeof(Rot4?), typeof(bool), typeof(float), typeof(bool) });
            Method(typeof(Thing), "get_UIIconOverride", typeof(Texture), false, Type.EmptyTypes);
            Method(typeof(Thing), "get_StyleDef", typeof(ThingStyleDef), false, Type.EmptyTypes);
            Method(typeof(ThingStyleDef), "get_UIIcon", typeof(Texture2D), false, Type.EmptyTypes);
            Field(typeof(BuildableDef), "uiIconPath", typeof(string), false);
            Field(typeof(Window), "doCloseButton", typeof(bool), false);
            Method(typeof(Widgets), "GetCheckboxTexture", typeof(Texture2D), true, new[] { typeof(bool) });
            Method(typeof(Widgets), "DrawTextureFitted", typeof(void), true, new[] {
                typeof(Rect), typeof(Texture), typeof(float), typeof(Vector2), typeof(Rect), typeof(float), typeof(Material), typeof(float) });
            Surface(typeof(Verse.Dialog_InfoCard), "get_InitialSize", typeof(UnityEngine.Vector2), false, Type.EmptyTypes);
            Surface(typeof(Verse.Dialog_InfoCard), "DoWindowContents", typeof(void), false, new[] { typeof(UnityEngine.Rect) });
            Surface(typeof(Verse.Dialog_InfoCard), "FillCard", typeof(void), false, new[] { typeof(UnityEngine.Rect) });
            Surface(typeof(RimWorld.StatsReportUtility), "DrawStatsWorker", typeof(void), true, new[] { typeof(UnityEngine.Rect), typeof(Verse.Thing), typeof(RimWorld.Planet.WorldObject) });
            Surface(typeof(Verse.Widgets), "Label", typeof(void), true, new[] { typeof(UnityEngine.Rect), typeof(System.String) });
            Surface(typeof(Verse.Text), "CalcHeight", typeof(System.Single), true, new[] { typeof(System.String), typeof(System.Single) });
            Surface(typeof(Verse.Text), "CalcSize", typeof(UnityEngine.Vector2), true, new[] { typeof(System.String) });
            Surface(typeof(Verse.Text), "get_LineHeight", typeof(System.Single), true, Type.EmptyTypes);
            Surface(typeof(Verse.Widgets), "DrawButtonGraphic", typeof(void), true, new[] { typeof(UnityEngine.Rect) });
            Surface(typeof(Verse.Widgets), "DrawMenuSection", typeof(void), true, new[] { typeof(UnityEngine.Rect) });
            Surface(typeof(Verse.Widgets), "ListSeparator", typeof(void), true, new[] { typeof(System.Single).MakeByRefType(), typeof(System.Single), typeof(System.String) });
            Surface(typeof(Verse.Widgets), "DrawHighlightSelected", typeof(void), true, new[] { typeof(UnityEngine.Rect) });
            Surface(typeof(Verse.Widgets), "BeginScrollView", typeof(void), true, new[] { typeof(UnityEngine.Rect), typeof(UnityEngine.Vector2).MakeByRefType(), typeof(UnityEngine.Rect), typeof(System.Boolean) });
            Surface(typeof(RimWorld.QuickSearchWidget), "OnGUI", typeof(void), false, new[] { typeof(UnityEngine.Rect), typeof(System.Action), typeof(System.Action) });
            Surface(typeof(Verse.Window), "QuickSearchWidgetRect", typeof(UnityEngine.Rect), false, new[] { typeof(UnityEngine.Rect), typeof(UnityEngine.Rect) });
            Surface(typeof(Verse.Dialog_InfoCard), "ShowMaterialsButton", typeof(System.Boolean), true, new[] { typeof(UnityEngine.Rect), typeof(System.Boolean) });
            Surface(typeof(Verse.Widgets), "BackButtonFor", typeof(System.Boolean), true, new[] { typeof(UnityEngine.Rect) });
            Surface(typeof(Verse.Widgets), "CloseButtonFor", typeof(System.Boolean), true, new[] { typeof(UnityEngine.Rect) });
            Method(typeof(Verse.Dialog_InfoCard), "get_ThingPawn", typeof(Verse.Pawn), false, Type.EmptyTypes);
            Method(typeof(RimWorld.StatDrawEntry), "Draw", typeof(System.Single), false, new[] { typeof(System.Single), typeof(System.Single), typeof(System.Single), typeof(System.Boolean), typeof(System.Boolean), typeof(System.Boolean), typeof(System.Action), typeof(System.Action), typeof(UnityEngine.Vector2), typeof(UnityEngine.Rect), typeof(System.String) });
            Method(typeof(RimWorld.StatDrawEntry), "GetExplanationText", typeof(System.String), false, new[] { typeof(RimWorld.StatRequest) });
            Method(typeof(RimWorld.StatDrawEntry), "GetHyperlinks", typeof(System.Collections.Generic.IEnumerable<>).MakeGenericType(typeof(Verse.Dialog_InfoCard.Hyperlink)), false, new[] { typeof(RimWorld.StatRequest) });
            Method(typeof(RimWorld.StatsReportUtility), "SelectEntry", typeof(void), true, new[] { typeof(System.Int32) });
            Surface(typeof(RimWorld.PermitsCardUtility), "DrawRecordsCard", typeof(void), true, new[] { typeof(UnityEngine.Rect), typeof(Verse.Pawn) });
            Surface(typeof(RimWorld.PermitsCardUtility), "DoLeftRect", typeof(void), true, new[] { typeof(UnityEngine.Rect), typeof(Verse.Pawn) });
            Method(typeof(Verse.Widgets), "LabelCacheHeight", typeof(void), true, new[] { typeof(UnityEngine.Rect).MakeByRefType(), typeof(System.String), typeof(System.Boolean), typeof(System.Boolean) });
            Field(typeof(Verse.Dialog_InfoCard), "thing", typeof(Verse.Thing), false);
            Field(typeof(Verse.Dialog_InfoCard), "stuff", typeof(Verse.ThingDef), false);
            Field(typeof(Verse.Dialog_InfoCard), "def", typeof(Verse.Def), false);
            Field(typeof(Verse.Dialog_InfoCard), "tab", AccessTools.Inner(typeof(Dialog_InfoCard), "InfoCardTab"), false);
            Field(typeof(Verse.Dialog_InfoCard), "history", typeof(System.Collections.Generic.List<>).MakeGenericType(typeof(Verse.Dialog_InfoCard.Hyperlink)), true);
            Field(typeof(RimWorld.StatsReportUtility), "cachedDrawEntries", typeof(System.Collections.Generic.List<>).MakeGenericType(typeof(RimWorld.StatDrawEntry)), true);
            Field(typeof(RimWorld.StatsReportUtility), "cachedEntryValues", typeof(System.Collections.Generic.List<>).MakeGenericType(typeof(System.String)), true);
            Field(typeof(RimWorld.StatsReportUtility), "selectedEntry", typeof(RimWorld.StatDrawEntry), true);
            Field(typeof(RimWorld.StatsReportUtility), "mousedOverEntry", typeof(RimWorld.StatDrawEntry), true);
            Field(typeof(RimWorld.StatsReportUtility), "scrollPosition", typeof(UnityEngine.Vector2), true);
            Field(typeof(RimWorld.StatsReportUtility), "scrollPositionRightPanel", typeof(UnityEngine.Vector2), true);
            Field(typeof(RimWorld.StatsReportUtility), "scrollPositioner", typeof(Verse.ScrollPositioner), true);
            Field(typeof(RimWorld.StatsReportUtility), "listHeight", typeof(System.Single), true);
            Field(typeof(RimWorld.StatsReportUtility), "rightPanelHeight", typeof(System.Single), true);
            Field(typeof(Verse.Window), "drawShadow", typeof(System.Boolean), false);
            Field(typeof(PermitsCardUtility), "selectedPermit", typeof(RoyalTitlePermitDef), true);

            // BUILD14 keeps native command dispatch; private layout/search seams are guarded.
            // INFO18 inspector cogs keep native option factories and eligibility.
            Surface(typeof(HealthCardUtility), "DoDebugOptions", typeof(void), true, new[] { typeof(Rect), typeof(Pawn) });
            Surface(typeof(SocialCardUtility), "DrawPawnCertainty", typeof(void), true, new[] { typeof(Pawn), typeof(Rect) });
            Type socialEntry = PawnInspectorExtrasSocial.EntryType;
            if (socialEntry == null) GuardResults.Add("FAIL SocialCardUtility social entry type absent");
            else
            {
                Method(typeof(SocialCardUtility), "DrawPregnancyApproach", typeof(void), true, new[] { socialEntry, typeof(Rect), typeof(Pawn) });
                Field(typeof(SocialCardUtility), "cachedEntries", typeof(List<>).MakeGenericType(socialEntry), true);
                Field(socialEntry, "otherPawn", typeof(Pawn), false);
                Field(socialEntry, "pregnancyApproach", typeof(PregnancyApproach?), false);
            }
            Field(typeof(SocialCardUtility), "showAllRelations", typeof(bool), true);
            Field(typeof(FloatMenu), "options", typeof(List<FloatMenuOption>), false);
            Method(typeof(Widgets), "ButtonText", typeof(bool), true, new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?) });
            Method(typeof(Ideo), "DrawIcon", typeof(void), false, new[] { typeof(Rect) });
            Method(typeof(Widgets), "DrawHighlight", typeof(void), true, new[] { typeof(Rect) });
            Method(typeof(Widgets), "FillableBar", typeof(Rect), true, new[] { typeof(Rect), typeof(float), typeof(Texture2D) });
            Method(typeof(SocialCardUtility), "ClearCaches", typeof(void), true, Type.EmptyTypes);

            Surface(typeof(MainTabWindow_Architect), "DoWindowContents", typeof(void), false, new[] { typeof(Rect) });
            Surface(typeof(MainTabWindow_Architect), "ExtraOnGUI", typeof(void), false, Type.EmptyTypes);
            Surface(typeof(MainTabWindow_Architect), "get_Margin", typeof(float), false, Type.EmptyTypes);
            Surface(typeof(DesignatorUtility), "GUIDoRotationControls", typeof(void), true,
                new[] { typeof(float), typeof(float), typeof(Rot4), typeof(Action<Rot4>) });
            Surface(typeof(Designator), "DoExtraGuiControls", typeof(void), false, new[] { typeof(float), typeof(float) });
            Surface(typeof(Window), "PostClose", typeof(void), false, Type.EmptyTypes);
            Surface(typeof(WindowStack), "WindowStackOnGUI", typeof(void), false, Type.EmptyTypes);
            Method(typeof(Designator), "get_DrawStyleCategory", typeof(DrawStyleCategoryDef), false, Type.EmptyTypes);
            Method(typeof(DesignatorManager), "get_SelectedStyle", typeof(DrawStyleDef), false, Type.EmptyTypes);
            Method(typeof(DesignatorManager), "set_SelectedStyle", typeof(void), false, new[] { typeof(DrawStyleDef) });
            Field(typeof(DrawStyleCategoryDef), "styles", typeof(List<DrawStyleDef>), false);
            Field(typeof(DrawStyleDef), "uiIcon", typeof(Texture2D), false);
            Surface(typeof(FloatMenu), "DoWindowContents", typeof(void), false, new[] { typeof(Rect) });
            Surface(typeof(FloatMenu), "get_ColumnWidth", typeof(float), false, Type.EmptyTypes);
            Surface(typeof(FloatMenuOption), "DoGUI", typeof(bool), false, new[] { typeof(Rect), typeof(bool), typeof(FloatMenu) });
            Method(typeof(Widgets), "DrawAtlas", typeof(void), true, new[] { typeof(Rect), typeof(Texture2D) });
            // WindowOnGUI is already an owned surface above; Architect scopes its second patch by exact window type.
            Surface(typeof(GizmoGridDrawer), "DrawGizmoGrid", typeof(void), true,
                new[] { typeof(IEnumerable<Gizmo>), typeof(float), typeof(Gizmo).MakeByRefType(),
                    typeof(Func<Gizmo, bool>), typeof(Func<Gizmo, bool>), typeof(Func<Gizmo, bool>), typeof(bool) });
            Surface(typeof(Command), "GizmoOnGUIInt", typeof(GizmoResult), false, new[] { typeof(Rect), typeof(GizmoRenderParms) });
            Method(typeof(MainTabWindow_Architect), "OpenTab", typeof(ArchitectCategoryTab), false, Type.EmptyTypes);
            Method(typeof(MainTabWindow_Architect), "ClickedCategory", typeof(void), false, new[] { typeof(ArchitectCategoryTab) });
            Method(typeof(MainTabWindow_Architect), "CacheSearchState", typeof(void), false, Type.EmptyTypes);
            Method(typeof(WindowStack), "GetsInput", typeof(bool), false, new[] { typeof(Window) });
            Field(typeof(Window), "ID", typeof(int), false);
            Field(typeof(FloatMenu), "baseColor", typeof(Color), false);
            Field(typeof(FloatMenu), "scrollPosition", typeof(Vector2), false);
            Method(typeof(Widgets), "BeginScrollView", typeof(void), true,
                new[] { typeof(Rect), typeof(Vector2).MakeByRefType(), typeof(Rect), typeof(bool) });
            Method(typeof(Widgets), "EndScrollView", typeof(void), true, Type.EmptyTypes);
            Field(typeof(Blueprint_Build), "stuffToUse", typeof(ThingDef), false);
            Method(typeof(ArchitectCategoryTab), "ShouldHighLightGizmo", typeof(bool), false, new[] { typeof(Gizmo) });
            Method(typeof(ArchitectCategoryTab), "ShouldLowLightGizmo", typeof(bool), false, new[] { typeof(Gizmo) });
            Method(typeof(Gizmo), "GizmoOnGUI", typeof(GizmoResult), false, new[] { typeof(Vector2), typeof(float), typeof(GizmoRenderParms) });
            Method(typeof(Command), "GizmoOnGUIShrunk", typeof(GizmoResult), false, new[] { typeof(Vector2), typeof(float), typeof(GizmoRenderParms) });
            Method(typeof(Command), "get_LabelCap", typeof(string), false, Type.EmptyTypes);
            Method(typeof(Command), "DrawIcon", typeof(void), false, new[] { typeof(Rect), typeof(Material), typeof(GizmoRenderParms) });
            Method(typeof(GenUI), "IconDrawScale", typeof(float), true, new[] { typeof(ThingDef) });
            Method(typeof(Designator_Build), "get_ThingStyleDefForPreview", typeof(ThingStyleDef), false, Type.EmptyTypes);
            Field(typeof(ThingStyleDef), "uiIconScale", typeof(float), false);
            Method(typeof(Widgets), "Label", typeof(void), true, new[] { typeof(Rect), typeof(string) });
            Field(typeof(TexUI), "GrayTextBG", typeof(Texture2D), true);
            Method(typeof(GenUI), "DrawTextureWithMaterial", typeof(void), true, new[] { typeof(Rect), typeof(Texture), typeof(Material), typeof(Rect) });
            Field(typeof(MainTabWindow_Architect), "desPanelsCached", typeof(List<ArchitectCategoryTab>), false);
            Field(typeof(MainTabWindow_Architect), "quickSearchWidget", typeof(QuickSearchWidget), false);
            Field(typeof(QuickSearchWidget), "controlName", typeof(string), false);
            Method(typeof(QuickSearchWidget), "Focus", typeof(void), false, Type.EmptyTypes);
            Method(typeof(QuickSearchWidget), "Unfocus", typeof(void), false, Type.EmptyTypes);
            Field(typeof(MainTabWindow_Architect), "forceActivatedCommand", typeof(Designator), false);
            Field(typeof(MainTabWindow_Architect), "didInitialUnfocus", typeof(bool), false);
            Field(typeof(GizmoGridDrawer), "heightDrawn", typeof(float), true);
            Field(typeof(GizmoGridDrawer), "heightDrawnFrame", typeof(int), true);
            Field(typeof(GizmoGridDrawer), "GizmoSpacing", typeof(Vector2), true);
            Field(typeof(Command), "BGTex", typeof(Texture2D), true);
            Field(typeof(Command), "BGTexShrunk", typeof(Texture2D), true);
            Field(typeof(UI), "screenWidth", typeof(int), true);
            Field(typeof(UI), "screenHeight", typeof(int), true);
            Method(typeof(UI), "RotateAroundPivot", typeof(void), true, new[] { typeof(float), typeof(Vector2) });
            VerifyBottomMenuMembers();
        }

        private static void Surface(Type type, string name, Type result, bool isStatic, Type[] arguments, string[] argumentNames = null)
        {
            if (Method(type, name, result, isStatic, arguments, argumentNames))
                OwnedSurfaces.Add(type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, arguments, null));
        }

        private static bool Method(Type type, string name, Type result, bool isStatic, Type[] arguments, string[] argumentNames = null)
        {
            bool matches = RuntimeMemberContract.MethodMatches(type, name, result, isStatic, arguments, argumentNames);
            GuardResults.Add((matches ? "PASS " : "FAIL ") + type.FullName + "." + name + "(" +
                string.Join(",", arguments.Select(argument => argument.FullName)) + ") -> " + result.FullName);
            return matches;
        }

        private static void Field(Type type, string name, Type fieldType, bool isStatic)
        {
            bool matches = RuntimeMemberContract.FieldMatches(type, name, fieldType, isStatic);
            GuardResults.Add((matches ? "PASS " : "FAIL ") + type.FullName + "." + name + " : " + fieldType.FullName);
        }
    }
#endif
}
