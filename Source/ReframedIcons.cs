using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    /// <summary>Small code-drawn icon candidates measured from the B01 source raster.</summary>
    [StaticConstructorOnStartup]
    internal static class ReframedIcons
    {
        internal const int VariantCount = 2;
        private static readonly string[] VariantIds = { "vector-refined", "native-plus-search" };
        private const int RasterSamplesPerAxis = 8;
        // Four texture pixels per unit of the largest current (24-unit) menu draw.
        // Smaller dock/specimen rectangles reuse this cache without upscaling a 16px bitmap.
        private const int GlyphTextureSize = 24 * 4;
        private static Texture2D[] vectorTextures;
        private static Texture2D unknownTexture;

        private sealed class IconInfo
        {
            internal readonly string Id;
            internal readonly int X, Y, Width, Height;
            internal readonly string Geometry;
            internal readonly string SourceContext;
            internal readonly float ProvisionalStrokePx;
            internal readonly bool RedFaceTint;
            internal readonly int SpecimenX, SpecimenY;

            internal IconInfo(string id, int x, int y, int width, int height, int specimenX, int specimenY,
                string geometry, string sourceContext, float provisionalStrokePx, bool redFaceTint = false)
            {
                Id = id; X = x; Y = y; Width = width; Height = height;
                SpecimenX = specimenX; SpecimenY = specimenY;
                Geometry = geometry; SourceContext = sourceContext; ProvisionalStrokePx = provisionalStrokePx; RedFaceTint = redFaceTint;
            }
        }

        private static readonly IconInfo[] Icons =
        {
            new IconInfo("menu.plus", 290, 523, 17, 16, 86, 8, "thin centered plus", "B01 New colony button", 1.4f),
            new IconInfo("menu.load", 214, 569, 16, 16, 277, 8, "upward arrow above an open tray", "B01 Load button", 1.7f),
            new IconInfo("menu.scroll", 358, 569, 16, 16, 467, 8, "filled scroll with cutout rules and curled ends", "B01 Tutorial button", 1.0f),
            new IconInfo("menu.cog", 215, 634, 14, 16, 658, 8, "solid eight-tooth cog with open center", "B01 Options button", 1.0f),
            new IconInfo("menu.briefcase", 358, 634, 16, 16, 86, 151, "case outline with raised handle, seam and latch", "B01 Mods button", 1.5f),
            new IconInfo("menu.book", 214, 674, 16, 15, 277, 151, "two solid open pages separated by a center crease", "B01 Credits button", 1.0f),
            new IconInfo("menu.exit", 358, 673, 16, 17, 467, 151, "open door outline with thin rightward arrow", "B01 Quit button", 1.5f, true),
            new IconInfo("save.search", 617, 441, 16, 16, 658, 151, "full-bounds lens and broad lower-right handle", "B01 save-browser search", 2.1f)
        };

        private static readonly string[] SpecimenLabels = { "PLUS", "LOAD", "TUTORIAL", "OPTIONS", "MODS", "CREDITS", "QUIT", "SEARCH" };
        private static readonly Dictionary<string, int> IndexById = BuildIndex();

        private static Dictionary<string, int> BuildIndex()
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < Icons.Length; i++) result[Icons[i].Id] = i;
            return result;
        }

        /// <summary>Draw an icon into its optical-bounds rectangle using caller tint. Unknown IDs draw a box/X and return false.</summary>
        internal static bool Draw(string id, Rect bounds, Color tint)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint || !Drawable(bounds)) return false;
            int index = -1;
            bool known = id != null && IndexById.TryGetValue(id, out index);
            if (!known) index = -1;

            using (new Theme.GuiScope(true))
            {
                GUI.color = tint * GUI.color;
                bool groupStarted = false;
                try
                {
                    GUI.BeginGroup(bounds);
                    groupStarted = true;
                    var localBounds = new Rect(0f, 0f, bounds.width, bounds.height);
                    GUI.DrawTexture(localBounds, known ? GetVectorTexture(index) : GetUnknownTexture(), ScaleMode.StretchToFill, true);
                }
                finally
                {
                    if (groupStarted) GUI.EndGroup();
                }
            }
            return known;
        }

        /// <summary>Stable 4-column by 2-row live specimen for the developer foundation lab.</summary>
        internal static void DrawSpecimen(Rect rect, int variant = 0)
        {
            if (!ValidVariant(variant) || !Drawable(rect) || !Mathf.Approximately(rect.width, 760f) || !Mathf.Approximately(rect.height, 286f) ||
                Event.current == null || Event.current.type != EventType.Repaint) return;
            using (new Theme.GuiScope(true))
            {
                GUI.color = Color.white;
                Widgets.DrawBoxSolid(rect, Theme.Rgb(DesignTokens.Panel));
                for (int i = 0; i < Icons.Length; i++)
                {
                    IconInfo icon = Icons[i];
                    int column = i % 4;
                    float cellX = rect.x + column * 190f;
                    var iconRect = new Rect(rect.x + icon.SpecimenX, rect.y + icon.SpecimenY, icon.Width, icon.Height);
                    DrawVariant(icon.Id, iconRect, Theme.Rgb(DesignTokens.Muted), variant);
                    Theme.TextLine(new Rect(cellX + 3f, iconRect.y + icon.Height + 3f, 184f, 16f),
                        SpecimenLabels[i], Theme.Muted, TextAnchor.MiddleCenter);
                }
            }
        }

        /// <summary>Compact evidence metadata for capture manifests and lab display.</summary>
        internal static string MetadataJson(int variant = 0)
        {
            if (!ValidVariant(variant)) variant = 0;
            var json = new StringBuilder("{\"schema\":\"reframed-icon-candidates-v1\",\"status\":\"diagnostic renderer variants; source identity unresolved\",\"variant_id\":\"")
                .Append(VariantIds[variant])
                .Append("\",\"variant_index\":").Append(variant)
                .Append(",\"specimen\":{\"size_px\":[760,286],\"background_rgb\":[14,19,23],\"tint_rgb\":[132,146,159],\"coordinate_origin\":\"top-left relative to DrawSpecimen(Rect); capture mapping is recorded separately\",\"extra_diagnostic_samples\":false},\"icons\":[");
            for (int i = 0; i < Icons.Length; i++)
            {
                if (i != 0) json.Append(',');
                IconInfo icon = Icons[i];
                json.Append("{\"id\":\"").Append(icon.Id).Append("\",\"board\":\"B01\",\"source_bounds_xyxy\":[")
                    .Append(icon.X).Append(',').Append(icon.Y).Append(',').Append(icon.X + icon.Width).Append(',').Append(icon.Y + icon.Height)
                    .Append("],\"specimen_rect_xywh\":[").Append(icon.SpecimenX).Append(',').Append(icon.SpecimenY).Append(',').Append(icon.Width).Append(',').Append(icon.Height)
                    .Append("],\"geometry\":\"").Append(icon.Geometry).Append("\",\"context\":\"").Append(icon.SourceContext)
                    .Append("\",\"provisional_stroke_px\":").Append(icon.ProvisionalStrokePx.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"renderer\":\"").Append(variant == 1 && (i == 0 || i == 7) ? "native texture candidate" : "refined vector candidate")
                    .Append("\",\"native_texture_path\":\"").Append(i == 0 ? "UI/Buttons/Plus" : i == 7 ? "UI/Widgets/Search" : "unresolved")
                    .Append("\",\"native_match\":\"unresolved\",\"source_tint\":\"")
                    .Append(icon.RedFaceTint ? "pale pink/white on red" : "light monochrome on dark face; pixel tint varies")
                    .Append("\"}");
            }
            return json.Append("]}").ToString();
        }

        internal static string GetVariantId(int variant) => ValidVariant(variant) ? VariantIds[variant] : "unknown";

        private static bool ValidVariant(int variant) => variant >= 0 && variant < VariantCount;

        private static void DrawVariant(string id, Rect bounds, Color tint, int variant)
        {
            if (variant != 1 || id != "menu.plus" && id != "save.search")
            {
                Draw(id, bounds, tint);
                return;
            }
            if (Event.current == null || Event.current.type != EventType.Repaint || !Drawable(bounds)) return;
            Texture2D texture = id == "menu.plus" ? TexButton.Plus : TexButton.Search;
            using (new Theme.GuiScope(true))
            {
                GUI.color = tint * GUI.color;
                bool groupStarted = false;
                try
                {
                    GUI.BeginGroup(bounds);
                    groupStarted = true;
                    GUI.DrawTexture(new Rect(0f, 0f, bounds.width, bounds.height), texture, ScaleMode.StretchToFill, true);
                }
                finally
                {
                    if (groupStarted) GUI.EndGroup();
                }
            }
        }

        private static bool Drawable(Rect rect) => Finite(rect.x) && Finite(rect.y) && Finite(rect.width) && Finite(rect.height) && rect.width > 0f && rect.height > 0f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static Texture2D GetVectorTexture(int index)
        {
            if (vectorTextures == null) vectorTextures = new Texture2D[Icons.Length];
            if (vectorTextures[index] == null) vectorTextures[index] = CreateGlyphTexture(index);
            return vectorTextures[index];
        }

        private static Texture2D GetUnknownTexture()
        {
            if (unknownTexture == null) unknownTexture = CreateUnknownTexture(16, 16);
            return unknownTexture;
        }

        private static Texture2D CreateGlyphTexture(int index)
        {
            IconInfo icon = Icons[index];
            return CreateTexture(GlyphTextureSize, GlyphTextureSize,
                alpha => RasterGlyph(alpha, GlyphTextureSize, GlyphTextureSize, index), "Reframed icon " + icon.Id);
        }

        private static Texture2D CreateUnknownTexture(int width, int height)
        {
            return CreateTexture(width, height, alpha => RasterUnknown(alpha, width, height), "Reframed unknown icon");
        }

        private static Texture2D CreateTexture(int width, int height, Action<byte[]> raster, string textureName)
        {
            int highWidth = width * RasterSamplesPerAxis;
            int highHeight = height * RasterSamplesPerAxis;
            var highResolutionAlpha = new byte[highWidth * highHeight];
            raster(highResolutionAlpha);

            var pixels = new Color32[width * height];
            int sampleCount = RasterSamplesPerAxis * RasterSamplesPerAxis;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int coveredSamples = 0;
                    int startX = x * RasterSamplesPerAxis;
                    int startY = y * RasterSamplesPerAxis;
                    for (int sy = 0; sy < RasterSamplesPerAxis; sy++)
                    {
                        int row = (startY + sy) * highWidth + startX;
                        for (int sx = 0; sx < RasterSamplesPerAxis; sx++)
                            if (highResolutionAlpha[row + sx] != 0) coveredSamples++;
                    }
                    byte alpha = (byte)((coveredSamples * 255 + sampleCount / 2) / sampleCount);
                    pixels[(height - 1 - y) * width + x] = new Color32(255, 255, 255, alpha);
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = textureName;
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void RasterGlyph(byte[] alpha, int width, int height, int index)
        {
            // Stroke recipes are expressed in the original source-sized canvas, not texture pixels.
            float t = Icons[index].ProvisionalStrokePx * Mathf.Min(
                (float)width / Icons[index].Width, (float)height / Icons[index].Height);
            switch (index)
            {
                case 0:
                    RasterLine(alpha, width, height, .50f, .08f, .50f, .92f, t);
                    RasterLine(alpha, width, height, .08f, .50f, .92f, .50f, t);
                    break;
                case 1:
                    RasterPolygon(alpha, width, height, new float[] {
                        .50f,.05f, .20f,.35f, .39f,.35f, .39f,.64f,
                        .61f,.64f, .61f,.35f, .80f,.35f });
                    RasterLine(alpha, width, height, .10f, .61f, .10f, .92f, t);
                    RasterLine(alpha, width, height, .10f, .92f, .90f, .92f, t);
                    RasterLine(alpha, width, height, .90f, .92f, .90f, .61f, t);
                    break;
                case 2:
                    // The sheet is a filled silhouette; dark rules are holes, not a speech-bubble outline.
                    RasterPolygon(alpha, width, height, new float[] {
                        .19f,.05f, .82f,.05f, .82f,.80f, .72f,.95f,
                        .07f,.95f, .03f,.86f, .07f,.76f, .19f,.76f });
                    RasterPolygon(alpha, width, height, new float[] {
                        .72f,.05f, .93f,.05f, .98f,.13f, .98f,.27f,
                        .83f,.27f, .83f,.15f, .72f,.15f });
                    RasterLine(alpha, width, height, .34f, .29f, .68f, .29f, t, 0);
                    RasterLine(alpha, width, height, .34f, .46f, .68f, .46f, t, 0);
                    RasterLine(alpha, width, height, .34f, .63f, .63f, .63f, t, 0);
                    RasterLine(alpha, width, height, .14f, .81f, .64f, .81f, t * .75f, 0);
                    break;
                case 3:
                    var teeth = new float[64];
                    for (int i = 0; i < 32; i++)
                    {
                        int corner = i % 4;
                        float angle = ((i / 4) + (corner == 0 ? -.42f : corner == 1 ? -.18f : corner == 2 ? .18f : .42f)) * (float)Math.PI / 4f;
                        float radius = corner == 1 || corner == 2 ? .48f : .35f;
                        teeth[i * 2] = .50f + (float)Math.Cos(angle) * radius;
                        teeth[i * 2 + 1] = .50f + (float)Math.Sin(angle) * radius;
                    }
                    RasterPolygon(alpha, width, height, teeth);
                    RasterDisc(alpha, width, height, .50f, .50f, .26f, 0);
                    break;
                case 4:
                    RasterLine(alpha, width, height, .08f, .31f, .92f, .31f, t);
                    RasterLine(alpha, width, height, .08f, .31f, .08f, .92f, t);
                    RasterLine(alpha, width, height, .92f, .31f, .92f, .92f, t);
                    RasterLine(alpha, width, height, .08f, .92f, .92f, .92f, t);
                    RasterLine(alpha, width, height, .32f, .30f, .32f, .10f, t);
                    RasterLine(alpha, width, height, .32f, .10f, .68f, .10f, t);
                    RasterLine(alpha, width, height, .68f, .10f, .68f, .30f, t);
                    RasterLine(alpha, width, height, .09f, .55f, .91f, .55f, t * .75f);
                    RasterPolygon(alpha, width, height, new float[] { .44f,.48f, .56f,.48f, .56f,.65f, .44f,.65f });
                    break;
                case 5:
                    // Solid pages with a narrow negative center crease; no grid or page-rule strokes.
                    RasterPolygon(alpha, width, height, new float[] {
                        .04f,.09f, .24f,.06f, .47f,.16f, .47f,.94f, .24f,.85f, .04f,.87f });
                    RasterPolygon(alpha, width, height, new float[] {
                        .53f,.16f, .76f,.06f, .96f,.09f, .96f,.87f, .76f,.85f, .53f,.94f });
                    break;
                case 6:
                    RasterLine(alpha, width, height, .08f, .08f, .57f, .08f, t);
                    RasterLine(alpha, width, height, .08f, .08f, .08f, .92f, t);
                    RasterLine(alpha, width, height, .08f, .92f, .57f, .92f, t);
                    RasterLine(alpha, width, height, .57f, .08f, .57f, .28f, t);
                    RasterLine(alpha, width, height, .57f, .72f, .57f, .92f, t);
                    RasterLine(alpha, width, height, .34f, .50f, .93f, .50f, t * .85f);
                    RasterLine(alpha, width, height, .74f, .31f, .93f, .50f, t * .85f);
                    RasterLine(alpha, width, height, .93f, .50f, .74f, .69f, t * .85f);
                    break;
                case 7:
                    RasterEllipse(alpha, width, height, .41f, .41f, .82f, t * .88f);
                    RasterLine(alpha, width, height, .62f, .62f, 1f, 1f, t * 1.05f);
                    break;
            }
        }

        // Normalized, code-native filled contours; alpha=0 cuts holes before coverage downsampling.
        private static void RasterPolygon(byte[] alpha, int width, int height, float[] points, byte value = 255)
        {
            int rasterWidth = width * RasterSamplesPerAxis;
            int rasterHeight = height * RasterSamplesPerAxis;
            int count = points.Length / 2;
            for (int y = 0; y < rasterHeight; y++)
            {
                float py = (y + .5f) / rasterHeight;
                for (int x = 0; x < rasterWidth; x++)
                {
                    float px = (x + .5f) / rasterWidth;
                    bool inside = false;
                    for (int i = 0, j = count - 1; i < count; j = i++)
                    {
                        float ix = points[i * 2], iy = points[i * 2 + 1];
                        float jx = points[j * 2], jy = points[j * 2 + 1];
                        if ((iy > py) != (jy > py) && px < (jx - ix) * (py - iy) / (jy - iy) + ix)
                            inside = !inside;
                    }
                    if (inside) alpha[y * rasterWidth + x] = value;
                }
            }
        }

        private static void RasterDisc(byte[] alpha, int width, int height, float cx, float cy, float diameter, byte value)
        {
            int rasterWidth = width * RasterSamplesPerAxis;
            int rasterHeight = height * RasterSamplesPerAxis;
            float radiusSquared = diameter * diameter * .25f;
            for (int y = 0; y < rasterHeight; y++)
            {
                float dy = (y + .5f) / rasterHeight - cy;
                for (int x = 0; x < rasterWidth; x++)
                {
                    float dx = (x + .5f) / rasterWidth - cx;
                    if (dx * dx + dy * dy <= radiusSquared) alpha[y * rasterWidth + x] = value;
                }
            }
        }

        private static void RasterUnknown(byte[] alpha, int width, int height)
        {
            float t = Mathf.Max(.9f, Mathf.Min(width, height) * .12f);
            RasterLine(alpha, width, height, .12f, .12f, .88f, .12f, t);
            RasterLine(alpha, width, height, .88f, .12f, .88f, .88f, t);
            RasterLine(alpha, width, height, .88f, .88f, .12f, .88f, t);
            RasterLine(alpha, width, height, .12f, .88f, .12f, .12f, t);
            RasterLine(alpha, width, height, .28f, .28f, .72f, .72f, t);
            RasterLine(alpha, width, height, .72f, .28f, .28f, .72f, t);
        }

        private static void RasterLine(byte[] alpha, int width, int height,
            float x1, float y1, float x2, float y2, float thickness, byte value = 255)
        {
            int samples = RasterSamplesPerAxis;
            int rasterWidth = width * samples;
            int rasterHeight = height * samples;
            float ax = x1 * width, ay = y1 * height;
            float bx = x2 * width, by = y2 * height;
            float dx = bx - ax, dy = by - ay;
            float lengthSquared = dx * dx + dy * dy;
            float half = Mathf.Max(.1f, thickness * .5f);
            int minX = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(ax, bx) - half) * samples));
            int maxX = Mathf.Min(rasterWidth - 1, Mathf.CeilToInt((Mathf.Max(ax, bx) + half) * samples));
            int minY = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(ay, by) - half) * samples));
            int maxY = Mathf.Min(rasterHeight - 1, Mathf.CeilToInt((Mathf.Max(ay, by) + half) * samples));
            float limit = half * half;
            for (int y = minY; y <= maxY; y++)
            {
                float py = (y + .5f) / samples;
                for (int x = minX; x <= maxX; x++)
                {
                    float px = (x + .5f) / samples;
                    float projection = lengthSquared > .0001f ? ((px - ax) * dx + (py - ay) * dy) / lengthSquared : 0f;
                    projection = Mathf.Clamp01(projection);
                    float nearX = ax + projection * dx, nearY = ay + projection * dy;
                    float ddx = px - nearX, ddy = py - nearY;
                    if (ddx * ddx + ddy * ddy <= limit) alpha[y * rasterWidth + x] = value;
                }
            }
        }

        private static void RasterEllipse(byte[] alpha, int width, int height,
            float centerX, float centerY, float diameter, float thickness)
        {
            int samples = RasterSamplesPerAxis;
            int rasterWidth = width * samples;
            int rasterHeight = height * samples;
            float cx = centerX * width, cy = centerY * height;
            float rx = diameter * width * .5f, ry = diameter * height * .5f;
            float innerRx = Mathf.Max(0f, rx - thickness * .5f);
            float innerRy = Mathf.Max(0f, ry - thickness * .5f);
            int minX = Mathf.Max(0, Mathf.FloorToInt((cx - rx) * samples));
            int maxX = Mathf.Min(rasterWidth - 1, Mathf.CeilToInt((cx + rx) * samples));
            int minY = Mathf.Max(0, Mathf.FloorToInt((cy - ry) * samples));
            int maxY = Mathf.Min(rasterHeight - 1, Mathf.CeilToInt((cy + ry) * samples));
            for (int y = minY; y <= maxY; y++)
            {
                float py = (y + .5f) / samples;
                float oy = (py - cy) / ry;
                for (int x = minX; x <= maxX; x++)
                {
                    float px = (x + .5f) / samples;
                    float ox = (px - cx) / rx;
                    bool inOuter = ox * ox + oy * oy <= 1f;
                    if (!inOuter) continue;
                    bool inInner = innerRx > 0f && innerRy > 0f &&
                        (px - cx) * (px - cx) / (innerRx * innerRx) +
                        (py - cy) * (py - cy) / (innerRy * innerRy) < 1f;
                    if (!inInner) alpha[y * rasterWidth + x] = 255;
                }
            }
        }
    }
}
