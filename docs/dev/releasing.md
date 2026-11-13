# Releasing

How a version goes out: the downloads are built by the Package workflow
(.github/workflows/package.yml) and attached to a draft GitHub Release, which is published by
hand once its notes are written.

## The downloads

| Download | Made by | Where it runs |
| --- | --- | --- |
| `pfwolf-<version>-win-x64.zip` | packaging/windows/make-zip.ps1 | windows-latest, or here |
| `pfwolf-<version>-linux-x64.tar.gz`, `-linux-arm64.tar.gz` | packaging/linux/make-tarball.sh | ubuntu-latest |
| `PFWolf-<version>-x86_64.AppImage`, `-aarch64.AppImage` | packaging/linux/make-appimage.sh | ubuntu-latest |
| `pfwolf-<version>-osx-arm64.zip`, `-osx-x64.zip` | packaging/macos/make-apps.sh | macos-latest |

Each has the game and the editor, `pfwolf.pk3` and the example mods. The Linux and macOS ones
have to be made on those systems (a tarball made on Windows loses the programs' executable bits;
the Mac apps need codesign, iconutil and sips). The Windows zip can be made here:
`powershell -File packaging\windows\make-zip.ps1` → `artifacts\`.

## Steps

1. **Version.** `Version`, `FileVersion` and `AssemblyVersion` in PFWolf/PFWolf.csproj and
   PFWolf.Editor/PFWolf.Editor.csproj (nothing else holds it). Commit as "Bump to x.y.z".
2. **Dry run.** Actions → Package → Run workflow on the branch. Without a tag it only builds
   the downloads (as the run's artifacts), so a broken script costs a fix, not a re-tag.
   Start each download on its own system: docs/dev/manual-testing.md, Part 3.
3. **Tag** the commit to release (the bump, or the last one after it: the workflow that runs is
   the tagged commit's), annotated, as the earlier ones are: `v0.9.1-beta` (`-alpha`,
   `-beta`, `-rc` make it a pre-release; no suffix is a full release).
   `git tag -a v0.9.1-beta -m "PFWolf 0.9.1 beta"`
4. **Push** the branch, then the tag: `git push origin v0.9.1-beta`. The tag runs the Package
   workflow, whose last job makes a draft release ("PFWolf 0.9.1 beta") with every download and
   GitHub's generated notes (the commits since the last release). Pushing the tag again after
   moving it (`git push -f origin v0.9.1-beta`) runs it again and replaces the draft's files.
5. **Notes.** On the draft: what's new, what changed for mods (modinfo, YAML definitions, the
   Lua API, UWMF, saves), what's known not to work, and the first-run steps for the unsigned
   Mac apps (packaging/macos/README.txt). Then **Publish release**.

## Not done yet

- The example mods as `.pk3` downloads on the release (docs/dev/todo.md).
- Signing: the Windows exes aren't signed (SmartScreen may warn on the first run) and the Mac
  apps aren't notarized (docs/dev/mac-linux-plan.md).
