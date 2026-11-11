# Builds examples/mods/mission-demo: the radio wall and its wrecked stage (textures/*.png), the
# documents (sprites/MDDOA0.png), the HUD's panel (graphics/MDPANEL.png), the radio's sound as
# it's shot up (sounds/MDZAP.wav), the documents' (sounds/MDDOCS.wav) and the level
# (maps/MAP01.wad). Run it after changing any:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-mission-demo.ps1 [-MidnightAssault <midnight-assault.pk3>]
#
# The radio wall, the documents and their sound are Midnight Assault's, taken out of its pk3
# under this mod's own names; the radio wrecked is its wall with the damage drawn over it here.
# The HUD's panel and the radio's zap are made here.
#
# The mission: steal the documents from the office, wreck the two radios in the comms room, and
# (if you can) kill the two officers, then reach the exit before reinforcements arrive. The
# objectives, the time limit and the HUD are in game-info.yaml and scripts/mission.lua.

param([string]$MidnightAssault = 'D:\projects\Wolf3D\Midnight Assault\midnight-assault.pk3')

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'mission-demo'
$ma = Open-MidnightAssault $MidnightAssault

# The radio set: Midnight Assault's comms console, a wall of grilles, dials and switches
Copy-MAPicture $ma 'Wall-095.bmp' "$modDir\textures\MDRADIO.png"

# Shot up: the same console, scorched round a smashed hole in its middle with wires hanging out
$wreck = Read-MAPicture $ma 'Wall-095.bmp'
for ($y = 0; $y -lt 64; $y++) {
    for ($x = 0; $x -lt 64; $x++) {
        $d = [Math]::Sqrt(($x - 28) * ($x - 28) + ($y - 34) * ($y - 34) * 1.4)
        if ($d -ge 26) { continue }
        $k = if ($d -lt 16) { 0.3 } else { 0.3 + 0.7 * ($d - 16) / 10 }      # blackest at the middle
        $c = $wreck.GetPixel($x, $y)
        $wreck.SetPixel($x, $y, [Drawing.Color]::FromArgb(255, [int]($c.R * $k), [int]($c.G * $k), [int]($c.B * $k)))
    }
}
$g = [Drawing.Graphics]::FromImage($wreck)
$g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::None
$g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::Half
Add-Poly $g @(10, 9, 8) @(16, 24, 26, 20, 40, 23, 42, 36, 34, 46, 20, 43, 14, 34)    # the hole
Add-Poly $g @(54, 50, 46) @(26, 20, 30, 22, 22, 27)                                    # a bent flap of the panel
Add-Rect $g @(200, 40, 30) 22 36 1 15                                                  # the wires
Add-Rect $g @(40, 120, 200) 28 38 1 18
Add-Rect $g @(220, 200, 40) 34 37 1 11
Add-Rect $g @(200, 40, 30) 38 36 1 7
$g.Dispose()
$wreck.Save("$modDir\textures\MDRADIOX.png", [Drawing.Imaging.ImageFormat]::Png)
$wreck.Dispose()
"Wrote $modDir\textures\MDRADIOX.png (Midnight Assault's Wall-095.bmp, wrecked)"

# The documents: Midnight Assault's papers, spread on the floor, and the sound of taking them
Copy-MAPicture $ma 'LOGBB0.bmp' "$modDir\sprites\MDDOA0.png"
Copy-MAFile $ma 'pickup_files.wav' "$modDir\sounds\MDDOCS.wav"
Close-MidnightAssault $ma

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
