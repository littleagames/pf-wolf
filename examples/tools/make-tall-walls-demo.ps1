# Builds examples/mods/tall-walls-demo: its tall wall textures (textures/*.png), its sky
# (graphics/TWSKY.png) and its level (maps/MAP01.wad). Run it after changing anything here:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-tall-walls-demo.ps1
#
# The pictures are drawn pixel by pixel below (original art, not taken from any game), in full
# color; the game picks the nearest palette color for each pixel as it loads them. The level is
# an ECWolf binary map (a WAD holding a MAP01 marker lump and a WDC3.1 PLANES lump) with
# PFWolf's planes up to the wall heights: walls/floors, objects, flats and wall heights.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$ModDir = Join-Path $PSScriptRoot '..\mods\tall-walls-demo'

# ---------------------------------------------------------------------------------------------
# Textures
#
# A texture 64 pixels wide and 64 * n tall spans n stories, from the floor up. Each painter
# below gives the color of pixel x (0-63, wrapping around) at yf, counted up from the floor.
# Every texture is saved twice, like the game's own walls: NAME1 for north and south faces,
# and a darker NAME2 for east and west ones.
# ---------------------------------------------------------------------------------------------

$Noise = New-Object 'int[,]' 64, 192
$rng = New-Object Random 1941
for ($x = 0; $x -lt 64; $x++) { for ($y = 0; $y -lt 192; $y++) { $Noise[$x, $y] = $rng.Next(-5, 6) } }

function Shade([int[]]$rgb, [int]$amount) {
    , @(($rgb[0] + $amount), ($rgb[1] + $amount), ($rgb[2] + $amount))
}

# Which block of a running bond pixel (x, yf) is in, counting rows up from $Base:
# Row/Col say which block, Edge is 'mortar', 'top', 'bottom' or '' (inside).
function Get-Block([int]$x, [int]$yf, [int]$Base, [int]$RowHeight, [int]$BlockWidth) {
    $v = $yf - $Base
    $row = [Math]::Floor($v / $RowHeight)
    $inRow = $v - $row * $RowHeight
    $shifted = ($x + ($row % 2) * [int]($BlockWidth / 2)) % 64
    $col = [Math]::Floor($shifted / $BlockWidth)
    $edge = ''
    if ($inRow -eq 0 -or $shifted % $BlockWidth -eq 0) { $edge = 'mortar' }
    elseif ($inRow -eq $RowHeight - 1) { $edge = 'top' }
    elseif ($inRow -eq 1) { $edge = 'bottom' }
    @{ Row = $row; Col = $col; Edge = $edge }
}

# A block's own tone, so blocks aren't all alike: -Spread..Spread, fixed per block
function Get-Tone([int]$row, [int]$col, [int]$seed, [int]$spread) {
    (($row * 37 + $col * 101 + $seed * 13) % (2 * $spread + 1)) - $spread
}

# Masonry: blocks in running bond, mortar between, a lit top edge and a shadowed bottom one
function Get-Masonry([int]$x, [int]$yf, [int]$Base, [int]$RowHeight, [int]$BlockWidth,
                     [int[]]$Stone, [int[]]$Mortar, [int]$Seed, [int]$Spread) {
    $b = Get-Block $x $yf $Base $RowHeight $BlockWidth
    if ($b.Edge -eq 'mortar') { return , $Mortar }
    $tone = Get-Tone $b.Row $b.Col $Seed $Spread
    if ($b.Edge -eq 'top') { $tone += 14 } elseif ($b.Edge -eq 'bottom') { $tone -= 14 }
    Shade $Stone $tone
}

# A horizontal band of dressed stone that sticks out: lit on top, a shadow line under it
function Get-Course([int]$yf, [int]$From, [int]$To, [int[]]$Stone) {
    if ($yf -eq $From) { return , @(64, 62, 58) }
    if ($yf -eq $To) { return Shade $Stone 22 }
    Shade $Stone 0
}

# TWSTON, 2 stories: a keep's wall, ashlar on a plinth, a string course between the stories,
# an arched window in the upper one and a cornice along the top
$KeepStone = @(150, 146, 136); $KeepMortar = @(92, 88, 82); $Dressed = @(182, 176, 160)
$PaintKeep = {
    param([int]$x, [int]$yf)
    if ($yf -lt 8) {
        if ($yf -eq 7) { return Shade $KeepStone -6 }
        return Get-Masonry $x $yf 0 8 32 @(112, 108, 102) $KeepMortar 3 6
    }
    if ($yf -ge 60 -and $yf -le 67) { return Get-Course $yf 60 67 $Dressed }
    if ($yf -ge 120) { return Get-Course $yf 120 127 $Dressed }

    # the window: an opening 16 wide with a round top, iron bars, a dressed frame and a sill
    $dx = $x - 31.5
    $inside = ([Math]::Abs($dx) -lt 8) -and $yf -ge 80 -and ($yf -lt 104 -or ($dx * $dx + ($yf - 104) * ($yf - 104)) -lt 64)
    $frame = ([Math]::Abs($dx) -lt 11) -and $yf -ge 78 -and ($yf -lt 104 -or ($dx * $dx + ($yf - 104) * ($yf - 104)) -lt 121)
    if ($inside) {
        if ($x -eq 31 -or $x -eq 32 -or $yf -eq 94) { return , @(70, 70, 76) }
        return , @(20, 22, 34)
    }
    if ($yf -ge 75 -and $yf -le 79 -and [Math]::Abs($dx) -lt 13) {
        return Get-Course $yf 75 79 $Dressed
    }
    if ($frame) { return Shade $Dressed -8 }

    if ($yf -lt 60) { return Get-Masonry $x $yf 8 13 32 $KeepStone $KeepMortar 1 10 }
    Get-Masonry $x $yf 68 13 32 $KeepStone $KeepMortar 2 10
}

# TWTOWR, 3 stories: a curtain wall, rough blocks below, finer stone above with a red banner
# hanging over the upper two stories, and battlements along the top
$Rough = @(126, 114, 98); $Fine = @(138, 136, 130); $TowerMortar = @(84, 80, 74)
$Gold = @(204, 160, 52); $Red = @(150, 26, 24)
$PaintTower = {
    param([int]$x, [int]$yf)
    if ($yf -lt 4) { return , @(86, 80, 72) }
    if ($yf -lt 64) { return Get-Masonry $x $yf 4 15 32 $Rough $TowerMortar 4 14 }
    if ($yf -lt 72) { return Get-Course $yf 64 71 @(168, 160, 144) }

    # battlements: merlons, with the dark gaps between them
    if ($yf -ge 180) {
        if ($yf -eq 180) { return , @(60, 58, 56) }
        if ((($x + 6) % 32) -lt 20) {
            if ($yf -eq 191) { return Shade $Fine 24 }
            if ((($x + 6) % 32) -eq 0 -or (($x + 6) % 32) -eq 19) { return , $TowerMortar }
            return Shade $Fine ($Noise[$x, $yf] - 4)
        }
        return , @(52, 52, 58)
    }

    # the rod the banner hangs from, with gold knobs
    if ($yf -ge 175 -and $yf -le 177 -and $x -ge 14 -and $x -le 49) {
        if ($x -le 15 -or $x -ge 48) { return , $Gold }
        return , @(96, 62, 30)
    }

    # the banner: a gold border, folds, a ring with a dot, and a swallowtail
    $dx = [Math]::Abs($x - 31.5)
    $bottom = 90 + [Math]::Max(0, 12 - $dx * 1.6)
    if ($dx -lt 14 -and $yf -ge $bottom -and $yf -lt 175) {
        $edge = $dx -ge 12 -or $yf -ge 173 -or $yf -lt $bottom + 2
        if ($edge) { return Shade $Gold ($Noise[$x, $yf]) }
        $r = [Math]::Sqrt(($x - 31.5) * ($x - 31.5) + ($yf - 140.5) * ($yf - 140.5))
        if (($r -ge 8 -and $r -lt 11) -or $r -lt 3.5) { return Shade $Gold ($Noise[$x, $yf]) }
        $fold = [int](14 * [Math]::Sin(($x - 18) * [Math]::PI / 7))
        return Shade $Red $fold
    }

    Get-Masonry $x $yf 72 12 16 $Fine $TowerMortar 5 10
}

# TWARCH, 2 stories: red brick with a ring of pale voussoirs and a keystone at the bottom of
# the upper story. On an arch, its face shows the stories from 2 up, so the voussoirs sit
# just over the opening, and the arch's underside is the bottom story's brick.
# (a redder brick has dark tones that land on the palette's pure reds, which show as red specks)
$Brick = @(150, 80, 52); $BrickMortar = @(150, 140, 126); $Voussoir = @(196, 184, 160)
$PaintArch = {
    param([int]$x, [int]$yf)
    if ($yf -lt 4) { return , @(100, 90, 82) }
    $key = $x -ge 27 -and $x -le 36
    if ($yf -ge 64 -and ($yf -le 75 -or ($key -and $yf -le 79))) {
        $top = if ($key) { 79 } else { 75 }
        if ($yf -eq 64) { return Shade $Voussoir -50 }
        if ($yf -eq $top) { return Shade $Voussoir 18 }
        if ($x -eq 27 -or $x -eq 37 -or (-not $key -and $x % 9 -eq 0)) { return , @(96, 88, 78) }
        $tone = if ($key) { 10 } else { (($x / 9) % 3) * 4 - 4 }
        return Shade $Voussoir ([int]$tone + $Noise[$x, $yf])
    }
    if ($yf -eq 76 -or ($key -and $yf -eq 80)) { return , @(70, 40, 32) }
    Get-Masonry $x $yf 4 8 16 $Brick $BrickMortar 6 12
}

function Save-Texture([string]$Name, [int]$Stories, [scriptblock]$Paint) {
    $height = 64 * $Stories
    $light = New-Object System.Drawing.Bitmap 64, $height
    $dark = New-Object System.Drawing.Bitmap 64, $height
    for ($x = 0; $x -lt 64; $x++) {
        for ($yf = 0; $yf -lt $height; $yf++) {
            $rgb = & $Paint $x $yf
            $n = $Noise[$x, $yf]
            $c = @(0, 0, 0); $d = @(0, 0, 0)
            for ($i = 0; $i -lt 3; $i++) {
                $c[$i] = [Math]::Max(0, [Math]::Min(255, [int]$rgb[$i] + $n))
                $d[$i] = [int]($c[$i] * 0.78)
            }
            $y = $height - 1 - $yf
            $light.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($c[0], $c[1], $c[2]))
            $dark.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($d[0], $d[1], $d[2]))
        }
    }
    $dir = Join-Path $ModDir 'textures'
    New-Item -ItemType Directory -Force $dir | Out-Null
    $light.Save((Join-Path (Resolve-Path $dir) "${Name}1.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $dark.Save((Join-Path (Resolve-Path $dir) "${Name}2.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $light.Dispose(); $dark.Dispose()
    "Wrote textures/${Name}1.png and ${Name}2.png (64x$height)"
}

Save-Texture 'TWSTON' 2 $PaintKeep
Save-Texture 'TWTOWR' 3 $PaintTower
Save-Texture 'TWARCH' 2 $PaintArch

# ---------------------------------------------------------------------------------------------
# Sky: dusk, deep blue fading to orange at the horizon, a few stars, distant hills. 512 wide,
# so it shows twice around the full circle.
# ---------------------------------------------------------------------------------------------

$skyW = 512; $skyH = 128
$sky = New-Object System.Drawing.Bitmap $skyW, $skyH
$stars = New-Object Random 7
$starAt = @{}
for ($i = 0; $i -lt 70; $i++) { $starAt["$($stars.Next(0, $skyW)),$($stars.Next(0, 56))"] = $true }
for ($x = 0; $x -lt $skyW; $x++) {
    $a = $x * 2 * [Math]::PI / $skyW
    $hill = 14 + 7 * [Math]::Sin($a * 3 + 1) + 4 * [Math]::Sin($a * 7) + 2 * [Math]::Sin($a * 17 + 2)
    for ($y = 0; $y -lt $skyH; $y++) {
        $t = $y / ($skyH - 1)
        if ($skyH - 1 - $y -lt $hill) {
            $c = @(34, 28, 38)
        }
        elseif ($starAt.ContainsKey("$x,$y")) {
            $c = @(236, 236, 220)
        }
        else {
            # blue at the top through purple to orange at the horizon
            $c = @(([int](24 + 196 * [Math]::Pow($t, 2.2))),
                   ([int](28 + 100 * [Math]::Pow($t, 3))),
                   ([int](72 + 40 * $t - 60 * [Math]::Pow($t, 4))))
        }
        $sky.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($c[0], $c[1], $c[2]))
    }
}
$graphicsDir = Join-Path $ModDir 'graphics'
New-Item -ItemType Directory -Force $graphicsDir | Out-Null
$sky.Save((Join-Path (Resolve-Path $graphicsDir) 'TWSKY.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$sky.Dispose()
"Wrote graphics/TWSKY.png (${skyW}x$skyH)"

# ---------------------------------------------------------------------------------------------
# The level
#
# One character per tile, placed with its top-left corner at tile ($Left, $Top). Every tile
# outside it is plain wall. The level's walls are 1 story by default (game-info.yaml); the
# height plane (plane 3) raises single tiles. A floor tile with a height of 2 or more is an
# arch: open below, a block of wall from story 2 up to that height, faced with the texture of
# the wall at the end of its run.
# ---------------------------------------------------------------------------------------------

$Left = 18
$Top = 20
$Layout = @(
    '###########################'   # y=20
    '#########nnnnnnnn##########'   #      the garden, behind the gate
    '#########*nnnnnnnX#########'   #      the elevator switch
    '#########nnnnnnnv##########'
    '##TTTTTTTTTHGGGHTTTTTTTTT##'   # y=24 the courtyard's north wall, 3 stories, and its
                                    #      gate: a 3-wide arch 3 stories tall between pillars
    '##T.*...................T##'
    '##T...I.................T##'
    '##T...a.................T##'
    '##T...I...........123...T##'   # y=28 stock grey stone, 1, 2 and 3 stories
    '##T...a.....KKK.........T##'   #      the keep, 2 stories
    '##T...I....vKKKv........T##'   # y=30
    '##T...a.....KKK.........T##'
    '##T...I.................T##'   #      the colonnade: pillars and arches, 2 stories
    '##T...a.................T##'
    '##T...I.................T##'
    '##T...a.................T##'
    '##T...I...............*.T##'
    '##T.....................T##'
    '##TTTTTTTTTTTDTTTTTTTTTTT##'   # y=38 the south wall and its door
    '###########,,,,,###########'   #      the gatehouse, 1 story
    '###########,,,,,###########'
    '###########,,P,,###########'   #      the player start
    '###########,,,,,###########'
    '###########################'
)

# What each character puts on plane 0 (walls and floor codes), plane 1 (objects) and the
# height plane. Floor codes are areas (mapdefs floors: area n is 107 + n); walls, doors and
# objects are mapdefs ids (pfwolf-pk3's mapdefs/wolf3d, and the mod's own walls 50-52).
$Gatehouse = 108; $Courtyard = 109
$Tiles = @{
    '#' = @{ Plane0 = 1 }                                   # grey stone, the level's 1 story
    '.' = @{ Plane0 = $Courtyard }
    'n' = @{ Plane0 = $Courtyard }                          # the garden
    ',' = @{ Plane0 = $Gatehouse }
    'P' = @{ Plane0 = $Gatehouse; Plane1 = 19 }             # player start, facing north
    '*' = @{ Plane0 = $Courtyard; Plane1 = 31 }             # green plant
    'v' = @{ Plane0 = $Courtyard; Plane1 = 35 }             # vase
    'T' = @{ Plane0 = 50; Height = 3 }                      # curtain wall (TWTOWR, 3 stories)
    'K' = @{ Plane0 = 51; Height = 2 }                      # the keep (TWSTON, 2 stories)
    'I' = @{ Plane0 = 52; Height = 2 }                      # a pillar (TWARCH, 2 stories)
    'H' = @{ Plane0 = 52; Height = 3 }                      # a gate pillar, 3 stories
    'a' = @{ Plane0 = $Courtyard; Height = 2 }              # an arch, 2 stories
    'G' = @{ Plane0 = $Courtyard; Height = 3 }              # the gate's arch, 3 stories
    'D' = @{ Plane0 = 91; Height = 3 }                      # a door, its lintel 3 stories up
    'X' = @{ Plane0 = 21 }                                  # elevator switch: ends the level
    '1' = @{ Plane0 = 1; Height = 1 }                       # stock 64x64 walls repeat a
    '2' = @{ Plane0 = 1; Height = 2 }                       # story at a time
    '3' = @{ Plane0 = 1; Height = 3 }
}

$Size = 64
$PlaneCount = 4
$HeightPlane = 3
$planes = @()
for ($p = 0; $p -lt $PlaneCount; $p++) { $planes += , (New-Object 'uint16[]' ($Size * $Size)) }
for ($i = 0; $i -lt $Size * $Size; $i++) { $planes[0][$i] = 1 }

for ($row = 0; $row -lt $Layout.Count; $row++) {
    $line = $Layout[$row]
    if ($line.Length -ne 27) { throw "Layout row $row is $($line.Length) wide, not 27" }
    for ($col = 0; $col -lt $line.Length; $col++) {
        $ch = [string]$line[$col]
        if (!$Tiles.ContainsKey($ch)) { throw "Unknown layout character '$ch' at row $row, column $col" }
        $tile = $Tiles[$ch]
        $i = ($Top + $row) * $Size + $Left + $col
        $planes[0][$i] = $tile.Plane0
        if ($tile.Plane1) { $planes[1][$i] = $tile.Plane1 }
        if ($tile.Height) { $planes[$HeightPlane][$i] = $tile.Height }
    }
}

# PLANES: "WDC3.1", map count, plane count, name length, name, width, height, then the planes
$ms = New-Object IO.MemoryStream
$bw = New-Object IO.BinaryWriter $ms
$bw.Write([Text.Encoding]::ASCII.GetBytes('WDC3.1'))
$bw.Write([int]1)
$bw.Write([uint16]$PlaneCount)
$bw.Write([uint16]16)
$name = New-Object byte[] 16
$title = 'Tall Walls Demo'
[Text.Encoding]::ASCII.GetBytes($title, 0, $title.Length, $name, 0) | Out-Null
$bw.Write($name)
$bw.Write([uint16]$Size)
$bw.Write([uint16]$Size)
foreach ($plane in $planes) { foreach ($value in $plane) { $bw.Write([uint16]$value) } }
$bw.Flush()
$planesLump = $ms.ToArray()

# A PWAD: header, the PLANES data, then the directory (the MAP01 marker, then PLANES)
$out = New-Object IO.MemoryStream
$w = New-Object IO.BinaryWriter $out
$w.Write([Text.Encoding]::ASCII.GetBytes('PWAD'))
$w.Write([int]2)
$w.Write([int](12 + $planesLump.Length))
$w.Write($planesLump)
foreach ($lump in @(@(12, 0, 'MAP01'), @(12, $planesLump.Length, 'PLANES'))) {
    $w.Write([int]$lump[0])
    $w.Write([int]$lump[1])
    $lumpName = New-Object byte[] 8
    [Text.Encoding]::ASCII.GetBytes($lump[2], 0, $lump[2].Length, $lumpName, 0) | Out-Null
    $w.Write($lumpName)
}
$w.Flush()

$mapsDir = Join-Path $ModDir 'maps'
New-Item -ItemType Directory -Force $mapsDir | Out-Null
$path = Join-Path (Resolve-Path $mapsDir) 'MAP01.wad'
[IO.File]::WriteAllBytes($path, $out.ToArray())
"Wrote $path ($($out.Length) bytes)"
