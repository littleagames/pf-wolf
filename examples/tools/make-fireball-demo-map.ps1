# Builds examples/mods/fireball-demo/maps/MAP01.wad, the Fireball Demo level, from the layout
# drawn below. Run it after changing the layout:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-fireball-demo-map.ps1
#
# The level is an ECWolf binary map (a WAD holding a MAP01 marker lump and a WDC3.1 PLANES
# lump) with PFWolf's six planes: walls/floors, objects, flats, wall heights, tags and light
# zones. Only walls/floors, objects and tags are used.

$ErrorActionPreference = 'Stop'

# The layout, one character per tile, placed with its top-left corner at tile ($Left, $Top).
# Every tile outside it is plain wall. You start at the south end and run north to the exit,
# past trap walls (eagles) with a fireball shooter on the floor in front of each.
$Left = 28
$Top = 20
$Layout = @(
    '#########'   # y=20
    '##....X##'   #      the exit (used from the west)
    '##..+..##'
    '##.....##'
    '#Ea...bE#'   # y=24 a single shooter (west), a spreader (east)
    '##.....##'
    '##..+..##'
    '##.....##'
    '#Ec...dE#'   # y=28 a burster (west), the switched shooter (east)
    '##.....##'
    '##.....##'
    '##.....S#'   # y=31 the switch that turns the switched shooter off and on
    '##.....##'
    '##..P..##'   # y=33 the player start
    '#########'
)

# What each character puts on plane 0 (walls and floor codes), plane 1 (objects) and the tag
# plane. Floor code 108 is area 1 (mapdefs floors: area n is 107 + n); walls and objects are
# mapdefs ids (pfwolf-pk3's mapdefs/wolf3d, and the mod's own walls 50-51 and things 510-513).
$Hall = 108
$Tiles = @{
    '#' = @{ Plane0 = 1 }                       # grey stone
    'E' = @{ Plane0 = 6 }                       # grey stone with an eagle: a trap wall
    '.' = @{ Plane0 = $Hall }
    '+' = @{ Plane0 = $Hall; Plane1 = 48 }      # first aid kit
    'P' = @{ Plane0 = $Hall; Plane1 = 19 }      # player start, facing north
    'a' = @{ Plane0 = $Hall; Plane1 = 510 }     # FireballShooter
    'b' = @{ Plane0 = $Hall; Plane1 = 511 }     # FireballSpreader
    'c' = @{ Plane0 = $Hall; Plane1 = 512 }     # FireballBurster
    'd' = @{ Plane0 = $Hall; Plane1 = 513; Tag = 1 }  # SwitchedFireballShooter
    'S' = @{ Plane0 = 50; Tag = 1 }             # its switch
    'X' = @{ Plane0 = 21 }                      # elevator switch: ends the level
}

$Size = 64
$PlaneCount = 6
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
        $i = ($Top + $row) * $Size + $Left + $col
        $planes[0][$i] = $tile.Plane0
        if ($tile.Plane1) { $planes[1][$i] = $tile.Plane1 }
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
$title = 'Fireball Demo'
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

$mapsDir = Join-Path $PSScriptRoot '..\mods\fireball-demo\maps'
New-Item -ItemType Directory -Force $mapsDir | Out-Null
$path = Join-Path (Resolve-Path $mapsDir) 'MAP01.wad'
[IO.File]::WriteAllBytes($path, $out.ToArray())
"Wrote $path ($($out.Length) bytes)"
