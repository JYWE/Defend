"""Restore the observed URP compatibility color and emission-keyword serialization only."""
from pathlib import Path
import zipfile,re,json

root=Path(__file__).resolve().parents[3]
restored=[]; verified=[]
with zipfile.ZipFile(root/'Logs/PhaseFiveEffects/ExistingContentBaseline.zip') as archive:
    for name in archive.namelist():
        if not (name.startswith('Assets/Defend/Content/Art/PhaseFive/Materials/') and name.endswith('.mat')):continue
        path=root/name; baseline=archive.read(name); current=path.read_bytes()
        verified.append(name)
        if current==baseline:continue
        original_line=re.search(rb'^    - _Color: .+$',baseline,re.M)
        current_line=re.search(rb'^    - _Color: .+$',current,re.M)
        if not original_line or not current_line:raise RuntimeError(name)
        repaired=current.replace(current_line.group(),original_line.group(),1)
        if name.endswith('PhaseFive_EnergyCyan.mat'):
            repaired=repaired.replace(b'  m_ValidKeywords: []\n',b'  m_ValidKeywords:\n  - _EMISSION\n',1)
        if repaired!=baseline:raise RuntimeError(f'Refusing unrelated changes in {name}')
        path.write_bytes(repaired);restored.append(name)
result={'verified_materials':verified,'restored_this_pass':restored,'allowed_observed_fields':['compatibility _Color','EnergyCyan _EMISSION keyword'],'guard':'full direct byte equality with captured baseline after the narrow corrections'}
(root/'Docs/Art/PhaseFiveEffects/Checks/CompatibilityColorRestoration.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result))
