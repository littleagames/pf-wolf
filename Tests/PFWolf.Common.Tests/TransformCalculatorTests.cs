namespace PFWolf.Common.Tests;

public class TransformCalculatorTests
{
    // Origin 0,0
    [TestCase(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, 0, 0, 0, 0, TestName = "Absolute top-left at 0,0")]
    [TestCase(640, 400, 0, 0, AnchorPoint.TopCenter, PositionType.Absolute, 88, 64, 0, 0, 44, 0, TestName = "Absolute top-center at 0,0")]
    [TestCase(640, 400, 0, 0, AnchorPoint.TopRight, PositionType.Absolute, 88, 64, 0, 0, 88, 0, TestName = "Absolute top-right at 0,0")]
    [TestCase(640, 400, 0, 0, AnchorPoint.MiddleLeft, PositionType.Absolute, 88, 64, 0, 0, 0, 32, TestName = "Absolute middle-left at 0,0")]
    [TestCase(640, 400, 0, 0, AnchorPoint.Center, PositionType.Absolute, 88, 64, 0, 0, 44, 32, TestName = "Absolute center at 0,0")]
    [TestCase(640, 400, 0, 0, AnchorPoint.MiddleRight, PositionType.Absolute, 88, 64, 0, 0, 88, 32, TestName = "Absolute middle-right at 0,0")]
    [TestCase(640, 400, 0, 0, AnchorPoint.BottomLeft, PositionType.Absolute, 88, 64, 0, 0, 0, 64, TestName = "Absolute bottom-left at 0,0")]
    [TestCase(640, 400, 0, 0, AnchorPoint.BottomCenter, PositionType.Absolute, 88, 64, 0, 0, 44, 64, TestName = "Absolute bottom-center at 0,0")]
    [TestCase(640, 400, 0, 0, AnchorPoint.BottomRight, PositionType.Absolute, 88, 64, 0, 0, 88, 64, TestName = "Absolute bottom-right at 0,0")]

    // Origin at non-zeros
    [TestCase(640, 400, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, 50, 32, 0, 0, TestName = "Absolute top-left at non-zero")]
    [TestCase(640, 400, 50, 32, AnchorPoint.TopCenter, PositionType.Absolute, 88, 64, 50, 32, 44, 0, TestName = "Absolute top-center at non-zero")]
    [TestCase(640, 400, 50, 32, AnchorPoint.TopRight, PositionType.Absolute, 88, 64, 50, 32, 88, 0, TestName = "Absolute top-right at non-zero")]
    [TestCase(640, 400, 50, 32, AnchorPoint.MiddleLeft, PositionType.Absolute, 88, 64, 50, 32, 0, 32, TestName = "Absolute middle-left at non-zero")]
    [TestCase(640, 400, 50, 32, AnchorPoint.Center, PositionType.Absolute, 88, 64, 50, 32, 44, 32, TestName = "Absolute center at non-zero")]
    [TestCase(640, 400, 50, 32, AnchorPoint.MiddleRight, PositionType.Absolute, 88, 64, 50, 32, 88, 32, TestName = "Absolute middle-right at non-zero")]
    [TestCase(640, 400, 50, 32, AnchorPoint.BottomLeft, PositionType.Absolute, 88, 64, 50, 32, 0, 64, TestName = "Absolute bottom-left at non-zero")]
    [TestCase(640, 400, 50, 32, AnchorPoint.BottomCenter, PositionType.Absolute, 88, 64, 50, 32, 44, 64, TestName = "Absolute bottom-center at non-zero")]
    [TestCase(640, 400, 50, 32, AnchorPoint.BottomRight, PositionType.Absolute, 88, 64, 50, 32, 88, 64, TestName = "Absolute bottom-right at non-zero")]

    // Negative origin
    [TestCase(640, 400, -100, -100, AnchorPoint.BottomRight, PositionType.Absolute, 88, 64, -100, -100, 88, 64, TestName = "Absolute bottom-right with negative positions")]

    // Relative
    [TestCase(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Relative, 88, 64, 0, 0, 0, 0, TestName = "Relative top-left at 0,0")]
    [TestCase(640, 400, 0, 0, AnchorPoint.TopCenter, PositionType.Relative, 88, 64, 0, 0, 44, 0, TestName = "Relative top-center at 0,0")]
    [TestCase(640, 400, 0, 0, AnchorPoint.TopRight, PositionType.Relative, 88, 64, 0, 0, 88, 0, TestName = "Relative top-right at 0,0")]
    [TestCase(640, 400, 0, 0, AnchorPoint.MiddleLeft, PositionType.Relative, 88, 64, 0, 0, 0, 32, TestName = "Relative middle-left at 0,0")]
    [TestCase(640, 400, 0, 0, AnchorPoint.Center, PositionType.Relative, 88, 64, 0, 0, 44, 32, TestName = "Relative center at 0,0")]
    [TestCase(640, 400, 0, 0, AnchorPoint.MiddleRight, PositionType.Relative, 88, 64, 0, 0, 88, 32, TestName = "Relative middle-right at 0,0")]
    [TestCase(640, 400, 0, 0, AnchorPoint.BottomLeft, PositionType.Relative, 88, 64, 0, 0, 0, 64, TestName = "Relative bottom-left at 0,0")]
    [TestCase(640, 400, 0, 0, AnchorPoint.BottomCenter, PositionType.Relative, 88, 64, 0, 0, 44, 64, TestName = "Relative bottom-center at 0,0")]
    [TestCase(640, 400, 0, 0, AnchorPoint.BottomRight, PositionType.Relative, 88, 64, 0, 0, 88, 64, TestName = "Relative bottom-right at 0,0")]
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

        // Act
        var transform = Transform.Create(
            new Point(positionX, positionY),
            scaleType,
            new Dimension(sizeWidth, sizeHeight),
            anchorPosition,
            BoundingBoxType.NoBounds,
            screenAnchorPoint: AnchorPoint.TopLeft
        );

        // Assert
        Assert.That(transform, Is.Not.Null, "Transform should not be null");
        Assert.That(transform, Is.InstanceOf<Transform>(), "Subject should be of type Tranform");
        Assert.That(transform.CalcuatedPosition, Is.EqualTo(new Point(expectedPositionX, expectedPositionY)), "Position is not equal to expected value.");
        Assert.That(transform.CalculatedOffset, Is.EqualTo(new Point(expectedOffsetX, expectedOffsetY)), "Offset is not equal to expected value.");
    }

    // Origin 0,0
    [TestCase(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopLeft, 0, 0, 0, 0, 176, 128, TestName = "Absolute position top-left at 0,0 scales graphic from screen top-left")]
    [TestCase(640, 480, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopLeft, 0, 0, 0, 0, 176, 128, TestName = "Non 4:3, absolute position top-left at 0,0 scales graphic from screen top-left")]
    [TestCase(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopCenter, 320, 0, 0, 0, 176, 128, TestName = "Absolute position top-left at 0,0 scales graphic from screen top-center")]
    [TestCase(640, 480, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopCenter, 320, 0, 0, 0, 176, 128, TestName = "Non 4:3, absolute position top-left at 0,0 scales graphic from screen top-center")]
    [TestCase(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopRight, 640, 0, 0, 0, 176, 128, TestName = "Absolute position top-left at 0,0 scales graphic from screen top-right")]
    [TestCase(640, 480, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopRight, 640, 0, 0, 0, 176, 128, TestName = "Non 4:3, absolute position top-left at 0,0 scales graphic from screen top-right")]
    [TestCase(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.MiddleLeft, 0, 200, 0, 0, 176, 128, TestName = "Absolute position top-left at 0,0 scales graphic from screen middle-left")]
    [TestCase(640, 480, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.MiddleLeft, 0, 240, 0, 0, 176, 128, TestName = "Non 4:3, absolute position top-left at 0,0 scales graphic from screen middle-left")]
    [TestCase(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.Center, 320, 200, 0, 0, 176, 128, TestName = "Absolute position top-left at 0,0 scales graphic from screen center")]
    [TestCase(640, 480, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.Center, 320, 240, 0, 0, 176, 128, TestName = "Non 4:3, absolute position top-left at 0,0 scales graphic from screen center")]
    [TestCase(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.MiddleRight, 640, 200, 0, 0, 176, 128, TestName = "Absolute position top-left at 0,0 scales graphic from screen middle-right")]
    [TestCase(640, 480, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.MiddleRight, 640, 240, 0, 0, 176, 128, TestName = "Non 4:3, absolute position top-left at 0,0 scales graphic from screen middle-right")]
    [TestCase(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomLeft, 0, 400, 0, 0, 176, 128, TestName = "Absolute position top-left at 0,0 scales graphic from screen bottom-left")]
    [TestCase(640, 480, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomLeft, 0, 480, 0, 0, 176, 128, TestName = "Non 4:3, absolute position top-left at 0,0 scales graphic from screen bottom-left")]
    [TestCase(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomCenter, 320, 400, 0, 0, 176, 128, TestName = "Absolute position top-left at 0,0 scales graphic from screen bottom-center")]
    [TestCase(640, 480, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomCenter, 320, 480, 0, 0, 176, 128, TestName = "Non 4:3, absolute position top-left at 0,0 scales graphic from screen bottom-center")]
    [TestCase(640, 400, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomRight, 640, 400, 0, 0, 176, 128, TestName = "Absolute position top-left at 0,0 scales graphic from screen bottom-right")]
    [TestCase(640, 480, 0, 0, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomRight, 640, 480, 0, 0, 176, 128, TestName = "Non 4:3, absolute position top-left at 0,0 scales graphic from screen bottom-right")]

    //// Non-zero origins
    [TestCase(640, 400, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopLeft, 50, 32, 0, 0, 176, 128, TestName = "Absolute position top-left at non-zero scales graphic from screen top-left")]
    [TestCase(640, 480, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopLeft, 50, 32, 0, 0, 176, 128, TestName = "Non 4:3, absolute position top-left at non-zero scales graphic from screen top-left")]
    [TestCase(640, 400, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopCenter, 370, 32, 0, 0, 176, 128, TestName = "Absolute position top-left at non-zero scales graphic from screen top-center")]
    [TestCase(640, 480, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopCenter, 370, 32, 0, 0, 176, 128, TestName = "Non 4:3, absolute position top-left at non-zero scales graphic from screen top-center")]
    [TestCase(640, 400, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopRight, 690, 32, 0, 0, 176, 128, TestName = "Absolute position top-left at non-zero scales graphic from screen top-right")]
    [TestCase(640, 480, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.TopRight, 690, 32, 0, 0, 176, 128, TestName = "Non 4:3, absolute position top-left at non-zero scales graphic from screen top-right")]
    [TestCase(640, 400, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.MiddleLeft, 50, 232, 0, 0, 176, 128, TestName = "Absolute position top-left at non-zero scales graphic from screen middle-left")]
    [TestCase(640, 480, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.MiddleLeft, 50, 272, 0, 0, 176, 128, TestName = "Non 4:3, absolute position top-left at non-zero scales graphic from screen middle-left")]
    [TestCase(640, 400, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.Center, 370, 232, 0, 0, 176, 128, TestName = "Absolute position top-left at non-zero scales graphic from screen center")]
    [TestCase(640, 480, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.Center, 370, 272, 0, 0, 176, 128, TestName = "Non 4:3, absolute position top-left at non-zero scales graphic from screen center")]
    [TestCase(640, 400, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.MiddleRight, 690, 232, 0, 0, 176, 128, TestName = "Absolute position top-left at non-zero scales graphic from screen middle-right")]
    [TestCase(640, 480, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.MiddleRight, 690, 272, 0, 0, 176, 128, TestName = "Non 4:3, absolute position top-left at non-zero scales graphic from screen middle-right")]
    [TestCase(640, 400, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomLeft, 50, 432, 0, 0, 176, 128, TestName = "Absolute position top-left at non-zero scales graphic from screen bottom-left")]
    [TestCase(640, 480, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomLeft, 50, 512, 0, 0, 176, 128, TestName = "Non 4:3, absolute position top-left at non-zero scales graphic from screen bottom-left")]
    [TestCase(640, 400, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomCenter, 370, 432, 0, 0, 176, 128, TestName = "Absolute position top-left at non-zero scales graphic from screen bottom-center")]
    [TestCase(640, 480, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomCenter, 370, 512, 0, 0, 176, 128, TestName = "Non 4:3, absolute position top-left at non-zero scales graphic from screen bottom-center")]
    [TestCase(640, 400, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomRight, 690, 432, 0, 0, 176, 128, TestName = "Absolute position top-left at non-zero scales graphic from screen bottom-right")]
    [TestCase(640, 480, 50, 32, AnchorPoint.TopLeft, PositionType.Absolute, 88, 64, AnchorPoint.BottomRight, 690, 512, 0, 0, 176, 128, TestName = "Non 4:3, absolute position top-left at non-zero scales graphic from screen bottom-right")]

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
        Assert.That(transform, Is.Not.Null, "Transform should not be null");
        Assert.That(transform, Is.InstanceOf<Transform>(), "Subject should be of type Tranform");
        Assert.That(transform.CalcuatedPosition, Is.EqualTo(new Point(expectedPositionX, expectedPositionY)), "Position is not equal to expected value.");
        Assert.That(transform.CalculatedOffset, Is.EqualTo(new Point(expectedOffsetX, expectedOffsetY)), "Offset is not equal to expected value.");
        //Assert.That(transform.OriginalSize, Is.EqualTo(new Dimension(expectedSizeWidth, expectedSizeHeight)), "OriginalSize is not equal to expected value.");
        Assert.That(transform.CalculatedSize, Is.EqualTo(new Dimension(expectedSizeWidth, expectedSizeHeight)), "(Calculated) Size is not equal to expected value.");
    }
}
