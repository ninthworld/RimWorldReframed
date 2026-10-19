using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Authored SVG masters are rasterized offline; semantic tint belongs to callers.
    internal static class InspectorIcons
    {
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        internal static Texture2D Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return BaseContent.BadTex;
            if (!Textures.TryGetValue(name, out Texture2D texture) || texture == null)
                Textures[name] = texture = ContentFinder<Texture2D>.Get("Reframed/Inspector/" + name);
            return texture;
        }
    }
}
