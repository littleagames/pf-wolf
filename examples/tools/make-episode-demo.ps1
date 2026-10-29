# Builds examples/mods/episode-demo: the episode's picture on the episode menu
# (graphics/C_EPISODE7.png), its night sky (graphics/NFSKY.png) and its three levels
# (maps/MAP61-MAP63.wad). Run it after changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-episode-demo.ps1
#
# The episode, its levels' order, music and weather, and its texts are YAML and text files
# (game-info.yaml, texts/).

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'episode-demo'

# The episode menu's picture, 48x24 as Wolf3D's own: a castle tower against a moon at night
Save-Picture "$modDir\graphics\C_EPISODE7.png" 48 24 {
    param($x, $y)
    $mx = $x - 34; $my = $y - 7
    if ($mx * $mx + $my * $my -lt 20) { return New-Color 240 236 200 }        # the moon
    if ($x -ge 12 -and $x -le 20 -and $y -ge 6) {                               # the tower
        if ($y -le 8 -and ($x % 3) -eq 0) { return New-Color 10 10 30 }         # battlements
        if ($x -eq 16 -and $y -ge 11 -and $y -le 13) { return New-Color 255 200 80 }    # a lit window
        return New-Color 20 20 36
    }
    if ($y -ge 20) { return New-Color 16 16 28 }
    New-Color (10 + $y) (12 + $y) (60 + 3 * $y)
}

# The night sky: dark blue, stars, a moon low on one side
$stars = New-Object Random 7
$starAt = @{}
for ($i = 0; $i -lt 90; $i++) { $starAt["$($stars.Next(512)),$($stars.Next(90))"] = $true }
Save-Picture "$modDir\graphics\NFSKY.png" 512 128 {
    param($x, $y)
    $mx = $x - 100; $my = $y - 50
    if ($mx * $mx + $my * $my -lt 120) { return New-Color 240 236 210 }
    if ($starAt.ContainsKey("$x,$y")) { return New-Color 220 220 240 }
    New-Color (6 + 0.15 * $y) (8 + 0.15 * $y) (30 + 0.4 * $y)
}

# Shared tiles: walls, the player start, enemies, the exit
function New-Tiles {
    $t = [Collections.Hashtable]::new([StringComparer]::Ordinal)
    $t['#'] = @{ Plane0 = 1 }
    $t['X'] = @{ Plane0 = 21 }
    $t['D'] = @{ Plane0 = 91 }
    $t['E'] = @{ Plane0 = 90 }
    $t
}

# MAP61, the Docks: rain on the dockside (open to the sky), crates, guards; the warehouse
# (roofed) and the elevator past it
$Docks = @(
    '########################'   # y=20
    '##################xxxX##'   #      the elevator
    '##################xxx###'
    '###################E####'
    '#################wwwww##'   #      the warehouse
    '#################wcwww##'   # y=25
    '##dddddddddddddd#wwwww##'   #      the dockside
    '##ddddddgddddodd#wwwww##'
    '##dddooddddddddd#wwkww##'
    '##ddddddddddgdddDwwwww##'
    '##dddddddddddddd#wwwww##'   # y=30
    '##ddddddddooddgd#wwwww##'
    '##dddddddddddddd#wwwcw##'
    '##dPdddddddddddd#wwwww##'   #      the player start, facing north
    '########################'
)
$Tiles = New-Tiles
$Roof = 3 * 256 + 1
$Tiles['d'] = @{ Plane0 = 108 }
$Tiles['P'] = @{ Plane0 = 108; Plane1 = 19 }
$Tiles['o'] = @{ Plane0 = 108; Plane1 = 58 }                     # a barrel
$Tiles['g'] = @{ Plane0 = 108; Plane1 = 108 }                    # a guard facing east
$Tiles['w'] = @{ Plane0 = 109; Flat = $Roof }
$Tiles['c'] = @{ Plane0 = 109; Flat = $Roof; Plane1 = 49 }       # a clip
$Tiles['k'] = @{ Plane0 = 109; Flat = $Roof; Plane1 = 111 }      # a guard facing south
$Tiles['x'] = @{ Plane0 = 110; Flat = $Roof }
$planes = New-MapPlanes
Add-MapLayout $planes $Docks $Tiles 0 20
Save-MapWad "$modDir\maps\MAP61.wad" 'MAP61' 'The Docks' $planes

# MAP62, the Fortress: a corridor between two halls; the gold key in the west hall, the gold
# door out of the east one
$Fortress = @(
    '########################'   # y=20
    '################xxxX####'   #      the elevator
    '#################L######'   #      the gold-locked door
    '########rrrrr#qqqqqqq###'   #      the west hall, the east hall
    '########rKrrr#qqqqqqq###'   #      the gold key
    '########rrrrr#qgqqqqq###'   # y=25
    '########rrrrr#qqqqqqq###'
    '########rrrrr#qqqqqqq###'
    '########rrrgr#qqqqqqq###'
    '########rrrrr#qqqqqdq###'
    '########rdrrr#qqqqqqq###'   # y=30
    '########rrrrr#qqqqqqq###'
    '########rrrrr#qqqqqqq###'
    '##aaaaa###D#####D#######'
    '##aaaaaEhhhhshhhhhshh###'   #      the corridor, SS on guard
    '##aaaaa#################'   # y=35
    '##aaPaa#################'   #      the player start, facing north
    '########################'
)
$Tiles = New-Tiles
$Tiles['#'] = @{ Plane0 = 8 }
$Tiles['a'] = @{ Plane0 = 108 }
$Tiles['P'] = @{ Plane0 = 108; Plane1 = 19 }
$Tiles['h'] = @{ Plane0 = 109 }
$Tiles['s'] = @{ Plane0 = 109; Plane1 = 128 }                    # an SS facing west
$Tiles['r'] = @{ Plane0 = 110 }
$Tiles['K'] = @{ Plane0 = 110; Plane1 = 43 }                     # the gold key
$Tiles['g'] = @{ Plane0 = 110; Plane1 = 111 }
$Tiles['d'] = @{ Plane0 = 110; Plane1 = 137 }                    # a dog facing south
$Tiles['q'] = @{ Plane0 = 111 }
$Tiles['x'] = @{ Plane0 = 112 }
$Tiles['L'] = @{ Plane0 = 93 }                                   # gold-locked, north-south
$planes = New-MapPlanes -Fill 8
Add-MapLayout $planes $Fortress $Tiles 0 20
# The east hall's guard and dog are in its area
$planes[0][25 * 64 + 15] = 111; $planes[0][29 * 64 + 19] = 111
Save-MapWad "$modDir\maps\MAP62.wad" 'MAP62' 'The Fortress' $planes

# MAP63, the Tower: a courtyard three stories high under the night sky, its guards; at the far
# end the floor that ends the episode (object 99: BJ runs for it, as on Wolf3D's last floors)
$Tower = @(
    '########################'   # y=20
    '########################'
    '########################'
    '###ccccccccvvcccccccc###'   #      the way out (v)
    '###cccccgccccccgccccc###'
    '###cccccccccccccccccc###'   # y=25
    '###cccIccccccccccIccc###'   #      pillars
    '###ccccccccsscccccccc###'   #      SS
    '###cccccccccccccccccc###'
    '###ccdccccccccccccdcc###'   #      dogs
    '###cccccccccccccccccc###'   # y=30
    '###cccccccccccccccccc###'
    '###cccIccccccccccIccc###'
    '###cccccccccccccccccc###'
    '###cccccccccccccccccc###'
    '###ccccccccPccccccccc###'   # y=35 the player start, facing north
    '########################'
)
$Tiles = New-Tiles
$Tiles['c'] = @{ Plane0 = 108 }
$Tiles['P'] = @{ Plane0 = 108; Plane1 = 19 }
$Tiles['I'] = @{ Plane0 = 1; Height = 3 }                        # a pillar, 3 stories
$Tiles['g'] = @{ Plane0 = 108; Plane1 = 111 }
$Tiles['s'] = @{ Plane0 = 108; Plane1 = 129 }
$Tiles['d'] = @{ Plane0 = 108; Plane1 = 137 }
$Tiles['v'] = @{ Plane0 = 108; Plane1 = 99 }                     # the episode's end
$planes = New-MapPlanes
Add-MapLayout $planes $Tower $Tiles 0 20
Save-MapWad "$modDir\maps\MAP63.wad" 'MAP63' 'The Tower' $planes
