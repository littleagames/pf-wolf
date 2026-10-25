namespace PFWolf.Tests;

public class TextPresenterTests
{
    // Splitting a script into pages touches none of the managers
    private static TextPresenter Presenter() => new(null!, null!, null!, null!, null!, null!, null!, null!);

    private static List<int> Pages(string script)
    {
        var pi = new PresenterInfo { Script = script };
        Presenter().TpInitScript(pi);
        return pi.Pages;
    }

    [Test]
    public void Each_Page_Starts_After_An_EP_But_The_Last_Ones()
    {
        // Arrange: three pages, each ended by ^EP
        const string script = "one^EPtwo^EPthree^EP^XX";

        // Act / Assert
        Assert.That(Pages(script), Is.EqualTo(new[] { 0, "one^EP".Length, "one^EPtwo^EP".Length }));
    }

    [Test]
    public void A_Script_Without_Pages_Is_One()
    {
        Assert.That(Pages("just text^XX"), Is.EqualTo(new[] { 0 }));
    }

    [Test]
    public void A_Commented_Line_Ends_No_Page()
    {
        // Arrange: a line starting with ; is a comment, whatever it holds
        const string script = "intro\r\n;not ^EP a page\r\nreal^EPnext^EP^XX";

        // Act / Assert
        Assert.That(Pages(script), Is.EqualTo(new[] { 0, script.IndexOf("next", StringComparison.Ordinal) }));
    }

    [TestCase("FC", 2)]
    [TestCase("SX", 1)]
    [TestCase("SH", 3)]
    [TestCase("CE", 0)]
    [TestCase("EP", 0)]
    public void Codes_Take_Their_Digits(string code, int digits)
    {
        Assert.That(TextPresenter.TpCodeDigits(code), Is.EqualTo(digits));
    }

    [Test]
    public void An_Info_Scripts_Words_Are_Its_Text_Without_Codes()
    {
        // Arrange: bstone's attack info for an electro-alien
        const string script = "^AN0B^FC17\n  ATTACKING:\n^FCA6 HIGH ENERGY\n PLASMA ALIEN";

        // Act / Assert
        Assert.That(TextPresenter.IsInfoScript(script), Is.True);
        Assert.That(TextPresenter.IsInfoScript("Food token: 1 credit"), Is.False);
        Assert.That(TextPresenter.InfoScriptText(script), Is.EqualTo("ATTACKING: HIGH ENERGY PLASMA ALIEN"));
    }
}
