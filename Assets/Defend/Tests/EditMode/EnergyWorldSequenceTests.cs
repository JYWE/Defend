using System;
using System.Linq;
using Defend.Simulation;
using NUnit.Framework;

namespace Defend.Tests
{
    public sealed class EnergyWorldSequenceTests
    {
        [Test]
        public void CoreUsesFiniteShieldBeforeHealthAndRepairDoesNotRefillShield()
        {
            var world = SimulationTestWorld.Create(
                reactor: SimulationTestWorld.Definition(BuildingKind.Reactor, capacity: 2500,
                    production: 2500, outputAmount: 2500, range: 3),
                core: SimulationTestWorld.Definition(BuildingKind.Core, capacity: 2500));
            var core = SimulationTestWorld.Place(world, BuildingKind.Core, 2, 0);
            var reactor = SimulationTestWorld.Place(world, BuildingKind.Reactor, 0, 0);
            SimulationTestWorld.Execute(world, WorldCommand.Damage(reactor.Id, 100));
            SimulationTestWorld.AdvanceUntil(world, () => core.StoredEnergy == 2500, 10,
                "核心先收到有上限的护盾储量。");

            SimulationTestWorld.Execute(world, WorldCommand.Damage(core.Id, 2));
            Assert.That(core.Health, Is.EqualTo(100));
            Assert.That(core.StoredEnergy, Is.EqualTo(500));
            SimulationTestWorld.Execute(world, WorldCommand.Damage(core.Id, 1));
            Assert.That(core.Health, Is.EqualTo(99));
            Assert.That(core.StoredEnergy, Is.Zero);
            SimulationTestWorld.Execute(world, WorldCommand.Repair(core.Id));
            Assert.That(core.Health, Is.EqualTo(100));
            Assert.That(core.StoredEnergy, Is.Zero,
                "修复生命值不能凭空补充核心护盾。");
            Assert.That(world.Energy.GetTotals().Consumed, Is.EqualTo(2500));
        }

        [Test]
        public void IdenticalSeededCommandSequencesProduceEqualFieldsAndConserveEnergy()
        {
            var first = CreateSequenceWorld();
            var second = CreateSequenceWorld();
            var random = new Random(102410);

            for (var stepIndex = 0; stepIndex < 220; stepIndex++)
            {
                var command = ChooseCommand(first, random);
                first.Enqueue(command);
                second.Enqueue(command);
                first.Step();
                second.Step();
                SimulationTestWorld.AssertBalanced(first);
                SimulationTestWorld.AssertBalanced(second);
                CompareWorldFields(first, second);
            }

            Assert.That(first.Energy.GetTotals().Generated, Is.GreaterThan(0));
            Assert.That(first.Energy.GetTotals().TransmissionLoss, Is.GreaterThan(0));
        }

        private static GameWorld CreateSequenceWorld()
        {
            var world = SimulationTestWorld.Create(
                reactor: SimulationTestWorld.Definition(BuildingKind.Reactor,
                    production: 2048, outputAmount: 4096, range: 6),
                relay: SimulationTestWorld.Definition(BuildingKind.Relay,
                    outputAmount: 3000, outputInterval: 3, range: 6),
                turret: SimulationTestWorld.Definition(BuildingKind.Turret,
                    capacity: 10000, consumption: 1000),
                energySettings: new EnergySettings(25, 1, 1000, 0.35), credits: 20000);
            world.Enqueue(WorldCommand.SetTestLoad(true));
            world.Enqueue(WorldCommand.Place(BuildingKind.Reactor, 2, 2));
            world.Enqueue(WorldCommand.Place(BuildingKind.Reactor, 2, 8));
            world.Enqueue(WorldCommand.Place(BuildingKind.Relay, 6, 2));
            world.Enqueue(WorldCommand.Place(BuildingKind.Relay, 6, 8));
            world.Enqueue(WorldCommand.Place(BuildingKind.Turret, 10, 2));
            world.Enqueue(WorldCommand.Place(BuildingKind.Turret, 10, 8));
            world.Step();
            SimulationTestWorld.Advance(world, 15);
            return world;
        }

        private static WorldCommand ChooseCommand(GameWorld world, Random random)
        {
            var building = world.Buildings[random.Next(world.Buildings.Count)];
            switch (random.Next(7))
            {
                case 0: return WorldCommand.SetPriority(building.Id, 1 << random.Next(3));
                case 1: return WorldCommand.Damage(building.Id, random.Next(1, 130));
                case 2: return WorldCommand.Repair(building.Id);
                case 3: return WorldCommand.Move(building.Id, random.Next(0, 15), random.Next(0, 12));
                case 4: return WorldCommand.Sell(building.Id);
                case 5: return WorldCommand.Place((BuildingKind)random.Next(3), random.Next(0, 15), random.Next(0, 12));
                default: return WorldCommand.SetTestLoad(random.Next(2) == 0);
            }
        }

        private static void CompareWorldFields(GameWorld first, GameWorld second)
        {
            // 逐字段核对可定位差异；不生成或比较任何状态哈希。
            Assert.That(second.CurrentTick, Is.EqualTo(first.CurrentTick));
            Assert.That(second.Credits, Is.EqualTo(first.Credits));
            Assert.That(second.LastCommandResult.Succeeded, Is.EqualTo(first.LastCommandResult.Succeeded));
            Assert.That(second.Buildings.Count, Is.EqualTo(first.Buildings.Count));
            for (var index = 0; index < first.Buildings.Count; index++)
            {
                var expected = first.Buildings[index];
                var actual = second.Buildings[index];
                Assert.That(actual.Id, Is.EqualTo(expected.Id));
                Assert.That(actual.Kind, Is.EqualTo(expected.Kind));
                Assert.That(actual.X, Is.EqualTo(expected.X));
                Assert.That(actual.Z, Is.EqualTo(expected.Z));
                Assert.That(actual.Health, Is.EqualTo(expected.Health));
                Assert.That(actual.IsAlive, Is.EqualTo(expected.IsAlive));
                Assert.That(actual.IsSold, Is.EqualTo(expected.IsSold));
                Assert.That(actual.LifeGeneration, Is.EqualTo(expected.LifeGeneration));
                Assert.That(actual.PositionRevision, Is.EqualTo(expected.PositionRevision));
                Assert.That(actual.PriorityWeight, Is.EqualTo(expected.PriorityWeight));
                Assert.That(actual.StoredEnergy, Is.EqualTo(expected.StoredEnergy));
                Assert.That(actual.ReservedIncoming, Is.EqualTo(expected.ReservedIncoming));
                Assert.That(actual.CommittedIncoming, Is.EqualTo(expected.CommittedIncoming));
                Assert.That(actual.TotalReceived, Is.EqualTo(expected.TotalReceived));
            }
            var expectedTotals = first.Energy.GetTotals();
            var actualTotals = second.Energy.GetTotals();
            Assert.That(actualTotals.Generated, Is.EqualTo(expectedTotals.Generated));
            Assert.That(actualTotals.Consumed, Is.EqualTo(expectedTotals.Consumed));
            Assert.That(actualTotals.TransmissionLoss, Is.EqualTo(expectedTotals.TransmissionLoss));
            Assert.That(actualTotals.DestructionLoss, Is.EqualTo(expectedTotals.DestructionLoss));
            Assert.That(actualTotals.ExpiredLoss, Is.EqualTo(expectedTotals.ExpiredLoss));
            Assert.That(actualTotals.InFlight, Is.EqualTo(expectedTotals.InFlight));
            Assert.That(actualTotals.Stored, Is.EqualTo(expectedTotals.Stored));
            Assert.That(actualTotals.ActiveBatchCount, Is.EqualTo(expectedTotals.ActiveBatchCount));
            var expectedBatches = first.Energy.BatchDiagnostics.OrderBy(batch => batch.Id).ToArray();
            var actualBatches = second.Energy.BatchDiagnostics.OrderBy(batch => batch.Id).ToArray();
            Assert.That(actualBatches.Length, Is.EqualTo(expectedBatches.Length));
            for (var index = 0; index < expectedBatches.Length; index++)
            {
                Assert.That(actualBatches[index].Id, Is.EqualTo(expectedBatches[index].Id));
                Assert.That(actualBatches[index].BuildingId, Is.EqualTo(expectedBatches[index].BuildingId));
                Assert.That(actualBatches[index].DestinationId, Is.EqualTo(expectedBatches[index].DestinationId));
                Assert.That(actualBatches[index].Amount, Is.EqualTo(expectedBatches[index].Amount));
                Assert.That(actualBatches[index].StalledTicks, Is.EqualTo(expectedBatches[index].StalledTicks));
                CollectionAssert.AreEqual(expectedBatches[index].History, actualBatches[index].History);
            }
        }
    }
}
