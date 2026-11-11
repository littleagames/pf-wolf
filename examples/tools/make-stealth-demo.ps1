# Builds examples/mods/stealth-demo: the coat rack, its uniform and the papers in hand
# (sprites/*.png), the papers' rustle (sounds/SDPAPER.wav) and the level (maps/MAP01.wad). Run
# it after changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-stealth-demo.ps1 [-MidnightAssault <midnight-assault.pk3>]
#
# The pictures and the rustle are Midnight Assault's, taken out of its pk3 under this mod's own
# names.
#
# The level: a storeroom with a uniform on a coat rack; a corridor ringing the barracks, two
# guards patrolling it; the barracks, guards with their backs to the door and a dog; and a
# checkpoint to the north with an officer, who isn't fooled by papers.

param([string]$MidnightAssault = 'D:\projects\Wolf3D\Midnight Assault\midnight-assault.pk3')

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'stealth-demo'
$ma = Open-MidnightAssault $MidnightAssault

# The coat rack with the uniform and cap hung on it (A, Midnight Assault's UNIF), and bare (B,
# its RACK)
Copy-MAPicture $ma 'UNIFA0.bmp' "$modDir\sprites\SDRKA0.png"
Copy-MAPicture $ma 'RACKA0.bmp' "$modDir\sprites\SDRKB0.png"

# The papers in hand: A held low, B-C raised and held out (shown), D-E put away
foreach ($frame in 'A', 'B', 'C', 'D', 'E') { Copy-MAPicture $ma "PPRS${frame}0.bmp" "$modDir\sprites\SDPP${frame}0.png" }

# The papers' rustle
Copy-MAFile $ma 'id_papers_show.wav' "$modDir\sounds\SDPAPER.wav"
Close-MidnightAssault $ma

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
