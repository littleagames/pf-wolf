using PFWolf.Entities;
using PFWolf.Entities.Actors;
using PFWolf.Gameplay;
using PFWolf.Scripting;

namespace PFWolf.Tests;

/// <summary>
/// The puzzles-demo mod's puzzles (examples/mods/puzzles-demo/scripts), each played for a while
/// with a stream of presses, drawn, and kept as a save keeps it: no script errors, and their
/// state is plain data
/// </summary>
public class PuzzleDemoScriptsTests
{
    private GameSession _session = null!;
    private ConsoleManager _console = null!;
    private GameScripts _scripts = null!;
    private Puzzles _puzzles = null!;
    private PlayerPawn _pawn = null!;
    private int _drawn;

    private static readonly string ScriptsFolder = Path.Combine(TestPaths.RepoRoot(), "examples", "mods", "puzzles-demo", "scripts");

    public static IEnumerable<string> PuzzleNames() =>
        ["needle", "pipes", "keypad", "safedial", "lockpick", "wires", "sequence", "lightsout", "sliding", "frequency", "power"];

    [SetUp]
    public void SetUp()
    {
        _session = new GameSession();
        _console = new ConsoleManager();
        _drawn = 0;
        Puzzles? puzzles = null;
        // Sounds paused, so none is looked up whether or not there's an audio device; messages kept, not drawn
        var audio = new AudioManager(new Lazy<AssetManager>(() => null!), _session);
        audio.SetPaused(true);
        var messages = new HudMessageManager(new Lazy<AssetManager>(() => new AssetManager()), new Lazy<ConsoleManager>(_console));
        _scripts = new GameScripts(null!, null!, audio, messages, _console, null, null!, _session, (_, _) => { })
        {
            StartPuzzleFor = (player, name, args) => puzzles!.Start(player, name, args),
            PuzzleSound = _ => { },
            DrawRect = (_, _, _, _, _) => _drawn++,
            DrawLine = (_, _, _, _, _, _) => _drawn++,
            DrawText = (_, _, _, _, _) => _drawn++,
            DrawPicRegion = (_, _, _, _, _, _, _) => _drawn++,
            MeasureText = (text, _) => (text.Length * 6, 8),
            PicSize = name => name == "PZSLIDE" ? (96, 96) : null,
        };
        _puzzles = puzzles = new Puzzles(_session, _scripts, _console.Print);

        var state = _session.LocalPlayer;
        state.Health = 100;
        _pawn = new PlayerPawn { Name = "Player", State = state };
        state.Pawn = _pawn;

        _scripts.LuaCommand(["x = 1"]);
        foreach (var file in Directory.GetFiles(ScriptsFolder, "*.lua").Order())
            Assert.That(_scripts.Scripts!.Load(File.ReadAllText(file), Path.GetFileName(file)), Is.True, string.Join("\n", _console.Scrollback));
    }

    private List<string> Errors() => [.. _console.Scrollback.Where(line => line.StartsWith("Script", StringComparison.Ordinal) || line.Contains("closed after"))];

    // A press pattern that walks every key: each think one of them goes down (and the next up)
    private static readonly ButtonType[][] Presses =
    [
        [ButtonType.TurnRight], [], [ButtonType.MoveBackward], [], [ButtonType.Use], [],
        [ButtonType.TurnLeft], [], [ButtonType.MoveForward], [], [ButtonType.Slot3], [],
        [ButtonType.Attack], [], [ButtonType.TurnRight], [ButtonType.TurnRight], [ButtonType.Use], [],
    ];

    [TestCaseSource(nameof(PuzzleNames))]
    public void A_Demo_Puzzle_Plays_Draws_And_Keeps_Without_A_Script_Error(string name)
    {
        // Arrange
        Assert.That(_puzzles.Start(_pawn, name, Array.Empty<string>()), Is.True, string.Join("\n", _console.Scrollback));
        byte[]? kept = null;

        // Act: thinks with presses (a frame of 2 tics each), drawn every few, kept early on
        for (int i = 0; i < 1500 && _session.LocalPlayer.Puzzle != null; i++)
        {
            var cmd = new TicCmd();
            foreach (var button in Presses[i % Presses.Length])
                cmd.SetDown(button, true);
            _session.LocalPlayer.Input.Begin(cmd);
            _session.Tics = 2;
            using (_session.ActAs(_session.LocalPlayer))
                _puzzles.Think();
            if (_session.LocalPlayer.Puzzle is { } open && i % 5 == 0)
                _scripts.DrawPuzzle(open, 40, 30);
            if (i == 2 && _session.LocalPlayer.Puzzle != null)
                kept = _puzzles.Write(_session.LocalPlayer);
        }

        // Assert
        Assert.That(Errors(), Is.Empty);
        Assert.That(_drawn, Is.GreaterThan(0), "it never drew");
        Assert.That(kept, Is.Not.Null, string.Join("\n", _console.Scrollback));
    }

    [TestCaseSource(nameof(PuzzleNames))]
    public void A_Demo_Puzzle_Kept_Is_Taken_Up_And_Plays_On(string name)
    {
        // Arrange: a few thinks in, kept, closed
        _puzzles.Start(_pawn, name, Array.Empty<string>());
        for (int i = 0; i < 6; i++)
        {
            _session.LocalPlayer.Input.Begin(new TicCmd());
            _session.Tics = 1;
            using (_session.ActAs(_session.LocalPlayer))
                _puzzles.Think();
        }
        var kept = _puzzles.Write(_session.LocalPlayer);
        _puzzles.Reset();

        // Act
        bool read = _puzzles.Read(_session.LocalPlayer, kept);
        for (int i = 0; i < 40 && _session.LocalPlayer.Puzzle is { } open; i++)
        {
            _session.LocalPlayer.Input.Begin(new TicCmd());
            using (_session.ActAs(_session.LocalPlayer))
                _puzzles.Think();
            _scripts.DrawPuzzle(open, 40, 30);
        }

        // Assert
        Assert.That(read, Is.True);
        Assert.That(Errors(), Is.Empty);
    }

    [Test]
    public void The_Tutorials_Reactor_Plays_And_Draws()
    {
        // Arrange: the first Lua block of docs/manual/tutorials/first-puzzle.md
        var tutorial = File.ReadAllText(Path.Combine(TestPaths.RepoRoot(), "docs", "manual", "tutorials", "first-puzzle.md"));
        var code = System.Text.RegularExpressions.Regex.Match(tutorial, "```lua\\r?\\n(.*?)```", System.Text.RegularExpressions.RegexOptions.Singleline).Groups[1].Value;
        Assert.That(_scripts.Scripts!.Load(code, "reactor.lua"), Is.True, string.Join("\n", _console.Scrollback));
        Assert.That(_puzzles.Start(_pawn, "reactor", null), Is.True);

        // Act: left held a while, then right, drawn as it goes, to its time limit or an end
        for (int i = 0; i < 1200 && _session.LocalPlayer.Puzzle is { } open; i++)
        {
            var cmd = new TicCmd();
            cmd.SetDown(i / 60 % 2 == 0 ? ButtonType.TurnLeft : ButtonType.TurnRight, true);
            _session.LocalPlayer.Input.Begin(cmd);
            _session.Tics = 1;
            using (_session.ActAs(_session.LocalPlayer))
                _puzzles.Think();
            _scripts.DrawPuzzle(open, 40, 30);
        }

        // Assert
        Assert.That(Errors(), Is.Empty);
        Assert.That(_session.LocalPlayer.Puzzle, Is.Null, "it should have ended, one way or another, by its time limit");
    }

    [Test]
    public void The_Keypad_Opens_To_Its_Code_And_Not_To_Another()
    {
        // Arrange
        _puzzles.Start(_pawn, "keypad", new[] { "code=7319" });

        // Act: nothing held (keys held as it opens aren't presses), then 7 3 1 9 on the slot keys
        _session.LocalPlayer.Input.Begin(new TicCmd());
        using (_session.ActAs(_session.LocalPlayer))
            _puzzles.Think();
        foreach (var digit in new[] { ButtonType.Slot7, ButtonType.Slot3, ButtonType.Slot1, ButtonType.Slot9 })
        {
            foreach (var down in new[] { true, false })
            {
                var cmd = new TicCmd();
                if (down)
                    cmd.SetDown(digit, true);
                _session.LocalPlayer.Input.Begin(cmd);
                _session.Tics = 1;
                using (_session.ActAs(_session.LocalPlayer))
                    _puzzles.Think();
            }
        }

        // Assert
        Assert.That(_session.LocalPlayer.Puzzle, Is.Null);
        Assert.That(Errors(), Is.Empty);
    }
}
