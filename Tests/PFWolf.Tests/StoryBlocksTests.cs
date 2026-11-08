using PFWolf.Definitions;

namespace PFWolf.Tests;

/// <summary>Height-plane blocks as the editor types and shows them (Definitions.StoryBlocks)</summary>
public class StoryBlocksTests
{
    [TestCase("3-5", 0x0305)]
    [TestCase("3", 3)]                  // a plain stop: from story 1
    [TestCase("1-3", 3)]
    [TestCase("1-2,4-8", 0x4082)]
    [TestCase(" 3 - 5 , 7 - 8 ", 0x7385)]
    [TestCase("1-2,3-5", 5)]            // touching: one block
    [TestCase("", 0)]
    [TestCase("0", 0)]
    public void Stories_Are_Typed_As_Start_Dash_Stop(string text, int value)
    {
        Assert.That(StoryBlocks.Parse(text, out var error), Is.EqualTo((ushort?)value));
        Assert.That(error, Is.Null);
    }

    [TestCase("9")]
    [TestCase("5-3")]
    [TestCase("3-5,4-6")]               // the second not above the first
    [TestCase("1,3,5")]
    [TestCase("a-b")]
    public void What_Isnt_Stories_Says_Why(string text)
    {
        Assert.That(StoryBlocks.Parse(text, out var error), Is.Null);
        Assert.That(error, Is.Not.Empty);
    }

    [TestCase(0, "")]
    [TestCase(3, "1-3")]
    [TestCase(0x0305, "3-5")]
    [TestCase(0x7385, "3-5,7-8")]
    [TestCase(0x0010, "16")]            // not blocks: the bare number
    public void A_Value_Shows_As_Its_Stories(int value, string text)
    {
        Assert.That(StoryBlocks.Format(value), Is.EqualTo(text));
    }
}
