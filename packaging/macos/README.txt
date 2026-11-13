PFWolf for macOS
================

Drag PFWolf.app and PFWolf Editor.app into Applications (or anywhere). Nothing else needs
installing: the .NET runtime, SDL2 and OpenAL Soft are inside the apps.

Opening them the first time
---------------------------
The apps aren't notarized by Apple yet, so macOS stops them the first time ("can't be opened" /
"Apple could not verify..."). Either:
  - right-click (Control-click) the app, choose Open, then Open again; or, on macOS 15 and later,
    try to open it once, then System Settings > Privacy & Security > Open Anyway; or
  - in Terminal: xattr -dr com.apple.quarantine "/Applications/PFWolf.app"
    (and the same for "PFWolf Editor.app").

The game's data files
---------------------
PFWolf plays the original games' data files, which aren't included. Copy them (VSWAP.WL6,
VGAGRAPH.WL6, AUDIOT.WL6, GAMEMAPS.WL6 and the rest, from Steam, GOG or the shareware) into
~/Library/Application Support/PFWolf/ (in Finder: Go > Go to Folder..., then paste that).
Upper or lower case names both work.

Where things go
---------------
  The game's data files, mods (mods/), crash.log   ~/Library/Application Support/PFWolf/
  Settings, saves, screenshots, recorded demos     ~/Library/Application Support/PFWolf/<game>/
  The editor's settings                            ~/Library/Application Support/PFWolf/Editor/

The editor
----------
In the editor, choose PFWolf.app as the game folder: it reads the game's files from inside it and
plays levels with it.

More
----
https://www.littleagames.com/pf-wolf/docs/
