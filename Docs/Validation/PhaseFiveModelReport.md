# 第五阶段首批模型交付与检查报告

日期：2026-10-10。工程：`E:/UnityProject/Defend`。本轮范围为已批准的工业科幻白盒模型制作，采用本地现有工作区。

## 交付结果

实际组织了三个专业子代理：建筑硬表面、机械敌人、环境与技术美术。主负责人统一比例、八种材质、轴向、公共工具、渲染审查、Unity 导入与最终整合；只有主负责人操作 Unity Editor。

已交付六种建筑（反应堆、中继器、连射炮、重击炮、范围炮、核心）、三种敌人（四足机械、快速滑行体、履带重装）、四个环境模块（两种低矮岩块、工业地面块、入口警示标识）。模型可继续在 Blender 编辑模式中手工修改。

| 交付项 | 实际数量与位置 |
| --- | --- |
| 单体 Blender 源文件 | 13 个，`ArtSource/PhaseFive/Buildings`、`Enemies`、`Environment` |
| 总览与相邻检查源文件 | `ArtSource/PhaseFive/PhaseFiveModelOverview.blend`，两个场景 |
| Blender 共享材质库 | `ArtSource/PhaseFive/Shared/PhaseFiveSharedMaterials.blend`，8 种材质，附 Palette.json |
| FBX 导出 | 13 个，`Assets/Defend/Content/Art/PhaseFive/Models` |
| Unity 规范轴向网格 | 24 个 `.asset`，第五阶段 `Meshes` 目录 |
| Unity 模型 Prefab | 13 个，第五阶段 `Prefabs` 目录 |
| Unity 共享材质 | 8 个 URP Lit `.mat`，第五阶段 `Materials` 目录 |
| 独立模型检视场景 | `Assets/Defend/Content/Art/PhaseFive/Scenes/PhaseFiveModelInspection.unity` |
| 真实模型预览 | 30 张：26 张单体、1 张整组、2 张相邻排列、1 张 Unity 离屏渲染 |
| 制作与检查脚本 | `Tools/Art/PhaseFive` |

全套单体合计 **9188 个三角面、24 个网格、76 个材质槽**。每栋建筑为 820—1600 个三角面，每个敌人为 324—624 个三角面。面数、网格和材质槽是不同指标，这些数量不代表帧时间或绘制调用已达标。特别是普通四足保留五个活动网格和十五个材质槽，后续游戏集成应实测其批量成本。

逐个模型的尺寸、预览、源文件、FBX 与 Prefab 链接见 [模型目录](../Art/PhaseFive/README.md)，机器可读记录见 [AssetCatalogue.json](../Art/PhaseFive/AssetCatalogue.json)。

## 实际 Blender 验证

核实程序为 `D:/Blender/blender.exe`，实际版本 **5.2.2 LTS**。所有正式制作命令使用 `--factory-startup --background`；未安装或升级 Blender，未修改用户偏好。首次普通后台启动遇到用户既有插件兼容性问题，改用临时默认配置后完成制作。

每个单体都实际执行以下步骤：创建可编辑网格 → 保存 `.blend` → 重新打开保存文件 → 渲染 70 度正交与 36 度近景 → 导出 FBX → 清场后重新导入 FBX → 比较直接字段。全部十三个单体均完成，未以脚本成功退出替代重开或导入检查。

检查内容包括世界尺寸、底面原点、正缩放、父子关系、枢轴、三角面、网格与材质数量、材质名称集合、闭合边、非流形边、退化面、法线有限性和有向体积。全部单体的直接布尔检查通过，FBX 往返边界误差小于 0.0001 米，面数、网格数量与材质名称一致。所有模型均无退化面、边界边或非流形边，法线有效、网格有正体积。

建筑另在保存后重开的源文件中实际测试三种炮塔的 90 度转向与 0.04 米后坐；敌人在重新导入的 FBX 中实际测试四个髋部枢轴转动 12 度、重装撞角前移 0.08 米。活动件响应、静止主体不动，恢复后位置一致。检查姿态没有写入源文件。

来源记录：

- [建筑制作检查](../Art/PhaseFive/Checks/Buildings/BuildingsReview.md)及 [活动枢轴与占地](../Art/PhaseFive/Checks/Buildings/BuildingMotionAndFootprint.json)。
- [机械敌人检查](../Art/PhaseFive/Checks/Enemies/EnemiesReview.md)。
- [环境检查](../Art/PhaseFive/Checks/Environment/EnvironmentReview.md)及 [技术美术复核](../Art/PhaseFive/Checks/Environment/TechnicalArtReview.md)。

## 实际渲染与修正

各专业逐张查看了二十六张单体实际渲染，主负责人进一步检查了关键近景、整组、一格相邻布局及 Unity 图像。完成的修正包括：

- 四足与重装的大面积顶甲改为深色金属，保持橙色识别片，减少与友方灰白建筑混淆。
- 重装履带外侧内收，修复轮盘与侧面共面闪烁；总宽仍为 1.04 米。
- 两种直射炮调整深色炮膛端面，近景可见凹入炮口。
- 环境岩块底面中心归零，入口标识删除完全被遮挡的螺栓几何。
- 整组展示降低背景亮度并加大标签，保持实际模型对比度。
- Unity 材质按 Blender 线性颜色转换到颜色属性空间，修正敌方橙色过红和暗部不易辨认的问题。

六种建筑底座为 0.72—0.88 米，仍全部占一格。连射炮和重击炮活动部分全转角最大水平半径为 0.5277 米、0.5301 米，仅炮管相对半格边界最多探出约 2.8 厘米、3.1 厘米。模型未改变任何建筑定义。

已实际渲染两套中心间距 1 米的六建筑排列：[常规排列](../Art/PhaseFive/Previews/AdjacentFootprints_GameView.png)与[高建筑前置](../Art/PhaseFive/Previews/AdjacentTallFront_GameView.png)。70 度正交视角下六种主轮廓、炮口、能源环与核心顶面仍可辨认；局部底座投影接近或遮叠。该结论只覆盖已检查排列与视角，不代替所有旋转布局、缩放级别或完整地图的可读性验收。

## Unity 导入、轴向与分件

使用现有 Unity CLI 与 **Unity 6000.6.5f1**，开始时 Editor ready、未播放、未编译；活动场景为第四阶段 `EnergyFrontDefense.unity`，无未保存更改。本轮没有进入播放模式。

原始 FBX 在当前 Unity 导入器中实测为单位比例正确，但前方朝 -Z，根节点带轴转换旋转。因此新增第五阶段专用制作工具，将导入几何和层级转换成 **+Z 朝前、+Y 向上、各节点旋转为零且缩放为 1** 的独立网格和 Prefab。原始 FBX 与 `.blend` 保留，Unity 生成网格通过脚本重建；不把二者不同的局部轴约定混用。

十三个规范 Prefab 均实际保存后重新加载，验证尺寸与原始导入一致、最低点贴地、前向参考位于 +Z、法线有效，所有材质引用指向八个共享 `.mat`。所有检查通过。FBX 导入保留 Read/Write 以供这阶段直接顶点检查；正式运行时优化可在集成后评估。

随后在 Unity 中对保存后重新加载的 Prefab 完成 **11 项实际活动部件检查**：三种炮塔各转向与后坐共六项、四个髋部枢轴四项、重装撞角一项。实际顶点位置与预期轴向变换误差小于 0.0001 米，静止部件不动、复位后恢复原位置。炮塔使用本地 Y 轴转向、后坐沿 -Z；四足髋部绕 X；撞角沿 +Z。没有添加动画控制器或战斗表现逻辑。

独立场景已保存并重新打开为临时预览场景，通过 URP SingleCameraRequest 完成真实离屏渲染并读取 PNG，已实际查看 [UnityModelInspection.png](../Art/PhaseFive/Previews/UnityModelInspection.png)。这是 Editor 内模型导入与渲染证据，不是可玩关卡或 Player 运行画面。场景保存和检查后恢复原活动场景，原场景仍无未保存更改。

详细记录：[UnityImport.json](../Art/PhaseFive/Checks/UnityImport.json)、[UnityMotionAndRender.json](../Art/PhaseFive/Checks/UnityMotionAndRender.json)、[Editor 最终状态](../Art/PhaseFive/Checks/UnityEditorFinalState.json)。检查结束时没有本轮新增的警告或错误；捕获缓冲仍含历史记录，未清空控制台，也未宣称解决旧日志中的问题。[当前检查范围控制台记录](../Art/PhaseFive/Checks/UnityConsoleSinceImport.json)。

## 现有工作保护

任务开始时保存了 378 个既有脚本、场景、Prefab、材质、定义、包与项目设置文件的直接字节基线。最终直接比较 **378/378 一致**，没有缺失文件。没有执行哈希值校验、reset、clean、提交、推送或旧长时间验收。

中途对照发现 `ProjectSettings/ProjectSettings.asset` 中原有 `preloadedAssets` 输入引用变为空列表。未把它当作允许的改动；已按起始记录恢复同一对象的 Editor 状态及该唯一序列化字段，随后重新确认所有受保护文件逐字节一致。未改变其他项目设置字段。记录见 [ExistingContentPreservation.json](../Art/PhaseFive/Checks/ExistingContentPreservation.json)。

本轮新增美术资源仅在清楚分离的第五阶段目录。第四阶段可玩关卡、旧 Prefab、原有未提交和未跟踪工作保留；不扩展波次、敌人逻辑、能源规则、UI 或整套特效。

## 后续游戏整合边界

当前交付为模型和独立检视 Prefab，尚未接入实际游戏：

- 现有 `EnemyView.Apply` 按类型再次缩放，并向单个渲染器覆盖黄/紫/红颜色。新敌人按最终尺寸制作，接入时必须处理该缩放、按材质槽设计受击反馈，并绑定血条、朝向和其他引用；不能直接替换旧整个敌人 Prefab。
- 建筑可通过既有 `VisualRoot` 作为美术子节点接入，同时保留定义、选取、状态和能源条引用。转向、后坐、步态与撞击本轮只检查机械分件与枢轴，没有完成运行时表现。
- 环境模块不包含导航或通行定义。工业地面以后应下沉到既有地表高度，既有 68 个岩体格、入口、通道与建造规则保持原配置。
- 未做实际关卡、全图缩放、游戏帧时间、100 建筑/200 敌人渲染或完整战斗验收；这些不能由单体 Blender 渲染、Unity 导入或面数通过替代。

所有本轮要求的模型资产、真实预览、可编辑源文件、导出和针对制作的技术检查已经交付。重建操作与逐个文件入口见模型目录。
