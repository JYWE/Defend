param([string]$OutputPath = '')
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$probeDirectory = Join-Path ([IO.Path]::GetTempPath()) ('DefendEnergyFairness-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $probeDirectory | Out-Null
Expand-Archive -LiteralPath (Join-Path $PSScriptRoot 'EnergyBaselineSource.zip') -DestinationPath $probeDirectory
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets/Defend/Scripts/Simulation/Energy') -Filter '*.cs' |
    Copy-Item -Destination (Join-Path $probeDirectory 'Simulation/Energy')
$testsDirectory = Join-Path $probeDirectory 'Tests'
New-Item -ItemType Directory -Path $testsDirectory | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'Assets/Defend/Tests/EditMode/SimulationTestWorld.cs') -Destination $testsDirectory
$prioritySource = Get-Content -LiteralPath (Join-Path $projectRoot 'Assets/Defend/Tests/EditMode/EnergyPriorityTests.cs') -Raw
# 只在临时副本扩大统计窗口；80% 吞吐下限和原有比例误差断言完整保留。
$prioritySource = $prioritySource.Replace('1200', '36000').Replace('1500', '36000').Replace('DrainConsumers(world, 1000,', 'DrainConsumers(world, 36000,')
$prioritySource = $prioritySource.Replace('var total = highReceived + normalReceived + lowReceived;', 'var total = highReceived + normalReceived + lowReceived; Console.WriteLine($"DELIVERY high={highReceived}, normal={normalReceived}, low={lowReceived}, total={total}");')
$prioritySource = $prioritySource.Replace('var minimumEffectiveDelivery =', 'Console.WriteLine($"UNEQUAL_LOSS nearby={nearbyReceived}, distant={distantReceived}, relaySent={relaySent}, expired={expired}"); var minimumEffectiveDelivery =')
Set-Content -LiteralPath (Join-Path $testsDirectory 'EnergyPriorityTests.cs') -Value $prioritySource -Encoding utf8
$framework = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Library/PackageCache') -Filter 'nunit.framework.dll' -Recurse | Select-Object -First 1
if (-not $framework) { throw '请先由 Unity 恢复工程自带的 Test Framework；此脚本不会安装依赖。' }
Set-Content -LiteralPath (Join-Path $probeDirectory 'Probe.csproj') -Value "<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><Nullable>disable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><Reference Include=`"nunit.framework`"><HintPath>$($framework.FullName)</HintPath></Reference></ItemGroup></Project>"
Set-Content -LiteralPath (Join-Path $probeDirectory 'Program.cs') -Value @'
using System.Reflection;
int passed = 0, failed = 0;
var type = typeof(Defend.Tests.EnergyPriorityTests);
foreach (var method in type.GetMethods().Where(method => method.GetCustomAttributes(false).Any(attribute => attribute is NUnit.Framework.TestAttribute || attribute is NUnit.Framework.TestCaseAttribute)))
{
    var cases = method.GetCustomAttributes<NUnit.Framework.TestCaseAttribute>().Select(attribute => attribute.Arguments).ToArray();
    if (cases.Length == 0) cases = new[] { Array.Empty<object>() };
    foreach (var arguments in cases)
    {
        try { method.Invoke(Activator.CreateInstance(type), arguments); passed++; Console.WriteLine("PASS " + method.Name); }
        catch (Exception exception) { failed++; Console.WriteLine("FAIL " + method.Name + " " + (exception.InnerException ?? exception)); }
    }
}
Console.WriteLine($"passed={passed}, failed={failed}; long statistical window=36000 simulation steps (30 minutes at 20 Hz)");
Environment.ExitCode = failed == 0 ? 0 : 1;
'@
dotnet build (Join-Path $probeDirectory 'Probe.csproj') -c Release --nologo --verbosity quiet | Out-Host
if ($LASTEXITCODE -ne 0) { throw '长期公平性探针编译失败。' }
$result = & dotnet (Join-Path $probeDirectory 'bin/Release/net9.0/Probe.dll')
if ($OutputPath) { $result | Set-Content -LiteralPath $OutputPath -Encoding utf8 }
$result
if ($LASTEXITCODE -ne 0) { throw '长期公平性检查未通过。' }
