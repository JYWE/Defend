#if DEBUG && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Defend.Simulation;
using UnityEngine;

namespace Defend.Unity
{
    /// <summary>完整关卡按一倍速实际运行；仅显式参数启用，不直接推进模拟，不创建 UI。</summary>
    public sealed class PhaseThreePlayerProbe : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public bool final, passed, sameWorld, sameUi;
            public string error, state, processor, graphics;
            public int strategy, wave, kills, buildings, peakEnemies, uiCount;
            public long tick, balance, damage, energyWaitTicks;
            public double elapsedSeconds, frameP95, frameP99, stepP95, stepP99, maximumPendingSeconds;
        }
        private readonly Report report = new Report();
        private readonly List<double> frameTimes = new List<double>();
        private readonly List<double> stepTimes = new List<double>();
        private string output;
        private double began, nextCheck, startWaveAt, finishAt;
        private int deployedWave;
        private GameSessionRoot session;
        private GameWorld originalWorld;
        private RectTransform[] initialUi;
        private string error;
        private bool selected, finishedCapture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartWhenRequested()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!Debug.isDebugBuild || Array.IndexOf(args, "-phase-three-smoke") < 0) return;
            var probe = new GameObject("第三阶段关卡验收").AddComponent<PhaseThreePlayerProbe>();
            int outputIndex = Array.IndexOf(args, "-phase-three-output");
            probe.output = outputIndex >= 0 ? args[outputIndex + 1] : Path.Combine(Application.persistentDataPath, "PhaseThree");
            int strategyIndex = Array.IndexOf(args, "-phase-three-strategy");
            probe.report.strategy = strategyIndex >= 0 ? int.Parse(args[strategyIndex + 1]) : 1;
            Directory.CreateDirectory(probe.output);
            probe.began = Time.realtimeSinceStartupAsDouble;
            probe.report.processor = SystemInfo.processorType; probe.report.graphics = SystemInfo.graphicsDeviceName;
            Application.runInBackground = true;
            Application.logMessageReceived += probe.Log;
        }
        private void Log(string message, string stack, LogType type)
        { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) error = message + "\n" + stack; }
        private void Update()
        {
            try
            {
                double now = Time.realtimeSinceStartupAsDouble;
                if (session == null)
                {
                    session = FindAnyObjectByType<GameSessionRoot>();
                    if (session == null || session.World == null) { session = null; return; }
                    originalWorld = session.World;
                    initialUi = FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
                    session.SimulationStepMeasured += MeasureStep;
                }
                if (error != null) throw new Exception(error);
                if (now - began > 1100) throw new TimeoutException("关卡超过验收时间上限。");
                if (now - began > 15 && !session.Battle.IsFinished)
                {
                    frameTimes.Add(Time.unscaledDeltaTime * 1000.0);
                    report.maximumPendingSeconds = Math.Max(report.maximumPendingSeconds, session.PendingSimulationSeconds);
                }
                report.peakEnemies = Math.Max(report.peakEnemies, session.Battle.Enemies.Count);
                if (now >= nextCheck)
                {
                    nextCheck = now + 10;
                    session.World.Energy.AssertInvariants();
                    var ui = FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
                    report.sameUi = initialUi.Length == ui.Length && initialUi.All(item => ui.Contains(item));
                    report.sameWorld = ReferenceEquals(originalWorld, session.World);
                    if (!report.sameUi || !report.sameWorld) throw new Exception("UI 对象或跨波次世界引用发生改变。");
                    Save(false, false);
                }
                var battle = session.Battle;
                if ((battle.State == BattleState.Preparing || battle.State == BattleState.WaveCleared) && deployedWave == battle.WaveNumber)
                {
                    deployedWave++;
                    PhaseThreeAcceptancePlan.Deploy(session.World, report.strategy, deployedWave, session.Submit);
                    startWaveAt = now + 5;
                }
                if (startWaveAt > 0 && now >= startWaveAt)
                { session.StartWave(); startWaveAt = 0; }
                if (!selected && battle.WaveNumber == 4 && battle.Enemies.Count >= 6)
                {
                    selected = true;
                    var turret = session.World.Buildings.First(item => item.Kind == BuildingKind.AreaTurret);
                    session.Select(turret.Id);
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "battle.png"));
                }
                if (battle.IsFinished && finishAt == 0)
                {
                    finishAt = now + 3;
                }
                if (finishAt > 0 && now >= finishAt - 2 && !finishedCapture)
                {
                    finishedCapture = true;
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "result.png"));
                }
                if (finishAt > 0 && now >= finishAt)
                {
                    Save(true, battle.State == BattleState.Victory);
                    Application.Quit(report.passed ? 0 : 1);
                    enabled = false;
                }
            }
            catch (Exception exception)
            {
                report.error = exception.ToString(); Save(true, false); Application.Quit(1); enabled = false;
            }
        }
        private void MeasureStep(double milliseconds) { if (Time.realtimeSinceStartupAsDouble - began > 15) stepTimes.Add(milliseconds); }
        private static double Percentile(List<double> values, double percentile)
        { if (values.Count == 0) return -1; values.Sort(); return values[(int)Math.Min(values.Count - 1, Math.Ceiling(values.Count * percentile) - 1)]; }
        private void Save(bool final, bool passed)
        {
            report.final = final; report.passed = passed;
            report.elapsedSeconds = Time.realtimeSinceStartupAsDouble - began;
            if (session?.World != null)
            {
                report.state = session.Battle.State.ToString(); report.wave = session.Battle.WaveNumber; report.kills = session.Battle.Kills;
                report.tick = session.World.CurrentTick; report.balance = session.World.Energy.GetTotals().BalanceError;
                report.buildings = session.World.Buildings.Count; report.uiCount = initialUi.Length;
                report.damage = session.World.Buildings.Sum(item => item.DamageDealt);
                report.energyWaitTicks = session.World.Buildings.Sum(item => item.EnergyWaitTicks);
            }
            if (final)
            {
                report.frameP95 = Percentile(frameTimes, 0.95); report.frameP99 = Percentile(frameTimes, 0.99);
                report.stepP95 = Percentile(stepTimes, 0.95); report.stepP99 = Percentile(stepTimes, 0.99);
            }
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
        }
        private void OnDestroy()
        { Application.logMessageReceived -= Log; if (session != null) session.SimulationStepMeasured -= MeasureStep; }
    }
}
#endif
