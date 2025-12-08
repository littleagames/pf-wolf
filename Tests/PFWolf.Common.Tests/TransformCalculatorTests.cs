namespace PFWolf.Common.Tests;

public class TransformCalculatorTests
{
    [Theory]
    [InlineData(640, 400, 0, 0, AnchorPosition.TopLeft, ScaleType.Absolute, 88, 64, 0, 0, 0, 0,
        TestDisplayName = "Absolute top-left at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.TopCenter, ScaleType.Absolute, 88, 64, 0, 0, -44, 0,
        TestDisplayName = "Absolute top-center at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.TopRight, ScaleType.Absolute, 88, 64, 0, 0, -88, 0,
        TestDisplayName = "Absolute top-right at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.LeftCenter, ScaleType.Absolute, 88, 64, 0, 0, 0, -32,
        TestDisplayName = "Absolute middle-left at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.Center, ScaleType.Absolute, 88, 64, 0, 0, -44, -32,
        TestDisplayName = "Absolute center at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.RightCenter, ScaleType.Absolute, 88, 64, 0, 0, -88, -32,
        TestDisplayName = "Absolute middle-right at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.BottomLeft, ScaleType.Absolute, 88, 64, 0, 0, 0, -64,
        TestDisplayName = "Absolute bottom-left at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.BottomCenter, ScaleType.Absolute, 88, 64, 0, 0, -44, -64,
        TestDisplayName = "Absolute bottom-center at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.BottomRight, ScaleType.Absolute, 88, 64, 0, 0, -88, -64,
        TestDisplayName = "Absolute bottom-right at 0,0")]
    //[InlineData(640, 400, 0, 0, 0, 0, AnchorPosition.TopLeft, ScaleType.Absolute, 100, 100,BoundingBoxType.NoBounds, AnchorPosition.TopLeft, 0, 0, 0, 0,
    //    TestDisplayName = "Absolute top-left/top-left at 0,0")]
    //[InlineData(640, 400, 0, 0, 0, 0, AnchorPosition.TopLeft, ScaleType.Relative, 100, 100, BoundingBoxType.NoBounds, AnchorPosition.TopLeft, 0, 0, 0, 0,
    //    TestDisplayName = "Relative top-left/top-left at 0,0")]
    //[InlineData(640, 400, 10, 10, 0, 0, AnchorPosition.TopLeft, ScaleType.Absolute, 100, 100, BoundingBoxType.NoBounds, AnchorPosition.TopLeft, 10, 10, 0, 0,
    //    TestDisplayName = "Absolute top-left/top-left")]
    //[InlineData(640, 400, 10, 10, 0, 0, AnchorPosition.TopLeft, ScaleType.Relative, 100, 100, BoundingBoxType.NoBounds, AnchorPosition.TopLeft, 20, 20, 0, 0,
    //    TestDisplayName = "Relative top-left/top-left")]
    //[InlineData(640, 400, 140, 100, 0, 0, AnchorPosition.TopCenter, ScaleType.Relative, 50, 22, BoundingBoxType.NoBounds, AnchorPosition.TopLeft, 280, 200, 0, 0,
    //    TestDisplayName = "Relative top-center/top-left")]
    //[InlineData(640, 400, 140, 100, 0, 0, AnchorPosition.TopCenter, ScaleType.Relative, 50, 22, BoundingBoxType.NoBounds, AnchorPosition.TopCenter, 280, 200, -25, 0,
    //    TestDisplayName = "Relative top-center/top-center")]
    //[InlineData(640, 400, 140, 100, 0, 0, AnchorPosition.TopLeft, ScaleType.Relative, 50, 22, BoundingBoxType.NoBounds, AnchorPosition.TopCenter, 280, 200, 25, 0,
    //    TestDisplayName = "Relative top-left/top-center")]

    // Check all options at 0,0
    // check absolute vs relative
    // priority
    // size scaling?
    public void Calculate_Transform_Position_Sets_Based_On_Scaling_Type(
        int screenWidth,
        int screenHeight,
        int positionX,
        int positionY,
        AnchorPosition anchorPosition,
        ScaleType scaleType,
        int sizeWidth,
        int sizeHeight,
        int expectedPositionX,
        int expectedPositionY,
        int expectedOffsetX,
        int expectedOffsetY)
    {
        // Arrange
        var calculator = new TransformCalculator(screenWidth, screenHeight);
        var sutTransform = new Transform
        {
            Position = new Position(
                new Vector2(positionX, positionY),
                anchorPosition,
                scaleType),
            Rotation = 0,
            Size = new Dimension(sizeWidth, sizeHeight),
            BoundingBox = BoundingBoxType.NoBounds,
            BoundingBoxAlignment = AnchorPosition.TopLeft
        };

        // Act
        var transform = calculator.CalculateTransform(sutTransform);

        // Assert
        Assert.NotNull(transform);
        Assert.IsType<Transform>(transform);
        Assert.Equal(new Vector2(expectedPositionX, expectedPositionY), transform.Position.Origin);
        Assert.Equal(new Vector2(expectedOffsetX, expectedOffsetY), transform.Position.Offset);
    }
}
