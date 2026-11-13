#!/usr/bin/env bash
# Publishes PFWolf and the editor for one Linux runtime and packs them into
# artifacts/pfwolf-<version>-<rid>.tar.gz: the two programs (self-contained, with SDL2 and OpenAL
# Soft beside them), pfwolf.pk3, the example mods, the icons, a README and the script that adds
# them to the desktop's application menu.
#
#   packaging/linux/make-tarball.sh [linux-x64|linux-arm64]
#
# Run it on Linux (or in CI): a tarball made on Windows loses the programs' executable bits.
set -euo pipefail

rid="${1:-linux-x64}"
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../.." && pwd)"
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$root/PFWolf/PFWolf.csproj" | head -n 1)"
name="pfwolf-$version-$rid"
artifacts="$root/artifacts"
stage="$artifacts/$name"

rm -rf "$stage"
mkdir -p "$stage"

dotnet publish "$root/PFWolf/PFWolf.csproj" -p:PublishProfile=linux -r "$rid" -o "$stage"
dotnet publish "$root/PFWolf.Editor/PFWolf.Editor.csproj" -p:PublishProfile=linux -r "$rid" -o "$stage"

cp "$root/icons/pfwolf-256.png" "$stage/pfwolf.png"
cp "$root/icons/pfwolf-editor-256.png" "$stage/pfwolf-editor.png"
cp "$here/install-desktop-entries.sh" "$here/README.txt" "$stage/"
chmod +x "$stage/PFWolf" "$stage/PFWolf.Editor" "$stage/install-desktop-entries.sh"

tar -C "$artifacts" -czf "$artifacts/$name.tar.gz" "$name"
echo "$artifacts/$name.tar.gz"
