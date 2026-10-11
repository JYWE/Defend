# 第二阶段战斗白盒使用说明

使用 Unity 6000.6.5f1。战斗场景为 `Assets/Defend/Content/Scenes/Dev/CombatWhitebox.unity`，也可以使用“涌现防线 / 创建或打开战斗白盒场景”菜单。已有场景及资源不会被制作工具覆盖。

Windows 开发包位于 `Builds/PhaseTwoWhitebox`，运行 `Start-Combat.cmd` 或 `DefendCombat.exe`。复制给团队时保留整个目录，不能仅复制可执行文件。

## 游玩

- 镜头为相对水平面向下 70 度的正交斜俯视，可在场景中手工调整。滚轮缩放，中键拖动。
- 左键选择建筑或确认放置，右键或 Esc 取消。购买后可以连续放置同类建筑。
- 点击“开始第一波”迎敌。每波清场后领取一次奖励，准备好再点击“开始下一波”。
- 炮塔从自己的缓存中支付完整一次攻击的能量；不足时等待供给，补足后继续射击。正在送达的能量不能提前消费。
- 核心先消耗有限护盾，再损失耐久。核心被摧毁即失败，不能靠维修撤销结局。第五波清场胜利。
- 战斗中可以建造、移动、出售、维修和调整终端供给优先级。暂停时操作排队，继续后结算；空格也可暂停或继续。
- 波次切换保留储能、在途能量、供给承诺、优先级和累计滞留；重新开始才创建新世界并恢复初始布局。

初始防线为 13 栋建筑，留有扩建资金。当前白盒需要继续建设，初始布局不操作会在第四波失败。一条用于流程验收的策略是在第二波后追加六座炮塔（格子坐标）：`(20,14)`、`(20,26)`、`(14,17)`、`(26,17)`、`(14,23)`、`(26,23)`，等待约五秒充能后继续。此例用于复现通关，不是唯一解法。

## 手工编辑

| 内容 | 入口 |
| --- | --- |
| 场景与初始布局 | `Content/Scenes/Dev/CombatWhitebox.unity`，初始建筑还需加入 `WorldPresentation.Authored Buildings` 列表 |
| 主界面 | `Content/Prefabs/UI/CombatWhiteboxPanel.prefab`，所有控件已保存，运行时不创建或实例化 UI |
| 敌人与波次、炮塔攻击 | `Content/Definitions/Combat/WhiteboxBattleSettings.asset` |
| 地图、初始资金、能源试验参数 | `Content/Definitions/Combat/CombatEnergySettings.asset` |
| 共用建筑参数 | `Content/Definitions/Buildings`，修改会同时影响能源试验场 |
| 敌人和射击外观 | `Content/Prefabs/Enemies` 与 `Content/Prefabs/VFX`，使用可替换白盒几何 |
| 镜头 | 场景摄像机的 Transform 和 `WorldCameraViewport`，保存的初始俯角为 70 度 |

表中相对路径以 `Assets/Defend` 为根。初始建筑同样支付购买费用，修改布局时须留足资金；不能占据四个敌人入口。运行时移动和重开不会写回资产。

## 当前白盒规则

场上最多 100 栋未出售建筑（包括损毁建筑）及 200 个同时存活敌人。敌人朝核心移动，有开放路线就绕开建筑；完全封路时攻击当前能接近、朝核心方向的阻挡建筑。这是拆障启发式，并非全局最优拆除路线。敌人可以共享格子，暂不实现实体之间的挤压碰撞。

炮塔优先攻击射程内最近敌人，同距离按稳定身份排序；伤害即时结算，攻击不受建筑遮挡，射击线只负责显示。建造、移动和恢复建筑不能占用敌人当前或正在走向的格子。核心不可移动、出售或重复购买。

维修暂沿用固定价格、即时修满耐久；波末不自动修复，也不补满护盾。能量精度、护盾换算和所有战斗数值都是当前可调整的白盒参数。正式美术、音效、存档和特殊敌人不在本阶段。

## 验证入口

`Tools/Unity/CombatValidation.cs` 提供资产检查、真实 Input System 交互、五波策略及压力场景制作入口。开发包仅在显式 `-combat-smoke` 参数下启动自动探针；正常游戏不会创建该探针。

`CombatStress.unity` 是单独的中型规模压力场景：100 栋建筑、200 个高耐久敌人，持续制造攻击和供电需求；其高耐久测试参数不属于正常玩法。开发包同时包含两张场景，用 `-combat-smoke -combat-stress-seconds <现实秒数>` 自动进入压力场景。

最终证据、实际耗时、失败项和未验证项以 `Docs/Validation/PhaseTwoWhiteboxReport.md` 为准。不得将编译通过或纯 C# 模拟通过当作 Player 画面及完整性能达标。

独立程序自动验收可从工程根目录复现：

```powershell
# 五波策略、胜利、重开与核心失败；按真实时间推进。
Tools/Unity/RunCombatAcceptance.ps1 -Mode Gameplay -Width 1280 -Height 720

# 100 建筑、200 敌人，正常一倍速实际运行 30 分钟。
Tools/Unity/RunCombatAcceptance.ps1 -Mode Stress -DurationSeconds 1800

# 单独记录 10 秒 CPU Profiler 文件；与帧率验收分开执行。
Tools/Unity/RunCombatAcceptance.ps1 -Mode Profile -DurationSeconds 60
```

脚本启动可见窗口，等待程序自动退出，记录退出码与 JSON；它不会替代人工查看 PNG 或实际窗口。只有显式启用采样时才记录 Profiler，文件可用 Unity Profiler 打开，或由 `Tools/Unity/ReadCombatProfile.cs` 读取。实际交互、镜头输入和六次重开的 Editor 验证入口在 `CombatValidation.cs`。
