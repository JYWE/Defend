"""打包第四阶段 Windows 程序；核对文件名和长度，不进行哈希值校验。"""
from pathlib import Path
import json
import zipfile

root = Path(__file__).resolve().parents[2]
source = root / 'Builds/PhaseFour'
destination = root / 'Builds/Defend-PhaseFour-Windows.zip'
if destination.exists():
    raise SystemExit('目标压缩包已存在，请明确保留或另存后再打包。')
files = sorted(path for path in source.rglob('*') if path.is_file()
               and not any('BackUpThisFolder_ButDontShipItWithYourGame' in part for part in path.parts))
required = ['Defend.exe', 'UnityPlayer.dll', 'Start-Defend.cmd', '使用说明.md', '验证报告.md']
for name in required:
    if not (source / name).is_file():
        raise SystemExit(f'缺少交付文件：{name}')
with zipfile.ZipFile(destination, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for path in files:
        archive.write(path, 'Defend-PhaseFour/' + path.relative_to(source).as_posix())
with zipfile.ZipFile(destination, 'r') as archive:
    entries = archive.infolist()
    expected = {'Defend-PhaseFour/' + path.relative_to(source).as_posix(): path.stat().st_size for path in files}
    actual = {entry.filename: entry.file_size for entry in entries}
    if actual != expected:
        raise SystemExit('压缩包内文件名或未压缩长度不一致。')
report = {'archive': str(destination), 'fileCount': len(files), 'archiveBytes': destination.stat().st_size,
          'uncompressedBytes': sum(expected.values()), 'namesAndLengthsEqual': True,
          'excluded': 'Unity 自动生成的调试备份目录，按目录名要求不随游戏分发'}
(root / 'Logs/PhaseFour/package.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(report, ensure_ascii=False, indent=2))
