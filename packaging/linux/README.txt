PFWolf for Linux
================

Unpack this folder anywhere and run ./PFWolf. Nothing needs installing: the .NET runtime,
SDL2 and OpenAL Soft come with it.

The game's data files
---------------------
PFWolf plays the original games' data files, which aren't included. Copy them (VSWAP.WL6,
VGAGRAPH.WL6, AUDIOT.WL6, GAMEMAPS.WL6 and the rest, from Steam, GOG or the shareware) into
this folder, beside PFWolf, or into ~/.local/share/PFWolf/. Upper or lower case names both work.

Where things go
---------------
  Settings, saves, screenshots, recorded demos   ~/.config/PFWolf/<game>/
  Mods                                           mods/ in this folder, or ~/.local/share/PFWolf/mods/
  The editor's settings                          ~/.config/PFWolf/Editor/
  A crash report (crash.log)                     this folder, else ~/.local/share/PFWolf/

($XDG_CONFIG_HOME and $XDG_DATA_HOME move those, when they're set.)

The application menu
--------------------
./install-desktop-entries.sh adds PFWolf and the PFWolf Editor to your desktop's application
menu. Run it again after moving this folder; ./install-desktop-entries.sh --remove takes them out.

More
----
https://www.littleagames.com/pf-wolf/docs/
