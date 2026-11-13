#!/usr/bin/env bash
# Publishes PFWolf and the editor for one macOS runtime as PFWolf.app and PFWolf Editor.app and
# zips them, with a README: artifacts/pfwolf-<version>-<rid>.zip. The apps are only ad-hoc signed
# (not notarized), so Gatekeeper asks before the first run; README.txt says how to open them.
#
#   packaging/macos/make-apps.sh [osx-arm64|osx-x64]
#
# Run it on macOS: it needs codesign, iconutil, sips and ditto.
set -euo pipefail

rid="${1:-osx-arm64}"
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../.." && pwd)"
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$root/PFWolf/PFWolf.csproj" | head -n 1)"
name="pfwolf-$version-$rid"
artifacts="$root/artifacts"
stage="$artifacts/$name"
work="$artifacts/$name-publish"

rm -rf "$stage" "$work"
mkdir -p "$stage" "$work"

# icon <png> <icns>: every size macOS asks for, scaled from one PNG
icon() {
    local iconset="$work/$(basename "$2" .icns).iconset"
    mkdir -p "$iconset"
    for size in 16 32 128 256; do
        sips -z "$size" "$size" "$1" --out "$iconset/icon_${size}x${size}.png" > /dev/null
        sips -z "$((size * 2))" "$((size * 2))" "$1" --out "$iconset/icon_${size}x${size}@2x.png" > /dev/null
    done
    iconutil -c icns "$iconset" -o "$2"
}

# make_app <project> <app name> <exe> <icon png> <bundle id> <category>
make_app() {
    local project="$1" app="$stage/$2.app" exe="$3" png="$4" id="$5" category="$6"
    local out="$work/$exe"

    dotnet publish "$root/$project/$project.csproj" -p:PublishProfile=osx -r "$rid" -o "$out"

    # The program and its native libraries in MacOS; everything else (pfwolf.pk3, mods,
    # symbols, the docs XML) in Resources, where GameInstall.Folder looks. The SDL2-CS package's
    # Mono dllmap file means nothing to .NET.
    mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
    for file in "$out"/*; do
        case "$(basename "$file")" in
            "$exe" | *.dylib) cp -R "$file" "$app/Contents/MacOS/" ;;
            *.dll.config) ;;
            *) cp -R "$file" "$app/Contents/Resources/" ;;
        esac
    done
    chmod +x "$app/Contents/MacOS/$exe"

    icon "$png" "$app/Contents/Resources/$exe.icns"
    sed -e "s|@NAME@|$2|g" -e "s|@EXE@|$exe|g" -e "s|@ID@|$id|g" \
        -e "s|@VERSION@|$version|g" -e "s|@CATEGORY@|$category|g" \
        "$here/Info.plist.in" > "$app/Contents/Info.plist"

    # Ad-hoc: Apple Silicon runs nothing unsigned
    codesign --force --deep --sign - "$app"
}

make_app PFWolf "PFWolf" PFWolf "$root/icons/pfwolf-256.png" com.littleagames.pfwolf public.app-category.action-games
make_app PFWolf.Editor "PFWolf Editor" PFWolf.Editor "$root/icons/pfwolf-editor-256.png" com.littleagames.pfwolf.editor public.app-category.developer-tools
cp "$here/README.txt" "$stage/"

rm -f "$artifacts/$name.zip"
ditto -c -k --keepParent "$stage" "$artifacts/$name.zip"
echo "$artifacts/$name.zip"
