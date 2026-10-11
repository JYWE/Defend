using System;
using System.Collections.Generic;

namespace Defend.Simulation
{
    /// <summary>
    /// 每局唯一的模拟组合入口。负责命令顺序、建筑生命周期和经济事务；能量结算委托给 EnergySystem。
    /// 可选战斗系统与能源试验负载互斥，原有能源白盒构造和行为保持兼容。
    /// </summary>
    public sealed class GameWorld
    {
        private readonly Dictionary<BuildingKind, BuildingDefinition> definitions;
        private readonly Dictionary<long, BuildingState> buildingsById = new Dictionary<long, BuildingState>();
        private readonly List<BuildingState> buildings = new List<BuildingState>();
        private readonly Queue<QueuedCommand> commands = new Queue<QueuedCommand>();
        private readonly BuildingGrid grid;
        private readonly List<WorldEventSnapshot> events = new List<WorldEventSnapshot>();
        public IReadOnlyList<WorldEventSnapshot> Events { get; }
        internal void Record(WorldEventKind kind, long id, double x, double z, int amount = 0)
            => events.Add(new WorldEventSnapshot(kind, id, x, z, amount));
        private long nextBuildingId = 1;
        private long nextCommandSequence = 1;

        public WorldSettings Settings { get; }
        public long CurrentTick { get; private set; }
        public int Credits { get; private set; }
        public bool TestLoadEnabled { get; private set; } = true;
        public IReadOnlyList<BuildingState> Buildings { get; }
        public EnergySystem Energy { get; }
        public BattleSystem Battle { get; }
        public CommandResult LastCommandResult { get; private set; }

        public GameWorld(IEnumerable<BuildingDefinition> definitions, WorldSettings settings = null,
            EnergySettings energySettings = null, BattleSettings battleSettings = null)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));
            this.definitions = new Dictionary<BuildingKind, BuildingDefinition>();
            foreach (BuildingDefinition definition in definitions)
            {
                if (definition == null || this.definitions.ContainsKey(definition.Kind))
                    throw new ArgumentException("建筑定义不能为空，同一种建筑只能注册一份定义。", nameof(definitions));
                this.definitions.Add(definition.Kind, definition);
            }
            if (this.definitions.Count == 0)
                throw new ArgumentException("世界至少需要一种建筑定义。", nameof(definitions));
            Settings = settings ?? new WorldSettings();
            Credits = Settings.StartingCredits;
            grid = new BuildingGrid(Settings.Width, Settings.Depth);
            if (battleSettings != null) foreach (var cell in battleSettings.BlockedCells) grid.Block(cell.X, cell.Z);
            Buildings = buildings.AsReadOnly();
            Events = events.AsReadOnly();
            Energy = new EnergySystem(Buildings, energySettings ?? new EnergySettings());
            if (battleSettings != null)
            {
                Battle = new BattleSystem(this, grid, battleSettings);
                TestLoadEnabled = false;
            }
            LastCommandResult = new CommandResult(0, true, "白盒世界已就绪。", 0);
        }

        public long Enqueue(WorldCommand command)
        {
            long sequence = nextCommandSequence;
            nextCommandSequence = checked(nextCommandSequence + 1);
            commands.Enqueue(new QueuedCommand(sequence, command));
            return sequence;
        }

        public BuildingState FindBuilding(long buildingId)
        {
            buildingsById.TryGetValue(buildingId, out BuildingState building);
            return building;
        }

        public BuildingState FindBuildingAt(int x, int z)
        {
            return FindBuilding(grid.Find(x, z));
        }

        public bool IsTerrainBlocked(int x, int z) => grid.Find(x, z) < 0;

        /// <summary>预览与实际操作共用边界条件；调用只读取状态，不扣费也不修改世界。</summary>
        public string PlacementIssue(BuildingKind kind, int x, int z, long movingId = 0)
        {
            if (!definitions.TryGetValue(kind, out var definition)) return "尚未配置这种建筑。";
            if (Battle != null && Battle.IsFinished) return "本局已经结束。";
            if (Battle != null && kind == BuildingKind.Core && (movingId != 0 || Battle.CoreBuildingId != 0)) return "核心不能移动或重复建造。";
            if (!grid.CanPlace(x, z, definition.Width, definition.Depth, movingId)) return "位置超出地图、位于岩体或已被占用。";
            if (Battle != null)
            {
                string reason = Battle.ValidatePlacement(x, z, definition.Width, definition.Depth);
                if (reason != null) return reason;
                if (movingId == 0)
                {
                    int count = 0; foreach (var owned in buildings) if (!owned.IsSold) count++;
                    if (count >= Battle.Settings.MaximumBuildings) return "建筑已达上限。";
                }
            }
            return movingId == 0 && Credits < definition.PurchaseCost ? "资金不足。" : null;
        }

        public void Step()
        {
            events.Clear();
            CurrentTick = checked(CurrentTick + 1);
            // 命令先于到货：同一步拆除或移动接收建筑，旧端点不能再接收能量。
            int countAtStart = commands.Count;
            for (int index = 0; index < countAtStart; index++)
            {
                QueuedCommand queued = commands.Dequeue();
                LastCommandResult = Apply(queued.Sequence, queued.Command);
            }

            if (TestLoadEnabled)
            {
                for (int index = 0; index < buildings.Count; index++)
                {
                    BuildingState building = buildings[index];
                    if (building.IsAlive && !building.IsSold && building.IsConsumer)
                        Energy.ConsumeUpTo(building.Id, building.Definition.ConsumptionPerTick);
                }
            }
            Battle?.StepCombat(CurrentTick);
            Energy.Step(CurrentTick);
            if (CurrentTick % Settings.TicksPerSecond == 0)
                foreach (var building in buildings)
                {
                    building.ReceivedLastSecond = building.TotalReceived - building.ReceivedAtLastSample;
                    building.ReceivedAtLastSample = building.TotalReceived;
                }
            Battle?.CompleteStep(CurrentTick);
        }

        private CommandResult Apply(long sequence, WorldCommand command)
        {
            if (Battle != null && Battle.IsFinished)
                return Result(sequence, false, "本局已经结束，请重新开始。", command.BuildingId);
            if (command.Kind == WorldCommandKind.StartWave)
                return Battle == null ? Result(sequence, false, "能源试验场没有战斗波次。", 0) : Battle.StartWave(sequence);
            if (command.Kind == WorldCommandKind.Place)
                return Place(sequence, command);
            if (command.Kind == WorldCommandKind.SetTestLoad)
            {
                if (Battle != null && command.Value != 0)
                    return Result(sequence, false, "战斗中由真实射击消耗能量，不能开启持续测试负载。", 0);
                TestLoadEnabled = command.Value != 0;
                return Result(sequence, true, TestLoadEnabled ? "持续测试负载已开启。" : "持续测试负载已关闭。", 0);
            }

            BuildingState building = FindBuilding(command.BuildingId);
            if (building == null || building.IsSold)
                return Result(sequence, false, "建筑不存在或已出售。", command.BuildingId);

            switch (command.Kind)
            {
                case WorldCommandKind.Move:
                    return Move(sequence, building, command.X, command.Z);
                case WorldCommandKind.Sell:
                    return Sell(sequence, building);
                case WorldCommandKind.Damage:
                    return Damage(sequence, building, command.Value);
                case WorldCommandKind.Repair:
                    return Repair(sequence, building);
                case WorldCommandKind.SetPriority:
                    if (!building.IsConsumer || command.Value <= 0 || command.Value > 100)
                        return Result(sequence, false, "仅用能建筑可设置优先级，权重必须在 1 到 100 之间。", building.Id);
                    building.PriorityWeight = command.Value;
                    return Result(sequence, true, "能量优先级已更新。", building.Id);
                default:
                    return Result(sequence, false, "无法识别的世界命令。", building.Id);
            }
        }

        private CommandResult Place(long sequence, WorldCommand command)
        {
            if (!definitions.TryGetValue(command.BuildingKind, out BuildingDefinition definition))
                return Result(sequence, false, "尚未配置这种建筑。", 0);
            if (!grid.CanPlace(command.X, command.Z, definition.Width, definition.Depth))
                return Result(sequence, false, "位置超出地图或与其他建筑重叠。", 0);
            if (Battle != null)
            {
                if (definition.Kind == BuildingKind.Core && Battle.CoreBuildingId != 0)
                    return Result(sequence, false, "每局只能有一座核心。", 0);
                int ownedCount = 0;
                foreach (BuildingState owned in buildings)
                    if (!owned.IsSold) ownedCount++;
                if (ownedCount >= Battle.Settings.MaximumBuildings)
                    return Result(sequence, false, "建筑数量已达本局上限，请先出售不需要的建筑。", 0);
                string reason = Battle.ValidatePlacement(command.X, command.Z, definition.Width, definition.Depth);
                if (reason != null) return Result(sequence, false, reason, 0);
            }
            if (Credits < definition.PurchaseCost)
                return Result(sequence, false, "资源不足，无法购买建筑。", 0);

            long identifier = nextBuildingId;
            long followingIdentifier = checked(nextBuildingId + 1);
            var building = new BuildingState(identifier, definition, command.X, command.Z);
            grid.Occupy(building);
            buildings.Add(building);
            buildingsById.Add(building.Id, building);
            nextBuildingId = followingIdentifier;
            Credits -= definition.PurchaseCost;
            Battle?.RegisterCore(building);
            return Result(sequence, true, definition.DisplayName + "已放置。", building.Id);
        }

        private CommandResult Move(long sequence, BuildingState building, int x, int z)
        {
            if (Battle != null && building.Kind == BuildingKind.Core)
                return Result(sequence, false, "核心不能移动。", building.Id);
            if (building.X == x && building.Z == z)
                return Result(sequence, true, "建筑位置未改变。", building.Id);
            if (!grid.CanPlace(x, z, building.Definition.Width, building.Definition.Depth, building.Id))
                return Result(sequence, false, "目标位置超出地图或已被占用。", building.Id);
            if (Battle != null)
            {
                string reason = Battle.ValidatePlacement(x, z, building.Definition.Width, building.Definition.Depth);
                if (reason != null) return Result(sequence, false, reason, building.Id);
            }
            int nextPositionRevision = checked(building.PositionRevision + 1);
            Energy.BeforeBuildingChanged(building, false);
            grid.Release(building);
            building.X = x;
            building.Z = z;
            building.PositionRevision = nextPositionRevision;
            if (building.IsAlive)
                grid.Occupy(building);
            return Result(sequence, true, "建筑已移动，旧位置的未完成接收已取消。", building.Id);
        }

        private CommandResult Sell(long sequence, BuildingState building)
        {
            if (Battle != null && building.Kind == BuildingKind.Core)
                return Result(sequence, false, "核心不能出售。", building.Id);
            if (Credits > int.MaxValue - building.Definition.SaleRefund)
                return Result(sequence, false, "资源数量达到上限，无法完成出售。", building.Id);
            Energy.BeforeBuildingChanged(building, true);
            grid.Release(building);
            building.IsSold = true;
            building.IsAlive = false;
            building.Health = 0;
            building.CombatState = TurretCombatState.None;
            Credits += building.Definition.SaleRefund;
            return Result(sequence, true, "建筑已出售，内部储能已损失。", building.Id);
        }

        private CommandResult Damage(long sequence, BuildingState building, int amount)
        {
            if (!building.IsAlive || amount <= 0)
                return Result(sequence, false, "建筑已经损坏，或伤害数值无效。", building.Id);
            int healthDamage = amount;
            if (building.Kind == BuildingKind.Core)
            {
                // 白盒阶段按 1 点能量吸收 1 点伤害；生命值为整数，剩余伤害向上取整。
                long absorbed = Energy.ConsumeUpTo(building.Id, (long)amount * 1000);
                healthDamage = (int)(((long)amount * 1000 - absorbed + 999) / 1000);
                if (absorbed > 0)
                {
                    Record(WorldEventKind.ShieldHit, building.Id, building.CenterX, building.CenterZ);
                    if (building.StoredEnergy == 0) Record(WorldEventKind.ShieldBreak, building.Id, building.CenterX, building.CenterZ);
                }
            }
            if (healthDamage > 0) Record(WorldEventKind.BuildingHit, building.Id, building.CenterX, building.CenterZ, Math.Min(healthDamage, building.Health));
            building.Health = Math.Max(0, building.Health - healthDamage);
            if (healthDamage > 0) building.LastDamageTick = CurrentTick;
            if (building.Health == 0)
            {
                building.DestructionCount++;
                Record(WorldEventKind.BuildingDestroyed, building.Id, building.CenterX, building.CenterZ);
                Energy.BeforeBuildingChanged(building, true);
                building.IsAlive = false;
                building.EnergyStatus = "建筑已损坏";
                building.CombatState = TurretCombatState.None;
                grid.Release(building);
                Battle?.BuildingDestroyed(building);
            }
            if (!building.IsAlive && Battle != null && building.Kind == BuildingKind.Core)
                return Result(sequence, true, "核心已被摧毁，本局结束。", building.Id);
            return Result(sequence, true, building.IsAlive ? "测试伤害已结算。" : "建筑已损坏，可修复后恢复线路。", building.Id);
        }

        private CommandResult Repair(long sequence, BuildingState building)
        {
            if (building.IsAlive && building.Health == building.Definition.MaximumHealth)
                return Result(sequence, false, "建筑状态完好，无需修复。", building.Id);
            if (Credits < building.Definition.RepairCost)
                return Result(sequence, false, "资源不足，无法修复建筑。", building.Id);
            if (!building.IsAlive && !grid.CanPlace(building.X, building.Z,
                    building.Definition.Width, building.Definition.Depth, building.Id))
                return Result(sequence, false, "原位置已被占用，不能恢复建筑。", building.Id);
            if (!building.IsAlive && Battle != null)
            {
                string reason = Battle.ValidatePlacement(building.X, building.Z, building.Definition.Width, building.Definition.Depth);
                if (reason != null) return Result(sequence, false, reason, building.Id);
            }

            int nextLifeGeneration = building.IsAlive ? building.LifeGeneration : checked(building.LifeGeneration + 1);
            // 普通补血不改生命代次；从已毁状态恢复才建立新接收端，避免旧到货事件复活。
            building.LifeGeneration = nextLifeGeneration;
            building.Health = building.Definition.MaximumHealth;
            building.LastRepairTick = CurrentTick;
            Record(WorldEventKind.Repair, building.Id, building.CenterX, building.CenterZ);
            building.IsAlive = true;
            grid.Occupy(building);
            Credits -= building.Definition.RepairCost;
            return Result(sequence, true, "建筑已修复，损失的能量不会返还。", building.Id);
        }

        internal void ApplyBattleDamage(BuildingState building, int amount)
        {
            Damage(0, building, amount);
        }

        internal int GrantBattleReward(int amount)
        {
            int actual = Math.Min(amount, int.MaxValue - Credits);
            Credits += actual;
            return actual;
        }

        private static CommandResult Result(long sequence, bool succeeded, string message, long buildingId)
        {
            return new CommandResult(sequence, succeeded, message, buildingId);
        }

        private readonly struct QueuedCommand
        {
            public long Sequence { get; }
            public WorldCommand Command { get; }

            public QueuedCommand(long sequence, WorldCommand command)
            {
                Sequence = sequence;
                Command = command;
            }
        }
    }
}
