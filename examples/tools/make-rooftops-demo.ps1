# Builds examples/mods/rooftops-demo: its dusk sky (graphics/RTSKY.png) and its level
# (maps/MAP01.wad). Run it after changing either:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-rooftops-demo.ps1
#
# The level is on story 3 (game-info `story`): the player walks the roofs of 2-story buildings
# (game-info `wall-height: 2`, so a plain wall is a block from the ground up to the player's
# floor) with a street two stories below. The height plane (plane 3) gives the rest their blocks:
# a start story in bits 8-11, a stop in bits 0-3, and a second block above the first in bits
# 12-15 (start) and 4-7 (stop). 0x4152 is stories 1 to 2 and 4 to 5.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'rooftops-demo'

#
# THE SKY: dusk over a city, 512x128 (going round the view twice); a skyline of far towers
# along the bottom with a few lit windows
#

Save-Picture "$modDir\graphics\RTSKY.png" 512 128 {
    param($x, $y)
    # the far skyline: blocks of towers, each its own height, repeating every 512 pixels
    $block = [int][Math]::Floor($x / 16)
    $tower = 92 + (($block * 37) % 23) - (($block * 11) % 7) * 2
    if ($y -ge $tower) {
        $lit = (($x % 16) -in 3, 4, 10, 11) -and (($y - $tower) % 6 -in 2, 3) -and ((($block * 7 + $y) % 5) -eq 0)
        if ($lit) { return New-Color 230 200 110 }
        return New-Color 28 26 38
    }
    # the sky: deep blue overhead to orange at the horizon, with a few streaks of cloud (strong
    # colors: a dull dark blue would snap to the palette's grey)
    $t = $y / 127
    $a = 2 * [Math]::PI * $x / 512
    $cloud = [Math]::Max(0, [Math]::Sin(3 * $a + 0.08 * $y) * [Math]::Sin(7 * $a - 0.05 * $y) - 0.55)
    New-Color (16 + 240 * [Math]::Pow($t, 2) + 80 * $cloud) (24 + 120 * [Math]::Pow($t, 2.5) + 50 * $cloud) (150 - 110 * $t + 30 * $cloud)
}

#
# THE LEVEL
#
# The brick roofs on the west ('r'), the street below ('.'), a wooden bridge over it ('b') with a
# rail on its north side ('h'), and the blue building on the east, entered by a window ('w'): its upper story is
# open ('i': a floor block up to the player's floor, a ceiling block over it), and a door ('D')
# leads out onto the grey terrace ('R'), with the exit switch ('E') and a crate ('C') that
# pushes along the terrace on its own story, leaving the roof under it. A tower ('T') stands at
# the corner and a sign hangs over the brick roofs ('o').
#
# The bridge's south side has no rail, so the street shows below it. The engine has no falling:
# step off and you walk on air at the player's story. A level blocks such an edge (the north
# rail, the parapets round the roofs: blocks solid on story 3), or makes it a trap.

$Level = @(
    '##########################'   # y=24
    '#pppppppp......WWWWWWWWTT#'
    '#rrrrrrrp......WiiiiiiWTT#'
    '#rPrrrrrphhhhhhWiiiiiiDRR#'   #      the player start, facing east; the terrace door
    '#rrrrrrrrbbbbbbwiiiiiiWRE#'   # y=28 the bridge, into the window; the exit switch
    '#rrrrrrrp......WiiiiiiWRR#'   #      no rail on the bridge's south side: see below
    '#rrrrorrp......WiiiiiiWRC#'   #      the sign; the crate
    '#rrrrrrrp......WWWwWWWWRR#'
    '#pppppppp......ppppppppRR#'
    '##########################'   # y=33
)

$Roof = 1                # flats: floor index 1 (grey stone) on the roofs
$Wood = 3 * 256 + 3      # floor and ceiling index 3 (wood): the bridge, the building's upper story
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 17; Height = 5 }                         # brick, up to story 5, round the level
$Tiles['p'] = @{ Plane0 = 17; Height = 3; Flat = $Roof }           # a parapet: a story over the roof
$Tiles['r'] = @{ Plane0 = 17; Flat = $Roof }                       # a brick roof (the level's 2 stories)
$Tiles['P'] = @{ Plane0 = 17; Flat = $Roof; Plane1 = 20 }          # the player start, facing east
$Tiles['o'] = @{ Plane0 = 28; Flat = $Roof; Height = 0x4042 }      # a roof, and a sign over it on story 4
$Tiles['.'] = @{ Plane0 = 108 }                                    # the street, two stories down
$Tiles['h'] = @{ Plane0 = 108; Flat = $Wood; Height = 0x0303 }     # a rail over the street, on story 3
$Tiles['b'] = @{ Plane0 = 108; Flat = $Wood; Height = 0x0202 }     # the bridge: a block on story 2, its top the floor
$Tiles['W'] = @{ Plane0 = 8; Height = 5 }                          # the blue building, 5 stories
$Tiles['w'] = @{ Plane0 = 8; Height = 0x4052 }                     # a window: wall but for story 3
$Tiles['i'] = @{ Plane0 = 109; Flat = $Wood; Height = 0x4152 }     # its upper story: floor 1-2, ceiling 4-5
$Tiles['D'] = @{ Plane0 = 90; Height = 5 }                         # a door out, its lintel up to story 5
$Tiles['T'] = @{ Plane0 = 1; Height = 7 }                          # the tower
$Tiles['R'] = @{ Plane0 = 1; Flat = $Roof }                        # the grey terrace (a roof)
$Tiles['E'] = @{ Plane0 = 21; Height = 3 }                         # the exit switch, on the player's story
$Tiles['C'] = @{ Plane0 = 12; Height = 3; Flat = $Roof; Plane1 = 450 }   # a crate: the mod's pushwall trigger 450
$planes = New-MapPlanes
Add-MapLayout $planes $Level $Tiles 18 24
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Rooftops' $planes
