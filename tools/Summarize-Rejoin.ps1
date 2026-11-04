<#
.SYNOPSIS
Prints what changed in a farmhand's state across a disconnect and rejoin run (scenarios rj36 to rj42).

.PARAMETER Stem
The OutName of the run, for example rj39-baseline.
#>
param([Parameter(Mandatory)][string] $Stem)

$results = Join-Path (Split-Path -Parent $PSScriptRoot) 'testprofile\results'
foreach ($role in 'host', 'client') {
    $file = Join-Path $results "$Stem-$role.json"
    if (-not (Test-Path $file)) { Write-Host "No $role file"; continue }
    $j = Get-Content $file -Raw | ConvertFrom-Json
    $name = ($Stem -split '-')[0]
    $r = $j.$name
    Write-Host "==== $Stem $role  note=$($j.note)  world=$($j.world)"
    if ($j.harness_error) { Write-Host "HARNESS ERROR: $($j.harness_error)" }
    $j.PSObject.Properties | Where-Object { $_.Name -like 'barrier_timeout*' } | ForEach-Object { Write-Host "BARRIER TIMEOUT: $($_.Name)" }
    if (-not $r) { continue }
    Write-Host "start: $($r.start)   end: $($r.end)"
    foreach ($p in $r.PSObject.Properties) {
        if ($p.Name -like 'DIFF_*') {
            Write-Host "-- $($p.Name)"
            if (@($p.Value).Count -eq 0) { Write-Host "   (no change)" } else { @($p.Value) | ForEach-Object { Write-Host "   $_" } }
        } elseif ($p.Name -like 'menusSeen*') {
            Write-Host "-- menus clicked through: $($p.Value | ConvertTo-Json -Compress)"
        } elseif ($p.Value -isnot [pscustomobject] -or $p.Name -notmatch '_(self|hostView|host_view_while_away)$') {
            if ($p.Name -notin 'start', 'end') { Write-Host "-- $($p.Name): $($p.Value | ConvertTo-Json -Compress -Depth 5)" }
        }
    }
}
