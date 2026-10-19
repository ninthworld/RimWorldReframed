"""Isolate actual rendered UI pixels for the README; never redraw controls.

Requires Pillow. Input captures and exact crop rectangles are recorded in the
output manifest. All RGB pixels inside the panels remain byte-identical.
"""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Docs/Showcase'
SPECS={
    'pawn-panel.png':('pawn-overview.jpg',[(13,856,853,1206)]),
    'work-panels.png':('work.jpg',[(182,734,1170,1003),(13,1011,1184,1204)]),
    'schedule-panel.png':('schedule.jpg',[(13,832,1184,1204)]),
    'research-panel.png':('research.jpg',[(13,424,1363,1204)]),
}
records=[]
for name,(source,boxes) in SPECS.items():
    src=Image.open(OUT/source).convert('RGBA')
    crops=[src.crop(box) for box in boxes]
    width=max(c.width for c in crops)+32
    height=sum(c.height for c in crops)+32+16*(len(crops)-1)
    canvas=Image.new('RGBA',(width,height))
    y=16
    for crop in crops:
        mask=Image.new('L',crop.size,0)
        ImageDraw.Draw(mask).rounded_rectangle((0,0,crop.width-1,crop.height-1),radius=8,fill=255)
        crop.putalpha(mask)
        canvas.paste(crop,((width-crop.width)//2,y))
        y+=crop.height+16
    path=OUT/name
    canvas.save(path,optimize=True)
    records.append({'path':name,'source':source,'source_sha256':hashlib.sha256((OUT/source).read_bytes()).hexdigest(),
                    'crop_boxes_xyxy':boxes,'output_size':list(canvas.size),
                    'processing':'Exact RGB pixel crops, 8px rounded outside-corner alpha mask, transparent padding. Work panels stacked. No retouching, recoloring, rescaling, generated content or reconstructed controls.',
                    'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
(OUT/'panel-crops.json').write_text(json.dumps({'schema':'actual-ui-panel-crops-v1','panels':records},indent=2)+'\n',encoding='utf-8',newline='\n')
print(json.dumps(records,indent=2))
