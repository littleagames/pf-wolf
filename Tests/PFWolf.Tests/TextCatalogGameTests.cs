using PFWolf.Editor.Data;
using PFWolf.Editor.Rendering;

namespace PFWolf.Tests;

/// <summary>The text browser's catalog and article layout over the real games. Needs a built game folder (pfwolf.pk3 and the data files).</summary>
[Category("GameData")]
public class TextCatalogGameTests
{
    private static string? GameFolder()
    {
        var gameBin = Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64");
        return new[] { Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA"), Path.Combine(gameBin, "Debug", "net10.0"), Path.Combine(gameBin, "Release", "net10.0") }
            .FirstOrDefault(folder => !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, "pfwolf.pk3")));
    }

    private static GameContent Load(string game, params string[] mods)
    {
        var folder = GameFolder();
        if (folder == null)
            Assert.Ignore("No built game folder with pfwolf.pk3; build PFWolf for x64 or set PFWOLF_GAMEDATA");

        var workingFolder = Directory.GetCurrentDirectory();
        try
        {
            return GameContent.Load(folder!, game, mods);
        }
        catch (PFWolf.Exceptions.DataFilesException)
        {
            Assert.Ignore($"{game}'s data files aren't in {folder}");
            throw;
        }
        finally
        {
            Directory.SetCurrentDirectory(workingFolder);
        }
    }

    [TestCase("wolf3d")]
    [TestCase("wolf3d-shareware")]
    public void Every_Article_Lays_Out_Without_Errors(string game)
    {
        // Arrange
        var content = Load(game);
        var art = TextCatalog.Art(content, out var font, out var fontProblem);

        // Act
        var articles = TextCatalog.Build(content).Where(entry => entry.Format == TextFormat.Article).ToList();

        // Assert
        Assert.That(font, Is.Not.Null, fontProblem);
        Assert.That(articles, Is.Not.Empty);
        foreach (var entry in articles)
        {
            var layout = ArticleLayout.Build(TextCatalog.Read(content, entry.Name)!, art);
            TestContext.Out.WriteLine($"{entry.Name}: {layout.Pages.Count} pages; {string.Join("; ", layout.Problems)}");
            Assert.That(layout.Problems.Where(problem => problem.IsError), Is.Empty, entry.Name);
            Assert.That(layout.Pages, Has.Count.EqualTo(layout.PageCount), entry.Name);

            // Something besides the background is drawn on each page
            var back = TextCatalog.BackColor(content);
            foreach (var page in layout.Pages)
                Assert.That(ArticleScreen.Draw(page, content, font, back).Indices.Count(index => index != back), Is.GreaterThan(1000), $"{entry.Name} page {page.Number}");
        }
    }

    [Test]
    public void The_End_Texts_Are_Shown_By_Their_Clusters()
    {
        // Arrange
        var content = Load("wolf3d");

        // Act
        var entries = TextCatalog.Build(content).ToDictionary(entry => entry.Name);

        // Assert
        Assert.That(entries["ENDART1"].Uses, Has.Some.Contains("Cluster 1"));
        Assert.That(entries["ENDART6"].Uses, Has.Some.Contains("Cluster 6"));
        Assert.That(entries["HELPART"].Uses, Has.Some.Contains("Read This!"));
    }

    [Test]
    public void Saved_Texts_Replace_And_Add_To_The_Games_Through_A_Mod()
    {
        // Arrange: ENDART1 changed and a new text saved into a mod folder
        var modFolder = Path.Combine(Path.GetTempPath(), $"pfwolf-texts-{Guid.NewGuid():N}");
        try
        {
            var path = TextCatalog.Save(modFolder, "ENDART1", "^P\r\nChanged.\r\n^E\r\n");
            TextCatalog.Save(modFolder, "MYSTORY", TextCatalog.NewArticle);

            // Act
            var content = Load("wolf3d", modFolder);
            var entries = TextCatalog.Build(content).ToDictionary(entry => entry.Name);

            // Assert
            Assert.That(path, Is.EqualTo(Path.Combine(modFolder, "texts", "endart1.txt")));
            Assert.That(TextCatalog.Read(content, "ENDART1"), Is.EqualTo("^P\r\nChanged.\r\n^E\r\n"));
            Assert.That(entries["ENDART1"].Origins[^1], Does.StartWith("replaced by").And.Contain("texts/endart1.txt"));
            Assert.That(entries["MYSTORY"].Format, Is.EqualTo(TextFormat.Article));
            Assert.That(content.ModPathOf("MYSTORY", nameof(PFWolf.Assets.TextAsset)), Is.EqualTo(Path.GetFullPath(modFolder)));
            Assert.That(TextCatalog.CheckNewName(content, "MYSTORY"), Is.Not.Null);
            Assert.That(TextCatalog.CheckNewName(content, "my story"), Is.Not.Null);
            Assert.That(TextCatalog.CheckNewName(content, "ENDART7"), Is.Null);

            var layout = ArticleLayout.Build(TextCatalog.Read(content, "MYSTORY")!, TextCatalog.Art(content, out _, out _));
            Assert.That(layout.Problems.Where(problem => problem.IsError), Is.Empty);
            Assert.That(layout.Pages, Has.Count.EqualTo(2));
        }
        finally
        {
            if (Directory.Exists(modFolder))
                Directory.Delete(modFolder, recursive: true);
        }
    }
}
