# 工业科幻美术接入战斗：验证报告

日期：2026-10-10。Unity 6000.6.5f1，Windows64，URP。没有升级包、修改全局渲染设置或调整玩法数值，没有执行哈希值校验。

## 交付

- 可播放场景：`Assets/Defend/Content/Scenes/Dev/IndustrialDefense.unity`。
- 六种建筑、三种敌人的战斗 Prefab：`Assets/Defend/Content/PhaseFiveCombat`。引用已交付模型与特效库，加入实际挂点、旋转、后坐、步态和对象回收。
- 模拟结算快照、逐步消费的战斗表现适配器、能源实际时长、暂停与重开清理。
- Windows 程序：`Builds/PhaseFiveCombat/Start-Defend.cmd`；分发包：`Builds/Defend-PhaseFiveCombat-Windows.zip`。
- 架构、触发条件与预算说明：`Docs/Architecture/PhaseFiveCombatIntegration.md`。

## 证据与结果

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 编译 | 完成，无编译错误 | Unity CLI `recompile_status` 返回 completed / failed=false |
| EditMode | 108 / 108 通过，7.56 秒；原 102 项加 6 项结算快照回归 | `Logs/PhaseFiveCombat/editmode-results.json` |
| 场景引用 | 六栋初始建筑、59 个 UI 对象；没有缺失脚本或材质 | `PhaseFiveCombatValidation.Inspect` |
| 三类敌人诊断波次，正常 Update | 87 次射击、15 次击杀、400 次实际到货；死亡记录 15，能源差 0 | `Logs/PhaseFiveCombat/editor-live-combat.json` |
| Editor 正常 Update 集成探针 | 13.060 秒通过：37 射击、6 死亡、108 到货、2 建筑受击、1 维修、1 护盾受击、1 护盾破裂；暂停和三次重开通过 | `Logs/PhaseFiveCombat/EditorProbe/report.json` |
| 真实重装攻击 | 正常播放观察到 24 次建筑受击；随后单步捕获真实 HeavyContact，三名重装的动作枢轴已绑定 | `heavy-attack-state.json`、`heavy-contact-check.json` |
| 满载数量与回收 | 100 建筑、200 存活敌人：普通 67、快速 67、重装 66；模型均单位缩放，重开清空敌人 | `Logs/PhaseFiveCombat/dense-check-final.json` |
| 满载特效预算 | 活跃根对象峰值 152；四类预算峰值分别为 32、0、120、20，未超过 64、128、128、32；特效池拒绝数 0，能源差 0 | 同上；能源端口装饰在容量达到 32 时主动省略 |
| Windows 最终构建 | 成功，17.637 秒，0 错误、1 警告 | `Logs/PhaseFiveCombat/build-status.json` |
| Windows 独立程序正常 Update | 13.515 秒诊断通过，退出码 0：38 射击、6 死亡、115 到货、2 建筑受击、1 维修、1 护盾受击、1 护盾破裂；暂停与三次重开通过 | `Logs/PhaseFiveCombat/PlayerProbe/report.json`、`exit-code.txt` |
| 旧资源与设置保护 | 基线 666 个文件中，14 个既有代码或程序集文件按接入需求修改，其余 652 个逐字节不变 | `Logs/PhaseFiveCombat/preservation.json` |

新增回归覆盖：维修与损毁只记录一次、护盾按伤害结算时刻记录、预约不伪装为到货、接收者在到达步移动、损毁后修复不能复活旧到货、范围攻击在死亡敌人移除前保留全部受击结果。

满载检查通过正式命令建立临时诊断世界，再使用会话单步驱动 Unity 视图与池；它是数量、绑定、预算与清理检查，不是持续正常帧率测试。常规 Editor 与 Player 探针则使用正常 Unity Update 推进。

最初满载检查发现能源端口闪光会挤占短效果容量，随后为战斗反馈预留一半容量。最终满载数据来自此调整后，旧 `dense-check.json` 保留作对照。

## 画面与边界

`Logs/PhaseFiveCombat/EditorProbe/combat.png` 为实际 Editor 游戏帧，含完整界面。`Logs/PhaseFiveCombat/combat-detail.png` 为实际战斗结算后暂停、采用临时近景摄像机和 URP 渲染请求生成的 1600 × 1000 图片；没有替换真实模型、粒子或命中结果。近景拍摄采用显式单步，不作为正常帧率证据。血条与能量条投影已关闭，最后近景使用修正后的 Prefab。

Player 以隐藏窗口运行，截图为黑图，因此仅认证独立程序的逻辑、生命周期与构建执行，不认证可见窗口画面或 60 帧。没有重新跑普通八波完整游玩、长期压力测试或第二阶段长验收。

最终构建的唯一警告是 Pipeline 插件缺少 Player 运行时配置，因此该开发工具连接在独立程序中关闭；这不是 URP 渲染管线缺失。首次完整构建另外出现两个 Unity 内部调试遮挡 Shader 裁剪警告，增量最终报告未再列出。Player 没有应用异常，但退出日志仍有引擎 `MemoryLeaks` 诊断（56,316 字节）；不能据此宣称没有任何资源泄漏。

Unity 自动改写过八份既有材质的兼容颜色或发光关键字，并启用了 UnityConnect 设置。确认差异仅属于这些自动字段后，已按工作前副本恢复；保留全部既有用户改动。旧关卡、玩法配置、交付模型与效果 Prefab 均未覆盖。

## 使用

Unity 已停在“工业防线”场景。直接播放，准备阶段建设并开始波次；滚轮缩放、中键拖动。独立试玩双击 `Start-Defend.cmd`，分发时保留整个文件夹。

常规启动不会启用诊断或自动建设。仅显式使用 `-phase-five-combat-smoke -phase-five-output <目录>` 时运行独立程序集成探针。
