# Builds examples/mods/blake-demo: its level (maps/MAP02.wad), Blake Stone: Aliens of Gold's
# Floor 1, replaced. Run it after changing it:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-blake-demo.ps1
#
# Everything on it is Blake Stone's own, by its mapdefs ids (pfwolf-pk3's mapdefs/blake): you
# arrive in the elevator; the lobby has bio-techs to talk to, food tokens and food units to spend
# them at, and a rent-a-cop; an arc barrier shuts the corridor to the card room, its switch on the
# lobby wall; the red access card there opens Floor 2 on the elevator's panel.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'blake-demo'

$Layout = @(
    '##########################'   # y=20
    '##########################'
    '##########################'
    '##########V#W#############'   #      the food units: a snack and a drink
    '######llllllllll##########'   #      the lobby
    '######lllllllltl#####55555'   # y=25 a food token; the card room
    '#22222llblllllll#####5rpr5'   #      a bio-tech; a pro guard
    '#2eee2llllllllll#####5rrr5'   #      the elevator
    '#3ePe4llllllllllkkBkkkrRr5'   #      its panel, the player, its door; the barrier; the red card
    '#2eee2lllllcllll#####5rrr5'   #      a rent-a-cop
    '#22222llllllllllS####5rrr5'   # y=30 the barrier switch
    '######lllllllbll#####55555'
    '######ltltllllll##########'
    '######llllllllll##########'
    '##########################'
)
# Floor codes (mapdefs/blake/floors.yaml): area n is 108 + n, area 0 the elevator's (enemies keep
# out); walls, doors and things are Blake Stone's mapdefs ids
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 12 }                                    # institute wall
$Tiles['2'] = @{ Plane0 = 22 }                                    # elevator wall
$Tiles['3'] = @{ Plane0 = 21 }                                    # the elevator's panel (A_FloorSelect)
$Tiles['4'] = @{ Plane0 = 94 }                                    # elevator door, east-west
$Tiles['5'] = @{ Plane0 = 20 }                                    # metal wall
$Tiles['e'] = @{ Plane0 = 108 }                                   # the elevator: area 0
$Tiles['P'] = @{ Plane0 = 108; Plane1 = 20 }                      # the player, facing east
$Tiles['l'] = @{ Plane0 = 109 }
$Tiles['b'] = @{ Plane0 = 109; Plane1 = 118 }                     # a bio-tech (scientist or informant)
$Tiles['t'] = @{ Plane0 = 109; Plane1 = 423 }                     # a food token
$Tiles['c'] = @{ Plane0 = 109; Plane1 = 110 }                     # a rent-a-cop, facing west
$Tiles['V'] = @{ Plane0 = 18 }                                    # food unit (snack)
$Tiles['W'] = @{ Plane0 = 15 }                                    # food unit (beverage)
$Tiles['k'] = @{ Plane0 = 109 }
$Tiles['B'] = @{ Plane0 = 109; Plane1 = 174; Tag = 1 }            # the arc barrier, tag 1
$Tiles['S'] = @{ Plane0 = 45; Tag = 1 }                           # its switch, on, tag 1
$Tiles['r'] = @{ Plane0 = 110 }
$Tiles['R'] = @{ Plane0 = 110; Plane1 = 55 }                      # the red access card
$Tiles['p'] = @{ Plane0 = 110; Plane1 = 129 }                     # a pro guard, facing south
$planes = New-MapPlanes -Fill 12
Add-MapLayout $planes $Layout $Tiles 18 20
Save-MapWad "$modDir\maps\MAP02.wad" 'MAP02' 'Blake Demo' $planes
