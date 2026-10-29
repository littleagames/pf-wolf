# Builds examples/mods/attract-demo: its title card (graphics/AMCARD.png), its showcase level
# (maps/MAP64.wad: a map number Wolf3D doesn't use, so it's in no one's way) and the demo of it
# the title loop plays (demos/DEMO0.dmo). Run it after changing any of them:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-attract-demo.ps1
#
# The demo isn't recorded: it's written here, as id's demo format is only the player's input,
# a frame at a time (4 tics, 1/17.5 of a second): the buttons held, how far they turn and how far
# forward they move. The engine plays it back exactly, the same every time. Change the steps and
# check them with `demotrace` (see the README).

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'attract-demo'

# The title card: "PFWolf" over a dark red field with a stripe, 320x200
Save-Drawing "$modDir\graphics\AMCARD.png" 320 200 {
    param($g)
    Add-Rect $g @(40, 0, 0) 0 0 320 200
    Add-Rect $g @(140, 0, 0) 0 70 320 60
    Add-Rect $g @(220, 180, 60) 0 68 320 2
    Add-Rect $g @(220, 180, 60) 0 130 320 2
    $font = New-Object Drawing.Font 'Arial Black', 34, ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel)
    $g.TextRenderingHint = [Drawing.Text.TextRenderingHint]::SingleBitPerPixelGridFit
    $fmt = New-Object Drawing.StringFormat
    $fmt.Alignment = [Drawing.StringAlignment]::Center
    $g.DrawString('PFWOLF', $font, (New-Brush @(20, 0, 0)), (New-Object Drawing.RectangleF 3, 77, 320, 50), $fmt)
    $g.DrawString('PFWOLF', $font, (New-Brush @(255, 230, 120)), (New-Object Drawing.RectangleF 0, 74, 320, 50), $fmt)
    $small = New-Object Drawing.Font 'Arial', 11, ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel)
    $g.DrawString('THE EXAMPLE MODS', $small, (New-Brush @(220, 200, 160)), (New-Object Drawing.RectangleF 0, 112, 320, 16), $fmt)
    $g.DrawString('press a key', $small, (New-Brush @(150, 120, 100)), (New-Object Drawing.RectangleF 0, 170, 320, 16), $fmt)
}

# The showcase: a corridor north from where the demo starts, two guards in it with their backs
# turned, a door at its end and a treasure room beyond, the elevator in its far wall
$Layout = @(
    '###########'   # y=20
    '###tXt#####'   #      the treasure room, its elevator
    '###tct#####'
    '###tTt#####'
    '####D######'   #      the door
    '###...#####'   # y=25
    '###...#####'
    '###.g.#####'   #      a guard, back turned
    '###...#####'
    '###...#####'
    '###...#####'   # y=30
    '###.g.#####'   #      another
    '###...#####'
    '###...#####'
    '###...#####'
    '###.P.#####'   # y=35 where the demo starts, facing north
    '###########'
)
$Tiles = [Collections.Hashtable]::new([StringComparer]::Ordinal)
$Tiles['#'] = @{ Plane0 = 8 }
$Tiles['.'] = @{ Plane0 = 108 }
$Tiles['P'] = @{ Plane0 = 108; Plane1 = 19 }
$Tiles['g'] = @{ Plane0 = 108; Plane1 = 109 }          # a guard facing north
$Tiles['t'] = @{ Plane0 = 109 }
$Tiles['c'] = @{ Plane0 = 109; Plane1 = 55 }           # a crown
$Tiles['T'] = @{ Plane0 = 109; Plane1 = 54 }           # a chest
$Tiles['D'] = @{ Plane0 = 91 }
$Tiles['X'] = @{ Plane0 = 21 }
$planes = New-MapPlanes -Fill 8
Add-MapLayout $planes $Layout $Tiles 26 20
Save-MapWad "$modDir\maps\MAP64.wad" 'MAP64' 'Attract Demo' $planes

# The demo: each step is @(frames, buttons, turn, forward), with a comma before it so the list
# keeps them as steps rather than running them together. Buttons: 1 fire, 2 strafe, 4 run,
# 8 use. Turn is negative to the left; forward negative is ahead (id's convention).
$Fire = 1; $Use = 8
$Steps = @(
    ,@(10, 0, 0, 0)            # stand a moment
    ,@(22, 0, 0, -50)          # walk up the corridor
    ,@(2, $Fire, 0, 0)         # shoot the first guard...
    ,@(8, 0, 0, 0)
    ,@(2, $Fire, 0, 0)
    ,@(8, 0, 0, 0)
    ,@(2, $Fire, 0, 0)
    ,@(10, 0, 0, 0)
    ,@(30, 0, 0, -50)          # on past him
    ,@(2, $Fire, 0, 0)         # the second guard
    ,@(8, 0, 0, 0)
    ,@(2, $Fire, 0, 0)
    ,@(8, 0, 0, 0)
    ,@(2, $Fire, 0, 0)
    ,@(10, 0, 0, 0)
    ,@(30, 0, 0, -50)          # to the door
    ,@(2, $Use, 0, 0)          # open it
    ,@(16, 0, 0, 0)
    ,@(24, 0, 0, -50)          # into the treasure room, over the chest and the crown
    ,@(20, 0, 0, 0)
)
$data = New-Object System.Collections.Generic.List[byte]
$data.Add(63)                # the map: MAP64 (0-based)
$data.Add(0); $data.Add(0); $data.Add(0)
foreach ($s in $Steps) {
    for ($i = 0; $i -lt $s[0]; $i++) {
        $data.Add([byte]$s[1]); $data.Add([byte]([sbyte]$s[2] -band 0xFF)); $data.Add([byte]([int]$s[3] -band 0xFF))
    }
}
$bytes = $data.ToArray()
[BitConverter]::GetBytes([int16]$bytes.Length).CopyTo($bytes, 1)
New-Item -ItemType Directory -Force "$modDir\demos" | Out-Null
[IO.File]::WriteAllBytes("$modDir\demos\DEMO0.dmo", $bytes)
"Wrote $modDir\demos\DEMO0.dmo ($(($bytes.Length - 4) / 3) frames)"
