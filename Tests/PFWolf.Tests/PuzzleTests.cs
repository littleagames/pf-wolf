using PFWolf.Assets;
using PFWolf.Entities;
using PFWolf.Entities.Actors;
using PFWolf.Gameplay;
using PFWolf.Scripting;

namespace PFWolf.Tests;

/// <summary>
/// Puzzles (Gameplay/Puzzles.cs) run from a script's game.puzzle (Scripting/GameScripts.Puzzles.cs):
/// opening, the controls going to them, presses, backing out, time limits, errors, the results the
/// scripts hear of, and what their drawing may do
/// </summary>
public class PuzzleTests
{
    private GameSession _session = null!;
    private ConsoleManager _console = null!;
    private GameScripts _scripts = null!;
    private Puzzles _puzzles = null!;
    private PlayerPawn _pawn = null!;
    private List<string> _drawn = null!;

    // A puzzle that counts presses of use, and is won at args.need (2); its result goes to onpuzzle
    private const string CountingPuzzle = """
        game.puzzle("count", {
          title = "Count",
          start = function(pz, args) pz.state.need = args.need or 2 pz.state.count = 0 end,
          tick = function(pz, input)
            if input.pressed.use then pz.state.count = pz.state.count + 1 end
            if pz.state.count >= pz.state.need then return "success" end
          end,
        })
        game.onpuzzle(function(player, name, result, x, y, tag) print("puzzle", name, result, x, y, tag) end)
        """;

    [SetUp]
    public void SetUp()
    {
        _session = new GameSession();
        _console = new ConsoleManager();
        _drawn = [];
        Puzzles? puzzles = null;
        _scripts = new GameScripts(null!, null!, null!, null!, _console, null, null!, _session, (_, _) => { })
        {
            StartPuzzleFor = (player, name, args) => puzzles!.Start(player, name, args),
            PuzzlePanel = () => new PuzzlePanelInfo { Font = "PanelFont", TextColor = "PanelColor" },
            DrawRect = (x, y, width, height, color) => _drawn.Add($"rect {x},{y} {width}x{height} {color}"),
            DrawText = (text, x, y, font, color) => _drawn.Add($"text {text} {x},{y} {font} {color}"),
        };
        _puzzles = puzzles = new Puzzles(_session, _scripts, _console.Print);

        var state = _session.LocalPlayer;
        state.Health = 100;
        _pawn = new PlayerPawn { Name = "Player", State = state };
        state.Pawn = _pawn;
    }

    // The lua command makes the Lua state; scripts then load into it as they would at startup
    private ScriptHost Load(string script)
    {
        _scripts.LuaCommand(["x = 1"]);
        var host = _scripts.Scripts!;
        Assert.That(host.Load(script, "puzzles.lua"), Is.True, string.Join("\n", Printed()));
        return host;
    }

    private List<string> Printed() => [.. _console.Scrollback];

    private PlayerState State => _session.LocalPlayer;

    // One think of the player's with these buttons down: whether it was the puzzle's
    private bool Think(params ButtonType[] down) => Think(new TicCmd(), down);

    private bool Think(TicCmd cmd, params ButtonType[] down)
    {
        foreach (var button in down)
            cmd.SetDown(button, true);
        State.Input.Begin(cmd);
        _session.Tics = 1;
        using (_session.ActAs(State))
            return _puzzles.Think();
    }

    [Test]
    public void A_Puzzle_Takes_The_Players_Controls_Until_Its_Script_Ends_It()
    {
        // Arrange
        Load(CountingPuzzle);

        // Act: opened from a switch at 5,6 with tag 7, then use pressed twice
        bool opened = _puzzles.Start(_pawn, "count", null, 5, 6, 7);
        bool[] thinks = [Think(), Think(ButtonType.Use), Think(), Think(ButtonType.Use)];

        // Assert: each think was the puzzle's, and it ended in a success the hooks heard of
        Assert.That(opened, Is.True);
        Assert.That(thinks, Is.All.True);
        Assert.That(State.Puzzle, Is.Null);
        Assert.That(Printed(), Has.Some.EqualTo("puzzle\tcount\tsuccess\t5\t6\t7"));
    }

    [Test]
    public void Use_Held_As_A_Puzzle_Opens_Isnt_A_Press_Until_Its_Let_Go()
    {
        // Arrange: opened by use, which is still held
        Load(CountingPuzzle);
        _puzzles.Start(_pawn, "count", new[] { "need=1" });

        // Act / Assert
        Think(ButtonType.Use);
        Think(ButtonType.Use);
        Assert.That(State.Puzzle, Is.Not.Null, "use held from before counted as a press");
        Think();
        Think(ButtonType.Use);
        Assert.That(State.Puzzle, Is.Null);
    }

    [Test]
    public void Use_Held_From_A_Closed_Puzzle_Counts_For_Nothing_Until_Its_Let_Go()
    {
        // Arrange: won by a press of use
        Load(CountingPuzzle);
        _puzzles.Start(_pawn, "count", new[] { "need=1" });
        Think();
        Think(ButtonType.Use);

        // Act: use still held, then let go of, then pressed again
        bool heldThink = Think(ButtonType.Use);
        bool heldCounted = State.Input.IsPressed(ButtonType.Use);
        Think();
        Think(ButtonType.Use);

        // Assert
        Assert.That(heldThink, Is.False, "with the puzzle closed, the think is the player's again");
        Assert.That(heldCounted, Is.False);
        Assert.That(State.Input.IsPressed(ButtonType.Use), Is.True);
        Assert.That(State.PuzzleButtonsHeld, Is.False);
    }

    [TestCase(null, "cancel")]
    [TestCase("nothing", "cancel")]
    [TestCase("fail", "fail")]
    public void Esc_Backs_Out_Of_A_Puzzle(string? cancel, string result)
    {
        // Arrange
        var setting = cancel == null ? "" : $"cancel = '{cancel}',";
        Load($"game.puzzle('p', {{ {setting} tick = function() end }}) game.onpuzzle(function(_, name, result) print('ended', result) end)");
        _puzzles.Start(_pawn, "p", null);

        // Act
        Think();
        Think(ButtonType.Esc);

        // Assert
        Assert.That(State.Puzzle, Is.Null);
        Assert.That(Printed(), Has.Some.EqualTo($"ended\t{result}"));
    }

    [Test]
    public void A_Puzzle_That_Cant_Be_Backed_Out_Of_Stays_Open()
    {
        // Arrange
        Load("game.puzzle('p', { cancel = 'never', tick = function() end })");
        _puzzles.Start(_pawn, "p", null);

        // Act
        Think();
        Think(ButtonType.Esc);

        // Assert
        Assert.That(State.Puzzle, Is.Not.Null);
    }

    [Test]
    public void A_Puzzles_Time_Running_Out_Is_A_Fail()
    {
        // Arrange: three tics long; it prints the time left each tick
        Load("""
            game.puzzle('p', { timelimit = 3, tick = function(pz) print('left', pz.timeleft) end })
            game.onpuzzle(function(_, name, result) print('ended', result) end)
            """);
        _puzzles.Start(_pawn, "p", null);

        // Act
        Think();
        Think();
        bool openAfterTwo = State.Puzzle != null;
        Think();

        // Assert
        Assert.That(openAfterTwo, Is.True);
        Assert.That(State.Puzzle, Is.Null);
        Assert.That(Printed(), Has.Some.EqualTo("left\t1").And.Some.EqualTo("ended\tfail"));
    }

    [Test]
    public void A_Puzzle_Whose_Script_Keeps_Failing_Is_Closed()
    {
        // Arrange
        Load("game.puzzle('broken', { tick = function() error('oops') end })");
        _puzzles.Start(_pawn, "broken", null);

        // Act
        for (int i = 0; i < Puzzles.ErrorLimit; i++)
            Think();

        // Assert
        Assert.That(State.Puzzle, Is.Null);
        Assert.That(Printed(), Has.Some.Contains("closed after 3 errors"));
    }

    [Test]
    public void A_Puzzles_Words_Come_To_Its_Start_By_Key_And_In_Order()
    {
        // Arrange
        Load("game.puzzle('p', { start = function(pz, args) print(args.size + 1, args[1], args[2]) end, tick = function() end })");

        // Act
        _puzzles.Start(_pawn, "p", new[] { "size=6", "red", "3" });

        // Assert: numbers as numbers
        Assert.That(Printed(), Has.Some.EqualTo("7\tred\t3"));
    }

    [Test]
    public void A_Script_Opens_A_Puzzle_With_A_Table_Of_Its_Own()
    {
        // Arrange
        var host = Load(CountingPuzzle);

        // Act: me is the player (the lua command's)
        var error = host.Execute("print('opened', game.startpuzzle(me, 'count', { need = 1 }))");
        Think();
        Think(ButtonType.Use);

        // Assert
        Assert.That(error, Is.Null);
        Assert.That(Printed(), Has.Some.EqualTo("opened\ttrue"));
        Assert.That(Printed(), Has.Some.EqualTo("puzzle\tcount\tsuccess\t-1\t-1\t0"));
    }

    [Test]
    public void Puzzles_Are_Made_Only_As_A_Script_Loads()
    {
        // Arrange: a tick hook that tries to make one, later
        var host = Load("game.ontick(function() print('late', pcall(game.puzzle, 'late', { tick = function() end })) end)");

        // Act
        _scripts.RunTickHooks();
        var noTick = host.Execute("game.puzzle('broken', {})");

        // Assert
        Assert.That(Printed(), Has.Some.Matches<string>(line => line.StartsWith("late\tfalse") && line.Contains("made when a script loads")));
        Assert.That(noTick, Does.Contain("needs a tick function"));
        Assert.That(_scripts.FindPuzzle("late"), Is.Null);
    }

    [Test]
    public void A_Puzzle_Doesnt_Open_In_A_Demo_Or_With_Others_Yet_Nor_Twice()
    {
        // Arrange
        Load(CountingPuzzle);

        // Act / Assert
        _session.DemoRecord = true;
        Assert.That(_puzzles.Start(_pawn, "count", null), Is.False);
        _session.DemoRecord = false;
        _session.NetGame = true;
        Assert.That(_puzzles.Start(_pawn, "count", null), Is.False);
        _session.NetGame = false;
        Assert.That(_puzzles.Start(_pawn, "nothere", null), Is.False);
        Assert.That(_puzzles.Start(_pawn, "count", null), Is.True);
        Assert.That(_puzzles.Start(_pawn, "count", null), Is.False);
        Assert.That(Printed(), Has.Some.Contains("don't open in a demo").And.Some.Contains("no puzzle of that name"));
    }

    [Test]
    public void Only_A_Puzzle_That_Pauses_Stops_The_Level_And_Only_Playing_Alone()
    {
        // Arrange
        Load("game.puzzle('paused', { tick = function() end }) game.puzzle('live', { pause = false, tick = function() end })");

        // Act / Assert
        Assert.That(_puzzles.FreezesWorld, Is.False);
        _puzzles.Start(_pawn, "paused", null);
        Assert.That(_puzzles.FreezesWorld, Is.True);
        _session.NetGame = true;
        Assert.That(_puzzles.FreezesWorld, Is.False);
        _session.NetGame = false;
        _puzzles.Reset();
        _puzzles.Start(_pawn, "live", null);
        Assert.That(_puzzles.FreezesWorld, Is.False);
    }

    [Test]
    public void A_Puzzles_Draw_Draws_In_Its_Panel_In_The_Panels_Font_And_Cant_Change_The_Game()
    {
        // Arrange
        Load("""
            game.puzzle('p', {
              tick = function() end,
              draw = function(pz, width, height)
                game.drawrect(1, 2, width, height, 'Red')
                game.drawtext('hi', 0, 0)
                print('random', pcall(game.random))
              end,
            })
            """);
        _puzzles.Start(_pawn, "p", null);
        int randomBefore = _session.Random.RndIndex;

        // Act: its panel at 40,30
        _scripts.DrawPuzzle(State.Puzzle!, 40, 30);

        // Assert
        Assert.That(_drawn, Is.EqualTo(new[] { "rect 41,32 240x140 Red", "text hi 40,30 PanelFont PanelColor" }));
        Assert.That(Printed(), Has.Some.StartsWith("random\tfalse"));
        Assert.That(_session.Random.RndIndex, Is.EqualTo(randomBefore));
    }

    [Test]
    public void The_Players_Controls_Come_To_A_Puzzle_As_Directions_Digits_And_The_Cursor()
    {
        // Arrange
        var definition = new PuzzleDefinition("p", "", "", true, PuzzleCancel.Nothing, 0, 240, 140);
        var puzzle = new OpenPuzzle(definition, _pawn, State, -1, -1, 0) { PreviousKeys = PuzzleKeys.None };
        var input = new PlayerInput();
        var cmd = new TicCmd { ControlStrafe = 100, ControlX = -7, ControlY = 3 };
        cmd.SetDown(ButtonType.MoveForward, true);
        cmd.SetDown(ButtonType.Slot3, true);
        input.Begin(cmd);

        // Act
        var keys = Puzzles.ReadKeys(puzzle, input, 1);
        var again = Puzzles.ReadKeys(puzzle, input, 1);

        // Assert: a stick pushed right is right; the cursor is the turn and walk; held the next time
        Assert.That(keys.Held, Is.EqualTo(PuzzleKeys.Up | PuzzleKeys.Right | Puzzles.DigitKey(3)));
        Assert.That(keys.Pressed, Is.EqualTo(keys.Held));
        Assert.That((keys.Digit, keys.Dx, keys.Dy), Is.EqualTo((3, -7, 3)));
        Assert.That(again.Pressed, Is.EqualTo(PuzzleKeys.None));
        Assert.That(again.Digit, Is.EqualTo(-1));
    }
}
