<#
.SYNOPSIS
Prints one row per nettune run: how far behind and how jerky the other side sees a moving player or monster, and what each side sent.

.PARAMETER Pattern
File name stem pattern of the runs, for example nt-*.
#>
param([string] $Pattern = 'nt-*')

$results = Join-Path (Split-Path -Parent $PSScriptRoot) 'testprofile\results'
$rows = foreach ($client in Get-ChildItem $results -Filter "$Pattern-client.json") {
    $stem = $client.Name -replace '-client\.json$', ''
    $hostFile = Join-Path $results "$stem-host.json"
    if (-not (Test-Path $hostFile)) { continue }
    $c = (Get-Content $client.FullName -Raw | ConvertFrom-Json).nettune
    $h = (Get-Content $hostFile -Raw | ConvertFrom-Json).nettune
    $hp = $c.host_player_watcher
    $hm = $c.host_monster_watcher
    $cp = $h.client_player_watcher
    $sent = 0
    foreach ($phase in "host_player", "host_monster", "client_player") { foreach ($k in $h.$phase.net.PSObject.Properties) { $sent += [double]$k.Value.totalKB * 1024 } }
    [pscustomobject]@{
        Run = $stem
        Cfg = "$($c.broadcastPeriod)/$($c.interpolationTicks)"
        HostPlayerLag = "$($hp.meanLagMs) +-$($hp.spreadMs) still $($hp.stillTickPercent)%"
        MonsterLag = "$($hm.meanLagMs) +-$($hm.spreadMs) still $($hm.stillTickPercent)%"
        ClientPlayerLag = "$($cp.meanLagMs) +-$($cp.spreadMs) still $($cp.stillTickPercent)%"
        HostSentKB = [math]::Round($sent / 1024, 0)
    }
}
$rows | Sort-Object Run | Format-Table -AutoSize
