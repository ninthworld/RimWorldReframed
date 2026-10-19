"""Author the static plain-arrow silhouette shown in the storyteller footer.

Offline rasterization only; the running game draws the resulting PNG asset.
"""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
out = root / "Textures/Reframed/NewGame/Next.png"
image = Image.new("RGBA", (128, 128))
draw = ImageDraw.Draw(image)
draw.line([(20, 64), (104, 64)], fill="white", width=14)
draw.line([(66, 26), (104, 64), (66, 102)], fill="white", width=14, joint="curve")
image.resize((64, 64), Image.Resampling.LANCZOS).save(out)
