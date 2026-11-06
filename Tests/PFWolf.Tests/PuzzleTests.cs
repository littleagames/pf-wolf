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
    public void A_Puzzle_Doesnt_Open_Twice_Nor_One_There_Isnt()
    {
        // Arrange
        Load(CountingPuzzle);

        // Act / Assert
        Assert.That(_puzzles.Start(_pawn, "nothere", null), Is.False);
        Assert.That(_puzzles.Start(_pawn, "count", null), Is.True);
        Assert.That(_puzzles.Start(_pawn, "count", null), Is.False);
        Assert.That(Printed(), Has.Some.Contains("no puzzle of that name"));
    }

    [Test]
    public void A_Switchs_Result_Comes_Before_The_Scripts_Hear_Of_It()
    {
        // Arrange: opened from a switch
        Load(CountingPuzzle);
        _puzzles.SwitchEnded = (origin, result) => _console.Print($"switch {result} {origin.X},{origin.Y}");
        _puzzles.Start(_pawn, "count", new[] { "need=1" }, 4, 5, 0, Switch(4, 5));

        // Act
        Think();
        Think(ButtonType.Use);

        // Assert
        var printed = Printed();
        Assert.That(printed.IndexOf("switch success 4,5"), Is.GreaterThanOrEqualTo(0).And.LessThan(printed.FindIndex(line => line.StartsWith("puzzle\tcount"))));
    }

    private static SwitchPuzzle Switch(int x, int y) => new(false, x, y, ControlDir.East, 3, 70, ["GoldKey"]);

    // A puzzle that keeps all sorts in its state, and one that keeps a function there
    private const string KeepingPuzzles = """
        game.puzzle("keep", {
          start = function(pz, args)
            pz.state.grid = { { 1, 2 }, { 3, 4 } }
            pz.state.name = "safe"
            pz.state.open = false
            pz.state[7] = 0.5
          end,
          tick = function(pz, input)
            if input.pressed.use then
              print("kept", pz.state.grid[2][1], pz.state.name, pz.state.open, pz.state[7], pz.args.size, pz.tics)
              return "success"
            end
          end,
        })
        game.puzzle("fn", { start = function(pz) pz.state.later = { f = function() end } end, tick = function() end })
        game.onpuzzle(function(player, name, result, x, y, tag) print("puzzle", name, result, x, y, tag) end)
        """;

    [Test]
    public void An_Open_Puzzle_Is_Kept_And_Taken_Up_Where_It_Was()
    {
        // Arrange: opened from a switch, three tics in
        Load(KeepingPuzzles);
        _puzzles.SwitchEnded = (origin, result) => _console.Print($"switch {result} {origin.X},{origin.Y} {origin.Dir} {origin.Tag} {origin.Id} {string.Join(",", origin.Keys)}");
        _puzzles.Start(_pawn, "keep", new[] { "size=6" }, 4, 5, 3, Switch(4, 5));
        Think();
        Think();
        Think();

        // Act: kept, closed, taken up again, then won
        var kept = _puzzles.Write(State);
        _puzzles.Reset();
        bool read = _puzzles.Read(State, kept);
        Think();
        Think(ButtonType.Use);

        // Assert: its state, args and clock as they were (one tic more), and its switch's result
        Assert.That(read, Is.True);
        Assert.That(Printed(), Has.Some.EqualTo("kept\t3\tsafe\tfalse\t0.5\t6\t5"));
        Assert.That(Printed(), Has.Some.EqualTo("switch success 4,5 East 3 70 GoldKey"));
        Assert.That(Printed(), Has.Some.EqualTo("puzzle\tkeep\tsuccess\t4\t5\t3"));
    }

    [Test]
    public void A_Puzzle_Keeping_A_Function_Isnt_Kept_And_Says_Where_It_Is()
    {
        // Arrange
        Load(KeepingPuzzles);
        _puzzles.Start(_pawn, "fn", null);

        // Act
        var kept = _puzzles.Write(State);

        // Assert
        Assert.That(kept, Is.Null);
        Assert.That(Printed(), Has.Some.Contains("pz.state.later.f is a function"));
    }

    [Test]
    public void Someone_Joining_Mid_Game_Closes_Only_The_Puzzles_That_Cant_Go_To_Them()
    {
        // Arrange: two players, one working a puzzle that can be sent, the other one that can't
        Load(KeepingPuzzles);
        var other = new PlayerState { Health = 100, Name = "Alice" };
        other.Pawn = new PlayerPawn { Name = "Player", State = other };
        _session.Players.Add(other);
        _session.NetGame = true;
        _puzzles.Start(_pawn, "keep", null, 1, 1, 0);
        _puzzles.Start(other.Pawn!, "fn", null, 2, 2, 0);
        bool othersShown = _puzzles.OthersPuzzles().Single().State == other;

        // Act
        _puzzles.CloseUnsendable();

        // Assert
        Assert.That(othersShown, Is.True);
        Assert.That(State.Puzzle, Is.Not.Null);
        Assert.That(other.Puzzle, Is.Null);
        Assert.That(Printed(), Has.Some.EqualTo("puzzle\tfn\tcancel\t2\t2\t0"));
    }

    [Test]
    public void A_Player_Dying_Closes_Their_Puzzle()
    {
        // Arrange
        Load(CountingPuzzle);
        _puzzles.Start(_pawn, "count", null);

        // Act
        using (_session.ActAs(State))
            _puzzles.CloseForDeath();

        // Assert
        Assert.That(State.Puzzle, Is.Null);
        Assert.That(Printed(), Has.Some.StartsWith("puzzle\tcount\tcancel"));
    }

    [Test]
    public void A_Kept_Puzzle_Thats_Gone_From_The_Scripts_Is_Closed()
    {
        // Arrange: kept in one game, read in another with no such puzzle
        Load(CountingPuzzle);
        _puzzles.Start(_pawn, "count", null);
        var kept = _puzzles.Write(State);
        SetUp();
        Load("x = 1");

        // Act
        bool read = _puzzles.Read(State, kept);

        // Assert
        Assert.That(read, Is.False);
        Assert.That(State.Puzzle, Is.Null);
        Assert.That(Printed(), Has.Some.Contains("isn't there any more"));
    }

    [Test]
    public void In_A_Demo_A_Puzzle_Reads_Only_What_A_Demo_Keeps()
    {
        // Arrange: turning right and walking forward, Esc and slot 5 held, slot 2 pressed
        var definition = new PuzzleDefinition("p", "", "", true, PuzzleCancel.Nothing, 0, 240, 140);
        var puzzle = new OpenPuzzle(definition, _pawn, State, -1, -1, 0) { PreviousKeys = PuzzleKeys.None };
        var input = new PlayerInput();
        var cmd = new TicCmd { ControlX = 35, ControlY = -35 };
        foreach (var button in new[] { ButtonType.Esc, ButtonType.Slot5, ButtonType.Slot2, ButtonType.TurnLeft })
            cmd.SetDown(button, true);
        input.Begin(cmd);

        // Act
        var keys = Puzzles.ReadKeys(puzzle, input, 1, demo: true);

        // Assert: the turn and walk, not the buttons a demo hasn't got
        Assert.That(keys.Held, Is.EqualTo(PuzzleKeys.Up | PuzzleKeys.Right | Puzzles.DigitKey(2)));
        Assert.That(keys.Digit, Is.EqualTo(2));
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
