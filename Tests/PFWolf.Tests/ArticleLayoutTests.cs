using PFWolf.Editor.Data;

namespace PFWolf.Tests;

/// <summary>The editor's Wolf3D article layout (help screens, end texts), against the game's WL_TEXT rules</summary>
public class ArticleLayoutTests
{
    // Every character 8 wide, and one 40x40 picture as number 3
    private static ArticleArt Art(int charWidth = 8) => new()
    {
        FontWidths = Enumerable.Repeat((byte)charWidth, 256).ToArray(),
        PictureName = number => number == 3 ? "pic" : null,
        PictureSize = name => name == "pic" ? (40, 40) : null,
        TextColor = 0,
        PageNumberColor = 99,
    };

    private static List<ArticleDraw> Words(ArticlePage page)
        => page.Draws.Where(draw => draw.Kind == ArticleDrawKind.Text && draw.Index >= 0).ToList();

    [Test]
    public void Pages_Are_Split_At_Each_P_And_Numbered_Out_Of_The_Count()
    {
        // Arrange
        var text = "^P\r\nfirst page\r\n^P\r\nsecond\r\n^E\r\n";

        // Act
        var layout = ArticleLayout.Build(text, Art());

        // Assert
        Assert.That(layout.Problems, Is.Empty);
        Assert.That(layout.PageCount, Is.EqualTo(2));
        Assert.That(layout.Pages, Has.Count.EqualTo(2));
        Assert.That(Words(layout.Pages[0]).Select(draw => draw.Text), Is.EqualTo(new[] { "first", "page" }));
        Assert.That(Words(layout.Pages[1]).Select(draw => draw.Text), Is.EqualTo(new[] { "second" }));
        Assert.That(layout.Pages[1].Draws.Last().Text, Is.EqualTo("pg 2 of 2"));
        Assert.That(layout.Pages[1].Draws.Last().Color, Is.EqualTo(99));
    }

    [Test]
    public void Words_Start_At_The_Margins_And_Are_Followed_By_Their_Spaces()
    {
        // Arrange: "ab" is 16 wide, then two spaces of 7
        var layout = ArticleLayout.Build("^P\nab  cd\nef\n^E", Art());

        // Act
        var words = Words(layout.Pages[0]);

        // Assert
        Assert.That(words[0], Is.EqualTo(new ArticleDraw(ArticleDrawKind.Text, 16, 16, 3) { Text = "ab" }));
        Assert.That(words[1].X, Is.EqualTo(16 + 16 + 2 * ArticleLayout.SpaceWidth));
        Assert.That((words[2].X, words[2].Y), Is.EqualTo((16, 26)));
    }

    [Test]
    public void A_Word_Past_The_Right_Margin_Wraps_To_The_Next_Line()
    {
        // Arrange: 36 words of 8 pixels with 7 between: 19 fit in 288 pixels (16 to 304)
        var text = "^P\n" + string.Join(" ", Enumerable.Repeat("a", 36)) + "\n^E";

        // Act
        var words = Words(ArticleLayout.Build(text, Art()).Pages[0]);

        // Assert
        Assert.That(words.Count(word => word.Y == 16), Is.EqualTo(19));
        Assert.That(words[19].X, Is.EqualTo(16));
        Assert.That(words[19].Y, Is.EqualTo(26));
    }

    [Test]
    public void C_Changes_The_Color_By_Its_Two_Hex_Digits()
    {
        // Act
        var words = Words(ArticleLayout.Build("^P\nplain ^C4Ared\n^E", Art()).Pages[0]);

        // Assert
        Assert.That(words.Select(word => (word.Text, word.Color)), Is.EqualTo(new[] { ("plain", (byte)0), ("red", (byte)0x4a) }));
    }

    [Test]
    public void G_Draws_The_Picture_And_Keeps_The_Text_Clear_Of_It()
    {
        // Arrange: a 40x40 picture at x 20 (left half: the left margin moves past it), y 16 (lines 0 to 4)
        var text = "^P\n^G16,20,3\nbeside\n\n\n\n\nbelow\n^E";

        // Act
        var page = ArticleLayout.Build(text, Art()).Pages[0];

        // Assert: drawn at x rounded down to 8; the text right of 20 + 40 + 8 until line 5
        var picture = page.Draws.Single(draw => draw.Kind == ArticleDrawKind.Picture && draw.Index >= 0);
        Assert.That((picture.Text, picture.X, picture.Y), Is.EqualTo(("pic", 16, 16)));
        var words = Words(page);
        Assert.That((words[0].Text, words[0].X), Is.EqualTo(("beside", 68)));
        Assert.That((words[1].Text, words[1].X, words[1].Y), Is.EqualTo(("below", 16, 66)));
    }

    [Test]
    public void A_Full_Page_Leaves_Out_The_Rest_Until_The_Next_Page()
    {
        // Arrange: more lines than the page has
        var lines = Enumerable.Range(1, ArticleLayout.TextRows + 3).Select(line => $"line{line}");
        var text = "^P\n" + string.Join("\n", lines) + "\n^P\nnext\n^E";

        // Act
        var layout = ArticleLayout.Build(text, Art());

        // Assert
        Assert.That(Words(layout.Pages[0]), Has.Count.EqualTo(ArticleLayout.TextRows));
        Assert.That(layout.Pages[0].CutFrom, Is.Not.Null);
        Assert.That(layout.Problems.Single().Message, Does.Contain("is full"));
        Assert.That(layout.Problems.Single().IsError, Is.False);
        Assert.That(Words(layout.Pages[1]).Single().Text, Is.EqualTo("next"));
    }

    [TestCase("no page\n^E", "must start with ^P")]
    [TestCase("^P\nno end\n", "No ^E")]
    [TestCase("^P\n^G16,20,7\n^E", "isn't in alias.yaml")]
    [TestCase("^P\n^L5,16\n^E", "^L's y")]
    public void What_Makes_The_Game_Quit_Is_An_Error(string text, string message)
    {
        // Act
        var layout = ArticleLayout.Build(text, Art());

        // Assert
        Assert.That(layout.Problems.Where(problem => problem.IsError).Select(problem => problem.Message), Has.Some.Contains(message));
    }

    [Test]
    public void Problems_Say_Which_Line_They_Are_On_And_The_Page_Follows_The_Text()
    {
        // Arrange
        var text = "^P\none\n^P\ntwo ^G1,1,9\n^E";

        // Act
        var layout = ArticleLayout.Build(text, Art());

        // Assert
        Assert.That(layout.Problems.Single().Line, Is.EqualTo(4));
        Assert.That(layout.PageAt(text.IndexOf("one")), Is.EqualTo(0));
        Assert.That(layout.PageAt(text.IndexOf("two")), Is.EqualTo(1));
    }
}
