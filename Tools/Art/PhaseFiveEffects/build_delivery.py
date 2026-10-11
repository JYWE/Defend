from pathlib import Path
import zipfile, json

root=Path(__file__).resolve().parents[3]
output=root/'Deliverables/PhaseFiveEffects/PhaseFiveIndustrialEffects.zip'
output.parent.mkdir(parents=True,exist_ok=True)
folders=('Assets/Defend/Effects','Assets/Defend/Content/Art/PhaseFiveEffects','Docs/Art/PhaseFiveEffects','Tools/Art/PhaseFiveEffects')
paths=[]
for folder in folders:
    paths.extend(p for p in (root/folder).rglob('*') if p.is_file() and '__pycache__' not in p.parts)
paths.extend(root/p for p in ('Assets/Defend/Effects.meta','Assets/Defend/Content/Art/PhaseFiveEffects.meta','Docs/Architecture/PhaseFiveEffectsProduction.md','Docs/Validation/PhaseFiveEffectsReport.md'))
paths=sorted(set(paths))
with zipfile.ZipFile(output,'w',zipfile.ZIP_DEFLATED) as archive:
    for path in paths:archive.writestr(path.relative_to(root).as_posix(),path.read_bytes())
with zipfile.ZipFile(output) as archive:
    matched=all(archive.read(path.relative_to(root).as_posix())==path.read_bytes() for path in paths)
    assert len(archive.namelist())==len(paths) and matched
result={'file':str(output.relative_to(root)),'files':len(paths),'bytes':output.stat().st_size,'archive_and_source_equal':matched,'verification':'direct byte comparison, no hashes','requires_previous_delivery':'PhaseFiveIndustrialModels.zip (existing model Prefabs, meshes, materials and font)'}
(output.parent/'PackageReport.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result,indent=2))
