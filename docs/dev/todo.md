# To do

Ideas and follow-ups for later, newest last. Move an item out (or delete it) once it's done.

## Example mods as downloads on GitHub Releases

The docs site's mod catalog (website repo, `docs-site/catalog/`, added 2026-10-21) lists the
example mods as `bundled:`: they come with PFWolf in its `mods` folder, so their pages say to
switch them on, and link their folder in this repository, with no file to download. Publishing
each as a `.pk3` on the GitHub release would let someone with an older or newer PFWolf, or just
the catalog, get one on its own.

- On each release, zip each `examples/mods/<id>` folder's contents (not the folder itself) as
  `<id>.pk3` and attach them to the GitHub release, beside the game's own build. A script in
  `examples/tools/` (or a CI workflow on tag push, if pf-wolf gets CI) keeps it repeatable.
- Give the example mods' `modinfo.yaml` an `id` (their folder names) and a `pfwolf` range, now
  that a release pins them to a version.
- In the website repo, `docs-site/scripts/catalog.mjs`: a bundled mod's page also links its
  `.pk3` on the latest release (`<repo>/releases/download/<tag>/<id>.pk3`), alongside "comes with
  PFWolf". Decide whether a bundled entry gives the link itself or it's worked out from the tag.
- Check: the release has a `.pk3` for each example; each loads with `--file <id>.pk3` on that
  release (`mods` lists it, nothing warned); the catalog's links download them.
