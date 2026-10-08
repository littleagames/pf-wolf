namespace PFWolf.Editor.Data;

/// <summary>What an article draws on its page</summary>
public enum ArticleDrawKind
{
    /// <summary>A VGA picture, its top left corner at X, Y</summary>
    Picture,
    /// <summary>A word in the small font, its top left corner at X, Y</summary>
    Text,
    /// <summary>A rectangle filled in the background color (^B)</summary>
    Bar,
}

/// <summary>
/// One thing drawn on an article page, in 320x200 screen pixels. <see cref="Index"/> is where
/// in the text it comes from (-1 for the page's frame and number).
/// </summary>
public sealed record ArticleDraw(ArticleDrawKind Kind, int X, int Y, int Index)
{
    /// <summary>Picture: its name; Text: the word</summary>
    public string Text { get; init; } = "";

    /// <summary>Text: its palette index</summary>
    public byte Color { get; init; }

    /// <summary>Bar: its size</summary>
    public int Width { get; init; }
    public int Height { get; init; }

    /// <summary>Picture: a ^T picture's wait before it's drawn, in tics (70 a second)</summary>
    public int DelayTics { get; init; }
}

/// <summary>One page of an article: what it draws, and the part of the text it lays out (from its ^P)</summary>
public sealed record ArticlePage(int Number, int Start, int End, IReadOnlyList<ArticleDraw> Draws)
{
    /// <summary>Text past the bottom of the page, which the game leaves out, from here to <see cref="End"/></summary>
    public int? CutFrom { get; init; }
}

/// <summary>Something wrong with an article. An error is what makes the game quit (or crash) showing it.</summary>
public sealed record ArticleProblem(int Index, int Line, string Message, bool IsError)
{
    public string Label => $"{(IsError ? "Error" : "Warning")}, line {Line}: {Message}";
    public override string ToString() => Label;
}

/// <summary>What an article is laid out with: the small font's widths, the pictures ^G numbers, and the colors</summary>
public sealed class ArticleArt
{
    /// <summary>Each character's width in the small font (256 of them); null draws nothing</summary>
    public byte[]? FontWidths { get; init; }

    /// <summary>The picture ^G and ^T name by number (alias.yaml art-extern), or null when it has none</summary>
    public Func<int, string?> PictureName { get; init; } = _ => null;

    /// <summary>A picture's size, or null when it isn't in the game</summary>
    public Func<string, (int Width, int Height)?> PictureSize { get; init; } = _ => null;

    /// <summary>The text's color until a ^C changes it (the theme's Black)</summary>
    public byte TextColor { get; init; }

    /// <summary>The "pg 1 of 2" color (the theme's Dark Yellow)</summary>
    public byte PageNumberColor { get; init; }

    public int Advance(char ch)
    {
        if (FontWidths is not { Length: > 0 } widths)
            return 0;
        return ch < widths.Length ? widths[ch] : widths[' '];
    }
}

/// <summary>
/// A Wolf3D article (the help text "Read This!", and the ENDARTn texts after an episode) laid out
/// page by page, the way the game's Program.WL_TEXT does: each page starts with ^P and the text
/// ends with ^E; words wrap between the margins, which ^G pictures push in. Where the game would
/// quit or crash on the text, this carries on and says so in <see cref="Problems"/>.
///
///   ^P               a new page (must be the first thing in the text)
///   ^E               the end of the text
///   ^Cxx             the text's color: a palette index in two hex digits
///   ^Gy,x,n          picture n (alias.yaml's art-extern) at x (rounded down to 8), y, and the margins kept clear of it
///   ^Ty,x,n,t        picture n at x, y after waiting t tics, the margins left as they are
///   ^By,x,w,h        a rectangle in the background color
///   ^Ly,x            go to x and the line y is in
///   ^>               go to the middle of the line
///   ^;               a comment to the end of the line
/// </summary>
public sealed class ArticleLayout
{
    public const int ScreenWidth = 320;
    public const int ScreenHeight = 200;
    public const int FontHeight = 10;
    public const int TopMargin = 16;
    public const int BottomMargin = 32;
    public const int LeftMargin = 16;
    public const int RightMargin = 16;
    public const int PicMargin = 8;
    public const int TextRows = (ScreenHeight - TopMargin - BottomMargin) / FontHeight;
    public const int SpaceWidth = 7;
    public const int WordLimit = 80;

    // How far the game looks for the ^E before giving up (CacheLayout's bombpoint)
    public const int SearchLimit = 30000;

    /// <summary>The help screen's frame, drawn first on every page</summary>
    public static readonly (string Picture, int X, int Y)[] Frame =
    [
        ("h_topwindow", 0, 0),
        ("h_leftwindow", 0, 8),
        ("h_rightwindow", 312, 8),
        ("h_bottominfo", 8, 176),
    ];

    private readonly string _text;
    private readonly ArticleArt _art;
    private readonly List<ArticlePage> _pages = [];
    private readonly List<ArticleProblem> _problems = [];
    private readonly int[] _lineStarts;

    // The page being laid out: where the text has got to, the margins of each line, the print
    // position, and the color ^C last set
    private int _index;
    private readonly int[] _leftMargin = new int[TextRows];
    private readonly int[] _rightMargin = new int[TextRows];
    private int _row;
    private int _printX, _printY;
    private byte _color;
    private bool _layoutDone;
    private int? _cutFrom;
    private List<ArticleDraw> _draws = [];

    private ArticleLayout(string text, ArticleArt art)
    {
        _text = text;
        _art = art;
        var starts = new List<int> { 0 };
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                starts.Add(i + 1);
        }
        _lineStarts = [.. starts];
    }

    public IReadOnlyList<ArticlePage> Pages => _pages;
    public IReadOnlyList<ArticleProblem> Problems => _problems;

    /// <summary>The pages the game counts (the ^Ps before the ^E): the "of n" in "pg 1 of n"</summary>
    public int PageCount { get; private set; }

    /// <summary>The text has an ^E within <see cref="SearchLimit"/> characters, so the game can show it</summary>
    public bool HasEnd { get; private set; }

    /// <summary>Whether the text looks like an article: its first thing is a ^P</summary>
    public static bool IsArticle(string text)
    {
        int i = 0;
        while (i < text.Length && text[i] <= ' ')
            i++;
        return i + 1 < text.Length && text[i] == '^' && char.ToUpperInvariant(text[i + 1]) == 'P';
    }

    public static ArticleLayout Build(string text, ArticleArt art)
    {
        var layout = new ArticleLayout(text, art);
        layout.Run();
        return layout;
    }

    /// <summary>The page the text at this index is on (the last page for text past it), or -1 when there are none</summary>
    public int PageAt(int index)
    {
        if (_pages.Count == 0)
            return -1;
        int page = 0;
        while (page + 1 < _pages.Count && _pages[page + 1].Start <= index)
            page++;
        return page;
    }

    /// <summary>The 1-based line of the text an index is on</summary>
    public int LineOf(int index)
    {
        int found = Array.BinarySearch(_lineStarts, Math.Clamp(index, 0, Math.Max(_text.Length, 0)));
        return (found >= 0 ? found : ~found - 1) + 1;
    }

    private void Problem(int index, string message, bool isError = false)
    {
        // Once at each spot is enough
        if (!_problems.Any(problem => problem.Index == index && problem.Message == message))
            _problems.Add(new ArticleProblem(index, LineOf(index), message, isError));
    }

    private bool AtEnd => _index >= _text.Length;
    private char Current => _index < _text.Length ? _text[_index] : '\0';
    private char At(int index) => index >= 0 && index < _text.Length ? _text[index] : '\0';

    private void Run()
    {
        if (_art.FontWidths == null)
            Problem(0, "There's no small font to lay the text out with", isError: true);

        CountPages();

        _index = 0;
        while (true)
        {
            while (!AtEnd && Current <= ' ')
                _index++;
            // Without an ^E: CountPages has said so
            if (AtEnd)
                break;

            if (Current == '^' && char.ToUpperInvariant(At(_index + 1)) == 'E')
                break;

            if (Current != '^' || char.ToUpperInvariant(At(_index + 1)) != 'P')
            {
                // Only the first page can start with something else: the others start where a ^P stopped the last
                Problem(_index, "The text must start with ^P (the game quits: \"PageLayout: Text not headed with ^P\")", isError: true);
                break;
            }

            LayOutPage();
        }

        if (HasEnd && _pages.Count != PageCount)
            Problem(0, $"The game counts {PageCount} pages but lays out {_pages.Count}", isError: false);
    }

    // CacheLayout: the ^Ps before the ^E
    private void CountPages()
    {
        int limit = Math.Min(_text.Length, SearchLimit);
        for (int i = 0; i < limit; i++)
        {
            if (_text[i] != '^')
                continue;

            char command = char.ToUpperInvariant(At(i + 1));
            if (command == 'P')
                PageCount++;
            else if (command == 'E')
            {
                HasEnd = true;
                return;
            }
            else if (command is 'G' or 'T')
            {
                // Its numbers, to the end of the line
                while (i < limit && _text[i] != '\n')
                    i++;
            }
        }

        Problem(Math.Max(0, limit - 1), _text.Length > SearchLimit
            ? $"No ^E in the first {SearchLimit} characters (the game quits: \"CacheLayout: No ^E to terminate file!\")"
            : "No ^E ends the text (the game quits: \"CacheLayout: No ^E to terminate file!\")", isError: true);
    }

    // PageLayout: the frame, then the text up to the next ^P or ^E
    private void LayOutPage()
    {
        int start = _index;
        _draws = [];
        foreach (var (picture, x, y) in Frame)
            _draws.Add(new ArticleDraw(ArticleDrawKind.Picture, x, y, -1) { Text = picture });

        for (int i = 0; i < TextRows; i++)
        {
            _leftMargin[i] = LeftMargin;
            _rightMargin[i] = ScreenWidth - RightMargin;
        }
        _printX = LeftMargin;
        _printY = TopMargin;
        _row = 0;
        _color = _art.TextColor;
        _layoutDone = false;
        _cutFrom = null;

        // Past the ^P and the rest of its line
        RipToEndOfLine();

        while (!_layoutDone)
        {
            if (AtEnd)
                break;

            char ch = Current;
            if (ch == '^')
                HandleCommand();
            else if (ch == '\t')
            {
                _printX = (_printX + 8) & 0xf8;
                _index++;
            }
            else if (ch <= ' ')
            {
                _index++;
                if (ch == '\n')
                    NewLine();
            }
            else
                HandleWord();
        }

        int number = _pages.Count + 1;
        _draws.Add(new ArticleDraw(ArticleDrawKind.Text, 213, 183, -1) { Text = $"pg {number} of {PageCount}", Color = _art.PageNumberColor });
        _pages.Add(new ArticlePage(number, start, Math.Min(_index, _text.Length), _draws) { CutFrom = _cutFrom });
    }

    private void HandleCommand()
    {
        int at = _index;
        _index++;
        char command = char.ToUpperInvariant(Current);
        switch (command)
        {
            case 'B':
            {
                _index++;
                int y = ParseNumber(at), x = ParseNumber(at), width = ParseNumber(at), height = ParseNumber(at);
                _draws.Add(new ArticleDraw(ArticleDrawKind.Bar, x, y, at) { Width = width, Height = height });
                RipToEndOfLine();
                break;
            }

            case ';':
                RipToEndOfLine();
                break;

            case 'P':
            case 'E':
                // The next page's start, or the end: back to the '^' for the next page to find
                _layoutDone = true;
                _index--;
                break;

            case 'C':
            {
                int high = HexDigit(At(_index + 1)), low = HexDigit(At(_index + 2));
                if (high < 0 || low < 0)
                    Problem(at, "^C takes two hex digits (00 to FF), the color's palette index");
                _color = (byte)(Math.Max(high, 0) * 16 + Math.Max(low, 0));
                _index += 3;
                break;
            }

            case '>':
                _printX = ScreenWidth / 2;
                _index++;
                break;

            case 'L':
            {
                _index++;
                int y = ParseNumber(at);
                int row = (y - TopMargin) / FontHeight;
                if (row < 0 || row >= TextRows)
                {
                    // The game reads past its margins there, and crashes
                    Problem(at, $"^L's y ({y}) is off the page's lines: it must be at most {TopMargin + TextRows * FontHeight - 1}", isError: true);
                    row = Math.Clamp(row, 0, TextRows - 1);
                }
                _row = row;
                _printY = TopMargin + row * FontHeight;
                _printX = ParseNumber(at);
                RipToEndOfLine();
                break;
            }

            case 'T':
            {
                _index++;
                int y = ParseNumber(at), x = ParseNumber(at), number = ParseNumber(at), delay = ParseNumber(at);
                RipToEndOfLine();
                if (PictureFor(at, number) is { } picture)
                    _draws.Add(new ArticleDraw(ArticleDrawKind.Picture, x & ~7, y, at) { Text = picture, DelayTics = delay });
                break;
            }

            case 'G':
            {
                _index++;
                int y = ParseNumber(at), x = ParseNumber(at), number = ParseNumber(at);
                RipToEndOfLine();
                if (PictureFor(at, number) is not { } picture)
                    break;

                _draws.Add(new ArticleDraw(ArticleDrawKind.Picture, x & ~7, y, at) { Text = picture });
                if (_art.PictureSize(picture) is not { Width: > 0, Height: > 0 } size)
                    break;

                // Keep the text clear of it: the picture's side of the page moves the margin in
                int middle = x + size.Width / 2;
                int margin = middle > ScreenWidth / 2 ? x - PicMargin : x + size.Width + PicMargin;
                int top = Math.Max((y - TopMargin) / FontHeight, 0);
                int bottom = Math.Min((y + size.Height - TopMargin) / FontHeight, TextRows - 1);
                for (int row = top; row <= bottom; row++)
                {
                    if (middle > ScreenWidth / 2)
                        _rightMargin[row] = margin;
                    else
                        _leftMargin[row] = margin;
                }

                if (_printX < _leftMargin[_row])
                    _printX = _leftMargin[_row];
                break;
            }

            default:
                // The game skips the '^' and lays out what follows as a word
                Problem(at, $"^{(Current > ' ' ? Current.ToString() : "")} isn't a command the game knows: it's left out");
                break;
        }
    }

    // A ^G or ^T picture by its number, or null (with a problem) when there's none
    private string? PictureFor(int at, int number)
    {
        if (_art.PictureName(number) is not { Length: > 0 } picture)
        {
            Problem(at, $"Picture {number} isn't in alias.yaml's art-extern list", isError: true);
            return null;
        }
        if (_art.PictureSize(picture) == null)
            Problem(at, $"Picture {number} is {picture}, which isn't in the game", isError: true);
        return picture;
    }

    private static int HexDigit(char ch) => ch switch
    {
        >= '0' and <= '9' => ch - '0',
        >= 'A' and <= 'F' => ch - 'A' + 10,
        >= 'a' and <= 'f' => ch - 'a' + 10,
        _ => -1,
    };

    // The next number, skipping anything before it (the commas)
    private int ParseNumber(int commandAt)
    {
        while (!AtEnd && (Current < '0' || Current > '9'))
        {
            if (Current == '\n')
                Problem(commandAt, "The command has too few numbers on its line: the game reads them from the next");
            _index++;
        }
        if (AtEnd)
        {
            Problem(commandAt, "The text ends in the middle of a command", isError: true);
            return 0;
        }

        long value = 0;
        while (!AtEnd && Current >= '0' && Current <= '9')
        {
            value = Math.Min(value * 10 + (Current - '0'), int.MaxValue);
            _index++;
        }
        return (int)value;
    }

    private void RipToEndOfLine()
    {
        while (!AtEnd && _text[_index++] != '\n')
        {
        }
    }

    private void HandleWord()
    {
        int start = _index;
        while (!AtEnd && (Current > ' ' || _index == start))
            _index++;
        var word = _text[start.._index];
        if (word.Length >= WordLimit)
            Problem(start, $"A word of {word.Length} characters: the game quits on {WordLimit} or more (\"PageLayout: Word limit exceeded\")", isError: true);

        int width = 0;
        foreach (char ch in word)
            width += _art.Advance(ch);

        while (_printX + width > _rightMargin[_row])
        {
            if (width > ScreenWidth - LeftMargin - RightMargin)
                Problem(start, $"\"{Shorten(word)}\" is wider than a line: it can't fit, and pushes the rest of the page off it");
            else if (width > _rightMargin[_row] - _leftMargin[_row] && _printX == _leftMargin[_row])
                Problem(start, $"\"{Shorten(word)}\" doesn't fit beside the picture on its line: it goes further down");
            NewLine(start);
            if (_layoutDone)
                return;
        }

        _draws.Add(new ArticleDraw(ArticleDrawKind.Text, _printX, _printY, start) { Text = word, Color = _color });
        _printX += width;

        // The spaces after it
        while (Current == ' ' && !AtEnd)
        {
            _printX += SpaceWidth;
            _index++;
        }
    }

    // The next line down, or, past the last, the rest of the page is cut until the next ^P or ^E
    private void NewLine(int? wordStart = null)
    {
        if (++_row == TextRows)
        {
            _layoutDone = true;
            _row = TextRows - 1;
            int cut = wordStart ?? _index;
            while (!AtEnd)
            {
                if (Current == '^' && char.ToUpperInvariant(At(_index + 1)) is 'E' or 'P')
                    break;
                _index++;
            }

            // Only worth a word when something besides blank lines is lost
            if (_text[cut..Math.Min(_index, _text.Length)].Any(ch => ch > ' '))
            {
                _cutFrom = cut;
                Problem(cut, $"Page {_pages.Count + 1} is full: the game leaves out the text from here to the next ^P");
            }
            return;
        }

        _printX = _leftMargin[_row];
        _printY += FontHeight;
    }

    private static string Shorten(string word) => word.Length > 24 ? word[..24] + "…" : word;
}
