using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Main-tab lifetime, header, motion and background belong to the common shell.
    internal static class WorldMenus
    {
        internal static Func<bool> Enabled = () => false;
        internal static bool WorldDrawing => WorldMenusInspector.Drawing;
        internal static bool CommandDrawing => WorldMenusCommands.Drawing;
        internal static bool Supports(Window window)
        {
            if (window == null) return false;
            if (window.GetType() == typeof(MainTabWindow_Factions))
                return Unmodified(typeof(MainTabWindow_Factions), "PreOpen", "DoWindowContents") && Unmodified(typeof(FactionUIUtility), "DoWindowContents", "DrawFactionRow");
            if (window.GetType() == typeof(MainTabWindow_History))
                return WorldMenusHistory.Supported((MainTabWindow_History)window) && Unmodified(typeof(MainTabWindow_History), "PreOpen", "DoWindowContents", "DoGraphPage", "DoMessagesPage", "DoStatisticsPage", "DoArchivableRow");
            return false;
        }
        internal static bool Unmodified(Type type, params string[] names)
        {
            foreach (string name in names)
            {
                MethodInfo method = AccessTools.Method(type, name);
                if (method == null) return false;
                Patches patches = Harmony.GetPatchInfo(method);
                if (patches != null && patches.Owners.Any(id => id != "ninthworld.reframedui")) return false;
            }
            return true;
        }
        internal static Vector2 PreferredSize(Window window) => window is MainTabWindow_History ? new Vector2(780, 460) : new Vector2(780, 500);
        internal static void Draw(Window window, Rect body)
        {
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                if (window.GetType() == typeof(MainTabWindow_Factions)) WorldMenusFactions.Draw((MainTabWindow_Factions)window, body);
                else if (window.GetType() == typeof(MainTabWindow_History)) WorldMenusHistory.Draw((MainTabWindow_History)window, body);
            }
        }
        internal static void Reset(Window window)
        {
            if (window is MainTabWindow_Factions factions) WorldMenusFactions.Reset(factions);
            if (window is MainTabWindow_History history) WorldMenusHistory.Reset(history);
        }
        internal static bool Button(Rect rect, string text, bool selected = false) => PawnInspectorWidgets.Button(rect, text, null, true, selected);
        internal static void Label(Rect rect, string text, bool strong = false, Color? color = null) => PawnInspectorView.Label(rect, text, color ?? Theme.Ink, strong);
        private static GUIStyle wrapped;
        internal static float Paragraph(Rect rect, string text, bool paint = true)
        {
            if (wrapped == null) wrapped = new GUIStyle(Text.CurFontStyle) { fontSize = 11, wordWrap = true, alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(), margin = new RectOffset(), richText = true };
            float height = Math.Max(15, wrapped.CalcHeight(new GUIContent(text ?? ""), Math.Max(1, rect.width)));
            if (paint)
                using (new Theme.GuiScope(true)) { GUI.color = Theme.Ink; GUI.Label(new Rect(rect.x, rect.y, rect.width, height), text ?? "", wrapped); }
            return height;
        }
        internal static void Surface(Rect rect) => Theme.RoundRect(rect, Theme.Rgb(DesignTokens.Deep).ToTransparent(.48f), 4);
    }
}
