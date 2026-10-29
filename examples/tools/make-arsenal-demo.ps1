# Builds examples/mods/arsenal-demo: its weapons' pictures (sprites/*.png: the view frames, the
# pickups, the grenade and its blast), their status bar icons and the rifle's scope
# (graphics/*.png), their sounds (sounds/*.wav) and the range to try them on (maps/MAP01.wad).
# Run it after changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-arsenal-demo.ps1
#
# A weapon's view frames are 64x64, drawn at the bottom middle of the view and scaled up as the
# game's own are; pickups stand on the floor at the bottom of theirs.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'arsenal-demo'
$spr = "$modDir\sprites"

# The colors everything's drawn in
$Metal = @(52, 54, 60); $MetalMid = @(88, 92, 100); $MetalLight = @(140, 144, 152)
$Wood = @(118, 70, 34); $WoodLight = @(156, 98, 52)
$Skin = @(232, 176, 128); $SkinDark = @(190, 132, 92)
$Olive = @(72, 90, 40); $OliveLight = @(108, 128, 62)
$Brass = @(206, 164, 64); $Red = @(176, 32, 24)
$Flash = @(255, 220, 80); $FlashHot = @(255, 250, 210)

# A muzzle flash: a star of yellow round a white-hot middle
function Add-Flash($g, [double]$cx, [double]$cy, [double]$r) {
    $pts = @()
    for ($i = 0; $i -lt 16; $i++) {
        $a = $i * [Math]::PI / 8
        $d = if ($i % 2 -eq 0) { $r } else { $r * 0.45 }
        $pts += $cx + $d * [Math]::Cos($a); $pts += $cy + $d * [Math]::Sin($a)
    }
    Add-Poly $g $Flash $pts
    Add-Ellipse $g $FlashHot ($cx - $r * 0.35) ($cy - $r * 0.35) ($r * 0.7) ($r * 0.7)
}

#
# THE SHOTGUN: two barrels running up the middle, a pump under them, the right hand on the grip
#

function Add-Shotgun($g, [double]$pump) {
    Add-Poly $g $Metal @(27, 16, 37, 16, 41, 54, 23, 54)                   # the barrels
    Add-Poly $g $MetalMid @(31, 16, 33, 16, 33.5, 54, 30.5, 54)
    Add-Rect $g $MetalLight 27 14 10 3                                     # their muzzles
    Add-Rect $g @(20, 20, 22) 31.5 14 1 3
    Add-Poly $g $Wood @(24, (30 + $pump), 40, (30 + $pump), 42, (42 + $pump), 22, (42 + $pump))    # the pump
    foreach ($k in 0..2) { Add-Rect $g $WoodLight 25 (32 + $pump + 4 * $k) 14 1 }
    Add-Poly $g $MetalMid @(21, 52, 43, 52, 47, 64, 17, 64)                # the receiver
    Add-Poly $g $Skin @(37, 54, 50, 55, 54, 64, 35, 64)                    # the hand on the grip
    Add-Poly $g $SkinDark @(37, 58, 46, 59, 47, 64, 36, 64)
}
Save-Drawing "$spr\ARSGA0.png" 64 64 { param($g) Add-Shotgun $g 0 }
Save-Drawing "$spr\ARSGB0.png" 64 64 { param($g) Add-Shotgun $g 0; Add-Flash $g 32 10 11 }
Save-Drawing "$spr\ARSGC0.png" 64 64 { param($g) Add-Shotgun $g 10 }          # the pump pulled back
# Lying on the floor, side on
Save-Drawing "$spr\ARSPA0.png" 64 64 {
    param($g)
    Add-Rect $g $Metal 6 54 40 3
    Add-Rect $g $Wood 14 57 14 3
    Add-Poly $g $Wood @(44, 54, 60, 55, 60, 61, 46, 60)
    Add-Rect $g $MetalMid 40 55 8 5
}
# A box of shells, red with brass ends showing
Save-Drawing "$spr\ARSBA0.png" 64 64 {
    param($g)
    Add-Rect $g $Red 22 50 20 13
    Add-Rect $g @(120, 20, 16) 22 50 20 2
    foreach ($k in 0..4) { Add-Rect $g $Brass (24 + 4 * $k) 46 3 4 }
}

#
# THE GRENADE: held low on the right, wound back, then thrown
#

function Add-Grenade($g, [double]$x, [double]$y, [double]$size) {
    Add-Ellipse $g $Olive $x $y $size ($size * 1.25)
    Add-Ellipse $g $OliveLight ($x + $size * 0.2) ($y + $size * 0.15) ($size * 0.3) ($size * 0.4)
    Add-Rect $g $MetalMid ($x + $size * 0.35) ($y - $size * 0.15) ($size * 0.3) ($size * 0.2)     # the fuse
    Add-Rect $g $MetalLight ($x + $size * 0.6) ($y - $size * 0.1) ($size * 0.35) ($size * 0.12)   # the lever
}
Save-Drawing "$spr\ARGVA0.png" 64 64 {
    param($g)
    Add-Grenade $g 36 34 16
    Add-Poly $g $Skin @(32, 46, 56, 44, 60, 64, 30, 64)
    Add-Poly $g $SkinDark @(34, 50, 52, 50, 50, 54, 35, 55)
}
Save-Drawing "$spr\ARGVB0.png" 64 64 {
    param($g)
    Add-Grenade $g 46 44 14
    Add-Poly $g $Skin @(42, 54, 64, 52, 64, 64, 40, 64)
}
Save-Drawing "$spr\ARGVC0.png" 64 64 {
    param($g)
    Add-Poly $g $Skin @(22, 40, 40, 38, 44, 64, 18, 64)                   # the open hand, thrown
    Add-Poly $g $SkinDark @(24, 40, 28, 34, 31, 40)
    Add-Poly $g $SkinDark @(30, 39, 34, 32, 37, 39)
}
# In flight and on the floor: drawn in the middle of its picture (its height is the missile's z)
Save-Drawing "$spr\ARGNA0.png" 64 64 { param($g) Add-Grenade $g 28 28 8 }
Save-Drawing "$spr\ARGNB0.png" 64 64 {
    param($g)
    Add-Ellipse $g $Olive 27 30 10 8
    Add-Ellipse $g $OliveLight 29 31 4 3
    Add-Rect $g $MetalMid 25 32 3 3
}
# A crate of grenades (the weapon's pickup) and a box of three (more of them)
Save-Drawing "$spr\ARGCA0.png" 64 64 {
    param($g)
    Add-Rect $g @(92, 100, 52) 16 44 32 19
    Add-Rect $g @(60, 66, 34) 16 44 32 2
    Add-Rect $g @(60, 66, 34) 16 53 32 1
    foreach ($k in 0..2) { Add-Grenade $g (20 + 9 * $k) 36 7 }
}
Save-Drawing "$spr\ARGBA0.png" 64 64 {
    param($g)
    Add-Rect $g @(92, 100, 52) 22 52 20 11
    foreach ($k in 0..2) { Add-Grenade $g (23 + 6 * $k) 46 5 }
}
# The blast: a white flash, a fireball, then smoke
Save-Drawing "$spr\AREXA0.png" 64 64 {
    param($g)
    Add-Ellipse $g $Flash 16 16 32 32
    Add-Ellipse $g $FlashHot 24 24 16 16
}
Save-Drawing "$spr\AREXB0.png" 64 64 {
    param($g)
    Add-Ellipse $g @(220, 90, 20) 6 8 52 50
    Add-Ellipse $g $Flash 16 18 32 28
    Add-Ellipse $g @(255, 160, 40) 22 24 20 14
}
Save-Drawing "$spr\AREXC0.png" 64 64 {
    param($g)
    Add-Ellipse $g @(70, 60, 56) 4 6 56 52
    Add-Ellipse $g @(110, 60, 30) 14 18 36 28
    Add-Ellipse $g @(96, 88, 82) 10 8 20 18
}

#
# THE SCOPED RIFLE: a long thin barrel, the scope on top, the stock at the bottom
#

function Add-Rifle($g) {
    Add-Poly $g $Metal @(30, 4, 34, 4, 36.5, 50, 27.5, 50)
    Add-Rect $g $MetalLight 31.5 4 1 30
    Add-Poly $g @(36, 40, 44) @(25, 26, 39, 26, 40, 42, 24, 42)           # the scope
    Add-Ellipse $g @(120, 170, 200) 27 23 10 5
    Add-Poly $g $Wood @(23, 48, 41, 48, 49, 64, 15, 64)
    Add-Poly $g $WoodLight @(26, 48, 30, 48, 26, 64, 20, 64)
    Add-Poly $g $Skin @(39, 55, 51, 56, 54, 64, 37, 64)
}
Save-Drawing "$spr\ARRVA0.png" 64 64 { param($g) Add-Rifle $g }
Save-Drawing "$spr\ARRVB0.png" 64 64 { param($g) Add-Rifle $g; Add-Flash $g 32 3 8 }
Save-Drawing "$spr\ARRPA0.png" 64 64 {
    param($g)
    Add-Rect $g $Metal 4 55 40 2
    Add-Rect $g @(36, 40, 44) 22 51 12 4
    Add-Poly $g $Wood @(40, 55, 60, 54, 60, 61, 44, 59)
}
Save-Drawing "$spr\ARRAA0.png" 64 64 {
    param($g)
    Add-Rect $g @(80, 84, 60) 24 54 16 9
    foreach ($k in 0..3) { Add-Rect $g $Brass (25 + 4 * $k) 48 2 6 }
}

#
# THE SILENCED PISTOL: a long silencer out in front, held in both hands
#

function Add-SilencedPistol($g) {
    Add-Poly $g $Metal @(29, 22, 35, 22, 36, 46, 28, 46)
    Add-Rect $g $MetalLight 31 22 1 24
    Add-Poly $g $MetalMid @(25, 44, 39, 44, 41, 54, 23, 54)
    Add-Poly $g $Skin @(19, 52, 45, 52, 49, 64, 15, 64)
    Add-Poly $g $SkinDark @(24, 56, 40, 56, 41, 60, 23, 60)
}
Save-Drawing "$spr\ARPVA0.png" 64 64 { param($g) Add-SilencedPistol $g }
Save-Drawing "$spr\ARPVB0.png" 64 64 {
    param($g)
    Add-SilencedPistol $g
    Add-Ellipse $g @(170, 170, 170) 28 15 8 6                              # a wisp, no flash
}
Save-Drawing "$spr\ARPPA0.png" 64 64 {
    param($g)
    Add-Rect $g $Metal 18 54 26 3
    Add-Poly $g $MetalMid @(38, 54, 46, 54, 46, 62, 41, 62)
}

#
# STATUS BAR ICONS (48x24, the size of the game's own) AND THE SCOPE
#

Save-Drawing "$modDir\graphics\ARICSG.png" 48 24 {
    param($g)
    Add-Rect $g $MetalLight 2 9 30 3
    Add-Rect $g $WoodLight 8 12 10 3
    Add-Poly $g $WoodLight @(30, 9, 46, 10, 46, 17, 32, 15)
}
Save-Drawing "$modDir\graphics\ARICGR.png" 48 24 {
    param($g)
    foreach ($k in 0..2) { Add-Grenade $g (6 + 13 * $k) 7 10 }
}
Save-Drawing "$modDir\graphics\ARICRF.png" 48 24 {
    param($g)
    Add-Rect $g $MetalLight 1 11 32 2
    Add-Rect $g @(70, 74, 80) 14 7 12 4
    Add-Poly $g $WoodLight @(30, 10, 47, 10, 47, 17, 33, 15)
}
Save-Drawing "$modDir\graphics\ARICSP.png" 48 24 {
    param($g)
    Add-Rect $g $MetalLight 4 9 26 3
    Add-Poly $g $MetalLight @(28, 8, 40, 8, 40, 19, 34, 19)
}
# The scope: covers the whole 320x200 screen, black but for a circle over the view's middle
# (160, 80: the view above the status bar), with crosshairs
Save-Picture "$modDir\graphics\ARSCOPE.png" 320 200 {
    param($x, $y)
    $dx = $x - 160; $dy = $y - 80
    $d = [Math]::Sqrt($dx * $dx + $dy * $dy)
    if ($d -gt 78) { return New-Color 0 0 0 }
    if ($d -gt 75) { return New-Color 30 30 32 }
    if (($x -eq 160 -or $y -eq 80) -and $d -gt 6) { return New-Color 0 0 0 }
    $null
}

#
# SOUNDS
#

$random = New-Object Random 9
$s = New-Samples 0.6; Add-Noise $s $random 0 1.0 0.45 0.09; Add-Tone $s 0 70 1.2 0.12; Add-Noise $s $random 0.04 0.4 0.08 0.25
Save-Wav "$modDir\sounds\ARSHOT.wav" $s 0.85
$s = New-Samples 0.45
foreach ($at in 0, 0.2) { Add-Noise $s $random $at 0.8 0.7 0.012 0.05; Add-Tone $s $at 700 0.5 0.02 0.04 }
Save-Wav "$modDir\sounds\ARPUMP.wav" $s 0.5
$s = New-Samples 0.15; Add-Noise $s $random 0 0.8 0.75 0.01 0.05; Add-Tone $s 0 1100 0.4 0.015
Save-Wav "$modDir\sounds\ARSHELL.wav" $s 0.4
$s = New-Samples 0.35
for ($i = 0; $i -lt $s.Length; $i++) { $t = $i / $script:SampleRate; $s[$i] = ($random.NextDouble() * 2 - 1) * [Math]::Sin([Math]::PI * $t / 0.35) }
Save-Wav "$modDir\sounds\ARTHROW.wav" $s 0.35
$s = New-Samples 1.4; Add-Noise $s $random 0 1.0 0.12 0.45; Add-Tone $s 0 38 1.4 0.5; Add-Noise $s $random 0 0.6 0.6 0.05
Save-Wav "$modDir\sounds\ARBOOM.wav" $s 0.95
$s = New-Samples 0.2; Add-Tone $s 0 1800 0.6 0.04; Add-Tone $s 0 2650 0.4 0.03; Add-Noise $s $random 0 0.3 0.9 0.005
Save-Wav "$modDir\sounds\ARCLINK.wav" $s 0.4
$s = New-Samples 0.2; Add-Noise $s $random 0 0.6 0.25 0.035; Add-Tone $s 0 260 0.5 0.03
Save-Wav "$modDir\sounds\ARQUIET.wav" $s 0.35
$s = New-Samples 0.9; Add-Noise $s $random 0 1.0 0.9 0.04; Add-Tone $s 0 95 1.0 0.08; Add-Noise $s $random 0.06 0.35 0.1 0.35
Save-Wav "$modDir\sounds\ARRIFLE.wav" $s 0.9
$s = New-Samples 0.08; Add-Tone $s 0 2200 0.6 0.01; Add-Noise $s $random 0 0.3 0.9 0.004
Save-Wav "$modDir\sounds\ARZOOM.wav" $s 0.3

#
# THE RANGE
#

# The armory (where you start, with every weapon and its ammo laid out), the range north of it
# (SS at the far end for the rifle, a knot of guards for a grenade), close quarters west (dogs
# and guards round the pillars, for the shotgun) and the barracks east (guards facing the
# walls: the silenced pistol drops one without the others hearing). The exit is off the range.
$Range = @(
    '##############################'   # y=18
    '##############################'
    '##########rrrrrrrrrr##########'   # y=20 the range
    '##########rZrrrZrrZr#xxx######'   #      SS at the far end; the exit room
    '##########rrrrrrrrrrExxxX#####'
    '##########rrrrrrrrrr#xxx######'
    '##########rorrrrrror##########'
    '##########rrrrrrrrrr##########'
    '##########rrrrrrrHrr##########'   #      guards in the middle
    '##########rrHrHrrrrr##########'
    '##########rrrHrrrrrr##########'
    '##########rrrrrrrrrr##########'   # y=29
    '##########rrrrrrrrrr##########'
    '##########rorrrrrror##########'
    '##########rrrrrrrrrr##########'
    '##########rrrrrrrrrr##########'
    '##############D###############'   # y=34
    '##cdcccccck#aaaaaa#bBbBbBbBbb#'   #      close quarters, the armory, the barracks
    '##ccoccoccc#SaRaGL#bbbbbbbbbb#'   #      the weapons...
    '##cccckcccc#safagl#bbnbbbbbbb#'   #      ...and their ammo
    '##cccccccccEaaaaaaEbbbbbnbbbb#'
    '##ccoccoccc#aaaaaa#bbbbbbbbnb#'
    '##dccccccdc#aaPaaa#bbbnbbbbbb#'   # y=40 the player start, facing north
    '##############################'
)
$Armory = 108; $RangeArea = 109; $Close = 110; $Barracks = 111; $Exit = 112
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 1 }
$Tiles['r'] = @{ Plane0 = $RangeArea }
$Tiles['Z'] = @{ Plane0 = $RangeArea; Plane1 = 129 }      # SS facing south
$Tiles['H'] = @{ Plane0 = $RangeArea; Plane1 = 111 }      # guard facing south
$Tiles['o'] = @{ Plane0 = $RangeArea; Plane1 = 30 }       # white column
$Tiles['x'] = @{ Plane0 = $Exit }
$Tiles['X'] = @{ Plane0 = 21 }
$Tiles['a'] = @{ Plane0 = $Armory }
$Tiles['P'] = @{ Plane0 = $Armory; Plane1 = 19 }
$Tiles['S'] = @{ Plane0 = $Armory; Plane1 = 860 }         # shotgun
$Tiles['s'] = @{ Plane0 = $Armory; Plane1 = 861 }         # shells
$Tiles['G'] = @{ Plane0 = $Armory; Plane1 = 862 }         # grenade crate
$Tiles['g'] = @{ Plane0 = $Armory; Plane1 = 863 }         # grenades
$Tiles['R'] = @{ Plane0 = $Armory; Plane1 = 864 }         # scoped rifle
$Tiles['f'] = @{ Plane0 = $Armory; Plane1 = 865 }         # rifle rounds
$Tiles['L'] = @{ Plane0 = $Armory; Plane1 = 866 }         # silenced pistol
$Tiles['l'] = @{ Plane0 = $Armory; Plane1 = 49 }          # a clip, for it
$Tiles['c'] = @{ Plane0 = $Close }
$Tiles['d'] = @{ Plane0 = $Close; Plane1 = 134 }          # dog facing east
$Tiles['k'] = @{ Plane0 = $Close; Plane1 = 108 }          # guard facing east
$Tiles['b'] = @{ Plane0 = $Barracks }
$Tiles['B'] = @{ Plane0 = $Barracks; Plane1 = 45 }        # a bed
$Tiles['n'] = @{ Plane0 = $Barracks; Plane1 = 109 }       # guard facing north, to the wall
$Tiles['D'] = @{ Plane0 = 91 }
$Tiles['E'] = @{ Plane0 = 90 }
$planes = New-MapPlanes
Add-MapLayout $planes $Range $Tiles 16 18
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Arsenal Demo' $planes
