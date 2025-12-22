namespace PFWolf.Common.Tests;

public class TransformTests
{
    [Fact]
    public void Transform_SetPosition_Doesnt_Modify_Previous_Transform()
    {
        // Arrange
        var initialPosition = new Point(10, 20);
        var initialSize = new Dimension(100, 32);
        var transform = Transform.Create(
            initialPosition,
            PositionType.Relative,
            initialSize,
            AnchorPoint.TopLeft,
            Common.BoundingBoxType.NoBounds,
            AnchorPoint.TopLeft);

        // Act
        var newX = 30;
        var newY = 40;
        var updatedTransform = transform.Copy().SetPosition(newX, newY);

        // Assert
        Assert.Equal(initialPosition, transform.Position); // Should remain unchanged
        Assert.Equal(newX, updatedTransform.Position.X);
        Assert.Equal(newY, updatedTransform.Position.Y);
        Assert.Equal(transform.Size, updatedTransform.Size); // Size should remain unchanged
    }

    [Fact]
    public void Transform_SetSize_Doesnt_Modify_Previous_Transform()
    {
        // Arrange
        var initialPosition = new Point(10, 20);
        var initialSize = new Dimension(100, 32);
        var transform = Transform.Create(
            initialPosition,
            PositionType.Relative,
            initialSize,
            AnchorPoint.TopLeft,
            Common.BoundingBoxType.NoBounds,
            AnchorPoint.TopLeft);

        // Act
        var newWidth = 130;
        var newHeight = 40;
        var updatedTransform = transform.Copy().SetSize(newWidth, newHeight);

        // Assert
        Assert.Equal(initialSize, transform.OriginalSize);
        Assert.Equal(newWidth, updatedTransform.OriginalSize.Width);
        Assert.Equal(newHeight, updatedTransform.OriginalSize.Height);
        Assert.Equal(transform.Position, updatedTransform.Position); // Should remain unchanged
    }
}
