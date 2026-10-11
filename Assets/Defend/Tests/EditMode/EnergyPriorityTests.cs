using System;
using System.Linq;
using Defend.Simulation;
using NUnit.Framework;

namespace Defend.Tests
{
    public sealed class EnergyPriorityTests
    {
        [Test]
        public void SharedRelayBottleneckPreservesFourTwoOneEffectiveDeliveryShares()
        {
            var world = SimulationTestWorld.Create(
                reactor: SimulationTestWorld.Definition(BuildingKind.Reactor,
                    production: 7000, outputAmount: 7000, range: 2.1),
                relay: SimulationTestWorld.Definition(BuildingKind.Relay,
                    outputAmount: 3500, range: 3),
                energySettings: new EnergySettings(200, 1, 500, 0));
            var relay = SimulationTestWorld.Place(world, BuildingKind.Relay, 2, 2);
            var high = SimulationTestWorld.Place(world, BuildingKind.Turret, 4, 0);
            var normal = SimulationTestWorld.Place(world, BuildingKind.Turret, 4, 2);
            var low = SimulationTestWorld.Place(world, BuildingKind.Turret, 4, 4);
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(high.Id, 4));
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(normal.Id, 2));
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(low.Id, 1));
            var reactor = SimulationTestWorld.Place(world, BuildingKind.Reactor, 0, 2);

            Assert.That(world.Energy.Connections.Any(connection =>
                connection.SourceId == reactor.Id && connection.TargetId != relay.Id), Is.False,
                "三个终端必须共同经过这一个中继，不能被直达路线绕过瓶颈。");
            DrainConsumers(world, 200, high, normal, low);
            var highBefore = high.TotalReceived;
            var normalBefore = normal.TotalReceived;
            var lowBefore = low.TotalReceived;
            DrainConsumers(world, 1200, high, normal, low);
            var highReceived = high.TotalReceived - highBefore;
            var normalReceived = normal.TotalReceived - normalBefore;
            var lowReceived = low.TotalReceived - lowBefore;
            var total = highReceived + normalReceived + lowReceived;

            // 持续供给、持续需求、单一瓶颈场景至少利用 80% 输出能力。
            // 预留 20% 给批量、逻辑步边界和短暂调度空隙，不能让全部目标饿死后只比较份额。
            Assert.That(total, Is.GreaterThanOrEqualTo(1200L * 3500 * 8 / 10),
                $"中继应保持有效供给。高、中、低实际到货分别为 {highReceived}、{normalReceived}、{lowReceived}。");
            Assert.That((double)highReceived / total, Is.EqualTo(4.0 / 7).Within(0.03));
            Assert.That((double)normalReceived / total, Is.EqualTo(2.0 / 7).Within(0.03));
            Assert.That((double)lowReceived / total, Is.EqualTo(1.0 / 7).Within(0.03));
            Assert.That(lowReceived, Is.GreaterThan(0));
        }

        [Test]
        public void SharedRelayChargesUnequalLossBranchesByEffectiveDelivery()
            => AssertSharedRelayUnequalLossFairness(1200);

        [Test]
        public void SharedRelayKeepsUnequalLossFairnessForThirtySimulatedMinutes()
            => AssertSharedRelayUnequalLossFairness(36000);

        private static void AssertSharedRelayUnequalLossFairness(int measurementTicks)
        {
            var world = SimulationTestWorld.Create(
                reactor: SimulationTestWorld.Definition(BuildingKind.Reactor,
                    production: 9000, outputAmount: 9000, range: 3.1),
                relay: SimulationTestWorld.Definition(BuildingKind.Relay,
                    outputAmount: 3000, range: 3),
                energySettings: new EnergySettings(200, 1, 500, 0.5));
            var relay = SimulationTestWorld.Place(world, BuildingKind.Relay, 3, 3);
            var nearby = SimulationTestWorld.Place(world, BuildingKind.Turret, 4, 3);
            var distant = SimulationTestWorld.Place(world, BuildingKind.Turret, 6, 3);
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(nearby.Id, 1));
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(distant.Id, 1));
            var reactor = SimulationTestWorld.Place(world, BuildingKind.Reactor, 0, 3);

            Assert.That(world.Energy.Connections.Any(connection =>
                connection.SourceId == reactor.Id && connection.TargetId != relay.Id), Is.False);
            DrainConsumers(world, 200, nearby, distant);
            var nearbyBefore = nearby.TotalReceived;
            var distantBefore = distant.TotalReceived;
            var relayReceivedBefore = relay.TotalReceived;
            var relayStoredBefore = relay.StoredEnergy;
            var expiredBefore = world.Energy.GetTotals().ExpiredLoss;
            DrainConsumers(world, measurementTicks, nearby, distant);
            var nearbyReceived = nearby.TotalReceived - nearbyBefore;
            var distantReceived = distant.TotalReceived - distantBefore;
            var relayReceived = relay.TotalReceived - relayReceivedBefore;
            var expired = world.Energy.GetTotals().ExpiredLoss - expiredBefore;
            // 此场景只有中继可能发生运输滞留，且没有建筑销毁，可由存量变化反推实际输出。
            var relaySent = relayReceived + relayStoredBefore - relay.StoredEnergy - expired;
            var nearbyEfficiency = world.Energy.Connections.Single(connection =>
                connection.SourceId == relay.Id && connection.TargetId == nearby.Id).Efficiency;
            var distantEfficiency = world.Energy.Connections.Single(connection =>
                connection.SourceId == relay.Id && connection.TargetId == distant.Id).Efficiency;
            var idealEffectivePerTick = 2 * relay.Definition.OutputAmount /
                (1 / nearbyEfficiency + 1 / distantEfficiency);
            var minimumEffectiveDelivery = (long)(idealEffectivePerTick * measurementTicks * 0.8);

            Assert.That(distantReceived, Is.GreaterThan(0));
            Assert.That(relaySent, Is.GreaterThanOrEqualTo((long)measurementTicks * relay.Definition.OutputAmount * 8 / 10),
                $"共同中继至少应利用 80% 的原始输出能力，本窗口实际发出 {relaySent}。");
            Assert.That(nearbyReceived + distantReceived, Is.GreaterThanOrEqualTo(minimumEffectiveDelivery),
                "公平分配必须同时保持有效到货，不能靠全网停止供能获得接近的比例。");
            Assert.That((double)nearbyReceived / distantReceived, Is.EqualTo(1).Within(0.05),
                $"经过共同中继后的出口仲裁仍应按最终有效到货计费。近端到货 {nearbyReceived}，" +
                $"远端到货 {distantReceived}，中继收到 {relayReceived}，中继发出 {relaySent}，" +
                $"窗口内滞留损失 {expired}，窗口结束中继库存 {relay.StoredEnergy}。");
        }

        [Test]
        public void SharedSupplyFollowsFourTwoOneWeightsWithoutStarvingLowPriority()
        {
            var world = SimulationTestWorld.Create(
                reactor: SimulationTestWorld.Definition(BuildingKind.Reactor,
                    production: 7000, outputAmount: 7000, range: 8));
            var high = SimulationTestWorld.Place(world, BuildingKind.Turret, 3, 0);
            var normal = SimulationTestWorld.Place(world, BuildingKind.Turret, 0, 3);
            var low = SimulationTestWorld.Place(world, BuildingKind.Turret, 3, 3);
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(high.Id, 4));
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(normal.Id, 2));
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(low.Id, 1));
            SimulationTestWorld.Place(world, BuildingKind.Reactor, 0, 0);

            DrainConsumers(world, 100, high, normal, low);
            var highBefore = high.TotalReceived;
            var normalBefore = normal.TotalReceived;
            var lowBefore = low.TotalReceived;
            DrainConsumers(world, 1000, high, normal, low);

            var highReceived = high.TotalReceived - highBefore;
            var normalReceived = normal.TotalReceived - normalBefore;
            var lowReceived = low.TotalReceived - lowBefore;
            var total = highReceived + normalReceived + lowReceived;
            Assert.That(total, Is.GreaterThan(0));
            // 统计窗口覆盖 1000 次输出机会，允许批量和飞行边界造成 2 个百分点误差。
            Assert.That((double)highReceived / total, Is.EqualTo(4.0 / 7).Within(0.02));
            Assert.That((double)normalReceived / total, Is.EqualTo(2.0 / 7).Within(0.02));
            Assert.That((double)lowReceived / total, Is.EqualTo(1.0 / 7).Within(0.02));
            Assert.That(lowReceived, Is.GreaterThan(0));
        }

        [Test]
        public void FiniteSeventyUnitSupplyRespectsFortyUnitDemandCaps()
        {
            var world = SimulationTestWorld.Create(
                reactor: SimulationTestWorld.Definition(BuildingKind.Reactor, capacity: 70000,
                    production: 70000, outputAmount: 70000, range: 8),
                turret: SimulationTestWorld.Definition(BuildingKind.Turret, capacity: 40000),
                energySettings: new EnergySettings(100, 1, 10000, 0));
            var high = SimulationTestWorld.Place(world, BuildingKind.Turret, 3, 0);
            var normal = SimulationTestWorld.Place(world, BuildingKind.Turret, 0, 3);
            var low = SimulationTestWorld.Place(world, BuildingKind.Turret, 3, 3);
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(high.Id, 4));
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(normal.Id, 2));
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(low.Id, 1));
            var reactor = SimulationTestWorld.Place(world, BuildingKind.Reactor, 0, 0);
            SimulationTestWorld.Execute(world, WorldCommand.Damage(reactor.Id, 100));
            SimulationTestWorld.Advance(world, 15);

            Assert.That(high.TotalReceived, Is.EqualTo(40000));
            Assert.That(normal.TotalReceived, Is.EqualTo(20000));
            Assert.That(low.TotalReceived, Is.EqualTo(10000));
        }

        [Test]
        public void SatisfiedHighPriorityConsumerReleasesUnusedShareToOthers()
        {
            var world = SimulationTestWorld.Create(
                reactor: SimulationTestWorld.Definition(BuildingKind.Reactor, capacity: 70000,
                    production: 70000, outputAmount: 70000, range: 8),
                turret: SimulationTestWorld.Definition(BuildingKind.Turret, capacity: 40000),
                core: SimulationTestWorld.Definition(BuildingKind.Core, capacity: 10000),
                energySettings: new EnergySettings(100, 1, 10000, 0));
            var high = SimulationTestWorld.Place(world, BuildingKind.Core, 3, 0);
            var normal = SimulationTestWorld.Place(world, BuildingKind.Turret, 0, 3);
            var low = SimulationTestWorld.Place(world, BuildingKind.Turret, 3, 3);
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(high.Id, 4));
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(normal.Id, 2));
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(low.Id, 1));
            var reactor = SimulationTestWorld.Place(world, BuildingKind.Reactor, 0, 0);
            SimulationTestWorld.Execute(world, WorldCommand.Damage(reactor.Id, 100));
            SimulationTestWorld.Advance(world, 15);

            Assert.That(high.TotalReceived, Is.EqualTo(10000));
            Assert.That(normal.TotalReceived, Is.EqualTo(40000));
            Assert.That(low.TotalReceived, Is.EqualTo(20000));
        }

        [Test]
        public void EqualWeightsAreChargedByEffectiveDeliveryDespiteDifferentLosses()
        {
            var world = SimulationTestWorld.Create(
                reactor: SimulationTestWorld.Definition(BuildingKind.Reactor,
                    production: 3000, outputAmount: 3000, range: 10),
                energySettings: new EnergySettings(200, 1, 1000, 0.5));
            var nearby = SimulationTestWorld.Place(world, BuildingKind.Turret, 1, 0);
            var distant = SimulationTestWorld.Place(world, BuildingKind.Turret, 10, 0);
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(nearby.Id, 1));
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(distant.Id, 1));
            SimulationTestWorld.Place(world, BuildingKind.Reactor, 0, 0);

            DrainConsumers(world, 100, nearby, distant);
            var nearbyBefore = nearby.TotalReceived;
            var distantBefore = distant.TotalReceived;
            DrainConsumers(world, 1500, nearby, distant);
            var nearbyReceived = nearby.TotalReceived - nearbyBefore;
            var distantReceived = distant.TotalReceived - distantBefore;

            // 连续损耗曲线下两条边效率分别为 0.995 与 0.5。
            // 正确分配应让有效到货接近相等，不能按源头支出平分后使远端少收一半。
            Assert.That(distantReceived, Is.GreaterThan(0));
            Assert.That((double)nearbyReceived / distantReceived, Is.EqualTo(1).Within(0.03));
            Assert.That(world.Energy.GetTotals().TransmissionLoss, Is.GreaterThan(0));
        }

        [Test]
        public void PriorityChangesDuringOperationAffectSubsequentDeliveredShares()
        {
            var world = SimulationTestWorld.Create(
                reactor: SimulationTestWorld.Definition(BuildingKind.Reactor,
                    production: 5000, outputAmount: 5000, range: 8));
            var first = SimulationTestWorld.Place(world, BuildingKind.Turret, 3, 0);
            var second = SimulationTestWorld.Place(world, BuildingKind.Turret, 0, 3);
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(first.Id, 4));
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(second.Id, 1));
            SimulationTestWorld.Place(world, BuildingKind.Reactor, 0, 0);
            DrainConsumers(world, 500, first, second);
            Assert.That(first.TotalReceived, Is.GreaterThan(second.TotalReceived * 3));

            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(first.Id, 1));
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(second.Id, 4));
            DrainConsumers(world, 100, first, second);
            var firstBefore = first.TotalReceived;
            var secondBefore = second.TotalReceived;
            DrainConsumers(world, 500, first, second);

            var firstReceived = first.TotalReceived - firstBefore;
            var secondReceived = second.TotalReceived - secondBefore;
            Assert.That(firstReceived, Is.GreaterThan(0));
            Assert.That((double)secondReceived / firstReceived, Is.EqualTo(4).Within(0.2));
        }

        [Test]
        public void UnreachableHighPriorityConsumerDoesNotBlockReachableLowPriorityConsumer()
        {
            var world = SimulationTestWorld.Create(
                reactor: SimulationTestWorld.Definition(BuildingKind.Reactor,
                    production: 1000, outputAmount: 1000, range: 3));
            var unreachable = SimulationTestWorld.Place(world, BuildingKind.Turret, 30, 30);
            var reachable = SimulationTestWorld.Place(world, BuildingKind.Turret, 2, 0);
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(unreachable.Id, 4));
            SimulationTestWorld.Execute(world, WorldCommand.SetPriority(reachable.Id, 1));
            SimulationTestWorld.Place(world, BuildingKind.Reactor, 0, 0);
            SimulationTestWorld.Advance(world, 40);

            Assert.That(unreachable.TotalReceived, Is.Zero);
            Assert.That(unreachable.CommittedIncoming, Is.Zero);
            Assert.That(reachable.TotalReceived, Is.GreaterThan(30000));
        }

        private static void DrainConsumers(GameWorld world, int ticks, params BuildingState[] consumers)
        {
            for (var index = 0; index < ticks; index++)
            {
                foreach (var consumer in consumers)
                    world.Energy.ConsumeUpTo(consumer.Id, consumer.Definition.EnergyCapacity);
                world.Step();
                SimulationTestWorld.AssertBalanced(world);
            }
        }
    }
}
