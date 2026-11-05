<#
.SYNOPSIS
Makes a throwaway copy of a played save for a real mod list run, or deletes it again.

.DESCRIPTION
The game needs the files inside a save folder to carry the folder's name, so a plain folder copy does not load. This copies the folder,
renames the inner files, and never changes the source. -Remove deletes the copy and the VFReal_<id> folder the game writes during a run.

.PARAMETER Source
The played save folder name, for example LazyRooster_402395808.
#>
param(
    [string] $Source = 'LazyRooster_402395808',
    [switch] $Remove
)

$saves = Join-Path $env:APPDATA 'StardewValley\Saves'
$copy = "VFReal_$Source"
$id = $Source.Substring($Source.LastIndexOf('_') + 1)
foreach ($name in $copy, "VFReal_$id") {
    $path = Join-Path $saves $name
    if (Test-Path $path) { Remove-Item $path -Recurse -Force }
}
if ($Remove) { Write-Host "Removed $copy and VFReal_$id"; return }

$src = Join-Path $saves $Source
if (-not (Test-Path $src)) { throw "Save not found: $src" }
$dest = Join-Path $saves $copy
Copy-Item $src $dest -Recurse
foreach ($file in Get-ChildItem $dest -File) {
    if ($file.Name -eq $Source -or $file.Name -eq ($Source + '_old')) {
        Rename-Item $file.FullName ($copy + $file.Name.Substring($Source.Length))
    }
}
Write-Host "Made $copy"
