using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>game-info article: the page articles are laid out on</summary>
public class ArticleInfoTests
{
    private const string OnePicture = """
        article:
          frame:
            - { pic: R_Background, x: 0, y: 0 }
          margins: { top: 32, bottom: 16 }
          page-number: { x: 243, y: 8, color: Black }
        """;

    [Test]
    public void Left_Out_It_Is_Wolf3ds_Help_Window()
    {
        var article = YamlDataEntryLoader.Deserialize<GameInfoAsset>("maps: {}\n").Article;

        Assert.That(article.Frame.Select(p => (p.Pic, p.X, p.Y)), Is.EqualTo(new[]
            { ("h_topwindow", 0, 0), ("h_leftwindow", 0, 8), ("h_rightwindow", 312, 8), ("h_bottominfo", 8, 176) }));
        Assert.That((article.Margins.Top, article.Margins.Bottom, article.TextRows), Is.EqualTo((16, 32, ArticleLayout.TextRows)));
        Assert.That(article.PageNumber.Text(2, 5), Is.EqualTo("pg 2 of 5"));
    }

    [Test]
    public void A_Game_Can_Frame_It_With_One_Picture_And_Its_Own_Margins()
    {
        // Act
        var article = YamlDataEntryLoader.Deserialize<GameInfoAsset>(OnePicture).Article;
        var layout = ArticleLayout.Build("^P\nab\n^E", new ArticleArt
        {
            FontWidths = Enumerable.Repeat((byte)8, 256).ToArray(),
            Layout = article,
        });

        // Assert: the one picture, the text from 32 down (16 lines between), the page number up top
        Assert.That(article.Margins.Left, Is.EqualTo(16));          // left out: Wolf3D's
        Assert.That(article.TextRows, Is.EqualTo(15));
        var draws = layout.Pages[0].Draws;
        Assert.That(draws[0], Is.EqualTo(new ArticleDraw(ArticleDrawKind.Picture, 0, 0, -1) { Text = "R_Background" }));
        Assert.That(draws.Count(d => d.Kind == ArticleDrawKind.Picture), Is.EqualTo(1));
        Assert.That((draws[1].X, draws[1].Y, draws[1].Text), Is.EqualTo((16, 32, "ab")));
        Assert.That((draws[^1].X, draws[^1].Y, draws[^1].Text), Is.EqualTo((243, 8, "pg 1 of 1")));
    }

    [Test]
    public void The_Page_Number_Can_Be_Left_Out()
    {
        var article = YamlDataEntryLoader.Deserialize<GameInfoAsset>("article:\n  page-number: { show: false }\n").Article;
        var layout = ArticleLayout.Build("^P\nab\n^E", new ArticleArt { FontWidths = Enumerable.Repeat((byte)8, 256).ToArray(), Layout = article });

        Assert.That(layout.Pages[0].Draws.Any(d => d.Text?.StartsWith("pg") == true), Is.False);
    }
}
