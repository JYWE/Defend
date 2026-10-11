using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Defend.Simulation;
using Defend.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>
/// 通过 Unity CLI 的 run_script 调用的集成验证入口。
/// 文件保存在 Assets 之外，不进入玩家构建，也不会因修改验证脚本触发工程重编译。
/// 所有校验直接检查字段和数量，不使用哈希值。
/// </summary>
public static class WhiteboxValidation
{
    public static object DiscardIncompleteGeneration()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path.Length != 0 || scene.name != "EnergyWhitebox")
            throw new InvalidOperationException("仅允许清理本生成器失败后留下的未保存 EnergyWhitebox 场景。");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        return new { discardedIncompleteScene = true };
    }

    public static object BuildContent()
    {
        if (Application.isPlaying) throw new InvalidOperationException("请先退出播放模式再制作资产。");
        Defend.Editor.WhiteboxSceneBuilder.Build();
        return ValidateContent();
    }

    public static object ApplyPresentationFixes()
    {
        Defend.Editor.WhiteboxPresentationMigration.Apply();
        Defend.Editor.WhiteboxPresentationMigration.RemoveUnusedLegacyFont();
        return ValidateContent();
    }

    public static object InspectPrefabReferences()
    {
        return AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Defend/Content/Prefabs/Buildings" })
            .Select(identifier =>
            {
                string path = AssetDatabase.GUIDToAssetPath(identifier);
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                BuildingView view = asset == null ? null : asset.GetComponent<BuildingView>();
                return new { path, rootPresent = asset != null, componentPresent = view != null,
                    definitionPresent = view != null && view.Definition != null,
                    definitionPath = view != null && view.Definition != null ? AssetDatabase.GetAssetPath(view.Definition) : "" };
            }).ToArray();
    }

    public static object ValidateContent()
    {
        if (Application.isPlaying) throw new InvalidOperationException("资产验证必须在编辑模式执行。");
        List<string> errors = new List<string>();
        int prefabCount = 0;
        int definitionCount = 0;
        foreach (string identifier in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Defend/Content" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(identifier);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            CheckHierarchy(prefab, path, errors);
            prefabCount++;
        }
        foreach (string identifier in AssetDatabase.FindAssets("t:BuildingDefinitionAsset", new[] { "Assets/Defend/Content" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(identifier);
            try { AssetDatabase.LoadAssetAtPath<BuildingDefinitionAsset>(path).CreateDefinition(); }
            catch (Exception exception) { errors.Add(path + ": " + exception.Message); }
            definitionCount++;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.path.EndsWith("EnergyWhitebox.unity", StringComparison.Ordinal))
            errors.Add("当前场景不是 EnergyWhitebox，请打开开发场景后验证。");
        foreach (GameObject root in scene.GetRootGameObjects()) CheckHierarchy(root, scene.path, errors);
        GameSessionRoot session = UnityEngine.Object.FindAnyObjectByType<GameSessionRoot>();
        EnergyControlPanel panel = UnityEngine.Object.FindAnyObjectByType<EnergyControlPanel>();
        if (session == null) errors.Add("场景缺少 GameSessionRoot。");
        if (panel == null) errors.Add("场景缺少已制作的 EnergyControlPanel。");
        int uiCount = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include).Length;
        int buttonCount = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include).Length;
        if (definitionCount != 4) errors.Add("应有四个建筑定义，实际为 " + definitionCount);
        if (buttonCount < 16) errors.Add("场景控制按钮不完整，实际为 " + buttonCount);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        return new { scene = scene.path, prefabCount, definitionCount, uiCount, buttonCount, errors = errors.ToArray() };
    }

    private static void CheckHierarchy(GameObject root, string asset, List<string> errors)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                errors.Add(asset + "/" + transform.name + " 存在丢失脚本。");
        }
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null || !behaviour.GetType().Namespace.StartsWith("Defend", StringComparison.Ordinal)) continue;
            SerializedObject serialized = new SerializedObject(behaviour);
            SerializedProperty property = serialized.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script") continue;
                if (behaviour is GameSessionRoot && (property.name == "battleSettings" ||
                    property.name == "battlePresentation" || property.name == "battleControlPanel")) continue;
                // 当前白盒组件的序列化对象引用均为必需项；空值必须回到 Inspector 修复。
                if (property.objectReferenceValue == null)
                    errors.Add(asset + "/" + behaviour.name + ": " + property.propertyPath + " 未绑定。");
            }
        }
    }

    public static object RuntimeState()
    {
        GameSessionRoot session = RequireSession();
        session.World.Energy.AssertInvariants();
        EnergyTotals totals = session.World.Energy.GetTotals();
        return new
        {
            tick = session.World.CurrentTick,
            paused = session.Paused,
            speed = session.Speed,
            credits = session.World.Credits,
            selected = session.SelectedBuildingId,
            mode = session.GridInput.ModeDescription,
            feedback = session.Feedback,
            uiCount = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include).Length,
            batchCount = session.World.Energy.ActiveBatchCount,
            connections = session.World.Energy.Connections.Count,
            generated = totals.Generated,
            consumed = totals.Consumed,
            balanceError = totals.BalanceError,
            buildings = session.World.Buildings.Select(building => new
            {
                building.Id, building.Kind, building.X, building.Z, building.Health,
                building.IsAlive, building.IsSold, building.PriorityWeight,
                building.StoredEnergy, building.CommittedIncoming, building.TotalReceived,
                building.EnergyStatus
            }).ToArray()
        };
    }

    public static object CaptureGameFrame(string output)
    {
        RequireSession();
        Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
        gameView.Focus();
        gameView.Repaint();
        string path = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        // Pipeline 的 camera.Render 截图不包含 Overlay Canvas，这里捕获实际游戏帧。
        ScreenCapture.CaptureScreenshot(path);
        return new { requestedPath = path, width = Screen.width, height = Screen.height };
    }

    /// <summary>无人值守截图期间允许后台帧刷新；只改变当前播放器状态，调用方在结束前恢复。</summary>
    public static object SetBackgroundPlayback(bool enabled)
    {
        RequireSession();
        bool previous = Application.runInBackground;
        Application.runInBackground = enabled;
        EditorApplication.QueuePlayerLoopUpdate();
        return new { previous, current = Application.runInBackground };
    }

    /// <summary>检查实际 Canvas 网格，避免只验证 fillAmount 而漏掉无 Sprite 时显示始终满格的问题。</summary>
    public static object ValidateEnergyBar()
    {
        GameSessionRoot session = RequireSession();
        Image fill = UnityEngine.Object.FindObjectsByType<Image>(FindObjectsInactive.Include)
            .Single(candidate => candidate.name == "EnergyFill");
        bool previousEnabled = session.enabled;
        float previousFill = fill.fillAmount;
        List<Defend.Editor.WhiteboxEnergyBarAuthoring.EnergyBarGeometry> samples =
            new List<Defend.Editor.WhiteboxEnergyBarAuthoring.EnergyBarGeometry>();
        try
        {
            session.enabled = false;
            foreach (float amount in new[] { 0f, 0.5f, 1f })
            {
                fill.fillAmount = amount;
                Canvas.ForceUpdateCanvases();
                var geometry = Defend.Editor.WhiteboxEnergyBarAuthoring.ReadVisibleGeometry(fill);
                Require(geometry.HasSprite, "储能条未配置 Sprite。");
                samples.Add(geometry);
            }
            float fullWidth = samples[2].VisibleWidth;
            // 内置 Sprite 可能带透明边距，因此按真实满条宽度比较比例，不把 Rect 宽度当作不透明像素宽度。
            Require(fullWidth > 0 && samples[0].VisibleWidth < 0.1f &&
                Mathf.Abs(samples[1].VisibleWidth - fullWidth * 0.5f) < 0.1f,
                "储能条空、半满、全满的可见宽度不符合比例：" +
                samples[0].VisibleWidth + ", " + samples[1].VisibleWidth + ", " + fullWidth);
            return new { success = true, samples };
        }
        finally
        {
            fill.fillAmount = previousFill;
            Canvas.ForceUpdateCanvases();
            session.enabled = previousEnabled;
        }
    }

    public static object RuntimeControls()
    {
        RequireSession();
        return UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include)
            .Select(button =>
            {
                RectTransform rectangle = (RectTransform)button.transform;
                Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rectangle.TransformPoint(rectangle.rect.center));
                return new { button.name, button.interactable, x = position.x, y = position.y,
                    label = button.GetComponentInChildren<Text>()?.text };
            }).ToArray();
    }

    public static object WorldScreenPosition(int x, int z)
    {
        RequireSession();
        Vector3 point = Camera.main.WorldToScreenPoint(new Vector3(x + 0.5f, 0, z + 0.5f));
        return new { x = point.x, y = point.y, Screen.width, Screen.height };
    }

    public static object PointerCheck(float x, float y)
    {
        RequireSession();
        EventSystem events = EventSystem.current;
        InputSystemUIInputModule module = events.GetComponent<InputSystemUIInputModule>();
        List<RaycastResult> hits = new List<RaycastResult>();
        events.RaycastAll(new PointerEventData(events) { position = new Vector2(x, y) }, hits);
        return new { focused = Application.isFocused, moduleEnabled = module.isActiveAndEnabled,
            pointEnabled = module.point.action.enabled, clickEnabled = module.leftClick.action.enabled,
            mousePosition = Mouse.current.position.ReadValue().ToString(),
            hits = hits.Select(hit => new { hit.gameObject.name, module = hit.module.GetType().Name }).ToArray() };
    }

    public static object ExercisePointerFlow()
    {
        GameSessionRoot session = RequireSession();
        if (!session.Paused) session.TogglePause();
        int uiCount = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include).Length;
        int initialCount = session.World.Buildings.Count;
        int originalCredits = session.World.Credits;
        ClickButton("Build1");
        Require(session.GridInput.HasPlacement, "鼠标事件没有进入中继放置模式。");
        ClickButton("Speed");
        Require(session.World.Buildings.Count == initialCount, "点击界面穿透到了建筑放置。");
        ClickCell(10, 10);
        ClickButton("SingleStep");
        BuildingState building = session.World.FindBuildingAt(10, 10);
        Require(building != null && building.Kind == BuildingKind.Relay, "网格输入未完成购买放置。");
        Require(session.World.Credits == originalCredits - building.Definition.PurchaseCost, "购买扣款不正确。");
        ClickButton("CancelPlacement");
        ClickCell(10, 10);
        Require(session.SelectedBuildingId == building.Id, "鼠标无法选择新建筑。");
        ClickButton("MoveBuilding");
        ClickCell(11, 10);
        ClickButton("SingleStep");
        Require(building.X == 11 && building.Z == 10, "移动输入没有修改所选建筑位置。");
        ClickButton("DamageBuilding");
        ClickButton("SingleStep");
        Require(!building.IsAlive, "损坏按钮没有作用于所选建筑。");
        ClickCell(12, 10);
        ClickCell(11, 10);
        Require(session.SelectedBuildingId == building.Id, "损坏建筑不能重新选择。");
        ClickButton("RepairBuilding");
        ClickButton("SingleStep");
        Require(building.IsAlive && building.LifeGeneration == 2, "修复按钮未恢复原建筑身份。");
        ClickButton("SellBuilding");
        ClickButton("SingleStep");
        Require(building.IsSold, "出售按钮未执行。");
        BuildingState consumer = session.World.Buildings.First(item => item.IsAlive && !item.IsSold && item.IsConsumer);
        ClickCell(consumer.X, consumer.Z);
        ClickButton("Priority2");
        ClickButton("SingleStep");
        Require(consumer.PriorityWeight == 4, "终端高优先级按钮未生效。");
        Require(uiCount == UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include).Length,
            "鼠标操作期间新增了 UI 结构。");
        session.World.Energy.AssertInvariants();
        return new { success = true, uiCount, tested = "UI防穿透、购买、放置、取消、选择、移动、损坏、残骸重选、修复、出售、优先级、单步、倍速",
            focusHandling = "为自动化临时忽略窗口焦点；输入、UI射线及回调走实际 Input System，结束后恢复设置。",
            tick = session.World.CurrentTick, balanceError = session.World.Energy.GetTotals().BalanceError };
    }

    private static void ClickButton(string name)
    {
        Button button = UnityEngine.Object.FindObjectsByType<Button>().Single(item => item.name == name);
        Require(button.interactable, "测试按钮不可用：" + name);
        RectTransform rectangle = (RectTransform)button.transform;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rectangle.TransformPoint(rectangle.rect.center));
        ClickPointer(point);
    }

    private static void ClickCell(int x, int z)
    {
        Vector3 point = Camera.main.WorldToScreenPoint(new Vector3(x + 0.5f, 0, z + 0.5f));
        Require(Camera.main.pixelRect.Contains(point), "目标格子在当前地图视口之外。");
        ClickPointer(point);
    }

    private static void ClickPointer(Vector2 position)
    {
        GameSessionRoot session = RequireSession();
        InputSystemUIInputModule module = EventSystem.current.GetComponent<InputSystemUIInputModule>();
        InputSettings.BackgroundBehavior previous = InputSystem.settings.backgroundBehavior;
        try
        {
            // CLI 不抢占用户的前台窗口。此临时设置仅用于有焦点情况下的输入回归。
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            QueuePointerState(position, true);
            InputSystem.Update();
            session.GridInput.ProcessInput();
            module.Process();
            QueuePointerState(position, false);
            InputSystem.Update();
            module.Process();
            InputSystem.Update();
        }
        finally
        {
            InputSystem.settings.backgroundBehavior = previous;
        }
    }

    private static void QueuePointerState(Vector2 position, bool pressed)
    {
        // 鼠标按钮是位域，不能把 float 大小的差量状态直接写入；复用 Input System 状态事件 API。
        using (StateEvent.From(Mouse.current, out InputEventPtr inputEvent))
        {
            Mouse.current.position.WriteValueIntoEvent(position, inputEvent);
            Mouse.current.leftButton.WriteValueIntoEvent(pressed ? 1f : 0f, inputEvent);
            InputSystem.QueueEvent(inputEvent);
        }
    }

    public static object RuntimeSmoke()
    {
        GameSessionRoot session = RequireSession();
        if (!session.Paused) session.TogglePause();
        int initialUiCount = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include).Length;
        BuildingState relay = session.World.Buildings.First(building => building.Kind == BuildingKind.Relay && building.IsAlive && !building.IsSold);
        BuildingState consumer = session.World.Buildings.First(building => building.IsConsumer && building.IsAlive && !building.IsSold);
        session.Select(consumer.Id);
        session.SetSelectedPriority(4);
        session.SingleStep();
        Require(consumer.PriorityWeight == 4, "优先级没有经输入适配层应用。");
        session.Select(relay.Id);
        int originalLife = relay.LifeGeneration;
        session.DamageSelected();
        session.SingleStep();
        Require(!relay.IsAlive, "中继损坏操作失败。");
        session.RepairSelected();
        session.SingleStep();
        Require(relay.IsAlive && relay.LifeGeneration == originalLife + 1, "中继修复没有更新生命代次。");
        for (int index = 0; index < 400; index++) session.SingleStep();
        session.World.Energy.AssertInvariants();
        Require(consumer.TotalReceived > 0, "连续运行后终端仍未获得有效能量。");
        Require(initialUiCount == UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include).Length,
            "运行操作期间 UI 结构数量发生变化。");
        return RuntimeState();
    }

    /// <summary>合成密集场景，仅测纯模拟；不把编辑器耗时冒充完整玩家帧率。</summary>
    public static object Measure(int count, int ticks)
    {
        return RunMeasurement(count, 0, ticks);
    }

    /// <summary>先运行指定预热步数，再采集后续供能期间的耗时及实际吞吐；预热本身不保证已达稳定状态。</summary>
    public static object MeasureSteady(int count, int warmupTicks, int ticks)
    {
        return RunMeasurement(count, warmupTicks, ticks);
    }

    /// <summary>极端压力场景按墙钟时间中止并报告实际完成步数，避免长期阻塞共享编辑器。</summary>
    public static object MeasureBounded(int count, int warmupTicks, int ticks, int maximumWallSeconds)
    {
        if (maximumWallSeconds < 1 || maximumWallSeconds > 60) throw new ArgumentOutOfRangeException();
        return RunMeasurement(count, warmupTicks, ticks, maximumWallSeconds);
    }

    private static object RunMeasurement(int count, int warmupTicks, int ticks, int maximumWallSeconds = 0)
    {
        if (count < 4 || count > 1600 || ticks < 1 || ticks > 2000 || warmupTicks < 0 || warmupTicks > 2000)
            throw new ArgumentOutOfRangeException();
        BuildingDefinition[] definitions =
        {
            Definition(BuildingKind.Reactor, 2000, 0, 8000),
            Definition(BuildingKind.Relay, 0, 0, 8000),
            Definition(BuildingKind.Turret, 0, 1000, 0),
            Definition(BuildingKind.Core, 0, 1000, 0)
        };
        GameWorld world = new GameWorld(definitions, new WorldSettings(40, 40, 20, 0));
        int side = (int)Math.Ceiling(Math.Sqrt(count));
        for (int index = 0; index < count; index++)
        {
            BuildingKind kind = index % 4 == 0 ? BuildingKind.Reactor : index % 4 == 3 ? BuildingKind.Turret : BuildingKind.Relay;
            world.Enqueue(WorldCommand.Place(kind, index % side, index / side));
        }
        Stopwatch totalWatch = Stopwatch.StartNew();
        Stopwatch watch = Stopwatch.StartNew();
        world.Step();
        watch.Stop();
        double setupMilliseconds = watch.Elapsed.TotalMilliseconds;
        List<double> samples = new List<double>(ticks);
        int peakBatchCount = world.Energy.ActiveBatchCount;
        int maximumPendingSearches = world.Energy.PlanningStatistics.PendingSearchCount;
        int maximumPathWork = world.Energy.PlanningStatistics.WorkUsedThisTick;
        double warmupMaximumMilliseconds = 0;
        int completedWarmupTicks = 0;
        for (int index = 0; index < warmupTicks; index++)
        {
            if (maximumWallSeconds > 0 && totalWatch.Elapsed.TotalSeconds >= maximumWallSeconds) break;
            watch.Restart();
            world.Step();
            watch.Stop();
            completedWarmupTicks++;
            warmupMaximumMilliseconds = Math.Max(warmupMaximumMilliseconds, watch.Elapsed.TotalMilliseconds);
            peakBatchCount = Math.Max(peakBatchCount, world.Energy.ActiveBatchCount);
            maximumPendingSearches = Math.Max(maximumPendingSearches, world.Energy.PlanningStatistics.PendingSearchCount);
            maximumPathWork = Math.Max(maximumPathWork, world.Energy.PlanningStatistics.WorkUsedThisTick);
        }
        long consumedBeforeMeasurement = world.Energy.GetTotals().Consumed;
        long expiredBeforeMeasurement = world.Energy.GetTotals().ExpiredLoss;
        long receivedBeforeMeasurement = world.Buildings.Where(building => building.IsConsumer).Sum(building => building.TotalReceived);
        // 部分 Unity Mono 环境的线程分配计数恒为零，先探测，避免报告虚假的零分配。
        long probeStart = GC.GetAllocatedBytesForCurrentThread();
        byte[] allocationProbe = new byte[1024];
        GC.KeepAlive(allocationProbe);
        bool allocationCounterAvailable = GC.GetAllocatedBytesForCurrentThread() > probeStart;
        long allocationStart = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < ticks; index++)
        {
            if (maximumWallSeconds > 0 && totalWatch.Elapsed.TotalSeconds >= maximumWallSeconds) break;
            watch.Restart();
            world.Step();
            watch.Stop();
            samples.Add(watch.Elapsed.TotalMilliseconds);
            peakBatchCount = Math.Max(peakBatchCount, world.Energy.ActiveBatchCount);
            maximumPendingSearches = Math.Max(maximumPendingSearches, world.Energy.PlanningStatistics.PendingSearchCount);
            maximumPathWork = Math.Max(maximumPathWork, world.Energy.PlanningStatistics.WorkUsedThisTick);
        }
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        world.Energy.AssertInvariants();
        samples.Sort();
        return new
        {
            count, warmupTicks, ticks, topology = "密集方阵，四分之一反应堆、二分之一中继、四分之一炮塔；传输半径 7 格",
            completedWarmupTicks,
            completedMeasurementTicks = samples.Count,
            completedRequestedWork = completedWarmupTicks == warmupTicks && samples.Count == ticks,
            elapsedWallSeconds = totalWatch.Elapsed.TotalSeconds,
            setupMilliseconds,
            warmupMaximumMilliseconds,
            medianMilliseconds = samples.Count == 0 ? (double?)null : samples[samples.Count / 2],
            percentile95Milliseconds = samples.Count == 0 ? (double?)null : samples[(int)Math.Ceiling(samples.Count * 0.95) - 1],
            maximumMilliseconds = samples.Count == 0 ? (double?)null : samples[samples.Count - 1],
            allocatedBytes = allocationCounterAvailable ? (long?)allocatedBytes : null,
            allocationCounterAvailable,
            connectionCount = world.Energy.Connections.Count,
            peakBatchCount,
            maximumPendingSearches,
            maximumPathWork,
            planning = world.Energy.PlanningStatistics,
            consumersWithDelivery = world.Buildings.Count(building => building.IsConsumer && building.TotalReceived > 0),
            consumed = world.Energy.GetTotals().Consumed,
            consumedDuringMeasurement = world.Energy.GetTotals().Consumed - consumedBeforeMeasurement,
            deliveredDuringMeasurement = world.Buildings.Where(building => building.IsConsumer).Sum(building => building.TotalReceived) - receivedBeforeMeasurement,
            expired = world.Energy.GetTotals().ExpiredLoss,
            expiredDuringMeasurement = world.Energy.GetTotals().ExpiredLoss - expiredBeforeMeasurement,
            balanceError = world.Energy.GetTotals().BalanceError,
            environment = SystemInfo.processorType,
            unityVersion = Application.unityVersion
        };
    }

    private static BuildingDefinition Definition(BuildingKind kind, long production, long consumption, long output)
    {
        return new BuildingDefinition(kind, kind.ToString(), 1, 1, 100000, production, consumption,
            output, 4, output > 0 ? 7 : 0, 100, 0, 0, 0);
    }

    private static GameSessionRoot RequireSession()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("该入口要求白盒场景正在播放。");
        GameSessionRoot session = UnityEngine.Object.FindAnyObjectByType<GameSessionRoot>();
        if (session == null || session.World == null) throw new InvalidOperationException("白盒会话尚未就绪。");
        return session;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
