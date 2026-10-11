# 第五阶段特效制作约定

2026-10-10。范围：工业科幻白盒特效资源、机械动作演示、可复用播放与回收、独立动态展示。保留已经交付的全部模型、第四阶段关卡与现有玩法。

## 制作语言与清单

灰白装甲和深色机械主体保持原配色。能源为青色；敌方、危险与命中使用暖色；维修使用短促绿色。使用小面积不透明亮片、细线与少量淡出粒子，避免大团光晕和常驻烟雾。70 度正交镜头优先，另检查近景。

| 专业 | 效果资源 | 时间与要求 |
| --- | --- | --- |
| 武器 | RapidMuzzle、HeavyMuzzle、AreaDischarge、RapidTracer、HeavyTracer、RapidImpact、HeavyImpact、AreaImpact | 连射闪光 0.06 秒、后坐恢复 0.12 秒；重炮闪光 0.10 秒、后坐恢复 0.32 秒；范围扩散 0.35 秒。射线与命中立即出现，不表达新增伤害延迟。 |
| 受击与破坏 | EnemyHit、EnemyDeath、HeavyContact、BuildingHit、BuildingDestruction、RepairComplete | 受击约 0.08 秒；死亡塌落 0.25 秒、碎片约 0.60 秒；撞击约 0.25 秒；建筑尘雾与残骸过渡约 0.50 秒；维修约 0.22 秒。重复播放可恢复，碎片没有碰撞和伤害。 |
| 能源与防护 | ReactorRunning、RelayRunning、PortSend、EnergyTransfer、EnergyArrival、EnergyShortage、EnergyDisconnected、HeavyLock、SearchMarker、ShieldHit、ShieldBreak | 运行灯为可停止循环；端口发送短闪；实际运输脉冲最多约 0.45 秒，仅接受已确认运输；到货独立确认；护盾命中 0.22 秒，破裂约 0.50 秒。状态图形须不同，不只依赖颜色。 |

三个专业子代理分别维护新目录的 Weapons、Damage、Energy；主负责人维护 Shared、Demo、展示场景、公共材质/Shader、预算与验证。只有主负责人操作 Editor。制作脚本可以调用共享制作工具，禁止改写已交付模型或共用源文件。

## 目录与公共接口

- 新运行时代码：`Assets/Defend/Effects/Runtime/{Shared,Weapons,Damage,Energy,Demo}`；程序集 `Defend.PhaseFiveEffects`，命名空间 `Defend.PhaseFiveEffects`。
- 新制作代码：`Assets/Defend/Effects/Editor/{Shared,Weapons,Damage,Energy}`；程序集 `Defend.PhaseFiveEffects.Editor`，命名空间 `Defend.PhaseFiveEffects.Editor`。
- 新效果资源：`Assets/Defend/Content/Art/PhaseFiveEffects/{Prefabs,Materials,Shaders,Scenes}`。
- 专业说明及证据：`Docs/Art/PhaseFiveEffects`；自动检查工具：`Tools/Art/PhaseFiveEffects`。
- 统一基类 `PhaseFiveEffect`：序列化字段 `Lifetime`、`Looping`、`BudgetGroup`、`BudgetUnits`；`Begin(EffectRequest)`、`Advance(float)`、`StopAndReset()`；子类实现 `OnBegin()`、`OnAdvance(float normalizedTime,float deltaTime)`、`OnReset()`，可覆写 `CanPlay(EffectRequest)`。
- `EffectRequest` 按值传递：`Origin`、`Destination`、`Rotation`、`Subject`（可选美术对象）、`Intensity`、`Seed`、`ConfirmedTransfer`、`EventSequence`。所有世界命中和运输端点是触发瞬间的值，不在播放中跟随目标。
- `EffectLibrary` 保存明确名称与 Prefab 引用；`EffectPool.TryPlay(string,EffectRequest)` 返回带代次的播放句柄。池统一推进，暂停时不推进年龄、粒子或机械动作；停止清空粒子/射线并复位。重复事件用有界序号记录过滤，过期句柄不得停止已复用对象。
- 子效果不自行启动协程或 Update，不修改 Time.timeScale，不访问模拟世界、不创建伤害或运输。Particle System 禁止自动播放，发射后由基类手动 Simulate，统一响应暂停。
- 预算：短攻击/命中 64 组、碎片 128 个、能源脉冲 128 个，状态效果另限 32 组。`BudgetUnits` 明确申报成本；不足时跳过视觉请求，不回写任何玩法。优先级、实际峰值及退化情况在检查报告中记录。

## 模型与挂点

采用已交付的规范 Unity Prefab：+Z 向前、+Y 向上。炮塔 `TurretYaw` 绕本地 Y；`BarrelRecoil` 沿 -Z；重装 `ImpactPivot` 沿 +Z。炮口采用 `MuzzleLeft` / `MuzzleRight` / `Muzzle`，绑定前检查实际层级。新增端口和状态挂点放在效果展示扩展 Prefab 中，原模型 Prefab 不修改。

受击不得改变共享材质；按渲染器与材质槽使用可恢复的 MaterialPropertyBlock，或使用独立局部反馈几何。对象池重复使用、停止、OnDisable、场景销毁均须恢复原状态；同一模型的重叠请求不能永久覆盖原配色。死亡演示的复位只服务资源预览，真实死亡后主体去留由未来玩法表现适配器决定。

## 验证与交付

先捕获既有资源直接字节基线和 Editor/Console 状态。编译后由主负责人制作资源与独立场景，实际进入 Play Mode，检查重复播放、循环停止、暂停恢复、对象池复用、失效句柄、重复序号、固定命中快照和场景重开清理。保存约 70 度及近景动态预览和关键帧，真实记录短高密度检查的对象、粒子、预算与帧耗时。离屏/隐藏执行不用于声明可见 60 帧。

本轮不接入真实战斗、不改变已确认伤害/能源语义、不新增包、不更改渲染管线或全局项目设置，不运行旧长时间验收。禁止哈希值校验，使用直接字节、字段、数量、集合、身份与实际画面。
