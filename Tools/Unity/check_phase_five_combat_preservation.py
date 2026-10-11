"""仅使用直接字节比较，分开报告本次接入修改与受保护文件。"""
from pathlib import Path
import json
root=Path(__file__).resolve().parents[2]
allowed={
"Assets/Defend/Scripts/Editor/Defend.Editor.asmdef",
"Assets/Defend/Scripts/Unity/Defend.Unity.asmdef",
"Assets/Defend/Scripts/Unity/Bootstrap/GameSessionRoot.cs",
"Assets/Defend/Scripts/Unity/Presentation/BattlePresentation.cs",
"Assets/Defend/Scripts/Unity/Presentation/BuildingView.cs",
"Assets/Defend/Scripts/Unity/Presentation/EnemyView.cs",
"Assets/Defend/Scripts/Unity/Presentation/WorldPresentation.cs",
"Assets/Defend/Scripts/Simulation/Combat/BattleSystem.cs",
"Assets/Defend/Scripts/Simulation/Energy/EnergySystem.cs",
"Assets/Defend/Scripts/Simulation/Energy/EnergyTransport.cs",
"Assets/Defend/Scripts/Simulation/World/GameWorld.cs",
"Assets/Defend/Effects/Runtime/Shared/EffectPool.cs",
"Assets/Defend/Effects/Runtime/Shared/PhaseFiveEffect.cs",
"Assets/Defend/Effects/Runtime/Weapons/WeaponImpactEffect.cs"}
files=json.loads((root/"Logs/PhaseFiveCombat/baseline-files.json").read_text(encoding="utf-8"))
changed=[]; missing=[]; intended=[]; unchanged=0
for path in files:
 current=root/path; previous=root/"Logs/PhaseFiveCombat/Baseline"/path
 if not current.exists(): missing.append(path)
 elif current.read_bytes()!=previous.read_bytes():
  (intended if path in allowed else changed).append(path)
 else: unchanged+=1
report={"method":"direct byte comparison","baselineCount":len(files),"protectedUnchanged":unchanged,"intendedChanges":intended,"unexpectedChanges":changed,"missing":missing,"passed":not changed and not missing}
(root/"Logs/PhaseFiveCombat/preservation.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
print(json.dumps(report,ensure_ascii=False,indent=2))
raise SystemExit(0 if report["passed"] else 1)
