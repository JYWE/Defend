namespace Defend.Simulation
{
    public enum WorldEventKind { EnemyHit, EnemyDeath, BuildingHit, BuildingDestroyed, Repair, ShieldHit, ShieldBreak }

    /// <summary>已完成结算的不可变结果，每步清空。外部读取不会改变伤害或能源账目。</summary>
    public readonly struct WorldEventSnapshot
    {
        public readonly WorldEventKind Kind;
        public readonly long SubjectId;
        public readonly double X, Z;
        public readonly int Amount;
        public WorldEventSnapshot(WorldEventKind kind, long subjectId, double x, double z, int amount = 0)
        { Kind = kind; SubjectId = subjectId; X = x; Z = z; Amount = amount; }
    }
}
