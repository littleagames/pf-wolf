# Builds examples/mods/music-demo: its two songs (music/DMCALM.wav, music/DMFIGHT.wav), the
# generators' hum (sounds/DMHUM.wav), the generator's sprite (sprites/DMGN*.png) and the level
# (maps/MAP01.wad). Run it after changing any of them (the songs take a little while):
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-music-demo.ps1
#
# The songs are written as notes and synthesized here: each loops seamlessly, being a whole
# number of bars with every note dying away inside it.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'music-demo'
$sr = $script:SampleRate

function Hz([double]$Midi) { 440 * [Math]::Pow(2, ($Midi - 69) / 12) }

# Adds a note: a tone with a soft second and third harmonic, fading in quickly and dying away over
# $Decay seconds, cut off at $Length (wrapping round the end of the loop, so the loop is seamless)
function Add-Note([double[]]$S, [double]$At, [double]$Midi, [double]$Amp, [double]$Decay, [double]$Length) {
    $f = Hz $Midi
    $start = [int]($At * $sr); $n = [int]($Length * $sr)
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sr
        $env = [Math]::Min(1, $t / 0.01) * [Math]::Exp(-$t / $Decay) * [Math]::Min(1, ($n - $i) / 200.0)
        $w = [Math]::Sin(2 * [Math]::PI * $f * $t) + 0.3 * [Math]::Sin(4 * [Math]::PI * $f * $t) + 0.12 * [Math]::Sin(6 * [Math]::PI * $f * $t)
        $S[($start + $i) % $S.Length] += $Amp * $env * $w
    }
}

# The calm song: four chords, four seconds each, an arpeggio over a held pad, at a walk
$calm = New-Samples 16
$chords = @(@(57, 60, 64), @(53, 57, 60), @(48, 52, 55), @(55, 59, 62))      # Am, F, C, G
for ($c = 0; $c -lt 4; $c++) {
    $chord = $chords[$c]
    foreach ($note in $chord) { Add-Note $calm (4 * $c) ($note - 12) 0.12 3.5 4 }   # the pad, an octave down
    $pattern = @($chord[0], $chord[1], $chord[2], $chord[0] + 12, $chord[2], $chord[1], $chord[2], $chord[0] + 12)
    for ($k = 0; $k -lt 8; $k++) { Add-Note $calm (4 * $c + 0.5 * $k) $pattern[$k] 0.22 0.6 0.5 }
}
Save-Wav "$modDir\music\DMCALM.wav" $calm 0.5

# The fight song: a driving bass in eighths, kick on the beat, snare on two and four, hi-hats;
# 140 beats a minute, four bars of 4
$beat = 60 / 140
$fight = New-Samples (16 * $beat)
$random = New-Object Random 13
$bass = @(40, 40, 43, 40, 45, 43, 40, 38)
for ($b = 0; $b -lt 16; $b++) {
    $at = $b * $beat
    # kick: a falling thump
    $s0 = [int]($at * $sr)
    for ($i = 0; $i -lt [int](0.15 * $sr); $i++) {
        $t = $i / $sr
        $fight[($s0 + $i) % $fight.Length] += 0.9 * [Math]::Sin(2 * [Math]::PI * (40 + 90 * [Math]::Exp(-$t / 0.03)) * $t) * [Math]::Exp(-$t / 0.08)
    }
    # snare on two and four
    if ($b % 2 -eq 1) {
        for ($i = 0; $i -lt [int](0.12 * $sr); $i++) {
            $t = $i / $sr
            $fight[($s0 + $i) % $fight.Length] += 0.45 * ($random.NextDouble() * 2 - 1) * [Math]::Exp(-$t / 0.04)
        }
    }
    # bass and hats in eighths
    foreach ($half in 0, 1) {
        $e = $at + $half * $beat / 2
        Add-Note $fight $e $bass[(2 * $b + $half) % 8] 0.35 0.18 ($beat / 2)
        $h0 = [int]($e * $sr); $prev = 0.0
        for ($i = 0; $i -lt [int](0.03 * $sr); $i++) {
            $n = $random.NextDouble() * 2 - 1
            $fight[($h0 + $i) % $fight.Length] += 0.15 * ($n - $prev) * [Math]::Exp(-$i / ($sr * 0.008))
            $prev = $n
        }
    }
    # a stab of the chord on the first beat of each bar
    if ($b % 4 -eq 0) { foreach ($note in 64, 67, 71) { Add-Note $fight $at $note 0.12 0.3 ($beat) } }
}
Save-Wav "$modDir\music\DMFIGHT.wav" $fight 0.55

# The generators' hum: mains hum and its harmonics, two seconds, looping
$hum = New-Samples 2
for ($i = 0; $i -lt $hum.Length; $i++) {
    $t = $i / $sr
    $hum[$i] = [Math]::Sin(2 * [Math]::PI * 60 * $t) + 0.5 * [Math]::Sin(2 * [Math]::PI * 120 * $t) + 0.25 * [Math]::Sin(2 * [Math]::PI * 180 * $t) +
        0.1 * [Math]::Sin(2 * [Math]::PI * 300 * $t) * (1 + 0.3 * [Math]::Sin(2 * [Math]::PI * 2 * $t))
}
Save-Wav "$modDir\sounds\DMHUM.wav" $hum 0.35

# A generator: a grey cabinet with a dial and a light, A and B blinking
foreach ($frame in 'A', 'B') {
    Save-Drawing "$modDir\sprites\DMGN${frame}0.png" 64 64 {
        param($g)
        Add-Rect $g @(90, 96, 100) 14 24 36 40
        Add-Rect $g @(60, 64, 70) 14 24 36 3
        foreach ($k in 0..3) { Add-Rect $g @(50, 54, 58) 18 (34 + 6 * $k) 28 2 }
        Add-Ellipse $g @(210, 210, 200) 34 28 10 10
        Add-Ellipse $g $(if ($frame -eq 'A') { @(80, 255, 80) } else { @(20, 80, 20) }) 18 28 6 6
    }
}

# The level: the start hall south; the courtyard north of it, guards in it; the generator room
# west (its tiles have tag 1, where the script plays the hum); the barracks east; the exit north
$Layout = @(
    '###########xxxX###########'   # y=20 the exit
    '############D#############'
    '######cccccccccccccc#bbbb#'   #      the courtyard, the barracks
    '#gggg#cckccccccccccc#bnbb#'   #      the generator room (tag 1)
    '#gGgg#cccccccccccccc#bbbb#'
    '#ggggEccccccccccccccEnbbb#'   # y=25
    '#gGgg#cccccccccccccc#bbbb#'
    '#gggg#cccccccccccccc#bbnb#'
    '######ccccccccccckcc#bbbb#'
    '######cccccccccccccc#bbbb#'
    '############D#############'   # y=30
    '#########hhhhhhhh#########'   #      the start hall
    '#########hhhhhhhh#########'
    '#########hhhhhhhh#########'
    '#########hhhhhhhh#########'
    '#########hhhhhhhh#########'   # y=35
    '#########hhhPhhhh#########'   #      the player start, facing north
    '##########################'
)
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 8 }
$Tiles['h'] = @{ Plane0 = 108 }
$Tiles['P'] = @{ Plane0 = 108; Plane1 = 19 }
$Tiles['c'] = @{ Plane0 = 109 }
$Tiles['k'] = @{ Plane0 = 109; Plane1 = 111 }                 # a guard facing south
$Tiles['g'] = @{ Plane0 = 110; Tag = 1 }
$Tiles['G'] = @{ Plane0 = 110; Tag = 1; Plane1 = 930 }        # a generator
$Tiles['b'] = @{ Plane0 = 111 }
$Tiles['n'] = @{ Plane0 = 111; Plane1 = 110 }                 # a guard facing west
$Tiles['x'] = @{ Plane0 = 112 }
$Tiles['X'] = @{ Plane0 = 21 }
$Tiles['D'] = @{ Plane0 = 91 }
$Tiles['E'] = @{ Plane0 = 90 }
$planes = New-MapPlanes -Fill 8
Add-MapLayout $planes $Layout $Tiles 0 20
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Music Demo' $planes
