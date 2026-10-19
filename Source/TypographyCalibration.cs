using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Calibration only: clones the three existing Verse.Text styles. No font asset is created or loaded here.
    // First use is the developer lab's OnGUI. Eager startup can initialize Verse.Text
    // before GUI.skin is legal (for example when the game initially has no visible frame).
    internal static class TypographyCalibration
    {
        private sealed class Role
        {
            internal int Slot, Size, SuffixSize;
            internal bool Bold;
            internal float Tracking, X, Y, SuffixGap;
            internal uint Tint = 0xD8E2EA, SuffixTint = 0x98A7B6, Background = 0x0E1317;
            internal GUIStyle Style, SuffixStyle;
        }
        private sealed class Candidate
        {
            internal string Id, Description, NativeSlot;
            internal GUIStyle Style, NormalStyle;
            internal GUIContent Header;
            internal float HeadingTracking;
            internal Role[] Roles;
        }
        private sealed class Sample
        {
            internal string Id, Text, Board, CropFile;
            internal int[] SourceRect;
            internal GUIContent Content, Label;
            internal GUIContent[] Characters;
        }
        private static readonly Sample[] Samples = {
            SampleRow("menu-body", "Load", "B01", 237, 567, 275, 590),
            SampleRow("comparison-bold", "Quality", "B03", 652, 446, 750, 468),
            SampleRow("micro-heading", "BASICS", "B03", 652, 413, 722, 427),
            SampleRow("pawn-title", "Futa Healer", "B04", 1071, 680, 1212, 709),
            SampleRow("work-tag", "Mine", "B04", 337, 243, 396, 270)
        };
        private static readonly Candidate[] candidates;
        private static readonly GUIStyle metadataStyle;
        private static readonly GUIContent Prefix = new GUIContent("Futa "), Suffix = new GUIContent("Healer");
        private static readonly GUIContent Footer = new GUIContent("Native fonts only. Role recipes are provisional. Right-column glyph probes have no source-match claim.");
        private static readonly GUIContent GlyphProbe = new GUIContent("agR 14% .,:;/()");
        private static readonly ulong[] nativeBefore = new ulong[3], nativeAfter = new ulong[3];
        private static readonly ulong[] nativeConstructionBefore = new ulong[3], nativeConstructionAfter = new ulong[3];
        private static bool nativeConstructionUnchanged = true;
        private static bool nativeProbeExecuted, nativeUnchanged = true, allNativeDrawsUnchanged = true;
        private static readonly Rect[] lastScreenCells = new Rect[5];
        private static int lastCandidate = -1, lastFrame = -1;
        private static bool lastDrawComplete;
        internal static int Count => candidates.Length;

        static TypographyCalibration()
        {
            CaptureNative(nativeConstructionBefore);
            var list = new List<Candidate>();
            string[] slots = { "Tiny", "Small", "Medium" };
            for (int slot = 0; slot < 3; slot++)
            {
                GUIStyle native = Text.fontStyles[slot];
                if (native == null || native.font == null) continue;
                Add(list, native, slots[slot], "native", native.fontSize, native.fontStyle, 0);
                foreach (int size in new[] { 9, 10, 12, 14, 16 })
                    foreach (FontStyle weight in new[] { FontStyle.Normal, FontStyle.Bold })
                        Add(list, native, slots[slot], "size" + size, size, weight, 0);
                if (slot < 2) Add(list, native, slots[slot], "micro-tracked", 9, FontStyle.Bold, 1);
            }
            // Omitted adjacent sizes: no duplicate Medium sweeps because its Arial raster tied Small.
            for (int slot = 0; slot < 2; slot++)
                foreach (int size in new[] { 11, 13, 15, 17 })
                    foreach (FontStyle weight in new[] { FontStyle.Normal, FontStyle.Bold })
                        Add(list, Text.fontStyles[slot], slots[slot], "adjacent" + size, size, weight, 0);
            AddRoleCandidates(list);
            candidates = list.ToArray();
            metadataStyle = Clone(Text.fontStyles[(int)GameFont.Tiny]);
            metadataStyle.fontSize = 11;
            CaptureNative(nativeConstructionAfter);
            for (int slot = 0; slot < 3; slot++) if (nativeConstructionBefore[slot] != nativeConstructionAfter[slot]) nativeConstructionUnchanged = false;
            CaptureNative(nativeBefore); // Warm cached GUIStyle state/offset wrappers before measured draws.
        }

        private static Sample SampleRow(string id, string text, string board, params int[] rect) => new Sample {
            Id = id, Text = text, Board = board, SourceRect = rect,
            CropFile = "Docs/UI-Reworked/measurements/" + id + ".png",
            Content = new GUIContent(text), Label = new GUIContent(id),
            Characters = text.Select(character => new GUIContent(character.ToString())).ToArray()
        };

        private static GUIStyle Clone(GUIStyle source)
        {
            var style = new GUIStyle(source) {
                alignment = TextAnchor.UpperLeft, wordWrap = false, richText = false,
                clipping = TextClipping.Clip, fixedWidth = 0, fixedHeight = 0
            };
            style.normal.textColor = Theme.Ink;
            return style;
        }

        private static void Add(List<Candidate> list, GUIStyle native, string slot, string variant, int size, FontStyle weight, float tracking)
        {
            var style = Clone(native);
            style.fontSize = size;
            style.fontStyle = weight;
            var normal = Clone(style);
            normal.fontStyle = FontStyle.Normal;
            string id = slot.ToLowerInvariant() + "-" + variant + "-" + weight.ToString().ToLowerInvariant();
            string label = id + " | " + native.font.name + " | size " + size + " | BASICS tracking " + tracking.ToString(CultureInfo.InvariantCulture);
            list.Add(new Candidate { Id = id, Description = label, NativeSlot = slot, Style = style,
                NormalStyle = normal, HeadingTracking = tracking, Header = new GUIContent(label) });
        }

        private static Role[] BaseRoles() => new[] {
            new Role { Slot = 0, Size = 16, Bold = true, X = 2, Y = 0, Background = 0x212832 },
            new Role { Slot = 0, Size = 16, Bold = true, X = 2, Y = 0 },
            new Role { Slot = 1, Size = 10, Bold = true, X = 2, Y = -1, Tracking = .4f, Tint = 0xB6C6D4 },
            new Role { Slot = 1, Size = 16, SuffixSize = 16, Bold = true, X = 1, Y = 6, SuffixGap = 3 },
            new Role { Slot = 1, Size = 14, Bold = true, X = 20, Y = 6, Background = 0x303948 }
        };

        private static void AddRoleCandidates(List<Candidate> list)
        {
            AddRecipe(list, "roles-measured", BaseRoles());
            var roles = BaseRoles(); roles[0].Slot = 1; roles[0].Size = 15; roles[0].Bold = false; AddRecipe(list, "roles-arial15-menu", roles);
            roles = BaseRoles(); roles[0].Size = 17; roles[0].Bold = false; AddRecipe(list, "roles-calibri17-menu", roles);
            roles = BaseRoles(); roles[1].Size = 15; AddRecipe(list, "roles-quality15", roles);
            roles = BaseRoles(); roles[1].Size = 17; AddRecipe(list, "roles-quality17", roles);
            roles = BaseRoles(); roles[1].Slot = 1; roles[1].Size = 15; AddRecipe(list, "roles-quality-arial15", roles);
            roles = BaseRoles(); roles[2].Tracking = .25f; AddRecipe(list, "roles-micro-track025", roles);
            roles = BaseRoles(); roles[2].Tracking = .5f; AddRecipe(list, "roles-micro-track05", roles);
            roles = BaseRoles(); roles[2].Slot = 0; roles[2].Size = 11; roles[2].Tracking = .5f; AddRecipe(list, "roles-micro-calibri11", roles);
            roles = BaseRoles(); roles[3].Size = roles[3].SuffixSize = 17; AddRecipe(list, "roles-title17", roles);
            roles = BaseRoles(); roles[3].Size = 17; AddRecipe(list, "roles-title-prefix17", roles);
            roles = BaseRoles(); roles[3].SuffixGap = 0; AddRecipe(list, "roles-title-gap0", roles);
            roles = BaseRoles(); roles[3].SuffixGap = 1.5f; AddRecipe(list, "roles-title-gap15", roles);
            roles = BaseRoles(); roles[3].SuffixTint = 0xB6C6D4; AddRecipe(list, "roles-title-secondary", roles);
            roles = BaseRoles(); roles[4].Slot = 0; roles[4].Size = 16; roles[4].Bold = false; AddRecipe(list, "roles-mine-calibri16", roles);
            roles = BaseRoles(); roles[4].Size = 15; roles[4].Bold = false; AddRecipe(list, "roles-mine-arial15", roles);
            roles = BaseRoles(); roles[0].Tint = roles[3].Tint = roles[4].Tint = 0xFFFFFF; AddRecipe(list, "roles-bright-main", roles);
            roles = BaseRoles(); foreach (var role in roles) role.Y += .5f; AddRecipe(list, "roles-baseline-down-half", roles);
            roles = BaseRoles(); foreach (var role in roles) role.Y -= .5f; AddRecipe(list, "roles-baseline-up-half", roles);
            // Final bounded combinations follow actual v0.5.0 geometry, not automatic raster registration.
            roles = RefinedRoles(); AddRecipe(list, "roles-refined", roles);
            roles = RefinedRoles(); roles[3].SuffixTint = 0xB6C6D4; AddRecipe(list, "roles-refined-title-secondary", roles);
            roles = RefinedRoles(); roles[3].SuffixTint = 0x84929F; AddRecipe(list, "roles-refined-title-muted", roles);
            roles = RefinedRoles(); roles[3].SuffixTint = 0xD8E2EA; AddRecipe(list, "roles-refined-title-primary", roles);
        }

        private static Role[] RefinedRoles()
        {
            var roles = BaseRoles();
            roles[1].Slot = 1; roles[1].Size = 15; roles[1].Y = -1;
            roles[3].Size = roles[3].SuffixSize = 17; roles[3].Y = 5; roles[3].SuffixGap = 0;
            roles[4].Slot = 0; roles[4].Size = 15; roles[4].Y = 7;
            return roles;
        }

        private static void AddRecipe(List<Candidate> list, string id, Role[] roles)
        {
            foreach (var role in roles)
            {
                role.Style = Clone(Text.fontStyles[role.Slot]); role.Style.fontSize = role.Size;
                role.Style.fontStyle = role.Bold ? FontStyle.Bold : FontStyle.Normal;
                role.Style.normal.textColor = Theme.Rgb(role.Tint);
                role.SuffixStyle = Clone(role.Style); role.SuffixStyle.fontSize = role.SuffixSize > 0 ? role.SuffixSize : role.Size;
                role.SuffixStyle.fontStyle = FontStyle.Normal; role.SuffixStyle.normal.textColor = Theme.Rgb(role.SuffixTint);
            }
            string description = id + " | measured role offsets, native font/tint hypotheses; no source acceptance";
            list.Add(new Candidate { Id = id, Description = description, NativeSlot = "per-role", Style = roles[0].Style,
                NormalStyle = roles[3].SuffixStyle, Header = new GUIContent(description), Roles = roles, HeadingTracking = roles[2].Tracking });
        }

        private static void Mix(ref ulong hash, int value) { unchecked { hash = (hash ^ (uint)value) * 1099511628211UL; } }
        private static void MixColor(ref ulong hash, Color value)
        { Mix(ref hash, value.r.GetHashCode()); Mix(ref hash, value.g.GetHashCode()); Mix(ref hash, value.b.GetHashCode()); Mix(ref hash, value.a.GetHashCode()); }
        private static void CaptureNative(ulong[] destination)
        {
            for (int slot = 0; slot < 3; slot++)
            {
                var style = Text.fontStyles[slot]; ulong hash = 14695981039346656037UL;
                Mix(ref hash, style.font == null ? 0 : style.font.GetInstanceID()); Mix(ref hash, style.fontSize); Mix(ref hash, (int)style.fontStyle);
                Mix(ref hash, (int)style.alignment); Mix(ref hash, style.wordWrap ? 1 : 0); Mix(ref hash, style.richText ? 1 : 0); Mix(ref hash, (int)style.clipping);
                Mix(ref hash, style.contentOffset.x.GetHashCode()); Mix(ref hash, style.contentOffset.y.GetHashCode());
                Mix(ref hash, style.fixedWidth.GetHashCode()); Mix(ref hash, style.fixedHeight.GetHashCode());
                Mix(ref hash, style.stretchWidth ? 1 : 0); Mix(ref hash, style.stretchHeight ? 1 : 0);
                Mix(ref hash, style.padding.left); Mix(ref hash, style.padding.right); Mix(ref hash, style.padding.top); Mix(ref hash, style.padding.bottom);
                Mix(ref hash, style.margin.left); Mix(ref hash, style.margin.right); Mix(ref hash, style.margin.top); Mix(ref hash, style.margin.bottom);
                MixColor(ref hash, style.normal.textColor); MixColor(ref hash, style.hover.textColor); MixColor(ref hash, style.active.textColor); MixColor(ref hash, style.focused.textColor);
                MixColor(ref hash, style.onNormal.textColor); MixColor(ref hash, style.onHover.textColor); MixColor(ref hash, style.onActive.textColor); MixColor(ref hash, style.onFocused.textColor);
                destination[slot] = hash;
            }
        }
        private static void FinishNativeProbe()
        {
            CaptureNative(nativeAfter); nativeProbeExecuted = true; nativeUnchanged = true;
            for (int slot = 0; slot < 3; slot++) if (nativeBefore[slot] != nativeAfter[slot]) nativeUnchanged = false;
            allNativeDrawsUnchanged &= nativeUnchanged;
        }

        private static Candidate At(int index)
        {
            if (index < 0 || index >= candidates.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return candidates[index];
        }
        internal static string GetCandidateId(int index) => At(index).Id;
        internal static string GetCandidateDescription(int index) => At(index).Description;

        // Exact specimen contract: 760 x 286 GUI units. Smaller surfaces clip; no scaling or input capture.
        internal static void Draw(Rect bounds, int candidateIndex = 0)
        {
            if (Event.current == null || !PrimitiveGeometry.Drawable(bounds.x, bounds.y, bounds.width, bounds.height)) return;
            var candidate = At(candidateIndex);
            CaptureNative(nativeBefore);
            try
            {
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.backgroundColor = GUI.contentColor = Color.white;
                GUI.enabled = true;
                GUI.BeginGroup(bounds);
                try
                {
                    Widgets.DrawBoxSolid(new Rect(0, 0, 760, 286), Theme.Rgb(0x0E1317));
                    GUI.Label(new Rect(8, 8, 744, 26), candidate.Header, metadataStyle);
                    for (int row = 0; row < Samples.Length; row++)
                    {
                        var sample = Samples[row];
                        var cell = new Rect(180, 40 + row * 40, 400, 32);
                        GUI.Label(new Rect(8, cell.y, 168, 32), sample.Label, metadataStyle);
                        if (Event.current.type == EventType.Repaint)
                        {
                            Vector2 top = GUIUtility.GUIToScreenPoint(cell.min), bottom = GUIUtility.GUIToScreenPoint(cell.max);
                            lastScreenCells[row] = Rect.MinMaxRect(top.x, top.y, bottom.x, bottom.y);
                        }
                        GUI.BeginGroup(cell);
                        try {
                            if (candidate.Roles == null) DrawSample(sample, candidate, cell.width, cell.height);
                            else DrawRole(sample, candidate.Roles[row], cell.width, cell.height);
                        }
                        finally { GUI.EndGroup(); }
                        GUI.Label(new Rect(600, cell.y, 152, 32), GlyphProbe,
                            candidate.Roles == null ? candidate.Style : candidate.Roles[row].Style);
                    }
                    GUI.Label(new Rect(8, 244, 744, 38), Footer, metadataStyle);
                    if (Event.current.type == EventType.Repaint) { lastCandidate = candidateIndex; lastFrame = Time.frameCount; lastDrawComplete = bounds.width >= 760 && bounds.height >= 286; }
                }
                finally { GUI.EndGroup(); }
            }
            }
            finally { FinishNativeProbe(); }
        }

        private static void DrawRole(Sample sample, Role role, float width, float height)
        {
            Widgets.DrawBoxSolid(new Rect(0, 0, width, height), Theme.Rgb(role.Background));
            if (sample.Id == "pawn-title")
            {
                GUI.Label(new Rect(role.X, role.Y, width - role.X, height - role.Y), Prefix, role.Style);
                float advance = role.Style.CalcSize(Prefix).x + role.SuffixGap;
                GUI.Label(new Rect(role.X + advance, role.Y, width - role.X - advance, height - role.Y), Suffix, role.SuffixStyle);
            }
            else if (role.Tracking != 0)
            {
                for (int index = 0; index < sample.Characters.Length; index++)
                {
                    float x = role.Style.GetCursorPixelPosition(new Rect(0, 0, width, height), sample.Content, index).x + index * role.Tracking;
                    GUI.Label(new Rect(role.X + x, role.Y, width - role.X - x, height - role.Y), sample.Characters[index], role.Style);
                }
            }
            else GUI.Label(new Rect(role.X, role.Y, width - role.X, height - role.Y), sample.Content, role.Style);
        }

        private static Vector2 MeasureRole(Sample sample, Role role)
        {
            if (sample.Id == "pawn-title")
            {
                Vector2 prefix = role.Style.CalcSize(Prefix), suffix = role.SuffixStyle.CalcSize(Suffix);
                return new Vector2(prefix.x + role.SuffixGap + suffix.x, Mathf.Max(prefix.y, suffix.y));
            }
            Vector2 size = role.Style.CalcSize(sample.Content);
            size.x += Math.Max(0, sample.Characters.Length - 1) * role.Tracking;
            return size;
        }

        private static void DrawSample(Sample sample, Candidate candidate, float width, float height)
        {
            if (sample.Id == "pawn-title")
            {
                GUI.Label(new Rect(0, 0, width, height), Prefix, candidate.Style);
                float advance = candidate.Style.CalcSize(Prefix).x;
                GUI.Label(new Rect(advance, 0, width - advance, height), Suffix, candidate.NormalStyle);
            }
            else if (sample.Id == "micro-heading" && candidate.HeadingTracking > 0)
            {
                float advance = 0;
                foreach (var character in sample.Characters)
                {
                    GUI.Label(new Rect(advance, 0, width - advance, height), character, candidate.Style);
                    advance += candidate.Style.CalcSize(character).x + candidate.HeadingTracking;
                }
            }
            else GUI.Label(new Rect(0, 0, width, height), sample.Content, candidate.Style);
        }

        private static Vector2 Measure(Sample sample, Candidate candidate)
        {
            if (sample.Id == "pawn-title")
            {
                Vector2 prefix = candidate.Style.CalcSize(Prefix), suffix = candidate.NormalStyle.CalcSize(Suffix);
                return new Vector2(prefix.x + suffix.x, Mathf.Max(prefix.y, suffix.y));
            }
            if (sample.Id == "micro-heading" && candidate.HeadingTracking > 0)
                return new Vector2(sample.Characters.Sum(character => candidate.Style.CalcSize(character).x) +
                    (sample.Characters.Length - 1) * candidate.HeadingTracking, candidate.Style.CalcSize(sample.Content).y);
            return candidate.Style.CalcSize(sample.Content);
        }

        // Main thread after Draw/Repaint. Export is immutable and identifies the loaded font, not an invented font file hash.
        internal static string WriteReport(string directory, int candidateIndex = 0, int expectedFrame = -1)
        {
            var candidate = At(candidateIndex);
            if (expectedFrame >= 0 && (lastFrame != expectedFrame || lastCandidate != candidateIndex))
                throw new InvalidOperationException("Typography capture does not match the last rendered candidate/frame.");
            Directory.CreateDirectory(directory);
            var text = new StringBuilder();
            text.Append("{\n\"schema_version\":1,\"kind\":\"native-font-calibration\",\"identity_status\":\"candidate; source match unverified\",");
            text.Append("\n\"candidate_index\":" + candidateIndex + ",\"candidate_id\":" + Quote(candidate.Id));
            text.Append(",\"description\":" + Quote(candidate.Description));
            text.Append(",\"native_slot\":" + Quote(candidate.NativeSlot));
            var font = candidate.Style.font;
            text.Append(",\"font_name\":" + Quote(font.name) + ",\"font_names\":[" + string.Join(",", (font.fontNames ?? new string[0]).Select(Quote)) + "]");
            text.Append(",\"font_instance_id\":" + font.GetInstanceID() + ",\"font_dynamic\":" + (font.dynamic ? "true" : "false"));
            text.Append(",\"font_asset_size\":" + font.fontSize + ",\"font_ascent\":" + font.ascent + ",\"font_line_height\":" + font.lineHeight);
            text.Append(",\"style_size\":" + candidate.Style.fontSize + ",\"style_weight\":" + Quote(candidate.Style.fontStyle.ToString()));
            text.Append(",\"style_line_height\":" + Number(candidate.Style.lineHeight));
            text.Append(",\"content_offset\":[" + Number(candidate.Style.contentOffset.x) + "," + Number(candidate.Style.contentOffset.y) + "]");
            text.Append(",\"padding_lrtb\":[" + candidate.Style.padding.left + "," + candidate.Style.padding.right + "," + candidate.Style.padding.top + "," + candidate.Style.padding.bottom + "]");
            text.Append(",\"micro_tracking_gui_units\":" + Number(candidate.HeadingTracking));
            text.Append(",\"tiny_supported\":" + (Text.TinyFontSupported ? "true" : "false") + ",\"ui_scale\":" + Number(Prefs.UIScale));
            text.Append(",\"screen_pixels\":[" + Screen.width + "," + Screen.height + "],\"ui_dimensions\":[" + UI.screenWidth + "," + UI.screenHeight + "]");
            text.Append(",\"game_version\":" + Quote(RimWorld.VersionControl.CurrentVersionStringWithRev) + ",\"unity_version\":" + Quote(Application.unityVersion));
            text.Append(",\"last_draw_candidate_index\":" + lastCandidate + ",\"last_draw_frame\":" + lastFrame);
            text.Append(",\"captured_frame\":" + (expectedFrame >= 0 ? expectedFrame.ToString(CultureInfo.InvariantCulture) : "null"));
            text.Append(",\"draw_complete\":" + (lastDrawComplete ? "true" : "false"));
            text.Append(",\"coordinate_status\":\"GUIToScreenPoint snapshot; integer source/client 1:1 requires capture calibration; no automatic scaling\"");
            text.Append(",\"native_style_probe\":{\"executed\":" + (nativeProbeExecuted ? "true" : "false") +
                ",\"unchanged\":" + (nativeUnchanged ? "true" : "false") + ",\"all_draws_unchanged\":" + (allNativeDrawsUnchanged ? "true" : "false") +
                ",\"construction_unchanged\":" + (nativeConstructionUnchanged ? "true" : "false") +
                ",\"algorithm\":\"FNV1a64 of native scalar field hash codes; observational fingerprint, not cryptographic proof\",\"fields\":\"font instance,size,style,alignment,wrap,richText,clipping,contentOffset,fixed dimensions,stretch,padding,margin,all eight text-state colors\",\"before\":[" +
                string.Join(",", nativeBefore.Select(value => Quote(value.ToString("X16")))) + "],\"after\":[" +
                string.Join(",", nativeAfter.Select(value => Quote(value.ToString("X16")))) + "],\"construction_before\":[" +
                string.Join(",", nativeConstructionBefore.Select(value => Quote(value.ToString("X16")))) + "],\"construction_after\":[" +
                string.Join(",", nativeConstructionAfter.Select(value => Quote(value.ToString("X16")))) + "]}");
            text.Append(",\"font_file_hash\":null,\"font_provenance\":\"reference to existing Text.fontStyles font; no font asset created or loaded; raw native font bytes unavailable through this API\",");
            text.Append("\n\"samples\":[");
            for (int row = 0; row < Samples.Length; row++)
            {
                var sample = Samples[row];
                var role = candidate.Roles == null ? null : candidate.Roles[row];
                var style = role == null ? candidate.Style : role.Style;
                var measured = role == null ? Measure(sample, candidate) : MeasureRole(sample, role);
                if (row > 0) text.Append(',');
                text.Append("\n{\"id\":" + Quote(sample.Id) + ",\"text\":" + Quote(sample.Text) + ",\"board\":" + Quote(sample.Board));
                text.Append(",\"source_crop\":" + Quote(sample.CropFile) + ",\"source_rect_xyxy\":[" + string.Join(",", sample.SourceRect) + "]");
                text.Append(",\"specimen_cell_xywh\":[180," + (40 + row * 40) + ",400,32]");
                text.Append(",\"screen_cell_xywh\":" + (lastCandidate == candidateIndex ? RectJson(lastScreenCells[row]) : "null"));
                text.Append(",\"measured_size_gui_units\":[" + Number(measured.x) + "," + Number(measured.y) + "]");
                text.Append(",\"overflow\":" + (measured.x + (role?.X ?? 0) > 400 || measured.y + (role?.Y ?? 0) > 32 ? "true" : "false"));
                text.Append(",\"font_name\":" + Quote(style.font.name) + ",\"font_names\":[" + string.Join(",", (style.font.fontNames ?? new string[0]).Select(Quote)) + "]");
                text.Append(",\"style_size\":" + style.fontSize + ",\"style_weight\":" + Quote(style.fontStyle.ToString()));
                text.Append(",\"role_recipe\":" + (role == null ? "null" : Quote(candidate.Id)));
                text.Append(",\"draw_offset_xy\":[" + Number(role?.X ?? 0) + "," + Number(role?.Y ?? 0) + "]");
                text.Append(",\"tracking_gui_units\":" + Number(role?.Tracking ?? (sample.Id == "micro-heading" ? candidate.HeadingTracking : 0)));
                if (role != null)
                {
                    text.Append(",\"tint_hex\":" + Quote(role.Tint.ToString("X6")) + ",\"background_hex\":" + Quote(role.Background.ToString("X6")));
                    text.Append(",\"suffix_tint_hex\":" + Quote(role.SuffixTint.ToString("X6")) + ",\"suffix_size\":" + role.SuffixStyle.fontSize + ",\"suffix_extra_gap\":" + Number(role.SuffixGap));
                }
                text.Append(",\"probe_text\":" + Quote(GlyphProbe.text) + ",\"probe_specimen_cell_xywh\":[600," + (40 + row * 40) + ",152,32]");
                text.Append(",\"weight_rule\":" + Quote(sample.Id == "pawn-title" ? "Futa prefix candidate weight; Healer suffix Normal; independent segments" : "candidate weight"));
                text.Append('}');
            }
            text.Append("\n]}\n");
            string path = Path.GetFullPath(Path.Combine(directory, "typography-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" + candidate.Id + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json"));
            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
            return path;
        }

        private static string RectJson(Rect rect) => "[" + Number(rect.x) + "," + Number(rect.y) + "," + Number(rect.width) + "," + Number(rect.height) + "]";
        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string Quote(string value)
        {
            var text = new StringBuilder("\"");
            foreach (char character in value ?? "")
                if (character == '\\') text.Append("\\\\");
                else if (character == '"') text.Append("\\\"");
                else if (character < 32) text.Append("\\u" + ((int)character).ToString("x4"));
                else text.Append(character);
            return text.Append('"').ToString();
        }
    }
}
