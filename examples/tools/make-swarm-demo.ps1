# Builds examples/mods/swarm-demo: a stress test, three levels packed with far more enemies than
# Wolf3D ever allowed (its limit was 150 actors), written straight out as UWMF (maps/MAPnn.uwmf)
# in PFWolf's own namespace. Purely for fun: open the door and see how the engine copes.
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/make-swarm-demo.ps1
#
# Each level is one huge open field (a single zone, so one shot wakes the lot) with stone pillars
# for cover, and a small fort at the middle of its west wall where the player starts, its door
# onto the field. The way out is an elevator in the middle of the east wall, on the far side of
# the army. The player starts with a chain gun and 999 bullets (actordefs/swarm.yaml).

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'example-mod-tools.ps1')
$modDir = Get-ModDir 'swarm-demo'

# The tiles the planemaps name, in order: their textures, and their mapdefs id (PFWolf's own)
$Stone = 0; $Blue = 1; $DoorEW = 2; $Elevator = 3
$tileDefs = @(
    @{ N = 'GSTONEA1'; E = 'GSTONEA2'; Id = 1 }
    @{ N = 'BSTONEA1'; E = 'BSTONEA2'; Id = 8 }
    @{ N = 'SLOT1_1'; E = 'DOOR1_2'; Id = 90; Offset = 'offsetvertical' }
    @{ N = 'ELEV1_1'; E = 'ELEV1_2'; Id = 21 }
)

# Who's in the army, and how often (out of 100)
$mix = @(
    @{ Type = 'Guard'; Weight = 55 }
    @{ Type = 'SS'; Weight = 20 }
    @{ Type = 'Dog'; Weight = 12 }
    @{ Type = 'Officer'; Weight = 9 }
    @{ Type = 'Mutant'; Weight = 4 }
)

function Get-Soldier([Random]$Random) {
    $roll = $Random.Next(100)
    foreach ($m in $mix) { if ($roll -lt $m.Weight) { return $m.Type }; $roll -= $m.Weight }
    'Guard'
}

# One level: a field $Size tiles square holding $Count enemies (or as many as fit)
function New-SwarmLevel([string]$MapName, [string]$Title, [int]$Size, [int]$Count, [int]$Seed) {
    $random = New-Object Random $Seed
    $tile = New-Object 'int[,]' $Size, $Size
    $zone = New-Object 'int[,]' $Size, $Size
    for ($y = 0; $y -lt $Size; $y++) { for ($x = 0; $x -lt $Size; $x++) { $tile[$x, $y] = -1; $zone[$x, $y] = 0 } }
    $things = New-Object System.Collections.Generic.List[string]
    $triggers = New-Object System.Collections.Generic.List[string]
    $taken = New-Object 'bool[,]' $Size, $Size
    function Add-Thing([double]$X, [double]$Y, [string]$Type, [int]$Angle = 0) {
        # One line each (z is 0 when left out): 20,000 of these make a big file as it is
        $things.Add("thing { x = $X; y = $Y; angle = $Angle; type = `"$Type`"; skill1 = true; skill2 = true; skill3 = true; skill4 = true; }")
    }

    # The outer wall
    for ($i = 0; $i -lt $Size; $i++) {
        $tile[$i, 0] = $Stone; $tile[$i, ($Size - 1)] = $Stone
        $tile[0, $i] = $Stone; $tile[($Size - 1), $i] = $Stone
    }

    # The fort: a 9x9 blue-stone room at the middle of the west wall (zone 1), its door east
    $mid = [int]($Size / 2)
    $fx0 = 1; $fx1 = 9; $fy0 = $mid - 4; $fy1 = $mid + 4
    for ($y = $fy0; $y -le $fy1; $y++) {
        for ($x = $fx0; $x -le $fx1; $x++) {
            if ($x -eq $fx1 -or $y -eq $fy0 -or $y -eq $fy1) { $tile[$x, $y] = $Blue }
            else { $zone[$x, $y] = 1 }
            $taken[$x, $y] = $true
        }
    }
    $tile[$fx1, $mid] = $DoorEW; $zone[$fx1, $mid] = -1
    $triggers.Add("trigger { x = $fx1; y = $mid; z = 0; action = `"Door_Open`"; arg1 = 16; arg2 = 300; playeruse = true; monsteruse = true; repeatable = true; activatenorth = false; activatesouth = false; }")
    # Keep a little room clear outside the door, so it can open onto the field
    for ($y = $mid - 1; $y -le $mid + 1; $y++) { for ($x = $fx1 + 1; $x -le $fx1 + 2; $x++) { $taken[$x, $y] = $true } }

    # Inside: the player, and supplies for the fight
    Add-Thing 2.5 ($mid + 0.5) '$Player1Start' 0
    foreach ($p in @(@(3, -3), @(5, -3), @(7, -3), @(3, 3), @(5, 3), @(7, 3))) {
        Add-Thing ($p[0] + 0.5) ($mid + $p[1] + 0.5) $(if ($p[1] -lt 0) { 'ClipBox' } else { 'FirstAidKit' })
    }
    Add-Thing 7.5 ($mid + 0.5) 'FloorLamp'

    # The way out: an elevator in the middle of the east wall, a small clearing in front of it
    $tile[($Size - 1), $mid] = $Elevator
    $triggers.Add("trigger { x = $($Size - 1); y = $mid; z = 0; action = `"Exit_Normal`"; playeruse = true; activatenorth = false; activateeast = false; activatesouth = false; }")
    for ($y = $mid - 1; $y -le $mid + 1; $y++) { $taken[($Size - 2), $y] = $true }

    # Pillars for cover, scattered over the field, about one tile in sixty
    $pillars = [int](($Size - 2) * ($Size - 2) / 60)
    for ($k = 0; $k -lt $pillars; $k++) {
        $px = 12 + $random.Next($Size - 15); $py = 2 + $random.Next($Size - 4)
        if ($taken[$px, $py]) { continue }
        $tile[$px, $py] = $Stone; $taken[$px, $py] = $true
    }

    # The army: one to a tile, anywhere open on the field, facing every which way
    $open = New-Object System.Collections.Generic.List[int]
    for ($y = 1; $y -lt $Size - 1; $y++) { for ($x = 11; $x -lt $Size - 1; $x++) { if (-not $taken[$x, $y]) { $open.Add($y * $Size + $x) } } }
    for ($i = $open.Count - 1; $i -gt 0; $i--) { $j = $random.Next($i + 1); $t = $open[$i]; $open[$i] = $open[$j]; $open[$j] = $t }
    $placed = [Math]::Min($Count, $open.Count)
    $tally = @{}
    for ($i = 0; $i -lt $placed; $i++) {
        $x = $open[$i] % $Size; $y = [int][Math]::Floor($open[$i] / $Size)
        $type = Get-Soldier $random
        $tally[$type] = 1 + [int]$tally[$type]
        Add-Thing ($x + 0.5) ($y + 0.5) $type (90 * $random.Next(4))
    }
    # A few lamps out on the field (statics, so the renderer has those to sort too)
    for ($k = 0; $k -lt [int]($Size / 4); $k++) {
        $i = $placed + $k
        if ($i -ge $open.Count) { break }
        Add-Thing ($open[$i] % $Size + 0.5) ([Math]::Floor($open[$i] / $Size) + 0.5) 'FloorLamp'
    }

    # Write it out
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append("namespace = `"PFWolf`";`nname = `"$Title`";`ntilesize = 64;`nwidth = $Size;`nheight = $Size;`n`n")
    foreach ($t in $tileDefs) {
        [void]$sb.Append("tile`n{`n`ttexturenorth = `"$($t.N)`";`n`ttexturesouth = `"$($t.N)`";`n`ttextureeast = `"$($t.E)`";`n`ttexturewest = `"$($t.E)`";`n")
        if ($t.Offset) { [void]$sb.Append("`t$($t.Offset) = true;`n") }
        [void]$sb.Append("`tid = $($t.Id);`n}`n")
    }
    [void]$sb.Append("sector`n{`n}`n")
    [void]$sb.Append("zone`n{`n}`nzone`n{`n}`n")
    [void]$sb.Append("plane`n{`n`tdepth = 64;`n}`nplanemap`n{`n")
    for ($y = 0; $y -lt $Size; $y++) {
        $cells = for ($x = 0; $x -lt $Size; $x++) {
            $z = if ($tile[$x, $y] -ge 0 -and $tile[$x, $y] -ne $DoorEW) { -1 } else { $zone[$x, $y] }
            "{$($tile[$x, $y]),0,$z}"
        }
        [void]$sb.Append("`t" + ($cells -join ',') + $(if ($y -lt $Size - 1) { ",`n" } else { "`n" }))
    }
    [void]$sb.Append("}`n")
    foreach ($t in $things) { [void]$sb.Append("$t`n") }
    foreach ($t in $triggers) { [void]$sb.Append("$t`n") }
    New-Item -ItemType Directory -Force "$modDir\maps" | Out-Null
    [IO.File]::WriteAllText("$modDir\maps\$MapName.uwmf", $sb.ToString().Replace("`n", "`r`n"), (New-Object Text.UTF8Encoding $false))
    $mixText = ($mix | ForEach-Object { "$([int]$tally[$_.Type]) $($_.Type)" }) -join ', '
    "Wrote $modDir\maps\$MapName.uwmf: ${Size}x$Size, $placed enemies ($mixText)"
}

New-SwarmLevel 'MAP01' 'The Courtyard' 64 1000 1001
New-SwarmLevel 'MAP02' 'The Parade Ground' 128 5000 1002
New-SwarmLevel 'MAP03' 'The Endless Army' 256 20000 1003
