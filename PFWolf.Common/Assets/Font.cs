namespace PFWolf.Common.Assets;

public record Font : Asset
{
    public Font(List<FontCharacter> fontCharacters)
    {
        FontCharacters = fontCharacters;
    }

    public List<FontCharacter> FontCharacters { get; } = new List<FontCharacter>();

    public Graphic ToGraphic(string text, byte fontColor, Dimension maxBounds)
    {
        var printX = 0;
        var printY = 0;

        var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries).ToList();
        var charWidthTable = new byte[3][];
        var charOffsetTable = new short[3][];


        // Calculate the widest line by summing each character's width from FontCharacters
        var largestLineWidth = 0;
        for (var li = 0; li < lines.Count; li++)
        {
            var line = lines[li];
            var lineWidth = 0;

            charWidthTable[li] ??= new byte[line.Length];
            charOffsetTable[li] ??= new short[line.Length+1];

            for (var ci = 0; ci < line.Length; ci++)
            {
                var ch = line[ci];
                var asciiIndex = (int)ch;
                byte width = 0;
                if (asciiIndex >= 0 && asciiIndex < FontCharacters.Count)
                {
                    width = FontCharacters[asciiIndex].Width;
                }
                else
                {
                    // Fallback: use space character width if available (ASCII 32)
                    const int spaceIndex = 32;
                    if (spaceIndex >= 0 && spaceIndex < FontCharacters.Count)
                    {
                        width = FontCharacters[spaceIndex].Width;
                    }
                    // otherwise skip unknown character
                }

                lineWidth += width;
                charWidthTable[li][ci] = (byte)width;
                charOffsetTable[li][ci+1] = (short)lineWidth;
            }

            if (lineWidth > largestLineWidth)
            {
                largestLineWidth = lineWidth;
            }
        }

        var textHeight = lines.Count * FontCharacters.Max(fc => fc.Height); // All should be the same size

        var graphicData = new byte[largestLineWidth * textHeight];

        foreach (char textChar in text)
        {
            var asciiIndex = (int)textChar;
            var fontChar = (asciiIndex >= 0 && asciiIndex < FontCharacters.Count) ? FontCharacters[asciiIndex] : null;

            if (fontChar is not null && fontChar.Data.Length > 0)
            {
                var modifiedFontData = new byte[fontChar.Data.Length];
                for (var i = 0; i < fontChar.Data.Length; i++)
                {
                    var fontFlag = fontChar.Data[i] > 0;
                    modifiedFontData[i] = fontFlag ? fontColor : (byte)0xff;
                }

                // TODO: Map textalign to position alignment
                //var charPosition = new Position(new Vector2(printX, printY), AnchorPosition.TopLeft, ScaleType.Relative);

                // TODO: Use the offset table to position characters correctly on graphicData

                for (var y = 0; y < fontChar.Height; y++)
                    for (var x = 0; x < fontChar.Width; x++)
                    {
                        //var gfxPosition = currentWidth
                        var gfxPosition = largestLineWidth * (printY+y) + printX + x;
                        var fontPosition = y * fontChar.Width + x;

                        graphicData[gfxPosition] = modifiedFontData[fontPosition];
                    }

                printX += fontChar?.Width ?? 0;
            }

            if (textChar == '\n')
            {
                printX = 0;
                printY = printY + (fontChar?.Height ?? 0);
                continue;
            }

            //printX += (int)(fontChar?.Width ?? 0);
        }

        return new Graphic
        {
            Data = graphicData,
            Size = new Dimension { Width = largestLineWidth, Height = textHeight },
            Offset = new Vector2(0, 0)
        };
    }
}

public record FontCharacter
{

    public short Height { get; set; }
    public byte Width { get; set; }
    public byte[] Data { get; set; } = [];
}
