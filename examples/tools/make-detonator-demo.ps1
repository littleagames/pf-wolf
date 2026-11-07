# Builds examples/mods/detonator-demo: its level (maps/MAP01.wad), Blake Stone: Planet Strike's
# Transport Arrival Area, replaced. Run it after changing it:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-detonator-demo.ps1
#
# A test of the fission detonator blowing doors open (A_BlastDoors: bstone's BlastNearDoors). As in
# Planet Strike, a detonator can only be dropped within two tiles of the floor's security cube, so
# the doors are round the drop spot (X, a red floor tile) beside the cube's alcove:
#   north: a plain door           - blown open for good
#   south: a door locked red      - stays shut (locked doors aren't blasted)
#   west:  a one-way door         - stays shut (one-way doors aren't blasted)
#   2 east, in the north wall: a plain door out of reach - untouched
# Everything on it is Planet Strike's own, by its mapdefs ids (pfwolf-pk3's mapdefs/planetstrike,
# over mapdefs/blake).

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'detonator-demo'

$Layout = @(
    '################'   # y=20
    '##nnn#fff#######'   #      the room behind the plain door; the room behind the far door
    '##nnn#fff##llll#'   #      the arrival room (east)
    '#ww#N#F####llll#'   #      the west room; the plain door; the far door
    '#wwOX.......Pd.#'   #      the one-way door; the drop spot; the player, a detonator
    '#ww#L#C####llll#'   # y=25 the locked door; the security cube's alcove
    '###sss#####llll#'   #      the room behind the locked door
    '###sss##########'
    '################'
)
# Floor codes (mapdefs/blake/floors.yaml): area n is 108 + n. The hall, the cube's alcove and the
# arrival room are one area, as the detonator has to be dropped in the cube's area. Flats (plane 2):
# floor index + ceiling index * 256 (mapdefs/blake/flats.yaml): grey tiles under grey panels, the
# drop spot red.
$floor = 3 + 10 * 256          # F_GRYTIL, F_GRYPN1
$spot = 22 + 10 * 256          # F_SQLITR
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 42 }                                    # planet wall
$Tiles['l'] = @{ Plane0 = 109; Flat = $floor }                    # the hall and arrival room: area 1
$Tiles['.'] = $Tiles['l']
$Tiles['X'] = @{ Plane0 = 109; Flat = $spot }                     # the drop spot
$Tiles['C'] = @{ Plane0 = 109; Plane1 = 177; Flat = $floor }      # the security cube
$Tiles['P'] = @{ Plane0 = 109; Plane1 = 22; Flat = $floor }       # the player, facing west
$Tiles['d'] = @{ Plane0 = 109; Plane1 = 486; Flat = $floor }      # a fission detonator
$Tiles['n'] = @{ Plane0 = 110; Flat = $floor }                    # north room: area 2
$Tiles['f'] = @{ Plane0 = 111; Flat = $floor }                    # far room: area 3
$Tiles['w'] = @{ Plane0 = 112; Flat = $floor }                    # west room: area 4
$Tiles['s'] = @{ Plane0 = 113; Flat = $floor }                    # south room: area 5
$Tiles['N'] = @{ Plane0 = 91 }                                    # metal door, east-west
$Tiles['F'] = @{ Plane0 = 91 }                                    # the same, out of reach
$Tiles['L'] = @{ Plane0 = 91; Plane1 = 55 }                       # the same, locked: red card
$Tiles['O'] = @{ Plane0 = 100 }                                   # one-way door, passed going east
$planes = New-MapPlanes -Fill 64
Add-MapLayout $planes $Layout $Tiles 20 20
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Detonator Demo' $planes
