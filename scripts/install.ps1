<#
.SYNOPSIS
    Builds and installs BlinkCameraServiceHandler (the BlinkCameraCall console app).

.DESCRIPTION
    Publishes a framework-dependent Release build of BlinkCameraCall to -InstallPath,
    adds a short "blink.cmd" wrapper next to it, and (unless -NoPath is given) adds
    that folder to the current user's PATH so "blink" works from any terminal.

    Does NOT touch settings.json if one already exists in -InstallPath - your saved
    credentials and HardwareId (see docs/USER_GUIDE.md) are left alone. The app
    creates settings.json itself on first run if none exists yet.

.PARAMETER InstallPath
    Where to publish the app. Defaults to "$env:LOCALAPPDATA\BlinkCameraServiceHandler".

.PARAMETER Configuration
    Build configuration to publish. Defaults to "Release".

.PARAMETER NoPath
    Skip adding -InstallPath to the user PATH environment variable.

.EXAMPLE
    .\scripts\install.ps1

.EXAMPLE
    .\scripts\install.ps1 -InstallPath "D:\Tools\BlinkCameraServiceHandler" -NoPath
#>
[CmdletBinding()]
param(
    [string]$InstallPath = (Join-Path $env:LOCALAPPDATA "BlinkCameraServiceHandler"),
    [string]$Configuration = "Release",
    [switch]$NoPath
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "BlinkCameraCall\BlinkCameraCall.csproj"

if (-not (Test-Path $projectPath)) {
    throw "Could not find BlinkCameraCall.csproj at '$projectPath'. Run this script from inside the repository (scripts\install.ps1)."
}

try {
    dotnet --version | Out-Null
}
catch {
    throw "The .NET SDK does not appear to be installed or on PATH. Install the .NET 8 SDK before running this script."
}

Write-Host "Publishing BlinkCameraCall ($Configuration) to '$InstallPath' ..."
New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null

$settingsBackup = Join-Path $InstallPath "settings.json"
$hadExistingSettings = Test-Path $settingsBackup

dotnet publish $projectPath `
    -c $Configuration `
    -o $InstallPath `
    --self-contained false `
    --nologo

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

if ($hadExistingSettings -and -not (Test-Path $settingsBackup)) {
    Write-Warning "settings.json existed before publish but is missing afterwards - this should not happen; check '$InstallPath' manually."
}
elseif ($hadExistingSettings) {
    Write-Host "Existing settings.json in '$InstallPath' was left untouched."
}

$wrapperPath = Join-Path $InstallPath "blink.cmd"
@"
@echo off
"%~dp0BlinkCameraCall.exe" %*
"@ | Set-Content -Encoding ASCII -Path $wrapperPath

Write-Host "Created wrapper: $wrapperPath"

if (-not $NoPath) {
    $userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
    $pathEntries = @()
    if ($userPath) {
        $pathEntries = $userPath -split ";" | Where-Object { $_ -ne "" }
    }

    $alreadyOnPath = $pathEntries | Where-Object {
        try { (Resolve-Path $_ -ErrorAction Stop).Path -eq (Resolve-Path $InstallPath).Path }
        catch { $false }
    }

    if ($alreadyOnPath) {
        Write-Host "'$InstallPath' is already on your user PATH."
    }
    else {
        $newPath = if ($userPath) { "$userPath;$InstallPath" } else { $InstallPath }
        [Environment]::SetEnvironmentVariable("PATH", $newPath, "User")
        Write-Host "Added '$InstallPath' to your user PATH."
        Write-Host "Open a NEW terminal window for the PATH change to take effect."
    }
}
else {
    Write-Host "Skipped PATH update (-NoPath). Run the app directly from '$InstallPath'."
}

Write-Host ""
Write-Host "Install complete."
Write-Host "Run it with:  blink          (new terminal, if added to PATH)"
Write-Host "          or: $InstallPath\BlinkCameraCall.exe"
Write-Host ""
Write-Host "First run creates settings.json in that folder - see docs\USER_GUIDE.md to configure your Blink login."
