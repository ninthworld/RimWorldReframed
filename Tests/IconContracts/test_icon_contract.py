import importlib.util
import json
import sys
import unittest
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools"))
spec = importlib.util.spec_from_file_location("icon_reconstruction", ROOT / "Tools" / "icon_reconstruction.py")
icon_reconstruction = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(icon_reconstruction)


class IconContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.contract = json.loads(icon_reconstruction.CONTRACT_PATH.read_text(encoding="utf-8"))

    def test_source_hashes_and_threshold_bounds_match_canonical_pixels(self):
        self.assertEqual([], icon_reconstruction.audit_contract())

    def test_each_vector_has_nonempty_transparent_canvas_and_stable_source_bounds(self):
        for icon in self.contract["icons"]:
            with self.subTest(icon=icon["id"]):
                width, height = map(int, icon["viewbox"].split("x"))
                image = icon_reconstruction.draw_vector(icon, 1)
                self.assertEqual((width, height), image.size)
                self.assertIsNotNone(image.getchannel("A").getbbox())
                x1, y1, x2, y2 = icon["source_bounds_xyxy"]
                self.assertEqual((width, height), (x2 - x1, y2 - y1))

    def test_offline_vector_geometry_uses_source_sized_bounded_canvases(self):
        recorded = self.contract["candidate_mask_metrics"]["icons"]
        for icon in self.contract["icons"]:
            with self.subTest(icon=icon["id"]):
                source_path = icon_reconstruction.DOCS / icon["source_file"]
                with Image.open(source_path) as board:
                    x1, y1, x2, y2 = icon["source_bounds_xyxy"]
                    crop = board.convert("RGB").crop((x1, y1, x2, y2))
                threshold = icon["pixel_threshold"]
                source_mask = []
                for y in range(crop.height):
                    for x in range(crop.width):
                        pixel = crop.getpixel((x, y))
                        source_mask.append(max(pixel) > threshold["max_channel_gt"]
                                           and sum(pixel) > threshold["rgb_sum_gt"])

                candidate = icon_reconstruction.draw_vector(icon, 1).getchannel("A")
                candidate_mask = [value > 0 for value in candidate.tobytes()]
                self.assertEqual((0, 0, crop.width, crop.height), candidate.getbbox(),
                                 "visible vector ink must reach the measured source bounds")
                candidate_ink = sum(candidate_mask)
                with self.subTest(icon=icon["id"], evidence="recorded metrics"):
                    self.assertEqual(candidate_ink, recorded[icon["id"]]["candidate_ink_px"])
                    self.assertEqual(sum(source_mask), recorded[icon["id"]]["source_ink_px"])

    def test_unknown_id_has_visible_non_source_fallback_and_false_status(self):
        fallback = self.contract["unknown_id"]
        self.assertIn("not a source icon", fallback["status"])
        renderer = (ROOT / "Source" / "ReframedIcons.cs").read_text(encoding="utf-8")
        self.assertIn("known ? GetVectorTexture(index) : GetUnknownTexture()", renderer)
        self.assertIn("return known;", renderer)
        self.assertIn("private static Texture2D CreateUnknownTexture", renderer)
        self.assertIn("RasterUnknown(alpha, width, height)", renderer)

    def test_draw_and_unknown_fallback_are_group_clipped_with_exception_safe_end(self):
        policy = self.contract["clip_policy"]
        self.assertIn("finally", policy["runtime"])
        self.assertIn("still required", policy["in_game_bleed_check"])
        renderer = (ROOT / "Source" / "ReframedIcons.cs").read_text(encoding="utf-8")
        self.assertIn("GUI.BeginGroup(bounds)", renderer)
        self.assertIn("var localBounds = new Rect(0f, 0f, bounds.width, bounds.height)", renderer)
        self.assertIn("if (groupStarted) GUI.EndGroup()", renderer)

    def test_runtime_glyphs_use_cached_bounded_supersampled_textures(self):
        renderer = (ROOT / "Source" / "ReframedIcons.cs").read_text(encoding="utf-8")
        draw = renderer.split("internal static bool Draw(string id", 1)[1].split("internal static void DrawSpecimen", 1)[0]
        self.assertIn("GetVectorTexture(index)", draw)
        self.assertIn("GetUnknownTexture()", draw)
        self.assertNotIn("new Texture2D", draw)
        self.assertNotIn("GUIUtility.RotateAroundPivot", renderer)
        self.assertIn("private const int RasterSamplesPerAxis = 8", renderer)
        self.assertIn("vectorTextures[index] = CreateGlyphTexture(index)", renderer)
        self.assertIn("texture.Apply(false, true)", renderer)
        self.assertIn("RasterLine(alpha, width, height", renderer)
        self.assertIn("RasterEllipse(alpha, width, height", renderer)

    def test_tint_and_native_identity_are_not_claimed_as_exact(self):
        evidence = self.contract["evidence_and_tint"]
        self.assertIn("unresolved", evidence["native_asset_match"])
        self.assertIn("caller", evidence["tint"])
        self.assertEqual("not accepted against source", self.contract["acceptance"]["status"])
        source_text = (ROOT / "Source" / "ReframedIcons.cs").read_text(encoding="utf-8")
        self.assertIn("float t = Icons[index].ProvisionalStrokePx", source_text)
        self.assertIn("provisional_stroke_px", source_text)

    def test_native_texture_records_are_candidates_not_source_matches(self):
        candidates = self.contract["native_asset_candidates"]
        audit = self.contract["native_asset_audit"]
        self.assertEqual("pass", audit["report_status"])
        self.assertIn("no source visual approval", audit["scope"])
        for key in ("assembly_sha256", "resources_assets_sha256", "resources_assets_resS_sha256"):
            self.assertRegex(audit[key], r"^[0-9a-f]{64}$")
        self.assertEqual({"menu.plus", "save.search", "menu.book"},
                         {item["candidate_for"] for item in candidates})
        for item in candidates:
            with self.subTest(candidate=item["candidate_for"]):
                expected_width = 60 if item["candidate_for"] == "menu.book" else 64
                self.assertEqual(expected_width, item["native_size"][0])
                self.assertTrue("not proven" in item["visual_assessment"]
                                or "not a suitable match" in item["visual_assessment"])
                self.assertIn("candidate", item["render_fit_status"])
                self.assertIn("not shipped", item["decoded_preview_path"])
                self.assertRegex(item["decoded_png_sha256"], r"^[0-9a-f]{64}$")
                self.assertRegex(item["serialized_object_sha256"], r"^[0-9a-f]{64}$")
                self.assertEqual("resources.assets.resS", item["stream_file"])

    def test_live_specimen_uses_exact_source_sizes_integral_rects_and_recorded_rgb(self):
        specimen = self.contract["specimen"]
        self.assertEqual([760, 286], specimen["size_px"])
        self.assertEqual([14, 19, 23], specimen["background_rgb"])
        self.assertEqual([132, 146, 159], specimen["tint_rgb"])
        self.assertFalse(specimen["extra_diagnostic_samples"])
        for icon in self.contract["icons"]:
            x, y, width, height = icon["specimen_rect_xywh"]
            sx1, sy1, sx2, sy2 = icon["source_bounds_xyxy"]
            with self.subTest(icon=icon["id"]):
                self.assertEqual((sx2 - sx1, sy2 - sy1), (width, height))
                self.assertEqual((int(x), int(y), int(width), int(height)), (x, y, width, height))
                self.assertLessEqual(x + width, specimen["size_px"][0])
                self.assertLessEqual(y + height, specimen["size_px"][1])

    def test_native_comparison_variant_changes_only_plus_and_search_at_same_rectangles(self):
        specimen = self.contract["specimen"]
        self.assertEqual(2, specimen["variant_count"])
        self.assertEqual(["vector-refined", "native-plus-search"],
                         [variant["id"] for variant in specimen["variants"]])
        renderer = (ROOT / "Source" / "ReframedIcons.cs").read_text(encoding="utf-8")
        self.assertIn("internal const int VariantCount = 2", renderer)
        self.assertIn("GetVariantId(int variant)", renderer)
        self.assertIn("TexButton.Plus : TexButton.Search", renderer)
        self.assertIn("GUI.DrawTexture(new Rect(0f, 0f, bounds.width, bounds.height), texture, ScaleMode.StretchToFill, true)", renderer)
        self.assertIn("if (variant != 1 || id != \"menu.plus\" && id != \"save.search\")", renderer)
        self.assertIn("DrawVariant(icon.Id, iconRect", renderer)
        ordinary_draw = renderer.split("internal static bool Draw(string id", 1)[1].split("internal static void DrawSpecimen", 1)[0]
        self.assertNotIn("DrawVariant", ordinary_draw)


if __name__ == "__main__":
    unittest.main()
