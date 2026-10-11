using System;
using Defend.Simulation;
using NUnit.Framework;

namespace Defend.Tests
{
    /// <summary>
    /// 通过正式命令构建测试世界。测试不直接写入储量、建筑状态或内部运输列表，
    /// 因而能够覆盖命令、生命周期与能源模块之间的真实边界。
    /// </summary>
    internal static class SimulationTestWorld
    {
        internal static BuildingDefinition Definition(
            BuildingKind kind,
            long capacity = 100000,
            long production = 0,
            long consumption = 0,
            long outputAmount = 100000,
            int outputInterval = 1,
            double range = 3,
            int width = 1,
            int depth = 1)
        {
            var isConsumer = kind == BuildingKind.Turret || kind == BuildingKind.Core;
            return new BuildingDefinition(kind, kind.ToString(), width, depth,
                capacity, production, consumption, isConsumer ? 0 : outputAmount, outputInterval,
                isConsumer ? 0 : range, 100, 100, 50, 20);
        }

        internal static GameWorld Create(
            BuildingDefinition reactor = null,
            BuildingDefinition relay = null,
            BuildingDefinition turret = null,
            BuildingDefinition core = null,
            EnergySettings energySettings = null,
            int credits = 10000)
        {
            var world = new GameWorld(new[]
            {
                reactor ?? Definition(BuildingKind.Reactor),
                relay ?? Definition(BuildingKind.Relay),
                turret ?? Definition(BuildingKind.Turret),
                core ?? Definition(BuildingKind.Core)
            }, new WorldSettings(40, 40, 20, credits),
                energySettings ?? new EnergySettings(200, 1, 1000, 0));
            world.Enqueue(WorldCommand.SetTestLoad(false));
            return world;
        }

        internal static BuildingState Place(GameWorld world, BuildingKind kind, int x, int z)
        {
            Execute(world, WorldCommand.Place(kind, x, z));
            var building = world.FindBuilding(world.LastCommandResult.BuildingId);
            Assert.That(building, Is.Not.Null, "成功放置后必须能通过返回的建筑身份找到建筑。");
            return building;
        }

        internal static void Execute(GameWorld world, WorldCommand command)
        {
            var sequence = world.Enqueue(command);
            world.Step();
            Assert.That(world.LastCommandResult.Sequence, Is.EqualTo(sequence));
            Assert.That(world.LastCommandResult.Succeeded, Is.True, world.LastCommandResult.Message);
            world.Energy.AssertInvariants();
        }

        internal static void Advance(GameWorld world, int ticks)
        {
            for (var index = 0; index < ticks; index++)
            {
                world.Step();
                AssertBalanced(world);
            }
        }

        internal static void AdvanceUntil(GameWorld world, Func<bool> condition, int maximumTicks, string message)
        {
            for (var index = 0; index < maximumTicks && !condition(); index++)
            {
                world.Step();
                AssertBalanced(world);
            }
            Assert.That(condition(), Is.True, message);
        }

        internal static void AssertBalanced(GameWorld world)
        {
            world.Energy.AssertInvariants();
            var totals = world.Energy.GetTotals();
            Assert.That(totals.BalanceError, Is.Zero, "权威账本必须逐步守恒。");
            Assert.That(totals.Generated,
                Is.EqualTo(totals.Stored + totals.InFlight + totals.Consumed +
                    totals.TransmissionLoss + totals.DestructionLoss + totals.ExpiredLoss),
                "预约不是实际能量，所有损失必须且只能计入一次。");

            foreach (var building in world.Buildings)
            {
                Assert.That(building.StoredEnergy, Is.GreaterThanOrEqualTo(0));
                Assert.That(building.ReservedIncoming, Is.GreaterThanOrEqualTo(0));
                Assert.That(building.CommittedIncoming, Is.GreaterThanOrEqualTo(0));
                var incoming = building.IsConsumer
                    ? building.CommittedIncoming
                    : building.ReservedIncoming;
                Assert.That(building.StoredEnergy + incoming,
                    Is.LessThanOrEqualTo(building.Definition.EnergyCapacity),
                    "储量与对应的预约之和不能超过容量。");
            }
        }
    }
}
