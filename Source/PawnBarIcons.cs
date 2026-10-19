using UnityEngine;
using Verse;

namespace ReframedUI
{
    /// <summary>Offline rasterized authored SVG icons; callers supply semantic tint.</summary>
    [StaticConstructorOnStartup]
    internal static class PawnBarIcons
    {
        private static Texture2D expand, collapse, warning;
        internal static Texture2D Expand => Resolve(ref expand, "Reframed/PawnBar/Expand");
        internal static Texture2D Collapse => Resolve(ref collapse, "Reframed/PawnBar/Collapse");
        internal static Texture2D Warning => Resolve(ref warning, "Reframed/PawnBar/Warning");

        private static Texture2D Resolve(ref Texture2D texture, string path)
        {
            // Typed Unity-null also catches textures destroyed by a content reload.
            if (texture == null) texture = ContentFinder<Texture2D>.Get(path, true);
            return texture;
        }
    }
}
