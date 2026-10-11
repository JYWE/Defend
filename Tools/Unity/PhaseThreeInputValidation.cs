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

public static class PhaseThreeInputValidation
{
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
        foreach (var entry in new[] { new { button = "Build3", kind = BuildingKind.BurstTurret, x = 18 }, new { button = "Build4", kind = BuildingKind.AreaTurret, x = 19 } })
        {
            ClickButton(entry.button); ClickCell(entry.x, 18); session.SingleStep();
            Require(session.World.FindBuildingAt(entry.x,18)?.Kind == entry.kind, "新增炮塔按钮未正确建造。");
            ClickButton("Cancel");
        }
        ClickButton("Build1"); ClickCell(11,17); session.SingleStep();
        Require(!session.World.LastCommandResult.Succeeded && session.World.IsTerrainBlocked(11,17), "岩体放置未被拒绝。");
        ClickButton("Cancel");
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
            float scrollStep = InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange &&
                (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer) ? 120 : 1;
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = point, scroll = new Vector2(0, scrollStep) });
            InputSystem.Update(); controller.SendMessage("LateUpdate");
            Require(camera.orthographicSize < originalSize, "滚轮没有放大地图。");
            Require(camera.orthographicSize <= originalSize * 0.9f, "单格滚轮的缩放幅度不足，请检查输入单位和灵敏度。");
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
    private static RectTransform[] ReadUi() => Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
    private static GameSessionRoot Session()
    {
        GameSessionRoot session = Object.FindAnyObjectByType<GameSessionRoot>();
        Require(Application.isPlaying && session?.Battle != null, "请先启动战斗场景。");
        return session;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
