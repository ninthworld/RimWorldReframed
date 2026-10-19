"""Native font calibration diagnostics; no fonts are loaded, rendered or invented here.

Unity captures are compared at their recorded physical cells. Glyph-origin normalization
is an explicitly disclosed *diagnostic* for candidate search, never layout/raster acceptance.
"""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import sys
from PIL import Image, ImageFilter

SAMPLES = [
    ("menu-body", "Load", "B01", (237, 567, 275, 590)),
    ("comparison-bold", "Quality", "B03", (652, 446, 750, 468)),
    ("micro-heading", "BASICS", "B03", (652, 413, 722, 427)),
    ("pawn-title", "Futa Healer", "B04", (1071, 680, 1212, 709)),
    ("work-tag", "Mine", "B04", (337, 243, 396, 270)),
]
MASK_RULE = "min(R,G,B)>=96 and max(R,G,B)-min(R,G,B)<=96; opaque neutral-light ink diagnostic, not recovered glyph alpha"


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def png(path: Path, require_opaque: bool = True) -> Image.Image:
    with Image.open(path) as image:
        if image.format != "PNG" or getattr(image, "n_frames", 1) != 1:
            raise ValueError("Require one lossless PNG frame")
        image.load()
        if require_opaque and "A" in image.getbands() and image.getchannel("A").getextrema() != (255, 255):
            raise ValueError("Require opaque PNG evidence; transparent RGB is not visible glyph ink")
        return image.convert("RGB") if require_opaque else image.copy()


def pixels(image: Image.Image):
    return image.get_flattened_data() if hasattr(image, 'get_flattened_data') else image.getdata()


def ink_mask(image: Image.Image) -> Image.Image:
    mask = Image.new("L", image.size)
    mask.putdata([255 if min(rgb) >= 96 and max(rgb) - min(rgb) <= 96 else 0 for rgb in pixels(image.convert("RGB"))])
    return mask


def mask_stats(mask: Image.Image) -> dict:
    box = mask.getbbox()
    if box is None:
        raise ValueError("No neutral-light ink pixels under the declared diagnostic threshold")
    return {"bbox_xyxy": list(box), "ink_pixels": sum(value > 0 for value in pixels(mask)),
            "ink_size": [box[2] - box[0], box[3] - box[1]]}


def source_inventory(repo: Path) -> dict:
    contract_path = repo / "Docs/UI-Reworked/Foundations/Reference/reference-contract.json"
    contract = json.loads(contract_path.read_text(encoding="utf-8-sig"))
    sources = {row["id"]: row for row in contract["sources"]}
    rows = []
    for sample_id, text, board, rect in SAMPLES:
        source = sources[board]
        board_path = repo / "Docs/UI-Reworked" / source["file"]
        if digest(board_path) != source["sha256"]:
            raise ValueError("Source board hash changed: " + board)
        crop_path = repo / "Docs/UI-Reworked/measurements" / (sample_id + ".png")
        image = png(crop_path)
        expected = png(board_path).crop(rect)
        if image.size != expected.size or image.tobytes() != expected.tobytes():
            raise ValueError("Source crop differs from canonical board: " + sample_id)
        rows.append({"id": sample_id, "text": text, "board": board, "board_sha256": source["sha256"],
                     "source_rect_xyxy": list(rect), "crop": str(crop_path.relative_to(repo)).replace("\\", "/"),
                     "crop_sha256": digest(crop_path), "size": list(image.size), "ink": mask_stats(ink_mask(image))})
    return {"schema_version": 1, "kind": "typography-source-inventory", "mask_rule": MASK_RULE,
            "identity_status": "unknown; rendered game candidates required", "sources": rows}


def integer_cell(value: object, size: tuple[int, int]) -> tuple[int, int, int, int]:
    if not isinstance(value, list) or len(value) != 4:
        raise ValueError("Missing physical screen cell; export after drawing the selected candidate")
    if any(isinstance(number, bool) or not isinstance(number, (int, float)) or int(number) != number for number in value):
        raise ValueError("Fractional physical cell: calibrate placement; do not silently round or rescale")
    x, y, width, height = (int(number) for number in value)
    if min(x, y) < 0 or min(width, height) <= 0 or x + width > size[0] or y + height > size[1]:
        raise ValueError("Physical cell exceeds the screenshot")
    return x, y, width, height


def compare_masks(reference: Image.Image, actual: Image.Image) -> tuple[dict, Image.Image]:
    reference_stats, actual_stats = mask_stats(reference), mask_stats(actual)
    ref_ink, actual_ink = reference.crop(reference.getbbox()), actual.crop(actual.getbbox())
    size = (max(ref_ink.width, actual_ink.width), max(ref_ink.height, actual_ink.height))
    left, right = Image.new("L", size), Image.new("L", size)
    left.paste(ref_ink, (0, 0)); right.paste(actual_ink, (0, 0))
    intersection = union = mismatch = 0
    overlay = Image.new("RGB", size)
    colors = []
    for a, b in zip(pixels(left), pixels(right)):
        intersection += bool(a and b); union += bool(a or b); mismatch += bool(a) != bool(b)
        colors.append((220, 220, 220) if a and b else (255, 50, 50) if a else (50, 255, 50) if b else (0, 0, 0))
    overlay.putdata(colors)
    return {"diagnostic_ink_iou": intersection / union, "unequal_mask_pixels": mismatch,
            "source_ink": reference_stats, "actual_ink": actual_stats,
            "width_delta": actual_ink.width - ref_ink.width, "height_delta": actual_ink.height - ref_ink.height,
            "normalization": "translate each thresholded ink bbox origin to 0,0; no scaling, blur, per-glyph registration or raster acceptance"}, overlay


def fixed_origin_metrics(reference: Image.Image, actual: Image.Image) -> dict:
    """Compare declared source coordinates to cell origin, with no registration of any kind."""
    if reference.width > actual.width or reference.height > actual.height:
        raise ValueError("Actual typography cell cannot contain the fixed source crop")
    ref_mask = Image.new("L", actual.size)
    ref_mask.paste(ink_mask(reference), (0, 0))
    actual_mask = ink_mask(actual)
    intersection = union = mismatch = 0
    for left, right in zip(pixels(ref_mask), pixels(actual_mask)):
        intersection += bool(left and right); union += bool(left or right); mismatch += bool(left) != bool(right)
    if not union:
        raise ValueError("Fixed-origin diagnostic contains no ink")
    cropped = actual.crop((0, 0, reference.width, reference.height)).convert("RGB")
    errors = [tuple(abs(a - b) for a, b in zip(left, right)) for left, right in zip(pixels(reference.convert("RGB")), pixels(cropped))]
    source_mask = ink_mask(reference)
    dilation = source_mask.filter(ImageFilter.MaxFilter(3))
    erosion = source_mask.filter(ImageFilter.MinFilter(3))
    source_edge = [maximum != minimum for maximum, minimum in zip(pixels(dilation), pixels(erosion))]
    edge_errors = [error for error, include in zip(errors, source_edge) if include]
    return {"mask_iou": intersection / union, "unequal_mask_pixels": mismatch,
            "source_edge_rgb": {"mask_pixels": len(edge_errors),
                "unequal_pixels": sum(any(error) for error in edge_errors),
                "mean_absolute_channel_error": sum(sum(error) for error in edge_errors) / (len(edge_errors) * 3) if edge_errors else None,
                "mask_rule": "fixed source-only threshold mask 3x3 dilation minus erosion; candidate geometry/tint never changes this edge mask"},
            "raw_source_crop_xywh": [0, 0, reference.width, reference.height],
            "raw_rgb_unequal_pixels": sum(any(error) for error in errors),
            "raw_rgb_compared_pixels": len(errors),
            "raw_rgb_mean_absolute_channel_error": sum(sum(error) for error in errors) / (len(errors) * 3),
            "criterion": "fixed source-crop origin relative to captured cell; zero alignment, resizing, blur or tolerance",
            "limit": "Raw crop includes background/indicator/shading differences; mask is threshold diagnostic only, not recovered alpha or automatic font acceptance"}


def fresh_directory(output: Path, inputs: list[Path]) -> Path:
    output = output.resolve()
    if any(output == path.resolve() or output in path.resolve().parents for path in inputs):
        raise ValueError("Output must not contain inputs")
    for ancestor in (output, *output.parents):
        if (ancestor / ".git").exists():
            relative = output.relative_to(ancestor)
            if not relative.parts or relative.parts[0] not in ("work", "artifacts"):
                raise ValueError("Repository diagnostic output belongs under ignored work/ or artifacts/")
            break
    if output.exists() and (not output.is_dir() or any(output.iterdir())):
        raise ValueError("Output directory must be new or empty")
    output.mkdir(parents=True, exist_ok=True)
    return output


def validate_native_style_probe(probe: object) -> None:
    if not isinstance(probe, dict) or any(probe.get(key) is not True for key in
            ("executed", "unchanged", "all_draws_unchanged", "construction_unchanged")):
        raise ValueError("Native shared-style isolation probe was unrun or changed")
    for key in ("before", "after", "construction_before", "construction_after"):
        values = probe.get(key)
        if not isinstance(values, list) or len(values) != 3 or any(not isinstance(value, str) or len(value) != 16 or any(char not in "0123456789abcdefABCDEF" for char in value) for value in values):
            raise ValueError("Native style probe requires three per-slot 64-bit fingerprints")
    if probe["before"] != probe["after"] or probe["construction_before"] != probe["construction_after"]:
        raise ValueError("Native style fingerprints changed across calibration")


def score(repo: Path, capture: Path, metadata: Path, output: Path) -> dict:
    inventory = source_inventory(repo)
    descriptor = json.loads(metadata.read_text(encoding="utf-8-sig"))
    if descriptor.get("kind") != "native-font-calibration" or descriptor.get("schema_version") != 1:
        raise ValueError("Unsupported typography metadata")
    if "native_style_probe" in descriptor:
        validate_native_style_probe(descriptor["native_style_probe"])
    if descriptor.get("last_draw_candidate_index", -1) != descriptor.get("candidate_index") or descriptor.get("last_draw_frame", -1) < 0:
        raise ValueError("Metadata was not exported after drawing this candidate")
    captured_frame = descriptor.get("captured_frame")
    if captured_frame is not None and (type(captured_frame) is not int or captured_frame < 0 or captured_frame != descriptor.get("last_draw_frame")):
        raise ValueError("Captured frame differs from the last rendered typography frame")
    if not descriptor.get("draw_complete", False):
        raise ValueError("Specimen was clipped by a surface smaller than 760x286")
    if descriptor.get("ui_scale") != 1:
        raise ValueError("This first source/client calibration requires UI scale 1; no automatic font/capture resizing")
    actual = png(capture, require_opaque=False)
    full_frame_alpha = list(actual.getchannel("A").getextrema()) if "A" in actual.getbands() else [255, 255]
    if descriptor.get("screen_pixels") != list(actual.size):
        raise ValueError("Capture dimensions differ from exported physical screen")
    samples = descriptor.get("samples", [])
    if len(samples) != len(SAMPLES) or {row.get("id") for row in samples} != {row[0] for row in SAMPLES}:
        raise ValueError("Require exactly the five unique known sample identities")
    samples = {row["id"]: row for row in samples}
    pending = []
    for source in inventory["sources"]:
        row = samples[source["id"]]
        if row.get("text") != source["text"] or row.get("source_rect_xyxy") != source["source_rect_xyxy"]:
            raise ValueError("Source identity/rectangle changed in runtime metadata")
        cell = integer_cell(row.get("screen_cell_xywh"), actual.size)
        x, y, width, height = cell
        actual_crop = actual.crop((x, y, x + width, y + height))
        if "A" in actual_crop.getbands() and actual_crop.getchannel("A").getextrema() != (255, 255):
            raise ValueError("Require opaque actual typography cells; outside-frame alpha is not compared")
        source_crop = png(repo / source["crop"])
        result, overlay = compare_masks(ink_mask(source_crop), ink_mask(actual_crop))
        result["fixed_origin"] = fixed_origin_metrics(source_crop, actual_crop)
        result.update({"font_name": row.get("font_name", descriptor["font_name"]),
                       "style_size": row.get("style_size", descriptor["style_size"]),
                       "style_weight": row.get("style_weight", descriptor["style_weight"]),
                       "role_recipe": row.get("role_recipe"), "draw_offset_xy": row.get("draw_offset_xy", [0, 0]),
                       "tracking_gui_units": row.get("tracking_gui_units", 0), "tint_hex": row.get("tint_hex"),
                       "measured_size_gui_units": row.get("measured_size_gui_units"),
                       "suffix_size": row.get("suffix_size"), "suffix_extra_gap": row.get("suffix_extra_gap"), "suffix_tint_hex": row.get("suffix_tint_hex"),
                       "id": source["id"], "text": source["text"], "screen_cell_xywh": list(cell),
                       "candidate_eligible": not row.get("overflow", True), "source_crop_sha256": source["crop_sha256"]})
        pending.append((result, actual_crop, overlay))
    output = fresh_directory(output, [capture, metadata, *[repo / row["crop"] for row in inventory["sources"]]])
    for result, actual_crop, overlay in pending:
        actual_crop.save(output / (result["id"] + "-actual.png"))
        overlay.save(output / (result["id"] + "-ink-overlay.png"))
    result = {"schema_version": 1, "kind": "typography-candidate-diagnostic", "acceptance": "none; threshold-mask candidate search only",
              "candidate_id": descriptor["candidate_id"], "font_name": descriptor["font_name"],
              "style_size": descriptor["style_size"], "style_weight": descriptor["style_weight"],
              "capture": {"path": str(capture.resolve()), "sha256": digest(capture), "full_frame_alpha_range": full_frame_alpha,
                          "compared_cells_require_opaque": True, "alpha_policy": "original RGBA/hash preserved; opacity required only inside compared cells"},
              "metadata": {"path": str(metadata.resolve()), "sha256": digest(metadata)},
              "tool_sha256": digest(Path(__file__)), "mask_rule": MASK_RULE,
              "samples": [row[0] for row in pending]}
    (output / "result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    return result


def rank(paths: list[Path], metric: str = "ink") -> dict:
    results = [json.loads(path.read_text(encoding="utf-8-sig")) for path in paths]
    if not results or any(row.get("kind") != "typography-candidate-diagnostic" for row in results):
        raise ValueError("Require candidate diagnostic result files")
    ranking = {}
    for sample_id, *_ in SAMPLES:
        rows = []
        for result in results:
            matches = [row for row in result["samples"] if row["id"] == sample_id and row["candidate_eligible"]]
            if matches:
                rows.append({"candidate_id": result["candidate_id"], "font_name": result["font_name"],
                             "style_size": result["style_size"], "style_weight": result["style_weight"],
                             **matches[0]})
        ranking[sample_id] = sorted(rows, key=lambda row: (
            -row["fixed_origin"]["mask_iou"] if metric == "fixed" else -row["diagnostic_ink_iou"],
            abs(row["width_delta"]) + abs(row["height_delta"])))
    return {"kind": "typography-diagnostic-ranking", "ranking_metric": metric, "acceptance": "none; inspect distinctive glyphs and fixed raster evidence before selection", "by_role": ranking}


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    inventory_parser = sub.add_parser("inventory")
    inventory_parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[1])
    inventory_parser.add_argument("--output", type=Path, required=True)
    score_parser = sub.add_parser("score")
    score_parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[1])
    for name in ("capture", "metadata", "output"):
        score_parser.add_argument("--" + name, type=Path, required=True)
    rank_parser = sub.add_parser("rank")
    rank_parser.add_argument("--result", type=Path, action="append", required=True)
    rank_parser.add_argument("--metric", choices=("ink", "fixed"), default="ink")
    args = parser.parse_args(argv)
    try:
        if args.command == "inventory":
            result = source_inventory(args.repo.resolve())
            output = fresh_directory(args.output, [args.repo / row["crop"] for row in result["sources"]])
            (output / "source-inventory.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
        elif args.command == "score":
            result = score(args.repo.resolve(), args.capture, args.metadata, args.output)
        else:
            print(json.dumps(rank(args.result, args.metric), indent=2)); return 0
        print(json.dumps({"status": "diagnostic-only", "sample_count": len(result.get("samples", result.get("sources", [])))}))
        return 0
    except (OSError, ValueError, TypeError, KeyError) as error:
        print(json.dumps({"status": "invalid", "error": str(error)}), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
