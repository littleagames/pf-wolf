# Builds examples/mods/stealth-demo: the coat rack, its uniform and the papers in hand
# (sprites/*.png), the papers' rustle (sounds/SDPAPER.wav) and the level (maps/MAP01.wad). Run
# it after changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-stealth-demo.ps1
#
# The level: a storeroom with a uniform on a coat rack; a corridor ringing the barracks, two
# guards patrolling it; the barracks, guards with their backs to the door and a dog; and a
# checkpoint to the north with an officer, who isn't fooled by papers.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'stealth-demo'

# The coat rack: a stand with a brown uniform and a cap hung on it (A), and bare (B)
foreach ($frame in 'A', 'B') {
    Save-Drawing "$modDir\sprites\SDRK${frame}0.png" 64 64 {
        param($g)
        Add-Rect $g @(70, 46, 26) 30 14 4 48                                 # the pole
        Add-Rect $g @(70, 46, 26) 22 60 20 3                                 # its feet
        Add-Rect $g @(70, 46, 26) 24 14 16 3                                 # the hooks' bar
        if ($frame -eq 'A') {
            Add-Poly $g @(150, 110, 60) @(22, 18, 42, 18, 46, 50, 18, 50)    # the coat
            Add-Rect $g @(120, 86, 46) 30 18 4 32
            foreach ($b in 0..3) { Add-Rect $g @(220, 200, 80) 35 (22 + 7 * $b) 2 2 }
            Add-Ellipse $g @(110, 90, 50) 23 6 18 8                          # the cap
            Add-Rect $g @(40, 30, 20) 21 11 22 2
        }
    }
}

# The papers in hand, held up at the bottom right; and held out (shown)
Save-Drawing "$modDir\sprites\SDPPA0.png" 64 64 {
    param($g)
    Add-Poly $g @(236, 230, 210) @(34, 40, 52, 38, 54, 60, 36, 62)
    Add-Rect $g @(140, 30, 30) 40 44 8 6                                     # the stamp
    foreach ($l in 0..2) { Add-Rect $g @(120, 120, 120) 38 (53 + 3 * $l) 12 1 }
    Add-Poly $g @(232, 176, 128) @(30, 50, 40, 48, 42, 64, 28, 64)
}
Save-Drawing "$modDir\sprites\SDPPB0.png" 64 64 {
    param($g)
    Add-Poly $g @(236, 230, 210) @(20, 26, 44, 24, 46, 52, 22, 54)
    Add-Rect $g @(140, 30, 30) 28 30 10 8
    foreach ($l in 0..3) { Add-Rect $g @(120, 120, 120) 25 (42 + 3 * $l) 16 1 }
    Add-Poly $g @(232, 176, 128) @(16, 44, 28, 42, 30, 64, 14, 64)
}

# The papers' rustle
$random = New-Object Random 8
$s = New-Samples 0.4
foreach ($k in 0..5) { Add-Noise $s $random (0.05 * $k) 0.6 0.6 0.02 0.04 }
Save-Wav "$modDir\sounds\SDPAPER.wav" $s 0.4

# The level. Every open tile is light zone 1 (plane 5), which the script's alarm turns red.
$Layout = @(
    '##########################'   # y=20
    '###########kkOkkkExxxxX###'   #      the checkpoint, its officer; the exit
    '###########kkkkkk#xxxx####'
    '#############D############'
    '########1rrrprrrrrr2######'   #      the ring corridor; a patrolling guard
    '########r##########r######'   # y=25
    '########r#bbbbbbbb#r######'   #      the barracks
    '########r#bnbbbbnb#r######'   #      guards facing the walls
    '########r#bbbbbbbb#r######'
    '########r#bbbbbbbb#r######'
    '########r#bbbbbbbb#r######'   # y=30
    '########r#bbnbbwbb#r######'
    '########r#bbbbbbbb#r######'
    '########r#bdbbbbbb#r######'   #      the dog
    '##sssRs#r####D#####r######'   #      the storeroom's coat rack
    '##sssssE4rrrrrrqrrr3######'   # y=35 the other patrolling guard
    '##sssss###################'
    '##sPsss###################'   #      the player start, facing north
    '##########################'
)
# The patrol arrows at the ring's corners turn the patrolling guards round it: 1 east, 2 south,
# 3 west, 4 north (pfwolf-pk3's mapdefs/wolf3d/patrolpoints.yaml)
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 1 }
$Tiles['s'] = @{ Plane0 = 108; Zone = 1 }
$Tiles['P'] = @{ Plane0 = 108; Zone = 1; Plane1 = 19 }
$Tiles['R'] = @{ Plane0 = 108; Zone = 1; Plane1 = 890 }        # the coat rack
$Tiles['r'] = @{ Plane0 = 109; Zone = 1 }
$Tiles['1'] = @{ Plane0 = 109; Zone = 1; Plane1 = 90 }
$Tiles['2'] = @{ Plane0 = 109; Zone = 1; Plane1 = 96 }
$Tiles['3'] = @{ Plane0 = 109; Zone = 1; Plane1 = 94 }
$Tiles['4'] = @{ Plane0 = 109; Zone = 1; Plane1 = 92 }
$Tiles['p'] = @{ Plane0 = 109; Zone = 1; Plane1 = 112 }        # a guard patrolling, heading east
$Tiles['q'] = @{ Plane0 = 109; Zone = 1; Plane1 = 114 }        # one heading west
$Tiles['b'] = @{ Plane0 = 110; Zone = 1 }
$Tiles['n'] = @{ Plane0 = 110; Zone = 1; Plane1 = 109 }        # a guard facing north
$Tiles['w'] = @{ Plane0 = 110; Zone = 1; Plane1 = 108 }        # one facing east
$Tiles['d'] = @{ Plane0 = 110; Zone = 1; Plane1 = 135 }        # a dog facing north
$Tiles['k'] = @{ Plane0 = 111; Zone = 1 }
$Tiles['O'] = @{ Plane0 = 111; Zone = 1; Plane1 = 117 }        # the officer, facing north
$Tiles['x'] = @{ Plane0 = 112; Zone = 1 }
$Tiles['X'] = @{ Plane0 = 21 }
$Tiles['D'] = @{ Plane0 = 91 }
$Tiles['E'] = @{ Plane0 = 90 }
$planes = New-MapPlanes
Add-MapLayout $planes $Layout $Tiles 0 20
# One guard in the barracks faces west, the one at col 12 row 11
$planes[1][31 * 64 + 12] = 110
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Stealth Demo' $planes
