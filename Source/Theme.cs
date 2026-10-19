using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    [StaticConstructorOnStartup]
    internal static class Theme
    {
        // Shared by the entry shell, DLC tray/card and save browser.
        internal const float EntryPanelOpacity = .96f;
        internal static Color Rgb(uint hex) => new Color32((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);
        internal static readonly Color Border = Rgb(DesignTokens.Divider);
        internal static readonly Color Surface = Rgb(DesignTokens.Button);
        internal static readonly Color Hover = Rgb(DesignTokens.Hover);
        internal static readonly Color Selected = Rgb(DesignTokens.Selected);
        // Retain the prototype's provisional general accent until individual HUD
        // semantic roles are reconstructed. Comparison Best is never a threat token.
        internal static readonly Color Accent = new Color(0.72f, 0.66f, 0.42f);
        internal static readonly Color Ink = Rgb(DesignTokens.Ink);
        internal static readonly Color Muted = Rgb(DesignTokens.Muted);
        internal static readonly Color Good = new Color(0.38f, 0.68f, 0.55f);
        internal static readonly Color Danger = new Color(0.86f, 0.35f, 0.32f);
        internal static Color PanelFill
        {
            get
            {
                Color face = Rgb(DesignTokens.Panel);
                face.a = Mathf.Clamp(ReframedMod.Settings?.PanelOpacity ?? .9f, .65f, .9f);
                return face;
            }
        }
        private static GUIStyle actionLabelStyle;
        private static readonly Dictionary<Color32, Texture2D> buttonRims = new Dictionary<Color32, Texture2D>();

        // Procedural geometry: no downloaded artwork or per-frame texture allocation.
        internal static void Panel(Rect rect)
        {
            if (!PrimitiveGeometry.Drawable(rect.x, rect.y, rect.width, rect.height)) return;
            RoundRect(rect, PanelFill, 6f);
        }

        internal static void RoundRect(Rect rect, Color color, float radius = 4f)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint ||
                !PrimitiveGeometry.Drawable(rect.x, rect.y, rect.width, rect.height)) return;
            float r = PrimitiveGeometry.Radius(radius, rect.width, rect.height);
            using (new GuiScope(true))
            {
            // Unity's native rounded-rectangle material draws one quad with
            // smooth corners. Separate edge/corner quads leave sampling seams
            // when a UI coordinate lands on a fractional physical pixel.
            Color tint = color * GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(rect, BaseContent.WhiteTex, ScaleMode.StretchToFill,
                true, 0f, tint, 0f, r);
            }
        }

        internal static void TextLine(Rect rect, string label, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            using (new GuiScope(true))
            {
            GUI.color = color;
            Text.Font = GameFont.Tiny;
            Text.Anchor = anchor;
            Text.WordWrap = false;
            Widgets.Label(rect, label);
            }
        }

        internal static void Label(Rect rect, string label, Color color)
        {
            using (new GuiScope(true))
            {
            GUI.color = color;
            // Button labels use the game's native smooth Small face. Tiny is
            // retained only for compact metadata, never enlarged as button text.
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.WordWrap = false;
            GUI.contentColor = Color.white;
            if (actionLabelStyle == null || actionLabelStyle.font != Text.CurFontStyle.font)
            {
                actionLabelStyle = new GUIStyle(Text.CurFontStyle);
                actionLabelStyle.normal.textColor = Color.white;
            }
            GUI.Label(rect, label, actionLabelStyle);
            }
        }

        // A shaded rim around a flat face, not a bright strip across the face.
        // Raster softness in the compressed concept is not proof of an outer glow.
        internal static void ButtonFace(Rect rect, Color face, bool disabled = false,
            float radius = 3f, bool openBottom = false, Color? rim = null, float opacity = 1f)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint ||
                !PrimitiveGeometry.Drawable(rect.x, rect.y, rect.width, rect.height)) return;
            using (new GuiScope(true))
            {
                float alpha = Mathf.Clamp01(opacity * GUI.color.a);
                GUI.color = Color.white;
                // BeginClip adds no control IDs; this paint-only helper must not
                // change the ID sequence between input and repaint events.
                GUI.BeginClip(rect);
                try
                {
                    var outer = new Rect(0, 0, rect.width, rect.height + (openBottom ? radius + 2 : 0));
                    Color top = rim ?? new Color(Mathf.Min(1, face.r + 18f / 255),
                        Mathf.Min(1, face.g + 20f / 255), Mathf.Min(1, face.b + 22f / 255), 1);
                    Texture2D gradient = ButtonRim(top);
                    GUI.DrawTexture(outer, gradient, ScaleMode.StretchToFill, true, 0, new Color(1, 1, 1, alpha), 0, radius);
                    face.a *= alpha;
                    RoundRect(outer.ContractedBy(1f), face, Mathf.Max(0, radius - .8f));
                    if (disabled) RoundRect(outer, new Color(.024f, .039f, .051f, .4f * alpha), radius);
                }
                finally { GUI.EndClip(); }
            }
        }

        private static Texture2D ButtonRim(Color top)
        {
            Color32 key = top;
            if (buttonRims.TryGetValue(key, out Texture2D texture)) return texture;
            texture = new Texture2D(1, 64, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            // Hue follows the face, including green Play and red Quit. The top
            // catches more light; the lower rim remains quiet in the same family.
            Color bottom = new Color(Mathf.Max(0, top.r - 12f / 255),
                Mathf.Max(0, top.g - 12f / 255), Mathf.Max(0, top.b - 12f / 255), 1);
            var pixels = new Color[64];
            for (int y = 0; y < pixels.Length; y++)
                pixels[y] = Color.Lerp(bottom, top, Mathf.Pow(y / 63f, 2));
            texture.SetPixels(pixels); texture.Apply(false, true);
            buttonRims.Add(key, texture);
            return texture;
        }

        internal static bool ActionButton(Rect rect, string label, bool enabled = true)
        {
            using (new GuiScope(true))
            {
                bool available = enabled && GUI.enabled;
                bool hover = available && Mouse.IsOver(rect);
                // Native GUI.Button owns capture. Do not infer a captured press
                // from a global mouse-down flag when a drag may have begun elsewhere.
                ButtonFace(rect, hover ? Hover : Surface, !available);
                Label(rect, label, available ? Color.white : Muted);
                GUI.enabled = available;
                return available && Widgets.ButtonInvisible(rect);
            }
        }

        internal readonly struct GuiScope : IDisposable
        {
            private readonly Color color;
            private readonly GameFont font;
            private readonly TextAnchor anchor;
            private readonly bool wrap;
            private readonly Color background, content;
            private readonly bool enabled;
            private readonly Matrix4x4 matrix;
            private readonly int depth;
            internal GuiScope(bool capture)
            {
                color = GUI.color;
                font = Text.Font;
                anchor = Text.Anchor;
                wrap = Text.WordWrap;
                background = GUI.backgroundColor;
                content = GUI.contentColor;
                enabled = GUI.enabled;
                matrix = GUI.matrix;
                depth = GUI.depth;
            }
            public void Dispose()
            {
                GUI.color = color;
                Text.Font = font;
                Text.Anchor = anchor;
                Text.WordWrap = wrap;
                GUI.backgroundColor = background;
                GUI.contentColor = content;
                GUI.enabled = enabled;
                GUI.matrix = matrix;
                GUI.depth = depth;
            }
        }
    }
}
