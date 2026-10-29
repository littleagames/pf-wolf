# Builds examples/mods/boss-demo: the boss health bar's pictures (graphics/*.png) and the arena
# (maps/MAP01.wad). Run it after changing either:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-boss-demo.ps1
#
# The boss and his second phase are actordefs (actordefs/warden.yaml); the fight's rules and its
# health bar are a script (scripts/boss.lua).

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'boss-demo'

# The bar's frame: 164x12, a dark inside with a gold rim; the script fills it 2 pixels a step
Save-Picture "$modDir\graphics\BFBAR.png" 164 12 {
    param($x, $y)
    if ($x -eq 0 -or $y -eq 0 -or $x -eq 163 -or $y -eq 11) { return New-Color 200 160 60 }
    if ($x -eq 1 -or $y -eq 1 -or $x -eq 162 -or $y -eq 10) { return New-Color 90 70 30 }
    New-Color 20 10 10
}
# A step of the bar: 2 pixels of red, lighter along the top
Save-Picture "$modDir\graphics\BFSEG.png" 2 8 {
    param($x, $y)
    if ($y -lt 2) { return New-Color 255 110 90 }
    if ($y -gt 5) { return New-Color 120 10 10 }
    New-Color 210 30 24
}

# The arena: the start room south, with ammo and first aid; the arena, its pillars for cover and
# the Warden at the far end; the exit north, through a door that's locked until he's dead
$Layout = @(
    '########################'   # y=20
    '##########xxxxX#########'   #      the exit
    '##########xxxx##########'
    '############G###########'   #      the locked door, tag 1
    '####aaaaaaaaaaaaaaaa####'
    '####aaaaaaaaBaaaaaaa####'   # y=25 the Warden
    '####aaaaaaaaaaaaaaaa####'
    '####aaaoaaaaaaaaoaaa####'   #      pillars
    '####aaaaaaaaaaaaaaaa####'
    '####aaaaaaaaaaaaaaaa####'
    '####aaaaaaaooaaaaaaa####'   # y=30
    '####aaaaaaaaaaaaaaaa####'
    '####aaaaaaaaaaaaaaaa####'
    '####aaaoaaaaaaaaoaaa####'
    '####aaaaaaaaaaaaaaaa####'
    '####aaaaaaaaaaaaaaaa####'   # y=35
    '####aaaaaaaaaaaaaaaa####'
    '###########D############'
    '#########hssssh#########'   #      the start room: first aid kits
    '#########ssssss#########'
    '#########ssssss#########'   # y=40
    '#########csPssc#########'   #      ammo; the player start, facing north
    '########################'
)
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 17 }                                 # red brick
$Tiles['a'] = @{ Plane0 = 109 }
$Tiles['o'] = @{ Plane0 = 109; Plane1 = 30 }                   # a white column
$Tiles['B'] = @{ Plane0 = 109; Plane1 = 900 }                  # the Warden
$Tiles['s'] = @{ Plane0 = 108 }
$Tiles['P'] = @{ Plane0 = 108; Plane1 = 19 }
$Tiles['h'] = @{ Plane0 = 108; Plane1 = 48 }                   # first aid kit
$Tiles['c'] = @{ Plane0 = 108; Plane1 = 51 }                   # the gatling gun
$Tiles['x'] = @{ Plane0 = 110 }
$Tiles['X'] = @{ Plane0 = 21 }
$Tiles['G'] = @{ Plane0 = 93; Tag = 1 }                        # a gold-locked door; the script opens it
$Tiles['D'] = @{ Plane0 = 91 }
$planes = New-MapPlanes -Fill 17
Add-MapLayout $planes $Layout $Tiles 20 20
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Boss Demo' $planes
