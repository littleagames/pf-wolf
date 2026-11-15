# Builds examples/mods/movement-demo: its floors (flats/*.png: ice, mud, the conveyor's belts, the
# wind tunnel's vents and the boost pads) and its level (maps/MAP01.wad). Run it after changing
# any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-movement-demo.ps1
#
# What each floor does is scripts/movement.lua's (its floors and tags tables); the conveyor's
# belts scroll by game-info.yaml's scrollers, at the speed the script carries them.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'movement-demo' @('flats', 'maps')
$a = 2 * [Math]::PI / 64          # sines that repeat every 64 pixels, so the floors tile

# Ice: pale blue, faintly mottled, with long white scratches from skates and boots
$iceRandom = New-Object Random 7
$scratches = @{}
for ($n = 0; $n -lt 9; $n++) {
    $sx = $iceRandom.Next(64); $sy = $iceRandom.Next(64); $len = 8 + $iceRandom.Next(20); $slope = $iceRandom.NextDouble() * 0.8 - 0.4
    for ($i = 0; $i -lt $len; $i++) { $scratches["$((($sx + $i) % 64)),$((([int]($sy + $i * $slope)) % 64 + 64) % 64)"] = 1 }
}
Save-Picture "$modDir\flats\MVICE.png" 64 64 {
    param($x, $y)
    if ($scratches.ContainsKey("$x,$y")) { return New-Color 236 248 255 }
    $v = 10 * [Math]::Sin($a * (2 * $x + $y)) + 8 * [Math]::Sin($a * (3 * $y - $x))
    New-Color (150 + $v) (196 + $v) (230 + $v / 2)
}

# Mud: dark brown, lumpy, with wet patches that catch the light
Save-Picture "$modDir\flats\MVMUD.png" 64 64 {
    param($x, $y)
    $v = [Math]::Sin($a * (2 * $x + 3 * $y)) + [Math]::Sin($a * (4 * $y - 2 * $x + 9)) + 0.5 * [Math]::Sin($a * 6 * ($x - $y))
    if ($v -gt 1.6) { return New-Color 120 100 76 }          # a wet shine
    if ($v -gt 0.4) { return New-Color 84 60 36 }
    if ($v -gt -0.9) { return New-Color 66 46 26 }
    New-Color 48 32 18
}

# The conveyor's belts: dark rubber slats with yellow chevrons pointing the way it runs (east, and
# a mirrored one west; the scrollers move them along)
foreach ($belt in @(@('MVBELTE', 1), @('MVBELTW', -1))) {
    $way = $belt[1]
    Save-Picture "$modDir\flats\$($belt[0]).png" 64 64 {
        param($x, $y)
        $along = if ($way -gt 0) { $x } else { 63 - $x }
        if ($y % 16 -eq 0) { return New-Color 20 20 22 }                         # a slat's seam
        $c = ($along + [Math]::Abs($y % 32 - 16)) % 32
        if ($c -lt 0) { $c += 32 }
        if ($c -lt 6) { return New-Color 230 190 30 }                             # a chevron, pointing on
        if ($x % 8 -eq 0) { return New-Color 44 44 48 }
        New-Color 58 58 62
    }
}

# The wind tunnel's vents: a grille of slots over the dark, the air blowing up through it
Save-Picture "$modDir\flats\MVVENT.png" 64 64 {
    param($x, $y)
    $bx = $x % 8; $by = $y % 16
    if ($by -lt 3 -or $bx -eq 0) { return New-Color 150 156 164 }
    if ($by -eq 3) { return New-Color 90 94 100 }
    New-Color 24 30 40
}

# A boost pad: ice with a red border and big white arrows pointing east
Save-Picture "$modDir\flats\MVBOOST.png" 64 64 {
    param($x, $y)
    if ($x -lt 3 -or $y -lt 3 -or $x -gt 60 -or $y -gt 60) { return New-Color 200 30 30 }
    $c = ($x + [Math]::Abs($y - 32)) % 24
    if ($c -lt 0) { $c += 24 }
    if ($c -lt 7 -and $x -gt 8 -and $x -lt 58) { return New-Color 250 250 250 }
    New-Color 150 196 230
}

# The level. The start room (P, facing north); north of it the ice rink, its guards facing away
# from the way in, pillars to slide round and a cross out in the middle; west, the mud pit, food and ammo in the
# thick of it; east, the conveyor hall, its north lane running east and its south lane west, a
# guard on it; past the hall, the wind tunnel blowing south and the exit at its top; south of
# the start, the ice runway, boost pads to send you down it and a crown at its far end.
$Layout = @(
    '##############################X#####'   # y=0  the exit
    '#iiiiiiiiiiiiiiiiiii#########xxx####'
    '#iiiiiiiiiTiiiiiiiii#########vvv####'   #      the cross out on the ice
    '#iiiGiiiiiiiiiiiGiii#########vvv####'   #      guards, facing north
    '#iiiiiiiiiiiiiiiiiii#########vvv####'
    '#iiiiiiciiiiiciiiiii#########vvv####'   # y=5  pillars
    '#iiiiiiiiiiiiiiiiiii#########vvv####'
    '#iiiiiiiiiiiiiiiiiHi#########vvv####'   #      a guard facing east
    '#iiiiiiiiiiiiiiiiiii#########vvv####'
    '#iiiiiiiiiiiiiiiiiii#########vvv####'
    '#########...#################vvv####'   # y=10
    '#.....#.......#EEEEEEEEEEEEE....####'   #      the hall's east lane
    '#.uuu.#.......#EEEEEEgEEEEEE....####'   #      a guard on the belt, facing west
    '#.uau.........EEEEEEEEEEEEEE....####'   #      the mud pit, ammo in it
    '#.uuu.........WWWWWWWWWWWWWW....####'   #      the hall's west lane
    '#.ufu.#...P...#WWWWWWWWWWWWW....####'   # y=15 food in the mud; the player start
    '#.uuu.###...###WWWWWWWWWWWWW....####'   #      down to the runway
    '#.....###...########################'
    '#iiiiiiiiiibbbiiiiiiiiiiiiiiiiiiii##'   #      the runway, boost pads (b)
    '#iiiiiiiiiibbbiiiiiiiiiiiiiiiiiiiK##'   #      the crown at its end
    '#iiiiiiiiiibbbiiiiiiiiiiiiiiiiiiii##'   # y=20
    '####################################'
)
# Floors (mapdefs/flats.yaml): 10 ice, 11 mud, 12 and 13 the belts east and west, 14 the vents,
# 15 the boost pads. Tags (the tag plane, plane 4): 2 the east lane, 3 the west lane, 4 the wind.
$Ice = @{ Plane0 = 108; Flat = 10 }
$EastBelt = @{ Plane0 = 108; Flat = 12; Tag = 2 }
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 1 }
$Tiles['.'] = @{ Plane0 = 108 }
$Tiles['x'] = @{ Plane0 = 108 }
$Tiles['X'] = @{ Plane0 = 21 }                                  # the elevator switch
$Tiles['P'] = @{ Plane0 = 108; Plane1 = 19 }                    # the player start, facing north
$Tiles['i'] = $Ice
$Tiles['T'] = $Ice + @{ Plane1 = 52 }                           # a cross
$Tiles['K'] = $Ice + @{ Plane1 = 55 }                           # a crown
$Tiles['c'] = $Ice + @{ Plane1 = 30 }                           # a white pillar
$Tiles['G'] = $Ice + @{ Plane1 = 109 }                          # a guard facing north, away from the way in
$Tiles['H'] = $Ice + @{ Plane1 = 108 }                          # a guard facing east, at the wall
$Tiles['u'] = @{ Plane0 = 108; Flat = 11 }
$Tiles['a'] = @{ Plane0 = 108; Flat = 11; Plane1 = 49 }         # a clip
$Tiles['f'] = @{ Plane0 = 108; Flat = 11; Plane1 = 47 }         # food
$Tiles['E'] = $EastBelt
$Tiles['g'] = $EastBelt + @{ Plane1 = 110 }                     # a guard facing west
$Tiles['W'] = @{ Plane0 = 108; Flat = 13; Tag = 3 }
$Tiles['v'] = @{ Plane0 = 108; Flat = 14; Tag = 4 }
$Tiles['b'] = @{ Plane0 = 108; Flat = 15 }
$planes = New-MapPlanes -Fill 1
Add-MapLayout $planes $Layout $Tiles 10 20
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Movement Demo' $planes
