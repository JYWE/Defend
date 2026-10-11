"""Build the asset catalogue and local delivery archive using direct checks."""
from pathlib import Path
import json
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[3]
DOCS = ROOT/'Docs/Art/PhaseFive'
ART = ROOT/'Assets/Defend/Content/Art/PhaseFive'
NAMES = ['Reactor','Relay','RapidTurret','HeavyTurret','AreaTurret','Core','StandardEnemy','FastEnemy','HeavyEnemy','RockLowA','RockLowB','IndustrialFloor','EntranceMarker']
LABELS = ['反应堆','中继器','连射炮','重击炮','范围炮','核心','普通四足机械','快速滑行体','履带重装','低矮岩块 A','低矮岩块 B','工业地面块','入口警示标识']

def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))

if '--package' in sys.argv:
    destination=ROOT/'Deliverables/PhaseFive/PhaseFiveIndustrialModels.zip'
    destination.parent.mkdir(parents=True,exist_ok=True)
    paths=[]
    for folder in ('ArtSource/PhaseFive','Assets/Defend/Content/Art/PhaseFive','Docs/Art/PhaseFive','Tools/Art/PhaseFive'):
        paths += [p for p in (ROOT/folder).rglob('*') if p.is_file() and '__pycache__' not in p.parts and p.suffix not in ('.blend1','.pyc')]
    paths += [ROOT/'Assets/Defend/Content/Art/PhaseFive.meta',
              ROOT/'Docs/Architecture/PhaseFiveModelProduction.md',
              ROOT/'Docs/Validation/PhaseFiveModelReport.md']
    paths = sorted(set(paths))
    with zipfile.ZipFile(destination,'w',zipfile.ZIP_DEFLATED) as archive:
        for path in paths: archive.writestr(path.relative_to(ROOT).as_posix(),path.read_bytes())
    with zipfile.ZipFile(destination) as archive:
        names=archive.namelist()
        assert names==[p.relative_to(ROOT).as_posix() for p in paths]
        assert all(archive.read(p.relative_to(ROOT).as_posix())==p.read_bytes() for p in paths)
    print(json.dumps({'archive':str(destination),'files':len(paths),'bytes':destination.stat().st_size,'direct_content_comparison':True}))
    raise SystemExit(0)

unity=read(DOCS/'Checks/UnityImport.json')
motions=read(DOCS/'Checks/UnityMotionAndRender.json')
preservation=read(DOCS/'Checks/ExistingContentPreservation.json')
assert preservation['passed']
assert unity['assetCount']==13 and unity['sharedMaterialCount']==8
assert len(motions['motions'])==11 and all(m['passed'] for m in motions['motions'])
assets=[]
rows=[]
for index,(name,label) in enumerate(zip(NAMES,LABELS)):
    category='Buildings' if index<6 else 'Enemies' if index<9 else 'Environment'
    check=read(DOCS/f'Checks/{category}/{name}.json')
    assert all(value for value in check['checks'].values() if isinstance(value,bool)),name
    source=ROOT/f'ArtSource/PhaseFive/{category}/{name}.blend'
    export=ART/f'Models/{category}/{name}.fbx'
    prefab=ART/f'Prefabs/{name}.prefab'
    views=[DOCS/f'Previews/{name}_{suffix}.png' for suffix in ('GameView','CloseView')]
    assert all(p.exists() and p.stat().st_size>0 for p in [source,export,prefab,*views]),name
    imported=next(record for record in unity['assets'] if record['name']==name)
    stats=check['source_reopened']
    assert stats['triangles']==imported['triangles'] and stats['mesh_count']==imported['renderers'],name
    dimensions=' × '.join(f'{v:.3f}' for v in stats['dimensions'])
    assets.append({'name':name,'label':label,'category':category,'dimensions_width_depth_height':stats['dimensions'],
                   'triangles':stats['triangles'],'renderers':stats['mesh_count'],'material_slots':stats['material_slot_count'],
                   'source':source.relative_to(ROOT).as_posix(),'fbx':export.relative_to(ROOT).as_posix(),'prefab':prefab.relative_to(ROOT).as_posix()})
    rows.append(f'| {label} | {name} | {dimensions} | {stats["triangles"]} | {stats["mesh_count"]} / {stats["material_slot_count"]} | [70°](Previews/{name}_GameView.png) · [近景](Previews/{name}_CloseView.png) | [源文件](../../../ArtSource/PhaseFive/{category}/{name}.blend) · [FBX](../../../Assets/Defend/Content/Art/PhaseFive/Models/{category}/{name}.fbx) · [Prefab](../../../Assets/Defend/Content/Art/PhaseFive/Prefabs/{name}.prefab) |')
manifest={'date':'2026-10-10','asset_count':len(assets),'triangles':sum(a['triangles'] for a in assets),
          'renderers':sum(a['renderers'] for a in assets),'material_slots':sum(a['material_slots'] for a in assets),
          'shared_material_count':8,'single_asset_blend_count':13,'fbx_count':13,'prefab_count':13,
          'normalized_unity_mesh_count':len(list((ART/'Meshes').glob('*.asset'))),
          'preview_png_count':len(list((DOCS/'Previews').glob('*.png'))),
          'unity_motion_checks':len(motions['motions']),'protected_files_unchanged':preservation['protected_file_count'],
          'all_direct_checks_passed':True,'assets':assets}
(DOCS/'AssetCatalogue.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False),encoding='utf-8')
intro='''# 第五阶段工业科幻白盒模型目录

2026-10-10。13 个实际模型：6 种一格建筑、3 类机械敌人、4 个环境模块。可编辑 Blender 源文件、FBX、Unity 规范轴向 Prefab 和共享材质均已保存。

![实际 Blender 模型一览](Previews/ModelOverview.png)

上图由交付网格在 Blender 5.2.2 LTS 中实际渲染，采用约 70 度正交视角。单体另有 36 度近景。不是概念生成图，也不是游戏运行画面。

## 单体资产

尺寸依次为宽 × 深 × 高，单位为米。网格数量与材质槽数量分别列出，不能据此推断实际绘制调用或帧时间。

| 模型 | 文件名 | 尺寸 | 三角面 | 网格 / 材质槽 | 实际预览 | 资产 |
| --- | --- | --- | ---: | ---: | --- | --- |
'''
tail='''

整套合计 **9188 个三角面、24 个网格、76 个材质槽、8 种共享材质**。

## 使用入口

- Blender 总览源文件：[PhaseFiveModelOverview.blend](../../../ArtSource/PhaseFive/PhaseFiveModelOverview.blend)。含完整模型一览和高建筑前置的一格相邻检视场景。
- Blender 材质库：[PhaseFiveSharedMaterials.blend](../../../ArtSource/PhaseFive/Shared/PhaseFiveSharedMaterials.blend)，参数见 [Palette.json](../../../ArtSource/PhaseFive/Shared/Palette.json)。
- Unity 独立场景：[PhaseFiveModelInspection.unity](../../../Assets/Defend/Content/Art/PhaseFive/Scenes/PhaseFiveModelInspection.unity)。通过 Project 面板打开。该场景没有玩法组件，不替换第四阶段关卡。
- Unity 材质：[Materials 目录](../../../Assets/Defend/Content/Art/PhaseFive/Materials)。八个 URP Lit 材质由所有模型共享，不依赖外部贴图。
- [Unity 编辑器实际离屏渲染](Previews/UnityModelInspection.png)、[常规相邻布局](Previews/AdjacentFootprints_GameView.png)、[高建筑位于前排](Previews/AdjacentTallFront_GameView.png)。
- [设计与制作约定](../../Architecture/PhaseFiveModelProduction.md)、[总检查报告](../../Validation/PhaseFiveModelReport.md)、[技术美术审查](Checks/Environment/TechnicalArtReview.md)。

优先将 `Prefabs` 目录的模型作为美术子节点使用：根节点与各级枢轴旋转为零、缩放为 1，Unity +Z 朝前、+Y 向上；根原点位于底面中心。炮塔绕 `TurretYaw` 的本地 Y 轴转向，`BarrelRecoil` 沿本地 -Z 后坐；四足髋枢轴绕本地 X 轴作简易步态，重装 `ImpactPivot` 沿本地 +Z 撞击。

原始 Blender 源文件为 Z 向上、-Y 向前。原始 FBX 在当前 Unity 导入器下前向为 -Z，根节点带轴转换旋转。制作工具已生成独立的 Unity 轴向网格与 Prefab，不能把原始 FBX 的局部轴约定套用到规范 Prefab。修改源网格后须重导出并重新执行 Unity 制作工具，生成的 `.asset` 网格不是另一份手工母版。

`EnemyView` 现有种类缩放和整渲染器颜色覆盖尚未改动。新敌人已按最终世界尺寸制作；游戏接入时须处理该旧缩放、颜色反馈以及血条/表现引用。没有接入战斗代码、完整步态、粒子、UI 或实际关卡。

工业地面块底部原点为 0，主要通行表面高 0.078 米，检修盖最高 0.086 米。以后铺设到通行平面时可整体下沉 0.078 米；岩块和标识不含碰撞，不改变既有通道和占地。

## 重新制作

在工程根目录运行以下命令。命令只适用于这些明确命名的第五阶段生成资产；手工编辑源文件后先另存副本，避免被生成脚本覆盖。

```powershell
& 'D:/Blender/blender.exe' --factory-startup --background --python 'Tools/Art/PhaseFive/build_buildings.py'
& 'D:/Blender/blender.exe' --factory-startup --background --python 'Tools/Art/PhaseFive/build_enemies.py'
& 'D:/Blender/blender.exe' --factory-startup --background --python 'Tools/Art/PhaseFive/build_environment.py'
& 'D:/Blender/blender.exe' --factory-startup --background --python 'Tools/Art/PhaseFive/build_overview.py'
& 'D:/Blender/blender.exe' --factory-startup --background --python 'Tools/Art/PhaseFive/render_adjacency.py'
& 'C:/Users/legion/AppData/Local/Unity/bin/unity.exe' command run_script --file 'Tools/Art/PhaseFive/PhaseFiveAssetBuilder.cs' --entry PhaseFiveAssetBuilder.ImportAndBuild --timeout_ms 120000 --timeout 130
& 'C:/Users/legion/AppData/Local/Unity/bin/unity.exe' command run_script --file 'Tools/Art/PhaseFive/PhaseFiveUnityReview.cs' --entry PhaseFiveUnityReview.Review
python 'Tools/Art/PhaseFive/build_delivery.py'
```

后台 Blender 使用临时默认配置，避免用户已安装插件干扰，不改变用户偏好。Unity 制作工具要求当前 Editor 停止播放，仅创建第五阶段独立美术资源；检查场景完成后恢复原活动场景。检查中没有执行哈希值校验。
'''
(DOCS/'README.md').write_text(intro+'\n'.join(rows)+tail,encoding='utf-8')
print(json.dumps({k:v for k,v in manifest.items() if k!='assets'},ensure_ascii=False))
