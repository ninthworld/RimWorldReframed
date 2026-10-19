using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal static partial class RuntimePatchBootstrap
    {
        private static void VerifyBottomMenuMembers()
        {
            // The same inherited native method can serve several main tabs.
            // Guard each actual declaring member once, without owning duplicates.
            foreach (var member in BottomMenuShell.ContentsMethods.Concat(BottomMenuShell.MarginMethods).Distinct())
            {
                if (member == null) { GuardResults.Add("FAIL bottom menu entry member missing"); continue; }
                var method = (System.Reflection.MethodInfo)member;
                if (OwnedSurfaces.Contains(member)) continue;
                Surface(method.DeclaringType, method.Name, method.ReturnType, method.IsStatic,
                    method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
            }
            Surface(typeof(FloatMenu), "UpdateBaseColor", typeof(void), false, Type.EmptyTypes);
            Surface(typeof(FloatMenu), "ExtraOnGUI", typeof(void), false, Type.EmptyTypes);
            Field(typeof(FloatMenu), "title", typeof(string), false);
            Surface(typeof(Widgets), "DrawBoxSolid", typeof(void), true, new[] { typeof(Rect), typeof(Color) });
            Surface(typeof(WorldInspectPane), "DoWindowContents", typeof(void), false, new[] { typeof(Rect) });
            Surface(typeof(InspectPaneUtility), "ExtraOnGUI", typeof(void), true, new[] { typeof(IInspectPane) });
            Surface(typeof(WorldGizmoUtility), "WorldUIOnGUI", typeof(void), true, Type.EmptyTypes);
            Surface(typeof(WorldGlobalControls), "WorldGlobalControlsOnGUI", typeof(void), false, Type.EmptyTypes);
            Method(typeof(GlobalControlsUtility), "DoTimespeedControls", typeof(void), true, new[] { typeof(float), typeof(float), typeof(float).MakeByRefType() });
            Method(typeof(GlobalControlsUtility), "DoDate", typeof(void), true, new[] { typeof(float), typeof(float), typeof(float).MakeByRefType() });

            Method(typeof(PawnColumnWorker_Label), "GetLabel", typeof(TaggedString), false, new[] { typeof(Pawn) });
            Method(typeof(PawnColumnWorker), "DoHeader", typeof(void), false, new[] { typeof(Rect), typeof(PawnTable) });
            Method(typeof(PawnColumnWorker), "HeaderClicked", typeof(void), false, new[] { typeof(Rect), typeof(PawnTable) });
            Method(typeof(PawnColumnWorker), "GetHeaderTip", typeof(string), false, new[] { typeof(PawnTable) });
            Method(typeof(PawnColumnDef), "get_HeaderIcon", typeof(Texture2D), false, Type.EmptyTypes);
            Method(typeof(PawnColumnDef), "get_HeaderInteractable", typeof(bool), false, Type.EmptyTypes);
            Method(typeof(TimeAssignmentSelector), "DrawTimeAssignmentSelectorFor", typeof(void), true, new[] { typeof(Rect), typeof(TimeAssignmentDef) });
            foreach (string name in new[] { "GetOptimalWidth", "GetMaxWidth", "GetMinHeaderHeight" })
                Method(typeof(PawnColumnWorker), name, typeof(int), false, new[] { typeof(PawnTable) });
            Method(typeof(PawnColumnWorker), "GetMinCellHeight", typeof(int), false, new[] { typeof(Pawn) });
            Method(typeof(TimeAssignmentSelector), "DrawTimeAssignmentSelectorGrid", typeof(void), true, new[] { typeof(Rect) });

            Field(typeof(MainTabWindow_Research), "selectedProject", typeof(ResearchProjectDef), false);
            Field(typeof(MainTabWindow_Research), "quickSearchWidget", typeof(QuickSearchWidget), false);
            Field(typeof(MainTabWindow_Research), "matchingProjects", typeof(HashSet<ResearchProjectDef>), false);
            Field(typeof(MainTabWindow_Quests), "selected", typeof(Quest), false);
            Field(typeof(MainTabWindow_Quests), "showAll", typeof(bool), false);
            Field(typeof(MainTabWindow_Quests), "quickSearchWidget", typeof(QuickSearchWidget), false);
            Field(typeof(MainTabWindow_Quests), "tmpQuestsToShow", typeof(List<Quest>), true);
            Type questTab = AccessTools.Inner(typeof(MainTabWindow_Quests), "QuestsTab");
            if (questTab == null) GuardResults.Add("FAIL QuestsTab absent");
            else Field(typeof(MainTabWindow_Quests), "curTab", questTab, false);
            Method(typeof(MainTabWindow_Research), "AttemptBeginResearch", typeof(void), false, new[] { typeof(ResearchProjectDef) });
            Method(typeof(MainTabWindow_Research), "UpdateSelectedProject", typeof(void), false, new[] { typeof(ResearchManager) });
            foreach (string name in new[] { "PosX", "PosY" })
                Method(typeof(MainTabWindow_Research), name, typeof(float), false, new[] { typeof(ResearchProjectDef) });
            Method(typeof(MainTabWindow_Research), "UpdateSearchResults", typeof(void), false, Type.EmptyTypes);
            Method(typeof(MainTabWindow_Research), "DrawStartButton", typeof(void), false, new[] { typeof(Rect) });
            Method(typeof(MainTabWindow_Research), "DrawSearchRect", typeof(void), false, new[] { typeof(Rect) });
            Method(typeof(MainTabWindow_Research), "DrawLeftRect", typeof(void), false, new[] { typeof(Rect) });
            Method(typeof(MainTabWindow_Research), "DrawRightRect", typeof(void), false, new[] { typeof(Rect), typeof(float) });
            Method(typeof(MainTabWindow_Research), "DrawTechprintInfo", typeof(void), false, new[] { typeof(Rect), typeof(float).MakeByRefType() });
            Method(typeof(MainTabWindow_Research), "DrawResearchPrerequisites", typeof(void), false, new[] { typeof(Rect), typeof(float).MakeByRefType(), typeof(ResearchProjectDef) });
            foreach (string name in new[] { "DrawResearchBenchRequirements", "DrawStudyRequirements", "DrawInspectionRequirements" })
                Method(typeof(MainTabWindow_Research), name, typeof(float), false, new[] { typeof(ResearchProjectDef), typeof(Rect) });
            Method(typeof(MainTabWindow_Research), "DrawUnlockableHyperlinks", typeof(float), false, new[] { typeof(Rect), typeof(Rect), typeof(ResearchProjectDef) });
            foreach (string name in new[] { "DrawCustomUnlockables", "DrawContentSource" })
                Method(typeof(MainTabWindow_Research), name, typeof(float), false, new[] { typeof(Rect), typeof(ResearchProjectDef) });
            Method(typeof(MainTabWindow_Quests), "SortQuestsByTab", typeof(void), false, Type.EmptyTypes);
            Method(typeof(MainTabWindow_Quests), "GetShortTimeInfo", typeof(string), false, new[] { typeof(Quest), typeof(string).MakeByRefType(), typeof(Color).MakeByRefType() });
            foreach (string name in new[] { "DoDismissButton", "DoAcceptButton", "DoOutcomeInfo", "DoDescription", "DoRewards", "DoLookTargets", "DoSelectTargets", "DoDefHyperlinks", "DoFactionInfo", "DoDebugInfoToggle", "DoDebugInfo" })
                Method(typeof(MainTabWindow_Quests), name, typeof(void), false, new[] { typeof(Rect), typeof(float).MakeByRefType() });
            Method(typeof(MainTabWindow_Quests), "DoCharityIcon", typeof(void), false, new[] { typeof(Rect) });
            Method(typeof(MainTabWindow_Quests), "DoRightAlignedInfo", typeof(void), false, new[] { typeof(Rect), typeof(float).MakeByRefType(), typeof(float) });
            foreach (string name in new[] { "DoAcceptanceRequirementInfo", "DoIdeoCharityInfo" })
                Method(typeof(MainTabWindow_Quests), name, typeof(void), false, new[] { typeof(Rect), typeof(bool), typeof(float).MakeByRefType() });

            Field(typeof(MainTabWindow_Factions), "scrollToFaction", typeof(Faction), false);
            foreach (string name in new[] { "GetOngoingEvents", "GetRecentEvents", "GetNaturalGoodwillExplanation" })
                Method(typeof(FactionUIUtility), name, typeof(TaggedString), true, new[] { typeof(Faction) });
            Field(typeof(MainTabWindow_History), "historyAutoRecorderGroup", typeof(HistoryAutoRecorderGroup), false);
            Field(typeof(MainTabWindow_History), "graphSection", typeof(FloatRange), false);
            Type historyTab = AccessTools.Inner(typeof(MainTabWindow_History), "HistoryTab");
            if (historyTab == null) GuardResults.Add("FAIL HistoryTab absent");
            else Field(typeof(MainTabWindow_History), "curTab", historyTab, true);
            Field(typeof(MainTabWindow_History), "showLetters", typeof(bool), true);
            Field(typeof(MainTabWindow_History), "showMessages", typeof(bool), true);
            Field(typeof(MainTabWindow_History), "tabs", typeof(List<TabRecord>), false);
            Field(typeof(MainTabWindow_History), "quickSearchWidget", typeof(QuickSearchWidget), true);
            Field(typeof(HistoryAutoRecorderGroup), "recorders", typeof(List<HistoryAutoRecorder>), false);
            Field(typeof(HistoryAutoRecorderGroup), "def", typeof(HistoryAutoRecorderGroupDef), false);
            Field(typeof(HistoryAutoRecorder), "records", typeof(List<float>), false);
            Field(typeof(HistoryAutoRecorder), "def", typeof(HistoryAutoRecorderDef), false);
            Field(typeof(HistoryAutoRecorderDef), "recordTicksFrequency", typeof(int), false);
            Field(typeof(HistoryAutoRecorderDef), "graphColor", typeof(Color), false);
            Field(typeof(HistoryAutoRecorderDef), "valueFormat", typeof(string), false);
            foreach (string name in new[] { "useFixedScale", "integersOnly", "onlyPositiveValues" })
                Field(typeof(HistoryAutoRecorderGroupDef), name, typeof(bool), false);
            Field(typeof(HistoryAutoRecorderGroupDef), "fixedScale", typeof(Vector2), false);
            Method(typeof(CurveMark), "get_X", typeof(float), false, Type.EmptyTypes);
            Method(typeof(CurveMark), "get_Message", typeof(string), false, Type.EmptyTypes);
            Method(typeof(CurveMark), "get_Color", typeof(Color), false, Type.EmptyTypes);
            Method(typeof(SimpleCurveDrawerStyle), "get_LabelX", typeof(string), false, Type.EmptyTypes);
            Field(typeof(InspectTabBase), "size", typeof(Vector2), false);
            foreach (string name in new[] { "FillTab", "ExtraOnGUI", "UpdateSize" })
                Method(typeof(InspectTabBase), name, typeof(void), false, Type.EmptyTypes);
            Method(typeof(InspectTabBase), "get_StillValid", typeof(bool), false, Type.EmptyTypes);
            Method(typeof(InspectPaneUtility), "InterfaceToggleTab", typeof(void), true, new[] { typeof(InspectTabBase), typeof(IInspectPane) });
        }
    }
}
