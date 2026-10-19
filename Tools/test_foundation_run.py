"""Negative evidence probes use synthetic files, never original game artifacts."""
import copy
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

from PIL import Image
import validate_foundation_run as vr
import visual_compare as vc

ROOT = Path(__file__).resolve().parents[1]


class FoundationRunTests(unittest.TestCase):
    def setUp(self):
        scratch = (ROOT / 'work/foundation-run-tests').resolve()
        scratch.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=scratch, prefix='case-')
        self.root = Path(self.temp.name).resolve()
        self.root.relative_to(scratch)
        self.addCleanup(self.temp.cleanup)
        self.png = self.root / 'capture.png'
        self.repeat = self.root / 'repeat.png'
        self.dll = self.root / 'retained.dll'
        self.dll.write_bytes(b'synthetic DLL evidence, not executable or game code')
        self.report = self.root / 'runtime.xml'
        self.request_path = self.root / 'request.json'
        self.run_path = self.root / 'run.json'
        self.mapping_path = self.root / 'mapping.json'
        self.trace_path = self.root / 'trace.json'
        self.rect = [2, 2, 8, 6]
        image = Image.new('RGB', (16, 12), (10, 20, 30))
        self.corners = {'top_left': ([2, 2], [255, 0, 255, 255]), 'top_right': ([9, 2], [0, 255, 255, 255]),
                        'bottom_left': ([2, 7], [255, 255, 0, 255]), 'bottom_right': ([9, 7], [255, 255, 255, 255])}
        for point, color in self.corners.values(): image.putpixel(tuple(point), tuple(color[:3]))
        image.save(self.png)
        image.save(self.repeat)
        xml = ET.Element('reframedRuntimeReport', schemaVersion='1')
        environment = ET.SubElement(xml, 'environment', unityVersion='test-unity')
        ET.SubElement(environment, 'assembly', role='mod', sha256=vr.digest(self.dll), identity='ReframedUI, Version=0.3.0.0, Culture=neutral')
        ET.SubElement(environment, 'assembly', role='game', sha256='a' * 64)
        ET.ElementTree(xml).write(self.report, encoding='utf-8', xml_declaration=True)
        self.request = {'schema': 'reframed-foundation-capture-v1', 'acceptance': 'unreviewed development fixture',
                        'capture': str(self.png), 'unity_version': 'test-unity', 'assembly_version': '0.3.0.0',
                        'mod_sha256': vr.digest(self.dll), 'runtime_report': str(self.report), 'runtime_report_sha256': vr.digest(self.report),
                        'color_space': 'Gamma', 'graphics_api': 'test-api', 'client_width': 16, 'client_height': 12,
                        'ui_width': 16, 'ui_height': 12, 'ui_scale': 1, 'frame_requested': 10,
                        'specimen_gui_to_screen': self.rect, 'specimen_coordinates_status': 'unverified GUI report', 'font': 'unresolved',
                        'gui_state_probe_passed': True, 'command_count': 1,
                        'dispatches': [{'sequence': 1, 'command': 'increment', 'target': 'foundation-counter', 'frame': 3, 'event': 'MouseUp'}]}
        self.save(self.request_path, self.request)
        self.trace = {'schema_version': 1, 'test_id': 'SYNTHETIC-CAPTURE',
                      'actions': [{'action_id': 'click-1', 'command': 'increment', 'target_id': 'foundation-counter', 'expected_count': 1},
                                  {'action_id': 'disabled', 'command': 'increment', 'target_id': 'foundation-counter', 'expected_count': 0}],
                      'dispatches': [{'dispatch_id': 'one', 'action_id': 'click-1', 'command': 'increment',
                                      'target_id': 'foundation-counter', 'frame': 3, 'gui_event': 'MouseUp'}]}
        self.save(self.trace_path, self.trace)
        vc.compare(self.png, self.repeat, self.rect, self.rect, self.root / 'comparison')
        self.run = vr.load_json(ROOT / 'Docs/UI-Reworked/Foundations/Harness/run-manifest.template.json')
        self.run.update(test_id='SYNTHETIC-CAPTURE', execution_status='evaluated')
        self.run['acceptance']['claim'] = 'smoke_only'
        for dotted, value in {'display.client_width': 16, 'display.client_height': 12, 'display.ui_scale': 1,
                              'display.graphics_api': 'test-api', 'display.color_space': 'Gamma',
                              'environment.unity_version': 'test-unity', 'environment.mod_dll_sha256': vr.digest(self.dll),
                              'environment.assembly_csharp_sha256': 'a'*64, 'capture.full_frame': self.artifact(self.png),
                              'capture.capture_frame_number': 10, 'capture.timing_source': 'Synthetic request-frame only',
                              'capture.component_frame': self.artifact(self.root / 'comparison/actual_crop.png'),
                              'capture.logs': [self.artifact(self.request_path), self.artifact(self.report)],
                              'comparison.reference': self.artifact(self.png), 'comparison.actual': self.artifact(self.repeat),
                              'comparison.reference_rect_xywh': self.rect, 'comparison.actual_rect_xywh': self.rect,
                              'comparison.result': self.artifact(self.root / 'comparison/result.json'), 'comparison.channels': 'rgb',
                              'input.trace': self.artifact(self.trace_path)}.items(): self.reading(dotted, value)
        self.run['comparison']['inclusion_mask'] = {'status': 'not_applicable', 'value': None, 'reason': 'All crop pixels included'}
        evidence = self.root / 'calibration-notes.txt'
        evidence.write_text('Synthetic explicit corner measurements only; not game evidence', encoding='utf-8')
        self.mapping = {'schema': 'reframed-foundation-mapping-v1', 'status': 'observed', 'request_sha256': vr.digest(self.request_path),
                        'png_sha256': vr.digest(self.png), 'client_dimensions': [16, 12], 'ui_scale': 1,
                        'reported_rect_xywh': self.rect, 'client_rect_xywh': self.rect,
                        'method': 'Explicit synthetic inside-corner pixels', 'reason': 'Known fixture geometry', 'evidence': self.artifact(evidence),
                        'fiducials': [{'name': name, 'client_xy': point, 'rgba': color} for name, (point, color) in self.corners.items()]}
        self.save(self.mapping_path, self.mapping)
        vr.complete(self.request_path, self.dll, self.root / 'completion')
        self.completion = self.root / 'completion/completion.json'
        self.save(self.run_path, self.run)

    @staticmethod
    def save(path, data): path.write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8')
    @staticmethod
    def artifact(path): return {'path': str(path), 'sha256': vr.digest(path)}

    def reading(self, dotted, value):
        keys = dotted.split('.')
        node = self.run
        for key in keys[:-1]: node = node[key]
        node[keys[-1]] = {'status': 'observed', 'value': value, 'reason': 'Synthetic test contract'}

    def validate(self, completion=True, mapping=True):
        self.save(self.run_path, self.run)
        return vr.validate(self.run_path, self.request_path, self.dll, self.root / 'audit',
                           self.completion if completion else None, self.mapping_path if mapping else None)

    def assert_failed(self, text=None):
        result = self.validate()
        self.assertEqual(result['status'], 'fail', result)
        self.assertFalse(result['source_acceptance'])
        if text: self.assertTrue(any(text in row for row in result['errors']), result['errors'])
        return result

    def update_request(self):
        self.save(self.request_path, self.request)
        self.reading('capture.logs', [self.artifact(self.request_path), self.artifact(self.report)])

    def test_complete_synthetic_bundle_passes_integrity_only(self):
        result = self.validate()
        self.assertEqual(result['status'], 'pass', result)
        self.assertFalse(result['source_acceptance'])
        self.assertFalse(result['golden_approved'])
        self.assertGreaterEqual(len(result['verified_artifacts']), 8)

    def test_automatic_capture_can_be_sealed_without_fabricating_gui_probe(self):
        self.request['gui_state_probe_passed'] = False
        self.update_request()
        receipt = vr.complete(self.request_path, self.dll, self.root / 'auto-completion')
        self.assertEqual(receipt['state'], 'complete')
        result = self.validate(completion=False, mapping=False)
        self.assertEqual(result['status'], 'incomplete')
        self.assertTrue(any('GUI-state probe not passed' in row for row in result['gaps']))
        self.assertFalse(result['source_acceptance'])

    def test_absent_completion_or_mapping_is_incomplete(self):
        result = self.validate(completion=False, mapping=False)
        self.assertEqual(result['status'], 'incomplete')
        self.assertIn('No independent post-write completion receipt', result['gaps'])
        self.assertTrue(any('physical-client' in row for row in result['gaps']))

    def test_missing_png_fails(self):
        self.png.unlink()
        self.assert_failed('missing artifact')

    def test_modified_png_hash_fails(self):
        self.png.write_bytes(self.png.read_bytes() + b'modified')
        self.assert_failed('SHA-256 mismatch')

    def test_missing_xml_fails(self):
        self.report.unlink()
        self.assert_failed('missing artifact')

    def test_modified_xml_hash_fails(self):
        self.report.write_bytes(self.report.read_bytes() + b' ')
        self.assert_failed('SHA-256 mismatch')

    def test_missing_retained_dll_fails(self):
        self.dll.unlink()
        self.assert_failed('retained mod DLL')

    def test_modified_retained_dll_fails(self):
        self.dll.write_bytes(b'other build')
        self.assert_failed('SHA-256 mismatch')

    def test_png_dimensions_checked_independently_of_hashes(self):
        Image.new('RGB', (17, 12)).save(self.png)
        self.reading('capture.full_frame', self.artifact(self.png))
        self.assert_failed('PNG dimensions')

    def test_recorded_capture_frame_must_match_manifest(self):
        self.reading('capture.capture_frame_number', 900)
        self.assert_failed('Manifest capture frame differs')

    def test_declared_mod_order_must_match_runtime_report(self):
        self.reading('environment.active_mods_in_order', [{'package_id': 'missing-mod', 'order': 0,
                     'version': {'status': 'unknown', 'value': None, 'reason': 'synthetic'}}])
        self.assert_failed('active mod order differs')

    def test_changed_comparison_channel_claim_rejected(self):
        path = self.root / 'comparison/result.json'
        result = vr.load_json(path)
        result['channels'] = 'rgba'
        self.save(path, result)
        self.reading('comparison.result', self.artifact(path))
        self.assert_failed('channel mode differs')

    def test_zero_scale_rejected(self):
        self.request['ui_scale'] = 0
        self.update_request()
        self.assert_failed('ui_scale')

    def test_inconsistent_ui_and_client_scale_rejected(self):
        self.request['ui_scale'] = 2
        self.update_request()
        self.assert_failed('inconsistent with declared scale')

    def test_out_of_bounds_crop_rejected(self):
        self.reading('comparison.actual_rect_xywh', [15, 2, 8, 6])
        self.assert_failed('exceeds image')

    def test_noninteger_observed_crop_rejected(self):
        self.mapping['client_rect_xywh'] = [2.5, 2, 8, 6]
        self.save(self.mapping_path, self.mapping)
        self.assert_failed('four integers')

    def test_mapping_never_guesses_unobserved_coordinates(self):
        self.mapping['status'] = 'inferred'
        self.save(self.mapping_path, self.mapping)
        self.assert_failed('explicitly observed')

    def test_wrong_mapping_corner_color_rejected(self):
        self.mapping['fiducials'][0]['rgba'] = [0, 0, 0, 255]
        self.save(self.mapping_path, self.mapping)
        self.assert_failed('pixel color mismatch')

    def test_wrong_mapping_request_hash_rejected(self):
        self.mapping['request_sha256'] = '0'*64
        self.save(self.mapping_path, self.mapping)
        self.assert_failed('different request/PNG')

    def test_duplicate_original_dispatch_sequence_rejected(self):
        self.request['dispatches'].append(copy.deepcopy(self.request['dispatches'][0]))
        self.request['command_count'] = 2
        self.update_request()
        self.assert_failed('Duplicate/out-of-order')

    def test_duplicate_semantic_dispatch_rejected(self):
        self.trace['dispatches'].append({**self.trace['dispatches'][0], 'dispatch_id': 'two'})
        self.save(self.trace_path, self.trace)
        self.reading('input.trace', self.artifact(self.trace_path))
        self.assert_failed('Semantic input trace fails')

    def test_wrong_semantic_target_rejected(self):
        self.trace['dispatches'][0]['target_id'] = 'wrong-target'
        self.save(self.trace_path, self.trace)
        self.reading('input.trace', self.artifact(self.trace_path))
        self.assert_failed('target_id_mismatch')

    def test_trace_conversion_cannot_omit_original_dispatch(self):
        self.trace['dispatches'] = []
        self.trace['actions'][0]['expected_count'] = 0
        self.save(self.trace_path, self.trace)
        self.reading('input.trace', self.artifact(self.trace_path))
        self.assert_failed('omitted/added')

    def test_unknown_fields_cannot_be_promoted_to_source_acceptance(self):
        self.run['run_kind'] = 'reference_reconstruction'
        self.run['acceptance']['claim'] = 'source_exact'
        result = self.assert_failed('Exact claim blocked')
        self.assertFalse(result['golden_approved'])

    def test_inferred_values_cannot_be_promoted_to_regression_acceptance(self):
        self.run['run_kind'] = 'deterministic_regression'
        self.run['acceptance']['claim'] = 'regression_exact'
        self.run['display']['ui_scale']['status'] = 'inferred'
        self.assert_failed('inferred')

    def test_unknown_reading_must_have_null_value(self):
        self.run['display']['windows_dpi_percent']['value'] = 100
        self.assert_failed('reading/value shape')

    def test_unexpected_manifest_field_is_not_silently_ignored(self):
        self.run['display']['automatic_source_pass'] = True
        self.assert_failed('unexpected field')

    def test_component_png_must_equal_declared_full_frame_crop(self):
        path = self.root / 'comparison/actual_crop.png'
        Image.new('RGB', (8, 6), (50, 50, 50)).save(path)
        self.reading('capture.component_frame', self.artifact(path))
        self.assert_failed('does not equal declared')

    def test_forged_zero_metrics_are_recomputed(self):
        image = Image.open(self.repeat).copy()
        image.putpixel((4, 4), (200, 200, 200))
        image.save(self.repeat)
        self.reading('comparison.actual', self.artifact(self.repeat))
        path = self.root / 'comparison/result.json'
        forged = vr.load_json(path)
        forged['actual']['sha256'] = vr.digest(self.repeat)
        self.save(path, forged)
        self.reading('comparison.result', self.artifact(path))
        self.assert_failed('independently recomputed')

    def test_receipt_cannot_assert_monitor_presentation_from_file_completion(self):
        receipt = vr.load_json(self.completion)
        receipt['presented_frame'] = {'status': 'observed', 'value': 10, 'reason': 'assumed'}
        self.save(self.completion, receipt)
        self.assert_failed('does not certify monitor')

    def test_optional_synchronous_render_frame_fields_and_metadata_hashes(self):
        self.request.update(frame_captured=11, capture_phase='end_of_frame_render_target', png_sha256=vr.digest(self.png), png_width=16, png_height=12,
                            metadata_files=[self.artifact(self.root / 'calibration-notes.txt')])
        self.update_request()
        vr.complete(self.request_path, self.dll, self.root / 'completed-new')
        receipt = vr.load_json(self.root / 'completed-new/completion.json')
        self.assertEqual(receipt['frame_captured'], 11)
        self.assertEqual(receipt['presented_frame']['status'], 'unknown')
        self.assertEqual(receipt['presented_frame']['value'], None)

    def test_optional_png_dimensions_must_be_complete_exact_integers(self):
        for index, fields in enumerate(({'png_width': 16}, {'png_width': 16, 'png_height': True},
                                        {'png_width': 15, 'png_height': 12}, {'png_width': 16, 'png_height': 13})):
            with self.subTest(fields=fields):
                request = dict(self.request, **fields)
                self.save(self.request_path, request)
                with self.assertRaisesRegex(vr.EvidenceError, 'PNG.*(pair|width/height)'):
                    vr.complete(self.request_path, self.dll, self.root / f'invalid-png-dimensions-{index}')

    def test_malformed_captured_frame_is_never_sealed(self):
        for index, value in enumerate((9, -1, True, 10.5, '10', None)):
            with self.subTest(frame=value):
                self.save(self.request_path, dict(self.request, frame_captured=value, capture_phase='end_of_frame_render_target'))
                with self.assertRaisesRegex(vr.EvidenceError, 'Captured render frame'):
                    vr.complete(self.request_path, self.dll, self.root / f'invalid-frame-{index}')

    def test_capture_phase_requires_valid_paired_frame(self):
        for index, fields in enumerate(({'capture_phase': 'end_of_frame_render_target'},
                                        {'frame_captured': 10}, {'frame_captured': 10, 'capture_phase': 'screen_guess'})):
            with self.subTest(fields=fields):
                self.save(self.request_path, dict(self.request, **fields))
                with self.assertRaisesRegex(vr.EvidenceError, '(Capture phase|Captured frame)'):
                    vr.complete(self.request_path, self.dll, self.root / f'invalid-phase-{index}')

    def test_optional_metadata_file_modified_is_detected(self):
        self.request['metadata_files'] = [self.artifact(self.root / 'calibration-notes.txt')]
        self.update_request()
        (self.root / 'calibration-notes.txt').write_text('modified', encoding='utf-8')
        self.assert_failed('metadata_files')

    def test_explicit_relocation_no_basename_search(self):
        old = self.root / 'archived-original-location'
        self.request['capture'] = str(old / self.png.name)
        self.request['runtime_report'] = str(old / self.report.name)
        self.update_request()
        receipt = vr.complete(self.request_path, self.dll, self.root / 'relocated', [(old, self.root)])
        self.assertEqual(Path(receipt['png']['path']), self.png)

    def test_duplicate_keys_and_nan_are_invalid_json_evidence(self):
        for text in ('{"a":1,"a":2}', '{"a":NaN}'):
            path = self.root / 'bad.json'
            path.write_text(text, encoding='utf-8')
            with self.assertRaises(vr.EvidenceError): vr.load_json(path)

    def test_schema_changes_fail_closed_on_unsupported_keywords(self):
        with self.assertRaisesRegex(vr.EvidenceError, 'Unsupported schema keywords'):
            vr.schema_errors({'type': 'object', 'unevaluatedProperties': False}, {})

    def test_cli_valid_and_incomplete_exit_codes(self):
        for index, mapping in enumerate((True, False)):
            command = [sys.executable, str(ROOT / 'Tools/validate_foundation_run.py'), 'validate', '--run', str(self.run_path),
                       '--request', str(self.request_path), '--dll', str(self.dll), '--completion', str(self.completion),
                       '--output', str(self.root / f'cli-{index}')]
            if mapping: command += ['--mapping', str(self.mapping_path)]
            completed = subprocess.run(command, capture_output=True, text=True)
            self.assertEqual(completed.returncode, 0 if mapping else 1, completed.stderr + completed.stdout)
            self.assertEqual(json.loads(completed.stdout)['status'], 'pass' if mapping else 'incomplete')


if __name__ == '__main__':
    unittest.main(verbosity=2)