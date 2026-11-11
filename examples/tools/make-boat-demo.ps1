# Builds examples/mods/boat-demo: its floors (flats/*.png), sky (graphics/BDSKY.png), the boat,
# the gun nest and its gun in hand (sprites/*.png), its sounds (sounds/*.wav) and its level
# (maps/MAP01.wad). Run it after changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-boat-demo.ps1 [-MidnightAssault <midnight-assault.pk3>]
#
# The docks, the sky, the boat, the nest, its gun and their sounds are Midnight Assault's, taken
# out of its pk3 under this mod's own names. The river is made here: Midnight Assault's water is
# all at night, and this river runs under a day sky.
#
# The level: a pier in the west, where the boat's moored; a river running east past four guard
# posts on islands either side; and a landing at the east end with a machine-gun nest
# overlooking a yard full of enemies, and the way out past them.

param([string]$MidnightAssault = 'D:\projects\Wolf3D\Midnight Assault\midnight-assault.pk3')

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'boat-demo'
$ma = Open-MidnightAssault $MidnightAssault

#
# FLATS AND SKY
#

# The river: dark green-blue water with ripples. The ripples' sines repeat every 64 pixels, so
# the floor tiles without a seam (and the game-info scroller slides it east as the current).
Save-Picture "$modDir\flats\BDWATER.png" 64 64 {
    param($x, $y)
    $a = 2 * [Math]::PI / 64
    $ripple = [Math]::Sin($a * (2 * $x + 3 * [Math]::Sin($a * 2 * $y))) + 0.6 * [Math]::Sin($a * (5 * $y - 2 * $x))
    if ($ripple -gt 1.25) { return New-Color 120 176 184 }
    New-Color (24 + 10 * $ripple) (64 + 14 * $ripple) (80 + 16 * $ripple)
}

# The docks: Midnight Assault's wooden slats, running east-west
Copy-MAPicture $ma 'WOOD_SLATS.bmp' "$modDir\flats\BDDOCK.png"

# The sky: Midnight Assault's river valley by day, wooded banks and the river winding off
# between them (1024x64, so it goes round the view once)
Copy-MAPicture $ma 'RIVER_VALLEY.png' "$modDir\graphics\BDSKY.png"

#
# SPRITES: Midnight Assault's
#

# The boat: its dinghy, rocking on the water (A-D). Sprites stand on the floor, so it sits at
# the picture's bottom.
foreach ($frame in 'A', 'B', 'C', 'D') { Copy-MAPicture $ma "BOAT${frame}0.bmp" "$modDir\sprites\BDBT${frame}0.png" }
# The gun nest: a ring of sandbags with a machine gun on it
Copy-MAPicture $ma 'NESTA0.bmp' "$modDir\sprites\BDNSA0.png"
# The nest's gun as the view's weapon: A ready, B-C firing (C the flash), D the spent case
foreach ($frame in 'A', 'B', 'C', 'D') { Copy-MAPicture $ma "MGNS${frame}0.bmp" "$modDir\sprites\BDMG${frame}0.png" }

#
# SOUNDS
#

# The boat's motor, swinging the gun round and loading it, and its fire: Midnight Assault's
Copy-MAFile $ma 'boat_enter.wav' "$modDir\sounds\BDMOTOR.wav"
Copy-MAFile $ma 'mgnest_load.wav' "$modDir\sounds\BDCLACK.wav"
Copy-MAFile $ma 'mgnest_fire1.wav' "$modDir\sounds\BDMGUN1.wav"
Copy-MAFile $ma 'mgnest_fire2.wav' "$modDir\sounds\BDMGUN2.wav"
Close-MidnightAssault $ma

# Stepping onto the dock: a hollow knock on planks
$random = New-Object Random 3
$thud = New-Samples 0.3
Add-Noise $thud $random 0 0.9 0.2 0.03
Add-Tone $thud 0 120 1.0 0.07
Add-Tone $thud 0.005 190 0.4 0.05
Save-Wav "$modDir\sounds\BDTHUD.wav" $thud 0.6

#
# THE LEVEL
#

# One character per tile, the top-left corner at tile ($Left, $Top); every tile outside is
# grey stone. '~' is the river, 'd' the docks along it (the only tiles the boat stops at, and
# the only ones a rider gets off onto), '.' dry land.
$Left = 12
$Top = 20
$Layout = @(
    '########################################'   # y=20
    '#####...G...#########...G...h###########'   #      the north guard posts
    '#####.G...a.#########.G..G...###########'
    '#####ddddddd#########dddddddd###########'
    '#dd~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~dd...#'   # y=24 the river
    '#Pdb~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~dd...#'   #      the pier, the boat, the east landing
    '#dd~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~dd...#'
    '#dd~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~dd...#'
    '####dddddd##########dddddd########...n.#'   # y=28 the south guard posts' docks; behind the nest
    '####.g..h.##########..g.a.########wD#N##'   #      the yard door and the gun nest
    '####......##########......##...........#'
    '############################.s..s...g..#'   # y=31 the yard
    '############################...........#'
    '############################s....g...s.#'
    '############################...........#'
    '################################D#######'   # y=35 the door to the exit
    '###############################eeeX#####'
    '###############################eee######'
    '########################################'
)

# Floor codes: area n is 107 + n. The river and its banks are one area, so the posts hear the
# shooting; the yard and the exit room are their own.
$River = 108; $Yard = 109; $ExitRoom = 110
# Flats (the mod's mapdefs/flats.yaml) and the river's tag (its scroller in game-info)
$WaterFlat = 20; $DockFlat = 21; $RiverTag = 1
# Things (the mod's mapdefs/things.yaml)
$Dock = 800; $KeepOut = 801; $Boat = 802; $Nest = 803

$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 1 }                                                           # grey stone
$Tiles['w'] = @{ Plane0 = 17 }                                                          # red brick by the nest
$Tiles['.'] = @{ Plane0 = $River }
$Tiles['~'] = @{ Plane0 = $River; Plane1 = $KeepOut; Flat = $WaterFlat; Tag = $RiverTag }
$Tiles['d'] = @{ Plane0 = $River; Plane1 = $Dock; Flat = $DockFlat }
# The boat's mooring: planks under it, as a tile holds one thing and so can't keep the player
# out too (once the boat's gone, it's somewhere to stand, not river)
$Tiles['b'] = @{ Plane0 = $River; Plane1 = $Boat; Flat = $DockFlat }
$Tiles['P'] = @{ Plane0 = $River; Plane1 = 20 }                                         # player start, facing east
$Tiles['G'] = @{ Plane0 = $River; Plane1 = 111 }                                        # guard facing south
$Tiles['g'] = @{ Plane0 = $River; Plane1 = 109 }                                        # guard facing north
$Tiles['a'] = @{ Plane0 = $River; Plane1 = 49 }                                         # ammo
$Tiles['h'] = @{ Plane0 = $River; Plane1 = 48 }                                         # first aid kit
$Tiles['N'] = @{ Plane0 = $River; Plane1 = $Nest }                                      # the gun nest
$Tiles['n'] = @{ Plane0 = $River }                                                      # behind it
$Tiles['D'] = @{ Plane0 = 91 }                                                          # door (north-south)
$Tiles['s'] = @{ Plane0 = $Yard; Plane1 = 127 }                                         # SS facing north
$Tiles['e'] = @{ Plane0 = $ExitRoom }
$Tiles['X'] = @{ Plane0 = 21 }                                                          # elevator switch

# The yard's own tiles: everything south of the nest row, inside the yard walls
$planes = New-MapPlanes
Add-MapLayout $planes $Layout $Tiles $Left $Top
for ($y = $Top + 10; $y -le $Top + 14; $y++) {
    for ($x = $Left + 28; $x -le $Left + 38; $x++) {
        $i = $y * 64 + $x
        if ($planes[0][$i] -eq $River) { $planes[0][$i] = $Yard }
    }
}
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Boat Demo' $planes
