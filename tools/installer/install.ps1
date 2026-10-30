# Installs (or updates) SpireMeter into Slay the Spire 2's mods folder.
# Run it through install.cmd next to it, or: powershell -ExecutionPolicy Bypass -File install.ps1 [-GameDir "<game folder>"]
param([string]$GameDir)

$ErrorActionPreference = 'Stop'
$GameName = 'Slay the Spire 2'

function Finish([string]$message, [int]$code) {
    Write-Host ''
    Write-Host $message
    Write-Host ''
    Read-Host 'Press Enter to close'
    exit $code
}

# Every Steam library listed in libraryfolders.vdf, starting with Steam's own folder.
function Find-GameDir {
    $steam = $null
    foreach ($key in 'HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam') {
        try {
            $item = Get-ItemProperty -Path $key -ErrorAction Stop
            if ($item.SteamPath) { $steam = $item.SteamPath; break }
            if ($item.InstallPath) { $steam = $item.InstallPath; break }
        } catch { }
    }
    if (-not $steam) { return $null }
    $libraries = @($steam)
    $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
    if (Test-Path $vdf) {
        foreach ($match in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"')) {
            $libraries += $match.Groups[1].Value -replace '\\\\', '\'
        }
    }
    foreach ($library in $libraries) {
        $candidate = Join-Path $library "steamapps\common\$GameName"
        if (Test-Path $candidate) { return $candidate }
    }
    return $null
}

$source = Join-Path $PSScriptRoot 'SpireMeter'
if (-not (Test-Path (Join-Path $source 'SpireMeter.dll'))) {
    Finish 'The SpireMeter folder is missing next to this script. Unzip the whole download first, then run install.cmd again.' 1
}
$version = (Get-Content (Join-Path $source 'SpireMeter.json') -Raw | ConvertFrom-Json).version
Write-Host "SpireMeter $version installer"
Write-Host ''

if (-not $GameDir) { $GameDir = Find-GameDir }
if (-not $GameDir -or -not (Test-Path $GameDir)) {
    Write-Host "Could not find $GameName through Steam."
    Write-Host 'In Steam: right-click the game > Manage > Browse local files, then copy that folder path.'
    $GameDir = (Read-Host 'Paste the game folder here').Trim('"', ' ')
}
if (-not (Test-Path (Join-Path $GameDir 'data_sts2_windows_x86_64'))) {
    Finish "That does not look like the $GameName folder: $GameDir" 1
}
Write-Host "Game folder: $GameDir"

if (Get-Process -Name 'SlayTheSpire2' -ErrorAction SilentlyContinue) {
    Finish "$GameName is running. Close the game, then run install.cmd again." 1
}

$mods = Join-Path $GameDir 'mods'
New-Item -ItemType Directory -Force -Path $mods | Out-Null

# The old name of this mod: having both would count everything twice.
$old = Join-Path $mods 'VibeNight'
if (Test-Path $old) {
    Write-Host ''
    Write-Host 'Found the old VibeNight mod. It is replaced by SpireMeter and would count everything twice.'
    $answer = Read-Host 'Remove it? (Y/n)'
    if ($answer -eq '' -or $answer -match '^[Yy]') {
        Remove-Item -Recurse -Force $old
        Write-Host 'Removed VibeNight.'
    } else {
        Write-Host 'Kept VibeNight. Remove mods\VibeNight yourself before playing.'
    }
}

$target = Join-Path $mods 'SpireMeter'
$previous = $null
$manifest = Join-Path $target 'SpireMeter.json'
if (Test-Path $manifest) {
    try { $previous = (Get-Content $manifest -Raw | ConvertFrom-Json).version } catch { }
}
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force

if ($previous) {
    Finish "Updated SpireMeter $previous to $version. Start the game and have fun." 0
}
Finish "Installed SpireMeter $version. Start the game; if it asks whether to load mods, say yes." 0
