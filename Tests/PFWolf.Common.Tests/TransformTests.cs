namespace PFWolf.Common.Tests;

public class TransformTests
{
    [Test]
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
        Assert.That(transform.Position, Is.EqualTo(initialPosition)); // Should remain unchanged
        Assert.That(updatedTransform.Position.X, Is.EqualTo(newX));
        Assert.That(updatedTransform.Position.Y, Is.EqualTo(newY));
        Assert.That(updatedTransform.CalculatedSize, Is.EqualTo(transform.CalculatedSize)); // Size should remain unchanged
    }

    [Test]
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
        Assert.That(transform.OriginalSize, Is.EqualTo(initialSize));
        Assert.That(updatedTransform.OriginalSize.Width, Is.EqualTo(newWidth));
        Assert.That(updatedTransform.OriginalSize.Height, Is.EqualTo(newHeight));
        Assert.That(updatedTransform.Position, Is.EqualTo(transform.Position)); // Should remain unchanged
    }
}
