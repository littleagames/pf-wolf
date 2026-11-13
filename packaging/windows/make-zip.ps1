# Publishes PFWolf and the editor for Windows (the win-x64 publish profiles) into one folder and
# zips it with a README: artifacts\pfwolf-<version>-win-x64.zip.
#
#   powershell -File packaging\windows\make-zip.ps1
#
# Works in Windows PowerShell 5.1 and in PowerShell 7 (the Package workflow's).
$ErrorActionPreference = 'Stop'

$here = $PSScriptRoot
$root = (Resolve-Path (Join-Path $here '..\..')).Path
$version = ([xml](Get-Content (Join-Path $root 'PFWolf\PFWolf.csproj') -Raw)).Project.PropertyGroup |
    ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1
$name = "pfwolf-$version-win-x64"
$artifacts = Join-Path $root 'artifacts'
$stage = Join-Path $artifacts $name
$zip = Join-Path $artifacts "$name.zip"

if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $stage | Out-Null

foreach ($project in 'PFWolf\PFWolf.csproj', 'PFWolf.Editor\PFWolf.Editor.csproj') {
    dotnet publish (Join-Path $root $project) -p:PublishProfile=win-x64 -o $stage
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish $project failed ($LASTEXITCODE)" }
}
Copy-Item (Join-Path $here 'README.txt') $stage

# Entry by entry, with '/' in the names as zip files must have (5.1's CreateFromDirectory writes '\')
if (Test-Path $zip) { Remove-Item -Force $zip }
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem $stage -Recurse -File) {
        $entry = "$name/" + $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entry, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $archive.Dispose()
}
Write-Output $zip
