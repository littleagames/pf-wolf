# Builds examples/mods/puzzles-demo's art and level: the terminal walls (textures/PZTERM*.png),
# the note on the wall (textures/PZNOTE.png), the sliding puzzle's picture (graphics/PZSLIDE.png),
# the auto-hack tool (sprites/PZHKA0.png) and the level (maps/MAP01.wad). Run it after changing
# any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-puzzles-demo.ps1
#
# The level: a long hall with a terminal every few tiles along both walls, each a switch with a
# puzzle (mapdefs/walls.yaml) whose win opens the locked closet beside it. The keypad's code is on
# a note by the start; one closet holds a prisoner the sequence puzzle frees to fight for you; a
# failed needle sounds the alarm, and the guards in the barracks at the east end come.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'puzzles-demo'

#
# ART
#

# A terminal set in the wall: a steel cabinet, a screen (its color by state), a keypad below
function Save-Terminal([string]$Name, $Screen, [scriptblock]$Extra = $null) {
    Save-Drawing "$modDir\textures\$Name.png" 64 64 {
        param($g)
        Add-Rect $g @(96, 96, 104) 0 0 64 64
        Add-Rect $g @(70, 70, 78) 0 0 64 2
        Add-Rect $g @(70, 70, 78) 0 62 64 2
        Add-Rect $g @(52, 54, 60) 10 8 44 48                         # the cabinet's face
        Add-Rect $g @(20, 20, 22) 14 12 36 22                        # the screen's bezel
        Add-Rect $g $Screen 16 14 32 18                              # the screen
        foreach ($row in 0..2) { foreach ($col in 0..3) { Add-Rect $g @(150, 150, 140) (17 + 8 * $col) (38 + 5 * $row) 5 3 } }
        if ($Extra) { & $Extra $g }
    }
}
# Waiting: a red screen with a blinking cursor line
Save-Terminal 'PZTERM' @(110, 20, 20) { param($g) Add-Rect $g @(240, 80, 80) 18 16 12 2; Add-Rect $g @(240, 80, 80) 18 20 20 2 }
# Done: green, the lines of text of an open door
Save-Terminal 'PZTERMOK' @(20, 90, 30) { param($g) foreach ($k in 0..3) { Add-Rect $g @(90, 240, 110) 18 (16 + 4 * $k) (8 + 6 * $k) 2 } }
# Broken: the screen smashed, scorched round it, wires hanging
Save-Terminal 'PZTERMX' @(10, 10, 10) {
    param($g)
    Add-Poly $g @(200, 200, 210) @(30, 15, 33, 22, 40, 18, 34, 25, 44, 30, 32, 27, 24, 31, 29, 23)
    Add-Ellipse $g @(30, 26, 22) 6 4 20 14
    Add-Rect $g @(220, 60, 40) 26 34 1 12
    Add-Rect $g @(60, 120, 220) 34 34 1 9
}

# The note: a sheet of paper pinned to the grey stone, lines of writing
Save-Drawing "$modDir\textures\PZNOTE.png" 64 64 {
    param($g)
    Add-Rect $g @(112, 112, 112) 0 0 64 64
    foreach ($y in 0, 16, 32, 48) { Add-Rect $g @(88, 88, 88) 0 ($y + 15) 64 1 }
    foreach ($x in 0, 32) { foreach ($y in 0, 32) { Add-Rect $g @(88, 88, 88) ($x + 31) $y 1 16 } }
    Add-Poly $g @(236, 230, 210) @(18, 14, 46, 12, 48, 50, 16, 52)
    foreach ($k in 0..6) { Add-Rect $g @(60, 60, 90) (21 + ($k % 2)) (20 + 4 * $k) (20 - 2 * ($k % 3)) 1 }
    Add-Ellipse $g @(200, 30, 30) 30 13 4 4                        # the pin
}

# The sliding puzzle's picture (96 x 96, three by three tiles): a castle on a hill at sunset
Save-Picture "$modDir\graphics\PZSLIDE.png" 96 96 {
    param($x, $y)
    $hill = 70 - 10 * [Math]::Sin($x / 15.0)
    $sun = [Math]::Sqrt(($x - 72) * ($x - 72) + ($y - 26) * ($y - 26))
    # the castle: a keep, two towers with battlements, a gate
    $inKeep = $x -ge 30 -and $x -lt 62 -and $y -ge 40 -and $y -lt $hill
    $inTower = (($x -ge 22 -and $x -lt 32) -or ($x -ge 60 -and $x -lt 70)) -and $y -ge 28 -and $y -lt $hill
    $battlement = (($x -ge 22 -and $x -lt 32) -or ($x -ge 60 -and $x -lt 70)) -and $y -ge 24 -and $y -lt 28 -and ($x % 4) -lt 2
    $gate = $x -ge 42 -and $x -lt 50 -and $y -ge 54 -and $y -lt $hill
    $window = ($x -ge 25 -and $x -lt 28 -and $y -ge 34 -and $y -lt 39) -or ($x -ge 64 -and $x -lt 67 -and $y -ge 34 -and $y -lt 39)
    if ($window) { return New-Color 255 220 90 }
    if ($gate) { return New-Color 40 24 16 }
    if ($inKeep -or $inTower -or $battlement) { return New-Color (70 + ($x * 7 + $y * 3) % 20) (66 + ($x * 5) % 14) 80 }
    if ($y -ge $hill) { return New-Color 40 (90 + ($x + $y) % 30) 40 }
    if ($sun -lt 9) { return New-Color 255 230 120 }
    New-Color (240 - $y) (120 + $y) (90 + 2 * $y)
}

# The auto-hack tool, on the floor: a small grey box with a green screen and a cable
Save-Drawing "$modDir\sprites\PZHKA0.png" 64 64 {
    param($g)
    Add-Rect $g @(70, 74, 80) 22 46 20 14
    Add-Rect $g @(110, 114, 120) 22 46 20 2
    Add-Rect $g @(30, 200, 80) 26 49 12 6
    Add-Rect $g @(200, 180, 40) 40 57 10 2
}

#
# THE LEVEL
#

$Size = 64
$planes = New-MapPlanes -Size $Size -Fill 1
function Set-Tile([int]$X, [int]$Y, [int]$Wall, [int]$Thing = 0, [int]$Tag = 0) {
    $i = $Y * $Size + $X
    $planes[0][$i] = $Wall
    if ($Thing) { $planes[1][$i] = $Thing }
    if ($Tag) { $planes[4][$i] = $Tag }
}

$Hall = 108; $Barracks = 120
$LockedDoor = 93              # gold-locked, north-south: only its switch opens it (there's no gold key)
$HallTop = 32; $HallBottom = 34; $HallLeft = 2; $HallRight = 48

# The hall: the player at its west end facing east, the auto-hack tool beside them, the
# elevator switch at its east end
for ($y = $HallTop; $y -le $HallBottom; $y++) { for ($x = $HallLeft; $x -le $HallRight; $x++) { Set-Tile $x $y $Hall } }
Set-Tile $HallLeft 33 $Hall 20                      # the player, facing east
Set-Tile ($HallLeft + 1) 34 $Hall 590               # the auto-hack tool (mapdefs/things.yaml)
Set-Tile ($HallRight + 1) 33 21                     # the elevator switch
Set-Tile ($HallLeft + 1) ($HallTop - 1) 77 0 50     # the note with the keypad's code (scripts/notes.lua, tag 50)

# The stations: a terminal (wall 64 + n - 1, tag n) with a locked closet two tiles east of it,
# its door tagged n too, and what's inside. North side first, then south.
$stations = @(
    @{ X = 5;  North = $true;  Thing = 50 }          # 1 needle: a machine gun
    @{ X = 11; North = $true;  Thing = 54 }          # 2 keypad: a chest
    @{ X = 17; North = $true;  Thing = 52 }          # 3 pipes: a cross
    @{ X = 23; North = $true;  Thing = 55 }          # 4 safe dial: a crown
    @{ X = 29; North = $true;  Thing = 48 }          # 5 lockpick (it can break): a first aid kit
    @{ X = 35; North = $true;  Thing = 56 }          # 6 wires (the auto-hack tool skips it): an extra life
    @{ X = 5;  North = $false; Thing = 109 }         # 7 sequence: a prisoner, facing the door (freed, he fights for you)
    @{ X = 11; North = $false; Thing = 53 }          # 8 lights out: a chalice
    @{ X = 17; North = $false; Thing = 51 }          # 9 sliding picture: a chain gun
    @{ X = 23; North = $false; Thing = 47 }          # 10 frequency: food
    @{ X = 29; North = $false; Thing = 54 }          # 11 power routing (the vault): a chest
)
for ($n = 1; $n -le $stations.Count; $n++) {
    $s = $stations[$n - 1]
    $wallY = if ($s.North) { $HallTop - 1 } else { $HallBottom + 1 }
    $step = if ($s.North) { -1 } else { 1 }
    $area = $Hall + $n                                  # 109 on: each closet its own room
    Set-Tile $s.X $wallY (64 + $n - 1) 0 $n             # the terminal
    Set-Tile ($s.X + 2) $wallY $LockedDoor 0 $n         # its closet's door
    for ($d = 1; $d -le 3; $d++) {
        for ($x = $s.X + 1; $x -le $s.X + 3; $x++) { Set-Tile $x ($wallY + $step * $d) $area }
    }
    $tag = if ($s.Thing -eq 109) { $n } else { 0 }      # the prisoner shares the station's tag (A_SetFaction)
    Set-Tile ($s.X + 2) ($wallY + $step * 2) $area $s.Thing $tag
}

# The barracks, south of the hall's east end through an open doorway: three guards lying in
# ambush (floor code 106), facing the doorway. A failed needle's alarm (A_Alarm) wakes them.
for ($y = $HallBottom + 2; $y -le $HallBottom + 5; $y++) { for ($x = 39; $x -le 45; $x++) { Set-Tile $x $y $Barracks } }
Set-Tile 42 ($HallBottom + 1) $Barracks               # the doorway
foreach ($x in 40, 42, 44) { Set-Tile $x ($HallBottom + 5) 106 109 }

Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Puzzles Demo' $planes
