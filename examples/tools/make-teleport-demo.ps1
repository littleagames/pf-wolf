# Builds examples/mods/teleport-demo: its pads, token and textures (sprites/*.png,
# textures/*.png), its sounds (sounds/*.wav) and its level (maps/MAP01.wad). Run it after
# changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-teleport-demo.ps1
#
# The level is rooms with no doors between them: the only ways on are teleporter pads in pairs
# (A_TeleportTag), a ladder (the same action, "beside"), and one-way warp pads (A_Warp) into the
# arena. On the way, a vending machine (A_Vend) trades the tokens lying about for ammo.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'teleport-demo'

#
# PICTURES
#

# A pad on the floor: a glowing ring, its colors given; drawn at the picture's bottom, flat
function Save-Pad([string]$Name, $Rim, $Core) {
    Save-Picture "$modDir\sprites\$Name.png" 64 64 {
        param($x, $y)
        $dx = ($x - 31.5) / 26; $dy = ($y - 57) / 5
        $d = $dx * $dx + $dy * $dy
        if ($d -gt 1) {
            # a shimmer rising off it
            if ($y -lt 52 -and $y -gt 20 -and [Math]::Abs($x - 31.5) -lt 20 -and (($x * 7 + $y * 3) % 23) -eq 0) { return New-Color $Core[0] $Core[1] $Core[2] }
            return $null
        }
        if ($d -gt 0.6) { return New-Color $Rim[0] $Rim[1] $Rim[2] }
        New-Color $Core[0] $Core[1] $Core[2]
    }
}
Save-Pad 'TPPDA0' @(40, 90, 200) @(150, 220, 255)       # the two-way pads: blue
Save-Pad 'TPWPA0' @(200, 90, 20) @(255, 200, 80)        # the one-way warp pads: orange

# A token for the vending machine: a brass disc with a hole
Save-Picture "$modDir\sprites\TPTKA0.png" 64 64 {
    param($x, $y)
    $dx = $x - 31.5; $dy = $y - 54.5
    $r = [Math]::Sqrt($dx * $dx + $dy * $dy)
    if ($r -gt 7) { return $null }
    if ($r -lt 2) { return $null }
    if ($r -gt 6) { return New-Color 140 100 30 }
    New-Color 220 180 60
}

# The vending machine: a red cabinet with a lit window of tins, a coin slot and a tray
Save-Drawing "$modDir\textures\TPVEND.png" 64 64 {
    param($g)
    Add-Rect $g @(150, 24, 24) 0 0 64 64
    Add-Rect $g @(110, 16, 16) 0 0 64 4
    Add-Rect $g @(30, 30, 34) 8 8 34 34
    foreach ($row in 0..2) { foreach ($col in 0..3) { Add-Rect $g @(200, 190, 120) (11 + 8 * $col) (11 + 11 * $row) 5 7 } }
    Add-Rect $g @(60, 60, 64) 48 12 8 16
    Add-Rect $g @(10, 10, 10) 51 15 2 8                      # the coin slot
    Add-Rect $g @(220, 200, 60) 48 32 8 4
    Add-Rect $g @(20, 20, 22) 10 48 30 8                     # the tray
}
# The ladder: wooden rungs fixed to blue stone
Save-Drawing "$modDir\textures\TPLADDR.png" 64 64 {
    param($g)
    Add-Rect $g @(40, 50, 140) 0 0 64 64
    for ($y = 0; $y -lt 64; $y += 16) { Add-Rect $g @(30, 36, 110) 0 $y 64 2 }
    Add-Rect $g @(120, 80, 40) 16 0 5 64
    Add-Rect $g @(120, 80, 40) 43 0 5 64
    for ($y = 4; $y -lt 64; $y += 12) { Add-Rect $g @(150, 100, 52) 16 $y 32 4 }
}

#
# SOUNDS
#

$random = New-Object Random 6
$s = New-Samples 0.6
Add-Tone $s 0 { param($t) 200 + 1800 * $t } 0.7 0 0.6
Add-Noise $s $random 0 0.3 0.9 0.3
Save-Wav "$modDir\sounds\TPZAP.wav" $s 0.5
$s = New-Samples 0.7
foreach ($k in 0..3) { Add-Noise $s $random (0.15 * $k) 0.8 0.15 0.04 0.1; Add-Tone $s (0.15 * $k) 140 0.5 0.05 0.1 }
Save-Wav "$modDir\sounds\TPCLIMB.wav" $s 0.5
$s = New-Samples 0.5
Add-Noise $s $random 0 1.0 0.3 0.06 0.1
Add-Tone $s 0.15 1300 0.4 0.08
Add-Tone $s 0.22 1700 0.4 0.08
Save-Wav "$modDir\sounds\TPVEND.wav" $s 0.5

#
# THE LEVEL
#

$Layout = @(
    '##############################'   # y=20
    '##############################'
    '##eeeeeeeeeee########ddddddd##'   #      the arena; the warp gallery, up the ladder
    '##eegeeeeeeee########dwdwdwd##'   #      three warp pads, all to the arena
    '##eeeeeeeeeee########ddddddd##'
    '##eeeeeGeeeee########ddddddd##'   # y=25 where the warp pads come out: a guard on it
    '##eeeeeeeeeee########ddddddd##'
    '##eeeeeeeeeee########ddddddd##'
    '##egeeeeeegee########ddddddd##'
    '##eeeeeeeeeee###########L#####'   #      the ladder's top
    '#######D######################'   # y=30
    '######xxx#####################'   #      the exit room
    '######xxxX####################'
    '########################L#####'   #      the ladder's foot
    '##aaaaaa####bbbbbb###ccccccc##'   #      the start room, the island room, the gallery
    '##taaapa####bpbbbb###cqccccc##'   # y=35 pads in pairs: p with p, q with q
    '##aaaaaa####bbbbbb###ccccccc##'
    '##taaaaa####bbbbbb###ccccccc##'
    '##ataaaa####bbbbqb###ccccccc##'
    '##aaPaaa####bbbbbb###ccccccc##'   #      the player start, facing north
    '##############V###############'   # y=40 the vending machine
    '##############################'
)

# Triggers (mapdefs/triggers.yaml) on the object plane; the tag plane pairs them up: pads 1 and 2,
# the ladder 3, the warp pads and where they come out 4
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 8 }                                          # blue stone
$Tiles['a'] = @{ Plane0 = 108 }
$Tiles['P'] = @{ Plane0 = 108; Plane1 = 19 }
$Tiles['t'] = @{ Plane0 = 108; Plane1 = 851 }                          # a token
$Tiles['p'] = @{ Plane1 = 850; Tag = 1 }                               # a pad, pair 1 (its area set below)
$Tiles['b'] = @{ Plane0 = 109 }
$Tiles['q'] = @{ Plane1 = 850; Tag = 2 }                               # a pad, pair 2
$Tiles['V'] = @{ Plane0 = 151 }                                        # the vending machine
$Tiles['c'] = @{ Plane0 = 110 }
$Tiles['L'] = @{ Plane0 = 150; Plane1 = 853; Tag = 3 }                 # a ladder end, its use trigger
$Tiles['d'] = @{ Plane0 = 111 }
$Tiles['w'] = @{ Plane0 = 111; Plane1 = 852; Tag = 4 }                 # a warp pad
$Tiles['e'] = @{ Plane0 = 112 }
$Tiles['G'] = @{ Plane0 = 112; Plane1 = 108; Tag = 4 }                 # the warp's way out, a guard on it
$Tiles['g'] = @{ Plane0 = 112; Plane1 = 110 }                          # a guard facing west
$Tiles['D'] = @{ Plane0 = 91 }
$Tiles['x'] = @{ Plane0 = 113 }
$Tiles['X'] = @{ Plane0 = 21 }
$planes = New-MapPlanes -Fill 8
Add-MapLayout $planes $Layout $Tiles 0 20
# A pad's floor is its room's
foreach ($t in (Find-LayoutTiles $Layout 'p' 0 20) + (Find-LayoutTiles $Layout 'q' 0 20)) {
    $planes[0][$t.Index] = $planes[0][$t.Index + 1]
    if ($planes[0][$t.Index] -lt 108) { $planes[0][$t.Index] = $planes[0][$t.Index - 1] }
}
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Teleport Demo' $planes
