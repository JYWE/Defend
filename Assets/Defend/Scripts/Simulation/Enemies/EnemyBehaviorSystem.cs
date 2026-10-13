using System;
using System.Collections.Generic;

namespace Defend.Simulation
{
    /// <summary>重装的局部感应、有限记忆和有预算的目标选择；不访问供电诊断或全图网络。</summary>
    internal sealed class EnemyBehaviorSystem
    {
        private readonly GameWorld world;
        private readonly BattleSystem battle;
        private readonly EnemyNavigation coreNavigation;
        private readonly EnemyStrategicNavigation navigation;
        private readonly EnemyBehaviorSettings settings;
        private readonly int memoryTicks, evaluationTicks, lockTicks;
        private readonly List<Candidate> candidates = new List<Candidate>();
        private long evaluationCursor;
        internal EnemyBehaviorStatistics Statistics { get; } = new EnemyBehaviorStatistics();

        internal EnemyBehaviorSystem(GameWorld world, BuildingGrid grid, BattleSystem battle, EnemyNavigation coreNavigation)
        {
            this.world = world;
            this.battle = battle;
            this.coreNavigation = coreNavigation;
            settings = battle.Settings.EnemyBehavior;
            memoryTicks = Math.Max(1, (int)Math.Ceiling(settings.ObservationMemorySeconds * world.Settings.TicksPerSecond));
            evaluationTicks = Math.Max(1, (int)Math.Ceiling(settings.EvaluationIntervalSeconds * world.Settings.TicksPerSecond));
            lockTicks = (int)Math.Ceiling(settings.MinimumTargetLockSeconds * world.Settings.TicksPerSecond);
            navigation = new EnemyStrategicNavigation(grid, settings, Statistics);
            if (settings.Enabled) world.Energy.TransferCompleted = ObserveDelivery;
        }

        internal void BeginStep(long tick)
        {
            Statistics.BeginStep();
            if (!settings.Enabled || battle.State != BattleState.Combat) return;
            navigation.BeginStep();
            foreach (var enemy in battle.Enemies)
            {
                if (!CanObserve(enemy)) continue;
                var memory = Memory(enemy);
                foreach (var building in world.Buildings)
                    if (building.IsAlive && !building.IsSold && CanSense(enemy, building))
                    {
                        memory.Observe(building, tick);
                        Statistics.BuildingsObservedThisTick++;
                        Statistics.TotalBuildingsObserved++;
                    }
                for (int index = memory.Buildings.Count - 1; index >= 0; index--)
                {
                    var observation = memory.Buildings[index];
                    var building = world.FindBuilding(observation.Id);
                    if (tick - observation.LastSeenTick < memoryTicks && building != null && building.IsAlive &&
                        !building.IsSold && observation.LifeGeneration == building.LifeGeneration) continue;
                    memory.ForgetSupplies(observation.Id);
                    memory.Buildings.RemoveAt(index);
                }
                for (int index = memory.Supplies.Count - 1; index >= 0; index--)
                    if (tick - memory.Supplies[index].Deliveries.LastEventTick >= memoryTicks) memory.Supplies.RemoveAt(index);
            }
        }

        internal void ObserveDamage(BuildingState turret, double victimX, double victimZ, int actualDamage, long tick)
        {
            if (!settings.Enabled || actualDamage <= 0) return;
            foreach (var enemy in battle.Enemies)
            {
                if (!CanObserve(enemy) || !CanSense(enemy, turret)) continue;
                double differenceX = enemy.X - victimX, differenceZ = enemy.Z - victimZ;
                if (differenceX * differenceX + differenceZ * differenceZ > settings.SensingRadius * settings.SensingRadius) continue;
                var observation = Memory(enemy).Observe(turret, tick);
                observation.Damage.Add(actualDamage, tick, memoryTicks);
                Statistics.DamageEventsObservedThisTick++;
                Statistics.TotalDamageEventsObserved++;
            }
        }

        private void ObserveDelivery(BuildingState source, BuildingState target, long amount, long tick)
        {
            if (battle.State != BattleState.Combat || !source.IsAlive || source.IsSold) return;
            foreach (var enemy in battle.Enemies)
            {
                if (!CanObserve(enemy) || !CanSense(enemy, source) || !CanSense(enemy, target)) continue;
                var memory = Memory(enemy);
                memory.Observe(source, tick);
                memory.Observe(target, tick);
                EnemySupplyObservation supply = null;
                foreach (var entry in memory.Supplies)
                    if (entry.SourceId == source.Id && entry.TargetId == target.Id) { supply = entry; break; }
                if (supply == null)
                {
                    supply = new EnemySupplyObservation { SourceId = source.Id, TargetId = target.Id };
                    memory.Supplies.Add(supply);
                }
                supply.Deliveries.Add(amount, tick, memoryTicks);
                Statistics.DeliveriesObservedThisTick++;
                Statistics.TotalDeliveriesObserved++;
            }
        }

        internal void Evaluate(BuildingState core, long tick)
        {
            if (!settings.Enabled) return;
            int count = battle.Enemies.Count;
            int start = 0;
            while (start < count && battle.Enemies[start].Id <= evaluationCursor) start++;
            if (start == count) start = 0;
            for (int offset = 0; offset < count; offset++)
            {
                var enemy = battle.Enemies[(start + offset) % count];
                if (!CanObserve(enemy)) continue;
                var memory = Memory(enemy);
                ValidateTarget(enemy, tick);
                if (BattleSystem.InAttackRange(enemy, core))
                {
                    ClearTarget(enemy);
                    continue;
                }
                if (tick < memory.NextEvaluationTick) continue;
                if (Statistics.EvaluationsThisTick >= settings.MaximumEvaluationsPerTick)
                {
                    Statistics.Deferred();
                    continue;
                }
                evaluationCursor = enemy.Id;
                Statistics.EvaluationsThisTick++;
                Statistics.TotalEvaluations++;
                enemy.LastBehaviorEvaluationTick = tick;
                memory.NextEvaluationTick = checked(tick + evaluationTicks);
                EvaluateEnemy(enemy, core, tick);
            }
            Statistics.MaximumEvaluationsObserved = Math.Max(Statistics.MaximumEvaluationsObserved, Statistics.EvaluationsThisTick);
            Statistics.MaximumPathBuildsObserved = Math.Max(Statistics.MaximumPathBuildsObserved, Statistics.PathBuildsThisTick);
        }

        private void ValidateTarget(EnemyState enemy, long tick)
        {
            var memory = Memory(enemy);
            if (memory.StrategicTargetId == 0) return;
            var observation = memory.Find(memory.StrategicTargetId);
            var building = world.FindBuilding(memory.StrategicTargetId);
            if (observation == null || building == null || !building.IsAlive || building.IsSold ||
                building.LifeGeneration != memory.StrategicLifeGeneration || tick - observation.LastSeenTick >= memoryTicks)
            {
                ClearTarget(enemy);
                return;
            }
            // 只有已知坐标用于行动；隐藏移动不会更新此处的位置。
            enemy.TargetBuildingId = observation.Id;
            enemy.TargetAssignedTick = memory.TargetAssignedTick;
            enemy.TargetX = observation.CenterX;
            enemy.TargetZ = observation.CenterZ;
            enemy.TargetPurpose = observation.LastSeenTick == tick
                ? (observation.IsTurret ? EnemyTargetPurpose.SuppressTurret : EnemyTargetPurpose.DisruptEnergy)
                : EnemyTargetPurpose.SearchLastKnownPosition;
            if (memory.Path != null && (memory.Path.X != observation.X || memory.Path.Z != observation.Z)) memory.Path = null;
            if (!navigation.IsCurrent(memory.Path))
            {
                memory.Path = navigation.Get(observation);
                if (memory.Path == null) Statistics.DeferredPath();
            }
            if (navigation.IsCurrent(memory.Path) && navigation.Distance(memory.Path, enemy.CellX, enemy.CellZ) < 0)
                ClearTarget(enemy);
        }

        private void EvaluateEnemy(EnemyState enemy, BuildingState core, long tick)
        {
            var memory = Memory(enemy);
            int coreDistance = coreNavigation.GetDistance(enemy.CellX, enemy.CellZ, core);
            if (coreDistance < 0) { ClearTarget(enemy); return; }
            bool deferred = false;
            Candidate current = default;
            if (memory.StrategicTargetId != 0)
            {
                var observation = memory.Find(memory.StrategicTargetId);
                var path = navigation.Get(observation);
                if (path == null) deferred = true;
                else
                {
                    current = Score(enemy, observation, path, core, coreDistance, tick);
                    if (current.Score < 0) ClearTarget(enemy);
                    else memory.Path = path;
                }
                if (memory.StrategicTargetId != 0 && tick - memory.TargetAssignedTick < lockTicks)
                {
                    if (deferred) Statistics.Deferred();
                    return;
                }
                // 不在感应范围时完成有限的旧位置搜索，不拿缺失信息重新算出远处目标。
                if (memory.StrategicTargetId != 0 && enemy.TargetPurpose == EnemyTargetPurpose.SearchLastKnownPosition)
                {
                    if (deferred) Statistics.Deferred();
                    return;
                }
            }

            candidates.Clear();
            foreach (var observation in memory.Buildings)
            {
                if (observation.LastSeenTick != tick || observation.Kind == BuildingKind.Core) continue;
                double benefit = Benefit(memory, observation, tick);
                if (benefit <= 0) continue;
                double distance = Math.Abs(observation.CenterX - enemy.X) + Math.Abs(observation.CenterZ - enemy.Z);
                double approximateCost = 2 + distance / Speed(enemy) + DemolitionSeconds(enemy, observation);
                candidates.Add(new Candidate { Observation = observation, Score = benefit / approximateCost });
            }
            candidates.Sort(Candidate.Compare);
            Candidate best = default;
            best.Score = 1; // 低收益绕行让位于持续进攻核心。
            if (current.Path != null && current.Score > best.Score) best = current;
            int compared = current.Path == null ? 0 : 1;
            for (int index = 0; index < candidates.Count && compared < settings.MaximumCandidates; index++)
            {
                var observation = candidates[index].Observation;
                if (current.Path != null && observation.Id == current.Observation.Id) continue;
                compared++;
                var path = navigation.Get(observation);
                if (path == null) { deferred = true; continue; }
                var candidate = Score(enemy, observation, path, core, coreDistance, tick);
                if (candidate.Score > best.Score || (candidate.Score == best.Score && best.Observation != null && observation.Id < best.Observation.Id))
                    best = candidate;
            }
            if (deferred) Statistics.Deferred();
            if (best.Observation == null)
            {
                // 预算延期不抹去仍然有效的行动；已完成比较且当前收益消失后才恢复推进。
                if (!deferred && memory.StrategicTargetId != 0 && current.Score <= 1) ClearTarget(enemy);
                return;
            }
            if (memory.StrategicTargetId == best.Observation.Id)
            {
                memory.Path = best.Path;
                return;
            }
            if (memory.StrategicTargetId != 0 && (deferred && current.Path == null || best.Score < Math.Max(1, current.Score) * settings.SwitchScoreRatio)) return;
            memory.StrategicTargetId = best.Observation.Id;
            memory.StrategicLifeGeneration = best.Observation.LifeGeneration;
            memory.TargetAssignedTick = tick;
            memory.Path = best.Path;
            enemy.TargetBuildingId = best.Observation.Id;
            enemy.TargetAssignedTick = tick;
            enemy.TargetX = best.Observation.CenterX;
            enemy.TargetZ = best.Observation.CenterZ;
            enemy.TargetPurpose = best.Observation.IsTurret ? EnemyTargetPurpose.SuppressTurret : EnemyTargetPurpose.DisruptEnergy;
            Statistics.Switched();
        }

        private Candidate Score(EnemyState enemy, EnemyBuildingObservation observation, EnemyStrategicPath path,
            BuildingState core, int coreDistance, long tick)
        {
            var result = new Candidate { Observation = observation, Path = path, Score = -1 };
            int distance = navigation.Distance(path, enemy.CellX, enemy.CellZ);
            if (distance < 0) return result;
            int x = enemy.CellX, z = enemy.CellZ;
            double exposureDamage = 0;
            var memory = Memory(enemy);
            for (int step = 0; step <= distance; step++)
            {
                foreach (var turret in memory.Buildings)
                {
                    if (!turret.IsTurret) continue;
                    double differenceX = turret.CenterX - (x + 0.5), differenceZ = turret.CenterZ - (z + 0.5);
                    double range = battle.Settings.GetTurret(turret.Kind).Range;
                    if (differenceX * differenceX + differenceZ * differenceZ <= range * range)
                        exposureDamage += Damage(turret, tick) / settings.ObservationMemorySeconds / Speed(enemy);
                }
                if (!navigation.TryNext(path, x, z, out int nextX, out int nextZ)) break;
                x = nextX; z = nextZ;
            }
            int remainingCoreDistance = coreNavigation.GetDistance(x, z, core);
            if (remainingCoreDistance < 0 || distance + remainingCoreDistance - coreDistance > settings.MaximumAdditionalTravelCells) return result;
            double cost = 2 + distance / Speed(enemy) + DemolitionSeconds(enemy, observation) + 20 * exposureDamage / enemy.MaximumHealth;
            result.Score = Benefit(memory, observation, tick) / cost;
            return result;
        }

        private double Benefit(EnemyBehaviorMemory memory, EnemyBuildingObservation observation, long tick)
        {
            double damageRate = Damage(observation, tick) / settings.ObservationMemorySeconds;
            if (observation.IsTurret) return 3 * Math.Sqrt(damageRate);
            if (observation.Kind != BuildingKind.Reactor && observation.Kind != BuildingKind.Relay) return 0;
            double outgoing = 0, supportedDamage = 0;
            foreach (var supply in memory.Supplies)
                if (supply.SourceId == observation.Id) outgoing += Delivery(supply, tick);
            if (outgoing <= 0) return 0;
            foreach (var turret in memory.Buildings)
            {
                if (!turret.IsTurret) continue;
                int supportedRoutes = 0;
                int alternatives = 0;
                foreach (var supply in memory.Supplies)
                {
                    if (supply.TargetId != turret.Id || Delivery(supply, tick) <= 0) continue;
                    alternatives++;
                    if (supply.SourceId == observation.Id) supportedRoutes++;
                    else
                    {
                        // 仅追踪已现场观察到的两跳关系；不遍历实际全图拓扑。
                        foreach (var upstream in memory.Supplies)
                            if (upstream.SourceId == observation.Id && upstream.TargetId == supply.SourceId && Delivery(upstream, tick) > 0)
                            { supportedRoutes++; break; }
                    }
                }
                if (supportedRoutes > 0) supportedDamage += Damage(turret, tick) / settings.ObservationMemorySeconds * supportedRoutes / Math.Max(1, alternatives);
            }
            double deliveryRate = outgoing / 1000 / settings.ObservationMemorySeconds;
            return 10 * Math.Sqrt(deliveryRate) + 2 * Math.Sqrt(supportedDamage);
        }

        internal bool HasStrategicTarget(EnemyState enemy) => settings.Enabled && enemy.BehaviorMemory != null && enemy.BehaviorMemory.StrategicTargetId != 0;

        internal bool HasCurrentPath(EnemyState enemy) => HasStrategicTarget(enemy) &&
            navigation.Distance(enemy.BehaviorMemory.Path, enemy.CellX, enemy.CellZ) >= 0;

        internal bool TryNextCell(EnemyState enemy, out int x, out int z)
        {
            return navigation.TryNext(enemy.BehaviorMemory.Path, enemy.CellX, enemy.CellZ, out x, out z);
        }

        internal bool CanAttack(EnemyState enemy, BuildingState building, long tick)
        {
            if (!HasStrategicTarget(enemy) || building.Id != enemy.BehaviorMemory.StrategicTargetId) return true;
            var observation = enemy.BehaviorMemory.Find(building.Id);
            return observation != null && observation.LastSeenTick == tick && building.IsAlive && !building.IsSold &&
                observation.X == building.X && observation.Z == building.Z && observation.LifeGeneration == building.LifeGeneration;
        }

        private void ClearTarget(EnemyState enemy)
        {
            var memory = enemy.BehaviorMemory;
            if (memory == null || memory.StrategicTargetId == 0) return;
            memory.StrategicTargetId = 0;
            memory.Path = null;
            memory.NextEvaluationTick = 0;
            enemy.TargetBuildingId = 0;
            Statistics.Switched();
        }

        private bool CanSense(EnemyState enemy, BuildingState building)
        {
            double closestX = Math.Max(building.X, Math.Min(enemy.X, building.X + building.Definition.Width));
            double closestZ = Math.Max(building.Z, Math.Min(enemy.Z, building.Z + building.Definition.Depth));
            double x = enemy.X - closestX, z = enemy.Z - closestZ;
            return x * x + z * z <= settings.SensingRadius * settings.SensingRadius;
        }

        private static bool CanObserve(EnemyState enemy) => enemy.IsAlive && enemy.Kind == EnemyKind.Heavy;
        private static EnemyBehaviorMemory Memory(EnemyState enemy) => enemy.BehaviorMemory ?? (enemy.BehaviorMemory = new EnemyBehaviorMemory());
        private double Damage(EnemyBuildingObservation observation, long tick) => observation.Damage.Value(tick, memoryTicks);
        private double Delivery(EnemySupplyObservation supply, long tick) => supply.Deliveries.Value(tick, memoryTicks);
        private double Speed(EnemyState enemy) => battle.Settings.EnemySpeedCellsPerSecond * enemy.SpeedMultiplier;
        private double DemolitionSeconds(EnemyState enemy, EnemyBuildingObservation observation) =>
            (double)observation.Health * battle.Settings.EnemyAttackIntervalTicks /
            ((double)battle.Settings.EnemyAttackDamage * enemy.DamageMultiplier * world.Settings.TicksPerSecond);

        private struct Candidate
        {
            internal EnemyBuildingObservation Observation;
            internal EnemyStrategicPath Path;
            internal double Score;
            internal static int Compare(Candidate left, Candidate right)
            {
                int score = right.Score.CompareTo(left.Score);
                return score != 0 ? score : left.Observation.Id.CompareTo(right.Observation.Id);
            }
        }
    }
}
