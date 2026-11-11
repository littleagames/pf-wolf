# Builds examples/mods/vehicles-demo: its floors and ceiling (flats/*.png), sky
# (graphics/VDSKY.png), the jeep, the tank, the tank's cannon and its shell (sprites/*.png), its
# sounds (sounds/*.wav) and its level (maps/MAP01.wad). Run it after changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-vehicles-demo.ps1 [-MidnightAssault <midnight-assault.pk3>]
#
# The floors, ceiling, sky, the tank, its shell and their sounds are Midnight Assault's, taken
# out of its pk3 under this mod's own names; the jeep, the cannon in your hands and the rest of
# the sounds, which it has none of, are made here.
#
# The level: a motor pool in the west with a jeep and a tank parked in it; a courtyard full of
# guards and dogs behind its door; a bunker of SS and an officer past the courtyard; and the
# exit beneath the bunker.

param([string]$MidnightAssault = 'D:\projects\Wolf3D\Midnight Assault\midnight-assault.pk3')

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'vehicles-demo'
$ma = Open-MidnightAssault $MidnightAssault

#
# FLATS AND SKY: Midnight Assault's
#

# Concrete for the motor pool and the bunker, the courtyard's tarmac, and the motor pool's roof
# (panels between beams)
Copy-MAPicture $ma 'ROAD.bmp' "$modDir\flats\VDCONC.png"
Copy-MAPicture $ma 'ASPHALT.bmp' "$modDir\flats\VDROAD.png"
Copy-MAPicture $ma 'INTERIOR_CEILING.bmp' "$modDir\flats\VDROOF.png"
# A day sky with clouds (1024x64, so it goes round the view once)
Copy-MAPicture $ma 'SKY_DAY.png' "$modDir\graphics\VDSKY.png"

#
# SPRITES
#

# The tank, Midnight Assault's, seen from the front. Sprites stand on the floor: it sits at the
# bottom.
Copy-MAPicture $ma 'TANKA0.bmp' "$modDir\sprites\VDTKA0.png"
# The cannon's shell: Midnight Assault's enemy rocket from its 8 sides (VDSH A1-A8), and its
# blast (B-D)
foreach ($view in 1..8) { Copy-MAPicture $ma "ERKTA$view.bmp" "$modDir\sprites\VDSHA$view.png" }
$i = 0
foreach ($frame in 'A', 'B', 'C') { Copy-MAPicture $ma "EBOM${frame}0.bmp" "$modDir\sprites\VDSH$([char](66 + $i))0.png"; $i++ }

# The jeep, side on, facing right: an olive body with a white star, a fold-down windscreen,
# black wheels and a spare on the back. Sprites stand on the floor: it sits at the bottom.
# (a dull olive snaps to grey in Wolf3D's palette, so these are its own greens)
$olive = @(84, 116, 20); $oliveDark = @(52, 76, 8); $tyre = @(24, 24, 26); $hub = @(120, 120, 112)
Save-Drawing "$modDir\sprites\VDJPA0.png" 64 64 {
    param($g)
    Add-Rect $g $oliveDark 4 40 54 10                                   # the tub, in shadow below
    Add-Rect $g $olive 4 36 54 8                                        # its sides
    Add-Poly $g $olive @(44, 36, 62, 39, 62, 46, 44, 46)                # the bonnet, sloping to the grille
    Add-Rect $g @(40, 40, 36) 61 39 2 7                                 # the grille
    Add-Rect $g @(60, 60, 64) 37 22 2 15                                # the windscreen's frame
    Add-Rect $g @(150, 190, 210) 39 24 2 12                             # its glass, edge on
    Add-Rect $g @(48, 48, 50) 14 30 14 3                                # the seat backs
    Add-Ellipse $g $tyre 0 33 7 14                                      # the spare on the back
    Add-Poly $g @(236, 236, 228) @(22, 37, 24, 41, 28, 41, 25, 43, 26, 47, 22, 44, 18, 47, 19, 43, 16, 41, 20, 41)   # the star
    foreach ($wx in 8, 42) {
        Add-Ellipse $g $tyre $wx 45 15 15
        Add-Ellipse $g $hub ($wx + 5) 50 5 5
    }
}


# The cannon from inside the turret, as the view's weapon (64x64, drawn at the bottom of the
# view): the breech and barrel running up from the bottom middle, its muzzle brake short of the
# middle of the view so it doesn't hide what it's aimed at; B is the moment it fires, with a
# flash beyond the muzzle
function Add-Cannon($g) {
    Add-Poly $g @(52, 80, 16) @(24, 64, 40, 64, 35, 44, 29, 44)         # the barrel, narrowing away
    Add-Poly $g @(96, 128, 40) @(30, 64, 34, 64, 33, 44, 31, 44)        # lit along its top
    Add-Rect $g @(36, 56, 8) 27 41 10 4                                 # the muzzle brake
    Add-Rect $g @(72, 104, 24) 27 41 10 1
    Add-Rect $g @(36, 38, 34) 16 58 32 6                                # the breech
}
Save-Drawing "$modDir\sprites\VDCNA0.png" 64 64 { param($g); Add-Cannon $g }
Save-Drawing "$modDir\sprites\VDCNB0.png" 64 64 {
    param($g)
    Add-Ellipse $g @(255, 160, 40) 20 24 24 20
    Add-Ellipse $g @(255, 230, 120) 26 28 12 12
    Add-Cannon $g
}

# The cannon's status bar picture (48x24, as Wolf3D's weapon pictures): the tank, on the bar's
# blue (the bar draws it over the last weapon's picture, so it mustn't be see-through)
Save-MAIcon $ma 'TANKA0.bmp' "$modDir\graphics\VDICCN.png" -Background ([Drawing.Color]::FromArgb(255, 0, 0, 164))

#
# SOUNDS: the tank's engine, its gun and the shell's blast are Midnight Assault's
#

Copy-MAFile $ma 'tank_move.wav' "$modDir\sounds\VDTANK.wav"
Copy-MAFile $ma 'tank_attack.wav' "$modDir\sounds\VDCANN.wav"
Copy-MAFile $ma 'rocket_explode.wav' "$modDir\sounds\VDBOOM.wav"
Close-MidnightAssault $ma

$random = New-Object Random 3
$sr = $script:SampleRate

# The jeep starting: a cranking engine catching and revving up to a buzz
$jeep = New-Samples 1.2
for ($i = 0; $i -lt $jeep.Length; $i++) {
    $t = $i / $sr
    $rate = 8 + 40 * [Math]::Min(1, [Math]::Max(0, $t - 0.25) / 0.6)    # firings a second
    $pulse = [Math]::Pow([Math]::Max(0, [Math]::Sin(2 * [Math]::PI * $rate * $t)), 4)
    $tone = [Math]::Sin(2 * [Math]::PI * 90 * $t) + 0.6 * [Math]::Sin(2 * [Math]::PI * 180 * $t) + 0.3 * [Math]::Sin(2 * [Math]::PI * 270 * $t)
    $fade = [Math]::Min(1, (1.2 - $t) / 0.2)
    $jeep[$i] = (0.35 + 0.65 * $pulse) * $tone * $fade
}
Add-Noise $jeep $random 0 0.2 0.1
Save-Wav "$modDir\sounds\VDJEEP.wav" $jeep 0.55

# Running someone down: a thump and a crunch
$crunch = New-Samples 0.45
Add-Tone $crunch 0 80 1.0 0.12
Add-Noise $crunch $random 0 0.8 0.5 0.08
foreach ($k in 1..3) { Add-Noise $crunch $random (0.05 + 0.06 * $k) 0.5 0.7 0.01 0.03 }
Save-Wav "$modDir\sounds\VDCRSH.wav" $crunch 0.6

# Getting out: a door's clunk
$clunk = New-Samples 0.3
Add-Tone $clunk 0 140 1.0 0.06
Add-Tone $clunk 0.004 260 0.4 0.04
Add-Noise $clunk $random 0 0.6 0.4 0.02
Save-Wav "$modDir\sounds\VDOUT.wav" $clunk 0.55

#
# THE LEVEL
#

# One character per tile, the top-left corner at tile ($Left, $Top); every tile outside is grey
# stone. Doors: '-' passes east-west, '|' north-south.
$Left = 12
$Top = 20
$Layout = @(
    '########################################'   # y=20
    '#......#.........................#.....#'   #      the motor pool | the courtyard | the bunker
    '#.PJ...#....g.......d.......g....#..s..#'   #      the start, beside the jeep
    '#......#.........B.........B.....#.....#'
    '#......-..g.........g.........d..-..o..#'   # y=24 the doors out of the motor pool and into the bunker
    '#..T...#.........B.........B.....#.....#'   #      the tank
    '#......#......g..........g.......#..s..#'
    '#a.h...#.........................#.....#'
    '###################################|####'   # y=28 the way down to the exit
    '##################################eeX###'
    '########################################'
)

# Floor codes: area n is 107 + n
$MotorPool = 108; $Courtyard = 109; $Bunker = 110; $ExitRoom = 111
# Flats (the mod's mapdefs/flats.yaml): concrete under a roof indoors, tarmac under the sky out
$Indoors = 40 + 40 * 256; $Outdoors = 41
# Things (the mod's mapdefs/things.yaml)
$Jeep = 940; $Tank = 941

$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 1 }                                           # grey stone
$Tiles['.'] = @{ Plane0 = $MotorPool }
$Tiles['-'] = @{ Plane0 = 90 }                                          # door, passed east-west
$Tiles['|'] = @{ Plane0 = 91 }                                          # door, passed north-south
$Tiles['P'] = @{ Plane0 = $MotorPool; Plane1 = 20 }                     # player start, facing east
$Tiles['J'] = @{ Plane0 = $MotorPool; Plane1 = $Jeep }
$Tiles['T'] = @{ Plane0 = $MotorPool; Plane1 = $Tank }
$Tiles['g'] = @{ Plane0 = $MotorPool; Plane1 = 110 }                    # guard facing west
$Tiles['d'] = @{ Plane0 = $MotorPool; Plane1 = 136 }                    # dog facing west
$Tiles['s'] = @{ Plane0 = $MotorPool; Plane1 = 128 }                    # SS facing west
$Tiles['o'] = @{ Plane0 = $MotorPool; Plane1 = 118 }                    # officer facing west
$Tiles['B'] = @{ Plane0 = $MotorPool; Plane1 = 24 }                     # green barrel
$Tiles['a'] = @{ Plane0 = $MotorPool; Plane1 = 49 }                     # ammo
$Tiles['h'] = @{ Plane0 = $MotorPool; Plane1 = 48 }                     # first aid kit
$Tiles['e'] = @{ Plane0 = $ExitRoom }
$Tiles['X'] = @{ Plane0 = 21 }                                          # elevator switch

$planes = New-MapPlanes
Add-MapLayout $planes $Layout $Tiles $Left $Top

# Each floor tile's area and flats by where it is: the courtyard (columns 8-32) is outdoors
for ($row = 0; $row -lt $Layout.Count; $row++) {
    for ($col = 0; $col -lt $Layout[$row].Length; $col++) {
        $i = ($Top + $row) * 64 + $Left + $col
        if ($planes[0][$i] -notin $MotorPool, $ExitRoom) { continue }
        if ($planes[0][$i] -eq $MotorPool) {
            if ($col -ge 8 -and $col -le 32) { $planes[0][$i] = $Courtyard }
            elseif ($col -ge 34) { $planes[0][$i] = $Bunker }
        }
        $planes[2][$i] = if ($planes[0][$i] -eq $Courtyard) { $Outdoors } else { $Indoors }
    }
}
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Vehicles Demo' $planes
