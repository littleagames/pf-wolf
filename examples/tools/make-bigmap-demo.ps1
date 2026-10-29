# Builds examples/mods/bigmap-demo: its sky (graphics/BMSKY.png) and its level, a town 128 tiles
# by 96, written straight out as UWMF (maps/MAP01.uwmf): ECWolf's text map format, here in
# PFWolf's own namespace, so things go by their actordefs class names. Run it after changing it:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-bigmap-demo.ps1
#
# The town: streets in a grid between blocks of houses (brick and wood, each a room with a door
# onto the street below it), a plaza with a well at the middle, guards about, treasure in the
# houses, and the way out in the house in the far north-east corner.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'bigmap-demo'

# A clear afternoon sky over the rooftops
Save-Picture "$modDir\graphics\BMSKY.png" 512 128 {
    param($x, $y)
    $a = 2 * [Math]::PI * $x / 512
    $t = $y / 127
    $cloud = [Math]::Sin(4 * $a + 0.05 * $y) + 0.6 * [Math]::Sin(9 * $a + 2 - 0.04 * $y) - 2.2 * [Math]::Abs($t - 0.5)
    if ($cloud -gt 0.4) { return New-Color 245 240 230 }
    New-Color (90 + 110 * $t) (140 + 80 * $t) (220 + 25 * $t)
}

$W = 128; $H = 96
$random = New-Object Random 2026

# What's on each tile: a tile index (-1 open), a sector index and a zone (area) index (-1 none)
$tile = New-Object 'int[,]' $W, $H
$sector = New-Object 'int[,]' $W, $H
$zone = New-Object 'int[,]' $W, $H
for ($y = 0; $y -lt $H; $y++) { for ($x = 0; $x -lt $W; $x++) { $tile[$x, $y] = -1; $sector[$x, $y] = 0; $zone[$x, $y] = 0 } }

# The tiles the planemap names, in order: their textures, and their mapdefs id (PFWolf's own)
$Stone = 0; $Brick = 1; $Wood = 2; $DoorNS = 3; $Elevator = 4
$tileDefs = @(
    @{ N = 'GSTONEA1'; E = 'GSTONEA2'; Id = 1 }
    @{ N = 'BRICK1'; E = 'BRICK2'; Id = 17 }
    @{ N = 'WOOD1'; E = 'WOOD2'; Id = 12 }
    @{ N = 'DOOR1_1'; E = 'SLOT1_2'; Id = 91; Offset = 'offsethorizontal' }
    @{ N = 'ELEV1_1'; E = 'ELEV1_2'; Id = 21 }
)
# Sectors: 0 the street (no flats: the floor color, and the sky above); 1 a house's floor and roof
$Street = 0; $House = 1

$things = New-Object System.Collections.Generic.List[string]
$triggers = New-Object System.Collections.Generic.List[string]
function Add-Thing([double]$X, [double]$Y, [string]$Type, [int]$Angle = 0) {
    $things.Add("thing`n{`n`tx = $X;`n`ty = $Y;`n`tz = 0.0;`n`tangle = $Angle;`n`ttype = `"$Type`";`n`tskill1 = true;`n`tskill2 = true;`n`tskill3 = true;`n`tskill4 = true;`n}")
}

# The town's wall
for ($x = 0; $x -lt $W; $x++) { $tile[$x, 0] = $Stone; $tile[$x, ($H - 1)] = $Stone }
for ($y = 0; $y -lt $H; $y++) { $tile[0, $y] = $Stone; $tile[($W - 1), $y] = $Stone }

# The houses: lots of 12, a 9x9 house in each, its door in the middle of its south wall
$zones = 1
$Lot = 12; $Side = 9
for ($j = 0; $j -lt 7; $j++) {
    for ($i = 0; $i -lt 10; $i++) {
        if ($j -eq 3 -and $i -in 4, 5) { continue }                    # the plaza
        $x0 = 4 + $Lot * $i; $y0 = 4 + $Lot * $j
        $wall = if (($i + $j) % 2 -eq 0) { $Brick } else { $Wood }
        for ($y = $y0; $y -lt $y0 + $Side; $y++) {
            for ($x = $x0; $x -lt $x0 + $Side; $x++) {
                if ($x -eq $x0 -or $y -eq $y0 -or $x -eq $x0 + $Side - 1 -or $y -eq $y0 + $Side - 1) { $tile[$x, $y] = $wall }
                else { $sector[$x, $y] = $House; $zone[$x, $y] = $zones }
            }
        }
        $dx = $x0 + 4; $dy = $y0 + $Side - 1
        $tile[$dx, $dy] = $DoorNS; $zone[$dx, $dy] = -1
        $triggers.Add("trigger`n{`n`tx = $dx;`n`ty = $dy;`n`tz = 0;`n`taction = `"Door_Open`";`n`targ1 = 16;`n`targ2 = 300;`n`tplayeruse = true;`n`tmonsteruse = true;`n`trepeatable = true;`n`tactivateeast = false;`n`tactivatewest = false;`n}")
        # Inside: treasure or food, and now and then a guard
        $cx = $x0 + 1 + $random.Next($Side - 2) + 0.5; $cy = $y0 + 1 + $random.Next($Side - 3) + 0.5
        Add-Thing $cx $cy (@('Cross', 'Chalice', 'Food', 'Clip', 'FirstAidKit')[$random.Next(5)])
        if ($random.Next(3) -eq 0) { Add-Thing ($x0 + 4.5) ($y0 + 2.5) 'Guard' 270 }
        # The house in the far north-east corner holds the way out
        if ($i -eq 9 -and $j -eq 0) {
            $tile[($x0 + 4), $y0] = $Elevator
            $triggers.Add("trigger`n{`n`tx = $($x0 + 4);`n`ty = $y0;`n`tz = 0;`n`taction = `"Exit_Normal`";`n`tplayeruse = true;`n`tactivatenorth = false;`n`tactivateeast = false;`n`tactivatewest = false;`n}")
        }
        $zones++
    }
}

# The plaza's well, guards in the streets, SS here and there, and the player in the south-west
Add-Thing 64.5 46.5 'WellWithWater'
for ($k = 0; $k -lt 18; $k++) {
    $sx = 3 + $Lot * $random.Next(10) + 0.5; $sy = 2 + $random.Next($H - 4) + 0.5
    Add-Thing $sx $sy $(if ($k % 3 -eq 0) { 'SS' } else { 'Guard' }) (90 * $random.Next(4))
}
Add-Thing 2.5 93.5 '$Player1Start' 0

# Write it out
$sb = New-Object System.Text.StringBuilder
[void]$sb.Append("namespace = `"PFWolf`";`nname = `"Big Map Demo`";`ntilesize = 64;`nwidth = $W;`nheight = $H;`n`n")
foreach ($t in $tileDefs) {
    [void]$sb.Append("tile`n{`n`ttexturenorth = `"$($t.N)`";`n`ttexturesouth = `"$($t.N)`";`n`ttextureeast = `"$($t.E)`";`n`ttexturewest = `"$($t.E)`";`n")
    if ($t.Offset) { [void]$sb.Append("`t$($t.Offset) = true;`n") }
    [void]$sb.Append("`tid = $($t.Id);`n}`n")
}
[void]$sb.Append("sector`n{`n}`n")
[void]$sb.Append("sector`n{`n`ttexturefloor = `"GSTONEA1`";`n`ttextureceiling = `"WOOD1`";`n}`n")
for ($z = 0; $z -lt $zones; $z++) { [void]$sb.Append("zone`n{`n}`n") }
[void]$sb.Append("plane`n{`n`tdepth = 64;`n}`nplanemap`n{`n")
for ($y = 0; $y -lt $H; $y++) {
    $cells = for ($x = 0; $x -lt $W; $x++) {
        $z = if ($tile[$x, $y] -ge 0 -and $tile[$x, $y] -ne $DoorNS) { -1 } else { $zone[$x, $y] }
        "{$($tile[$x, $y]),$($sector[$x, $y]),$z}"
    }
    [void]$sb.Append("`t" + ($cells -join ',') + $(if ($y -lt $H - 1) { ",`n" } else { "`n" }))
}
[void]$sb.Append("}`n")
foreach ($t in $things) { [void]$sb.Append("$t`n") }
foreach ($t in $triggers) { [void]$sb.Append("$t`n") }
New-Item -ItemType Directory -Force "$modDir\maps" | Out-Null
[IO.File]::WriteAllText("$modDir\maps\MAP01.uwmf", $sb.ToString().Replace("`n", "`r`n"), (New-Object Text.UTF8Encoding $false))
"Wrote $modDir\maps\MAP01.uwmf ($($sb.Length) characters, $($things.Count) things, $zones zones)"
