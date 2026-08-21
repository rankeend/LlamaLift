param([string]$ExpectedPackageVersion = "")

$ErrorActionPreference = "Stop"
$projectDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$installerPath = Join-Path $projectDir "installer\LlamaLift.iss"
$modelsPath = Join-Path $projectDir "Models.cs"
$programPath = Join-Path $projectDir "Program.cs"
$buildPath = Join-Path $projectDir "build.ps1"

function Assert-Match([string]$Text, [string]$Pattern, [string]$Message) {
    if ($Text -notmatch $Pattern) { throw "Installer upgrade contract failed: $Message" }
}

function Assert-NotMatch([string]$Text, [string]$Pattern, [string]$Message) {
    if ($Text -match $Pattern) { throw "Installer upgrade contract failed: $Message" }
}

$installer = Get-Content -LiteralPath $installerPath -Raw
$models = Get-Content -LiteralPath $modelsPath -Raw
$program = Get-Content -LiteralPath $programPath -Raw
$build = Get-Content -LiteralPath $buildPath -Raw

Assert-Match $installer '(?m)^#define MyUpgradeAppId "\{\{BDE1C8B1-4E9B-4F54-B2A7-7B82B7DF42A0\}"\s*$' 'The permanent AppId changed.'
Assert-Match $installer '(?m)^AppId=\{#MyUpgradeAppId\}\s*$' 'Setup does not use the permanent AppId.'
Assert-Match $installer '(?m)^UsePreviousAppDir=yes\s*$' 'Setup must reuse the previous application directory.'
Assert-Match $installer '(?m)^UsePreviousGroup=yes\s*$' 'Setup must reuse the previous Start menu group.'
Assert-Match $installer '(?m)^UsePreviousTasks=yes\s*$' 'Setup must reuse the previous task selections.'
Assert-Match $installer '(?m)^CloseApplications=yes\s*$' 'Setup must safely close LlamaLift before replacing files.'
Assert-Match $installer '(?m)^CloseApplicationsFilter=\{#MyAppExeName\}\s*$' 'CloseApplicationsFilter must only target LlamaLift.exe.'
Assert-Match $installer '(?m)^RestartApplications=no\s*$' 'The finish page must be the single application restart path.'
Assert-NotMatch $installer '(?im)^\s*\[(InstallDelete|UninstallDelete)\]\s*$' 'Setup must not contain destructive delete sections.'
Assert-NotMatch $installer '(?i)\{localappdata\}|\{userappdata\}' 'Setup must not write to or delete the user data directory.'

Assert-Match $models 'File\.Exists\(Path\.Combine\(AppDomain\.CurrentDomain\.BaseDirectory, "portable\.flag"\)\)' 'Portable mode detection is missing.'
Assert-Match $models 'Environment\.SpecialFolder\.LocalApplicationData\), "LlamaLift"' 'Installed data must remain in %LOCALAPPDATA%\LlamaLift.'
Assert-Match $build 'Remove-Item -LiteralPath \(Join-Path \$installerDistDir "portable\.flag"\) -Force' 'The installer payload must exclude portable.flag.'

$installerVersion = [regex]::Match($installer, '(?m)^#define MyAppVersion "([^"]+)"').Groups[1].Value
$channel = [regex]::Match($installer, '(?m)^#define MyAppChannel "([^"]+)"').Groups[1].Value
$productVersion = [regex]::Match($models, 'ProductVersion = "([^"]+)"').Groups[1].Value
$displayVersion = [regex]::Match($models, 'DisplayVersion = "v([^"]+)"').Groups[1].Value
$assemblyVersion = [regex]::Match($program, 'AssemblyFileVersion\("([0-9]+\.[0-9]+\.[0-9]+)\.0"\)').Groups[1].Value
$buildVersion = [regex]::Match($build, '\$version = "([^"]+)"').Groups[1].Value
$packageVersion = $installerVersion + "-" + $channel

if ([string]::IsNullOrWhiteSpace($installerVersion) -or [string]::IsNullOrWhiteSpace($channel)) {
    throw "Installer upgrade contract failed: Setup version could not be read."
}
if ($productVersion -ne $installerVersion -or $assemblyVersion -ne $installerVersion) {
    throw "Installer upgrade contract failed: Product, assembly, and Setup versions differ."
}
if ($displayVersion -ne $packageVersion -or $buildVersion -ne $packageVersion) {
    throw "Installer upgrade contract failed: Display, build, and Setup channel versions differ."
}
if (-not [string]::IsNullOrWhiteSpace($ExpectedPackageVersion) -and $packageVersion -ne $ExpectedPackageVersion) {
    throw "Installer upgrade contract failed: expected $ExpectedPackageVersion, found $packageVersion."
}

Write-Host "[PASS] Setup keeps the permanent AppId and can replace older versions."
Write-Host "[PASS] Installed user data is isolated from the application directory."
Write-Host "[PASS] Product, build, and Setup versions agree: $packageVersion"
Write-Host "INSTALLER UPGRADE CONTRACT PASSED" -ForegroundColor Green
