# Builds examples/mods/faction-demo/maps/MAP01.wad, the Faction Demo level, from the layout
# drawn below. Run it after changing the layout:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-faction-demo-map.ps1
#
# The level is an ECWolf binary map (a WAD holding a MAP01 marker lump and a WDC3.1 PLANES
# lump) with PFWolf's six planes: walls/floors, objects, flats, wall heights, tags and light
# zones. Only walls/floors and objects are used.

$ErrorActionPreference = 'Stop'

# The layout, one character per tile, placed with its top-left corner at tile ($Left, $Top).
# Every tile outside it is plain wall. You start at the south end with your squad, free the
# prisoners in the cell block, then open the hall, where the axis and the mutants are already
# at each other's throats, and fight through to the exit.
$Left = 20
$Top = 10
$Layout = @(
    '#########################'   # y=10
    '##########.....X#########'   #      the exit room; the elevator switch (used from the west)
    '##########.....##########'
    '############=############'   #      door
    '##HHHHHHHHHHHHHHHHHHHHH##'   # y=14 the hall: the axis (west) facing the mutants (east)
    '##HHgHHsHHHHHHHHHmHHmHH##'
    '##HHHHHHHHHHHHHHHHHHHHH##'
    '##HHgHHsHHHHHHHHHmHHmHH##'
    '##HHHHHHHHHHHHHHHHHHHHH##'
    '############=############'   # y=19 door
    '######CpCpCCCCCpCpC######'   # y=20 the cell block: four prisoners along the north wall
    '######CCCCCCCCCCCCC######'
    '######CCCCCC+CCCCCC######'   #      a first aid kit
    '############=############'   # y=23 door
    '##########SSSSS##########'   # y=24 the start room: your squad, ahead of you
    '##########SfSfS##########'
    '##########SSSSS##########'
    '##########SSPSS##########'   #      the player start, facing north
    '#########################'   # y=28
)

# What each character puts on plane 0 (walls, doors and floor codes) and plane 1 (objects).
# Floor code 107 + n is area n (mapdefs floors), one per room, so a room's actors only wake once
# a door to it is open; walls, doors and objects are mapdefs ids (pfwolf-pk3's mapdefs/wolf3d,
# and the mod's own things 540-545).
$Exit = 108; $Hall = 109; $Cells = 110; $Start = 111
# (Case matters: S and s, P and p are different tiles, and PowerShell's @{} ignores case)
$Tiles = New-Object System.Collections.Hashtable ([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 1 }                        # grey stone
$Tiles['.'] = @{ Plane0 = $Exit }
$Tiles['X'] = @{ Plane0 = 21 }                       # elevator switch: ends the level
$Tiles['='] = @{ Plane0 = 91 }                       # a door, passed through north-south
$Tiles['H'] = @{ Plane0 = $Hall }
$Tiles['g'] = @{ Plane0 = $Hall; Plane1 = 108 }      # guard (axis), standing, facing east
$Tiles['s'] = @{ Plane0 = $Hall; Plane1 = 126 }      # SS (axis), standing, facing east
$Tiles['m'] = @{ Plane0 = $Hall; Plane1 = 545 }      # mutant (the mutants' side), facing west
$Tiles['C'] = @{ Plane0 = $Cells }
$Tiles['p'] = @{ Plane0 = $Cells; Plane1 = 541 }     # prisoner, facing south
$Tiles['+'] = @{ Plane0 = $Cells; Plane1 = 48 }      # a first aid kit
$Tiles['S'] = @{ Plane0 = $Start }
$Tiles['f'] = @{ Plane0 = $Start; Plane1 = 540 }     # one of your squad, facing north
$Tiles['P'] = @{ Plane0 = $Start; Plane1 = 19 }      # player start, facing north
$Size = 64
$PlaneCount = 6
$planes = @()
for ($p = 0; $p -lt $PlaneCount; $p++) { $planes += , (New-Object 'uint16[]' ($Size * $Size)) }
for ($i = 0; $i -lt $Size * $Size; $i++) { $planes[0][$i] = 1 }

for ($row = 0; $row -lt $Layout.Count; $row++) {
    $line = $Layout[$row]
    if ($line.Length -ne $Layout[0].Length) { throw "Layout row $row is $($line.Length) wide, not $($Layout[0].Length)" }
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
$title = 'Faction Demo'
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

$mapsDir = Join-Path $PSScriptRoot '..\mods\faction-demo\maps'
New-Item -ItemType Directory -Force $mapsDir | Out-Null
$path = Join-Path (Resolve-Path $mapsDir) 'MAP01.wad'
[IO.File]::WriteAllBytes($path, $out.ToArray())
"Wrote $path ($($out.Length) bytes)"
