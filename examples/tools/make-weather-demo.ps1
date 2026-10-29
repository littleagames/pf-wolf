# Builds examples/mods/weather-demo: its three skies (graphics/*.png), the bonfire's sprites
# (sprites/*.png) and its three levels (maps/MAP01-MAP03.wad). Run it after changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-weather-demo.ps1
#
# The levels, one weather each (game-info.yaml says which):
#   MAP01  a castle courtyard in a night storm (heavy rain): a roofed porch, a colonnade of arches
#          and a gatehouse to shelter in
#   MAP02  a farm in snow squalls that come and go (the mod's own "squalls", which varies)
#   MAP03  a burned-out village with ash falling (the mod's own "ash"), and fires still burning
#
# Rain and snow don't fall under a ceiling flat, under an arch, in a doorway or inside a wall, so
# every roofed tile here has a ceiling flat: the flat plane's high byte (pfwolf-pk3's index 3,
# wood planks).

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'weather-demo'

#
# SKIES: 512x128, going round the view twice; every sine repeats every 512 pixels
#

# A night storm: low, heavy cloud, dark blue-grey, lighter where the clouds bank up
Save-Picture "$modDir\graphics\WDSTORM.png" 512 128 {
    param($x, $y)
    $a = 2 * [Math]::PI * $x / 512
    $t = $y / 127
    $cloud = [Math]::Sin(2 * $a + 0.1 * $y) + 0.6 * [Math]::Sin(5 * $a - 0.07 * $y + 2) + 0.35 * [Math]::Sin(11 * $a + 0.2 * $y)
    $v = 26 + 18 * $t + 10 * $cloud
    New-Color ($v * 0.8) ($v * 0.9) ($v * 1.25)
}

# Snow cloud at dusk: slate grey, darker at the top, with a line of dark hills; dark enough
# for the white flakes to show against it
Save-Picture "$modDir\graphics\WDSNOW.png" 512 128 {
    param($x, $y)
    $a = 2 * [Math]::PI * $x / 512
    $hills = 104 + 8 * [Math]::Sin(3 * $a) + 4 * [Math]::Sin(8 * $a + 1) + 2 * [Math]::Sin(19 * $a)
    if ($y -gt $hills) { return New-Color 40 44 52 }
    $v = 60 + 50 * ($y / 127) + 6 * [Math]::Sin(4 * $a + 0.05 * $y)
    New-Color $v ($v + 4) ($v + 14)
}

# Smoke over a fire: dark red-brown at the top to a dull orange glow at the horizon, with
# drifting bands of smoke
Save-Picture "$modDir\graphics\WDSMOKE.png" 512 128 {
    param($x, $y)
    $a = 2 * [Math]::PI * $x / 512
    $t = $y / 127
    $smoke = [Math]::Sin(3 * $a + 0.12 * $y) + 0.5 * [Math]::Sin(9 * $a - 0.09 * $y)
    $glow = [Math]::Pow($t, 2.2)
    New-Color (40 + 150 * $glow + 10 * $smoke) (20 + 60 * $glow + 6 * $smoke) (16 + 10 * $glow + 4 * $smoke)
}

#
# THE BONFIRE: two frames of flames over a heap of logs
#

foreach ($frame in 'A', 'B') {
    $seed = if ($frame -eq 'A') { 0.0 } else { 1.7 }
    Save-Picture "$modDir\sprites\WDFR${frame}0.png" 64 64 {
        param($x, $y)
        # the logs: crossed dark bars along the bottom
        $dx = $x - 32
        if ($y -ge 54 -and $y -le 61 -and [Math]::Abs($dx) -le 18 -and ([Math]::Abs(($y - 57.5) - 0.3 * $dx) -le 2.5 -or [Math]::Abs(($y - 57.5) + 0.3 * $dx) -le 2.5)) {
            if (($x + $y) % 7 -eq 0) { return New-Color 230 120 30 }
            return New-Color 70 40 22
        }
        # the flames: a tongue shape, narrowing upward, its edge wavering
        $h = 56 - $y
        if ($h -lt 0 -or $h -gt 40) { return $null }
        $width = 15 * (1 - $h / 40) + 3 * [Math]::Sin($h * 0.45 + $seed * 2 + $x * 0.1)
        $sway = 3 * [Math]::Sin($h * 0.15 + $seed)
        $d = [Math]::Abs($dx - $sway)
        if ($d -gt $width) { return $null }
        $heat = 1 - $d / [Math]::Max($width, 1) * 0.7 - $h / 60
        if ($heat -gt 0.75) { return New-Color 255 240 160 }
        if ($heat -gt 0.5) { return New-Color 255 190 40 }
        if ($heat -gt 0.3) { return New-Color 240 110 20 }
        New-Color 190 50 10
    }
}

#
# THE LEVELS
#

# Ceiling flat index 3 (wood planks) over floor index 1 (grey stone): a roof
$Roof = 3 * 256 + 1
# Wood floor too, for the barn
$Barn = 3 * 256 + 3
$Lamp = 810; $Fire = 811

# MAP01: the storm. Walls are 2 stories (game-info wall-height); the colonnade's arches ('a')
# are open below with a block of wall above, faced like the pillars ('I') beside them.
$Storm = @(
    '##X#######################'   # y=24 the elevator switch
    '#eee#pppppppLppppppp#rrr##'   #      the elevator room; the porch, roofed, with lamps
    '#eeeEppppppppppppppp#rrr##'   #      the elevator room's door
    '#eee#...............IrLr##'   # y=27 the courtyard; the colonnade and its gallery
    '#####...............arrr##'
    '#####......w........Irrr##'   #      a well
    '#####...............arrr##'
    '#####...v.......v...Irrr##'   # y=31 barrels
    '#####...............arLr##'
    '#####...............Irrr##'
    '#####...............arrr##'
    '#####...............Irrr##'
    '##########D###############'   # y=36 the gatehouse door
    '#######ggggggg############'
    '#######gggPggg############'   #      the player start, facing north
    '##########################'
)

$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 1 }                                          # grey stone
$Tiles['I'] = @{ Plane0 = 1 }                                          # a pillar
$Tiles['a'] = @{ Plane0 = 108; Height = 2 }                            # an arch: open below, wall above
$Tiles['.'] = @{ Plane0 = 108 }                                        # the courtyard, open to the sky
$Tiles['p'] = @{ Plane0 = 108; Flat = $Roof }                          # the porch
$Tiles['L'] = @{ Plane0 = 108; Flat = $Roof; Plane1 = $Lamp }          # a porch lamp
$Tiles['r'] = @{ Plane0 = 108; Flat = $Roof }                          # the gallery behind the arches
$Tiles['w'] = @{ Plane0 = 108; Plane1 = 59 }                           # the well
$Tiles['v'] = @{ Plane0 = 108; Plane1 = 58 }                           # a barrel
$Tiles['g'] = @{ Plane0 = 109; Flat = $Roof }                          # the gatehouse
$Tiles['P'] = @{ Plane0 = 109; Flat = $Roof; Plane1 = 19 }             # player start, facing north
$Tiles['e'] = @{ Plane0 = 110; Flat = $Roof }                          # the elevator room
$Tiles['D'] = @{ Plane0 = 91 }                                         # a door, north-south
$Tiles['E'] = @{ Plane0 = 90 }                                         # a door, east-west
$Tiles['X'] = @{ Plane0 = 21 }                                         # elevator switch
$planes = New-MapPlanes
Add-MapLayout $planes $Storm $Tiles 19 24
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Night Storm' $planes

# MAP02: the farm. The barn ('b') is roofed and open to the yard on its east side; the shed with
# the elevator ('e') is roofed. Wood walls ('W') for the farm buildings.
$Farm = @(
    '####################X###'   # y=26
    '#bbbbW..t.......t..#eee#'
    '#bbbbW.............Deee#'   #      the shed's door
    '#bbbb..........t...#####'
    '#bPbb.....WWW......#####'   # y=30 the player start, in the barn, facing east
    '#bbbb.....WWW....t.#####'
    '#bbbbW....WWW......#####'
    '#bbbbW.t...........#####'
    '########################'
)
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 1 }
$Tiles['W'] = @{ Plane0 = 12 }                                         # wood
$Tiles['.'] = @{ Plane0 = 108 }                                        # the yard, open to the sky
$Tiles['t'] = @{ Plane0 = 108; Plane1 = 34 }                           # a bare bush
$Tiles['b'] = @{ Plane0 = 109; Flat = $Barn }                          # the barn
$Tiles['P'] = @{ Plane0 = 109; Flat = $Barn; Plane1 = 20 }             # player start, facing east
$Tiles['e'] = @{ Plane0 = 110; Flat = $Roof }                          # the shed
$Tiles['D'] = @{ Plane0 = 90 }                                         # door, east-west
$Tiles['X'] = @{ Plane0 = 21 }
$planes = New-MapPlanes
Add-MapLayout $planes $Farm $Tiles 20 26
Save-MapWad "$modDir\maps\MAP02.wad" 'MAP02' 'Snow Squalls' $planes

# MAP03: the burned village. Digits are what's left of its walls, 1 to 3 stories; 'f' a fire
# still burning; the chapel ('c') still has its roof.
$Ruins = @(
    '##################X###'   # y=26
    '#P...1..f..3....#ccc##'   #      the player start, facing east
    '#.........2.....Dccc##'   #      the chapel's door
    '#..33........f..#ccc##'
    '#...........1...######'   # y=30
    '#.f....22.......######'
    '#.......2...f...######'
    '#..1..........3.######'
    '#.....f.........######'
    '######################'
)
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 1 }
$Tiles['1'] = @{ Plane0 = 1; Height = 1 }
$Tiles['2'] = @{ Plane0 = 1; Height = 2 }
$Tiles['3'] = @{ Plane0 = 1; Height = 3 }
$Tiles['.'] = @{ Plane0 = 108 }
$Tiles['f'] = @{ Plane0 = 108; Plane1 = $Fire }
$Tiles['P'] = @{ Plane0 = 108; Plane1 = 20 }
$Tiles['c'] = @{ Plane0 = 109; Flat = $Roof }
$Tiles['D'] = @{ Plane0 = 90 }
$Tiles['X'] = @{ Plane0 = 21 }
$planes = New-MapPlanes
Add-MapLayout $planes $Ruins $Tiles 20 26
Save-MapWad "$modDir\maps\MAP03.wad" 'MAP03' 'Ashfall' $planes
