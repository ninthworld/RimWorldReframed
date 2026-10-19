"""Exact solid-face sideband regression for the declared foundation button fixture.

Geometry is frozen before comparisons. No registration, resizing, tolerance or font
comparison is performed. A pass proves only these opaque interior sidebands.
"""
from __future__ import annotations
import argparse
from collections import Counter
import json
import math
from pathlib import Path
import sys
from PIL import Image
import validate_foundation_run as vr
import visual_compare as vc

CONTRACT = {
    'id': 'foundation-button-sidebands-v1',
    'source_commit': '0fec83e3b0ef70a7ed344420237eb24a8943c77a',
    'source': 'FoundationPreview.DrawSpecimen geometry / Theme.ButtonFace draw order / DesignTokens colors',
    'specimen_ui_size': [760, 286], 'button_first_xy': [16, 82], 'button_step_x': 145,
    'button_size': [133, 36], 'button_radius': 3,
    'bands_button_local_half_open': {'left': [1, 5, 5, 27], 'right': [127, 5, 5, 27]},
    'faces': [('normal', [33, 40, 50]), ('hover_inferred', [45, 56, 71]),
              ('pressed_inferred', [24, 31, 40]), ('selected', [39, 47, 60])],
    'pixel_rule': 'Include physical pixel iff its center is within the scaled half-open band; origin from measured fiducials',
    'scope': 'Opaque sidebands only; excludes text, corners, top three fringe rows and bottom line; no whole-control/source acceptance',
}


def pixel_interval(low, high):
    if not vr.finite_number(low) or not vr.finite_number(high) or high <= low:
        raise vr.EvidenceError('Invalid physical interval')
    return math.ceil(low - 0.5), math.ceil(high - 0.5)


def analyze(image, origin, scale):
    if scale not in (1, 1.5) or type(scale) is bool:
        raise vr.EvidenceError('Only measured scale1 and1.5 contracts are supported')
    if len(origin) != 2 or any(type(v) is not int or v < 0 for v in origin):
        raise vr.EvidenceError('Measured physical origin must be two nonnegative integers')
    rgba = image.convert('RGBA')
    inclusion = Image.new('L', image.size)
    difference = Image.new('RGB', image.size)
    records = []
    total_included = total_bad = 0
    for index, (name, expected) in enumerate(CONTRACT['faces']):
        button_x, button_y = 16 + 145 * index, 82
        for side, (local_x, local_y, width, height) in CONTRACT['bands_button_local_half_open'].items():
            x0, x1 = pixel_interval(origin[0] + (button_x + local_x) * scale,
                                    origin[0] + (button_x + local_x + width) * scale)
            y0, y1 = pixel_interval(origin[1] + (button_y + local_y) * scale,
                                    origin[1] + (button_y + local_y + height) * scale)
            if min(x0, y0) < 0 or x1 > image.width or y1 > image.height or x1 <= x0 or y1 <= y0:
                raise vr.EvidenceError('Declared sideband is empty or exceeds actual capture')
            bad = []
            unexpected = Counter()
            maximum = total_error = 0
            for y in range(y0, y1):
                for x in range(x0, x1):
                    if inclusion.getpixel((x, y)):
                        raise vr.EvidenceError('Declared bands overlap')
                    inclusion.putpixel((x, y), 255)
                    actual = rgba.getpixel((x, y))
                    errors = [abs(a-b) for a,b in zip(actual, expected + [255])]
                    if any(errors):
                        bad.append((x, y))
                        unexpected[actual] += 1
                        maximum = max(maximum, max(errors))
                        total_error += sum(errors)
                        difference.putpixel((x, y), tuple(errors[:3]))
            bbox = None
            if bad:
                xs, ys = zip(*bad)
                bbox = [min(xs), min(ys), max(xs)-min(xs)+1, max(ys)-min(ys)+1]
            count = (x1-x0)*(y1-y0)
            total_included += count
            total_bad += len(bad)
            records.append({'button_index': index, 'state': name, 'side': side, 'expected_rgba': expected + [255],
                            'physical_rect_xywh': [x0, y0, x1-x0, y1-y0], 'included_pixels': count,
                            'bad_pixels': len(bad), 'maximum_channel_error': maximum, 'total_absolute_channel_error': total_error,
                            'difference_bbox_client_xywh': bbox,
                            'unexpected_rgba_counts': [{'rgba': list(color), 'pixels': count} for color,count in sorted(unexpected.items())]})
    if not total_included: raise vr.EvidenceError('Empty sideband union cannot pass')
    return {'status': 'pass' if total_bad == 0 else 'fail', 'included_pixels': total_included,
            'bad_pixels': total_bad, 'bands': records}, inclusion, difference


def check(capture, request_path, mapping_path, output):
    request_path, mapping_path = Path(request_path).resolve(), Path(mapping_path).resolve()
    request = vr.load_json(request_path)
    if request.get('specimen_page') != 'primitives' or request.get('specimen_version') not in (3, 4, 5):
        raise vr.EvidenceError('Requires primitive specimen3,4 or5; other layouts need a reviewed contract')
    png_path, png = vc.load_png(capture)
    if request.get('png_sha256') != vc.sha256(png_path): raise vr.EvidenceError('Request PNG hash mismatch')
    if png.size != (request.get('client_width'), request.get('client_height')): raise vr.EvidenceError('Client dimension mismatch')
    audit = vr.Audit()
    rect = vr.check_mapping(audit, mapping_path, request_path, request, png_path, png)
    scale = request['ui_scale']
    if scale not in (1, 1.5) or rect[2:] != [round(760*scale), round(286*scale)]:
        raise vr.EvidenceError('Measured specimen dimensions disagree with fixed local geometry')
    result, inclusion, difference = analyze(png, rect[:2], scale)
    result.update(schema='foundation-button-sideband-result-v1', contract=CONTRACT,
                  capture={'path': str(png_path), 'sha256': vc.sha256(png_path)},
                  request={'path': str(request_path), 'sha256': vc.sha256(request_path)},
                  mapping={'path': str(mapping_path), 'sha256': vc.sha256(mapping_path)},
                  observed_specimen_client_rect_xywh=rect, ui_scale=scale,
                  source_acceptance=False, golden_approved=False, tool_sha256=vc.sha256(Path(__file__)))
    out = vc.prepare_output(output, [png_path, request_path, mapping_path])
    # These diagnostics preserve original physical coordinates/pixels. No scaling.
    inclusion.save(out/'sideband_mask.png')
    difference.save(out/'sideband_raw_diff.png')
    png.crop((rect[0],rect[1],rect[0]+rect[2],rect[1]+rect[3])).save(out/'actual_specimen.png')
    result['artifacts']={p.name: vc.sha256(p) for p in out.glob('*.png')}
    (out/'result.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    return result


def main(argv=None):
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('capture','request','mapping','output'):parser.add_argument('--'+name,required=True)
    args=parser.parse_args(argv)
    try:
        result=check(args.capture,args.request,args.mapping,args.output)
        print(json.dumps({'status':result['status'],'bad_pixels':result['bad_pixels'],'included_pixels':result['included_pixels']}))
        return 0 if result['status']=='pass' else 1
    except (vr.EvidenceError,vc.InvalidInput,OSError,ValueError,KeyError,TypeError) as exc:
        print(json.dumps({'status':'invalid','error':str(exc)}),file=sys.stderr)
        return 2


if __name__=='__main__':raise SystemExit(main())