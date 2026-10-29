# Builds examples/mods/boat-demo: its floors (flats/*.png), sky (graphics/BDSKY.png), the boat
# and gun nest sprites (sprites/*.png), its sounds (sounds/*.wav) and its level (maps/MAP01.wad).
# Run it after changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-boat-demo.ps1
#
# The level: a pier in the west, where the boat's moored; a river running east past four guard
# posts on islands either side; and a landing at the east end with a machine-gun nest
# overlooking a yard full of enemies, and the way out past them.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'boat-demo'

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

# The docks: planks running east-west, 8 pixels wide with dark gaps between, and nail heads
$grain = New-Object Random 7
Save-Picture "$modDir\flats\BDDOCK.png" 64 64 {
    param($x, $y)
    $plank = [Math]::Floor($y / 8)
    if ($y % 8 -eq 7) { return New-Color 36 22 12 }
    if (($x - 13 * $plank) % 32 -eq 3 -and $y % 8 -in 2, 4) { return New-Color 40 40 44 }
    $shade = 8 * [Math]::Sin(($x + 17 * $plank) * 0.7) + $grain.Next(-6, 7) - 6 * ($plank % 2)
    New-Color (128 + $shade) (88 + $shade) (52 + $shade / 2)
}

# The sky: blue at the top, paler toward the horizon, with soft clouds; 512 wide so it goes
# round the view twice, and the clouds' sines repeat every 512 pixels so it joins up
Save-Picture "$modDir\graphics\BDSKY.png" 512 128 {
    param($x, $y)
    $t = $y / 127
    $a = 2 * [Math]::PI * $x / 512
    $cloud = [Math]::Sin(3 * $a + 0.08 * $y) + 0.7 * [Math]::Sin(7 * $a - 0.05 * $y + 1) + 0.4 * [Math]::Sin(13 * $a + 2) -
        2.2 * [Math]::Abs($t - 0.45) * 2
    # a distant tree line along the bottom
    $trees = 110 + 6 * [Math]::Sin(9 * $a) + 3 * [Math]::Sin(31 * $a + 1) + 2 * [Math]::Sin(67 * $a)
    if ($y -gt $trees) { return New-Color 34 58 40 }
    $r = 70 + 110 * $t; $g = 120 + 90 * $t; $b = 200 + 40 * $t
    if ($cloud -gt 0.4) { $k = [Math]::Min(1, ($cloud - 0.4) * 1.5); return New-Color ($r + (236 - $r) * $k) ($g + (238 - $g) * $k) ($b + (242 - $b) * $k) }
    New-Color $r $g $b
}

#
# SPRITES
#

# The boat, side on: a red hull with a white stripe, its bow to the right, a windscreen and an
# outboard motor at the stern. Sprites stand on the floor, so it sits at the picture's bottom.
Save-Picture "$modDir\sprites\BDBTA0.png" 64 64 {
    param($x, $y)
    # the hull: from the gunwale (y 44) down to the keel (y 57), the bow sweeping up at the right
    if ($y -ge 44 -and $y -le 57) {
        $depth = ($y - 44) / 13
        $left = 6 + 4 * $depth
        $right = 61 - 16 * $depth * $depth
        if ($x -ge $left -and $x -le $right) {
            if ($y -le 45) { return New-Color 236 236 228 }
            if ($y -in 49, 50) { return New-Color 236 236 228 }
            return New-Color (176 - 40 * $depth) 24 20
        }
    }
    # spray along the waterline
    if ($y -ge 58 -and $y -le 60 -and ($x * 7 + $y * 3) % 5 -lt 2 -and $x -ge 8 -and $x -le 50) { return New-Color 200 224 232 }
    # the windscreen
    if ($y -ge 36 -and $y -le 43 -and $x -ge 30 -and $x -le 40 - ($y - 36) / 2 -and $x -ge 30 + (43 - $y) / 3) {
        if ($x -eq 30 -or $y -eq 36) { return New-Color 60 60 64 }
        return New-Color 150 196 220
    }
    # the outboard motor at the stern, hanging into the water
    if ($x -ge 1 -and $x -le 6 -and $y -ge 36 -and $y -le 46) { return New-Color 52 52 56 }
    if ($x -ge 3 -and $x -le 4 -and $y -ge 47 -and $y -le 58) { return New-Color 72 72 76 }
    $null
}

# The gun nest: a ring of sandbags with a machine gun on a pivot, its barrel to the right
Save-Picture "$modDir\sprites\BDNSA0.png" 64 64 {
    param($x, $y)
    # the barrel, with a cooling jacket and the gun's body behind it
    if ($y -ge 33 -and $y -le 36 -and $x -ge 30 -and $x -le 60) {
        if ($x -ge 34 -and $x -le 48 -and ($x % 3) -eq 0) { return New-Color 20 20 22 }
        return New-Color (60 + 20 * ($y -eq 33)) (60 + 20 * ($y -eq 33)) 64
    }
    if ($y -ge 30 -and $y -le 39 -and $x -ge 18 -and $x -le 30) { return New-Color (44 + 30 * ($y -eq 30)) 44 48 }
    if ($y -ge 36 -and $y -le 42 -and $x -ge 14 -and $x -le 18) { return New-Color 70 48 30 }      # the stock
    if ($y -ge 40 -and $y -le 46 -and $x -ge 23 -and $x -le 25) { return New-Color 50 50 54 }      # the pivot
    # the sandbags: three rows of rounded bags, staggered
    if ($y -ge 46) {
        $row = [Math]::Floor(($y - 46) / 6)
        $bx = ($x + 5 * ($row % 2)) % 10
        $by = ($y - 46) % 6
        $inside = (($bx - 4.5) / 5) * (($bx - 4.5) / 5) + (($by - 2.5) / 3.2) * (($by - 2.5) / 3.2)
        if ($x -lt 2 -or $x -gt 61) { return $null }
        if ($inside -gt 1) { return New-Color 92 78 50 }
        return New-Color (182 - 40 * $inside) (160 - 36 * $inside) (110 - 30 * $inside)
    }
    $null
}

#
# SOUNDS
#

# The boat's motor starting: a puttering that quickens and settles into a steady drone
$random = New-Object Random 3
$motor = New-Samples 1.4
$sr = $script:SampleRate
for ($i = 0; $i -lt $motor.Length; $i++) {
    $t = $i / $sr
    $rate = 5 + 13 * [Math]::Min(1, $t / 0.9)                       # firings a second
    $pulse = [Math]::Pow([Math]::Max(0, [Math]::Sin(2 * [Math]::PI * $rate * $t * (1 + $t / 3))), 6)
    $tone = [Math]::Sin(2 * [Math]::PI * 55 * $t) + 0.5 * [Math]::Sin(2 * [Math]::PI * 110 * $t) + 0.25 * [Math]::Sin(2 * [Math]::PI * 165 * $t)
    $fade = [Math]::Min(1, (1.4 - $t) / 0.2)
    $motor[$i] = (0.4 + 0.6 * $pulse) * $tone * $fade
}
Add-Noise $motor $random 0 0.25 0.08
Save-Wav "$modDir\sounds\BDMOTOR.wav" $motor 0.55

# Stepping onto the dock: a hollow knock on planks
$thud = New-Samples 0.3
Add-Noise $thud $random 0 0.9 0.2 0.03
Add-Tone $thud 0 120 1.0 0.07
Add-Tone $thud 0.005 190 0.4 0.05
Save-Wav "$modDir\sounds\BDTHUD.wav" $thud 0.6

# Swinging the gun round on its pivot: a short metal clack and a ratchet
$clack = New-Samples 0.35
Add-Noise $clack $random 0 1.0 0.7 0.01
Add-Tone $clack 0 900 0.5 0.03
foreach ($k in 1..4) { Add-Noise $clack $random (0.06 + 0.04 * $k) 0.5 0.8 0.006 0.02 }
Save-Wav "$modDir\sounds\BDCLACK.wav" $clack 0.5

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
