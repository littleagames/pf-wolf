# Builds examples/mods/classes-demo: the medic's kit and the armor vest (sprites/*.png) and the
# proving ground (maps/MAP01.wad). Run it after changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-classes-demo.ps1
#
# The classes themselves (actordefs/classes.yaml), their menu (game-info.yaml) and their status
# bar faces (graphicdefs/faces.yaml, cut from Wolf3D's own enemy sprites) are YAML.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'classes-demo'

# A field medkit: a bigger white case than the first aid kit, a red cross, a strap
Save-Drawing "$modDir\sprites\CLMKA0.png" 64 64 {
    param($g)
    Add-Rect $g @(230, 230, 224) 16 42 32 21
    Add-Rect $g @(190, 190, 184) 16 42 32 3
    Add-Rect $g @(200, 20, 20) 29 46 6 14
    Add-Rect $g @(200, 20, 20) 23 50 18 6
    Add-Rect $g @(90, 70, 40) 26 38 12 4
}
# An armor vest: olive cloth with plates, lying folded on the floor
Save-Drawing "$modDir\sprites\CLVSA0.png" 64 64 {
    param($g)
    Add-Poly $g @(80, 96, 50) @(18, 46, 46, 46, 50, 62, 14, 62)
    Add-Rect $g @(60, 72, 36) 22 50 9 10
    Add-Rect $g @(60, 72, 36) 34 50 9 10
    Add-Rect $g @(110, 126, 70) 18 46 28 2
    Add-Rect $g @(40, 40, 40) 30 46 4 16
}

# The proving ground: the hall where you start; the infirmary west (the medic's kits, which only
# a medic can take, and an ordinary first aid kit); the armory east (the vest, a machine gun, the
# gatling gun a scout can't carry, ammo); north, a room of guards, SS and dogs; the exit beyond.
$Layout = @(
    '###########Xxxx#############'   # y=20 the exit
    '#############D##############'
    '########rrrrrrrrrrrr########'   #      the fighting room
    '########rgrrrrrrrrgr########'
    '########rrrrrsrrrrrr########'
    '########rrrrrrrrrrrr########'   # y=25
    '########rrrrrrrrrrrr########'
    '########rrrrrrrrrrrr########'
    '########rrrsrrrrdrrr########'
    '########rdrrrrrrrrrr########'
    '########rrrrrrrrrrrr########'   # y=30
    '#############D##############'
    '###iiiiii#hhhhhhhh#aaaaaa###'   #      the infirmary, the hall, the armory
    '###imimii#hhhhhhhh#avaaGa###'   #      medic's kits; the vest, the gatling gun
    '###iiiiiiEhhhhhhhhEaaaaaa###'
    '###iiiiii#hhhhhhhh#aaaaaa###'   # y=35
    '###imiifi#hhhhhhhh#aacaMa###'   #      a first aid kit; ammo, a machine gun
    '###iiiiii#hhhPhhhh#aaaaaa###'   #      the player start, facing north
    '############################'
)
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 2 }                                   # grey stone
$Tiles['h'] = @{ Plane0 = 108 }
$Tiles['P'] = @{ Plane0 = 108; Plane1 = 19 }
$Tiles['i'] = @{ Plane0 = 109 }
$Tiles['m'] = @{ Plane0 = 109; Plane1 = 870 }                   # a field medkit
$Tiles['f'] = @{ Plane0 = 109; Plane1 = 48 }                    # a first aid kit
$Tiles['a'] = @{ Plane0 = 110 }
$Tiles['v'] = @{ Plane0 = 110; Plane1 = 871 }                   # the armor vest
$Tiles['G'] = @{ Plane0 = 110; Plane1 = 51 }                    # the gatling gun
$Tiles['M'] = @{ Plane0 = 110; Plane1 = 50 }                    # a machine gun
$Tiles['c'] = @{ Plane0 = 110; Plane1 = 49 }                    # a clip
$Tiles['r'] = @{ Plane0 = 111 }
$Tiles['g'] = @{ Plane0 = 111; Plane1 = 111 }                   # a guard facing south
$Tiles['s'] = @{ Plane0 = 111; Plane1 = 129 }                   # an SS facing south
$Tiles['d'] = @{ Plane0 = 111; Plane1 = 137 }                   # a dog facing south
$Tiles['x'] = @{ Plane0 = 112 }
$Tiles['X'] = @{ Plane0 = 21 }
$Tiles['D'] = @{ Plane0 = 91 }
$Tiles['E'] = @{ Plane0 = 90 }
$planes = New-MapPlanes -Fill 2
Add-MapLayout $planes $Layout $Tiles 18 20
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Classes Demo' $planes
