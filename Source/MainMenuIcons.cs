using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    /// <summary>Static source-family menu images. Draw with white to retain their slate/pink colors.</summary>
    [StaticConstructorOnStartup]
    internal static class MainMenuIcons
    {
        private static readonly Dictionary<string, CachedIcon> Images = new Dictionary<string, CachedIcon>(System.StringComparer.Ordinal)
        {
            { "menu.plus", new CachedIcon("Reframed/MainMenu/Plus") },
            { "menu.load", new CachedIcon("Reframed/MainMenu/Load") },
            { "menu.scroll", new CachedIcon("Reframed/MainMenu/Scroll") },
            { "menu.cog", new CachedIcon("Reframed/MainMenu/Cog") },
            { "menu.briefcase", new CachedIcon("Reframed/MainMenu/Case") },
            { "menu.book", new CachedIcon("Reframed/MainMenu/Book") },
            { "menu.exit", new CachedIcon("Reframed/MainMenu/Exit") },
            { "ui.external-link", new CachedIcon("Reframed/MainMenu/ExternalLink") }
        };
        private sealed class CachedIcon
        {
            private readonly string path;
            private Texture2D texture;
            internal CachedIcon(string path) { this.path = path; }
            internal Texture2D Resolve()
            {
                // Language reload destroys mod PNG objects and replaces the native content holders.
                // Typed Unity equality detects a destroyed texture even while its managed reference survives.
                if (texture == null) texture = ContentFinder<Texture2D>.Get(path, true);
                return texture;
            }
        }
        internal static Texture2D Get(string existingMenuId) => existingMenuId != null && Images.TryGetValue(existingMenuId, out CachedIcon image) ? image.Resolve() : null;
    }
}
