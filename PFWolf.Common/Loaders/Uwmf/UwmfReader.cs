using System.Globalization;
using System.Text;

namespace PFWolf.Loaders.Uwmf;

/// <summary>
/// Reads a TEXTMAP (UWMF, ECWolf's text level format, as its src/gamemap_uwmf.cpp reads it):
/// `key = value;` header lines and `name { key = value; ... }` blocks (tile, sector, zone, plane,
/// thing, trigger), and each plane's `planemap { {tile, sector, zone[, tag]}, ... }`. Comments
/// are // to the end of the line and /* ... */. Unknown blocks are skipped up to their closing
/// brace. A mistake throws InvalidDataException naming its line.
/// </summary>
public static class UwmfReader
{
    public static UwmfMap Read(byte[] textmap) => Read(Encoding.UTF8.GetString(textmap));

    public static UwmfMap Read(string text)
    {
        var tokens = new Lexer(text);
        var map = new UwmfMap();
        while (tokens.Peek().Kind != TokenKind.End)
        {
            var name = tokens.Expect(TokenKind.Identifier, "a key or a block name");
            if (tokens.TryTake('='))
            {
                if (name.Text.Equals("width", StringComparison.OrdinalIgnoreCase) || name.Text.Equals("height", StringComparison.OrdinalIgnoreCase))
                    if (map.PlaneMaps.Count > 0)
                        throw tokens.Problem(name, "the level's size is set after a planemap");
                map.Header[name.Text] = ReadValue(tokens);
                tokens.Expect(';');
                continue;
            }

            var open = tokens.Expect('{');
            switch (name.Text.ToLowerInvariant())
            {
                case "planemap":
                    map.PlaneMaps.Add(ReadPlaneMap(tokens, map.Width, map.Height, open));
                    break;
                case "tile":
                    map.Tiles.Add(ReadBlock(tokens, open));
                    break;
                case "sector":
                    map.Sectors.Add(ReadBlock(tokens, open));
                    break;
                case "zone":
                    map.Zones.Add(ReadBlock(tokens, open));
                    break;
                case "plane":
                    map.Planes.Add(ReadBlock(tokens, open));
                    break;
                case "thing":
                    map.Things.Add(ReadBlock(tokens, open));
                    break;
                case "trigger":
                    map.Triggers.Add(ReadBlock(tokens, open));
                    break;
                default:
                    map.UnknownBlocks.Add(name.Text);
                    SkipBlock(tokens);
                    break;
            }
        }
        return map;
    }

    private static UwmfBlock ReadBlock(Lexer tokens, Token open)
    {
        var block = new UwmfBlock { Line = open.Line };
        while (!tokens.TryTake('}'))
        {
            var key = tokens.Expect(TokenKind.Identifier, "a key");
            tokens.Expect('=');
            block.Add(key.Text, ReadValue(tokens));
            tokens.Expect(';');
        }
        return block;
    }

    // As ECWolf: everything up to the block's own closing brace, nested ones included
    private static void SkipBlock(Lexer tokens)
    {
        int depth = 1;
        while (depth > 0)
        {
            var token = tokens.Take();
            if (token.Kind == TokenKind.End)
                throw tokens.Problem(token, "the file ends inside a block");
            if (token.Is('{'))
                depth++;
            else if (token.Is('}'))
                depth--;
        }
    }

    private static UwmfSpot[] ReadPlaneMap(Lexer tokens, int width, int height, Token open)
    {
        if (width <= 0 || height <= 0)
            throw tokens.Problem(open, $"a planemap for a {width}x{height} level");

        var spots = new UwmfSpot[width * height];
        int count = 0;
        while (!tokens.TryTake('}'))
        {
            if (count > 0)
                tokens.Expect(',');
            if (tokens.TryTake('}'))       // a comma before the closing brace
                break;
            var start = tokens.Expect('{');
            int tile = ReadInt(tokens);
            tokens.Expect(',');
            int sector = ReadInt(tokens);
            tokens.Expect(',');
            int zone = ReadInt(tokens);
            int tag = 0;
            if (tokens.TryTake(','))
                tag = ReadInt(tokens);
            tokens.Expect('}');
            if (count >= spots.Length)
                throw tokens.Problem(start, $"the planemap has more than the {width}x{height} = {spots.Length} spots of the level");
            spots[count++] = new UwmfSpot(tile, sector, zone, tag);
        }
        if (count != spots.Length)
            throw tokens.Problem(open, $"the planemap has {count} spots; a {width}x{height} level has {spots.Length}");
        return spots;
    }

    private static int ReadInt(Lexer tokens)
    {
        bool negative = tokens.TryTake('-');
        var token = tokens.Expect(TokenKind.Int, "a whole number");
        return negative ? -token.Int : token.Int;
    }

    private static UwmfValue ReadValue(Lexer tokens)
    {
        bool negative = tokens.TryTake('-');
        var token = tokens.Take();
        return token.Kind switch
        {
            TokenKind.Int => negative ? -token.Int : token.Int,
            TokenKind.Float => negative ? -token.Float : token.Float,
            TokenKind.String when !negative => token.Text,
            TokenKind.Identifier when !negative && token.Text.Equals("true", StringComparison.OrdinalIgnoreCase) => true,
            TokenKind.Identifier when !negative && token.Text.Equals("false", StringComparison.OrdinalIgnoreCase) => false,
            _ => throw tokens.Error(token, "a value (a number, a \"string\", true or false)"),
        };
    }

    private enum TokenKind { Identifier, String, Int, Float, Symbol, End }

    private readonly record struct Token(TokenKind Kind, string Text, int Line, int Int = 0, double Float = 0)
    {
        public bool Is(char symbol) => Kind == TokenKind.Symbol && Text.Length == 1 && Text[0] == symbol;

        public string Describe() => Kind switch
        {
            TokenKind.End => "the end of the file",
            TokenKind.String => $"\"{Text}\"",
            _ => $"'{Text}'",
        };
    }

    private sealed class Lexer(string text)
    {
        private int _at;
        private int _line = 1;
        private Token? _peeked;

        public Token Peek() => _peeked ??= Next();

        public Token Take()
        {
            var token = Peek();
            _peeked = null;
            return token;
        }

        public bool TryTake(char symbol)
        {
            if (!Peek().Is(symbol))
                return false;
            _peeked = null;
            return true;
        }

        public Token Expect(char symbol)
        {
            var token = Take();
            if (!token.Is(symbol))
                throw Error(token, $"'{symbol}'");
            return token;
        }

        public Token Expect(TokenKind kind, string what)
        {
            var token = Take();
            if (token.Kind != kind)
                throw Error(token, what);
            return token;
        }

        public InvalidDataException Error(Token at, string expected) =>
            new($"TEXTMAP line {at.Line}: expected {expected}, found {at.Describe()}");

        public InvalidDataException Problem(Token at, string problem) => new($"TEXTMAP line {at.Line}: {problem}");

        private Token Next()
        {
            SkipSpaceAndComments();
            if (_at >= text.Length)
                return new Token(TokenKind.End, "", _line);

            char c = text[_at];
            int line = _line;
            if (char.IsLetter(c) || c == '_')
            {
                int start = _at;
                while (_at < text.Length && (char.IsLetterOrDigit(text[_at]) || text[_at] == '_'))
                    _at++;
                return new Token(TokenKind.Identifier, text[start.._at], line);
            }
            if (char.IsDigit(c) || (c == '.' && _at + 1 < text.Length && char.IsDigit(text[_at + 1])))
                return Number(line);
            if (c == '"')
                return QuotedString(line);

            _at++;
            return new Token(TokenKind.Symbol, c.ToString(), line);
        }

        private Token Number(int line)
        {
            int start = _at;
            if (text[_at] == '0' && _at + 1 < text.Length && (text[_at + 1] is 'x' or 'X'))
            {
                _at += 2;
                while (_at < text.Length && Uri.IsHexDigit(text[_at]))
                    _at++;
                var hex = text[(start + 2).._at];
                if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
                    throw new InvalidDataException($"TEXTMAP line {line}: '{text[start.._at]}' isn't a number");
                return new Token(TokenKind.Int, text[start.._at], line, Int: value);
            }

            bool isFloat = false;
            while (_at < text.Length && (char.IsDigit(text[_at]) || text[_at] == '.' || text[_at] is 'e' or 'E'
                                         || (text[_at] is '+' or '-' && text[_at - 1] is 'e' or 'E')))
            {
                isFloat |= text[_at] is '.' or 'e' or 'E';
                _at++;
            }
            var number = text[start.._at];
            if (isFloat)
            {
                if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    throw new InvalidDataException($"TEXTMAP line {line}: '{number}' isn't a number");
                return new Token(TokenKind.Float, number, line, Float: d);
            }
            if (!int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var i))
                throw new InvalidDataException($"TEXTMAP line {line}: '{number}' is too big a number");
            return new Token(TokenKind.Int, number, line, Int: i);
        }

        private Token QuotedString(int line)
        {
            var value = new StringBuilder();
            _at++;      // the opening quote
            while (true)
            {
                if (_at >= text.Length)
                    throw new InvalidDataException($"TEXTMAP line {line}: a string with no closing quote");
                char c = text[_at++];
                if (c == '"')
                    break;
                if (c == '\n')
                    _line++;
                if (c == '\\' && _at < text.Length)
                {
                    char escaped = text[_at++];
                    value.Append(escaped switch { 'n' => '\n', 't' => '\t', _ => escaped });
                    continue;
                }
                value.Append(c);
            }
            return new Token(TokenKind.String, value.ToString(), line);
        }

        private void SkipSpaceAndComments()
        {
            while (_at < text.Length)
            {
                char c = text[_at];
                if (c == '\n')
                {
                    _line++;
                    _at++;
                }
                else if (char.IsWhiteSpace(c))
                    _at++;
                else if (c == '/' && _at + 1 < text.Length && text[_at + 1] == '/')
                {
                    while (_at < text.Length && text[_at] != '\n')
                        _at++;
                }
                else if (c == '/' && _at + 1 < text.Length && text[_at + 1] == '*')
                {
                    int end = text.IndexOf("*/", _at + 2, StringComparison.Ordinal);
                    var comment = end < 0 ? text[_at..] : text[_at..(end + 2)];
                    _line += comment.Count(ch => ch == '\n');
                    _at = end < 0 ? text.Length : end + 2;
                }
                else
                    break;
            }
        }
    }
}
