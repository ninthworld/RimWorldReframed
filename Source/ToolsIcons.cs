using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    [StaticConstructorOnStartup]
    internal static class ToolsIcons
    {
        private static readonly Dictionary<string, Texture2D> Icons = new Dictionary<string, Texture2D>();
        private static readonly string[] Names = { "search", "back", "clear", "codex", "compare", "repair", "roof",
            "colonists", "helper", "resources", "beauty", "heat", "pollution", "terrain", "fertility", "zones", "roofs", "roomstats", "more" };

        static ToolsIcons()
        {
            foreach (string name in Names) Icons[name] = ContentFinder<Texture2D>.Get("Reframed/Tools/" + name, true);
        }

        internal static Texture2D Get(string name) => name != null && Icons.TryGetValue(name, out Texture2D icon) ? icon : BaseContent.BadTex;
    }
}
