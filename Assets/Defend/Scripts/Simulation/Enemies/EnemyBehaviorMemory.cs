using System;
using System.Collections.Generic;

namespace Defend.Simulation
{
    internal sealed class EnemyBuildingObservation
    {
        internal long Id;
        internal BuildingKind Kind;
        internal int X, Z, Width, Depth, Health, LifeGeneration, PositionRevision;
        internal long LastSeenTick;
        internal readonly EnemyObservedActivity Damage = new EnemyObservedActivity();
        internal double CenterX => X + Width * 0.5;
        internal double CenterZ => Z + Depth * 0.5;
        internal bool IsTurret => Kind == BuildingKind.Turret || Kind == BuildingKind.BurstTurret || Kind == BuildingKind.AreaTurret;

        internal void Refresh(BuildingState building, long tick)
        {
            Id = building.Id;
            Kind = building.Kind;
            X = building.X;
            Z = building.Z;
            Width = building.Definition.Width;
            Depth = building.Definition.Depth;
            Health = building.Health;
            LifeGeneration = building.LifeGeneration;
            PositionRevision = building.PositionRevision;
            LastSeenTick = tick;
        }
    }

    internal sealed class EnemySupplyObservation
    {
        internal long SourceId, TargetId;
        internal readonly EnemyObservedActivity Deliveries = new EnemyObservedActivity();
    }

    /// <summary>精确有限窗口中的指数衰减记录。同一步的事件合并，查询不重新遍历整段历史。</summary>
    internal sealed class EnemyObservedActivity
    {
        private readonly List<Sample> samples = new List<Sample>();
        private int first;
        private long lastSampleTick = -1;
        private double signal;
        internal long LastEventTick { get; private set; } = -1;

        internal void Add(double amount, long tick, int memoryTicks)
        {
            Value(tick, memoryTicks);
            signal += amount;
            if (samples.Count > first && samples[samples.Count - 1].Tick == tick)
            {
                var previous = samples[samples.Count - 1];
                samples[samples.Count - 1] = new Sample { Tick = tick, Amount = previous.Amount + amount };
            }
            else samples.Add(new Sample { Tick = tick, Amount = amount });
            LastEventTick = tick;
        }

        internal double Value(long tick, int memoryTicks)
        {
            if (lastSampleTick >= 0 && tick != lastSampleTick) signal *= Math.Exp(-(double)(tick - lastSampleTick) / memoryTicks);
            lastSampleTick = tick;
            while (first < samples.Count && tick - samples[first].Tick >= memoryTicks)
            {
                var expired = samples[first++];
                signal -= expired.Amount * Math.Exp(-(double)(tick - expired.Tick) / memoryTicks);
            }
            if (first == samples.Count) { samples.Clear(); first = 0; signal = 0; }
            else if (first >= 32) { samples.RemoveRange(0, first); first = 0; }
            return Math.Max(0, signal);
        }

        private struct Sample { internal long Tick; internal double Amount; }
    }

    /// <summary>每个敌人单独拥有；只有当前感知和现场事件可以写入，不读取累计账本。</summary>
    internal sealed class EnemyBehaviorMemory
    {
        internal readonly List<EnemyBuildingObservation> Buildings = new List<EnemyBuildingObservation>();
        internal readonly List<EnemySupplyObservation> Supplies = new List<EnemySupplyObservation>();
        internal long StrategicTargetId;
        internal int StrategicLifeGeneration;
        internal long TargetAssignedTick;
        internal long NextEvaluationTick;
        internal EnemyStrategicPath Path;

        internal EnemyBuildingObservation Find(long id)
        {
            for (int index = 0; index < Buildings.Count; index++) if (Buildings[index].Id == id) return Buildings[index];
            return null;
        }

        internal EnemyBuildingObservation Observe(BuildingState building, long tick)
        {
            var result = Find(building.Id);
            if (result != null && (result.LifeGeneration != building.LifeGeneration || result.PositionRevision != building.PositionRevision))
            {
                // 再发现移动后的建筑可以更新位置，但旧位置的火力与供给关系不能一起搬走。
                Buildings.Remove(result);
                ForgetSupplies(building.Id);
                result = null;
            }
            if (result == null)
            {
                result = new EnemyBuildingObservation();
                Buildings.Add(result);
            }
            result.Refresh(building, tick);
            return result;
        }

        internal void ForgetSupplies(long id)
        {
            for (int index = Supplies.Count - 1; index >= 0; index--)
                if (Supplies[index].SourceId == id || Supplies[index].TargetId == id) Supplies.RemoveAt(index);
        }

    }
}
