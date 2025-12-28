using System;
using PFWolf.Common;

namespace PFWolf.Common;

public class TransformCalculator
{
    private readonly int _screenWidth;
    private readonly int _screenHeight;

    public TransformCalculator(int screenWidth, int screenHeight)
    {
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;
    }

    private static int GetNormalizedValue(Transform transform, Func<Transform, int> func, float scaleFactor)
    {
        int value = func(transform);
        if (transform.PositionType == PositionType.Relative)
        {
            return (int)(value * scaleFactor);
        }

        return value;
    }

    /// <summary>
    /// Takes an existing transform and updates values to accomodate the video layout.
    /// </summary>
    /// <param name="transform"></param>
    /// <returns></returns>
    public Transform CalculateTransform(Transform transform)
    {
        return transform;

        var scaleFactorX = _screenWidth / 320.0f;
        var scaleFactorY = _screenHeight / 200.0f;

        var scale = Math.Min(scaleFactorX, scaleFactorY); // For maintaining aspect ratio

        //switch (transform.BoundingBox)
        //{
        //    case BoundingBoxType.Scale:
        //        {
        //            var size = new Dimension(
        //                (int)(transform.Size.Width * scale),
        //                (int)(transform.Size.Height * scale));
        //            transform = transform.SetSize(size);
        //        }
        //        break;
        //    case BoundingBoxType.Stretch:
        //        {
        //            var size = new Dimension(
        //                (int)(transform.Size.Width * scaleFactorX),
        //                (int)(transform.Size.Height * scaleFactorY));
        //            transform.SetSize(size);
        //        }
        //        break;
        //}

        switch (transform.AnchorPoint)
        {
            case AnchorPoint.TopLeft:
                // No change needed
                break;
            case AnchorPoint.TopCenter:
                transform.SetOffset(new Point(
                    - (GetNormalizedValue(transform, x => x.CalculatedSize.Width, scaleFactorX) / 2),
                    0
                ));
                break;
            case AnchorPoint.TopRight:
                transform.SetOffset(new Point(
                    - (GetNormalizedValue(transform, x => x.CalculatedSize.Width, scaleFactorX)),
                    0
                ));
                break;
            case AnchorPoint.MiddleLeft:
                transform.SetOffset(new Point(
                    0,
                    -(GetNormalizedValue(transform, x => x.CalculatedSize.Height, scaleFactorY) / 2)
                ));
                break;
            case AnchorPoint.Center:
                transform.SetOffset(new Point(
                    -(GetNormalizedValue(transform, x => x.CalculatedSize.Width, scaleFactorX) / 2),
                    -(GetNormalizedValue(transform, x => x.CalculatedSize.Height, scaleFactorY) / 2)
                ));
                break;
            case AnchorPoint.MiddleRight:
                transform.SetOffset(new Point(
                    -(GetNormalizedValue(transform, x => x.CalculatedSize.Width, scaleFactorX)),
                    -(GetNormalizedValue(transform, x => x.CalculatedSize.Height, scaleFactorY) / 2)
                ));
                break;
            case AnchorPoint.BottomLeft:
                transform.SetOffset(new Point(
                    0,
                    -(GetNormalizedValue(transform, x => x.CalculatedSize.Height, scaleFactorY))
                ));
                break;
            case AnchorPoint.BottomCenter:
                transform.SetOffset(new Point(
                    -(GetNormalizedValue(transform, x => x.CalculatedSize.Width, scaleFactorX) / 2),
                    -(GetNormalizedValue(transform, x => x.CalculatedSize.Height, scaleFactorY))
                ));
                break;
            case AnchorPoint.BottomRight:
                transform.SetOffset(new Point(
                    -(GetNormalizedValue(transform, x => x.CalculatedSize.Width, scaleFactorX)),
                    -(GetNormalizedValue(transform, x => x.CalculatedSize.Height, scaleFactorY))
                ));
                break;
        }

        switch (transform.ScreenAnchorPoint)
        {
            case AnchorPoint.TopCenter:
                transform.SetPosition((_screenWidth / 2) + transform.Position.X, transform.Position.Y);
                break;
            case AnchorPoint.TopRight:
                transform.SetPosition(_screenWidth + transform.Position.X, transform.Position.Y);
                break;
            case AnchorPoint.MiddleLeft:
                transform.SetPosition(transform.Position.X, (_screenHeight / 2) + transform.Position.Y);
                break;
            case AnchorPoint.Center:
                transform.SetPosition((_screenWidth / 2) + transform.Position.X, (_screenHeight / 2) + transform.Position.Y);
                break;
            case AnchorPoint.MiddleRight:
                transform.SetPosition((_screenWidth) + transform.Position.X, (_screenHeight / 2) + transform.Position.Y);
                break;
            case AnchorPoint.BottomLeft:
                transform.SetPosition(transform.Position.X, (_screenHeight) + transform.Position.Y);
                break;
            case AnchorPoint.BottomCenter:
                transform.SetPosition((_screenWidth / 2) + transform.Position.X, (_screenHeight ) + transform.Position.Y);
                break;
            case AnchorPoint.BottomRight:
                transform.SetPosition((_screenWidth) + transform.Position.X, (_screenHeight) + transform.Position.Y);
                break;
        }


        //if (transform.BoundingBox == BoundingBoxType.ScaleToScreen)
        //{
        //    float scaleX = _screenWidth / (float)transform.Size.Width;
        //    float scaleY = _screenHeight / (float)transform.Size.Height;
        //    float localScale = Math.Min(scaleX, scaleY);
        //    int newW = Math.Max(1, (int)Math.Round(transform.Size.Width * localScale));
        //    int newH = Math.Max(1, (int)Math.Round(transform.Size.Height * localScale));
        //    transform.Update(new Dimension(newW, newH));
        //}

        //if (transform.BoundingBox == BoundingBoxType.StretchToScreen)
        //{
        //    transform.Update(new Dimension(_screenWidth, _screenHeight));
        //}

        //if (transform.BoundingBox == BoundingBoxType.ScaleWidthToScreen)
        //{
        //    transform.Update(new Dimension(_screenWidth, (int)(transform.Size.Height * scaleFactorY)));
        //}

        //if (transform.Size == Dimension.Zero)
        //{
        //    throw new Exception("Transform size is zero, cannot calculate bounding box size.");
        //    // invalid data? What do?
        //}

        //if (transform.BoundingBox == BoundingBoxType.NoBounds)
        //{
        //    // No changes needed
        //    return transform;
        //}

        //if (transform.BoundingBox == BoundingBoxType.Scale)
        //{
        //    var size = new Dimension(
        //        (int)(transform.Size.Width * scaleFactorX),
        //        (int)(transform.Size.Height * scaleFactorY));

        //    transform.Update(position, size);

        //}

        //if (transform.BoundingBox == BoundingBoxType.Scale)
        //{
        //    var size = new Dimension(
        //        (int)(transform.Size.Width * scale),
        //        (int)(transform.Size.Height * scale));

        //    transform.Update(position, size);
        //    // TODO: Position needs a "Scale" option too
        //    // Relative = 10, 10 is 10*scaleFactorX, 10*scaleFactorY
        //    // Absolute = 10, 10 is always 10, 10
        //    // TODO: Take size and scale by resolution factor

        //}

        return transform;
    }
}