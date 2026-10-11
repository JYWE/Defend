using System;
using Defend.Simulation;
using NUnit.Framework;

namespace Defend.Tests
{
    /// <summary>覆盖战斗与建筑操作、共享寻路及数量上限之间的边界，全部通过正式命令推进。</summary>
    public sealed class BattleBoundaryTests
    {
        private static BuildingDefinition Definition(BuildingKind kind, int width = 1, int depth = 1,
            long production = 0)
        {
            bool supplier = kind == BuildingKind.Reactor || kind == BuildingKind.Relay;
            return new BuildingDefinition(kind, kind.ToString(), width, depth, 100000, production, 0,
                supplier ? 100000 : 0, 1, supplier ? 30 : 0, 1000, 10, 5, 3);
        }

        private static GameWorld Create(int count = 1, double speed = 2, int attackDamage = 10,
            int attackInterval = 20, int cooldown = 8, int turretDamage = 20,
            long production = 0, BuildingDefinition relay = null, SpawnPoint[] spawns = null)
        {
            return new GameWorld(new[] {
                Definition(BuildingKind.Core), Definition(BuildingKind.Reactor, production: production),
                relay ?? Definition(BuildingKind.Relay), Definition(BuildingKind.Turret)
            }, new WorldSettings(16, 16, 20, 10000), new EnergySettings(200, 1, 1000, 0),
                new BattleSettings(new[] { new WaveDefinition(count, 10000, 1, 300) },
                    enemySpeedCellsPerSecond: speed, enemyAttackDamage: attackDamage,
                    enemyAttackIntervalTicks: attackInterval, turretRange: 30,
                    turretDamage: turretDamage, turretCooldownTicks: cooldown,
                    spawnPoints: spawns ?? new[] { new SpawnPoint(2, 8) }));
        }

        private static BuildingState Place(GameWorld world, BuildingKind kind, int x, int z) =>
            SimulationTestWorld.Place(world, kind, x, z);

        private static void Start(GameWorld world) => SimulationTestWorld.Execute(world, WorldCommand.StartWave());

        private static void ReachMovingSegment(GameWorld world)
        {
            SimulationTestWorld.AdvanceUntil(world, () => world.Battle.Enemies[0].X >= 3.59,
                100, "敌人应进入从第三列走向第四列的路段。");
            Assert.That(world.Battle.Enemies[0].X, Is.LessThan(3.8));
            Assert.That(world.Battle.Enemies[0].Z, Is.EqualTo(8.5).Within(0.0001));
        }

        private static void AssertRejectedWithoutPayment(GameWorld world, WorldCommand command)
        {
            int credits = world.Credits;
            world.Enqueue(command);
            world.Step();
            Assert.That(world.LastCommandResult.Succeeded, Is.False);
            Assert.That(world.LastCommandResult.Message, Does.Contain("敌人"));
            Assert.That(world.Credits, Is.EqualTo(credits));
            SimulationTestWorld.AssertBalanced(world);
        }

        [Test]
        public void ConstructionRejectsBothOccupiedAndNextMovementCell()
        {
            GameWorld world = Create();
            Place(world, BuildingKind.Core, 13, 8);
            Start(world);
            ReachMovingSegment(world);
            AssertRejectedWithoutPayment(world, WorldCommand.Place(BuildingKind.Relay, 3, 8));
            AssertRejectedWithoutPayment(world, WorldCommand.Place(BuildingKind.Relay, 4, 8));
            Assert.That(world.FindBuildingAt(3, 8), Is.Null);
            Assert.That(world.FindBuildingAt(4, 8), Is.Null);
            Assert.That(world.Buildings.Count, Is.EqualTo(1));
        }

        [Test]
        public void RejectedMoveIntoEnemyRoutePreservesPositionAndIdentity()
        {
            GameWorld world = Create();
            Place(world, BuildingKind.Core, 13, 8);
            BuildingState relay = Place(world, BuildingKind.Relay, 5, 5);
            Start(world);
            ReachMovingSegment(world);
            int positionRevision = relay.PositionRevision;
            AssertRejectedWithoutPayment(world, WorldCommand.Move(relay.Id, 4, 8));
            Assert.That(relay.X, Is.EqualTo(5));
            Assert.That(relay.Z, Is.EqualTo(5));
            Assert.That(relay.PositionRevision, Is.EqualTo(positionRevision));
            Assert.That(world.FindBuildingAt(5, 5), Is.SameAs(relay));
            Assert.That(world.FindBuildingAt(4, 8), Is.Null);
        }

        [Test]
        public void DestroyedBuildingCannotBeRepairedIntoEnemyRouteButCanAfterEnemyPasses()
        {
            GameWorld world = Create();
            Place(world, BuildingKind.Core, 13, 8);
            BuildingState relay = Place(world, BuildingKind.Relay, 4, 8);
            SimulationTestWorld.Execute(world, WorldCommand.Damage(relay.Id, 1000));
            Start(world);
            ReachMovingSegment(world);
            int generation = relay.LifeGeneration;
            AssertRejectedWithoutPayment(world, WorldCommand.Repair(relay.Id));
            Assert.That(relay.IsAlive, Is.False);
            Assert.That(relay.LifeGeneration, Is.EqualTo(generation));
            SimulationTestWorld.AdvanceUntil(world, () => world.Battle.Enemies[0].X > 5.6,
                100, "敌人应走出建筑恢复位置。");
            SimulationTestWorld.Execute(world, WorldCommand.Repair(relay.Id));
            Assert.That(relay.IsAlive, Is.True);
            Assert.That(relay.LifeGeneration, Is.EqualTo(generation + 1));
            Assert.That(world.FindBuildingAt(4, 8), Is.SameAs(relay));
        }

        [Test]
        public void DynamicConstructionUpdatesRouteWithoutWalkingThroughNewBuilding()
        {
            GameWorld world = Create(speed: 3);
            BuildingState core = Place(world, BuildingKind.Core, 13, 8);
            Start(world);
            SimulationTestWorld.AdvanceUntil(world, () => world.Battle.Enemies[0].X > 4.1,
                100, "敌人应接近中间路段。");
            int rebuilds = world.Battle.NavigationRebuildCount;
            BuildingState relay = Place(world, BuildingKind.Relay, 6, 8);
            Assert.That(world.Battle.NavigationRebuildCount, Is.EqualTo(rebuilds + 1));
            for (int index = 0; index < 200 && core.Health == 1000; index++)
            {
                world.Step();
                EnemyState enemy = world.Battle.Enemies[0];
                bool insideBuilding = enemy.X >= 6 && enemy.X < 7 && enemy.Z >= 8 && enemy.Z < 9;
                Assert.That(insideBuilding, Is.False, "动态建造后的敌人不得穿过建筑占地。");
                SimulationTestWorld.AssertBalanced(world);
            }
            Assert.That(core.Health, Is.LessThan(1000));
            Assert.That(relay.Health, Is.EqualTo(1000));
        }

        [Test]
        public void MovingTurretKeepsCooldownAndDoesNotProduceExtraShot()
        {
            const int cooldown = 20;
            GameWorld world = Create(speed: 0.1, production: 10000, cooldown: cooldown);
            Place(world, BuildingKind.Core, 13, 8);
            Place(world, BuildingKind.Reactor, 8, 10);
            BuildingState turret = Place(world, BuildingKind.Turret, 9, 10);
            SimulationTestWorld.Advance(world, 100);
            Start(world);
            SimulationTestWorld.AdvanceUntil(world, () => turret.ShotsFired == 1, 100, "炮塔应完成首次射击。");
            long nextShotTick = turret.NextShotTick;
            long id = turret.Id;
            SimulationTestWorld.Execute(world, WorldCommand.Move(id, 9, 11));
            Assert.That(turret.Id, Is.EqualTo(id));
            Assert.That(turret.NextShotTick, Is.EqualTo(nextShotTick));
            Assert.That(turret.ShotsFired, Is.EqualTo(1));
            while (world.CurrentTick < nextShotTick - 1) world.Step();
            Assert.That(turret.ShotsFired, Is.EqualTo(1));
            world.Step();
            Assert.That(turret.ShotsFired, Is.EqualTo(2));
            Assert.That(turret.NextShotTick, Is.EqualTo(nextShotTick + cooldown));
            SimulationTestWorld.AssertBalanced(world);
        }

        [Test]
        public void DestroyedTargetIsReplacedByCoreWithinSameStep()
        {
            GameWorld world = Create(relay: Definition(BuildingKind.Relay, 2, 16));
            BuildingState core = Place(world, BuildingKind.Core, 13, 8);
            BuildingState wall = Place(world, BuildingKind.Relay, 6, 0);
            Start(world);
            world.Step();
            Assert.That(world.Battle.Enemies[0].TargetBuildingId, Is.EqualTo(wall.Id));
            int rebuilds = world.Battle.NavigationRebuildCount;
            SimulationTestWorld.Execute(world, WorldCommand.Damage(wall.Id, 1000));
            Assert.That(world.Battle.Enemies[0].TargetBuildingId, Is.EqualTo(core.Id));
            Assert.That(world.Battle.NavigationRebuildCount, Is.EqualTo(rebuilds + 1));
            Assert.That(wall.Health, Is.Zero);
            SimulationTestWorld.AdvanceUntil(world, () => core.Health < 1000, 200,
                "目标被摧毁后应沿新路线抵达核心。");
        }

        [Test]
        public void SimultaneousCoreAttackStopsAtFirstLethalHitAndLocksDefeat()
        {
            GameWorld world = Create(count: 3, speed: 20, attackDamage: 1000,
                spawns: new[] { new SpawnPoint(10, 8), new SpawnPoint(11, 8), new SpawnPoint(12, 8) });
            BuildingState core = Place(world, BuildingKind.Core, 13, 8);
            Start(world);
            SimulationTestWorld.Advance(world, 2);
            Assert.That(world.Battle.Enemies.Count, Is.EqualTo(3));
            foreach (EnemyState enemy in world.Battle.Enemies)
            {
                Assert.That(enemy.X, Is.EqualTo(12.5).Within(0.0001));
                Assert.That(enemy.NextAttackTick, Is.Zero);
            }
            int credits = world.Credits;
            world.Step();
            Assert.That(core.Health, Is.Zero);
            Assert.That(world.Battle.State, Is.EqualTo(BattleState.Defeat));
            Assert.That(world.Battle.Enemies[0].NextAttackTick, Is.GreaterThan(world.CurrentTick));
            Assert.That(world.Battle.Enemies[1].NextAttackTick, Is.Zero);
            Assert.That(world.Battle.Enemies[2].NextAttackTick, Is.Zero);
            world.Enqueue(WorldCommand.Repair(core.Id));
            world.Enqueue(WorldCommand.StartWave());
            world.Step();
            Assert.That(world.LastCommandResult.Succeeded, Is.False);
            Assert.That(world.Credits, Is.EqualTo(credits));
            Assert.That(world.Battle.LastWaveReward, Is.Zero);
            Assert.That(core.IsAlive, Is.False);
        }

        [Test]
        public void TwoHundredLivingEnemiesRemainBoundedAndFreedSlotSpawnsOneReplacement()
        {
            GameWorld world = Create(count: 225, speed: 0.001, production: 10000,
                turretDamage: 10000, cooldown: 1000);
            Place(world, BuildingKind.Core, 13, 8);
            Start(world);
            SimulationTestWorld.Advance(world, 300);
            Assert.That(world.Battle.Enemies.Count, Is.EqualTo(200));
            Assert.That(world.Battle.RemainingToSpawn, Is.EqualTo(25));
            Assert.That(world.Battle.NavigationRebuildCount, Is.EqualTo(1));
            long previousId = 0;
            foreach (EnemyState enemy in world.Battle.Enemies)
            {
                Assert.That(enemy.Id, Is.GreaterThan(previousId));
                Assert.That(enemy.IsAlive, Is.True);
                Assert.That(double.IsNaN(enemy.X) || double.IsInfinity(enemy.X), Is.False);
                previousId = enemy.Id;
            }
            Place(world, BuildingKind.Reactor, 8, 5);
            Place(world, BuildingKind.Turret, 8, 6);
            SimulationTestWorld.AdvanceUntil(world, () => world.Battle.Kills == 1, 100,
                "供能后的炮塔应击败一个敌人。");
            Assert.That(world.Battle.Enemies.Count, Is.EqualTo(200));
            Assert.That(world.Battle.RemainingToSpawn, Is.EqualTo(24));
            Assert.That(world.Battle.Enemies[199].Id, Is.EqualTo(201));
            SimulationTestWorld.Advance(world, 10);
            Assert.That(world.Battle.Enemies.Count, Is.EqualTo(200));
            Assert.That(world.Battle.RemainingToSpawn, Is.EqualTo(24));
        }

        [Test]
        public void NavigationIsReusedUntilOccupancyChanges()
        {
            GameWorld world = Create(speed: 0.1);
            BuildingState core = Place(world, BuildingKind.Core, 13, 8);
            Start(world);
            SimulationTestWorld.Advance(world, 100);
            int rebuilds = world.Battle.NavigationRebuildCount;
            Assert.That(rebuilds, Is.EqualTo(1));
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(core.Id, 4));
            SimulationTestWorld.Execute(world, WorldCommand.Damage(core.Id, 1));
            SimulationTestWorld.Execute(world, WorldCommand.Repair(core.Id));
            SimulationTestWorld.Advance(world, 30);
            Assert.That(world.Battle.NavigationRebuildCount, Is.EqualTo(rebuilds),
                "优先级和存活建筑的生命值改变不应重建敌人距离场。");
            BuildingState relay = Place(world, BuildingKind.Relay, 5, 5);
            Assert.That(world.Battle.NavigationRebuildCount, Is.EqualTo(rebuilds + 1));
            SimulationTestWorld.Execute(world, WorldCommand.Move(relay.Id, 5, 6));
            Assert.That(world.Battle.NavigationRebuildCount, Is.EqualTo(rebuilds + 2));
            SimulationTestWorld.Advance(world, 30);
            Assert.That(world.Battle.NavigationRebuildCount, Is.EqualTo(rebuilds + 2));
        }
    }
}
