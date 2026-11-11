# Builds examples/mods/hazards-demo: its floors (flats/*.png: lava, acid and water, the moving
# ones in frames that animdefs/hazards.yaml plays), the hazard suit (sprites/HZSTA0.png) and the
# level (maps/MAP01.wad). Run it after changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-hazards-demo.ps1 [-MidnightAssault <midnight-assault.pk3>]
#
# The water, live and dead, is Midnight Assault's (out of its pk3); the lava, the acid and the
# suit, which it has none of, are drawn here.

param([string]$MidnightAssault = 'D:\projects\Wolf3D\Midnight Assault\midnight-assault.pk3')

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'hazards-demo'
$a = 2 * [Math]::PI / 64          # sines that repeat every 64 pixels, so the floors tile

# Lava: glowing orange and yellow, crusted dark; three frames, the glow moving through them
foreach ($f in 1..3) {
    Save-Picture "$modDir\flats\HZLAVA$f.png" 64 64 {
        param($x, $y)
        $v = [Math]::Sin($a * (2 * $x + 2 * $f * 3)) + [Math]::Sin($a * (3 * $y - $x + 5 * $f)) + 0.6 * [Math]::Sin($a * 5 * ($x + $y))
        if ($v -gt 1.2) { return New-Color 255 230 120 }
        if ($v -gt 0.2) { return New-Color 250 140 30 }
        if ($v -gt -0.8) { return New-Color 200 60 10 }
        New-Color 70 24 10
    }
}
# Acid: a sickly green with bubbles that come and go; two frames
foreach ($f in 1..2) {
    Save-Picture "$modDir\flats\HZACID$f.png" 64 64 {
        param($x, $y)
        $bx = ($x + 11 * $f) % 16 - 8; $by = ($y + 7 * $f) % 16 - 8
        if ($bx * $bx + $by * $by -lt 6) { return New-Color 200 255 120 }
        $v = [Math]::Sin($a * (2 * $x + 3 * $y)) * 14
        New-Color (60 + $v) (150 + $v) (40 + $v / 2)
    }
}
# Water: Midnight Assault's dark water (the current off), and its electrified water, the current
# crawling over it in four frames (WATER_ELECTRIC1-4)
$ma = Open-MidnightAssault $MidnightAssault
Copy-MAPicture $ma 'WATER_DARK.bmp' "$modDir\flats\HZWATER.png"
foreach ($f in 1..4) { Copy-MAPicture $ma "WATER_ELECTRIC$f.bmp" "$modDir\flats\HZSHOCK$f.png" }
Close-MidnightAssault $ma

# The hazard suit: a yellow suit and a helmet with a dark visor, folded on the floor
Save-Drawing "$modDir\sprites\HZSTA0.png" 64 64 {
    param($g)
    Add-Poly $g @(220, 190, 30) @(16, 50, 48, 50, 50, 62, 14, 62)
    Add-Rect $g @(170, 140, 20) 16 55 32 2
    Add-Ellipse $g @(230, 200, 40) 22 34 20 18
    Add-Rect $g @(30, 40, 50) 26 40 12 6
}

# The level: the start room; the lava hall, a winding stone path across it; north of it the acid
# room, the suit in its far corner; east, a room with an electrified channel across it and the
# switch that turns the current off; the exit past the channel.
$Layout = @(
    '############################'   # y=20
    '#######################xxxX#'   #      the exit
    '########################D###'
    '#######aaaaaau########rrrrr#'   #      the acid room, the suit (u)
    '#######accccca########rrrrr#'   #      acid (c)
    '#######accccca########rrrrr#'   # y=25
    '#######aaaaaaa########rrrrr#'
    '#######aaaacca########rrrrr#'
    '#######accacca########rrrrr#'
    '#######accacca########wwwww#'   #      the electrified channel
    '#######accaaaa########wwwww#'   # y=30
    '#######aaaaaaa########wwwww#'
    '#########D############Srrrr#'   #      the switch for the current
    '#######llplllllllllll#rrrrr#'   #      the lava hall (l), its path (p)
    '#######llppppplllllll#rrrrr#'
    '##ssss#llplllplllppppErrrrr#'   # y=35
    '##ssssEppplllplllplll#rrrrr#'
    '##ssss#llllllppppplll#rrrrr#'
    '##sPss#llllllllllllll#rrrrr#'   #      the player start, facing north
    '############################'
    '############################'   # y=40
)
# Floors (mapdefs/flats.yaml): 40 lava, 41 acid, 42 electrified water
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 2 }
$Tiles['s'] = @{ Plane0 = 108 }
$Tiles['P'] = @{ Plane0 = 108; Plane1 = 19 }
$Tiles['l'] = @{ Plane0 = 109; Flat = 40; Zone = 1 }
$Tiles['p'] = @{ Plane0 = 109; Zone = 1 }
$Tiles['a'] = @{ Plane0 = 110 }
$Tiles['c'] = @{ Plane0 = 110; Flat = 41 }
$Tiles['u'] = @{ Plane0 = 110; Plane1 = 920 }                 # the hazard suit
$Tiles['r'] = @{ Plane0 = 111 }
$Tiles['w'] = @{ Plane0 = 111; Flat = 42; Tag = 1 }
$Tiles['S'] = @{ Plane0 = 160; Tag = 1 }                      # the switch, tag 1 with the water
$Tiles['x'] = @{ Plane0 = 112 }
$Tiles['X'] = @{ Plane0 = 21 }
$Tiles['D'] = @{ Plane0 = 91 }
$Tiles['E'] = @{ Plane0 = 90 }
$planes = New-MapPlanes -Fill 2
Add-MapLayout $planes $Layout $Tiles 0 20
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Hazards Demo' $planes
