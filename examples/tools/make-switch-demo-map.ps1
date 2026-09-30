# Builds examples/mods/switch-demo/maps/MAP01.wad, the Switch Demo level, from the layout
# drawn below. Run it after changing the layout:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-switch-demo-map.ps1
#
# The level is an ECWolf binary map (a WAD holding a MAP01 marker lump and a WDC3.1 PLANES
# lump) with PFWolf's five planes: walls/floors, objects, flats, wall heights and tags.

$ErrorActionPreference = 'Stop'

# The layout, one character per tile, placed with its top-left corner at tile ($Left, $Top).
# Every tile outside it is plain wall.
$Left = 16
$Top = 20
$Layout = @(
    '#################################'   # y=20
    '#################################'
    '############aaaaa################'   #      the door room (behind station 1's door)
    '############aaaaa################'
    '############aaCaa################'
    '############aaaaa################'
    '############aaaaa################'
    '###########1##D####2#############'   # y=27 station 1 and its door, station 2
    '########.................########'   #      the hall
    '########.................########'
    '#######3.................5#######'   # y=30 station 3, station 5
    '########...L....L....L...########'
    '##sssss#.................#eeeee##'   #      the secret room, the exit room
    '##sssss#.................#eeeee##'
    '##sKsssB.................VeeeeeX#'   # y=34 the brick pushwall, station 5's door, the exit
    '##sssss#...L.........L...#eeeee##'
    '##sssss#.................#eeeee##'
    '#######4........P........########'   # y=37 station 4, the player start
    '########.................########'
    '##########FFFFF##################'   # y=39 station 4's walls
    '#################################'
    '#################################'
)

# What each character puts on plane 0 (walls and floor codes), plane 1 (objects) and the tag
# plane. Floor codes are areas (mapdefs floors: area n is 107 + n); walls, doors and objects
# are mapdefs ids (pfwolf-pk3's mapdefs/wolf3d, and the mod's own walls 50-59 and thing 500).
$Hall = 108; $DoorRoom = 109; $ExitRoom = 110; $SecretRoom = 111
$Tiles = @{
    '#' = @{ Plane0 = 1 }                                   # grey stone
    '.' = @{ Plane0 = $Hall }
    'a' = @{ Plane0 = $DoorRoom }
    'e' = @{ Plane0 = $ExitRoom }
    's' = @{ Plane0 = $SecretRoom }
    'P' = @{ Plane0 = $Hall; Plane1 = 19 }                  # player start, facing north
    'L' = @{ Plane0 = $Hall; Plane1 = 500; Tag = 2 }        # SwitchLightOff
    'C' = @{ Plane0 = $DoorRoom; Plane1 = 52 }              # cross (treasure)
    'K' = @{ Plane0 = $SecretRoom; Plane1 = 44 }            # silver key
    '1' = @{ Plane0 = 50; Tag = 1 }                         # station 1: door toggle
    'D' = @{ Plane0 = 93; Tag = 1 }                         # gold-locked door (east-west)
    '2' = @{ Plane0 = 52; Tag = 2 }                         # station 2: lights
    '3' = @{ Plane0 = 54; Tag = 3 }                         # station 3: pushwall
    'B' = @{ Plane0 = 17; Tag = 3 }                         # brick wall it pushes
    '4' = @{ Plane0 = 56; Tag = 4 }                         # station 4: flags
    'F' = @{ Plane0 = 1; Tag = 4 }                          # walls that get flags
    '5' = @{ Plane0 = 58; Tag = 5 }                         # station 5: locked, opens the exit
    'V' = @{ Plane0 = 92; Tag = 5 }                         # gold-locked door (north-south)
    'X' = @{ Plane0 = 21 }                                  # elevator switch: ends the level
}

$Size = 64
$PlaneCount = 5
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
$title = 'Switch Demo'
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

$mapsDir = Join-Path $PSScriptRoot '..\mods\switch-demo\maps'
New-Item -ItemType Directory -Force $mapsDir | Out-Null
$path = Join-Path (Resolve-Path $mapsDir) 'MAP01.wad'
[IO.File]::WriteAllBytes($path, $out.ToArray())
"Wrote $path ($($out.Length) bytes)"
