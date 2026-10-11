# 第五阶段工业科幻白盒模型目录

2026-10-10。13 个实际模型：6 种一格建筑、3 类机械敌人、4 个环境模块。可编辑 Blender 源文件、FBX、Unity 规范轴向 Prefab 和共享材质均已保存。

![实际 Blender 模型一览](Previews/ModelOverview.png)

上图由交付网格在 Blender 5.2.2 LTS 中实际渲染，采用约 70 度正交视角。单体另有 36 度近景。不是概念生成图，也不是游戏运行画面。

## 单体资产

尺寸依次为宽 × 深 × 高，单位为米。网格数量与材质槽数量分别列出，不能据此推断实际绘制调用或帧时间。

| 模型 | 文件名 | 尺寸 | 三角面 | 网格 / 材质槽 | 实际预览 | 资产 |
| --- | --- | --- | ---: | ---: | --- | --- |
| 反应堆 | Reactor | 0.840 × 0.840 × 1.110 | 1600 | 1 / 4 | [70°](Previews/Reactor_GameView.png) · [近景](Previews/Reactor_CloseView.png) | [源文件](../../../ArtSource/PhaseFive/Buildings/Reactor.blend) · [FBX](../../../Assets/Defend/Content/Art/PhaseFive/Models/Buildings/Reactor.fbx) · [Prefab](../../../Assets/Defend/Content/Art/PhaseFive/Prefabs/Reactor.prefab) |
| 中继器 | Relay | 0.720 × 0.720 × 1.165 | 820 | 1 / 4 | [70°](Previews/Relay_GameView.png) · [近景](Previews/Relay_CloseView.png) | [源文件](../../../ArtSource/PhaseFive/Buildings/Relay.blend) · [FBX](../../../Assets/Defend/Content/Art/PhaseFive/Models/Buildings/Relay.fbx) · [Prefab](../../../Assets/Defend/Content/Art/PhaseFive/Prefabs/Relay.prefab) |
| 连射炮 | RapidTurret | 0.840 × 0.926 × 0.618 | 988 | 3 / 9 | [70°](Previews/RapidTurret_GameView.png) · [近景](Previews/RapidTurret_CloseView.png) | [源文件](../../../ArtSource/PhaseFive/Buildings/RapidTurret.blend) · [FBX](../../../Assets/Defend/Content/Art/PhaseFive/Models/Buildings/RapidTurret.fbx) · [Prefab](../../../Assets/Defend/Content/Art/PhaseFive/Prefabs/RapidTurret.prefab) |
| 重击炮 | HeavyTurret | 0.880 × 0.959 × 0.705 | 956 | 3 / 10 | [70°](Previews/HeavyTurret_GameView.png) · [近景](Previews/HeavyTurret_CloseView.png) | [源文件](../../../ArtSource/PhaseFive/Buildings/HeavyTurret.blend) · [FBX](../../../Assets/Defend/Content/Art/PhaseFive/Models/Buildings/HeavyTurret.fbx) · [Prefab](../../../Assets/Defend/Content/Art/PhaseFive/Prefabs/HeavyTurret.prefab) |
| 范围炮 | AreaTurret | 0.840 × 0.840 × 0.580 | 1124 | 3 / 10 | [70°](Previews/AreaTurret_GameView.png) · [近景](Previews/AreaTurret_CloseView.png) | [源文件](../../../ArtSource/PhaseFive/Buildings/AreaTurret.blend) · [FBX](../../../Assets/Defend/Content/Art/PhaseFive/Models/Buildings/AreaTurret.fbx) · [Prefab](../../../Assets/Defend/Content/Art/PhaseFive/Prefabs/AreaTurret.prefab) |
| 核心 | Core | 0.860 × 0.860 × 0.796 | 1244 | 1 / 4 | [70°](Previews/Core_GameView.png) · [近景](Previews/Core_CloseView.png) | [源文件](../../../ArtSource/PhaseFive/Buildings/Core.blend) · [FBX](../../../Assets/Defend/Content/Art/PhaseFive/Models/Buildings/Core.fbx) · [Prefab](../../../Assets/Defend/Content/Art/PhaseFive/Prefabs/Core.prefab) |
| 普通四足机械 | StandardEnemy | 0.760 × 0.760 × 0.480 | 600 | 5 / 15 | [70°](Previews/StandardEnemy_GameView.png) · [近景](Previews/StandardEnemy_CloseView.png) | [源文件](../../../ArtSource/PhaseFive/Enemies/StandardEnemy.blend) · [FBX](../../../Assets/Defend/Content/Art/PhaseFive/Models/Enemies/StandardEnemy.fbx) · [Prefab](../../../Assets/Defend/Content/Art/PhaseFive/Prefabs/StandardEnemy.prefab) |
| 快速滑行体 | FastEnemy | 0.500 × 0.800 × 0.254 | 324 | 1 / 4 | [70°](Previews/FastEnemy_GameView.png) · [近景](Previews/FastEnemy_CloseView.png) | [源文件](../../../ArtSource/PhaseFive/Enemies/FastEnemy.blend) · [FBX](../../../Assets/Defend/Content/Art/PhaseFive/Models/Enemies/FastEnemy.fbx) · [Prefab](../../../Assets/Defend/Content/Art/PhaseFive/Prefabs/FastEnemy.prefab) |
| 履带重装 | HeavyEnemy | 1.040 × 1.160 × 0.488 | 624 | 2 / 7 | [70°](Previews/HeavyEnemy_GameView.png) · [近景](Previews/HeavyEnemy_CloseView.png) | [源文件](../../../ArtSource/PhaseFive/Enemies/HeavyEnemy.blend) · [FBX](../../../Assets/Defend/Content/Art/PhaseFive/Models/Enemies/HeavyEnemy.fbx) · [Prefab](../../../Assets/Defend/Content/Art/PhaseFive/Prefabs/HeavyEnemy.prefab) |
| 低矮岩块 A | RockLowA | 0.870 × 0.680 × 0.420 | 46 | 1 / 1 | [70°](Previews/RockLowA_GameView.png) · [近景](Previews/RockLowA_CloseView.png) | [源文件](../../../ArtSource/PhaseFive/Environment/RockLowA.blend) · [FBX](../../../Assets/Defend/Content/Art/PhaseFive/Models/Environment/RockLowA.fbx) · [Prefab](../../../Assets/Defend/Content/Art/PhaseFive/Prefabs/RockLowA.prefab) |
| 低矮岩块 B | RockLowB | 0.920 × 0.590 × 0.280 | 46 | 1 / 1 | [70°](Previews/RockLowB_GameView.png) · [近景](Previews/RockLowB_CloseView.png) | [源文件](../../../ArtSource/PhaseFive/Environment/RockLowB.blend) · [FBX](../../../Assets/Defend/Content/Art/PhaseFive/Models/Environment/RockLowB.fbx) · [Prefab](../../../Assets/Defend/Content/Art/PhaseFive/Prefabs/RockLowB.prefab) |
| 工业地面块 | IndustrialFloor | 1.000 × 1.000 × 0.086 | 324 | 1 / 3 | [70°](Previews/IndustrialFloor_GameView.png) · [近景](Previews/IndustrialFloor_CloseView.png) | [源文件](../../../ArtSource/PhaseFive/Environment/IndustrialFloor.blend) · [FBX](../../../Assets/Defend/Content/Art/PhaseFive/Models/Environment/IndustrialFloor.fbx) · [Prefab](../../../Assets/Defend/Content/Art/PhaseFive/Prefabs/IndustrialFloor.prefab) |
| 入口警示标识 | EntranceMarker | 0.840 × 0.600 × 0.088 | 492 | 1 / 4 | [70°](Previews/EntranceMarker_GameView.png) · [近景](Previews/EntranceMarker_CloseView.png) | [源文件](../../../ArtSource/PhaseFive/Environment/EntranceMarker.blend) · [FBX](../../../Assets/Defend/Content/Art/PhaseFive/Models/Environment/EntranceMarker.fbx) · [Prefab](../../../Assets/Defend/Content/Art/PhaseFive/Prefabs/EntranceMarker.prefab) |

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
