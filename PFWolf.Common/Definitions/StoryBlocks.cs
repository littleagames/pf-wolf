using System.Globalization;

namespace PFWolf.Definitions;

/// <summary>
/// A tile's blocks on the height plane (plane 3), shared by the game (MapManager) and the editor.
/// A block runs from a start story to a stop story, each 1 to 8: the start in bits 8-11 (0 is 1),
/// the stop in bits 0-3. A second block above the first, with a gap between, has its start in
/// bits 12-15 and its stop in bits 4-7: 0x0305 is stories 3 to 5, 0x7385 stories 3 to 5 and 7 to 8.
/// A plain stop (3) is a block from story 1, as tall walls always were. 0 is no block of its own.
/// </summary>
public static class StoryBlocks
{
    /// <summary>The highest story</summary>
    public const int MaxStories = 8;

    /// <summary>
    /// A height-plane value's blocks, null for none: 0, or anything that isn't a start and stop of
    /// 1 to 8 with the start no higher than the stop (such as an ECWolf info plane's values). A
    /// second block must start above the first's stop: touching it, the two are one block;
    /// anything else and the value's no block.
    /// </summary>
    public static ((int Start, int Stop)? Lower, (int Start, int Stop)? Upper) Decode(int value)
    {
        if ((uint)value > 0xffff)
            return (null, null);
        int stop = value & 0x0f, start = value >> 8 & 0x0f;
        int stop2 = value >> 4 & 0x0f, start2 = value >> 12 & 0x0f;
        if (start == 0)
            start = 1;
        if (stop is < 1 or > MaxStories || start > stop)
            return (null, null);
        if (stop2 == 0 && start2 == 0)
            return ((start, stop), null);
        if (stop2 > MaxStories || start2 <= stop || start2 > stop2)
            return (null, null);
        if (start2 == stop + 1)
            return ((start, stop2), null);          // touching: one block
        return ((start, stop), (start2, stop2));
    }

    /// <summary>A block as a height-plane value: a plain stop when it starts at story 1</summary>
    public static ushort Encode(int start, int stop) => (ushort)(start <= 1 ? stop : start << 8 | stop);

    /// <summary>Two blocks as a height-plane value (the second above the first with a gap; a stop of 0: none)</summary>
    public static ushort Encode(int start, int stop, int start2, int stop2) =>
        (ushort)(stop == 0 ? 0 : Encode(start, stop) | (stop2 == 0 ? 0 : start2 << 12 | stop2 << 4));

    /// <summary>
    /// A value as it's typed: "3-5", "1-2,4-8", a plain "3" (from story 1, as tall walls and old
    /// arches were written); "" for 0, and the bare number for a value that isn't blocks
    /// </summary>
    public static string Format(int value)
    {
        if (value == 0)
            return "";
        var (lower, upper) = Decode(value);
        if (lower is not var (start, stop))
            return value.ToString(CultureInfo.InvariantCulture);
        bool plain = (value >> 8 & 0x0f) == 0;
        return (plain ? $"{stop}" : $"{start}-{stop}") + (upper is var (s2, e2) ? $",{s2}-{e2}" : "");
    }

    /// <summary>Whether a value's first block is a plain stop (no start of its own): a tall wall's, or an old arch's</summary>
    public static bool IsPlain(int value) => value is >= 1 and <= MaxStories;

    /// <summary>
    /// What's typed as blocks: "3-5", "1-2,4-8", a plain "3" (story 1 to 3, written as a plain
    /// stop), "" or "0" (none); null with the reason when it can't be (stories past 8, a start
    /// above its stop, a second block not above the first, more than two). A block typed with its
    /// start keeps it, even 1 ("1-3" is 0x0103), so it's never taken for an old arch (a plain stop
    /// on open floor).
    /// </summary>
    public static ushort? Parse(string? text, out string? error)
    {
        error = null;
        text = text?.Trim() ?? "";
        if (text is "" or "0")
            return 0;
        var parts = text.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length > 2)
        {
            error = "at most two blocks, as 1-2,4-8";
            return null;
        }

        var blocks = new (int Start, int Stop)[parts.Length];
        bool plain = false;
        for (int i = 0; i < parts.Length; i++)
        {
            var ends = parts[i].Split('-', StringSplitOptions.TrimEntries);
            if (ends.Length is < 1 or > 2
                || !int.TryParse(ends[0], NumberStyles.None, CultureInfo.InvariantCulture, out int first)
                || ends.Length == 2 && !int.TryParse(ends[1], NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                error = $"'{parts[i]}' isn't a story or stories, as 3 or 3-5";
                return null;
            }
            int last = ends.Length == 2 ? int.Parse(ends[1], CultureInfo.InvariantCulture) : first;
            if (ends.Length == 1)
            {
                first = 1;                          // a plain stop: from story 1
                if (i == 0)
                    plain = true;
            }
            if (first < 1 || last > MaxStories || first > last)
            {
                error = $"'{parts[i]}': stories go from 1 to {MaxStories}, the first no higher than the last";
                return null;
            }
            blocks[i] = (first, last);
        }

        // the first block with its start (unless typed plain)
        ushort First(int start, int stop) => (ushort)(plain ? stop : start << 8 | stop);
        if (blocks.Length == 1)
            return First(blocks[0].Start, blocks[0].Stop);
        if (blocks[1].Start <= blocks[0].Stop)
        {
            error = "the second block has to start above where the first stops";
            return null;
        }
        if (blocks[1].Start == blocks[0].Stop + 1)
            return First(blocks[0].Start, blocks[1].Stop);      // touching: one block
        return (ushort)(First(blocks[0].Start, blocks[0].Stop) | blocks[1].Start << 12 | blocks[1].Stop << 4);
    }
}
