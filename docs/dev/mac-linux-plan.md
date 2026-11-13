# macOS and Linux

Running PFWolf and the editor on macOS and Linux, from a terminal first and then as proper
downloads (a tarball/AppImage, a `.app`). The game and its files behave the same on every
system; only where things are kept and how they're packaged differ.

## Why it's close already

- .NET 10 runs on all three, and every managed package we use (YamlDotNet, NVorbis, NLayer,
  NukedOPL3Sharp, LiteNetLib, Mono.Nat, LuaCSharp, CommandLineParser) is plain managed code.
- The engine draws with SDL's renderer, not OpenGL calls of its own, and reads input as SDL
  scancodes.
- pk3 and mod-folder entry paths are already `/`-separated, and folder mods open entries by
  their real names (`DirectoryAssetSource`), so mixed-case art in mods works.
- The editor is Avalonia, which ships its native parts (Skia, HarfBuzz, AvaloniaNative) for
  osx, linux-x64 and linux-arm64.
- The Windows-only bits that exist are mostly guarded: `ConsoleWindow` checks
  `OperatingSystem.IsWindows()`, the editor's Play/Watch pick `PFWolf` or `PFWolf.exe`,
  `Restart` uses `Environment.ProcessPath`, and `GameFolder.Use` moves to the exe's folder when
  started from elsewhere (as Finder and file managers do).

## What breaks

### Code

1. **User folders built with backslashes.** `ConfigDirectories.Default`
   (PFWolf/Configuration/ConfigDirectories.cs) joins every folder with `\\`, so off Windows the
   config, save, screenshot, log, demo and export folders each become one file name with
   backslashes in it. Use `Path.Combine`. The `--configdir`/`--savedir`/`--demodir` help text
   and the docs say `%APPDATA%`; they should name each system's folder.
2. **Data file names must match case.** gamepack-info.yaml names the data files in lower case
   (`vswap.wl6`) and `GamePackInfoAsset.ResolveDataFile` uses the name as given. Steam and GOG
   copies are upper case (`VSWAP.WL6`), so on Linux every game reports its files missing.
   Needs one helper that finds a file in a folder whatever its case, used by
   `ResolveDataFile`, `FindMissingDataFiles`/`FindMismatchedDataFiles`, the md5 picking of
   same-named files, the classic data loaders (`Wolf3d*FileLoader`), the raw data mod
   generator and `--make-mod`. (macOS's usual file system ignores case, but a case-sensitive
   volume exists there too.)
3. **Sound library.** `AudioManager` looks for `OpenAL32.dll`, which is copied into the build
   folder by hand (it isn't in git or a package), and `DescribeLibraryFailure` reads Windows
   DLL headers to explain a failure. OpenTK ships no OpenAL library for any system. Linux uses
   the system's openal-soft (`libopenal.so.1`); macOS uses a bundled openal-soft or Apple's
   deprecated OpenAL framework (see Decisions). The failure message needs a version for each.
4. **Editor sound player.** `PFWolf.Editor/Audio/SoundPlayer.cs` plays through winmm and is
   silent elsewhere (`IsAvailable`). Move it onto OpenAL (OpenTK.Audio.OpenAL, as the engine
   does), keeping its interface: one sound or song at a time, a chunk at a time from a
   `MusicSource`, position, volume.
5. **Editor external art editor.** The program picker only offers `*.exe`
   (ArtBrowserWindow.axaml.cs). On macOS a picked `.app` is started with `open -a`; on Linux
   any executable file. Opening a file in its default program already works through .NET
   (`xdg-open`, `open`), though the `edit` verb is Windows-only and is ignored elsewhere.
6. **Tests.** HeadlessGame, RawDataModGeneratorTests, StandaloneGameTests and WarpTests start
   `PFWolf.exe` by name; use the same pick as the editor.
7. **Smaller things.** `Path.GetInvalidFileNameChars()` is only `/` and NUL off Windows, so
   names typed for saves, mods and demos should also refuse the Windows set (a mod made on
   Linux should still unzip on Windows). Path comparisons that ignore case (`ModFiles`,
   the editor's mod list) are harmless, but two mods differing only in case would merge.

### Native libraries

- **SDL2.** `ppy.SDL2-CS` 1.0.82 ships `linux-x64`, `linux-x86` and `osx-x64` only; the macOS
  one is Intel-only (checked: a thin x86_64 Mach-O). Apple Silicon Macs and ARM Linux need
  their own builds (see Decisions). On Linux, the distribution's SDL2 is `libSDL2-2.0.so.0`,
  which the `SDL2` import name doesn't find: add a `NativeLibrary.SetDllImportResolver`
  fallback so a system SDL2 works when none is bundled.
- **OpenAL.** See item 3.
- **`SDL2_mixer.dll`** sits in the Windows build folder but nothing calls it (no `Mix_`
  calls). Drop it.

### Where files live

- **User folder.** `SpecialFolder.ApplicationData` is `~/.config` on Linux and macOS alike.
  Fine for Linux (it honours `XDG_CONFIG_HOME`); macOS apps normally use
  `~/Library/Application Support`. `GameTypes.DemosFolder` (for the editor) and the editor's
  `EditorSettings` build the same path and must agree with the engine.
- **Beside the exe.** `pfwolf.pk3`, the data files and `mods/` are read from the game folder
  (`ModSource.ModsFolder`, `GameFolder`), and `CrashReport` writes there first. That folder
  is read-only, or shouldn't be written, inside a `.app` (writing breaks its signature), an
  AppImage or a system install. So the game also needs a user data folder, searched after
  the game folder: data files, `mods/` (and where the Mods screen and the editor's New mod put
  new ones), crash logs. On Windows the game folder stays first, so nothing changes there.

### Things to check on the machines themselves

- **Editor 3D view.** It asks for GLSL 330 core (LevelView3D.cs). macOS offers OpenGL up to 4.1
  core, which covers it, but Avalonia on macOS may draw with Metal by default; if the 3D view
  stays blank, ask for OpenGL in `Program.BuildAvaloniaApp`
  (`AvaloniaNativePlatformOptions.RenderingMode`).
- **Retina screens.** The game window doesn't ask SDL for high DPI, so it's drawn at point
  size and scaled up. Acceptable first; `SDL_WINDOW_ALLOW_HIGHDPI` plus the renderer's output
  size for the view later.
- **Keys on macOS.** The editor's shortcuts are `Ctrl+…`; Mac users expect `Cmd`. Avalonia's
  `PlatformHotkeyConfiguration` gives the platform's modifier. In the game, `Cmd+Q` should
  quit as Alt+F4 does on Windows.
- **Linux desktops.** The editor on Wayland runs through XWayland; SDL picks X11 or Wayland
  itself.
- **Multiplayer.** LiteNetLib and Mono.Nat (UPnP) are cross-platform; check a Linux–Windows game
  and the macOS firewall prompt.
- **Controllers.** SDL's game controller database covers the usual pads on all three.

## Packaging

- **Publish per system.** `PublishSelfContained` is already on; publishing needs a runtime:
  `win-x64`, `linux-x64`, `linux-arm64`, `osx-arm64`, `osx-x64`. The pk3s and example mods
  are added by `PublishPfWolfExtras`, which works the same way everywhere.
- **Linux.** A tarball first (exe, pk3s, mods, a `.desktop` file and the icon PNGs), an
  AppImage later. The tarball works from wherever it's unpacked, as the Windows zip does.
- **macOS.** A `.app` each for the game and the editor: `Info.plist`, an `.icns` made from
  icons/pfwolf.svg and pfwolf-editor.svg, the published files in `Contents/MacOS` (pk3s in
  `Contents/Resources`, if `GameFolder` learns to look there). Without signing and
  notarization (an Apple Developer account, a hardened runtime with .NET's JIT entitlements),
  Gatekeeper blocks a downloaded app until the player clears its quarantine; documented in
  getting-started either way.
- **The editor finding the game.** Its Play button starts `GameFolder/PFWolf`. With two `.app`
  bundles, the game folder setting should accept the `.app` and look inside it.

## Testing

No WSL or Docker on the development PC, so testing off Windows is by CI and by hand:

- **CI.** A GitHub Actions run on windows-latest, ubuntu-latest and macos-latest: build, then
  the tests headless (`SDL_VIDEODRIVER=dummy`, `SDL_AUDIODRIVER=dummy`). CI has no game data
  files, so the tests that need them (the `*GameTests`, demo regression, warp) skip; check
  each skips cleanly rather than failing. Linux also needs openal-soft installed
  (`libopenal1`).
- **By hand.** Adds to docs/dev/manual-testing.md: play a level on Linux and on a Mac, sound and
  music, save/load, the Mods screen, a LAN game against Windows, the editor's 3D view, sound
  browser and Play button.

## Phases

1. **Code that assumes Windows.** Items 1–7: user folders with `Path.Combine`, the case-free
   data file helper used everywhere data files are found, OpenAL loading and its failure
   message for each system, the editor's sound player on OpenAL, the art editor's program
   picker, the tests' exe name, file-name checks. Check: every test passes on Windows (nothing
   changes there); a test for the case-free helper with an upper-case `VSWAP.WL6`.
   *Done 2026-11-12:*
   - `ConfigDirectories` uses `Path.Combine`; `--help` names `~/.config` beside `%APPDATA%`.
   - `CaseInsensitiveFile.Find` (Common/Loaders) in `ResolveDataFile`, so every data file,
     movie and map pair lookup gets the on-disk name; also the raw data mod generator's base
     files and movies, and HeadlessGame.
   - `AudioManager` names the OpenAL library per system (`libopenal.so.1`, Apple's framework)
     and explains a failure with dlopen's reason plus an install hint on Linux.
   - The editor's sound player keeps winmm on Windows: the editor can't count on an
     OpenAL32.dll beside it, and Creative's OpenAL32.dll that some PCs have in System32 (its
     "Generic Software" device) plays at the wrong speed. OpenAL is used elsewhere
     (SoundPlayer.OpenAL.cs). `SoundPlayer.UseOpenAL` lets the tests run it on Windows with
     OpenAL Soft's DLL copied beside them; they skip on Creative's.
   - Art editor picker: `*.exe` on Windows, `.app` on macOS (started with `open -a`), any
     file on Linux; "the system's own program for PNGs" wording.
   - Tests start `TestPaths.GameExe()`.
   - `PortableFileName` (Windows' invalid characters, on every system) for a mod game's user
     folder, demo names and exported sound names.
   - Found: OpenTK only looks for Apple's framework on macOS, so bundling OpenAL Soft there
     (phase 2) needs our own load ahead of OpenTK's.
2. **Native libraries and CI.** SDL2 for every runtime we publish (see Decisions), the system
   SDL2 fallback on Linux, OpenAL for macOS, `SDL2_mixer.dll` dropped; the GitHub Actions run on
   all three. Check: CI green on all three, a headless `demotest` on Linux identical to
   Windows (the demos are the engine's own regression check).
3. **Linux build.** User data folder searched after the game folder (data files, mods, crash
   logs), Mods screen and New mod writing there when the game folder isn't writable, publish
   for linux-x64/arm64, tarball with a `.desktop` file, docs (getting-started: where files go,
   installing openal-soft). Check: by hand on a Linux machine.
4. **macOS build.** The user folder decision, `.app` bundles for the game and the editor with
   icons, `Contents/Resources` in `GameFolder`, the editor's 3D view on macOS, `Cmd` shortcuts,
   signing/notarization if decided, docs. Check: by hand on a Mac.
5. **Polish.** High-DPI game window, AppImage, a Linux package if wanted.

## Decisions to make

- **SDL2 for Apple Silicon and ARM Linux.** Build and bundle our own (`runtimes/osx-arm64`,
  `runtimes/linux-arm64`, or one universal macOS library) next to ppy.SDL2-CS; or switch to a
  binding package that already ships them all (Silk.NET.SDL does, at the cost of rewriting the
  SDL calls). Moving to SDL3 is a bigger, separate job.
- **OpenAL on macOS.** Bundle openal-soft (the same library as Windows and Linux, so the same
  sound) or use Apple's OpenAL framework (always there, but deprecated and older).
- **macOS user folder.** `~/Library/Application Support/PFWolf` (the convention) or
  `~/.config/PFWolf` (what .NET gives, the same as Linux).
- **Signing.** Whether to get an Apple Developer account for notarized downloads, or ship
  unsigned with instructions.
- **Linux format.** Tarball only, or AppImage/Flatpak too.
- **Intel Macs.** Whether to publish `osx-x64` at all, or Apple Silicon only.
