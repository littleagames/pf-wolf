# Builds the art, sounds and level of examples/mods/standalone-demo, a game that needs no other
# game's data files: its wall textures (textures/*.png), sprites (sprites/*.png), font sheets
# (graphics/SDFONTS.png, SDFONTL.png), sounds (sounds/*.wav) and level (maps/MAP01.wad). Run it
# after changing anything here:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-standalone-demo.ps1
#
# Everything is drawn or synthesized below (original, not taken from any game), in full color;
# the game picks the nearest palette color for each pixel as it loads the pictures. The YAML
# files beside them (gamepack-info, game-info, actordefs, mapdefs, fonts, colors) are written
# by hand.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$ModDir = Join-Path $PSScriptRoot '..\mods\standalone-demo'

function New-Folder([string]$Name) {
    $dir = Join-Path $ModDir $Name
    New-Item -ItemType Directory -Force $dir | Out-Null
    (Resolve-Path $dir).Path
}

function Clamp([double]$v) { [int][Math]::Max(0, [Math]::Min(255, $v)) }

$rng = New-Object Random 2026
$Noise = New-Object 'int[,]' 64, 64
for ($x = 0; $x -lt 64; $x++) { for ($y = 0; $y -lt 64; $y++) { $Noise[$x, $y] = $rng.Next(-6, 7) } }

# ---------------------------------------------------------------------------------------------
# Textures: 64x64, each saved twice like the game's own walls: NAME1 for north and south faces,
# a darker NAME2 for east and west ones. Each painter gives pixel (x, y)'s color, y down.
# ---------------------------------------------------------------------------------------------

function Save-Texture([string]$Name, [scriptblock]$Paint) {
    $dir = New-Folder 'textures'
    $light = New-Object System.Drawing.Bitmap 64, 64
    $dark = New-Object System.Drawing.Bitmap 64, 64
    for ($x = 0; $x -lt 64; $x++) {
        for ($y = 0; $y -lt 64; $y++) {
            $rgb = & $Paint $x $y
            $n = $Noise[$x, $y]
            $c = @((Clamp ($rgb[0] + $n)), (Clamp ($rgb[1] + $n)), (Clamp ($rgb[2] + $n)))
            $light.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($c[0], $c[1], $c[2]))
            $dark.SetPixel($x, $y, [System.Drawing.Color]::FromArgb([int]($c[0] * 0.75), [int]($c[1] * 0.75), [int]($c[2] * 0.75)))
        }
    }
    $light.Save((Join-Path $dir "${Name}1.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $dark.Save((Join-Path $dir "${Name}2.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $light.Dispose(); $dark.Dispose()
    "Wrote textures/${Name}1.png and ${Name}2.png"
}

# Stone: blue-grey blocks in running bond, lit top edges, dark mortar
$PaintStone = {
    param([int]$x, [int]$y)
    $row = [Math]::Floor($y / 16)
    $sx = ($x + ($row % 2) * 16) % 64
    if ($y % 16 -eq 15 -or $sx % 32 -eq 31) { return , @(40, 46, 56) }
    $tone = ((($row * 7 + [Math]::Floor($sx / 32) * 13) % 5) - 2) * 6
    if ($y % 16 -eq 0) { $tone += 18 }
    , @((92 + $tone), (104 + $tone), (122 + $tone))
}

# Panels: brushed metal plates with a seam down the middle and rivets in the corners
function Get-Panel([int]$x, [int]$y) {
    $px = $x % 32
    if ($px -eq 0 -or $y -eq 0) { return , @(150, 158, 168) }
    if ($px -eq 31 -or $y -eq 63) { return , @(48, 52, 60) }
    foreach ($r in @(@(4, 4), @(27, 4), @(4, 59), @(27, 59))) {
        $dx = $px - $r[0]; $dy = $y - $r[1]
        if ($dx * $dx + $dy * $dy -le 2) { return , @(170, 176, 186) }
    }
    $streak = (($y * 3 + $px * 0) % 7) - 3
    , @((104 + $streak), (110 + $streak), (120 + $streak))
}
$PaintPanel = { param([int]$x, [int]$y) Get-Panel $x $y }

# The exit switch: a panel with a lever housing; up and red, or (thrown) down and green
function Get-Switch([int]$x, [int]$y, [bool]$On) {
    if ($x -ge 20 -and $x -le 43 -and $y -ge 16 -and $y -le 47) {
        if ($x -eq 20 -or $y -eq 16) { return , @(30, 32, 36) }
        if ($x -eq 43 -or $y -eq 47) { return , @(170, 176, 186) }
        $knobY = if ($On) { 38 } else { 25 }
        $lamp = if ($On) { @(60, 230, 90) } else { @(230, 50, 40) }
        if ([Math]::Abs($x - 31.5) -le 1.5 -and $y -ge 22 -and $y -le 41) { return , @(60, 62, 68) }
        $dx = $x - 31.5; $dy = $y - $knobY
        if ($dx * $dx + $dy * $dy -le 16) { return , $lamp }
        return , @(52, 56, 64)
    }
    Get-Panel $x $y
}
$PaintExit = { param([int]$x, [int]$y) Get-Switch $x $y $false }
$PaintExitOn = { param([int]$x, [int]$y) Get-Switch $x $y $true }

# The door: a steel slab with yellow and black hazard stripes along the bottom
$PaintDoor = {
    param([int]$x, [int]$y)
    if ($x -le 1 -or $x -ge 62) { return , @(50, 54, 62) }
    if ($y -ge 50 -and $y -le 58) {
        if ((($x + $y) % 12) -lt 6) { return , @(220, 180, 30) }
        return , @(26, 26, 26)
    }
    if ($y -eq 49 -or $y -eq 59) { return , @(40, 40, 44) }
    if ($x % 16 -eq 0) { return , @(70, 76, 86) }
    , @(120, 128, 140)
}

# The door's slot: the frame either side of the doorway
$PaintSlot = {
    param([int]$x, [int]$y)
    if ($x -lt 6 -or $x -gt 57) { return , @(60, 64, 72) }
    if ($x -eq 6 -or $x -eq 57) { return , @(30, 32, 36) }
    , @(84, 90, 100)
}

Save-Texture 'SDSTON' $PaintStone
Save-Texture 'SDPANL' $PaintPanel
Save-Texture 'SDEXIT' $PaintExit
Save-Texture 'SDEXON' $PaintExitOn
Save-Texture 'SDDOOR' $PaintDoor
Save-Texture 'SDSLOT' $PaintSlot

# ---------------------------------------------------------------------------------------------
# Sprites: 64x64 with see-through backgrounds, standing on the bottom row
# ---------------------------------------------------------------------------------------------

function Save-Sprite([string]$Name, [scriptblock]$Paint) {
    $dir = New-Folder 'sprites'
    $bmp = New-Object System.Drawing.Bitmap 64, 64
    for ($x = 0; $x -lt 64; $x++) {
        for ($y = 0; $y -lt 64; $y++) {
            $rgb = & $Paint $x $y
            if ($rgb) { $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, (Clamp $rgb[0]), (Clamp $rgb[1]), (Clamp $rgb[2]))) }
        }
    }
    $bmp.Save((Join-Path $dir "$Name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    "Wrote sprites/$Name.png"
}

# The blaster in hand: a barrel pointing up the middle of the view, a grip, a glowing coil.
# $Kick moves it down (recoil); $Flash adds the bolt leaving the muzzle.
function Get-Blaster([int]$x, [int]$y, [int]$Kick, [bool]$Flash) {
    $y -= $Kick
    $dx = [Math]::Abs($x - 31.5)
    if ($Flash -and $y -ge 18 - $Kick -and $y -lt 34) {
        $r = [Math]::Sqrt(($x - 31.5) * ($x - 31.5) + ($y - 26) * ($y - 26) * 0.6)
        if ($r -lt 4) { return , @(240, 250, 255) }
        if ($r -lt 8) { return , @(110, 190, 255) }
    }
    if ($y -ge 34 -and $y -lt 48 -and $dx -lt 4) {                    # barrel
        if ($dx -ge 3) { return , @(40, 44, 52) }
        return , @((120 - $dx * 10), (126 - $dx * 10), (140 - $dx * 10))
    }
    if ($y -ge 44 -and $y -lt 54 -and $dx -lt 7) {                    # coil
        if ($y % 3 -eq 0) { return , @(60, 140, 230) }
        return , @(36, 40, 50)
    }
    if ($y -ge 54 -and $y -lt 64 -and $dx -lt 10) {                   # body
        if ($dx -ge 9 -or $y -eq 54) { return , @(30, 32, 38) }
        return , @((96 - $dx * 3), (100 - $dx * 3), (112 - $dx * 3))
    }
    $null
}
Save-Sprite 'BLSTA0' { param([int]$x, [int]$y) Get-Blaster $x $y 0 $false }
Save-Sprite 'BLSTB0' { param([int]$x, [int]$y) Get-Blaster $x $y 3 $true }
Save-Sprite 'BLSTC0' { param([int]$x, [int]$y) Get-Blaster $x $y 2 $false }

# An energy cell: a small battery with a blue band
Save-Sprite 'CELLA0' {
    param([int]$x, [int]$y)
    if ($x -lt 26 -or $x -gt 37 -or $y -lt 46) { return $null }
    if ($y -lt 49) { if ($x -ge 30 -and $x -le 33) { return , @(180, 180, 190) } else { return $null } }
    if ($x -eq 26 -or $x -eq 37 -or $y -eq 63) { return , @(30, 34, 40) }
    if ($y -ge 54 -and $y -le 57) { return , @(70, 160, 240) }
    , @(90, 96, 108)
}

# A medkit: a white case with a red cross
Save-Sprite 'MEDKA0' {
    param([int]$x, [int]$y)
    if ($x -lt 20 -or $x -gt 43 -or $y -lt 48) { return $null }
    if ($y -eq 48 -and ($x -lt 28 -or $x -gt 35)) { return $null }
    if ($x -eq 20 -or $x -eq 43 -or $y -eq 63 -or $y -eq 49) { return , @(120, 120, 120) }
    if ($y -eq 48) { return , @(80, 80, 80) }
    if (([Math]::Abs($x - 31.5) -le 2 -and $y -ge 52 -and $y -le 61) -or ($x -ge 26 -and $x -le 37 -and [Math]::Abs($y - 56.5) -le 2)) {
        return , @(210, 30, 30)
    }
    , @(230, 230, 226)
}

# A floor lamp: a stand and a glowing shade
Save-Sprite 'LAMPA0' {
    param([int]$x, [int]$y)
    $dx = [Math]::Abs($x - 31.5)
    if ($y -ge 60 -and $dx -lt 8) { return , @(50, 52, 58) }
    if ($y -ge 22 -and $y -lt 60 -and $dx -lt 1.5) { return , @(80, 84, 92) }
    if ($y -ge 8 -and $y -lt 22) {
        $half = 5 + ($y - 8) * 0.6
        if ($dx -lt $half) { return , @(255, (220 - ($y - 8) * 3), (140 - ($y - 8) * 4)) }
    }
    $null
}

# ---------------------------------------------------------------------------------------------
# Fonts: sheets of glyphs for characters 32 ' ' to 95 '_', 16 cells a row. Each glyph is drawn
# from a 5x7 bitmap (one hex number per row, the top bit on the left), white on see-through:
# the fonts are colorized, drawn in each text's color. Lower case uses the upper case glyphs
# (fonts.yaml upper-case). The large font is the small one at twice the width and 11 pixels tall
# (rows 1, 3 and 5 aren't doubled), so it fits the shared menus' 13-pixel lines.
# ---------------------------------------------------------------------------------------------

$Glyphs = @{
    ' ' = '00 00 00 00 00 00 00'; '!' = '04 04 04 04 04 00 04'; '"' = '0A 0A 0A 00 00 00 00'
    '#' = '0A 0A 1F 0A 1F 0A 0A'; '$' = '04 0F 14 0E 05 1E 04'; '%' = '18 19 02 04 08 13 03'
    '&' = '0C 12 14 08 15 12 0D'; "'" = '04 04 08 00 00 00 00'; '(' = '02 04 08 08 08 04 02'
    ')' = '08 04 02 02 02 04 08'; '*' = '00 04 15 0E 15 04 00'; '+' = '00 04 04 1F 04 04 00'
    ',' = '00 00 00 00 0C 04 08'; '-' = '00 00 00 1F 00 00 00'; '.' = '00 00 00 00 00 0C 0C'
    '/' = '00 01 02 04 08 10 00'; '0' = '0E 11 13 15 19 11 0E'; '1' = '04 0C 04 04 04 04 0E'
    '2' = '0E 11 01 02 04 08 1F'; '3' = '1F 02 04 02 01 11 0E'; '4' = '02 06 0A 12 1F 02 02'
    '5' = '1F 10 1E 01 01 11 0E'; '6' = '06 08 10 1E 11 11 0E'; '7' = '1F 01 02 04 08 08 08'
    '8' = '0E 11 11 0E 11 11 0E'; '9' = '0E 11 11 0F 01 02 0C'; ':' = '00 0C 0C 00 0C 0C 00'
    ';' = '00 0C 0C 00 0C 04 08'; '<' = '02 04 08 10 08 04 02'; '=' = '00 00 1F 00 1F 00 00'
    '>' = '08 04 02 01 02 04 08'; '?' = '0E 11 01 02 04 00 04'; '@' = '0E 11 01 0D 15 15 0E'
    'A' = '0E 11 11 11 1F 11 11'; 'B' = '1E 11 11 1E 11 11 1E'; 'C' = '0E 11 10 10 10 11 0E'
    'D' = '1C 12 11 11 11 12 1C'; 'E' = '1F 10 10 1E 10 10 1F'; 'F' = '1F 10 10 1E 10 10 10'
    'G' = '0E 11 10 17 11 11 0F'; 'H' = '11 11 11 1F 11 11 11'; 'I' = '0E 04 04 04 04 04 0E'
    'J' = '07 02 02 02 02 12 0C'; 'K' = '11 12 14 18 14 12 11'; 'L' = '10 10 10 10 10 10 1F'
    'M' = '11 1B 15 15 11 11 11'; 'N' = '11 11 19 15 13 11 11'; 'O' = '0E 11 11 11 11 11 0E'
    'P' = '1E 11 11 1E 10 10 10'; 'Q' = '0E 11 11 11 15 12 0D'; 'R' = '1E 11 11 1E 14 12 11'
    'S' = '0F 10 10 0E 01 01 1E'; 'T' = '1F 04 04 04 04 04 04'; 'U' = '11 11 11 11 11 11 0E'
    'V' = '11 11 11 11 11 0A 04'; 'W' = '11 11 11 15 15 15 0A'; 'X' = '11 11 0A 04 0A 11 11'
    'Y' = '11 11 11 0A 04 04 04'; 'Z' = '1F 01 02 04 08 10 1F'; '[' = '0E 08 08 08 08 08 0E'
    '\' = '00 10 08 04 02 01 00'; ']' = '0E 02 02 02 02 02 0E'; '^' = '04 0A 11 00 00 00 00'
    '_' = '00 00 00 00 00 00 1F'
}

# $RowHeights: how many pixels tall each of the 7 glyph rows is drawn
function Save-FontSheet([string]$Name, [int]$Scale, [int[]]$RowHeights, [int]$CellW, [int]$CellH) {
    $sheet = New-Object System.Drawing.Bitmap ($CellW * 16), ($CellH * 4)
    for ($code = 32; $code -le 95; $code++) {
        $rows = $Glyphs[[string][char]$code] -split ' '
        $cx = (($code - 32) % 16) * $CellW
        $cy = [Math]::Floor(($code - 32) / 16) * $CellH + 1
        for ($row = 0; $row -lt 7; $row++) {
            $bits = [Convert]::ToInt32($rows[$row], 16)
            for ($col = 0; $col -lt 5; $col++) {
                if (($bits -band (16 -shr $col)) -eq 0) { continue }
                for ($sx = 0; $sx -lt $Scale; $sx++) {
                    for ($sy = 0; $sy -lt $RowHeights[$row]; $sy++) {
                        $sheet.SetPixel($cx + $col * $Scale + $sx, $cy + $sy, [System.Drawing.Color]::White)
                    }
                }
            }
            $cy += $RowHeights[$row]
        }
    }
    $dir = New-Folder 'graphics'
    $sheet.Save((Join-Path $dir "$Name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $sheet.Dispose()
    "Wrote graphics/$Name.png"
}
Save-FontSheet 'SDFONTS' 1 @(1, 1, 1, 1, 1, 1, 1) 8 10
Save-FontSheet 'SDFONTL' 2 @(2, 1, 2, 1, 2, 1, 2) 12 13

# ---------------------------------------------------------------------------------------------
# Full-screen pictures, 320x200: the title (game-info title-pics) and the signon screen, whose
# dark panel is where the startup info is printed (game-info signon text-area)
# ---------------------------------------------------------------------------------------------

# Text in the 5x7 glyphs, each pixel a Scale x Scale block, centered on $CenterX
function Write-Text($Bmp, [string]$Text, [int]$CenterX, [int]$Top, [int]$Scale, [System.Drawing.Color]$Color) {
    $x = $CenterX - [int](($Text.Length * 6 - 1) * $Scale / 2)
    foreach ($ch in $Text.ToUpper().ToCharArray()) {
        $rows = $Glyphs[[string]$ch] -split ' '
        for ($row = 0; $row -lt 7; $row++) {
            $bits = [Convert]::ToInt32($rows[$row], 16)
            for ($col = 0; $col -lt 5; $col++) {
                if (($bits -band (16 -shr $col)) -eq 0) { continue }
                for ($sx = 0; $sx -lt $Scale; $sx++) {
                    for ($sy = 0; $sy -lt $Scale; $sy++) {
                        $Bmp.SetPixel($x + $col * $Scale + $sx, $Top + $row * $Scale + $sy, $Color)
                    }
                }
            }
        }
        $x += 6 * $Scale
    }
}

# Deep water: dark blue fading lighter toward the top, with light rays and rising bubbles
function New-Backdrop {
    $bmp = New-Object System.Drawing.Bitmap 320, 200
    $bubbles = New-Object Random 11
    $spots = @(); for ($i = 0; $i -lt 40; $i++) { $spots += , @($bubbles.Next(0, 320), $bubbles.Next(0, 200), $bubbles.Next(1, 4)) }
    for ($x = 0; $x -lt 320; $x++) {
        for ($y = 0; $y -lt 200; $y++) {
            $t = $y / 199
            $ray = [Math]::Max(0, [Math]::Sin(($x * 0.04) + $y * 0.015)) * (1 - $t) * 22
            $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb((Clamp (10 + $ray)), (Clamp (60 - 44 * $t + $ray)), (Clamp (120 - 84 * $t + $ray))))
        }
    }
    foreach ($b in $spots) {
        for ($dx = -$b[2]; $dx -le $b[2]; $dx++) {
            for ($dy = -$b[2]; $dy -le $b[2]; $dy++) {
                $d = $dx * $dx + $dy * $dy
                $px = $b[0] + $dx; $py = $b[1] + $dy
                if ($px -ge 0 -and $px -lt 320 -and $py -ge 0 -and $py -lt 200 -and $d -le $b[2] * $b[2] -and $d -ge ($b[2] - 1) * ($b[2] - 1)) {
                    $bmp.SetPixel($px, $py, [System.Drawing.Color]::FromArgb(150, 200, 230))
                }
            }
        }
    }
    , $bmp
}

$Gold = [System.Drawing.Color]::FromArgb(240, 200, 90)
$Pale = [System.Drawing.Color]::FromArgb(190, 220, 240)
$Shadow = [System.Drawing.Color]::FromArgb(4, 12, 30)

$title = New-Backdrop
Write-Text $title 'STANDALONE' 162 52 4 $Shadow
Write-Text $title 'STANDALONE' 160 50 4 $Gold
Write-Text $title 'DEMO' 162 92 4 $Shadow
Write-Text $title 'DEMO' 160 90 4 $Gold
Write-Text $title 'A GAME MADE OF ONE MOD' 160 150 1 $Pale
Write-Text $title 'NO OTHER GAME NEEDED' 160 162 1 $Pale
$dir = New-Folder 'graphics'
$title.Save((Join-Path $dir 'SDTITLE.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$title.Dispose()
'Wrote graphics/SDTITLE.png'

$signon = New-Backdrop
Write-Text $signon 'STANDALONE DEMO' 161 17 2 $Shadow
Write-Text $signon 'STANDALONE DEMO' 160 16 2 $Gold
for ($x = 12; $x -lt 308; $x++) {
    for ($y = 40; $y -lt 186; $y++) {
        $edge = $x -eq 12 -or $x -eq 307 -or $y -eq 40 -or $y -eq 185
        $signon.SetPixel($x, $y, $(if ($edge) { [System.Drawing.Color]::FromArgb(60, 120, 180) } else { [System.Drawing.Color]::FromArgb(6, 18, 40) }))
    }
}
$signon.Save((Join-Path $dir 'SDSIGNON.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$signon.Dispose()
'Wrote graphics/SDSIGNON.png'

# ---------------------------------------------------------------------------------------------
# The pictures the shared menus (pfwolf.pk3's menudefs/) and the engine draw by name: Wolf3D's
# come from its VGAGRAPH, so a standalone game gives its own. Pictures don't have see-through
# pixels, so each is filled with the color it's drawn on (colors.yaml's BORDCOLOR for the menu
# screen, BKGDCOLOR inside the menu windows): both are matched to the same palette color.
# ---------------------------------------------------------------------------------------------

$MenuBack = [System.Drawing.Color]::FromArgb(0x1C, 0x4C, 0x78)      # BORDCOLOR
$WindowBack = [System.Drawing.Color]::FromArgb(0x0C, 0x24, 0x40)    # BKGDCOLOR
$Black = [System.Drawing.Color]::Black

function Save-Pic([string]$Name, [int]$Width, [int]$Height, [System.Drawing.Color]$Back, [scriptblock]$Draw) {
    $bmp = New-Object System.Drawing.Bitmap $Width, $Height
    $g = [System.Drawing.Graphics]::FromImage($bmp); $g.Clear($Back); $g.Dispose()
    & $Draw $bmp
    $bmp.Save((Join-Path (New-Folder 'graphics') "$Name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    "Wrote graphics/$Name.png"
}

# The headings over the menus' stripe (y 0, black) or in their windows
foreach ($banner in @(
        @('c_options', 'OPTIONS', $Black), @('c_control', 'CONTROL', $Black), @('c_customize', 'CUSTOMIZE', $Black),
        @('c_loadgame', 'LOAD GAME', $Black), @('c_savegame', 'SAVE GAME', $Black), @('c_fxtitle', 'SOUND', $MenuBack))) {
    $text = $banner[1]
    Save-Pic $banner[0] ($text.Length * 12 + 8) 22 $banner[2] { param($bmp) Write-Text $bmp $text ($bmp.Width / 2) 4 2 $Gold }
}

# The hint along the bottom of the menu screen
Save-Pic 'c_mouselback' 232 11 $MenuBack { param($bmp) Write-Text $bmp 'ARROWS MOVE - ENTER PICKS - ESC BACK' 116 2 1 $Pale }

# The menu cursor: a little fish, its tail flicking for the blink (c_cursor2)
foreach ($frame in 1, 2) {
    Save-Pic "c_cursor$frame" 20 12 $WindowBack {
        param($bmp)
        $body = [System.Drawing.Color]::FromArgb(240, 170, 60)
        for ($x = 0; $x -lt 20; $x++) {
            for ($y = 0; $y -lt 12; $y++) {
                $dx = ($x - 12) / 7.0; $dy = ($y - 5.5) / 4.0
                if ($dx * $dx + $dy * $dy -le 1) { $bmp.SetPixel($x, $y, $body) }
                $tail = if ($frame -eq 1) { 3 } else { 1 }
                if ($x -lt 6 -and [Math]::Abs($y - 5.5) -le (6 - $x) * 0.9 -and [Math]::Abs($y - 5.5) -ge $tail - 1) { $bmp.SetPixel($x, $y, $body) }
            }
        }
        $bmp.SetPixel(15, 4, [System.Drawing.Color]::Black)
    }
}

# Checkboxes (the Sound and Options menus), on and off
foreach ($box in @(@('c_selected', $true), @('c_notselected', $false))) {
    $on = $box[1]
    Save-Pic $box[0] 12 12 $WindowBack {
        param($bmp)
        for ($i = 1; $i -lt 11; $i++) { foreach ($e in 1, 10) { $bmp.SetPixel($i, $e, $Pale); $bmp.SetPixel($e, $i, $Pale) } }
        if ($on) { for ($x = 3; $x -le 8; $x++) { for ($y = 3; $y -le 8; $y++) { $bmp.SetPixel($x, $y, $Gold) } } }
    }
}

# Shown while a game loads or saves: a disk, then with its light on
foreach ($frame in 1, 2) {
    Save-Pic "c_diskloading$frame" 16 16 $WindowBack {
        param($bmp)
        for ($x = 1; $x -lt 15; $x++) { for ($y = 1; $y -lt 15; $y++) { $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(60, 70, 90)) } }
        for ($x = 4; $x -lt 12; $x++) { for ($y = 2; $y -lt 6; $y++) { $bmp.SetPixel($x, $y, $Pale) } }
        if ($frame -eq 2) { $bmp.SetPixel(12, 12, $Gold); $bmp.SetPixel(11, 12, $Gold) }
    }
}

# ---------------------------------------------------------------------------------------------
# Sounds: 16-bit mono WAVs, synthesized
# ---------------------------------------------------------------------------------------------

function Save-Wav([string]$Name, [double]$Seconds, [scriptblock]$Sample) {
    $rate = 22050
    $count = [int]($rate * $Seconds)
    $ms = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter $ms
    $w.Write([Text.Encoding]::ASCII.GetBytes('RIFF')); $w.Write([int](36 + $count * 2))
    $w.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt ')); $w.Write([int]16)
    $w.Write([int16]1); $w.Write([int16]1); $w.Write([int]$rate); $w.Write([int]($rate * 2))
    $w.Write([int16]2); $w.Write([int16]16)
    $w.Write([Text.Encoding]::ASCII.GetBytes('data')); $w.Write([int]($count * 2))
    for ($i = 0; $i -lt $count; $i++) {
        $v = & $Sample ($i / $rate) ($i / $count)
        $w.Write([int16][Math]::Max(-32767, [Math]::Min(32767, $v * 32767)))
    }
    $w.Flush()
    $dir = New-Folder 'sounds'
    [IO.File]::WriteAllBytes((Join-Path $dir "$Name.wav"), $ms.ToArray())
    "Wrote sounds/$Name.wav"
}

# The blaster: a falling square-ish zap with a little noise, fading out
$zapRng = New-Object Random 5
Save-Wav 'BLASTER' 0.22 {
    param([double]$t, [double]$f)
    $freq = 1400 * [Math]::Pow(0.12, $f)
    $wave = [Math]::Sign([Math]::Sin(2 * [Math]::PI * $freq * $t)) * 0.5 + [Math]::Sin(2 * [Math]::PI * $freq * 0.5 * $t) * 0.3
    ($wave + ($zapRng.NextDouble() - 0.5) * 0.2) * (1 - $f) * 0.6
}

# The menus' cursor moving and picking: short clicks
Save-Wav 'CLICK' 0.04 { param([double]$t, [double]$f) [Math]::Sin(2 * [Math]::PI * 1800 * $t) * (1 - $f) * 0.5 }
Save-Wav 'SELECT' 0.12 { param([double]$t, [double]$f) [Math]::Sin(2 * [Math]::PI * (900 + 900 * $f) * $t) * (1 - $f) * 0.5 }

# Walking into a wall: a dull thud
Save-Wav 'THUD' 0.12 { param([double]$t, [double]$f) [Math]::Sin(2 * [Math]::PI * (120 - 60 * $f) * $t) * (1 - $f) * 0.8 }

# Picking something up: two rising notes
Save-Wav 'PICKUP' 0.2 {
    param([double]$t, [double]$f)
    $note = if ($f -lt 0.5) { 880 } else { 1320 }
    [Math]::Sin(2 * [Math]::PI * $note * $t) * (1 - ($f * 2 % 1) * 0.6) * 0.4
}

# A door sliding: filtered rumble
$doorRng = New-Object Random 9; $doorLast = 0.0
Save-Wav 'DOOR' 0.6 {
    param([double]$t, [double]$f)
    $script:doorLast = $script:doorLast * 0.97 + ($doorRng.NextDouble() - 0.5) * 0.3
    $script:doorLast * [Math]::Sin([Math]::PI * $f) * 2.5
}

# The exit switch: a low clunk and a rising chime
Save-Wav 'SWITCH' 0.5 {
    param([double]$t, [double]$f)
    $clunk = [Math]::Sin(2 * [Math]::PI * 90 * $t) * [Math]::Exp(-$t * 30)
    $chime = if ($t -gt 0.08) { [Math]::Sin(2 * [Math]::PI * (660 + 440 * $f) * $t) * [Math]::Exp(-($t - 0.08) * 6) * 0.4 } else { 0 }
    ($clunk + $chime) * 0.7
}

# ---------------------------------------------------------------------------------------------
# The level: one character per tile, its top-left corner at tile ($Left, $Top); every tile
# outside it is stone. Floor codes are areas (mapdefs floors: area n is 107 + n), so each room
# that doors close off has its own. Wall, door and thing numbers are the mod's mapdefs/map.yaml.
# ---------------------------------------------------------------------------------------------

$Left = 22
$Top = 24
$Layout = @(
    '###################'
    '#######PPXPP#######'   # the exit switch, in the vault room's north wall
    '#######P...P#######'   # the vault room
    '#######P.c.P#######'
    '#######PP-PP#######'
    '#P.....L...L.....P#'   # the hall: lamps, pillars, cells and medkits
    '#P..c...........mP#'
    '#P...PP.....PP...P#'
    '#P...PP.....PP...P#'
    '#P..m...........cP#'
    '#P.....L...L.....P#'
    '#PPPPPPPP-PPPPPPPP#'
    '########...########'   # the airlock, where the player starts
    '########.S.########'
    '########...########'
    '###################'
)

$Vault = 110; $Hall = 109; $Airlock = 108
$Tiles = @{
    '#' = @(1, 0)                       # stone
    'P' = @(2, 0)                       # metal panels
    'X' = @(3, 0)                       # the exit switch
    '-' = @(91, 0)                      # a door in a wall running east-west
    'S' = @($Airlock, 19)               # the player start, facing north
    'L' = @($Hall, 23)                  # lamp
    'c' = @($null, 24)                  # energy cells (the room's floor)
    'm' = @($null, 25)                  # medkit
    '.' = @($null, 0)
}

$Size = 64
$PlaneCount = 3
$planes = @()
for ($p = 0; $p -lt $PlaneCount; $p++) { $planes += , (New-Object 'uint16[]' ($Size * $Size)) }
for ($i = 0; $i -lt $Size * $Size; $i++) { $planes[0][$i] = 1 }

for ($row = 0; $row -lt $Layout.Count; $row++) {
    $line = $Layout[$row]
    if ($line.Length -ne 19) { throw "Layout row $row is $($line.Length) wide, not 19" }
    $room = if ($row -le 3) { $Vault } elseif ($row -le 11) { $Hall } else { $Airlock }
    for ($col = 0; $col -lt $line.Length; $col++) {
        $ch = [string]$line[$col]
        if (!$Tiles.ContainsKey($ch)) { throw "Unknown layout character '$ch' at row $row, column $col" }
        $tile = $Tiles[$ch]
        $i = ($Top + $row) * $Size + $Left + $col
        $planes[0][$i] = if ($null -eq $tile[0]) { $room } else { $tile[0] }
        $planes[1][$i] = $tile[1]
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
$title = 'The Vault'
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

$path = Join-Path (New-Folder 'maps') 'MAP01.wad'
[IO.File]::WriteAllBytes($path, $out.ToArray())
"Wrote maps/MAP01.wad ($($out.Length) bytes)"
