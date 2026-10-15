# Usage: powershell -File icons\make-icons.ps1   (needs Microsoft Edge)
param([string]$IconDir = $PSScriptRoot, [string]$Work = $env:TEMP)
# Renders each icons/*.svg with Edge at every icon size, then packs a PNG-entry .ico
Add-Type -AssemblyName System.Drawing
$edge = "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
$sizes = 256, 128, 64, 48, 32, 24, 16
foreach ($svg in Get-ChildItem $IconDir -Filter *.svg) {
    $name = $svg.BaseName
    $url = 'file:///' + ($svg.FullName -replace '\\', '/')
    # One row, each size at a known x
    $x = 0; $imgs = ''; $pos = @{}
    foreach ($n in $sizes) { $imgs += "<img src='$url' style='position:absolute;left:${x}px;top:0;width:${n}px;height:${n}px'>"; $pos[$n] = $x; $x += $n + 8 }
    $html = Join-Path $Work "$name-sheet.html"
    "<html><body style='margin:0;background:transparent'>$imgs</body></html>" | Set-Content -Encoding utf8 $html
    $shot = Join-Path $Work "$name-sheet.png"
    & $edge --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 --default-background-color=00000000 --window-size=$x,300 --screenshot="$shot" ('file:///' + ($html -replace '\\', '/')) | Out-Null
    $sheet = [System.Drawing.Bitmap]::FromFile($shot)
    $pngs = @{}
    foreach ($n in $sizes) {
        $bmp = $sheet.Clone((New-Object System.Drawing.Rectangle $pos[$n], 0, $n, $n), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngs[$n] = $ms.ToArray()
        if ($n -in 256, 64) { [IO.File]::WriteAllBytes((Join-Path $IconDir "$name-$n.png"), $pngs[$n]) }
        $bmp.Dispose()
    }
    $sheet.Dispose()
    # ICONDIR + ICONDIRENTRYs + PNG data
    $out = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter $out
    $w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    foreach ($n in $sizes) {
        $b = if ($n -ge 256) { 0 } else { $n }
        $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([UInt16]1); $w.Write([UInt16]32)
        $w.Write([UInt32]$pngs[$n].Length); $w.Write([UInt32]$offset)
        $offset += $pngs[$n].Length
    }
    foreach ($n in $sizes) { $w.Write($pngs[$n]) }
    $w.Flush()
    [IO.File]::WriteAllBytes((Join-Path $IconDir "$name.ico"), $out.ToArray())
}
