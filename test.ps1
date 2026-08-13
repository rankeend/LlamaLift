$ErrorActionPreference = "Stop"

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$testDir = Join-Path $projectDir "test-output"
$compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"

New-Item -ItemType Directory -Force -Path $testDir | Out-Null

$arguments = @(
    "/nologo",
    "/target:exe",
    "/platform:x64",
    "/optimize+",
    "/codepage:65001",
    "/main:LlamaServerManager.SmokeTests",
    "/out:$testDir\SmokeTests.exe",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Web.Extensions.dll",
    "/reference:System.Management.dll",
    "/reference:System.IO.Compression.dll",
    "/reference:System.IO.Compression.FileSystem.dll",
    (Join-Path $projectDir "Models.cs"),
    (Join-Path $projectDir "ApiProtocols.cs"),
    (Join-Path $projectDir "Services.cs"),
    (Join-Path $projectDir "CommandEditing.cs"),
    (Join-Path $projectDir "CommandValidation.cs"),
    (Join-Path $projectDir "ApiKeyStore.cs"),
    (Join-Path $projectDir "RuntimeServices.cs"),
    (Join-Path $projectDir "AdaptiveTuning.cs"),
    (Join-Path $projectDir "SmokeTests.cs")
)

& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw "Test compilation failed: $LASTEXITCODE" }

& (Join-Path $testDir "SmokeTests.exe")
if ($LASTEXITCODE -ne 0) { throw "Offline tests failed: $LASTEXITCODE" }
