# 第五阶段动态特效交付与验证

日期：2026-10-10。交付范围是独立特效资源库及模型动态展示；未替换第四阶段关卡表现，未改变模拟、伤害、能源、寻路或经济逻辑。

## 交付结果

- 25 个特效 Prefab：8 个武器、6 个受击破坏、11 个能源与防护。
- 9 个新增模型扩展 Prefab，引用上一轮模型；新增能源端口、状态与受击挂点。
- 7 个共享效果材质、1 个 URP Shader。受击使用独立局部几何，不改模型原材质或多材质槽颜色。
- 资源库、带代次句柄的对象池、统一播放时钟、暂停/停止/复位和有界重复事件过滤。
- 独立九台动态展示，支持组合循环、单项循环和全景/近景。
- 五段实际 Unity 动态 GIF 与五张关键帧，见 [预览入口](../Art/PhaseFiveEffects/README.md)。资源目录及接入说明也在该文档。

## 实际完成的验证

环境：Unity 6000.6.5f1、项目现有 URP 17.6.0，Direct3D 11；Intel Core i9-14900HX、NVIDIA GeForce RTX 4060 Laptop GPU。

| 检查 | 结果与证据 |
| --- | --- |
| 编译与资源 | 最终编译成功，效果 Shader 无错误；25 个 Prefab 无缺失脚本/材质，全部粒子关闭自动播放。[AssetInspection.json](../Art/PhaseFiveEffects/Checks/AssetInspection.json) |
| 生命周期 | 实际 Play Mode 内 62 项检查全部通过；每种效果都检查开始、暂停、恢复、清空和有限寿命/循环寿命。暂停直接比较粒子位置、剩余寿命和效果年龄，停止检查包含失活子节点的粒子系统。[PlayModeLifecycle.json](../Art/PhaseFiveEffects/Checks/PlayModeLifecycle.json) |
| 叠播与回收 | 验证旧句柄不能停止复用实例、旧机械动作不能复位新请求、最新动作恢复姿态、池禁用恢复主体、销毁主体引用安全；包含受击专业的七项所有权检查。 |
| 语义 | 同一范围事件只保留一个冲击环；预留请求不能播放运输；射线端点不跟随移动目标；运输与到货独立。发送、运输、到货展示统一使用两端 EnergyPort。 |
| 原材质 | Play Mode 检查主体各 Renderer 的共享材质槽引用与 BaseColor 均保持；文件保护进一步直接比较原始字节。 |
| 预算 | 同时尝试超额请求，确认短效果 64、碎片 128、运输 128、状态 32 上限；停止后各预算归零。 |
| 场景重开 | 停止 Play Mode 后重新打开保存场景，运行时效果子节点为 0；最终回到原第四阶段场景，停止运行，运行时效果实例为 0。[SceneReopen.json](../Art/PhaseFiveEffects/Checks/SceneReopen.json)、[FinalEditorState.json](../Art/PhaseFiveEffects/Checks/FinalEditorState.json) |
| 控制台 | 首次代码尚未到齐时出现过缺失制作类的编译错误，随后修复；最终编译无错误，后续游标范围内没有新警告或错误。[FinalConsole.json](../Art/PhaseFiveEffects/Checks/FinalConsole.json) |

## 短时高密度采样

在独立展示的实际 Play Mode 中运行 600 帧，约 3.08 秒。先预热池，跳过开始后的 8 帧，每 30 帧同时请求 80 组范围冲击、20 组敌人破坏、144 个运输脉冲，共 20 轮。其间不编码图片，不进行旧战斗验收。由于超过预算，按设计丢弃额外视觉请求。

| 指标 | 实测 |
| --- | --- |
| 活跃效果根对象峰值 | 208（64 短效果 + 16 组破坏 + 128 运输） |
| 碎片峰值 | 128 个，包含于上述 16 组破坏 |
| 活跃粒子峰值 | 784 |
| 已创建池实例 | 266，包含先前生命周期检查与展示使用的各类缓存实例 |
| 本次采样复用次数 | 4160 |
| 预算退化次数 | 720，均为新视觉请求被跳过 |
| 帧间隔平均 / 95 分位 / 最大 | 5.139 / 7.110 / 14.828 毫秒 |
| 表现代码 CPU 平均 / 最大 | 0.714 / 6.963 毫秒，包含池推进和测量轮次的发射/回收 |
| 测量代码区间主线程托管分配 | 0 字节；不包含预热、最终报告序列化、Editor 或渲染系统 |
| 停止清理 | 活跃效果 0，活跃粒子 0 |

原始数据：[PlayModeLoad.json](../Art/PhaseFiveEffects/Checks/PlayModeLoad.json)。这组数据是当前电脑的短时 Editor 背景采样；没有据此认证可见窗口 60 帧、正式 Player、低端硬件或完整战斗压力。没有设置新的帧率上限或更改项目渲染设置。

## 画面证据与修正

五段视频源各含 96 张真实引擎帧，以固定 1/30 秒表现步长录制，编码后均为 3.2 秒循环。GIF 会合并完全重复的画面并保留时间，因此编码帧数可能少于 96。录制为 Play Mode 的 URP RenderRequest，不是 Blender 静态渲染或概念图；固定时间录制不用于性能判断。[PreviewEncoding.json](../Art/PhaseFiveEffects/Checks/PreviewEncoding.json)

主负责人和三个专业分别只读审图。最终重炮命中点调整到上甲板，显著减小短距离炮口方向折角；没有新增炮管俯仰。发送/运输/到货改用同一组端口快照。护盾命中为局部六边形与弧线，破裂为分段环和亮片；缺能断口环与断流斜杠在近景可分辨。碎片数量受控，无持续烟团遮挡主体；总览中的小碎片需结合近景观看。

## 工作区保护

开始前捕获 527 个既有文件的字节基线，包含旧 Assets、设置、包配置和上一轮 Blender 源文件。过程中 URP 自动同步了 8 个原材质的兼容 `_Color` 字段，并清理了能源材质的 `_EMISSION` 关键字；修正脚本仅允许恢复这两种已观察字段，而且修正后整文件必须与基线完全相等才写回。旧场景内存中也被引擎自动加上灯光附加组件，已保留临时副本检查差异，原场景文件未保存覆盖。

最终 527 个受保护文件全部直接字节一致，无缺失，没有哈希值校验。[ExistingContentPreservation.json](../Art/PhaseFiveEffects/Checks/ExistingContentPreservation.json)、[CompatibilityColorRestoration.json](../Art/PhaseFiveEffects/Checks/CompatibilityColorRestoration.json)

录制和采样临时启用运行时 `Application.runInBackground`，最终恢复 false；没有变更 ProjectSettings 或 Packages。没有提交、推送、发布或运行旧长时间验收。

## 交付边界

压缩包是上一轮模型包的增量资源，需项目中已存在 `Assets/Defend/Content/Art/PhaseFive` 和原字体。实际游戏接入仍需单独处理 EnemyView 的二次缩放、旧颜色覆盖、模型生命周期与状态挂点跟随。死亡/建筑破坏的自动恢复只用于可重复演示，不能直接代替真实死亡后的隐藏或回收。

当前没有音效、正式游戏适配器、独立 Player 构建或长期显存/内存证明。循环状态采用先到先显示，预算不足时丢弃新请求；接入后可根据玩法重要性制定显式视觉优先级。
