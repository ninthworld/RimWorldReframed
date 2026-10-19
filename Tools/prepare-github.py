"""Create a fresh source-only publication tree; never mutate the local Git history.

Uses an explicit allowlist. Only the named public showcase media are included;
other local evidence, logs, native binaries, saves and Git objects stay local.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SHOWCASE = set(json.loads((ROOT / 'Tools/showcase-files.json').read_text(encoding='utf-8')))
TOP_FILES = {
    '.gitattributes', '.gitignore', 'global.json', 'AGENTS.md', 'Build.ps1',
    'README.md', 'README.txt', 'PROJECT.md', 'CONTRIBUTING.md', 'LICENSE',
    'THIRD_PARTY_NOTICES.md',
}
TREE_EXTENSIONS = {
    'Source': {'.cs', '.csproj'}, 'Tests': {'.cs', '.csproj', '.ps1', '.py', '.json', '.md', '.png'},
    'Tools': {'.py', '.ps1', '.json', '.txt', '.cjs', '.cs', '.csproj', '.md'},
    'Assets': {'.svg'}, 'Textures': {'.png'}, 'About': {'.xml'},
    'Defs': {'.xml'}, 'Patches': {'.xml'}, '.github': {'.yml', '.yaml', '.md'},
}
DOCS = {
    '01-design-specification.md', '02-test-regimen.md', '03-work-order-plan.md',
    '04-team-workflow.md', '05-inferred-design-policy.md', '06-repository-setup.md',
    '07-github-readiness.md', 'README.md', 'decision-log.md', 'project-register.csv',
    'foundation-register.csv', 'inference-register.csv', 'visual-completion.md',
}
AUDIT = {'README.md', 'ui-todo.csv', 'native-ui-inventory.csv', 'native-ui-manifest.json',
         'native-ui-coverage.csv', 'coverage-rules.json', 'coverage-summary.json', 'verification.json'}
SKIP_PARTS = {'bin', 'obj', 'node_modules', '__pycache__', '.git', 'work', 'artifacts'}
FORBIDDEN = {'.dll', '.pdb', '.exe', '.rws', '.log', '.zip', '.7z', '.nupkg', '.webp', '.jpg'}
NOTICE = '''# Local evidence omitted from this source snapshot

The original development repository retains reference boards, screenshots,
pixel measurements, historical game logs, private native-method extracts and
deployment receipts. Those files and the original Git history are intentionally
absent here. Links to omitted evidence lead to this notice; they are not evidence
of a passed test. Source-shown visual comparisons need access to the original
reference and properly attributed local captures.

Design reference: [RimWorld UI Reworked by yarich SA](https://www.behance.net/gallery/232485291/RimWorld-UI-Reworked).
The concept and RimWorld assets are not relicensed by the project's MIT license.
See [third-party notices](../../THIRD_PARTY_NOTICES.md).

The textual specifications and decision/status registers are retained for
continued development. Their historical observations remain historical. The
current [audit and TODO list](Audit32/README.md) records the remaining work.

The separately curated [public showcase](../Showcase/README.md) contains the
user-requested current UI demonstrations. It does not restore the omitted
historical evidence or concept boards.
'''


def allowed(path: Path) -> bool:
    parts = path.parts
    if any(part in SKIP_PARTS for part in parts):
        return False
    name = path.as_posix()
    if name in SHOWCASE:
        return True
    if path.suffix.lower() in FORBIDDEN:
        return False
    if len(parts) == 1:
        return name in TOP_FILES
    if parts[0] in TREE_EXTENSIONS:
        return path.suffix.lower() in TREE_EXTENSIONS[parts[0]]
    if name.startswith('Docs/UI-Reworked/Audit32/'):
        return len(parts) == 4 and parts[-1] in AUDIT
    return len(parts) == 3 and parts[:2] == ('Docs', 'UI-Reworked') and parts[-1] in DOCS


def git(*args: str) -> str:
    return subprocess.check_output(['git', '-C', str(ROOT), *args], text=True).strip()


def sanitize_local_paths(text: str) -> str:
    # Historical personal paths are context, not required developer configuration.
    text = re.sub(r'C:(?:\\+|/)Users(?:\\+|/)[^\\/\s"<>]+', '<LOCAL_USER>', text, flags=re.I)
    text = re.sub(r'process_id"\s*:\s*\d+', 'process_id": null', text)
    return text


def rewrite_omitted_links(dest: Path, files: list[Path]) -> int:
    count = 0
    notice = dest / 'Docs/UI-Reworked/LOCAL_EVIDENCE.md'
    import os
    from urllib.parse import unquote
    for rel in files:
        if rel.suffix != '.md':
            continue
        p = dest / rel
        text = p.read_text(encoding='utf-8-sig')
        def replace(match: re.Match) -> str:
            nonlocal count
            label, url = match.group(1), match.group(2)
            if re.match(r'^(?:https?:|mailto:|#)', url):
                return match.group(0)
            path = unquote(url.split('#')[0])
            if not path or (p.parent / path).exists():
                return match.group(0)
            count += 1
            target = Path(os.path.relpath(notice, p.parent)).as_posix()
            return f'[{label} — local evidence omitted]({target})'
        # Do not span another opening bracket/newline: specifications contain
        # unchecked task markers before later image links.
        text = re.sub(r'!?\[([^\[\]\r\n]*)\]\(([^)\r\n]+)\)', replace, text)
        p.write_text(sanitize_local_paths(text), encoding='utf-8', newline='\n')
    return count


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True, help='New empty directory; existing content is never deleted')
    args = parser.parse_args()
    dest = args.output.resolve()
    if dest == ROOT or ROOT.is_relative_to(dest):
        parser.error('Output cannot be the source checkout or any ancestor')
    if dest.exists() and any(dest.iterdir()):
        parser.error('Output must be empty; choose a new directory to preserve earlier exports')
    # Select tracked and new, non-ignored source files. Generated caches cannot leak in.
    paths = subprocess.check_output(['git', '-C', str(ROOT), 'ls-files', '-z', '--cached', '--others', '--exclude-standard']).decode().split('\0')
    selected = sorted({Path(p) for p in paths if p and allowed(Path(p)) and (ROOT / p).is_file()})
    required = [Path(p) for p in ('README.md', 'LICENSE', 'CONTRIBUTING.md', 'Tools/Test.ps1', 'Docs/UI-Reworked/Audit32/README.md')]
    if not all(p in selected for p in required):
        raise SystemExit('Required onboarding/audit files missing; finish preparation before exporting')
    dest.mkdir(parents=True, exist_ok=True)
    for rel in selected:
        src = ROOT / rel
        if src.is_symlink():
            raise SystemExit(f'Refusing symlink: {rel}')
        target = dest / rel
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(src, target)
    docs = dest / 'Docs/UI-Reworked'
    (docs / 'LOCAL_EVIDENCE.md').write_text(NOTICE, encoding='utf-8')
    # This portable checkpoint replaces the local machine/session history, not its evidence.
    version = re.search(r'<Version>([^<]+)</Version>', (dest / 'Source/ReframedUI.csproj').read_text()).group(1)
    source_head = git('rev-parse', 'HEAD')
    state = {'schema_version': 1, 'updated_date': '2026-10-19', 'phase': 'source_snapshot_ready_for_development',
             'version': version, 'source_repository_commit': source_head, 'local_installed_dll_sha256': '1851FCAF146B322CAD9AA6A630708C897A071D5BF5E8DD3D7600B690A48915B8',
             'installed_here': False, 'active_agents': [], 'active_orders': [], 'visual_acceptance': False,
             'next_dependency': 'Work through Audit32/ui-todo.csv; obtain licensed game locally for build/live tests.',
             'historical_evidence': 'LOCAL_EVIDENCE.md'}
    (docs / 'execution-state.json').write_text(json.dumps(state, indent=2) + '\n', encoding='utf-8')
    (docs / 'HANDOFF.md').write_text(f'''# Continue development — v{version}\n\nThis is a source snapshot prepared from `{source_head}`. No game DLL or live game is included.\n\nRead [the current UI audit](Audit32/README.md), [the TODO list](Audit32/ui-todo.csv), [contribution workflow](../../CONTRIBUTING.md) and [publication record](07-github-readiness.md). All prior visual/functional acceptance gaps remain open. Startup and offline checks never establish visual completion.\n\nThe original local history/evidence is preserved separately; see [omissions](LOCAL_EVIDENCE.md). No workers or game processes are assigned here. Verify your own checkout status and DLL before tests. Every window and nested panel must open and contract on dismissal with a slightly translucent background, while native cleanup remains immediate.\n''', encoding='utf-8')
    (dest / 'AGENTS.md').write_text('''# Contributor continuity

Read `Docs/UI-Reworked/HANDOFF.md` and `execution-state.json`, then inspect actual Git status/HEAD and relevant audit entries. The checkout you are in is the source workspace; do not assume an installed game or inherited agent/process exists. Read `CONTRIBUTING.md` before building.

Use useful independent subagents with explicit ownership. Put editing workers in isolated worktrees outside any Steam Mods directory. One integrator controls shared source integration, deployment, game session, saves and baseline promotion.

Every window/panel, including secondary panels, must animate open and contract closed with a slightly translucent background. Keep native close/input cleanup immediate; motion is presentation only. Record older gaps instead of claiming global coverage. Preserve observed/measured/inferred distinctions and do not call inferred designs exact source matches.

Update the handoff and execution state at meaningful checkpoints. Edit authoritative documentation under `Docs/UI-Reworked`; preserve status/decision registers. Store disposable outputs under ignored `work/` or `artifacts/`. Never commit game/Harmony DLLs, saves, logs, credentials or reference artwork. Publication and external messaging require explicit user scope.
''', encoding='utf-8')
    files = sorted(p.relative_to(dest) for p in dest.rglob('*') if p.is_file())
    rewritten = rewrite_omitted_links(dest, files)
    # Generated source-only documentation does not pretend to verify absent source imagery.
    subprocess.run([__import__('sys').executable, str(dest / 'Tools/build-specification.py'), '--root', str(docs), '--source-only'], check=True)
    files = sorted(p.relative_to(dest) for p in dest.rglob('*') if p.is_file())
    manifest = {'schema': 'github-source-export-v1', 'source_commit': source_head,
                'source_worktree_dirty': bool(git('status', '--porcelain')), 'version': version,
                'existing_history_copied': False, 'published': False, 'omitted_local_links_redirected': rewritten,
                'showcase_allowlist': sorted(SHOWCASE),
                'excluded': ['existing Git history', 'reference artwork/crops', 'game screenshots except the named public showcase', 'game logs', 'native implementation/IL extracts', 'machine execution/deployment history', 'game and Harmony binaries', 'save data', 'build outputs and caches'],
                'files': [{'path': p.as_posix(), 'bytes': (dest / p).stat().st_size, 'sha256': hashlib.sha256((dest / p).read_bytes()).hexdigest()} for p in files]}
    (dest / 'SOURCE_SNAPSHOT.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'output': str(dest), 'files': len(files), 'bytes': sum(f['bytes'] for f in manifest['files']), 'rewritten_local_links': rewritten, 'published': False}, indent=2))


if __name__ == '__main__':
    main()
