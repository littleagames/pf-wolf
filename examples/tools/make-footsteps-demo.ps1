# Builds examples/mods/footsteps-demo: its step sounds (sounds/*.wav), its two floors
# (flats/*.png) and its level (maps/MAP01.wad). Run it after changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-footsteps-demo.ps1
#
# The sounds are made from noise and sine waves, two of each kind (seeded, so the same every
# run): a hard step for stone, a crunching one for dirt and a ringing one for metal grating.
# The level is an ECWolf binary map (a WAD holding a MAP01 marker lump and a WDC3.1 PLANES lump)
# with PFWolf's six planes; a tile's floor is the low byte of its flat plane (plane 2) value,
# an index in the mod's mapdefs/flats.yaml.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$modDir = Join-Path $PSScriptRoot '..\mods\footsteps-demo'
$soundsDir = Join-Path $modDir 'sounds'
$flatsDir = Join-Path $modDir 'flats'
$mapsDir = Join-Path $modDir 'maps'
foreach ($dir in $soundsDir, $flatsDir, $mapsDir) { New-Item -ItemType Directory -Force $dir | Out-Null }

#
# SOUNDS
#

$Rate = 22050

# Writes samples (about -1 to 1) as a 16-bit mono WAV, scaled so its loudest is $Peak: steps
# sit well under the guns
function Save-Wav([string]$Name, [double[]]$Samples, [double]$Peak = 0.45) {
    $loudest = ($Samples | ForEach-Object { [Math]::Abs($_) } | Measure-Object -Maximum).Maximum
    $scale = if ($loudest -gt 0) { $Peak * 32767 / $loudest } else { 0 }
    $data = New-Object byte[] ($Samples.Length * 2)
    for ($i = 0; $i -lt $Samples.Length; $i++) {
        $value = [int][Math]::Round($Samples[$i] * $scale)
        $value = [Math]::Max(-32768, [Math]::Min(32767, $value))
        $data[2 * $i] = $value -band 0xFF
        $data[2 * $i + 1] = ($value -shr 8) -band 0xFF
    }
    $ms = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter $ms
    $w.Write([Text.Encoding]::ASCII.GetBytes('RIFF'))
    $w.Write([int](36 + $data.Length))
    $w.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt '))
    $w.Write([int]16)
    $w.Write([uint16]1)          # PCM
    $w.Write([uint16]1)          # mono
    $w.Write([int]$Rate)
    $w.Write([int]($Rate * 2))
    $w.Write([uint16]2)
    $w.Write([uint16]16)
    $w.Write([Text.Encoding]::ASCII.GetBytes('data'))
    $w.Write([int]$data.Length)
    $w.Write($data)
    $w.Flush()
    $path = Join-Path (Resolve-Path $soundsDir) "$Name.wav"
    [IO.File]::WriteAllBytes($path, $ms.ToArray())
    "Wrote $path"
}

# Adds an impact into the samples at $At seconds: noise through a one-pole low-pass ($Smooth,
# 0-1: lower is duller) dying away over $Decay seconds, and a low thump at $Thump Hz
function Add-Hit([double[]]$Samples, [Random]$Random, [double]$At, [double]$Amp, [double]$Smooth, [double]$Decay,
    [double]$Thump, [double]$ThumpDecay) {
    $start = [int]($At * $Rate)
    $filtered = 0.0
    for ($i = $start; $i -lt $Samples.Length; $i++) {
        $t = ($i - $start) / $Rate
        $filtered += $Smooth * ($Random.NextDouble() * 2 - 1 - $filtered)
        $thud = 0.6 * [Math]::Sin(2 * [Math]::PI * $Thump * $t) * [Math]::Exp(-$t / $ThumpDecay)
        $Samples[$i] += $Amp * ($filtered * [Math]::Exp(-$t / $Decay) + $thud)
    }
}

# A step on stone: the heel, then the toe a moment after
function New-StoneStep([int]$Seed, [double]$Pitch) {
    $random = New-Object Random $Seed
    $samples = New-Object double[] ([int](0.14 * $Rate))
    Add-Hit $samples $random 0.0 1.0 (0.30 * $Pitch) 0.012 (150 * $Pitch) 0.02
    Add-Hit $samples $random 0.045 0.55 (0.40 * $Pitch) 0.008 (190 * $Pitch) 0.012
    , $samples
}

# A step on dirt: a dull thump and a crunch of grit under it
function New-DirtStep([int]$Seed, [double]$Pitch) {
    $random = New-Object Random $Seed
    $samples = New-Object double[] ([int](0.2 * $Rate))
    Add-Hit $samples $random 0.0 0.8 (0.10 * $Pitch) 0.03 (90 * $Pitch) 0.03
    Add-Hit $samples $random 0.06 0.45 (0.12 * $Pitch) 0.02 (110 * $Pitch) 0.02
    # The grit: little clicks scattered over the step, louder at first
    for ($grain = 0; $grain -lt 70; $grain++) {
        $at = [int]([Math]::Pow($random.NextDouble(), 1.6) * 0.13 * $Rate)
        $amp = (0.15 + 0.35 * $random.NextDouble()) * (1 - $at / (0.16 * $Rate))
        $length = [int]((0.001 + 0.002 * $random.NextDouble()) * $Rate)
        for ($i = 0; $i -lt $length -and $at + $i -lt $samples.Length; $i++) {
            $samples[$at + $i] += $amp * ($random.NextDouble() * 2 - 1) * (1 - $i / $length)
        }
    }
    , $samples
}

# A step on metal grating: a sharp hit, the grating ringing, and it rattling as it settles
function New-MetalStep([int]$Seed, [double]$Pitch) {
    $random = New-Object Random $Seed
    $samples = New-Object double[] ([int](0.4 * $Rate))
    $partials = @(@(380, 0.50, 0.15), @(1020, 0.35, 0.09), @(1730, 0.25, 0.07), @(2650, 0.15, 0.05))
    foreach ($hit in @(@(0.0, 1.0), @(0.028, 0.45), @(0.075, 0.2))) {
        $start = [int]($hit[0] * $Rate)
        $previous = 0.0
        for ($i = $start; $i -lt $samples.Length; $i++) {
            $t = ($i - $start) / $Rate
            $noise = $random.NextDouble() * 2 - 1
            $value = 0.8 * ($noise - $previous) * [Math]::Exp(-$t / 0.004)     # a bright click
            $previous = $noise
            foreach ($p in $partials) {
                $value += $p[1] * [Math]::Sin(2 * [Math]::PI * $p[0] * $Pitch * $t) * [Math]::Exp(-$t / $p[2])
            }
            $value += 0.5 * [Math]::Sin(2 * [Math]::PI * 120 * $Pitch * $t) * [Math]::Exp(-$t / 0.02)
            $samples[$i] += $hit[1] * $value
        }
    }
    , $samples
}

Save-Wav 'FSSTEP1' (New-StoneStep 11 1.0)
Save-Wav 'FSSTEP2' (New-StoneStep 12 0.9)
Save-Wav 'FSDIRT1' (New-DirtStep 21 1.0)
Save-Wav 'FSDIRT2' (New-DirtStep 22 0.88)
Save-Wav 'FSMETAL1' (New-MetalStep 31 1.0) 0.35
Save-Wav 'FSMETAL2' (New-MetalStep 32 0.93) 0.35

#
# FLATS
#

# Draws a 64x64 floor pixel by pixel ($Paint gives each pixel's color) and saves it. Colors
# snap to the nearest palette color as the game loads them.
function Save-Flat([string]$Name, [scriptblock]$Paint) {
    $bitmap = New-Object Drawing.Bitmap 64, 64
    for ($y = 0; $y -lt 64; $y++) {
        for ($x = 0; $x -lt 64; $x++) { $bitmap.SetPixel($x, $y, (& $Paint $x $y)) }
    }
    $path = Join-Path (Resolve-Path $flatsDir) "$Name.png"
    $bitmap.Save($path, [Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    "Wrote $path"
}

# Packed dirt: brown, mottled, with pebbles
$dirtRandom = New-Object Random 41
$pebbles = @{}
for ($n = 0; $n -lt 40; $n++) {
    $px = $dirtRandom.Next(64); $py = $dirtRandom.Next(64); $shade = $dirtRandom.Next(2)
    foreach ($d in @(@(0, 0), @(1, 0), @(0, 1))) { $pebbles["$(($px + $d[0]) % 64),$(($py + $d[1]) % 64)"] = $shade }
}
Save-Flat 'FSDIRT' {
    param($x, $y)
    if ($pebbles.ContainsKey("$x,$y")) {
        if ($pebbles["$x,$y"] -eq 0) { return [Drawing.Color]::FromArgb(140, 120, 96) }
        return [Drawing.Color]::FromArgb(60, 44, 28)
    }
    $mottle = [int](12 * [Math]::Sin($x * 0.35 + $y * 0.2) * [Math]::Cos($y * 0.31 - $x * 0.12))
    $grain = $dirtRandom.Next(-14, 15)
    [Drawing.Color]::FromArgb([Math]::Max(0, 100 + $mottle + $grain), [Math]::Max(0, 72 + $mottle + $grain), [Math]::Max(0, 44 + $grain / 2))
}

# Metal grating: bars 4 pixels wide every 16, lit along their top and left edges, over a dark pit
Save-Flat 'FSGRATE' {
    param($x, $y)
    $bx = $x % 16; $by = $y % 16
    if ($bx -lt 4 -or $by -lt 4) {
        if ($bx -eq 0 -or $by -eq 0) { return [Drawing.Color]::FromArgb(176, 176, 184) }
        if ($bx -eq 3 -and $by -ge 4 -or $by -eq 3 -and $bx -ge 4) { return [Drawing.Color]::FromArgb(64, 64, 72) }
        return [Drawing.Color]::FromArgb(120, 120, 128)
    }
    [Drawing.Color]::FromArgb(20, 20, 24)
}

#
# THE LEVEL
#

# The layout, one character per tile, its top-left corner at tile ($Left, $Top). Every tile
# outside it is plain grey stone. West to east: stone (no flat: the floor color, and the
# default step), dirt, then metal grating, and the elevator switch at the far end.
$Left = 16
$Top = 28
$Layout = @(
    '##############################'   # y=28
    '#.........ddddddddggggggggg###'
    '#.........ddddddddggggggggg###'
    '#S........ddddddddgggggggggX##'   # y=31 the player start, facing east, and the elevator
    '#.........ddddddddggggggggg###'
    '#.........ddddddddggggggggg###'
    '##############################'
)
if (($Layout | ForEach-Object Length | Sort-Object -Unique).Count -ne 1) { throw 'The layout rows are different lengths' }

# What each character puts on plane 0 (a wall or floor code), plane 1 (objects) and the flat
# plane. Walls are pfwolf-pk3's mapdefs/wolf3d ids; floors are the mod's mapdefs/flats.yaml.
$Hall = 108
$Grey = 1
$Dirt = 10; $Grate = 11
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = $Grey }
$Tiles['.'] = @{ Plane0 = $Hall }
$Tiles['d'] = @{ Plane0 = $Hall; Flat = $Dirt }
$Tiles['g'] = @{ Plane0 = $Hall; Flat = $Grate }
$Tiles['S'] = @{ Plane0 = $Hall; Plane1 = 20 }                          # player start, facing east
$Tiles['X'] = @{ Plane0 = 21 }                                          # elevator switch: ends the level

$Size = 64
$PlaneCount = 6
$FlatPlane = 2
$planes = @()
for ($p = 0; $p -lt $PlaneCount; $p++) { $planes += , (New-Object 'uint16[]' ($Size * $Size)) }
for ($i = 0; $i -lt $Size * $Size; $i++) { $planes[0][$i] = $Grey }

for ($row = 0; $row -lt $Layout.Count; $row++) {
    $line = $Layout[$row]
    for ($col = 0; $col -lt $line.Length; $col++) {
        $ch = [string]$line[$col]
        if (!$Tiles.ContainsKey($ch)) { throw "Unknown layout character '$ch' at row $row, column $col" }
        $tile = $Tiles[$ch]
        $i = ($Top + $row) * $Size + $Left + $col
        $planes[0][$i] = $tile.Plane0
        if ($tile.Plane1) { $planes[1][$i] = $tile.Plane1 }
        if ($tile.Flat) { $planes[$FlatPlane][$i] = $tile.Flat }
    }
}

# PLANES: "WDC3.1", map count, plane count, name length, name, width, height, then the planes
$ms = New-Object IO.MemoryStream
$bw = New-Object IO.BinaryWriter $ms
$bw.Write([Text.Encoding]::ASCII.GetBytes('WDC3.1'))
$bw.Write([int]1)
$bw.Write([uint16]$PlaneCount)
$bw.Write([uint16]16)
$name = New-Object byte[] 16
$title = 'Footsteps Demo'
[Text.Encoding]::ASCII.GetBytes($title, 0, $title.Length, $name, 0) | Out-Null
$bw.Write($name)
$bw.Write([uint16]$Size)
$bw.Write([uint16]$Size)
foreach ($plane in $planes) { foreach ($value in $plane) { $bw.Write([uint16]$value) } }
$bw.Flush()
$planesLump = $ms.ToArray()

# A PWAD: header, the PLANES data, then the directory (the MAP01 marker, then PLANES)
$out = New-Object IO.MemoryStream
$w = New-Object IO.BinaryWriter $out
$w.Write([Text.Encoding]::ASCII.GetBytes('PWAD'))
$w.Write([int]2)
$w.Write([int](12 + $planesLump.Length))
$w.Write($planesLump)
foreach ($lump in @(@(12, 0, 'MAP01'), @(12, $planesLump.Length, 'PLANES'))) {
    $w.Write([int]$lump[0])
    $w.Write([int]$lump[1])
    $lumpName = New-Object byte[] 8
    [Text.Encoding]::ASCII.GetBytes($lump[2], 0, $lump[2].Length, $lumpName, 0) | Out-Null
    $w.Write($lumpName)
}
$w.Flush()

$path = Join-Path (Resolve-Path $mapsDir) 'MAP01.wad'
[IO.File]::WriteAllBytes($path, $out.ToArray())
"Wrote $path ($($out.Length) bytes)"
