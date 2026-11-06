using PFWolf.Managers;

namespace PFWolf.Tests;

public class ScreenTransitionTests
{
    // The styles that draw a transition (not the palette, which steps the palette), by name:
    // FadeStyle is internal, so a public test can't take one
    static IEnumerable<string> TransitionStyles() =>
        Enum.GetValues<FadeStyle>().Where(s => s != FadeStyle.Palette).Select(s => s.ToString());

    // The LFSR VideoManager uses for 640x400 and under (17 bits, 8 of them y)
    const uint RndMask = 0x00012000;
    const int RndBitsY = 8;

    const int Stride = 64, ScreenHeight = 40;
    const uint OldPixel = 0xff102030, NewPixel = 0xffa0b0c0, Outside = 0xff555555;

    // A 64x40 screen, the transition's rectangle 48x30 at (8, 5) of it; the old and new frames
    // patterned or solid inside it, and outside it a color neither has
    static TransitionCanvas Canvas(bool fromSolid = false, bool toSolid = false)
    {
        var from = new uint[Stride * ScreenHeight];
        var to = new uint[Stride * ScreenHeight];
        Array.Fill(from, Outside);
        Array.Fill(to, Outside);
        for (int y = 5; y < 35; y++)
        {
            for (int x = 8; x < 56; x++)
            {
                from[y * Stride + x] = fromSolid ? OldPixel : OldPixel + (uint)((x + y) & 7);
                to[y * Stride + x] = toSolid ? NewPixel : NewPixel + (uint)((x * y) & 7);
            }
        }

        return new TransitionCanvas
        {
            From = from,
            To = to,
            Output = (uint[])from.Clone(),
            Stride = Stride,
            X = 8,
            Y = 5,
            Width = 48,
            Height = 30,
            ScaleFactor = 1,
            FromIsSolid = fromSolid,
            ToIsSolid = toSolid,
        };
    }

    static FadeStyle Style(string name) => Enum.Parse<FadeStyle>(name);

    [TestCaseSource(nameof(TransitionStyles))]
    public void Every_Style_Ends_On_The_New_Frame(string name)
    {
        foreach (var (fromSolid, toSolid) in new[] { (false, false), (false, true), (true, false) })
        {
            // Arrange
            var canvas = Canvas(fromSolid, toSolid);
            var transition = ScreenTransition.Create(Style(name), canvas, RndMask, RndBitsY);

            // Act: some way through, then to the end
            transition.Draw(canvas, 0.3);
            transition.Draw(canvas, 0.6);
            bool done = transition.Draw(canvas, 1);

            // Assert
            Assert.That(done, Is.True, $"{name} ({fromSolid}, {toSolid}) isn't done at the end");
            Assert.That(canvas.Output, Is.EqualTo(canvas.To), $"{name} ({fromSolid}, {toSolid}) doesn't end on the new frame");
        }
    }

    [TestCaseSource(nameof(TransitionStyles))]
    public void No_Style_Draws_Outside_Its_Rectangle(string name)
    {
        // Arrange
        var canvas = Canvas();
        var transition = ScreenTransition.Create(Style(name), canvas, RndMask, RndBitsY);

        for (double progress = 0; progress <= 1; progress += 0.1)
        {
            // Act
            transition.Draw(canvas, progress);

            // Assert
            for (int y = 0; y < ScreenHeight; y++)
                for (int x = 0; x < Stride; x++)
                    if (x < 8 || x >= 56 || y < 5 || y >= 35)
                        Assert.That(canvas.Output[y * Stride + x], Is.EqualTo(Outside), $"{name} drew at {x},{y} at {progress:0.0}");
        }
    }

    [TestCase("Crossfade")]
    [TestCase("Dither")]
    [TestCase("Burn")]
    [TestCase("Clock")]
    [TestCase("Spiral")]
    [TestCase("WipeLeft")]
    [TestCase("WipeRight")]
    [TestCase("WipeUp")]
    [TestCase("WipeDown")]
    [TestCase("Blinds")]
    [TestCase("Iris")]
    [TestCase("Doors")]
    [TestCase("Crt")]
    public void The_New_Styles_Start_On_The_Old_Frame(string name)
    {
        // Arrange
        var canvas = Canvas();
        var transition = ScreenTransition.Create(Style(name), canvas, RndMask, RndBitsY);

        // Act
        bool done = transition.Draw(canvas, 0);

        // Assert
        Assert.That(done, Is.False);
        Assert.That(canvas.Output, Is.EqualTo(canvas.From));
    }

    [TestCase("Iris")]
    [TestCase("Doors")]
    [TestCase("Crt")]
    public void Shutting_Styles_Are_Shut_Halfway_Between_Two_Pictures(string name)
    {
        // Arrange
        var canvas = Canvas();
        var transition = ScreenTransition.Create(Style(name), canvas, RndMask, RndBitsY);

        // Act
        transition.Draw(canvas, 0.5);

        // Assert: neither picture shows
        for (int y = 0; y < 30; y++)
            for (int x = 0; x < 48; x++)
            {
                int i = canvas.Index(x, y);
                Assert.That(canvas.Output[i], Is.Not.EqualTo(canvas.From[i]).And.Not.EqualTo(canvas.To[i]), $"{name} at {x},{y}");
            }
    }

    [TestCase("Iris")]
    [TestCase("Crt")]
    public void Fading_Out_To_A_Color_Shuts_On_It(string name)
    {
        // Arrange
        var canvas = Canvas(toSolid: true);
        var transition = ScreenTransition.Create(Style(name), canvas, RndMask, RndBitsY);

        // Act: nearly done, it should be all but shut on the new color
        transition.Draw(canvas, 0.995);

        // Assert
        int colored = 0;
        for (int y = 0; y < 30; y++)
            for (int x = 0; x < 48; x++)
                if (canvas.Output[canvas.Index(x, y)] == NewPixel)
                    colored++;
        Assert.That(colored, Is.GreaterThan(48 * 30 * 9 / 10));
    }

    [Test]
    public void A_Wipe_Right_Reaches_The_Left_First()
    {
        // Arrange
        var canvas = Canvas();
        var transition = ScreenTransition.Create(FadeStyle.WipeRight, canvas, RndMask, RndBitsY);

        // Act
        transition.Draw(canvas, 0.5);

        // Assert
        Assert.That(canvas.Output[canvas.Index(0, 10)], Is.EqualTo(canvas.To[canvas.Index(0, 10)]));
        Assert.That(canvas.Output[canvas.Index(47, 10)], Is.EqualTo(canvas.From[canvas.Index(47, 10)]));
    }

    [Test]
    public void The_Clock_Starts_At_Twelve_And_Goes_Clockwise()
    {
        // Arrange
        var canvas = Canvas();
        var transition = ScreenTransition.Create(FadeStyle.Clock, canvas, RndMask, RndBitsY);

        // Act: a quarter of the way round, the top right quarter is in and the top left isn't
        transition.Draw(canvas, 0.26);

        // Assert
        int topRight = canvas.Index(40, 2), topLeft = canvas.Index(8, 2);
        Assert.That(canvas.Output[topRight], Is.EqualTo(canvas.To[topRight]));
        Assert.That(canvas.Output[topLeft], Is.EqualTo(canvas.From[topLeft]));
    }

    [TestCase("wipe-left", "WipeLeft")]
    [TestCase("WipeLeft", "WipeLeft")]
    [TestCase("wipe_up", "WipeUp")]
    [TestCase("CRT", "Crt")]
    [TestCase(" iris ", "Iris")]
    public void Styles_Are_Read_By_Name_With_Or_Without_Dashes(string text, string expected)
    {
        Assert.That(FadeStyles.TryParse(text, out var style), Is.True);
        Assert.That(style.ToString(), Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("4")]
    [TestCase("sparkle")]
    public void Unknown_Styles_Are_Not_Read(string text)
    {
        Assert.That(FadeStyles.TryParse(text, out _), Is.False);
    }

    [Test]
    public void Styles_Are_Named_In_Lower_Case_With_Dashes()
    {
        Assert.That(FadeStyles.Name(FadeStyle.WipeDown), Is.EqualTo("wipe-down"));
        Assert.That(FadeStyles.Names, Does.Contain("crt").And.Contain("palette"));
        Assert.That(FadeStyles.Names.All(n => FadeStyles.TryParse(n, out _)), Is.True);
    }

    [Test]
    public void A_Rectangle_Of_One_Color_Is_Solid()
    {
        var canvas = Canvas(toSolid: true);
        Assert.That(TransitionCanvas.IsSolid(canvas.To, Stride, 8, 5, 48, 30), Is.True);
        Assert.That(TransitionCanvas.IsSolid(canvas.From, Stride, 8, 5, 48, 30), Is.False);
        Assert.That(TransitionCanvas.IsSolid(canvas.To, Stride, 0, 0, 64, 40), Is.False);       // the outside differs
    }
}
