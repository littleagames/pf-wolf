using PFWolf.Fonts;

namespace PFWolf.Tests;

public class ConsoleDrawingTests
{
    /// <summary>Every character one unit wide</summary>
    private sealed class OneWideFont : Font
    {
        public override int Height => 8;
        public override int Advance(char ch) => 1;
        protected override void DrawText(VideoManager video, int x, int y, string text, string color, bool silhouette, byte[]? rowColors) { }
    }

    private readonly Font _font = new OneWideFont();

    [Test]
    public void Only_Printable_Ascii_Is_Drawn()
    {
        Assert.That(ConsoleDrawing.ConsoleSafeText("a\tbéc\u0001~"), Is.EqualTo("a b?c?~"));
    }

    [Test]
    public void A_Long_Line_Breaks_At_The_Last_Space_That_Fits()
    {
        Assert.That(ConsoleDrawing.ConsoleWrapText("the quick brown fox", 10, _font), Is.EqualTo(new[] { "the quick", "brown fox" }));
    }

    [Test]
    public void A_Word_Wider_Than_The_Console_Is_Cut()
    {
        Assert.That(ConsoleDrawing.ConsoleWrapText("abcdefghij", 4, _font), Is.EqualTo(new[] { "abcd", "efgh", "ij" }));
    }

    [Test]
    public void A_Line_That_Fits_Is_One_Row()
    {
        Assert.That(ConsoleDrawing.ConsoleWrapText("short", 10, _font), Is.EqualTo(new[] { "short" }));
    }

    [Test]
    public void Text_Fits_Up_To_Its_Width()
    {
        Assert.That(ConsoleDrawing.ConsoleFitText("console", 3, _font), Is.EqualTo("con"));
        Assert.That(ConsoleDrawing.ConsoleTextWidth("console", _font), Is.EqualTo(7));
    }

    [Test]
    public void Nothing_Is_Put_Back_Until_The_Console_Was_Drawn_And_Is_Shut()
    {
        // Arrange: no screen to draw on (drawing would throw)
        var console = new ConsoleManager();
        var drawing = new ConsoleDrawing(null!, null!, null!, console, null!, null!);

        // Act / Assert: never drawn
        Assert.DoesNotThrow(drawing.RestoreBorderAfterConsole);

        // drawn, but still open
        drawing.consoleDrawn = true;
        console.Open();
        Assert.DoesNotThrow(drawing.RestoreBorderAfterConsole);
        Assert.That(drawing.consoleDrawn, Is.True);
    }
}
