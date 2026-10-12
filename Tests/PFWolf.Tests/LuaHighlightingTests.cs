using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using PFWolf.Editor.Rendering;

namespace PFWolf.Tests;

/// <summary>The script browser's Lua colors (Resources/Lua.xshd): what each kind of word is colored as</summary>
public class LuaHighlightingTests
{
    // The color names given to each part of a line, as (text, color) in order
    private static List<(string Text, string Color)> Colors(string text, int lineNumber = 1)
    {
        var document = new TextDocument(text);
        var highlighter = new DocumentHighlighter(document, LuaHighlighting.Definition);
        var line = highlighter.HighlightLine(lineNumber);
        return line.Sections
            .Where(section => section.Color?.Name != null)
            .Select(section => (document.GetText(section.Offset, section.Length), section.Color.Name!))
            .ToList();
    }

    [Test]
    public void The_Definition_Loads()
    {
        Assert.That(LuaHighlighting.Definition.Name, Is.EqualTo("Lua"));
    }

    [TestCase("local x = nil", "local", "Keyword")]
    [TestCase("local x = nil", "nil", "Literal")]
    [TestCase("function A_FireRing(self, n) end", "A_FireRing", "Action")]
    [TestCase("function A_FireRing(self, n) end", "self", "Self")]
    [TestCase("T_Think = 1", "T_Think", "Action")]
    [TestCase("game.random(6)", "game", "Library")]
    [TestCase("print(ipairs(t))", "print", "BuiltIn")]
    [TestCase("x = 0x1F + 2.5e3", "0x1F", "Number")]
    [TestCase("x = 0x1F + 2.5e3", "2.5e3", "Number")]
    [TestCase("x = math.random(6)", "math.random", "Missing")]
    [TestCase("x = os.time()", "os", "Missing")]
    [TestCase("require('x')", "require", "Missing")]
    [TestCase("x = 'it''s' .. \"say \\\"hi\\\"\"", "\"say \\\"hi\\\"\"", "String")]
    [TestCase("x = 1 -- math.random here is a comment", "-- math.random here is a comment", "Comment")]
    public void Each_Kind_Of_Word_Has_Its_Color(string line, string word, string color)
    {
        // Act
        var colors = Colors(line);

        // Assert
        Assert.That(colors, Does.Contain((word, color)), string.Join(", ", colors));
    }

    [Test]
    public void Math_Floor_Is_The_Library_Not_Missing()
    {
        // Act
        var colors = Colors("x = math.floor(2.5)");

        // Assert
        Assert.That(colors, Does.Contain(("math", "Library")));
        Assert.That(colors.Select(c => c.Color), Does.Not.Contain("Missing"));
    }

    [Test]
    public void Block_Comments_And_Long_Strings_Run_Over_Lines()
    {
        // Arrange
        const string text = "--[[ a\nfunction inside()\n]] local s = [[one\ntwo]]\n";

        // Act / Assert: line 2 is all comment, line 4 all string
        Assert.That(Colors(text, 2), Is.EqualTo(new[] { ("function inside()", "Comment") }));
        Assert.That(Colors(text, 4), Is.EqualTo(new[] { ("two]]", "String") }));
    }
}
