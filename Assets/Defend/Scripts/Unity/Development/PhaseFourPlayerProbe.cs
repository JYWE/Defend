#if DEBUG && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Defend.Simulation;
using UnityEngine;

namespace Defend.Unity
{
    /// <summary>显式开启的第四阶段短验收。诊断波次仅覆盖运行时副本；正常 Unity Update 推进，不直接调用世界步进。</summary>
    public sealed class PhaseFourPlayerProbe : MonoBehaviour
    {
        [Serializable] private sealed class TargetObservation
        {
            public long tick, enemy, building;
            public string purpose;
            public double x, z;
        }

        [Serializable] private sealed class Report
        {
            public string mode = "第四阶段诊断波次，正常一倍速 Update；不作为普通关卡平衡或可见帧率证据";
            public string error, processor, graphics, battleState;
            public bool final, passed, diagnosticConfigurationBound, energyTargetSeen, energyFacilityDamageSeen, pausedDuringCombat, pausedTickStable, sameUi, restartedWorld, clearedEnemyPresentation;
            public int uiCount, peakEnemies, restarts, width, height;
            public long tick, balance, totalReceived, energyWaitTicks, evaluations, pathBuilds, targetSwitches;
            public double elapsedSeconds, stepP95, stepP99, maximumPendingSeconds;
            public List<TargetObservation> observations = new List<TargetObservation>();
        }

        private readonly Report report = new Report();
        private readonly List<double> stepTimes = new List<double>();
        private GameSessionRoot session;
        private BattleSettingsAsset diagnosticSettings;
        private RectTransform[] initialUi;
        private string output, receivedError;
        private double began, nextSample, firstDamageAt = -1, pauseAt = -1, finishAt = -1;
        private long pausedTick;
        private bool startedWave, captured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartWhenRequested()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            if (!Debug.isDebugBuild || Array.IndexOf(arguments, "-phase-four-smoke") < 0) return;
            var probe = new GameObject("第四阶段短运行验收").AddComponent<PhaseFourPlayerProbe>();
            int outputIndex = Array.IndexOf(arguments, "-phase-four-output");
            probe.output = outputIndex >= 0 && outputIndex + 1 < arguments.Length ? arguments[outputIndex + 1] :
                Path.Combine(Application.persistentDataPath, "PhaseFour");
            Directory.CreateDirectory(probe.output);
            probe.began = Time.realtimeSinceStartupAsDouble;
            probe.report.processor = SystemInfo.processorType;
            probe.report.graphics = SystemInfo.graphicsDeviceName;
            Application.runInBackground = true;
            Application.logMessageReceived += probe.ReceiveLog;
            try
            {
                probe.session = FindAnyObjectByType<GameSessionRoot>();
                if (probe.session == null) throw new InvalidOperationException("第四阶段场景缺少会话入口。");
                var original = Resources.FindObjectsOfTypeAll<BattleSettingsAsset>().Single(item => item.CreateSettings().EnemyBehavior.Enabled);
                probe.diagnosticSettings = Instantiate(original);
                // 起始布局、建筑参数和能源参数沿用关卡；缩短接敌时间并提高敌人耐久，以覆盖设施破坏及清理。
                JsonUtility.FromJsonOverwrite("{\"waves\":[{\"enemyCount\":3,\"enemyHealth\":2000,\"spawnIntervalTicks\":40,\"clearReward\":0,\"enemyPattern\":[2],\"entranceIndex\":0,\"briefing\":\"诊断波次：检查重装目标和供电受损\"}],\"spawnPoints\":[{\"x\":18,\"y\":16}]}", probe.diagnosticSettings);
                probe.session.ConfigureBattle(probe.diagnosticSettings, FindAnyObjectByType<BattlePresentation>(), FindAnyObjectByType<BattleControlPanel>());
            }
            catch (Exception exception) { probe.receivedError = exception.ToString(); }
        }

        private void ReceiveLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                receivedError = message + "\n" + stack;
        }

        private void Update()
        {
            try
            {
                double now = Time.realtimeSinceStartupAsDouble;
                if (receivedError != null) throw new InvalidOperationException(receivedError);
                if (now - began > 100) throw new TimeoutException("重装能源袭击短运行未在时限内完成。");
                if (session == null || session.World == null) return;
                if (initialUi == null)
                {
                    var settings = session.Battle.Settings;
                    report.diagnosticConfigurationBound = settings.EnemyBehavior.Enabled && settings.Waves.Count == 1 &&
                        settings.Waves[0].EnemyCount == 3 && settings.Waves[0].EnemyHealth == 2000 &&
                        settings.SpawnPoints.Count == 1 && settings.SpawnPoints[0].X == 18 && settings.SpawnPoints[0].Z == 16;
                    if (!report.diagnosticConfigurationBound) throw new InvalidOperationException("诊断配置副本未绑定到实际运行世界。");
                    initialUi = FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
                    report.uiCount = initialUi.Length;
                    report.width = Screen.width;
                    report.height = Screen.height;
                    session.SimulationStepMeasured += MeasureStep;
                }
                if (!startedWave && now - began >= 4)
                {
                    startedWave = true;
                    session.StartWave();
                }
                if (finishAt > 0)
                {
                    if (now >= finishAt)
                    {
                        CheckUi();
                        session.World.Energy.AssertInvariants();
                        report.clearedEnemyPresentation = FindObjectsByType<EnemyView>().Length == 0 && FindObjectsByType<EnemyTargetView>().Length == 0;
                        Save(true, report.energyTargetSeen && report.energyFacilityDamageSeen && report.pausedDuringCombat && report.pausedTickStable &&
                            report.sameUi && report.restartedWorld && report.clearedEnemyPresentation);
                        Application.Quit(report.passed ? 0 : 1);
                        enabled = false;
                    }
                    return;
                }
                if (pauseAt > 0)
                {
                    if (now - pauseAt < 1.5) return;
                    report.pausedTickStable = session.World.CurrentTick == pausedTick;
                    for (int index = 0; index < 3; index++)
                    {
                        GameWorld previous = session.World;
                        session.Restart();
                        report.restartedWorld = !ReferenceEquals(previous, session.World);
                        if (!report.restartedWorld || session.Battle.Enemies.Count != 0 || session.Battle.State != BattleState.Preparing)
                            throw new InvalidOperationException("重开没有恢复准备状态。");
                        report.restarts++;
                    }
                    finishAt = now + 1;
                    return;
                }
                report.peakEnemies = Math.Max(report.peakEnemies, session.Battle.Enemies.Count);
                report.maximumPendingSeconds = Math.Max(report.maximumPendingSeconds, session.PendingSimulationSeconds);
                if (now >= nextSample)
                {
                    nextSample = now + 0.5;
                    session.World.Energy.AssertInvariants();
                    CheckUi();
                    foreach (EnemyState enemy in session.Battle.Enemies)
                    {
                        if (enemy.TargetPurpose == EnemyTargetPurpose.DisruptEnergy)
                        {
                            report.energyTargetSeen = true;
                            session.Select(enemy.TargetBuildingId);
                        }
                        if (report.observations.Count < 512)
                            report.observations.Add(new TargetObservation { tick = session.World.CurrentTick, enemy = enemy.Id,
                                building = enemy.TargetBuildingId, purpose = enemy.TargetPurpose.ToString(), x = enemy.TargetX, z = enemy.TargetZ });
                    }
                    report.energyFacilityDamageSeen |= session.World.Buildings.Any(item =>
                        (item.Kind == BuildingKind.Reactor || item.Kind == BuildingKind.Relay) && item.Health < item.Definition.MaximumHealth);
                    if (report.energyFacilityDamageSeen && firstDamageAt < 0) firstDamageAt = now;
                    RecordBattle();
                    Save(false, false);
                }
                if (report.energyTargetSeen && !captured)
                {
                    captured = true;
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "target.png"));
                }
                if ((firstDamageAt > 0 && now - firstDamageAt >= 6) || session.Battle.IsFinished)
                {
                    if (!report.energyTargetSeen || !report.energyFacilityDamageSeen)
                        throw new InvalidOperationException("战斗未覆盖重装锁定与攻击能源设施。");
                    RecordBattle();
                    report.pausedDuringCombat = session.Battle.State == BattleState.Combat;
                    if (!report.pausedDuringCombat) throw new InvalidOperationException("战斗已结束，不能把终局冻结当作正常暂停证据。");
                    if (!session.Paused) session.TogglePause();
                    pausedTick = session.World.CurrentTick;
                    pauseAt = now;
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "damage.png"));
                }
            }
            catch (Exception exception)
            {
                report.error = exception.ToString();
                Save(true, false);
                Application.Quit(1);
                enabled = false;
            }
        }

        private void CheckUi()
        {
            var current = FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
            report.sameUi = current.Length == initialUi.Length && initialUi.All(item => current.Contains(item));
            if (!report.sameUi) throw new InvalidOperationException("运行期间 UI 对象集合发生改变。");
        }

        private void RecordBattle()
        {
            report.tick = session.World.CurrentTick;
            report.battleState = session.Battle.State.ToString();
            report.balance = session.World.Energy.GetTotals().BalanceError;
            report.totalReceived = session.World.Buildings.Where(item => item.IsConsumer).Sum(item => item.TotalReceived);
            report.energyWaitTicks = session.World.Buildings.Sum(item => item.EnergyWaitTicks);
            var statistics = session.Battle.BehaviorStatistics;
            report.evaluations = statistics.TotalEvaluations;
            report.pathBuilds = statistics.TotalPathBuilds;
            report.targetSwitches = statistics.TotalTargetSwitches;
        }

        private void MeasureStep(double milliseconds) { if (stepTimes.Count < 5000) stepTimes.Add(milliseconds); }
        private static double Percentile(List<double> values, double fraction)
        {
            if (values.Count == 0) return -1;
            values.Sort();
            return values[Math.Min(values.Count - 1, (int)Math.Ceiling(values.Count * fraction) - 1)];
        }
        private void Save(bool final, bool passed)
        {
            report.final = final; report.passed = passed;
            report.elapsedSeconds = Time.realtimeSinceStartupAsDouble - began;
            if (final) { report.stepP95 = Percentile(stepTimes, 0.95); report.stepP99 = Percentile(stepTimes, 0.99); }
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
        }
        private void OnDestroy()
        {
            Application.logMessageReceived -= ReceiveLog;
            if (session != null) session.SimulationStepMeasured -= MeasureStep;
            if (diagnosticSettings != null) Destroy(diagnosticSettings);
        }
    }
}
#endif
