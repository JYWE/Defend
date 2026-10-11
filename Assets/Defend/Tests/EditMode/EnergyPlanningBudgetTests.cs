using System;
using System.Linq;
using Defend.Simulation;
using NUnit.Framework;

namespace Defend.Tests
{
    /// <summary>
    /// 用很小的工作预算主动触发跨步规划，验证性能限制不会悄悄改变运输规则。
    /// 这里只检查工作量、进展和玩法行为，不给未测硬件编造毫秒级性能承诺。
    /// </summary>
    public sealed class EnergyPlanningBudgetTests
    {
        [Test]
        public void SmallPlanningBudgetContinuesAllReachableConsumersWithoutFalseExpiry()
        {
            const int workBudget = 2;
            var world = SimulationTestWorld.Create(
                reactor: SimulationTestWorld.Definition(BuildingKind.Reactor,
                    production: 1000, outputAmount: 1000, range: 2.1),
                relay: SimulationTestWorld.Definition(BuildingKind.Relay, range: 2.9),
                energySettings: new EnergySettings(10, 1, 1000, 0,
                    maximumPathWorkPerTick: workBudget, pathWorkSlice: 1));
            for (var x = 2; x <= 12; x += 2)
                world.Enqueue(WorldCommand.Place(BuildingKind.Relay, x, 0));
            for (var x = 13; x <= 14; x++)
                for (var z = 0; z <= 2; z++)
                    world.Enqueue(WorldCommand.Place(BuildingKind.Turret, x, z));
            world.Enqueue(WorldCommand.Place(BuildingKind.Reactor, 0, 0));
            world.Step();
            var consumers = world.Buildings.Where(building => building.IsConsumer).ToArray();
            Assert.That(consumers.Length, Is.EqualTo(6));
            var sawPendingSearches = world.Energy.PlanningStatistics.PendingSearchCount > 0;
            var elapsedTicks = 0;

            while (consumers.Any(consumer => consumer.TotalReceived == 0) && elapsedTicks < 1000)
            {
                world.Step();
                elapsedTicks++;
                SimulationTestWorld.AssertBalanced(world);
                var statistics = world.Energy.PlanningStatistics;
                Assert.That(statistics.WorkUsedThisTick, Is.LessThanOrEqualTo(workBudget));
                sawPendingSearches |= statistics.PendingSearchCount > 0;
            }

            Assert.That(sawPendingSearches, Is.True, "本测试必须真正触发规划延后。");
            foreach (var consumer in consumers)
                Assert.That(consumer.TotalReceived, Is.GreaterThan(0),
                    "固定顺序之外的可达目标也必须得到规划服务，不能长期饿死。");
            Assert.That(world.Energy.GetTotals().ExpiredLoss, Is.Zero,
                "单纯等待工程规划预算不能被算作玩法滞留故障。");
            Assert.That(world.Energy.PlanningStatistics.CompletedSearchCount, Is.GreaterThanOrEqualTo(6));
        }

        [Test]
        public void CachedRoutesStillRejectPreviouslyVisitedRelayForOldBatch()
        {
            var world = SimulationTestWorld.Create(
                reactor: SimulationTestWorld.Definition(BuildingKind.Reactor, capacity: 1000,
                    production: 1000, outputAmount: 1000, range: 2.1),
                relay: SimulationTestWorld.Definition(BuildingKind.Relay, outputAmount: 1000, range: 2.1),
                turret: SimulationTestWorld.Definition(BuildingKind.Turret, capacity: 1000),
                energySettings: new EnergySettings(100, 1, 1000, 0));
            var firstRelay = SimulationTestWorld.Place(world, BuildingKind.Relay, 2, 0);
            var secondRelay = SimulationTestWorld.Place(world, BuildingKind.Relay, 4, 0);
            var turret = SimulationTestWorld.Place(world, BuildingKind.Turret, 6, 0);
            var reactor = SimulationTestWorld.Place(world, BuildingKind.Reactor, 0, 0);
            var oldBatchIdentifier = world.Energy.Transfers.Single().Id;
            SimulationTestWorld.Execute(world, WorldCommand.Damage(reactor.Id, 100));
            SimulationTestWorld.AdvanceUntil(world,
                () => world.Energy.Transfers.Any(transfer => transfer.Id == oldBatchIdentifier &&
                    transfer.SourceId == firstRelay.Id && transfer.TargetId == secondRelay.Id),
                10, "旧批次先实际访问 A，再进入 A 到 B 的一跳。");

            // 将终端移动到 A 后方，再从另一侧加入新的源头。
            // 新批次合法路线是 新源头 → B → A → 终端，旧批次却不能使用 B → A 的缓存后缀。
            world.Enqueue(WorldCommand.Move(turret.Id, 1, 1));
            SimulationTestWorld.Place(world, BuildingKind.Reactor, 6, 0);
            var cacheHitsBefore = world.Energy.PlanningStatistics.CacheHitCount;
            var sawValidNewTransferThroughFirstRelay = false;
            for (var index = 0; index < 30; index++)
            {
                world.Energy.ConsumeUpTo(turret.Id, turret.Definition.EnergyCapacity);
                world.Step();
                SimulationTestWorld.AssertBalanced(world);
                foreach (var transfer in world.Energy.Transfers)
                {
                    if (transfer.Id == oldBatchIdentifier)
                        Assert.That(transfer.TargetId, Is.Not.EqualTo(firstRelay.Id),
                            "共享路线缓存不能绕过旧批次的实际访问历史。");
                    else if (transfer.SourceId == secondRelay.Id && transfer.TargetId == firstRelay.Id)
                        sawValidNewTransferThroughFirstRelay = true;
                }
            }

            Assert.That(world.Energy.PlanningStatistics.CacheHitCount, Is.GreaterThan(cacheHitsBefore));
            Assert.That(sawValidNewTransferThroughFirstRelay, Is.True,
                "必须有新批次实际使用这条缓存方向，才能区分历史过滤与全局禁止该边。");
            var oldBatch = world.Energy.BatchDiagnostics.Single(batch => batch.Id == oldBatchIdentifier);
            Assert.That(oldBatch.BuildingId, Is.EqualTo(secondRelay.Id));
            CollectionAssert.Contains(oldBatch.History.ToArray(), firstRelay.Id);
        }

        [Test]
        public void BusyPlanningQueueDoesNotFreezeAlreadyAccumulatedStallTimeout()
        {
            var world = SimulationTestWorld.Create(
                reactor: SimulationTestWorld.Definition(BuildingKind.Reactor, capacity: 1000,
                    production: 1000, outputAmount: 1000, range: 2.1),
                relay: SimulationTestWorld.Definition(BuildingKind.Relay, outputAmount: 1000, range: 2.1),
                turret: SimulationTestWorld.Definition(BuildingKind.Turret, capacity: 1000),
                energySettings: new EnergySettings(15, 1, 1000, 0,
                    maximumPathWorkPerTick: 2, pathWorkSlice: 1));
            var firstRelay = SimulationTestWorld.Place(world, BuildingKind.Relay, 2, 0);
            var secondRelay = SimulationTestWorld.Place(world, BuildingKind.Relay, 4, 0);
            SimulationTestWorld.Place(world, BuildingKind.Turret, 6, 0);
            var reactor = SimulationTestWorld.Place(world, BuildingKind.Reactor, 0, 0);
            SimulationTestWorld.AdvanceUntil(world,
                () => world.Energy.Transfers.Any(transfer => transfer.SourceId == reactor.Id),
                80, "低预算规划完成后才能开始运输，不能假设放置当步就已发出。");
            var oldBatchIdentifier = world.Energy.Transfers.Single(transfer => transfer.SourceId == reactor.Id).Id;
            world.Enqueue(WorldCommand.Damage(reactor.Id, 100));
            SimulationTestWorld.Execute(world, WorldCommand.Damage(secondRelay.Id, 100));
            SimulationTestWorld.AdvanceUntil(world,
                () => world.Energy.BatchDiagnostics.Any(batch => batch.Id == oldBatchIdentifier && batch.StalledTicks >= 1),
                80, "必须先确认旧批次已经因实际断路进入滞留，而不是尚在规划队列中。");

            // 独立区域创建大量新的可达请求，让规划队列跨过旧批次的剩余超时时间。
            for (var z = 20; z < 28; z++)
                world.Enqueue(WorldCommand.Place(BuildingKind.Reactor, 20, z));
            for (var z = 20; z < 36; z++)
                world.Enqueue(WorldCommand.Place(BuildingKind.Turret, 22, z));
            world.Step();
            var sawPendingWhileOldBatchWasWaiting = false;
            var sawExpiryWhilePlanningWasPending = false;
            for (var index = 0; index < 25; index++)
            {
                var oldBatchExists = world.Energy.BatchDiagnostics.Any(batch => batch.Id == oldBatchIdentifier);
                var pending = world.Energy.PlanningStatistics.PendingSearchCount > 0;
                sawPendingWhileOldBatchWasWaiting |= oldBatchExists && pending;
                if (!oldBatchExists)
                {
                    sawExpiryWhilePlanningWasPending = pending;
                    break;
                }
                world.Step();
                SimulationTestWorld.AssertBalanced(world);
                Assert.That(world.Energy.PlanningStatistics.WorkUsedThisTick, Is.LessThanOrEqualTo(2));
            }

            Assert.That(sawPendingWhileOldBatchWasWaiting, Is.True);
            Assert.That(sawExpiryWhilePlanningWasPending, Is.True,
                "全网规划工作未完成时，既有故障滞留仍应按模拟时间正常到期。");
            Assert.That(world.Energy.BatchDiagnostics.Any(batch => batch.Id == oldBatchIdentifier), Is.False);
            Assert.That(firstRelay.StoredEnergy, Is.Zero);
            Assert.That(world.Energy.GetTotals().ExpiredLoss, Is.GreaterThanOrEqualTo(1000));
        }
    }
}
