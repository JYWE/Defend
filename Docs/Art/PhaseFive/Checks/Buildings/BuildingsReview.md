# 第五阶段建筑模型制作与检查

日期：2026-10-10。制作工具：本地 Blender 5.2.2 LTS。此记录仅针对六种建筑的建模、Blender 渲染和导出验证；Unity 导入与游戏接入由主负责人另行记录。

## 交付目录

- 可编辑源文件：`ArtSource/PhaseFive/Buildings/`，共 6 个 `.blend`。
- Unity 导出网格：`Assets/Defend/Content/Art/PhaseFive/Models/Buildings/`，共 6 个 `.fbx`。
- 制作及活动枢轴检查脚本：`Tools/Art/PhaseFive/build_buildings.py`，使用同目录 `common.py`。
- 实际渲染：`Docs/Art/PhaseFive/Previews/`，每栋 `名称_GameView.png` 与 `名称_CloseView.png`，分别为约 70 度正交视图及 36 度近景，共 12 张。
- 单体检查：本目录的 `Reactor.json`、`Relay.json`、`RapidTurret.json`、`HeavyTurret.json`、`AreaTurret.json`、`Core.json`。
- 枢轴与占地检查：本目录 `BuildingMotionAndFootprint.json`。

## 造型与数量

尺寸为 Blender 世界坐标 X、Y、Z，即宽、深、高；单位为米。炮管朝前时计入整体深度。

| 文件名 | 对应建筑与轮廓 | 宽 × 深 × 高 | 三角面 | 网格数 | 材质槽总数 |
| --- | --- | --- | ---: | ---: | ---: |
| Reactor | 反应堆；竖向能源舱、双侧散热体 | 0.840 × 0.840 × 1.110 | 1600 | 1 | 4 |
| Relay | 中继器；细柱、中空能源环 | 0.720 × 0.720 × 1.165 | 820 | 1 | 4 |
| RapidTurret | 连射炮；紧凑转台、并列双炮管 | 0.840 × 0.927 × 0.618 | 988 | 3 | 9 |
| HeavyTurret | 重击炮；宽肩、单根粗管、双后坐轨 | 0.880 × 0.959 × 0.705 | 956 | 3 | 10 |
| AreaTurret | 范围炮；低矮六段外环、中央发射器 | 0.840 × 0.840 × 0.580 | 1124 | 3 | 10 |
| Core | 核心；方形装甲舱、四角护罩 | 0.860 × 0.860 × 0.796 | 1244 | 1 | 4 |

合计 6732 个三角面、12 个网格、41 个材质槽。每栋使用相同的四种命名材质：`PhaseFive_ArmorIvory`、`PhaseFive_StructureDark`、`PhaseFive_MetalSteel`、`PhaseFive_EnergyCyan`。网格数量不是绘制调用数量；材质槽与后续 Unity 渲染设置同样影响开销。

六种建筑静止结构均保持一格，底座边长为 0.72—0.88 米。连射炮与重击炮活动组件的全角度旋转最大水平半径分别为 0.5277 米、0.5301 米，即比半格边界最多伸出约 2.8 厘米、3.1 厘米；范围炮为 0.4050 米。核心与反应堆通过形状和高度区分，没有扩大占地。

## 分件与操作约定

每个 `.blend` 内均有与文件同名的根节点，底部中心在 `(0, 0, 0)`，缩放为 1。Blender 使用 Z 向上、-Y 向前。根节点下 `ForwardReference` 为 `(0, -0.5, 0)`，用于检查导出方向。展示灯光、相机和地面被明确命名为展示内容，并且不导出。

三个炮塔均保留以下层级：

1. 根节点下的 `名称_StaticStructure` 是固定底座。
2. `TurretYaw` 位于世界 `(0, 0, 0.30)`，绕 Blender 本地 Z 轴转向。
3. `BarrelRecoil` 是转台子节点；沿 Blender 本地 +Y 后坐，转换后对应 Unity 本地 -Z。
4. 连射炮的 `MuzzleLeft`、`MuzzleRight` 及重击炮的 `Muzzle` 位于实际炮口；范围炮 `Muzzle` 位于中央发射器上方。

连射炮两个炮管采用同一后坐组件。范围炮的 `BarrelRecoil` 是预留发射组件节点，本轮没有制作后坐或脉冲动画。对象仍由普通可编辑网格构成，可在 Blender 编辑模式下继续修改。

## 实际检查

制作过程中实际执行保存 `.blend`、重新打开该文件、渲染两种视角、导出 FBX、清场并重新导入 FBX。每个单体的检查 JSON 都记录了保存前、重开后和 FBX 再导入后的直接字段：尺寸、面数、网格数、材质名称、枢轴位置、法线及变换。

六栋模型的重开尺寸一致，FBX 再导入后的边界、三角面数、网格数和材质名称一致；边界误差小于 0.0001 米。所有网格缩放为 `(1, 1, 1)`，无负行列式变换、非有限法线、退化面、边界边或非流形边，各网格有正的有向体积。建筑底部最低点为 0。

已逐张查看全部 12 张真实 Blender 渲染。反应堆在高俯视角仍能看到能源舱前部和两侧散热片；中继器中空环及支撑可辨；连射炮和重击炮的双管、粗管与后坐轨清楚；范围炮分段外环与核心方舱在顶面轮廓上有明显区别。炮口使用有厚度的中空环，审查后把深色炮膛端面前移到炮管封口前方，保留可见凹深，重新保存、渲染与导出两种炮塔。

枢轴检查单独重开已保存源文件，在内存中让三种炮塔转台旋转 90 度，再让后坐节点平移 0.04 米；三种炮塔的炮口节点均正确跟随，转向位置误差低于 0.000001 米，后坐产生的世界位移为 `(0, 0.04, 0)`。六栋底部原点、单位缩放、前向参考、静止结构一格范围检查全部通过。检查程序不把测试姿态写回源文件。检查结果和全转角的最大水平半径记录于 `BuildingMotionAndFootprint.json`。

未在此子任务中操作 Unity Editor、替换第四阶段场景或 Prefab、改变玩法、添加动画代码、升级包、修改项目全局设置、提交或推送。没有执行哈希值校验。

## 重建与复查

在项目根目录运行：

```powershell
& 'D:/Blender/blender.exe' --factory-startup --background --python 'Tools/Art/PhaseFive/build_buildings.py'
& 'D:/Blender/blender.exe' --factory-startup --background --python 'Tools/Art/PhaseFive/build_buildings.py' -- --audit
```

可在 `--` 后指定完整资产名，只重建所需单体，例如 `RapidTurret HeavyTurret`。重新制作会覆盖这些明确命名的第五阶段建筑源文件、导出文件、预览和检查记录；手工继续修改前应另存副本或在脚本中同步变更。
