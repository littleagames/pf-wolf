using PFWolf.Networking;

namespace PFWolf.Tests;

public class FormattingTests
{
    [TestCase(0, "0:00")]
    [TestCase(69, "0:00")]
    [TestCase(70, "0:01")]
    [TestCase(70 * 75, "1:15")]
    [TestCase(70 * 3600, "1:00:00")]
    [TestCase(70 * (3600 * 12 + 61), "12:01:01")]
    [TestCase(-70, "0:00")]
    public void FormatPlayTime_Is_Minutes_Or_Hours(int tics, string expected)
    {
        Assert.That(Program.FormatPlayTime(tics), Is.EqualTo(expected));
    }

    [TestCase("Alice", "Alice")]
    [TestCase("  Bob  ", "Bob")]
    [TestCase(null, "Player")]
    [TestCase("", "Player")]
    [TestCase("\t\n", "Player")]
    [TestCase("Anné", "Ann")]
    [TestCase("A\u0007B", "AB")]
    [TestCase("ABCDEFGHIJKLMNOPQRST", "ABCDEFGHIJKLMNOP")]
    [TestCase("ABCDEFGHIJKLMNO QRST", "ABCDEFGHIJKLMNO")]
    public void CleanName_Keeps_Printable_Ascii_Up_To_The_Limit(string? name, string expected)
    {
        Assert.That(NetProtocol.CleanName(name), Is.EqualTo(expected));
    }

    [Test]
    public void DoChecksum_Sums_Xors_Of_Neighbouring_Bytes()
    {
        // (1^2) + (2^4) + (4^8) = 3 + 6 + 12
        Assert.That(Program.DoChecksum([1, 2, 4, 8], 100), Is.EqualTo(121));
        Assert.That(Program.DoChecksum([], 7), Is.EqualTo(7));
    }

    [Test]
    public void SameMods_Compares_Name_And_Version_In_Order_Ignoring_Case()
    {
        // Arrange
        List<SavedMod> a = [new("Switch Demo", "1.0", "switch-demo"), new("Lights", "", "lights.pk3")];
        List<SavedMod> sameOtherCase = [new("switch demo", "1.0", "elsewhere"), new("LIGHTS", "", "x.pk3")];
        List<SavedMod> otherOrder = [a[1], a[0]];
        List<SavedMod> otherVersion = [new("Switch Demo", "1.1", "switch-demo"), a[1]];

        // Act / Assert
        Assert.That(SavedMod.SameMods(a, sameOtherCase), Is.True);
        Assert.That(SavedMod.SameMods(a, otherOrder), Is.False);
        Assert.That(SavedMod.SameMods(a, otherVersion), Is.False);
        Assert.That(SavedMod.SameMods(a, [a[0]]), Is.False);
    }
}
