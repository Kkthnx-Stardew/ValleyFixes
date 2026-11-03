<#
.SYNOPSIS
Runs a two player scenario: one game process hosts a throwaway farm and a second one joins it as the farmhand. Results land in testprofile\results.

.PARAMETER Label
'baseline' turns every ValleyFixes toggle off, 'defaults' deletes config.json, 'fixed' turns every toggle on. Both players use the same setting.

.PARAMETER Scenario
join, perf, soak, roundtrip or gold. See ModEntry.Multiplayer.cs in the harness.

.PARAMETER Days
Nights for the soak scenario.

.PARAMETER Heavy
Fill the host's farm with a late-game style farm first (soak only).

.PARAMETER OutName
File name stem. The host writes <OutName>-host.json and the client <OutName>-client.json.
#>
param(
    [Parameter(Mandatory)][ValidateSet('baseline', 'defaults', 'fixed')][string] $Label,
    [string] $Scenario = 'join',
    [int] $Days = 14,
    [switch] $Heavy,
    [switch] $HostOnly,
    [switch] $KeepSave,
    [switch] $HeapDumps,
    [switch] $Visit,
    [string] $LoadSave = '',
    [string] $OutName = '',
    [int] $TimeoutSeconds = 900,
    [int] $StallSeconds = 240,
    [string] $GamePath = 'D:\SteamLibrary\steamapps\common\Stardew Valley',
    [ValidateSet('Debug', 'Release')][string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$mods = Join-Path $repo 'testprofile\Mods'
$modsClient = Join-Path $repo 'testprofile\ModsClient'
$results = Join-Path $repo 'testprofile\results'
New-Item -ItemType Directory -Force $results | Out-Null
if (-not $OutName) { $OutName = "mp-$Scenario-$Label" }

& (Join-Path $PSScriptRoot 'Run-CleanProfile.ps1') -NoLaunch -GamePath $GamePath -Configuration $Configuration

dotnet build (Join-Path $repo 'ValleyFixes.DevHarness\ValleyFixes.DevHarness.csproj') -c Debug --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Harness build failed.' }
$harness = Join-Path $mods 'ValleyFixes.DevHarness'
New-Item -ItemType Directory -Force $harness | Out-Null
Copy-Item (Join-Path $repo 'ValleyFixes.DevHarness\bin\Debug\net6.0\ValleyFixes.DevHarness.dll') $harness -Force
Copy-Item (Join-Path $repo 'ValleyFixes.DevHarness\manifest.json') $harness -Force

$configFile = Join-Path $mods 'ValleyFixes\config.json'
if ($Label -eq 'defaults') {
    Remove-Item $configFile -ErrorAction SilentlyContinue
} else {
    $on = $Label -eq 'fixed'
    $config = [ordered]@{ FixFairThirdPlaceText = $on; FixMovieReactionScripts = $on; FixLumberjackMahoganyBonus = $on; FixBlankNames = $on; FixChefBundleReward = $on; FixPortugueseSingleStatus = $on; TrimLocationChangeBacklog = $on; FixQuestItemGroundDrops = $on; FixTitleMusicMute = $on; FixGeorgeWheelchair = $on; FixOfflineFarmhandWallets = $on; CheckStaleOrderParticipants = $on; RepairStaleOrderParticipants = $on; FixTrashBearSardine = $on; SyncCoalCarts = $on; LogEatInterrupts = $on } | ConvertTo-Json
    Set-Content $configFile $config
}

# the joining player gets its own copy of the same Mods folder
if (Test-Path $modsClient) { Remove-Item $modsClient -Recurse -Force }
Copy-Item $mods $modsClient -Recurse

$prefs = Join-Path $env:APPDATA 'StardewValley\startup_preferences'
$prefsBackup = $null
if (Test-Path $prefs) { $prefsBackup = Get-Content $prefs -Raw }

$sync = Join-Path $results "sync-$OutName"
if (Test-Path $sync) { Remove-Item $sync -Recurse -Force }
New-Item -ItemType Directory $sync | Out-Null
Remove-Item (Join-Path $results "$OutName-*.json") -ErrorAction SilentlyContinue

$env:VFH_ONLY = $Scenario
$env:VFH_DAYS = "$Days"
$env:VFH_SYNC = $sync
if ($Visit) { $env:VFH_VISIT = '1' } else { Remove-Item Env:VFH_VISIT -ErrorAction SilentlyContinue }
if ($HeapDumps) { $env:VFH_HOLD = '1' } else { Remove-Item Env:VFH_HOLD -ErrorAction SilentlyContinue }
if ($LoadSave) { $env:VFH_LOAD = $LoadSave } else { Remove-Item Env:VFH_LOAD -ErrorAction SilentlyContinue }
if ($HostOnly) { $env:VFH_HOSTONLY = '1' } else { Remove-Item Env:VFH_HOSTONLY -ErrorAction SilentlyContinue }
if ($Heavy) { $env:VFH_HEAVY = '1' } else { Remove-Item Env:VFH_HEAVY -ErrorAction SilentlyContinue }

$logDir = Join-Path $env:APPDATA 'StardewValley\ErrorLogs'
$env:VFH_ROLE = 'host'
$env:VFH_OUT = Join-Path $results "$OutName-host.json"
$hostProc = Start-Process -FilePath (Join-Path $GamePath 'StardewModdingAPI.exe') -ArgumentList '--no-terminal', '--mods-path', "`"$mods`"" -WorkingDirectory $GamePath -PassThru

# the joiner starts once the host's world exists (the harness writes hostready), so the two logs do not fight over the same file at startup
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while (-not $hostProc.HasExited -and -not (Test-Path (Join-Path $sync 'hostready.host')) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 2 }
$clientProc = $null
if (-not $HostOnly -and (Test-Path (Join-Path $sync 'hostready.host'))) {
    Start-Sleep -Seconds 3
    $hostLogCopy = Join-Path $results "$OutName-host.startup.log"
    $latest = Join-Path $logDir 'SMAPI-latest.txt'
    if (Test-Path $latest) { Copy-Item $latest $hostLogCopy -Force }
    $env:VFH_ROLE = 'client'
    $env:VFH_OUT = Join-Path $results "$OutName-client.json"
    $clientProc = Start-Process -FilePath (Join-Path $GamePath 'StardewModdingAPI.exe') -ArgumentList '--no-terminal', '--mods-path', "`"$modsClient`"" -WorkingDirectory $GamePath -PassThru
}
Remove-Item Env:VFH_ROLE, Env:VFH_OUT -ErrorAction SilentlyContinue

# a run that stops making progress is stopped early: progress means a new file in the sync folder, or a growing result or log file
$lastProgress = Get-Date
$lastStamp = ''
$stalled = $false
while ((Get-Date) -lt $deadline) {
    $stamp = (Get-ChildItem $sync -File -ErrorAction SilentlyContinue | Measure-Object).Count.ToString() + '/' + ((Get-ChildItem $logDir -File -Filter 'SMAPI-latest*.txt' -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum)
    if ($stamp -ne $lastStamp) { $lastStamp = $stamp; $lastProgress = Get-Date }
    elseif (((Get-Date) - $lastProgress).TotalSeconds -gt $StallSeconds) { $stalled = $true; Write-Warning "STALLED: no progress for $StallSeconds seconds, stopping both games."; break }
    $hostDone = $hostProc.HasExited
    $clientDone = ($null -eq $clientProc) -or $clientProc.HasExited
    if ($hostDone -and $clientDone) { break }
    if ($HeapDumps) {
        foreach ($pair in @(@('host', $hostProc), @('client', $clientProc))) {
            $role = $pair[0]; $proc = $pair[1]
            if ($null -eq $proc -or $proc.HasExited) { continue }
            $jsonOut = Join-Path $results "$OutName-$role.json"
            foreach ($hold in Get-ChildItem "$jsonOut.hold-*" -ErrorAction SilentlyContinue) {
                $name = $hold.Name.Substring((Split-Path $jsonOut -Leaf).Length + '.hold-'.Length)
                $dump = Join-Path $results "$OutName-$role-$name.gcdump"
                Write-Host "Heap dump of $role at '$name'"
                dotnet-gcdump collect -p $proc.Id -o $dump 2>&1 | Select-Object -Last 1
                Remove-Item $hold.FullName
                Set-Content "$jsonOut.go-$name" 'go'
            }
        }
    }
    Start-Sleep -Seconds 2
}
foreach ($proc in @($hostProc, $clientProc)) {
    if ($proc -and -not $proc.HasExited) { Write-Warning "Timed out after $TimeoutSeconds seconds, stopping process $($proc.Id)."; Stop-Process -Id $proc.Id -Force }
}
Remove-Item Env:VFH_HOLD, Env:VFH_VISIT -ErrorAction SilentlyContinue
Remove-Item Env:VFH_ONLY, Env:VFH_DAYS, Env:VFH_SYNC, Env:VFH_HEAVY, Env:VFH_HOSTONLY, Env:VFH_LOAD -ErrorAction SilentlyContinue

# the throwaway farm and its farmhand are written to the shared Saves folder, so remove only the VFTest_* folders
$saves = Join-Path $env:APPDATA 'StardewValley\Saves'
if ($KeepSave) { Get-ChildItem $saves -Directory -Filter 'VFTest_*' | ForEach-Object { Write-Host "Kept save: $($_.Name)" } } else { Get-ChildItem $saves -Directory -Filter 'VFTest_*' -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force }
if ($null -ne $prefsBackup -and (Get-Content $prefs -Raw) -ne $prefsBackup) { Set-Content $prefs $prefsBackup -NoNewline }

# the host writes SMAPI-latest.txt and the second process SMAPI-latest.player-2.txt
foreach ($f in Get-ChildItem $logDir -File -Filter 'SMAPI-latest*.txt') {
    Copy-Item $f.FullName (Join-Path $results "$OutName-$($f.Name)") -Force
}
Get-ChildItem $logDir -File | Sort-Object LastWriteTime -Descending | Select-Object -First 4 | ForEach-Object { Write-Host "log file: $($_.Name) $($_.Length) bytes $($_.LastWriteTime)" }
foreach ($role in 'host', 'client') {
    if ($HostOnly -and $role -eq 'client') { continue }
    $f = Join-Path $results "$OutName-$role.json"
    if (Test-Path $f) { Write-Host "Results: $f" } else { Write-Warning "No $role results file." }
}
