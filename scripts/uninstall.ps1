<#
.SYNOPSIS
    Uninstalls BlinkCameraServiceHandler (reverses scripts\install.ps1).

.DESCRIPTION
    Removes -InstallPath from the current user's PATH, then removes the installed
    files. By default config.json (which holds your Blink email/password and
    device id) is kept on disk even though everything else is removed - pass
    -RemoveSettings to delete it too.

.PARAMETER InstallPath
    Where the app was published. Defaults to "$env:LOCALAPPDATA\BlinkCameraServiceHandler"
    (must match what was passed to install.ps1, if anything non-default was used).

.PARAMETER RemoveSettings
    Also delete config.json (and therefore your saved Blink credentials and
    HardwareId) and the now-empty install folder. Without this switch, config.json
    is left behind and the rest of the folder's contents are removed around it.

.PARAMETER Force
    Skip the confirmation prompt that is otherwise shown when config.json is about
    to be deleted.

.EXAMPLE
    .\scripts\uninstall.ps1

.EXAMPLE
    .\scripts\uninstall.ps1 -RemoveSettings -Force
#>
[CmdletBinding()]
param(
    [string]$InstallPath = (Join-Path $env:LOCALAPPDATA "BlinkCameraServiceHandler"),
    [switch]$RemoveSettings,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
if ($userPath) {
    $resolvedInstall = $null
    try { $resolvedInstall = (Resolve-Path $InstallPath -ErrorAction Stop).Path } catch {}

    $pathEntries = $userPath -split ";" | Where-Object { $_ -ne "" }
    $kept = $pathEntries | Where-Object {
        $entry = $_
        $matches = $false
        try { $matches = ((Resolve-Path $entry -ErrorAction Stop).Path -eq $resolvedInstall) } catch {}
        -not $matches
    }

    if ($kept.Count -ne $pathEntries.Count) {
        [Environment]::SetEnvironmentVariable("PATH", ($kept -join ";"), "User")
        Write-Host "Removed '$InstallPath' from your user PATH."
    }
}

if (-not (Test-Path $InstallPath)) {
    Write-Host "'$InstallPath' does not exist; nothing to remove."
    exit 0
}

$settingsPath = Join-Path $InstallPath "config.json"
$hasSettings = Test-Path $settingsPath

if ($hasSettings -and -not $RemoveSettings) {
    Get-ChildItem -Path $InstallPath -Force | Where-Object { $_.FullName -ne $settingsPath } | ForEach-Object {
        Remove-Item -Recurse -Force $_.FullName
    }
    Write-Host "Removed the installed app, but kept '$settingsPath' (it holds your Blink credentials)."
    Write-Host "Re-run with -RemoveSettings if you want that deleted too."
    exit 0
}

if ($hasSettings -and -not $Force) {
    $answer = Read-Host "This will also delete '$settingsPath' (your saved Blink credentials). Continue? [y/N]"
    if ($answer -notmatch '^[Yy]') {
        Write-Host "Cancelled. Nothing was removed."
        exit 1
    }
}

Remove-Item -Recurse -Force $InstallPath
Write-Host "Removed '$InstallPath'."
