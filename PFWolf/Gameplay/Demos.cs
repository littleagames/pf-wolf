using PFWolf.Assets;

namespace PFWolf.Gameplay;

/// <summary>What playing and recording demos need from the game</summary>
/// <param name="NewGame">Starts a game on a skill, from a map, as a player class (GameHost.NewGame)</param>
/// <param name="DemoSkill">The skill demos play on: game-info's demo-skill, else the hardest</param>
/// <param name="DrawPlayScreen">Draws the status bar and view border</param>
/// <param name="SetupGameLevel">Loads the level and spawns everything in it</param>
/// <param name="RunLevelHooks">The scripts' game.onlevel hooks</param>
/// <param name="DemoTesting">demotest: report each demo when it's played through</param>
/// <param name="DemoTestReport">How a demo ended: score, kills, the player and the actors' hash</param>
/// <param name="IntroSong">The title's music, back on after a demo</param>
internal sealed record DemosHooks(
    Action<short, EpisodeInfo, MapInfo, string> NewGame,
    Func<short> DemoSkill,
    Action DrawPlayScreen,
    Action SetupGameLevel,
    Action<string> RunLevelHooks,
    Func<bool> DemoTesting,
    Func<int, string> DemoTestReport,
    Func<string> IntroSong);

/// <summary>
/// A recording recorddemo asked for; the title loop starts it next (a game in progress ends first)
/// </summary>
internal record DemoRecordRequest(int Level, int? DemoNumber);

/// <summary>
/// Demos in id's format: the game's own and recorded ones played back (PlayDemo), recorded from
/// a level's start to its end and saved as DEMO0-9.dmo in the demos folder (RecordDemo), and the
/// ones the console's playdemo and recorddemo ask the title loop for.
/// </summary>
internal sealed class Demos(
    AssetManager assets,
    GameEngineManager engine,
    VideoManager video,
    AudioManager audio,
    UiText ui,
    LineInput lineInput,
    DemoBuffer demo,
    LevelMusic music,
    LevelLoop loop,
    GameSession session,
    DemosHooks hooks)
{
    /// <summary>
    /// Plays demo <paramref name="demonumber"/>: one recorded with that number, else the game's
    /// own; or <paramref name="data"/> when given (demotrace's file), numbered as asked
    /// </summary>
    internal void PlayDemo(int demonumber, byte[]? data = null)
    {
        short length;

        if ((data ?? DemoData(demonumber)) is not { } found)
            return;
        demo.DemoData = found;
        demo.DemoPtr = 0;

        // id's header: the floor in the first episode (0 = MAP01), a 16-bit length counting the
        // header, and a pad byte. Every demo plays on game-info's demo-skill (the hardest, as id's did).
        if (demo.DemoData.Length < 4)
            return;

        var mapName = $"MAP{demo.DemoData[0] + 1:D2}";
        if (!engine.GetGameInfo().Maps.TryGetValue(mapName, out var mapInfo))
            return;

        length = BitConverter.ToInt16(demo.DemoData, 1);
        demo.DemoPtr = 4;
        demo.LastDemoPtr = Math.Min((int)length, demo.DemoData.Length);   // stop at the data's end if the length overshoots
        if (demo.LastDemoPtr - demo.DemoPtr < 3)
            return;

        hooks.NewGame(hooks.DemoSkill(), new EpisodeInfo { StartMap = mapName }, mapInfo, PlayerClassRules.BasePlayerClass);   // recorded as Player: another class's stats would change how it plays

        video.FadeOut();

        hooks.DrawPlayScreen();

        session.GameChosen = false;
        session.DemoPlayback = true;

        hooks.SetupGameLevel();
        hooks.RunLevelHooks("new");       // as when it was recorded (RecordDemo)
        music.StartMusic();

        loop.PlayLoop();
        if (hooks.DemoTesting())
            Console.WriteLine(hooks.DemoTestReport(demonumber));

        session.DemoPlayback = false;

        music.StopMusic();
        audio.StopAll();
    }

    /// <summary>Demo <paramref name="demonumber"/>'s data: one recorded with that number plays in place of the game's own</summary>
    internal byte[]? DemoData(int demonumber)
        => ReadRecordedDemo(demonumber) ?? assets.Find<DemoAsset>($"demo{demonumber}")?.RawData;

    /// <summary>
    /// A demo by what the console was given: a number (0-9), a demo's name (a mod's
    /// demos/NAME.dmo), else a .dmo file. Null when there's none of those.
    /// </summary>
    /// <returns>Its number (-1 for one by name or file), its data, and a name for it</returns>
    internal (int Number, byte[] Data, string Label)? FindDemo(string what)
    {
        if (what.Length == 1 && int.TryParse(what, out var number))
            return DemoData(number) is { } numbered ? (number, numbered, what) : null;

        var name = Path.GetFileNameWithoutExtension(what);
        if (!what.Contains('/') && !what.Contains('\\') && assets.Exists<DemoAsset>(name) && assets.Find<DemoAsset>(name) is { } asset)
            return (-1, asset.RawData, name);

        try
        {
            return File.Exists(what) ? (-1, File.ReadAllBytes(what), Path.GetFileName(what).Replace(' ', '_')) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Console.WriteLine($"Couldn't read the demo {what}: {e.Message}");
            return null;
        }
    }

    /// <summary>The names of the demos the packs and mods have (demos/NAME.dmo and the data files' DEMO0-9)</summary>
    internal IEnumerable<string> DemoNames() => assets.NamesOf<DemoAsset>();

    internal const int MaxDemoSize = 8192;

    /// <summary>
    /// A demo asked for by playdemo: its number (-1 for one by name or file) and data; the title
    /// loop plays it next (a game in progress ends first)
    /// </summary>
    internal (int Number, byte[] Data)? PendingDemo;

    /// <summary>Whether demo number <paramref name="demonumber"/> has been recorded or comes with the game</summary>
    internal bool DemoExists(int demonumber)
        => File.Exists(DemoFilePath(demonumber)) || assets.Exists<DemoAsset>($"demo{demonumber}");

    /// <summary>
    /// Plays the demo playdemo asked for, if any, leaving the screen faded and the title music on
    /// </summary>
    internal void PlayPendingDemo()
    {
        if (PendingDemo is not { } pending)
            return;

        PendingDemo = null;
        PlayDemo(pending.Number, pending.Data);
        video.FadeOut();
        if (video.HasMargins)
            video.ClearScreen(0x00);
        audio.PlayMusic(hooks.IntroSong());
    }

    /// <summary>A recorded demo's file: DEMO0.dmo to DEMO9.dmo in the demos folder</summary>
    internal string DemoFilePath(int demonumber)
        => Path.Combine(engine.ConfigDirectories.DemosDirectory ?? "", $"DEMO{demonumber}.dmo");

    internal byte[]? ReadRecordedDemo(int demonumber)
    {
        var path = DemoFilePath(demonumber);
        try
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Couldn't read the demo {path}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Starts recording in id's format: the map's number less one (0 = MAP01) in the first byte,
    /// then room for the length, then 3 bytes a frame from PollControls
    /// </summary>
    internal void StartDemoRecord(int mapIndex)
    {
        demo.DemoData = new byte[MaxDemoSize];
        demo.DemoData[0] = (byte)mapIndex;
        demo.DemoPtr = 4;                            // leave space for length
        demo.LastDemoPtr = MaxDemoSize;
        session.DemoRecord = true;
    }

    /// <summary>
    /// Ends the recording and saves it as demo <paramref name="demonumber"/>, or asks which
    /// number to save it as when none was given (Esc throws it away)
    /// </summary>
    internal void FinishDemoRecord(int? demonumber = null)
    {
        int length;

        session.DemoRecord = false;

        // The length counts the 4 header bytes; the byte after it is padding
        length = demo.DemoPtr;
        demo.DemoData[1] = (byte)length;
        demo.DemoData[2] = (byte)(length >> 8);
        demo.DemoData[3] = 0;

        if (demonumber == null)
        {
            video.FadeIn();
            var window = ui.CenterWindow(24, 3, UiText.PromptStyle);
            window.PrintY += 6;
            window.Print(" Demo number (0-9): ");
            video.Update();

            string str = "";
            if (lineInput.Run(window.PrintX, window.PrintY, ref str, "", true, 1, 0, window.Style)
                && int.TryParse(str, out int typed) && typed >= 0 && typed <= 9)
                demonumber = typed;
        }

        if (demonumber is int number)
            SaveDemo(number, demo.DemoData[..length]);

        demo.DemoData = [];
    }

    internal void SaveDemo(int demonumber, byte[] data)
    {
        var path = DemoFilePath(demonumber);
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllBytes(path, data);
            Console.WriteLine($"Recorded demo saved to {path}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Couldn't save the demo to {path}: {e.Message}");
        }
    }

    //==========================================================================

    /*
    ==================
    =
    = RecordDemo
    =
    = Fades the screen out, then starts a demo.  Exits with the screen faded
    =
    ==================
    */
    internal void RecordDemo()
    {
        int maps = DemoMapCount();
        if (maps == 0)
            return;

        // Clear what's behind: a title drawn in its own palette (Spear's) is noise in the game's
        video.ClearScreen(0);
        var window = ui.CenterWindow(26, 3, UiText.PromptStyle);
        window.PrintY += 6;
        window.Print($"  Demo which level(1-{maps}): ");
        video.Update();
        video.FadeIn();
        string str = "";
        var esc = !lineInput.Run(window.PrintX, window.PrintY, ref str, "", true, 2, 0, window.Style);
        if (esc || !int.TryParse(str, out int level) || level < 1 || level > maps)
            return;

        RecordDemo(level);
    }

    /// <summary>
    /// How many maps a demo can be recorded on: a demo names its map by one byte, so it can be
    /// any of MAP01 to MAP99 the game has, counting up from MAP01
    /// </summary>
    internal int DemoMapCount()
    {
        var gameInfo = engine.GetGameInfo();
        int maps = 0;
        while (maps < 99 && gameInfo.Maps.ContainsKey($"MAP{maps + 1:D2}"))
            maps++;
        return maps;
    }

    internal DemoRecordRequest? PendingRecord;

    /// <summary>
    /// Starts the recording recorddemo asked for, if any. Returns whether one ran.
    /// </summary>
    internal bool RecordPendingDemo()
    {
        if (PendingRecord is not { } request)
            return false;

        PendingRecord = null;
        RecordDemo(request.Level, request.DemoNumber);
        return true;
    }

    /// <summary>
    /// Records a demo of MAP<paramref name="level"/> on game-info's demo-skill until the level ends, then
    /// saves it as <paramref name="demonumber"/>, or asks for a number when none is given
    /// </summary>
    internal void RecordDemo(int level, int? demonumber = null)
    {
        var gameInfo = engine.GetGameInfo();
        var mapName = $"MAP{level:D2}";
        video.FadeOut();
        hooks.NewGame(hooks.DemoSkill(), new EpisodeInfo { StartMap = mapName }, gameInfo.Maps[mapName], PlayerClassRules.BasePlayerClass);
        StartDemoRecord(level - 1);

        hooks.DrawPlayScreen();
        video.FadeIn();

        session.GameChosen = false;
        session.DemoRecord = true;

        hooks.SetupGameLevel();
        hooks.RunLevelHooks("new");       // as when it's played back (PlayDemo)
        music.StartMusic();

        session.FizzleIn = true;

        loop.PlayLoop();

        session.DemoPlayback = false;

        music.StopMusic();
        video.FadeOut();
        audio.StopAll();

        FinishDemoRecord(demonumber);
    }
}
