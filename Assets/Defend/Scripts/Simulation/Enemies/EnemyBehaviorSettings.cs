using System;

namespace Defend.Simulation
{
    /// <summary>局部感应和战略目标的不可变参数；旧战斗配置默认不开启。</summary>
    public sealed class EnemyBehaviorSettings
    {
        public bool Enabled { get; }
        public double SensingRadius { get; }
        public double ObservationMemorySeconds { get; }
        public double EvaluationIntervalSeconds { get; }
        public double MinimumTargetLockSeconds { get; }
        public double SwitchScoreRatio { get; }
        public int MaximumCandidates { get; }
        public int MaximumEvaluationsPerTick { get; }
        public int MaximumPathBuildsPerTick { get; }
        public int MaximumAdditionalTravelCells { get; }

        public EnemyBehaviorSettings(bool enabled = false, double sensingRadius = 8,
            double observationMemorySeconds = 3, double evaluationIntervalSeconds = 0.5,
            double minimumTargetLockSeconds = 3, double switchScoreRatio = 1.35,
            int maximumCandidates = 6, int maximumEvaluationsPerTick = 20,
            int maximumPathBuildsPerTick = 2, int maximumAdditionalTravelCells = 6)
        {
            PositiveFinite(sensingRadius, nameof(sensingRadius));
            PositiveFinite(observationMemorySeconds, nameof(observationMemorySeconds));
            PositiveFinite(evaluationIntervalSeconds, nameof(evaluationIntervalSeconds));
            if (double.IsNaN(minimumTargetLockSeconds) || double.IsInfinity(minimumTargetLockSeconds) || minimumTargetLockSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumTargetLockSeconds));
            if (double.IsNaN(switchScoreRatio) || double.IsInfinity(switchScoreRatio) || switchScoreRatio < 1)
                throw new ArgumentOutOfRangeException(nameof(switchScoreRatio));
            if (maximumCandidates <= 0 || maximumCandidates > 100) throw new ArgumentOutOfRangeException(nameof(maximumCandidates));
            if (maximumEvaluationsPerTick <= 0) throw new ArgumentOutOfRangeException(nameof(maximumEvaluationsPerTick));
            if (maximumPathBuildsPerTick <= 0) throw new ArgumentOutOfRangeException(nameof(maximumPathBuildsPerTick));
            if (maximumAdditionalTravelCells < 0) throw new ArgumentOutOfRangeException(nameof(maximumAdditionalTravelCells));
            Enabled = enabled;
            SensingRadius = sensingRadius;
            ObservationMemorySeconds = observationMemorySeconds;
            EvaluationIntervalSeconds = evaluationIntervalSeconds;
            MinimumTargetLockSeconds = minimumTargetLockSeconds;
            SwitchScoreRatio = switchScoreRatio;
            MaximumCandidates = maximumCandidates;
            MaximumEvaluationsPerTick = maximumEvaluationsPerTick;
            MaximumPathBuildsPerTick = maximumPathBuildsPerTick;
            MaximumAdditionalTravelCells = maximumAdditionalTravelCells;
        }

        private static void PositiveFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0 || value > 10000)
                throw new ArgumentOutOfRangeException(name);
        }
    }

    public enum EnemyTargetPurpose { AdvanceToCore, ClearObstacle, DisruptEnergy, SuppressTurret, SearchLastKnownPosition }

    /// <summary>只读工作量证据；观察记录不计作完整目标评估或距离场构建。</summary>
    public sealed class EnemyBehaviorStatistics
    {
        public int EvaluationsThisTick { get; internal set; }
        public int PathBuildsThisTick { get; internal set; }
        public int DeferredEvaluationsThisTick { get; internal set; }
        public int DeferredPathsThisTick { get; internal set; }
        public int TargetSwitchesThisTick { get; internal set; }
        public long TotalEvaluations { get; internal set; }
        public long TotalPathBuilds { get; internal set; }
        public long TotalDeferredEvaluations { get; internal set; }
        public long TotalDeferredPaths { get; internal set; }
        public long TotalTargetSwitches { get; internal set; }
        public int CachedPathCount { get; internal set; }
        public int MaximumEvaluationsObserved { get; internal set; }
        public int MaximumPathBuildsObserved { get; internal set; }
        public int BuildingsObservedThisTick { get; internal set; }
        public int DamageEventsObservedThisTick { get; internal set; }
        public int DeliveriesObservedThisTick { get; internal set; }
        public long TotalBuildingsObserved { get; internal set; }
        public long TotalDamageEventsObserved { get; internal set; }
        public long TotalDeliveriesObserved { get; internal set; }

        internal void BeginStep()
        {
            EvaluationsThisTick = PathBuildsThisTick = DeferredEvaluationsThisTick = TargetSwitchesThisTick = 0;
            DeferredPathsThisTick = 0;
            BuildingsObservedThisTick = DamageEventsObservedThisTick = DeliveriesObservedThisTick = 0;
        }

        internal void Deferred() { DeferredEvaluationsThisTick++; TotalDeferredEvaluations++; }
        internal void DeferredPath() { DeferredPathsThisTick++; TotalDeferredPaths++; }
        internal void Switched() { TargetSwitchesThisTick++; TotalTargetSwitches++; }
    }
}
