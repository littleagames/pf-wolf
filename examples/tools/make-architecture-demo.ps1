# Builds examples/mods/architecture-demo: its wall, floor and ceiling textures (textures/*.png,
# flats/*.png), its sky (graphics/ATSKY.png) and its level (maps/MAP01.wad). Run it after
# changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-architecture-demo.ps1
#
# The level: an octagonal atrium, its corners cut by 45 degree walls faced with mosaic, a marble
# floor laid in a checkerboard, a coffered ceiling with a lattice ring round an open skylight,
# two sign pillars pointing the way out, and a corridor running off at 45 degrees to the exit.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'architecture-demo'

#
# WALLS
#

# Sandstone blocks: 16 pixels high, 32 wide, staggered, with mortar between
$grit = New-Object Random 3
$Sandstone = {
    param($x, $y)
    $row = [Math]::Floor($y / 16)
    $bx = ($x + 16 * ($row % 2)) % 32
    if ($y % 16 -eq 15 -or $bx -eq 31) { return New-Color 120 96 64 }
    $shade = 10 * [Math]::Sin($x * 0.31 + $row) + $grit.Next(-8, 9)
    if ($y % 16 -eq 0 -or $bx -eq 0) { $shade += 18 }
    New-Color (196 + $shade) (164 + $shade) (112 + $shade * 0.7)
}
Save-Picture "$modDir\textures\ATSTONE.png" 64 64 $Sandstone

# Mosaic for the diagonal faces: blue and gold diamonds with a border
Save-Picture "$modDir\textures\ATMOSAIC.png" 64 64 {
    param($x, $y)
    if ($y -lt 4 -or $y -gt 59) { return New-Color 160 120 40 }
    $d = [Math]::Abs(($x % 16) - 7.5) + [Math]::Abs(($y % 16) - 7.5)
    if (($x % 4 -eq 0) -or ($y % 4 -eq 0)) { return New-Color 70 64 56 }             # the grout
    if ($d -lt 5) { return New-Color 220 180 60 }
    if ($d -lt 8) { return New-Color 30 70 150 }
    New-Color 20 40 100
}

# A sign: a dark plaque on the sandstone with a gold arrow pointing right. walls.yaml mirrors it
# on the pillars' north faces, so from either side it points the same way through the room.
Save-Picture "$modDir\textures\ATARROW.png" 64 64 {
    param($x, $y)
    if ($x -ge 8 -and $x -le 55 -and $y -ge 18 -and $y -le 45) {
        if ($x -eq 8 -or $x -eq 55 -or $y -eq 18 -or $y -eq 45) { return New-Color 160 120 40 }
        # the arrow: a shaft and a head, pointing right
        if (($y -ge 28 -and $y -le 35 -and $x -ge 14 -and $x -le 38) -or ($x -ge 38 -and $x -le 50 -and [Math]::Abs($y - 31.5) -le (50 - $x))) {
            return New-Color 232 196 70
        }
        return New-Color 40 30 24
    }
    & $Sandstone $x $y
}

#
# FLATS
#

# Marble: a light and a dark slab, each with veins (the floor's laid in a checkerboard of them)
function Save-Marble([string]$Name, $Base, $Vein, [int]$Seed) {
    $r = New-Object Random $Seed
    $phase = $r.NextDouble() * 6
    Save-Picture "$modDir\flats\$Name.png" 64 64 {
        param($x, $y)
        if ($x -eq 0 -or $y -eq 0) { return New-Color ($Base[0] * 0.7) ($Base[1] * 0.7) ($Base[2] * 0.7) }
        $v = [Math]::Sin(($x + $y) * 0.12 + 3 * [Math]::Sin($y * 0.09 + $phase) + $phase)
        if ([Math]::Abs($v) -lt 0.08) { return New-Color $Vein[0] $Vein[1] $Vein[2] }
        $n = $r.Next(-5, 6)
        New-Color ($Base[0] + $n) ($Base[1] + $n) ($Base[2] + $n)
    }
}
Save-Marble 'ATMARB1' @(214, 210, 200) @(150, 146, 140) 11
Save-Marble 'ATMARB2' @(44, 84, 66) @(120, 160, 140) 12

# The coffered ceiling: dark wood beams round a sunken panel
Save-Picture "$modDir\flats\ATCOFFER.png" 64 64 {
    param($x, $y)
    $e = [Math]::Min([Math]::Min($x, 63 - $x), [Math]::Min($y, 63 - $y))
    if ($e -lt 6) { return New-Color 92 56 30 }
    if ($e -lt 9) { return New-Color 64 38 20 }
    if ($e -lt 11) { return New-Color 150 110 60 }
    New-Color 120 84 46
}

# The lattice: wooden bars on a grid, clear between them so the sky shows through
Save-Picture "$modDir\flats\ATLATTIC.png" 64 64 {
    param($x, $y)
    if (($x % 16) -lt 4 -or ($y % 16) -lt 4) {
        if (($x % 16) -eq 0 -or ($y % 16) -eq 0) { return New-Color 140 96 52 }
        return New-Color 96 60 32
    }
    $null
}

# The sky: a clear day, deeper blue overhead, with a few soft clouds
Save-Picture "$modDir\graphics\ATSKY.png" 512 128 {
    param($x, $y)
    $a = 2 * [Math]::PI * $x / 512
    $t = $y / 127
    $cloud = [Math]::Sin(2 * $a + 0.06 * $y) + 0.6 * [Math]::Sin(5 * $a + 1 - 0.05 * $y) - 2.4 * [Math]::Abs($t - 0.55)
    if ($cloud -gt 0.3) { return New-Color 244 244 248 }
    New-Color (60 + 120 * $t) (120 + 90 * $t) (220 + 30 * $t)
}

#
# THE LEVEL
#

# Walls are 2 stories here (game-info wall-height); the sign pillars are 1 (plane 3). Every wall
# id is above 63, as any can be now (up to 255): 200 sandstone, 201 a sign pillar. The diagonal
# corners are sandstone walls with this mod's markers (820-823, mosaic faces) on the object plane;
# the 45 degree corridor's are pfwolf-pk3's own (400 SolidNW, 403 SolidSE), faced with the wall.
$Layout = @(
    '##############################'   # y=20
    '##############################'
    '#######################xxxx###'   #      the exit room
    '#######################xxxxX##'
    '#######################xxxx###'
    '######################dxxxx###'
    '######1......2#######dac######'   # y=26 the atrium's north side; the 45 degree corridor
    '#####1........2#####dac#######'
    '####1..........2###dac########'
    '####...llllll...##dac#########'   #      the lattice ring round the skylight
    '####...lssssl...#dac##########'   # y=30
    '####...Iswssl...Eac###########'   #      a sign pillar, the well under the sky, the way out
    '####...lssssI...##############'   #      the other pillar
    '####...lssssl...##############'
    '####...llllll...##############'
    '####3..........4##############'   # y=35
    '#####3........4###############'
    '######3......4################'
    '#########D####################'   #      the atrium door
    '#######ggggg##################'
    '#######ggPgg##################'   # y=40 the player start, facing north
    '##############################'
)

$Area = 108; $ExitArea = 109
# Flat plane values: floor index (low byte) + ceiling index x 256. Floors 30, 31: the marbles;
# ceilings 30: coffered, 31: the lattice; none: the sky
$Coffer = 30 * 256; $Lattice = 31 * 256
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 200 }
$Tiles['1'] = @{ Plane0 = 200; Plane1 = 820 }          # diagonal, the solid corner north-west
$Tiles['2'] = @{ Plane0 = 200; Plane1 = 821 }          # ...north-east
$Tiles['3'] = @{ Plane0 = 200; Plane1 = 822 }          # ...south-west
$Tiles['4'] = @{ Plane0 = 200; Plane1 = 823 }          # ...south-east
$Tiles['d'] = @{ Plane0 = 200; Plane1 = 400 }          # the corridor's sides: SolidNW
$Tiles['c'] = @{ Plane0 = 200; Plane1 = 403 }          # ...and SolidSE
$Tiles['I'] = @{ Plane0 = 201; Height = 1 }            # a sign pillar, 1 story
$Tiles['.'] = @{ Plane0 = $Area; Flat = $Coffer }
$Tiles['l'] = @{ Plane0 = $Area; Flat = $Lattice }
$Tiles['s'] = @{ Plane0 = $Area; Flat = 0 }            # open to the sky
$Tiles['w'] = @{ Plane0 = $Area; Flat = 0; Plane1 = 59 }   # the well
$Tiles['a'] = @{ Plane0 = $Area; Flat = $Coffer }
$Tiles['g'] = @{ Plane0 = $Area; Flat = $Coffer }
$Tiles['P'] = @{ Plane0 = $Area; Flat = $Coffer; Plane1 = 19 }
$Tiles['x'] = @{ Plane0 = $ExitArea; Flat = $Coffer }
$Tiles['X'] = @{ Plane0 = 21 }
$Tiles['D'] = @{ Plane0 = 91 }
$Tiles['E'] = @{ Plane0 = 90 }
$planes = New-MapPlanes -Fill 200
Add-MapLayout $planes $Layout $Tiles 0 20
# The marble floor, in a checkerboard: every open tile's floor index by its square's color
for ($i = 0; $i -lt 64 * 64; $i++) {
    if ($planes[0][$i] -ge 108 -and $planes[0][$i] -le 143) {
        $x = $i % 64; $y = [Math]::Floor($i / 64)
        $planes[2][$i] = ($planes[2][$i] -band 0xFF00) + 30 + (($x + $y) % 2)
    }
}
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Architecture Demo' $planes
