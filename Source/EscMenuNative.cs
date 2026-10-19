using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal sealed class EscMenuOptions
    {
        internal readonly MainTabWindow_Menu Owner;
        internal readonly Game Game;
        internal readonly bool SavingDisabled, Commitment, AnyGameFiles;
        internal IReadOnlyList<EscMenuOption> Actions { get; }
        internal IReadOnlyList<EscMenuOption> Links { get; }
        internal EscMenuOptions(MainTabWindow_Menu owner, List<ListableOption> actions, List<ListableOption> links, bool anyGameFiles)
        {
            Owner = owner; Game = Verse.Current.Game; SavingDisabled = GameDataSaveLoader.SavingIsTemporarilyDisabled;
            Commitment = Game.Info.permadeathMode; AnyGameFiles = anyGameFiles;
            string[] keys = EscMenuPolicy.ActionKeys(SavingDisabled, Commitment, AnyGameFiles);
            // Only attach presentation roles when the captured native order and
            // current translations agree. Original callbacks remain untouched.
            bool knownOrder = actions.Count == keys.Length;
            for (int i = 0; knownOrder && i < keys.Length; i++)
                knownOrder = string.Equals(actions[i].label, keys[i].Translate().ToString(), StringComparison.Ordinal);
            Actions = Wrap(actions, knownOrder ? keys : null); Links = Wrap(links);
        }
        private EscMenuOption[] Wrap(List<ListableOption> source, string[] keys = null)
        {
            var result = new EscMenuOption[source.Count];
            for (int i = 0; i < result.Length; i++) result[i] = new EscMenuOption(this, source[i], keys?[i]);
            return result;
        }
        internal bool Current => EscMenuNative.ValidOwner(Owner) && ReferenceEquals(Game, Verse.Current.Game) &&
            SavingDisabled == GameDataSaveLoader.SavingIsTemporarilyDisabled && Commitment == Game.Info.permadeathMode &&
            AnyGameFiles == EscMenuNative.AnyGameFiles(Owner);
    }

    internal sealed class EscMenuOption
    {
        private readonly EscMenuOptions owner;
        private readonly string actionKey;
        internal ListableOption Native { get; }
        internal string Label => Native.label ?? "";
        internal string Tooltip => Label;
        internal Texture2D Icon
        {
            get
            {
                if (Native is ListableOption_WebLink web) return web.image ?? MainMenuIcons.Get("ui.external-link");
                switch (actionKey)
                {
                    case "Save": return SaveBrowserIcons.Save;
                    case "LoadGame": return MainMenuIcons.Get("menu.load");
                    case "Options": return MainMenuIcons.Get("menu.cog");
                    case "QuitToMainMenu": case "SaveAndQuitToMainMenu": return SaveBrowserIcons.House;
                    case "QuitToOS": case "SaveAndQuitToOS": return MainMenuIcons.Get("menu.exit");
                    default: return MainMenuIcons.Get("menu.scroll");
                }
            }
        }
        internal Texture2D DrawIcon => Icon;
        internal EscMenuOption(EscMenuOptions owner, ListableOption native, string actionKey = null)
        { this.owner = owner; Native = native; this.actionKey = actionKey; }
        // Root calls this only after its real, settled native GUI button returns true.
        // Native ListableOption buttons add no separate click sound and do not close here.
        internal bool Activate()
        {
            if (!owner.Current || !GUI.enabled || !Find.WindowStack.GetsInput(owner.Owner) || !EscMenuNative.Supported(Native)) return false;
            return EscMenuNative.DispatchOption(Native, Application.OpenURL);
        }
        internal void Highlight(Rect rect)
        {
            if (!owner.Current || !GUI.enabled || !Find.WindowStack.GetsInput(owner.Owner) || Native.GetType() != typeof(ListableOption)) return;
            string tag = EscMenuNative.HighlightTag(Native);
            if (tag != null) UIHighlighter.HighlightOpportunity(rect, tag);
        }
    }

    // Builds the original option objects without drawing or invoking any option.
    // The only native rendering adapter is a scoped prefix at the two listings.
    internal static class EscMenuNative
    {
        private static readonly FieldInfo AnyFiles = AccessTools.DeclaredField(typeof(MainTabWindow_Menu), "anyGameFiles");
        private static readonly FieldInfo WebRect = AccessTools.DeclaredField(typeof(MainMenuDrawer), "webRect");
        private static readonly FieldInfo WebBottom = AccessTools.DeclaredField(typeof(MainMenuDrawer), "webBackgroundYMax");
        private static readonly FieldInfo Highlight = AccessTools.DeclaredField(typeof(ListableOption), "uiHighlightTag");
        private static readonly MethodInfo Controls = AccessTools.DeclaredMethod(typeof(MainMenuDrawer), nameof(MainMenuDrawer.DoMainMenuControls), new[] { typeof(Rect), typeof(bool) });
        private static readonly MethodInfo Listing = AccessTools.DeclaredMethod(typeof(OptionListingUtility), nameof(OptionListingUtility.DrawOptionListing), new[] { typeof(Rect), typeof(List<ListableOption>) });
        private static readonly MethodInfo CapturePrefix = AccessTools.DeclaredMethod(typeof(EscMenuOptionListingPatch), "Prefix");
        [ThreadStatic] private static Capture capture;
        private sealed class Capture
        {
            internal readonly List<List<ListableOption>> Batches = new List<List<ListableOption>>();
            internal bool Invalid;
        }
        internal static bool Ready => AnyFiles?.FieldType == typeof(bool) && WebRect?.FieldType == typeof(Rect) &&
            WebBottom?.FieldType == typeof(float) && Highlight?.FieldType == typeof(string) &&
            Controls?.ReturnType == typeof(void) && Listing?.ReturnType == typeof(float) && CapturePrefix != null;
        internal static bool ForeignPatches(Patches info)
        {
            if (info == null) return false;
            foreach (Patch patch in info.Prefixes) if (patch.PatchMethod != CapturePrefix) return true;
            return info.Postfixes.Count != 0 || info.Transpilers.Count != 0 || info.Finalizers.Count != 0;
        }
        internal static bool AnyGameFiles(MainTabWindow_Menu owner) => (bool)AnyFiles.GetValue(owner);
        internal static string HighlightTag(ListableOption option) => (string)Highlight.GetValue(option);
        internal static bool ValidOwner(MainTabWindow_Menu owner) => Ready && owner != null && owner.GetType() == typeof(MainTabWindow_Menu) &&
            Current.ProgramState == ProgramState.Playing && Current.Game != null && Find.MainTabsRoot != null &&
            ReferenceEquals(Find.MainTabsRoot.OpenTab?.TabWindow, owner) && Find.WindowStack.Windows.Contains(owner);
        internal static bool Supported(ListableOption option)
        {
            if (option == null) return false;
            Type type = option.GetType();
            if (type == typeof(ListableOption)) return option.action != null;
            return type == typeof(ListableOption_WebLink) && (option.action != null || !string.IsNullOrEmpty(((ListableOption_WebLink)option).url));
        }
        // The exact native action-before-URL branch is independently testable
        // with test-owned delegates, without constructing a game or running GUI.
        internal static bool DispatchOption(ListableOption option, Action<string> openUrl)
        {
            if (!Supported(option)) return false;
            if (option.action != null) option.action();
            else
            {
                if (openUrl == null) return false;
                openUrl(((ListableOption_WebLink)option).url);
            }
            return true;
        }
        internal static bool TryCollect(MainTabWindow_Menu owner, out EscMenuOptions options)
        {
            options = null;
            if (!ValidOwner(owner) || capture != null || Event.current == null ||
                ForeignPatches(Harmony.GetPatchInfo(Controls)) || ForeignPatches(Harmony.GetPatchInfo(Listing))) return false;
            var local = new Capture();
            object oldWebRect = WebRect.GetValue(null), oldWebBottom = WebBottom.GetValue(null);
            int keyboard = GUIUtility.keyboardControl, hot = GUIUtility.hotControl;
            bool files = AnyGameFiles(owner);
            try
            {
                capture = local;
                using (new Theme.GuiScope(true))
                {
                    GUI.enabled = false;
                    // No replacement Event, Event.Use or synthetic input. The native
                    // playing branch has no controls outside the intercepted listings.
                    GUI.BeginClip(new Rect(-100000, -100000, 0, 0));
                    try { MainMenuDrawer.DoMainMenuControls(new Rect(0, 0, 450, 390), files); }
                    finally { GUI.EndClip(); }
                }
            }
            catch (Exception)
            {
                // Unsupported option factories/patches retain the complete native route.
                return false;
            }
            finally
            {
                capture = null;
                WebRect.SetValue(null, oldWebRect); WebBottom.SetValue(null, oldWebBottom);
                GUIUtility.keyboardControl = keyboard; GUIUtility.hotControl = hot;
            }
            if (local.Invalid || local.Batches.Count != 2 || local.Batches[0].Count == 0 || !ValidOwner(owner)) return false;
            options = new EscMenuOptions(owner, local.Batches[0], local.Batches[1], files);
            return true;
        }
        internal static bool CaptureListing(List<ListableOption> options, ref float result)
        {
            if (capture == null) return true;
            if (options == null) { capture.Invalid = true; capture.Batches.Add(new List<ListableOption>()); }
            else
            {
                var batch = new List<ListableOption>(options);
                foreach (ListableOption option in batch) if (!Supported(option)) capture.Invalid = true;
                capture.Batches.Add(batch);
            }
            result = 0;
            return false;
        }
    }

    [HarmonyPatch(typeof(OptionListingUtility), nameof(OptionListingUtility.DrawOptionListing), new[] { typeof(Rect), typeof(List<ListableOption>) })]
    internal static class EscMenuOptionListingPatch
    {
        private static bool Prefix(List<ListableOption> __1, ref float __result) => EscMenuNative.CaptureListing(__1, ref __result);
    }
}
