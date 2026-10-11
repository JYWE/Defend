using System;
using System.Linq;
using Defend.Simulation;
using Defend.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>第四阶段的编辑器集成验收。运行时配置副本只用于短场景，不保存回项目资产。</summary>
public static class PhaseFourIntegration
{
    private const string DiagnosticName = "第四阶段临时验收配置";
    private const string UiSessionKey = "Defend.PhaseFour.UiObjects";

    public static object CreateContent()
    {
        Defend.Editor.PhaseFourSceneBuilder.CreateOrOpen();
        return new { scene = SceneManager.GetActiveScene().path,
            ui = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include).Length };
    }

    public static object ConfigureDiagnosticRuntime()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("需要播放模式。");
        var session = Object.FindAnyObjectByType<GameSessionRoot>();
        var original = AssetDatabase.LoadAssetAtPath<BattleSettingsAsset>(Defend.Editor.PhaseFourSceneBuilder.BattleSettingsPath);
        if (original == null || !original.CreateSettings().EnemyBehavior.Enabled) throw new InvalidOperationException("第四阶段配置未启用。");
        var copy = Object.Instantiate(original);
        copy.name = DiagnosticName;
        JsonUtility.FromJsonOverwrite("{\"waves\":[{\"enemyCount\":3,\"enemyHealth\":2000,\"spawnIntervalTicks\":40,\"clearReward\":0,\"enemyPattern\":[2],\"entranceIndex\":0,\"briefing\":\"诊断波次：检查重装目标和供电受损\"}],\"spawnPoints\":[{\"x\":18,\"y\":16}]}", copy);
        session.ConfigureBattle(copy, Object.FindAnyObjectByType<BattlePresentation>(), Object.FindAnyObjectByType<BattleControlPanel>());
        session.Restart();
        SessionState.SetString(UiSessionKey, string.Join(",", Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include).Select(item => item.GetEntityId().ToString()).OrderBy(item => item, StringComparer.Ordinal)));
        Application.runInBackground = true;
        return RuntimeState(false);
    }

    public static object StartDiagnosticWave()
    {
        var session = Object.FindAnyObjectByType<GameSessionRoot>();
        session.StartWave();
        return RuntimeState(false);
    }

    public static object CaptureFrame()
    {
        string path = System.IO.Path.GetFullPath("Logs/PhaseFour/editor-complete-frame.png");
        ScreenCapture.CaptureScreenshot(path);
        return new { requested = path };
    }

    public static object ContinueCombat()
    {
        var session = Object.FindAnyObjectByType<GameSessionRoot>();
        if (session.Paused) session.TogglePause();
        return new { resumed = !session.Paused };
    }

    public static object RuntimeState(bool pause)
    {
        if (!Application.isPlaying) throw new InvalidOperationException("需要播放模式。");
        var session = Object.FindAnyObjectByType<GameSessionRoot>();
        if (pause && !session.Paused) session.TogglePause();
        var target = session.Battle.Enemies.FirstOrDefault(item => item.TargetPurpose == EnemyTargetPurpose.DisruptEnergy);
        if (target != null) session.Select(target.TargetBuildingId);
        var ui = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
        string current = string.Join(",", ui.Select(item => item.GetEntityId().ToString()).OrderBy(item => item, StringComparer.Ordinal));
        if (current != SessionState.GetString(UiSessionKey, current)) throw new InvalidOperationException("UI 对象集合改变。");
        session.World.Energy.AssertInvariants();
        Canvas.ForceUpdateCanvases();
        return new { tick = session.World.CurrentTick, paused = session.Paused, state = session.Battle.State.ToString(),
            uiCount = ui.Length, selected = session.SelectedBuildingId, balance = session.World.Energy.GetTotals().BalanceError,
            behavior = session.Battle.BehaviorStatistics,
            enemies = session.Battle.Enemies.Select(item => new { item.Id, item.X, item.Z, item.Health, item.TargetBuildingId,
                purpose = item.TargetPurpose.ToString(), item.TargetX, item.TargetZ, item.LastAttackTick, item.TargetAssignedTick }).ToArray(),
            buildings = session.World.Buildings.Select(item => new { item.Id, kind = item.Kind.ToString(), item.Health,
                item.StoredEnergy, item.ReceivedLastSecond, item.EnergyWaitTicks, item.DestructionCount }).ToArray(),
            markers = Object.FindObjectsByType<EnemyTargetView>().Select(item => new { item.name,
                x = item.transform.position.x, y = item.transform.position.y, z = item.transform.position.z }).ToArray(),
            text = Object.FindObjectsByType<Text>().Select(item => new { item.name, item.text,
                height = item.rectTransform.rect.height, item.preferredHeight,
                overflow = item.verticalOverflow != VerticalWrapMode.Overflow && item.preferredHeight > item.rectTransform.rect.height + 2 }).ToArray() };
    }

    public static object CheckRestartAndCleanup()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("需要播放模式。");
        var session = Object.FindAnyObjectByType<GameSessionRoot>();
        var original = AssetDatabase.LoadAssetAtPath<BattleSettingsAsset>(Defend.Editor.PhaseFourSceneBuilder.BattleSettingsPath);
        session.ConfigureBattle(original, Object.FindAnyObjectByType<BattlePresentation>(), Object.FindAnyObjectByType<BattleControlPanel>());
        for (int index = 0; index < 3; index++)
        {
            var previous = session.World;
            session.Restart();
            if (ReferenceEquals(previous, session.World) || session.World.Credits != 840 || session.World.Buildings.Count != 6 ||
                session.Battle.Enemies.Count != 0 || session.Battle.State != BattleState.Preparing)
                throw new InvalidOperationException("重开没有恢复保存的初始布局。");
        }
        foreach (var asset in Resources.FindObjectsOfTypeAll<BattleSettingsAsset>())
            if (asset.name == DiagnosticName && !AssetDatabase.Contains(asset)) Object.Destroy(asset);
        return RuntimeState(false);
    }
}
