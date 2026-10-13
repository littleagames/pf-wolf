# Builds examples/mods/patch-demo: its patch pictures (textures/*.png) and its level
# (maps/MAP01.wad), from the drawing code and the layout below. Run it after changing either:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-patch-demo.ps1
#
# The pictures are drawn on a clear background: what's clear is see-through, so the wall a patch
# is on shows around it. Colors snap to the nearest palette color as the game loads them. The
# level is an ECWolf binary map (a WAD holding a MAP01 marker lump and a WDC3.1 PLANES lump)
# with PFWolf's six planes; a wall's patch is the high byte of its plane 0 value.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$modDir = Join-Path $PSScriptRoot '..\mods\patch-demo'
$texturesDir = Join-Path $modDir 'textures'
New-Item -ItemType Directory -Force $texturesDir | Out-Null

function Brush([int]$r, [int]$g, [int]$b) { New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, $r, $g, $b)) }
function Pen([int]$r, [int]$g, [int]$b, [float]$width) { New-Object Drawing.Pen ([Drawing.Color]::FromArgb(255, $r, $g, $b)), $width }

# Draws a picture on a clear background, with hard edges (no half see-through pixels), and saves it
function Save-Patch([string]$Name, [int]$Height, [scriptblock]$Paint) {
    $bitmap = New-Object Drawing.Bitmap 64, $Height
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::None
    $g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.Clear([Drawing.Color]::Transparent)
    & $Paint $g
    $g.Dispose()
    $path = Join-Path (Resolve-Path $texturesDir) "$Name.png"
    $bitmap.Save($path, [Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    "Wrote $path"
}

# A brass plaque with lines of engraving: the same patch goes on any wall
Save-Patch 'PDPLAQ' 64 {
    param($g)
    $g.FillRectangle((Brush 120 84 20), 14, 16, 36, 26)
    $g.FillRectangle((Brush 216 172 48), 16, 18, 32, 22)
    $g.FillRectangle((Brush 120 84 20), 20, 22, 24, 2)
    $g.FillRectangle((Brush 120 84 20), 20, 27, 18, 2)
    $g.FillRectangle((Brush 120 84 20), 20, 32, 22, 2)
    foreach ($x in 16, 46) { foreach ($y in 18, 38) { $g.FillRectangle((Brush 80 56 12), $x, $y, 2, 2) } }
}

# Cracks running down the wall
Save-Patch 'PDCRACK' 64 {
    param($g)
    $dark = Pen 24 24 24 2
    $g.DrawLines($dark, [Drawing.Point[]]@((New-Object Drawing.Point 30, 0), (New-Object Drawing.Point 26, 10), (New-Object Drawing.Point 32, 20),
        (New-Object Drawing.Point 28, 31), (New-Object Drawing.Point 35, 42), (New-Object Drawing.Point 31, 52)))
    $g.DrawLines($dark, [Drawing.Point[]]@((New-Object Drawing.Point 32, 20), (New-Object Drawing.Point 41, 25), (New-Object Drawing.Point 46, 23)))
    $g.DrawLines($dark, [Drawing.Point[]]@((New-Object Drawing.Point 28, 31), (New-Object Drawing.Point 19, 36), (New-Object Drawing.Point 16, 44)))
}

# A red banner two stories long, hung from a rod at the top of the upper story
Save-Patch 'PDBANR' 128 {
    param($g)
    $g.FillRectangle((Brush 96 64 32), 10, 4, 44, 4)
    $g.FillPolygon((Brush 216 172 48), [Drawing.Point[]]@((New-Object Drawing.Point 15, 8), (New-Object Drawing.Point 49, 8),
        (New-Object Drawing.Point 49, 116), (New-Object Drawing.Point 32, 102), (New-Object Drawing.Point 15, 116)))
    $g.FillPolygon((Brush 168 0 0), [Drawing.Point[]]@((New-Object Drawing.Point 18, 8), (New-Object Drawing.Point 46, 8),
        (New-Object Drawing.Point 46, 110), (New-Object Drawing.Point 32, 98), (New-Object Drawing.Point 18, 110)))
    $g.FillEllipse((Brush 252 252 252), 21, 24, 22, 22)
    $g.FillRectangle((Brush 0 0 0), 30, 28, 4, 14)
    $g.FillRectangle((Brush 0 0 0), 25, 33, 14, 4)
}

# An alarm lamp, off and lit (the PDALARM animation blinks between them)
foreach ($lamp in @(@('PDALRM1', 96, 0, 0), @('PDALRM2', 252, 32, 32))) {
    Save-Patch $lamp[0] 64 {
        param($g)
        $g.FillRectangle((Brush 72 72 72), 22, 30, 20, 6)
        $g.FillPie((Brush $lamp[1] $lamp[2] $lamp[3]), 24, 16, 16, 28, 180, 180)
        if ($lamp[1] -gt 200) { $g.FillRectangle((Brush 252 252 140), 28, 20, 3, 4) }
    }
}

# A first aid cabinet, taken off the wall when it's used
Save-Patch 'PDMEDK' 64 {
    param($g)
    $g.FillRectangle((Brush 140 140 140), 18, 16, 28, 28)
    $g.FillRectangle((Brush 236 236 236), 20, 18, 24, 24)
    $g.FillRectangle((Brush 216 0 0), 29, 22, 6, 16)
    $g.FillRectangle((Brush 216 0 0), 24, 27, 16, 6)
}

# A box of ammo on a shelf
Save-Patch 'PDAMMO' 64 {
    param($g)
    $g.FillRectangle((Brush 96 64 32), 10, 44, 44, 4)
    $g.FillRectangle((Brush 72 84 32), 18, 28, 28, 16)
    $g.FillRectangle((Brush 216 172 48), 18, 33, 28, 4)
}

# A button, out and pressed in
foreach ($button in @(@('PDBTN1', 7, 216), @('PDBTN2', 5, 120))) {
    Save-Patch $button[0] 64 {
        param($g)
        $g.FillRectangle((Brush 100 100 100), 22, 22, 20, 20)
        $g.FillRectangle((Brush 160 160 160), 24, 24, 16, 16)
        $radius = $button[1]
        $g.FillEllipse((Brush $button[2] 0 0), 32 - $radius, 32 - $radius, 2 * $radius, 2 * $radius)
    }
}

# A lever, up and down
foreach ($lever in @(@('PDLEVR1', 14), @('PDLEVR2', 40))) {
    Save-Patch $lever[0] 64 {
        param($g)
        $g.FillRectangle((Brush 100 100 100), 24, 18, 16, 28)
        $g.FillRectangle((Brush 60 60 60), 30, 22, 4, 20)
        $tip = $lever[1]
        $g.FillRectangle((Brush 32 32 32), 31, [Math]::Min($tip, 32), 2, [Math]::Abs(32 - $tip))
        $g.FillEllipse((Brush 216 0 0), 28, $tip - 3, 8, 8)
    }
}

# The layout, one character per tile, its top-left corner at tile ($Left, $Top). Every tile
# outside it is plain grey stone.
$Left = 16
$Top = 20
$Layout = @(
    '#########################'   # y=20
    '#####GBWKGBWK#ATT########'   #      the north wall: plaques on four kinds of wall, the alarm
                                  #      lamp, two 2-story walls with banners
    '#sCs#............#eee####'   # y=22 the secret room (its crown), the hall, the exit room
    '#sssP.S..........DeeeX###'   #      the cracked pushwall, the player start, the door, the elevator
    '#sss#............#eee####'
    '#####............########'
    '#####............########'
    '######c#m#o#u#v##########'   # y=27 the south wall: crack, first aid, ammo, button, lever
    '#########################'
)
if (($Layout | ForEach-Object Length | Sort-Object -Unique).Count -ne 1) { throw 'The layout rows are different lengths' }

# What each character puts on plane 0 (a wall, door or floor code, plus its patch * 256),
# plane 1 (objects), the height plane and the tag plane. Walls and doors are pfwolf-pk3's
# mapdefs/wolf3d ids; patches are the mod's mapdefs/patches.yaml. Floor codes are areas
# (area n is 107 + n).
$Hall = 108; $SecretRoom = 109; $ExitRoom = 110
$Grey = 1; $Blue = 8; $Wood = 12; $Brick = 17
$Plaque = 1; $Crack = 2; $Banner = 3; $Alarm = 4; $FirstAid = 5; $Ammo = 6; $Button = 7; $Lever = 9
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)   # case matters: 's' and 'S'
$Tiles['#'] = @{ Plane0 = $Grey }
$Tiles['.'] = @{ Plane0 = $Hall }
$Tiles['s'] = @{ Plane0 = $SecretRoom }
$Tiles['e'] = @{ Plane0 = $ExitRoom }
$Tiles['S'] = @{ Plane0 = $Hall; Plane1 = 20 }                          # player start, facing east
$Tiles['C'] = @{ Plane0 = $SecretRoom; Plane1 = 55 }                    # crown (treasure)
$Tiles['G'] = @{ Plane0 = $Grey; Patch = $Plaque }
$Tiles['B'] = @{ Plane0 = $Blue; Patch = $Plaque }
$Tiles['W'] = @{ Plane0 = $Wood; Patch = $Plaque }
$Tiles['K'] = @{ Plane0 = $Brick; Patch = $Plaque }
$Tiles['T'] = @{ Plane0 = $Grey; Patch = $Banner; Height = 2 }
$Tiles['A'] = @{ Plane0 = $Grey; Patch = $Alarm }
$Tiles['P'] = @{ Plane0 = $Grey; Patch = $Crack; Plane1 = 98 }          # pushwall (mapdefs trigger 98)
$Tiles['c'] = @{ Plane0 = $Grey; Patch = $Crack }
$Tiles['m'] = @{ Plane0 = $Grey; Patch = $FirstAid }
$Tiles['o'] = @{ Plane0 = $Grey; Patch = $Ammo }
$Tiles['u'] = @{ Plane0 = $Grey; Patch = $Button }
$Tiles['v'] = @{ Plane0 = $Grey; Patch = $Lever; Tag = 1 }             # the lever and the door it works
$Tiles['D'] = @{ Plane0 = 90; Tag = 1 }                                 # door (north-south)
$Tiles['X'] = @{ Plane0 = 21 }                                          # elevator switch: ends the level

$Size = 64
$PlaneCount = 6
$HeightPlane = 3
$TagPlane = 4
$planes = @()
for ($p = 0; $p -lt $PlaneCount; $p++) { $planes += , (New-Object 'uint16[]' ($Size * $Size)) }
for ($i = 0; $i -lt $Size * $Size; $i++) { $planes[0][$i] = $Grey }

for ($row = 0; $row -lt $Layout.Count; $row++) {
    $line = $Layout[$row]
    for ($col = 0; $col -lt $line.Length; $col++) {
        $ch = [string]$line[$col]
        if (!$Tiles.ContainsKey($ch)) { throw "Unknown layout character '$ch' at row $row, column $col" }
        $tile = $Tiles[$ch]
        $i = ($Top + $row) * $Size + $Left + $col
        $planes[0][$i] = $tile.Plane0 + 256 * [int]$tile.Patch
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
$title = 'Patch Demo'
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

$mapsDir = Join-Path $modDir 'maps'
New-Item -ItemType Directory -Force $mapsDir | Out-Null
$path = Join-Path (Resolve-Path $mapsDir) 'MAP01.wad'
[IO.File]::WriteAllBytes($path, $out.ToArray())
"Wrote $path ($($out.Length) bytes)"
