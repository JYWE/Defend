param(
    [ValidateSet('Gameplay', 'Stress', 'Profile')]
    [string]$Mode = 'Gameplay',
    [ValidateRange(1, 86400)]
    [int]$DurationSeconds = 1800,
    [ValidateRange(800, 7680)]
    [int]$Width = 1920,
    [ValidateRange(600, 4320)]
    [int]$Height = 1080,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$projectDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$playerPath = Join-Path $projectDirectory 'Builds\PhaseTwoWhitebox\DefendCombat.exe'
if (-not (Test-Path -LiteralPath $playerPath)) { throw "请先构建独立程序：$playerPath" }
if (Get-Process -Name DefendCombat -ErrorAction SilentlyContinue) {
    throw '已有战斗程序在运行。请先结束当前测试，避免并行负载污染性能记录。'
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $projectDirectory ('Logs\CombatValidation\' + $Mode + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$acceptanceDirectory = [IO.Path]::GetFullPath($OutputPath)
if ($acceptanceDirectory.Contains('"')) { throw '输出目录不能包含双引号。' }
New-Item -ItemType Directory -Path $acceptanceDirectory -Force | Out-Null
$playerArguments = @('-screen-fullscreen', '0', '-screen-width', $Width, '-screen-height', $Height,
    '-combat-smoke', '-combat-output', ('"' + $acceptanceDirectory + '"'),
    '-logFile', ('"' + (Join-Path $acceptanceDirectory 'player.log') + '"'))
if ($Mode -ne 'Gameplay') { $playerArguments += @('-combat-stress-seconds', $DurationSeconds) }
if ($Mode -eq 'Profile') { $playerArguments += '-combat-profile' }

# 本验收需要真实渲染的可见窗口；不使用无图形模式或隐藏窗口冒充画面验证。
$acceptancePlayer = Start-Process -FilePath $playerPath -ArgumentList $playerArguments -WindowStyle Normal -PassThru
$acceptancePlayer.Id | Set-Content -LiteralPath (Join-Path $acceptanceDirectory 'process-id.txt') -Encoding utf8
Write-Output "正在运行 $Mode 验收，进程 $($acceptancePlayer.Id)，结果目录 $acceptanceDirectory"
$acceptancePlayer.WaitForExit()
$acceptancePlayer.Refresh()
$acceptancePlayer.ExitCode | Set-Content -LiteralPath (Join-Path $acceptanceDirectory 'exit-code.txt') -Encoding utf8
$reportPath = Join-Path $acceptanceDirectory 'combat-player-report.json'
if (-not (Test-Path -LiteralPath $reportPath)) { throw '独立程序没有产生验收报告，请检查 player.log。' }
$acceptanceReport = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if (-not $acceptanceReport.final -or -not $acceptanceReport.success -or $acceptancePlayer.ExitCode -ne 0) {
    throw ('独立程序验收失败：' + $acceptanceReport.failure)
}
$acceptanceReport | Select-Object mode, success, width, height, elapsedSeconds, initialBuildings,
    peakEnemies, victory, victoryKills, restart, defeat, balanceError, warmMaximumPendingSeconds,
    frameP95, frameP99, stepP95, stepP99 | ConvertTo-Json
