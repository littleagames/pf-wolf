# Builds examples/mods/arena-demo: a multiplayer arena, Twin Keeps (maps/AR01.wad), and the sky
# over it (graphics/ARSKY.png). Run it after changing either:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-arena-demo.ps1
#
# The arena is drawn as its west half, red's; the east half, blue's, is that mirrored, each red
# piece swapped for blue's. Every multiplayer mode can be played on it but Heist: the team starts,
# the flags, the control points, the hills and Horde's way in are all marked.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'arena-demo'

Save-Picture "$modDir\graphics\ARSKY.png" 512 128 {
    param($x, $y)
    $a = 2 * [Math]::PI * $x / 512
    $t = $y / 127
    $hills = 108 + 7 * [Math]::Sin(2 * $a) + 4 * [Math]::Sin(7 * $a + 1)
    if ($y -gt $hills) { return New-Color 50 70 50 }
    $cloud = [Math]::Sin(3 * $a + 0.06 * $y) + 0.5 * [Math]::Sin(11 * $a) - 2 * [Math]::Abs($t - 0.45)
    if ($cloud -gt 0.5) { return New-Color 250 220 190 }
    New-Color (230 - 80 * (1 - $t)) (150 + 40 * $t) (120 + 30 * $t)
}

# The west half, red's: their keep (k) with their flag base (F), team starts (T) and a gatling
# gun; corridors (c) round the field's north and south edges; the field (.), its cover (o), a
# control point either side (C), a hill spot (H) by the middle, deathmatch starts (d), weapons,
# ammo and first aid
$West = @(
    '####################'   # y=20
    '####################'
    '###ccccccccccccccacc'   #      the north corridor
    '###ccMcccccccccccccc'
    '###c#####....C......'   #      a control point
    '###c#####d..........'   # y=25
    '##kkkkkk#..o........'   #      red's keep
    '##TkkkTk#...........'   #      team starts
    '##kkkkkkg.P.........'   #      its gate (g); a start for one player trying it (map AR01)
    '##kFkkkkgh.....o..H.'   #      the flag base; a hill spot by the middle
    '##kkGkkkg......o....'   # y=30 a gatling gun
    '##kkkkkkg...........'
    '##TkkkTk#........d..'
    '##kkkkkk#..o........'
    '###c#####d..........'
    '###c#####....C......'   # y=35
    '###ccMcccccccccccccc'   #      the south corridor
    '###ccccccccccccccacc'
    '####################'
    '####################'
)
# Mirrored: each row reversed, red's pieces swapped for blue's
$Swap = @{ [char]'F' = [char]'f'; [char]'T' = [char]'t'; [char]'k' = [char]'K'; [char]'G' = [char]'G'; [char]'P' = [char]'.' }
$Layout = foreach ($row in $West) {
    $east = -join ($row.ToCharArray()[($row.Length - 1)..0] | ForEach-Object { if ($Swap.ContainsKey($_)) { $Swap[$_] } else { $_ } })
    $row + $east
}

# Floor codes: the field and corridors are one area, each keep its own. Light zones (plane 5):
# red's keep 1, blue's 2 (game-info tints them). Things are pfwolf-pk3's mapdefs/wolf3d markers.
$Field = 108; $RedKeep = 109; $BlueKeep = 110
$Roof = 3 * 256 + 1
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 1 }
$Tiles['o'] = @{ Plane0 = 1; Height = 2 }                      # cover, 2 stories
$Tiles['.'] = @{ Plane0 = $Field }
$Tiles['c'] = @{ Plane0 = $Field }
$Tiles['g'] = @{ Plane0 = $Field }
$Tiles['d'] = @{ Plane0 = $Field; Plane1 = 410 }               # deathmatch start, facing north
$Tiles['C'] = @{ Plane0 = $Field; Plane1 = 442 }               # Domination: a control point
$Tiles['H'] = @{ Plane0 = $Field; Plane1 = 443 }               # King of the Hill: a spot the hill can be
$Tiles['a'] = @{ Plane0 = $Field; Plane1 = 444 }               # Horde: where enemies come in
$Tiles['M'] = @{ Plane0 = $Field; Plane1 = 50 }                # machine gun
$Tiles['P'] = @{ Plane0 = $Field; Plane1 = 20 }                # player start, facing east
$Tiles['h'] = @{ Plane0 = $Field; Plane1 = 48 }                # first aid kit
$Tiles['k'] = @{ Plane0 = $RedKeep; Flat = $Roof; Zone = 1 }
$Tiles['F'] = @{ Plane0 = $RedKeep; Flat = $Roof; Zone = 1; Plane1 = 440 }    # red's flag
$Tiles['T'] = @{ Plane0 = $RedKeep; Flat = $Roof; Zone = 1; Plane1 = 431 }    # red start, facing east
$Tiles['G'] = @{ Plane0 = $RedKeep; Flat = $Roof; Zone = 1; Plane1 = 51 }     # gatling gun
$Tiles['K'] = @{ Plane0 = $BlueKeep; Flat = $Roof; Zone = 2 }
$Tiles['f'] = @{ Plane0 = $BlueKeep; Flat = $Roof; Zone = 2; Plane1 = 441 }   # blue's flag
$Tiles['t'] = @{ Plane0 = $BlueKeep; Flat = $Roof; Zone = 2; Plane1 = 437 }   # blue start, facing west
$planes = New-MapPlanes
Add-MapLayout $planes $Layout $Tiles 12 20
# The gatling gun on blue's side is in blue's keep
foreach ($t in Find-LayoutTiles $Layout 'G' 12 20) { if ($t.X -gt 31) { $planes[0][$t.Index] = $BlueKeep; $planes[5][$t.Index] = 2 } }
Save-MapWad "$modDir\maps\AR01.wad" 'AR01' 'Twin Keeps' $planes
