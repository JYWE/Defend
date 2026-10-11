using System;

namespace Defend.Simulation
{
    /// <summary>单波只读配置。生成计划只控制敌人，不清空或补满能源网络。</summary>
    public sealed class WaveDefinition
    {
        public int EnemyCount { get; }
        public int EnemyHealth { get; }
        public int SpawnIntervalTicks { get; }
        public int ClearReward { get; }
        public string Briefing { get; }
        public System.Collections.Generic.IReadOnlyList<EnemyKind> EnemyPattern { get; }
        public int EntranceIndex { get; }

        public WaveDefinition(int enemyCount = 20, int enemyHealth = 60,
            int spawnIntervalTicks = 10, int clearReward = 300, EnemyKind[] enemyPattern = null,
            int entranceIndex = -1, string briefing = "")
        {
            if (enemyCount <= 0 || enemyCount > 100000) throw new ArgumentOutOfRangeException(nameof(enemyCount));
            if (enemyHealth <= 0) throw new ArgumentOutOfRangeException(nameof(enemyHealth));
            if (spawnIntervalTicks <= 0) throw new ArgumentOutOfRangeException(nameof(spawnIntervalTicks));
            if (clearReward < 0) throw new ArgumentOutOfRangeException(nameof(clearReward));
            EnemyCount = enemyCount;
            EnemyHealth = enemyHealth;
            SpawnIntervalTicks = spawnIntervalTicks;
            ClearReward = clearReward;
            var pattern = enemyPattern == null || enemyPattern.Length == 0 ? new[] { EnemyKind.Normal } : (EnemyKind[])enemyPattern.Clone();
            foreach (var kind in pattern) if (!Enum.IsDefined(typeof(EnemyKind), kind)) throw new ArgumentException("无效敌人类型。");
            EnemyPattern = Array.AsReadOnly(pattern);
            EntranceIndex = entranceIndex;
            Briefing = briefing ?? "";
        }
    }

    public enum BattleState { Preparing, Combat, WaveCleared, Victory, Defeat }

    public readonly struct SpawnPoint
    {
        public int X { get; }
        public int Z { get; }
        public SpawnPoint(int x, int z) { X = x; Z = z; }
    }
}
