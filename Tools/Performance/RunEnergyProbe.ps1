param(
    [int]$Count = 100,
    [int]$Warmup = 200,
    [int]$Steps = 200,
    [switch]$Baseline,
    [switch]$Profile,
    [string]$OutputPath = ''
)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$probeDirectory = Join-Path ([IO.Path]::GetTempPath()) ('DefendEnergyProbe-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $probeDirectory | Out-Null
# 冻结同一份第一阶段世界；对照组保留原能源，改进组只覆盖能源源码。
# 所有临时编译产物留在系统临时目录，不增加 Unity 工程依赖。
Expand-Archive -LiteralPath (Join-Path $PSScriptRoot 'EnergyBaselineSource.zip') -DestinationPath $probeDirectory
if (-not $Baseline) {
    Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets/Defend/Scripts/Simulation/Energy') -Filter '*.cs' |
        Copy-Item -Destination (Join-Path $probeDirectory 'Simulation/Energy')
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'EnergyProbe.cs') -Destination (Join-Path $probeDirectory 'Program.cs')
if ($Profile) {
    $energyFile = Join-Path $probeDirectory 'Simulation/Energy/EnergySystem.cs'
    $source = Get-Content -LiteralPath $energyFile -Raw
    $source = $source.Replace('RefreshNodes(tick);', 'long profileStart = 0; RefreshNodes(tick);')
    $calls = @('RefreshNodes(tick);', 'SettleElapsedStalls(tick);', 'planner.Refresh(buildings);',
        'transport.CompleteArrivals(tick);', 'planner.RefreshCongestion(transport.Batches);',
        'planner.BeginStep(tick);', 'ProduceEnergy();', 'bool deferredByWorkBudget = dispatch.Run(tick);',
        'UpdateWaiting(tick, deferredByWorkBudget);', 'transport.RemoveRetired();',
        'transport.UpdateSnapshots();', 'UpdateStatuses(tick, deferredByWorkBudget);')
    foreach ($call in $calls) {
        $name = $call.Replace('bool deferredByWorkBudget = ', '').Replace(';', '')
        $source = $source.Replace($call, "profileStart = System.Diagnostics.Stopwatch.GetTimestamp(); $call ProfileProbe.Add(`"$name`", profileStart);")
    }
    Set-Content -LiteralPath $energyFile -Value $source -Encoding utf8
    $programFile = Join-Path $probeDirectory 'Program.cs'
    $source = Get-Content -LiteralPath $programFile -Raw
    $source = $source.Replace('long consumedBefore =', 'ProfileProbe.Total.Clear(); long consumedBefore =')
    $source = $source.Replace('runtime = System.Runtime', 'phaseMillisecondsPerStep = ProfileProbe.Total.ToDictionary(pair => pair.Key, pair => pair.Value * 1000.0 / Stopwatch.Frequency / steps), runtime = System.Runtime')
    $source += "`ninternal static class ProfileProbe { public static Dictionary<string,long> Total = new Dictionary<string,long>(); public static void Add(string name,long start) { Total.TryGetValue(name, out long value); Total[name]=value+Stopwatch.GetTimestamp()-start; } }"
    Set-Content -LiteralPath $programFile -Value $source -Encoding utf8
}
Set-Content -LiteralPath (Join-Path $probeDirectory 'Probe.csproj') -Value '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><Nullable>disable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>'
dotnet build (Join-Path $probeDirectory 'Probe.csproj') -c Release --nologo --verbosity quiet | Out-Host
if ($LASTEXITCODE -ne 0) { throw '纯 C# 性能探针编译失败。' }
$previousTieredCompilation = $env:DOTNET_TieredCompilation
try {
    # 关闭分层即时编译，避免将运行过程中重新编译方法的尖峰混入前后对照。
    $env:DOTNET_TieredCompilation = '0'
    $result = & dotnet (Join-Path $probeDirectory 'bin/Release/net9.0/Probe.dll') $Count $Warmup $Steps
} finally {
    $env:DOTNET_TieredCompilation = $previousTieredCompilation
}
if ($LASTEXITCODE -ne 0) { throw '纯 C# 性能探针运行失败。' }
if ($OutputPath) { $result | Set-Content -LiteralPath $OutputPath -Encoding utf8 }
$result
