# Builds examples/mods/pits-demo: its grate (flats/PDGRATE.png), its sea of cloud
# (graphics/PDCLOUDS.png) and its level (maps/MAP01.wad). Run it after changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-pits-demo.ps1
#
# The level is on story 3 (game-info `story`) with walls from the ground to story 3
# (`wall-height: 3`): the walkways are floor tiles with a block under them (plane 3: 2 is a block
# from story 1 to 2, its top the floor), and a pit is a floor tile with no block at all, open two
# stories down to the ground. The height plane's blocks: a start story in bits 8-11 and a stop in
# bits 0-3 (0x0202 is story 2 alone); see make-rooftops-demo.ps1 for a second block.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'pits-demo'

#
# THE GRATE: steel bars, 4 pixels wide every 16, with see-through squares between (the floor
# color shows through them: a flat's see-through pixels don't show the pit under it)
#

Save-Picture "$modDir\flats\PDGRATE.png" 64 64 {
    param($x, $y)
    $bx = $x % 16; $by = $y % 16
    if ($bx -ge 4 -and $by -ge 4) { return $null }
    # a lit edge on the bars' top and left, a dark one on their bottom and right
    if ($bx -eq 0 -or $by -eq 0) { return New-Color 150 150 170 }
    if (($bx -eq 3 -and $by -ge 4) -or ($by -eq 3 -and $bx -ge 4)) { return New-Color 60 60 76 }
    New-Color 104 104 124
}

#
# THE SEA OF CLOUD: the east pit's floor sky, 512x96 (going round the view twice): deep blue with
# streaks of white cloud, finer towards the horizon (row 0)
#

Save-Picture "$modDir\graphics\PDCLOUDS.png" 512 96 {
    param($x, $y)
    $t = $y / 95                                  # 0 at the horizon, 1 straight down
    $a = 2 * [Math]::PI * $x / 512
    $scale = 1 + 3 * (1 - $t)                     # streaks bunch up into the distance
    $cloud = [Math]::Sin(2 * $a + $y * 0.09 * $scale) * [Math]::Sin(5 * $a - $y * 0.05 * $scale) + 0.35 * [Math]::Sin(11 * $a + $y * 0.2)
    if ($cloud -gt 0.55) { return New-Color 236 240 252 }
    if ($cloud -gt 0.35) { return New-Color 168 188 236 }
    New-Color (24 + 30 * (1 - $t)) (40 + 50 * (1 - $t)) (128 + 50 * (1 - $t))
}

#
# THE LEVEL
#
# You start in the hall at the south ('S', facing north) between two lamps ('L'). North of it is
# the chasm: a stone bridge ('b') runs straight across it to the door ('D') and the elevator.
# West of the bridge the pit ('w') goes down two stories to a stone floor, crossed by a steel
# grate walkway ('g', a block on story 2 alone, so story 1 is open under it); east of it the pit
# ('c') drops into a sea of cloud. Stone pillars ('P') stand in the pits, from the bottom up to
# the ceiling.
#
# The engine has no falling: a thing walks across a pit at the player's story as if it were
# floor. So every pit tile has a PitEdge marker (980, `blocktile: all`) that keeps everyone off it.

#
# North of the door a passage turns east into the elevator ('V' its door, 'T' its walls), with
# the switch ('E') at its east end. Wall 21 is a switch only on its east and west faces: its
# north and south ones are the elevator's plain walls. So the elevator runs east-west, the switch
# faced from the west, and its side walls (21 too) show their plain faces.

$Level = @(
    '#########TT####'   # y=10 the elevator's north wall
    '#######eVeeE###'   #      the passage, the elevator door, the elevator, the switch
    '#######e#TT####'   #      the elevator's south wall
    'BBBBBBBDBBBBBBB'   #      the door to the exit
    'BwwwwwwbccccccB'   # y=14 the chasm: the stone pit, the bridge, the cloud pit
    'BwwwwwwbccccccB'
    'BwwPwwwbccccPcB'   #      pillars in both pits
    'BwwwwwwbccccccB'
    'BggggggbccccccB'   #      the grate walkway to the west wall
    'BggggggbccccccB'
    'BwwwPwwbccccccB'   # y=20
    'BBBBBBBeBBBBBBB'
    '####eeeeeee####'   #      the hall
    '####eLeeeLe####'
    '####eeeSeee####'   # y=24 the player start, facing north
)

$Floor = 2                 # a floor tile's block: stories 1 to 2, its top the floor on story 3
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 1 }                                          # grey stone, the ground to story 3
$Tiles['B'] = @{ Plane0 = 8 }                                          # blue stone round the chasm
$Tiles['E'] = @{ Plane0 = 21 }                                         # the elevator switch, faced from the west
$Tiles['T'] = @{ Plane0 = 21 }                                         # the elevator's walls (their plain faces)
$Tiles['V'] = @{ Plane0 = 100; Flat = 1 }                              # the elevator door (east-west)
$Tiles['D'] = @{ Plane0 = 91; Flat = 1 }                               # a door on story 3, its frame down to the ground
$Tiles['e'] = @{ Plane0 = 108; Flat = 1; Height = $Floor }             # the hall's floor (grey stone, index 1)
$Tiles['L'] = @{ Plane0 = 108; Flat = 1; Height = $Floor; Plane1 = 26 }   # a floor lamp
$Tiles['S'] = @{ Plane0 = 108; Flat = 1; Height = $Floor; Plane1 = 19 }   # the player start, facing north
$Tiles['b'] = @{ Plane0 = 108; Flat = 1; Height = $Floor }             # the bridge: its sides from flats sides: 1
$Tiles['w'] = @{ Plane0 = 108; Flat = 40; Plane1 = 980 }               # the stone pit: no block, floor 40 at the bottom
$Tiles['c'] = @{ Plane0 = 108; Flat = 41; Plane1 = 980 }               # the cloud pit: no block, floor sky 41 at the bottom
$Tiles['g'] = @{ Plane0 = 108; Flat = 42; Height = 0x0202 }            # the grate: a block on story 2 alone
$Tiles['P'] = @{ Plane0 = 1 }                                          # a pillar, the ground to story 3
$planes = New-MapPlanes
Add-MapLayout $planes $Level $Tiles 24 10
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Pits Demo' $planes
