using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Defend.Simulation;

/// <summary>只测纯 C# 能源世界；不能替代 Unity Player 的帧耗时和画面验收。</summary>
internal static class EnergyProbe
{
    private static void Main(string[] args)
    {
        int count = args.Length > 0 ? int.Parse(args[0]) : 100;
        int warmup = args.Length > 1 ? int.Parse(args[1]) : 200;
        int steps = args.Length > 2 ? int.Parse(args[2]) : 200;
        var definitions = new[] {
            Definition(BuildingKind.Reactor, 2000, 0, 8000), Definition(BuildingKind.Relay, 0, 0, 8000),
            Definition(BuildingKind.Turret, 0, 1000, 0), Definition(BuildingKind.Core, 0, 1000, 0) };
        var world = new GameWorld(definitions, new WorldSettings(40, 40, 20, 0));
        int side = (int)Math.Ceiling(Math.Sqrt(count));
        for (int index = 0; index < count; index++)
            world.Enqueue(WorldCommand.Place(index % 4 == 0 ? BuildingKind.Reactor : index % 4 == 3 ? BuildingKind.Turret : BuildingKind.Relay, index % side, index / side));
        var watch = Stopwatch.StartNew();
        world.Step();
        double setup = watch.Elapsed.TotalMilliseconds;
        int peakBatches = 0, peakPending = 0;
        for (int index = 0; index < warmup; index++) world.Step();
        long consumedBefore = world.Energy.GetTotals().Consumed;
        long receivedBefore = world.Buildings.Sum(b => b.TotalReceived);
        long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
        var samples = new List<double>();
        var windows = new List<object>();
        long previousWindowConsumed = consumedBefore;
        for (int index = 0; index < steps; index++)
        {
            watch.Restart();
            world.Step();
            samples.Add(watch.Elapsed.TotalMilliseconds);
            peakBatches = Math.Max(peakBatches, world.Energy.ActiveBatchCount);
            peakPending = Math.Max(peakPending, world.Energy.PlanningStatistics.PendingSearchCount);
            if (index % 200 == 0) world.Energy.AssertInvariants();
            if (steps >= 2000 && (index + 1) % 2000 == 0)
            {
                long totalConsumed = world.Energy.GetTotals().Consumed;
                windows.Add(new { endStep = index + 1, consumed = totalConsumed - previousWindowConsumed,
                    batches = world.Energy.ActiveBatchCount, pending = world.Energy.PlanningStatistics.PendingSearchCount,
                    retainedManagedBytes = GC.GetTotalMemory(true) });
                previousWindowConsumed = totalConsumed;
            }
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
        world.Energy.AssertInvariants();
        samples.Sort();
        Console.WriteLine(JsonSerializer.Serialize(new {
            count, warmup, steps, setupMilliseconds = setup,
            p50 = samples[samples.Count / 2], p95 = samples[(int)Math.Ceiling(samples.Count * .95) - 1], p99 = samples[(int)Math.Ceiling(samples.Count * .99) - 1], maximum = samples[samples.Count - 1],
            allocatedBytes = allocated, bytesPerStep = allocated / steps,
            connections = world.Energy.Connections.Count, peakBatches, peakPending,
            pending = world.Energy.PlanningStatistics.PendingSearchCount,
            completedSearches = world.Energy.PlanningStatistics.CompletedSearchCount,
            startedSearches = world.Energy.PlanningStatistics.SearchStartedCount,
            consumed = world.Energy.GetTotals().Consumed - consumedBefore,
            demand = (long)(count / 4) * 1000 * steps,
            receivedAtAllNodes = world.Buildings.Sum(b => b.TotalReceived) - receivedBefore,
            consumersWithDelivery = world.Buildings.Count(b => b.IsConsumer && b.TotalReceived > 0),
            balanceError = world.Energy.GetTotals().BalanceError,
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            windows,
            boundary = "纯 C# 进程，未包含 Unity 表现、敌人和战斗；不代表 Player 帧率"
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static BuildingDefinition Definition(BuildingKind kind, long production, long consumption, long output)
    {
        return new BuildingDefinition(kind, kind.ToString(), 1, 1, 100000, production, consumption,
            output, 4, output > 0 ? 7 : 0, 100, 0, 0, 0);
    }
}
