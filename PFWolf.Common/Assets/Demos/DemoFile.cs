namespace PFWolf.Assets.Demos;

/// <summary>
/// One frame of a demo: the buttons held (bit n is the game's ButtonType n) and a signed byte
/// each of turning (right is positive) and walking (forward is negative) a tic
/// </summary>
public readonly record struct DemoFrame(byte Buttons, sbyte Turn, sbyte Move)
{
    public bool Attack => (Buttons & 1 << DemoFile.AttackButton) != 0;
    public bool Strafe => (Buttons & 1 << DemoFile.StrafeButton) != 0;
    public bool Run => (Buttons & 1 << DemoFile.RunButton) != 0;
    public bool Use => (Buttons & 1 << DemoFile.UseButton) != 0;

    /// <summary>Whether weapon slot key 1-4 is held</summary>
    public bool Slot(int slot) => slot is >= 1 and <= 4 && (Buttons & 1 << (DemoFile.Slot1Button + slot - 1)) != 0;
}

/// <summary>
/// A demo in id's format: byte 0 the map less one (0 = MAP01), bytes 1-2 the length counting the
/// 4-byte header, byte 3 padding, then 3 bytes a frame (<see cref="DemoFrame"/>). The game plays
/// each frame for <see cref="TicsPerFrame"/> tics of 70 a second, and stops at the length, or at
/// the data's end when the length overshoots it (as Gameplay/Demos.cs does).
/// </summary>
public sealed class DemoFile
{
    public const int HeaderSize = 4;
    public const int FrameSize = 3;

    /// <summary>The tics each frame lasts: the game's DemoTics</summary>
    public const int TicsPerFrame = 4;
    public const int TicsPerSecond = 70;

    // The game's ButtonType: only the first eight fit the buttons byte
    public const int AttackButton = 0, StrafeButton = 1, RunButton = 2, UseButton = 3, Slot1Button = 4;

    /// <summary>The buttons byte's bits, first to last, as the timeline names them</summary>
    public static readonly string[] ButtonNames = ["Fire", "Strafe", "Run", "Use", "Slot 1", "Slot 2", "Slot 3", "Slot 4"];

    public required byte[] Data { get; init; }

    /// <summary>The map's index: 0 for MAP01</summary>
    public int MapIndex { get; init; }

    public string MapName => $"MAP{MapIndex + 1:D2}";

    /// <summary>The length the header gives, header included</summary>
    public int DeclaredLength { get; init; }

    public IReadOnlyList<DemoFrame> Frames { get; init; } = [];

    /// <summary>What's wrong with it, if anything; a demo with problems may still play</summary>
    public IReadOnlyList<string> Problems { get; init; } = [];

    /// <summary>Whether the game would play it at all: a header and at least one frame</summary>
    public bool IsPlayable => Data.Length >= HeaderSize && Frames.Count > 0;

    public int Tics => Frames.Count * TicsPerFrame;
    public TimeSpan Duration => TimeSpan.FromSeconds(Tics / (double)TicsPerSecond);

    /// <summary>The time a frame starts at</summary>
    public static TimeSpan TimeOf(int frame) => TimeSpan.FromSeconds(frame * TicsPerFrame / (double)TicsPerSecond);

    public static DemoFile Parse(byte[] data)
    {
        var problems = new List<string>();
        if (data.Length < HeaderSize)
        {
            problems.Add($"Too short for a demo: {data.Length} bytes, and the header alone is {HeaderSize}");
            return new DemoFile { Data = data, Problems = problems };
        }

        int length = BitConverter.ToInt16(data, 1);
        int end = Math.Min(length, data.Length);
        if (length > data.Length)
            problems.Add($"The header says {length} bytes, but there are only {data.Length}: it stops at the end of the data");
        else if (length < HeaderSize)
            problems.Add($"The header's length ({length}) is shorter than the header itself");
        if (end > HeaderSize && (end - HeaderSize) % FrameSize != 0)
            problems.Add($"{(end - HeaderSize) % FrameSize} bytes left over after the last whole frame");

        // As the game reads them: a frame at a time while a whole one is left (InputPolling)
        var frames = new List<DemoFrame>();
        for (int at = HeaderSize; at + FrameSize <= end; at += FrameSize)
            frames.Add(new DemoFrame(data[at], (sbyte)data[at + 1], (sbyte)data[at + 2]));
        if (frames.Count == 0)
            problems.Add("No frames: the game won't play it");

        return new DemoFile { Data = data, MapIndex = data[0], DeclaredLength = length, Frames = frames, Problems = problems };
    }
}
