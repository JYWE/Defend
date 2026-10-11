from pathlib import Path
import zipfile,json,sys
root=Path(__file__).resolve().parents[3]
archive=root/'Logs/PhaseFiveEffects/ExistingContentBaseline.zip'
if '--capture' in sys.argv:
    if archive.exists(): raise SystemExit('Refusing to replace the initial baseline')
    paths=[]
    for folder in ('Assets/Defend','Assets/Settings','ProjectSettings','Packages','ArtSource/PhaseFive'):
        for path in (root/folder).rglob('*'):
            if path.is_file() and 'PhaseFiveEffects' not in path.as_posix() and '/Effects/' not in path.as_posix(): paths.append(path)
    archive.parent.mkdir(parents=True,exist_ok=True)
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as target:
        for path in paths: target.writestr(path.relative_to(root).as_posix(),path.read_bytes())
    print(json.dumps({'captured_files':len(paths)}))
else:
    changed=[];missing=[]
    with zipfile.ZipFile(archive) as baseline:
        names=baseline.namelist()
        for name in names:
            path=root/name
            if not path.exists():missing.append(name)
            elif path.read_bytes()!=baseline.read(name):changed.append(name)
    result={'method':'direct byte comparison','protected_files':len(names),'changed':changed,'missing':missing,'passed':not changed and not missing}
    output=root/'Docs/Art/PhaseFiveEffects/Checks/ExistingContentPreservation.json'
    output.parent.mkdir(parents=True,exist_ok=True)
    output.write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(result))
