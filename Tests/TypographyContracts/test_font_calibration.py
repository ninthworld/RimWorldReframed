"""Self-tests for calibration diagnostics, not Unity/font integration tests."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from PIL import Image

REPO = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("font_calibration", REPO / "Tools/font_calibration.py")
fc = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fc)


class TypographyContracts(unittest.TestCase):
    def test_source_provenance_and_five_strings(self):
        inventory = fc.source_inventory(REPO)
        self.assertEqual(len(inventory["sources"]), 5)
        self.assertEqual([row["text"] for row in inventory["sources"]], ["Load", "Quality", "BASICS", "Futa Healer", "Mine"])
        self.assertTrue(all(len(row["crop_sha256"]) == 64 for row in inventory["sources"]))
        self.assertEqual(inventory["sources"][0]["ink"]["ink_size"], [31, 12])

    def test_transparent_pixels_are_not_treated_as_visible_ink(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "transparent.png"
            Image.new("RGBA", (2, 2), (255, 255, 255, 0)).save(path)
            with self.assertRaises(ValueError):
                fc.png(path)

    def test_dark_and_chromatic_background_excluded(self):
        image = Image.new("RGB", (3, 1))
        image.putdata([(14, 19, 23), (0, 255, 170), (205, 215, 225)])
        self.assertEqual(list(fc.pixels(fc.ink_mask(image))), [0, 0, 255])

    def test_empty_mask_is_invalid(self):
        with self.assertRaises(ValueError):
            fc.compare_masks(Image.new("L", (5, 5)), Image.new("L", (5, 5)))

    def test_diagnostic_translation_is_disclosed_not_exact(self):
        left, right = Image.new("L", (8, 8)), Image.new("L", (8, 8))
        left.putpixel((1, 1), 255); right.putpixel((5, 6), 255)
        measured, _ = fc.compare_masks(left, right)
        self.assertEqual(measured["diagnostic_ink_iou"], 1)
        self.assertNotEqual(left.tobytes(), right.tobytes())
        self.assertNotEqual(measured["source_ink"]["bbox_xyxy"], measured["actual_ink"]["bbox_xyxy"])
        self.assertIn("no scaling", measured["normalization"])

    def test_fixed_origin_does_not_hide_translation(self):
        left, right = Image.new("RGB", (8, 8)), Image.new("RGB", (12, 12))
        left.putpixel((1, 1), (255, 255, 255)); right.putpixel((5, 6), (255, 255, 255))
        normalized, _ = fc.compare_masks(fc.ink_mask(left), fc.ink_mask(right))
        fixed = fc.fixed_origin_metrics(left, right)
        self.assertEqual(normalized["diagnostic_ink_iou"], 1)
        self.assertEqual(fixed["mask_iou"], 0)
        self.assertEqual(fixed["raw_rgb_unequal_pixels"], 2)
        self.assertIn("zero alignment", fixed["criterion"])

    def test_fixed_origin_detects_tint_difference_without_shape_change(self):
        left, right = Image.new("RGB", (4, 4)), Image.new("RGB", (8, 8))
        left.putpixel((1, 1), (210, 220, 230)); right.putpixel((1, 1), (211, 220, 230))
        fixed = fc.fixed_origin_metrics(left, right)
        self.assertEqual(fixed["mask_iou"], 1)
        self.assertEqual(fixed["raw_rgb_unequal_pixels"], 1)
        self.assertGreater(fixed["raw_rgb_mean_absolute_channel_error"], 0)
        self.assertEqual(fixed["source_edge_rgb"]["unequal_pixels"], 1)
        self.assertGreater(fixed["source_edge_rgb"]["mean_absolute_channel_error"], 0)
        self.assertIn("source-only", fixed["source_edge_rgb"]["mask_rule"])

    def test_fixed_origin_identical_source_is_exact(self):
        left = Image.new("RGB", (4, 4)); left.putpixel((1, 1), (210, 220, 230))
        fixed = fc.fixed_origin_metrics(left, left.copy())
        self.assertEqual(fixed["mask_iou"], 1)
        self.assertEqual(fixed["raw_rgb_unequal_pixels"], 0)
        self.assertEqual(fixed["raw_rgb_mean_absolute_channel_error"], 0)

    def test_missing_glyph_pixel_is_detected(self):
        left = Image.new("L", (3, 3), 255)
        right = left.copy(); right.putpixel((1, 1), 0)
        measured, _ = fc.compare_masks(left, right)
        self.assertEqual(measured["unequal_mask_pixels"], 1)
        self.assertLess(measured["diagnostic_ink_iou"], 1)

    def test_size_is_not_scaled_away(self):
        measured, _ = fc.compare_masks(Image.new("L", (2, 2), 255), Image.new("L", (3, 3), 255))
        self.assertEqual((measured["width_delta"], measured["height_delta"]), (1, 1))
        self.assertLess(measured["diagnostic_ink_iou"], 1)

    def test_invalid_physical_cells_rejected(self):
        for cell in ([0.5, 0, 10, 10], [True, 0, 10, 10], [-1, 0, 10, 10], [0, 0, 0, 10], [95, 0, 10, 10], None):
            with self.subTest(cell=cell), self.assertRaises(ValueError):
                fc.integer_cell(cell, (100, 100))

    def test_output_does_not_overwrite_evidence(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); evidence = root / "evidence.png"; evidence.write_bytes(b"evidence")
            with self.assertRaises(ValueError): fc.fresh_directory(root, [evidence])
            output = root / "occupied"; output.mkdir(); (output / "existing").write_text("keep")
            with self.assertRaises(ValueError): fc.fresh_directory(output, [])

    def make_synthetic_capture(self, root):
        image = Image.new("RGB", (800, 286), (14, 19, 23))
        rows = []
        for row, (sample_id, text, board, rect) in enumerate(fc.SAMPLES):
            image.paste(fc.png(REPO / "Docs/UI-Reworked/measurements" / (sample_id + ".png")), (180, 40 + row * 40))
            rows.append({"id": sample_id, "text": text, "source_rect_xyxy": list(rect),
                         "screen_cell_xywh": [180, 40 + row * 40, 400, 32], "overflow": False})
        capture = root / "synthetic.png"; image.save(capture)
        descriptor = {"schema_version": 1, "kind": "native-font-calibration", "candidate_index": 0,
                      "last_draw_candidate_index": 0, "last_draw_frame": 10, "captured_frame": 10, "draw_complete": True, "ui_scale": 1, "screen_pixels": list(image.size),
                      "candidate_id": "synthetic-source-copy-not-a-font", "font_name": "test-only", "style_size": 0,
                      "style_weight": "test-only", "samples": rows}
        metadata = root / "synthetic.json"; metadata.write_text(json.dumps(descriptor))
        return capture, metadata, descriptor

    def test_full_scoring_cli_contract_and_ranking(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); capture, metadata, _ = self.make_synthetic_capture(root)
            result = fc.score(REPO, capture, metadata, root / "result")
            self.assertEqual(len(result["samples"]), 5)
            self.assertTrue(all(row["diagnostic_ink_iou"] == 1 for row in result["samples"]))
            self.assertIn("none", result["acceptance"])
            ranking = fc.rank([root / "result/result.json"])
            self.assertEqual(len(ranking["by_role"]), 5)
            fixed_ranking = fc.rank([root / "result/result.json"], "fixed")
            self.assertEqual(fixed_ranking["ranking_metric"], "fixed")
            self.assertTrue(all(rows[0]["fixed_origin"]["mask_iou"] == 1 for rows in fixed_ranking["by_role"].values()))
            self.assertEqual(len(list((root / "result").glob("*.png"))), 10)

    def test_nonopaque_outside_compared_cells_is_accepted_and_preserved(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); capture, metadata, _ = self.make_synthetic_capture(root)
            image = fc.png(capture).convert("RGBA")
            image.putpixel((0, 0), (255, 255, 255, 0)); image.save(capture)
            original_hash = fc.digest(capture)
            result = fc.score(REPO, capture, metadata, root / "outside-alpha")
            self.assertEqual(result["capture"]["full_frame_alpha_range"], [0, 255])
            self.assertEqual(result["capture"]["sha256"], original_hash)
            self.assertEqual(fc.digest(capture), original_hash)
            self.assertTrue(all(row["diagnostic_ink_iou"] == 1 for row in result["samples"]))

    def test_nonopaque_inside_compared_cell_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); capture, metadata, _ = self.make_synthetic_capture(root)
            image = fc.png(capture).convert("RGBA")
            image.putpixel((180, 40), (255, 255, 255, 0)); image.save(capture)
            with self.assertRaisesRegex(ValueError, "opaque actual typography cells"):
                fc.score(REPO, capture, metadata, root / "inside-alpha")

    def test_native_style_probe_detects_unrun_or_mutated_state(self):
        valid = {"executed": True, "unchanged": True, "all_draws_unchanged": True, "construction_unchanged": True,
                 "before": ["1234567890abcdef"] * 3, "after": ["1234567890abcdef"] * 3,
                 "construction_before": ["0123456789abcdef"] * 3, "construction_after": ["0123456789abcdef"] * 3}
        fc.validate_native_style_probe(valid)
        for key in ("executed", "unchanged", "all_draws_unchanged", "construction_unchanged"):
            mutation = json.loads(json.dumps(valid)); mutation[key] = False
            with self.subTest(key=key), self.assertRaises(ValueError):
                fc.validate_native_style_probe(mutation)
        changed = json.loads(json.dumps(valid)); changed["after"][1] = "ffffffffffffffff"
        with self.assertRaisesRegex(ValueError, "fingerprints changed"):
            fc.validate_native_style_probe(changed)
        truncated = json.loads(json.dumps(valid)); truncated["construction_before"] = []
        with self.assertRaises(ValueError): fc.validate_native_style_probe(truncated)

    def test_current_candidate_with_prior_draw_frame_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); capture, metadata, descriptor = self.make_synthetic_capture(root)
            descriptor["captured_frame"] = 11
            metadata.write_text(json.dumps(descriptor))
            with self.assertRaisesRegex(ValueError, "Captured frame differs"):
                fc.score(REPO, capture, metadata, root / "stale-frame")

    def test_stale_scale_duplicate_and_identity_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); capture, metadata, original = self.make_synthetic_capture(root)
            mutations = []
            stale = json.loads(json.dumps(original)); stale["last_draw_candidate_index"] = 1; mutations.append(stale)
            clipped = json.loads(json.dumps(original)); clipped["draw_complete"] = False; mutations.append(clipped)
            scale = json.loads(json.dumps(original)); scale["ui_scale"] = 1.5; mutations.append(scale)
            duplicate = json.loads(json.dumps(original)); duplicate["samples"][0] = duplicate["samples"][1]; mutations.append(duplicate)
            identity = json.loads(json.dumps(original)); identity["samples"][0]["text"] = "Wrong"; mutations.append(identity)
            dimensions = json.loads(json.dumps(original)); dimensions["screen_pixels"] = [20, 20]; mutations.append(dimensions)
            for number, mutation in enumerate(mutations):
                with self.subTest(number=number), self.assertRaises(ValueError):
                    metadata.write_text(json.dumps(mutation))
                    fc.score(REPO, capture, metadata, root / ("invalid" + str(number)))


if __name__ == "__main__":
    unittest.main()
