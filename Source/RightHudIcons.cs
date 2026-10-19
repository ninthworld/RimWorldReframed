using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal static class RightHudIcons
    {
        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        internal static Texture2D Get(string name)
        {
            if (string.IsNullOrEmpty(name)) name = "info";
            if (!cache.TryGetValue(name, out Texture2D icon) || icon == null)
                cache[name] = icon = ContentFinder<Texture2D>.Get("Reframed/RightHud/" + name, false) ??
                    ContentFinder<Texture2D>.Get("Reframed/RightHud/info");
            return icon;
        }
    }
}
