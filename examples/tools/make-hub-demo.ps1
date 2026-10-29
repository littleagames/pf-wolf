# Builds examples/mods/hub-demo's four levels (maps/MAP01.wad, the hub, and maps/HUB1.wad to
# HUB3.wad, its levels) from the layouts below. Run it after changing one:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-hub-demo.ps1
#
# Each level is an ECWolf binary map (a WAD holding a marker lump named for the level and a
# WDC3.1 PLANES lump) with PFWolf's six planes: walls/floors, objects, flats, wall heights, tags
# and light zones (only walls/floors, objects and tags are used here). A level is named by its
# file name; the mod's game-info.yaml says how they join up.
#
# The way through: the hub's west elevator goes to the Cellblock, its east elevator to the
# Armory, and the teleport pad north of its hall to the Chapel. Each of them has an emblem and an
# elevator back to the hub. The vault south of the hall needs all three emblems; its elevator
# wins the game.

$ErrorActionPreference = 'Stop'

$Size = 64
$PlaneCount = 6
$TagPlane = 4

# Tiles every level shares: plane 0 (walls, doors and floor codes: area n is 107 + n) and plane
# 1 (objects). Walls, doors and things are pfwolf-pk3's mapdefs/wolf3d, else this mod's own:
# walls 50-53, door 80, player starts 600-602, trigger 610 and things 620-622.
# Keys are case-sensitive, so 'a' and 'A' are different tiles.
function New-Tiles([hashtable[]]$tables) {
    $tiles = New-Object Collections.Hashtable ([StringComparer]::Ordinal)
    foreach ($table in $tables) { foreach ($key in $table.Keys) { $tiles[$key] = $table[$key] } }
    $tiles
}
$Common = New-Object Collections.Hashtable ([StringComparer]::Ordinal)
$Common['E'] = @{ Plane0 = 21 }        # elevator wall (its lever faces only east and west)
$Common['X'] = @{ Plane0 = 21 }        # elevator switch: A_Exit, back to the hub (or, in the vault, the win)
$Common['z'] = @{ Plane0 = 100 }       # elevator door, passed east-west
$Common['D'] = @{ Plane0 = 91 }        # plain door, passed north-south

# Writes one level: the layout's top-left corner at tile ($left, $top), every tile outside it
# plain wall ($wall)
function Write-Level([string]$name, [string]$title, [int]$wall, [int]$left, [int]$top, $tiles, [string[]]$layout) {
    $planes = @()
    for ($p = 0; $p -lt $PlaneCount; $p++) { $planes += , (New-Object 'uint16[]' ($Size * $Size)) }
    for ($i = 0; $i -lt $Size * $Size; $i++) { $planes[0][$i] = $wall }

    $width = $layout[0].Length
    for ($row = 0; $row -lt $layout.Count; $row++) {
        $line = $layout[$row]
        if ($line.Length -ne $width) { throw "${name}: row $row is $($line.Length) wide, not $width" }
        for ($col = 0; $col -lt $line.Length; $col++) {
            $ch = [string]$line[$col]
            if ($ch -eq '#') { continue }
            if (!$tiles.ContainsKey($ch)) { throw "${name}: unknown layout character '$ch' at row $row, column $col" }
            $tile = $tiles[$ch]
            $i = ($top + $row) * $Size + $left + $col
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
    $nameBytes = New-Object byte[] 16
    [Text.Encoding]::ASCII.GetBytes($title, 0, [Math]::Min($title.Length, 16), $nameBytes, 0) | Out-Null
    $bw.Write($nameBytes)
    $bw.Write([uint16]$Size)
    $bw.Write([uint16]$Size)
    foreach ($plane in $planes) { foreach ($value in $plane) { $bw.Write([uint16]$value) } }
    $bw.Flush()
    $planesLump = $ms.ToArray()

    # A PWAD: header, the PLANES data, then the directory (the level's marker, then PLANES)
    $out = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter $out
    $w.Write([Text.Encoding]::ASCII.GetBytes('PWAD'))
    $w.Write([int]2)
    $w.Write([int](12 + $planesLump.Length))
    $w.Write($planesLump)
    foreach ($lump in @(@(12, 0, $name), @(12, $planesLump.Length, 'PLANES'))) {
        $w.Write([int]$lump[0])
        $w.Write([int]$lump[1])
        $lumpName = New-Object byte[] 8
        [Text.Encoding]::ASCII.GetBytes($lump[2], 0, $lump[2].Length, $lumpName, 0) | Out-Null
        $w.Write($lumpName)
    }
    $w.Flush()

    $mapsDir = Join-Path $PSScriptRoot '..\mods\hub-demo\maps'
    New-Item -ItemType Directory -Force $mapsDir | Out-Null
    $path = Join-Path (Resolve-Path $mapsDir) "$name.wad"
    [IO.File]::WriteAllBytes($path, $out.ToArray())
    "Wrote $path ($($out.Length) bytes)"
}

# ---------------------------------------------------------------------------------------------
# MAP01, The Crossroads: the hub (grey stone). Its ways out run A_ChangeMap: the elevator
# levers (walls 50 and 52) and the teleport pad (trigger 610). Coming back, the player comes out
# at the entrance start of the way they went (1, 2 and 3: the levels' next-entrance). The floor
# of each way out has a tag (1-3) that the mod's script says where it goes by.
# ---------------------------------------------------------------------------------------------
$Hall = 108; $WestLift = 109; $EastLift = 110; $PadRoom = 111; $Vault = 112
$HubTiles = New-Tiles @($Common, @{})
$HubTiles['.'] = @{ Plane0 = $Hall }
$HubTiles['P'] = @{ Plane0 = $Hall; Plane1 = 19 }                   # the hub's own start, facing north
$HubTiles['L'] = @{ Plane0 = $Hall; Plane1 = 37 }                   # ceiling light
$HubTiles['p'] = @{ Plane0 = $Hall; Plane1 = 31 }                   # plant
$HubTiles['w'] = @{ Plane0 = $WestLift; Tag = 1 }
$HubTiles['1'] = @{ Plane0 = $WestLift; Plane1 = 600; Tag = 1 }     # entrance west: back from the Cellblock
$HubTiles['W'] = @{ Plane0 = 50 }                                   # lever: A_ChangeMap("HUB1")
$HubTiles['e'] = @{ Plane0 = $EastLift; Tag = 2 }
$HubTiles['2'] = @{ Plane0 = $EastLift; Plane1 = 601; Tag = 2 }     # entrance east: back from the Armory
$HubTiles['Q'] = @{ Plane0 = 52 }                                   # lever: A_ChangeMap("HUB2")
$HubTiles['n'] = @{ Plane0 = $PadRoom; Tag = 3 }
$HubTiles['T'] = @{ Plane0 = $PadRoom; Plane1 = 610; Tag = 3 }      # teleport pad: A_ChangeMap("HUB3")
$HubTiles['3'] = @{ Plane0 = $PadRoom; Plane1 = 602; Tag = 3 }      # entrance north: back from the Chapel
$HubTiles['l'] = @{ Plane0 = $PadRoom; Plane1 = 37; Tag = 3 }       # ceiling light
$HubTiles['v'] = @{ Plane0 = $Vault }
$HubTiles['c'] = @{ Plane0 = $Vault; Plane1 = 55 }                  # crown
$HubTiles['j'] = @{ Plane0 = $Vault; Plane1 = 54 }                  # chest of jewels
$HubTiles['V'] = @{ Plane0 = 80 }                                   # the vault door: all three emblems

Write-Level 'MAP01' 'The Crossroads' 1 18 20 $HubTiles @(
    '#############################'   # y=20
    '###########nnnnnnn###########'   #      the teleport pad's room
    '###########nnnTnnn###########'   #      the pad
    '###########nnl3lnn###########'   #      where the player comes back from the Chapel
    '###########nnnnnnn###########'
    '##############D##############'   # y=25
    '########.............########'   #      the hall
    '########.p....L....p.########'
    '###EEEE#.............#EEEE###'   #      the elevators' walls
    '###Www1z......L......z2eeQ###'   # y=29 west elevator (lever W), east elevator (lever Q)
    '###EEEE#.............#EEEE###'
    '########.p....P....p.########'   #      the hub's own start
    '########.............########'
    '##############V##############'   # y=33 the vault door
    '###########vvvvvvv###########'   #      the vault
    '###########vcvvvjvX##########'   #      its elevator: the win
    '###########vvvvvvv###########'
    '#############################'
)

# ---------------------------------------------------------------------------------------------
# HUB1, The Cellblock (blue stone): a corridor of cells, the emblem in the last one. Its
# elevator goes back to the hub (game-info next: MAP01, next-entrance: west).
# ---------------------------------------------------------------------------------------------
$Corridor = 108; $StartRoom = 109; $Cell1 = 110; $Cell2 = 111; $Cell3 = 112; $Lift = 113
$CellblockTiles = New-Tiles @($Common, @{})
$CellblockTiles['.'] = @{ Plane0 = $Corridor }
$CellblockTiles['L'] = @{ Plane0 = $Corridor; Plane1 = 37 }         # ceiling light
$CellblockTiles['h'] = @{ Plane0 = $Corridor; Plane1 = 110 }        # guard, facing west
$CellblockTiles['s'] = @{ Plane0 = $StartRoom }
$CellblockTiles['P'] = @{ Plane0 = $StartRoom; Plane1 = 21 }        # start, facing south
$CellblockTiles['c'] = @{ Plane0 = $Cell1 }
$CellblockTiles['G'] = @{ Plane0 = $Cell1; Plane1 = 111 }           # guard, facing south
$CellblockTiles['k'] = @{ Plane0 = $Cell2 }
$CellblockTiles['K'] = @{ Plane0 = $Cell2; Plane1 = 41 }            # cage with a skeleton
$CellblockTiles['A'] = @{ Plane0 = $Cell2; Plane1 = 49 }            # clip
$CellblockTiles['m'] = @{ Plane0 = $Cell3 }
$CellblockTiles['M'] = @{ Plane0 = $Cell3; Plane1 = 620 }           # the Cellblock's emblem
$CellblockTiles['g'] = @{ Plane0 = $Cell3; Plane1 = 111 }           # guard, facing south
$CellblockTiles['b'] = @{ Plane0 = 5 }                              # cell bars
$CellblockTiles['w'] = @{ Plane0 = $Lift }

Write-Level 'HUB1' 'The Cellblock' 8 18 24 $CellblockTiles @(
    '###########################'   # y=24
    '#sssss#####################'   #      the start room
    '#sssss#ccc#kkk#mmm#########'   #      three cells
    '#ssPss#cGc#kKk#mMm#########'
    '#sssss#ccc#kAk#mgm#########'
    '###D###bDb#bDb#bDb#####EE##'   # y=29 the cells' doors
    '#.....................zwwX#'   #      the corridor, the elevator back to the hub
    '#....L......L......L..#EE##'
    '#..................h..#####'
    '###########################'
)

# ---------------------------------------------------------------------------------------------
# HUB2, The Armory (wood): a guarded hall with dogs, the emblem among the racks north of it.
# Its elevator goes back to the hub (next-entrance: east).
# ---------------------------------------------------------------------------------------------
$Hall = 108; $Armory = 109; $StartRoom = 110; $Lift = 111
$ArmoryTiles = New-Tiles @($Common, @{})
$ArmoryTiles['.'] = @{ Plane0 = $Hall }
$ArmoryTiles['T'] = @{ Plane0 = $Hall; Plane1 = 36 }                # table
$ArmoryTiles['m'] = @{ Plane0 = $Hall; Plane1 = 50 }                # machine gun
$ArmoryTiles['o'] = @{ Plane0 = $Hall; Plane1 = 134 }               # dog, facing east
$ArmoryTiles['O'] = @{ Plane0 = $Hall; Plane1 = 136 }               # dog, facing west
$ArmoryTiles['a'] = @{ Plane0 = $Armory }
$ArmoryTiles['R'] = @{ Plane0 = $Armory; Plane1 = 69 }              # rack
$ArmoryTiles['M'] = @{ Plane0 = $Armory; Plane1 = 621 }             # the Armory's emblem
$ArmoryTiles['g'] = @{ Plane0 = $Armory; Plane1 = 111 }             # guard, facing south
$ArmoryTiles['s'] = @{ Plane0 = $StartRoom }
$ArmoryTiles['P'] = @{ Plane0 = $StartRoom; Plane1 = 19 }           # start, facing north
$ArmoryTiles['A'] = @{ Plane0 = $StartRoom; Plane1 = 49 }           # clip
$ArmoryTiles['w'] = @{ Plane0 = $Lift }

Write-Level 'HUB2' 'The Armory' 12 18 22 $ArmoryTiles @(
    '###########################'   # y=22
    '#######aaaaaaaaaaa#########'   #      the armory
    '#######aRaRaMaRaRa#########'
    '#######aaagaaagaaa#########'
    '#######aaaaaaaaaaa#########'
    '############D##############'   # y=27
    '#####.................#EE##'   #      the hall
    '#####..T.....m.....T..zwwX#'   #      the elevator back to the hub
    '#####..o...........O..#EE##'
    '#####.................#####'
    '############D##############'   # y=32
    '##########sssss############'   #      the start room
    '##########ssPss############'
    '##########ssAss############'
    '###########################'
)

# ---------------------------------------------------------------------------------------------
# HUB3, The Chapel (red brick): a nave of columns, the emblem at its far end. The elevator in
# its vestibule goes back to the hub (next-entrance: north).
# ---------------------------------------------------------------------------------------------
$Nave = 108; $Vestibule = 109; $Lift = 110
$ChapelTiles = New-Tiles @($Common, @{})
$ChapelTiles['n'] = @{ Plane0 = $Nave }
$ChapelTiles['C'] = @{ Plane0 = $Nave; Plane1 = 30 }                # white column
$ChapelTiles['L'] = @{ Plane0 = $Nave; Plane1 = 27 }                # chandelier
$ChapelTiles['M'] = @{ Plane0 = $Nave; Plane1 = 622 }               # the Chapel's emblem
$ChapelTiles['S'] = @{ Plane0 = $Nave; Plane1 = 129 }               # SS, facing south
$ChapelTiles['g'] = @{ Plane0 = $Nave; Plane1 = 111 }               # guard, facing south
$ChapelTiles['.'] = @{ Plane0 = $Vestibule }
$ChapelTiles['P'] = @{ Plane0 = $Vestibule; Plane1 = 19 }           # start, facing north
$ChapelTiles['A'] = @{ Plane0 = $Vestibule; Plane1 = 49 }           # clip
$ChapelTiles['H'] = @{ Plane0 = $Vestibule; Plane1 = 48 }           # first aid kit
$ChapelTiles['w'] = @{ Plane0 = $Lift }

Write-Level 'HUB3' 'The Chapel' 17 18 20 $ChapelTiles @(
    '###########################'   # y=20
    '##########nnnnnnn##########'   #      the nave
    '##########nCnMnCn##########'   #      the emblem
    '##########nnnSnnn##########'
    '##########nnnnnnn##########'
    '##########CnnLnnC##########'   # y=25
    '##########nnnnnnn##########'
    '##########CgnnngC##########'
    '##########nnnnnnn##########'
    '##########CnnLnnC##########'
    '##########nnnnnnn##########'   # y=30
    '#############D#############'
    '########EE#.......#########'   #      the vestibule
    '#######Xwwz...P...#########'   #      the elevator back to the hub
    '########EE#.A...H.#########'
    '###########################'
)
