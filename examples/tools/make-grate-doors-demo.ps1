# Builds examples/mods/grate-doors-demo's door pictures (textures/*.png) from the drawing code
# below. Run it after changing them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-grate-doors-demo.ps1
#
# The pictures are drawn on a clear background: what's clear is see-through, so a shut door
# with them is seen through (Rendering/SeeThroughPosts.cs). Colors snap to the nearest palette
# color as the game loads them.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$modDir = Join-Path $PSScriptRoot '..\mods\grate-doors-demo'
$texturesDir = Join-Path $modDir 'textures'
New-Item -ItemType Directory -Force $texturesDir | Out-Null

function Brush([int]$r, [int]$g, [int]$b) { New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, $r, $g, $b)) }

# Draws a 64x64 picture on a clear background, with hard edges (no half see-through pixels), and saves it
function Save-Texture([string]$Name, [scriptblock]$Paint) {
    $bitmap = New-Object Drawing.Bitmap 64, 64
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::None
    $g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.Clear([Drawing.Color]::Transparent)
    & $Paint $g
    $g.Dispose()
    $path = Join-Path (Resolve-Path $texturesDir) "$Name.png"
    $bitmap.Save($path, [Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    "Wrote $path"
}

# A barred gate: a frame round the edge, upright bars and two crossbars, with rivets where they
# cross. The _1 picture is the lighter one (east-west doors, seen from north or south) and _2 the
# darker one (north-south doors), as Wolfenstein 3D's door pairs are.
foreach ($gate in @(@('GRATE1', 1.0), @('GRATE2', 0.8))) {
    $shade = $gate[1]
    Save-Texture $gate[0] {
        param($g)
        $dark = Brush ([int](64 * $shade)) ([int](64 * $shade)) ([int](72 * $shade))
        $metal = Brush ([int](128 * $shade)) ([int](128 * $shade)) ([int](140 * $shade))
        $light = Brush ([int](184 * $shade)) ([int](184 * $shade)) ([int](196 * $shade))

        # the frame
        $g.FillRectangle($dark, 0, 0, 64, 4)
        $g.FillRectangle($dark, 0, 60, 64, 4)
        $g.FillRectangle($dark, 0, 0, 3, 64)
        $g.FillRectangle($dark, 61, 0, 3, 64)
        $g.FillRectangle($metal, 0, 1, 64, 2)
        $g.FillRectangle($metal, 0, 61, 64, 2)

        # upright bars, lit down their left edge
        foreach ($x in 9, 18, 27, 36, 45, 54) {
            $g.FillRectangle($dark, $x, 4, 3, 56)
            $g.FillRectangle($metal, $x, 4, 2, 56)
            $g.FillRectangle($light, $x, 4, 1, 56)
        }

        # crossbars, and a rivet where each meets an upright
        foreach ($y in 20, 42) {
            $g.FillRectangle($dark, 3, $y, 58, 3)
            $g.FillRectangle($metal, 3, $y, 58, 2)
            foreach ($x in 9, 18, 27, 36, 45, 54) { $g.FillRectangle($light, $x, $y, 2, 2) }
        }
    }
}
