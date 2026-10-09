# Builds Wolf3D's deathmatch arenas, pfwolf-pk3/maps/wolf3d/DM01.wad to DM04.wad, from the layouts
# drawn below. Run it after changing a layout:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-deathmatch-maps.ps1
#
# Each arena is drawn as its top-left quarter and mirrored across, then down, so every side
# is the same (the last column and row are the middle ones, not repeated). The middle tile is
# the level's own player start, which a deathmatch never uses but a level needs.
#
# Each level is an ECWolf binary map (a WAD holding a marker lump and a WDC3.1 PLANES lump)
# with PFWolf's six planes; only walls/floors and objects are used. The arenas are listed in
# gamepacks/wolf3d/game-info.yaml with `deathmatch: true`. -Show prints each whole layout.

param([switch]$Show)

$ErrorActionPreference = 'Stop'

# What the characters every arena shares put on plane 0 or plane 1 (mapdefs/wolf3d ids).
# Floors get their area codes worked out: each run of floor that doors close off is an area.
# A Dictionary, not a hashtable: hashtables don't tell 'm' from 'M'.
$Common = New-Object 'System.Collections.Generic.Dictionary[char,int[]]'
$Common['.'] = @(0, 0)              # floor
$Common['|'] = @(90, 0)             # door, in a wall running north-south
$Common['-'] = @(91, 0)             # door, in a wall running east-west
$Common['^'] = @(0, 410)            # deathmatch starts, facing north, east, south, west
$Common['>'] = @(0, 411)
$Common['v'] = @(0, 412)
$Common['<'] = @(0, 413)
$Common['P'] = @(0, 19)             # the level's own start (the middle tile)
$Common['h'] = @(0, 47)             # food
$Common['+'] = @(0, 48)             # first aid kit
$Common['m'] = @(0, 49)             # clip
$Common['M'] = @(0, 50)             # machine gun
$Common['G'] = @(0, 51)             # chaingun
$Common['o'] = @(0, 30)             # white column
$Common['g'] = @(0, 31)             # green plant
$Common['c'] = @(0, 27)             # chandelier
$Common['L'] = @(0, 37)             # ceiling light

# Walls: '#' each arena's own wall, '%' and '&' its others
$Arenas = @(
    @{
        Name = 'DM01'; Title = 'The Courtyard'
        Walls = @{ '#' = 1; '%' = 3 }                   # grey stone, grey stone with a flag
        Quarter = @(
            '###############'
            '#v.........m...'
            '#..............'
            '#..#####%#####-'
            '#..#...........'
            '#..#.>.........'
            '#..#...........'
            '#+.#..o.....o..'
            '#..#...........'
            '#..#.....L.....'
            '#.m#..o...h....'
            '#..#...........'
            '#..#.......M...'
            '#..#...........'
            '#..|.....+....P'
        )
    }
    @{
        Name = 'DM02'; Title = 'Cellblock'
        Walls = @{ '#' = 8; '%' = 9 }                   # blue stone
        Quarter = @(
            '###############'
            '#....#.........'
            '#.v..#....L....'
            '#..m.|...v.....'
            '#....#......+..'
            '##-###.%%%%%%%-'
            '#....#.%.......'
            '#....#.%.o...o.'
            '#.v..#.%.......'
            '#....#.%...G...'
            '#.+..#.%.......'
            '#....#.%.o...o.'
            '#..h.#.%.......'
            '#....#.%.......'
            '#.m....|......P'
        )
    }
    @{
        Name = 'DM03'; Title = 'Wooden Halls'
        Walls = @{ '#' = 12; '%' = 10 }                 # wood, wood with an eagle
        Quarter = @(
            '###############'
            '#......#.......'
            '#.>....#..c....'
            '#......%.......'
            '#..##.....##...'
            '#..##..m..##...'
            '#..............'
            '####..##.%%%%%.'
            '#.....##.%.....'
            '#.+...##.%..g..'
            '#.......c%.....'
            '#..##....%..G..'
            '#..##.^..%.....'
            '#........%.....'
            '#..h....M.....P'
        )
    }
    @{
        Name = 'DM04'; Title = 'The Red Pit'
        Walls = @{ '#' = 17; '%' = 18; '&' = 19 }      # red brick, with a swastika, purple
        Quarter = @(
            '###############'
            '#.....####.....'
            '#.v...####..v..'
            '#...+.####.....'
            '#.....####..o..'
            '#..m..####.....'
            '##%-%#####.....'
            '#..............'
            '#..o.....o.....'
            '#..............'
            '#..>....&&&....'
            '#.......&&&.G..'
            '#..o....&&&....'
            '#.....m........'
            '#......h......P'
        )
    }
)

$Size = 64
$PlaneCount = 6
$AreaStart = 108                    # area 1 (mapdefs/wolf3d/floors.yaml: area n is 107 + n)
$AreaCount = 36

function Swap([string]$line, [char]$a, [char]$b) {
    -join ($line.ToCharArray() | ForEach-Object { if ($_ -eq $a) { $b } elseif ($_ -eq $b) { $a } else { $_ } })
}

# The whole arena from its top-left quarter: mirrored right (west/east starts swap), then down
# (north/south starts swap)
function Expand([string[]]$quarter) {
    $width = $quarter[0].Length
    foreach ($row in $quarter) { if ($row.Length -ne $width) { throw "Row '$row' isn't $width wide" } }
    $rows = foreach ($row in $quarter) {
        $right = -join $row.Substring(0, $width - 1).ToCharArray()[($width - 2)..0]
        $row + (Swap $right '<' '>')
    }
    $bottom = for ($i = $rows.Count - 2; $i -ge 0; $i--) { Swap $rows[$i] '^' 'v' }
    @($rows) + @($bottom)
}

function Write-Arena($arena) {
    $layout = Expand $arena.Quarter
    $height = $layout.Count
    $width = $layout[0].Length
    $left = [int](($Size - $width) / 2)
    $top = [int](($Size - $height) / 2)
    $outerWall = $arena.Walls['#']

    $planes = @()
    for ($p = 0; $p -lt $PlaneCount; $p++) { $planes += , (New-Object 'uint16[]' ($Size * $Size)) }
    for ($i = 0; $i -lt $Size * $Size; $i++) { $planes[0][$i] = $outerWall }

    # Walls, doors and things; floors are marked 0 for now
    $floor = New-Object 'bool[,]' $width, $height
    for ($y = 0; $y -lt $height; $y++) {
        for ($x = 0; $x -lt $width; $x++) {
            $ch = $layout[$y][$x]
            $i = ($top + $y) * $Size + $left + $x
            if ($arena.Walls.ContainsKey([string]$ch)) {
                $planes[0][$i] = $arena.Walls[[string]$ch]
                continue
            }
            if (!$Common.ContainsKey($ch)) { throw "$($arena.Name): unknown character '$ch' at $x,$y" }
            $tile = $Common[$ch]
            $planes[0][$i] = $tile[0]
            $planes[1][$i] = $tile[1]
            if ($tile[0] -eq 0) { $floor[$x, $y] = $true }
            elseif ($x -eq 0 -or $y -eq 0 -or $x -eq $width - 1 -or $y -eq $height - 1) { throw "$($arena.Name): door on the edge at $x,$y" }
        }
    }

    # Areas: floor reached without going through a door
    $areas = 0
    for ($y = 0; $y -lt $height; $y++) {
        for ($x = 0; $x -lt $width; $x++) {
            if (!$floor[$x, $y] -or $planes[0][($top + $y) * $Size + $left + $x] -ne 0) { continue }
            if ($areas -ge $AreaCount) { throw "$($arena.Name): more than $AreaCount areas" }
            $code = $AreaStart + $areas
            $areas++
            $queue = New-Object 'System.Collections.Generic.Queue[int[]]'
            $queue.Enqueue(@($x, $y))
            $planes[0][($top + $y) * $Size + $left + $x] = $code
            while ($queue.Count -gt 0) {
                $at = $queue.Dequeue()
                foreach ($step in @(@(1, 0), @(-1, 0), @(0, 1), @(0, -1))) {
                    $nx = $at[0] + $step[0]; $ny = $at[1] + $step[1]
                    if ($nx -lt 0 -or $ny -lt 0 -or $nx -ge $width -or $ny -ge $height) { throw "$($arena.Name): floor reaches the edge at $($at[0]),$($at[1])" }
                    $ni = ($top + $ny) * $Size + $left + $nx
                    if ($floor[$nx, $ny] -and $planes[0][$ni] -eq 0) {
                        $planes[0][$ni] = $code
                        $queue.Enqueue(@($nx, $ny))
                    }
                }
            }
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
    $title = $arena.Title
    [Text.Encoding]::ASCII.GetBytes($title, 0, [Math]::Min($title.Length, 16), $name, 0) | Out-Null
    $bw.Write($name)
    $bw.Write([uint16]$Size)
    $bw.Write([uint16]$Size)
    foreach ($plane in $planes) { foreach ($value in $plane) { $bw.Write([uint16]$value) } }
    $bw.Flush()
    $planesLump = $ms.ToArray()

    # A PWAD: header, the PLANES data, then the directory (the map's marker, then PLANES)
    $out = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter $out
    $w.Write([Text.Encoding]::ASCII.GetBytes('PWAD'))
    $w.Write([int]2)
    $w.Write([int](12 + $planesLump.Length))
    $w.Write($planesLump)
    foreach ($lump in @(@(12, 0, $arena.Name), @(12, $planesLump.Length, 'PLANES'))) {
        $w.Write([int]$lump[0])
        $w.Write([int]$lump[1])
        $lumpName = New-Object byte[] 8
        [Text.Encoding]::ASCII.GetBytes($lump[2], 0, $lump[2].Length, $lumpName, 0) | Out-Null
        $w.Write($lumpName)
    }
    $w.Flush()

    $path = Join-Path $mapsDir "$($arena.Name).wad"
    [IO.File]::WriteAllBytes($path, $out.ToArray())
    "Wrote ${path}: $($arena.Title), ${width}x$height, $areas area(s)"
    if ($Show) { $layout; '' }
}

$mapsDir = Join-Path $PSScriptRoot '..\..\pfwolf-pk3\maps\wolf3d'
New-Item -ItemType Directory -Force $mapsDir | Out-Null
$mapsDir = (Resolve-Path $mapsDir).Path
foreach ($arena in $Arenas) { Write-Arena $arena }
