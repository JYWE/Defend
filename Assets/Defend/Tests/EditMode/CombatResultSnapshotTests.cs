using System.Linq;
using Defend.Simulation;
using NUnit.Framework;

namespace Defend.Tests
{
    public sealed class CombatResultSnapshotTests
    {
        [Test]
        public void RepairAndDestructionAreRecordedOnceAndClearedNextStep()
        {
            var world = SimulationTestWorld.Create();
            var relay = SimulationTestWorld.Place(world, BuildingKind.Relay, 2, 2);
            SimulationTestWorld.Execute(world, WorldCommand.Damage(relay.Id, 1000));
            Assert.That(world.Events.Count(e => e.Kind == WorldEventKind.BuildingDestroyed), Is.EqualTo(1));
            SimulationTestWorld.Execute(world, WorldCommand.Repair(relay.Id));
            Assert.That(world.Events.Count(e => e.Kind == WorldEventKind.Repair), Is.EqualTo(1));
            Assert.That(world.Events.Any(e => e.Kind == WorldEventKind.BuildingDestroyed), Is.False);
            world.Step(); Assert.That(world.Events, Is.Empty);
            world.Enqueue(WorldCommand.Repair(relay.Id)); world.Step();
            Assert.That(world.LastCommandResult.Succeeded, Is.False); Assert.That(world.Events, Is.Empty);
        }

        [Test]
        public void ShieldBreakUsesDamageSettlementEvenWhenEnergyArrivesInSameStep()
        {
            var world = SimulationTestWorld.Create(reactor: SimulationTestWorld.Definition(BuildingKind.Reactor, production: 10000));
            var core = SimulationTestWorld.Place(world, BuildingKind.Core, 2, 2);
            SimulationTestWorld.Place(world, BuildingKind.Reactor, 1, 2);
            SimulationTestWorld.AdvanceUntil(world, () => core.StoredEnergy > 0, 10, "没有实际到货。");
            SimulationTestWorld.Execute(world, WorldCommand.Damage(core.Id, 1000));
            Assert.That(world.Events.Count(e => e.Kind == WorldEventKind.ShieldHit), Is.EqualTo(1));
            Assert.That(world.Events.Count(e => e.Kind == WorldEventKind.ShieldBreak), Is.EqualTo(1));
            Assert.That(world.Events.Count(e => e.Kind == WorldEventKind.BuildingDestroyed), Is.EqualTo(1));
        }

        [Test]
        public void ReservationIsNotDeliveryAndEverySuccessfulHopHasActualAmount()
        {
            var world = SimulationTestWorld.Create(reactor: SimulationTestWorld.Definition(BuildingKind.Reactor, production: 1000, range: 2.1),
                relay: SimulationTestWorld.Definition(BuildingKind.Relay, range: 2.1));
            var relay = SimulationTestWorld.Place(world, BuildingKind.Relay, 2, 0);
            var turret = SimulationTestWorld.Place(world, BuildingKind.Turret, 4, 0);
            SimulationTestWorld.Place(world, BuildingKind.Reactor, 0, 0);
            Assert.That(world.Energy.Departures.Count, Is.GreaterThan(0));
            Assert.That(world.Energy.Deliveries, Is.Empty);
            long delivered = 0;
            for (int i = 0; i < 30; i++)
            {
                world.Step();
                foreach (var item in world.Energy.Deliveries)
                {
                    Assert.That(item.ArrivalTick, Is.EqualTo(world.CurrentTick));
                    Assert.That(item.Amount, Is.GreaterThan(0));
                    if (item.TargetId == turret.Id) delivered += item.Amount;
                }
            }
            Assert.That(delivered, Is.EqualTo(turret.TotalReceived));
            Assert.That(relay.TotalReceived, Is.GreaterThan(0));
            SimulationTestWorld.AssertBalanced(world);
        }

        [Test]
        public void MovingReceiverAtArrivalNeverCreatesAFalseDelivery()
        {
            var world = SimulationTestWorld.Create(reactor: SimulationTestWorld.Definition(BuildingKind.Reactor, production: 1000));
            var turret = SimulationTestWorld.Place(world, BuildingKind.Turret, 2, 0);
            SimulationTestWorld.Place(world, BuildingKind.Reactor, 0, 0);
            var flight = world.Energy.Departures.First();
            while (world.CurrentTick < flight.ArrivalTick - 1) world.Step();
            SimulationTestWorld.Execute(world, WorldCommand.Move(turret.Id, 20, 20));
            Assert.That(world.Energy.Deliveries.Any(d => d.Id == flight.Id), Is.False);
            Assert.That(turret.TotalReceived, Is.Zero);
        }

        [Test]
        public void DestroyAndRepairCannotReviveOldIncomingDelivery()
        {
            var world = SimulationTestWorld.Create(reactor: SimulationTestWorld.Definition(BuildingKind.Reactor, production: 1000),
                energySettings: new EnergySettings(200, 10, 1000, 0));
            var turret = SimulationTestWorld.Place(world, BuildingKind.Turret, 2, 0);
            SimulationTestWorld.Place(world, BuildingKind.Reactor, 0, 0);
            var flight = world.Energy.Departures.First();
            SimulationTestWorld.Execute(world, WorldCommand.Damage(turret.Id, 1000));
            SimulationTestWorld.Execute(world, WorldCommand.Repair(turret.Id));
            while (world.CurrentTick <= flight.ArrivalTick)
            {
                world.Step();
                Assert.That(world.Energy.Deliveries.Any(d => d.Id == flight.Id), Is.False);
            }
            SimulationTestWorld.AssertBalanced(world);
        }

        [Test]
        public void AreaHitRecordsEachVictimBeforeDeadEnemiesAreRemoved()
        {
            var definitions = new[] { BuildingKind.Reactor, BuildingKind.Core, BuildingKind.AreaTurret }.Select(kind =>
                new BuildingDefinition(kind, kind.ToString(), 1, 1, 100000, kind == BuildingKind.Reactor ? 10000 : 0, 0,
                    kind == BuildingKind.Reactor ? 100000 : 0, 1, 30, 1000, 10, 5, 3));
            var settings = new BattleSettings(new[] { new WaveDefinition(3, 500, 1, 0) }, enemySpeedCellsPerSecond: .1,
                spawnPoints: new[] { new SpawnPoint(0, 10) }, turretDefinitions: new[] { new TurretDefinition(BuildingKind.AreaTurret, 30, 600, 6000, 20, 3) });
            var world = new GameWorld(definitions, new WorldSettings(20, 20, 20, 10000), new EnergySettings(200, 1, 1000, 0), settings);
            SimulationTestWorld.Place(world, BuildingKind.Core, 18, 10); SimulationTestWorld.Place(world, BuildingKind.Reactor, 6, 10);
            SimulationTestWorld.Execute(world, WorldCommand.StartWave()); SimulationTestWorld.Advance(world, 8);
            SimulationTestWorld.Place(world, BuildingKind.AreaTurret, 5, 10);
            SimulationTestWorld.AdvanceUntil(world, () => world.Battle.IsFinished, 100, "范围攻击未结算。");
            Assert.That(world.Battle.Enemies, Is.Empty);
            Assert.That(world.Events.Count(e => e.Kind == WorldEventKind.EnemyDeath), Is.EqualTo(3));
            Assert.That(world.Events.Where(e => e.Kind == WorldEventKind.EnemyHit).Sum(e => e.Amount), Is.EqualTo(1500));
        }
    }
}
