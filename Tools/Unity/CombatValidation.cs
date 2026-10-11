using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Defend.Simulation;
using Defend.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>第二阶段集成验证入口；通过现有 CLI 执行，不进入玩家构建。</summary>
public static class CombatValidation
{
    public static object CreateContent()
    {
        Require(!Application.isPlaying, "先退出播放模式。");
        Defend.Editor.CombatWhiteboxSceneBuilder.CreateOrOpen();
        return ValidateContent();
    }

    public static object ValidateContent()
    {
        Require(!Application.isPlaying, "资产检查需要编辑模式。");
        var errors = new List<string>();
        Scene scene = SceneManager.GetActiveScene();
        Require(scene.path.EndsWith("CombatWhitebox.unity") || scene.path.EndsWith("CombatStress.unity"), "当前不是战斗场景。");
        foreach (GameObject root in scene.GetRootGameObjects()) CheckHierarchy(root, errors);
        foreach (string identifier in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Defend/Content/Prefabs" }))
            CheckHierarchy(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(identifier)), errors);
        var rootSession = Object.FindAnyObjectByType<GameSessionRoot>();
        Require(rootSession != null && Object.FindAnyObjectByType<BattleControlPanel>() != null, "缺少会话或持久化战斗面板。");
        Require(Math.Abs(Camera.main.transform.eulerAngles.x - 70) < 0.1, "镜头角度不是70度。");
        Require(Camera.main.orthographic, "本次约定保留正交投影。");
        Require(errors.Count == 0, string.Join("\n", errors));
        return new { scene = scene.path, cameraPitch = Camera.main.transform.eulerAngles.x,
            uiCount = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include).Length,
            buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include).Length,
            prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Defend/Content/Prefabs" }).Length,
            authoredBuildings = Object.FindAnyObjectByType<WorldPresentation>().AuthoredBuildings.Count,
            errors };
    }

    private static void CheckHierarchy(GameObject root, List<string> errors)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                errors.Add(root.name + "/" + transform.name + " 丢失脚本。");
        foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null || component.GetType().Namespace != "Defend.Unity") continue;
            SerializedProperty property = new SerializedObject(component).GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script") continue;
                if (component is GameSessionRoot && (property.name == "controlPanel" || property.name == "battleSettings" ||
                    property.name == "battlePresentation" || property.name == "battleControlPanel")) continue;
                if (property.objectReferenceValue == null) errors.Add(root.name + "/" + component.name + ": " + property.propertyPath);
            }
        }
    }

    public static object RuntimeState()
    {
        GameSessionRoot session = Session();
        session.World.Energy.AssertInvariants();
        return new { tick = session.World.CurrentTick, state = session.Battle.State.ToString(), session.Battle.WaveNumber,
            session.Battle.Kills, session.Battle.RemainingToSpawn, enemies = session.Battle.Enemies.Count,
            buildings = session.World.Buildings.Count, session.World.Credits,
            uiCount = ReadUi().Length, cameraPitch = Camera.main.transform.eulerAngles.x,
            balanceError = session.World.Energy.GetTotals().BalanceError,
            shots = session.World.Buildings.Sum(item => item.ShotsFired), session.PendingSimulationSeconds };
    }

    public static object ExerciseInput()
    {
        GameSessionRoot session = Session();
        session.Restart();
        if (!session.Paused) session.TogglePause();
        RectTransform[] ui = ReadUi();
        int count = session.World.Buildings.Count;
        int credits = session.World.Credits;
        ClickButton("Build1");
        Require(session.GridInput.HasPlacement, "购买没有进入放置模式。");
        ClickButton("Cancel");
        Require(!session.GridInput.HasPlacement && count == session.World.Buildings.Count, "取消或UI防穿透失败。");
        ClickButton("Build1");
        ClickCell(10, 10);
        session.SingleStep();
        BuildingState building = session.World.FindBuildingAt(10, 10);
        Require(building != null && building.Kind == BuildingKind.Relay, "70度镜头下放置失败。");
        Require(session.World.Credits == credits - building.Definition.PurchaseCost, "购买费用不正确。");
        ClickButton("Cancel");
        ClickCell(10, 10);
        Require(session.SelectedBuildingId == building.Id, "70度镜头下选择失败。");
        ClickButton("Move");
        ClickCell(11, 10);
        session.SingleStep();
        Require(building.X == 11, "移动失败。");
        session.Submit(WorldCommand.Damage(building.Id, building.Definition.MaximumHealth));
        session.SingleStep();
        ClickButton("Repair");
        session.SingleStep();
        Require(building.IsAlive && building.LifeGeneration == 2, "维修失败。");
        ClickButton("Sell");
        session.SingleStep();
        Require(building.IsSold, "出售失败。");
        BuildingState turret = session.World.Buildings.First(item => item.Kind == BuildingKind.Turret);
        ClickCell(turret.X, turret.Z);
        ClickButton("Priority2");
        session.SingleStep();
        Require(turret.PriorityWeight == 4, "调整优先级失败。");
        ClickButton("Pause");
        ClickButton("StartWave");
        session.SingleStep();
        Require(session.Battle.State == BattleState.Combat, "开始波次失败。");
        GameWorld oldWorld = session.World;
        ClickButton("Restart");
        Require(session.World != oldWorld && session.Battle.State == BattleState.Preparing, "重开失败。");
        Require(session.World.Buildings.Count == count && session.World.Credits == credits, "重开没有恢复初始布局和费用。");
        Require(SameUi(ui), "UI对象被新增、删除或替换。");
        return new { success = true, uiCount = ui.Length,
            tested = "真实Input System鼠标、UI防穿透、70度地面放置选择移动、维修出售优先级、暂停、开始波次和重开", state = RuntimeState() };
    }

    public static object InspectInput()
    {
        Canvas.ForceUpdateCanvases();
        Button button = Object.FindObjectsByType<Button>().Single(item => item.name == "Build1");
        RectTransform rect = (RectTransform)button.transform;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var pointer = new PointerEventData(EventSystem.current) { position = point };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        var module = EventSystem.current.GetComponent<InputSystemUIInputModule>();
        return new { point = point.ToString(), rect = rect.rect.ToString(), screen = new[] { Screen.width, Screen.height },
            EventSystem.current.isFocused, module.enabled, pointEnabled = module.point.action.enabled,
            clickEnabled = module.leftClick.action.enabled, hits = hits.Select(hit => hit.gameObject.name).ToArray() };
    }

    public static object PlayFiveWaves()
    {
        GameSessionRoot session = Session();
        session.Restart();
        session.TogglePause();
        GameWorld world = session.World;
        for (int index = 0; index < 200; index++) world.Step();
        RectTransform[] ui = ReadUi();
        int ticks = 0;
        bool expanded = false;
        while (!world.Battle.IsFinished && ticks++ < 20000)
        {
            if (world.Battle.State == BattleState.WaveCleared && world.Battle.WaveNumber == 2 && !expanded)
            {
                foreach (Vector2Int point in new[] { new Vector2Int(20,14), new Vector2Int(20,26),
                    new Vector2Int(14,17), new Vector2Int(26,17), new Vector2Int(14,23), new Vector2Int(26,23) })
                    world.Enqueue(WorldCommand.Place(BuildingKind.Turret, point.x, point.y));
                for (int index = 0; index < 100; index++) world.Step();
                expanded = true;
            }
            if (world.Battle.State == BattleState.Preparing || world.Battle.State == BattleState.WaveCleared)
                world.Enqueue(WorldCommand.StartWave());
            world.Step();
            if (ticks % 100 == 0) world.Energy.AssertInvariants();
        }
        Require(world.Battle.State == BattleState.Victory, "第二波后扩建六座炮塔的五波策略未胜利，状态：" + world.Battle.State);
        Require(SameUi(ui), "五波推进改变UI结构。");
        return new { state = world.Battle.State.ToString(), ticks, waves = world.Battle.WaveNumber,
            kills = world.Battle.Kills, credits = world.Credits, expanded,
            shots = world.Buildings.Sum(item => item.ShotsFired), balanceError = world.Energy.GetTotals().BalanceError };
    }

    public static object RepeatedRestart()
    {
        GameSessionRoot session = Session();
        RectTransform[] ui = ReadUi();
        var presentation = Object.FindAnyObjectByType<WorldPresentation>();
        var battlePresentation = Object.FindAnyObjectByType<BattlePresentation>();
        var objectCounts = new List<int>();
        for (int round = 0; round < 6; round++)
        {
            session.Restart();
            session.Submit(WorldCommand.Place(BuildingKind.Relay, 10, 10));
            session.SingleStep();
            session.StartWave();
            for (int tick = 0; tick < 600; tick++)
            {
                session.SingleStep();
                presentation.Refresh(session.World, 0, session.World.CurrentTick);
                battlePresentation.Refresh(session.World, session.World.CurrentTick);
            }
            session.World.Energy.AssertInvariants();
            Require(SameUi(ui), "重复开始替换了 UI 对象。");
            objectCounts.Add(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Length);
        }
        Require(objectCounts.Skip(1).All(count => count == objectCounts[0]), "重复相同流程后表现对象数量持续变化。");
        session.Restart();
        Require(session.World.Buildings.Count == 13 && SameUi(ui), "最终没有恢复初始布局。");
        return new { repeatedWorlds = 6, ticksPerWorld = 600, objectCounts, uiCount = ui.Length,
            initialBuildingsRestored = session.World.Buildings.Count, balanceError = session.World.Energy.GetTotals().BalanceError };
    }

    public static object CameraInput()
    {
        Session();
        Camera camera = Camera.main;
        var controller = Object.FindAnyObjectByType<WorldCameraViewport>();
        Vector3 originalPosition = camera.transform.position;
        float originalSize = camera.orthographicSize;
        var serialized = new SerializedObject(controller);
        float originalZoom = serialized.FindProperty("zoomFactor").floatValue;
        InputSettings.BackgroundBehavior originalBehavior = InputSystem.settings.backgroundBehavior;
        try
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Canvas.ForceUpdateCanvases();
            Vector2 point = camera.pixelRect.center;
            Vector3 groundBeforeZoom = Ground(camera, point);
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = point, scroll = new Vector2(0, 120) });
            InputSystem.Update(); controller.SendMessage("LateUpdate");
            Require(camera.orthographicSize < originalSize, "滚轮没有放大地图。");
            Require(Vector3.Distance(groundBeforeZoom, Ground(camera, point)) < 0.001f, "缩放没有保持指向地面的位置。");
            float zoomedSize = camera.orthographicSize;
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = point }.WithButton(MouseButton.Middle, true));
            InputSystem.Update(); controller.SendMessage("LateUpdate");
            Vector3 groundBeforeDrag = Ground(camera, point);
            Vector2 moved = point + new Vector2(60, 30);
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = moved }.WithButton(MouseButton.Middle, true));
            InputSystem.Update(); controller.SendMessage("LateUpdate");
            Require(Vector3.Distance(groundBeforeDrag, Ground(camera, moved)) < 0.001f, "中键拖动未保持地面抓取位置。");
            Require(Vector3.Distance(originalPosition, camera.transform.position) > 0.01f, "摄像机没有移动。");
            Require(Math.Abs(camera.transform.eulerAngles.x - 70) < 0.1, "缩放拖动改变了70度俯角。");
            return new { originalSize, zoomedSize, pitch = camera.transform.eulerAngles.x,
                zoomKeepsGroundPoint = true, dragKeepsGroundPoint = true };
        }
        finally
        {
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = camera.pixelRect.center });
            InputSystem.Update();
            InputSystem.settings.backgroundBehavior = originalBehavior;
            serialized.Update();
            serialized.FindProperty("zoomFactor").floatValue = originalZoom;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            camera.transform.position = originalPosition;
            camera.orthographicSize = originalSize;
        }
    }

    private static Vector3 Ground(Camera camera, Vector2 point)
    {
        Ray ray = camera.ScreenPointToRay(point);
        Require(new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance), "摄像机射线未命中地面。");
        return ray.GetPoint(distance);
    }

    public static object CreateStressContent()
    {
        Require(!Application.isPlaying, "压力场景制作需要编辑模式。");
        const string scenePath = "Assets/Defend/Content/Scenes/Dev/CombatStress.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
        {
            EditorSceneManager.OpenScene(scenePath);
            return ValidateContent();
        }
        EditorSceneManager.OpenScene(Defend.Editor.CombatWhiteboxSceneBuilder.CombatScenePath);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);
        var session = Object.FindAnyObjectByType<GameSessionRoot>();
        var sessionData = new SerializedObject(session);
        var worldPresentation = Object.FindAnyObjectByType<WorldPresentation>();
        var battleSettings = Object.Instantiate((BattleSettingsAsset)sessionData.FindProperty("battleSettings").objectReferenceValue);
        var battleData = new SerializedObject(battleSettings);
        battleData.FindProperty("enemySpeedCellsPerSecond").floatValue = 0.5f;
        battleData.FindProperty("enemyAttackDamage").intValue = 1;
        battleData.FindProperty("turretRange").floatValue = 30;
        battleData.FindProperty("turretEnergyPerShot").longValue = 6000;
        SerializedProperty waves = battleData.FindProperty("waves");
        waves.arraySize = 1;
        SerializedProperty wave = waves.GetArrayElementAtIndex(0);
        wave.FindPropertyRelative("enemyCount").intValue = 200;
        wave.FindPropertyRelative("enemyHealth").intValue = 1000000000;
        wave.FindPropertyRelative("spawnIntervalTicks").intValue = 1;
        battleData.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(battleSettings, "Assets/Defend/Content/Definitions/Combat/StressBattleSettings.asset");
        sessionData.FindProperty("battleSettings").objectReferenceValue = battleSettings;
        var energySettings = Object.Instantiate((EnergyExperimentSettings)sessionData.FindProperty("experimentSettings").objectReferenceValue);
        var energyData = new SerializedObject(energySettings);
        energyData.FindProperty("startingCredits").intValue = 1000000;
        energyData.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(energySettings, "Assets/Defend/Content/Definitions/Combat/StressEnergySettings.asset");
        sessionData.FindProperty("experimentSettings").objectReferenceValue = energySettings;
        SerializedProperty definitions = sessionData.FindProperty("buildingDefinitions");
        for (int index = 0; index < definitions.arraySize; index++)
        {
            var definition = Object.Instantiate((BuildingDefinitionAsset)definitions.GetArrayElementAtIndex(index).objectReferenceValue);
            var definitionData = new SerializedObject(definition);
            definitionData.FindProperty("maximumHealth").intValue = 1000000000;
            definitionData.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, "Assets/Defend/Content/Definitions/Combat/Stress" + definition.Kind + ".asset");
            definitions.GetArrayElementAtIndex(index).objectReferenceValue = definition;
        }
        sessionData.ApplyModifiedPropertiesWithoutUndo();
        Transform parent = worldPresentation.AuthoredBuildings[0].transform.parent;
        foreach (BuildingView view in worldPresentation.AuthoredBuildings) Object.DestroyImmediate(view.gameObject);
        var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Defend/Content/Prefabs/Buildings" })
            .Select(id => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(id)))
            .Select(item => item.GetComponent<BuildingView>()).ToDictionary(view => view.Definition.Kind);
        var views = new List<BuildingView>();
        for (int index = 0; index < 100; index++)
        {
            BuildingKind kind = index == 99 ? BuildingKind.Core : index % 4 == 0 ? BuildingKind.Reactor :
                index % 4 == 3 ? BuildingKind.Turret : BuildingKind.Relay;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[kind].gameObject, parent);
            instance.transform.position = new Vector3(15 + index % 10, 0, 15 + index / 10);
            views.Add(instance.GetComponent<BuildingView>());
        }
        var presentationData = new SerializedObject(worldPresentation);
        SerializedProperty authored = presentationData.FindProperty("authoredBuildings");
        authored.arraySize = views.Count;
        for (int index = 0; index < views.Count; index++) authored.GetArrayElementAtIndex(index).objectReferenceValue = views[index];
        presentationData.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        return ValidateContent();
    }

    private static void ClickButton(string name)
    {
        Canvas.ForceUpdateCanvases();
        Button button = Object.FindObjectsByType<Button>().Single(item => item.name == name);
        Require(button.interactable, "按钮不可用：" + name);
        RectTransform rectangle = (RectTransform)button.transform;
        Click(RectTransformUtility.WorldToScreenPoint(null, rectangle.TransformPoint(rectangle.rect.center)));
    }
    private static void ClickCell(int x, int z)
    {
        Vector3 point = Camera.main.WorldToScreenPoint(new Vector3(x + 0.5f, 0, z + 0.5f));
        Require(Camera.main.pixelRect.Contains(point), "测试格在视口外。");
        Click(point);
    }
    private static void Click(Vector2 point)
    {
        InputSettings.BackgroundBehavior original = InputSystem.settings.backgroundBehavior;
        try
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystemUIInputModule module = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            QueuePointer(point, true); InputSystem.Update(); Session().GridInput.ProcessInput(); module.Process();
            QueuePointer(point, false); InputSystem.Update(); module.Process(); InputSystem.Update();
        }
        finally { InputSystem.settings.backgroundBehavior = original; }
    }
    private static void QueuePointer(Vector2 point, bool pressed)
    {
        using (StateEvent.From(Mouse.current, out InputEventPtr input))
        {
            Mouse.current.position.WriteValueIntoEvent(point, input);
            Mouse.current.leftButton.WriteValueIntoEvent(pressed ? 1f : 0f, input);
            InputSystem.QueueEvent(input);
        }
    }
    private static bool SameUi(RectTransform[] previous) { RectTransform[] current = ReadUi(); return previous.Length == current.Length && previous.All(item => item != null && current.Contains(item)); }
        private static RectTransform[] ReadUi() => Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include)
        ;
    private static GameSessionRoot Session()
    {
        GameSessionRoot session = Object.FindAnyObjectByType<GameSessionRoot>();
        Require(Application.isPlaying && session?.Battle != null, "请先启动战斗场景。");
        return session;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
