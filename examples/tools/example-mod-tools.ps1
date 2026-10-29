# Shared helpers for the example mods' generator scripts (examples/tools/make-*.ps1): writing
# levels, pictures and sounds. A script dot-sources it:
#
#   . (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
#
# The older generators each carry their own copy of the level writer; these are the same steps.

Add-Type -AssemblyName System.Drawing

# A mod's folder (examples/mods/<id>), with any subfolders it needs made
function Get-ModDir([string]$Id, [string[]]$Subfolders = @()) {
    $dir = Join-Path $PSScriptRoot "..\mods\$Id"
    New-Item -ItemType Directory -Force $dir | Out-Null
    foreach ($sub in $Subfolders) { New-Item -ItemType Directory -Force (Join-Path $dir $sub) | Out-Null }
    (Resolve-Path $dir).Path
}

#
# PICTURES
#

# Draws a picture pixel by pixel and saves it as a PNG: $Paint gets x and y and returns a
# [Drawing.Color], or $null for a clear (see-through) pixel. Colors snap to the nearest palette
# color as the game loads them.
function Save-Picture([string]$Path, [int]$Width, [int]$Height, [scriptblock]$Paint) {
    $bitmap = New-Object Drawing.Bitmap $Width, $Height
    for ($y = 0; $y -lt $Height; $y++) {
        for ($x = 0; $x -lt $Width; $x++) {
            $color = & $Paint $x $y
            $bitmap.SetPixel($x, $y, $(if ($null -eq $color) { [Drawing.Color]::Transparent } else { $color }))
        }
    }
    New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
    $bitmap.Save($Path, [Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    "Wrote $Path"
}

# Saves a picture drawn as text, one character per pixel: '.' is clear, and $Colors maps each
# other character to @(r, g, b). Rows shorter than the widest are clear past their end. With
# $Scale, each character is that many pixels square.
function Save-CharPicture([string]$Path, [string[]]$Rows, [hashtable]$Colors, [int]$Scale = 1) {
    $width = ($Rows | ForEach-Object Length | Measure-Object -Maximum).Maximum
    Save-Picture $Path ($width * $Scale) ($Rows.Count * $Scale) {
        param($x, $y)
        $row = $Rows[[int][Math]::Floor($y / $Scale)]
        $col = [int][Math]::Floor($x / $Scale)
        if ($col -ge $row.Length) { return $null }
        $ch = [string]$row[$col]
        if ($ch -eq '.') { return $null }
        if (!$Colors.ContainsKey($ch)) { throw "No color for '$ch' in $Path" }
        $c = $Colors[$ch]
        [Drawing.Color]::FromArgb(255, $c[0], $c[1], $c[2])
    }
}

# A color from r, g, b, each kept within 0-255
function New-Color([double]$R, [double]$G, [double]$B) {
    [Drawing.Color]::FromArgb(255, [int][Math]::Max(0, [Math]::Min(255, $R)), [int][Math]::Max(0, [Math]::Min(255, $G)),
        [int][Math]::Max(0, [Math]::Min(255, $B)))
}

#
# SOUNDS
#

$script:SampleRate = 22050

# Writes samples (about -1 to 1) as a 16-bit mono WAV, scaled so its loudest is $Peak
function Save-Wav([string]$Path, [double[]]$Samples, [double]$Peak = 0.6) {
    $loudest = 0.0
    foreach ($s in $Samples) { if ([Math]::Abs($s) -gt $loudest) { $loudest = [Math]::Abs($s) } }
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
    $w.Write([int]$script:SampleRate)
    $w.Write([int]($script:SampleRate * 2))
    $w.Write([uint16]2)
    $w.Write([uint16]16)
    $w.Write([Text.Encoding]::ASCII.GetBytes('data'))
    $w.Write([int]$data.Length)
    $w.Write($data)
    $w.Flush()
    New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
    [IO.File]::WriteAllBytes($Path, $ms.ToArray())
    "Wrote $Path"
}

# An empty sound $Seconds long, to add into
function New-Samples([double]$Seconds) { , (New-Object double[] ([int]($Seconds * $script:SampleRate))) }

# Adds a tone into the samples from $At seconds: $Hz (a scriptblock of t gives a sweep), dying
# away over $Decay seconds (0: steady), for $Length seconds (0: to the end)
function Add-Tone([double[]]$Samples, [double]$At, $Hz, [double]$Amp, [double]$Decay = 0, [double]$Length = 0) {
    $start = [int]($At * $script:SampleRate)
    $end = if ($Length -gt 0) { [Math]::Min($Samples.Length, $start + [int]($Length * $script:SampleRate)) } else { $Samples.Length }
    $phase = 0.0
    for ($i = $start; $i -lt $end; $i++) {
        $t = ($i - $start) / $script:SampleRate
        $f = if ($Hz -is [scriptblock]) { & $Hz $t } else { $Hz }
        $phase += 2 * [Math]::PI * $f / $script:SampleRate
        $env = if ($Decay -gt 0) { [Math]::Exp(-$t / $Decay) } else { 1 }
        # a short fade in and out, so it doesn't click
        $edge = [Math]::Min(1, [Math]::Min(($i - $start) / 60.0, ($end - $i) / 60.0))
        $Samples[$i] += $Amp * [Math]::Sin($phase) * $env * $edge
    }
}

# Adds noise into the samples from $At seconds: through a one-pole low-pass ($Smooth, 0-1:
# lower is duller), dying away over $Decay seconds (0: steady), for $Length seconds (0: to the end)
function Add-Noise([double[]]$Samples, [Random]$Random, [double]$At, [double]$Amp, [double]$Smooth, [double]$Decay = 0, [double]$Length = 0) {
    $start = [int]($At * $script:SampleRate)
    $end = if ($Length -gt 0) { [Math]::Min($Samples.Length, $start + [int]($Length * $script:SampleRate)) } else { $Samples.Length }
    $filtered = 0.0
    for ($i = $start; $i -lt $end; $i++) {
        $t = ($i - $start) / $script:SampleRate
        $filtered += $Smooth * ($Random.NextDouble() * 2 - 1 - $filtered)
        $env = if ($Decay -gt 0) { [Math]::Exp(-$t / $Decay) } else { 1 }
        $edge = [Math]::Min(1, [Math]::Min(($i - $start) / 60.0, ($end - $i) / 60.0))
        $Samples[$i] += $Amp * $filtered * $env * $edge
    }
}

#
# LEVELS
#

# A level's planes: $Count planes of $Size x $Size, plane 0 filled with $Fill (a wall). PFWolf's
# six planes are walls/floors, objects, flats, wall heights, tags and light zones.
function New-MapPlanes([int]$Size = 64, [int]$Fill = 1, [int]$Count = 6) {
    $planes = @()
    for ($p = 0; $p -lt $Count; $p++) { $planes += , (New-Object 'uint16[]' ($Size * $Size)) }
    for ($i = 0; $i -lt $Size * $Size; $i++) { $planes[0][$i] = $Fill }
    , $planes
}

# Lays an ASCII layout onto the planes, one character per tile, its top-left corner at tile
# ($Left, $Top). $Tiles maps each character to a hashtable of what goes on each plane: Plane0
# (wall, door or floor code), Plane1 (object), Flat (plane 2: floor index + ceiling index * 256),
# Height (plane 3), Tag (plane 4), Zone (plane 5). A key left out leaves that plane as it is.
function Add-MapLayout($Planes, [string[]]$Layout, [hashtable]$Tiles, [int]$Left, [int]$Top, [int]$Size = 64) {
    if (($Layout | ForEach-Object Length | Sort-Object -Unique).Count -ne 1) { throw 'The layout rows are different lengths' }
    $keys = @{ Plane0 = 0; Plane1 = 1; Flat = 2; Height = 3; Tag = 4; Zone = 5 }
    for ($row = 0; $row -lt $Layout.Count; $row++) {
        $line = $Layout[$row]
        for ($col = 0; $col -lt $line.Length; $col++) {
            $ch = [string]$line[$col]
            if (!$Tiles.ContainsKey($ch)) { throw "Unknown layout character '$ch' at row $row, column $col" }
            $tile = $Tiles[$ch]
            $i = ($Top + $row) * $Size + $Left + $col
            foreach ($key in $keys.Keys) {
                if ($tile.ContainsKey($key) -and $null -ne $tile[$key]) { $Planes[$keys[$key]][$i] = [uint16]$tile[$key] }
            }
        }
    }
}

# A layout character's tiles, for a script to change after Add-MapLayout (e.g. a whole river's
# tag): each @{ X; Y; Index }
function Find-LayoutTiles([string[]]$Layout, [string]$Char, [int]$Left, [int]$Top, [int]$Size = 64) {
    for ($row = 0; $row -lt $Layout.Count; $row++) {
        for ($col = 0; $col -lt $Layout[$row].Length; $col++) {
            if ([string]$Layout[$row][$col] -ceq $Char) {
                @{ X = $Left + $col; Y = $Top + $row; Index = ($Top + $row) * $Size + $Left + $col }
            }
        }
    }
}

# Writes the planes as an ECWolf binary map: a PWAD holding the map's marker lump ($MapName,
# MAP01) and a WDC3.1 PLANES lump with the level's $Title
function Save-MapWad([string]$Path, [string]$MapName, [string]$Title, $Planes, [int]$Size = 64) {
    $ms = New-Object IO.MemoryStream
    $bw = New-Object IO.BinaryWriter $ms
    $bw.Write([Text.Encoding]::ASCII.GetBytes('WDC3.1'))
    $bw.Write([int]1)
    $bw.Write([uint16]$Planes.Count)
    $bw.Write([uint16]16)
    $name = New-Object byte[] 16
    $text = if ($Title.Length -gt 16) { $Title.Substring(0, 16) } else { $Title }
    [Text.Encoding]::ASCII.GetBytes($text, 0, $text.Length, $name, 0) | Out-Null
    $bw.Write($name)
    $bw.Write([uint16]$Size)
    $bw.Write([uint16]$Size)
    foreach ($plane in $Planes) { foreach ($value in $plane) { $bw.Write([uint16]$value) } }
    $bw.Flush()
    $planesLump = $ms.ToArray()

    $out = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter $out
    $w.Write([Text.Encoding]::ASCII.GetBytes('PWAD'))
    $w.Write([int]2)
    $w.Write([int](12 + $planesLump.Length))
    $w.Write($planesLump)
    foreach ($lump in @(@(12, 0, $MapName), @(12, $planesLump.Length, 'PLANES'))) {
        $w.Write([int]$lump[0])
        $w.Write([int]$lump[1])
        $lumpName = New-Object byte[] 8
        [Text.Encoding]::ASCII.GetBytes($lump[2], 0, $lump[2].Length, $lumpName, 0) | Out-Null
        $w.Write($lumpName)
    }
    $w.Flush()
    New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
    [IO.File]::WriteAllBytes($Path, $out.ToArray())
    "Wrote $Path ($($out.Length) bytes)"
}
