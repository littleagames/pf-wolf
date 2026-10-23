using PFWolf.Configuration;
using PFWolf.Exceptions;
using PFWolf.Loaders;
using PFWolf.Managers;

namespace PFWolf.Startup;

/// <summary>
/// --make-mod: makes a raw data mod from a folder of WDC/ChaosEdit-edited data files
/// (docs/dev/raw-data-mods-plan.md), using pfwolf.pk3 in the game folder for the base game's
/// names, then quits. Prints the base games the files may be built on and the report.
/// </summary>
internal static class MakeModCommand
{
    /// <returns>The exit code: 0 when the mod was written</returns>
    internal static int Run(GameParams gameParams, TextWriter output)
    {
        var dataFolder = gameParams.MakeMod;
        var folderName = Path.GetFileName(dataFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var sink = WarningLog.Sink;
        WarningLog.Sink = message => output.WriteLine($"Warning: {message}");
        try
        {
            if (!Directory.Exists(dataFolder))
            {
                output.WriteLine($"--make-mod: there's no folder {dataFolder}");
                return 1;
            }

            var candidates = RawDataModGenerator.DetectBase(dataFolder);
            if (candidates.Count == 0)
            {
                output.WriteLine($"--make-mod: {dataFolder} has none of the data files (VSWAP, VGAGRAPH, AUDIOT, GAMEMAPS...) a game PFWolf plays has");
                return 1;
            }

            output.WriteLine("The games the files may be built on, best first:");
            foreach (var candidate in candidates)
                output.WriteLine($"  {candidate.ReleaseId,-18} {candidate.Title}: "
                    + (candidate.Share is { } share ? $"{share:P0} of its chunks the same" : "its data files aren't in the game folder to compare")
                    + $", {candidate.NamesMatched} file names the same");

            var release = BaseRelease(gameParams.Base, candidates, output);
            if (release == null)
                return 1;

            var title = string.IsNullOrWhiteSpace(gameParams.Title) ? folderName : gameParams.Title.Trim();
            var gameId = RawDataModWriter.GameIdFrom(title);
            var modFolder = string.IsNullOrWhiteSpace(gameParams.Out) ? Path.Combine(ModSource.ModsFolder, folderName) : gameParams.Out;

            output.WriteLine($"Building on {release}; the game is {gameId} (\"{title}\"), written to {modFolder}");
            var match = RawDataModGenerator.Match(dataFolder, release);
            // Run again on a mod made before: the names given since are kept
            var previous = Path.Combine(modFolder, "raw-data-map.yaml");
            var files = RawDataModWriter.Write(match, gameId, title, File.Exists(previous) ? File.ReadAllText(previous) : null);
            var clashes = RawDataModWriter.Save(files, modFolder, gameParams.Overwrite);
            if (clashes.Count > 0 && !gameParams.Overwrite)
            {
                output.WriteLine($"--make-mod: {modFolder} already has {string.Join(", ", clashes.Select(Path.GetFileName))}: nothing was written (--overwrite writes over them)");
                return 1;
            }

            output.WriteLine();
            output.Write(files.Report);
            output.WriteLine();
            output.WriteLine($"Play it with: PFWolf --file \"{modFolder}\"");
            return 0;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or KeyNotFoundException
                                      or ArgumentException or PfWolfFileLoaderException or PfWolfGraphicException or PfWolfMapException)
        {
            output.WriteLine($"--make-mod: {e.Message}");
            return 1;
        }
        finally
        {
            WarningLog.Sink = sink;
        }
    }

    // --base as a release key or one of PFWolf's pack ids, else the best match
    private static string? BaseRelease(string asked, List<BaseCandidate> candidates, TextWriter output)
    {
        if (string.IsNullOrWhiteSpace(asked))
            return candidates[0].ReleaseId;

        var release = candidates.FirstOrDefault(candidate => candidate.ReleaseId.Equals(asked.Trim(), StringComparison.OrdinalIgnoreCase)
                                                             || candidate.PackId.Equals(asked.Trim(), StringComparison.OrdinalIgnoreCase))?.ReleaseId;
        if (release == null)
            output.WriteLine($"--make-mod: --base {asked} isn't one the files can be built on ({string.Join(", ", candidates.Select(candidate => candidate.ReleaseId))})");
        return release;
    }
}
