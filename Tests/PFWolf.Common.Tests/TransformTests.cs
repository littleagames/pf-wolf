namespace PFWolf.Common.Tests;

public class TransformTests
{
    [Fact]
    public void Transform_HasChanged_Flag_Works_Correctly()
    {
        // Arrange
        var transform = new Transform(
            new Vector2(100, 200),
            AnchorPosition.TopLeft,
            ScaleType.Relative,
            BoundingBoxType.ScaleWidthToScreen);
        // Initial state should have HasChanged as true due to constructor setting values
        Assert.True(transform.HasChanged);
        // Reset HasChanged flag
       //// transform.ResetHasChangedFlag();
        Assert.False(transform.HasChanged);
        // Act & Assert
        // Change Position
        transform.Position = new Position(new Vector2(150, 250), AnchorPosition.TopLeft, ScaleType.Relative);
        Assert.True(transform.HasChanged);
       // transform.ResetHasChangedFlag();
        // Change Rotation
        transform.Rotation = 90.0;
        Assert.True(transform.HasChanged);
        //transform.ResetHasChangedFlag();
        // Change Size
        transform.Size = new Dimension(350, 450);
        Assert.True(transform.HasChanged);
        //transform.ResetHasChangedFlag();
        // Change SizeScaling
        transform.BoundingBox = BoundingBoxType.ScaleHeightToScreen;
        Assert.True(transform.HasChanged);
        //transform.ResetHasChangedFlag();
        // No change
        transform.Position = transform.Position;
        Assert.False(transform.HasChanged);
    }
}
