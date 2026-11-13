#!/usr/bin/env bash
# Publishes PFWolf and the editor for one Linux runtime into one AppImage:
# artifacts/PFWolf-<version>-<arch>.AppImage. It runs the game; with --editor, the editor
# (appimage/AppRun). Its own files are read-only, so the game's data files and new mods go in
# ~/.local/share/PFWolf (UserDataFolder) and the editor's game folder is the AppImage's.
#
#   packaging/linux/make-appimage.sh [linux-x64|linux-arm64]
#
# Run it on Linux. It uses appimagetool from PATH, or $APPIMAGETOOL, or downloads it.
set -euo pipefail

rid="${1:-linux-x64}"
case "$rid" in
    linux-x64) arch=x86_64 ;;
    linux-arm64) arch=aarch64 ;;
    *) echo "No AppImage for $rid" >&2; exit 1 ;;
esac

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../.." && pwd)"
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$root/PFWolf/PFWolf.csproj" | head -n 1)"
artifacts="$root/artifacts"
appdir="$artifacts/PFWolf-$rid.AppDir"
out="$artifacts/PFWolf-$version-$arch.AppImage"

rm -rf "$appdir"
mkdir -p "$appdir/usr/bin" "$appdir/usr/share/icons/hicolor/256x256/apps"

dotnet publish "$root/PFWolf/PFWolf.csproj" -p:PublishProfile=linux -r "$rid" -o "$appdir/usr/bin"
dotnet publish "$root/PFWolf.Editor/PFWolf.Editor.csproj" -p:PublishProfile=linux -r "$rid" -o "$appdir/usr/bin"
chmod +x "$appdir/usr/bin/PFWolf" "$appdir/usr/bin/PFWolf.Editor"

cp "$here/appimage/AppRun" "$appdir/AppRun"
chmod +x "$appdir/AppRun"
cp "$here/appimage/pfwolf.desktop" "$appdir/pfwolf.desktop"
cp "$root/icons/pfwolf-256.png" "$appdir/pfwolf.png"
cp "$root/icons/pfwolf-256.png" "$appdir/usr/share/icons/hicolor/256x256/apps/pfwolf.png"
ln -sf pfwolf.png "$appdir/.DirIcon"

tool="${APPIMAGETOOL:-$(command -v appimagetool || true)}"
if [ -z "$tool" ]; then
    # (The tool itself runs on the build machine's architecture; ARCH below picks the AppImage's)
    tool="$artifacts/tools/appimagetool-$(uname -m).AppImage"
    if [ ! -x "$tool" ]; then
        mkdir -p "$artifacts/tools"
        curl -fsSL -o "$tool" "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-$(uname -m).AppImage"
        chmod +x "$tool"
    fi
fi

# Without FUSE (CI's containers), the tool unpacks itself and runs
rm -f "$out"
APPIMAGE_EXTRACT_AND_RUN=1 ARCH="$arch" "$tool" "$appdir" "$out"
echo "$out"
