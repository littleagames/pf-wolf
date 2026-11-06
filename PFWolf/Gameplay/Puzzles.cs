using PFWolf.Entities;
using PFWolf.Entities.Actors;

namespace PFWolf.Gameplay;

/// <summary>What backing out of a puzzle (Esc) comes to</summary>
internal enum PuzzleCancel
{
    /// <summary>No result: the puzzle's left as it was, to be tried again</summary>
    Nothing,

    /// <summary>A fail</summary>
    Fail,

    /// <summary>It can't be backed out of: once started, it's finished</summary>
    Never,
}

/// <summary>A puzzle a script made (game.puzzle): its name and how it runs</summary>
/// <param name="Name">What starts it (A_Puzzle("name"), game.startpuzzle)</param>
/// <param name="Title">Shown over its panel; none when empty</param>
/// <param name="Verb">What others see the player doing ("hacking")</param>
/// <param name="Pause">The level stops while it's open (playing alone)</param>
/// <param name="Cancel">What Esc comes to</param>
/// <param name="TimeLimit">Tics it may be open before it's a fail; 0 for no limit</param>
/// <param name="Width">Its panel's width, in layout pixels</param>
/// <param name="Height">Its panel's height</param>
internal sealed record PuzzleDefinition(string Name, string Title, string Verb, bool Pause, PuzzleCancel Cancel, int TimeLimit, int Width, int Height)
{
    internal const int DefaultWidth = 240, DefaultHeight = 140;
}

/// <summary>The keys a puzzle reads, held or just pressed: directions, use, fire, back out, and the digits</summary>
[Flags]
internal enum PuzzleKeys : uint
{
    None = 0,
    Up = 1 << 0,
    Down = 1 << 1,
    Left = 1 << 2,
    Right = 1 << 3,
    Use = 1 << 4,
    Fire = 1 << 5,
    Cancel = 1 << 6,
    Digit0 = 1 << 7,        // Digit0 + n for n
}

/// <summary>
/// A puzzle's controls for one think, from the player's TicCmd (so the same on every machine):
/// what's held, what was just pressed, a digit just pressed (-1 for none), the cursor's
/// movement (mouse or stick) and the tics the think covers
/// </summary>
internal readonly record struct PuzzleInput(PuzzleKeys Held, PuzzleKeys Pressed, int Digit, int Dx, int Dy, int Tics)
{
    internal bool IsHeld(PuzzleKeys key) => (Held & key) != 0;
    internal bool IsPressed(PuzzleKeys key) => (Pressed & key) != 0;
}

/// <summary>A puzzle a player has open: what it is, who's working it, where it was started, and how far it's got</summary>
internal sealed class OpenPuzzle(PuzzleDefinition definition, PlayerPawn player, PlayerState state, int x, int y, int tag)
{
    internal PuzzleDefinition Definition { get; } = definition;
    internal PlayerPawn Player { get; } = player;
    internal PlayerState State { get; } = state;

    /// <summary>The tile it was started from (a switch's), -1, -1 for none</summary>
    internal int X { get; } = x;
    internal int Y { get; } = y;

    /// <summary>The tag it was started with (the switch's), 0 for none</summary>
    internal int Tag { get; } = tag;

    /// <summary>Tics it's been open</summary>
    internal int Tics { get; set; }

    /// <summary>Tics left before its time limit, or null for none</summary>
    internal int? TimeLeft => Definition.TimeLimit > 0 ? Math.Max(Definition.TimeLimit - Tics, 0) : null;

    /// <summary>A result the script gave out of turn (pz:finish), taken at the end of its think</summary>
    internal string? PendingResult { get; set; }

    /// <summary>The keys held last think, to tell fresh presses from held ones: all of them as it opens</summary>
    internal PuzzleKeys PreviousKeys { get; set; } = (PuzzleKeys)uint.MaxValue;

    /// <summary>Its script's errors in a row; it's given up on at <see cref="Puzzles.ErrorLimit"/></summary>
    internal int Errors { get; set; }

    /// <summary>The script's own (its Lua table, Scripting/GameScripts.Puzzles.cs)</summary>
    internal object? Handle { get; set; }

    /// <summary>The switch it was opened from, whose result throws it or runs its fail actions (Gameplay/PuzzleSwitches.cs); null for none</summary>
    internal SwitchPuzzle? Switch { get; init; }
}

/// <summary>What a puzzle's script did with a think: the result it gave (null: still going), and whether it erred</summary>
internal readonly record struct PuzzleTickResult(string? Result, bool Failed);

/// <summary>The scripts' side of the puzzles (Scripting/GameScripts.Puzzles.cs)</summary>
internal interface IPuzzleScripts
{
    /// <summary>The puzzle a script made with that name, or null</summary>
    PuzzleDefinition? FindPuzzle(string name);

    /// <summary>Starts a puzzle's script with its arguments (A_Puzzle's words, or a script's table): false if it erred, and it isn't opened</summary>
    bool StartPuzzle(OpenPuzzle puzzle, object? args);

    /// <summary>A think of the puzzle's</summary>
    PuzzleTickResult TickPuzzle(OpenPuzzle puzzle, in PuzzleInput input);

    /// <summary>A puzzle's ended (game.onpuzzle): "success", "fail", "cancel" or a result of the script's own</summary>
    void PuzzleFinished(OpenPuzzle puzzle, string result);

    /// <summary>
    /// Writes what a puzzle's script keeps (pz.state and pz.args), for a save or a player
    /// joining: null if it went, else what in it can't be kept (a function, an actor: "pz.state.f
    /// is a function"), and nothing's written
    /// </summary>
    string? WritePuzzleState(OpenPuzzle puzzle, BinaryWriter bw);

    /// <summary>Takes up a puzzle as WritePuzzleState wrote it, without its start running again: false if it can't be</summary>
    bool RestorePuzzle(OpenPuzzle puzzle, BinaryReader br);
}

/// <summary>
/// Puzzles (mini-games) the player works in the middle of a level: a script's game.puzzle,
/// started by A_Puzzle (a switch, a trigger, an actor's state), game.startpuzzle or the puzzle
/// command. While one's open, the player's controls go to it instead of their legs and gun: it
/// thinks in the game's own think, from their TicCmd, so it comes out the same everywhere.
/// It ends in a result the scripts hear of (game.onpuzzle). One with pause on stops the level
/// while it's open (playing alone). Only its drawing is this machine's (Ui/PuzzlePanel.cs).
/// </summary>
/// <remarks>
/// With others, every machine runs every puzzle from the same controls and pause is ignored.
/// An open puzzle goes into a save and to a player joining mid-game (Write / Read), its
/// script's pz.state and pz.args with it, as long as they're plain data; one that isn't is left
/// out of a save, and closed (as backed out of) on every machine as someone joins. In a demo, a
/// puzzle reads only the controls a demo keeps.
/// </remarks>
internal sealed class Puzzles(GameSession session, IPuzzleScripts scripts, Action<string> print)
{
    /// <summary>Errors in a row a puzzle's script may make before it's closed (as backed out of)</summary>
    internal const int ErrorLimit = 3;

    // How far a stick (or movement that isn't a button) has to push, a tic, to count as a direction
    const int StickPush = 40;

    // The acting player (an actor's A_Puzzle opens it for them)
    PlayerPawn? ActingPawn => session.PlayerState.Pawn;

    /// <summary>The puzzle the player on this machine has open, if any</summary>
    internal OpenPuzzle? LocalPuzzle => session.LocalPlayer?.Puzzle;

    /// <summary>Whether a player has a puzzle open that stops the level (pause, playing alone)</summary>
    internal bool FreezesWorld => !session.NetGame && session.Players.Any(p => p.Puzzle?.Definition.Pause == true);

    /// <summary>
    /// Opens a puzzle for a player, started from a tile (-1, -1 for none) with a tag (0 for
    /// none), <paramref name="ended"/> told its result as it closes: false if it can't be (none
    /// of that name, they have one open already, they're dead, or its script erred)
    /// </summary>
    internal bool Start(PlayerPawn player, string name, object? args, int x = -1, int y = -1, int tag = 0, SwitchPuzzle? origin = null)
    {
        name = name.Trim();
        if (scripts.FindPuzzle(name) is not { } definition)
        {
            print($"Puzzle {name}: there's no puzzle of that name (a script's game.puzzle)");
            return false;
        }
        var state = player.State;
        if (state == null || state.Puzzle != null || state.Health <= 0)
            return false;

        var puzzle = new OpenPuzzle(definition, player, state, x, y, tag) { Switch = origin };
        state.Puzzle = puzzle;
        if (!scripts.StartPuzzle(puzzle, args))
        {
            state.Puzzle = null;
            return false;
        }
        return true;
    }

    /// <summary>
    /// The acting player's puzzle thinks, if they have one open: true then, and the rest of
    /// their think is skipped (they don't move, turn, fire or use). Else use and fire held from
    /// one just closed are let go of first.
    /// </summary>
    internal bool Think()
    {
        var state = session.PlayerState;
        var input = session.PlayerInput;
        if (state.Puzzle is not { } puzzle)
        {
            LetGoOfHeldButtons(state, input);
            return false;
        }
        if (state.Health <= 0 || puzzle.Player.IsRemoved)
        {
            Close(puzzle, "cancel");
            return false;
        }

        var keys = ReadKeys(puzzle, input, (int)session.Tics, demo: session.DemoRecord || session.DemoPlayback);
        puzzle.Tics += keys.Tics;

        string? result = null;
        if (keys.IsPressed(PuzzleKeys.Cancel) && puzzle.Definition.Cancel != PuzzleCancel.Never)
            result = puzzle.Definition.Cancel == PuzzleCancel.Fail ? "fail" : "cancel";
        else
        {
            var tick = scripts.TickPuzzle(puzzle, keys);
            result = puzzle.PendingResult ?? tick.Result;
            if (!tick.Failed)
                puzzle.Errors = 0;
            else if (++puzzle.Errors >= ErrorLimit && result == null)
            {
                print($"Puzzle {puzzle.Definition.Name} is closed after {ErrorLimit} errors in a row");
                result = "cancel";
            }
        }
        if (result == null && puzzle.TimeLeft == 0)
            result = "fail";

        if (result != null)
            Close(puzzle, result);
        return true;
    }

    /// <summary>
    /// The level stopped for a puzzle (FreezesWorld): only the puzzles think, each as its
    /// player, from the controls they've been given this frame
    /// </summary>
    internal void ThinkFrozen()
    {
        foreach (var p in session.Players)
        {
            if (p.Puzzle == null)
                continue;
            using (session.ActAs(p))
                Think();
        }
    }

    /// <summary>A new level, life or loaded game: no puzzle is left open from before, and nothing's held from one</summary>
    internal void Reset()
    {
        foreach (var p in session.Players)
        {
            p.Puzzle = null;
            p.PuzzleButtonsHeld = false;
        }
    }

    /// <summary>What a switch's puzzle's result does to it (Gameplay/PuzzleSwitches.cs Ended)</summary>
    internal Action<SwitchPuzzle, string>? SwitchEnded { get; set; }

    /// <summary>
    /// A player's joining mid-game: every open puzzle whose script's state can't be sent to them
    /// closes, as backed out of, on every machine at the same frame (each machine's scripts hold
    /// the same, so all of them find the same ones), each as its own player
    /// </summary>
    internal void CloseUnsendable()
    {
        foreach (var p in session.Players.ToList())
        {
            if (p.Puzzle is not { } puzzle || scripts.WritePuzzleState(puzzle, new BinaryWriter(Stream.Null)) == null)
                continue;
            using (session.ActAs(p))
                Close(puzzle, "cancel");
        }
    }

    /// <summary>The acting player's died: a puzzle they had open closes, as backed out of</summary>
    internal void CloseForDeath()
    {
        if (session.PlayerState.Puzzle is { } puzzle)
            Close(puzzle, "cancel");
    }

    // After it's closed: what it ends may open another
    void Close(OpenPuzzle puzzle, string result)
    {
        if (ReferenceEquals(puzzle.State.Puzzle, puzzle))
            puzzle.State.Puzzle = null;
        puzzle.State.PuzzleButtonsHeld = true;
        if (puzzle.Switch is { } origin)
            SwitchEnded?.Invoke(origin, result);     // a switch thrown, its fail actions...
        scripts.PuzzleFinished(puzzle, result);     // game.onpuzzle
    }

    /*
    =============================================================================
    SAVING AND JOINING
    =============================================================================
    */

    /// <summary>
    /// A player's open puzzle as a save or a joiner keeps it: what it is and how far it's got,
    /// the switch it's for, and its script's state. Null when they have none, or (with why,
    /// told on the console) when its script's state can't be kept.
    /// </summary>
    internal byte[]? Write(PlayerState p)
    {
        if (p.Puzzle is not { } puzzle)
            return null;
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            bw.Write(puzzle.Definition.Name);
            bw.Write(puzzle.X);
            bw.Write(puzzle.Y);
            bw.Write(puzzle.Tag);
            bw.Write(puzzle.Tics);
            bw.Write((uint)puzzle.PreviousKeys);
            bw.Write(puzzle.Errors);
            bw.Write(puzzle.Switch != null);
            puzzle.Switch?.Write(bw);
            if (scripts.WritePuzzleState(puzzle, bw) is { } problem)
            {
                print($"Puzzle {puzzle.Definition.Name} can't be kept ({problem}), so it's left out");
                return null;
            }
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Puts back a player's open puzzle as Write wrote it (none for null), on a player whose pawn
    /// is in place: false (with why, on the console) if it can't be, and they have none
    /// </summary>
    internal bool Read(PlayerState p, byte[]? saved)
    {
        p.Puzzle = null;
        if (saved == null || p.Pawn is not { } pawn)
            return saved == null;
        try
        {
            using var br = new BinaryReader(new MemoryStream(saved));
            var name = br.ReadString();
            if (scripts.FindPuzzle(name) is not { } definition)
            {
                print($"Puzzle {name} isn't there any more (a script's game.puzzle), so it's closed");
                return false;
            }
            int x = br.ReadInt32(), y = br.ReadInt32(), tag = br.ReadInt32();
            int tics = br.ReadInt32();
            var previous = (PuzzleKeys)br.ReadUInt32();
            int errors = br.ReadInt32();
            var origin = br.ReadBoolean() ? SwitchPuzzle.Read(br) : null;
            var puzzle = new OpenPuzzle(definition, pawn, p, x, y, tag)
            {
                Switch = origin, Tics = tics, PreviousKeys = previous, Errors = errors,
            };
            if (!scripts.RestorePuzzle(puzzle, br))
                return false;
            p.Puzzle = puzzle;
            return true;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or FormatException)
        {
            print($"An open puzzle couldn't be read back: {e.Message}");
            return false;
        }
    }

    /// <summary>The other players working puzzles (with others), for the line saying so (Ui/PuzzlePanel.cs)</summary>
    internal IEnumerable<OpenPuzzle> OthersPuzzles() =>
        session.Players.Where(p => !ReferenceEquals(p, session.LocalPlayer) && p.Puzzle != null).Select(p => p.Puzzle!);

    // Use and fire still held from a closed puzzle count for nothing till they're let go of
    static void LetGoOfHeldButtons(PlayerState state, PlayerInput input)
    {
        if (!state.PuzzleButtonsHeld)
            return;
        if (input.IsPressed(ButtonType.Use) || input.IsPressed(ButtonType.Attack))
        {
            input.SetPressed(ButtonType.Use, false);
            input.SetPressed(ButtonType.Attack, false);
        }
        else
            state.PuzzleButtonsHeld = false;
    }

    // In a demo, how far the turn or walk has to go, a tic, to count as a direction (the arrow
    // keys walk and turn at 35)
    const int DemoPush = 20;

    /// <summary>
    /// The puzzle's keys from the player's controls this think: the movement and turn buttons
    /// (or a stick pushed far enough) as directions, use, fire, Esc, the weapon slot keys as
    /// digits; fresh against what was held the think before. In a demo, only what a demo keeps
    /// (the first eight buttons, the turn and the walk): the turn and walk as directions, slots 1
    /// to 4 as digits, and no Esc, so it plays back as it was recorded.
    /// </summary>
    internal static PuzzleInput ReadKeys(OpenPuzzle puzzle, PlayerInput input, int tics, bool demo = false)
    {
        tics = Math.Max(tics, 1);
        var held = PuzzleKeys.None;
        if (demo)
        {
            int push = DemoPush * tics;
            if (input.ControlY <= -push)
                held |= PuzzleKeys.Up;
            if (input.ControlY >= push)
                held |= PuzzleKeys.Down;
            if (input.ControlX <= -push)
                held |= PuzzleKeys.Left;
            if (input.ControlX >= push)
                held |= PuzzleKeys.Right;
        }
        else
        {
            int push = StickPush * tics;
            if (input.IsPressed(ButtonType.MoveForward) || input.ControlY <= -push)
                held |= PuzzleKeys.Up;
            if (input.IsPressed(ButtonType.MoveBackward) || input.ControlY >= push)
                held |= PuzzleKeys.Down;
            if (input.IsPressed(ButtonType.TurnLeft) || input.IsPressed(ButtonType.StrafeLeft) || input.ControlStrafe <= -push)
                held |= PuzzleKeys.Left;
            if (input.IsPressed(ButtonType.TurnRight) || input.IsPressed(ButtonType.StrafeRight) || input.ControlStrafe >= push)
                held |= PuzzleKeys.Right;
            if (input.IsPressed(ButtonType.Esc))
                held |= PuzzleKeys.Cancel;
        }
        if (input.IsPressed(ButtonType.Use))
            held |= PuzzleKeys.Use;
        if (input.IsPressed(ButtonType.Attack))
            held |= PuzzleKeys.Fire;
        for (int digit = 0; digit <= 9; digit++)
            if ((!demo || digit is >= 1 and <= 4) && input.IsPressed(SlotButton(digit)))
                held |= DigitKey(digit);

        var pressed = held & ~puzzle.PreviousKeys;
        puzzle.PreviousKeys = held;

        int digitPressed = -1;
        for (int digit = 0; digit <= 9 && digitPressed < 0; digit++)
            if ((pressed & DigitKey(digit)) != 0)
                digitPressed = digit;

        return new PuzzleInput(held, pressed, digitPressed, input.ControlX, input.ControlY, tics);
    }

    internal static PuzzleKeys DigitKey(int digit) => (PuzzleKeys)((uint)PuzzleKeys.Digit0 << digit);

    // The weapon slot key for a digit: Slot1..Slot9, and Slot0 for 0
    static ButtonType SlotButton(int digit) => digit switch
    {
        1 => ButtonType.Slot1,
        2 => ButtonType.Slot2,
        3 => ButtonType.Slot3,
        4 => ButtonType.Slot4,
        5 => ButtonType.Slot5,
        6 => ButtonType.Slot6,
        7 => ButtonType.Slot7,
        8 => ButtonType.Slot8,
        9 => ButtonType.Slot9,
        _ => ButtonType.Slot0,
    };

    /*
    =============================================================================
    ACTIONS
    =============================================================================
    */

    /// <summary>
    /// A_Puzzle("name"[, args...]): opens a puzzle, for switches and mapdefs triggers (the player
    /// who used it, its tile and tag) and actors' states (the acting player, the actor's tile
    /// and tag). Its arguments go to the puzzle's start as a table: "key=value" words by key,
    /// others in order.
    /// </summary>
    internal static void RegisterActions(Func<Puzzles> puzzles)
    {
        MapTriggerRegistry.Register("A_Puzzle", (trigger, args) =>
            Named(args) is { } name && trigger.Activator is PlayerPawn player
            && puzzles().Start(player, name, args[1..], trigger.TileX, trigger.TileY, trigger.Tag));
        ActorActionRegistry.Register("A_Puzzle", (Entities.Actors.Actor ob, string[] args) =>
        {
            var self = puzzles();
            if (Named(args) is { } name && self.ActingPawn is { } player)
                self.Start(player, name, args[1..], ob.TileX, ob.TileY, ob.Tag);
        });
    }

    static string? Named(string[] args)
    {
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            return args[0].Trim();
        Console.WriteLine("A_Puzzle: give it the puzzle's name, A_Puzzle(\"name\")");
        return null;
    }
}
