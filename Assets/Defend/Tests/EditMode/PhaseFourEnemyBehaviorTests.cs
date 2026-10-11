using System;
using System.Collections.Generic;
using System.Linq;
using Defend.Simulation;
using NUnit.Framework;

namespace Defend.Tests
{
    /// <summary>全部战局由正式世界命令建立；不写敌人、能源或内部记忆来制造预期结果。</summary>
    public sealed class PhaseFourEnemyBehaviorTests
    {
        private static BuildingDefinition Definition(BuildingKind kind, long production = 0,
            double range = 3.6, int health = 600, int width = 1, int depth = 1)
        {
            bool supplier = kind == BuildingKind.Reactor || kind == BuildingKind.Relay;
            return new BuildingDefinition(kind, kind.ToString(), width, depth, 20000, production, 0,
                supplier ? 20000 : 0, 1, supplier ? range : 0, health, 100, 50, 20);
        }

        private static GameWorld Create(EnemyKind[] pattern = null, int count = 1, double speed = 2,
            EnemyBehaviorSettings behavior = null, SpawnPoint[] terrain = null, SpawnPoint[] spawns = null,
            BuildingDefinition relay = null, double turretRange = 10, int turretDamage = 1,
            int enemyDamage = 10, int enemyHealth = 100000, int coreHealth = 100000,
            EnergySettings energy = null, WaveDefinition[] waves = null)
        {
            return new GameWorld(new[]
            {
                Definition(BuildingKind.Core, health: coreHealth),
                Definition(BuildingKind.Reactor, production: 2000),
                relay ?? Definition(BuildingKind.Relay, health: 120),
                Definition(BuildingKind.Turret), Definition(BuildingKind.BurstTurret), Definition(BuildingKind.AreaTurret)
            }, new WorldSettings(40, 40, 20, 100000), energy ?? new EnergySettings(200, 1, 1000, 0),
                new BattleSettings(waves ?? new[] { new WaveDefinition(count, enemyHealth, 1, 0,
                    pattern ?? new[] { EnemyKind.Heavy }) }, enemySpeedCellsPerSecond: speed,
                    enemyAttackDamage: enemyDamage, enemyAttackIntervalTicks: 20,
                    spawnPoints: spawns ?? new[] { new SpawnPoint(4, 20) }, blockedCells: terrain,
                    turretDefinitions: new[] { new TurretDefinition(BuildingKind.Turret, turretRange,
                        turretDamage, 2000, 4) }, enemyBehavior: behavior ?? new EnemyBehaviorSettings(enabled: true)));
        }

        private static BuildingState Place(GameWorld world, BuildingKind kind, int x, int z)
            => SimulationTestWorld.Place(world, kind, x, z);
        private static void Execute(GameWorld world, WorldCommand command) => SimulationTestWorld.Execute(world, command);
        private static void Advance(GameWorld world, int ticks) => SimulationTestWorld.Advance(world, ticks);
        private static void Until(GameWorld world, Func<bool> condition, int ticks, string message)
            => SimulationTestWorld.AdvanceUntil(world, condition, ticks, message);
        private static EnemyState Enemy(GameWorld world) => world.Battle.Enemies[0];

        private static GameWorld SuppliedFront(EnemyKind[] pattern = null, int count = 1, double speed = 2,
            EnemyBehaviorSettings behavior = null, SpawnPoint[] terrain = null, SpawnPoint[] spawns = null,
            double turretRange = 10, EnergySettings energy = null)
        {
            GameWorld world = Create(pattern, count, speed, behavior, terrain, spawns,
                turretRange: turretRange, energy: energy);
            Place(world, BuildingKind.Core, 34, 20);
            Place(world, BuildingKind.Reactor, 6, 24);
            Place(world, BuildingKind.Relay, 8, 22);
            Place(world, BuildingKind.Turret, 11, 22);
            return world;
        }

        private static BuildingState AcquireEnergyTarget(GameWorld world)
        {
            Execute(world, WorldCommand.StartWave());
            Until(world, () => Enemy(world).TargetPurpose == EnemyTargetPurpose.DisruptEnergy,
                160, "道路畅通时，重装应依据真实局部供给选择能源设施。");
            BuildingState target = world.FindBuilding(Enemy(world).TargetBuildingId);
            Assert.That(target, Is.Not.Null);
            Assert.That(target.Kind == BuildingKind.Reactor || target.Kind == BuildingKind.Relay, Is.True);
            return target;
        }

        [Test]
        public void BehaviorIsDisabledByDefaultAndLegacyHeavyKeepsCoreRoute()
        {
            var defaults = new BattleSettings();
            Assert.That(defaults.EnemyBehavior.Enabled, Is.False);
            GameWorld world = SuppliedFront(behavior: new EnemyBehaviorSettings());
            Execute(world, WorldCommand.StartWave());
            Advance(world, 100);
            Assert.That(Enemy(world).TargetBuildingId, Is.EqualTo(world.Battle.CoreBuildingId));
            Assert.That(world.Battle.BehaviorStatistics.TotalEvaluations, Is.Zero);
            Assert.That(world.Buildings.Single(item => item.IsTurret).ShotsFired, Is.GreaterThan(0));
        }

        [Test]
        public void NormalAndFastEnemiesKeepAdvancingWhileHeavyDisruptsSupply()
        {
            GameWorld world = SuppliedFront(new[] { EnemyKind.Normal, EnemyKind.Fast, EnemyKind.Heavy }, 3);
            Execute(world, WorldCommand.StartWave());
            Until(world, () => world.Battle.Enemies.Count == 3 && world.Battle.Enemies[2].TargetPurpose == EnemyTargetPurpose.DisruptEnergy,
                160, "混合敌群中的重装应主动转向供给设施。");
            foreach (EnemyState enemy in world.Battle.Enemies.Where(item => item.Kind != EnemyKind.Heavy))
            {
                Assert.That(enemy.TargetBuildingId, Is.EqualTo(world.Battle.CoreBuildingId));
                Assert.That(enemy.TargetPurpose, Is.EqualTo(EnemyTargetPurpose.AdvanceToCore));
                Assert.That(enemy.X, Is.GreaterThan(4.5));
            }
        }

        [Test]
        public void HeavyActuallyDamagesUsefulSupplyWithAnOpenCoreRoute()
        {
            GameWorld world = SuppliedFront();
            BuildingState target = AcquireEnergyTarget(world);
            Until(world, () => target.Health < target.Definition.MaximumHealth, 300,
                "能源攻击意图必须最终形成真实建筑伤害，不能只改变标记。");
            Assert.That(Enemy(world).LastAttackTick, Is.GreaterThan(0));
            Assert.That(world.FindBuilding(world.Battle.CoreBuildingId).Health, Is.EqualTo(100000));
            Assert.That(world.Buildings.Single(item => item.IsTurret).TotalReceived, Is.GreaterThan(0));
        }

        [Test]
        public void IdleSuppliersAndUnpoweredTurretsDoNotAttractHeavyByTypeAlone()
        {
            GameWorld world = Create(speed: 2);
            Place(world, BuildingKind.Core, 34, 20);
            Place(world, BuildingKind.Reactor, 5, 24);
            Place(world, BuildingKind.Relay, 7, 16);
            Place(world, BuildingKind.Turret, 10, 20);
            Execute(world, WorldCommand.StartWave());
            for (int tick = 0; tick < 100; tick++)
            {
                world.Step();
                Assert.That(Enemy(world).TargetBuildingId, Is.EqualTo(world.Battle.CoreBuildingId));
            }
            Assert.That(world.Buildings.Single(item => item.IsTurret).TotalReceived, Is.Zero);
        }

        [Test]
        public void ReservationsDoNotCreateObservedSupplyBeforeActualArrival()
        {
            GameWorld world = SuppliedFront(speed: 2, energy: new EnergySettings(200, 100, 1000, 0));
            Execute(world, WorldCommand.StartWave());
            Advance(world, 60);
            BuildingState turret = world.Buildings.Single(item => item.IsTurret);
            Assert.That(turret.CommittedIncoming, Is.GreaterThan(0), "战局应确实存在预约，才能覆盖预约与到货的差别。");
            Assert.That(turret.TotalReceived, Is.Zero);
            Assert.That(Enemy(world).TargetBuildingId, Is.EqualTo(world.Battle.CoreBuildingId));
            SimulationTestWorld.AssertBalanced(world);
        }

        [Test]
        public void EnemyDoesNotInheritCompletedTransfersFromBeforeItSpawned()
        {
            GameWorld world = SuppliedFront(speed: 2, turretRange: 0.1);
            Advance(world, 200);
            BuildingState turret = world.Buildings.Single(item => item.IsTurret);
            Assert.That(turret.TotalReceived, Is.GreaterThan(0));
            Assert.That(turret.StoredEnergy, Is.EqualTo(turret.Definition.EnergyCapacity));
            Execute(world, WorldCommand.StartWave());
            Advance(world, 80);
            Assert.That(turret.ShotsFired, Is.Zero);
            Assert.That(Enemy(world).TargetBuildingId, Is.EqualTo(world.Battle.CoreBuildingId));
        }

        [Test]
        public void FarAwayChangesDoNotRevealHiddenFacilitiesOrChangeLocalDecisions()
        {
            GameWorld baseline = SuppliedFront(speed: 2);
            GameWorld changed = SuppliedFront(speed: 2);
            BuildingState firstHidden = Place(baseline, BuildingKind.Reactor, 30, 35);
            BuildingState secondHidden = Place(changed, BuildingKind.Reactor, 30, 35);
            Execute(baseline, WorldCommand.StartWave());
            Execute(changed, WorldCommand.StartWave());
            Advance(baseline, 30); Advance(changed, 30);
            baseline.Enqueue(WorldCommand.Move(firstHidden.Id, 30, 35));
            changed.Enqueue(WorldCommand.Move(secondHidden.Id, 35, 35));
            for (int tick = 0; tick < 90; tick++)
            {
                baseline.Step(); changed.Step();
                Assert.That(Enemy(changed).TargetBuildingId, Is.EqualTo(Enemy(baseline).TargetBuildingId));
                Assert.That(Enemy(changed).TargetPurpose, Is.EqualTo(Enemy(baseline).TargetPurpose));
                Assert.That(Enemy(changed).X, Is.EqualTo(Enemy(baseline).X).Within(0.000001));
                Assert.That(Enemy(changed).Z, Is.EqualTo(Enemy(baseline).Z).Within(0.000001));
            }
        }

        [Test]
        public void IndividualHeavyEnemiesDoNotShareRemoteObservations()
        {
            GameWorld world = SuppliedFront(count: 2, speed: 2,
                spawns: new[] { new SpawnPoint(4, 20), new SpawnPoint(4, 2) });
            Execute(world, WorldCommand.StartWave());
            Until(world, () => world.Battle.Enemies.Count == 2 && world.Battle.Enemies[0].TargetPurpose == EnemyTargetPurpose.DisruptEnergy,
                120, "靠近供给线路的敌人应获得局部观测。");
            EnemyState distant = world.Battle.Enemies[1];
            Assert.That(distant.TargetBuildingId, Is.EqualTo(world.Battle.CoreBuildingId));
            Assert.That(distant.TargetPurpose, Is.EqualTo(EnemyTargetPurpose.AdvanceToCore));
            Assert.That(distant.LastBehaviorEvaluationTick, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void MovingTargetOutOfSensePreservesLastKnownPositionThenExpires()
        {
            GameWorld world = SuppliedFront(speed: 2);
            BuildingState target = AcquireEnergyTarget(world);
            double knownX = Enemy(world).TargetX, knownZ = Enemy(world).TargetZ;
            Execute(world, WorldCommand.Move(target.Id, 32, 35));
            EnemyState enemy = Enemy(world);
            Assert.That(enemy.TargetPurpose, Is.EqualTo(EnemyTargetPurpose.SearchLastKnownPosition));
            Assert.That(enemy.TargetX, Is.EqualTo(knownX));
            Assert.That(enemy.TargetZ, Is.EqualTo(knownZ));
            Assert.That(enemy.TargetX, Is.Not.EqualTo(target.CenterX));
            Advance(world, 80);
            Assert.That(enemy.TargetBuildingId, Is.Not.EqualTo(target.Id));
            Assert.That(enemy.TargetPurpose, Is.Not.EqualTo(EnemyTargetPurpose.SearchLastKnownPosition));
            Assert.That(enemy.X, Is.LessThan(10));
        }

        [Test]
        public void VisibleTargetMovementUpdatesKnownPositionWithoutAttackingOldCell()
        {
            GameWorld world = SuppliedFront(speed: 2);
            BuildingState target = AcquireEnergyTarget(world);
            Execute(world, WorldCommand.Move(target.Id, target.X, target.Z + 1));
            Until(world, () => Enemy(world).TargetBuildingId == target.Id &&
                    Math.Abs(Enemy(world).TargetX - target.CenterX) < 0.000001 &&
                    Math.Abs(Enemy(world).TargetZ - target.CenterZ) < 0.000001,
                20, "仍可感知的目标移动后，应更新实际已知位置。");
            Assert.That(Enemy(world).LastAttackTick, Is.LessThan(0));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DestructionOrSaleImmediatelyReleasesStrategicTarget(bool sell)
        {
            GameWorld world = SuppliedFront(speed: 2);
            BuildingState target = AcquireEnergyTarget(world);
            Execute(world, sell ? WorldCommand.Sell(target.Id) : WorldCommand.Damage(target.Id, target.Health));
            Assert.That(Enemy(world).TargetBuildingId, Is.Not.EqualTo(target.Id));
            Assert.That(target.IsAlive, Is.False);
            Assert.That(Enemy(world).TargetPurpose, Is.Not.EqualTo(EnemyTargetPurpose.SearchLastKnownPosition));
        }

        [Test]
        public void OrdinaryRepairPreservesAttackCooldownAndTargetLock()
        {
            GameWorld world = SuppliedFront();
            BuildingState target = AcquireEnergyTarget(world);
            Until(world, () => target.Health < target.Definition.MaximumHealth, 300, "重装应先完成一次真实攻击。");
            EnemyState enemy = Enemy(world);
            long cooldown = enemy.NextAttackTick, assignment = enemy.TargetAssignedTick, attack = enemy.LastAttackTick;
            int generation = target.LifeGeneration, credits = world.Credits;
            Execute(world, WorldCommand.Repair(target.Id));
            Assert.That(world.Credits, Is.EqualTo(credits - target.Definition.RepairCost));
            Assert.That(target.LifeGeneration, Is.EqualTo(generation));
            Assert.That(enemy.NextAttackTick, Is.EqualTo(cooldown));
            Assert.That(enemy.TargetAssignedTick, Is.EqualTo(assignment));
            Assert.That(enemy.LastAttackTick, Is.EqualTo(attack));
            while (world.CurrentTick < cooldown - 1) world.Step();
            Assert.That(target.Health, Is.EqualTo(target.Definition.MaximumHealth));
            world.Step();
            Assert.That(target.Health, Is.EqualTo(target.Definition.MaximumHealth - 30));
            Assert.That(enemy.LastAttackTick, Is.EqualTo(cooldown));
        }

        [Test]
        public void MinimumLockSurvivesLossOfTheLocalConsumer()
        {
            GameWorld world = SuppliedFront(speed: 2);
            BuildingState target = AcquireEnergyTarget(world);
            EnemyState enemy = Enemy(world);
            long assignment = enemy.TargetAssignedTick;
            BuildingState turret = world.Buildings.Single(item => item.IsTurret);
            Execute(world, WorldCommand.Sell(turret.Id));
            while (world.CurrentTick < assignment + 59)
            {
                world.Step();
                Assert.That(enemy.TargetBuildingId, Is.EqualTo(target.Id), "短暂失去供给活动不应中断有效目标的最短保持时间。");
                Assert.That(enemy.TargetAssignedTick, Is.EqualTo(assignment));
            }
        }

        [Test]
        public void RockEnclosedUsefulRelayIsNeverChosenAsReachableTarget()
        {
            var rocks = new[] { new SpawnPoint(7, 22), new SpawnPoint(9, 22), new SpawnPoint(8, 21), new SpawnPoint(8, 23) };
            GameWorld world = SuppliedFront(speed: 2, terrain: rocks);
            BuildingState relay = world.Buildings.Single(item => item.Kind == BuildingKind.Relay);
            Execute(world, WorldCommand.StartWave());
            for (int tick = 0; tick < 120; tick++)
            {
                world.Step();
                Assert.That(Enemy(world).TargetBuildingId, Is.Not.EqualTo(relay.Id));
                Assert.That(world.IsTerrainBlocked((int)Enemy(world).X, (int)Enemy(world).Z), Is.False);
            }
            Assert.That(relay.TotalReceived, Is.GreaterThan(0), "岩体不应阻断能源运输，候选必须确有供给价值。");
            Assert.That(world.Buildings.Single(item => item.IsTurret).ShotsFired, Is.GreaterThan(0));
        }

        [Test]
        public void ZeroAdditionalTravelBudgetRejectsOffRouteSupplyTargets()
        {
            GameWorld world = SuppliedFront(speed: 2,
                behavior: new EnemyBehaviorSettings(enabled: true, maximumAdditionalTravelCells: 0));
            Execute(world, WorldCommand.StartWave());
            for (int tick = 0; tick < 100; tick++)
            {
                world.Step();
                Assert.That(Enemy(world).TargetBuildingId, Is.EqualTo(world.Battle.CoreBuildingId));
            }
            Assert.That(world.Buildings.Single(item => item.IsTurret).TotalReceived, Is.GreaterThan(0));
        }

        [Test]
        public void MultiCellCompleteBarrierRetainsExistingObstacleAttack()
        {
            GameWorld world = Create(relay: Definition(BuildingKind.Relay, width: 2, depth: 40, health: 600));
            Place(world, BuildingKind.Core, 34, 20);
            BuildingState wall = Place(world, BuildingKind.Relay, 9, 0);
            Execute(world, WorldCommand.StartWave());
            Advance(world, 1);
            Assert.That(Enemy(world).TargetBuildingId, Is.EqualTo(wall.Id));
            Assert.That(Enemy(world).TargetPurpose, Is.EqualTo(EnemyTargetPurpose.ClearObstacle));
            Until(world, () => wall.Health < wall.Definition.MaximumHealth, 180, "多格封路建筑应能沿原有规则受到攻击。");
            Assert.That(Enemy(world).X, Is.LessThan(9));
        }

        [Test]
        public void AdjacentCoreOverridesSupplyAndLethalDamageStopsLaterEnemies()
        {
            GameWorld world = Create(count: 3, speed: 40, enemyDamage: 1000, coreHealth: 1000,
                spawns: new[] { new SpawnPoint(2, 20), new SpawnPoint(3, 20), new SpawnPoint(4, 20) });
            BuildingState core = Place(world, BuildingKind.Core, 5, 20);
            Execute(world, WorldCommand.StartWave());
            Advance(world, 2);
            Assert.That(world.Battle.Enemies.Count, Is.EqualTo(3));
            world.Step();
            Assert.That(world.Battle.State, Is.EqualTo(BattleState.Defeat));
            Assert.That(core.Health, Is.Zero);
            Assert.That(world.Battle.Enemies.Count(item => item.LastAttackTick == world.CurrentTick), Is.EqualTo(1));
            Assert.That(world.Battle.Enemies[0].TargetBuildingId, Is.EqualTo(core.Id));
            Assert.That(world.Battle.Enemies.Skip(1).All(item => item.NextAttackTick == 0), Is.True);
            int credits = world.Credits;
            world.Enqueue(WorldCommand.Repair(core.Id)); world.Step();
            Assert.That(world.LastCommandResult.Succeeded, Is.False);
            Assert.That(world.Credits, Is.EqualTo(credits));
            Assert.That(core.IsAlive, Is.False);
        }

        [Test]
        public void DestroyingSupplyDoesNotEraseConsumerCacheOrInventInstantOutage()
        {
            GameWorld world = SuppliedFront(speed: 2);
            Advance(world, 60);
            Execute(world, WorldCommand.StartWave());
            BuildingState turret = world.Buildings.Single(item => item.IsTurret);
            BuildingState relay = world.Buildings.Single(item => item.Kind == BuildingKind.Relay);
            Until(world, () => turret.ShotsFired > 0, 20, "炮塔应先由实际缓存开火。");
            long energy = turret.StoredEnergy, shots = turret.ShotsFired;
            Execute(world, WorldCommand.Damage(relay.Id, relay.Health));
            Assert.That(turret.StoredEnergy, Is.GreaterThanOrEqualTo(energy - 2000));
            Assert.That(turret.StoredEnergy, Is.GreaterThan(0));
            Until(world, () => turret.ShotsFired > shots, 8, "中继被毁后，炮塔仍应使用已有缓存射击。");
            SimulationTestWorld.AssertBalanced(world);
        }

        [Test]
        public void WaveTransitionPreservesEnergyAndDoesNotRepairDamagedBuildings()
        {
            var waves = new[] { new WaveDefinition(1, 1, 1, 10), new WaveDefinition(1, 1, 1, 10) };
            GameWorld world = Create(turretRange: 40, turretDamage: 10, waves: waves);
            Place(world, BuildingKind.Core, 34, 20);
            Place(world, BuildingKind.Reactor, 6, 24);
            BuildingState relay = Place(world, BuildingKind.Relay, 8, 22);
            BuildingState turret = Place(world, BuildingKind.Turret, 11, 22);
            Advance(world, 60);
            Execute(world, WorldCommand.Damage(relay.Id, 10));
            Execute(world, WorldCommand.StartWave());
            Until(world, () => world.Battle.State == BattleState.WaveCleared, 30, "首波应被真实炮塔射击结束。");
            long received = turret.TotalReceived, generated = world.Energy.GetTotals().Generated;
            Assert.That(turret.StoredEnergy, Is.GreaterThan(0));
            Execute(world, WorldCommand.StartWave());
            Assert.That(turret.TotalReceived, Is.GreaterThanOrEqualTo(received));
            Assert.That(world.Energy.GetTotals().Generated, Is.GreaterThanOrEqualTo(generated));
            Assert.That(turret.StoredEnergy, Is.GreaterThan(0));
            Assert.That(relay.Health, Is.EqualTo(relay.Definition.MaximumHealth - 10));
        }

        [Test]
        public void PauseFreezesObservationMemoryAndTargetLockInSimulationTime()
        {
            GameWorld world = SuppliedFront(speed: 2);
            BuildingState target = AcquireEnergyTarget(world);
            EnemyState enemy = Enemy(world);
            long tick = world.CurrentTick, assigned = enemy.TargetAssignedTick, evaluated = enemy.LastBehaviorEvaluationTick;
            var clock = new SimulationClock();
            Assert.That(clock.Advance(60, true, 1, world.Step), Is.Zero);
            Assert.That(world.CurrentTick, Is.EqualTo(tick));
            Assert.That(enemy.TargetBuildingId, Is.EqualTo(target.Id));
            Assert.That(enemy.TargetAssignedTick, Is.EqualTo(assigned));
            Assert.That(enemy.LastBehaviorEvaluationTick, Is.EqualTo(evaluated));
        }

        [Test]
        public void TwoHundredHeavyEnemiesRespectWorkBudgetsWithoutStarvingLateSpawns()
        {
            var behavior = new EnemyBehaviorSettings(enabled: true, maximumEvaluationsPerTick: 3,
                maximumPathBuildsPerTick: 2);
            GameWorld world = SuppliedFront(count: 201, speed: 0.01, behavior: behavior);
            Execute(world, WorldCommand.StartWave());
            for (int tick = 0; tick < 320; tick++)
            {
                world.Step();
                Assert.That(world.Battle.BehaviorStatistics.EvaluationsThisTick, Is.LessThanOrEqualTo(3));
                Assert.That(world.Battle.BehaviorStatistics.PathBuildsThisTick, Is.LessThanOrEqualTo(2));
                Assert.That(world.Battle.Enemies.Count, Is.LessThanOrEqualTo(200));
                if (tick % 20 == 0) SimulationTestWorld.AssertBalanced(world);
            }
            Assert.That(world.Battle.Enemies.Count, Is.EqualTo(200));
            Assert.That(world.Battle.RemainingToSpawn, Is.EqualTo(1));
            Assert.That(world.Battle.BehaviorStatistics.TotalDeferredEvaluations, Is.GreaterThan(0));
            foreach (EnemyState enemy in world.Battle.Enemies)
            {
                Assert.That(enemy.LastBehaviorEvaluationTick, Is.GreaterThanOrEqualTo(world.CurrentTick - 90),
                    "预算不足时应公平延期，不能让队尾敌人长期得不到评估。敌人身份：" + enemy.Id);
                Assert.That(enemy.X != 4.5 || enemy.Z != 20.5, Is.True, "延期评估不能停止已有导航行动。");
            }
            SimulationTestWorld.AssertBalanced(world);
        }

        [Test]
        public void RepeatedRemoteTopologyChangesDoNotFreezeEnemiesWhenPathBudgetIsOne()
        {
            GameWorld world = Create(count: 2,
                behavior: new EnemyBehaviorSettings(enabled: true, maximumPathBuildsPerTick: 1),
                spawns: new[] { new SpawnPoint(4, 8), new SpawnPoint(4, 30) });
            Place(world, BuildingKind.Core, 34, 20);
            Place(world, BuildingKind.Reactor, 6, 12);
            Place(world, BuildingKind.Relay, 8, 10);
            Place(world, BuildingKind.Turret, 11, 10);
            Place(world, BuildingKind.Reactor, 6, 34);
            Place(world, BuildingKind.Relay, 8, 32);
            Place(world, BuildingKind.Turret, 11, 32);
            BuildingState distant = Place(world, BuildingKind.Relay, 30, 37);
            Execute(world, WorldCommand.StartWave());
            Until(world, () => world.Battle.Enemies.Count == 2 && world.Battle.Enemies.All(enemy =>
                    enemy.TargetPurpose == EnemyTargetPurpose.DisruptEnergy),
                120, "两个独立前沿的重装都应发现自己的局部供给目标。");
            Assert.That(world.Battle.Enemies[0].TargetBuildingId,
                Is.Not.EqualTo(world.Battle.Enemies[1].TargetBuildingId));
            var positions = world.Battle.Enemies.Select(enemy => new[] { enemy.X, enemy.Z }).ToArray();
            for (int tick = 0; tick < 60; tick++)
            {
                Execute(world, WorldCommand.Move(distant.Id, tick % 2 == 0 ? 31 : 30, 37));
                Assert.That(world.Battle.BehaviorStatistics.PathBuildsThisTick, Is.LessThanOrEqualTo(1));
                foreach (EnemyState enemy in world.Battle.Enemies)
                    Assert.That(world.FindBuildingAt((int)enemy.X, (int)enemy.Z), Is.Null,
                        "延期不能借用失效路径穿过建筑占地。");
            }
            for (int index = 0; index < 2; index++)
            {
                EnemyState enemy = world.Battle.Enemies[index];
                double moved = Math.Abs(enemy.X - positions[index][0]) + Math.Abs(enemy.Z - positions[index][1]);
                Assert.That(moved, Is.GreaterThan(0.2), "远处持续改变拓扑时，各敌人仍应沿有效路线推进。");
            }
            SimulationTestWorld.AssertBalanced(world);
        }

        [Test]
        public void HeavySuppressesObservedFireWithoutKnowingTheHiddenPowerSource()
        {
            var world = new GameWorld(new[] { Definition(BuildingKind.Core, health: 100000),
                    Definition(BuildingKind.Reactor, production: 2000, range: 40),
                    Definition(BuildingKind.Turret, health: 120) },
                new WorldSettings(40, 40, 20, 10000), new EnergySettings(200, 1, 1000, 0),
                new BattleSettings(new[] { new WaveDefinition(1, 100000, 1, 0, new[] { EnemyKind.Heavy }) },
                    enemySpeedCellsPerSecond: 2, enemyAttackDamage: 10,
                    spawnPoints: new[] { new SpawnPoint(4, 20) },
                    turretDefinitions: new[] { new TurretDefinition(BuildingKind.Turret, 10, 20, 2000, 4) },
                    enemyBehavior: new EnemyBehaviorSettings(enabled: true)));
            Place(world, BuildingKind.Core, 34, 20);
            BuildingState source = Place(world, BuildingKind.Reactor, 25, 25);
            BuildingState turret = Place(world, BuildingKind.Turret, 8, 20);
            Execute(world, WorldCommand.StartWave());
            Until(world, () => Enemy(world).TargetPurpose == EnemyTargetPurpose.SuppressTurret,
                100, "真实的局部火力应能触发炮塔压制，不必先发现其远处电源。");
            Assert.That(turret.DamageDealt, Is.GreaterThan(0));
            Assert.That(Enemy(world).TargetBuildingId, Is.EqualTo(turret.Id));
            Assert.That(Enemy(world).TargetBuildingId, Is.Not.EqualTo(source.Id));
            Until(world, () => turret.Health < turret.Definition.MaximumHealth, 160,
                "压制目标应受到真实攻击。");
        }

        [Test]
        public void ObservingOnlyOneEndpointDoesNotRevealRemoteEnergyDeliveries()
        {
            var world = new GameWorld(new[] { Definition(BuildingKind.Core, health: 100000),
                    Definition(BuildingKind.Reactor, production: 2000, range: 40), Definition(BuildingKind.Turret) },
                new WorldSettings(40, 40, 20, 10000), new EnergySettings(200, 1, 1000, 0),
                new BattleSettings(new[] { new WaveDefinition(1, 100000, 1, 0, new[] { EnemyKind.Heavy }) },
                    enemySpeedCellsPerSecond: 2, spawnPoints: new[] { new SpawnPoint(4, 20) },
                    turretDefinitions: new[] { new TurretDefinition(BuildingKind.Turret, 0.1, 1, 2000, 4) },
                    enemyBehavior: new EnemyBehaviorSettings(enabled: true)));
            Place(world, BuildingKind.Core, 34, 20);
            Place(world, BuildingKind.Reactor, 6, 24);
            BuildingState distantTurret = Place(world, BuildingKind.Turret, 30, 35);
            Execute(world, WorldCommand.StartWave());
            for (int tick = 0; tick < 100; tick++)
            {
                world.Step();
                Assert.That(Enemy(world).TargetBuildingId, Is.EqualTo(world.Battle.CoreBuildingId));
            }
            Assert.That(distantTurret.TotalReceived, Is.GreaterThan(0), "隐藏端点必须真实到货，才能覆盖观测范围隔离。");
            Assert.That(world.Battle.BehaviorStatistics.TotalDeliveriesObserved, Is.Zero);
        }
    }
}
