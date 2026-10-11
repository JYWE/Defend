using System.Collections.Generic;
using Defend.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Defend.Unity
{
    /// <summary>白盒鼠标输入适配。预览只改变表现，确认后才把命令交给模拟验证。</summary>
    public sealed class GridInputController : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private EventSystem eventSystem;
        private GameSessionRoot session;
        private WorldPresentation presentation;
        private BuildingKind? placingKind;
        private long movingBuildingId;
        private BuildingDefinition previewDefinition;
        private readonly Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        private readonly List<RaycastResult> interfaceHits = new List<RaycastResult>();
        private PointerEventData pointerEvent;
        public bool HasPlacement => placingKind.HasValue || movingBuildingId != 0;
        public string ModeDescription { get; private set; } = "浏览 / 选择";
        public void Configure(Camera camera, EventSystem events) { worldCamera = camera; eventSystem = events; }
        public void Initialize(GameSessionRoot root, WorldPresentation views)
        {
            session = root;
            presentation = views;
            pointerEvent = new PointerEventData(eventSystem);
        }

        public void BeginPlacement(BuildingKind kind)
        {
            placingKind = kind;
            movingBuildingId = 0;
            previewDefinition = session.FindDefinition(kind).CreateDefinition();
            ModeDescription = "放置 " + session.FindDefinition(kind).DisplayName;
            session.ShowFeedback("在网格上单击确认放置；右键或 Esc 取消。");
        }

        public void BeginMove()
        {
            BuildingState building = session.SelectedBuilding;
            if (building == null || building.IsSold) return;
            if (session.Battle != null && building.Kind == BuildingKind.Core) { session.ShowFeedback("核心不能移动。"); return; }
            placingKind = null;
            movingBuildingId = building.Id;
            previewDefinition = building.Definition;
            ModeDescription = "移动 " + building.Definition.DisplayName;
            long atRisk = 0;
            foreach (EnergyTransferSnapshot transfer in session.World.Energy.Transfers)
                if (transfer.TargetId == building.Id) atRisk += transfer.Amount;
            session.ShowFeedback("移动确认后重算连接；发往旧位置的在途能量可能损失 " + (atRisk / 1000.0).ToString("0.###") + " 点。右键取消。");
        }

        public void Cancel()
        {
            placingKind = null;
            movingBuildingId = 0;
            ModeDescription = "浏览 / 选择";
            presentation.SetPreview(false, 0, 0, 1, 1, false);
        }

        public void ProcessInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) Cancel();
                if (keyboard.spaceKey.wasPressedThisFrame) session.TogglePause();
            }
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.rightButton.wasPressedThisFrame) Cancel();
            Vector2 pointerPosition = mouse.position.ReadValue();
            // 显式射线检测不依赖 EventSystem.Update 的先后顺序，避免刚移到按钮上的点击穿透。
            pointerEvent.position = pointerPosition;
            interfaceHits.Clear();
            eventSystem.RaycastAll(pointerEvent, interfaceHits);
            if (interfaceHits.Count > 0 || !worldCamera.pixelRect.Contains(pointerPosition))
            {
                presentation.SetPreview(false, 0, 0, 1, 1, false);
                return;
            }
            Ray ray = worldCamera.ScreenPointToRay(pointerPosition);
            if (!groundPlane.Raycast(ray, out float distance)) return;
            Vector3 point = ray.GetPoint(distance);
            int x = Mathf.FloorToInt(point.x);
            int z = Mathf.FloorToInt(point.z);
            bool inside = x >= 0 && z >= 0 && x < session.World.Settings.Width && z < session.World.Settings.Depth;
            if (HasPlacement)
            {
                BuildingState moving = movingBuildingId == 0 ? null : session.World.FindBuilding(movingBuildingId);
                if (movingBuildingId != 0 && (moving == null || moving.IsSold)) { Cancel(); return; }
                BuildingDefinitionAsset definition = session.FindDefinition(placingKind ?? moving.Kind);
                string issue = session.World.PlacementIssue(definition.Kind, x, z, movingBuildingId);
                bool valid = inside && issue == null;
                presentation.SetPreview(inside, x, z, definition.Width, definition.Depth, valid);
                presentation.ShowPreviewRange(session.World, previewDefinition, x, z, inside);
                ModeDescription = (movingBuildingId != 0 ? "移动：" : "建造：") + definition.DisplayName + (valid ? " · 可放置" : " · " + issue);
                if (mouse.leftButton.wasPressedThisFrame && inside)
                {
                    session.Submit(placingKind.HasValue ? WorldCommand.Place(placingKind.Value, x, z) : WorldCommand.Move(movingBuildingId, x, z));
                    // 连续购买保留建筑选择；移动完成后返回选择模式。
                    if (movingBuildingId != 0) Cancel();
                }
            }
            else if (mouse.leftButton.wasPressedThisFrame && inside)
            {
                BuildingState selected = session.World.FindBuildingAt(x, z);
                if (selected == null)
                {
                    // 损坏建筑不占格，但仍属玩家所有；优先选活建筑，再选择该位置的残骸以便修复。
                    foreach (BuildingState candidate in session.World.Buildings)
                    {
                        if (candidate.IsSold || candidate.IsAlive) continue;
                        if (x >= candidate.X && x < candidate.X + candidate.Definition.Width &&
                            z >= candidate.Z && z < candidate.Z + candidate.Definition.Depth)
                        { selected = candidate; break; }
                    }
                }
                session.Select(selected == null ? 0 : selected.Id);
            }
        }

        private bool IsAreaAvailable(int x, int z, int width, int depth)
        {
            if (x + width > session.World.Settings.Width || z + depth > session.World.Settings.Depth) return false;
            for (int column = x; column < x + width; column++)
                for (int row = z; row < z + depth; row++)
                {
                    BuildingState occupant = session.World.FindBuildingAt(column, row);
                    if (occupant != null && occupant.Id != movingBuildingId) return false;
                }
            return true;
        }
    }
}
