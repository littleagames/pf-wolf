namespace PFWolf.Common.Tests;

public class TransformCalculatorTests
{
    [Theory]
    // Origin 0,0
    [InlineData(640, 400, 0, 0, AnchorPosition.TopLeft, ScaleType.Absolute, 88, 64, 0, 0, 0, 0,
        TestDisplayName = "Absolute top-left at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.TopCenter, ScaleType.Absolute, 88, 64, 0, 0, -44, 0,
        TestDisplayName = "Absolute top-center at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.TopRight, ScaleType.Absolute, 88, 64, 0, 0, -88, 0,
        TestDisplayName = "Absolute top-right at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.MiddleLeft, ScaleType.Absolute, 88, 64, 0, 0, 0, -32,
        TestDisplayName = "Absolute middle-left at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.Center, ScaleType.Absolute, 88, 64, 0, 0, -44, -32,
        TestDisplayName = "Absolute center at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.MiddleRight, ScaleType.Absolute, 88, 64, 0, 0, -88, -32,
        TestDisplayName = "Absolute middle-right at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.BottomLeft, ScaleType.Absolute, 88, 64, 0, 0, 0, -64,
        TestDisplayName = "Absolute bottom-left at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.BottomCenter, ScaleType.Absolute, 88, 64, 0, 0, -44, -64,
        TestDisplayName = "Absolute bottom-center at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.BottomRight, ScaleType.Absolute, 88, 64, 0, 0, -88, -64,
        TestDisplayName = "Absolute bottom-right at 0,0")]

    // Origin at non-zeros
    [InlineData(640, 400, 50, 32, AnchorPosition.TopLeft, ScaleType.Absolute, 88, 64, 50, 32, 0, 0,
        TestDisplayName = "Absolute top-left at non-zero")]
    [InlineData(640, 400, 50, 32, AnchorPosition.TopCenter, ScaleType.Absolute, 88, 64, 50, 32, -44, 0,
        TestDisplayName = "Absolute top-center at non-zero")]
    [InlineData(640, 400, 50, 32, AnchorPosition.TopRight, ScaleType.Absolute, 88, 64, 50, 32, -88, 0,
        TestDisplayName = "Absolute top-right at non-zero")]
    [InlineData(640, 400, 50, 32, AnchorPosition.MiddleLeft, ScaleType.Absolute, 88, 64, 50, 32, 0, -32,
        TestDisplayName = "Absolute middle-left at non-zero")]
    [InlineData(640, 400, 50, 32, AnchorPosition.Center, ScaleType.Absolute, 88, 64, 50, 32, -44, -32,
        TestDisplayName = "Absolute center at non-zero")]
    [InlineData(640, 400, 50, 32, AnchorPosition.MiddleRight, ScaleType.Absolute, 88, 64, 50, 32, -88, -32,
        TestDisplayName = "Absolute middle-right at non-zero")]
    [InlineData(640, 400, 50, 32, AnchorPosition.BottomLeft, ScaleType.Absolute, 88, 64, 50, 32, 0, -64,
        TestDisplayName = "Absolute bottom-left at non-zero")]
    [InlineData(640, 400, 50, 32, AnchorPosition.BottomCenter, ScaleType.Absolute, 88, 64, 50, 32, -44, -64,
        TestDisplayName = "Absolute bottom-center at non-zero")]
    [InlineData(640, 400, 50, 32, AnchorPosition.BottomRight, ScaleType.Absolute, 88, 64, 50, 32, -88, -64,
        TestDisplayName = "Absolute bottom-right at non-zero")]

    
    [InlineData(640, 400, -100, -100, AnchorPosition.BottomRight, ScaleType.Absolute, 88, 64, -100, -100, -88, -64,
        TestDisplayName = "Absolute bottom-right with negative positions")]

    [InlineData(640, 400, 0, 0, AnchorPosition.TopLeft, ScaleType.Relative, 88, 64, 0, 0, 0, 0,
        TestDisplayName = "Relative top-left at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.TopCenter, ScaleType.Relative, 88, 64, 0, 0, -88, 0,
        TestDisplayName = "Relative top-center at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.TopRight, ScaleType.Relative, 88, 64, 0, 0, -176, 0,
        TestDisplayName = "Relative top-right at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.MiddleLeft, ScaleType.Relative, 88, 64, 0, 0, 0, -64,
        TestDisplayName = "Relative middle-left at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.Center, ScaleType.Relative, 88, 64, 0, 0, -88, -64,
        TestDisplayName = "Relative center at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.MiddleRight, ScaleType.Relative, 88, 64, 0, 0, -176, -64,
        TestDisplayName = "Relative middle-right at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.BottomLeft, ScaleType.Relative, 88, 64, 0, 0, 0, -128,
        TestDisplayName = "Relative bottom-left at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.BottomCenter, ScaleType.Relative, 88, 64, 0, 0, -88, -128,
        TestDisplayName = "Relative bottom-center at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPosition.BottomRight, ScaleType.Relative, 88, 64, 0, 0, -176, -128,
        TestDisplayName = "Relative bottom-right at 0,0")]
    public void Calculate_Transform_Position_Sets_Based_On_Scaling_Type_Adjusts_Position_Origin_AndOr_Offset(
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

    [Theory]
    // Origin 0,0
    [InlineData(640, 400, 0, 0, AnchorPosition.TopLeft, ScaleType.Absolute, 88, 64, AnchorPosition.TopLeft, 0, 0, 0, 0, 176, 128,
        TestDisplayName = "Absolute position top-left at 0,0 scales graphic from screen top-left")]
    [InlineData(640, 480, 0, 0, AnchorPosition.TopLeft, ScaleType.Absolute, 88, 64, AnchorPosition.TopLeft, 0, 0, 0, 0, 176, 128,
        TestDisplayName = "Non 4:3, absolute position top-left at 0,0 scales graphic from screen top-left")]
    [InlineData(640, 400, 0, 0, AnchorPosition.TopLeft, ScaleType.Absolute, 88, 64, AnchorPosition.TopCenter, 320, 0, 0, 0, 176,128,
        TestDisplayName = "Absolute position top-left at 0,0 scales graphic from screen top-center")]
    [InlineData(640, 480, 0, 0, AnchorPosition.TopLeft, ScaleType.Absolute, 88, 64, AnchorPosition.TopCenter, 320, 0, 0, 0, 176, 128,
        TestDisplayName = "Non 4:3, absolute position top-left at 0,0 scales graphic from screen top-center")]
    public void Calculate_Transform_BoundingBox_Scales_Transform(
        int screenWidth,
        int screenHeight,
        int positionX,
        int positionY,
        AnchorPosition anchorPosition,
        ScaleType scaleType,
        int sizeWidth,
        int sizeHeight,
        AnchorPosition boundingBoxAlignment,
        int expectedPositionX,
        int expectedPositionY,
        int expectedOffsetX,
        int expectedOffsetY,
        int expectedSizeWidth,
        int expectedSizeHeight)
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
            BoundingBox = BoundingBoxType.Scale,
            BoundingBoxAlignment = boundingBoxAlignment
        };

        // Act
        var transform = calculator.CalculateTransform(sutTransform);

        // Assert
        Assert.NotNull(transform);
        Assert.IsType<Transform>(transform);
        Assert.Equal(new Vector2(expectedPositionX, expectedPositionY), transform.Position.Origin);
        Assert.Equal(new Vector2(expectedOffsetX, expectedOffsetY), transform.Position.Offset);
        Assert.Equal(new Dimension(expectedSizeWidth, expectedSizeHeight), transform.Size);
    }
}
