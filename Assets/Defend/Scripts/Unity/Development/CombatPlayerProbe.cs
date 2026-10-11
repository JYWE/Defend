#if DEBUG && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Defend.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Profiling;

namespace Defend.Unity
{
    /// <summary>显式启用的独立开发包验收；正常 Update 驱动世界，不创建任何 UI。</summary>
    public sealed class CombatPlayerProbe : MonoBehaviour
    {
        private GameSessionRoot session;
        private GameWorld originalWorld;
        private readonly Report report = new Report();
        private readonly List<double> frameTimes = new List<double>(120000);
        private readonly List<double> stepTimes = new List<double>(50000);
        private long recordedFrames, recordedSteps;
        private readonly List<MemorySample> memory = new List<MemorySample>();
        private RectTransform[] originalUi;
        private string output;
        private double began, lastFrame, nextMemory, prepareUntil, stressDuration;
        private int stage;
        private long previousTick;
        private bool originalBackground;
        private bool expanded;
        private double nextWaveAt;
        private int requestedWave;
        private bool captureProfile;
        private double profileEnds;
        private string runtimeError;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateWhenRequested()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!Debug.isDebugBuild || Array.IndexOf(args, "-combat-smoke") < 0) return;
            var host = new GameObject("Combat Player Acceptance Probe");
            DontDestroyOnLoad(host);
            host.AddComponent<CombatPlayerProbe>().Initialize(args);
        }

        private void Initialize(string[] args)
        {
            output = Argument(args, "-combat-output", Path.Combine(Application.persistentDataPath, "CombatValidation"));
            stressDuration = double.Parse(Argument(args, "-combat-stress-seconds", "0"), System.Globalization.CultureInfo.InvariantCulture);
            captureProfile = Array.IndexOf(args, "-combat-profile") >= 0;
            Directory.CreateDirectory(output);
            began = lastFrame = Time.realtimeSinceStartupAsDouble;
            originalBackground = Application.runInBackground;
            Application.runInBackground = true;
            SceneManager.sceneLoaded += Loaded;
            Application.logMessageReceived += Log;
            report.startedUtc = DateTime.UtcNow.ToString("O");
            report.processor = SystemInfo.processorType;
            report.graphics = SystemInfo.graphicsDeviceName;
            report.memoryMegabytes = SystemInfo.systemMemorySize;
            report.unity = Application.unityVersion;
            report.mode = stressDuration > 0 ? "stress" : "five-waves-restart-defeat";
        }

        private static string Argument(string[] args, string name, string fallback)
        {
            int index = Array.IndexOf(args, name);
            if (index < 0) return fallback;
            if (index + 1 >= args.Length || args[index + 1].StartsWith("-"))
                throw new ArgumentException(name + " 后缺少参数。");
            return args[index + 1];
        }

        private void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (stressDuration > 0 && scene.name != "CombatStress")
            {
                SceneManager.LoadScene("CombatStress");
                return;
            }
            session = FindAnyObjectByType<GameSessionRoot>();
            originalUi = ReadUi();
            report.scene = scene.name;
            report.initialUiCount = originalUi.Length;
        }

        private static bool SameUi(RectTransform[] previous) { RectTransform[] current = ReadUi(); return previous.Length == current.Length && previous.All(item => item != null && current.Contains(item)); }
        private static RectTransform[] ReadUi()
            => FindObjectsByType<RectTransform>(FindObjectsInactive.Include);

        private void Update()
        {
            if (stage == 9) return;
            try
            {
                if (runtimeError != null) throw new InvalidOperationException(runtimeError);
                double now = Time.realtimeSinceStartupAsDouble;
                if (now - began > Math.Max(600, stressDuration + 120)) throw new TimeoutException("独立程序验收超时。");
                if (session == null || session.World == null || session.Battle == null) return;
                report.maximumPendingSeconds = Math.Max(report.maximumPendingSeconds, session.PendingSimulationSeconds);
                if (stage == 2 && now - prepareUntil > 15)
                    report.warmMaximumPendingSeconds = Math.Max(report.warmMaximumPendingSeconds, session.PendingSimulationSeconds);
                if (captureProfile && profileEnds == 0 && stage == 2 && now - prepareUntil > 30)
                {
                    report.profilePath = Path.Combine(output, "cpu-profile.raw");
                    Profiler.logFile = report.profilePath;
                    Profiler.enableBinaryLog = true;
                    Profiler.enabled = true;
                    profileEnds = now + 10;
                }
                if (captureProfile && profileEnds > 0 && now >= profileEnds && Profiler.enabled)
                {
                    Profiler.enabled = false;
                    Profiler.enableBinaryLog = false;
                }
                if (stage == 0)
                {
                    originalWorld = session.World;
                    session.SimulationStepMeasured += MeasureStep;
                    report.initialBuildings = session.World.Buildings.Count;
                    report.width = Screen.width;
                    report.height = Screen.height;
                    report.cameraPitch = Camera.main.transform.eulerAngles.x;
                    Require(Math.Abs(report.cameraPitch - 70) < 0.1, "场景摄像机不是 70 度斜俯视。");
                    prepareUntil = now + 6;
                    nextMemory = now;
                    stage = 1;
                }
                if (now >= nextMemory)
                {
                    nextMemory = now + 10;
                    CheckWorld();
                    memory.Add(new MemorySample { seconds = now - began,
                        managedBytes = GC.GetTotalMemory(false), allocatedBytes = Profiler.GetTotalAllocatedMemoryLong(),
                        enemies = session.Battle.Enemies.Count, batches = session.World.Energy.ActiveBatchCount,
                        pendingSearches = session.World.Energy.PlanningStatistics.PendingSearchCount });
                    WriteReport(false);
                }
                if (stage == 1 && now >= prepareUntil)
                {
                    session.StartWave();
                    requestedWave = 1;
                    stage = 2;
                }
                if (stage == 2)
                {
                    Require(ReferenceEquals(originalWorld, session.World), "波次过程中世界被替换。");
                    report.peakEnemies = Math.Max(report.peakEnemies, session.Battle.Enemies.Count);
                    report.peakBatches = Math.Max(report.peakBatches, session.World.Energy.ActiveBatchCount);
                    report.completedWaves = Math.Max(report.completedWaves,
                        session.Battle.State == BattleState.Combat ? session.Battle.WaveNumber - 1 : session.Battle.WaveNumber);
                    if (session.Battle.Enemies.Count > 5 && string.IsNullOrEmpty(report.battleScreenshot))
                    {
                        report.battleScreenshot = Path.Combine(output, "battle-visible.png");
                        ScreenCapture.CaptureScreenshot(report.battleScreenshot);
                    }
                    if (stressDuration > 0)
                    {
                        Require(session.Battle.State != BattleState.Defeat, "压力场景提前失败。");
                        if (now - prepareUntil >= stressDuration)
                        {
                            Require(report.initialBuildings == 100 && report.peakEnemies == 200, "压力场景未达到100建筑和200敌人。");
                            Finish();
                        }
                    }
                    else if (session.Battle.State == BattleState.WaveCleared)
                    {
                        if (session.Battle.WaveNumber == 2 && !expanded)
                        {
                            foreach (Vector2Int point in new[] { new Vector2Int(20,14), new Vector2Int(20,26),
                                new Vector2Int(14,17), new Vector2Int(26,17), new Vector2Int(14,23), new Vector2Int(26,23) })
                                session.Submit(WorldCommand.Place(BuildingKind.Turret, point.x, point.y));
                            expanded = true;
                            report.expansionTurrets = 6;
                            nextWaveAt = now + 5;
                        }
                        if (now >= nextWaveAt && requestedWave <= session.Battle.WaveNumber)
                        {
                            requestedWave = session.Battle.WaveNumber + 1;
                            session.StartWave();
                        }
                    }
                    else if (session.Battle.State == BattleState.Defeat) throw new InvalidOperationException("默认布局未通过五波流程，核心提前失败。");
                    else if (session.Battle.State == BattleState.Victory)
                    {
                        CheckWorld();
                        report.victory = true;
                        report.victoryTick = session.World.CurrentTick;
                        report.victoryKills = session.Battle.Kills;
                        report.victoryConsumed = session.World.Energy.GetTotals().Consumed;
                        report.victoryScreenshot = Path.Combine(output, "victory-visible.png");
                        ScreenCapture.CaptureScreenshot(report.victoryScreenshot);
                        prepareUntil = now + 1;
                        stage = 3;
                    }
                }
                else if (stage == 3 && now >= prepareUntil)
                {
                    session.Restart();
                    Require(!ReferenceEquals(originalWorld, session.World), "重新开始没有创建新世界。");
                    Require(session.Battle.State == BattleState.Preparing, "重新开始没有回到准备阶段。");
                    Require(session.World.Buildings.Count == report.initialBuildings, "重新开始没有恢复初始建筑。");
                    Require(SameUi(originalUi), "重新开始创建、替换或删除了UI对象。");
                    report.restart = true;
                    BuildingState core = session.World.Buildings.First(item => item.Kind == BuildingKind.Core);
                    session.Submit(WorldCommand.Damage(core.Id, int.MaxValue));
                    stage = 4;
                }
                else if (stage == 4 && session.Battle.State == BattleState.Defeat)
                {
                    report.defeat = true;
                    Finish();
                }
            }
            catch (Exception exception) { Finish(exception.ToString()); }
        }

        private void LateUpdate()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (stage == 2 && now - prepareUntil > 15)
                AddBounded(frameTimes, (now - lastFrame) * 1000, 120000, recordedFrames++);
            lastFrame = now;
        }

        private void MeasureStep(double milliseconds)
        {
            if (stage == 2 && Time.realtimeSinceStartupAsDouble - prepareUntil > 15)
                AddBounded(stepTimes, milliseconds, 50000, recordedSteps++);
        }

        private static void AddBounded(List<double> samples, double value, int capacity, long index)
        {
            // 采样容器有固定上限；长时间验收保留最近窗口，不让诊断器本身制造无限内存增长。
            if (samples.Count < capacity) samples.Add(value);
            else samples[(int)(index % capacity)] = value;
        }

        private void CheckWorld()
        {
            session.World.Energy.AssertInvariants();
            Require(SameUi(originalUi), "UI对象身份集合发生变化。");
            if (ReferenceEquals(originalWorld, session.World))
                Require(session.World.CurrentTick >= previousTick, "同局逻辑步发生回退。");
            previousTick = session.World.CurrentTick;
            report.finalTick = session.World.CurrentTick;
            report.balanceError = session.World.Energy.GetTotals().BalanceError;
            report.consumed = session.World.Energy.GetTotals().Consumed;
            report.maximumPendingSeconds = Math.Max(report.maximumPendingSeconds, session.PendingSimulationSeconds);
        }

        private void Finish(string failure = null)
        {
            if (stage == 9) return;
            stage = 9;
            report.failure = failure ?? "";
            report.success = failure == null;
            try { if (session != null) CheckWorld(); WriteReport(true); }
            catch (Exception exception) { report.success = false; report.failure += exception; WriteReport(true); }
            Cleanup();
            Application.Quit(report.success ? 0 : 1);
        }

        private void WriteReport(bool final)
        {
            report.elapsedSeconds = Time.realtimeSinceStartupAsDouble - began;
            report.final = final;
            report.frameSamples = frameTimes.Count;
            report.stepSamples = stepTimes.Count;
            if (final)
            {
                frameTimes.Sort(); stepTimes.Sort();
                report.frameP50 = Percentile(frameTimes, 0.5); report.frameP95 = Percentile(frameTimes, 0.95);
                report.frameP99 = Percentile(frameTimes, 0.99); report.frameMaximum = Percentile(frameTimes, 1);
                report.stepP50 = Percentile(stepTimes, 0.5); report.stepP95 = Percentile(stepTimes, 0.95);
                report.stepP99 = Percentile(stepTimes, 0.99); report.stepMaximum = Percentile(stepTimes, 1);
            }
            report.memorySamples = memory.ToArray();
            File.WriteAllText(Path.Combine(output, "combat-player-report.json"), JsonUtility.ToJson(report, true));
        }

        private static double Percentile(List<double> values, double percentile)
            => values.Count == 0 ? 0 : values[Math.Min(values.Count - 1, (int)Math.Ceiling(values.Count * percentile) - 1)];
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = message + "\n" + stack;
        }
        private void Cleanup()
        {
            if (captureProfile) { Profiler.enabled = false; Profiler.enableBinaryLog = false; }
            if (session != null) session.SimulationStepMeasured -= MeasureStep;
            SceneManager.sceneLoaded -= Loaded;
            Application.logMessageReceived -= Log;
            Application.runInBackground = originalBackground;
        }
        private void OnDestroy() => Cleanup();

        [Serializable] private sealed class MemorySample
        {
            public double seconds; public long managedBytes, allocatedBytes;
            public int enemies, batches, pendingSearches;
        }
        [Serializable] private sealed class Report
        {
            public string startedUtc, processor, graphics, unity, mode, scene, failure;
            public bool success, final, victory, restart, defeat;
            public int memoryMegabytes, initialUiCount, initialBuildings, width, height, peakEnemies, peakBatches, completedWaves, expansionTurrets;
            public long finalTick, balanceError, consumed, victoryTick, victoryConsumed;
            public int victoryKills;
            public double elapsedSeconds, cameraPitch, maximumPendingSeconds, warmMaximumPendingSeconds;
            public int frameSamples, stepSamples;
            public double frameP50, frameP95, frameP99, frameMaximum, stepP50, stepP95, stepP99, stepMaximum;
            public string battleScreenshot, victoryScreenshot, profilePath;
            public MemorySample[] memorySamples;
        }
    }
}
#endif
