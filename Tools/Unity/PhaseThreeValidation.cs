using System;
using System.Collections.Generic;
using System.Linq;
using Defend.Simulation;
using Defend.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class PhaseThreeValidation
{
    public static object CreateContent()
    {
        Defend.Editor.PhaseThreeSceneBuilder.CreateOrOpen();
        return Content();
    }
    public static object Content()
    {
        var scene = SceneManager.GetActiveScene();
        int missing = 0;
        foreach (var root in scene.GetRootGameObjects()) foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
        if (missing != 0) throw new Exception("场景有丢失脚本。");
        var battle = AssetDatabase.LoadAssetAtPath<BattleSettingsAsset>(Defend.Editor.PhaseThreeContent.Definitions + "/TwinPassBattleSettings.asset").CreateSettings();
        var issues = new List<string>();
        var session = Object.FindAnyObjectByType<GameSessionRoot>();
        foreach (var component in session.GetComponents<MonoBehaviour>())
        {
            var property = new SerializedObject(component).GetIterator();
            while (property.NextVisible(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "controlPanel" &&
                    property.objectReferenceValue == null) issues.Add(component.GetType().Name + "." + property.propertyPath);
        }
        var panel = Object.FindAnyObjectByType<BattleControlPanel>();
        var panelProperty = new SerializedObject(panel).GetIterator();
        while (panelProperty.NextVisible(true))
            if (panelProperty.propertyType == SerializedPropertyType.ObjectReference && panelProperty.objectReferenceValue == null)
                issues.Add("面板." + panelProperty.propertyPath);
        foreach (string id in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Defend/Content/Prefabs/PhaseThree", "Assets/Defend/Content/Prefabs/Enemies", "Assets/Defend/Content/Prefabs/VFX" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(id));
            if (prefab.GetComponentInChildren<RectTransform>(true) != null) issues.Add(prefab.name + " 非 UI 表现包含 UI。");
        }
        if (issues.Count > 0) throw new Exception(string.Join("\n", issues));
        return new { scene = scene.path, missing, waves = battle.Waves.Count, rocks = battle.BlockedCells.Count,
            cameraAngle = Camera.main.transform.eulerAngles.x, ui = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include).Length,
            buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include).Length,
            buildings = Object.FindAnyObjectByType<WorldPresentation>().AuthoredBuildings.Count };
    }
    public static object ScaleBoundary()
    {
        var definitions = Enum.GetValues(typeof(BuildingKind)).Cast<BuildingKind>().Select(kind =>
            AssetDatabase.LoadAssetAtPath<BuildingDefinitionAsset>(Defend.Editor.PhaseThreeContent.Definitions + "/" + kind + ".asset").CreateDefinition()).ToArray();
        var settings = AssetDatabase.LoadAssetAtPath<BattleSettingsAsset>(Defend.Editor.PhaseThreeContent.Definitions + "/TwinPassBattleSettings.asset").CreateSettings();
        var battle = new BattleSettings(new[] { new WaveDefinition(201, 1000000, 1, 0, new[] { EnemyKind.Normal, EnemyKind.Fast, EnemyKind.Heavy }) },
            enemySpeedCellsPerSecond: 0.1, spawnPoints: new[] { new SpawnPoint(0,20) },
            turretDefinitions: new[] { settings.GetTurret(BuildingKind.Turret), settings.GetTurret(BuildingKind.BurstTurret), settings.GetTurret(BuildingKind.AreaTurret) });
        var world = new GameWorld(definitions, new WorldSettings(40,40,20,100000), new EnergySettings(), battle);
        BuildingKind[] kinds = { BuildingKind.Reactor, BuildingKind.Relay, BuildingKind.Turret, BuildingKind.BurstTurret, BuildingKind.AreaTurret };
        for (int index = 0; index < 100; index++) Place(world, index == 99 ? BuildingKind.Core : kinds[index % kinds.Length], 5 + index % 10, 15 + index / 10);
        world.Enqueue(WorldCommand.Place(BuildingKind.Turret,30,30)); world.Step();
        if (world.LastCommandResult.Succeeded) throw new Exception("第 101 栋建筑未被拒绝。");
        world.Enqueue(WorldCommand.StartWave());
        long allocatedBefore = GC.GetTotalMemory(false);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        for (int index = 0; index < 600; index++) { world.Step(); if (index % 100 == 0) world.Energy.AssertInvariants(); }
        timer.Stop();
        world.Energy.AssertInvariants();
        if (world.Battle.Enemies.Count != 200 || world.Battle.RemainingToSpawn != 1) throw new Exception("200 敌人上限失效。");
        return new { mode = "pure-simulation-short-boundary", ticks = 600, simulationSeconds = 30, executionMilliseconds = timer.Elapsed.TotalMilliseconds,
            buildings = world.Buildings.Count, enemies = world.Battle.Enemies.Count, pending = world.Battle.RemainingToSpawn,
            shots = world.Buildings.Sum(item => item.ShotsFired), actualArrival = world.Buildings.Where(item => item.IsConsumer).Sum(item => item.TotalReceived),
            waitingTicks = world.Buildings.Sum(item => item.EnergyWaitTicks), batches = world.Energy.ActiveBatchCount, balance = world.Energy.GetTotals().BalanceError,
            managedBytesBefore = allocatedBefore, managedBytesAfter = GC.GetTotalMemory(false) };
    }
    public static object LegacyCompatibility()
    {
        var legacy = AssetDatabase.LoadAssetAtPath<BattleSettingsAsset>("Assets/Defend/Content/Definitions/Combat/WhiteboxBattleSettings.asset").CreateSettings();
        if (legacy.Waves.Count != 5 || legacy.Waves.Any(wave => wave.EntranceIndex != -1 || wave.EnemyPattern.Count != 1 || wave.EnemyPattern[0] != EnemyKind.Normal))
            throw new Exception("旧战斗配置的默认波次行为被改变。");
        return new { waves = legacy.Waves.Count, entrances = legacy.Waves.Select(wave => wave.EntranceIndex).ToArray(),
            ordinaryTurretDamage = legacy.GetTurret(BuildingKind.Turret).Damage,
            ordinaryEnergyPerShot = legacy.GetTurret(BuildingKind.Turret).EnergyPerShot,
            maximumBuildings = legacy.MaximumBuildings, maximumLivingEnemies = legacy.MaximumLivingEnemies };
    }
    public static object Simulate(int strategy)
    {
        var data = Defend.Editor.PhaseThreeContent.Definitions;
        var definitions = Enum.GetValues(typeof(BuildingKind)).Cast<BuildingKind>().Select(kind =>
            AssetDatabase.LoadAssetAtPath<BuildingDefinitionAsset>(data + "/" + kind + ".asset").CreateDefinition()).ToArray();
        var settings = AssetDatabase.LoadAssetAtPath<EnergyExperimentSettings>(data + "/TwinPassEnergySettings.asset");
        var battle = AssetDatabase.LoadAssetAtPath<BattleSettingsAsset>(data + "/TwinPassBattleSettings.asset");
        var world = new GameWorld(definitions, settings.CreateWorldSettings(), settings.CreateEnergySettings(), battle.CreateSettings());
        foreach (var view in Object.FindAnyObjectByType<WorldPresentation>().AuthoredBuildings)
            Place(world, view.Definition.Kind, Mathf.FloorToInt(view.transform.position.x), Mathf.FloorToInt(view.transform.position.z));
        var waves = new List<object>();
        while (!world.Battle.IsFinished && world.CurrentTick < 36000)
        {
            if (world.Battle.State == BattleState.Preparing || world.Battle.State == BattleState.WaveCleared)
            {
                int next = world.Battle.WaveNumber + 1;
                if (strategy >= 0) PhaseThreeAcceptancePlan.Deploy(world, strategy, next, command => { world.Enqueue(command); world.Step(); if (!world.LastCommandResult.Succeeded) throw new Exception(world.LastCommandResult.Message); });
                for (int index = 0; index < 100; index++) world.Step();
                world.Enqueue(WorldCommand.StartWave()); world.Step();
            }
            world.Step();
            if (world.CurrentTick % 100 == 0) world.Energy.AssertInvariants();
            if (world.Battle.State != BattleState.Combat)
                waves.Add(new { wave = world.Battle.WaveNumber, seconds = world.CurrentTick / 20.0,
                    health = world.FindBuilding(world.Battle.CoreBuildingId).Health, world.Credits, state = world.Battle.State.ToString() });
        }
        world.Energy.AssertInvariants();
        return new { strategy, state = world.Battle.State.ToString(), seconds = world.CurrentTick / 20.0,
            world.Battle.Kills, world.Credits, waves, balance = world.Energy.GetTotals().BalanceError,
            totals = world.Energy.GetTotals(), buildings = world.Buildings.Select(b => new { kind = b.Kind.ToString(), b.X, b.Z, b.Health, b.DamageDealt, b.ShotsFired, b.EnergyWaitTicks, b.DestructionCount }) };
    }
    public static object RuntimeVisual(bool finish)
    {
        var session = Object.FindAnyObjectByType<GameSessionRoot>();
        if (!Application.isPlaying) throw new Exception("需要播放模式。");
        session.Restart(); session.TogglePause();
        var originalUi = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
        var world = session.World;
        while (!world.Battle.IsFinished && world.CurrentTick < 30000)
        {
            if (world.Battle.State == BattleState.Preparing || world.Battle.State == BattleState.WaveCleared)
            {
                PhaseThreeAcceptancePlan.Deploy(world, 1, world.Battle.WaveNumber + 1, command => { world.Enqueue(command); world.Step(); });
                for (int index = 0; index < 100; index++) world.Step();
                world.Enqueue(WorldCommand.StartWave());
            }
            world.Step();
            if (!finish && world.Battle.WaveNumber == 4 && world.Battle.Enemies.Count >= 5) break;
        }
        var selected = world.Buildings.First(item => item.Kind == BuildingKind.AreaTurret && item.Z == 29);
        session.Select(selected.Id);
        Object.FindAnyObjectByType<WorldPresentation>().Refresh(world, selected.Id, world.CurrentTick);
        Object.FindAnyObjectByType<BattlePresentation>().Refresh(world, world.CurrentTick);
        Object.FindAnyObjectByType<BattleControlPanel>().Refresh();
        Application.runInBackground = true;
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        EditorApplication.QueuePlayerLoopUpdate();
        var currentUi = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
        if (originalUi.Length != currentUi.Length || !originalUi.All(item => currentUi.Contains(item))) throw new Exception("UI 对象发生改变。");
        world.Energy.AssertInvariants();
        var text = Object.FindObjectsByType<Text>(FindObjectsInactive.Include).Select(item => new { item.name, item.text }).ToArray();
        return new { state = world.Battle.State.ToString(), tick = world.CurrentTick, uiCount = currentUi.Length, text };
    }
    public static object Layout()
    {
        Canvas.ForceUpdateCanvases();
        return Object.FindObjectsByType<Text>().Select(item => new { item.name, width = item.rectTransform.rect.width,
            height = item.rectTransform.rect.height, preferredHeight = item.preferredHeight, overflow = item.preferredHeight > item.rectTransform.rect.height + 2 }).ToArray();
    }
    public static object RuntimeLifecycle()
    {
        var session = Object.FindAnyObjectByType<GameSessionRoot>();
        if (!Application.isPlaying) throw new Exception("需要播放模式。");
        var ui = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
        for (int repeat = 0; repeat < 3; repeat++)
        {
            var previous = session.World;
            session.Restart(); session.TogglePause();
            if (ReferenceEquals(previous, session.World) || session.World.Buildings.Count != 6 || session.World.Credits != 840)
                throw new Exception("重开没有恢复初始布局与经济。");
            session.Submit(WorldCommand.Place(BuildingKind.AreaTurret,18,18)); session.SingleStep();
            session.Submit(WorldCommand.Place(BuildingKind.BurstTurret,19,18)); session.SingleStep();
            session.Submit(WorldCommand.Damage(session.Battle.CoreBuildingId, 100000)); session.SingleStep();
            session.Submit(WorldCommand.Repair(session.Battle.CoreBuildingId)); session.SingleStep();
            if (session.Battle.State != BattleState.Defeat) throw new Exception("核心死亡没有锁定失败。");
            session.World.Energy.AssertInvariants();
            var current = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
            if (ui.Length != current.Length || !ui.All(item => current.Contains(item))) throw new Exception("重开改变 UI 对象。");
        }
        Object.FindAnyObjectByType<BattleControlPanel>().Refresh();
        return new { repeats = 3, ui = ui.Length, state = session.Battle.State.ToString(),
            resultText = Object.FindObjectsByType<Text>(FindObjectsInactive.Include).First(item => item.name == "ResultDetails").text };
    }
    private static void Place(GameWorld world, BuildingKind kind, int x, int z)
    {
        world.Enqueue(WorldCommand.Place(kind, x, z)); world.Step();
        if (!world.LastCommandResult.Succeeded) throw new Exception(kind + " " + x + "," + z + ": " + world.LastCommandResult.Message + " funds=" + world.Credits);
    }
}
