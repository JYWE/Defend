# 第一阶段能源白盒使用说明

使用 Unity 6000.6.5f1 打开工程。开发场景为 `Assets/Defend/Content/Scenes/Dev/EnergyWhitebox.unity`，也可以通过菜单“涌现防线 / 创建或打开能源白盒场景”打开。已有场景、Prefab 和配置不会被生成器重建覆盖。

Windows 开发包在 `Builds/PhaseOneWhitebox`。双击 `Start-Whitebox.cmd` 以 1600 × 900 窗口启动，也可以直接运行 `DefendWhitebox.exe`。复制给团队试玩时需要保留整个目录，包括数据目录、UnityPlayer.dll 和第三方许可，不能只复制 exe。构建产物被 Git 忽略，换机器后按下方命令重新生成。

## 本阶段可以做什么

地图中预置反应堆、上下两条中继支路、炮塔和核心。可以在运行中购买、放置、选择、移动、出售、损坏和修复建筑，修改终端优先级，观察能量在有效路线上的运输。暂停后，操作先排队，按“单步”或继续模拟才结算。

- 左键选择建筑或确认放置，右键 / Esc 取消放置。
- 地图区域用滚轮缩放，中键拖动；空格键暂停或继续。
- 终端优先级为低、中、高，对应权重 1、2、4。比例按有效到货统计，单个时刻不保证同时到货。
- “测试消耗”给炮塔和核心持续负载，用于验证补充缓存；关闭后可以观察储能逐渐填满。
- “损坏试验”施加相当于最大耐久的伤害。核心先消耗已有护盾，因此一次试验不一定损坏核心。
- 底部可以暂停、单步和调整速度。左侧开发诊断显示储量、损耗和守恒差额。

显示层默认最多显示 160 个运输批次和 600 条连接，可在试验配置中调整；这是画面数量上限，不裁剪逻辑批次、连接或能量账目。

移动只在确认时改变逻辑。发往旧位置、尚未完成接收的能量会损失；建筑内部储能随建筑保留。中继被毁或出售后内部能量损失，其他存活中继中的旧能量会重新选择路线和目标。恢复原路不会清除访问历史或重新补满缓存。

## 手动编辑入口

| 内容 | 位置和方式 |
| --- | --- |
| 四种建筑参数 | `Content/Definitions/Buildings` 下的 ScriptableObject，在 Inspector 调整容量、产量、输出间隔、费用等 |
| 模拟参数 | `Content/Definitions/Energy/WhiteboxEnergySettings.asset` |
| 初始布局 | 开发场景中的“建筑 · 可直接编辑初始布局”，移动 Prefab 实例；逻辑占格按位置向下取整 |
| 建筑外观 | `Content/Prefabs/Buildings`，替换 `VisualRoot` 下的白盒几何；保留视图引用和功能锚点 |
| 整套界面 | `Content/Prefabs/UI/EnergyWhiteboxPanel.prefab`，进入 Prefab Mode 编辑文字、层级和布局 |
| 能量外观 | `Content/Prefabs/VFX`，仅负责显示，不决定能量到货 |
| 镜头 | 场景主摄像机的 `WorldCameraViewport` |

以上相对路径以 `Assets/Defend` 为根。UI 在编辑状态已经完整存在；运行时不会创建或实例化 UI 控件。建筑和能量表现可以实例化已有 Prefab。

新增或删除 UI 控件后，应同步检查 `EnergyControlPanel` 的序列化引用。新增建筑类型需要同时扩展定义、商店绑定和模拟角色，不靠运行时补控件兜底。初始摆放建筑也会按配置消耗初始资金，配置时应留出足够资金。

新增初始建筑时，除了把建筑 Prefab 拖入场景，还需要将其实例加入 `WorldPresentation` 的 `Authored Buildings` 列表。该列表明确规定参与新世界初始化的建筑，场景中的装饰或临时预览不会被自动当作逻辑建筑。

## CLI 与验证

保持同一工程仅有一个 Unity Editor 进程。使用现有 Unity CLI 和 Pipeline，无需再安装桥接包。

```powershell
unity --no-banner --json command --project-path 'E:\UnityProject\Defend' recompile
unity --no-banner --json command --project-path 'E:\UnityProject\Defend' recompile_status
unity --no-banner --json command --project-path 'E:\UnityProject\Defend' run_tests --mode editor --filter Defend.Simulation.EditModeTests --filter_type assembly --async_tests true
unity --no-banner --json command --project-path 'E:\UnityProject\Defend' test_status
unity --no-banner --json command --project-path 'E:\UnityProject\Defend' run_script --file Tools/Unity/WhiteboxValidation.cs --entry WhiteboxValidation.ValidateContent
unity --no-banner --json command --project-path 'E:\UnityProject\Defend' build --target StandaloneWindows64 --outputPath 'Builds/PhaseOneWhitebox/DefendWhitebox.exe' --options '["Development","DetailedBuildReport"]' --scenes '["Assets/Defend/Content/Scenes/Dev/EnergyWhitebox.unity"]' --confirm true
unity --no-banner --json command --project-path 'E:\UnityProject\Defend' build_status
```

等待 `build_status` 明确返回 `Succeeded` 后，复制版本控制中的启动脚本模板：

```powershell
Copy-Item 'Tools/Unity/Templates/Start-Whitebox.cmd' 'Builds/PhaseOneWhitebox/Start-Whitebox.cmd'
```

`ValidateContent` 要求在编辑模式打开白盒场景。工具脚本还提供播放模式的 `RuntimeState`、`RuntimeSmoke`、`ExercisePointerFlow` 以及纯模拟测量 `Measure`。运行测试和测量前阅读源码中的前置条件；自动输入测试会临时忽略窗口焦点并在结束后恢复，不修改输入资产。

最终验证结论记录在 `Docs/Validation/PhaseOneWhiteboxReport.md`。CLI 的请求提交成功不等于内层执行成功，尤其应检查 `run_script` 的内层 `success` 和异步测试的完成状态。

Windows 开发包的自动验收另见 `Docs/Validation/WhiteboxPlayerSmoke.md`。该入口默认关闭，使用实际玩家帧推进模拟，保存 PNG 和 JSON 后退出；正式发行版本不包含该探针。

## 当前边界

这是能源网络试验场，尚未加入敌人、完整战斗和波次、存档、音效或最终美术。即时修复费用、护盾换算、初始资金和测试产能均为白盒参数。能源本身不引用波次状态，后续接入波次时必须复用同一世界，不能清空或重新生成能源状态。

字体使用 Noto Sans CJK SC，许可和上游来源保存在字体目录；构建会附带第三方许可。正式关卡规模和性能目标需要以测量结果为依据，不能把小场景通过当成全规模性能达标。

本轮高密度 400、1600 建筑场景未通过默认 20 Hz 的性能要求，并有供给积压；详细数字见验证报告。当前交付适合默认小型网络的玩法和交互讨论，不能据此承诺大规模关卡性能。
