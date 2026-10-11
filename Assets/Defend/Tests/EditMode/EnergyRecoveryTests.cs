using System.Linq;
using Defend.Simulation;
using NUnit.Framework;

namespace Defend.Tests
{
    public sealed class EnergyRecoveryTests
    {
        [Test]
        public void PersistentOutputCongestionAllowsAlternativeBeforeRelayStorageIsFull()
        {
            var world = SimulationTestWorld.Create(
                reactor: SimulationTestWorld.Definition(BuildingKind.Reactor,
                    production: 1000, outputAmount: 1000, range: 6),
                relay: SimulationTestWorld.Definition(BuildingKind.Relay, capacity: 1000000,
                    outputAmount: 1000, outputInterval: 100, range: 6),
                turret: SimulationTestWorld.Definition(BuildingKind.Turret, capacity: 1000000),
                energySettings: new EnergySettings(300, 1, 10000, 0.5));
            var relay = SimulationTestWorld.Place(world, BuildingKind.Relay, 3, 0);
            var turret = SimulationTestWorld.Place(world, BuildingKind.Turret, 6, 0);
            var reactor = SimulationTestWorld.Place(world, BuildingKind.Reactor, 0, 0);
            SimulationTestWorld.AdvanceUntil(world,
                () => world.Energy.BatchDiagnostics.Any(batch =>
                    batch.BuildingId == relay.Id && batch.StalledTicks >= 5),
                130, "缓存未满的慢中继也会在错失输出机会后进入持续拥堵。");
            Assert.That(relay.StoredEnergy + relay.ReservedIncoming,
                Is.LessThan(relay.Definition.EnergyCapacity / 2),
                "本例必须排除已满容量触发备用路线的因素。");

            var usedDirectAlternative = false;
            for (var index = 0; index < 20; index++)
            {
                world.Step();
                SimulationTestWorld.AssertBalanced(world);
                usedDirectAlternative |= world.Energy.Transfers.Any(transfer =>
                    transfer.SourceId == reactor.Id && transfer.TargetId == turret.Id);
            }
            Assert.That(usedDirectAlternative, Is.True,
                "已经持续拥堵的低损耗通路不能继续吸收全部新供给，源头应允许选择更高损耗的可行备用路。");
        }

        [Test]
        public void FullLowLossRelayAllowsHigherLossDirectAlternative()
        {
            var world = SimulationTestWorld.Create(
                reactor: SimulationTestWorld.Definition(BuildingKind.Reactor,
                    production: 3000, outputAmount: 3000, range: 6),
                relay: SimulationTestWorld.Definition(BuildingKind.Relay, capacity: 1000,
                    outputAmount: 1000, outputInterval: 100, range: 6),
                energySettings: new EnergySettings(200, 1, 10000, 0.5));
            var relay = SimulationTestWorld.Place(world, BuildingKind.Relay, 3, 0);
            var turret = SimulationTestWorld.Place(world, BuildingKind.Turret, 6, 0);
            var reactor = SimulationTestWorld.Place(world, BuildingKind.Reactor, 0, 0);

            // 经中继的保留率为 0.875²，直达保留率为 0.5。
            // 首选路径的下一跳空间用完后，仍可用更费能量的直达路线继续供给。
            Assert.That(relay.ReservedIncoming, Is.EqualTo(relay.Definition.EnergyCapacity));
            Assert.That(world.Energy.Transfers.Any(transfer =>
                transfer.SourceId == reactor.Id && transfer.TargetId == relay.Id), Is.True);
            Assert.That(world.Energy.Transfers.Any(transfer =>
                transfer.SourceId == reactor.Id && transfer.TargetId == turret.Id), Is.True,
                "低损耗路线被容量阻塞时，应启用可行的高损耗备用路线。");
            SimulationTestWorld.Advance(world, 20);
            Assert.That(turret.TotalReceived, Is.GreaterThan(0));
        }

        [Test]
        public void OldEnergyCanChangeDestinationFromItsCurrentRelay()
        {
            var world = CreateChain(out var reactor, out var firstRelay, out var secondRelay, out var originalTurret);
            StopSourceAndWaitForSecondHop(world, reactor, firstRelay, secondRelay);

            world.Enqueue(WorldCommand.Damage(originalTurret.Id, 100));
            var replacement = SimulationTestWorld.Place(world, BuildingKind.Turret, 4, 2);
            SimulationTestWorld.AdvanceUntil(world, () => replacement.TotalReceived > 0, 20,
                "原目标消失后，旧能量应从当前中继改送合法的新目标。");

            Assert.That(originalTurret.TotalReceived, Is.Zero);
            Assert.That(originalTurret.CommittedIncoming, Is.Zero);
            Assert.That(replacement.TotalReceived, Is.EqualTo(1000));
            Assert.That(world.Energy.GetTotals().ExpiredLoss, Is.Zero);
            Assert.That(world.Energy.GetTotals().DestructionLoss, Is.Zero);
        }

        [Test]
        public void ReplanningCannotReturnThroughAnAlreadyVisitedRelay()
        {
            var world = CreateStrandedEnergy(out var firstRelay, out var secondRelay, out var replacement);
            var batch = world.Energy.BatchDiagnostics.Single(item => item.BuildingId == secondRelay.Id);
            CollectionAssert.Contains(batch.History.ToArray(), firstRelay.Id);
            CollectionAssert.Contains(batch.History.ToArray(), secondRelay.Id);

            // 唯一几何通路为 B → A → 新炮塔，但 A 已实际访问，所以必须拒绝。
            for (var index = 0; index < 10; index++)
            {
                world.Step();
                SimulationTestWorld.AssertBalanced(world);
                Assert.That(world.Energy.Transfers.Any(transfer =>
                    transfer.SourceId == secondRelay.Id && transfer.TargetId == firstRelay.Id), Is.False);
            }

            Assert.That(replacement.TotalReceived, Is.Zero);
            Assert.That(secondRelay.StoredEnergy, Is.EqualTo(1000));
        }

        [Test]
        public void RepeatedMovesAndReplanningDoNotResetAccumulatedStallTime()
        {
            var world = CreateStrandedEnergy(out var firstRelay, out var secondRelay, out var replacement);
            var previousStalledTicks = 0;
            var observedWaiting = false;

            for (var index = 0; index < 40 && world.Energy.GetTotals().ExpiredLoss == 0; index++)
            {
                // 两个位置都只能经 A 返回，新路径仍违反历史约束。
                SimulationTestWorld.Execute(world, WorldCommand.Move(secondRelay.Id, 4, index % 2));
                foreach (var batch in world.Energy.BatchDiagnostics.Where(item => item.BuildingId == secondRelay.Id))
                {
                    Assert.That(batch.StalledTicks, Is.GreaterThanOrEqualTo(previousStalledTicks),
                        "移动和重新寻路不能把同一旧批次的累计滞留时间清零。");
                    previousStalledTicks = batch.StalledTicks;
                    observedWaiting = true;
                    CollectionAssert.Contains(batch.History.ToArray(), firstRelay.Id);
                }
            }

            Assert.That(observedWaiting, Is.True);
            Assert.That(world.Energy.GetTotals().ExpiredLoss, Is.EqualTo(1000));
            Assert.That(secondRelay.StoredEnergy, Is.Zero);
            Assert.That(replacement.TotalReceived, Is.Zero);
            Assert.That(world.Energy.ActiveBatchCount, Is.Zero);
            SimulationTestWorld.AssertBalanced(world);
        }

        [Test]
        public void RepairOfUnvisitedPlannedRelayAllowsWaitingEnergyToResume()
        {
            var world = CreateChain(out var reactor, out var firstRelay, out var secondRelay, out var turret);
            world.Enqueue(WorldCommand.Damage(reactor.Id, 100));
            SimulationTestWorld.Execute(world, WorldCommand.Damage(secondRelay.Id, 100));
            SimulationTestWorld.AdvanceUntil(world, () => firstRelay.StoredEnergy == 1000, 10,
                "后方线路故障不应取消仍然有效的源头到 A 的当前一跳。");
            SimulationTestWorld.Advance(world, 3);

            var waiting = world.Energy.BatchDiagnostics.Single(item => item.BuildingId == firstRelay.Id);
            CollectionAssert.DoesNotContain(waiting.History.ToArray(), secondRelay.Id,
                "计划经过、但尚未实际到达的节点不应加入访问历史。");
            SimulationTestWorld.Execute(world, WorldCommand.Repair(secondRelay.Id));
            SimulationTestWorld.AdvanceUntil(world, () => turret.TotalReceived > 0, 20,
                "修复后的合法原路径应恢复旧批次的运输。");

            Assert.That(turret.TotalReceived, Is.EqualTo(1000));
            Assert.That(world.Energy.GetTotals().ExpiredLoss, Is.Zero);
            Assert.That(world.Energy.GetTotals().DestructionLoss, Is.Zero);
        }

        [Test]
        public void RepairOnTheExpiryTickCannotEraseAlreadyElapsedWaitingTime()
        {
            var world = CreateChain(out var reactor, out var firstRelay, out var secondRelay, out var turret);
            world.Enqueue(WorldCommand.Damage(reactor.Id, 100));
            SimulationTestWorld.Execute(world, WorldCommand.Damage(secondRelay.Id, 100));
            SimulationTestWorld.AdvanceUntil(world,
                () => world.Energy.BatchDiagnostics.Any(batch =>
                    batch.BuildingId == firstRelay.Id && batch.StalledTicks == 29),
                35, "批次应在故障中累计到距离 30 步时限仅剩一步。");

            // 上一个时间区间一直处于滞留，本步修复不能抹去已经过去的最后一个等待步。
            SimulationTestWorld.Execute(world, WorldCommand.Repair(secondRelay.Id));

            Assert.That(secondRelay.IsAlive, Is.True);
            Assert.That(world.Energy.GetTotals().ExpiredLoss, Is.EqualTo(1000));
            Assert.That(firstRelay.StoredEnergy, Is.Zero);
            Assert.That(world.Energy.Transfers, Is.Empty);
            Assert.That(turret.TotalReceived, Is.Zero);
        }

        [Test]
        public void SplittingAnAlreadyStalledBatchPreservesHistoryAndWaitingBudget()
        {
            var world = SimulationTestWorld.Create(
                reactor: SimulationTestWorld.Definition(BuildingKind.Reactor, capacity: 6000,
                    production: 6000, outputAmount: 6000, range: 2.1),
                relay: SimulationTestWorld.Definition(BuildingKind.Relay, outputAmount: 2000,
                    outputInterval: 4, range: 2.1),
                turret: SimulationTestWorld.Definition(BuildingKind.Turret, capacity: 6000),
                energySettings: new EnergySettings(30, 1, 10000, 0));
            var firstRelay = SimulationTestWorld.Place(world, BuildingKind.Relay, 2, 0);
            var secondRelay = SimulationTestWorld.Place(world, BuildingKind.Relay, 4, 0);
            SimulationTestWorld.Place(world, BuildingKind.Turret, 6, 0);
            var reactor = SimulationTestWorld.Place(world, BuildingKind.Reactor, 0, 0);
            world.Enqueue(WorldCommand.Damage(reactor.Id, 100));
            SimulationTestWorld.Execute(world, WorldCommand.Damage(secondRelay.Id, 100));
            SimulationTestWorld.AdvanceUntil(world, () => firstRelay.StoredEnergy == 6000, 10,
                "整批能量应到达仍有效的 A，并因后方故障而等待。");
            SimulationTestWorld.Advance(world, 3);
            var beforeSplit = world.Energy.BatchDiagnostics.Single(batch => batch.BuildingId == firstRelay.Id);
            Assert.That(beforeSplit.StalledTicks, Is.GreaterThan(0));

            SimulationTestWorld.Execute(world, WorldCommand.Repair(secondRelay.Id));
            var travelling = world.Energy.BatchDiagnostics.Single(batch => batch.InFlight);
            var retained = world.Energy.BatchDiagnostics.Single(batch => batch.BuildingId == firstRelay.Id);
            Assert.That(travelling.Amount, Is.EqualTo(2000));
            Assert.That(retained.Amount, Is.EqualTo(4000));
            CollectionAssert.AreEqual(beforeSplit.History, travelling.History);
            CollectionAssert.AreEqual(beforeSplit.History, retained.History);
            Assert.That(travelling.StalledTicks, Is.EqualTo(beforeSplit.StalledTicks + 1),
                "拆分前先结算上一个完整的滞留区间，再让发出的部分暂停累计。");
            Assert.That(retained.StalledTicks, Is.EqualTo(travelling.StalledTicks));
            var retainedWaiting = retained.StalledTicks;

            world.Step();
            SimulationTestWorld.AssertBalanced(world);
            var stillRetained = world.Energy.BatchDiagnostics.Single(batch => batch.BuildingId == firstRelay.Id);
            Assert.That(stillRetained.StalledTicks, Is.GreaterThan(retainedWaiting),
                "已经滞留的剩余部分不能因另一部分成功发出而获得免费的冷却等待时间。");
        }

        [Test]
        public void RestoredOldRouteCannotOverfillTargetAlreadyServedByAnotherSource()
        {
            var world = CreateChain(out var reactor, out var firstRelay, out var secondRelay, out var turret);
            world.Enqueue(WorldCommand.Damage(reactor.Id, 100));
            SimulationTestWorld.Execute(world, WorldCommand.Damage(secondRelay.Id, 100));
            SimulationTestWorld.AdvanceUntil(world, () => firstRelay.StoredEnergy == 1000, 10,
                "旧能量先滞留在 A 中。");
            var replacementSource = SimulationTestWorld.Place(world, BuildingKind.Reactor, 6, 2);
            SimulationTestWorld.AdvanceUntil(world, () => turret.StoredEnergy == 1000, 10,
                "替代来源先填满炮塔缓存。");
            SimulationTestWorld.Execute(world, WorldCommand.Damage(replacementSource.Id, 100));
            SimulationTestWorld.Execute(world, WorldCommand.Repair(secondRelay.Id));
            SimulationTestWorld.Advance(world, 10);

            Assert.That(turret.StoredEnergy, Is.EqualTo(1000));
            Assert.That(turret.TotalReceived, Is.EqualTo(1000));
            Assert.That(turret.CommittedIncoming, Is.Zero,
                "恢复旧路时应重新查询目标缺口，而不是恢复过时的供给预约。");
        }

        private static GameWorld CreateChain(out BuildingState reactor, out BuildingState firstRelay,
            out BuildingState secondRelay, out BuildingState turret)
        {
            var world = SimulationTestWorld.Create(
                reactor: SimulationTestWorld.Definition(BuildingKind.Reactor, capacity: 1000,
                    production: 1000, outputAmount: 1000, range: 2.1),
                relay: SimulationTestWorld.Definition(BuildingKind.Relay, outputAmount: 1000, range: 2.1),
                turret: SimulationTestWorld.Definition(BuildingKind.Turret, capacity: 1000),
                energySettings: new EnergySettings(30, 1, 1000, 0));
            firstRelay = SimulationTestWorld.Place(world, BuildingKind.Relay, 2, 0);
            secondRelay = SimulationTestWorld.Place(world, BuildingKind.Relay, 4, 0);
            turret = SimulationTestWorld.Place(world, BuildingKind.Turret, 6, 0);
            reactor = SimulationTestWorld.Place(world, BuildingKind.Reactor, 0, 0);
            return world;
        }

        private static void StopSourceAndWaitForSecondHop(GameWorld world, BuildingState reactor,
            BuildingState firstRelay, BuildingState secondRelay)
        {
            SimulationTestWorld.Execute(world, WorldCommand.Damage(reactor.Id, 100));
            SimulationTestWorld.AdvanceUntil(world,
                () => world.Energy.Transfers.Any(transfer =>
                    transfer.SourceId == firstRelay.Id && transfer.TargetId == secondRelay.Id),
                10, "第一批能量应进入 A 到 B 的当前一跳。");
        }

        private static GameWorld CreateStrandedEnergy(out BuildingState firstRelay,
            out BuildingState secondRelay, out BuildingState replacement)
        {
            var world = CreateChain(out var reactor, out firstRelay, out secondRelay, out var turret);
            StopSourceAndWaitForSecondHop(world, reactor, firstRelay, secondRelay);
            world.Enqueue(WorldCommand.Damage(turret.Id, 100));
            replacement = SimulationTestWorld.Place(world, BuildingKind.Turret, 1, 1);
            var destinationRelay = secondRelay;
            SimulationTestWorld.AdvanceUntil(world, () => destinationRelay.StoredEnergy == 1000, 10,
                "B 的接收端仍有效，旧能量应先完成当前一跳，再寻找新目标。");
            return world;
        }
    }
}
