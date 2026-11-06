# Builds examples/mods/transitions-demo: two panel textures for each fade style (textures/TD*.png),
# the walls that show them (mapdefs/walls.yaml) and the gallery level (maps/MAP01.wad). Run it
# after changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-transitions-demo.ps1
#
# The level is two halls with no way between them but the panels: each style has a panel in
# both, the pair sharing a tag (plane 4), and using one takes you to the other (A_TeleportTag,
# mapdefs/triggers.yaml). scripts/transitions-demo.lua plays the panel's style as you go: from
# the Hall of Cuts straight from one view to the other (game.transition), from the Hall of Fades
# out to black and back in (game.fade). The rest of the mod (game-info.yaml, triggers.yaml, the
# script) is written by hand.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'transitions-demo' @('textures', 'mapdefs', 'maps')

# The styles in tag order (tag 1 is palette); the script lists them in the same order
$Styles = @('palette', 'fizzle', 'melt', 'mosaic', 'crossfade', 'dither', 'burn', 'iris', 'doors',
    'crt', 'clock', 'spiral', 'wipe-left', 'wipe-right', 'wipe-up', 'wipe-down', 'blinds')
$FirstWall = 160        # the panels' wall ids: 160 to 193, one per style in each hall

#
# PANELS
#

# 5x7 capitals for the labels (one hex number per row, the top bit on the left)
$Glyphs = @{
    'A' = '0E 11 11 11 1F 11 11'; 'B' = '1E 11 11 1E 11 11 1E'; 'C' = '0E 11 10 10 10 11 0E'
    'D' = '1C 12 11 11 11 12 1C'; 'E' = '1F 10 10 1E 10 10 1F'; 'F' = '1F 10 10 1E 10 10 10'
    'G' = '0E 11 10 17 11 11 0F'; 'H' = '11 11 11 1F 11 11 11'; 'I' = '0E 04 04 04 04 04 0E'
    'K' = '11 12 14 18 14 12 11'; 'L' = '10 10 10 10 10 10 1F'; 'M' = '11 1B 15 15 11 11 11'
    'N' = '11 11 19 15 13 11 11'; 'O' = '0E 11 11 11 11 11 0E'; 'P' = '1E 11 11 1E 10 10 10'
    'R' = '1E 11 11 1E 14 12 11'; 'S' = '0F 10 10 0E 01 01 1E'; 'T' = '1F 04 04 04 04 04 04'
    'U' = '11 11 11 11 11 11 0E'; 'W' = '11 11 11 15 15 15 0A'; 'Z' = '1F 01 02 04 08 10 1F'
    '-' = '00 00 00 0E 00 00 00'
}

function Test-GlyphBit([string]$Ch, [int]$Col, [int]$Row) {
    $bits = [Convert]::ToInt32(($Glyphs[$Ch] -split ' ')[$Row], 16)
    ($bits -band (16 -shr $Col)) -ne 0
}

# Whether the label's pixel at (x, y) is lit: the text centered on the panel, its top at row 37
# (letters 6 pixels apart, 4 for a dash)
function Test-Label([string]$Text, [int]$X, [int]$Y) {
    $row = $Y - 37
    if ($row -lt 0 -or $row -ge 7) { return $false }
    $advances = foreach ($ch in $Text.ToCharArray()) { if ($ch -eq '-') { 4 } else { 6 } }
    $width = ($advances | Measure-Object -Sum).Sum - 1
    $left = [int][Math]::Floor((64 - $width) / 2)
    foreach ($i in 0..($Text.Length - 1)) {
        $ch = [string]$Text[$i]
        $col = $X - $left
        # A dash is only the middle 3 columns of its 5, so it takes up 3 (and a gap)
        if ($ch -eq '-') {
            if ($col -ge 0 -and $col -lt 3) { return (Test-GlyphBit $ch ($col + 1) $row) }
        }
        elseif ($col -ge 0 -and $col -lt 5) { return (Test-GlyphBit $ch $col $row) }
        $left += $advances[$i]
    }
    $false
}

# The pictogram's colors: the old picture blue, the new one orange, each with a checker to read
# as a picture
function Get-Old([int]$U, [int]$V) { if ((([Math]::Floor($U / 4) + [Math]::Floor($V / 4)) % 2) -eq 0) { @(50, 90, 180) } else { @(80, 130, 220) } }
function Get-New([int]$U, [int]$V) { if ((([Math]::Floor($U / 4) + [Math]::Floor($V / 4)) % 2) -eq 0) { @(220, 110, 30) } else { @(250, 170, 60) } }
function Get-Mix($A, $B, [double]$T) { @(($A[0] + ($B[0] - $A[0]) * $T), ($A[1] + ($B[1] - $A[1]) * $T), ($A[2] + ($B[2] - $A[2]) * $T)) }
# A random number 0-1 for each spot, the same every run
$Noise = New-Object 'double[]' 4096
$random = New-Object Random 7
for ($i = 0; $i -lt $Noise.Length; $i++) { $Noise[$i] = $random.NextDouble() }
function Get-Hash([int]$A, [int]$B) { $Noise[(($B -band 63) * 64 + ($A -band 63))] }

# A pictogram of a style half way through, 48x26: what the screen looks like as it goes
function Get-Pictogram([string]$Style, [int]$U, [int]$V) {
    $old = Get-Old $U $V; $new = Get-New $U $V
    $black = @(0, 0, 0)
    $cx = 23.5; $cy = 12.5
    switch ($Style) {
        'palette' {
            # left to right: the old picture darkening to black, then the new one coming up
            $t = $U / 47.0
            if ($t -lt 0.5) { return Get-Mix $old $black ($t * 2) }
            return Get-Mix $black $new (($t - 0.5) * 2)
        }
        'fizzle' { if ((Get-Hash $U $V) -lt 0.5) { return $new } else { return $old } }
        'melt' {
            $fallen = 6 + [int]((Get-Hash ([Math]::Floor($U / 2)) 7) * 10)
            if ($V -lt $fallen) { return $new } else { return Get-Old $U ($V - $fallen) }
        }
        'mosaic' {
            # big blocks, each the color at its middle, some already on the new picture
            $bu = [Math]::Floor($U / 8); $bv = [Math]::Floor($V / 8)
            $tone = 0.75 + 0.25 * (Get-Hash ($bu + 9) ($bv + 3))
            $c = if ((Get-Hash $bu $bv) -lt 0.4) { @(240, 150, 50) } else { @(70, 110, 205) }
            return @(($c[0] * $tone), ($c[1] * $tone), ($c[2] * $tone))
        }
        'crossfade' { return Get-Mix $old $new 0.5 }
        'dither' {
            $bayer = @(0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5)
            if ($bayer[($V % 4) * 4 + ($U % 4)] -lt 8) { return $new } else { return $old }
        }
        'burn' {
            $n = 0.5 * [Math]::Sin($U / 5.0) + 0.5 * [Math]::Cos($V / 4.0 + $U / 9.0) + 0.3 * (Get-Hash $U $V)
            if ($n -lt -0.1) { return $new }
            if ($n -lt 0.05) { return @(255, 200, 80) }
            if ($n -lt 0.2) { return @(200, 60, 10) }
            return $old
        }
        'iris' {
            $d = [Math]::Sqrt(($U - $cx) * ($U - $cx) + ($V - $cy) * ($V - $cy))
            if ($d -lt 9) { return $old } else { return $black }
        }
        'doors' {
            if ($U -lt 13 -or $U -ge 35) {
                if ($U -eq 12 -or $U -eq 35) { return @(170, 175, 185) }
                return @(90, 94, 104)
            }
            return $old
        }
        'crt' {
            if ([Math]::Abs($V - $cy) -lt 1 -and [Math]::Abs($U - $cx) -lt 16) { return @(255, 255, 255) }
            if ([Math]::Abs($V - $cy) -lt 2 -and [Math]::Abs($U - $cx) -lt 18) { return @(120, 140, 160) }
            return $black
        }
        'clock' {
            $angle = [Math]::Atan2($U - $cx, - ($V - $cy))
            if ($angle -lt 0) { $angle += 2 * [Math]::PI }
            if ([Math]::Abs($angle - 2.2) -lt 0.06 -and ($U - $cx) * ($U - $cx) + ($V - $cy) * ($V - $cy) -lt 150) { return @(255, 255, 255) }
            if ($angle -lt 2.2) { return $new } else { return $old }
        }
        'spiral' {
            $bu = [Math]::Floor($U / 6); $bv = [Math]::Floor($V / 6)
            $ring = [Math]::Min([Math]::Min($bu, 7 - $bu), [Math]::Min($bv, 4 - $bv))
            if ($ring -eq 0 -or ($ring -eq 1 -and $bv -eq 1 -and $bu -le 5)) { return $new } else { return $old }
        }
        'wipe-left' { return Get-Mix $old $new ([Math]::Max(0, [Math]::Min(1, ($U - 18) / 12.0))) }
        'wipe-right' { return Get-Mix $old $new ([Math]::Max(0, [Math]::Min(1, (29 - $U) / 12.0))) }
        'wipe-up' { return Get-Mix $old $new ([Math]::Max(0, [Math]::Min(1, ($V - 8) / 8.0))) }
        'wipe-down' { return Get-Mix $old $new ([Math]::Max(0, [Math]::Min(1, (17 - $V) / 8.0))) }
        'blinds' {
            $r = $V % 6
            if ($r -ge 1 -and $r -le 4) { return $new } else { return $old }
        }
    }
    throw "No pictogram for $Style"
}

# A panel: a metal plate with a bevelled edge, a little screen showing the style half way
# through, the style's name, and a button to press. $Plate is the plate's colors: its face,
# lit edge, shadowed edge and rivets.
$Steel = @{ Face = @(72, 76, 88); Lit = @(150, 154, 166); Shadow = @(34, 36, 44); Rivet = @(170, 174, 186); Fleck = @(84, 88, 100) }
$Brass = @{ Face = @(140, 96, 40); Lit = @(220, 180, 100); Shadow = @(70, 44, 16); Rivet = @(240, 210, 140); Fleck = @(160, 112, 52) }
function Save-Panel([string]$Name, [string]$Style, [hashtable]$Plate) {
    $label = $Style.ToUpperInvariant()
    Save-Picture "$modDir\textures\$Name.png" 64 64 {
        param($x, $y)
        # The screen, framed in black
        if ($x -ge 8 -and $x -lt 56 -and $y -ge 6 -and $y -lt 32) {
            $c = Get-Pictogram $Style ($x - 8) ($y - 6)
            return New-Color $c[0] $c[1] $c[2]
        }
        if ($x -ge 7 -and $x -lt 57 -and $y -ge 5 -and $y -lt 33) { return New-Color 10 10 12 }
        # The name, lit
        if (Test-Label $label $x $y) { return New-Color 255 230 120 }
        # The button
        $dx = $x - 31.5; $dy = $y - 52.5
        $d = $dx * $dx + $dy * $dy
        if ($d -lt 20) { if ($dy -lt -1) { return New-Color 255 120 110 } else { return New-Color 200 30 20 } }
        if ($d -lt 30) { return New-Color 30 30 34 }
        # The plate: lit along the top and left, shadowed along the bottom and right, rivets
        $c = if ($x -lt 2 -or $y -lt 2) { $Plate.Lit }
            elseif ($x -ge 62 -or $y -ge 62) { $Plate.Shadow }
            elseif (($x -eq 4 -or $x -eq 59) -and ($y -eq 4 -or $y -eq 59)) { $Plate.Rivet }
            elseif ((Get-Hash $x $y) -lt 0.08) { $Plate.Fleck }
            else { $Plate.Face }
        New-Color $c[0] $c[1] $c[2]
    }
}

# Steel panels in the Hall of Cuts (walls 160-176, TDCUT01-17), brass in the Hall of Fades
# (177-193, TDFADE01-17), so every panel changes the whole middle of the view
$Halls = @(
    @{ Name = 'Hall of Cuts'; Prefix = 'TDCUT'; Plate = $Steel; First = $FirstWall },
    @{ Name = 'Hall of Fades'; Prefix = 'TDFADE'; Plate = $Brass; First = $FirstWall + $Styles.Count }
)
$walls = @(
    '# The Transitions Demo''s panels, one per fade style in each hall (160-193 are this mod''s):'
    '# steel in the Hall of Cuts, brass in the Hall of Fades. Using one is the use trigger on its'
    '# tile (object 970, mapdefs/triggers.yaml). Written by tools/make-transitions-demo.ps1, with'
    '# the textures.'
    'walls:'
)
foreach ($hall in $Halls) {
    $walls += "    # The $($hall.Name)"
    for ($i = 0; $i -lt $Styles.Count; $i++) {
        $texture = '{0}{1:D2}' -f $hall.Prefix, ($i + 1)
        Save-Panel $texture $Styles[$i] $hall.Plate
        $walls += "    # $($Styles[$i]), tag $($i + 1)"
        $walls += "    '$($hall.First + $i)':"
        foreach ($face in 'North', 'South', 'East', 'West') { $walls += "        ${face}: $texture" }
    }
}
# YamlDotNet won't read a byte order mark, which PowerShell 5's UTF-8 writes: no BOM
[IO.File]::WriteAllText("$modDir\mapdefs\walls.yaml", ($walls -join "`n") + "`n", (New-Object Text.UTF8Encoding $false))
"Wrote $modDir\mapdefs\walls.yaml"

#
# THE LEVEL
#

# Two halls, the same shape: the Hall of Cuts above (blue stone), the Hall of Fades below (red
# brick). Each has nine panels along its north wall (tags 1-9) and eight along its south
# (10-17). Using a north panel facing north brings you out south of its partner, still facing
# north at the same panel: so the view changes from one hall to the other and nothing else.
# Off the Hall of Fades: the elevator (the screen fades, between levels) and a guard room
# behind a door (the death fade, if they get you).
$Layout = @(
    '#######################'   # y=20
    '###N#N#N#N#N#N#N#N#N###'   #      the Hall of Cuts' north panels, tags 1-9
    '##aaaaaaaaaaaaaaaaaaa##'
    '##aaaaaaaaaaaaaaaaaaa##'
    '##paalaaalaPalaaalaap##'   #      the player start, facing north; ceiling lights
    '##aaaaaaaaaaaaaaaaaaa##'   # y=25
    '##aaaaaaaaaaaaaaaaaaa##'
    '####S#S#S#S#S#S#S#S####'   #      its south panels, tags 10-17
    '%%%%%%%%%%%%%%%%%%%%%%%'
    '%%%n%n%n%n%n%n%n%n%n%%%'   #      the Hall of Fades' north panels
    '%%bbbbbbbbbbbbbbbbbbb%%'   # y=30
    '%%fbbbbbbbbbbbbbbbbbb%%'
    '%%bbbbbhbbbbhbbbbhbbbbX'   #      chandeliers; the elevator at the east end
    '%%fbbbbbbbbbbbbbbbbbb%%'
    '%%bbbbbbbbbbbbbbbbbbb%%'
    '%%%%s%s%s%s%Ds%s%s%s%%%'   # y=35 its south panels, the guard room's door
    '%%%%%%%%%%ccc%%%%%%%%%%'
    '%%%%%%%%%%gcg%%%%%%%%%%'   #      two guards, facing north
    '%%%%%%%%%%ccc%%%%%%%%%%'
    '%%%%%%%%%%%%%%%%%%%%%%%'
)

$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 8 }                                          # blue stone
$Tiles['%'] = @{ Plane0 = 17 }                                         # red brick
$Tiles['a'] = @{ Plane0 = 108 }                                        # the Hall of Cuts' floor
$Tiles['P'] = @{ Plane0 = 108; Plane1 = 19 }                           # the player start, facing north
$Tiles['p'] = @{ Plane0 = 108; Plane1 = 31 }                           # a green plant
$Tiles['l'] = @{ Plane0 = 108; Plane1 = 37 }                           # a ceiling light
$Tiles['b'] = @{ Plane0 = 109 }                                        # the Hall of Fades' floor
$Tiles['f'] = @{ Plane0 = 109; Plane1 = 26 }                           # a floor lamp
$Tiles['h'] = @{ Plane0 = 109; Plane1 = 27 }                           # a chandelier
$Tiles['X'] = @{ Plane0 = 21 }                                         # the elevator switch
$Tiles['D'] = @{ Plane0 = 91 }                                         # a door, north-south
$Tiles['c'] = @{ Plane0 = 110 }                                        # the guard room's floor
$Tiles['g'] = @{ Plane0 = 110; Plane1 = 109 }                          # a guard facing north
foreach ($ch in 'N', 'S', 'n', 's') { $Tiles[$ch] = @{ Plane1 = 970 } }    # a panel: walls and tags set below

$planes = New-MapPlanes -Fill 8
Add-MapLayout $planes $Layout $Tiles 0 20

# The panels in each hall, in tag order: north wall left to right, then south
foreach ($h in 0, 1) {
    $chars = @(@('N', 'S'), @('n', 's'))[$h]
    $panels = @(Find-LayoutTiles $Layout $chars[0] 0 20) + @(Find-LayoutTiles $Layout $chars[1] 0 20)
    if ($panels.Count -ne $Styles.Count) { throw "$($panels.Count) panels in a hall for $($Styles.Count) styles" }
    for ($i = 0; $i -lt $panels.Count; $i++) {
        $planes[0][$panels[$i].Index] = $Halls[$h].First + $i
        $planes[4][$panels[$i].Index] = $i + 1
    }
}
Save-MapWad "$modDir\maps\MAP01.wad" 'MAP01' 'Transitions' $planes
