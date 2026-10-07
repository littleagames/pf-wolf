# Copies SplitWolf's directional enemy sprites into multiplayer-pk3/sprites/enemies, renamed to
# the game's own sprite names. Run it again to bring in a newer SplitWolf set:
#
#   powershell -ExecutionPolicy Bypass -File examples/tools/import-splitwolf-sprites.ps1 [-Source <lwmp\sprites>]
#
# The original draws enemies shooting, in pain and (the bosses) walking from the front only: a
# single view, letter 0 (GARDF0). In a netgame the other players see an enemy from the side, so
# SplitWolf drew those frames from all 8 sides, one folder per side (dir1 is the front, dir2 to
# dir8 go round the same way as the game's walking views). Each picture here becomes view 1-8 of
# that frame (GARDF2..GARDF8); the renderer turns any frame with views to face its way, and a
# view that's missing (most of dir1) falls back to the game's own front view.
#
# SplitWolf's names (wl_def.h) to the game's sprite and frame letter, as raw-data-map.yaml names
# the VSWAP sprites. Wolf3D and Spear share the regular enemies; TRNS and WILL are Spear's.

param([string]$Source = 'D:\projects\Wolf3D\SplitWolf_DM.1\lwmp\sprites')

$ErrorActionPreference = 'Stop'

$Frames = [ordered]@{
    # Guard: shooting F G H, pain I J
    'SPR_GRD_SHOOT1' = 'GARDF'; 'SPR_GRD_SHOOT2' = 'GARDG'; 'SPR_GRD_SHOOT3' = 'GARDH'
    'SPR_GRD_PAIN_1' = 'GARDI'; 'SPR_GRD_PAIN_2' = 'GARDJ'
    # Dog: the leap E F G
    'SPR_DOG_JUMP1' = 'DOGYE'; 'SPR_DOG_JUMP2' = 'DOGYF'; 'SPR_DOG_JUMP3' = 'DOGYG'
    # SS: shooting E F G, pain I (first) H (second)
    'SPR_SS_SHOOT1' = 'SSWVE'; 'SPR_SS_SHOOT2' = 'SSWVF'; 'SPR_SS_SHOOT3' = 'SSWVG'
    'SPR_SS_PAIN_1' = 'SSWVI'; 'SPR_SS_PAIN_2' = 'SSWVH'
    # Mutant: shooting F G H I, pain J K
    'SPR_MUT_SHOOT1' = 'MTNTF'; 'SPR_MUT_SHOOT2' = 'MTNTG'; 'SPR_MUT_SHOOT3' = 'MTNTH'; 'SPR_MUT_SHOOT4' = 'MTNTI'
    'SPR_MUT_PAIN_1' = 'MTNTJ'; 'SPR_MUT_PAIN_2' = 'MTNTK'
    # Officer: shooting F G H, pain I J
    'SPR_OFC_SHOOT1' = 'OFFIF'; 'SPR_OFC_SHOOT2' = 'OFFIG'; 'SPR_OFC_SHOOT3' = 'OFFIH'
    'SPR_OFC_PAIN_1' = 'OFFII'; 'SPR_OFC_PAIN_2' = 'OFFIJ'
    # Bosses: walking A B C D, then shooting
    'SPR_BOSS_W1' = 'HANSA'; 'SPR_BOSS_W2' = 'HANSB'; 'SPR_BOSS_W3' = 'HANSC'; 'SPR_BOSS_W4' = 'HANSD'
    'SPR_BOSS_SHOOT1' = 'HANSE'; 'SPR_BOSS_SHOOT2' = 'HANSF'; 'SPR_BOSS_SHOOT3' = 'HANSG'
    'SPR_SCHABB_W1' = 'SHABA'; 'SPR_SCHABB_W2' = 'SHABB'; 'SPR_SCHABB_W3' = 'SHABC'; 'SPR_SCHABB_W4' = 'SHABD'
    'SPR_SCHABB_SHOOT1' = 'SHABE'; 'SPR_SCHABB_SHOOT2' = 'SHABF'
    'SPR_FAKE_W1' = 'FAKEA'; 'SPR_FAKE_W2' = 'FAKEB'; 'SPR_FAKE_W3' = 'FAKEC'; 'SPR_FAKE_W4' = 'FAKED'
    'SPR_FAKE_SHOOT' = 'FAKEE'
    'SPR_MECHA_W1' = 'MECHA'; 'SPR_MECHA_W2' = 'MECHB'; 'SPR_MECHA_W3' = 'MECHC'; 'SPR_MECHA_W4' = 'MECHD'
    'SPR_MECHA_SHOOT1' = 'MECHE'; 'SPR_MECHA_SHOOT2' = 'MECHF'; 'SPR_MECHA_SHOOT3' = 'MECHG'
    'SPR_HITLER_W1' = 'HTLRA'; 'SPR_HITLER_W2' = 'HTLRB'; 'SPR_HITLER_W3' = 'HTLRC'; 'SPR_HITLER_W4' = 'HTLRD'
    'SPR_HITLER_SHOOT1' = 'HTLRE'; 'SPR_HITLER_SHOOT2' = 'HTLRF'; 'SPR_HITLER_SHOOT3' = 'HTLRG'
    'SPR_GIFT_W1' = 'GIFTA'; 'SPR_GIFT_W2' = 'GIFTB'; 'SPR_GIFT_W3' = 'GIFTC'; 'SPR_GIFT_W4' = 'GIFTD'
    'SPR_GIFT_SHOOT1' = 'GIFTE'; 'SPR_GIFT_SHOOT2' = 'GIFTF'
    'SPR_GRETEL_W1' = 'GRETA'; 'SPR_GRETEL_W2' = 'GRETB'; 'SPR_GRETEL_W3' = 'GRETC'; 'SPR_GRETEL_W4' = 'GRETD'
    'SPR_GRETEL_SHOOT1' = 'GRETE'; 'SPR_GRETEL_SHOOT2' = 'GRETF'; 'SPR_GRETEL_SHOOT3' = 'GRETG'
    'SPR_FAT_W1' = 'FATFA'; 'SPR_FAT_W2' = 'FATFB'; 'SPR_FAT_W3' = 'FATFC'; 'SPR_FAT_W4' = 'FATFD'
    'SPR_FAT_SHOOT1' = 'FATFE'; 'SPR_FAT_SHOOT2' = 'FATFF'; 'SPR_FAT_SHOOT3' = 'FATFG'; 'SPR_FAT_SHOOT4' = 'FATFH'
    # Spear: Trans Grosse, Wilhelm
    'SPR_TRANS_W1' = 'TRNSA'; 'SPR_TRANS_W2' = 'TRNSB'; 'SPR_TRANS_W3' = 'TRNSC'; 'SPR_TRANS_W4' = 'TRNSD'
    'SPR_TRANS_SHOOT1' = 'TRNSE'; 'SPR_TRANS_SHOOT2' = 'TRNSF'; 'SPR_TRANS_SHOOT3' = 'TRNSG'
    'SPR_WILL_W1' = 'WILLA'; 'SPR_WILL_W2' = 'WILLB'; 'SPR_WILL_W3' = 'WILLC'; 'SPR_WILL_W4' = 'WILLD'
    'SPR_WILL_SHOOT1' = 'WILLE'; 'SPR_WILL_SHOOT2' = 'WILLF'; 'SPR_WILL_SHOOT3' = 'WILLG'; 'SPR_WILL_SHOOT4' = 'WILLH'
}

$out = Join-Path $PSScriptRoot '..\..\multiplayer-pk3\sprites\enemies'
New-Item -ItemType Directory -Force $out | Out-Null
Get-ChildItem $out -File | Remove-Item

$copied = 0
foreach ($view in 1..8) {
    $dir = Join-Path $Source "dir$view"
    $files = @{}
    Get-ChildItem $dir -File | ForEach-Object { $files[$_.BaseName.ToUpperInvariant()] = $_ }
    foreach ($name in $Frames.Keys) {
        if ($files.ContainsKey($name)) {
            Copy-Item $files[$name].FullName (Join-Path $out "$($Frames[$name])$view.bmp")
            $copied++
        }
        elseif ($view -ne 1) {
            Write-Warning "dir$view has no $name"
        }
    }
}
"$copied sprites written to $((Resolve-Path $out).Path)"
