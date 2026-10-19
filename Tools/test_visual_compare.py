"""Harness defect probes. Synthetic fixtures are NOT game acceptance/goldens."""
from __future__ import annotations

import copy
import json
from pathlib import Path
import struct
import subprocess
import sys
import tempfile
import unittest
import zlib

from PIL import Image

import visual_compare as vc


ROOT = Path(__file__).resolve().parents[1]
FIXTURES = ROOT / "Tests" / "Fixtures" / "Visual"
TRACE = ROOT / "Tests" / "Fixtures" / "Input" / "exactly-once.json"
RECT = (0, 0, 16, 12)


class HarnessTests(unittest.TestCase):
    def setUp(self):
        # The managed Windows runtime's default TEMP can be unwritable. Keep all
        # disposable test output in this worktree's ignored scratch directory.
        scratch = (ROOT / "work" / "harness-tests").resolve()
        scratch.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix="case-", dir=scratch)
        self.root = Path(self.temp.name).resolve()
        self.root.relative_to(scratch)  # Verify recursive cleanup stays in scratch.
        self.addCleanup(self.temp.cleanup)

    def compare(self, name, **kwargs):
        return vc.compare(FIXTURES / "reference.png", FIXTURES / name, RECT, RECT,
                          self.root / "result", **kwargs)

    def assert_errors(self, name, count, total, bbox):
        result = self.compare(name)
        measured = result["metrics"]
        self.assertEqual(result["status"], "fail" if count else "pass")
        self.assertEqual(measured["included_pixels"], 192)
        self.assertEqual(measured["mismatched_pixels"], count)
        self.assertEqual(measured["total_absolute_channel_error"], total)
        self.assertEqual(measured["maximum_channel_error"], 255 if count else 0)
        self.assertEqual(measured["mean_absolute_channel_error"], total / 576)
        self.assertEqual(measured["mismatch_fraction"], count / 192)
        self.assertEqual(measured["difference_bbox_xywh"], bbox)
        self.assertEqual(sum(r["pixels"] for r in result["connected_regions"]), count)
        self.assertEqual(json.loads((self.root / "result" / "result.json").read_text()), result)
        return result

    def test_identical(self):
        result = self.assert_errors("reference.png", 0, 0, None)
        self.assertEqual(result["connected_regions"], [])
        for name in ("raw_diff.png", "heatmap.png"):
            with Image.open(self.root / "result" / name) as image:
                self.assertIsNone(image.getbbox())
        for name, record in result["artifacts"].items():
            self.assertEqual(vc.sha256(self.root / "result" / name), record["sha256"])
        with Image.open(self.root / "result" / "side_by_side.png") as image:
            self.assertEqual(image.size, (32, 12))

    def test_one_pixel_shift_is_not_registered_away(self):
        self.assert_errors("shift-right-one.png", 22, 16830, [2, 2, 11, 5])

    def test_altered_color(self):
        self.assert_errors("altered-color.png", 1, 510, [2, 2, 1, 1])

    def test_diagnostics_contain_known_error_and_unscaled_crops(self):
        self.compare("altered-color.png")
        expected = {"raw_diff.png": (0, 255, 255), "heatmap.png": (255, 0, 0),
                    "overlay.png": (255, 127, 127)}
        for name, pixel in expected.items():
            with Image.open(self.root / "result" / name) as image:
                self.assertEqual(image.getpixel((2, 2)), pixel)
        with Image.open(self.root / "result" / "side_by_side.png") as image:
            self.assertEqual(image.getpixel((2, 2)), (255, 255, 255))
            self.assertEqual(image.getpixel((18, 2)), (255, 0, 0))

    def test_missing_icon(self):
        result = self.assert_errors("absent-icon.png", 9, 6885, [2, 2, 3, 3])
        self.assertEqual(result["connected_regions"], [{"pixels": 9, "bbox_xywh": [2, 2, 3, 3]}])

    def test_altered_glyph(self):
        self.assert_errors("altered-glyph.png", 1, 765, [10, 2, 1, 1])

    def test_mask_denominator_and_category_counts(self):
        result = self.compare("absent-icon.png", mask=FIXTURES / "content-mask.png",
                              region_masks={"icons": FIXTURES / "icon-mask.png", "text": FIXTURES / "text-mask.png"})
        self.assertEqual(result["metrics"]["included_pixels"], 24)
        self.assertEqual(result["metrics"]["mismatch_fraction"], 9 / 24)
        self.assertEqual(result["regions"]["icons"]["mismatched_pixels"], 9)
        self.assertEqual(result["regions"]["text"]["mismatched_pixels"], 0)

    def test_empty_mask_is_invalid_even_on_identical_images(self):
        with self.assertRaisesRegex(vc.InvalidInput, "includes no pixels"):
            self.compare("reference.png", mask=FIXTURES / "empty-mask.png")
        self.assertFalse((self.root / "result").exists())

    def test_nonbinary_and_wrong_sized_masks_are_invalid(self):
        for name, image in (("gray", Image.new("L", (16, 12), 128)),
                            ("wrong-size", Image.new("L", (15, 12), 255)),
                            ("rgb", Image.new("RGB", (16, 12), "white"))):
            with self.subTest(name=name):
                path = self.root / f"{name}.png"
                image.save(path)
                with self.assertRaises(vc.InvalidInput):
                    self.compare("reference.png", mask=path)

    def test_region_cannot_expand_main_inclusion(self):
        with self.assertRaisesRegex(vc.InvalidInput, "excluded by the main mask"):
            self.compare("reference.png", mask=FIXTURES / "icon-mask.png",
                         region_masks={"text": FIXTURES / "text-mask.png"})

    def test_dimensions_and_out_of_bounds_are_invalid(self):
        for reference_rect, actual_rect in ((RECT, (0, 0, 15, 12)),
                                           ((1, 0, 16, 12), RECT),
                                           ((0, 0, 0, 12), RECT),
                                           ((-1, 0, 16, 12), RECT)):
            with self.subTest(rects=(reference_rect, actual_rect)), self.assertRaises(vc.InvalidInput):
                vc.compare(FIXTURES / "reference.png", FIXTURES / "reference.png",
                           reference_rect, actual_rect, self.root / "result")

    def test_explicit_distinct_origins_map_difference_bounds(self):
        full_ref = Image.new("RGB", (20, 20))
        full_actual = Image.new("RGB", (24, 20))
        full_actual.putpixel((8, 8), (1, 0, 0))
        ref_path, actual_path = self.root / "ref.png", self.root / "actual.png"
        full_ref.save(ref_path)
        full_actual.save(actual_path)
        result = vc.compare(ref_path, actual_path, (1, 2, 4, 4), (6, 7, 4, 4), self.root / "result")
        self.assertEqual(result["metrics"]["difference_bbox_xywh"], [2, 1, 1, 1])
        self.assertEqual(result["difference_bbox_reference_xywh"], [3, 3, 1, 1])
        self.assertEqual(result["difference_bbox_actual_xywh"], [8, 8, 1, 1])
        self.assertEqual(result["metrics"]["maximum_channel_error"], 1)

    def test_non_png_missing_inputs_and_overwrite_are_invalid(self):
        wrong = self.root / "lossy.jpg"
        Image.new("RGB", (16, 12)).save(wrong)
        for path in (wrong, self.root / "missing.png", self.root):
            with self.subTest(path=path), self.assertRaises(vc.InvalidInput):
                vc.compare(path, FIXTURES / "reference.png", RECT, RECT, self.root / "result")
        self.compare("reference.png")
        with self.assertRaisesRegex(vc.InvalidInput, "new or empty"):
            self.compare("reference.png")

    def test_output_may_not_contain_inputs_or_write_tracked_tree(self):
        with self.assertRaisesRegex(vc.InvalidInput, "contain an input"):
            vc.prepare_output(FIXTURES, [FIXTURES / "reference.png"])
        with self.assertRaisesRegex(vc.InvalidInput, "ignored work/"):
            vc.prepare_output(ROOT / "Tests" / "not-an-output", [])

    def test_alpha_difference_requires_explicit_rgba_and_is_detected(self):
        reference, actual = self.root / "ref.png", self.root / "actual.png"
        Image.new("RGBA", (1, 1), (10, 20, 30, 255)).save(reference)
        Image.new("RGBA", (1, 1), (10, 20, 30, 254)).save(actual)
        with self.assertRaisesRegex(vc.InvalidInput, "require --channels rgba"):
            vc.compare(reference, actual, (0, 0, 1, 1), (0, 0, 1, 1), self.root / "result")
        result = vc.compare(reference, actual, (0, 0, 1, 1), (0, 0, 1, 1), self.root / "result", channels="rgba")
        self.assertEqual(result["metrics"]["mismatched_pixels"], 1)
        self.assertEqual(result["metrics"]["total_absolute_channel_error"], 1)
        self.assertEqual(result["metrics"]["mean_absolute_channel_error"], .25)
        with Image.open(self.root / "result" / "raw_alpha_diff.png") as image:
            self.assertEqual(image.getpixel((0, 0)), 1)

    def test_8bit_precision_cannot_be_silently_reduced(self):
        path = self.root / "sixteen.png"
        Image.new("I;16", (16, 12), 65535).save(path)
        with self.assertRaisesRegex(vc.InvalidInput, "Unsupported precision"):
            vc.compare(path, FIXTURES / "reference.png", RECT, RECT, self.root / "result")

    def test_truecolor_sixteen_bit_is_rejected_before_pillow_reduces_it(self):
        def chunk(kind, data):
            return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))

        # Valid 1x1 RGB16 PNG; Pillow would silently expose an RGB8 image.
        path = self.root / "rgb16.png"
        path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 16, 2, 0, 0, 0))
                         + chunk(b"IDAT", zlib.compress(b"\0\x12\x34\x56\x78\x9a\xbc")) + chunk(b"IEND", b""))
        with self.assertRaisesRegex(vc.InvalidInput, "bit depth 16"):
            vc.load_png(path)

    def trace(self, data):
        path = self.root / "trace.json"
        path.write_text(json.dumps(data), encoding="utf-8")
        return vc.validate_trace(path, self.root / "trace-result")

    def test_trace_exactly_once_pass_and_hover_without_dispatch(self):
        self.assertEqual(vc.validate_trace(TRACE, self.root / "result")["status"], "pass")

    def test_trace_duplicate_command_fails(self):
        data = json.loads(TRACE.read_text())
        duplicate = copy.deepcopy(data["dispatches"][0])
        duplicate.update(dispatch_id="dispatch-2", gui_event="Repaint")
        data["dispatches"].append(duplicate)
        result = self.trace(data)
        self.assertEqual(result["status"], "fail")
        self.assertIn({"kind": "dispatch_count", "action_id": "click-1", "expected": 1, "actual": 2}, result["failures"])

    def test_trace_wrong_target_and_command_fail(self):
        data = json.loads(TRACE.read_text())
        data["dispatches"][0].update(target_id="Pawn_202", command="DraftPawn")
        result = self.trace(data)
        self.assertEqual({row["kind"] for row in result["failures"]}, {"target_id_mismatch", "command_mismatch"})

    def test_trace_missing_unknown_and_hover_dispatch_fail(self):
        data = json.loads(TRACE.read_text())
        data["dispatches"][0]["action_id"] = "unknown"
        hover = copy.deepcopy(data["dispatches"][0])
        hover.update(action_id="hover-1", dispatch_id="hover-dispatch")
        data["dispatches"].append(hover)
        result = self.trace(data)
        self.assertEqual({row["kind"] for row in result["failures"]}, {"unknown_action", "dispatch_count"})

    def test_trace_duplicate_dispatch_identity_is_not_deduplicated(self):
        data = json.loads(TRACE.read_text())
        data["dispatches"].append(data["dispatches"][0])
        self.assertIn("duplicate_dispatch_id", {f["kind"] for f in self.trace(data)["failures"]})

    def test_trace_invalid_empty_actions(self):
        data = json.loads(TRACE.read_text())
        data["actions"] = []
        with self.assertRaises(vc.InvalidInput):
            self.trace(data)

    def test_cli_exit_codes_and_result_artifacts(self):
        base = [sys.executable, str(ROOT / "Tools" / "visual_compare.py"), "compare",
                "--reference", str(FIXTURES / "reference.png"), "--reference-rect", "0,0,16,12",
                "--actual-rect", "0,0,16,12"]
        for name, expected in (("reference.png", 0), ("altered-glyph.png", 1), ("missing.png", 2)):
            with self.subTest(name=name):
                result = subprocess.run(base + ["--actual", str(FIXTURES / name), "--output", str(self.root / name)], capture_output=True, text=True)
                self.assertEqual(result.returncode, expected, result.stderr)
                payload = json.loads(result.stderr if expected == 2 else result.stdout)
                self.assertEqual(payload["status"], {0: "pass", 1: "fail", 2: "invalid"}[expected])

    def test_trace_cli_defect_exit_code(self):
        data = json.loads(TRACE.read_text())
        data["dispatches"][0]["target_id"] = "wrong-pawn"
        path = self.root / "trace.json"
        path.write_text(json.dumps(data), encoding="utf-8")
        result = subprocess.run([sys.executable, str(ROOT / "Tools" / "visual_compare.py"),
                                 "validate-trace", "--trace", str(path), "--output", str(self.root / "result")],
                                capture_output=True, text=True)
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertEqual(json.loads(result.stdout)["status"], "fail")

    def test_manifest_and_trace_schema_json_and_internal_refs(self):
        docs = ROOT / "Docs" / "UI-Reworked" / "Foundations" / "Harness"
        for name in ("run-manifest.schema.json", "input-trace.schema.json"):
            schema = json.loads((docs / name).read_text(encoding="utf-8"))

            def check_refs(node):
                if isinstance(node, dict):
                    if "$ref" in node:
                        self.assertTrue(node["$ref"].startswith("#/"))
                        target = schema
                        for key in node["$ref"][2:].split("/"):
                            target = target[key]
                    for value in node.values():
                        check_refs(value)
                elif isinstance(node, list):
                    for value in node:
                        check_refs(value)

            check_refs(schema)
        # Structural lint only. A full Draft 2020-12 validator is not bundled.
        template = json.loads((docs / "run-manifest.template.json").read_text(encoding="utf-8"))
        self.assertEqual(template["execution_status"], "not_run")
        self.assertEqual(template["run_kind"], "smoke")
        self.assertEqual(template["acceptance"]["claim"], "none")
        self.assertNotIn("UNSET", json.dumps(template))

        def check_unknowns(node):
            if isinstance(node, dict):
                if node.get("status") == "unknown":
                    self.assertIsNone(node["value"])
                    self.assertTrue(node["reason"].strip())
                for value in node.values():
                    check_unknowns(value)
            elif isinstance(node, list):
                for value in node:
                    check_unknowns(value)

        check_unknowns(template)


if __name__ == "__main__":
    unittest.main(verbosity=2)
