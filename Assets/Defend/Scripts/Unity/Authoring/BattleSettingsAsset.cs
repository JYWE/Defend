using System;
using System.Collections.Generic;
using Defend.Simulation;
using UnityEngine;

namespace Defend.Unity
{
    /// <summary>可编辑的战斗与敌人行为配置，旧资产默认关闭主动目标评估。</summary>
    [CreateAssetMenu(menuName = "涌现防线/战斗配置", fileName = "BattleSettings")]
    public sealed class BattleSettingsAsset : ScriptableObject
    {
        [Serializable]
        private sealed class WaveAuthoring
        {
            [Min(1)] public int enemyCount;
            [Min(1)] public int enemyHealth;
            [Min(1)] public int spawnIntervalTicks;
            [Min(0)] public int clearReward;
            public EnemyKind[] enemyPattern = { EnemyKind.Normal };
            public int entranceIndex = -1;
            [TextArea] public string briefing = "";
            public WaveAuthoring(int count, int health, int interval, int reward)
            { enemyCount = count; enemyHealth = health; spawnIntervalTicks = interval; clearReward = reward; }
        }
        [Serializable]
        private sealed class TurretAuthoring
        {
            public BuildingKind kind;
            public float range = 7;
            public int damage = 20;
            public long energyPerShot = 2000;
            public int cooldownTicks = 8;
            public float splashRadius;
        }
        [SerializeField] private TurretAuthoring[] turretDefinitions = Array.Empty<TurretAuthoring>();
        [SerializeField] private Vector2Int[] spawnPoints = Array.Empty<Vector2Int>();
        [SerializeField] private Vector2Int[] blockedCells = Array.Empty<Vector2Int>();
        [Header("场上数量上限")]
        [SerializeField, Min(1)] private int maximumBuildings = 100;
        [SerializeField, Min(1)] private int maximumLivingEnemies = 200;
        [Header("敌人")]
        [SerializeField, Min(0.1f)] private float enemySpeedCellsPerSecond = 1.8f;
        [SerializeField, Min(1)] private int enemyAttackDamage = 8;
        [SerializeField, Min(1)] private int enemyAttackIntervalTicks = 20;
        [Header("重装敌人：局部目标判断")]
        [SerializeField] private bool enableEnemyBehavior;
        [SerializeField, Min(0.1f)] private float enemySensingRadius = 8;
        [SerializeField, Min(0.1f)] private float enemyObservationMemorySeconds = 3;
        [SerializeField, Min(0.01f)] private float enemyEvaluationIntervalSeconds = 0.5f;
        [SerializeField, Min(0)] private float enemyMinimumTargetLockSeconds = 3;
        [SerializeField, Min(1)] private float enemySwitchScoreRatio = 1.35f;
        [SerializeField, Min(1)] private int enemyMaximumCandidates = 6;
        [SerializeField, Min(1)] private int enemyMaximumEvaluationsPerTick = 20;
        [SerializeField, Min(1)] private int enemyMaximumPathBuildsPerTick = 2;
        [SerializeField, Min(0)] private int enemyMaximumAdditionalTravelCells = 6;
        [Header("炮塔：一次攻击需要全额能量")]
        [SerializeField, Min(0.1f)] private float turretRange = 7;
        [SerializeField, Min(1)] private int turretDamage = 20;
        [SerializeField, Min(1)] private long turretEnergyPerShot = 2000;
        [SerializeField, Min(1)] private int turretCooldownTicks = 8;
        [SerializeField, Min(1)] private int shotDisplayTicks = 6;
        [SerializeField] private WaveAuthoring[] waves =
        {
            new WaveAuthoring(20, 60, 10, 300), new WaveAuthoring(35, 75, 9, 400),
            new WaveAuthoring(55, 90, 8, 500), new WaveAuthoring(80, 105, 7, 600),
            new WaveAuthoring(110, 120, 6, 800)
        };

        public BattleSettings CreateSettings()
        {
            List<WaveDefinition> definitions = new List<WaveDefinition>();
            foreach (WaveAuthoring wave in waves)
                definitions.Add(new WaveDefinition(wave.enemyCount, wave.enemyHealth, wave.spawnIntervalTicks, wave.clearReward,
                    wave.enemyPattern, wave.entranceIndex, wave.briefing));
            var weapons = new List<TurretDefinition>();
            foreach (var turret in turretDefinitions) weapons.Add(new TurretDefinition(turret.kind, turret.range, turret.damage,
                turret.energyPerShot, turret.cooldownTicks, turret.splashRadius));
            var entrances = new List<SpawnPoint>();
            foreach (var point in spawnPoints) entrances.Add(new SpawnPoint(point.x, point.y));
            var terrain = new List<SpawnPoint>();
            foreach (var point in blockedCells) terrain.Add(new SpawnPoint(point.x, point.y));
            return new BattleSettings(definitions, maximumBuildings, maximumLivingEnemies, enemySpeedCellsPerSecond,
                enemyAttackDamage, enemyAttackIntervalTicks, turretRange, turretDamage, turretEnergyPerShot,
                turretCooldownTicks, shotDisplayTicks, entrances, weapons, terrain,
                enemyBehavior: new EnemyBehaviorSettings(enabled: enableEnemyBehavior,
                    sensingRadius: enemySensingRadius, observationMemorySeconds: enemyObservationMemorySeconds,
                    evaluationIntervalSeconds: enemyEvaluationIntervalSeconds, minimumTargetLockSeconds: enemyMinimumTargetLockSeconds,
                    switchScoreRatio: enemySwitchScoreRatio, maximumCandidates: enemyMaximumCandidates,
                    maximumEvaluationsPerTick: enemyMaximumEvaluationsPerTick, maximumPathBuildsPerTick: enemyMaximumPathBuildsPerTick,
                    maximumAdditionalTravelCells: enemyMaximumAdditionalTravelCells));
        }
    }
}
