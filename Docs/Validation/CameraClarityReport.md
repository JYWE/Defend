# 镜头缩放与 Game 窗口清晰度修正

2026-10-10，Unity 6000.6.5f1，场景 `IndustrialDefense.unity`。用户确认模糊出现在 Unity Game 窗口。

## 原因与修改

- 当前 Input System 使用 `UniformAcrossAllPlatforms`，一格滚轮输入为 1。旧镜头系数按 Windows 原始 120 单位设计，实际一格仅缩小视野 0.1499%。`WorldCameraViewport` 现在按归一化滚轮计算，默认灵敏度 0.22，一格缩小视野 19.7481%；若切换为 Windows 原始单位，则先除以 120。细分输入仍按比例连续生效。
- 本机 Game 窗口开启了 `Low Resolution Aspect Ratios`，在 150% 屏幕缩放下只渲染 979×498，再放大 1.5 倍。已关闭该选项，同一个窗口实际渲染 1468×748，预览比例为 1:1。该设置属于本机编辑器布局，换用其他布局时应检查该选项。
- URP 渲染比例原本就是 1，摄像机没有动态分辨率或后处理。本轮没有改动模型、材质、渲染管线、玩法参数、UI 结构或镜头远近限位。

## 验证

- Unity 重编译完成，无编译错误。
- 在实际关卡 Play Mode 中，通过 Input System 事件验证归一化单格输入、Windows 原始 120 单位、四次 0.25 输入、反向恢复、界面区域防穿透、近远限位、暂停时操作以及模拟状态不变。全部通过。
- 鼠标指向地面的保持误差约 0.000004 世界单位。原有中键拖动检查通过，镜头仍保持 70 度俯角。
- 修正旧验证脚本使用 120 作为固定测试输入的问题，新增最小缩放幅度检查，防止慢速问题被仅检查方向的测试遗漏。
- 已检查 Game 窗口实际截图：`Logs/CameraClarity/before-screen.png`（979×498）与 `after-screen.png`（1468×748）。取图期间临时允许运行在后台，冻结战斗以保持相同构图；结束时恢复后台运行设置并退出播放模式。
- 本轮针对 Editor 问题验证，未重新构建独立试玩程序，也没有进行完整八波或持续帧率测试。

检查入口：`Tools/Unity/CameraClarityValidation.cs`；缩放与拖动结果分别见 `Logs/CameraClarity/zoom-validation.json`、`pan-validation.json`。
