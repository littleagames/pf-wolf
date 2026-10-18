# Builds Wolf3D's deathmatch arenas, the Multiplayer mod's multiplayer-pk3/maps/wolf3d/DM01.wad
# to DM04.wad (and HE01 for Heist, PH01 for Prop Hunt), from the layouts drawn below. Run it
# after changing a layout:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-deathmatch-maps.ps1
#
# Each arena is drawn as its top-left quarter and mirrored across, then down, so every side
# is the same (the last column and row are the middle ones, not repeated). The middle tile is
# the level's own player start, which a deathmatch never uses but a level needs (an arena with
# `Start` puts it there instead).
#
# Each level is an ECWolf binary map (a WAD holding a marker lump and a WDC3.1 PLANES lump)
# with PFWolf's six planes; walls/floors, objects and (for an arena with `Zones`) light zones
# are used. The arenas are listed in multiplayer-pk3/gamepacks/wolf3d/game-info.yaml with
# `deathmatch: true` (and `modes:` for one made for some modes only, as HE01 is for Heist).
# -Show prints each whole layout.
#
# Besides `Walls`, an arena can give:
#   Folder         the pk3 source folder it's written into (default multiplayer-pk3)
#   QuadrantWalls  a character whose wall differs by quarter: @(NW, NE, SW, SE)
#   Pushwalls      a character that's a pushwall in the wall of another (a Walls or QuadrantWalls one)
#   Shooters       a character that's a wall with a thing inside it (mapdefs `in-wall: true`), by
#                  thing number, the wall being ShooterWall: '(' and ')' swap top to bottom, so
#                  one facing north in the quarter faces south in the mirrored half
#   Sided          a character whose thing differs by half: @(west, east)
#   Start          @(x, y) of the level's own start, when the middle tile is taken
#   Heights        a character's height in stories (plane 3), for walls taller than the level's
#                  `wall-height` (game-info); on a floor character (':', ';') it's an arch overhead
#   Ceilings       a character's ceiling flat (mapdefs flats `ceiling:` index, plane 2's high
#                  byte); on an arch, its underside
#   Zones          the quarter's light zones, drawn like the layout (plane 5), and
#   ZoneIds        what each of their characters is: a zone id, or @(NW, NE, SW, SE)
# 45 degree walls are drawn 7 9 1 3 (where the solid corner is, as on a number pad: NW, NE, SW,
# SE) in the arena's 'D' wall; mirroring turns them to match.

param([switch]$Show)

$ErrorActionPreference = 'Stop'

# What the characters every arena shares put on plane 0 or plane 1 (mapdefs/wolf3d ids).
# Floors get their area codes worked out: each run of floor that doors close off is an area.
# A Dictionary, not a hashtable: hashtables don't tell 'm' from 'M'.
$Common = New-Object 'System.Collections.Generic.Dictionary[char,int[]]'
$Common['.'] = @(0, 0)              # floor
$Common[':'] = @(0, 0)              # floor under an arch (its height is the arena's Heights)
$Common[';'] = @(0, 0)              # another, for an arch with its own underside (the arena's Ceilings)
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
# For team games (mirrored across, each swaps for the other: red's side is the west)
$Common['R'] = @(0, 431)            # red team start, facing east
$Common['B'] = @(0, 437)            # blue team start, facing west
$Common['q'] = @(0, 108)            # guard, facing east
$Common['Q'] = @(0, 110)            # guard, facing west
# Heist's loot and the keys to it
$Common['x'] = @(0, 52)             # cross (1)
$Common['y'] = @(0, 53)             # chalice (5)
$Common['J'] = @(0, 54)             # chest of jewels (10)
$Common['K'] = @(0, 55)             # crown (50)
$Common['k'] = @(0, 43)             # gold key
$Common['s'] = @(0, 44)             # silver key
$Common['{'] = @(92, 0)             # gold-locked door, in a wall running north-south
$Common['}'] = @(93, 0)             # gold-locked door, in a wall running east-west
$Common['['] = @(94, 0)             # silver-locked door, north-south
$Common[']'] = @(95, 0)             # silver-locked door, east-west
# Prop Hunt's clutter: decorations of every size for props to look like and hide among
$Common['d'] = @(0, 24)             # oil drum
$Common['t'] = @(0, 25)             # table with chairs
$Common['f'] = @(0, 26)             # floor lamp
$Common['S'] = @(0, 33)             # sink
$Common['p'] = @(0, 34)             # brown plant
$Common['V'] = @(0, 35)             # vase
$Common['T'] = @(0, 36)             # table
$Common['a'] = @(0, 39)             # suit of armor
$Common['b'] = @(0, 58)             # barrel
$Common['w'] = @(0, 60)             # well
$Common['e'] = @(0, 68)             # stove
$Common['r'] = @(0, 69)             # rack
# multiplayer-pk3's ammo (mapdefs/things.yaml), for arenas in that mod
$Common['u'] = @(0, 424)            # gas can
$Common['z'] = @(0, 422)            # rockets
# 45 degree wall markers (mapdefs/wolf3d/diagonals.yaml), by the number-pad character
$Diagonals = @{ '7' = 400; '9' = 401; '1' = 402; '3' = 403 }
$Pushwall = 98                      # mapdefs/wolf3d/triggers.yaml

# Walls: '#' each arena's own wall, '%' and '&' its others
$Arenas = @(
    # Hell's Highways, DM02: the whole 64x64, three lanes between a base on the west and one on
    # the east (each its own wall: grey stone, blue stone). The outer lanes (5 wide) run the whole
    # width past fire walls (EoD's, multiplayer-pk3 mapdefs/walls.yaml; 1 story, each at the back
    # of a 1-tile alcove under an arch) that shoot across them, to a
    # flamethrower in the middle of each; the middle lane (7 wide) runs from base mouth to base
    # mouth under four arches, to the rocket launcher in the very middle behind 45 degree cover.
    # Two connectors a side join the lanes past an armory (chaingun). Secret passages through the
    # solid blocks (pushwalls in the outer lanes', middle lane's and bases' walls) lead to stash
    # rooms. Lane walls are 3 stories, the rest 2 (game-info wall-height), under a sky; light
    # zones: cool bases, fiery outer lanes, a red middle lane with a bright middle, dim
    # connectors, bright armories, dark passages, flickering stashes.
    @{
        Name = 'DM02'; Title = "Hell's Highways"
        Walls = @{ '#' = 44; '%' = 17; 'D' = 19 }        # brown stone, red brick; cover in purple
        QuadrantWalls = @{ 'C' = @(1, 8, 1, 8) }        # grey stone (west base), blue stone (east)
        Pushwalls = @{ '*' = '%'; '$' = 'C' }
        Shooters = @{ '(' = 446; ')' = 448 }            # fire wall shooters (multiplayer-pk3 mapdefs/things.yaml), firing north / south
        ShooterWall = 65                                # the fire wall (multiplayer-pk3 mapdefs/walls.yaml)
        # The fire walls are 1 story, set back in alcoves whose arch (';': red brick, from the
        # lane wall beside it, and underneath, ceiling flat 17 in multiplayer-pk3's
        # mapdefs/flats.yaml) hides the stories above them
        Heights = @{ '%' = 3; '*' = 3; '(' = 1; ')' = 1; ':' = 3; ';' = 3 }
        Ceilings = @{ ';' = 17 }
        Sided = @{ 'F' = @(421, 421); 'W' = @(420, 420) } # flamethrower, rocket launcher
        Start = @(31, 29)                               # the rocket launcher has the middle
        Quarter = @(
            '################################'
            '#...............................'
            '#.....h.........u...............'
            '#.>..........D......D..........F'
            '#........................u......'
            '#...............................'
            '#...%%%%%%%%*%%%%;%%%%..%%%;%%%%'
            '#...%%%%%%%%...%%(%%%%..%%%(%%%%'
            '#...%%%%%%%%.%.%%%%%%%..%%%%%%%%'
            '#.v.%%%%%%%%%%.%%%%%%%..%%%%%%%%'
            '#...%%%%%%%%%.....%%%%.m%%%%%%%%'
            '#...%%%%%%%%%z.u..%%%%..%%%%%%%%'
            '#...%%%%%%%%%.+..z%%%%..%%%%%%%%'
            '#...%%%%%%%%%.....%%%%..%%%%%%%%'
            '#...%%%%%%%%%%%%.%%%%%..%%%.....'
            '#...%%%%%%%%%%%%.%%%%%..%%%.L...'
            '#...%%%%%%%%%%%%.%%%%%.......+.G'
            '#...%%%%%%%%%%%%.%%%%%..%%%.....'
            '#...%%%%%%%%%%%%.%%%%%..%%%.....'
            '#...CCCCCC%%%%%%.%%%%%..%%%%%%%%'
            '#........C%%%%%%.%%%%%..%%%%%%%%'
            '#........C.......%%%%%..%%%%%%%%'
            '#..>...m.C.%%%%%.%%%%%..%%%%%%%%'
            '#........$..%%%%.%%%%%..%%%%%%%%'
            '#....M...C%%%%%%.%%%%%..%%%%%%%%'
            '#........C%%%%%..%%%%%..%%%%%%%%'
            '#...L....C%%%%%..%%%%%..%%%%%%%%'
            '#........C%%%%%*%%%%%%..%%%%%%%%'
            '#.....>..C....:.........:......z'
            '#.............:.........:...3...'
            '#.+...........:...z.....:.......'
            '#.............:.........:.z.D..W'
        )
        # b bases, o outer lanes, m middle lane, c its middle, k connectors and corridors,
        # a armories, s secret passages, t stashes
        ZoneIds = @{ 'b' = @(1, 2, 1, 2); 'o' = 3; 'm' = 4; 'c' = 5; 'k' = 6; 'a' = 7; 's' = 8; 't' = 9 }
        Zones = @(
            '................................'
            '.ooooooooooooooooooooooooooooooo'
            '.ooooooooooooooooooooooooooooooo'
            '.ooooooooooooooooooooooooooooooo'
            '.ooooooooooooooooooooooooooooooo'
            '.ooooooooooooooooooooooooooooooo'
            '.kkk.............o....kk...o....'
            '.kkk........sss.......kk........'
            '.kkk........s.s.......kk........'
            '.kkk..........s.......kk........'
            '.kkk.........ttttt....kk........'
            '.kkk.........ttttt....kk........'
            '.kkk.........ttttt....kk........'
            '.kkk.........ttttt....kk........'
            '.kkk............s.....kk...aaaaa'
            '.kkk............s.....kk...aaaaa'
            '.kkk............s.....kkkkkaaaaa'
            '.kkk............s.....kk...aaaaa'
            '.kkk............s.....kk...aaaaa'
            '.kkk............s.....kk........'
            '.bbbbbbbb.......s.....kk........'
            '.bbbbbbbb.sssssss.....kk........'
            '.bbbbbbbb.s.....s.....kk........'
            '.bbbbbbbb.ss....s.....kk........'
            '.bbbbbbbb.......s.....kk........'
            '.bbbbbbbb......ss.....kk........'
            '.bbbbbbbb......ss.....kk........'
            '.bbbbbbbb.............kk........'
            '.bbbbbbbb.mmmmmmmmmmmmmmmmmmmmmm'
            '.bbbbbbbbmmmmmmmmmmmmmmmmmmccccc'
            '.bbbbbbbbmmmmmmmmmmmmmmmmmmccccc'
            '.bbbbbbbbmmmmmmmmmmmmmmmmmmccccc'
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
    # Heist (game-info modes: [heist, team-deathmatch]): red's bases on the west, blue's on the
    # east. Crosses and chalices lie about in the open; chests are behind the silver doors
    # (silver keys in the side halls); crowns in the vault in the middle, behind its gold doors
    # (one on each team's side), the gold keys at the dead ends of the halls between the silver
    # rooms, where both teams can get at them. A few guards keep watch.
    @{
        Name = 'HE01'; Title = 'The Vault'
        Walls = @{ '#' = 1; '%' = 19 }                  # grey stone, purple for the vault
        Quarter = @(
            '###############'
            '#R....#.#J..y#.'
            '#..h..#.#....#k'
            '#R....|.#..J.#.'
            '#.+...#.##]###.'
            '#..m..#.....x..'
            '###-###..o...o.'
            '#..........y...'
            '#.x..o.....L...'
            '#.......q......'
            '#.s..o...x.....'
            '#..........%%%%'
            '#.h........%K..'
            '#.....g....%...'
            '#.m........{..P'
        )
    }
    # Prop Hunt (game-info modes: [prop-hunt, deathmatch]): storerooms cluttered with every kind
    # of decoration for props to look like, rooms joined by doors, a yard in the middle
    @{
        Name = 'PH01'; Title = 'The Storerooms'
        Walls = @{ '#' = 12 }                           # wood
        Quarter = @(
            '###############'
            '#v..b.d..f...T.'
            '#.....>.......b'
            '#.t..a..##-####'
            '#.......#V....p'
            '##-######..S...'
            '#.......#....e.'
            '#.d.b.d.|.r....'
            '#.......#..a...'
            '#.f.....###-###'
            '#....^........o'
            '#.T...p...w....'
            '#.........L....'
            '#.b..V..t...d..'
            '#.....f.......P'
        )
    }
    # Four Corners, DM01: the whole 64x64. A
    # base in each corner, each its own wall (steel, wood, grey stone, blue stone), four starts
    # in each (16 in all). From each base, one hall to the joint room it shares with its neighbour (north
    # and south, two machine guns each) and one to a corner of the arena, where a chaingun sits
    # in the middle behind four 45 degree cover blocks that hide it from the halls. A pushwall
    # in each base's south (north) wall opens a long passage to a vault: the flamethrower on
    # the west side, the rocket launcher on the east. Brown stone around the edges, red brick
    # in the arena. Light zones: tinted bases, dim halls, dark passages, a flickering vault, a
    # dim arena with a bright middle (multiplayer-pk3/gamepacks/wolf3d/game-info.yaml).
    # Hashtable keys ignore case, so 'D', 'C', 'F' and 'W' here hide 'd', 'c', 'f' and 'w'.
    @{
        Name = 'DM01'; Title = 'Four Corners'
        Walls = @{ '#' = 17; '%' = 44; 'D' = 17 }      # red brick, brown stone; cover in red brick
        QuadrantWalls = @{ 'C' = @(15, 12, 1, 8) }      # steel, wood, grey stone, blue stone
        Pushwalls = @{ '*' = 'C' }
        Sided = @{ 'F' = @(421, 420); 'W' = @(424, 422) } # flamethrower + gas west, rocket launcher + rockets east
        Start = @(31, 30)                               # the chaingun has the middle
        Quarter = @(
            'CCCCCCCCCCCCC########%%%%%%%%%%%'
            'C...........C########%..........'
            'C........m<.C########%.......m..'
            'C..v........C%%%%%%%%%...m......'
            'C...........|...................'
            'C.....L.....C..............M...L'
            'C...........C%%%%%%%%%..........'
            'C.....h.....C########%...m......'
            'C........<..C########%.......m..'
            'C.m.........C########%..........'
            'C...^.......C########%%%%%%%%%%%'
            'C...........C###################'
            'CCC*CCCCC-CCC###################'
            '%...%###%..%####################'
            '%.%.%###%..%%%%%%%%%%###########'
            '%.%%%###%...........%###########'
            '%.%#####%...........%###########'
            '%.%#####%%%%%%%%%#..############'
            '%.%###############..............'
            '%.%###############..............'
            '%.%###############..L........m..'
            '%.%%%%%%%%%%%%%###..............'
            '%.............%###......+......L'
            '%%%%%%%%%%%%%.%###.....3D1......'
            '###%..........%###....mDDDz.....'
            '###%.%%%%%%%%%%###.....9D7......'
            '%%%%.%%%%#########......u.......'
            '%.......%#########..............'
            '%...+...%#########..............'
            '%.W...W.%#########..h...........'
            '%.......%#########..............'
            '%...F...%#########.............G'
        )
        # b base, h halls (and their doors), j joint rooms, s secret passages, w vaults, a arena,
        # c the arena's bright middle
        ZoneIds = @{ 'b' = @(1, 2, 3, 4); 'h' = 5; 'j' = 6; 's' = 7; 'w' = 8; 'a' = 9; 'c' = 10 }
        Zones = @(
            '................................'
            '.bbbbbbbbbbb..........jjjjjjjjjj'
            '.bbbbbbbbbbb..........jjjjjjjjjj'
            '.bbbbbbbbbbb..........jjjjjjjjjj'
            '.bbbbbbbbbbbhhhhhhhhhhjjjjjjjjjj'
            '.bbbbbbbbbbb.hhhhhhhhhjjjjjjjjjj'
            '.bbbbbbbbbbb..........jjjjjjjjjj'
            '.bbbbbbbbbbb..........jjjjjjjjjj'
            '.bbbbbbbbbbb..........jjjjjjjjjj'
            '.bbbbbbbbbbb..........jjjjjjjjjj'
            '.bbbbbbbbbbb....................'
            '.bbbbbbbbbbb....................'
            '...s.....h......................'
            '.sss.....hh.....................'
            '.s.s.....hh.....................'
            '.s.......hhhhhhhhhhh............'
            '.s.......hhhhhhhhhhh............'
            '.s................hh............'
            '.s................aaaaaaaaaaaaaa'
            '.s................aaaaaaaaaaaaaa'
            '.s................aaaaaaaaaaaaaa'
            '.s................aaaaaaaaaaaaaa'
            '.sssssssssssss....aaaaaaaaaaaaaa'
            '.............s....aaaaaaaaaaaaaa'
            '....ssssssssss....aaaaaaaaaaaaaa'
            '....s.............aaaaaaaaaaaaaa'
            '....s.............aaaaaaaaaaaaaa'
            '.wwwwwww..........aaaaaaaaaaaaaa'
            '.wwwwwww..........aaaaaaaaaaaaaa'
            '.wwwwwww..........aaaaaaaaaaaccc'
            '.wwwwwww..........aaaaaaaaaaaccc'
            '.wwwwwww..........aaaaaaaaaaaccc'
        )
    }
)

$Size = 64
$PlaneCount = 6
$AreaStart = 108                    # area 1 (mapdefs/wolf3d/floors.yaml: area n is 107 + n)
$AreaCount = 36

function Swap([string]$line, [char]$a, [char]$b) {
    # -ceq: 'b' (a barrel) mustn't swap as 'B' (a blue team start) does
    -join ($line.ToCharArray() | ForEach-Object { if ($_ -ceq $a) { $b } elseif ($_ -ceq $b) { $a } else { $_ } })
}

# The whole arena from its top-left quarter: mirrored right (west/east starts swap, and 45
# degree walls), then down (north/south starts swap, 45 degree walls, and shooters)
function Expand([string[]]$quarter) {
    $width = $quarter[0].Length
    foreach ($row in $quarter) { if ($row.Length -ne $width) { throw "Row '$row' isn't $width wide" } }
    $rows = foreach ($row in $quarter) {
        $right = -join $row.Substring(0, $width - 1).ToCharArray()[($width - 2)..0]
        $row + (Swap (Swap (Swap (Swap (Swap $right '<' '>') 'R' 'B') 'q' 'Q') '7' '9') '1' '3')
    }
    $bottom = for ($i = $rows.Count - 2; $i -ge 0; $i--) { Swap (Swap (Swap (Swap $rows[$i] '^' 'v') '7' '1') '9' '3') '(' ')' }
    @($rows) + @($bottom)
}

# Which quarter (0 NW, 1 NE, 2 SW, 3 SE) a tile of a width x height layout is in; the middle
# column and row count as west and north
function Quarter([int]$x, [int]$y, [int]$width, [int]$height) {
    ([int]($x -gt ($width - 1) / 2)) + 2 * [int]($y -gt ($height - 1) / 2)
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
            $s = [string]$ch
            $i = ($top + $y) * $Size + $left + $x
            $quarter = Quarter $x $y $width $height
            if ($arena.Heights -and $arena.Heights.ContainsKey($s)) { $planes[3][$i] = $arena.Heights[$s] }
            if ($arena.Ceilings -and $arena.Ceilings.ContainsKey($s)) { $planes[2][$i] = $arena.Ceilings[$s] -shl 8 }
            if ($arena.Walls.ContainsKey($s)) {
                $planes[0][$i] = $arena.Walls[$s]
                continue
            }
            if ($arena.QuadrantWalls -and $arena.QuadrantWalls.ContainsKey($s)) {
                $planes[0][$i] = $arena.QuadrantWalls[$s][$quarter]
                continue
            }
            if ($arena.Pushwalls -and $arena.Pushwalls.ContainsKey($s)) {
                $in = $arena.Pushwalls[$s]
                $planes[0][$i] = if ($arena.QuadrantWalls -and $arena.QuadrantWalls.ContainsKey($in)) { $arena.QuadrantWalls[$in][$quarter] } else { $arena.Walls[$in] }
                $planes[1][$i] = $Pushwall
                continue
            }
            if ($arena.Shooters -and $arena.Shooters.ContainsKey($s)) {
                $planes[0][$i] = $arena.ShooterWall
                $planes[1][$i] = $arena.Shooters[$s]
                continue
            }
            if ($Diagonals.ContainsKey($s)) {
                $planes[0][$i] = $arena.Walls['D']
                $planes[1][$i] = $Diagonals[$s]
                continue
            }
            if ($arena.Sided -and $arena.Sided.ContainsKey($s)) {
                $planes[0][$i] = 0
                $planes[1][$i] = $arena.Sided[$s][[int]($x -gt ($width - 1) / 2)]
                $floor[$x, $y] = $true
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

    if ($arena.Start) {
        $i = ($top + $arena.Start[1]) * $Size + $left + $arena.Start[0]
        if ($planes[1][$i] -ne 0 -or $planes[0][$i] -lt $AreaStart) { throw "$($arena.Name): the start isn't on an empty floor tile" }
        $planes[1][$i] = $Common['P'][1]
    }

    # Light zones (plane 5)
    if ($arena.Zones) {
        $zones = Expand $arena.Zones
        if ($zones.Count -ne $height -or $zones[0].Length -ne $width) { throw "$($arena.Name): Zones isn't the layout's size" }
        for ($y = 0; $y -lt $height; $y++) {
            for ($x = 0; $x -lt $width; $x++) {
                $s = [string]$zones[$y][$x]
                if ($s -eq '.') { continue }
                if (!$arena.ZoneIds.ContainsKey($s)) { throw "$($arena.Name): unknown zone '$s' at $x,$y" }
                $id = $arena.ZoneIds[$s]
                $planes[5][($top + $y) * $Size + $left + $x] = if ($id -is [array]) { $id[(Quarter $x $y $width $height)] } else { $id }
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

    $folder = if ($arena.Folder) { $arena.Folder } else { 'multiplayer-pk3' }
    $mapsDir = Join-Path $PSScriptRoot "..\..\$folder\maps\wolf3d"
    New-Item -ItemType Directory -Force $mapsDir | Out-Null
    $path = Join-Path (Resolve-Path $mapsDir).Path "$($arena.Name).wad"
    [IO.File]::WriteAllBytes($path, $out.ToArray())
    "Wrote ${path}: $($arena.Title), ${width}x$height, $areas area(s)"
    if ($Show) { $layout; '' }
}

foreach ($arena in $Arenas) { Write-Arena $arena }
