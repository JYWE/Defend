#if DEBUG && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Defend.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Defend.Unity
{
    /// <summary>
    /// 显式命令行启用的开发包验收入口。只观察已有世界，不创建界面，也不代替正常 Update 推进模拟。
    /// 常规 Release 构建与 Editor 不包含此类型，普通开发包启动也不会创建诊断宿主。
    /// </summary>
    public sealed class WhiteboxPlayerProbe : MonoBehaviour
    {
        private const long RequiredLogicalTicks = 200;
        private const double StartupTimeoutSeconds = 30;
        private const double SimulationTimeoutSeconds = 120;
        private const double ScreenshotTimeoutSeconds = 30;

        private enum ProbeStage { WaitingForWorld, ObservingWorld, WaitingForScreenshot, Finished }

        private readonly ProbeReport report = new ProbeReport();
        private readonly List<ConsumerReport> initialConsumers = new List<ConsumerReport>();
        private GameSessionRoot session;
        private GameWorld observedWorld;
        private ProbeStage stage;
        private double startedAt;
        private double deadline;
        private long lastCheckedTick = -1;
        private string reportPath;
        private string firstRuntimeError;
        private bool originalRunInBackground;
        private bool subscribed;
        private bool baselineRecorded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateWhenRequested()
        {
            // Unity 6.6 使用托管代码变体宏；开发包身份改为运行时判定，避免依赖已弃用的编译宏。
            if (!Debug.isDebugBuild) return;
            string[] arguments = Environment.GetCommandLineArgs();
            if (Array.IndexOf(arguments, "-whitebox-smoke") < 0) return;

            // 这是不带 RectTransform 的诊断宿主，不是运行期生成的界面或游戏内容。
            var host = new GameObject("Whitebox Player Acceptance Probe");
            DontDestroyOnLoad(host);
            host.AddComponent<WhiteboxPlayerProbe>().Initialize(arguments);
        }

        private void Initialize(string[] arguments)
        {
            startedAt = Time.realtimeSinceStartupAsDouble;
            deadline = startedAt + StartupTimeoutSeconds;
            originalRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.logMessageReceived += OnLogMessage;
            subscribed = true;
            report.startedUtc = DateTime.UtcNow.ToString("O");
            report.unityVersion = Application.unityVersion;
            report.requiredLogicalTicks = RequiredLogicalTicks;
            reportPath = Path.Combine(Application.persistentDataPath, "WhiteboxValidation", "whitebox-smoke-report.json");

            try
            {
                int outputIndex = Array.IndexOf(arguments, "-whitebox-output");
                if (outputIndex >= 0)
                {
                    if (outputIndex + 1 >= arguments.Length || arguments[outputIndex + 1].StartsWith("-", StringComparison.Ordinal))
                        throw new ArgumentException("-whitebox-output 后必须提供输出目录。");
                    reportPath = Path.Combine(Path.GetFullPath(arguments[outputIndex + 1]), "whitebox-smoke-report.json");
                }

                string outputDirectory = Path.GetDirectoryName(reportPath);
                Directory.CreateDirectory(outputDirectory);
                // 每次截图使用独立文件名，避免把上次运行留下的图片误认为本次完成。
                report.screenshotPath = Path.Combine(outputDirectory,
                    "whitebox-frame-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".png");
                WriteReport();
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (baselineRecorded || stage == ProbeStage.Finished) return;
            try
            {
                session = FindAnyObjectByType<GameSessionRoot>();
                if (session == null) return;

                // sceneLoaded 位于场景 Start 之前，使 Start 中意外生成的界面也会改变基线。
                report.initialRectTransformCount = CountSceneRectTransforms();
                report.sceneName = scene.name;
                baselineRecorded = true;
            }
            catch (Exception exception)
            {
                firstRuntimeError = exception.ToString();
            }
        }

        private void Update()
        {
            if (stage == ProbeStage.Finished) return;
            try
            {
                if (!string.IsNullOrEmpty(firstRuntimeError))
                    throw new InvalidOperationException("玩家运行期间出现错误：" + firstRuntimeError);
                if (Time.realtimeSinceStartupAsDouble > deadline)
                    throw new TimeoutException("白盒验收等待超时，当前阶段：" + stage);

                if (stage == ProbeStage.WaitingForWorld)
                {
                    if (session == null || session.World == null) return;
                    BeginObservation();
                }
                else if (stage == ProbeStage.ObservingWorld)
                {
                    ObserveWorld();
                }
                else if (stage == ProbeStage.WaitingForScreenshot && TryReadCompletedScreenshot(out long fileLength))
                {
                    report.screenshotBytes = fileLength;
                    ValidateFinalState();
                    Finish(true, string.Empty);
                }
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }

        private void BeginObservation()
        {
            if (!baselineRecorded) throw new InvalidOperationException("没有记录场景启动前的界面数量基线。");
            observedWorld = session.World;
            report.initialTick = observedWorld.CurrentTick;
            foreach (BuildingState building in observedWorld.Buildings)
            {
                if (!building.IsAlive || building.IsSold || !building.IsConsumer) continue;
                initialConsumers.Add(new ConsumerReport
                {
                    buildingId = building.Id,
                    displayName = building.Definition.DisplayName,
                    initialReceived = building.TotalReceived
                });
            }
            if (initialConsumers.Count == 0)
                throw new InvalidOperationException("初始场景没有消费者，不能用空集合通过供能验收。");

            deadline = Time.realtimeSinceStartupAsDouble + SimulationTimeoutSeconds;
            stage = ProbeStage.ObservingWorld;
            ObserveWorld();
        }

        private void ObserveWorld()
        {
            if (session == null || session.World != observedWorld)
                throw new InvalidOperationException("验收期间世界被替换或场景入口被销毁。");
            report.observedFrameCount++;
            if (observedWorld.CurrentTick != lastCheckedTick)
            {
                // 不调用 World.Step；实际推进完全来自场景原有的 GameSessionRoot.Update。
                observedWorld.Energy.AssertInvariants();
                lastCheckedTick = observedWorld.CurrentTick;
            }
            if (observedWorld.CurrentTick - report.initialTick < RequiredLogicalTicks) return;

            ValidateFinalState();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || Screen.width <= 0 || Screen.height <= 0)
                throw new InvalidOperationException("没有可用的游戏画面，截图验收不能在无图形设备模式下通过。");
            report.gameFrameWidth = Screen.width;
            report.gameFrameHeight = Screen.height;
            ScreenCapture.CaptureScreenshot(report.screenshotPath);
            deadline = Time.realtimeSinceStartupAsDouble + ScreenshotTimeoutSeconds;
            stage = ProbeStage.WaitingForScreenshot;
        }

        private void ValidateFinalState()
        {
            observedWorld.Energy.AssertInvariants();
            ReadWorldReport();
            if (report.observedLogicalTicks < RequiredLogicalTicks)
                throw new InvalidOperationException("实际逻辑推进尚未达到验收要求。");
            if (report.balanceError != 0)
                throw new InvalidOperationException("实际能量总账不守恒。");
            if (!report.allInitialConsumersReceived)
                throw new InvalidOperationException("至少一个初始消费者尚未收到有效能量或已被移除。");
            if (!report.rectTransformCountUnchanged)
                throw new InvalidOperationException("从场景启动到验收结束，RectTransform 数量发生变化。");
        }

        private void ReadWorldReport()
        {
            if (observedWorld == null) return;
            EnergyTotals totals = observedWorld.Energy.GetTotals();
            report.finalTick = observedWorld.CurrentTick;
            report.observedLogicalTicks = report.finalTick - report.initialTick;
            report.generated = totals.Generated;
            report.stored = totals.Stored;
            report.inFlight = totals.InFlight;
            report.consumed = totals.Consumed;
            report.transmissionLoss = totals.TransmissionLoss;
            report.destructionLoss = totals.DestructionLoss;
            report.expiredLoss = totals.ExpiredLoss;
            report.balanceError = totals.BalanceError;
            report.activeBatchCount = totals.ActiveBatchCount;
            report.finalRectTransformCount = CountSceneRectTransforms();
            report.rectTransformCountUnchanged = baselineRecorded &&
                report.initialRectTransformCount == report.finalRectTransformCount;
            report.allInitialConsumersReceived = initialConsumers.Count > 0;
            foreach (ConsumerReport consumer in initialConsumers)
            {
                BuildingState building = observedWorld.FindBuilding(consumer.buildingId);
                consumer.isPresentAndAlive = building != null && building.IsAlive && !building.IsSold;
                consumer.finalReceived = building == null ? 0 : building.TotalReceived;
                consumer.receivedDuringObservation = consumer.finalReceived - consumer.initialReceived;
                report.allInitialConsumersReceived &= consumer.isPresentAndAlive && consumer.receivedDuringObservation > 0;
            }
            report.initialConsumers = initialConsumers.ToArray();
        }

        private static int CountSceneRectTransforms()
        {
            return FindObjectsByType<RectTransform>(FindObjectsInactive.Include).Length;
        }

        private bool TryReadCompletedScreenshot(out long fileLength)
        {
            fileLength = 0;
            if (!File.Exists(report.screenshotPath)) return false;
            try
            {
                // 只读取 PNG 标准文件签名与终止块，不计算或比较任何哈希或校验和。
                // 禁止并发写入的共享方式确保截图写入句柄已释放，不能仅凭文件存在就退出。
                using (var stream = new FileStream(report.screenshotPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length < 33) return false;
                    byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
                    foreach (byte expected in signature)
                        if (stream.ReadByte() != expected) return false;
                    stream.Seek(-12, SeekOrigin.End);
                    byte[] finalChunk = { 0, 0, 0, 0, 73, 69, 78, 68 };
                    foreach (byte expected in finalChunk)
                        if (stream.ReadByte() != expected) return false;
                    fileLength = stream.Length;
                    return true;
                }
            }
            catch (IOException)
            {
                // 屏幕截图在帧末异步写入，文件仍被占用时等待下一帧，最终受截图超时约束。
                return false;
            }
        }

        private void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (firstRuntimeError != null || stage == ProbeStage.Finished) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                firstRuntimeError = message + Environment.NewLine + stackTrace;
        }

        private void Finish(bool success, string failure)
        {
            if (stage == ProbeStage.Finished) return;
            stage = ProbeStage.Finished;
            report.success = success;
            report.status = success ? "passed" : "failed";
            report.exitCode = success ? 0 : 1;
            report.failure = failure;
            report.finishedUtc = DateTime.UtcNow.ToString("O");
            report.elapsedSeconds = Time.realtimeSinceStartupAsDouble - startedAt;
            Cleanup();
            try
            {
                ReadWorldReport();
                WriteReport();
            }
            catch (Exception exception)
            {
                report.success = false;
                report.status = "failed";
                report.exitCode = 1;
                report.failure += Environment.NewLine + "写入验收报告失败：" + exception;
                // 显式目录不可写时尽力保留失败证据；无法写入时仍以非零退出码通知调用方。
                reportPath = Path.Combine(Application.persistentDataPath, "WhiteboxValidation", "whitebox-smoke-report.json");
                try { WriteReport(); }
                catch (Exception fallbackException) { Debug.LogError(report.failure + Environment.NewLine + fallbackException); }
            }
            Debug.Log("白盒自动验收结束：" + report.status + "，报告：" + reportPath);
            Application.Quit(report.exitCode);
        }

        private void WriteReport()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        }

        private void Cleanup()
        {
            if (!subscribed) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Application.logMessageReceived -= OnLogMessage;
            Application.runInBackground = originalRunInBackground;
            subscribed = false;
        }

        private void OnApplicationQuit()
        {
            if (stage == ProbeStage.Finished) return;
            report.success = false;
            report.status = "interrupted";
            report.exitCode = 1;
            report.failure = "玩家进程在自动验收完成前收到退出请求。";
            report.finishedUtc = DateTime.UtcNow.ToString("O");
            report.elapsedSeconds = Time.realtimeSinceStartupAsDouble - startedAt;
            try { ReadWorldReport(); WriteReport(); }
            catch (Exception exception) { Debug.LogError(exception); }
            Cleanup();
        }

        private void OnDestroy() => Cleanup();

        [Serializable]
        private sealed class ProbeReport
        {
            public string status = "running";
            public bool success;
            public int exitCode = 1;
            public string failure = string.Empty;
            public string startedUtc;
            public string finishedUtc;
            public string unityVersion;
            public string sceneName;
            public double elapsedSeconds;
            public long requiredLogicalTicks;
            public long initialTick;
            public long finalTick;
            public long observedLogicalTicks;
            public int observedFrameCount;
            public int initialRectTransformCount;
            public int finalRectTransformCount;
            public bool rectTransformCountUnchanged;
            public bool allInitialConsumersReceived;
            public long generated;
            public long stored;
            public long inFlight;
            public long consumed;
            public long transmissionLoss;
            public long destructionLoss;
            public long expiredLoss;
            public long balanceError;
            public int activeBatchCount;
            public ConsumerReport[] initialConsumers;
            public string screenshotPath;
            public long screenshotBytes;
            public int gameFrameWidth;
            public int gameFrameHeight;
        }

        [Serializable]
        private sealed class ConsumerReport
        {
            public long buildingId;
            public string displayName;
            public bool isPresentAndAlive;
            public long initialReceived;
            public long finalReceived;
            public long receivedDuringObservation;
        }
    }
}
#endif
