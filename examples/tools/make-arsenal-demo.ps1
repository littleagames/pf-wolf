# Builds examples/mods/arsenal-demo: its weapons' pictures (sprites/*.png: the view frames, the
# pickups, the grenade, the rocket and their blasts), their status bar icons and the rifle's scope
# (graphics/*.png), their sounds (sounds/*.wav) and the range to try them on (maps/MAP01.wad).
# Run it after changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-arsenal-demo.ps1 [-MidnightAssault <midnight-assault.pk3>]
#
# The weapons' pictures and sounds are Midnight Assault's, taken out of its pk3 under this mod's
# own names (AR...), so they can't clash with another pack's. The silenced pistol's quiet shot is
# the one thing it has no sound for, so that's made here.

param([string]$MidnightAssault = 'D:\projects\Wolf3D\Midnight Assault\midnight-assault.pk3')

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'arsenal-demo'
$spr = "$modDir\sprites"
$ma = Open-MidnightAssault $MidnightAssault

# Everything below is made afresh, so pictures and sounds an older version made don't linger
foreach ($old in @("$spr\*.png", "$modDir\graphics\AR*.png", "$modDir\sounds\*.wav")) { Remove-Item $old -ErrorAction SilentlyContinue }

# Copies Midnight Assault's sprite $From frames $Frames (letters, each frame's view 0) as this
# mod's sprite $To, frame for frame from $ToFirst on (A unless given)
function Copy-Frames([string]$From, [string]$Frames, [string]$To, [char]$ToFirst = 'A') {
    $i = 0
    foreach ($frame in $Frames.ToCharArray()) {
        $letter = [char]([int]$ToFirst + $i)
        Copy-MAPicture $ma "$From${frame}0.bmp" "$spr\$To${letter}0.png"
        $i++
    }
}

#
# THE SHOTGUN: SGUN (A in hand, B-C firing, D-H the pump), SGNR (loading shells), SHOT lying on
# the floor and SBOX, a box of shells
#
Copy-Frames 'SGUN' 'ABCDEFGH' 'ARSG'
Copy-Frames 'SGNR' 'ABCDEF' 'ARSR'
Copy-Frames 'SHOT' 'A' 'ARSP'
Copy-Frames 'SBOX' 'A' 'ARSB'

#
# THE GRENADE: GRNG (one in hand), GRNF (A held, B-D wound back, E thrown, F the follow-through),
# GBOX (a box of them, the weapon's pickup and its ammo's), GRND (A-D in flight, drawn at the
# bottom of the picture; E-H its blast in the air, I-L on the floor)
#
Copy-Frames 'GRNG' 'A' 'ARGV'
Copy-Frames 'GRNF' 'ABCDEF' 'ARGT'
Copy-Frames 'GBOX' 'A' 'ARGC'
Copy-Frames 'GRND' 'ABCDEFGHIJKL' 'ARGN'

#
# THE SCOPED RIFLE: SNPG (in hand), SNPF (A-B firing, C-D after), SNPB (working the bolt), SNIP
# lying on the floor and SCLP, a clip of rounds
#
Copy-Frames 'SNPG' 'A' 'ARRV'
Copy-Frames 'SNPF' 'ABCD' 'ARRF'
Copy-Frames 'SNPB' 'ABCDEFG' 'ARRB'
Copy-Frames 'SNIP' 'A' 'ARRP'
Copy-Frames 'SCLP' 'A' 'ARRA'

#
# THE SILENCED PISTOL: Midnight Assault's pistol (PISG in hand, PISF firing, PIST on the floor).
# It has no silenced one: its B frame's flash goes unused (actordefs/arsenal.yaml)
#
Copy-Frames 'PISG' 'A' 'ARPV'
Copy-Frames 'PISF' 'ABCD' 'ARPF'
Copy-Frames 'PIST' 'A' 'ARPP'

#
# THE BAZOOKA: BAZK (A in hand, B-C firing, D-F the backblast clearing), BAZR (loading a rocket),
# BZKA lying on the floor, RBOX a box of rockets, and RCKT, the rocket in flight from its 8 sides
# (A1-A8) and its blast (B-D)
#
Copy-Frames 'BAZK' 'ABCDEF' 'ARBZ'
Copy-Frames 'BAZR' 'ABCDEFGHIJK' 'ARBR'
Copy-Frames 'BZKA' 'A' 'ARBP'
Copy-Frames 'RBOX' 'A' 'ARBA'
foreach ($view in 1..8) { Copy-MAPicture $ma "RCKTA$view.bmp" "$spr\ARRKA$view.png" }
Copy-Frames 'RCKT' 'BCD' 'ARRK' 'B'

#
# STATUS BAR ICONS (48x24, the size of the game's own), made from the pickups on the status
# bar's blue (it draws an icon over the last one, so it mustn't be see-through), AND THE SCOPE
#
$BarBlue = [Drawing.Color]::FromArgb(255, 0, 0, 164)
Save-MAIcon $ma 'SHOTA0.bmp' "$modDir\graphics\ARICSG.png" -Background $BarBlue
Save-MAIcon $ma 'GBOXA0.bmp' "$modDir\graphics\ARICGR.png" -Background $BarBlue
Save-MAIcon $ma 'SNIPA0.bmp' "$modDir\graphics\ARICRF.png" -Background $BarBlue
Save-MAIcon $ma 'PISTA0.bmp' "$modDir\graphics\ARICSP.png" -Background $BarBlue
Save-MAIcon $ma 'BZKAA0.bmp' "$modDir\graphics\ARICBZ.png" -Background $BarBlue
# Midnight Assault's scope covers the whole 320x200 screen with its circle over the middle of a
# view with no status bar (y 100); moved up 20 it's over the middle of Wolf3D's (y 80), the
# rows it uncovers at the bottom black
Copy-MAPicture $ma 'ScopeOverlay.png' "$modDir\graphics\ARSCOPE.png" -ShiftY -20 -Fill ([Drawing.Color]::Black)

#
# SOUNDS (named in sounds/sound-seq.yaml)
#
$Sounds = [ordered]@{
    ARSHOT  = 'shotgun_fire.wav';       ARPUMP  = 'shotgun_pump.wav'
    ARSHELL = 'shotgun_load_shell.wav'; ARLDPMP = 'shotgun_load_pump.wav'
    ARTHROW = 'grenade_throw.wav';      ARBOOM  = 'grenade_explode.wav'
    ARCLNK1 = 'grenade_bounce1.wav';    ARCLNK2 = 'grenade_bounce2.wav'; ARCLNK3 = 'grenade_bounce3.wav'
    ARRIFLE = 'sniper_fire.wav';        ARBOLT  = 'sniper_bolt.wav';     ARZOOM = 'sniper_use_scope.wav'
    ARBAZK  = 'bazooka_fire.wav';       ARBZLD  = 'bazooka_load.wav';    ARRKHIT = 'rocket_explode.wav'
    ARPKGUN = 'pickup_gun.wav';         ARPKGR  = 'pickup_grenades.wav'; ARPKBZ = 'pickup_bazooka.wav'
}
foreach ($name in $Sounds.Keys) { Copy-MAFile $ma $Sounds[$name] "$modDir\sounds\$name.wav" }
Close-MidnightAssault $ma

# The silenced pistol's shot: a short, dull puff
$random = New-Object Random 9
$s = New-Samples 0.2; Add-Noise $s $random 0 0.6 0.25 0.035; Add-Tone $s 0 260 0.5 0.03
Save-Wav "$modDir\sounds\ARQUIET.wav" $s 0.35

#
# THE RANGE
#

# The armory (where you start, with every weapon and its ammo laid out), the range north of it
# (SS at the far end for the rifle, a knot of guards for a grenade or a rocket), close quarters
# west (dogs and guards round the pillars, for the shotgun) and the barracks east (guards facing
# the walls: the silenced pistol drops one without the others hearing). The exit is off the range.
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
    '##ccoccoccc#aVaaaa#bbbbbbbbnb#'   #      the bazooka...
    '##dccccccdc#avPaaa#bbbnbbbbbb#'   # y=40 ...its rockets, and the player start, facing north
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
$Tiles['V'] = @{ Plane0 = $Armory; Plane1 = 867 }         # bazooka
$Tiles['v'] = @{ Plane0 = $Armory; Plane1 = 868 }         # rockets
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
