# Builds examples/mods/secrets-demo: its two levels, MAP01 (replacing Floor 1) and MAP10, the
# secret level Floor 1's secret elevator goes to. Run it after changing either:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-secrets-demo.ps1
#
# Floor 1 has five secrets: three pushwalls in the gallery, a hidden nook (a walk trigger), and
# the pushwall to the secret elevator. The rest shows pushwalls and floor codes at work: a switch
# that slides four walls at once, and a room of guards waiting in ambush beside guards who aren't.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'secrets-demo'

# Floor codes (pfwolf-pk3's mapdefs/wolf3d/floors.yaml): area n is 107 + n; 106 is the ambush
# code; 107, area 0, is also the secret-exit code, so the secret elevator's room is area 0.
$Ambush = 106; $SecretExit = 107
$Hall = 108; $Gallery = 109; $Vault = 110; $AmbushRoom = 111; $ExitRoom = 112

$Floor1 = @(
    '##########X#######################'   # y=22 the elevator switch
    '#########eeee###############Y#####'   #      the exit room; the secret elevator switch
    '#########eeee###########ssssssss##'   #      the secret elevator's room (floor code 107)
    '#########eeee###########ssssssss##'
    '############e###########ssssssss##'
    '############e###########ssssssss##'
    '############e###########ssssssss##'
    '############e#############s#######'
    '############e#############s#######'   # y=30
    '###u#u#u####e#vvvvvckv####s##s####'   #      the gallery's closets; the vault
    '###1#2#3####e#vvvvvvvc####ssss####'
    '###p#p#p####D#TTTT##S########q####'   # y=33 pushwalls; the vault's walls and switch
    '###gggggggg#..........#aaaaaaaaa##'   #      the gallery, the hall, the ambush room
    '###gggggggg#..........#aaaAaAaAa##'   #      guards in ambush (A)...
    '###ggggggggE..........Eaaaaaaaaa##'
    '##wgggggggg#..........#aaaaaaaaa##'   #      the hidden nook (w)
    '###gggggggg#..........#aaaNaNaNa##'   #      ...and guards not (N)
    '###gggggggg#.f..P...f.#aaaaaaaaa##'   # y=39 the player start, facing north
    '##################################'
)

$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 1 }                                                   # grey stone
$Tiles['.'] = @{ Plane0 = $Hall }
$Tiles['f'] = @{ Plane0 = $Hall; Plane1 = 31 }                                  # a plant
$Tiles['P'] = @{ Plane0 = $Hall; Plane1 = 19 }                                  # player start, facing north
$Tiles['g'] = @{ Plane0 = $Gallery }
$Tiles['u'] = @{ Plane0 = $Gallery }                                            # a closet behind a pushwall
$Tiles['1'] = @{ Plane0 = $Gallery; Plane1 = 52 }                               # cross
$Tiles['2'] = @{ Plane0 = $Gallery; Plane1 = 53 }                               # chalice
$Tiles['3'] = @{ Plane0 = $Gallery; Plane1 = 54 }                               # chest
$Tiles['p'] = @{ Plane0 = 8; Plane1 = 98 }                                      # pushwall (blue stone), a secret
$Tiles['w'] = @{ Plane0 = $Gallery; Plane1 = 840 }                              # the nook's walk trigger, a secret
$Tiles['v'] = @{ Plane0 = $Vault }
$Tiles['c'] = @{ Plane0 = $Vault; Plane1 = 54 }                                 # chest
$Tiles['k'] = @{ Plane0 = $Vault; Plane1 = 55 }                                 # crown
$Tiles['T'] = @{ Plane0 = 9; Tag = 1 }                                          # the vault's walls (blue stone), tag 1
$Tiles['S'] = @{ Plane0 = 140; Tag = 1 }                                        # the vault switch, tag 1
$Tiles['a'] = @{ Plane0 = $AmbushRoom }
$Tiles['A'] = @{ Plane0 = $Ambush; Plane1 = 108 }                               # guard in ambush, facing east
$Tiles['N'] = @{ Plane0 = $AmbushRoom; Plane1 = 108 }                           # guard, facing east
$Tiles['q'] = @{ Plane0 = 1; Plane1 = 98 }                                      # pushwall to the secret elevator
$Tiles['s'] = @{ Plane0 = $SecretExit }
$Tiles['Y'] = @{ Plane0 = 21 }                                                  # the secret elevator's switch
$Tiles['e'] = @{ Plane0 = $ExitRoom }
$Tiles['X'] = @{ Plane0 = 21 }                                                  # the elevator switch
$Tiles['D'] = @{ Plane0 = 91 }                                                  # door, north-south
$Tiles['E'] = @{ Plane0 = 90 }                                                  # door, east-west
$planes = New-MapPlanes
Add-MapLayout $planes $Floor1 $Tiles 15 22
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Secrets Demo' $planes

# Floor 10, the secret level: a treasure room, and an elevator on to Floor 2 (game-info's
# MAP10 next)
$Bonus = @(
    '#######X########'   # y=26
    '#bbbbbbbbbbbbbb#'
    '#bcbkb1bb2bkbcb#'
    '#bbbbbbbbbbbbbb#'
    '#bbbbbbbbbbbbbb#'   # y=30
    '#bcbbbbhbbbbbcb#'   #      an extra life
    '#bbbbbbbbbbbbbb#'
    '#bbbbbbbbbbbbbb#'
    '#bbbbbbPbbbbbbb#'   #      the player start, facing north
    '################'
)
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 8 }                                                   # blue stone
$Tiles['b'] = @{ Plane0 = 108 }
$Tiles['P'] = @{ Plane0 = 108; Plane1 = 19 }
$Tiles['c'] = @{ Plane0 = 108; Plane1 = 54 }
$Tiles['k'] = @{ Plane0 = 108; Plane1 = 55 }
$Tiles['1'] = @{ Plane0 = 108; Plane1 = 52 }
$Tiles['2'] = @{ Plane0 = 108; Plane1 = 53 }
$Tiles['h'] = @{ Plane0 = 108; Plane1 = 56 }
$Tiles['X'] = @{ Plane0 = 21 }
$planes = New-MapPlanes
Add-MapLayout $planes $Bonus $Tiles 24 26
Save-MapWad "$modDir\maps\MAP10.wad" 'MAP10' 'Bonus Room' $planes
