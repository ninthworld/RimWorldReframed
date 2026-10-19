"""Author static storyteller rhythm artwork; no curve is generated during play.

Cassandra follows the measured path in details/storyteller.png (source pixels).
Phoebe and Randy are qualitative design extensions, never incident forecasts.
The output is transparent and contains only the thin line, leaving panel opacity
and rounded borders to the view. Run with Pillow from the repository root.
"""
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Textures" / "Reframed" / "NewGame"
OUT.mkdir(parents=True, exist_ok=True)
SCALE = 4
SUPERSAMPLE = 16

# Measured source path relative to the 276x65 graph's upper left.
PATHS = {
    "Cassandra": [(18, 56), (61, 54), (91, 51), (131, 43), (171, 31),
                  (211, 15), (241, 9), (265, 13)],
    "Phoebe": [(18, 55), (60, 54), (92, 51), (118, 36), (139, 50),
               (181, 53), (213, 50), (241, 29), (265, 48)],
    "Randy": [(18, 52), (42, 41), (63, 49), (91, 14), (115, 44),
              (143, 24), (166, 48), (192, 43), (218, 9), (241, 36), (265, 22)],
}


def smooth_path(points):
    """Catmull-Rom interpolation through the authored source points."""
    expanded = [points[0], *points, points[-1]]
    output = []
    for i in range(1, len(expanded) - 2):
        p0, p1, p2, p3 = expanded[i - 1:i + 3]
        for step in range(128):
            t = step / 128
            output.append(tuple(.5 * ((2 * p1[d]) + (-p0[d] + p2[d]) * t
                + (2 * p0[d] - 5 * p1[d] + 4 * p2[d] - p3[d]) * t * t
                + (-p0[d] + 3 * p1[d] - 3 * p2[d] + p3[d]) * t * t * t)
                for d in range(2)))
    return [*output, points[-1]]


for name, points in PATHS.items():
    mask = Image.new("L", (276 * SUPERSAMPLE, 65 * SUPERSAMPLE))
    draw = ImageDraw.Draw(mask)
    path = smooth_path(points)
    # One continuous path avoids short-segment endpoints punching tiny gaps into
    # the line. Antialias coverage independently from its source opacity ramp.
    draw.line([tuple(v * SUPERSAMPLE for v in p) for p in path],
              fill=255, width=SUPERSAMPLE, joint="curve")
    ramp = Image.new("L", (mask.width, 1))
    ramp.putdata([round(255 * min(1, max(.025, (x / SUPERSAMPLE - 15) / 230)))
                  for x in range(mask.width)])
    mask = ImageChops.multiply(mask, ramp.resize(mask.size))
    mask = mask.resize((276 * SCALE, 65 * SCALE), Image.Resampling.LANCZOS)
    # Keep RGB white even in transparent texels so Unity's bilinear sampling
    # cannot darken the fine stroke against invisible black neighboring pixels.
    image = Image.new("RGBA", mask.size, (230, 234, 237, 0))
    image.putalpha(mask)
    image.save(OUT / f"Curve{name}.png", optimize=True)
    print(f"{name}: {image.width}x{image.height}, transparent authored curve")
