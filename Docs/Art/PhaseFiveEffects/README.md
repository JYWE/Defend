# 第五阶段动态特效资源

本包提供 25 个可编辑特效 Prefab、9 个模型扩展 Prefab、7 个共享效果材质、1 个 URP Shader，以及独立动态展示场景。模型扩展引用上一轮模型，保留原模型文件。效果全部由 Unity 几何、Line Renderer 和 Particle System 实现，没有烘焙成不可编辑视频。

## 先看效果

- [70 度正交全景](Previews/Overview.gif)
- [重炮近景](Previews/Close.gif)
- [敌人与建筑破坏近景](Previews/Damage.gif)
- [能源发送、运输、到货与状态近景](Previews/Energy.gif)
- [护盾命中与破裂近景](Previews/Shield.gif)

预览来自实际 Unity Play Mode 中的 URP 相机渲染，按每步 1/30 秒推进表现时钟，每段 3.2 秒；GIF 仅作尺寸缩放与格式编码。它们证明动态表现，不是可见窗口帧率证据。PNG 关键帧与 GIF 位于同一目录。

## 打开展示

打开 `Assets/Defend/Content/Art/PhaseFiveEffects/Scenes/PhaseFiveEffectsShowcase.unity` 后进入 Play Mode。场景为九个演示台，引用上一轮真实模型。

| 操作 | 行为 |
| --- | --- |
| 空格 | 同时暂停或恢复时间、粒子、机械动作 |
| R | 清空效果，重新开始 |
| S | 停止所有效果并恢复模型姿态 |
| Tab | 按资源库顺序循环单项演示；当前资源名见 EffectShowcase 的 SingleEffect 字段 |
| Enter | 返回组合循环 |
| 1 / 2 | 70 度全景 / 重炮近景 |

也可以在 Inspector 设置 `SingleEffect` 为下表中的名称，再按 R 或调用公开的 `Restart()` 方法重播。`EffectPool.TryPlay`、`Stop`、`StopAll` 和 `Paused` 可供后续表现适配器调用。

## 资源目录

| 分类 | 名称 |
| --- | --- |
| 武器 | RapidMuzzle、HeavyMuzzle、AreaDischarge、RapidTracer、HeavyTracer、RapidImpact、HeavyImpact、AreaImpact |
| 受击破坏 | EnemyHit、EnemyDeath、HeavyContact、BuildingHit、BuildingDestruction、RepairComplete |
| 能源与防护 | ReactorRunning、RelayRunning、PortSend、EnergyTransfer、EnergyArrival、EnergyShortage、EnergyDisconnected、HeavyLock、SearchMarker、ShieldHit、ShieldBreak |

Prefab 路径为 `Assets/Defend/Content/Art/PhaseFiveEffects/Prefabs/{Weapons,Damage,Energy}`；模型扩展在 `Prefabs/Models`。材质在 `Materials`，Shader 在 `Shaders/IndustrialEffect.shader`，资源库为 `EffectLibrary.asset`。

运行时代码位于 `Assets/Defend/Effects/Runtime`；编辑器制作代码位于 `Assets/Defend/Effects/Editor`。均为新增独立程序集，无模拟程序集依赖。

## 接入约定

效果是已经确认的游戏结果的表现，不产生伤害、碰撞、寻路或能源记账。真实战斗适配器尚未接入本库。

- 射击请求同时播放炮口、瞬时射线与命中；射线没有飞行等待。`Origin`、`Destination` 是世界坐标快照。范围冲击只显示一圈，不作周期伤害。
- `EventSequence` 非零时按“效果名称 + 事件序号”去重；保留最近 512 个有效请求。调用方应提供单调或全局唯一事件序号。零表示不启用去重。`StopAll()` 默认清除历史，新一轮模拟可重新使用序号。
- `EnergyTransfer` 必须设置 `ConfirmedTransfer=true` 且两个端点不同。预留或请求不代表真实运输。`EnergyArrival` 由确认到货事件单独触发，运输动画结束不会自动播放到货。
- `Subject` 只供临时机械姿态演示，不代表伤害对象。死亡/建筑破坏演示会恢复该 Transform，实际死亡后的模型隐藏、销毁或回收应由未来适配器负责，并先停止关联效果句柄。
- 所有效果定位默认是触发快照。持续状态需要拥有者持有句柄，在状态解除、实体禁用或回收时停止；移动实体需由表现适配器管理状态挂点更新。
- 原模型 `TurretYaw` 绕 Y 转向，`BarrelRecoil` 沿本地 -Z 后坐；新增 `EnergyPort`、`StatusAnchor`、`HitAnchor` 位于扩展 Prefab。展示重炮朝上甲板开火；本库没有新增炮管俯仰机构。
- 不改目标材质或 MaterialPropertyBlock；受击使用独立局部几何。所有 Line Renderer 修改自身顶点颜色，粒子使用颜色模块，共享材质不在播放时修改。

示例（调用者已经收到权威射击结果）：

```csharp
var request = new EffectRequest {
    Origin = muzzlePosition,
    Destination = confirmedHitPosition,
    Rotation = muzzleRotation,
    Subject = modelRoot,
    Intensity = 1,
    Seed = eventSeed,
    EventSequence = eventSequence
};
pool.TryPlay("RapidMuzzle", request);
pool.TryPlay("RapidTracer", request);
pool.TryPlay("RapidImpact", request);
```

## 预算与退化

短效果总计最多 64 组；碎片最多 128 个（每次破坏固定 8 个，最多 16 组）；能源运输最多 128 个；循环状态最多 32 组。每个 Prefab 的实例上限为 128，整体受上述分类预算约束。池实例按需创建后复用，短时负载测量先预热再计时。

采用先到先显示，不抢占已有状态；预算不足或单项实例上限达到后丢弃新视觉请求，`Dropped` 递增。无模拟回调，因此退化不会改伤害或能源结果。屏幕空间过绘制和实际场景中的视觉优先级仍需接入后按目标硬件测量。

## 重建与复核

Unity 菜单 `Defend / Art / Build Phase Five Effects` 重新生成本轮资源和展示场景。先关闭本轮展示场景，避免覆盖正在编辑的展示内容；不保存或重建旧关卡。制作脚本使用临时附加场景隔离生成对象。

主负责人验证工具：`Tools/Art/PhaseFiveEffects/EffectsReview.cs`。`Build` 检查资源；`Open` 打开展示；进入 Play Mode 后 `PlayChecks` 检查生命周期，`Measure` 启动短时负载，`CaptureOverview`、`CaptureClose`、`CaptureDamage`、`CaptureEnergy`、`CaptureShield` 录制。`Reopen` 在退出 Play Mode 后确认无运行时残留。不要在录制或负载采样时修改 Assets 脚本。

动画编码：用带 Pillow 的 Python 运行 `Tools/Art/PhaseFiveEffects/encode_previews.py`。源帧在 `Logs/PhaseFiveEffects/Frames`，不打包到发布资源中。保护检查使用 `preserve_existing.py`，只直接比较原始文件字节，不进行哈希值校验。

完整结果见 [验证报告](../../Validation/PhaseFiveEffectsReport.md)，专业说明见 [武器](Weapons.md)、[受击破坏](Damage.md)、[能源与防护](Energy.md)。
