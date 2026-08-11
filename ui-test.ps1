$ErrorActionPreference = "Stop"
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$testDir = Join-Path $projectDir "test-output\ui"
$compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$antdDll = Join-Path $projectDir "packages\AntdUI.2.4.4\lib\net48\AntdUI.dll"
New-Item -ItemType Directory -Force -Path $testDir | Out-Null
New-Item -ItemType File -Force -Path (Join-Path $testDir "portable.flag") | Out-Null
Copy-Item -LiteralPath $antdDll -Destination $testDir -Force

$arguments = @(
    "/nologo", "/target:exe", "/platform:x64", "/optimize+", "/codepage:65001",
    "/main:LlamaServerManager.UiSmokeTest",
    "/out:$testDir\UiSmokeTest.exe",
    "/reference:System.dll", "/reference:System.Core.dll", "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll", "/reference:System.Web.Extensions.dll", "/reference:System.Management.dll",
    "/reference:System.IO.Compression.dll", "/reference:System.IO.Compression.FileSystem.dll", "/reference:$antdDll",
    (Join-Path $projectDir "Models.cs"), (Join-Path $projectDir "Services.cs"),
    (Join-Path $projectDir "CommandEditing.cs"),
    (Join-Path $projectDir "CommandValidation.cs"),
    (Join-Path $projectDir "ApiKeyStore.cs"),
    (Join-Path $projectDir "ApiKeyManagerDialog.cs"),
    (Join-Path $projectDir "RuntimeServices.cs"), (Join-Path $projectDir "AdaptiveTuning.cs"),
    (Join-Path $projectDir "PerformanceMonitoring.cs"),
    (Join-Path $projectDir "Theme.cs"), (Join-Path $projectDir "MainFormV2.cs"),
    (Join-Path $projectDir "Program.cs"),
    (Join-Path $projectDir "UiSmokeTest.cs")
)
& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw "UI test compilation failed: $LASTEXITCODE" }
$scenarios = @(
    @{ Name="Light-Dashboard"; Theme="Light"; Page="dashboard"; Width=1320; Height=840; Scale="1.0"; Scroll="top" },
    @{ Name="Light-Monitoring"; Theme="Light"; Page="monitoring"; Width=1320; Height=840; Scale="1.0"; Scroll="top" },
    @{ Name="Dark-Monitoring-Bottom"; Theme="Dark"; Page="monitoring"; Width=1320; Height=840; Scale="1.0"; Scroll="bottom" },
    @{ Name="Dark-Profiles"; Theme="Dark"; Page="profiles"; Width=1320; Height=840; Scale="1.0"; Scroll="top" },
    @{ Name="Dark-Parameters"; Theme="Dark"; Page="parameters"; Width=1320; Height=840; Scale="1.0"; Scroll="top" },
    @{ Name="Dark-ApiKeys"; Theme="Dark"; Page="api-keys"; Width=780; Height=520; Scale="1.0"; Scroll="top" },
    @{ Name="Light-Runtimes"; Theme="Light"; Page="runtimes"; Width=1320; Height=840; Scale="1.0"; Scroll="top" },
    @{ Name="Dark-Logs"; Theme="Dark"; Page="logs"; Width=1320; Height=840; Scale="1.0"; Scroll="top" },
    @{ Name="Light-Settings"; Theme="Light"; Page="settings"; Width=1320; Height=840; Scale="1.0"; Scroll="top" },
    @{ Name="Dark-Dashboard-Minimum-Bottom"; Theme="Dark"; Page="dashboard"; Width=940; Height=600; Scale="1.0"; Scroll="bottom" },
    @{ Name="Dark-Monitoring-Minimum-Bottom"; Theme="Dark"; Page="monitoring"; Width=940; Height=600; Scale="1.0"; Scroll="bottom" },
    @{ Name="Light-Profiles-Minimum-Bottom"; Theme="Light"; Page="profiles"; Width=940; Height=600; Scale="1.0"; Scroll="bottom" },
    @{ Name="Light-Parameters-Minimum-Bottom"; Theme="Light"; Page="parameters"; Width=940; Height=600; Scale="1.0"; Scroll="bottom" },
    @{ Name="Dark-Runtimes-Minimum-Bottom"; Theme="Dark"; Page="runtimes"; Width=940; Height=600; Scale="1.0"; Scroll="bottom" },
    @{ Name="Light-Logs-Minimum-Bottom"; Theme="Light"; Page="logs"; Width=940; Height=600; Scale="1.0"; Scroll="bottom" },
    @{ Name="Dark-Settings-Minimum-Bottom"; Theme="Dark"; Page="settings"; Width=940; Height=600; Scale="1.0"; Scroll="bottom" },
    @{ Name="Light-Settings-125pct"; Theme="Light"; Page="settings"; Width=1500; Height=950; Scale="1.25"; Scroll="top" },
    @{ Name="Dark-Profiles-150pct"; Theme="Dark"; Page="profiles"; Width=1700; Height=1000; Scale="1.5"; Scroll="top" },
    @{ Name="Light-Monitoring-150pct"; Theme="Light"; Page="monitoring"; Width=1700; Height=1000; Scale="1.5"; Scroll="top" },
    @{ Name="Light-Profiles-175pct"; Theme="Light"; Page="profiles"; Width=1645; Height=1050; Scale="1.75"; Scroll="top" },
    @{ Name="Light-Profiles-200pct"; Theme="Light"; Page="profiles"; Width=1880; Height=1200; Scale="2.0"; Scroll="top" }
)
$images = @()
$uiError = Join-Path $testDir "ui-error.txt"
foreach ($scenario in $scenarios) {
    if (Test-Path -LiteralPath $uiError) { Remove-Item -LiteralPath $uiError -Force }
    $image = Join-Path $testDir ("LlamaLift-v0.3-" + $scenario.Name + ".png")
    & (Join-Path $testDir "UiSmokeTest.exe") $image $scenario.Theme $scenario.Page $scenario.Width $scenario.Height $scenario.Scale $scenario.Scroll
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $image)) { throw ("UI render test failed: " + $scenario.Name) }
    $images += $image
}
Get-Item -LiteralPath $images | Select-Object FullName,Length,LastWriteTime
