using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Defend.Simulation;
using Defend.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>由主负责人通过 run_script 显式调用。模拟对照不进入播放、不改资产、不强制制造断电。</summary>
public static class PhaseFourValidation
{
    private sealed class Scenario
    {
        public GameWorld World;
        public string Name;
        public int PurchaseSpend;
        public int RepairSpend;
        public int RepairCount;
        public long DisruptionTargetTicks;
        public long SuppressionTargetTicks;
        public long Attacks;
        public long FirstEnergyAttackTick = -1;
        public long FirstSupplyDestroyedTick = -1;
        public long ArrivalAtSupplyDestruction;
        public long WaitAtSupplyDestruction;
        public long ArrivalTwoSecondsAfterDestruction = -1;
        public long WaitTwoSecondsAfterDestruction = -1;
        public readonly List<object> Events = new List<object>();
    }

    public static object Content()
    {
        if (Application.isPlaying) throw new InvalidOperationException("序列化检查需要停止播放模式。");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != Defend.Editor.PhaseFourSceneBuilder.CombatScenePath)
            throw new InvalidOperationException("请先打开第四阶段 EnergyFrontDefense 场景。");
        int missing = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
        if (missing != 0) throw new Exception("第四阶段场景存在丢失脚本。");
        var session = Object.FindAnyObjectByType<GameSessionRoot>();
        var presentation = Object.FindAnyObjectByType<BattlePresentation>();
        var panel = Object.FindAnyObjectByType<BattleControlPanel>();
        if (session == null || presentation == null || panel == null) throw new Exception("关卡组合入口或表现组件缺失。");
        var issues = new List<string>();
        CheckReferences(panel, issues);
        CheckReferences(presentation, issues);
        foreach (MonoBehaviour component in session.GetComponents<MonoBehaviour>())
            CheckReferences(component, issues, "controlPanel");
        string[] geometryPaths = { Defend.Editor.PhaseFourSceneBuilder.EnemyPath,
            Defend.Editor.PhaseFourSceneBuilder.TargetMarkerPath };
        foreach (string path in geometryPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { issues.Add("缺少预制体：" + path); continue; }
            if (prefab.GetComponentInChildren<RectTransform>(true) != null)
                issues.Add("敌人或目标标记中出现运行时 UI：" + path);
            foreach (MonoBehaviour component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                CheckReferences(component, issues);
        }
        var settings = AssetDatabase.LoadAssetAtPath<BattleSettingsAsset>(
            Defend.Editor.PhaseFourSceneBuilder.BattleSettingsPath).CreateSettings();
        if (!settings.EnemyBehavior.Enabled) issues.Add("第四阶段没有启用敌人行为。");
        var legacy = AssetDatabase.LoadAssetAtPath<BattleSettingsAsset>(
            "Assets/Defend/Content/Definitions/PhaseThree/TwinPassBattleSettings.asset").CreateSettings();
        if (legacy.EnemyBehavior.Enabled) issues.Add("第三阶段对照配置被启用新行为。");
        var panelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Defend.Editor.PhaseFourSceneBuilder.PanelPath);
        if (panelAsset == null || panelAsset.GetComponentInChildren<BattleControlPanel>(true) == null)
            issues.Add("完整战斗面板没有保存于第四阶段 Prefab。");
        if (issues.Count != 0) throw new Exception(string.Join("\n", issues));
        return new
        {
            mode = "serialized-content-read-only", scene = scene.path, missing,
            settings.EnemyBehavior, waves = settings.Waves.Count,
            ui = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include).Length,
            buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include).Length,
            authoredBuildings = Object.FindAnyObjectByType<WorldPresentation>().AuthoredBuildings.Count,
            maximumTargetMarkers = new SerializedObject(presentation).FindProperty("maximumVisibleTargetMarkers").intValue,
            legacyBehaviorEnabled = legacy.EnemyBehavior.Enabled,
            waveBriefings = settings.Waves.Select(wave => new { wave.Briefing, pattern = wave.EnemyPattern.Select(kind => kind.ToString()).ToArray() }).ToArray()
        };
    }

    /// <summary>三个合成战局使用完全相同的建筑数量、购买成本、初始资金、波次和维修政策。</summary>
    public static object CompareLayouts()
    {
        var results = new List<object>();
        var purchaseSpends = new List<int>();
        for (int layout = 0; layout < 3; layout++)
        {
            Scenario scenario = BuildComparison(layout);
            purchaseSpends.Add(scenario.PurchaseSpend);
            Execute(scenario.World, WorldCommand.StartWave());
            for (int tick = 0; tick < 6000 && !scenario.World.Battle.IsFinished; tick++)
            {
                if (tick > 0 && tick % 100 == 0) RepairOneBuilding(scenario);
                scenario.World.Step();
                Observe(scenario);
                if (tick % 100 == 0) scenario.World.Energy.AssertInvariants();
            }
            scenario.World.Energy.AssertInvariants();
            results.Add(Summarize(scenario));
        }
        if (purchaseSpends.Distinct().Count() != 1) throw new Exception("对照布局购买开支不相等。");
        return new
        {
            mode = "pure-simulation-matched-purchase-budget", initialCredits = 1000,
            purchaseSpendEach = purchaseSpends[0], repairPolicy = "每 5 秒最多维修一栋低于一半耐久的建筑，所有布局使用相同剩余资金限制。",
            note = "合成诊断战局；只记录实际结果，不要求每种布局胜利，不代表原八波人工难度验收。",
            results
        };
    }

    /// <summary>复用既有普通购买策略，只替换为第四阶段配置。败局和预算不足如实返回。</summary>
    public static object SimulateCampaign(int strategy)
    {
        if (strategy != 0 && strategy != 1) throw new ArgumentOutOfRangeException(nameof(strategy));
        if (SceneManager.GetActiveScene().path != Defend.Editor.PhaseFourSceneBuilder.CombatScenePath)
            throw new InvalidOperationException("请先打开第四阶段场景，读取其中保存的初始布局。");
        BuildingDefinition[] definitions = Enum.GetValues(typeof(BuildingKind)).Cast<BuildingKind>()
            .Select(kind =>
            {
                // 第四阶段仅复制波次和能源配置，建筑规则与已保存场景一样复用第三阶段定义。
                string path = Defend.Editor.PhaseThreeContent.Definitions + "/" + kind + ".asset";
                var definition = AssetDatabase.LoadAssetAtPath<BuildingDefinitionAsset>(path);
                if (definition == null) throw new InvalidOperationException("八波模拟缺少建筑定义：" + path);
                return definition.CreateDefinition();
            }).ToArray();
        var energy = AssetDatabase.LoadAssetAtPath<EnergyExperimentSettings>(Defend.Editor.PhaseFourSceneBuilder.EnergySettingsPath);
        var battle = AssetDatabase.LoadAssetAtPath<BattleSettingsAsset>(Defend.Editor.PhaseFourSceneBuilder.BattleSettingsPath);
        if (energy == null) throw new InvalidOperationException("八波模拟缺少能源配置：" + Defend.Editor.PhaseFourSceneBuilder.EnergySettingsPath);
        if (battle == null) throw new InvalidOperationException("八波模拟缺少战斗配置：" + Defend.Editor.PhaseFourSceneBuilder.BattleSettingsPath);
        var scenario = new Scenario { Name = "第四阶段八波策略 " + strategy,
            World = new GameWorld(definitions, energy.CreateWorldSettings(), energy.CreateEnergySettings(), battle.CreateSettings()) };
        GameWorld world = scenario.World;
        foreach (BuildingView view in Object.FindAnyObjectByType<WorldPresentation>().AuthoredBuildings)
            Purchase(scenario, view.Definition.Kind, Mathf.FloorToInt(view.transform.position.x), Mathf.FloorToInt(view.transform.position.z));
        var waves = new List<object>();
        var rejected = new List<object>();
        while (!world.Battle.IsFinished && world.CurrentTick < 36000)
        {
            if (world.Battle.State == BattleState.Preparing || world.Battle.State == BattleState.WaveCleared)
            {
                int waveNumber = world.Battle.WaveNumber + 1;
                PhaseThreeAcceptancePlan.Deploy(world, strategy, waveNumber, command =>
                {
                    int credits = world.Credits;
                    world.Enqueue(command); world.Step();
                    if (!world.LastCommandResult.Succeeded)
                        rejected.Add(new { wave = waveNumber, command = command.Kind.ToString(), world.LastCommandResult.Message });
                    else if (command.Kind == WorldCommandKind.Place) scenario.PurchaseSpend += credits - world.Credits;
                    else if (command.Kind == WorldCommandKind.Repair)
                    { scenario.RepairSpend += credits - world.Credits; scenario.RepairCount++; }
                });
                for (int tick = 0; tick < 100; tick++) world.Step();
                Execute(world, WorldCommand.StartWave());
            }
            BattleState before = world.Battle.State;
            world.Step();
            Observe(scenario);
            if (world.CurrentTick % 100 == 0) world.Energy.AssertInvariants();
            if (before == BattleState.Combat && world.Battle.State != BattleState.Combat)
                waves.Add(new { wave = world.Battle.WaveNumber, seconds = world.CurrentTick / (double)world.Settings.TicksPerSecond,
                    state = world.Battle.State.ToString(), coreHealth = world.FindBuilding(world.Battle.CoreBuildingId).Health,
                    world.Credits, actualArrival = world.Buildings.Where(item => item.IsConsumer).Sum(item => item.TotalReceived),
                    waitingTicks = world.Buildings.Sum(item => item.EnergyWaitTicks) });
        }
        world.Energy.AssertInvariants();
        return new { mode = "pure-simulation-eight-wave-regression", strategy, outcome = Summarize(scenario), waves,
            rejectedCommands = rejected, reachedTimeLimit = !world.Battle.IsFinished };
    }

    public static object ScaleBoundary()
    {
        var behavior = new EnemyBehaviorSettings(enabled: true);
        var battle = new BattleSettings(new[] { new WaveDefinition(201, 1000000, 1, 0, new[] { EnemyKind.Heavy }) },
            enemySpeedCellsPerSecond: 0.05, enemyAttackDamage: 1,
            spawnPoints: new[] { new SpawnPoint(4, 20) },
            turretDefinitions: new[] { new TurretDefinition(BuildingKind.Turret, 10, 1, 2000, 4) }, enemyBehavior: behavior);
        var world = new GameWorld(DiagnosticDefinitions(1000000), new WorldSettings(40, 40, 20, 100000),
            new EnergySettings(200, 1, 1000, 0), battle);
        Place(world, BuildingKind.Core, 34, 20);
        Place(world, BuildingKind.Reactor, 6, 24);
        Place(world, BuildingKind.Relay, 8, 22);
        Place(world, BuildingKind.Turret, 11, 22);
        for (int index = 0; index < 96; index++)
            Place(world, index % 2 == 0 ? BuildingKind.Relay : BuildingKind.Turret, 20 + index % 12, 28 + index / 12);
        world.Enqueue(WorldCommand.Place(BuildingKind.Turret, 36, 36)); world.Step();
        if (world.LastCommandResult.Succeeded) throw new Exception("第 101 栋建筑未被拒绝。");
        Execute(world, WorldCommand.StartWave());
        for (int tick = 0; tick < 240; tick++) { world.Step(); AssertBehaviorBudget(world); }
        var initialPositions = world.Battle.Enemies.ToDictionary(enemy => enemy.Id, enemy => new[] { enemy.X, enemy.Z });
        var samples = new double[600];
        long managedBefore = GC.GetTotalMemory(false);
        long maximumEvaluationAge = 0;
        int maximumEvaluations = 0, maximumPaths = 0, maximumBatches = world.Energy.ActiveBatchCount;
        long evaluationsBefore = world.Battle.BehaviorStatistics.TotalEvaluations;
        long pathBuildsBefore = world.Battle.BehaviorStatistics.TotalPathBuilds;
        var timer = new Stopwatch();
        for (int index = 0; index < samples.Length; index++)
        {
            timer.Restart(); world.Step(); timer.Stop();
            samples[index] = timer.Elapsed.TotalMilliseconds;
            AssertBehaviorBudget(world);
            maximumEvaluations = Math.Max(maximumEvaluations, world.Battle.BehaviorStatistics.EvaluationsThisTick);
            maximumPaths = Math.Max(maximumPaths, world.Battle.BehaviorStatistics.PathBuildsThisTick);
            maximumBatches = Math.Max(maximumBatches, world.Energy.ActiveBatchCount);
            foreach (EnemyState enemy in world.Battle.Enemies)
            {
                if (enemy.LastBehaviorEvaluationTick < 0) throw new Exception("预热后仍存在未评估的重装敌人。");
                maximumEvaluationAge = Math.Max(maximumEvaluationAge, world.CurrentTick - enemy.LastBehaviorEvaluationTick);
            }
            if (index % 100 == 0) world.Energy.AssertInvariants();
        }
        if (world.Buildings.Count != 100 || world.Battle.Enemies.Count != 200 || world.Battle.RemainingToSpawn != 1)
            throw new Exception("100 栋建筑、200 个存活敌人的数量边界失效。");
        if (maximumEvaluationAge > 30) throw new Exception("正常 20 次评估预算下存在长期未响应的重装敌人。");
        int moved = world.Battle.Enemies.Count(enemy => initialPositions.TryGetValue(enemy.Id, out double[] start) &&
            (Math.Abs(enemy.X - start[0]) > 0.000001 || Math.Abs(enemy.Z - start[1]) > 0.000001));
        if (moved != 200) throw new Exception("目标评估延期导致部分敌人停止推进。");
        world.Energy.AssertInvariants();
        Array.Sort(samples);
        return new
        {
            mode = "pure-simulation-short-boundary", warmupTicks = 240, sampledTicks = samples.Length,
            sampledSimulationSeconds = samples.Length / (double)world.Settings.TicksPerSecond,
            buildings = world.Buildings.Count, enemies = world.Battle.Enemies.Count, pending = world.Battle.RemainingToSpawn,
            moved, maximumEvaluationAge, maximumEvaluations, maximumPaths,
            totalEvaluationsInSample = world.Battle.BehaviorStatistics.TotalEvaluations - evaluationsBefore,
            totalPathBuildsInSample = world.Battle.BehaviorStatistics.TotalPathBuilds - pathBuildsBefore,
            statistics = world.Battle.BehaviorStatistics,
            milliseconds = new { median = Percentile(samples, 0.5), percentile95 = Percentile(samples, 0.95),
                percentile99 = Percentile(samples, 0.99), maximum = samples[samples.Length - 1] },
            actualArrival = world.Buildings.Where(item => item.IsConsumer).Sum(item => item.TotalReceived),
            waitingTicks = world.Buildings.Sum(item => item.EnergyWaitTicks), maximumBatches,
            balance = world.Energy.GetTotals().BalanceError,
            managedBytesBefore = managedBefore, managedBytesAfter = GC.GetTotalMemory(false),
            limitation = "慢速、高耐久诊断配置的纯模拟短边界结果，不是可见窗口帧率或长期运行认证。"
        };
    }

    /// <summary>高密度局部感知与真实供能同时运行。占地间保留通道，损毁建筑仍计入 100 栋上限。</summary>
    public static object DenseBehaviorBoundary()
    {
        var behavior = new EnemyBehaviorSettings(enabled: true);
        var battle = new BattleSettings(new[] { new WaveDefinition(201, 1000000, 1, 0, new[] { EnemyKind.Heavy }) },
            enemySpeedCellsPerSecond: 2, enemyAttackDamage: 30, enemyAttackIntervalTicks: 20,
            spawnPoints: new[] { new SpawnPoint(9, 15), new SpawnPoint(9, 23), new SpawnPoint(15, 9), new SpawnPoint(23, 9),
                new SpawnPoint(29, 15), new SpawnPoint(29, 23), new SpawnPoint(15, 29), new SpawnPoint(23, 29) },
            turretDefinitions: new[] { new TurretDefinition(BuildingKind.Turret, 10, 1, 20000, 2) },
            enemyBehavior: behavior);
        BuildingDefinition[] definitions = Enum.GetValues(typeof(BuildingKind)).Cast<BuildingKind>().Select(kind =>
        {
            bool supplier = kind == BuildingKind.Reactor || kind == BuildingKind.Relay;
            return new BuildingDefinition(kind, kind.ToString(), 1, 1, 2000000,
                kind == BuildingKind.Reactor ? 50000 : 0, 0, supplier ? 200000 : 0, 1,
                kind == BuildingKind.Reactor ? 2.1 : kind == BuildingKind.Relay ? 6.1 : 0,
                kind == BuildingKind.Core ? 10000000 : 10000, 100, 50, 20);
        }).ToArray();
        var world = new GameWorld(definitions, new WorldSettings(40, 40, 20, 100000),
            new EnergySettings(200, 1, 1000, 0), battle);
        for (int row = 0; row < 10; row++)
            for (int column = 0; column < 10; column++)
            {
                BuildingKind kind = column == 0 || column == 5 ? BuildingKind.Reactor :
                    column == 1 || column == 6 ? BuildingKind.Relay : BuildingKind.Turret;
                if (row == 5 && column == 5) kind = BuildingKind.Core;
                Place(world, kind, 10 + column * 2, 10 + row * 2);
            }
        for (int tick = 0; tick < 60; tick++) world.Step();
        int connectedBuildingsBeforeCombat = world.Buildings.Count(building => world.Energy.Connections.Any(connection =>
            connection.SourceId == building.Id || connection.TargetId == building.Id));
        if (connectedBuildingsBeforeCombat != 100)
            throw new Exception("密集局部检查必须让全部 100 栋建筑加入有效供电连接。");
        int receivedBeforeCombat = world.Buildings.Count(building => building.CanReceive && building.TotalReceived > 0);
        Execute(world, WorldCommand.StartWave());
        for (int tick = 0; tick < 220; tick++) { world.Step(); AssertBehaviorBudget(world); }
        if (world.Battle.Enemies.Count != 200) throw new Exception("密集行为检查预热后没有达到 200 个存活重装。");

        var samples = new double[600];
        var intervalReports = new List<object>();
        var targetIds = new HashSet<long>();
        var attackedBuildingIds = new HashSet<long>();
        var dispatchedSupplierIds = new HashSet<long>();
        var snapshot = world.Battle.BehaviorStatistics;
        long evaluationsBefore = snapshot.TotalEvaluations, pathBuildsBefore = snapshot.TotalPathBuilds;
        long observationsBefore = snapshot.TotalBuildingsObserved, deliveriesBefore = snapshot.TotalDeliveriesObserved;
        long actualArrivalBefore = world.Buildings.Where(building => building.IsConsumer).Sum(building => building.TotalReceived);
        long waitingBefore = world.Buildings.Sum(building => building.EnergyWaitTicks);
        long managedBefore = GC.GetTotalMemory(false);
        long attackCount = 0, strategicEnemyTicks = 0, maximumEligibleEvaluationAge = 0;
        int maximumObservedBuildings = 0, maximumStrategicEnemies = 0, maximumSimultaneousTargets = 0;
        int maximumEvaluations = 0, maximumPaths = 0, maximumBatches = world.Energy.ActiveBatchCount;
        var timer = new Stopwatch();
        for (int index = 0; index < samples.Length; index++)
        {
            timer.Restart(); world.Step(); timer.Stop();
            samples[index] = timer.Elapsed.TotalMilliseconds;
            AssertBehaviorBudget(world);
            snapshot = world.Battle.BehaviorStatistics;
            maximumObservedBuildings = Math.Max(maximumObservedBuildings, snapshot.BuildingsObservedThisTick);
            maximumEvaluations = Math.Max(maximumEvaluations, snapshot.EvaluationsThisTick);
            maximumPaths = Math.Max(maximumPaths, snapshot.PathBuildsThisTick);
            maximumBatches = Math.Max(maximumBatches, world.Energy.ActiveBatchCount);
            BuildingState core = world.FindBuilding(world.Battle.CoreBuildingId);
            int strategicEnemies = 0;
            var currentTargets = new HashSet<long>();
            foreach (EnemyState enemy in world.Battle.Enemies)
            {
                bool strategic = enemy.TargetPurpose == EnemyTargetPurpose.DisruptEnergy ||
                    enemy.TargetPurpose == EnemyTargetPurpose.SuppressTurret;
                if (strategic)
                {
                    strategicEnemies++; strategicEnemyTicks++;
                    currentTargets.Add(enemy.TargetBuildingId); targetIds.Add(enemy.TargetBuildingId);
                }
                if (enemy.LastAttackTick == world.CurrentTick)
                { attackCount++; attackedBuildingIds.Add(enemy.TargetBuildingId); }
                double differenceX = enemy.X - Math.Max(core.X, Math.Min(enemy.X, core.X + core.Definition.Width));
                double differenceZ = enemy.Z - Math.Max(core.Z, Math.Min(enemy.Z, core.Z + core.Definition.Depth));
                // 已贴近核心的敌人直接执行攻击，不再要求完整战略评估。
                if (differenceX * differenceX + differenceZ * differenceZ > 0.36)
                {
                    if (enemy.LastBehaviorEvaluationTick < 0) throw new Exception("预热后存在从未进行目标评估的重装。");
                    maximumEligibleEvaluationAge = Math.Max(maximumEligibleEvaluationAge, world.CurrentTick - enemy.LastBehaviorEvaluationTick);
                }
            }
            foreach (EnergyTransferSnapshot transfer in world.Energy.Transfers) dispatchedSupplierIds.Add(transfer.SourceId);
            maximumStrategicEnemies = Math.Max(maximumStrategicEnemies, strategicEnemies);
            maximumSimultaneousTargets = Math.Max(maximumSimultaneousTargets, currentTargets.Count);
            if (index % 100 == 0 || index == samples.Length - 1)
            {
                world.Energy.AssertInvariants();
                intervalReports.Add(new
                {
                    sampledSeconds = (index + 1) / (double)world.Settings.TicksPerSecond,
                    aliveBuildings = world.Buildings.Count(building => building.IsAlive), strategicEnemies,
                    distinctTargets = currentTargets.Count, snapshot.BuildingsObservedThisTick,
                    snapshot.DeliveriesObservedThisTick, snapshot.EvaluationsThisTick, snapshot.PathBuildsThisTick,
                    actualArrival = world.Buildings.Where(building => building.IsConsumer).Sum(building => building.TotalReceived),
                    waitingTicks = world.Buildings.Sum(building => building.EnergyWaitTicks),
                    facilityDestructions = world.Buildings.Where(building => building.CanOutput ||
                        building.Kind == BuildingKind.Reactor || building.Kind == BuildingKind.Relay).Sum(building => building.DestructionCount)
                });
            }
        }
        world.Energy.AssertInvariants();
        if (world.Buildings.Count != 100 || world.Battle.Enemies.Count != 200 || world.Battle.RemainingToSpawn != 1)
            throw new Exception("密集检查未保持 100 栋未出售建筑、200 个存活敌人及 1 个延期生成名额。");
        if (maximumObservedBuildings < 4000)
            throw new Exception("密集战局没有达到平均每个敌人至少 20 栋建筑的同时感知覆盖。");
        if (maximumStrategicEnemies == 0 || attackCount == 0)
            throw new Exception("密集战局没有同时产生战略选择与真实建筑攻击。");
        if (maximumEligibleEvaluationAge > 40)
            throw new Exception("密集战局中尚未贴近核心的重装存在长期评估饥饿。");
        long actualArrival = world.Buildings.Where(building => building.IsConsumer).Sum(building => building.TotalReceived) - actualArrivalBefore;
        if (actualArrival <= 0 || snapshot.TotalDeliveriesObserved <= deliveriesBefore)
            throw new Exception("采样窗口没有实际到货或局部运输观测。");
        Array.Sort(samples);
        return new
        {
            mode = "pure-simulation-dense-local-behavior", preparationTicks = 60, combatWarmupTicks = 220,
            sampledTicks = samples.Length, sampledSimulationSeconds = samples.Length / (double)world.Settings.TicksPerSecond,
            enemySpeedCellsPerSecond = battle.EnemySpeedCellsPerSecond,
            buildings = world.Buildings.Count, aliveBuildings = world.Buildings.Count(building => building.IsAlive),
            enemies = world.Battle.Enemies.Count, pending = world.Battle.RemainingToSpawn,
            connectedBuildingsBeforeCombat, receivedBeforeCombat,
            receiversWithActualArrival = world.Buildings.Count(building => building.Kind != BuildingKind.Reactor && building.TotalReceived > 0),
            dispatchedSuppliersInSample = dispatchedSupplierIds.Count,
            averageObservedBuildingsPerEnemy = (snapshot.TotalBuildingsObserved - observationsBefore) / (200.0 * samples.Length),
            maximumObservedBuildings, maximumStrategicEnemies, maximumSimultaneousTargets,
            distinctStrategicTargets = targetIds.Count, strategicEnemyTicks, attackCount,
            distinctAttackedBuildings = attackedBuildingIds.Count,
            maximumEligibleEvaluationAge, maximumEvaluations, maximumPaths,
            totalEvaluationsInSample = snapshot.TotalEvaluations - evaluationsBefore,
            totalPathBuildsInSample = snapshot.TotalPathBuilds - pathBuildsBefore,
            totalObservedDeliveriesInSample = snapshot.TotalDeliveriesObserved - deliveriesBefore,
            actualArrival, waitingTicksInSample = world.Buildings.Sum(building => building.EnergyWaitTicks) - waitingBefore,
            maximumBatches, balance = world.Energy.GetTotals().BalanceError, statistics = snapshot,
            milliseconds = new { median = Percentile(samples, 0.5), percentile95 = Percentile(samples, 0.95),
                percentile99 = Percentile(samples, 0.99), maximum = samples[samples.Length - 1] },
            managedBytesBefore = managedBefore, managedBytesAfter = GC.GetTotalMemory(false), intervalReports,
            limitation = "密集供电、较高建筑耐久与敌人生命的短期纯模拟诊断；停下攻击属于正常行为，不认证可见帧率或正式关卡难度。"
        };
    }

    /// <summary>不会暂停、重开、推进模拟或刷新表现。返回直接状态与对象身份供前后比较。</summary>
    public static object RuntimeSnapshot()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("运行快照需要已经处于播放模式。");
        var session = Object.FindAnyObjectByType<GameSessionRoot>();
        if (session == null || session.World == null) throw new Exception("没有正在运行的游戏世界。");
        GameWorld world = session.World;
        world.Energy.AssertInvariants();
        var markers = Object.FindObjectsByType<EnemyTargetView>(FindObjectsInactive.Include);
        var ui = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
        return new
        {
            mode = "read-only-runtime-snapshot", tick = world.CurrentTick, state = world.Battle.State.ToString(),
            world.Battle.WaveNumber, world.Credits, enabled = world.Battle.Settings.EnemyBehavior.Enabled,
            uiObjectIds = ui.Select(item => item.GetEntityId().ToString()).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            activeMarkers = markers.Count(item => item.gameObject.activeInHierarchy),
            markers = markers.Select(item => new { active = item.gameObject.activeInHierarchy,
                x = item.transform.position.x, z = item.transform.position.z,
                containsUi = item.GetComponentInChildren<RectTransform>(true) != null }).ToArray(),
            enemies = world.Battle.Enemies.Select(enemy => new { enemy.Id, kind = enemy.Kind.ToString(), enemy.X, enemy.Z,
                purpose = enemy.TargetPurpose.ToString(), enemy.TargetBuildingId, enemy.TargetX, enemy.TargetZ,
                enemy.LastAttackTick, enemy.TargetAssignedTick, enemy.LastBehaviorEvaluationTick }).ToArray(),
            text = Object.FindObjectsByType<Text>(FindObjectsInactive.Include).Select(item => new { item.name, item.text }).ToArray(),
            balance = world.Energy.GetTotals().BalanceError, statistics = world.Battle.BehaviorStatistics
        };
    }

    private static Scenario BuildComparison(int layout)
    {
        var battle = new BattleSettings(new[] { new WaveDefinition(18, 90, 30, 0,
                new[] { EnemyKind.Heavy, EnemyKind.Normal, EnemyKind.Fast, EnemyKind.Heavy }) },
            enemySpeedCellsPerSecond: 1.5, enemyAttackDamage: 10, enemyAttackIntervalTicks: 20,
            spawnPoints: new[] { new SpawnPoint(4, 20) },
            turretDefinitions: new[] { new TurretDefinition(BuildingKind.Turret, 5, 8, 2000, 8) },
            enemyBehavior: new EnemyBehaviorSettings(enabled: true));
        var scenario = new Scenario
        {
            Name = new[] { "单一末端中继", "独立备用线路", "前置火力保护节点" }[layout],
            World = new GameWorld(DiagnosticDefinitions(120), new WorldSettings(40, 40, 20, 1000),
                new EnergySettings(200, 1, 1000, 0), battle)
        };
        Purchase(scenario, BuildingKind.Core, 34, 20);
        if (layout == 1)
        {
            Purchase(scenario, BuildingKind.Reactor, 6, 25); Purchase(scenario, BuildingKind.Reactor, 6, 15);
            Purchase(scenario, BuildingKind.Relay, 8, 22); Purchase(scenario, BuildingKind.Relay, 8, 18);
            Purchase(scenario, BuildingKind.Turret, 11, 19); Purchase(scenario, BuildingKind.Turret, 11, 20);
            Purchase(scenario, BuildingKind.Turret, 11, 21);
        }
        else
        {
            Purchase(scenario, BuildingKind.Reactor, 6, 25); Purchase(scenario, BuildingKind.Reactor, 5, 24);
            Purchase(scenario, BuildingKind.Relay, 8, 23); Purchase(scenario, BuildingKind.Relay, 8, 20);
            Purchase(scenario, BuildingKind.Turret, layout == 2 ? 5 : 11, 18);
            Purchase(scenario, BuildingKind.Turret, layout == 2 ? 7 : 11, layout == 2 ? 18 : 19);
            Purchase(scenario, BuildingKind.Turret, layout == 2 ? 10 : 11, 20);
        }
        // 等长准备时间只允许真实运输；不给建筑或敌人直接写入缓存和耐久。
        for (int tick = 0; tick < 40; tick++) scenario.World.Step();
        return scenario;
    }

    private static BuildingDefinition[] DiagnosticDefinitions(int relayHealth)
    {
        return Enum.GetValues(typeof(BuildingKind)).Cast<BuildingKind>().Select(kind =>
        {
            bool supplier = kind == BuildingKind.Reactor || kind == BuildingKind.Relay;
            return new BuildingDefinition(kind, kind.ToString(), 1, 1, 20000,
                kind == BuildingKind.Reactor ? 2000 : 0, 0, supplier ? 20000 : 0, 1,
                supplier ? 3.7 : 0, kind == BuildingKind.Relay ? relayHealth : kind == BuildingKind.Core ? 1000 : 600,
                100, 50, 20);
        }).ToArray();
    }

    private static void RepairOneBuilding(Scenario scenario)
    {
        GameWorld world = scenario.World;
        BuildingState target = world.Buildings.Where(item => !item.IsSold &&
                item.Health < item.Definition.MaximumHealth / 2 && world.Credits >= item.Definition.RepairCost)
            .OrderBy(item => item.Health).ThenBy(item => item.Id).FirstOrDefault();
        if (target == null) return;
        int credits = world.Credits;
        world.Enqueue(WorldCommand.Repair(target.Id)); world.Step();
        if (world.LastCommandResult.Succeeded)
        { scenario.RepairSpend += credits - world.Credits; scenario.RepairCount++; }
        else if (scenario.Events.Count < 100)
            scenario.Events.Add(new { tick = world.CurrentTick, action = "维修被拒绝", target.Id, world.LastCommandResult.Message });
        Observe(scenario);
    }

    private static void Observe(Scenario scenario)
    {
        GameWorld world = scenario.World;
        AssertBehaviorBudget(world);
        foreach (EnemyState enemy in world.Battle.Enemies)
        {
            if (enemy.TargetPurpose == EnemyTargetPurpose.DisruptEnergy) scenario.DisruptionTargetTicks++;
            if (enemy.TargetPurpose == EnemyTargetPurpose.SuppressTurret) scenario.SuppressionTargetTicks++;
            if (enemy.LastAttackTick != world.CurrentTick) continue;
            scenario.Attacks++;
            if (enemy.TargetPurpose == EnemyTargetPurpose.DisruptEnergy && scenario.FirstEnergyAttackTick < 0)
            {
                scenario.FirstEnergyAttackTick = world.CurrentTick;
                scenario.Events.Add(new { tick = world.CurrentTick, action = "首次能源设施攻击", enemy.Id, enemy.TargetBuildingId });
            }
        }
        if (scenario.FirstSupplyDestroyedTick < 0 && world.Buildings.Any(item =>
                (item.Kind == BuildingKind.Reactor || item.Kind == BuildingKind.Relay) && item.DestructionCount > 0))
        {
            scenario.FirstSupplyDestroyedTick = world.CurrentTick;
            scenario.ArrivalAtSupplyDestruction = world.Buildings.Where(item => item.IsConsumer).Sum(item => item.TotalReceived);
            scenario.WaitAtSupplyDestruction = world.Buildings.Sum(item => item.EnergyWaitTicks);
            scenario.Events.Add(new { tick = world.CurrentTick, action = "首次能源设施损毁" });
        }
        if (scenario.FirstSupplyDestroyedTick >= 0 && scenario.ArrivalTwoSecondsAfterDestruction < 0 &&
            world.CurrentTick >= scenario.FirstSupplyDestroyedTick + world.Settings.TicksPerSecond * 2)
        {
            scenario.ArrivalTwoSecondsAfterDestruction = world.Buildings.Where(item => item.IsConsumer).Sum(item => item.TotalReceived);
            scenario.WaitTwoSecondsAfterDestruction = world.Buildings.Sum(item => item.EnergyWaitTicks);
        }
    }

    private static object Summarize(Scenario scenario)
    {
        GameWorld world = scenario.World;
        BuildingState core = world.FindBuilding(world.Battle.CoreBuildingId);
        return new
        {
            scenario.Name, state = world.Battle.State.ToString(), seconds = world.CurrentTick / (double)world.Settings.TicksPerSecond,
            world.Battle.WaveNumber, world.Battle.Kills, world.Credits, scenario.PurchaseSpend, scenario.RepairSpend, scenario.RepairCount,
            coreHealth = core.Health, coreDamage = core.Definition.MaximumHealth - core.Health, coreShield = core.StoredEnergy,
            actualArrival = world.Buildings.Where(item => item.IsConsumer).Sum(item => item.TotalReceived),
            waitingTicks = world.Buildings.Sum(item => item.EnergyWaitTicks),
            damageDealt = world.Buildings.Sum(item => item.DamageDealt),
            facilityDestructions = world.Buildings.Where(item => item.Kind == BuildingKind.Reactor || item.Kind == BuildingKind.Relay).Sum(item => item.DestructionCount),
            scenario.DisruptionTargetTicks, scenario.SuppressionTargetTicks, scenario.Attacks,
            scenario.FirstEnergyAttackTick, scenario.FirstSupplyDestroyedTick,
            actualArrivalTwoSecondsAfterLoss = scenario.ArrivalTwoSecondsAfterDestruction < 0 ? -1 :
                scenario.ArrivalTwoSecondsAfterDestruction - scenario.ArrivalAtSupplyDestruction,
            waitingTicksTwoSecondsAfterLoss = scenario.WaitTwoSecondsAfterDestruction < 0 ? -1 :
                scenario.WaitTwoSecondsAfterDestruction - scenario.WaitAtSupplyDestruction,
            balance = world.Energy.GetTotals().BalanceError, totals = world.Energy.GetTotals(),
            statistics = world.Battle.BehaviorStatistics, events = scenario.Events,
            buildings = world.Buildings.Select(item => new { item.Id, kind = item.Kind.ToString(), item.X, item.Z,
                item.Health, item.StoredEnergy, item.TotalReceived, item.EnergyWaitTicks, item.DestructionCount }).ToArray()
        };
    }

    private static void CheckReferences(MonoBehaviour component, List<string> issues, string optional = null)
    {
        var property = new SerializedObject(component).GetIterator();
        while (property.NextVisible(true))
            if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != optional &&
                property.objectReferenceValue == null) issues.Add(component.GetType().Name + "." + property.propertyPath);
    }

    private static void AssertBehaviorBudget(GameWorld world)
    {
        var statistics = world.Battle.BehaviorStatistics;
        var settings = world.Battle.Settings.EnemyBehavior;
        if (statistics.EvaluationsThisTick > settings.MaximumEvaluationsPerTick ||
            statistics.PathBuildsThisTick > settings.MaximumPathBuildsPerTick)
            throw new Exception("敌人目标评估或战略路径构建超出每步预算。");
    }

    private static double Percentile(double[] sortedSamples, double fraction)
        => sortedSamples[Math.Min(sortedSamples.Length - 1, Math.Max(0, (int)Math.Ceiling(sortedSamples.Length * fraction) - 1))];
    private static void Execute(GameWorld world, WorldCommand command)
    {
        world.Enqueue(command); world.Step();
        if (!world.LastCommandResult.Succeeded) throw new Exception(world.LastCommandResult.Message);
    }
    private static void Place(GameWorld world, BuildingKind kind, int x, int z) => Execute(world, WorldCommand.Place(kind, x, z));
    private static void Purchase(Scenario scenario, BuildingKind kind, int x, int z)
    {
        int credits = scenario.World.Credits;
        Place(scenario.World, kind, x, z);
        scenario.PurchaseSpend += credits - scenario.World.Credits;
    }
}
