# Builds examples/mods/animated-title: every picture of its title screens (graphics/AT*.png) and
# the animations made of them (animdefs/animated-title.yaml). Run it after changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-animated-title.ps1
#
# Two screens, both in the mod's game-info.yaml:
#   - the intro: a spotlight finds the logo on a black screen, opens out and flashes. One
#     full-screen animation of 48 frames that plays once and holds its last.
#   - the title: layers drawn in order, five of them animated: searchlights sweeping a night sky
#     (64 frames), the logo, the castle wall in front, two banners in the wind, two torches and a
#     blinking "press a key".
# Colors snap to Wolf3D's palette as the game loads the pictures, so smooth shades come out banded.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'animated-title' @('graphics', 'animdefs')
$gfx = "$modDir\graphics"

# Old frames go first, so a shorter animation doesn't leave frames behind
Get-ChildItem $gfx -Filter 'AT*.png' -ErrorAction SilentlyContinue | Remove-Item

function New-ArgbBrush([int]$A, $Rgb) { New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb($A, $Rgb[0], $Rgb[1], $Rgb[2])) }

# Text drawn hard-edged (no anti-aliasing), so it snaps cleanly to the palette
function Add-Text([Drawing.Graphics]$G, [string]$Text, [Drawing.Font]$Font, [Drawing.Brush]$Brush, [double]$X, [double]$Y, [double]$W, [double]$H) {
    $G.TextRenderingHint = [Drawing.Text.TextRenderingHint]::SingleBitPerPixelGridFit
    $fmt = New-Object Drawing.StringFormat
    $fmt.Alignment = [Drawing.StringAlignment]::Center
    $G.DrawString($Text, $Font, $Brush, (New-Object Drawing.RectangleF ([single]$X), ([single]$Y), ([single]$W), ([single]$H)), $fmt)
}

$Gold = @(250, 200, 60)
$wrote = 0

#
# THE LOGO: "PFWOLF" in gold over a dark red shadow, and a line under it. Kept as a bitmap too,
# for the intro to draw through its spotlight.
#
$LogoW = 320; $LogoH = 72
function Draw-Logo([Drawing.Graphics]$G) {
    $big = New-Object Drawing.Font 'Arial Black', 52, ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel)
    $small = New-Object Drawing.Font 'Arial', 11, ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel)
    Add-Text $G 'PFWOLF' $big (New-Brush @(70, 0, 0)) 3 3 320 64
    $face = New-Object Drawing.Drawing2D.LinearGradientBrush (New-Object Drawing.Point 0, 8), (New-Object Drawing.Point 0, 62),
        ([Drawing.Color]::FromArgb(255, 255, 236, 120)), ([Drawing.Color]::FromArgb(255, 220, 120, 20))
    Add-Text $G 'PFWOLF' $big $face 0 0 320 64
    Add-Text $G 'A N I M A T E D   T I T L E' $small (New-Brush @(30, 10, 0)) 1 59 320 14
    Add-Text $G 'A N I M A T E D   T I T L E' $small (New-Brush @(230, 220, 200)) 0 58 320 14
}
Save-Drawing "$gfx\ATLOGO.png" $LogoW $LogoH { param($g) Draw-Logo $g } | Out-Null
$wrote++
$logo = [Drawing.Bitmap]::FromFile("$gfx\ATLOGO.png")

# The logo all white, for the intro's flash
$whiteLogo = New-Object Drawing.Bitmap $LogoW, $LogoH
$wg = [Drawing.Graphics]::FromImage($whiteLogo)
$matrix = New-Object Drawing.Imaging.ColorMatrix
$matrix.Matrix00 = 0; $matrix.Matrix11 = 0; $matrix.Matrix22 = 0
$matrix.Matrix40 = 1; $matrix.Matrix41 = 1; $matrix.Matrix42 = 1
$attrs = New-Object Drawing.Imaging.ImageAttributes
$attrs.SetColorMatrix($matrix)
$wg.DrawImage($logo, (New-Object Drawing.Rectangle 0, 0, $LogoW, $LogoH), 0, 0, $LogoW, $LogoH, [Drawing.GraphicsUnit]::Pixel, $attrs)
$wg.Dispose()

#
# THE INTRO (ATREV01-48): black, a spotlight crossing it shows the logo a piece at a time, comes
# back to the middle and opens out over the whole of it, then a white flash.
#
$RevealFrames = 48
$LogoY = 64
for ($f = 0; $f -lt $RevealFrames; $f++) {
    Save-Drawing ("$gfx\ATREV{0:D2}.png" -f ($f + 1)) 320 200 {
        param($g)
        Add-Rect $g @(0, 0, 0) 0 0 320 200
        $light = 1.0   # how bright the spotlight's pool is
        if ($f -lt 24) {
            # Across, left to right, bobbing
            $t = $f / 23
            $cx = -30 + 380 * $t; $cy = 100 + 14 * [Math]::Sin($t * 2 * [Math]::PI); $r = 36
        }
        elseif ($f -lt 36) {
            # Back to the middle and opening out, its pool dimming as it spreads
            $t = ($f - 24) / 11
            $cx = 160; $cy = 100; $r = 36 + 180 * $t * $t; $light = 1 - $t
        }
        else {
            $cx = 160; $cy = 100; $r = 400; $light = 0
        }
        $path = New-Object Drawing.Drawing2D.GraphicsPath
        $path.AddEllipse([single]($cx - $r), [single]($cy - $r), [single](2 * $r), [single](2 * $r))
        if ($light -gt 0) {
            Add-Ellipse $g @([int](36 * $light), [int](36 * $light), [int](44 * $light)) ($cx - $r) ($cy - $r) (2 * $r) (2 * $r)
            Add-Ellipse $g @([int](56 * $light), [int](56 * $light), [int](64 * $light)) ($cx - $r * 0.6) ($cy - $r * 0.6) (1.2 * $r) (1.2 * $r)
        }
        $g.SetClip($path)
        $g.DrawImage($(if ($f -eq 36 -or $f -eq 37) { $whiteLogo } else { $logo }), 0, $LogoY, $LogoW, $LogoH)
        $g.ResetClip()
        # The flash's ring, spreading out from the logo
        if ($f -ge 36 -and $f -lt 44) {
            $ring = 54 + ($f - 36) * 20
            $pen = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(255, 255, 240, 160 - ($f - 36) * 15)), 2
            $g.DrawEllipse($pen, [single](160 - $ring * 1.6), [single](100 - $ring * 0.6), [single]($ring * 3.2), [single]($ring * 1.2))
        }
    } | Out-Null
    $wrote++
}

#
# THE SKY (ATSKY01-64, 320x110): night, stars, and two searchlights from behind the wall,
# sweeping out of step with each other. Their beams are see-through, so where they cross is
# brighter. Drawn opaque: it's the back layer.
#
$SkyFrames = 64
$sky = New-Object Drawing.Bitmap 320, 110
$sg = [Drawing.Graphics]::FromImage($sky)
for ($y = 0; $y -lt 110; $y++) {
    $t = $y / 109
    # Little red in it: Wolf3D's palette has purples nearer than blues to a reddish navy
    $sg.FillRectangle((New-Brush @([int](4 + 8 * $t), [int](4 + 24 * $t), [int](36 + 56 * $t))), 0, $y, 320, 1)
}
$rand = New-Object Random 7
for ($i = 0; $i -lt 70; $i++) {
    $c = 150 + $rand.Next(105)
    $sg.FillRectangle((New-Brush @($c, $c, $c)), $rand.Next(320), $rand.Next(96), 1, 1)
}
$sg.Dispose()

function Add-Beam([Drawing.Graphics]$G, [double]$BaseX, [double]$Angle) {
    foreach ($beam in @(@{ Spread = 0.1; Alpha = 55 }, @{ Spread = 0.04; Alpha = 70 })) {
        $len = 300
        $l = $Angle - $beam.Spread; $r = $Angle + $beam.Spread
        $pts = [Drawing.PointF[]]@(
            (New-Object Drawing.PointF ([single]($BaseX - 2)), 116),
            (New-Object Drawing.PointF ([single]($BaseX + 2)), 116),
            (New-Object Drawing.PointF ([single]($BaseX + $len * [Math]::Sin($r))), ([single](116 - $len * [Math]::Cos($r)))),
            (New-Object Drawing.PointF ([single]($BaseX + $len * [Math]::Sin($l))), ([single](116 - $len * [Math]::Cos($l)))))
        $G.FillPolygon((New-ArgbBrush $beam.Alpha @(235, 235, 190)), $pts)
    }
}

for ($f = 0; $f -lt $SkyFrames; $f++) {
    Save-Drawing ("$gfx\ATSKY{0:D2}.png" -f ($f + 1)) 320 110 {
        param($g)
        $g.DrawImage($sky, 0, 0, 320, 110)
        $phase = 2 * [Math]::PI * $f / $SkyFrames
        Add-Beam $g 70 (0.6 * [Math]::Sin($phase))
        Add-Beam $g 250 (0.6 * [Math]::Sin($phase + 2.2))
    } | Out-Null
    $wrote++
}

#
# THE WALL (ATWALL, 320x100, drawn at y 100): battlements along its top (the gaps between them
# are see-through, so the sky shows), stone blocks, a gate with its portcullis down, and the
# torches' brackets.
#
Save-Drawing "$gfx\ATWALL.png" 320 100 {
    param($g)
    $rand = New-Object Random 3
    for ($x = 0; $x -lt 320; $x += 28) { Add-Rect $g @(78, 78, 82) $x 0 16 12 }
    Add-Rect $g @(78, 78, 82) 0 10 320 90
    for ($row = 0; $row -lt 9; $row++) {
        $y = 12 + $row * 10
        $shift = if ($row % 2) { 10 } else { 0 }
        for ($x = -$shift; $x -lt 320; $x += 20) {
            $s = 68 + $rand.Next(22)
            Add-Rect $g @($s, $s, ($s + 4)) ($x + 1) ($y + 1) 18 8
        }
    }
    # The gate: an arch, dark inside, the portcullis's bars in front
    Add-Rect $g @(52, 50, 54) 128 48 64 52
    Add-Ellipse $g @(52, 50, 54) 128 30 64 40
    Add-Rect $g @(18, 12, 8) 134 52 52 48
    Add-Ellipse $g @(18, 12, 8) 134 36 52 34
    for ($x = 138; $x -lt 186; $x += 8) { Add-Rect $g @(60, 56, 52) $x 40 2 60 }
    foreach ($y in 56, 72, 88) { Add-Rect $g @(60, 56, 52) 134 $y 52 2 }
    # The torches' brackets, under where their flames are drawn
    foreach ($x in 109, 205) {
        Add-Rect $g @(60, 38, 18) ($x + 1) 44 4 12
        Add-Rect $g @(100, 66, 30) ($x - 2) 42 10 4
    }
} | Out-Null
$wrote++

#
# THE BANNERS (ATBAN01-12, 24x64): red with a gold diamond, hanging from a gold pole, a wave
# running down them from the left, each column shaded by its slope.
#
$BannerFrames = 12
for ($f = 0; $f -lt $BannerFrames; $f++) {
    Save-Drawing ("$gfx\ATBAN{0:D2}.png" -f ($f + 1)) 24 64 {
        param($g)
        Add-Rect $g $Gold 0 0 24 3
        for ($x = 0; $x -lt 24; $x++) {
            $a = 2 * [Math]::PI * $f / $BannerFrames - $x * 0.4
            $dy = [Math]::Round(1.6 * [Math]::Sin($a))
            $shade = 0.72 + 0.28 * [Math]::Cos($a)
            $notch = [Math]::Max(0, 5 - [Math]::Abs($x - 11.5)) * 1.4   # the swallowtail
            $bottom = 60 + $dy - $notch
            Add-Rect $g @([int](175 * $shade), [int](24 * $shade), [int](20 * $shade)) $x 3 1 ($bottom - 3)
            $half = 8 - [Math]::Abs($x - 11.5) * 1.3
            if ($half -gt 0) {
                Add-Rect $g @([int]($Gold[0] * $shade), [int]($Gold[1] * $shade), [int]($Gold[2] * $shade)) $x (28 + $dy - $half) 1 (2 * $half)
            }
        }
    } | Out-Null
    $wrote++
}

#
# THE TORCH FLAMES (ATFLAME1-8, 14x22): a red-orange flame round a yellow heart, its tip
# swaying and its height flickering, now and then a spark.
#
$FlameFrames = 8
$rand = New-Object Random 11
for ($f = 0; $f -lt $FlameFrames; $f++) {
    $tip = 7 + 3 * [Math]::Sin(2 * [Math]::PI * $f / $FlameFrames) + ($rand.NextDouble() - 0.5) * 2
    # (Not $height: the drawing runs inside Save-Drawing, whose $Height would be found first)
    $flameH = 15 + $rand.Next(6)
    $spark = $rand.Next(3) -eq 0
    Save-Drawing ("$gfx\ATFLAME{0}.png" -f ($f + 1)) 14 22 {
        param($g)
        Add-Poly $g @(200, 50, 10) @(2, 21, 12, 21, 12, 15, $tip, (21 - $flameH), 2, 15)
        Add-Poly $g @(250, 140, 20) @(3, 21, 11, 21, 10, 16, (($tip + 7) / 2), (21 - $flameH * 0.75), 4, 16)
        Add-Poly $g @(255, 240, 120) @(5, 21, 9, 21, 9, 18, (($tip + 14) / 3), (21 - $flameH * 0.45), 5, 18)
        if ($spark) { Add-Rect $g @(255, 220, 80) ($tip + 2) 1 1 1 }
    } | Out-Null
    $wrote++
}

#
# PRESS A KEY (ATKEY) and an empty picture (ATBLANK) to blink it with
#
Save-Drawing "$gfx\ATKEY.png" 120 12 {
    param($g)
    $font = New-Object Drawing.Font 'Arial', 10, ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel)
    Add-Text $g 'PRESS A KEY' $font (New-Brush @(20, 10, 0)) 1 1 120 12
    Add-Text $g 'PRESS A KEY' $font (New-Brush @(255, 230, 120)) 0 0 120 12
} | Out-Null
Save-Drawing "$gfx\ATBLANK.png" 1 1 { param($g) } | Out-Null
$wrote += 2

$logo.Dispose(); $whiteLogo.Dispose(); $sky.Dispose()
"Wrote $wrote pictures to $gfx"

#
# THE ANIMATIONS
#
function Frame-List([string]$Format, [int]$Count, [int[]]$Order = $null) {
    if (!$Order) { $Order = 1..$Count }
    ($Order | ForEach-Object { $Format -f $_ }) -join ', '
}

$Torch2Order = @(5, 2, 7, 4, 1, 6, 3, 8)
$yaml = @"
# Written by examples/tools/make-animated-title.ps1: change it there and run it again. The fields
# are in the animdefs reference: https://www.littleagames.com/pf-wolf/docs/latest/reference/animdefs
#
# Each animation is a picture name the title screens draw (game-info.yaml's title-sequence), shown
# a frame at a time, `tics` each (70 a second). On a title screen they start from their first
# frame as the screen finishes fading in.

# The intro: $RevealFrames frames, 2 tics each, then `stop`: it plays once and holds the last frame.
# Its screen has until-animation-ends, so it stays up until this has played.
ATREVEAL:
  tics: 2
  frames: [$(Frame-List 'ATREV{0:D2}' $RevealFrames), stop]

# The title's sky: $SkyFrames frames, the searchlights' sweep there and back, round and round
ATSKY:
  tics: 3
  frames: [$(Frame-List 'ATSKY{0:D2}' $SkyFrames)]

# Both banners show this, so they wave together
ATBANNER:
  tics: 5
  frames: [$(Frame-List 'ATBAN{0:D2}' $BannerFrames)]

# The torches share their frames but not their order or speed, so they don't flicker in step
ATTORCH1:
  tics: 5
  frames: [$(Frame-List 'ATFLAME{0}' $FlameFrames)]
ATTORCH2:
  tics: 4
  frames: [$(Frame-List 'ATFLAME{0}' $FlameFrames $Torch2Order)]

# "Press a key": on for 40 tics, off (an empty picture) for 25. A frame can give its own tics.
ATPRESS:
  frames:
    - { name: ATKEY, tics: 40 }
    - { name: ATBLANK, tics: 25 }
"@
$animdefs = "$modDir\animdefs\animated-title.yaml"
[IO.File]::WriteAllText($animdefs, $yaml.Replace("`r`n", "`n") + "`n")
"Wrote $animdefs"
