namespace PFWolf.Common.Tests;

public class TransformCalculatorTests
{
    [Theory]
    // Origin 0,0
    [InlineData(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, 0, 0, 0, 0,
        TestDisplayName = "Absolute top-left at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPoint.TopCenter, PositionType.Absolute, 88, 64, 0, 0, -44, 0,
        TestDisplayName = "Absolute top-center at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPoint.TopRight, PositionType.Absolute, 88, 64, 0, 0, -88, 0,
        TestDisplayName = "Absolute top-right at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPoint.MiddleLeft, PositionType.Absolute, 88, 64, 0, 0, 0, -32,
        TestDisplayName = "Absolute middle-left at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPoint.Center, PositionType.Absolute, 88, 64, 0, 0, -44, -32,
        TestDisplayName = "Absolute center at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPoint.MiddleRight, PositionType.Absolute, 88, 64, 0, 0, -88, -32,
        TestDisplayName = "Absolute middle-right at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPoint.BottomLeft, PositionType.Absolute, 88, 64, 0, 0, 0, -64,
        TestDisplayName = "Absolute bottom-left at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPoint.BottomCenter, PositionType.Absolute, 88, 64, 0, 0, -44, -64,
        TestDisplayName = "Absolute bottom-center at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPoint.BottomRight, PositionType.Absolute, 88, 64, 0, 0, -88, -64,
        TestDisplayName = "Absolute bottom-right at 0,0")]

    // Origin at non-zeros
    [InlineData(640, 400, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, 50, 32, 0, 0,
        TestDisplayName = "Absolute top-left at non-zero")]
    [InlineData(640, 400, 50, 32, AnchorPoint.TopCenter, PositionType.Absolute, 88, 64, 50, 32, -44, 0,
        TestDisplayName = "Absolute top-center at non-zero")]
    [InlineData(640, 400, 50, 32, AnchorPoint.TopRight, PositionType.Absolute, 88, 64, 50, 32, -88, 0,
        TestDisplayName = "Absolute top-right at non-zero")]
    [InlineData(640, 400, 50, 32, AnchorPoint.MiddleLeft, PositionType.Absolute, 88, 64, 50, 32, 0, -32,
        TestDisplayName = "Absolute middle-left at non-zero")]
    [InlineData(640, 400, 50, 32, AnchorPoint.Center, PositionType.Absolute, 88, 64, 50, 32, -44, -32,
        TestDisplayName = "Absolute center at non-zero")]
    [InlineData(640, 400, 50, 32, AnchorPoint.MiddleRight, PositionType.Absolute, 88, 64, 50, 32, -88, -32,
        TestDisplayName = "Absolute middle-right at non-zero")]
    [InlineData(640, 400, 50, 32, AnchorPoint.BottomLeft, PositionType.Absolute, 88, 64, 50, 32, 0, -64,
        TestDisplayName = "Absolute bottom-left at non-zero")]
    [InlineData(640, 400, 50, 32, AnchorPoint.BottomCenter, PositionType.Absolute, 88, 64, 50, 32, -44, -64,
        TestDisplayName = "Absolute bottom-center at non-zero")]
    [InlineData(640, 400, 50, 32, AnchorPoint.BottomRight, PositionType.Absolute, 88, 64, 50, 32, -88, -64,
        TestDisplayName = "Absolute bottom-right at non-zero")]

    // Negative origin
    [InlineData(640, 400, -100, -100, AnchorPoint.BottomRight, PositionType.Absolute, 88, 64, -100, -100, -88, -64,
        TestDisplayName = "Absolute bottom-right with negative positions")]

    // Relative
    [InlineData(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Relative, 88, 64, 0, 0, 0, 0,
        TestDisplayName = "Relative top-left at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPoint.TopCenter, PositionType.Relative, 88, 64, 0, 0, -88, 0,
        TestDisplayName = "Relative top-center at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPoint.TopRight, PositionType.Relative, 88, 64, 0, 0, -176, 0,
        TestDisplayName = "Relative top-right at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPoint.MiddleLeft, PositionType.Relative, 88, 64, 0, 0, 0, -64,
        TestDisplayName = "Relative middle-left at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPoint.Center, PositionType.Relative, 88, 64, 0, 0, -88, -64,
        TestDisplayName = "Relative center at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPoint.MiddleRight, PositionType.Relative, 88, 64, 0, 0, -176, -64,
        TestDisplayName = "Relative middle-right at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPoint.BottomLeft, PositionType.Relative, 88, 64, 0, 0, 0, -128,
        TestDisplayName = "Relative bottom-left at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPoint.BottomCenter, PositionType.Relative, 88, 64, 0, 0, -88, -128,
        TestDisplayName = "Relative bottom-center at 0,0")]
    [InlineData(640, 400, 0, 0, AnchorPoint.BottomRight, PositionType.Relative, 88, 64, 0, 0, -176, -128,
        TestDisplayName = "Relative bottom-right at 0,0")]
    public void Calculate_Transform_Position_Sets_Based_On_Scaling_Type_Adjusts_Position_Origin_AndOr_Offset(
        int screenWidth,
        int screenHeight,
        int positionX,
        int positionY,
        AnchorPoint anchorPosition,
        PositionType scaleType,
        int sizeWidth,
        int sizeHeight,
        int expectedPositionX,
        int expectedPositionY,
        int expectedOffsetX,
        int expectedOffsetY)
    {
        // Arrange
        //var calculator = new TransformCalculator(screenWidth, screenHeight);
        var transform = Transform.Create(
            new Point(positionX, positionY),
            scaleType,
            new Dimension(sizeWidth, sizeHeight),
            anchorPosition,
            BoundingBoxType.NoBounds,
            screenAnchorPoint: AnchorPoint.TopLeft
        );

        // Act
        //var transform = calculator.CalculateTransform(sutTransform);

        // Assert
        Assert.NotNull(transform);
        Assert.IsType<Transform>(transform);
        Assert.Equal(new Point(expectedPositionX, expectedPositionY), transform.Position);
        Assert.Equal(new Point(expectedOffsetX, expectedOffsetY), transform.Offset);
    }

    [Theory]
    // Origin 0,0
    [InlineData(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopLeft, 0, 0, 0, 0, 176, 128,
        TestDisplayName = "Absolute position top-left at 0,0 scales graphic from screen top-left")]
    [InlineData(640, 480, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopLeft, 0, 0, 0, 0, 176, 128,
        TestDisplayName = "Non 4:3, absolute position top-left at 0,0 scales graphic from screen top-left")]
    [InlineData(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopCenter, 320, 0, 0, 0, 176,128,
        TestDisplayName = "Absolute position top-left at 0,0 scales graphic from screen top-center")]
    [InlineData(640, 480, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopCenter, 320, 0, 0, 0, 176, 128,
        TestDisplayName = "Non 4:3, absolute position top-left at 0,0 scales graphic from screen top-center")]
    [InlineData(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopRight, 640, 0, 0, 0, 176, 128,
        TestDisplayName = "Absolute position top-left at 0,0 scales graphic from screen top-right")]
    [InlineData(640, 480, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopRight, 640, 0, 0, 0, 176, 128,
        TestDisplayName = "Non 4:3, absolute position top-left at 0,0 scales graphic from screen top-right")]
    [InlineData(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.MiddleLeft, 0, 200, 0, 0, 176, 128,
        TestDisplayName = "Absolute position top-left at 0,0 scales graphic from screen middle-left")]
    [InlineData(640, 480, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.MiddleLeft, 0, 240, 0, 0, 176, 128,
        TestDisplayName = "Non 4:3, absolute position top-left at 0,0 scales graphic from screen middle-left")]
    [InlineData(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.Center, 320, 200, 0, 0, 176, 128,
        TestDisplayName = "Absolute position top-left at 0,0 scales graphic from screen center")]
    [InlineData(640, 480, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.Center, 320, 240, 0, 0, 176, 128,
        TestDisplayName = "Non 4:3, absolute position top-left at 0,0 scales graphic from screen center")]
    [InlineData(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.MiddleRight, 640, 200, 0, 0, 176, 128,
        TestDisplayName = "Absolute position top-left at 0,0 scales graphic from screen middle-right")]
    [InlineData(640, 480, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.MiddleRight, 640, 240, 0, 0, 176, 128,
        TestDisplayName = "Non 4:3, absolute position top-left at 0,0 scales graphic from screen middle-right")]
    [InlineData(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomLeft, 0, 400, 0, 0, 176, 128,
        TestDisplayName = "Absolute position top-left at 0,0 scales graphic from screen bottom-left")]
    [InlineData(640, 480, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomLeft, 0, 480, 0, 0, 176, 128,
        TestDisplayName = "Non 4:3, absolute position top-left at 0,0 scales graphic from screen bottom-left")]
    [InlineData(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomCenter, 320, 400, 0, 0, 176, 128,
        TestDisplayName = "Absolute position top-left at 0,0 scales graphic from screen bottom-center")]
    [InlineData(640, 480, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomCenter, 320, 480, 0, 0, 176, 128,
        TestDisplayName = "Non 4:3, absolute position top-left at 0,0 scales graphic from screen bottom-center")]
    [InlineData(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomRight, 640, 400, 0, 0, 176, 128,
        TestDisplayName = "Absolute position top-left at 0,0 scales graphic from screen bottom-right")]
    [InlineData(640, 480, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomRight, 640, 480, 0, 0, 176, 128,
        TestDisplayName = "Non 4:3, absolute position top-left at 0,0 scales graphic from screen bottom-right")]

    // Non-zero origins
    [InlineData(640, 400, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopLeft, 50, 32, 0, 0, 176, 128,
        TestDisplayName = "Absolute position top-left at non-zero scales graphic from screen top-left")]
    [InlineData(640, 480, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopLeft, 60, 32, 0, 0, 176, 128,
        TestDisplayName = "Non 4:3, absolute position top-left at non-zero scales graphic from screen top-left")]
    [InlineData(640, 400, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopCenter, 270, 32, 0, 0, 176, 128,
        TestDisplayName = "Absolute position top-left at non-zero scales graphic from screen top-center")]
    [InlineData(640, 480, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopCenter, 270, 32, 0, 0, 176, 128,
        TestDisplayName = "Non 4:3, absolute position top-left at non-zero scales graphic from screen top-center")]
    [InlineData(640, 400, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopRight, 590, 32, 0, 0, 176, 128,
        TestDisplayName = "Absolute position top-left at non-zero scales graphic from screen top-right")]
    [InlineData(640, 480, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopRight, 590, 32, 0, 0, 176, 128,
        TestDisplayName = "Non 4:3, absolute position top-left at non-zero scales graphic from screen top-right")]
    [InlineData(640, 400, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.MiddleLeft, 50, 232, 0, 0, 176, 128,
        TestDisplayName = "Absolute position top-left at non-zero scales graphic from screen middle-left")]
    [InlineData(640, 480, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.MiddleLeft, 50, 272, 0, 0, 176, 128,
        TestDisplayName = "Non 4:3, absolute position top-left at non-zero scales graphic from screen middle-left")]
    [InlineData(640, 400, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.Center, 270, 232, 0, 0, 176, 128,
        TestDisplayName = "Absolute position top-left at non-zero scales graphic from screen center")]
    [InlineData(640, 480, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.Center, 270, 272, 0, 0, 176, 128,
        TestDisplayName = "Non 4:3, absolute position top-left at non-zero scales graphic from screen center")]
    [InlineData(640, 400, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.MiddleRight, 590, 232, 0, 0, 176, 128,
        TestDisplayName = "Absolute position top-left at non-zero scales graphic from screen middle-right")]
    [InlineData(640, 480, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.MiddleRight, 590, 272, 0, 0, 176, 128,
        TestDisplayName = "Non 4:3, absolute position top-left at non-zero scales graphic from screen middle-right")]
    [InlineData(640, 400, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomLeft, 50, 432, 0, 0, 176, 128,
        TestDisplayName = "Absolute position top-left at non-zero scales graphic from screen bottom-left")]
    [InlineData(640, 480, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomLeft, 50, 512, 0, 0, 176, 128,
        TestDisplayName = "Non 4:3, absolute position top-left at non-zero scales graphic from screen bottom-left")]
    [InlineData(640, 400, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomCenter, 270, 432, 0, 0, 176, 128,
        TestDisplayName = "Absolute position top-left at non-zero scales graphic from screen bottom-center")]
    [InlineData(640, 480, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomCenter, 270, 512, 0, 0, 176, 128,
        TestDisplayName = "Non 4:3, absolute position top-left at non-zero scales graphic from screen bottom-center")]
    [InlineData(640, 400, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomRight, 590, 432, 0, 0, 176, 128,
        TestDisplayName = "Absolute position top-left at non-zero scales graphic from screen bottom-right")]
    [InlineData(640, 480, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomRight, 590, 512, 0, 0, 176, 128,
        TestDisplayName = "Non 4:3, absolute position top-left at non-zero scales graphic from screen bottom-right")]

    // TODO: Relative
    // TODO: Non-zero origins
    public void Calculate_Transform_BoundingBox_Scales_Transform(
        int screenWidth,
        int screenHeight,
        int positionX,
        int positionY,
        AnchorPoint anchorPosition,
        PositionType scaleType,
        int sizeWidth,
        int sizeHeight,
        AnchorPoint boundingBoxAlignment,
        int expectedPositionX,
        int expectedPositionY,
        int expectedOffsetX,
        int expectedOffsetY,
        int expectedSizeWidth,
        int expectedSizeHeight)
    {
        // Arrange
        //var calculator = new TransformCalculator(screenWidth, screenHeight);
        var transform = Transform.Create(
            new Point(positionX, positionY),
            scaleType,
            new Dimension(sizeWidth, sizeHeight),
            anchorPosition,
            BoundingBoxType.Scale,
            screenAnchorPoint:  boundingBoxAlignment
        );

        transform.SetScreenSize(screenWidth, screenHeight);

        // Act
        //var transform = calculator.CalculateTransform(suTransform);

        // Assert
        Assert.NotNull(transform);
        Assert.IsType<Transform>(transform);
        Assert.Equal(new Point(expectedPositionX, expectedPositionY), transform.Position);
        Assert.Equal(new Point(expectedOffsetX, expectedOffsetY), transform.Offset);
        Assert.Equal(new Dimension(expectedSizeWidth, expectedSizeHeight), transform.Size);
    }
}
