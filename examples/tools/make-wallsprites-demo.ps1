# Builds examples/mods/wallsprites-demo: its panels' pictures (sprites/*.png), its sky
# (graphics/WSSKY.png) and its level (maps/MAP01.wad). Run it after changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-wallsprites-demo.ps1
#
# Wall sprites are flat panels standing in their tile (not sprites that turn to face you): a
# picture on a clear background, drawn across the tile the way its map thing faces.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'wallsprites-demo'
$spr = "$modDir\sprites"

# Chain-link: a post at each edge, a top rail, and a diamond mesh you can see (and shoot) through
Save-Picture "$spr\WSCHA0.png" 64 64 {
    param($x, $y)
    if ($x -le 2 -or $x -ge 61) { return New-Color 110 112 118 }
    if ($y -le 2) { return New-Color 130 132 138 }
    if ((($x + $y) % 8 -eq 0) -or (($x - $y + 64) % 8 -eq 0)) { return New-Color 150 152 160 }
    $null
}
# A clipped hedge: green and leafy along the bottom, clear above it
$leaf = New-Object Random 4
Save-Picture "$spr\WSHDA0.png" 64 64 {
    param($x, $y)
    $top = 30 + 3 * [Math]::Sin($x * 0.5) + 2 * [Math]::Sin($x * 1.3)
    if ($y -lt $top) { return $null }
    $n = $leaf.Next(-24, 25)
    New-Color (40 + $n / 2) (100 + $n) (36 + $n / 3)
}
# Iron bars: upright bars every 8 pixels, two cross bars
Save-Picture "$spr\WSBRA0.png" 64 64 {
    param($x, $y)
    if ($y -in 10, 11, 52, 53) { return New-Color 70 66 64 }
    if ($x % 8 -in 3, 4) { return New-Color $(if ($x % 8 -eq 3) { 96 } else { 56 }) 90 86 }
    $null
}
# A banner hanging from a pole: red cloth with a gold border and emblem, a fringed bottom
Save-Picture "$spr\WSBNA0.png" 64 64 {
    param($x, $y)
    if ($y -le 2 -and $x -ge 10 -and $x -le 53) { return New-Color 92 60 30 }
    if ($x -lt 16 -or $x -gt 47 -or $y -gt 56) { return $null }
    if ($y -gt 52 -and ($x % 3) -ne 0) { return $null }                        # the fringe
    if ($x -le 17 -or $x -ge 46 -or $y -le 4) { return New-Color 210 170 50 }
    $d = [Math]::Abs($x - 31.5) + [Math]::Abs($y - 26)
    if ($d -lt 9) { return New-Color 220 180 60 }
    New-Color 150 20 24
}
# A cobweb across a passage: threads from the top corners and rings round them
Save-Picture "$spr\WSWBA0.png" 64 64 {
    param($x, $y)
    foreach ($cx in 0, 63) {
        $dx = $x - $cx; $dy = $y
        $r = [Math]::Sqrt($dx * $dx + $dy * $dy)
        $a = [Math]::Atan2($dy, [Math]::Abs($dx))
        if ($r -lt 30 -and ([Math]::Abs($a * 6 / [Math]::PI - [Math]::Round($a * 6 / [Math]::PI)) -lt 0.04 -or [Math]::Abs($r % 7 - 3.5) -lt 0.45)) {
            return New-Color 200 200 204
        }
    }
    $null
}

# A pale sky over the yard
Save-Picture "$modDir\graphics\WSSKY.png" 512 128 {
    param($x, $y)
    $a = 2 * [Math]::PI * $x / 512
    $t = $y / 127
    $cloud = [Math]::Sin(3 * $a + 0.07 * $y) + 0.5 * [Math]::Sin(8 * $a - 0.05 * $y) - 2 * [Math]::Abs($t - 0.5)
    if ($cloud -gt 0.5) { return New-Color 236 238 240 }
    New-Color (110 + 100 * $t) (150 + 80 * $t) (210 + 30 * $t)
}

# The level: the stable corridor (cobwebs across it) and the hall (banners), roofed; the yard
# (open to the sky), split by a chain-link fence with guards behind it, a hedged bed, a fence
# across a corner, and in its east wall a barred window onto a locked cell
$Layout = @(
    '###########################'   # y=20
    '###########################'
    '###shhhhbhhhbhhhbhhhhh#####'   #      the hall, banners across it
    '###shhhhbhhhbhhhbhhhhh#####'
    '###s########y##############'   #      the way into the yard
    '###s###yyyyyyyyyyyyyyd#####'   # y=25 a fence across the corner
    '###s###yeeeyyyyyyyyyyy#####'   #      the hedged bed
    '###w###yyyyyyyyyyyyyyy#xx##'   #      a cobweb
    '###s###yeeeyyyyyyyyyyyExxX#'   #      the exit
    '###s###yyyyyyyyyyyyyyy#xx##'
    '###s###yyyyyyyyyyyyyyy#####'   # y=30
    '###w###fffffffGfffffff#####'   #      the chain-link fence and a gate
    '###s###yyyyyyyyyyyyyyy#cc##'   #      guards behind it; the cell
    '##sss##yygyyyyyyygyyyyick##'   #      the barred window
    '##sss##yyyyyygyyyyyygy#cc##'
    '##sPs##yyyyyyyyyyyyyyy#####'   # y=35 the player start, facing north
    '###########################'
    '###########################'
)
$Inside = 108; $Yard = 109; $ExitRoom = 110; $Cell = 111
$Roof = 3 * 256 + 1                                                      # wood ceiling over grey stone
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 12 }                                           # wood walls
$Tiles['s'] = @{ Plane0 = $Inside; Flat = $Roof }
$Tiles['P'] = @{ Plane0 = $Inside; Flat = $Roof; Plane1 = 19 }
$Tiles['w'] = @{ Plane0 = $Inside; Flat = $Roof; Plane1 = 836 }          # cobweb, east-west
$Tiles['h'] = @{ Plane0 = $Inside; Flat = $Roof }
$Tiles['b'] = @{ Plane0 = $Inside; Flat = $Roof; Plane1 = 835 }          # banner, north-south
$Tiles['y'] = @{ Plane0 = $Yard }
$Tiles['e'] = @{ Plane0 = $Yard; Plane1 = 833 }                          # hedge, east-west
$Tiles['f'] = @{ Plane0 = $Yard; Plane1 = 831 }                          # chain-link, east-west
$Tiles['G'] = @{ Plane0 = $Yard; Plane1 = 839 }                          # the gate, east-west
$Tiles['d'] = @{ Plane0 = $Yard; Plane1 = 832 }                          # chain-link, corner to corner
$Tiles['g'] = @{ Plane0 = $Yard; Plane1 = 109 }                          # guard facing north
$Tiles['i'] = @{ Plane0 = $Cell; Flat = $Roof; Plane1 = 834 }            # iron bars on the west edge
$Tiles['c'] = @{ Plane0 = $Cell; Flat = $Roof }
$Tiles['k'] = @{ Plane0 = $Cell; Flat = $Roof; Plane1 = 54 }             # a chest, out of reach
$Tiles['x'] = @{ Plane0 = $ExitRoom; Flat = $Roof }
$Tiles['X'] = @{ Plane0 = 21 }
$Tiles['E'] = @{ Plane0 = 90 }
$planes = New-MapPlanes -Fill 12
Add-MapLayout $planes $Layout $Tiles 0 20
# A plant in the hedged bed
$planes[1][27 * 64 + 9] = 31
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Wall Sprites Demo' $planes
