using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Authored, offline-rasterized stock icon family. Mod-added tabs retain their
    // own artwork; unknown iconless tabs receive a quiet generic grid.
    [StaticConstructorOnStartup]
    internal static class DockIcons
    {
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        internal static void Draw(MainButtonDef def, Rect rect, Color tint)
        {
            string stock = StockName(def?.defName);
            Texture2D texture = stock == null ? def?.Icon : Resolve(stock);
            DrawTexture(texture == null ? Resolve("Other") : texture, rect, tint);
        }
        internal static void DrawArchitectPlus(Rect rect, Color tint) => DrawTexture(Resolve("Architect"), rect, tint);

        private static string StockName(string name)
        {
            switch (name)
            {
                case "Ideos": return "Ideoligions";
                case "Work": case "Schedule": case "Assign": case "Animals": case "Wildlife":
                case "Research": case "Quests": case "World": case "Factions": case "Ideoligions": case "History": return name;
                default: return null;
            }
        }
        private static Texture2D Resolve(string name)
        {
            if (!Textures.TryGetValue(name, out Texture2D texture) || texture == null)
                Textures[name] = texture = ContentFinder<Texture2D>.Get("Reframed/Dock/" + name);
            return texture;
        }
        private static void DrawTexture(Texture2D texture, Rect rect, Color tint)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint || rect.width <= 0 || rect.height <= 0) return;
            using (new Theme.GuiScope(true))
            {
                GUI.color = tint;
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            }
        }
    }
}
