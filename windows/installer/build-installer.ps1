# Builds dist\NotificationBridge-Setup-<version>.exe: publishes the Windows app self-contained
# (so the target PC doesn't need the .NET runtime), then compiles the Inno Setup script.
# Requires Inno Setup 6 (ISCC.exe).

$ErrorActionPreference = "Stop"

$installerDir = $PSScriptRoot
$repoRoot = Resolve-Path (Join-Path $installerDir "..\..")
$publishDir = Join-Path $installerDir "publish"

$iscc = @(
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) { throw "Inno Setup 6 not found. Install it (winget install JRSoftware.InnoSetup) and retry." }

if (Test-Path $publishDir) { Remove-Item -LiteralPath $publishDir -Recurse -Force }

dotnet publish (Join-Path $repoRoot "windows\src") -c Release -r win-x64 --self-contained true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

& $iscc "/DPublishDir=$publishDir" (Join-Path $installerDir "NotificationBridge.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed" }

Write-Host "Installer written to $(Join-Path $repoRoot 'dist')"
