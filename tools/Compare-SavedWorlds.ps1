<#
.SYNOPSIS
Compares worlds saved after real nights, to see whether turning settings on changes the simulated world more than the game changes it by itself.

.DESCRIPTION
Each save-<tag>.xml in -Directory is streamed and every element name is counted overall and per location, and the player's money, date and play
time are read. Every pair of saves is compared by how many of those counts differ. The game's own random spawns differ between any two runs,
so the number that matters is how the pairs inside one group (both settings off) compare with the pairs across groups (off against on).

Make the saves by running tools\Run-Harness.ps1 with -Only realsp and the VFH_NOTOUR environment variable set to 1 on a copy of a real save
(tools\New-RealSaveCopy.ps1), and copying the written world file out of %APPDATA%\StardewValley\Saves after each run.

.PARAMETER Directory
The folder holding save-<tag>.xml files.

.PARAMETER Off
Tags of the runs with every setting off.

.PARAMETER On
Tags of the runs with settings on.
#>
param(
    [Parameter(Mandatory)][string] $Directory,
    [Parameter(Mandatory)][string[]] $Off,
    [Parameter(Mandatory)][string[]] $On
)

function Summarize($path) {
    $settings = New-Object System.Xml.XmlReaderSettings
    $settings.DtdProcessing = 'Ignore'
    $reader = [System.Xml.XmlReader]::Create($path, $settings)
    $all = @{}
    $perLocation = @{}
    $facts = @{}
    $locationDepth = -1
    $locationName = $null
    $counts = $null
    while ($reader.Read()) {
        if ($reader.NodeType -eq 'Element') {
            $name = $reader.Name
            $depth = $reader.Depth
            $all[$name] = 1 + [int]$all[$name]
            if ($name -eq 'GameLocation' -and $locationDepth -lt 0) { $locationDepth = $depth; $locationName = $null; $counts = @{} }
            if ($locationDepth -ge 0) {
                $counts[$name] = 1 + [int]$counts[$name]
                if ($name -eq 'name' -and $null -eq $locationName -and $depth -eq $locationDepth + 1) { $locationName = $reader.ReadElementContentAsString() }
            }
            if ($name -in 'money', 'totalMoneyEarned', 'millisecondsPlayed', 'dayOfMonth', 'year', 'currentSeason', 'whichFarm' -and -not $facts.ContainsKey($name)) {
                $facts[$name] = $reader.ReadElementContentAsString()
            }
        } elseif ($reader.NodeType -eq 'EndElement' -and $reader.Name -eq 'GameLocation' -and $reader.Depth -eq $locationDepth) {
            $key = if ($locationName) { $locationName } else { "unnamed@$($perLocation.Count)" }
            $perLocation[$key] = $counts
            $locationDepth = -1
        }
    }
    $reader.Close()
    [pscustomobject]@{ All = $all; PerLocation = $perLocation; Facts = $facts }
}

function DifferingCounts($a, $b) {
    $keys = New-Object System.Collections.Generic.HashSet[string]
    foreach ($n in (@($a.All.Keys) + @($b.All.Keys) | Sort-Object -Unique)) {
        if ([int]$a.All[$n] -ne [int]$b.All[$n]) { [void]$keys.Add("element:$n") }
    }
    foreach ($k in (@($a.PerLocation.Keys) + @($b.PerLocation.Keys) | Sort-Object -Unique)) {
        $ca = $a.PerLocation[$k]
        $cb = $b.PerLocation[$k]
        $names = (@(if ($ca) { $ca.Keys }) + @(if ($cb) { $cb.Keys })) | Sort-Object -Unique
        foreach ($f in $names) {
            $x = if ($ca) { [int]$ca[$f] } else { -1 }
            $y = if ($cb) { [int]$cb[$f] } else { -1 }
            if ($x -ne $y) { [void]$keys.Add("location:${k}:$f") }
        }
    }
    $keys.Count
}

function PairStats([string[]] $first, [string[]] $second, [bool] $sameGroup) {
    $counts = @()
    for ($i = 0; $i -lt $first.Count; $i++) {
        for ($j = 0; $j -lt $second.Count; $j++) {
            if ($sameGroup -and $j -le $i) { continue }
            $counts += DifferingCounts $summaries[$first[$i]] $summaries[$second[$j]]
        }
    }
    $m = $counts | Measure-Object -Average -Minimum -Maximum
    "pairs {0}, mean {1:N0} differing counts, range {2} to {3}" -f $counts.Count, $m.Average, $m.Minimum, $m.Maximum
}

$summaries = @{}
foreach ($tag in $Off + $On) {
    $path = Join-Path $Directory "save-$tag.xml"
    if (-not (Test-Path $path)) { throw "Missing $path" }
    $summaries[$tag] = Summarize $path
}

$factSets = @{}
foreach ($tag in $summaries.Keys) {
    $key = ($summaries[$tag].Facts.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ' '
    $factSets[$key] = 1 + [int]$factSets[$key]
}
"distinct money, date and play time sets across $($summaries.Count) saved worlds, $($factSets.Count)"
"settings off against settings off: " + (PairStats $Off $Off $true)
"settings off against settings on:  " + (PairStats $Off $On $false)
"settings on against settings on:   " + (PairStats $On $On $true)
