# Builds examples/mods/hunters-demo: its level (maps/MAP01.wad), a maze. Run it after changing it:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-hunters-demo.ps1
#
# The maze is carved by a seeded random walk (the same maze every run; change $Seed for another):
# 15x15 cells, each a tile with the wall tiles between, so 31x31 tiles, with a few extra gaps
# knocked through so there's more than one way round. The player starts in a hall at the middle.
# Hunters (scripts/hunters.lua finds their way by A*) and ordinary guards are scattered through it.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'hunters-demo'

$Seed = 1944
$Cells = 15
$Size = 2 * $Cells + 1
$Left = 16; $Top = 16
$random = New-Object Random $Seed

# The grid of tiles: true where it's open. Cells are at odd positions.
$open = New-Object 'bool[,]' $Size, $Size
$visited = New-Object 'bool[,]' $Cells, $Cells
$stack = New-Object System.Collections.Generic.Stack[object]
$stack.Push(@(0, 0)); $visited[0, 0] = $true; $open[1, 1] = $true
$dirs = @(@(1, 0), @(-1, 0), @(0, 1), @(0, -1))
while ($stack.Count -gt 0) {
    $c = $stack.Peek()
    $next = @($dirs | Where-Object {
        $nx = $c[0] + $_[0]; $ny = $c[1] + $_[1]
        $nx -ge 0 -and $ny -ge 0 -and $nx -lt $Cells -and $ny -lt $Cells -and -not $visited[$nx, $ny]
    })
    if ($next.Count -eq 0) { [void]$stack.Pop(); continue }
    $d = $next[$random.Next($next.Count)]
    $nx = $c[0] + $d[0]; $ny = $c[1] + $d[1]
    $visited[$nx, $ny] = $true
    $open[(2 * $c[0] + 1 + $d[0]), (2 * $c[1] + 1 + $d[1])] = $true
    $open[(2 * $nx + 1), (2 * $ny + 1)] = $true
    $stack.Push(@($nx, $ny))
}
# Loops: knock through some walls between two open tiles
for ($k = 0; $k -lt 40; $k++) {
    $x = 1 + $random.Next($Size - 2); $y = 1 + $random.Next($Size - 2)
    if ($open[$x, $y]) { continue }
    if (($open[($x - 1), $y] -and $open[($x + 1), $y]) -or ($open[$x, ($y - 1)] -and $open[$x, ($y + 1)])) { $open[$x, $y] = $true }
}
# The hall in the middle, 5x5
$mid = [int](($Size - 1) / 2)
for ($y = $mid - 2; $y -le $mid + 2; $y++) { for ($x = $mid - 2; $x -le $mid + 2; $x++) { $open[$x, $y] = $true } }

$planes = New-MapPlanes -Fill 8
$Area = 108
for ($y = 0; $y -lt $Size; $y++) {
    for ($x = 0; $x -lt $Size; $x++) {
        if ($open[$x, $y]) { $planes[0][($Top + $y) * 64 + $Left + $x] = $Area }
    }
}
# The player in the middle of the hall, facing north
$planes[1][($Top + $mid) * 64 + $Left + $mid] = 19

# Enemies on open cells away from the hall, alternating: hunter guard, ordinary guard, hunter
# dog, ordinary dog. Each stands facing a way chosen at random.
$kinds = @(910, 'guard', 911, 'dog')
$placed = 0
while ($placed -lt 24) {
    $cx = $random.Next($Cells); $cy = $random.Next($Cells)
    $x = 2 * $cx + 1; $y = 2 * $cy + 1
    if ([Math]::Abs($x - $mid) -le 4 -and [Math]::Abs($y - $mid) -le 4) { continue }
    $i = ($Top + $y) * 64 + $Left + $x
    if ($planes[1][$i] -ne 0) { continue }
    $kind = $kinds[$placed % 4]
    $facing = $random.Next(4)
    $planes[1][$i] = switch ($kind) { 'guard' { 108 + $facing } 'dog' { 134 + $facing } default { $kind } }
    $placed++
}
# The exit: an elevator switch in the outer wall, beside the maze's far corner
$planes[0][($Top + $Size - 2) * 64 + $Left + $Size - 1] = 21

Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Hunters Demo' $planes
