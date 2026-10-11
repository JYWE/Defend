using System.Text;
using Defend.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Defend.Unity
{
    /// <summary>完整 UI 来自预制体；本组件只订阅现有按钮、更新文字和交互状态。</summary>
    public sealed class EnergyControlPanel : MonoBehaviour
    {
        [SerializeField] private Text resourceText;
        [SerializeField] private Text selectionTitle;
        [SerializeField] private Text selectionText;
        [SerializeField] private Text diagnosticsText;
        [SerializeField] private Text feedbackText;
        [SerializeField] private Text modeText;
        [SerializeField] private Text pauseLabel;
        [SerializeField] private Text speedLabel;
        [SerializeField] private Text loadLabel;
        [SerializeField] private Image energyFill;
        [SerializeField] private Button[] placementButtons;
        [SerializeField] private Text[] placementLabels;
        [SerializeField] private Button moveButton;
        [SerializeField] private Button sellButton;
        [SerializeField] private Button damageButton;
        [SerializeField] private Button repairButton;
        [SerializeField] private Button[] priorityButtons;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button stepButton;
        [SerializeField] private Button speedButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button cancelButton;
        private GameSessionRoot session;
        private readonly StringBuilder textBuffer = new StringBuilder(768);
        private static readonly BuildingKind[] ShopKinds = { BuildingKind.Reactor, BuildingKind.Relay, BuildingKind.Turret, BuildingKind.Core };

        public void Initialize(GameSessionRoot root)
        {
            ValidateReferences();
            if (session != null) Unbind();
            session = root;
            placementButtons[0].onClick.AddListener(PlaceReactor);
            placementButtons[1].onClick.AddListener(PlaceRelay);
            placementButtons[2].onClick.AddListener(PlaceTurret);
            placementButtons[3].onClick.AddListener(PlaceCore);
            moveButton.onClick.AddListener(MoveSelected);
            sellButton.onClick.AddListener(session.SellSelected);
            damageButton.onClick.AddListener(session.DamageSelected);
            repairButton.onClick.AddListener(session.RepairSelected);
            priorityButtons[0].onClick.AddListener(SetLowPriority);
            priorityButtons[1].onClick.AddListener(SetNormalPriority);
            priorityButtons[2].onClick.AddListener(SetHighPriority);
            pauseButton.onClick.AddListener(session.TogglePause);
            stepButton.onClick.AddListener(session.SingleStep);
            speedButton.onClick.AddListener(session.CycleSpeed);
            loadButton.onClick.AddListener(session.ToggleTestLoad);
            cancelButton.onClick.AddListener(CancelPlacement);
            for (int index = 0; index < ShopKinds.Length; index++)
            {
                BuildingDefinition definition = session.FindDefinition(ShopKinds[index]).CreateDefinition();
                placementLabels[index].text = definition.DisplayName + "\n" + definition.PurchaseCost + " 资金";
            }
        }

        public void Refresh()
        {
            if (session == null || session.World == null) return;
            GameWorld world = session.World;
            resourceText.text = "资金  " + world.Credits + "     模拟时间  " + (world.CurrentTick / (double)world.Settings.TicksPerSecond).ToString("0.0") + " 秒";
            feedbackText.text = session.Feedback;
            modeText.text = session.GridInput.ModeDescription;
            pauseLabel.text = session.Paused ? "继续模拟" : "暂停模拟";
            speedLabel.text = "速度  " + session.Speed.ToString("0") + " 倍";
            loadLabel.text = world.TestLoadEnabled ? "测试消耗：开启" : "测试消耗：关闭";
            cancelButton.interactable = session.GridInput.HasPlacement;
            RefreshSelection();
            RefreshDiagnostics();
        }

        private void RefreshSelection()
        {
            BuildingState building = session.SelectedBuilding;
            bool available = building != null && !building.IsSold;
            moveButton.interactable = available;
            sellButton.interactable = available;
            damageButton.interactable = available && building.IsAlive;
            repairButton.interactable = available && building.Health < building.Definition.MaximumHealth;
            foreach (Button button in priorityButtons) button.interactable = available && building.IsConsumer && building.IsAlive;
            if (!available)
            {
                selectionTitle.text = "未选择建筑";
                selectionText.text = "单击地图中的建筑\n查看储能、供给与优先级。\n\n青色  反应堆\n蓝色  中继器\n橙色  炮塔\n紫色  核心";
                energyFill.fillAmount = 0;
                return;
            }
            selectionTitle.text = building.Definition.DisplayName + "  /  " + building.Id;
            textBuffer.Clear();
            textBuffer.Append(building.IsAlive ? "运行中" : "已损坏，等待修复");
            textBuffer.Append("    耐久 ").Append(building.Health).Append('/').Append(building.Definition.MaximumHealth);
            textBuffer.Append("\n\n本地储能  ").Append(Amount(building.StoredEnergy)).Append(" / ").Append(Amount(building.Definition.EnergyCapacity));
            textBuffer.Append("\n预计供给  ").Append(Amount(building.CommittedIncoming));
            textBuffer.Append("\n下一跳预约  ").Append(Amount(building.ReservedIncoming));
            textBuffer.Append("\n累计有效到货  ").Append(Amount(building.TotalReceived));
            textBuffer.Append("\n\n优先级  ").Append(building.IsConsumer ? PriorityName(building.PriorityWeight) : "由下游需求决定");
            textBuffer.Append("\n状态  ").Append(building.EnergyStatus);
            textBuffer.Append("\n\n出售返还  ").Append(building.Definition.SaleRefund);
            textBuffer.Append("    修复费用  ").Append(building.Definition.RepairCost);
            selectionText.text = textBuffer.ToString();
            energyFill.fillAmount = building.Definition.EnergyCapacity == 0 ? 0 : (float)building.StoredEnergy / building.Definition.EnergyCapacity;
        }

        private void RefreshDiagnostics()
        {
            EnergyTotals totals = session.World.Energy.GetTotals();
            int activeBuildings = 0;
            foreach (BuildingState building in session.World.Buildings) if (!building.IsSold) activeBuildings++;
            textBuffer.Clear();
            textBuffer.Append("建筑 ").Append(activeBuildings).Append("  ·  连接 ").Append(session.World.Energy.Connections.Count);
            textBuffer.Append("\n活动批次 ").Append(session.World.Energy.ActiveBatchCount);
            textBuffer.Append("\n产生 ").Append(Amount(totals.Generated)).Append("    消耗 ").Append(Amount(totals.Consumed));
            textBuffer.Append("\n储存 ").Append(Amount(totals.Stored)).Append("    在途 ").Append(Amount(totals.InFlight));
            textBuffer.Append("\n传输损耗 ").Append(Amount(totals.TransmissionLoss));
            textBuffer.Append("\n摧毁 / 端点失效 ").Append(Amount(totals.DestructionLoss));
            textBuffer.Append("\n滞留消散 ").Append(Amount(totals.ExpiredLoss));
            textBuffer.Append("\n守恒差额 ").Append(Amount(totals.BalanceError));
            textBuffer.Append("\n逻辑步 ").Append(session.World.CurrentTick).Append("    积压 ").Append(session.PendingSimulationSeconds.ToString("0.000")).Append(" 秒");
            diagnosticsText.text = textBuffer.ToString();
        }

        private static string Amount(long amount) => (amount / 1000.0).ToString("0.###");
        private static string PriorityName(int weight) => weight == 4 ? "高（权重 4）" : weight == 2 ? "中（权重 2）" : weight == 1 ? "低（权重 1）" : "权重 " + weight;
        private void PlaceReactor() => session.GridInput.BeginPlacement(BuildingKind.Reactor);
        private void PlaceRelay() => session.GridInput.BeginPlacement(BuildingKind.Relay);
        private void PlaceTurret() => session.GridInput.BeginPlacement(BuildingKind.Turret);
        private void PlaceCore() => session.GridInput.BeginPlacement(BuildingKind.Core);
        private void MoveSelected() => session.GridInput.BeginMove();
        private void CancelPlacement() => session.GridInput.Cancel();
        private void SetLowPriority() => session.SetSelectedPriority(1);
        private void SetNormalPriority() => session.SetSelectedPriority(2);
        private void SetHighPriority() => session.SetSelectedPriority(4);

        private void OnDestroy() { Unbind(); }
        private void ValidateReferences()
        {
            if (resourceText == null || selectionTitle == null || selectionText == null || diagnosticsText == null ||
                feedbackText == null || modeText == null || pauseLabel == null || speedLabel == null || loadLabel == null || energyFill == null ||
                moveButton == null || sellButton == null || damageButton == null || repairButton == null || pauseButton == null ||
                stepButton == null || speedButton == null || loadButton == null || cancelButton == null)
                throw new System.InvalidOperationException("能源控制面板缺少必需的文字、按钮或储能条引用，请检查 UI Prefab 的 Inspector。");
            if (placementButtons == null || placementButtons.Length != 4 || placementLabels == null || placementLabels.Length != 4 ||
                priorityButtons == null || priorityButtons.Length != 3)
                throw new System.InvalidOperationException("能源控制面板需要四个建筑按钮、四个建筑标签和三个优先级按钮。");
            foreach (Button button in placementButtons) if (button == null) throw new System.InvalidOperationException("建筑按钮引用不能为空。");
            foreach (Text label in placementLabels) if (label == null) throw new System.InvalidOperationException("建筑标签引用不能为空。");
            foreach (Button button in priorityButtons) if (button == null) throw new System.InvalidOperationException("优先级按钮引用不能为空。");
            if (energyFill.sprite == null || energyFill.type != Image.Type.Filled || energyFill.fillMethod != Image.FillMethod.Horizontal)
                throw new System.InvalidOperationException("储能条必须配置 Sprite 和水平填充模式，否则不能正确表示剩余能量。");
        }

        private void Unbind()
        {
            if (session == null) return;
            placementButtons[0].onClick.RemoveListener(PlaceReactor);
            placementButtons[1].onClick.RemoveListener(PlaceRelay);
            placementButtons[2].onClick.RemoveListener(PlaceTurret);
            placementButtons[3].onClick.RemoveListener(PlaceCore);
            moveButton.onClick.RemoveListener(MoveSelected);
            sellButton.onClick.RemoveListener(session.SellSelected);
            damageButton.onClick.RemoveListener(session.DamageSelected);
            repairButton.onClick.RemoveListener(session.RepairSelected);
            priorityButtons[0].onClick.RemoveListener(SetLowPriority);
            priorityButtons[1].onClick.RemoveListener(SetNormalPriority);
            priorityButtons[2].onClick.RemoveListener(SetHighPriority);
            pauseButton.onClick.RemoveListener(session.TogglePause);
            stepButton.onClick.RemoveListener(session.SingleStep);
            speedButton.onClick.RemoveListener(session.CycleSpeed);
            loadButton.onClick.RemoveListener(session.ToggleTestLoad);
            cancelButton.onClick.RemoveListener(CancelPlacement);
            session = null;
        }
    }
}
