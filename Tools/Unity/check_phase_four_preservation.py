"""逐字节比较本轮受保护文件和工作前副本，不计算哈希值。"""
from pathlib import Path
import json

root = Path(__file__).resolve().parents[2]
baseline = root / "Logs/PhaseFour/Baseline"
checked = []
changed = []
missing = []
for group in ("Assets", "Packages", "ProjectSettings"):
    for source in sorted((baseline / group).rglob("*")):
        if not source.is_file():
            continue
        relative = source.relative_to(baseline)
        target = root / relative
        checked.append(relative.as_posix())
        if not target.is_file():
            missing.append(relative.as_posix())
        elif source.read_bytes() != target.read_bytes():
            changed.append(relative.as_posix())
report = {"method": "direct-byte-comparison", "checkedCount": len(checked),
          "changed": changed, "missing": missing, "passed": not changed and not missing,
          "scope": "工作前复制的项目设置、包清单、模板设置、既有关卡、定义和预制体；新增第四阶段资产允许存在"}
(root / "Logs/PhaseFour/preservation.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False, indent=2))
raise SystemExit(0 if report["passed"] else 1)
