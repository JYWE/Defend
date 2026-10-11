#if UNITY_EDITOR || DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Defend.Simulation;
using Defend.PhaseFiveEffects;
using UnityEngine;

namespace Defend.Unity
{
    /// <summary>显式诊断入口；临时配置仅存在内存，使用正常 Update、购买、伤害和修复命令。</summary>
    public sealed class PhaseFiveCombatProbe : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public bool complete, passed, pauseStable, sameUi, restartClean;
            public string error, mode = "正常 Unity Update 的临时诊断波次，不代表完整关卡或可见帧率";
            public int shots, deaths, deliveries, buildingHits, repairs, shieldHits, shieldBreaks, peakEffects, peakParticles, dropped, restarts;
            public double seconds;
            public long balance;
        }
        private readonly Report report = new Report();
        private GameSessionRoot session;
        private CombatArtPresentation art;
        private BattleSettingsAsset settings;
        private RectTransform[] originalUi;
        private PhaseFiveEffect[] pausedEffects;
        private float[] pausedAges;
        private Vector3[] pausedPositions;
        private EnemyView[] pausedEnemies;
        private long pausedTick;
        private double began, stageTime;
        private int stage;
        private string output;
        private bool quit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartWhenRequested()
        {
            var arguments = Environment.GetCommandLineArgs();
            if (!Debug.isDebugBuild || Array.IndexOf(arguments, "-phase-five-combat-smoke") < 0) return;
            var probe = new GameObject("工业战斗集成验收").AddComponent<PhaseFiveCombatProbe>();
            int index = Array.IndexOf(arguments, "-phase-five-output");
            probe.output = index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : Path.Combine(Application.persistentDataPath, "PhaseFiveCombat");
            probe.quit = !Application.isEditor;
        }
        public void BeginEditor(string directory) { output = directory; }
        private void Start()
        {
            if (string.IsNullOrEmpty(output)) { enabled = false; return; }
            Directory.CreateDirectory(output);
            began = Time.realtimeSinceStartupAsDouble;
            Application.runInBackground = true;
            Application.logMessageReceived += ReceiveLog;
        }
        private void ReceiveLog(string message, string stack, LogType kind)
        { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) report.error = message + "\n" + stack; }
        private void Check(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
        private void Update()
        {
            if (string.IsNullOrEmpty(output)) return;
            try
            {
                double now = Time.realtimeSinceStartupAsDouble;
                Check(report.error == null, report.error);
                Check(now - began < 75, "诊断未在时限内完成。");
                if (stage == 0)
                {
                    session = FindAnyObjectByType<GameSessionRoot>(); art = FindAnyObjectByType<CombatArtPresentation>();
                    if (session == null || session.World == null) return;
                    originalUi = FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
                    var original = Resources.FindObjectsOfTypeAll<BattleSettingsAsset>().First(x => x.CreateSettings().EnemyBehavior.Enabled);
                    settings = Instantiate(original); settings.name = "第五阶段运行时诊断副本";
                    JsonUtility.FromJsonOverwrite("{\"waves\":[{\"enemyCount\":30,\"enemyHealth\":160,\"spawnIntervalTicks\":10,\"clearReward\":0,\"enemyPattern\":[0,1,2],\"entranceIndex\":0,\"briefing\":\"战斗表现检查\"}],\"spawnPoints\":[{\"x\":18,\"y\":16}]}", settings);
                    session.ConfigureBattle(settings, FindAnyObjectByType<BattlePresentation>(), FindAnyObjectByType<BattleControlPanel>());
                    session.Restart();
                    session.Submit(WorldCommand.Place(BuildingKind.BurstTurret, 24, 20));
                    session.Submit(WorldCommand.Place(BuildingKind.AreaTurret, 24, 22));
                    stage = 1; stageTime = now;
                }
                else if (stage == 1 && now - stageTime > 4)
                {
                    session.Submit(WorldCommand.Damage(session.Battle.CoreBuildingId, 85));
                    session.Submit(WorldCommand.Damage(2, 1000));
                    stage = 2; stageTime = now;
                }
                else if (stage == 2 && now - stageTime > .3)
                {
                    session.Submit(WorldCommand.Repair(2)); session.StartWave();
                    stage = 3; stageTime = now;
                }
                else if (stage == 3 && art.ObservedDeaths >= 6 && art.Pool.ParticleCount > 0)
                {
                    Check(session.Battle.State == BattleState.Combat, "必须在战斗中暂停。");
                    session.TogglePause(); pausedTick = session.World.CurrentTick;
                    pausedEffects = FindObjectsByType<PhaseFiveEffect>().Where(x => x.IsRunning).ToArray();
                    pausedAges = pausedEffects.Select(x => x.Age).ToArray();
                    pausedEnemies = FindObjectsByType<EnemyView>(); pausedPositions = pausedEnemies.Select(x => x.transform.position).ToArray();
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "combat.png"));
                    stage = 4; stageTime = now;
                }
                else if (stage == 4 && now - stageTime > .7)
                {
                    report.pauseStable = session.World.CurrentTick == pausedTick && pausedEffects.Select((x, i) => x.Age == pausedAges[i]).All(x => x)
                        && pausedEnemies.Select((x, i) => x.transform.position == pausedPositions[i]).All(x => x);
                    Check(report.pauseStable, "暂停期间表现或逻辑发生变化。");
                    report.shots = art.ObservedShots; report.deaths = art.ObservedDeaths; report.deliveries = art.ObservedDeliveries;
                    report.buildingHits = art.ObservedBuildingHits; report.repairs = art.ObservedRepairs;
                    report.shieldHits = art.ObservedShieldHits; report.shieldBreaks = art.ObservedShieldBreaks;
                    report.peakEffects = art.Pool.PeakActive; report.peakParticles = art.Pool.PeakParticles; report.dropped = art.Pool.Dropped;
                    report.balance = session.World.Energy.GetTotals().BalanceError;
                    Check(report.shots > 0 && report.deaths > 0 && report.deliveries > 0 && report.buildingHits > 0 && report.repairs > 0 && report.shieldHits > 0 && report.shieldBreaks > 0, "实际结算覆盖不足。");
                    for (int i = 0; i < 3; i++)
                    {
                        session.Restart(); report.restarts++;
                        Check(art.ObservedShots == 0 && art.ObservedDeaths == 0 && FindObjectsByType<EnemyView>().Length == 0, "重开残留敌人或旧事件。");
                        Check(FindObjectsByType<BuildingView>().All(x => x.VisualRoot.localScale == Vector3.one), "重开残留损毁缩放。");
                    }
                    report.restartClean = true;
                    var ui = FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
                    report.sameUi = ui.Length == originalUi.Length && originalUi.All(x => ui.Contains(x));
                    Check(report.sameUi && report.balance == 0, "UI 对象或能源守恒发生变化。");
                    session.World.Energy.AssertInvariants();
                    Finish(true);
                }
            }
            catch (Exception exception) { report.error = exception.ToString(); Finish(false); }
        }
        private void Finish(bool passed)
        {
            report.complete = true; report.passed = passed; report.seconds = Time.realtimeSinceStartupAsDouble - began;
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            enabled = false;
            if (quit) Application.Quit(passed ? 0 : 1);
        }
        private void OnDestroy() { Application.logMessageReceived -= ReceiveLog; if (settings != null) Destroy(settings); }
    }
}
#endif
