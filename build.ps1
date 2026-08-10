$ErrorActionPreference = "Stop"

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$version = "0.1.0-internal"
$distDir = Join-Path $projectDir "dist"
$installerDistDir = Join-Path $projectDir "dist-installer"
$releaseDir = Join-Path $projectDir "release"
$compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$antdDll = Join-Path $projectDir "packages\AntdUI.2.4.4\lib\net48\AntdUI.dll"

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "找不到 Windows 自带 C# 编译器：$compiler"
}

if (-not (Test-Path -LiteralPath $antdDll)) {
    throw "找不到 AntdUI.dll：$antdDll"
}

New-Item -ItemType Directory -Force -Path $distDir | Out-Null
New-Item -ItemType Directory -Force -Path $installerDistDir | Out-Null
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
Get-ChildItem -LiteralPath $distDir -Force | Remove-Item -Recurse -Force
Get-ChildItem -LiteralPath $installerDistDir -Force | Remove-Item -Recurse -Force

$sources = @(
    (Join-Path $projectDir "Models.cs"),
    (Join-Path $projectDir "Services.cs"),
    (Join-Path $projectDir "Theme.cs"),
    (Join-Path $projectDir "MainFormV2.cs"),
    (Join-Path $projectDir "Program.cs")
)

$arguments = @(
    "/nologo",
    "/target:winexe",
    "/platform:x64",
    "/optimize+",
    "/debug-",
    "/codepage:65001",
    "/win32manifest:$projectDir\app.manifest",
    "/out:$distDir\LlamaServerManager.exe",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll",
    "/reference:System.Web.Extensions.dll",
    "/reference:$antdDll"
) + $sources

& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
    throw "编译失败，退出代码：$LASTEXITCODE"
}

Copy-Item -LiteralPath $antdDll -Destination $distDir -Force
Copy-Item -LiteralPath (Join-Path $projectDir "README.txt") -Destination $distDir -Force
Copy-Item -LiteralPath (Join-Path $projectDir "THIRD-PARTY-NOTICES.txt") -Destination $distDir -Force
New-Item -ItemType File -Path (Join-Path $distDir "portable.flag") -Force | Out-Null

Copy-Item -Path (Join-Path $distDir "*") -Destination $installerDistDir -Recurse -Force
Remove-Item -LiteralPath (Join-Path $installerDistDir "portable.flag") -Force

$packagePath = Join-Path $releaseDir ("LlamaServerManager-v" + $version + "-portable-win-x64.zip")
Compress-Archive -Path (Join-Path $distDir "*") -DestinationPath $packagePath -Force

Write-Host ""
Write-Host "Build succeeded:" -ForegroundColor Green
Write-Host (Join-Path $distDir "LlamaServerManager.exe")
Write-Host "Portable package: $packagePath"
Write-Host "Installer payload: $installerDistDir"
