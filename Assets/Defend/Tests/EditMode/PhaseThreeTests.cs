using System;
using System.Linq;
using Defend.Simulation;
using NUnit.Framework;

namespace Defend.Tests
{
    public sealed class PhaseThreeTests
    {
        private static BuildingDefinition Definition(BuildingKind kind, long production = 0, double range = 30, long output = 100000)
        {
            bool supplier = kind == BuildingKind.Reactor || kind == BuildingKind.Relay;
            return new BuildingDefinition(kind, kind.ToString(), 1, 1, 100000, production, 0,
                supplier ? output : 0, 1, supplier ? range : 0, 1000, 10, 5, 3);
        }
        private static GameWorld World(BattleSettings battle = null, long production = 10000)
            => new GameWorld(new[] { Definition(BuildingKind.Reactor, production), Definition(BuildingKind.Relay),
                Definition(BuildingKind.Turret), Definition(BuildingKind.BurstTurret), Definition(BuildingKind.AreaTurret), Definition(BuildingKind.Core) },
                new WorldSettings(20, 20, 20, 10000), new EnergySettings(200, 1, 1000, 0), battle ?? Settings());
        private static BattleSettings Settings(EnemyKind[] pattern = null, SpawnPoint[] terrain = null, TurretDefinition[] weapons = null)
            => new BattleSettings(new[] { new WaveDefinition(3, 500, 1, 10, pattern) }, enemySpeedCellsPerSecond: 0.1,
                spawnPoints: new[] { new SpawnPoint(0, 10) }, blockedCells: terrain, turretDefinitions: weapons);
        private static BuildingState Place(GameWorld world, BuildingKind kind, int x, int z) => SimulationTestWorld.Place(world, kind, x, z);

        [Test]
        public void AdditionalTurretsReceiveEnergyAndConsumeWholeShot()
        {
            foreach (var kind in new[] { BuildingKind.BurstTurret, BuildingKind.AreaTurret })
            {
                var world = World(Settings(weapons: new[] { new TurretDefinition(kind, 30, 30, 8000, 20) }));
                Place(world, BuildingKind.Core, 18, 10); Place(world, BuildingKind.Reactor, 5, 10);
                var turret = Place(world, kind, 6, 10);
                SimulationTestWorld.Advance(world, 100);
                Assert.That(turret.TotalReceived, Is.GreaterThan(0));
                long before = world.Energy.GetTotals().Consumed;
                SimulationTestWorld.Execute(world, WorldCommand.StartWave());
                SimulationTestWorld.AdvanceUntil(world, () => turret.ShotsFired > 0, 20, "炮塔没有发射。");
                Assert.That(world.Energy.GetTotals().Consumed - before, Is.EqualTo(8000));
                Assert.That(turret.DamageDealt, Is.EqualTo(30));
            }
        }

        [Test]
        public void AreaAttackHitsGroupedEnemiesButPaysOnceAndDoesNotCountOverkill()
        {
            var world = World(Settings(weapons: new[] { new TurretDefinition(BuildingKind.AreaTurret, 30, 600, 6000, 20, 3) }));
            Place(world, BuildingKind.Core, 18, 10); Place(world, BuildingKind.Reactor, 6, 10);
            SimulationTestWorld.Execute(world, WorldCommand.StartWave()); SimulationTestWorld.Advance(world, 8);
            var turret = Place(world, BuildingKind.AreaTurret, 5, 10);
            SimulationTestWorld.AdvanceUntil(world, () => world.Battle.IsFinished, 100, "范围攻击没有结束战斗。");
            Assert.That(world.Battle.Kills, Is.EqualTo(3)); Assert.That(turret.ShotsFired, Is.EqualTo(1));
            Assert.That(turret.DamageDealt, Is.EqualTo(1500)); Assert.That(world.Energy.GetTotals().Consumed, Is.EqualTo(6000));
        }

        [Test]
        public void EnemyPatternHasDifferentHealthAndMovementWithoutChangingWaveCount()
        {
            var world = World(Settings(new[] { EnemyKind.Normal, EnemyKind.Fast, EnemyKind.Heavy }));
            Place(world, BuildingKind.Core, 18, 10); SimulationTestWorld.Execute(world, WorldCommand.StartWave());
            SimulationTestWorld.Advance(world, 100);
            var enemies = world.Battle.Enemies;
            Assert.That(enemies.Count, Is.EqualTo(3));
            Assert.That(enemies[1].X, Is.GreaterThan(enemies[0].X));
            Assert.That(enemies[2].X, Is.LessThan(enemies[0].X));
            Assert.That(enemies[2].MaximumHealth, Is.EqualTo(enemies[0].MaximumHealth * 5));
            Assert.That(world.Battle.RemainingToSpawn, Is.Zero);
        }

        [Test]
        public void RockBlocksPlacementAndMovementWithoutBecomingAttackTarget()
        {
            var world = World(Settings(terrain: new[] { new SpawnPoint(6, 10), new SpawnPoint(6, 9) }));
            Place(world, BuildingKind.Core, 18, 10);
            Assert.That(world.PlacementIssue(BuildingKind.Turret, 6, 10), Is.Not.Null);
            world.Enqueue(WorldCommand.Place(BuildingKind.Turret, 6, 10)); world.Step();
            Assert.That(world.LastCommandResult.Succeeded, Is.False);
            SimulationTestWorld.Execute(world, WorldCommand.StartWave()); SimulationTestWorld.Advance(world, 1600);
            foreach (var enemy in world.Battle.Enemies) Assert.That(enemy.TargetBuildingId, Is.EqualTo(world.Battle.CoreBuildingId));
        }

        [Test]
        public void ReservationsDoNotAppearAsActualArrival()
        {
            var world = World(); Place(world, BuildingKind.Core, 18, 18);
            Place(world, BuildingKind.Reactor, 1, 1); var turret = Place(world, BuildingKind.Turret, 8, 1);
            Assert.That(turret.CommittedIncoming, Is.GreaterThan(0));
            Assert.That(turret.TotalReceived, Is.Zero); Assert.That(turret.ReceivedLastSecond, Is.Zero);
            SimulationTestWorld.Advance(world, 40);
            Assert.That(turret.TotalReceived, Is.GreaterThan(0));
        }

        [Test]
        public void SupplySeparatesDisconnectedGenerationShortageAndCharging()
        {
            var world = World(production: 1); Place(world, BuildingKind.Core, 18, 18);
            var turret = Place(world, BuildingKind.Turret, 8, 1);
            Assert.That(SupplyAssessment.Read(world, turret).Connected, Is.False);
            Place(world, BuildingKind.Reactor, 1, 1);
            var report = SupplyAssessment.Read(world, turret);
            Assert.That(report.Connected, Is.True); Assert.That(report.GenerationInsufficient, Is.True);
        }

        [Test]
        public void DestructionAndOutputStatisticsSurviveRepairAndSale()
        {
            var world = World(); Place(world, BuildingKind.Core, 18, 18);
            var turret = Place(world, BuildingKind.BurstTurret, 10, 10);
            SimulationTestWorld.Execute(world, WorldCommand.Damage(turret.Id, 1000));
            SimulationTestWorld.Execute(world, WorldCommand.Repair(turret.Id));
            SimulationTestWorld.Execute(world, WorldCommand.Damage(turret.Id, 1000));
            SimulationTestWorld.Execute(world, WorldCommand.Sell(turret.Id));
            Assert.That(turret.DestructionCount, Is.EqualTo(2));
            Assert.That(world.Buildings.Contains(turret), Is.True);
        }

        [Test]
        public void CongestionDiagnosticRequiresActualStalledRelayEnergy()
        {
            var world = new GameWorld(new[] { Definition(BuildingKind.Reactor, 2000, 3),
                Definition(BuildingKind.Relay, 0, 3, 1), Definition(BuildingKind.Turret), Definition(BuildingKind.Core) },
                new WorldSettings(20,20,20,10000), new EnergySettings(200,1,1000,0), Settings());
            Place(world, BuildingKind.Core,18,18); Place(world, BuildingKind.Reactor,1,1);
            var relay = Place(world, BuildingKind.Relay,3,1); var turret = Place(world, BuildingKind.Turret,6,1);
            SimulationTestWorld.AdvanceUntil(world, () => world.Energy.HasStalledEnergy(relay.Id), 500, "低吞吐中继没有形成可观察滞留。");
            var report = SupplyAssessment.Read(world, turret);
            Assert.That(report.Connected, Is.True); Assert.That(report.RelayCongested, Is.True);
            Assert.That(report.Summary, Does.Contain("中继拥堵"));
        }
    }
}
