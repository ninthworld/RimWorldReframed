"""Strict, fixed-coordinate PNG comparison and semantic input trace validation.

No registration, scaling, tolerance, baseline promotion or game control occurs here.
See Docs/UI-Reworked/Foundations/Harness/README.md for the capture contract.
"""
from __future__ import annotations

import argparse
from collections import deque
import hashlib
import json
from pathlib import Path
import sys

from PIL import Image, ImageChops, UnidentifiedImageError


VERSION = "1.0"
EXIT_PASS, EXIT_FAIL, EXIT_INVALID = 0, 1, 2


class InvalidInput(ValueError):
    """The evidence cannot be compared under the requested contract."""


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def input_file(path: str | Path) -> Path:
    resolved = Path(path).resolve()
    if not resolved.is_file():
        raise InvalidInput(f"Input is not a regular file: {resolved}")
    return resolved


def load_png(path: str | Path) -> tuple[Path, Image.Image]:
    resolved = input_file(path)
    try:
        # Pillow expands some 16-bit truecolor PNGs to 8-bit RGB on decode.
        # Check IHDR before decoding so that precision loss cannot be hidden.
        with resolved.open("rb") as stream:
            header = stream.read(29)
        if (header[:8] == b"\x89PNG\r\n\x1a\n" and header[12:16] == b"IHDR"
                and len(header) > 24 and header[24] > 8):
            raise InvalidInput(f"Unsupported precision: PNG bit depth {header[24]}; require <= 8: {resolved}")
        with Image.open(resolved) as opened:
            if opened.format != "PNG" or getattr(opened, "n_frames", 1) != 1:
                raise InvalidInput(f"Expected a single-frame lossless PNG: {resolved}")
            opened.load()
            image = opened.copy()
    except (UnidentifiedImageError, OSError, Image.DecompressionBombError) as exc:
        raise InvalidInput(f"Cannot decode PNG {resolved}: {exc}") from exc
    return resolved, image


def rectangle(value: str) -> tuple[int, int, int, int]:
    try:
        parts = tuple(int(part.strip()) for part in value.split(","))
    except ValueError as exc:
        raise argparse.ArgumentTypeError("Rectangle must be x,y,width,height integers") from exc
    if len(parts) != 4 or min(parts[:2]) < 0 or min(parts[2:]) <= 0:
        raise argparse.ArgumentTypeError("Rectangle requires x/y >= 0 and width/height > 0")
    return parts


def crop(image: Image.Image, rect: tuple[int, int, int, int], label: str) -> Image.Image:
    if len(rect) != 4 or any(type(v) is not int for v in rect):
        raise InvalidInput(f"{label} rectangle must contain four integers")
    x, y, width, height = rect
    if min(x, y) < 0 or min(width, height) <= 0:
        raise InvalidInput(f"{label} rectangle must have nonnegative origin and positive size")
    if x + width > image.width or y + height > image.height:
        raise InvalidInput(f"{label} rectangle {rect} exceeds image {image.size}")
    return image.crop((x, y, x + width, y + height))


def binary_mask(path: str | Path, size: tuple[int, int]) -> tuple[Path, Image.Image]:
    resolved, mask = load_png(path)
    if mask.size != size:
        raise InvalidInput(f"Mask dimensions {mask.size} differ from crop {size}: {resolved}")
    if mask.mode not in ("1", "L"):
        raise InvalidInput(f"Mask must be monochrome 1 or grayscale L, without alpha: {resolved}")
    mask = mask.convert("L")
    if any(value not in (0, 255) for value in mask.tobytes()):
        raise InvalidInput(f"Mask contains values other than 0/255: {resolved}")
    if mask.getbbox() is None:
        raise InvalidInput(f"Mask includes no pixels: {resolved}")
    return resolved, mask


def prepare_output(path: str | Path, inputs: list[Path]) -> Path:
    """Only fresh directories outside tracked source paths may receive artifacts."""
    output = Path(path).resolve()
    if any(p == output or output in p.parents for p in inputs):
        raise InvalidInput("Output directory must not contain an input file")
    for ancestor in (output, *output.parents):
        if (ancestor / ".git").exists():
            relative = output.relative_to(ancestor)
            if not relative.parts or relative.parts[0] not in ("work", "artifacts"):
                raise InvalidInput("Repository output must be under ignored work/ or artifacts/")
            break
    if output.exists() and (not output.is_dir() or any(output.iterdir())):
        raise InvalidInput(f"Output must be a new or empty directory: {output}")
    output.mkdir(parents=True, exist_ok=True)
    return output


def metrics(diff: bytes, mask: bytes, size: tuple[int, int], channels: int) -> tuple[dict, bytearray]:
    included = mismatch = total = maximum = 0
    width, height = size
    left, top, right, bottom = width, height, -1, -1
    changed = bytearray(width * height)
    for index, include in enumerate(mask):
        if not include:
            continue
        included += 1
        errors = diff[index * channels:(index + 1) * channels]
        local_max = max(errors)
        total += sum(errors)
        maximum = max(maximum, local_max)
        if local_max:
            mismatch += 1
            changed[index] = 1
            x, y = index % width, index // width
            left, right = min(left, x), max(right, x)
            top, bottom = min(top, y), max(bottom, y)
    if not included:
        raise InvalidInput("Effective inclusion mask is empty")
    return {
        "included_pixels": included,
        "mismatched_pixels": mismatch,
        "mismatch_fraction": mismatch / included,
        "maximum_channel_error": maximum,
        "mean_absolute_channel_error": total / (included * channels),
        "total_absolute_channel_error": total,
        "difference_bbox_xywh": None if not mismatch else [left, top, right-left+1, bottom-top+1],
    }, changed


def connected_regions(changed: bytearray, width: int, height: int) -> list[dict]:
    """4-connected components, in scan order; no smoothing or geometry inference."""
    pending = changed.copy()
    regions = []
    for start in range(len(pending)):
        if not pending[start]:
            continue
        pending[start] = 0
        queue = deque([start])
        count = 0
        left, top, right, bottom = width, height, -1, -1
        while queue:
            index = queue.popleft()
            x, y = index % width, index // width
            count += 1
            left, right, top, bottom = min(left, x), max(right, x), min(top, y), max(bottom, y)
            neighbours = []
            if x: neighbours.append(index - 1)
            if x + 1 < width: neighbours.append(index + 1)
            if y: neighbours.append(index - width)
            if y + 1 < height: neighbours.append(index + width)
            for adjacent in neighbours:
                if pending[adjacent]:
                    pending[adjacent] = 0
                    queue.append(adjacent)
        regions.append({"pixels": count, "bbox_xywh": [left, top, right-left+1, bottom-top+1]})
    return regions


def compare(reference: str | Path, actual: str | Path, reference_rect: tuple,
            actual_rect: tuple, output: str | Path, mask: str | Path | None = None,
            region_masks: dict[str, str | Path] | None = None, channels: str = "rgb",
            heatmap_gain: int = 8) -> dict:
    if channels not in ("rgb", "rgba"):
        raise InvalidInput("Channels must be rgb or rgba")
    if type(heatmap_gain) is not int or heatmap_gain < 1 or heatmap_gain > 255:
        raise InvalidInput("Heatmap gain must be an integer in 1..255")
    ref_path, ref_full = load_png(reference)
    actual_path, actual_full = load_png(actual)
    ref = crop(ref_full, reference_rect, "Reference")
    got = crop(actual_full, actual_rect, "Actual")
    if ref.size != got.size:
        raise InvalidInput(f"Crop dimensions differ: {ref.size} versus {got.size}; no resizing allowed")
    # Converting an 8-bit PNG's palette/grayscale into RGB is a lossless color expansion.
    # Reject high-bit-depth modes so that a conversion cannot discard source precision.
    for image in (ref, got):
        if image.mode not in ("1", "L", "LA", "P", "RGB", "RGBA"):
            raise InvalidInput(f"Unsupported precision/mode {image.mode}; require 8-bit PNG channels")
        if channels == "rgb" and image.convert("RGBA").getchannel("A").getextrema() != (255, 255):
            raise InvalidInput("Nonopaque pixels require --channels rgba; alpha must not be silently ignored")
    mode = channels.upper()
    ref, got = ref.convert(mode), got.convert(mode)
    inputs = [ref_path, actual_path]
    mask_record = {"policy": "all crop pixels", "path": None, "sha256": None}
    inclusion = Image.new("L", ref.size, 255)
    if mask is not None:
        mask_path, inclusion = binary_mask(mask, ref.size)
        inputs.append(mask_path)
        mask_record = {"policy": "255 included; 0 excluded; crop-local coordinates", "path": str(mask_path), "sha256": sha256(mask_path)}
    categories = {}
    for name, path in (region_masks or {}).items():
        if not name or not all(c.isalnum() or c in "_-" for c in name):
            raise InvalidInput(f"Invalid region name: {name}")
        region_path, region = binary_mask(path, ref.size)
        if ImageChops.subtract(region, inclusion).getbbox() is not None:
            raise InvalidInput(f"Region mask {name} includes pixels excluded by the main mask")
        categories[name] = (region_path, region)
        inputs.append(region_path)
    raw_diff = ImageChops.difference(ref, got)
    diff_bytes = raw_diff.tobytes()
    measured, changed = metrics(diff_bytes, inclusion.tobytes(), ref.size, len(mode))
    category_metrics = {}
    for name, (path, region) in categories.items():
        region_metrics, _ = metrics(diff_bytes, region.tobytes(), ref.size, len(mode))
        category_metrics[name] = {"mask_path": str(path), "mask_sha256": sha256(path), **region_metrics}
    output_path = prepare_output(output, inputs)
    # Diagnostic transformations affect exported visualizations only, never compared data.
    ref.save(output_path / "reference_crop.png")
    got.save(output_path / "actual_crop.png")
    inclusion.save(output_path / "inclusion_mask.png")
    black = Image.new(mode, ref.size, 0)
    masked_diff = Image.composite(raw_diff, black, inclusion)
    masked_diff.convert("RGB").save(output_path / "raw_diff.png")
    if channels == "rgba":
        masked_diff.getchannel("A").save(output_path / "raw_alpha_diff.png")
    heat = bytearray(ref.width * ref.height * 3)
    for index, include in enumerate(inclusion.tobytes()):
        if include:
            value = min(255, max(diff_bytes[index*len(mode):(index+1)*len(mode)]) * heatmap_gain)
            heat[index*3:index*3+3] = bytes((value, 0, 0))
    Image.frombytes("RGB", ref.size, bytes(heat)).save(output_path / "heatmap.png")
    Image.blend(ref.convert("RGB"), got.convert("RGB"), 0.5).save(output_path / "overlay.png")
    side = Image.new("RGB", (ref.width*2, ref.height))
    side.paste(ref.convert("RGB"), (0, 0))
    side.paste(got.convert("RGB"), (ref.width, 0))
    side.save(output_path / "side_by_side.png")
    box = measured["difference_bbox_xywh"]
    result = {
        "schema_version": 1, "tool": "visual_compare", "tool_version": VERSION,
        "tool_sha256": sha256(Path(__file__)), "status": "pass" if not measured["mismatched_pixels"] else "fail",
        "criterion": "zero unequal included pixels; no tolerance", "channels": channels,
        "acceptance_class": "unassigned; comparator results alone confer no source/golden approval",
        "reference": {"path": str(ref_path), "sha256": sha256(ref_path), "full_size": list(ref_full.size), "rect_xywh": list(reference_rect)},
        "actual": {"path": str(actual_path), "sha256": sha256(actual_path), "full_size": list(actual_full.size), "rect_xywh": list(actual_rect)},
        "mask": mask_record, "metrics": measured,
        "difference_bbox_reference_xywh": None if box is None else [box[0]+reference_rect[0], box[1]+reference_rect[1], *box[2:]],
        "difference_bbox_actual_xywh": None if box is None else [box[0]+actual_rect[0], box[1]+actual_rect[1], *box[2:]],
        "connected_regions": connected_regions(changed, ref.width, ref.height),
        "regions": category_metrics,
        "geometry_note": "Pixel bounds only; anchors/baselines require separately recorded geometry contracts.",
        "diagnostics": {"heatmap_gain": heatmap_gain, "heatmap": "red = clamped max-channel error * gain; excluded pixels black", "raw_diff": "RGB absolute errors; alpha absolute errors in raw_alpha_diff.png when rgba", "overlay": "50/50 encoded RGB channel blend; alpha discarded for display only", "side_by_side": "reference left, actual right, 100% scale; alpha discarded for display only"},
    }
    result["artifacts"] = {p.name: {"sha256": sha256(p)} for p in sorted(output_path.glob("*.png"))}
    (output_path / "result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    return result


def require_keys(value: object, required: set[str], label: str) -> dict:
    if not isinstance(value, dict) or set(value) != required:
        raise InvalidInput(f"{label} must have exactly these keys: {sorted(required)}")
    return value


def nonempty_string(value: object, label: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise InvalidInput(f"{label} must be a nonempty string")
    return value


def validate_trace(trace: str | Path, output: str | Path) -> dict:
    trace_path = input_file(trace)
    try:
        data = json.loads(trace_path.read_text(encoding="utf-8-sig"))
    except (ValueError, UnicodeError) as exc:
        raise InvalidInput(f"Invalid trace JSON: {exc}") from exc
    require_keys(data, {"schema_version", "test_id", "actions", "dispatches"}, "Trace")
    if type(data["schema_version"]) is not int or data["schema_version"] != 1:
        raise InvalidInput("Trace schema_version must be integer 1")
    nonempty_string(data["test_id"], "test_id")
    if not isinstance(data["actions"], list) or not data["actions"] or not isinstance(data["dispatches"], list):
        raise InvalidInput("Trace actions must be nonempty and dispatches must be an array")
    actions, grouped, ids = {}, {}, set()
    for row in data["actions"]:
        require_keys(row, {"action_id", "command", "target_id", "expected_count"}, "Action")
        for key in ("action_id", "command", "target_id"):
            nonempty_string(row[key], key)
        if type(row["expected_count"]) is not int or row["expected_count"] not in (0, 1):
            raise InvalidInput("expected_count must be 0 (no dispatch) or 1 (exactly once)")
        action_id = row["action_id"]
        if action_id in actions:
            raise InvalidInput(f"Duplicate action_id: {action_id}")
        actions[action_id], grouped[action_id] = row, []
    failures = []
    for row in data["dispatches"]:
        require_keys(row, {"dispatch_id", "action_id", "command", "target_id", "gui_event", "frame"}, "Dispatch")
        for key in ("dispatch_id", "action_id", "command", "target_id", "gui_event"):
            nonempty_string(row[key], key)
        if type(row["frame"]) is not int or row["frame"] < 0:
            raise InvalidInput("Dispatch frame must be a nonnegative integer")
        if row["dispatch_id"] in ids:
            failures.append({"kind": "duplicate_dispatch_id", "dispatch_id": row["dispatch_id"]})
        ids.add(row["dispatch_id"])
        action_id = row["action_id"]
        if action_id not in actions:
            failures.append({"kind": "unknown_action", "action_id": action_id})
            continue
        grouped[action_id].append(row)
        for field in ("command", "target_id"):
            if row[field] != actions[action_id][field]:
                failures.append({"kind": f"{field}_mismatch", "action_id": action_id, "expected": actions[action_id][field], "actual": row[field]})
    for action_id, action in actions.items():
        count = len(grouped[action_id])
        if count != action["expected_count"]:
            failures.append({"kind": "dispatch_count", "action_id": action_id, "expected": action["expected_count"], "actual": count})
    output_path = prepare_output(output, [trace_path])
    result = {"schema_version": 1, "tool": "validate_trace", "tool_version": VERSION,
              "tool_sha256": sha256(Path(__file__)), "test_id": data["test_id"],
              "trace_path": str(trace_path), "trace_sha256": sha256(trace_path),
              "status": "fail" if failures else "pass", "action_count": len(actions),
              "dispatch_count": len(data["dispatches"]), "failures": failures,
              "limit": "Checks recorded semantic dispatches only; does not prove instrumentation completeness or native game outcome."}
    (output_path / "result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    return result


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="operation", required=True)
    compare_parser = sub.add_parser("compare", help="Compare fixed PNG rectangles with exact errors")
    for name in ("reference", "actual", "output"):
        compare_parser.add_argument(f"--{name}", required=True)
    for name in ("reference-rect", "actual-rect"):
        compare_parser.add_argument(f"--{name}", type=rectangle, required=True)
    compare_parser.add_argument("--mask")
    compare_parser.add_argument("--region-mask", action="append", default=[], metavar="NAME=PNG")
    compare_parser.add_argument("--channels", choices=("rgb", "rgba"), default="rgb")
    compare_parser.add_argument("--heatmap-gain", type=int, default=8)
    trace_parser = sub.add_parser("validate-trace", help="Check exactly-once commands and stable target IDs")
    trace_parser.add_argument("--trace", required=True)
    trace_parser.add_argument("--output", required=True)
    args = parser.parse_args(argv)
    try:
        if args.operation == "compare":
            regions = {}
            for pair in args.region_mask:
                name, separator, path = pair.partition("=")
                if not separator or not path or name in regions:
                    raise InvalidInput("Region masks require unique NAME=PNG arguments")
                regions[name] = path
            result = compare(args.reference, args.actual, args.reference_rect, args.actual_rect,
                             args.output, args.mask, regions, args.channels, args.heatmap_gain)
        else:
            result = validate_trace(args.trace, args.output)
    except (InvalidInput, OSError, Image.DecompressionBombError) as exc:
        print(json.dumps({"status": "invalid", "error": str(exc)}), file=sys.stderr)
        return EXIT_INVALID
    print(json.dumps({"status": result["status"], "result": str(Path(args.output).resolve() / "result.json")}))
    return EXIT_PASS if result["status"] == "pass" else EXIT_FAIL


if __name__ == "__main__":
    raise SystemExit(main())
