#!/usr/bin/env python3
"""Audit measured icon bounds and generate a source-vs-vector diagnostic atlas."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
import re
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "Docs" / "UI-Reworked"
CONTRACT_PATH = DOCS / "Foundations" / "Icons" / "icon-contract.json"
DEFAULT_ATLAS = DOCS / "Foundations" / "Icons" / "icon-candidate-atlas.png"
SCALE = 4  # Diagnostic enlargement only; source and vector are never registered or resized relative to one another.


def load_contract() -> dict:
    return json.loads(CONTRACT_PATH.read_text(encoding="utf-8"))


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def measured_bounds(image: Image.Image, icon: dict) -> tuple[int, int, int, int]:
    x1, y1, x2, y2 = icon["source_bounds_xyxy"]
    threshold = icon["pixel_threshold"]
    margin = 3
    x1, y1 = max(0, x1 - margin), max(0, y1 - margin)
    x2, y2 = min(image.width, x2 + margin), min(image.height, y2 + margin)
    # Count chromatic/bright marks while excluding the dark button face and its border.
    points = []
    rgb = image.convert("RGB")
    for y in range(y1, y2):
        for x in range(x1, x2):
            pixel = rgb.getpixel((x, y))
            if max(pixel) > threshold["max_channel_gt"] and sum(pixel) > threshold["rgb_sum_gt"]:
                points.append((x, y))
    if not points:
        return (0, 0, 0, 0)
    return (min(x for x, _ in points), min(y for _, y in points),
            max(x for x, _ in points) + 1, max(y for _, y in points) + 1)


def draw_vector(icon: dict, scale: int = SCALE) -> Image.Image:
    width, height = map(int, icon["viewbox"].split("x"))
    image = Image.new("RGBA", (width * scale, height * scale), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    color = (132, 146, 159, 255)  # diagnostic tint only; runtime tint comes from caller.
    t = max(1, round(icon["provisional_stroke_px"] * scale))

    def point(x: float, y: float) -> tuple[int, int]:
        return round(x * width * scale), round(y * height * scale)

    def line(x1: float, y1: float, x2: float, y2: float, thickness: int | None = None) -> None:
        draw.line([point(x1, y1), point(x2, y2)], fill=color, width=thickness or t)

    def circle(cx: float, cy: float, diameter: float, thickness: int | None = None) -> None:
        # Pillow's ellipse outline has a one-pixel asymmetric edge; normalize through its bounding box.
        radius = diameter * .5
        box = (round((cx-radius)*width*scale), round((cy-radius)*height*scale),
               round((cx+radius)*width*scale)-1, round((cy+radius)*height*scale)-1)
        draw.ellipse(box, outline=color, width=thickness or t)

    kind = icon["vector"]
    if kind == "plus":
        line(.50,0,.50,1); line(0,.50,1,.50,2 * scale)
    elif kind == "upload-tray":
        tray_t = max(1, round(t * 1.3))
        line(.50,0,.50,.46); line(.50,0,.25,.30); line(.75,.30,.50,0)
        line(0,.57,0,1,tray_t); line(0,1,1,1,tray_t); line(1,1,1,.57,tray_t)
        line(.25,.70,.43,.70,max(1,round(t*.8)))
    elif kind == "scroll":
        line(.22,0,1,0); line(1,0,1,.79); line(1,.79,.18,.79)
        line(.18,.79,.18,.18); line(.18,.18,.30,.18)
        line(.34,.32,.78,.32,max(1,round(t*.82)))
        line(.34,.49,.78,.49,max(1,round(t*.82)))
        line(.34,.66,.67,.66,max(1,round(t*.82)))
        line(.18,.79,0,1,max(1,round(t*.72)))
    elif kind == "cog":
        for i in range(8):
            angle = i * math.pi / 4
            c, s = math.cos(angle), math.sin(angle)
            line(.50+c*.28,.50+s*.28,.50+c*.50,.50+s*.50,max(1,round(t*1.30)))
        circle(.50,.50,.70,max(1,round(t*.90))); circle(.50,.50,.30,max(1,round(t*.82)))
    elif kind == "briefcase":
        line(.08,.34,.92,.34); line(0,.34,0,1); line(1,.34,1,1); line(0,1,1,1)
        line(.25,.34,.25,0); line(.25,0,.75,0); line(.75,0,.75,.34)
        line(0,.56,1,.56,max(1,round(t*.92))); line(.46,.47,.54,.47,max(1,round(t*1.15)))
    elif kind == "book":
        ex, ey = t / (2 * width * scale), t / (2 * height * scale)
        line(.50,ey,.50,1-ey); line(.50,.18,.08,.08); line(.08,.08,.08,.82); line(.08,.82,.50,.98)
        line(.50,.18,.92,.08); line(.92,.08,.92,.82); line(.92,.82,.50,.98)
        for a,b,c,d in ((.14,.34,.43,.39),(.14,.51,.43,.56),(.57,.39,.86,.34),(.57,.56,.86,.51)):
            line(a,b,c,d,max(1,round(t*.72)))
    elif kind == "exit-door":
        line(0,0,.58,0); line(0,0,0,1); line(0,1,.58,1)
        line(.58,0,.58,.32); line(.58,.68,.58,1)
        line(.32,.50,1,.50); line(.70,.24,1,.50); line(1,.50,.70,.76)
    elif kind == "search":
        circle(.41,.41,.82,max(1,round(t*.88))); line(.62,.62,1-t/(2*width*scale),1-t/(2*height*scale),max(1,round(t*1.05)))
    else:
        raise ValueError(f"Unknown vector descriptor: {kind}")
    return image


def audit_contract() -> list[str]:
    contract = load_contract()
    errors = []
    seen = set()
    for source in contract["sources"]:
        path = DOCS / source["file"]
        if not path.is_file():
            errors.append(f"missing source {source['file']}")
        elif digest(path) != source["sha256"]:
            errors.append(f"source hash changed: {source['file']}")
    for icon in contract["icons"]:
        if icon["id"] in seen:
            errors.append(f"duplicate icon id: {icon['id']}")
        seen.add(icon["id"])
        x1, y1, x2, y2 = icon["source_bounds_xyxy"]
        source_path = DOCS / icon["source_file"]
        if not source_path.is_file():
            continue
        with Image.open(source_path) as source:
            bounds = tuple(icon["source_bounds_xyxy"])
            actual = measured_bounds(source, icon)
            if actual != bounds:
                errors.append(f"{icon['id']} threshold bounds {actual} != contract {bounds}")
            if not (0 <= bounds[0] < bounds[2] <= source.width and 0 <= bounds[1] < bounds[3] <= source.height):
                errors.append(f"{icon['id']} bounds outside source image")
        vector = draw_vector(icon, 1)
        if vector.getchannel("A").getbbox() is None:
            errors.append(f"{icon['id']} vector has no visible geometry")
        if vector.getchannel("A").getbbox() != (0, 0, vector.width, vector.height):
            errors.append(f"{icon['id']} visible candidate ink does not reach its measured source bounds")
        if not (1.5 <= icon.get("provisional_stroke_px", 0) <= 5.0):
            errors.append(f"{icon['id']} provisional stroke width is missing or outside the measured small-icon range")
        sx, sy, sw, sh = icon["specimen_rect_xywh"]
        if (sw, sh) != (x2 - x1, y2 - y1):
            errors.append(f"{icon['id']} specimen size {sw}x{sh} differs from measured source size {x2-x1}x{y2-y1}")
        if sx % 1 or sy % 1 or sx < 0 or sy < 0 or sx + sw > contract["specimen"]["size_px"][0] or sy + sh > contract["specimen"]["size_px"][1]:
            errors.append(f"{icon['id']} specimen rectangle is nonintegral or outside the fixed canvas")
    source_text = (ROOT / "Source" / "ReframedIcons.cs").read_text(encoding="utf-8")
    if contract["specimen"]["size_px"] != [760, 286]:
        errors.append("live specimen contract must stay at 760x286 pixels")
    if contract["specimen"].get("variant_count") != 2 or [v["id"] for v in contract["specimen"].get("variants", [])] != ["vector-refined", "native-plus-search"]:
        errors.append("specimen variants must remain the named vector and native comparison pair")
    if contract["specimen"]["background_rgb"] != [14, 19, 23] or contract["specimen"]["tint_rgb"] != [132, 146, 159]:
        errors.append("live specimen RGB values differ from the documented capture contract")
    for required in (
        "Widgets.DrawBoxSolid(rect, Theme.Rgb(DesignTokens.Panel))",
        "Mathf.Approximately(rect.width, 760f)",
        "Mathf.Approximately(rect.height, 286f)",
        "new Rect(rect.x + icon.SpecimenX, rect.y + icon.SpecimenY, icon.Width, icon.Height)",
        "Theme.Rgb(DesignTokens.Muted)",
        "GUI.BeginGroup(bounds)",
        "var localBounds = new Rect(0f, 0f, bounds.width, bounds.height)",
        "if (groupStarted) GUI.EndGroup()",
        "finally",
        "internal const int VariantCount = 2",
        "internal static string GetVariantId(int variant)",
        "TexButton.Plus : TexButton.Search",
        "GUI.DrawTexture(new Rect(0f, 0f, bounds.width, bounds.height), texture, ScaleMode.StretchToFill, true)",
        "DrawVariant(icon.Id, iconRect",
    ):
        if required not in source_text:
            errors.append(f"C# specimen contract is missing: {required}")
    renderer_records = {}
    record_pattern = re.compile(r'new IconInfo\("([^"]+)",\s*(\d+),\s*(\d+),\s*(\d+),\s*(\d+),\s*(\d+),\s*(\d+),')
    for match in record_pattern.finditer(source_text):
        renderer_records[match.group(1)] = tuple(map(int, match.groups()[1:]))
    stroke_records = {}
    stroke_pattern = re.compile(
        r'new IconInfo\("([^"]+)",\s*\d+,\s*\d+,\s*\d+,\s*\d+,\s*\d+,\s*\d+,\s*"[^"]*",\s*"[^"]*",\s*([0-9]+(?:\.[0-9]+)?)f'
    )
    for match in stroke_pattern.finditer(source_text):
        stroke_records[match.group(1)] = float(match.group(2))
    for icon in contract["icons"]:
        icon_id = icon["id"]
        x1, y1, x2, y2 = icon["source_bounds_xyxy"]
        expected_record = (x1, y1, x2 - x1, y2 - y1, *icon["specimen_rect_xywh"][:2])
        if renderer_records.get(icon_id) != expected_record:
            errors.append(f"C# renderer bounds for {icon_id} {renderer_records.get(icon_id)} != contract {expected_record}")
        if stroke_records.get(icon_id) != icon["provisional_stroke_px"]:
            errors.append(f"C# provisional stroke for {icon_id} {stroke_records.get(icon_id)} != contract {icon['provisional_stroke_px']}")
    if 'known ? GetVectorTexture(index) : GetUnknownTexture()' not in source_text or 'return known;' not in source_text:
        errors.append("cached glyph texture or visible unknown-ID fallback is missing")
    if "private const int RasterSamplesPerAxis = 8" not in source_text or "GUIUtility.RotateAroundPivot" in source_text:
        errors.append("runtime glyph renderer must use bounded supersampled textures, not per-segment GUI rotation")
    if "vectorTextures[index] = CreateGlyphTexture(index)" not in source_text or "texture.Apply(false, true)" not in source_text:
        errors.append("glyph raster textures must be cached and uploaded once")
    return errors


def make_atlas(output: Path) -> None:
    contract = load_contract()
    icons = contract["icons"]
    cell_w, cell_h = 216, 160
    margin = 12
    width, height = margin * 2 + cell_w * 4, 70 + cell_h * 2 + margin * 3
    atlas = Image.new("RGB", (width, height), (239, 242, 245))
    draw = ImageDraw.Draw(atlas)
    font = ImageFont.load_default()
    draw.text((margin, 10), "F02-B candidate icon atlas · diagnostic only", fill=(30, 43, 53), font=font)
    draw.text((margin, 30), "Source crop retains original button pixels; vector uses caller tint; both enlarged 4× nearest-neighbor for inspection.", fill=(55, 67, 78), font=font)

    for i, icon in enumerate(icons):
        col, row = i % 4, i // 4
        x = margin + col * cell_w
        y = 58 + margin + row * cell_h
        draw.rounded_rectangle((x, y, x + cell_w - 6, y + cell_h - 6), radius=5,
                               fill=(255, 255, 255), outline=(194, 204, 211), width=1)
        draw.text((x + 8, y + 7), icon["id"], fill=(30, 43, 53), font=font)
        bx1, by1, bx2, by2 = icon["source_bounds_xyxy"]
        source_path = DOCS / icon["source_file"]
        with Image.open(source_path) as source:
            crop = source.crop((bx1, by1, bx2, by2)).convert("RGBA")
        vector = draw_vector(icon, SCALE)
        w, h = crop.width * SCALE, crop.height * SCALE
        source_scaled = crop.resize((w, h), Image.Resampling.NEAREST)
        # Checkerboard makes only the reconstruction's transparent background visible.
        checker = Image.new("RGBA", (w, h), (255, 255, 255, 255))
        checker_draw = ImageDraw.Draw(checker)
        unit = SCALE * 2
        for yy in range(0, h, unit):
            for xx in range(0, w, unit):
                if ((xx // unit) + (yy // unit)) % 2:
                    checker_draw.rectangle((xx, yy, min(xx+unit-1,w-1), min(yy+unit-1,h-1)), fill=(225, 231, 235, 255))
        vector_composite = Image.alpha_composite(checker, vector)
        y_icon = y + 30 + (72 - h) // 2
        atlas.paste(source_scaled.convert("RGB"), (x + 12, y_icon))
        atlas.paste(vector_composite.convert("RGB"), (x + 120, y_icon))
        draw.text((x + 12, y + 108), f"source {crop.width}x{crop.height}px", fill=(55, 67, 78), font=font)
        draw.text((x + 120, y + 108), "vector candidate", fill=(55, 67, 78), font=font)
        draw.text((x + 8, y + 128), "No alignment; no source repaint; exact acceptance uses native coordinates.", fill=(90, 101, 110), font=font)

    output.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(output)
    manifest = {
        "schema": "icon-atlas-diagnostic-v1",
        "output": output.name,
        "source_inputs": contract["sources"],
        "icon_ids_and_source_bounds": [
            {"id": icon["id"], "board": icon["board"], "bounds_xyxy": icon["source_bounds_xyxy"]}
            for icon in icons
        ],
        "transform": "source crop and vector canvas each enlarged 4x nearest-neighbor for viewing; no registration, repositioning, recoloring, or native-size acceptance implied",
        "source_pixels": "original canonical B01 pixels including their button background and antialiasing",
        "vector_pixels": "transparent-background proposed path rendered in #84929F for this diagnostic only",
        "runtime_tint": "provided by caller; not fixed to diagnostic tint",
        "runtime_specimen": {
            "size_px": contract["specimen"]["size_px"],
            "background_rgb": contract["specimen"]["background_rgb"],
            "candidate_tint_rgb": contract["specimen"]["tint_rgb"],
            "candidate_rects_relative_to_canvas": [
                {"id": icon["id"], "rect_xywh": icon["specimen_rect_xywh"]}
                for icon in icons
            ]
        },
        "acceptance": "diagnostic inspection only; compare actual runtime capture at the fixed source bounding boxes"
    }
    (output.with_name("icon-atlas-manifest.json")).write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="verify source hashes, measured bounds, IDs, and fallback without writing files")
    parser.add_argument("--atlas", type=Path, default=DEFAULT_ATLAS, help="diagnostic atlas output path")
    args = parser.parse_args(argv)
    errors = audit_contract()
    if errors:
        print(f"Icon contract failed: {len(errors)} issue(s)")
        for error in errors:
            print(f"- {error}")
        return 1
    print(f"Icon contract passed: {len(load_contract()['icons'])} source bounds and stable IDs checked.")
    if not args.check:
        make_atlas(args.atlas)
        print(f"Diagnostic atlas written: {args.atlas}")
    else:
        print("Read-only check; contract and source images were not modified.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
