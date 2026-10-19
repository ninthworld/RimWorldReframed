using UnityEngine;
using Verse;

namespace ReframedUI
{
    /// <summary>Static PNGs rasterized offline from source-silhouette SVG masters; no runtime glyph geometry.</summary>
    [StaticConstructorOnStartup]
    internal static class SaveBrowserIcons
    {
        // Back/Search/House/Save/Left/Right are neutral white-alpha assets for caller tinting.
        // Play/Delete retain their source mint/slate RGB and should be drawn with Color.white.
        // Keep each PNG's transparent padding; dimensions and extraction provenance are in Batch05/icon-assets.md.
        private static Texture2D back, search, house, save, play, delete, left, right;
        internal static Texture2D Back => Resolve(ref back, "Reframed/SaveBrowser/Back");
        internal static Texture2D Search => Resolve(ref search, "Reframed/SaveBrowser/Search");
        internal static Texture2D House => Resolve(ref house, "Reframed/SaveBrowser/House");
        internal static Texture2D Save => Resolve(ref save, "Reframed/SaveBrowser/Save");
        internal static Texture2D Play => Resolve(ref play, "Reframed/SaveBrowser/Play");
        internal static Texture2D Delete => Resolve(ref delete, "Reframed/SaveBrowser/Delete");
        internal static Texture2D Left => Resolve(ref left, "Reframed/SaveBrowser/Left");
        internal static Texture2D Right => Resolve(ref right, "Reframed/SaveBrowser/Right");

        private static Texture2D Resolve(ref Texture2D texture, string path)
        {
            // Main-thread UI access only. Typed Unity-null also catches objects destroyed by language/content reload.
            if (texture == null) texture = ContentFinder<Texture2D>.Get(path, true);
            return texture;
        }
    }
}
