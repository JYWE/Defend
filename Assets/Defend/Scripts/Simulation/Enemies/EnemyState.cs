using System;

namespace Defend.Simulation
{
    public enum EnemyKind { Normal, Fast, Heavy }
    /// <summary>敌人的权威位置以格子为单位；表现对象只读取，不参与移动或伤害结算。</summary>
    public sealed class EnemyState
    {
        public long Id { get; }
        public EnemyKind Kind { get; }
        public double SpeedMultiplier => Kind == EnemyKind.Fast ? 1.8 : Kind == EnemyKind.Heavy ? 0.55 : 1;
        public int DamageMultiplier => Kind == EnemyKind.Heavy ? 3 : 1;
        public double X { get; internal set; }
        public double Z { get; internal set; }
        public int Health { get; internal set; }
        public int MaximumHealth { get; }
        public long TargetBuildingId { get; internal set; }
        public EnemyTargetPurpose TargetPurpose { get; internal set; } = EnemyTargetPurpose.AdvanceToCore;
        public double TargetX { get; internal set; }
        public double TargetZ { get; internal set; }
        public long LastAttackTick { get; internal set; } = -1;
        public long LastAttackTargetBuildingId { get; internal set; }
        public double LastAttackTargetX { get; internal set; }
        public double LastAttackTargetZ { get; internal set; }
        public long LastBehaviorEvaluationTick { get; internal set; } = -1;
        public long TargetAssignedTick { get; internal set; } = -1;
        public long NextAttackTick { get; internal set; }
        public bool IsAlive => Health > 0;
        internal int CellX;
        internal int CellZ;
        internal int NextCellX;
        internal int NextCellZ;
        internal EnemyBehaviorMemory BehaviorMemory;

        internal EnemyState(long id, SpawnPoint spawn, int health, EnemyKind kind = EnemyKind.Normal)
        {
            Id = id;
            Kind = kind;
            CellX = NextCellX = spawn.X;
            CellZ = NextCellZ = spawn.Z;
            X = spawn.X + 0.5;
            Z = spawn.Z + 0.5;
            Health = MaximumHealth = kind == EnemyKind.Heavy ? checked(health * 5) : kind == EnemyKind.Fast ? Math.Max(1, health * 2 / 3) : health;
        }
    }
}
