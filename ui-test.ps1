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
    "/reference:System.Windows.Forms.dll", "/reference:System.Web.Extensions.dll", "/reference:$antdDll",
    (Join-Path $projectDir "Models.cs"), (Join-Path $projectDir "Services.cs"),
    (Join-Path $projectDir "Theme.cs"), (Join-Path $projectDir "MainFormV2.cs"),
    (Join-Path $projectDir "Program.cs"),
    (Join-Path $projectDir "UiSmokeTest.cs")
)
& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw "UI test compilation failed: $LASTEXITCODE" }
$scenarios = @(
    @{ Name="Light-Dashboard"; Theme="Light"; Page="dashboard"; Width=1320; Height=840; Scale="1.0"; Scroll="top" },
    @{ Name="Dark-Profiles"; Theme="Dark"; Page="profiles"; Width=1320; Height=840; Scale="1.0"; Scroll="top" },
    @{ Name="Light-Settings-Compact"; Theme="Light"; Page="settings"; Width=980; Height=640; Scale="1.0"; Scroll="top" },
    @{ Name="Light-Settings-Compact-Bottom"; Theme="Light"; Page="settings"; Width=980; Height=640; Scale="1.0"; Scroll="bottom" },
    @{ Name="Dark-Profiles-Bottom"; Theme="Dark"; Page="profiles"; Width=1320; Height=840; Scale="1.0"; Scroll="bottom" },
    @{ Name="Dark-Logs-Compact"; Theme="Dark"; Page="logs"; Width=980; Height=640; Scale="1.0"; Scroll="top" },
    @{ Name="Light-Settings-125pct"; Theme="Light"; Page="settings"; Width=1500; Height=950; Scale="1.25"; Scroll="top" },
    @{ Name="Dark-Profiles-150pct"; Theme="Dark"; Page="profiles"; Width=1700; Height=1000; Scale="1.5"; Scroll="top" },
    @{ Name="Dark-Profiles-150pct-Bottom"; Theme="Dark"; Page="profiles"; Width=1700; Height=1000; Scale="1.5"; Scroll="bottom" }
)
$images = @()
foreach ($scenario in $scenarios) {
    $image = Join-Path $testDir ("LlamaServerManager-v0.1-" + $scenario.Name + ".png")
    & (Join-Path $testDir "UiSmokeTest.exe") $image $scenario.Theme $scenario.Page $scenario.Width $scenario.Height $scenario.Scale $scenario.Scroll
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $image)) { throw ("UI render test failed: " + $scenario.Name) }
    $images += $image
}
Get-Item -LiteralPath $images | Select-Object FullName,Length,LastWriteTime
