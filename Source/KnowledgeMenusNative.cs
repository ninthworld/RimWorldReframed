using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Exact built-in instances only. Private adapters preserve all native quest
    // parts and research requirements rather than duplicating their gameplay.
    internal static class KnowledgeMenusNative
    {
        internal static readonly FieldInfo ResearchSelected = AccessTools.DeclaredField(typeof(MainTabWindow_Research), "selectedProject");
        internal static readonly FieldInfo QuestSelected = AccessTools.DeclaredField(typeof(MainTabWindow_Quests), "selected");
        internal static readonly FieldInfo QuestTab = AccessTools.DeclaredField(typeof(MainTabWindow_Quests), "curTab");
        internal static readonly FieldInfo QuestShowAll = AccessTools.DeclaredField(typeof(MainTabWindow_Quests), "showAll");
        internal static readonly FieldInfo QuestList = AccessTools.DeclaredField(typeof(MainTabWindow_Quests), "tmpQuestsToShow");
        internal static readonly FieldInfo QuestSearch = AccessTools.DeclaredField(typeof(MainTabWindow_Quests), "quickSearchWidget");
        internal static readonly FieldInfo ResearchSearch = AccessTools.DeclaredField(typeof(MainTabWindow_Research), "quickSearchWidget");
        internal static readonly FieldInfo ResearchMatches = AccessTools.DeclaredField(typeof(MainTabWindow_Research), "matchingProjects");
        internal static readonly MethodInfo SearchResearch = AccessTools.DeclaredMethod(typeof(MainTabWindow_Research), "UpdateSearchResults", Type.EmptyTypes);
        internal static readonly MethodInfo BeginResearch = AccessTools.DeclaredMethod(typeof(MainTabWindow_Research), "AttemptBeginResearch", new[] { typeof(ResearchProjectDef) });
        internal static readonly MethodInfo SortQuests = AccessTools.DeclaredMethod(typeof(MainTabWindow_Quests), "SortQuestsByTab", Type.EmptyTypes);
        internal static readonly MethodInfo QuestTime = AccessTools.DeclaredMethod(typeof(MainTabWindow_Quests), "GetShortTimeInfo", new[] { typeof(Quest), typeof(string).MakeByRefType(), typeof(Color).MakeByRefType() });
        private static readonly Dictionary<string, MethodInfo> research = new Dictionary<string, MethodInfo>();
        private static readonly Dictionary<string, MethodInfo> quests = new Dictionary<string, MethodInfo>();
        private static readonly MethodInfo ResearchContents = AccessTools.DeclaredMethod(typeof(MainTabWindow_Research), nameof(Window.DoWindowContents), new[] { typeof(Rect) });
        private static readonly MethodInfo QuestContents = AccessTools.DeclaredMethod(typeof(MainTabWindow_Quests), nameof(Window.DoWindowContents), new[] { typeof(Rect) });
        static KnowledgeMenusNative()
        {
            Type refFloat = typeof(float).MakeByRefType();
            Add(research, typeof(MainTabWindow_Research), "UpdateSelectedProject", typeof(void), typeof(ResearchManager));
            foreach (string method in new[] { "PosX", "PosY" })
                Add(research, typeof(MainTabWindow_Research), method, typeof(float), typeof(ResearchProjectDef));
            foreach (string method in new[] { "DrawSearchRect", "DrawLeftRect" })
                Add(research, typeof(MainTabWindow_Research), method, typeof(void), typeof(Rect));
            Add(research, typeof(MainTabWindow_Research), "DrawRightRect", typeof(void), typeof(Rect), typeof(float));
            Add(research, typeof(MainTabWindow_Research), "DrawStartButton", typeof(void), typeof(Rect));
            Add(research, typeof(MainTabWindow_Research), "DrawTechprintInfo", typeof(void), typeof(Rect), refFloat);
            Add(research, typeof(MainTabWindow_Research), "DrawResearchPrerequisites", typeof(void), typeof(Rect), refFloat, typeof(ResearchProjectDef));
            foreach (string method in new[] { "DrawResearchBenchRequirements", "DrawStudyRequirements", "DrawInspectionRequirements" })
                Add(research, typeof(MainTabWindow_Research), method, typeof(float), typeof(ResearchProjectDef), typeof(Rect));
            Add(research, typeof(MainTabWindow_Research), "DrawUnlockableHyperlinks", typeof(float), typeof(Rect), typeof(Rect), typeof(ResearchProjectDef));
            foreach (string method in new[] { "DrawCustomUnlockables", "DrawContentSource" })
                Add(research, typeof(MainTabWindow_Research), method, typeof(float), typeof(Rect), typeof(ResearchProjectDef));
            foreach (string method in new[] { "DoDismissButton", "DoAcceptButton", "DoOutcomeInfo", "DoDescription", "DoRewards", "DoLookTargets", "DoSelectTargets", "DoDefHyperlinks", "DoFactionInfo", "DoDebugInfoToggle", "DoDebugInfo" })
                Add(quests, typeof(MainTabWindow_Quests), method, typeof(void), typeof(Rect), refFloat);
            Add(quests, typeof(MainTabWindow_Quests), "DoCharityIcon", typeof(void), typeof(Rect));
            Add(quests, typeof(MainTabWindow_Quests), "DoRightAlignedInfo", typeof(void), typeof(Rect), refFloat, typeof(float));
            Add(quests, typeof(MainTabWindow_Quests), "DoAcceptanceRequirementInfo", typeof(void), typeof(Rect), typeof(bool), refFloat);
            Add(quests, typeof(MainTabWindow_Quests), "DoIdeoCharityInfo", typeof(void), typeof(Rect), typeof(bool), refFloat);
        }
        private static void Add(Dictionary<string, MethodInfo> target, Type owner, string name, Type returns, params Type[] parameters)
        {
            MethodInfo method = AccessTools.DeclaredMethod(owner, name, parameters);
            target[name] = method?.ReturnType == returns && !method.IsStatic ? method : null;
        }
        internal static bool ResearchReady => ResearchSelected?.FieldType == typeof(ResearchProjectDef) && BeginResearch?.ReturnType == typeof(void) &&
            ResearchSearch?.FieldType == typeof(QuickSearchWidget) && ResearchMatches?.FieldType == typeof(HashSet<ResearchProjectDef>) && SearchResearch?.ReturnType == typeof(void) && All(research);
        internal static bool QuestsReady => QuestSelected?.FieldType == typeof(Quest) && QuestTabReady && QuestShowAll?.FieldType == typeof(bool) &&
            QuestList?.FieldType == typeof(List<Quest>) && QuestList.IsStatic && QuestSearch?.FieldType == typeof(QuickSearchWidget) && SortQuests?.ReturnType == typeof(void) && QuestTime?.ReturnType == typeof(string) && All(quests);
        private static bool All(Dictionary<string, MethodInfo> methods) { foreach (MethodInfo method in methods.Values) if (method == null) return false; return true; }
        private static bool QuestTabReady => QuestTab?.FieldType.IsEnum == true && Enum.GetUnderlyingType(QuestTab.FieldType) == typeof(byte) &&
            Enum.GetNames(QuestTab.FieldType).SequenceEqual(new[] { "Available", "Active", "Historical" }) &&
            Enum.GetName(QuestTab.FieldType, (byte)0) == "Available" && Enum.GetName(QuestTab.FieldType, (byte)1) == "Active" && Enum.GetName(QuestTab.FieldType, (byte)2) == "Historical";
        internal static bool ContentsCompatible(bool isResearch)
        {
            MethodInfo method = isResearch ? ResearchContents : QuestContents;
            if (method == null) return false;
            var names = isResearch ? new[] { method, AccessTools.DeclaredMethod(typeof(MainTabWindow_Research), "ListProjects"), AccessTools.DeclaredMethod(typeof(MainTabWindow_Research), "DrawRightRect"), AccessTools.DeclaredMethod(typeof(MainTabWindow_Research), "DrawProjectInfo") } : new[] { method };
            foreach (MethodInfo entry in names)
            {
                if (entry == null) return false;
                var patches = Harmony.GetPatchInfo(entry);
                if (patches != null && patches.Owners.Any(owner => owner != RuntimePatchBootstrap.OwnerId)) return false;
            }
            return true;
        }
        internal static ResearchProjectDef Project(MainTabWindow_Research owner) => (ResearchProjectDef)ResearchSelected.GetValue(owner);
        internal static Quest Selected(MainTabWindow_Quests owner) => (Quest)QuestSelected.GetValue(owner);
        internal static void QuestPage(MainTabWindow_Quests owner, int page)
        { QuestTab.SetValue(owner, Enum.ToObject(QuestTab.FieldType, page)); owner.Select(null); }
        internal static List<Quest> QuestRows(MainTabWindow_Quests owner)
        {
            if (DebugSettings.godMode) QuestShowAll.SetValue(owner, true);
            else if (!Prefs.DevMode) QuestShowAll.SetValue(owner, false);
            SortQuests.Invoke(owner, null);
            var candidates = new List<Quest>((List<Quest>)QuestList.GetValue(null));
            return KnowledgeMenusPolicy.Hierarchy(candidates, quest => quest.parent, quest => quest.GetSubquests());
        }
        internal static HashSet<ResearchProjectDef> ResearchResults(MainTabWindow_Research owner, string query)
        {
            ((QuickSearchWidget)ResearchSearch.GetValue(owner)).filter.Text = query;
            SearchResearch.Invoke(owner, null);
            return (HashSet<ResearchProjectDef>)ResearchMatches.GetValue(owner);
        }
        internal static HashSet<ResearchProjectDef> ExistingResearchResults(MainTabWindow_Research owner) => (HashSet<ResearchProjectDef>)ResearchMatches.GetValue(owner);
        internal static Vector2 ResearchPosition(MainTabWindow_Research owner, ResearchProjectDef project)
        {
            // Audited pure native coordinate methods; the surrounding graph scope owns GUI state.
            return new Vector2((float)research["PosX"].Invoke(owner, new object[] { project }), (float)research["PosY"].Invoke(owner, new object[] { project }));
        }
        internal static bool ResearchSearchFocused(MainTabWindow_Research owner) => ((QuickSearchWidget)ResearchSearch.GetValue(owner)).CurrentlyFocused();
        internal static string QuestTimeText(MainTabWindow_Quests owner, Quest quest, out string tooltip, out Color color)
        {
            object[] args = { quest, null, Color.white };
            using (new Theme.GuiScope(true))
            { NativeGui(); string value = (string)QuestTime.Invoke(owner, args); tooltip = (string)args[1]; color = (Color)args[2]; return value; }
        }
        internal static void QuestQuery(MainTabWindow_Quests owner, string query) => ((QuickSearchWidget)QuestSearch.GetValue(owner)).filter.Text = query;
        internal static void ResearchCall(MainTabWindow_Research owner, string method, params object[] args)
        { using (new Theme.GuiScope(true)) { NativeGui(); research[method].Invoke(owner, args); } }
        internal static float ResearchHeight(MainTabWindow_Research owner, string method, params object[] args)
        { using (new Theme.GuiScope(true)) { NativeGui(); return (float)research[method].Invoke(owner, args); } }
        internal static void QuestCall(MainTabWindow_Quests owner, string method, params object[] args)
        { using (new Theme.GuiScope(true)) { NativeGui(); quests[method].Invoke(owner, args); } }
        internal static void NativeGui()
        { Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true; GUI.color = GUI.contentColor = Color.white; }
    }
}
