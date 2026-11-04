<#
.SYNOPSIS
Prints a compact table of a realnet multiplayer run: per phase and per player, bytes written, biggest message types, mod messages, frame cost and frame gaps.

.PARAMETER Stem
The OutName used for the run, for example mp-realnet-baseline.
#>
param([Parameter(Mandatory)][string] $Stem)

$results = Join-Path (Split-Path -Parent $PSScriptRoot) 'testprofile\results'

function Show-Net($label, $net) {
    $total = 0.0; $msgs = 0
    foreach ($p in $net.PSObject.Properties) { $total += $p.Value.totalKB; $msgs += $p.Value.messages }
    $top = ($net.PSObject.Properties | Select-Object -First 4 | ForEach-Object { '{0}={1}KB/{2}msg/mean{3}B/max{4}KB' -f $_.Name, $_.Value.totalKB, $_.Value.messages, $_.Value.meanBytes, $_.Value.maxKB }) -join '  '
    Write-Host ("{0,-22} sent {1,9:N1} KB in {2,6} msgs   {3}" -f $label, $total, $msgs, $top)
}

function Show-ModNet($rows) {
    if (-not $rows) { return }
    $top = ($rows | Select-Object -First 5 | ForEach-Object { '{0}={1}KB/{2}msg/mean{3}B' -f $_.message, $_.totalKB, $_.messages, $_.meanBytes }) -join '  '
    Write-Host ("{0,-22} mod messages sent:     {1}" -f '', $top)
}

function Show-Mod($rows) {
    if (-not $rows) { return }
    $top = ($rows | Select-Object -First 3 | ForEach-Object { '{0}={1}KB/{2}msg/mean{3}ch' -f $_.message, $_.totalKB, $_.count, $_.meanChars }) -join '  '
    Write-Host ("{0,-22} mod messages received: {1}" -f '', $top)
}

foreach ($role in 'host', 'client') {
    $file = Join-Path $results "$Stem-$role.json"
    if (-not (Test-Path $file)) { Write-Host "No $role file"; continue }
    $j = Get-Content $file -Raw | ConvertFrom-Json
    Write-Host "==== $Stem $role  note=$($j.note)  world=$($j.world)  join_to_ready_ms=$($j.join_to_ready_ms)"
    if ($j.harness_error) { Write-Host "HARNESS ERROR: $($j.harness_error)" }
    $r = $j.realnet
    if (-not $r) { continue }
    $w = $r.world
    Write-Host ("world: {0} mods={1} locs={2} chars={3} farmObjects={4} farmTerrain={5}" -f $w.date, $w.mods, $w.topLevelLocations, $w.allCharacters, $w.farmObjects, $w.farmTerrainFeatures)
    $m = $r.memory_light_after_join
    if ($m) { Write-Host ("memory after join (no forced GC): heap {0} MB, committed {1} MB, fragmented {2} MB, private {3} MB, working set {4} MB" -f $m.gcHeapMB, $m.gcCommittedMB, $m.gcFragmentedMB, $m.privateMB, $m.workingSetMB) }
    if ($r.gaps_since_host_ready) { $g = $r.gaps_since_host_ready; Write-Host ("host gaps since ready (includes the joiner starting up): frames={0} max={1} over100={2} over500={3}" -f $g.frames, $g.maxMs, $g.over100ms, $g.over500ms); $g.bigGaps | ForEach-Object { Write-Host "    gap $($_.at) $($_.ms) ms in $($_.location)" } }
    Show-Net 'join_and_load' $r.join_and_load.bytesByType
    Show-ModNet $r.join_and_load.modNet
    Show-Mod $r.join_and_load.modMessagesReceived
    $slow = $r.join_and_load.slowHandlers.slowCalls
    if ($slow) { $slow | ForEach-Object { Write-Host "    slow handler $($_.at) $($_.mod) $($_.event) $($_.ms) ms" } }

    foreach ($name in 'first_farm_entry', 'idle_together_30s', 'walking_together_15s', 'client_to_town', 'client_back_to_farm', 'one_night') {
        $p = $r.$name
        if (-not $p) { Write-Host "$name : missing"; continue }
        Show-Net $name $p.net
        Show-ModNet $p.modNet
        Show-Mod $p.modMessagesReceived
        $s = $p.sample
        $g = $p.gaps
        $mem = $p.memory
        Write-Host ("{0,-22} {1,6}s  update mean {2} p99 {3} max {4} | render mean {5} p99 {6} | alloc {7} MB/s gen0 {8} gen2 {9} | gaps max {10} >100ms {11} >500ms {12} | heap {13} MB ws {14} MB" -f '', $p.seconds, $s.update.meanMs, $s.update.p99Ms, $s.update.maxMs, $s.render.meanMs, $s.render.p99Ms, $s.allocatedMBPerSecond, $s.gen0, $s.gen2, $g.maxMs, $g.over100ms, $g.over500ms, $mem.gcHeapMB, $mem.workingSetMB)
        if ($g.bigGaps) { $g.bigGaps | ForEach-Object { Write-Host "    gap $($_.at) $($_.ms) ms in $($_.location)" } }
        $sl = $s.events.slowCalls
        if ($sl) { $sl | ForEach-Object { Write-Host "    slow handler $($_.at) $($_.mod) $($_.event) $($_.ms) ms" } }
    }
    $e = $r.memory_end.light
    Write-Host "in bed: $($r."in_bed_$role")  date after night: $($r.date_after_night)  end memory: heap $($e.gcHeapMB) MB, committed $($e.gcCommittedMB) MB, private $($e.privateMB) MB, ws $($e.workingSetMB) MB; retained after full GC $($r.memory_end.forced.retainedMB) MB"
}
