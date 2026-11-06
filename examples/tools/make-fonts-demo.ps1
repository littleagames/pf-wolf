# Builds the picture fonts of examples/mods/fonts-demo: one of each kind fonts.yaml can make
# from files of its own (the rest of the mod's fonts are Wolf3D's SmallFont and LargeFont with
# effects, needing no files). Run it after changing anything here:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-fonts-demo.ps1
#
#   fonts/FDITALIC.fnt     a Wolf3D-format font file (type: vga, source: FDITALIC)
#   graphics/FDDOTS.png    a sheet of dot-matrix glyphs, white on see-through (type: sheet, colorize)
#   graphics/FDGOLD.png    a sheet of gold glyphs on a magenta key color (type: sheet, transparent)
#   graphics/FDLED*.png    a picture per character, seven-segment digits (type: graphic, pattern)
#
# Every glyph comes from the same 5x7 bitmaps below (one hex number per row, the top bit on the
# left), drawn a different way for each font. The YAML files beside them are written by hand.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')

$ModDir = Get-ModDir 'fonts-demo' @('fonts', 'graphics')

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

# Whether a glyph's pixel (column 0-4, row 0-6) is set
function Test-Bit([string]$Ch, [int]$Col, [int]$Row) {
    $bits = [Convert]::ToInt32(($Glyphs[$Ch] -split ' ')[$Row], 16)
    ($bits -band (16 -shr $Col)) -ne 0
}

# The columns a glyph uses: first and last (a space: none)
function Get-GlyphSpan([string]$Ch) {
    $first = -1; $last = -1
    for ($col = 0; $col -lt 5; $col++) {
        for ($row = 0; $row -lt 7; $row++) {
            if (Test-Bit $Ch $col $row) { if ($first -lt 0) { $first = $col }; $last = $col; break }
        }
    }
    , @($first, $last)
}

# ---------------------------------------------------------------------------------------------
# FDITALIC.fnt: Wolf3D's own font format, as VGAGRAPH's SmallFont and LargeFont are: a 16-bit
# height, 256 16-bit glyph offsets, 256 byte widths, then each glyph's rows of one byte per pixel
# (non-zero is drawn, in the text's color). The 5x7 glyphs leant right by moving the upper rows
# along, 9 pixels tall with a blank row above and below. Lower case uses the capitals' glyphs.
# ---------------------------------------------------------------------------------------------

$Height = 9
$Lean = @(2, 2, 2, 1, 1, 1, 0)      # how far each of the 7 rows moves right
$header = 2 + 512 + 256
$widths = New-Object byte[] 256
$offsets = New-Object int[] 256
$data = New-Object Collections.Generic.List[byte]

for ($code = 32; $code -le 122; $code++) {
    $ch = [string][char]$code
    $source = if ($code -ge 97) { $ch.ToUpper() } else { $ch }
    if (!$Glyphs.ContainsKey($source)) { continue }
    $span = Get-GlyphSpan $source
    if ($span[0] -lt 0) {
        $widths[$code] = 4                                      # the space
        $offsets[$code] = $header + $data.Count
        for ($i = 0; $i -lt 4 * $Height; $i++) { $data.Add(0) }
        continue
    }
    # The glyph's own columns, plus the lean, plus a pixel's gap after it
    $width = ($span[1] - $span[0] + 1) + 2 + 1
    $widths[$code] = $width
    $offsets[$code] = $header + $data.Count
    for ($y = 0; $y -lt $Height; $y++) {
        for ($x = 0; $x -lt $width; $x++) {
            $row = $y - 1
            $col = $x - $(if ($row -ge 0 -and $row -lt 7) { $Lean[$row] } else { 0 }) + $span[0]
            $on = $row -ge 0 -and $row -lt 7 -and $col -ge $span[0] -and $col -le $span[1] -and (Test-Bit $source $col $row)
            $data.Add($(if ($on) { 1 } else { 0 }))
        }
    }
}

$ms = New-Object IO.MemoryStream
$w = New-Object IO.BinaryWriter $ms
$w.Write([int16]$Height)
foreach ($o in $offsets) { $w.Write([int16]$o) }
$w.Write($widths)
$w.Write($data.ToArray())
$w.Flush()
[IO.File]::WriteAllBytes((Join-Path $ModDir 'fonts\FDITALIC.fnt'), $ms.ToArray())
"Wrote fonts/FDITALIC.fnt ($($ms.Length) bytes)"

# ---------------------------------------------------------------------------------------------
# FDDOTS.png: a dot-matrix sheet, each glyph pixel a dot with a gap after it (9x13), white on
# see-through so the font can be colorized. Cells of 10x14, 16 a row, from '!' (33) to '_'
# (95): no space cell, so fonts.yaml's first-char is 33 and space-width says how wide a space is.
# ---------------------------------------------------------------------------------------------

$CellW = 10; $CellH = 14
$first = 33; $last = 95
$rows = [Math]::Ceiling(($last - $first + 1) / 16)
Save-Picture (Join-Path $ModDir 'graphics\FDDOTS.png') ($CellW * 16) ($CellH * $rows) {
    param($x, $y)
    $cell = [int]([Math]::Floor($y / $CellH) * 16 + [Math]::Floor($x / $CellW))
    $code = $first + $cell
    if ($code -gt $last) { return $null }
    $px = $x % $CellW; $py = $y % $CellH
    if ($px % 2 -eq 1 -or $py % 2 -eq 1 -or $px -ge 9 -or $py -ge 13) { return $null }
    if (Test-Bit ([string][char]$code) ([int]($px / 2)) ([int]($py / 2))) { [Drawing.Color]::White } else { $null }
}

# ---------------------------------------------------------------------------------------------
# FDGOLD.png: big gold capitals in their own colors, each glyph pixel 2x2, shaded light to dark
# down the letter with a dark brown edge on the right and bottom. Fixed-width cells of 13x17 on
# a magenta background: no see-through pixels, so fonts.yaml's `transparent` names the magenta
# as the color to leave out. Only the characters in $GoldChars, in that order (fonts.yaml chars).
# ---------------------------------------------------------------------------------------------

$GoldChars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!?.,:-'''
$GoldCellW = 13; $GoldCellH = 17
$Key = [Drawing.Color]::FromArgb(255, 255, 0, 255)
$Edge = [Drawing.Color]::FromArgb(255, 80, 44, 8)

function Test-GoldPixel([string]$Ch, [int]$X, [int]$Y) {
    # The glyph scaled 2x, 1 pixel in from the cell's top-left corner
    $gx = $X - 1; $gy = $Y - 1
    if ($gx -lt 0 -or $gy -lt 0 -or $gx -ge 10 -or $gy -ge 14) { return $false }
    Test-Bit $Ch ([int][Math]::Floor($gx / 2)) ([int][Math]::Floor($gy / 2))
}

Save-Picture (Join-Path $ModDir 'graphics\FDGOLD.png') ($GoldCellW * 14) ($GoldCellH * 3) {
    param($x, $y)
    $cell = [int]([Math]::Floor($y / $GoldCellH) * 14 + [Math]::Floor($x / $GoldCellW))
    if ($cell -ge $GoldChars.Length) { return $Key }
    $ch = [string]$GoldChars[$cell]
    $cx = $x % $GoldCellW; $cy = $y % $GoldCellH
    if (Test-GoldPixel $ch $cx $cy) {
        $t = ($cy - 1) / 13.0                                           # 0 at the top, 1 at the bottom
        return New-Color (255 - 40 * $t) (236 - 120 * $t) (150 - 140 * $t)
    }
    # The edge: just right of or below a lit pixel
    if ((Test-GoldPixel $ch ($cx - 1) $cy) -or (Test-GoldPixel $ch $cx ($cy - 1)) -or (Test-GoldPixel $ch ($cx - 1) ($cy - 1))) {
        return $Edge
    }
    $Key
}

# ---------------------------------------------------------------------------------------------
# FDLED048.png - FDLED057.png: a seven-segment digit per picture, 9x15, lit segments red with a
# bright core, unlit ones a faint dark red, on see-through. fonts.yaml's pattern FDLED{code:000}
# names them by character code ('0' is 48). FDLEDCOL.png is the colon, given by itself (glyphs).
# ---------------------------------------------------------------------------------------------

#                a  b  c  d  e  f  g      (a top, b top right, c bottom right, d bottom,
$Segments = @{  #                           e bottom left, f top left, g middle)
    '0' = '1111110'; '1' = '0110000'; '2' = '1101101'; '3' = '1111001'; '4' = '0110011'
    '5' = '1011011'; '6' = '1011111'; '7' = '1110000'; '8' = '1111111'; '9' = '1111011'
}
$Lit = [Drawing.Color]::FromArgb(255, 255, 40, 24)
$Core = [Drawing.Color]::FromArgb(255, 255, 190, 150)
$Unlit = [Drawing.Color]::FromArgb(255, 70, 8, 8)

# Which segment (0-6) pixel x, y of a 9x15 digit is in, and whether it's the segment's middle line
function Get-Segment([int]$X, [int]$Y) {
    $horizontal = $X -ge 2 -and $X -le 6
    if ($horizontal -and $Y -le 1) { return , @(0, ($Y -eq 0)) }
    if ($horizontal -and ($Y -eq 7)) { return , @(6, $true) }
    if ($horizontal -and $Y -ge 13) { return , @(3, ($Y -eq 14)) }
    $upper = $Y -ge 2 -and $Y -le 6; $lower = $Y -ge 8 -and $Y -le 12
    if ($X -ge 7 -and $upper) { return , @(1, ($X -eq 8)) }
    if ($X -ge 7 -and $lower) { return , @(2, ($X -eq 8)) }
    if ($X -le 1 -and $lower) { return , @(4, ($X -eq 0)) }
    if ($X -le 1 -and $upper) { return , @(5, ($X -eq 0)) }
    , @(-1, $false)
}

foreach ($digit in $Segments.Keys) {
    $on = $Segments[$digit]
    $name = 'FDLED{0:000}' -f [int][char]$digit
    Save-Picture (Join-Path $ModDir "graphics\$name.png") 9 15 {
        param($x, $y)
        $seg = Get-Segment $x $y
        if ($seg[0] -lt 0) { return $null }
        if ($on[$seg[0]] -eq [char]'1') { if ($seg[1]) { $Lit } else { $Core } } else { $Unlit }
    }
}

Save-Picture (Join-Path $ModDir 'graphics\FDLEDCOL.png') 3 15 {
    param($x, $y)
    if ($x -ge 0 -and $x -le 1 -and (($y -ge 4 -and $y -le 5) -or ($y -ge 9 -and $y -le 10))) { $Lit } else { $null }
}

# ---------------------------------------------------------------------------------------------
# The Font Gallery's pages, menudefs/fonts-demo-*.yaml: a window of samples, each a Label in a
# font (left) and what makes it, in the small font (right), and one item going on to the next
# page. Each row below is @(text, font, color, @(more label params), @(description lines)); a
# row of several samples side by side has an array of them as its first entry.
# ---------------------------------------------------------------------------------------------

$Pages = [ordered]@{
    shadows = @{
        Title = 'Shadows'; Next = @('gradients', 'Next: Gradients', 'G')
        Rows  = @(
            , @('No shadow', 'LargeFont', 'READHCOLOR', @(), @('LargeFont, as it is'))
            , @('Shadow', 'FdShadow', 'READHCOLOR', @(), @('shadow: {}'))
            , @('Deep shadow', 'FdShadowDeep', 'READHCOLOR', @(), @('shadow: { x: 2, y: 2 }'))
            , @('Blue below', 'FdShadowBlue', 'White', @(), @('shadow: { x: 0, y: 2,', '  color: Blue }'))
            , @('Lit from below', 'FdEngraved', 'Black', @(), @('shadow: { x: -1, y: -1,', '  color: White }'))
            , @('Small, shadowed', 'FdShadowSmall', 'White', @(), @('source: SmallFont', 'shadow: {}'))
        )
    }
    gradients = @{
        Title = 'Gradients'; Next = @('outlines', 'Next: Outlines', 'O')
        Rows  = @(
            , @('Gradient', 'FdGradient', 'READHCOLOR', @(), @('gradient: {}', '(top 40, bottom -40)'))
            , @('Strong', 'FdGradientStrong', 'READHCOLOR', @(), @('gradient: { top: 75,', '  bottom: -75 }'))
            , @('Flipped', 'FdGradientFlip', 'READHCOLOR', @(), @('gradient: { top: -60,', '  bottom: 40 }'))
            , @('Shine', 'FdGradientShine', 'Maroon', @(), @('gradient: { top: 80,', '  bottom: 0 }'))
            , @(@(@('Lime', 'FdGradient', 'Lime', 14), @('Blue', 'FdGradient', 'Light Blue', 58), @('Grey', 'FdGradient', 'HIGHLIGHT', 104)),
                $null, $null, @(), @('One font, any color:', 'shades the text''s own'))
        )
    }
    outlines = @{
        Title = 'Outlines'; Next = @('glows', 'Next: Glows', 'L')
        Rows  = @(
            , @('Outline', 'FdOutline', 'READHCOLOR', @(), @('outline: {}'))
            , @('Edged in white', 'FdOutlineWhite', 'Black', @(), @('outline: { color: White }'))
            , @('Thick', 'FdOutlineThick', 'READHCOLOR', @(), @('outline: { thickness: 2 }'))
            , @('Round corners', 'FdOutlineRound', 'READHCOLOR', @(), @('outline: { thickness: 2,', '  diagonals: false }'))
            , @('Small, outlined', 'FdOutlineSmall', 'White', @(), @('source: SmallFont', 'outline: {}'))
        )
    }
    glows = @{
        Title = 'Glows'; Next = @('pictures', 'Next: Picture Fonts', 'P')
        Rows  = @(
            , @('Glow', 'FdGlow', 'READHCOLOR', @(), @('glow: {}', '(radius 2, strength 50)'))
            , @('Yellow glow', 'FdGlowYellow', 'White', @(), @('glow: { color: Yellow }'))
            , @('Wide, bright', 'FdGlowWide', 'White', @(), @('glow: { radius: 4,', '  strength: 80 }'))
            , @('Faint', 'FdGlowFaint', 'HIGHLIGHT', @(), @('glow: { radius: 3,', '  strength: 25 }'))
            , @('Everything', 'FdEverything', 'READHCOLOR', @(), @('shadow, gradient,', 'outline and glow'))
        )
    }
    pictures = @{
        Title = 'Picture Fonts'; Next = @('labels', 'Next: Label Settings', 'B')
        Rows  = @(
            , @('Italic .fnt file', 'FdItalic', 'White', @(), @('type: vga', 'fonts/FDITALIC.fnt'))
            , @('All upper case', 'FdCaps', 'White', @(), @('source: SmallFont', 'upper-case: true'))
            , @('Dot matrix', 'FdDots', 'Lime', @(), @('sheet, colorize,', 'proportional'))
            , @('Gold sheet', 'FdGold', 'White', @(), @('sheet, own colors,', 'transparent: "#FF00FF"'))
            , @(@(@('12:45', 'FdLed', 'White', 14), @('08:30', 'FdLedTinted', 'Light Blue', 84)),
                $null, $null, @(), @('graphic: a picture a', 'character; colorized'))
        )
    }
    labels = @{
        Title = 'Label Settings'; Next = $null
        Rows  = @(
            , @('Shadow', 'LargeFont', 'READHCOLOR', @(, @('shadow', 'true')), @('shadow: true'))
            , @('Shadow, blue', 'LargeFont', 'READHCOLOR', @(@('shadow-x', '3'), @('shadow-color', 'Blue')), @('shadow-x: 3', 'shadow-color: Blue'))
            , @('Shadow off', 'FdShadowDeep', 'READHCOLOR', @(, @('shadow', 'false')), @('font: FdShadowDeep', 'shadow: false'))
            , @('Gradient', 'LargeFont', 'Lime', @(@('gradient-top', '70'), @('gradient-bottom', '-70')), @('gradient-top: 70', 'gradient-bottom: -70'))
            , @('Outline', 'LargeFont', 'Black', @(@('outline-color', 'White'), @('outline-thickness', '2')), @('outline-color: White', 'outline-thickness: 2'))
            , @('Glow', 'LargeFont', 'White', @(@('glow-color', 'Lime'), @('glow-radius', '3')), @('glow-color: Lime', 'glow-radius: 3'))
        )
    }
}

function Format-Label([string]$Text, [int]$X, [int]$Y, [string]$Font, [string]$Color, $More = @(), [string]$Comment = $null) {
    $lines = @()
    if ($Comment) { $lines += "  # $Comment" }
    $lines += '  -'
    $lines += '    type: "Label"'
    $lines += '    params:'
    $lines += "      - text: `"$($Text -replace '"', '\"')`""
    if ($X -ge 0) { $lines += "      - x: $X" } else { $lines += '      - horizontal-orientation: "Center"' }
    $lines += "      - y: $Y"
    $lines += "      - font: `"$Font`""
    $lines += "      - color: `"$Color`""
    foreach ($param in $More) { $lines += "      - $($param[0]): $($param[1])" }
    $lines
}

foreach ($id in $Pages.Keys) {
    $page = $Pages[$id]
    $out = @(
        "# The Font Gallery's $($page.Title) page (menudefs/fonts-demo.yaml opens it). Made by"
        '# examples/tools/make-fonts-demo.ps1: change it there and run it again.'
        '#'
        '# Each sample is a Label in a font from fonts.yaml (left), with what makes it beside it in the'
        '# small font (right). Glows fade into the window''s color (BKGDCOLOR), as text in a menu'
        '# window does.'
    )
    if ($id -eq 'labels') {
        $out += '#'
        $out += '# Here the fonts are plain LargeFont, and each Label gives its own effect (or takes away its'
        $out += '# font''s: shadow: false). Setting any of an effect''s values (shadow-x, glow-color, ...) turns'
        $out += '# it on; the rest are the defaults.'
    }
    $out += 'music: "WONDERIN"'
    $out += 'type: "wolf3d-menu"'
    $out += 'position:'
    $out += '  x: 58'
    $out += '  y: 163'
    $out += 'indent: 24'
    $out += 'components:'
    $out += '  -'
    $out += '    id: "background"'
    $out += '    type: "Background"'
    $out += '    params:'
    $out += '      - color: "BORDCOLOR"'
    $out += '  -'
    $out += '    id: "legend"'
    $out += '    type: "Graphic"'
    $out += '    params:'
    $out += '      - name: "c_mouselback"'
    $out += '      - horizontal-orientation: "Center"'
    $out += '      - y: 184'
    $out += Format-Label $page.Title -1 4 'FdShadowDeep' 'READHCOLOR'
    $out += '  -'
    $out += '    type: "Window"'
    $out += '    params:'
    $out += '      - x: 6'
    $out += '      - y: 22'
    $out += '      - width: 308'
    $out += '      - height: 134'

    $gap = [Math]::Floor(128 / $page.Rows.Count)
    for ($r = 0; $r -lt $page.Rows.Count; $r++) {
        $row = $page.Rows[$r]
        $y = 28 + $r * $gap
        if ($row[0] -is [array]) {
            foreach ($sample in $row[0]) { $out += Format-Label $sample[0] $sample[3] $y $sample[1] $sample[2] }
        }
        else {
            $out += Format-Label $row[0] 14 $y $row[1] $row[2] $row[3]
        }
        $lines = $row[4]
        for ($l = 0; $l -lt $lines.Count; $l++) {
            $out += Format-Label $lines[$l] 160 ($y + 1 + $l * 9) 'SmallFont' 'TEXTCOLOR'
        }
    }

    $out += '  # One item: 13 pixels plus 6'
    $out += '  -'
    $out += '    type: "Window"'
    $out += '    params:'
    $out += '      - x: 50'
    $out += '      - y: 160'
    $out += '      - width: 220'
    $out += '      - height: 19'
    $out += 'menu-items:'
    if ($page.Next) {
        $out += '  -'
        $out += '      id: "next"'
        $out += '      type: "MenuSwitcher"'
        $out += "      text: `"$($page.Next[1])`""
        $out += "      short-key: $($page.Next[2])"
        $out += '      enabled: true'
        $out += '      action: "CP_Menu"'
        $out += "      menu: `"fonts-demo-$($page.Next[0])`""
    }
    else {
        $out += '  # The last page: a text (texts/FDHOWTO.txt) on where it all is'
        $out += '  -'
        $out += '      id: "howto"'
        $out += '      type: "MenuSwitcher"'
        $out += '      text: "How It''s Made"'
        $out += '      short-key: H'
        $out += '      enabled: true'
        $out += '      action: "CP_Article"'
        $out += '      article: "FDHOWTO"'
    }

    $path = Join-Path $ModDir "menudefs\fonts-demo-$id.yaml"
    New-Item -ItemType Directory -Force (Split-Path $path) | Out-Null
    [IO.File]::WriteAllLines($path, [string[]]$out, (New-Object Text.UTF8Encoding $false))
    "Wrote menudefs/fonts-demo-$id.yaml"
}
