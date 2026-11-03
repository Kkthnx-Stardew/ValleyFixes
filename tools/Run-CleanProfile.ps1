<#
.SYNOPSIS
Builds ValleyFixes and starts SMAPI with a Mods folder that holds only ValleyFixes and SMAPI's own ConsoleCommands.

.DESCRIPTION
The live Mods folder is never read or written. Saves cannot be redirected: the game always uses %APPDATA%\StardewValley\Saves,
so use a new farm name for tests and do not load an existing farm.

.PARAMETER NoLaunch
Build and copy only, do not start the game.

.PARAMETER GamePath
The Stardew Valley folder. Defaults to the Steam library on D:.
#>
param(
    [switch] $NoLaunch,
    [string] $ModsName = 'Mods',
    [string] $GamePath = 'D:\SteamLibrary\steamapps\common\Stardew Valley',
    [ValidateSet('Debug', 'Release')][string] $Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$mods = Join-Path $repo ("testprofile\" + $ModsName)
$target = Join-Path $mods 'ValleyFixes'
$built = Join-Path $repo "ValleyFixes\bin\$Configuration\net6.0"

dotnet build (Join-Path $repo 'ValleyFixes\ValleyFixes.csproj') -c $Configuration --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

New-Item -ItemType Directory -Force $target | Out-Null
# config.json is left alone so a toggle you changed for a test survives a rebuild
Copy-Item (Join-Path $built 'ValleyFixes.dll') $target -Force
Copy-Item (Join-Path $repo 'ValleyFixes\manifest.json') $target -Force

$console = Join-Path $mods 'ConsoleCommands'
if (-not (Test-Path $console)) {
    Copy-Item (Join-Path $GamePath 'Mods\ConsoleCommands') $console -Recurse
}

Write-Host "Clean Mods folder: $mods"
Get-ChildItem $mods -Directory | ForEach-Object { Write-Host "  $($_.Name)" }

if ($NoLaunch) { return }

Start-Process -FilePath (Join-Path $GamePath 'StardewModdingAPI.exe') -ArgumentList '--mods-path', "`"$mods`"" -WorkingDirectory $GamePath
Write-Host 'Started. The log is %APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt'
