# Builds examples/mods/ambience-demo: its sounds (sounds/AM*.wav), the generator's and the
# radio's sprites (sprites/AMGN*.png, sprites/AMRD*.png) and the level (maps/MAP01.wad). Run it
# after changing any of them (the sounds take a minute or so):
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-ambience-demo.ps1
#
# Every sound is synthesized here. The loops (the zones' ambient sounds) loop seamlessly: a tone
# is a whole number of cycles long, and noise is made a little longer than the loop with its end
# faded into its start (Close-Loop).

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'ambience-demo'
$sr = $script:SampleRate
$random = New-Object Random 31

function Hz([double]$Midi) { 440 * [Math]::Pow(2, ($Midi - 69) / 12) }

# A loop $Seconds long from samples $Overlap seconds longer: the extra past the end is faded into
# the start, so the end runs on into the start with no seam
function Close-Loop([double[]]$S, [double]$Seconds, [double]$Overlap) {
    $n = [int]($Seconds * $sr); $o = [int]($Overlap * $sr)
    $out = New-Object double[] $n
    for ($i = 0; $i -lt $n; $i++) { $out[$i] = $S[$i] }
    for ($i = 0; $i -lt $o; $i++) {
        $a = $i / $o
        $out[$i] = $S[$i] * $a + $S[$n + $i] * (1 - $a)
    }
    , $out
}

function Rnd { $random.NextDouble() * 2 - 1 }

#
# THE ZONES' LOOPS
#

# Wind (the courtyard): a deep rush and a little hiss, in slow gusts, eight seconds
$len = 8; $ov = 1
$s = New-Samples ($len + $ov)
$lp1 = 0.0; $lp2 = 0.0
for ($i = 0; $i -lt $s.Length; $i++) {
    $t = $i / $sr
    $lp1 += 0.02 * ((Rnd) - $lp1)
    $lp2 += 0.12 * ((Rnd) - $lp2)
    $gust = 0.55 + 0.3 * [Math]::Sin(2 * [Math]::PI * $t / 8) + 0.15 * [Math]::Sin(2 * [Math]::PI * $t / 2.9 + 1)
    $s[$i] = $gust * (3 * $lp1 + 0.4 * $lp2)
}
Save-Wav "$modDir\sounds\AMWIND.wav" (Close-Loop $s $len $ov) 0.6

# The sewer: a low rumble and water trickling, six seconds
$len = 6
$s = New-Samples ($len + $ov)
$lp1 = 0.0; $lp2 = 0.0
for ($i = 0; $i -lt $s.Length; $i++) {
    $t = $i / $sr
    $lp1 += 0.006 * ((Rnd) - $lp1)
    $lp2 += 0.3 * ((Rnd) - $lp2)
    $bubble = 0.5 + 0.5 * [Math]::Sin(2 * [Math]::PI * 3.1 * $t) * [Math]::Sin(2 * [Math]::PI * 0.7 * $t)
    $s[$i] = 6 * $lp1 + 0.25 * $lp2 * $bubble
}
Save-Wav "$modDir\sounds\AMSEWER.wav" (Close-Loop $s $len $ov) 0.5

# The generator room's buzz: mains hum (50 Hz) and its harmonics, two seconds (a whole number of
# cycles); the generators' own drone is AMGEN, below
$hum = New-Samples 2
for ($i = 0; $i -lt $hum.Length; $i++) {
    $t = $i / $sr
    $hum[$i] = [Math]::Sin(2 * [Math]::PI * 50 * $t) + 0.5 * [Math]::Sin(2 * [Math]::PI * 100 * $t) + 0.3 * [Math]::Sin(2 * [Math]::PI * 150 * $t) +
        0.12 * [Math]::Sin(2 * [Math]::PI * 350 * $t) * (1 + 0.4 * [Math]::Sin(2 * [Math]::PI * 2 * $t))
}
Save-Wav "$modDir\sounds\AMHUM.wav" $hum 0.4

# The hall's hush: soft air and a faint 100 Hz from the lights, five seconds
$len = 5
$s = New-Samples ($len + $ov)
$lp1 = 0.0
for ($i = 0; $i -lt $s.Length; $i++) {
    $t = $i / $sr
    $lp1 += 0.08 * ((Rnd) - $lp1)
    $s[$i] = 0.5 * $lp1 + 0.04 * [Math]::Sin(2 * [Math]::PI * 100 * $t)
}
Save-Wav "$modDir\sounds\AMTONE.wav" (Close-Loop $s $len $ov) 0.25

# The guard post, unseen: a low drone, a clock ticking and a heartbeat, four seconds
$s = New-Samples 4
for ($i = 0; $i -lt $s.Length; $i++) {
    $t = $i / $sr
    $s[$i] = 0.25 * [Math]::Sin(2 * [Math]::PI * 55 * $t) + 0.1 * [Math]::Sin(2 * [Math]::PI * 82.5 * $t)
}
for ($k = 0; $k -lt 8; $k++) {
    Add-Noise $s $random (0.5 * $k) 0.6 0.6 0.004 0.02
    Add-Tone $s (0.5 * $k) 2800 0.15 0.008 0.03
}
for ($k = 0; $k -lt 4; $k++) {
    foreach ($beat in 0.0, 0.28) {
        Add-Tone $s ($k + $beat) { param($t) 45 + 30 * [Math]::Exp(-$t / 0.03) } 0.9 0.07 0.25
    }
}
Save-Wav "$modDir\sounds\AMTENSE.wav" $s 0.5

# The alarm (the guard post, once you're seen): a two-tone klaxon, two seconds
$s = New-Samples 2
foreach ($part in @(@(0, 660), @(1, 520))) {
    foreach ($h in 1, 3, 5) { Add-Tone $s $part[0] ($part[1] * $h) (0.4 / $h) 0 1 }
}
Save-Wav "$modDir\sounds\AMALARM.wav" $s 0.45

#
# PLACED SOUNDS AND ONE-SHOTS
#

# Drips: a falling ping and a little splash, two of them (sound-seq picks one, and varies them)
$drips = @(@(900, 1300, 0.02), @(700, 1000, 0.025))
for ($k = 0; $k -lt 2; $k++) {
    $d = $drips[$k]
    $s = New-Samples 0.35
    Add-Tone $s 0 ([scriptblock]::Create("param(`$t) $($d[0]) + $($d[1]) * [Math]::Exp(-`$t / $($d[2]))")) 0.9 0.06 0.3
    Add-Noise $s $random 0.01 0.15 0.5 0.02 0.08
    Save-Wav "$modDir\sounds\AMDRIP$($k + 1).wav" $s 0.6
}

# Birds: three songs - a run of rising chirps, a trill and a two-note whistle
$s = New-Samples 0.5
foreach ($at in 0, 0.13, 0.26) { Add-Tone $s $at { param($t) 2600 + 20000 * $t } 0.6 0.05 0.08 }
Save-Wav "$modDir\sounds\AMBIRD1.wav" $s 0.5
$s = New-Samples 0.5
for ($k = 0; $k -lt 8; $k++) { Add-Tone $s (0.05 * $k) { param($t) 3400 - 8000 * $t } 0.5 0.02 0.035 }
Save-Wav "$modDir\sounds\AMBIRD2.wav" $s 0.45
$s = New-Samples 0.6
Add-Tone $s 0 { param($t) 2100 + 40 * [Math]::Sin(2 * [Math]::PI * 30 * $t) } 0.5 0 0.2
Add-Tone $s 0.24 { param($t) 1700 + 40 * [Math]::Sin(2 * [Math]::PI * 30 * $t) } 0.5 0.2 0.3
Save-Wav "$modDir\sounds\AMBIRD3.wav" $s 0.45

# Thunder far off: a crack, then a long rolling rumble, three and a half seconds
$s = New-Samples 3.5
$lp1 = 0.0
for ($i = 0; $i -lt $s.Length; $i++) {
    $t = $i / $sr
    $lp1 += 0.01 * ((Rnd) - $lp1)
    $env = (1 - [Math]::Exp(-$t / 0.15)) * [Math]::Exp(-$t / 1.1) * (1 + 0.5 * [Math]::Sin(2 * [Math]::PI * 1.3 * $t))
    $s[$i] = 12 * $lp1 * $env
}
Add-Noise $s $random 0.05 0.5 0.4 0.08 0.4
Save-Wav "$modDir\sounds\AMTHUND.wav" $s 0.6

# A generator's clank: a struck metal panel (out-of-tune partials) and its knock
$s = New-Samples 0.5
foreach ($p in @(@(410, 0.12), @(977, 0.08), @(1543, 0.06), @(2210, 0.04))) { Add-Tone $s 0 $p[0] 0.4 $p[1] 0.5 }
Add-Noise $s $random 0 0.6 0.4 0.01 0.05
Save-Wav "$modDir\sounds\AMCLANK.wav" $s 0.55

# The generators winding down, and up again
$s = New-Samples 1.5
Add-Tone $s 0 { param($t) 50 + 70 * (1 - $t / 1.5) } 1 0.6 1.5
Add-Tone $s 0 { param($t) 100 + 140 * (1 - $t / 1.5) } 0.4 0.5 1.5
Save-Wav "$modDir\sounds\AMPOWDN.wav" $s 0.5
$s = New-Samples 1.2
for ($i = 0; $i -lt $s.Length; $i++) {
    $t = $i / $sr
    $f = 20 + 30 * $t / 1.2
    $s[$i] = ($t / 1.2) * ([Math]::Sin(2 * [Math]::PI * $f * $t) + 0.5 * [Math]::Sin(4 * [Math]::PI * $f * $t))
}
Save-Wav "$modDir\sounds\AMPOWUP.wav" $s 0.45

# The radio: two stations' tunes, four seconds each (the radio plays one again every 280 tics),
# thin and crackly; and the click of its knob
function Add-RadioNote([double[]]$S, [double]$At, [double]$Midi, [double]$Amp, [double]$Length, [bool]$Square) {
    $f = Hz $Midi
    $start = [int]($At * $sr); $n = [int]($Length * $sr)
    for ($i = 0; $i -lt $n -and $start + $i -lt $S.Length; $i++) {
        $t = $i / $sr
        $env = [Math]::Min(1, $t / 0.005) * [Math]::Min(1, ($n - $i) / 300.0) * [Math]::Exp(-$t / 0.5)
        $w = if ($Square) { [Math]::Sin(2 * [Math]::PI * $f * $t) + [Math]::Sin(6 * [Math]::PI * $f * $t) / 3 + [Math]::Sin(10 * [Math]::PI * $f * $t) / 5 }
             else { [Math]::Sin(2 * [Math]::PI * $f * $t) * (1 + 0.3 * [Math]::Sin(2 * [Math]::PI * 6 * $t)) }
        $S[$start + $i] += $Amp * $env * $w
    }
}
function Add-Crackle([double[]]$S) {
    for ($i = 0; $i -lt $S.Length; $i++) {
        $S[$i] += 0.02 * (Rnd)
        if ($random.NextDouble() -lt 0.0004) { $S[$i] += 0.4 * (Rnd) }
    }
}
$s = New-Samples 4
$melody = @(72, 74, 76, 79, 76, 74, 72, 67, 69, 72, 74, 72, 69, 67, 64, 67)
for ($k = 0; $k -lt 15; $k++) { Add-RadioNote $s (0.25 * $k) $melody[$k] 0.3 0.24 $true }
Add-RadioNote $s 3.75 $melody[15] 0.3 0.2 $true
$bass = @(48, 55, 53, 55, 45, 52, 43, 48)
for ($k = 0; $k -lt 8; $k++) { Add-RadioNote $s (0.5 * $k) $bass[$k] 0.25 0.45 $false }
Add-Crackle $s
Save-Wav "$modDir\sounds\AMRAD1.wav" $s 0.5
$s = New-Samples 4
$melody = @(64, 67, 71, 72, 71, 67, 64, 62)
for ($k = 0; $k -lt 8; $k++) { Add-RadioNote $s (0.5 * $k) $melody[$k] 0.35 0.46 $false }
foreach ($k in 0, 2, 4, 6) { Add-RadioNote $s (0.5 * $k) 40 0.2 0.9 $false }
Add-Crackle $s
Save-Wav "$modDir\sounds\AMRAD2.wav" $s 0.5
$s = New-Samples 0.08
Add-Noise $s $random 0 0.8 0.7 0.008 0.03
Add-Tone $s 0 1100 0.3 0.01 0.04
Save-Wav "$modDir\sounds\AMCLICK.wav" $s 0.5

#
# LOOPS PLACED ON THINGS (actordefs loopsound)
#
# (After the rest, so the sounds above keep the random numbers they had)

# A generator's drone: a 45 Hz motor and its harmonics chugging three times a second, a whine
# wavering over it and a little rush of air, two seconds (the tones a whole number of cycles)
$len = 2; $ov = 0.5
$s = New-Samples ($len + $ov)
$lp1 = 0.0
for ($i = 0; $i -lt $s.Length; $i++) {
    $t = $i / $sr
    $lp1 += 0.05 * ((Rnd) - $lp1)
    $chug = 1 + 0.3 * [Math]::Sin(2 * [Math]::PI * 3 * $t)
    $s[$i] = $chug * ([Math]::Sin(2 * [Math]::PI * 45 * $t) + 0.6 * [Math]::Sin(2 * [Math]::PI * 90 * $t) + 0.3 * [Math]::Sin(2 * [Math]::PI * 135 * $t)) +
        0.15 * [Math]::Sin(2 * [Math]::PI * 720 * $t) * (1 + 0.6 * [Math]::Sin(2 * [Math]::PI * 4 * $t)) + 0.6 * $lp1
}
Save-Wav "$modDir\sounds\AMGEN.wav" (Close-Loop $s $len $ov) 0.5

# Water pouring from a pipe: a rushing band of noise, swelling a little, with bubbles, four seconds
$len = 4; $ov = 1
$s = New-Samples ($len + $ov)
$lp1 = 0.0; $lp2 = 0.0
for ($i = 0; $i -lt $s.Length; $i++) {
    $t = $i / $sr
    $n = Rnd
    $lp1 += 0.35 * ($n - $lp1)
    $lp2 += 0.04 * ($n - $lp2)
    $swell = 0.8 + 0.2 * [Math]::Sin(2 * [Math]::PI * $t / 1.7)
    $s[$i] = $swell * ($lp1 - $lp2 + 1.5 * $lp2)
}
for ($k = 0; $k -lt 40; $k++) {
    $at = [int]($random.NextDouble() * ($s.Length - $sr * 0.05)); $f0 = 300 + 500 * $random.NextDouble()
    for ($i = 0; $i -lt [int]($sr * 0.04); $i++) {
        $t = $i / $sr
        $s[$at + $i] += 0.25 * [Math]::Exp(-$t / 0.012) * [Math]::Sin(2 * [Math]::PI * ($f0 + 4000 * $t) * $t)
    }
}
Save-Wav "$modDir\sounds\AMWATER.wav" (Close-Loop $s $len $ov) 0.5

#
# A SECOND LOOP OVER A ZONE'S FIRST (game-info ambient-2)
#

# Light rain (over the courtyard's wind): a soft high hiss and drops ticking, five seconds
$len = 5; $ov = 1
$s = New-Samples ($len + $ov)
$lp1 = 0.0
for ($i = 0; $i -lt $s.Length; $i++) {
    $n = Rnd
    $lp1 += 0.5 * ($n - $lp1)
    $s[$i] = 0.25 * ($n - $lp1)
}
for ($k = 0; $k -lt 900; $k++) {
    $at = [int]($random.NextDouble() * ($s.Length - $sr * 0.01)); $amp = 0.1 + 0.5 * $random.NextDouble()
    for ($i = 0; $i -lt [int]($sr * 0.008); $i++) {
        $s[$at + $i] += $amp * [Math]::Exp(-$i / ($sr * 0.0015)) * (Rnd)
    }
}
Save-Wav "$modDir\sounds\AMRAIN.wav" (Close-Loop $s $len $ov) 0.35

#
# SPRITES
#

# A generator: a grey cabinet with a dial and a light, A and B blinking, C switched off
foreach ($frame in 'A', 'B', 'C') {
    Save-Drawing "$modDir\sprites\AMGN${frame}0.png" 64 64 {
        param($g)
        Add-Rect $g @(90, 96, 100) 14 24 36 40
        Add-Rect $g @(60, 64, 70) 14 24 36 3
        foreach ($k in 0..3) { Add-Rect $g @(50, 54, 58) 18 (34 + 6 * $k) 28 2 }
        Add-Ellipse $g $(if ($frame -eq 'C') { @(90, 90, 84) } else { @(210, 210, 200) }) 34 28 10 10
        Add-Ellipse $g $(if ($frame -eq 'A') { @(80, 255, 80) } elseif ($frame -eq 'B') { @(20, 80, 20) } else { @(40, 40, 40) }) 18 28 6 6
    }
}

# The radio: a wooden set with a speaker grille, a dial (lit while it plays: A; dark: B) and an aerial
foreach ($frame in 'A', 'B') {
    Save-Drawing "$modDir\sprites\AMRD${frame}0.png" 64 64 {
        param($g)
        Add-Rect $g @(150, 150, 150) 40 30 2 14
        Add-Rect $g @(120, 70, 30) 18 44 28 20
        Add-Rect $g @(80, 44, 18) 18 44 28 2
        foreach ($k in 0..3) { Add-Rect $g @(60, 34, 14) 21 (48 + 4 * $k) 12 2 }
        Add-Ellipse $g $(if ($frame -eq 'A') { @(255, 220, 90) } else { @(70, 60, 40) }) 36 48 7 7
        Add-Rect $g @(40, 40, 40) 37 58 5 3
    }
}

#
# THE LEVEL
#

# A hall running north from the start, rooms either side, each its own zone (plane 5) and tag
# (plane 4, which the script tells the rooms apart by):
#   west: the courtyard (zone 1), the generator room (zone 3), the radio room (zone 5)
#   east: the sewer (zone 2), the guard post (zone 4), the elevator (no zone: what was playing goes on)
#   the hall: zone 6. Doors and the doorways are zone 0, so a sound carries through them.
$Layout = @(
    '#########################################'   # y=10
    '#LLLLLLLLLLLLLLLL#hhhhh#BBBBBBBBBBBBBBBB#'
    '#LccccccccccccccL#hhhhh#BssssssssssssssB#'
    '#LcctccccccccuccL#hhlhh#BssdsssspssssssB#'
    '#LccccccccccccccL#hhhhh#BsssssssssssdssB#'
    '#LccccccccccccccL#hhhhh#BsssspsssssssssB#'   # y=15
    '#LcccccccwccccccvEhhhhhEysssssssssssssfB#'
    '#LccccccccccccccL#hhhhh#BsssssssssspsssB#'
    '#LccccccccccccccL#hhhhh#BssssssssssssssB#'
    '#LccuccccccccctcL#hhlhh#BsssssdssssssdsB#'
    '#LccccccccccccccL#hhhhh#BssssssssssssssB#'   # y=20
    '#LLLLLLLLLLLLLLLL#hhhhh#BBBBBBBBBBBBBBBB#'
    '#SSSSSSSSSSSSSSSS#hhhhh#RRRRRRRRRRRRRRRR#'
    '#SggggggggggggggS#hhhhh#RqqqqqTqqqqqqqqR#'
    '#SggggggggggggggS#hhhhh#RqqqqqqqqkqqqqqR#'
    '#SggggGgggggggggS#hhlhh#RqqqqqqqqqqqqqqR#'
    '#SggggggggggggggjEhhhhhEmqqqqqqiqqqqqqqR#'
    '#SggggggggggggggS#hhhhh#RqqqqqqqqqqqqqqR#'
    '#SgggggggggGggggS#hhhhh#RqqqqqqqqqqqkqqR#'
    '#SggggggggggggggS#hhhhh#RqqqqqTqqqqqqqqR#'
    '#SggggggggggggggS#hhhhh#RqqqqqqqqqqqqqqR#'   # y=30
    '#SSSSSSSSSSSSSSSS#hhhhh#RRRRRRRRRRRRRRRR#'
    '#WWWWWWWWWWWWWWWW#hhlhh##################'
    '#WrrrrrrrrrrrrrrW#hhhhh##eeeeeeeeeeeeee##'
    '#WrrOrrrrrrrrrrrW#hhhhh##eeeeeeeeeeeeee##'
    '#WrrrrrrrrrrrrrrzEhhhhhEneeeeeeeeeeeeeeX#'   # y=35
    '#WrrrrrrrrrrrrrrW#hhhhh##eeeeeeeeeeeeee##'
    '#WrrrrrrrrrrrrrrW#hhhhh##eeeeeeeeeeeeee##'
    '#WrrrrrrrrrrrrrrW#hhlhh##eeeeeeeeeeeeee##'
    '#WWWWWWWWWWWWWWWW#hhhhh##################'
    '##################hhhhh##################'   # y=40
    '##################hhhhh##################'
    '##################hhhhh##################'
    '##################hhPhh##################'   #      the player start, facing north
    '#########################################'
)
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 1 }                                     # grey stone
# the hall (zone 6), its ceiling lights, the start
$Tiles['h'] = @{ Plane0 = 108; Zone = 6 }
$Tiles['l'] = @{ Plane0 = 108; Zone = 6; Plane1 = 37 }
$Tiles['P'] = @{ Plane0 = 108; Zone = 6; Plane1 = 19 }
# the doors (zone 0)
$Tiles['E'] = @{ Plane0 = 90 }
# the courtyard (zone 1, tag 1): the landscape on its walls, its trees (tag 11: the birds sing from them) and well
$Tiles['L'] = @{ Plane0 = 16 }
$Tiles['c'] = @{ Plane0 = 109; Zone = 1; Tag = 1 }
$Tiles['t'] = @{ Plane0 = 109; Zone = 1; Tag = 11; Plane1 = 31 }
$Tiles['u'] = @{ Plane0 = 109; Zone = 1; Tag = 11; Plane1 = 34 }
$Tiles['w'] = @{ Plane0 = 109; Zone = 1; Tag = 1; Plane1 = 59 }
$Tiles['v'] = @{ Plane0 = 109 }                                   # its doorway (zone 0)
# the sewer (zone 2, tag 2): blue stone, the drips (object 990), puddles, and water pouring out at the far end
$Tiles['B'] = @{ Plane0 = 8 }
$Tiles['s'] = @{ Plane0 = 110; Zone = 2; Tag = 2 }
$Tiles['d'] = @{ Plane0 = 110; Zone = 2; Tag = 2; Plane1 = 990 }
$Tiles['p'] = @{ Plane0 = 110; Zone = 2; Tag = 2; Plane1 = 23 }
$Tiles['f'] = @{ Plane0 = 110; Zone = 2; Tag = 2; Plane1 = 993 }   # the water pouring out (object 993)
$Tiles['y'] = @{ Plane0 = 110 }
# the generator room (zone 3, tag 3): steel, the generators (object 991)
$Tiles['S'] = @{ Plane0 = 15 }
$Tiles['g'] = @{ Plane0 = 111; Zone = 3; Tag = 3 }
$Tiles['G'] = @{ Plane0 = 111; Zone = 3; Tag = 3; Plane1 = 991 }
$Tiles['j'] = @{ Plane0 = 111 }
# the guard post (zone 4, tag 4): red brick, two guards standing with their backs to the door
# (object 108: facing east), tables and a light
$Tiles['R'] = @{ Plane0 = 17 }
$Tiles['q'] = @{ Plane0 = 112; Zone = 4; Tag = 4 }
$Tiles['k'] = @{ Plane0 = 112; Zone = 4; Tag = 4; Plane1 = 108 }
$Tiles['T'] = @{ Plane0 = 112; Zone = 4; Tag = 4; Plane1 = 36 }
$Tiles['i'] = @{ Plane0 = 112; Zone = 4; Tag = 4; Plane1 = 37 }
$Tiles['m'] = @{ Plane0 = 112 }
# the radio room (zone 5, tag 5): wood, the radio (object 992)
$Tiles['W'] = @{ Plane0 = 12 }
$Tiles['r'] = @{ Plane0 = 113; Zone = 5; Tag = 5 }
$Tiles['O'] = @{ Plane0 = 113; Zone = 5; Tag = 5; Plane1 = 992 }
$Tiles['z'] = @{ Plane0 = 113 }
# the elevator room (no zone, tag 6) and its switch
$Tiles['e'] = @{ Plane0 = 114; Tag = 6 }
$Tiles['n'] = @{ Plane0 = 114 }
$Tiles['X'] = @{ Plane0 = 21 }
$planes = New-MapPlanes -Fill 1
Add-MapLayout $planes $Layout $Tiles 10 10
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Ambience Demo' $planes
