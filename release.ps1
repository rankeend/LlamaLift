param(
    [switch]$SkipOfflineTests,
    [switch]$SkipUiTests
)

$ErrorActionPreference = "Stop"
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$modelsPath = Join-Path $projectDir "Models.cs"
$models = Get-Content -LiteralPath $modelsPath -Raw
$packageVersion = [regex]::Match($models, 'DisplayVersion = "v([^"]+)"').Groups[1].Value
if ([string]::IsNullOrWhiteSpace($packageVersion)) { throw "Cannot read the release version from Models.cs." }

$verifyScript = Join-Path $projectDir "installer\verify-upgrade-contract.ps1"
& $verifyScript -ExpectedPackageVersion $packageVersion

if (-not $SkipOfflineTests) { & (Join-Path $projectDir "test.ps1") }
if (-not $SkipUiTests) { & (Join-Path $projectDir "ui-test.ps1") }
& (Join-Path $projectDir "build.ps1")
& $verifyScript -ExpectedPackageVersion $packageVersion

$installerPayload = Join-Path $projectDir "dist-installer"
$portableFlag = Join-Path $installerPayload "portable.flag"
if (Test-Path -LiteralPath $portableFlag) { throw "The installer payload must not contain portable.flag." }

$innoCandidates = @(
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
)
$innoCompiler = $innoCandidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($innoCompiler)) { throw "Inno Setup 6 compiler was not found." }

& $innoCompiler (Join-Path $projectDir "installer\LlamaLift.iss")
if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed with exit code $LASTEXITCODE." }

$releaseDir = Join-Path $projectDir "release"
$portablePackage = Join-Path $releaseDir ("LlamaLift-v" + $packageVersion + "-portable-win-x64.zip")
$setupPackage = Join-Path $releaseDir ("LlamaLift-v" + $packageVersion + "-Setup.exe")
$checksumPath = Join-Path $releaseDir ("LlamaLift-v" + $packageVersion + "-SHA256SUMS.txt")
foreach ($required in @($portablePackage, $setupPackage)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Missing release asset: $required" }
    if ((Get-Item -LiteralPath $required).Length -le 0) { throw "Release asset is empty: $required" }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($portablePackage)
try {
    $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    foreach ($requiredEntry in @("LlamaLift.exe", "AntdUI.dll", "portable.flag", "README.txt", "AUTHORS.md", "THIRD-PARTY-NOTICES.txt")) {
        if ($requiredEntry -notin $entries) { throw "Portable package is missing: $requiredEntry" }
    }
    if (@($entries | Where-Object { $_ -like "data/*" }).Count -gt 0) {
        throw "Portable package must not contain build-machine user data."
    }
}
finally { $archive.Dispose() }

$installedExe = Join-Path $installerPayload "LlamaLift.exe"
$fileVersion = (Get-Item -LiteralPath $installedExe).VersionInfo.FileVersion
if ($fileVersion -ne ($packageVersion.Replace("-preview", ".0"))) {
    throw "LlamaLift.exe file version is incorrect: $fileVersion"
}

$hashLines = foreach ($asset in @($setupPackage, $portablePackage)) {
    $hash = (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant()
    $hash + "  " + [System.IO.Path]::GetFileName($asset)
}
Set-Content -LiteralPath $checksumPath -Value $hashLines -Encoding ascii

Write-Host ""
Write-Host "Release package succeeded: v$packageVersion" -ForegroundColor Green
Get-Item -LiteralPath @($setupPackage, $portablePackage, $checksumPath) |
    Select-Object Name, Length, LastWriteTime
