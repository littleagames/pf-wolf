#!/usr/bin/env bash
# Adds PFWolf and the PFWolf Editor to the desktop's application menu, for this user, run from
# the folder they were unpacked to. Run it again after moving the folder; --remove takes them out.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
applications="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
game="$applications/pfwolf.desktop"
editor="$applications/pfwolf-editor.desktop"

if [ "${1:-}" = "--remove" ]; then
    rm -f "$game" "$editor"
    echo "Removed $game and $editor"
    exit 0
fi

mkdir -p "$applications"

# Path= starts each in its own folder, where pfwolf.pk3 and the game's data files are
cat > "$game" <<EOF
[Desktop Entry]
Type=Application
Name=PFWolf
Comment=Wolfenstein 3-D, Spear of Destiny and Blake Stone, with mods
Exec="$here/PFWolf" %F
Path=$here
Icon=$here/pfwolf.png
Terminal=false
Categories=Game;ActionGame;
EOF

cat > "$editor" <<EOF
[Desktop Entry]
Type=Application
Name=PFWolf Editor
Comment=Maps, art, sounds and definitions for PFWolf mods
Exec="$here/PFWolf.Editor"
Path=$here
Icon=$here/pfwolf-editor.png
Terminal=false
Categories=Development;Game;
EOF

chmod +x "$game" "$editor"
command -v update-desktop-database > /dev/null && update-desktop-database "$applications" || true
echo "Added $game and $editor"
