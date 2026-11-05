# Builds examples/mods/particles-demo: the brazier's sprites (sprites/PDFR*.png), the steam
# grate's flat (flats/PDGRATE.png) and its level (maps/MAP01.wad). Run it after changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-particles-demo.ps1
#
# One level, a dim hall of particle effects (the mod's particles.yaml):
#   the rocket gallery (north)  a launcher in its west wall fires rockets along it while you're in
#                               sight: smoke trails, and sparks where they hit the east wall
#   the hall                    four braziers (embers and smoke), two smoldering barrels, and a
#                               magic orb circling the pillar in the middle (the mod's script)
#   the fountain (south-west)   water thrown up out of a well, splashing as it lands
#   the steam grates (south)    steam out of the floor; the valve on the wall behind them shuts it
#                               off and on (tag 1, theirs and the valve's)
#   the treasure nook (west)    treasure that sparkles as it's picked up (the script)
#   the guard room (west)       guards: blood when they're hit, gibs when they die (the script)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'particles-demo' @('sprites', 'flats', 'maps')

#
# THE BRAZIER: an iron bowl on three legs, two frames of flames in it
#

foreach ($frame in 'A', 'B') {
    $seed = if ($frame -eq 'A') { 0.0 } else { 1.9 }
    Save-Picture "$modDir\sprites\PDFR${frame}0.png" 64 64 {
        param($x, $y)
        $dx = $x - 32
        # the legs: three iron bars from the bowl down to the floor, splayed
        if ($y -ge 44) {
            $spread = ($y - 44) * 0.45
            foreach ($leg in @((-10 - $spread), 0, (10 + $spread))) {
                if ([Math]::Abs($dx - $leg) -le 1.2) { return New-Color 60 58 62 }
            }
        }
        # the bowl: a wide iron dish, lit along its rim
        if ($y -ge 34 -and $y -le 44) {
            $half = 16 - ($y - 34) * 0.7
            if ([Math]::Abs($dx) -le $half) {
                if ($y -le 35) { return New-Color 150 110 80 }
                if (($x + $y) % 5 -eq 0) { return New-Color 92 88 96 }
                return New-Color 70 66 74
            }
        }
        # the flames: tongues rising out of the bowl, narrowing, their edges wavering
        $h = 35 - $y
        if ($h -lt 0 -or $h -gt 30) { return $null }
        $width = 13 * (1 - $h / 30) + 3 * [Math]::Sin($h * 0.5 + $seed * 2 + $x * 0.15)
        $sway = 2.5 * [Math]::Sin($h * 0.18 + $seed)
        $d = [Math]::Abs($dx - $sway)
        if ($d -gt $width) { return $null }
        $heat = 1 - $d / [Math]::Max($width, 1) * 0.7 - $h / 45
        if ($heat -gt 0.75) { return New-Color 255 240 170 }
        if ($heat -gt 0.5) { return New-Color 255 190 50 }
        if ($heat -gt 0.3) { return New-Color 240 110 20 }
        New-Color 180 50 10
    }
}

#
# THE GRATE: 64x64, an iron grating over black, rivets at its corners
#

Save-Picture "$modDir\flats\PDGRATE.png" 64 64 {
    param($x, $y)
    $border = $x -lt 3 -or $y -lt 3 -or $x -gt 60 -or $y -gt 60
    if ($border) {
        if (($x -lt 6 -or $x -gt 57) -and ($y -lt 6 -or $y -gt 57) -and ($x + $y) % 2 -eq 0) { return New-Color 140 140 150 }
        return New-Color 84 84 92
    }
    # bars every 8 texels both ways, 3 wide, with slots of black between
    $bx = ($x % 8) -lt 3; $by = ($y % 8) -lt 3
    if ($bx -and $by) { return New-Color 100 100 108 }
    if ($bx -or $by) { return New-Color 72 72 80 }
    New-Color 8 8 10
}

#
# THE LEVEL
#

$Brazier = 960; $Vent = 961; $Fountain = 962; $Pot = 963; $Launcher = 964
$Grate = 40                 # the flat plane's floor index for the grate (mapdefs/flats.yaml)

# 28 wide. The launcher ('R') is inside the gallery's west wall, firing east along its north row.
$Level = @(
    '############################'   # y=20
    'R..........................#'   #      the rocket gallery: rockets fly along this row
    '#..........................#'
    '#..........................#'
    '##########......############'   #      the way from the hall into the gallery
    '#ttt#..b................b..#'   # y=25 the treasure nook; braziers
    '#t..D.......k..o..k........X'   #      its door; smoldering barrels either side of the pillar; the elevator switch
    '#ttt#......................#'
    '#####......................#'
    '#GGG#..b................b..#'   #      the guard room; braziers
    '#...D..........P...........#'   # y=30 its door; the player start, facing north
    '#G.G#....f.........ggg.....#'   #      the fountain; the steam grates
    '#####################S######'   #      the valve, behind the grates
)
foreach ($row in $Level) { if ($row.Length -ne 28) { throw "a row of the level is $($row.Length) wide, not 28: '$row'" } }

$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 1 }                                               # grey stone
$Tiles['R'] = @{ Plane0 = 8; Plane1 = $Launcher }                           # the launcher, in blue stone
$Tiles['.'] = @{ Plane0 = 108 }                                             # the floor
$Tiles['b'] = @{ Plane0 = 108; Plane1 = $Brazier }
$Tiles['k'] = @{ Plane0 = 108; Plane1 = $Pot }
$Tiles['o'] = @{ Plane0 = 8; Tag = 2 }                                      # the pillar the orb circles (the script finds tag 2)
$Tiles['f'] = @{ Plane0 = 108; Plane1 = $Fountain }
$Tiles['g'] = @{ Plane0 = 108; Flat = $Grate; Plane1 = $Vent; Tag = 1 }     # a steam grate, tag 1 with the valve
$Tiles['S'] = @{ Plane0 = 174; Tag = 1 }                                    # the valve (mapdefs/walls.yaml)
$Tiles['t'] = @{ Plane0 = 109; Plane1 = 52 }                                # a cross, in the nook
$Tiles['G'] = @{ Plane0 = 110; Plane1 = 108 }                               # a guard, standing, facing east
$Tiles['D'] = @{ Plane0 = 90 }                                              # a door, east-west
$Tiles['P'] = @{ Plane0 = 108; Plane1 = 19 }                                # the player start, facing north
$Tiles['X'] = @{ Plane0 = 21 }                                              # the elevator switch
$planes = New-MapPlanes
Add-MapLayout $planes $Level $Tiles 14 20
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Particles Demo' $planes
