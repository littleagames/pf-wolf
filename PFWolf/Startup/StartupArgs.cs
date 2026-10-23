using CommandLine;
using PFWolf.Configuration;
using PFWolf.Loaders;

namespace PFWolf.Startup;

/// <summary>The command line, read</summary>
internal static class StartupArgs
{
    /// <summary>
    /// The command line's options, and its mods (--file, then the bare paths) with any relative
    /// path that exists made full, since relative mod paths mean the folder the game was started
    /// in. Bad arguments are reported to <paramref name="helpWriter"/> and otherwise ignored: run
    /// with the defaults. --file can be given more than once.
    /// </summary>
    internal static (GameParams Params, List<string> ModPaths) Parse(string[] args, TextWriter helpWriter)
    {
        using var parser = new Parser(settings =>
        {
            settings.HelpWriter = helpWriter;
            settings.AllowMultiInstance = true;
        });
        var gameParams = parser.ParseArguments<GameParams>(args).Value ?? new GameParams();
        var modPaths = gameParams.Files.Concat(gameParams.Paths).Select(ModSource.FullPathIfExists).ToList();

        // --make-mod's folders mean the folder the game was started in, too
        if (!string.IsNullOrWhiteSpace(gameParams.MakeMod))
            gameParams.MakeMod = Path.GetFullPath(gameParams.MakeMod);
        if (!string.IsNullOrWhiteSpace(gameParams.Out))
            gameParams.Out = Path.GetFullPath(gameParams.Out);
        if (!string.IsNullOrWhiteSpace(gameParams.Palette))
            gameParams.Palette = Path.GetFullPath(gameParams.Palette);
        return (gameParams, modPaths);
    }
}

/// <summary>Where the game's files are read from</summary>
internal static class GameFolder
{
    internal const string BasePk3 = "pfwolf.pk3";

    /// <summary>
    /// Files dropped on the exe start it in some other folder (often the dropped files' own), but
    /// pfwolf.pk3 and the game's data files are read from the working folder. When they aren't
    /// there and are beside the exe, work from there instead.
    /// </summary>
    internal static void Use()
    {
        if (Pick(Directory.GetCurrentDirectory(), AppContext.BaseDirectory) is { } folder)
            Directory.SetCurrentDirectory(folder);
    }

    /// <summary>The folder to move to (<paramref name="exeFolder"/>), or null to stay in <paramref name="workingFolder"/></summary>
    internal static string? Pick(string workingFolder, string exeFolder) =>
        !File.Exists(Path.Combine(workingFolder, BasePk3)) && File.Exists(Path.Combine(exeFolder, BasePk3))
            ? exeFolder
            : null;
}
