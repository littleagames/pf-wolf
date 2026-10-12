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
# Every tile outside it is plain wall. You start at the south end and work north to the exit.
# The trap shooters sit inside eagle walls (mapdefs things with `in-wall: true`) and fire out
# of the face they look out of.
$Left = 25
$Top = 20
$Layout = @(
    '#######F#######'   # y=20 a fan shooter in the north wall, looking south into the exit room
    '#####.....X####'   #      the exit (used from the west)
    '#####.....#####'
    '#####..+..#####'
    '#######.#######'
    '######A.B######'   # y=25 aimed shooters either side of the passage
    '#######.#######'
    '####T......S###'   # y=27 the switched shooter, firing east along the corridor, and its switch
    '##########.####'
    '######.......##'   # y=29 the bone room
    '######.......##'
    '######.g.R...##'   #      the fireball ring, with a guard two tiles west of it
    '######.......##'
    '######.....h.##'   #      and another two tiles southeast
    '########...####'
    '#######e...####'   # y=35 the fire lanes: straight shooters across the passage
    '########...w###'
    '#######e...####'
    '########...w###'
    '########.+.####'
    '########...####'   # y=40
    '##...........##'   #      the pattern hall (the scripts' patterns)
    '##...........##'
    '#>...........<#'   #      sweepers either side, fanning back and forth
    '##.....*.....##'   # y=44 the skull pile's spiral
    '#>...........<#'
    '##...........##'
    '##...........##'
    '####^#...######'   # y=48 the homing launcher, looking north into the hall; the start alcove
    '######.P.######'   #      the player start
    '###############'
)

# What each character puts on plane 0 (walls and floor codes), plane 1 (objects) and the tag
# plane. Floor code 108 is area 1 (mapdefs floors: area n is 107 + n); walls and objects are
# mapdefs ids (pfwolf-pk3's mapdefs/wolf3d, and the mod's own walls 50-51 and things 510-535).
$Hall = 108
$Eagle = 6                                      # grey stone with an eagle: a trap wall
$Tiles = @{
    '#' = @{ Plane0 = 1 }                       # grey stone
    '.' = @{ Plane0 = $Hall }
    '+' = @{ Plane0 = $Hall; Plane1 = 48 }      # first aid kit
    'P' = @{ Plane0 = $Hall; Plane1 = 19 }      # player start, facing north
    'e' = @{ Plane0 = $Eagle; Plane1 = 510 }    # WallFireball, looking east
    'w' = @{ Plane0 = $Eagle; Plane1 = 512 }    # WallFireball, looking west
    'F' = @{ Plane0 = $Eagle; Plane1 = 517 }    # WallFireballFan, looking south
    'A' = @{ Plane0 = $Eagle; Plane1 = 518 }    # WallFireballAimed, looking east
    'B' = @{ Plane0 = $Eagle; Plane1 = 520 }    # WallFireballAimed, looking west
    'T' = @{ Plane0 = $Eagle; Plane1 = 522; Tag = 1 }  # SwitchedWallFireball, looking east
    'S' = @{ Plane0 = 50; Tag = 1 }             # its switch
    'R' = @{ Plane0 = $Hall; Plane1 = 526 }     # FireballRing
    'g' = @{ Plane0 = $Hall; Plane1 = 108 }     # guard, standing, facing east
    'h' = @{ Plane0 = $Hall; Plane1 = 110 }     # guard, standing, facing west
    'X' = @{ Plane0 = 21 }                      # elevator switch: ends the level
    '>' = @{ Plane0 = $Eagle; Plane1 = 527 }    # WallFireballSweep, looking east
    '<' = @{ Plane0 = $Eagle; Plane1 = 529 }    # WallFireballSweep, looking west
    '^' = @{ Plane0 = $Eagle; Plane1 = 532 }    # WallFireballHoming, looking north
    '*' = @{ Plane0 = $Hall; Plane1 = 535 }     # FireballSpiral
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
