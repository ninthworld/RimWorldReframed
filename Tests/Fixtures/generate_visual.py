"""Explicit regeneration of tiny synthetic harness probes, never game goldens."""
import json
from pathlib import Path

from PIL import Image, ImageChops


root = Path(__file__).resolve().parent / "Visual"
root.mkdir(parents=True, exist_ok=True)
reference = Image.new("RGB", (16, 12), (0, 0, 0))
for y in range(2, 5):
    for x in range(2, 5):
        reference.putpixel((x, y), (255, 255, 255))
glyph = [(9, 2), (10, 2), (11, 2), (9, 3), (11, 3), (9, 4),
         (10, 4), (11, 4), (9, 5), (11, 5), (9, 6), (11, 6)]
for point in glyph:
    reference.putpixel(point, (255, 255, 255))
reference.save(root / "reference.png")
shifted = Image.new("RGB", reference.size)
shifted.paste(reference, (1, 0))
shifted.save(root / "shift-right-one.png")
color = reference.copy()
color.putpixel((2, 2), (255, 0, 0))
color.save(root / "altered-color.png")
absent = reference.copy()
for y in range(2, 5):
    for x in range(2, 5):
        absent.putpixel((x, y), (0, 0, 0))
absent.save(root / "absent-icon.png")
altered = reference.copy()
altered.putpixel((10, 2), (0, 0, 0))
altered.save(root / "altered-glyph.png")
icon_mask = Image.new("L", reference.size)
text_mask = Image.new("L", reference.size)
for y in range(2, 5):
    for x in range(2, 5):
        icon_mask.putpixel((x, y), 255)
for y in range(2, 7):
    for x in range(9, 12):
        text_mask.putpixel((x, y), 255)
icon_mask.save(root / "icon-mask.png")
text_mask.save(root / "text-mask.png")
ImageChops.lighter(icon_mask, text_mask).save(root / "content-mask.png")
Image.new("L", reference.size).save(root / "empty-mask.png")
expected = {
    "evidence_class": "synthetic harness self-test only; not game evidence or an approved golden",
    "image_size": [16, 12], "channels": "rgb", "included_pixels_without_mask": 192,
    "reference.png": {"mismatched_pixels": 0, "total_error": 0, "bbox_xywh": None},
    "shift-right-one.png": {"mismatched_pixels": 22, "total_error": 16830, "bbox_xywh": [2, 2, 11, 5]},
    "altered-color.png": {"mismatched_pixels": 1, "total_error": 510, "bbox_xywh": [2, 2, 1, 1]},
    "absent-icon.png": {"mismatched_pixels": 9, "total_error": 6885, "bbox_xywh": [2, 2, 3, 3]},
    "altered-glyph.png": {"mismatched_pixels": 1, "total_error": 765, "bbox_xywh": [10, 2, 1, 1]},
    "content-mask.png": {"included_pixels": 24, "purpose": "9-pixel icon rectangle plus 15-pixel glyph rectangle"},
    "empty-mask.png": {"expected": "invalid, never pass"},
}
(root / "expected.json").write_text(json.dumps(expected, indent=2) + "\n", encoding="utf-8", newline="\n")
