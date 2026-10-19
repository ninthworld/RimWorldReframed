"""Audit foundation capture evidence. Passing integrity checks never approves art/goldens."""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

from PIL import ImageChops
import visual_compare as vc

ROOT = Path(__file__).resolve().parents[1]
SCHEMA = ROOT / 'Docs/UI-Reworked/Foundations/Harness/run-manifest.schema.json'
KNOWN = {'observed', 'measured'}


class EvidenceError(ValueError):
    pass


def load_json(path):
    def pairs(items):
        result = {}
        for key, value in items:
            if key in result:
                raise EvidenceError(f'Duplicate JSON key: {key}')
            result[key] = value
        return result
    try:
        return json.loads(Path(path).read_text(encoding='utf-8-sig'), object_pairs_hook=pairs,
                          parse_constant=lambda value: (_ for _ in ()).throw(EvidenceError(f'Nonfinite JSON number: {value}')))
    except (OSError, ValueError) as exc:
        raise EvidenceError(f'Cannot read JSON {path}: {exc}') from exc


def finite_number(value):
    if type(value) not in (int, float): return False
    try: return math.isfinite(value)
    except OverflowError: return False


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def schema_errors(schema, value):
    """Fail-closed interpreter for this project's fixed schema keyword subset only.

    Not a general Draft 2020-12 implementation. Unsupported keywords stop validation.
    """
    supported = {'$schema', '$id', '$defs', '$ref', '$comment', 'title', 'description', 'type', 'const', 'enum',
                 'required', 'properties', 'additionalProperties', 'oneOf', 'allOf', 'if', 'then',
                 'minimum', 'minLength', 'pattern', 'minItems', 'maxItems', 'items', 'prefixItems'}

    def audit(node):
        if not isinstance(node, dict):
            raise EvidenceError('Unsupported nonobject schema node')
        unknown = set(node) - supported
        if unknown:
            raise EvidenceError(f'Unsupported schema keywords: {sorted(unknown)}')
        for key in ('properties', '$defs'):
            for child in node.get(key, {}).values(): audit(child)
        for key in ('oneOf', 'allOf', 'prefixItems'):
            for child in node.get(key, []): audit(child)
        for key in ('if', 'then', 'items'):
            if key in node: audit(node[key])
    audit(schema)

    def check(node, data, path):
        errors = []
        if '$ref' in node:
            target = schema
            if not node['$ref'].startswith('#/'):
                raise EvidenceError('Only local schema references are supported')
            try:
                for part in node['$ref'][2:].split('/'): target = target[part]
            except KeyError as exc:
                raise EvidenceError(f'Broken schema reference: {node["$ref"]}') from exc
            errors += check(target, data, path)
        if 'const' in node and (type(data) is not type(node['const']) or data != node['const']):
            errors.append(f'{path}: wrong constant')
        if 'enum' in node and not any(type(data) is type(option) and data == option for option in node['enum']):
            errors.append(f'{path}: outside enum')
        kind = node.get('type')
        types = {'object': lambda x: isinstance(x, dict), 'array': lambda x: isinstance(x, list),
                 'string': lambda x: isinstance(x, str), 'integer': lambda x: type(x) is int,
                 'number': finite_number, 'boolean': lambda x: type(x) is bool, 'null': lambda x: x is None}
        if kind and (kind not in types or not types[kind](data)):
            return errors + [f'{path}: expected {kind}']
        if 'oneOf' in node and sum(not check(child, data, path) for child in node['oneOf']) != 1:
            errors.append(f'{path}: must match exactly one reading/value shape')
        for child in node.get('allOf', []): errors += check(child, data, path)
        if 'if' in node and not check(node['if'], data, path) and 'then' in node:
            errors += check(node['then'], data, path)
        if isinstance(data, dict):
            properties = node.get('properties', {})
            for key in node.get('required', []):
                if key not in data: errors.append(f'{path}.{key}: missing')
            for key, child in properties.items():
                if key in data: errors += check(child, data[key], f'{path}.{key}')
            if node.get('additionalProperties') is False:
                for key in set(data) - set(properties): errors.append(f'{path}.{key}: unexpected field')
        if isinstance(data, list):
            if len(data) < node.get('minItems', 0) or len(data) > node.get('maxItems', math.inf):
                errors.append(f'{path}: invalid array length')
            for index, child in enumerate(node.get('prefixItems', [])):
                if index < len(data): errors += check(child, data[index], f'{path}[{index}]')
            if 'items' in node:
                for index in range(len(node.get('prefixItems', [])), len(data)):
                    errors += check(node['items'], data[index], f'{path}[{index}]')
        if isinstance(data, str):
            if len(data) < node.get('minLength', 0): errors.append(f'{path}: string too short')
            if 'pattern' in node and not re.search(node['pattern'], data): errors.append(f'{path}: pattern mismatch')
        if finite_number(data) and data < node.get('minimum', -math.inf): errors.append(f'{path}: below minimum')
        return errors
    return check(schema, value, '$')


class Audit:
    def __init__(self, relocations=()):
        self.relocations = relocations
        self.errors, self.gaps, self.verified = [], [], {}

    def path(self, value, base):
        if not isinstance(value, str) or not value.strip(): raise EvidenceError('Missing artifact path')
        original = Path(value)
        for source, destination in self.relocations:
            try: return (destination / original.relative_to(source)).resolve()
            except ValueError: pass
        return (original if original.is_absolute() else base / original).resolve()

    def artifact(self, record, base, label):
        if not isinstance(record, dict) or set(record) != {'path', 'sha256'}:
            raise EvidenceError(f'{label}: expected path/sha256 artifact')
        if not isinstance(record['sha256'], str) or not re.fullmatch('[0-9a-f]{64}', record['sha256']):
            raise EvidenceError(f'{label}: invalid SHA-256')
        path = self.path(record['path'], base)
        if not path.is_file(): raise EvidenceError(f'{label}: missing artifact {path}')
        actual = digest(path)
        if actual != record['sha256']: raise EvidenceError(f'{label}: SHA-256 mismatch for {path}')
        self.verified[str(path)] = actual
        return path

    def attempt(self, label, function):
        try: return function()
        except (EvidenceError, vc.InvalidInput, OSError, ET.ParseError, KeyError, TypeError, ValueError) as exc:
            self.errors.append(f'{label}: {exc}')
            return None

    def reading(self, run, dotted, required=True):
        node = run
        for key in dotted.split('.'): node = node[key]
        if node['status'] not in KNOWN:
            if required: self.gaps.append(f'{dotted}: {node["status"]}: {node["reason"]}')
            return None
        return node['value']

    def walk_artifacts(self, node, base, path='$'):
        if isinstance(node, dict):
            if set(node) == {'path', 'sha256'}:
                self.attempt(path, lambda: self.artifact(node, base, path))
            else:
                for key, value in node.items(): self.walk_artifacts(value, base, f'{path}.{key}')
        elif isinstance(node, list):
            for index, value in enumerate(node): self.walk_artifacts(value, base, f'{path}[{index}]')


def positive(value, label, integer=False):
    if not finite_number(value) or value <= 0 or (integer and type(value) is not int):
        raise EvidenceError(f'{label}: expected positive {"integer" if integer else "finite number"}')
    return value


def check_request(audit, path, dll_path):
    request = load_json(path)
    required = {'schema', 'acceptance', 'capture', 'unity_version', 'assembly_version', 'mod_sha256',
                'runtime_report', 'runtime_report_sha256', 'color_space', 'graphics_api', 'client_width', 'client_height',
                'ui_width', 'ui_height', 'ui_scale', 'frame_requested', 'specimen_gui_to_screen',
                'specimen_coordinates_status', 'font', 'gui_state_probe_passed', 'command_count', 'dispatches'}
    if not isinstance(request, dict) or not required <= set(request): raise EvidenceError('Incomplete capture request fields')
    if request['schema'] != 'reframed-foundation-capture-v1': raise EvidenceError('Unsupported capture request schema')
    if type(request['gui_state_probe_passed']) is not bool: raise EvidenceError('GUI-state probe result must be Boolean')
    for key in ('client_width', 'client_height', 'ui_width', 'ui_height'): positive(request[key], key, True)
    scale = positive(request['ui_scale'], 'ui_scale')
    for axis in ('width', 'height'):
        if abs(request['client_' + axis] - request['ui_' + axis] * scale) > max(1, scale):
            raise EvidenceError(f'{axis}: client/UI dimensions inconsistent with declared scale (rounding allowance one UI pixel)')
    if type(request['frame_requested']) is not int or request['frame_requested'] < 0: raise EvidenceError('Invalid request frame')
    if 'frame_captured' in request and (type(request['frame_captured']) is not int or request['frame_captured'] < request['frame_requested']):
        raise EvidenceError('Captured render frame precedes request or is invalid')
    rect = request['specimen_gui_to_screen']
    if not isinstance(rect, list) or len(rect) != 4 or not all(finite_number(v) for v in rect) or min(rect[2:]) <= 0:
        raise EvidenceError('Invalid reported GUIToScreen rectangle')
    png_path = audit.path(request['capture'], path.parent)
    _, png = vc.load_png(png_path)
    if png.size != (request['client_width'], request['client_height']): raise EvidenceError('PNG dimensions differ from client metadata')
    if 'png_width' in request or 'png_height' in request:
        if any(type(request.get('png_' + axis)) is not int for axis in ('width', 'height')):
            raise EvidenceError('PNG width/height metadata must be a complete integer pair')
        if png.size != (request['png_width'], request['png_height']):
            raise EvidenceError('PNG dimensions differ from declared PNG width/height')
    if 'capture_phase' in request and 'frame_captured' not in request:
        raise EvidenceError('Capture phase without captured render frame')
    if 'png_sha256' in request and request['png_sha256'] != digest(png_path): raise EvidenceError('Capture PNG hash differs from final request')
    if 'frame_captured' in request and request.get('capture_phase') != 'end_of_frame_render_target':
        raise EvidenceError('Captured frame needs explicit end_of_frame_render_target phase')
    report = audit.artifact({'path': request['runtime_report'], 'sha256': request['runtime_report_sha256']}, path.parent, 'runtime XML')
    raw = report.read_bytes()
    if b'<!DOCTYPE' in raw.upper() or b'<!ENTITY' in raw.upper(): raise EvidenceError('XML DTD/entities are unsupported')
    xml = ET.fromstring(raw)
    environment = xml.find('environment')
    if xml.tag != 'reframedRuntimeReport' or environment is None: raise EvidenceError('Invalid runtime report root/environment')
    if environment.get('unityVersion') != request['unity_version']: raise EvidenceError('Unity version differs between request and XML')
    assemblies = environment.findall("assembly[@role='mod']")
    if len(assemblies) != 1 or assemblies[0].get('sha256') != request['mod_sha256']: raise EvidenceError('Mod hash differs between request and runtime XML')
    if 'Version=' + request['assembly_version'] + ',' not in assemblies[0].get('identity', ''): raise EvidenceError('Mod assembly version differs from XML')
    dll_path = Path(dll_path).resolve()
    audit.artifact({'path': str(dll_path), 'sha256': request['mod_sha256']}, path.parent, 'retained mod DLL')
    metadata_files = request.get('metadata_files', [])
    if not isinstance(metadata_files, list): raise EvidenceError('metadata_files must be an array')
    for index, artifact in enumerate(metadata_files): audit.artifact(artifact, path.parent, f'metadata_files[{index}]')
    rows = request['dispatches']
    if not isinstance(rows, list) or type(request['command_count']) is not int or request['command_count'] != len(rows):
        raise EvidenceError('command_count differs from recorded dispatch count')
    for index, row in enumerate(rows):
        if not isinstance(row, dict) or not {'sequence', 'command', 'target', 'frame', 'event'} <= set(row): raise EvidenceError('Malformed request dispatch')
        if type(row['sequence']) is not int or row['sequence'] != index + 1: raise EvidenceError('Duplicate/out-of-order dispatch sequence')
        if type(row['frame']) is not int or not 0 <= row['frame'] <= request['frame_requested']: raise EvidenceError('Invalid dispatch frame')
        if not all(isinstance(row[k], str) and row[k].strip() for k in ('command', 'target', 'event')): raise EvidenceError('Empty dispatch command/target/event')
    audit.verified[str(png_path)] = digest(png_path)
    audit.verified[str(path)] = digest(path)
    return request, png_path, png, report, xml


def complete(request_path, dll_path, output, relocations=()):
    audit = Audit(relocations)
    request_path = Path(request_path).resolve()
    request, png_path, png, report, _ = check_request(audit, request_path, dll_path)
    receipt = {'schema': 'reframed-foundation-completion-v1', 'state': 'complete',
               'request': {'path': str(request_path), 'sha256': digest(request_path)},
               'png': {'path': str(png_path), 'sha256': digest(png_path)},
               'runtime_report': {'path': str(report), 'sha256': digest(report)},
               'mod_dll': {'path': str(Path(dll_path).resolve()), 'sha256': digest(dll_path)},
               'png_dimensions': list(png.size), 'frame_requested': request['frame_requested'],
               'completed_utc': datetime.now(timezone.utc).isoformat(),
               'completion_method': 'Post-write PNG decode, dimensions and SHA-256; no monitor presentation assertion',
               'presented_frame': {'status': 'unknown', 'value': None, 'reason': 'File completion does not identify the monitor-presented frame'}}
    if 'frame_captured' in request:
        receipt.update(frame_captured=request['frame_captured'], capture_phase=request['capture_phase'])
    out = vc.prepare_output(output, [request_path, png_path, report, Path(dll_path).resolve()])
    (out / 'completion.json').write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
    return receipt


def check_mapping(audit, path, request_path, request, png_path, png):
    mapping = load_json(path)
    if mapping.get('schema') != 'reframed-foundation-mapping-v1' or mapping.get('status') != 'observed':
        raise EvidenceError('Mapping must be explicitly observed, not inferred/unknown')
    if mapping.get('request_sha256') != digest(request_path) or mapping.get('png_sha256') != digest(png_path): raise EvidenceError('Mapping bound to different request/PNG')
    if mapping.get('client_dimensions') != list(png.size) or mapping.get('ui_scale') != request['ui_scale']: raise EvidenceError('Mapping dimensions/scale mismatch')
    if mapping.get('reported_rect_xywh') != request['specimen_gui_to_screen']: raise EvidenceError('Mapping GUI rectangle mismatch')
    if not all(isinstance(mapping.get(k), str) and mapping[k].strip() for k in ('method', 'reason')): raise EvidenceError('Mapping lacks calibration method/reason')
    rect = mapping.get('client_rect_xywh')
    if not isinstance(rect, list): raise EvidenceError('Mapping lacks explicit client pixel rectangle')
    vc.crop(png, rect, 'Observed client crop')
    x, y, w, h = rect
    corners = {'top_left': [x, y], 'top_right': [x+w-1, y], 'bottom_left': [x, y+h-1], 'bottom_right': [x+w-1, y+h-1]}
    fiducials = mapping.get('fiducials')
    if not isinstance(fiducials, list) or len(fiducials) != 4 or {f.get('name') for f in fiducials if isinstance(f, dict)} != set(corners):
        raise EvidenceError('Four distinct inside-corner fiducials are required')
    rgba = png.convert('RGBA')
    for point in fiducials:
        if point.get('client_xy') != corners[point['name']]: raise EvidenceError('Fiducial coordinate differs from observed crop corner')
        color = point.get('rgba')
        if not isinstance(color, list) or len(color) != 4 or any(type(v) is not int or not 0 <= v <= 255 for v in color): raise EvidenceError('Invalid fiducial RGBA')
        if tuple(color) != rgba.getpixel(tuple(point['client_xy'])): raise EvidenceError('Fiducial pixel color mismatch')
    audit.artifact(mapping['evidence'], path.parent, 'mapping review evidence')
    return rect


def validate(run_path, request_path, dll_path, output, completion_path=None, mapping_path=None, relocations=()):
    audit = Audit(relocations)
    run_path, request_path = Path(run_path).resolve(), Path(request_path).resolve()
    run = load_json(run_path)
    schema = load_json(SCHEMA)
    audit.errors.extend(schema_errors(schema, run))
    if audit.errors:
        return finish(audit, run_path, request_path, output, 'not_checked')
    audit.walk_artifacts(run, run_path.parent)
    checked = audit.attempt('capture', lambda: check_request(audit, request_path, dll_path))
    if checked:
        request, png_path, png, report, xml = checked
        if request['gui_state_probe_passed'] is not True: audit.gaps.append('GUI-state probe not passed in this capture; file integrity does not imply GUI-state acceptance')
        full = audit.reading(run, 'capture.full_frame')
        if full and full['sha256'] != digest(png_path): audit.errors.append('Run full frame differs from requested PNG')
        for dotted, request_key in [('display.client_width', 'client_width'), ('display.client_height', 'client_height'),
                                    ('display.ui_scale', 'ui_scale'), ('display.graphics_api', 'graphics_api'), ('display.color_space', 'color_space'),
                                    ('environment.unity_version', 'unity_version'), ('environment.mod_dll_sha256', 'mod_sha256')]:
            value = audit.reading(run, dotted)
            if value is not None and value != request[request_key]: audit.errors.append(f'{dotted}: disagrees with capture request')
        frame = audit.reading(run, 'capture.capture_frame_number')
        expected_frame = request.get('frame_captured', request['frame_requested'])
        if frame is not None and frame != expected_frame: audit.errors.append('Manifest capture frame differs from recorded render/request frame')
        audit.reading(run, 'capture.timing_source')
        mods = audit.reading(run, 'environment.active_mods_in_order', required=False)
        if mods is not None:
            declared = [(row['order'], row['package_id']) for row in mods]
            observed = [(int(row.get('sequence', '-1')), row.get('packageId')) for row in xml.findall('environment/activeMods/mod')]
            if declared != observed or len({row[0] for row in declared}) != len(declared): audit.errors.append('Manifest active mod order differs from runtime XML')
        logs = audit.reading(run, 'capture.logs')
        if logs is not None and not any(r['sha256'] == digest(request_path) for r in logs): audit.errors.append('Capture request is not hash-bound in run capture.logs')
        if logs is not None and not any(r['sha256'] == digest(report) for r in logs): audit.errors.append('Runtime XML is not hash-bound in run capture.logs')
        game_hash = audit.reading(run, 'environment.assembly_csharp_sha256')
        game_assemblies = xml.findall("environment/assembly[@role='game']")
        if game_hash and (len(game_assemblies) != 1 or game_assemblies[0].get('sha256') != game_hash): audit.errors.append('Game assembly hash differs from runtime XML')
        if completion_path:
            def receipt_check():
                receipt = load_json(completion_path)
                if receipt.get('schema') != 'reframed-foundation-completion-v1' or receipt.get('state') != 'complete': raise EvidenceError('Invalid completion schema/state')
                expected = {'request': request_path, 'png': png_path, 'runtime_report': report, 'mod_dll': Path(dll_path).resolve()}
                for key, path in expected.items():
                    found = audit.artifact(receipt[key], Path(completion_path).resolve().parent, f'completion.{key}')
                    if found != path: raise EvidenceError(f'Completion {key} refers to a different artifact')
                if receipt.get('png_dimensions') != list(png.size) or receipt.get('frame_requested') != request['frame_requested']: raise EvidenceError('Completion dimensions/frame mismatch')
                if not isinstance(receipt.get('completion_method'), str) or not receipt['completion_method'].strip(): raise EvidenceError('Completion method missing')
                stamp = datetime.fromisoformat(receipt['completed_utc'].replace('Z', '+00:00'))
                if stamp.tzinfo is None: raise EvidenceError('Completion timestamp needs timezone')
                presented = receipt.get('presented_frame')
                if not isinstance(presented, dict) or presented.get('status') != 'unknown' or presented.get('value') is not None or not presented.get('reason'):
                    raise EvidenceError('This completion contract does not certify monitor-presented frames')
                if 'frame_captured' in request and (receipt.get('frame_captured') != request['frame_captured'] or receipt.get('capture_phase') != request['capture_phase']): raise EvidenceError('Completion captured-render-frame mismatch')
            audit.attempt('completion', receipt_check)
        else: audit.gaps.append('No independent post-write completion receipt')
        client_rect = None
        if mapping_path:
            client_rect = audit.attempt('mapping', lambda: check_mapping(audit, Path(mapping_path).resolve(), request_path, request, png_path, png))
        else: audit.gaps.append('No observed physical-client crop mapping; GUIToScreen/ratio alone is unverified')
        def compare_check():
            reference = audit.reading(run, 'comparison.reference')
            actual = audit.reading(run, 'comparison.actual')
            ref_rect = audit.reading(run, 'comparison.reference_rect_xywh')
            actual_rect = audit.reading(run, 'comparison.actual_rect_xywh')
            result_ref = audit.reading(run, 'comparison.result')
            mode = audit.reading(run, 'comparison.channels')
            if any(v is None for v in (reference, actual, ref_rect, actual_rect, result_ref, mode)): return
            if mode not in ('rgb', 'rgba'): raise EvidenceError('Invalid comparison channels')
            ref_path = audit.artifact(reference, run_path.parent, 'comparison.reference')
            actual_path = audit.artifact(actual, run_path.parent, 'comparison.actual')
            _, ref_png = vc.load_png(ref_path)
            _, actual_png = vc.load_png(actual_path)
            ref_crop, actual_crop = vc.crop(ref_png, ref_rect, 'Reference crop'), vc.crop(actual_png, actual_rect, 'Actual crop')
            if ref_crop.size != actual_crop.size: raise EvidenceError('Comparison crop dimensions differ')
            if actual_png.size != png.size: raise EvidenceError('Actual comparison frame dimensions differ from captured client')
            if client_rect is not None:
                selected = ref_rect if digest(ref_path) == digest(png_path) else actual_rect if digest(actual_path) == digest(png_path) else None
                if selected != client_rect: raise EvidenceError('Comparison rectangle differs from observed physical crop')
            if mode == 'rgb' and any(im.convert('RGBA').getchannel('A').getextrema() != (255, 255) for im in (ref_crop, actual_crop)): raise EvidenceError('RGB comparison silently drops alpha')
            inclusion = run['comparison']['inclusion_mask']
            if inclusion['status'] in KNOWN:
                _, mask = vc.binary_mask(audit.artifact(inclusion['value'], run_path.parent, 'mask'), ref_crop.size)
            elif inclusion['status'] == 'not_applicable':
                from PIL import Image
                mask = Image.new('L', ref_crop.size, 255)
            else:
                audit.gaps.append('Comparison inclusion policy unknown; declare full inclusion not_applicable or provide binary mask')
                return
            raw = ImageChops.difference(ref_crop.convert(mode.upper()), actual_crop.convert(mode.upper()))
            measured, _ = vc.metrics(raw.tobytes(), mask.tobytes(), ref_crop.size, len(mode))
            result_path = audit.artifact(result_ref, run_path.parent, 'comparison.result')
            result = load_json(result_path)
            if result.get('channels') != mode: raise EvidenceError('Comparison result channel mode differs from manifest')
            expected_mask_hash = inclusion['value']['sha256'] if inclusion['status'] in KNOWN else None
            if result.get('mask', {}).get('sha256') != expected_mask_hash: raise EvidenceError('Comparison result inclusion mask hash mismatch')
            if result.get('metrics') != measured: raise EvidenceError('Stored comparison metrics disagree with independently recomputed pixels')
            if result.get('status') != ('pass' if measured['mismatched_pixels'] == 0 else 'fail'): raise EvidenceError('Stored comparison status disagrees with pixels')
            for key, record, rect in [('reference', reference, ref_rect), ('actual', actual, actual_rect)]:
                if result.get(key, {}).get('sha256') != record['sha256'] or result.get(key, {}).get('rect_xywh') != rect: raise EvidenceError('Comparison result provenance/rectangle mismatch')
            if measured['mismatched_pixels']: audit.errors.append('Comparison contains unequal included pixels')
            component = audit.reading(run, 'capture.component_frame')
            if component:
                _, image = vc.load_png(audit.artifact(component, run_path.parent, 'component crop'))
                if image.size != actual_crop.size or image.convert(mode.upper()).tobytes() != actual_crop.convert(mode.upper()).tobytes(): raise EvidenceError('Component PNG does not equal declared actual full-frame crop')
        audit.attempt('comparison', compare_check)
        def trace_check():
            record = audit.reading(run, 'input.trace')
            if record is None: return
            path = audit.artifact(record, run_path.parent, 'input.trace')
            trace = load_json(path)
            # Use the established semantic checker; disposable output remains ignored.
            import tempfile
            scratch = ROOT / 'work' / 'capture-audit'
            scratch.mkdir(parents=True, exist_ok=True)
            with tempfile.TemporaryDirectory(dir=scratch, prefix='trace-') as temp:
                Path(temp).resolve().relative_to(scratch.resolve())
                checked_trace = vc.validate_trace(path, Path(temp) / 'result')
            if checked_trace['status'] != 'pass': raise EvidenceError(f'Semantic input trace fails: {checked_trace["failures"]}')
            rows = trace['dispatches']
            if len(rows) != len(request['dispatches']): raise EvidenceError('Converted input trace omitted/added capture dispatches')
            for row, original in zip(rows, request['dispatches']):
                if (row['command'], row['target_id'], row['frame'], row['gui_event']) != (original['command'], original['target'], original['frame'], original['event']): raise EvidenceError('Converted input dispatch differs from original capture')
        audit.attempt('input trace', trace_check)
    claim = run['acceptance']['claim']
    if claim in ('source_exact', 'regression_exact'):
        def unknowns(node, path='$'):
            if isinstance(node, dict):
                if node.get('status') in ('unknown', 'inferred'): audit.errors.append(f'Exact claim blocked by {path}: {node["status"]}')
                for key, value in node.items(): unknowns(value, f'{path}.{key}')
            elif isinstance(node, list):
                for index, value in enumerate(node): unknowns(value, f'{path}[{index}]')
        unknowns(run)
        if any(row['blocks_claim'] for row in run['unresolved']): audit.errors.append('Exact claim has unresolved claim-blocking issues')
        audit.gaps.append('Exact acceptance requires independent source/baseline review; this tool never grants acceptance')
    if run['execution_status'] not in ('captured', 'evaluated'): audit.gaps.append('Run has not reached captured/evaluated state')
    return finish(audit, run_path, request_path, output, 'project-specific fail-closed schema keyword subset')


def finish(audit, run_path, request_path, output, schema_status):
    out = vc.prepare_output(output, [run_path, request_path, *map(Path, audit.verified)])
    result = {'schema': 'reframed-foundation-audit-v1', 'status': 'fail' if audit.errors else 'incomplete' if audit.gaps else 'pass',
              'scope': 'artifact integrity, structural completeness and declared crop/trace consistency only',
              'source_acceptance': False, 'golden_approved': False, 'schema_validation': schema_status,
              'run': {'path': str(run_path), 'sha256': digest(run_path)}, 'request': {'path': str(request_path), 'sha256': digest(request_path)},
              'errors': audit.errors, 'gaps': audit.gaps, 'verified_artifacts': audit.verified,
              'relocations': [{'from': str(a), 'to': str(b)} for a, b in audit.relocations],
              'tool_sha256': digest(__file__)}
    (out / 'result.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='operation', required=True)
    for operation in ('complete', 'validate'):
        command = sub.add_parser(operation)
        for name in ('request', 'dll', 'output'): command.add_argument('--' + name, required=True)
        command.add_argument('--relocate', action='append', default=[], metavar='OLD=NEW')
        if operation == 'validate':
            command.add_argument('--run', required=True)
            command.add_argument('--completion')
            command.add_argument('--mapping')
    args = parser.parse_args(argv)
    try:
        relocations = []
        for item in args.relocate:
            old, separator, new = item.partition('=')
            if not separator or not old or not new: raise EvidenceError('Relocation requires OLD=NEW explicit prefixes')
            relocations.append((Path(old).resolve(), Path(new).resolve()))
        if args.operation == 'complete':
            complete(args.request, args.dll, args.output, relocations)
            result = {'status': 'complete', 'source_acceptance': False}
        else:
            result = validate(args.run, args.request, args.dll, args.output, args.completion, args.mapping, relocations)
        print(json.dumps({'status': result['status'], 'output': str(Path(args.output).resolve())}))
        return 0 if result['status'] in ('pass', 'complete') else 1
    except (EvidenceError, vc.InvalidInput, OSError, ValueError, TypeError, KeyError, ET.ParseError) as exc:
        print(json.dumps({'status': 'invalid', 'error': str(exc)}), file=sys.stderr)
        return 2


if __name__ == '__main__':
    raise SystemExit(main())