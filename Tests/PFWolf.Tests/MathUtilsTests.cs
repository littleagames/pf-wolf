namespace PFWolf.Tests;

public class MathUtilsTests
{
    private const int One = 1 << MathUtils.FRACBITS;

    [TestCase(1, 1, 1)]
    [TestCase(2, 3, 6)]
    [TestCase(-2, 3, -6)]
    [TestCase(-4, -5, 20)]
    [TestCase(0, 7, 0)]
    public void FixedMul_Multiplies_Whole_Numbers(int a, int b, int expected)
    {
        // Act
        var result = MathUtils.FixedMul(a * One, b * One);

        // Assert
        Assert.That(result, Is.EqualTo(expected * One));
    }

    [Test]
    public void FixedMul_Multiplies_Fractions()
    {
        // Arrange
        var half = One / 2;

        // Act
        var result = MathUtils.FixedMul(half, half);

        // Assert
        Assert.That(result, Is.EqualTo(One / 4));
    }

    [Test]
    public void FixedMul_Does_Not_Overflow_On_Large_Intermediate()
    {
        // 1000.0 * 1000.0 overflows a 32-bit intermediate but the result fits in 16.16
        var result = MathUtils.FixedMul(1000 * One, 30 * One);

        Assert.That(result, Is.EqualTo(30000 * One));
    }

    [TestCase(6, 3, 2)]
    [TestCase(-6, 3, -2)]
    [TestCase(10, -5, -2)]
    public void FixedDiv_Divides_Whole_Numbers(int a, int b, int expected)
    {
        // Act
        var result = MathUtils.FixedDiv(a * One, b * One);

        // Assert
        Assert.That(result, Is.EqualTo(expected * One));
    }

    [Test]
    public void FixedDiv_Produces_Fractions()
    {
        // Act
        var result = MathUtils.FixedDiv(One, 4 * One);

        // Assert
        Assert.That(result, Is.EqualTo(One / 4));
    }

    [Test]
    public void FixedDiv_Undoes_FixedMul()
    {
        // Arrange
        var a = 123 * One + One / 8;
        var b = 7 * One;

        // Act
        var result = MathUtils.FixedDiv(MathUtils.FixedMul(a, b), b);

        // Assert
        Assert.That(result, Is.EqualTo(a));
    }
}
