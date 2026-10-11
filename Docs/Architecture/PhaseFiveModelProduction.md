# 第五阶段模型制作约定

日期：2026-10-10。范围：已批准的工业科幻白盒首批模型制作。概念图仅作造型参考，不作为地图拓扑或尺寸依据。

## 美术指导与分工

主负责人统一轮廓、公共制作工具、材质、渲染审查、Unity 导入与交付。建筑硬表面、机械敌人、环境与技术美术三个专业子代理分别维护自己的制作脚本和资产目录；只有主负责人操作 Unity Editor。保留第四阶段可玩关卡、旧 Prefab 和所有已有工作。

## 比例和轮廓

- 1 Blender 单位 = 1 米 = 1 Unity 单位 = 1 地图格。所有根节点位于底面中心，缩放为 1。
- 六种建筑底座控制在 0.88 × 0.88 米以内，静态投影不超过 0.96 × 0.96 米，炮管朝前可小幅伸出。转动炮管的包络也须检查。
- 反应堆约高 1.10 米，竖向青色能源舱加两侧散热体；中继器约高 1.16 米，细柱和中空环；连射炮约高 0.65 米，并列双炮管；重击炮约高 0.72 米，粗管和后坐轨；范围炮约高 0.57 米，低矮分段环；核心约高 0.78 米，方形装甲和四角护罩。
- 普通敌人是约 0.76 米宽四足机械；快速敌人是约 0.50 米宽、0.80 米长的低矮楔形滑行体；重装敌人约 1.04 米宽、1.16 米长，履带和前撞角。敌人按最终世界尺寸制作，后续接入须处理旧 EnemyView 按种类再次缩放的行为。
- 环境首批包含两种低矮岩块、1 米工业地面块、带方向箭头的低矮入口标识。地面块可摆在地表以下让顶面齐平；装饰不定义碰撞或通行。

## 共享材质

使用公共脚本中的八个固定名称和参数：`ArmorIvory` 灰白外壳、`StructureDark` 深色底座、`MetalSteel` 机械金属、`EnergyCyan` 青色能源、`EnemyOrange` 敌方橙色、`RubberDark` 履带、`GroundConcrete` 地面、`RockSlate` 岩体。名称前缀统一为 `PhaseFive_`。不使用贴图依赖，不烘焙灯光。能源采用不透明发光表面，避免透明排序。白盒共享材质以可编辑参数交付，不承诺最终性能。

## 轴向、分件与命名

- Blender 为 Z 向上、-Y 向前；交付的 Unity 规范 Prefab 为 Y 向上、+Z 向前。FBX 导出以 -Z Forward / Y Up；实测原始 FBX 在当前 Unity 中朝 -Z，专用制作工具已将几何和枢轴转换成各节点零旋转、单位缩放的 +Z Prefab。原始 FBX 与规范 Prefab 的局部轴不同，后续以规范 Prefab 接入。
- 单体文件名使用完整英文名：Reactor、Relay、RapidTurret、HeavyTurret、AreaTurret、Core、StandardEnemy、FastEnemy、HeavyEnemy、RockLowA、RockLowB、IndustrialFloor、EntranceMarker。
- 单体根空物体与文件同名，原点 (0,0,0)。每个静止部分合并成一个网格，保留共享材质槽。根节点子级 `ForwardReference` 位于 Blender (0,-0.5,0)，用于核查导出朝向，不是可见网格。
- 炮塔使用 `TurretYaw` 旋转枢轴、`BarrelRecoil` 后坐枢轴，`MuzzleLeft` / `MuzzleRight` 或 `Muzzle` 炮口空物体；后坐沿本地 Blender +Y（Unity -Z）。
- 四足使用 `LegFrontLeftPivot` 等四个髋部枢轴，足部刚性分件。重装使用 `ImpactPivot`，撞角和前甲板为其子级。预留机械动作，不制作战斗代码或整套动画。
- 对象缩放应用到网格，保持正缩放，避免负缩放翻转法线。倒角使用 1 段，圆柱主要 8—16 边。目标建筑约 500—2000 三角面，敌人约 200—800 三角面；超过建议值须记录原因并精简无用细节。

## 制作文件和检查

- 可编辑源文件：`ArtSource/PhaseFive/<专业目录>/*.blend`。
- 导出：`Assets/Defend/Content/Art/PhaseFive/Models/<专业目录>/*.fbx`。
- 脚本：`Tools/Art/PhaseFive/`，各专业独占一个制作脚本。
- 实际 Blender 渲染：`Docs/Art/PhaseFive/Previews/`，每个单体必须有约 70 度正交视图和近景。
- 技术记录：`Docs/Art/PhaseFive/Checks/`，记录重开 .blend、重新导入 FBX、边界、原点、活动部件、材质、面数、法线与网格数量。
- Unity 仅制作独立资产检视 Prefab 和场景，使用共享 URP 材质，不替换第四阶段内容。分别报告 Blender 渲染、Unity 导入与实际游戏接入状态。
- 禁止哈希值校验。采用文件集合、直接字段、数量、尺寸和可视检查。
