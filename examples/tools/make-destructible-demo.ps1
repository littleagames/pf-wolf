# Builds examples/mods/destructible-demo: its wall textures (textures/*.png), its rubble sprite
# (sprites/*.png), its sounds (sounds/*.wav) and its level (maps/MAP01.wad), from the drawing
# code and the layout below. Run it after changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-destructible-demo.ps1
#
# Everything is drawn or synthesized here (original, not taken from any game), in full color;
# the game picks the nearest palette color for each pixel as it loads the pictures. The YAML
# files beside them (mapdefs, animdefs, actordefs, sound-seq) are written by hand. The level is
# an ECWolf binary map (a WAD holding a MAP01 marker lump and a WDC3.1 PLANES lump) with
# PFWolf's six planes.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$ModDir = Join-Path $PSScriptRoot '..\mods\destructible-demo'

function New-Folder([string]$Name) {
    $dir = Join-Path $ModDir $Name
    New-Item -ItemType Directory -Force $dir | Out-Null
    (Resolve-Path $dir).Path
}

function Clamp([double]$v) { [int][Math]::Max(0, [Math]::Min(255, $v)) }

$rng = New-Object Random 1028
$Noise = New-Object 'int[,]' 64, 64
for ($x = 0; $x -lt 64; $x++) { for ($y = 0; $y -lt 64; $y++) { $Noise[$x, $y] = $rng.Next(-5, 6) } }

# ---------------------------------------------------------------------------------------------
# Textures: 64x64, each saved twice like the game's own walls: NAME1 for north and south faces,
# a darker NAME2 for east and west ones. Each painter gives pixel (x, y)'s color, y down.
# ---------------------------------------------------------------------------------------------

function Save-Texture([string]$Name, [scriptblock]$Paint, [switch]$LightOnly) {
    $dir = New-Folder 'textures'
    $light = New-Object Drawing.Bitmap 64, 64
    $dark = New-Object Drawing.Bitmap 64, 64
    for ($x = 0; $x -lt 64; $x++) {
        for ($y = 0; $y -lt 64; $y++) {
            $rgb = & $Paint $x $y
            $n = $Noise[$x, $y]
            $c = @((Clamp ($rgb[0] + $n)), (Clamp ($rgb[1] + $n)), (Clamp ($rgb[2] + $n)))
            $light.SetPixel($x, $y, [Drawing.Color]::FromArgb($c[0], $c[1], $c[2]))
            $dark.SetPixel($x, $y, [Drawing.Color]::FromArgb([int]($c[0] * 0.75), [int]($c[1] * 0.75), [int]($c[2] * 0.75)))
        }
    }
    $light.Save((Join-Path $dir "${Name}1.png"), [Drawing.Imaging.ImageFormat]::Png)
    if (!$LightOnly) { $dark.Save((Join-Path $dir "${Name}2.png"), [Drawing.Imaging.ImageFormat]::Png) }
    $light.Dispose(); $dark.Dispose()
    "Wrote textures/${Name}1.png$(if (!$LightOnly) { " and ${Name}2.png" })"
}

# Stone: grey blocks in running bond, lit top edges, dark mortar (greys: the palette has plenty,
# where a warm sandstone fell between its greys and oranges)
function Get-Stone([int]$x, [int]$y) {
    $row = [Math]::Floor($y / 16)
    $sx = ($x + ($row % 2) * 16) % 64
    if ($y % 16 -eq 15 -or $sx % 32 -eq 31) { return , @(40, 40, 44) }
    $tone = ((($row * 7 + [Math]::Floor($sx / 32) * 13) % 5) - 2) * 8
    if ($y % 16 -eq 0) { $tone += 20 }
    , @((124 + $tone), (122 + $tone), (118 + $tone))
}

# A crack: whether (x, y) is on one of the lines through these points (each a polyline)
function Test-OnLines([int]$x, [int]$y, $lines, [double]$width) {
    foreach ($line in $lines) {
        for ($i = 0; $i -lt $line.Count - 1; $i++) {
            $ax = $line[$i][0]; $ay = $line[$i][1]; $bx = $line[$i + 1][0]; $by = $line[$i + 1][1]
            $dx = $bx - $ax; $dy = $by - $ay
            $t = [Math]::Max(0, [Math]::Min(1, (($x - $ax) * $dx + ($y - $ay) * $dy) / ($dx * $dx + $dy * $dy)))
            $px = $ax + $t * $dx - $x; $py = $ay + $t * $dy - $y
            if ($px * $px + $py * $py -le $width * $width) { return $true }
        }
    }
    $false
}

$FewCracks = @(
    , @(@(34, 0), @(30, 12), @(36, 22), @(31, 34))
    , @(@(36, 22), @(46, 26), @(52, 24))
)
$ManyCracks = $FewCracks + @(
    , @(@(31, 34), @(38, 45), @(33, 56), @(36, 63))
    , @(@(31, 34), @(20, 40), @(14, 50), @(6, 52))
    , @(@(38, 45), @(52, 48), @(63, 44))
    , @(@(30, 12), @(18, 8), @(10, 14))
)

# Holes knocked through the stone as it crumbles: (x, y, radius) each, darkness behind
$Holes = @(@(32, 30, 9), @(18, 44, 7), @(46, 16, 7), @(44, 50, 8), @(14, 14, 6), @(54, 34, 6))

function Get-Crumbling([int]$x, [int]$y, [int]$Stage) {
    $count = @(2, 4, 6)[$Stage]
    $grow = @(0.7, 1.0, 1.5)[$Stage]
    for ($i = 0; $i -lt $count; $i++) {
        $h = $Holes[$i]; $dx = $x - $h[0]; $dy = $y - $h[1]; $r = $h[2] * $grow
        $d = [Math]::Sqrt($dx * $dx + $dy * $dy)
        if ($d -le $r - 1.5) { return , @(16, 12, 10) }
        if ($d -le $r) { return , @(84, 70, 54) }
    }
    if (Test-OnLines $x $y $ManyCracks 1.0) { return , @(40, 32, 26) }
    Get-Stone $x $y
}

Save-Texture 'DWSTON' { param([int]$x, [int]$y) Get-Stone $x $y }
Save-Texture 'DWCRKA' { param([int]$x, [int]$y) if (Test-OnLines $x $y $FewCracks 0.8) { , @(44, 36, 28) } else { Get-Stone $x $y } }
Save-Texture 'DWCRKB' { param([int]$x, [int]$y) if (Test-OnLines $x $y $ManyCracks 1.0) { , @(40, 32, 26) } else { Get-Stone $x $y } }
foreach ($stage in 0, 1, 2) {
    $frame = 'DWCRM' + [char](65 + $stage)
    Save-Texture $frame { param([int]$x, [int]$y) Get-Crumbling $x $y $stage } -LightOnly
}

# Glass: a steel frame with a cross bar, pale panes with streaks of light
Save-Texture 'DWGLAS' {
    param([int]$x, [int]$y)
    if ($x -lt 4 -or $x -gt 59 -or $y -lt 4 -or $y -gt 59 -or [Math]::Abs($x - 31.5) -lt 2 -or [Math]::Abs($y - 31.5) -lt 2) {
        if ($x -eq 0 -or $y -eq 0) { return , @(150, 154, 160) }
        return , @(84, 88, 96)
    }
    $streak = ($x + $y) % 22
    if ($streak -lt 2) { return , @(230, 245, 250) }
    , @(130, 180, 196)
}

# Boards: three planks nailed across a dark opening
Save-Texture 'DWBORD' {
    param([int]$x, [int]$y)
    foreach ($plank in @(@(10, 0), @(28, 2), @(46, -1))) {
        $top = $plank[0] + [Math]::Round($plank[1] * ($x - 32) / 32)
        if ($y -ge $top -and $y -lt $top + 11) {
            if ($y -eq $top) { return , @(160, 120, 76) }
            if ($y -eq $top + 10) { return , @(64, 44, 26) }
            if (($x -eq 6 -or $x -eq 57) -and ($y -eq $top + 3 -or $y -eq $top + 7)) { return , @(40, 40, 44) }
            $grain = [Math]::Sin(($x + $plank[0] * 3) * 0.4 + $y * 0.9) * 8
            return , @((122 + $grain), (86 + $grain), (50 + $grain))
        }
    }
    if ($x -lt 2 -or $x -gt 61) { return , @(70, 56, 40) }
    , @(18, 14, 12)
}

# The lever: a steel plate on the stone, its handle up (off) or down (thrown) with a lamp
function Get-Lever([int]$x, [int]$y, [bool]$Thrown) {
    if ($x -ge 20 -and $x -le 43 -and $y -ge 14 -and $y -le 49) {
        if ($x -eq 20 -or $y -eq 14) { return , @(160, 164, 172) }
        if ($x -eq 43 -or $y -eq 49) { return , @(40, 42, 48) }
        $knobY = if ($Thrown) { 41 } else { 22 }
        $lamp = if ($Thrown) { @(60, 220, 90) } else { @(220, 60, 40) }
        $dx = $x - 31.5; $dy = $y - $knobY
        if ($dx * $dx + $dy * $dy -le 14) { return , $lamp }
        if ([Math]::Abs($dx) -le 1.5 -and $y -ge 22 -and $y -le 41) { return , @(56, 58, 64) }
        return , @(100, 104, 112)
    }
    Get-Stone $x $y
}
Save-Texture 'DWLEVR' { param([int]$x, [int]$y) Get-Lever $x $y $false }
Save-Texture 'DWLEVD' { param([int]$x, [int]$y) Get-Lever $x $y $true }

# ---------------------------------------------------------------------------------------------
# The rubble a crumbled wall leaves: a heap of stones on the floor, see-through around it
# ---------------------------------------------------------------------------------------------

$sprites = New-Folder 'sprites'
$rubble = New-Object Drawing.Bitmap 64, 64
$stones = @(@(14, 58, 7, 5), @(26, 56, 9, 7), @(40, 58, 8, 5), @(50, 60, 6, 3), @(20, 51, 6, 4), @(33, 49, 7, 5), @(44, 52, 5, 4), @(8, 61, 5, 2))
for ($x = 0; $x -lt 64; $x++) {
    for ($y = 0; $y -lt 64; $y++) {
        foreach ($s in $stones) {
            $dx = ($x - $s[0]) / $s[2]; $dy = ($y - $s[1]) / $s[3]
            if ($dx * $dx + $dy * $dy -le 1) {
                $shade = if ($dy -lt -0.3) { 24 } elseif ($dy -gt 0.5) { -30 } else { 0 }
                $base = Get-Stone ($x * 3 % 64) ($y * 2 % 64)
                $rubble.SetPixel($x, $y, [Drawing.Color]::FromArgb(255, (Clamp ($base[0] + $shade)), (Clamp ($base[1] + $shade)), (Clamp ($base[2] + $shade))))
                break
            }
        }
    }
}
$rubble.Save((Join-Path $sprites 'DWRBA0.png'), [Drawing.Imaging.ImageFormat]::Png)
$rubble.Dispose()
"Wrote sprites/DWRBA0.png"

# ---------------------------------------------------------------------------------------------
# Sounds: 16-bit mono WAVs, synthesized
# ---------------------------------------------------------------------------------------------

function Save-Wav([string]$Name, [double]$Seconds, [scriptblock]$Sample) {
    $rate = 22050
    $count = [int]($rate * $Seconds)
    $ms = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter $ms
    $w.Write([Text.Encoding]::ASCII.GetBytes('RIFF')); $w.Write([int](36 + $count * 2))
    $w.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt ')); $w.Write([int]16)
    $w.Write([int16]1); $w.Write([int16]1); $w.Write([int]$rate); $w.Write([int]($rate * 2))
    $w.Write([int16]2); $w.Write([int16]16)
    $w.Write([Text.Encoding]::ASCII.GetBytes('data')); $w.Write([int]($count * 2))
    for ($i = 0; $i -lt $count; $i++) {
        $v = & $Sample ($i / $rate) ($i / $count)
        $w.Write([int16][Math]::Max(-32767, [Math]::Min(32767, $v * 32767)))
    }
    $w.Flush()
    $dir = New-Folder 'sounds'
    [IO.File]::WriteAllBytes((Join-Path $dir "$Name.wav"), $ms.ToArray())
    "Wrote sounds/$Name.wav"
}

# Glass: a bright crash of noise with ringing shards, fading fast
$glassRng = New-Object Random 3
Save-Wav 'DWGLASS' 0.5 {
    param([double]$t, [double]$f)
    $ring = [Math]::Sin(2 * [Math]::PI * 3100 * $t) * 0.3 + [Math]::Sin(2 * [Math]::PI * 4700 * $t) * 0.2
    (($glassRng.NextDouble() - 0.5) * 1.2 * [Math]::Pow(1 - $f, 3) + $ring * (1 - $f) * ([Math]::Floor($t * 40) % 3 -eq 0)) * 0.7
}

# Wood: a sharp snap, then a short splintering crackle
$woodRng = New-Object Random 4
Save-Wav 'DWWOOD' 0.35 {
    param([double]$t, [double]$f)
    $snap = [Math]::Sin(2 * [Math]::PI * (520 - 300 * $f) * $t) * [Math]::Exp(-$t * 40)
    ($snap + ($woodRng.NextDouble() - 0.5) * 0.6 * [Math]::Exp(-$t * 9) * ($woodRng.Next(4) -eq 0)) * 0.8
}

# A crack: a dull thump with a little grit
$crackRng = New-Object Random 6
Save-Wav 'DWCRACK' 0.25 {
    param([double]$t, [double]$f)
    ([Math]::Sin(2 * [Math]::PI * (110 - 50 * $f) * $t) * 0.8 + ($crackRng.NextDouble() - 0.5) * 0.3) * (1 - $f) * (1 - $f)
}

# Crumbling: a long low rumble with stones knocking
$rumbleRng = New-Object Random 8; $rumbleLast = 0.0
Save-Wav 'DWCRUMB' 1.0 {
    param([double]$t, [double]$f)
    $script:rumbleLast = $script:rumbleLast * 0.985 + ($rumbleRng.NextDouble() - 0.5) * 0.25
    $knock = if ($rumbleRng.Next(900) -eq 0) { 0.6 } else { 0 }
    ($script:rumbleLast * 3 + $knock) * [Math]::Sin([Math]::PI * [Math]::Min(1, $f * 4)) * (1 - $f * 0.7)
}

# ---------------------------------------------------------------------------------------------
# The level. One character per tile, its top-left corner at tile ($Left, $Top); every tile
# outside it is plain grey stone.
# ---------------------------------------------------------------------------------------------

$Left = 16
$Top = 20
$Layout = @(
    '#####################'   # y=20
    '####aaaaaaa##########'   #      the gallery behind the glass, with ammo in it
    '####aCaCaCa##########'
    '####GGGGGGG##########'   # y=23 the glass panes: shoot them out
    '##.............######'   # y=24 the hall
    'cB......S......Krrr##'   # y=25 boards over the closet (knife), the start, the cracked wall into
    '##.............#rTr##'   #      the secret room (shoot it, twice)
    '##.............#rrr##'
    'ddddLdhHHHhddddd#####'   # y=28 the south wall: the lever, and the 3-story stretch it brings down
    '#######.....#########'   # y=29 the corridor behind it
    '#######..w..#########'   #      the floor trigger
    '#######.....#########'
    '#######NNNNN#########'   # y=32 the wall the floor trigger takes away
    '#######eeeeeX########'   # y=33 the exit room and its elevator switch
    '#######eeeee#########'
    '#####################'
)
if (($Layout | ForEach-Object Length | Sort-Object -Unique).Count -ne 1) { throw 'The layout rows are different lengths' }

# What each character puts on plane 0 (a wall id or an area: area n is 107 + n), plane 1
# (objects), the height plane and the tag plane. Walls 70-78 are the mod's mapdefs/walls.yaml;
# 1 (grey stone) and 21 (the elevator switch) are Wolf3D's.
$Hall = 108; $Gallery = 109; $Secret = 110; $Corridor = 111; $Exit = 112; $Closet = 113
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)   # case matters
$Tiles['#'] = @{ Plane0 = 1 }
$Tiles['.'] = @{ Plane0 = $Hall }
$Tiles['a'] = @{ Plane0 = $Gallery }
$Tiles['C'] = @{ Plane0 = $Gallery; Plane1 = 49 }                       # a clip
$Tiles['r'] = @{ Plane0 = $Secret }
$Tiles['T'] = @{ Plane0 = $Secret; Plane1 = 55 }                        # a crown
$Tiles['c'] = @{ Plane0 = $Closet; Plane1 = 53 }                        # a chalice
$Tiles['S'] = @{ Plane0 = $Hall; Plane1 = 19 }                          # player start, facing north
$Tiles['G'] = @{ Plane0 = 70 }                                          # glass
$Tiles['B'] = @{ Plane0 = 71 }                                          # boards
$Tiles['K'] = @{ Plane0 = 72 }                                          # cracked wall (a secret)
$Tiles['d'] = @{ Plane0 = 78 }                                          # stone
$Tiles['h'] = @{ Plane0 = 78; Height = 3 }                              # stone, 3 stories
$Tiles['L'] = @{ Plane0 = 75; Tag = 1 }                                 # the lever
$Tiles['H'] = @{ Plane0 = 74; Height = 3; Tag = 1 }                     # what the lever brings down
$Tiles['w'] = @{ Plane0 = $Corridor; Plane1 = 460; Tag = 2 }            # the floor trigger
$Tiles['N'] = @{ Plane0 = 77; Tag = 2 }                                 # what it takes away
$Tiles['e'] = @{ Plane0 = $Exit }
$Tiles['X'] = @{ Plane0 = 21 }                                          # elevator switch

$Size = 64
$PlaneCount = 6
$HeightPlane = 3
$TagPlane = 4
$planes = @()
for ($p = 0; $p -lt $PlaneCount; $p++) { $planes += , (New-Object 'uint16[]' ($Size * $Size)) }
for ($i = 0; $i -lt $Size * $Size; $i++) { $planes[0][$i] = 1 }

for ($row = 0; $row -lt $Layout.Count; $row++) {
    $line = $Layout[$row]
    for ($col = 0; $col -lt $line.Length; $col++) {
        $ch = [string]$line[$col]
        if (!$Tiles.ContainsKey($ch)) { throw "Unknown layout character '$ch' at row $row, column $col" }
        $tile = $Tiles[$ch]
        $plane0 = $tile.Plane0
        # the corridor's floor is its own area, though it's drawn with dots like the hall
        if ($ch -eq '.' -and $row -ge 9) { $plane0 = $Corridor }
        $i = ($Top + $row) * $Size + $Left + $col
        $planes[0][$i] = $plane0
        if ($tile.Plane1) { $planes[1][$i] = $tile.Plane1 }
        if ($tile.Height) { $planes[$HeightPlane][$i] = $tile.Height }
        if ($tile.Tag) { $planes[$TagPlane][$i] = $tile.Tag }
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
$title = 'Breakable Walls'
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

$mapsDir = New-Folder 'maps'
$path = Join-Path $mapsDir 'MAP01.wad'
[IO.File]::WriteAllBytes($path, $out.ToArray())
"Wrote $path ($($out.Length) bytes)"
