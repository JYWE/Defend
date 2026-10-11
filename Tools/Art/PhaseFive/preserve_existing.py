"""Direct byte comparisons for existing project content. No digests are used."""
from pathlib import Path
import json
import sys
import zipfile

root = Path(__file__).resolve().parents[3]
archive = root / 'Logs/PhaseFive/ExistingContentBaseline.zip'
record = root / 'Docs/Art/PhaseFive/Checks/ExistingContentPreservation.json'
if '--capture' in sys.argv:
    if archive.exists():
        raise SystemExit('Baseline already exists; refusing to replace the initial record.')
    paths = []
    for folder in ('Assets/Defend', 'Assets/Settings', 'ProjectSettings', 'Packages'):
        for path in (root / folder).rglob('*'):
            if not path.is_file() or 'PhaseFive' in path.as_posix():
                continue
            if path.suffix in ('.cs','.unity','.prefab','.asset','.mat','.asmdef','.asmref','.json','.meta'):
                paths.append(path)
    archive.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as output:
        for path in paths:
            output.writestr(path.relative_to(root).as_posix(), path.read_bytes())
    print(json.dumps({'captured_files':len(paths), 'archive':str(archive)}))
else:
    changed = []
    missing = []
    with zipfile.ZipFile(archive) as original:
        names = original.namelist()
        for name in names:
            path = root / name
            if not path.exists():
                missing.append(name)
            elif path.read_bytes() != original.read(name):
                changed.append(name)
    result = {'method':'direct byte comparison', 'protected_file_count':len(names),
              'changed':changed, 'missing':missing, 'passed':not changed and not missing}
    record.parent.mkdir(parents=True, exist_ok=True)
    record.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
    print(json.dumps(result, ensure_ascii=False))
