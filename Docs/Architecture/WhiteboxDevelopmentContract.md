# 第一阶段白盒开发接口

2026-10-10。当前交付为可交互能源网络试验场，不等同完整敌人和波次成品。四种建筑、动态操作、能量调度和可编辑场景是本阶段集成范围。所有验证直接比较字段及数量，禁止哈希值校验。代码命名使用完整自然的术语，关键规则写清晰中文注释。

分工：主负责人维护 `Simulation/World`、`Simulation/Buildings`、`Simulation/Grid` 与总程序集；能源开发维护 `Simulation/Energy`；表现开发维护 `Scripts/Unity`、`Scripts/Editor` 与 `Content`；验证开发维护 `Tests`。只有主负责人运行 Unity 导入、场景生成、播放、测试和构建，避免争用同一个 Editor。

统一命名空间 `Defend.Simulation`。以下是跨模块契约，改变前通知主负责人和消费者。

## 模拟公共模型

- `BuildingKind`: Reactor, Relay, Turret, Core。
- `BuildingDefinition` 只读属性：`Kind`, `DisplayName`, `Width`, `Depth`, `EnergyCapacity` (long), `ProductionPerTick` (long), `ConsumptionPerTick` (long), `OutputAmount` (long), `OutputIntervalTicks` (int), `TransmissionRange` (double), `MaximumHealth` (int), `PurchaseCost` (int), `SaleRefund` (int), `RepairCost` (int)。能量 1000 最小单位 = 1 点。
- 定义构造：`BuildingDefinition(BuildingKind kind, string displayName, int width, int depth, long energyCapacity, long productionPerTick, long consumptionPerTick, long outputAmount, int outputIntervalTicks, double transmissionRange, int maximumHealth, int purchaseCost, int saleRefund, int repairCost)`。
- `BuildingState` 公开只读、程序集内部可写：`Id` (long), `Definition`, `X`, `Z`, `Health`, `LifeGeneration`, `PositionRevision`, `PriorityWeight`, `IsAlive`, `IsSold`, `StoredEnergy` (long), `ReservedIncoming` (long), `CommittedIncoming` (long), `TotalReceived` (long), `EnergyStatus` (string)。派生属性 `Kind`, `CanOutput`, `CanReceive`, `IsConsumer`, `CenterX`, `CenterZ`。
- 只有 EnergySystem 写入能量字段；生命周期事件通过该系统清理后再修改存活/位置状态。默认优先级权重 2。
- `WorldSettings` 构造 `(int width=40, int depth=40, int ticksPerSecond=20, int startingCredits=2000)`，同名 PascalCase 只读属性。
- `EnergySettings` 构造 `(int stallTimeoutTicks=200, int flightTicksPerCell=3, long baseQuantum=1000, double maximumLossAtRange=0.5, int maximumPathWorkPerTick=32768, int pathWorkSlice=2048)`。寻路按实际展开工作计数并跨步继续，不把成功发送次数当作寻路预算。
- `WorldCommand` 静态工厂：`Place(BuildingKind kind,int x,int z)`、`Move(long buildingId,int x,int z)`、`Sell(long buildingId)`、`Damage(long buildingId,int amount)`、`Repair(long buildingId)`、`SetPriority(long buildingId,int weight)`、`SetTestLoad(bool enabled)`。
- `CommandResult` 只读 `Sequence` (long), `Succeeded` (bool), `Message` (string), `BuildingId` (long)。
- `GameWorld(IEnumerable<BuildingDefinition> definitions, WorldSettings settings=null, EnergySettings energySettings=null)`。
- 世界公开 `Settings`, `CurrentTick` (long), `Credits`, `TestLoadEnabled`, `Buildings` (IReadOnlyList<BuildingState>), `Energy` (EnergySystem), `LastCommandResult`；`Enqueue(WorldCommand)` 返回命令序号；`Step()`；`FindBuilding(long)` 和 `FindBuildingAt(int,int)` 返回对象或 null。第一步时先应用队列命令，后消费测试负载，再调用 Energy.Step。
- `SimulationClock(int ticksPerSecond=20,int maximumStepsPerFrame=8)`：`Advance(double elapsedSeconds, bool paused, double speed, Action step)` 返回本次步数；不丢弃尚未执行的积压；公开 `PendingSeconds`。

## 能源接口

- `EnergySystem(IReadOnlyList<BuildingState> buildings, EnergySettings settings)`。
- `Step(long tick)`：刷新图/状态、到货、生产、调度、发送、清理。至少一 Tick 一跳；取消生命周期事件优先于之后到货。
- `BeforeBuildingChanged(BuildingState building, bool loseStoredEnergy)`：当移动时 false，死亡/出售时 true；立即取消发往该建筑旧端点的未到货批次、释放预约，必要时损失存量；已离开发送建筑的批次继续。恢复前建筑已经无库存，由之后 Step 重新识别图。
- `TryConsume(long buildingId,long amount)` 返回 bool；`ConsumeUpTo(long buildingId,long amount)` 返回实际量。
- `GetTotals()` 返回 `EnergyTotals`，公开 long `Generated`, `Consumed`, `TransmissionLoss`, `DestructionLoss`, `ExpiredLoss`, `InFlight`, `Stored`, `BalanceError`；丢失当前跳的量归到 DestructionLoss 并在内部区分原因。公开 int `ActiveBatchCount`。
- `Connections`：IReadOnlyList<EnergyConnection>，元素公开 `SourceId`, `TargetId` (long), `Efficiency` (double)。
- `Transfers`：IReadOnlyList<EnergyTransferSnapshot>，元素公开 `Id` (long), `SourceId`, `TargetId`, `SourceX`, `SourceZ`, `TargetX`, `TargetZ` (double), `StartTick`, `ArrivalTick`, `Amount` (long)。仅作为表现，不驱动到货。
- `AssertInvariants()`：失败抛带原因异常，直接比较数值与访问历史，不计算哈希。
- `PlanningStatistics`：只读 `WorkUsedThisTick`, `PendingSearchCount`, `CompletedSearchCount`, `CacheHitCount`, `SearchStartedCount`，用于观察寻路预算、缓存复用和积压。
- `BatchDiagnostics`：只读批次快照，包含 `Id`, `BuildingId`, `InFlight`, `History`, `StalledTicks`, `Amount`, `DestinationId`；仅诊断使用，不提供外部修改入口。

## 场景和表现

使用现有 Input System、uGUI、URP。全部 UI 在编辑器生成并保存为可手工编辑的资产/场景；运行时只绑定和更新，不生成控件或布局。白盒建筑使用 Unity 原始几何及材质，Prefab 资产由 Editor 工具保存。

表现开发提供一个静态编辑器入口 `Defend.Editor.WhiteboxSceneBuilder.Build()`，负责创建缺失白盒定义、四类建筑 Prefab、能量表现 Prefab、完整 UI Prefab、场景 `Assets/Defend/Content/Scenes/Dev/EnergyWhitebox.unity`。需要重建时应避免静默覆盖手工编辑的资产。场景生成仅由主负责人在编译成功后调用。

建议配置：40×40 网格，四种建筑初始均为 1×1；有效传输半径 7 格；反应堆每逻辑步产生 2000 最小单位，中继每 4 步最多输出 8000；终端持续测试负载可以关闭。全部为白盒试验值，非最终平衡。其余数值由定义资源表达。

可用交互：选择建筑、购买放置、移动预览与取消、出售、损坏/修复试验、低/中/高优先级、测试负载开关、暂停/倍速/单步；显示当前储量、已预约量、到货累计、损耗与守恒差额。开发诊断与面向玩家的文字分区显示。

## 验证约束

先写 EditMode 行为测试，再由主负责人跑 Unity 编译与测试。验证开发不修改生产模块，不操作共享 Editor。测试涵盖守恒、容量、防回流、单跳延迟、动态端点失效、修复身份、输出共享与优先级比例；不使用字段哈希或快照哈希。

已存在的用户改动必须保留。不得安装新包、重置分支、清理用户改动、替换模板设置或运行时拼装 UI。代码文件与 Unity 生成的 `.meta` 一起交付。
