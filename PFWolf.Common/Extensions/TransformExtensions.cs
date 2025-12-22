namespace PFWolf.Common.Extensions;

public static class TransformExtensions
{
    public static AnchorPoint ToAnchorPoint(this VerticalAnchorPoint verticalAnchorPoint)
    {
        return verticalAnchorPoint switch
        {
            VerticalAnchorPoint.Top => AnchorPoint.TopLeft,
            VerticalAnchorPoint.Middle => AnchorPoint.MiddleLeft,
            VerticalAnchorPoint.Bottom => AnchorPoint.BottomLeft,
            _ => throw new ArgumentOutOfRangeException(nameof(verticalAnchorPoint), verticalAnchorPoint, null)
        };
    }

    public static AnchorPoint ToAnchorPoint(this HorizontalAnchorPoint verticalAnchorPoint)
    {
        return verticalAnchorPoint switch
        {
            HorizontalAnchorPoint.Left => AnchorPoint.TopLeft,
            HorizontalAnchorPoint.Center => AnchorPoint.TopCenter,
            HorizontalAnchorPoint.Right => AnchorPoint.TopRight,
            _ => throw new ArgumentOutOfRangeException(nameof(verticalAnchorPoint), verticalAnchorPoint, null)
        };
    }
    //extension<AnchorPoint>(VerticalAnchorPoint verticalAnchorPoint)
    //{
    //    public static AnchorPoint ToAnchorPoint()
    //    {
    //        return verticalAnchorPoint switch
    //        {
    //            VerticalAnchorPoint.Top => AnchorPoint.TopLeft,
    //            VerticalAnchorPoint.Middle => AnchorPoint.MiddleLeft,
    //            VerticalAnchorPoint.Bottom => AnchorPoint.BottomLeft,
    //            _ => throw new ArgumentOutOfRangeException(nameof(verticalAnchorPoint), verticalAnchorPoint, null)
    //        };
    //    }
    //}
}
