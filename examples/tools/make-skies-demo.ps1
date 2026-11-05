# Builds examples/mods/skies-demo: its four skies (graphics/*.png), its flats (flats/*.png: grass
# and a see-through lattice) and its level (maps/MAP01.wad). Run it after changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-skies-demo.ps1
#
# One level, several skies at once. A tile's ceiling index on the flat plane (the high byte) can
# open onto a sky of its own (mapdefs/flats.yaml skies), so the level has:
#   the meadow (west)        ceiling 14: a day sky (SDDAY). Its switch turns it to night and back.
#   the hall (middle)        ceiling 3: wood planks (pfwolf-pk3's), a roof between the two
#   the night garden (east)  ceiling 15: a night sky (SDNIGHT); its pergola, ceiling 16, is a
#                            lattice (SDLATICE) with the night sky through its gaps
#   the sky bridge (north)   ceiling 0: no flat and no sky of its own, so the level's own sky
#                            (game-info sky: SDDUSK) shows. Floor 30 opens onto a floor sky of
#                            clouds (SDCLOUDS) below the bridge.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'skies-demo'

#
# SKIES: 512x128, going round the view twice; every sine repeats every 512 pixels
#

# Day: deep blue overhead to pale blue at the horizon, white clouds, the sun, green hills
Save-Picture "$modDir\graphics\SDDAY.png" 512 128 {
    param($x, $y)
    $a = 2 * [Math]::PI * $x / 512
    $hills = 112 + 6 * [Math]::Sin(2 * $a + 1) + 3 * [Math]::Sin(7 * $a) + 1.5 * [Math]::Sin(17 * $a + 2)
    if ($y -gt $hills) { return New-Color (60 + 10 * [Math]::Sin(5 * $a)) (130 + 10 * [Math]::Sin(3 * $a)) 50 }
    # the sun, high in the west (repeating every 256 pixels with the rest)
    $sx = ($x % 256) - 70; $sy = $y - 34
    $d = [Math]::Sqrt($sx * $sx + $sy * $sy)
    if ($d -lt 9) { return New-Color 255 252 210 }
    $t = $y / 127
    $r = 40 + 130 * $t; $g = 90 + 120 * $t; $b = 200 + 50 * $t
    if ($d -lt 22) { $glow = 1 - ($d - 9) / 13; $r += 90 * $glow; $g += 70 * $glow; $b += 20 * $glow }
    # puffy clouds in a band across the middle
    $cloud = [Math]::Sin(3 * $a + 0.08 * $y) + 0.7 * [Math]::Sin(8 * $a - 0.15 * $y + 1) + 0.4 * [Math]::Sin(19 * $a + 0.3 * $y)
    $band = 1 - [Math]::Abs($y - 70) / 30
    if ($band -gt 0 -and $cloud * $band -gt 0.55) {
        $shade = if ($cloud * $band -gt 0.9) { 255 } else { 225 }
        return New-Color $shade $shade ($shade + 5)
    }
    New-Color $r $g $b
}

# Night: black-blue overhead to navy at the horizon, stars, a crescent moon, dark hills
$random = New-Object Random 7
$stars = @{}
for ($i = 0; $i -lt 360; $i++) { $stars["$($random.Next(256)),$($random.Next(100))"] = 140 + $random.Next(116) }
Save-Picture "$modDir\graphics\SDNIGHT.png" 512 128 {
    param($x, $y)
    $a = 2 * [Math]::PI * $x / 512
    $hills = 110 + 7 * [Math]::Sin(3 * $a + 2) + 3 * [Math]::Sin(9 * $a) + 1.5 * [Math]::Sin(21 * $a + 1)
    if ($y -gt $hills) { return New-Color 8 10 16 }
    # the moon: a disc with a second disc taken out of its east side
    $mx = ($x % 256) - 190; $my = $y - 30
    if ($mx * $mx + $my * $my -lt 100 -and ($mx - 5) * ($mx - 5) + ($my + 2) * ($my + 2) -ge 81) { return New-Color 240 236 200 }
    $key = "$($x % 256),$y"
    if ($stars.ContainsKey($key)) { $v = $stars[$key]; return New-Color $v $v ($v + 20) }
    $t = $y / 127
    New-Color (4 + 22 * $t) (6 + 26 * $t) (24 + 60 * $t)
}

# Dusk, the level's own sky: purple overhead to orange at the horizon, the sun half set,
# dark streaks of cloud
Save-Picture "$modDir\graphics\SDDUSK.png" 512 128 {
    param($x, $y)
    $a = 2 * [Math]::PI * $x / 512
    $sx = ($x % 256) - 128; $sy = $y - 127
    if ($sx * $sx + $sy * $sy -lt 196) { return New-Color 255 200 90 }
    $streak = [Math]::Sin(2 * $a + 0.02 * $y) + 0.6 * [Math]::Sin(6 * $a + 1)
    if (($y -gt 60 -and $y -lt 66 -or $y -gt 84 -and $y -lt 88) -and $streak -gt 0.4) { return New-Color 60 10 80 }
    # strong colors from top to horizon (muted in-betweens snap to grey in the palette):
    # purple, magenta, red, orange
    $stops = @(@(70, 10, 110), @(150, 20, 130), @(220, 50, 60), @(250, 120, 30), @(255, 180, 60))
    $f = $y / 127 * ($stops.Count - 1)
    $i = [Math]::Min([int][Math]::Floor($f), $stops.Count - 2)
    $u = $f - $i
    $c0 = $stops[$i]; $c1 = $stops[$i + 1]
    New-Color ($c0[0] + ($c1[0] - $c0[0]) * $u) ($c0[1] + ($c1[1] - $c0[1]) * $u) ($c0[2] + ($c1[2] - $c0[2]) * $u)
}

# Clouds far below the bridge (a floor sky: its top row at the horizon, running down): the
# dusk's orange haze at the horizon, then a sea of cloud, its tops lit orange and white, with
# deep blue showing between. Muted purples snap to grey in the palette, so it keeps to strong
# oranges, whites and blues.
Save-Picture "$modDir\graphics\SDCLOUDS.png" 512 96 {
    param($x, $y)
    $a = 2 * [Math]::PI * $x / 512
    if ($y -lt 6) { return New-Color 248 150 60 }
    # the puffs get bigger the nearer they are (the lower in the picture)
    $scale = 1 + $y / 24
    $bank = [Math]::Sin(6 * $a / $scale * 2 + 0.5 * $y / $scale) + 0.6 * [Math]::Sin(13 * $a / $scale * 2 - 0.7 * $y / $scale + 2)
    if ($bank -gt 1.0) { return New-Color 255 236 200 }
    if ($bank -gt 0.55) { return New-Color 250 190 120 }
    if ($bank -gt 0.1) { return New-Color 220 120 70 }
    $t = $y / 95
    New-Color (30 - 20 * $t) (50 - 30 * $t) (150 - 60 * $t)
}

#
# FLATS: 64x64, one a tile
#

# Grass: green, speckled
$random = New-Object Random 3
Save-Picture "$modDir\flats\SDGRASS.png" 64 64 {
    param($x, $y)
    $n = $random.Next(100)
    if ($n -lt 12) { return New-Color 90 160 60 }
    if ($n -lt 30) { return New-Color 40 100 30 }
    New-Color 56 124 40
}

# A wooden lattice: beams every 16 texels both ways, clear between (a pixel left clear is
# palette index 0xFF in the game: see-through, so the sky behind shows)
Save-Picture "$modDir\flats\SDLATICE.png" 64 64 {
    param($x, $y)
    $onx = ($x % 16) -lt 4; $ony = ($y % 16) -lt 4
    if (!$onx -and !$ony) { return $null }
    # the beams one way pass over the other's
    if ($onx -and $ony) { return New-Color 140 96 52 }
    $edge = if ($onx) { $x % 16 } else { $y % 16 }
    if ($edge -eq 0) { return New-Color 150 104 58 }
    if ($edge -eq 3) { return New-Color 70 46 24 }
    New-Color 110 74 40
}

#
# THE LEVEL
#

# Flat plane values: floor index + ceiling index * 256
$Meadow = 14 * 256 + 20         # day sky over grass
$Night = 15 * 256 + 20          # night sky over grass
$Pergola = 16 * 256 + 20        # the lattice, night sky through it, over grass
$Hall = 3 * 256 + 1             # wood planks over grey stone (pfwolf-pk3's)
$Bridge = 0 * 256 + 1           # the level's own sky over grey stone
$Chasm = 0 * 256 + 30           # the level's own sky over the clouds below
$KeepOut = 950

# 35 wide: the meadow (x 15-23), the hall (25-33) and the night garden (35-47), with the sky
# bridge north of the hall. The walls between the hall and the yards are pillars with two wide
# openings each, so both skies show from the hall.
$Level = @(
    '###############X###################'   # y=24 the elevator switch
    '###########ppppppppp###############'   #      the platform
    '###########ccccbcccc###############'   #      the bridge over the clouds
    '###########ccccbcccc###############'
    '###########ccccbcccc###############'
    '###########ccccbcccc###############'
    'WWWWWWWWWWWBBBBdBBBBBNNNNNNNNNNNNNN'   # y=30 the bridge's doorway
    'WmmmmmmmmmBhhhhhhhhhBnnnnnnnnnnnnnN'
    'WmmTmmmmmmghlhhhhhlhgnnnLLLLLnnnnnN'   #      the pergola, east
    'WmmmmmmmmmghhhhhhhhhgnnnLLLLLnnvnnN'
    'WmmmmTmmmmBhhhhPhhhhBnnnLLwLLnnnnnN'   # y=34 the player start, facing north
    'WmmmmmmmmmghhhhhhhhhgnnnLLLLLnnnnnN'
    'WmTmmmmmTmghlhhhhhlhgnnnnnnnnnvnnnN'
    'WmmmmmmmmmBhhhhhhhhhBnnnnnnnnnnnnnN'
    'WWWWWSWWWWWBBBBBBBBBBNNNNNNNNNNNNNN'   # y=38 the meadow's switch
)

$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 1 }                                               # grey stone
$Tiles['W'] = @{ Plane0 = 12 }                                              # wood, round the meadow
$Tiles['B'] = @{ Plane0 = 17 }                                              # red brick, the hall
$Tiles['N'] = @{ Plane0 = 8 }                                               # blue stone, round the night garden
$Tiles['X'] = @{ Plane0 = 21 }                                              # the elevator switch
$Tiles['S'] = @{ Plane0 = 170; Tag = 1 }                                    # day/night switch, tag 1 with the meadow
$Tiles['m'] = @{ Plane0 = 108; Flat = $Meadow; Tag = 1; Zone = 1 }          # the meadow
$Tiles['T'] = @{ Plane0 = 108; Flat = $Meadow; Tag = 1; Zone = 1; Plane1 = 31 }     # a green plant
$Tiles['h'] = @{ Plane0 = 108; Flat = $Hall }                               # the hall
$Tiles['l'] = @{ Plane0 = 108; Flat = $Hall; Plane1 = 26 }                  # a floor lamp
$Tiles['P'] = @{ Plane0 = 108; Flat = $Hall; Plane1 = 19 }                  # player start, facing north
$Tiles['g'] = @{ Plane0 = 108; Flat = $Hall }                               # an opening in the hall's side walls
$Tiles['d'] = @{ Plane0 = 108; Flat = $Hall }                               # the doorway to the bridge
$Tiles['n'] = @{ Plane0 = 108; Flat = $Night; Zone = 2 }                    # the night garden
$Tiles['v'] = @{ Plane0 = 108; Flat = $Night; Zone = 2; Plane1 = 35 }       # a vase
$Tiles['L'] = @{ Plane0 = 108; Flat = $Pergola; Zone = 2 }                  # under the pergola
$Tiles['w'] = @{ Plane0 = 108; Flat = $Pergola; Zone = 2; Plane1 = 59 }     # the well
$Tiles['p'] = @{ Plane0 = 108; Flat = $Bridge }                             # the platform
$Tiles['b'] = @{ Plane0 = 108; Flat = $Bridge }                             # the bridge
$Tiles['c'] = @{ Plane0 = 108; Flat = $Chasm; Plane1 = $KeepOut }           # the clouds: no one walks there
$planes = New-MapPlanes
Add-MapLayout $planes $Level $Tiles 14 24
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Skies Demo' $planes
