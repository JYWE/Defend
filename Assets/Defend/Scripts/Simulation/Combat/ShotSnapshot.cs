namespace Defend.Simulation
{
    public enum TurretCombatState { None, Searching, CoolingDown, WaitingForEnergy, Fired }

    /// <summary>已经结算的射击表现记录。射线是否显示、播放多久都不影响伤害。</summary>
    public readonly struct ShotSnapshot
    {
        public long Id { get; }
        public long Tick { get; }
        public long TurretId { get; }
        public long EnemyId { get; }
        public double SourceX { get; }
        public double SourceZ { get; }
        public double TargetX { get; }
        public double TargetZ { get; }
        public BuildingKind TurretKind { get; }
        public double SplashRadius { get; }

        internal ShotSnapshot(long id, long tick, BuildingState turret, EnemyState enemy, double splashRadius = 0)
        {
            Id = id;
            TurretKind = turret.Kind;
            SplashRadius = splashRadius;
            Tick = tick;
            TurretId = turret.Id;
            EnemyId = enemy.Id;
            SourceX = turret.CenterX;
            SourceZ = turret.CenterZ;
            TargetX = enemy.X;
            TargetZ = enemy.Z;
        }
    }
}
