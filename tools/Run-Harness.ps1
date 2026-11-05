<#
.SYNOPSIS
Runs the in-game ValleyFixes scenarios once and saves the results and SMAPI log under testprofile\results.

.PARAMETER Label
'baseline' turns every ValleyFixes toggle off. 'defaults' deletes config.json so the shipped defaults apply. 'fixed' turns every toggle on, including the opt-in ones.

.PARAMETER Only
'natural' runs real overnight transitions for -Days nights. 'skull' samples Skull Cavern floors with monsters.

.PARAMETER Heavy
With -Only natural, fills the farm like a late-game one before the first night.

.PARAMETER OutName
File name stem for the results and log. Defaults to the label.

.PARAMETER TimeoutSeconds
How long to wait before the game is stopped.
#>
param(
    [Parameter(Mandatory)][ValidateSet('baseline', 'defaults', 'fixed')][string] $Label,
    [int] $TimeoutSeconds = 240,
    [switch] $HeapDumps,
    [string] $Only = '',
    [int] $Days = 10,
    [switch] $Heavy,
    [switch] $Profile,
    [string] $OutName = '',
    [string] $ModsName = 'Mods',
    [string] $LoadSave = '',
    [string[]] $Enable = @(),
    [string] $GamePath = 'D:\SteamLibrary\steamapps\common\Stardew Valley',
    [ValidateSet('Debug', 'Release')][string] $Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$mods = Join-Path $repo (Join-Path 'testprofile' $ModsName)
$results = Join-Path $repo 'testprofile\results'
New-Item -ItemType Directory -Force $results | Out-Null

& (Join-Path $PSScriptRoot 'Run-CleanProfile.ps1') -NoLaunch -GamePath $GamePath -Configuration $Configuration -ModsName $ModsName

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
    $config = [ordered]@{ FixFairThirdPlaceText = $on; FixMovieReactionScripts = $on; FixLumberjackMahoganyBonus = $on; FixBlankNames = $on; FixChefBundleReward = $on; FixPortugueseSingleStatus = $on; TrimLocationChangeBacklog = $on; FixQuestItemGroundDrops = $on; FixTitleMusicMute = $on; FixGeorgeWheelchair = $on; FixOfflineFarmhandWallets = $on; CheckStaleOrderParticipants = $on; RepairStaleOrderParticipants = $on; FixTrashBearSardine = $on; SyncCoalCarts = $on; FixStardropInterrupt = $on; FixStaleMapOverrides = $on; CacheAssetNames = $on; SkipEmptyLocationUpdates = $on; PauseIdleLocationMonsters = $on; RetryLostJoinRequests = $on; LogEatInterrupts = $on } | ConvertTo-Json
    Set-Content $configFile $config
    if ($Enable.Count -gt 0) {
        # only the listed fixes are on, so one change can be measured by itself
        $c = Get-Content $configFile -Raw | ConvertFrom-Json
        foreach ($p in $c.PSObject.Properties.Name) { $c.$p = $false }
        foreach ($e in ($Enable | ForEach-Object { $_ -split "," })) { $c.($e.Trim()) = $true }
        Set-Content $configFile ($c | ConvertTo-Json)
    }
}

if (-not $OutName) { $OutName = $Label }
$out = Join-Path $results "$OutName.json"
Remove-Item $out -ErrorAction SilentlyContinue
$env:VFH_OUT = $out

$prefs = Join-Path $env:APPDATA 'StardewValley\startup_preferences'
function Get-Lang { if (Test-Path $prefs) { $m = [regex]::Match((Get-Content $prefs -Raw), '<languageCode>([^<]*)</languageCode>'); if ($m.Success) { $m.Groups[1].Value } else { '(not set)' } } }
$langBefore = Get-Lang
# the title screen scenario clicks the real mute button, which writes this file, so the original is put back afterwards
$prefsBackup = $null
if (Test-Path $prefs) { $prefsBackup = Get-Content $prefs -Raw }

if ($HeapDumps) { $env:VFH_HOLD = '1' }
if ($Profile) { $env:VFH_PROFILE = "1" } else { Remove-Item Env:VFH_PROFILE -ErrorAction SilentlyContinue }
if ($Heavy) { $env:VFH_HEAVY = '1' } else { Remove-Item Env:VFH_HEAVY -ErrorAction SilentlyContinue }
if ($LoadSave) { $env:VFH_LOAD = $LoadSave } else { Remove-Item Env:VFH_LOAD -ErrorAction SilentlyContinue }
if ($Only) { $env:VFH_ONLY = $Only; $env:VFH_DAYS = "$Days" } else { Remove-Item Env:VFH_ONLY, Env:VFH_DAYS -ErrorAction SilentlyContinue }
$proc = Start-Process -FilePath (Join-Path $GamePath 'StardewModdingAPI.exe') -ArgumentList '--no-terminal', '--mods-path', "`"$mods`"" -WorkingDirectory $GamePath -PassThru
# with -HeapDumps a heap dump is taken whenever the harness announces a hold point, then it continues
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while (-not $proc.HasExited -and (Get-Date) -lt $deadline) {
    if ($HeapDumps) {
        foreach ($hold in Get-ChildItem "$out.hold-*" -ErrorAction SilentlyContinue) {
            $name = $hold.Name.Substring((Split-Path $out -Leaf).Length + '.hold-'.Length)
            $dump = Join-Path $results "$OutName-$name.gcdump"
            Write-Host "Heap dump at '$name'"
            dotnet-gcdump collect -p $proc.Id -o $dump 2>&1 | Select-Object -Last 1
            Remove-Item $hold.FullName
            Set-Content "$out.go-$name" 'go'
        }
    }
    Start-Sleep -Milliseconds 500
}
if (-not $proc.HasExited) {
    Write-Warning "Timed out after $TimeoutSeconds seconds, stopping the game."
    Stop-Process -Id $proc.Id -Force
}
Remove-Item "$out.go-*", "$out.hold-*" -ErrorAction SilentlyContinue
Remove-Item Env:VFH_HOLD -ErrorAction SilentlyContinue

# the throwaway farm is written to the shared Saves folder when the game quits, so remove only the VFTest_* folders it made
$saves = Join-Path $env:APPDATA 'StardewValley\Saves'
Get-ChildItem $saves -Directory -Filter 'VFTest_*' -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

if ($null -ne $prefsBackup -and (Get-Content $prefs -Raw) -ne $prefsBackup) { Write-Host 'startup_preferences differs from before the run (timesPlayed counts up every launch). Restoring the original.'; Set-Content $prefs $prefsBackup -NoNewline }
$langAfter = Get-Lang
if ($langBefore -ne $langAfter) { Write-Warning "Saved game language changed from '$langBefore' to '$langAfter'. Check startup_preferences." } else { Write-Host "Saved game language unchanged: '$langAfter'" }

$log = Join-Path $env:APPDATA 'StardewValley\ErrorLogs\SMAPI-latest.txt'
if (Test-Path $log) { Copy-Item $log (Join-Path $results "$OutName.log") -Force }
if (Test-Path $out) { Write-Host "Results: $out" } else { Write-Warning 'No results file was written.' }
