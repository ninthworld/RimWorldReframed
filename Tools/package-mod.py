"""Package only runtime mod files using an explicitly supplied, tested mod DLL."""
import argparse
import hashlib
import json
import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--assembly', required=True, type=Path, help='Explicit tested ReframedUI.dll; never bundles game/Harmony DLLs')
    parser.add_argument('--output', required=True, type=Path, help='New empty runtime package directory')
    args = parser.parse_args()
    assembly = args.assembly.resolve()
    dest = args.output.resolve()
    if assembly.name.lower() != 'reframedui.dll' or not assembly.is_file():
        parser.error('Supply a built ReframedUI.dll')
    if dest == ROOT or ROOT.is_relative_to(dest) or assembly.is_relative_to(dest):
        parser.error('Output must not contain the source checkout or input assembly')
    if dest.exists() and any(dest.iterdir()):
        parser.error('Output must be empty; existing packages are never deleted')
    version = re.search(r'<Version>([^<]+)</Version>', (ROOT / 'Source/ReframedUI.csproj').read_text()).group(1)
    selected = [ROOT / n for n in ('README.txt', 'LICENSE', 'THIRD_PARTY_NOTICES.md')]
    for name, suffix in [('About', '.xml'), ('Defs', '.xml'), ('Patches', '.xml'), ('Textures', '.png')]:
        selected.extend(sorted((ROOT / name).rglob('*' + suffix)))
    for src in selected:
        if src.is_symlink():
            parser.error(f'Refusing symlink: {src}')
        dst = dest / src.relative_to(ROOT)
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(src, dst)
    (dest / 'Assemblies').mkdir(parents=True)
    shutil.copyfile(assembly, dest / 'Assemblies/ReframedUI.dll')
    (dest / 'README.txt').write_text(f'''REFRAMED UI — {version}\n\nRimWorld 1.6 development build; complete visual/gameplay acceptance remains open.\nCopy this folder into RimWorld's Mods directory. Enable Harmony before Reframed UI\nand restart RimWorld after a DLL update. Harmony is an external dependency.\nUse backed-up saves for testing. Current runtime development targets Windows.\n\nThis player package intentionally contains no developer documentation or source.\nSee LICENSE and THIRD_PARTY_NOTICES.md for original contribution licensing and\nreference/dependency attribution. package-manifest.json records the supplied DLL\nand packaged-file hashes; it does not establish gameplay or release acceptance.\n''', encoding='utf-8')
    notice = dest / 'THIRD_PARTY_NOTICES.md'
    notice.write_text(notice.read_text(encoding='utf-8').replace('[source/package policy](Docs/UI-Reworked/07-github-readiness.md)', 'the source repository\'s publication policy'), encoding='utf-8')
    files = [{'path': p.relative_to(dest).as_posix(), 'sha256': hashlib.sha256(p.read_bytes()).hexdigest(), 'bytes': p.stat().st_size}
             for p in sorted(dest.rglob('*')) if p.is_file()]
    receipt = {'schema': 'runtime-package-v1', 'source_declared_version': version,
               'assembly_identity_note': 'Hash records supplied bytes. Caller must verify DLL version/source and runtime acceptance before release.',
               'published': False, 'files': files}
    (dest / 'package-manifest.json').write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'files': len(files), 'output': str(dest), 'source_declared_version': version, 'published': False}, indent=2))


if __name__ == '__main__':
    main()
