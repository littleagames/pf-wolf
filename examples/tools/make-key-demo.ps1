# Builds examples/mods/key-demo: the status bar pictures of its two new keys
# (graphics/redkey.png, graphics/bluekey.png) and its level (maps/MAP01.wad), from the drawing
# and the layout below. Run it after changing either:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-key-demo.ps1
#
# The level is an ECWolf binary map (a WAD holding a MAP01 marker lump and a WDC3.1 PLANES
# lump) with PFWolf's six planes: walls/floors, objects, flats, wall heights, tags and light
# zones (only the first two are used here).
#
# The way through: the red and gold keys lie in the hall. Any two keys open the west door
# (the blue key's room); the red and blue keys together open the east door (the silver key's
# room); all four open the north door, to the exit.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# The key pictures: 8x16, like Wolf3D's gold and silver ones, on a clear background so the
# status bar's empty key slot shows around them. One character per pixel: '.' clear, then the
# key's light, middle and dark shades. Colors snap to the nearest palette color as the game
# loads them.
$KeyPic = @(
    '........'
    '........'
    '........'
    '..LLLM..'
    '.LM..MD.'
    '.L....D.'
    '.MD..DD.'
    '..MDDD..'
    '...MD...'
    '...MD...'
    '...MDLD.'
    '...MD...'
    '...MDLD.'
    '...MD...'
    '........'
    '........'
)
$KeyColors = @{
    redkey  = @{ L = @(255, 128, 112); M = @(220, 0, 0); D = @(120, 0, 0) }
    bluekey = @{ L = @(176, 220, 255); M = @(64, 152, 255); D = @(16, 72, 200) }
}
$graphicsDir = Join-Path $PSScriptRoot '..\mods\key-demo\graphics'
New-Item -ItemType Directory -Force $graphicsDir | Out-Null
foreach ($name in $KeyColors.Keys) {
    $bitmap = New-Object Drawing.Bitmap 8, 16
    for ($y = 0; $y -lt 16; $y++) {
        for ($x = 0; $x -lt 8; $x++) {
            $shade = [string]$KeyPic[$y][$x]
            $color = if ($shade -eq '.') { [Drawing.Color]::Transparent } else {
                $rgb = $KeyColors[$name][$shade]; [Drawing.Color]::FromArgb(255, $rgb[0], $rgb[1], $rgb[2]) }
            $bitmap.SetPixel($x, $y, $color)
        }
    }
    $path = Join-Path (Resolve-Path $graphicsDir) "$name.png"
    $bitmap.Save($path, [Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    "Wrote $path"
}

# The layout, one character per tile, placed with its top-left corner at tile ($Left, $Top).
# Every tile outside it is plain wall.
$Left = 16
$Top = 20
$Layout = @(
    '#########################'   # y=20
    '#########eeeeeee#########'   #      the exit room
    '#########eeeeeeeX########'   #      the elevator switch
    '#########eeeeeee#########'
    '############A############'   # y=24 the all-four-keys door
    '#######.............#####'   #      the hall
    '#bbbbb#.............#sss#'   #      the blue key's room, the silver key's room
    '#bbbbb#.............#sss#'
    '#bb3bbM..1.......2..Ns4s#'   # y=28 any-two door, keys 1-4, red+blue door
    '#bbbbb#.............#sss#'
    '#bbbbb#.............#sss#'
    '#######.............#####'
    '#######.....P.......#####'   # y=32 the player start, facing north
    '#########################'
)

# What each character puts on plane 0 (walls, doors and floor codes: area n is 107 + n) and
# plane 1 (objects). Doors 80-82 are this mod's mapdefs/doors.yaml, things 500-501 its
# mapdefs/things.yaml; the rest are pfwolf-pk3's mapdefs/wolf3d.
$Hall = 108; $BlueRoom = 109; $SilverRoom = 110; $ExitRoom = 111
$Tiles = @{
    '#' = @{ Plane0 = 1 }                                   # grey stone
    '.' = @{ Plane0 = $Hall }
    'b' = @{ Plane0 = $BlueRoom }
    's' = @{ Plane0 = $SilverRoom }
    'e' = @{ Plane0 = $ExitRoom }
    'P' = @{ Plane0 = $Hall; Plane1 = 19 }                  # player start, facing north
    '1' = @{ Plane0 = $Hall; Plane1 = 500 }                 # red key
    '2' = @{ Plane0 = $Hall; Plane1 = 43 }                  # gold key
    '3' = @{ Plane0 = $BlueRoom; Plane1 = 501 }             # blue key
    '4' = @{ Plane0 = $SilverRoom; Plane1 = 44 }            # silver key
    'N' = @{ Plane0 = 80 }                                  # red + blue door (north-south)
    'A' = @{ Plane0 = 81 }                                  # all-four-keys door (east-west)
    'M' = @{ Plane0 = 82 }                                  # any-two-keys door (north-south)
    'X' = @{ Plane0 = 21 }                                  # elevator switch: ends the level
}

$Size = 64
$PlaneCount = 6
$planes = @()
for ($p = 0; $p -lt $PlaneCount; $p++) { $planes += , (New-Object 'uint16[]' ($Size * $Size)) }
for ($i = 0; $i -lt $Size * $Size; $i++) { $planes[0][$i] = 1 }

for ($row = 0; $row -lt $Layout.Count; $row++) {
    $line = $Layout[$row]
    for ($col = 0; $col -lt $line.Length; $col++) {
        $ch = [string]$line[$col]
        if (!$Tiles.ContainsKey($ch)) { throw "Unknown layout character '$ch' at row $row, column $col" }
        $tile = $Tiles[$ch]
        $i = ($Top + $row) * $Size + $Left + $col
        $planes[0][$i] = $tile.Plane0
        if ($tile.Plane1) { $planes[1][$i] = $tile.Plane1 }
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
$title = 'Key Demo'
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

$mapsDir = Join-Path $PSScriptRoot '..\mods\key-demo\maps'
New-Item -ItemType Directory -Force $mapsDir | Out-Null
$path = Join-Path (Resolve-Path $mapsDir) 'MAP01.wad'
[IO.File]::WriteAllBytes($path, $out.ToArray())
"Wrote $path ($($out.Length) bytes)"
