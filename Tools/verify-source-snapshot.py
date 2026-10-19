"""Verify the publication manifest, exclusions and local document links without game files."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from urllib.parse import unquote

FORBIDDEN={'.dll','.pdb','.exe','.rws','.log','.zip','.7z','.nupkg','.webp','.jpg'}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('root',type=Path)
    args=parser.parse_args()
    root=args.root.resolve()
    showcase=set(json.loads((root/'Tools/showcase-files.json').read_text(encoding='utf-8')))
    manifest=json.loads((root/'SOURCE_SNAPSHOT.json').read_text(encoding='utf-8'))
    errors=[]
    names=set()
    for row in manifest['files']:
        rel=Path(row['path'])
        path=(root/rel).resolve()
        if rel.is_absolute() or '..' in rel.parts or not path.is_relative_to(root):
            errors.append('Unsafe manifest path');continue
        if row['path'] in names:
            errors.append(f'Duplicate path: {rel}')
        names.add(row['path'])
        if (rel.suffix.lower() in FORBIDDEN and rel.as_posix() not in showcase) or any(p in {'work','artifacts','bin','obj','.git','node_modules','__pycache__'} for p in rel.parts):
            errors.append(f'Forbidden source artifact: {rel}')
        if rel.parts[0]=='Docs' and rel.suffix.lower() in {'.png','.svg','.jpg','.webp','.gif'} and rel.as_posix() not in showcase:
            errors.append(f'Local reference/capture media: {rel}')
        if not path.is_file() or path.is_symlink():
            errors.append(f'Missing/nonregular file: {rel}');continue
        data=path.read_bytes()
        if len(data)!=row['bytes'] or hashlib.sha256(data).hexdigest()!=row['sha256']:
            errors.append(f'Manifest bytes changed: {rel}')
        if rel.suffix.lower() in {'.md','.json','.csv','.html','.txt','.yml','.yaml'}:
            text=data.decode('utf-8-sig')
            if re.search(r'C:(?:\\+|/)Users(?:\\+|/)[^\\/\s"<>]+',text,re.I):
                errors.append(f'Personal home path remains: {rel}')
            if re.search(r'-----BEGIN (?:RSA |OPENSSH |EC )?PRIVATE KEY-----|\bgh[pousr]_[A-Za-z0-9]{30,}|\bAKIA[A-Z0-9]{16}\b',text):
                errors.append(f'Credential pattern needs review: {rel}')
            if rel.suffix.lower() in {'.md','.html'}:
                links=re.findall(r'(?:href|src)="([^"]+)"',text)
                if rel.suffix.lower()=='.md':
                    links+=re.findall(r'!?\[[^\[\]\r\n]*\]\(([^)\r\n]+)\)',text)
                for url in links:
                    if re.match(r'^(?:https?:|mailto:|#|data:)',url):continue
                    target=unquote(url.split('#')[0])
                    if target and not (path.parent/target).exists():errors.append(f'Broken local link: {rel}: {url}')
                for url in re.findall(r'<img[^>]+src="([^"]+)"',text):
                    if url.endswith('.md'):errors.append(f'Image points to a Markdown notice: {rel}')
    for required in ['README.md','LICENSE','THIRD_PARTY_NOTICES.md','CONTRIBUTING.md','AGENTS.md','Build.ps1','Source/ReframedUI.csproj','Tools/Test.ps1','Tools/pure-tests.json','.github/workflows/pure-tests.yml','Docs/UI-Reworked/Audit32/ui-todo.csv']:
        if required not in names:errors.append(f'Missing essential file: {required}')
    if manifest['existing_history_copied'] or manifest['published']:
        errors.append('Source preparation manifest has unexpected publication/history flags')
    result={'schema':'source-snapshot-check-v1','manifest_files_checked':len(names),'errors':errors,
            'pass':not errors,'scope':'Manifest bytes, exclusions, credential-pattern heuristic and local links only; build/tests separately recorded.'}
    print(json.dumps(result,indent=2))
    raise SystemExit(1 if errors else 0)


if __name__=='__main__':
    main()
