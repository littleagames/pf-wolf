# Builds examples/mods/mission-demo: the radio wall and its wrecked stage (textures/*.png), the
# documents (sprites/MDDOA0.png), the HUD's panel (graphics/MDPANEL.png), the radio's sound as
# it's shot up (sounds/MDZAP.wav) and the level (maps/MAP01.wad). Run it after changing any:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-mission-demo.ps1
#
# The mission: steal the documents from the office, wreck the two radios in the comms room, and
# (if you can) kill the two officers, then reach the exit before reinforcements arrive. The
# objectives, the time limit and the HUD are in game-info.yaml and scripts/mission.lua.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'mission-demo'

# The radio set: a grey steel cabinet with dials, a speaker grille and two rows of lamps
Save-Drawing "$modDir\textures\MDRADIO.png" 64 64 {
    param($g)
    Add-Rect $g @(84, 88, 96) 0 0 64 64
    Add-Rect $g @(60, 64, 70) 0 0 64 3
    Add-Rect $g @(60, 64, 70) 0 61 64 3
    Add-Rect $g @(44, 46, 52) 6 8 52 30                                   # the front panel
    foreach ($k in 0..2) { Add-Ellipse $g @(200, 200, 190) (10 + 16 * $k) 13 11 11; Add-Rect $g @(30, 30, 30) (15 + 16 * $k) 14 1 5 }
    foreach ($k in 0..5) { Add-Rect $g $(if ($k % 2) { @(40, 220, 60) } else { @(230, 180, 40) }) (10 + 8 * $k) 30 4 3 }
    for ($y = 42; $y -lt 58; $y += 3) { Add-Rect $g @(36, 38, 42) 8 $y 48 1 }    # the grille
}
# Shot up: the panel smashed and blackened, wires hanging out
Save-Drawing "$modDir\textures\MDRADIOX.png" 64 64 {
    param($g)
    Add-Rect $g @(70, 72, 78) 0 0 64 64
    Add-Rect $g @(24, 22, 22) 6 8 52 30
    Add-Poly $g @(12, 10, 10) @(14, 10, 50, 12, 40, 36, 20, 30)
    Add-Ellipse $g @(200, 200, 190) 42 13 11 11
    Add-Rect $g @(200, 40, 30) 20 36 1 12
    Add-Rect $g @(40, 120, 200) 30 36 1 16
    Add-Rect $g @(220, 200, 40) 36 36 1 9
    for ($y = 42; $y -lt 58; $y += 3) { Add-Rect $g @(36, 38, 42) 8 $y 48 1 }
    Add-Ellipse $g @(30, 26, 24) 4 40 20 18
}

# The documents: a manila folder, a red stamp, papers sticking out; on the floor
Save-Drawing "$modDir\sprites\MDDOA0.png" 64 64 {
    param($g)
    Add-Poly $g @(236, 236, 228) @(20, 50, 44, 48, 46, 56, 22, 58)
    Add-Poly $g @(214, 182, 110) @(16, 52, 46, 52, 48, 62, 14, 62)
    Add-Rect $g @(190, 30, 24) 24 55 12 4
}

# The HUD's panel: a dark plate the objectives are written on, its corners clipped
Save-Picture "$modDir\graphics\MDPANEL.png" 100 32 {
    param($x, $y)
    if (($x + $y) -lt 3 -or ($x + (31 - $y)) -lt 3 -or ((99 - $x) + $y) -lt 3 -or ((99 - $x) + (31 - $y)) -lt 3) { return $null }
    if ($x -eq 0 -or $y -eq 0 -or $x -eq 99 -or $y -eq 31) { return New-Color 120 110 70 }
    New-Color 24 22 18
}

# A radio shot up: a crackle of sparks and a dying hum
$random = New-Object Random 5
$zap = New-Samples 0.7
Add-Noise $zap $random 0 1.0 0.9 0.15
Add-Tone $zap 0 { param($t) 120 - 80 * $t } 0.6 0.4
foreach ($k in 0..7) { Add-Noise $zap $random (0.05 + 0.07 * $k) 0.7 0.95 0.01 0.02 }
Save-Wav "$modDir\sounds\MDZAP.wav" $zap 0.6

# The level: the start hall in the south; the comms room west (the radios are its west wall),
# the office east (the documents on the desk, an officer); north, the yard with the second
# officer, where the reinforcements come in (r), and the exit off it to the east (the marker,
# m, is what the HUD's compass points to).
$Layout = @(
    '################################'   # y=16
    '################################'
    '################################'
    '######yryyyyyyyyyyyyyyyyry######'   #      reinforcements come in at r
    '######yyyyyyyyyyyygyyyyyyy######'   # y=20 the yard
    '######yyyyyyOyyyyyyyyyyyyy#xxx##'   #      the second officer
    '######yyyyyyyyyyyyyyyyyyyyExxmX#'   #      the exit
    '######yyyyyyyyyyyyyyyyyyyy#xxx##'
    '######yyygyyyyyyyyyyyygyyy######'
    '######yyyyyyyyyyyyyyyyyyyy######'   # y=25
    '######yyyyyyyyyyryyyyyyyyy######'
    '###############h################'
    '###cccccccc####h#####oooooooo###'   #      the comms room, the office
    '##Rcccgcccc####h#####oooooTFo###'   #      a radio; the desk and the documents
    '###cccccccc####D#####oooooooo###'   # y=30
    '###cccccccc#hhhhhhhh#ooooOooo###'   #      the first officer
    '##Rcccccccc#hhhhhhhh#oooooooo###'   #      the other radio
    '###ccccccccEhhhhhhhhEoogooooo###'
    '###ctcccccc#hhhhhhhh#oooooooo###'
    '############hhhhhhhh############'   # y=35 the start hall
    '############hhhPhhhh############'   #      the player start, facing north
    '################################'
)
$Hall = 108; $Comms = 109; $Office = 110; $Yard = 111; $Exit = 112
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 1 }
$Tiles['h'] = @{ Plane0 = $Hall }
$Tiles['P'] = @{ Plane0 = $Hall; Plane1 = 19 }
$Tiles['c'] = @{ Plane0 = $Comms }
$Tiles['t'] = @{ Plane0 = $Comms; Plane1 = 25 }            # table and chairs
$Tiles['R'] = @{ Plane0 = 144 }                            # a radio (walls.yaml), facing east
$Tiles['o'] = @{ Plane0 = $Office }
$Tiles['T'] = @{ Plane0 = $Office; Plane1 = 36 }           # the desk
$Tiles['F'] = @{ Plane0 = $Office; Plane1 = 881 }          # the documents
$Tiles['O'] = @{ Plane0 = $Office; Plane1 = 880 }          # an officer, an objective
$Tiles['y'] = @{ Plane0 = $Yard }
$Tiles['r'] = @{ Plane0 = $Yard; Plane1 = 883 }            # where reinforcements come in
$Tiles['x'] = @{ Plane0 = $Exit }
$Tiles['m'] = @{ Plane0 = $Exit; Plane1 = 882 }            # the exit marker
$Tiles['X'] = @{ Plane0 = 21 }
$Tiles['g'] = @{ Plane0 = $Office; Plane1 = 108 }          # a guard (its area fixed below)
$Tiles['D'] = @{ Plane0 = 91 }
$Tiles['E'] = @{ Plane0 = 90 }
$planes = New-MapPlanes
Add-MapLayout $planes $Layout $Tiles 0 16
# Guards and the second officer stand in whichever room they're drawn in: give them its area
for ($row = 0; $row -lt $Layout.Count; $row++) {
    for ($col = 0; $col -lt $Layout[$row].Length; $col++) {
        if ([string]$Layout[$row][$col] -cnotin 'g', 'O') { continue }
        $left = [string]$Layout[$row][$col - 1]
        $area = @{ 'c' = $Comms; 'o' = $Office; 'y' = $Yard; 'h' = $Hall }[$left]
        if ($area) { $planes[0][(16 + $row) * 64 + $col] = $area }
    }
}
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Mission Demo' $planes
