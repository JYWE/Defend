# 开发包自动验收入口

入口为 `Assets/Defend/Scripts/Unity/Development/WhiteboxPlayerProbe.cs`。整个类型受 `DEBUG && !UNITY_EDITOR` 条件编译控制，并在运行时检查 `Debug.isDebugBuild`；采用 Unity 6.6 的托管代码变体方式，避免使用已弃用的开发包条件宏。常规 Release 编译和 Editor 不包含此类型。没有 `-whitebox-smoke` 参数时不创建宿主、不订阅事件、不改变后台运行设置。它不生成或实例化 UI。

## 启动

使用主负责人实际产出的 Windows Development Build 可执行文件；以下路径参数替换为该文件所在位置：

```powershell
& '<Development Build 可执行文件的绝对路径>' `
    -whitebox-smoke `
    -whitebox-output 'E:\UnityProject\Defend\Logs\WhiteboxValidation\PlayerSmoke' `
    -logFile 'E:\UnityProject\Defend\Logs\WhiteboxValidation\PlayerSmoke\player.log'
```

调用方应先创建 `-logFile` 所在目录，并等待玩家进程退出后检查进程退出码和 `whitebox-smoke-report.json`。自动化若使用 `Start-Process`，使用 `-PassThru -WindowStyle Hidden` 获取进程，等待它退出后读取 `ExitCode`。不要传入 `-nographics`；本项验收需要真实渲染帧。

`-whitebox-output` 指定报告和图片目录；省略时使用 `Application.persistentDataPath/WhiteboxValidation`。指定参数却遗漏目录值视为错误。探针会创建输出目录，报告固定命名为 `whitebox-smoke-report.json`，截图使用带时间戳的独立文件名，完整路径记录在报告中。

## 验证与退出

1. 在场景加载完成、场景 `Start` 尚未执行时，记录含 inactive 对象的 `RectTransform` 数量。
2. 等待已有 `GameSessionRoot.World` 就绪，记录起始逻辑步和初始消费者；没有消费者不能通过。
3. 临时允许后台运行。世界完全由现有 `GameSessionRoot.Update` 推进，探针不调用 `World.Step`、不修改优先级、不关闭测试负载。
4. 从观察起点继续至少 200 个逻辑步，检查观察到的每个逻辑状态的能源不变量。最终要求总账平衡、所有初始消费者仍存在且在本轮观察期间新增有效到货、界面数量与启动基线相同。
5. 请求实际游戏画面的截图，等待文件写入句柄释放，直接读取 PNG 文件格式标识和终止块，确认落盘后再保存最终报告。此过程不计算哈希值或校验和，也不替代视觉审查。
6. 成功报告 `status=passed`、`success=true`，以退出码 `0` 正常退出；异常、运行期 Error/Exception/Assert 或超时报告 `failed`，以退出码 `1` 正常退出。退出前恢复原后台运行设置、取消事件订阅；不强制结束其他进程。

启动等待上限为 30 秒，正常模拟观察上限为 120 秒，截图等待上限为 30 秒。输出目录不可写时，探针尝试把失败报告保存到 `Application.persistentDataPath/WhiteboxValidation`，仍以非零退出码结束。若进程收到外部退出请求且验收尚未完成，尽力写入 `interrupted` 报告；外部强制结束进程无法保证报告落盘。

报告直接保存逻辑步、观察帧数、UI 数量、各类能量数值、每个初始消费者的到货量和截图字节数。报告中的字节数是文件长度，不是校验值。

## 证据边界

本入口已经由主负责人完成 Unity 编译、Windows 构建及 1600×900、1280×720 两次独立程序运行，逻辑检查均通过，退出码均为 0。隐藏窗口下两次截图为黑图，因此 **Player 画面尚未通过验收**；不能把 JSON 的 `passed` 解读为视觉通过。完整结果见 `PhaseOneWhiteboxReport.md`，有效 UI 画面证据来自 Editor 的实际游戏帧。

首尾相同的 `RectTransform` 数量能发现数量变化，不能证明运行期间从未发生等量替换。禁止运行期创建 UI 还须结合界面创建调用的源码审查。日志不包含鼠标与键盘操作验证，也不证明 100/400/1600 建筑的性能指标。
