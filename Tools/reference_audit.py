#!/usr/bin/env python3
"""Read-only integrity audit for the measured UI reference contract."""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError as exc:  # pragma: no cover - message is user-facing
    raise SystemExit("Pillow is required: install the project's documentation dependencies") from exc


ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "Docs" / "UI-Reworked"
CONTRACT_PATH = DOCS / "Foundations" / "Reference" / "reference-contract.json"


class Audit:
    def __init__(self) -> None:
        self.errors: list[str] = []
        self.checks = 0

    def check(self, condition: bool, message: str) -> None:
        self.checks += 1
        if not condition:
            self.errors.append(message)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def rgb_from_hex(value: str) -> tuple[int, int, int]:
    value = value.removeprefix("#")
    return tuple(int(value[index:index + 2], 16) for index in (0, 2, 4))


def main() -> int:
    audit = Audit()
    contract = load_json(CONTRACT_PATH)
    source_manifest = load_json(DOCS / "source-manifest.json")
    region_manifest = load_json(DOCS / "region-manifest.json")
    manifest_by_file = {entry["file"]: entry for entry in source_manifest}
    canonical_by_id = {
        entry["id"]: entry for entry in source_manifest
        if entry.get("role") == "canonical raster evidence"
    }

    # Immutable source bytes, dimensions, and manifest/contract agreement.
    for source in contract["sources"]:
        path = DOCS / source["file"]
        audit.check(path.is_file(), f"Missing source: {source['file']}")
        if not path.is_file():
            continue
        actual_hash = sha256(path)
        audit.check(actual_hash == source["sha256"], f"SHA-256 mismatch: {source['file']}")
        manifest = manifest_by_file.get(source["file"])
        audit.check(manifest is not None, f"Source absent from source-manifest.json: {source['file']}")
        if manifest:
            audit.check(manifest["sha256"] == source["sha256"], f"Contract/manifest hash differs: {source['file']}")
            audit.check((manifest["width"], manifest["height"], manifest["format"]) ==
                        (source["width"], source["height"], source["format"]),
                        f"Contract/manifest dimensions or format differ: {source['file']}")
        with Image.open(path) as image:
            audit.check(image.size == (source["width"], source["height"]),
                        f"Raster dimensions differ: {source['file']} is {image.size}")

    # Palette provenance must decode to the declared final raster pixel.
    for sample in contract["palette"]:
        source = canonical_by_id.get(sample["evidence"]["board"])
        audit.check(source is not None, f"Palette sample has no canonical source: {sample['id']}")
        if source is None:
            continue
        path = DOCS / source["file"]
        if not path.is_file():
            continue
        with Image.open(path) as image:
            x, y = sample["evidence"]["x"], sample["evidence"]["y"]
            inside = 0 <= x < image.width and 0 <= y < image.height
            audit.check(inside, f"Palette coordinate outside {source['file']}: {sample['id']}")
            if inside:
                actual = image.convert("RGB").getpixel((x, y))
                audit.check(actual == rgb_from_hex(sample["hex"]),
                            f"Palette pixel differs for {sample['id']}: got {actual}, expected {sample['hex']}")

    # Every declared inspection rectangle must match the supplied manifest and
    # its derived crop pixel-for-pixel. The crop is not treated as an opaque mask.
    regions = region_manifest
    contract_regions = {entry["id"]: entry for entry in contract["component_rectangles"]}
    audit.check(set(contract_regions) == set(regions),
                "Contract component IDs differ from the supplied region manifest")
    for region_id, region in regions.items():
        contracted = contract_regions.get(region_id)
        audit.check(contracted is not None, f"Region absent from contract: {region_id}")
        if contracted:
            audit.check((contracted["board"], contracted["box_xyxy"], contracted["path"], contracted["status"]) ==
                        (region["board"], region["box_xyxy"], region["path"], region["status"]),
                        f"Contract geometry differs from supplied manifest: {region_id}")
            x1, y1, x2, y2 = contracted["box_xyxy"]
            audit.check(contracted["box_xyxy"] == [x1, y1, x2, y2]
                        and x1 < x2 and y1 < y2,
                        f"Invalid contracted crop rectangle: {region_id}")
        source = canonical_by_id.get(region["board"])
        audit.check(source is not None, f"Region {region_id} has no canonical source")
        if source is None:
            continue
        x1, y1, x2, y2 = region["box_xyxy"]
        audit.check(x1 < x2 and y1 < y2, f"Invalid rectangle for region {region_id}")
        with Image.open(DOCS / source["file"]) as image:
            audit.check(0 <= x1 < x2 <= image.width and 0 <= y1 < y2 <= image.height,
                        f"Region outside source raster: {region_id}")
            crop_path = DOCS / region["path"]
            audit.check(crop_path.is_file(), f"Missing derived crop: {region['path']}")
            if crop_path.is_file():
                with Image.open(crop_path) as crop:
                    expected_size = (x2 - x1, y2 - y1)
                    audit.check(crop.size == expected_size,
                                f"Crop dimensions differ for {region_id}: {crop.size} != {expected_size}")
                    if crop.size == expected_size:
                        expected = image.crop((x1, y1, x2, y2)).convert("RGBA")
                        actual = crop.convert("RGBA")
                        audit.check(actual.tobytes() == expected.tobytes(),
                                    f"Crop pixels differ from source rectangle: {region_id}")

    # Ensure contract and manifests describe the same crop bounds and typography crops.
    for typography in contract["typography"]["source_crops"]:
        source = canonical_by_id.get(typography["board"])
        x1, y1, x2, y2 = typography["box_xyxy"]
        audit.check(source is not None and x1 < x2 and y1 < y2,
                    f"Invalid typography source or crop rectangle: {typography['id']}")
        crop_path = DOCS / "measurements" / f"{typography['id']}.png"
        audit.check(crop_path.is_file(), f"Missing typography evidence crop: {crop_path.name}")
        if source and crop_path.is_file():
            with Image.open(DOCS / source["file"]) as image, Image.open(crop_path) as crop:
                in_bounds = 0 <= x1 < x2 <= image.width and 0 <= y1 < y2 <= image.height
                audit.check(in_bounds, f"Typography crop outside source raster: {typography['id']}")
                if in_bounds:
                    expected = image.crop((x1, y1, x2, y2)).convert("RGBA")
                    actual = crop.convert("RGBA")
                    audit.check(actual.size == expected.size and actual.tobytes() == expected.tobytes(),
                                f"Typography crop differs from source pixels: {typography['id']}")

    audit.check(contract["geometry_source"] == "../region-manifest.json",
                 "Contract no longer points at the supplied region manifest")
    audit.check(contract["canonical_capture"]["client_pixels"] == [1920, 1080],
                 "Canonical capture dimensions disagree with B02")
    audit.check(contract["canonical_capture"]["mapping"]["source_pixel_to_rimworld_gui_unit"] is None,
                 "Runtime GUI-unit scale must remain unresolved until calibrated")
    audit.check(contract["typography"]["font_family"] is None
                 and contract["typography"]["unity_raster_path_verified"] is False,
                 "Unverified typography was promoted to a selected runtime fact")
    audit.check(contract["icon_status_policy"]["native_asset"] ==
                 "unresolved until a candidate is compared against the source at original scale",
                 "Native icon identity policy changed without source comparison")
    audit.check(contract["icon_status_policy"]["applies_to_each_named_symbol"] is True
                and len(contract["icons"]) > 0
                and all(family["symbols"] and family["source_board"] for family in contract["icons"]),
                "Icon inventory lacks per-symbol source coverage or status policy")

    if audit.errors:
        print(f"Reference audit failed: {len(audit.errors)} of {audit.checks} checks failed.")
        for error in audit.errors:
            print(f"- {error}")
        return 1
    print(f"Reference audit passed: {audit.checks} checks; {len(contract['sources'])} sources, "
          f"{len(contract['palette'])} palette samples, {len(regions)} region crops.")
    print("Read-only audit; contract and human decisions were not modified.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
