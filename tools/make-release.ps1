# Builds a release zip: release\SpireMeter-<version>.zip with the mod folder, the installer and a short readme.
# Runs the core checks and a Release build first, and refuses to package if the two version numbers disagree.
# Usage: powershell -ExecutionPolicy Bypass -File tools/make-release.ps1 [-GameDir "<game folder>"]
param([string]$GameDir)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = 'C:\Program Files\dotnet\dotnet.exe' }

$manifest = Join-Path $root 'src\SpireMeter.json'
$version = (Get-Content $manifest -Raw | ConvertFrom-Json).version
$code = Get-Content (Join-Path $root 'src\SpireMeter\ModEntry.cs') -Raw
if ($code -notmatch "Version = `"$([regex]::Escape($version))`"") {
    throw "SpireMeter.json says $version but ModEntry.Version differs. Bump both."
}

Write-Host "Checking core..."
$checks = & $dotnet run --project (Join-Path $root 'tests\SpireMeter.Core.Tests') 2>&1
if ($LASTEXITCODE -ne 0 -or ($checks -match '^FAIL')) {
    $checks | Write-Host
    throw 'Core checks failed.'
}

Write-Host "Building $version..."
$buildArgs = @('build', (Join-Path $root 'src\SpireMeter.csproj'), '-c', 'Release', '-nologo', '-v', 'q', '-clp:ErrorsOnly')
if ($GameDir) { $buildArgs += "-p:GameDir=$GameDir" }
& $dotnet @buildArgs
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$stage = Join-Path $root 'release\stage'
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
$mod = Join-Path $stage 'SpireMeter'
New-Item -ItemType Directory -Force -Path $mod | Out-Null
Copy-Item (Join-Path $root 'src\bin\Release\net9.0\SpireMeter.dll') $mod
Copy-Item $manifest $mod
Copy-Item (Join-Path $root 'tools\installer\*') $stage

# dist\SpireMeter mirrors what gets installed, for local testing.
$dist = Join-Path $root 'dist\SpireMeter'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
Copy-Item (Join-Path $mod '*') $dist -Force

$zip = Join-Path $root "release\SpireMeter-$version.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Remove-Item -Recurse -Force $stage
Write-Host "Done: $zip"
