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
        if (transform.Position.ScaleType == ScaleType.Relative)
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
        var scaleFactorX = _screenWidth / 320.0f;
        var scaleFactorY = _screenHeight / 200.0f;

        var scale = Math.Min(scaleFactorX, scaleFactorY); // For maintaining aspect ratio

        if (transform.BoundingBox == BoundingBoxType.Scale)
        {
            var size = new Dimension(
                (int)(transform.Size.Width * scale),
                (int)(transform.Size.Height * scale));
            transform.Update(size);
        }

        var position = transform.Position;

        switch (transform.Position.Alignment)
        {
            case AnchorPosition.TopLeft:
                // No change needed
                break;
            case AnchorPosition.TopCenter:
                position.SetOffset(new Vector2(
                    - (GetNormalizedValue(transform, x => x.Size.Width, scaleFactorX) / 2),
                    0
                ));
                break;
            case AnchorPosition.TopRight:
                position.SetOffset(new Vector2(
                    - (GetNormalizedValue(transform, x => x.Size.Width, scaleFactorX)),
                    0
                ));
                break;
            case AnchorPosition.MiddleLeft:
                position.SetOffset(new Vector2(
                    0,
                    -(GetNormalizedValue(transform, x => x.Size.Height, scaleFactorY) / 2)
                ));
                break;
            case AnchorPosition.Center:
                position.SetOffset(new Vector2(
                    -(GetNormalizedValue(transform, x => x.Size.Width, scaleFactorX) / 2),
                    -(GetNormalizedValue(transform, x => x.Size.Height, scaleFactorY) / 2)
                ));
                break;
            case AnchorPosition.MiddleRight:
                position.SetOffset(new Vector2(
                    -(GetNormalizedValue(transform, x => x.Size.Width, scaleFactorX)),
                    -(GetNormalizedValue(transform, x => x.Size.Height, scaleFactorY) / 2)
                ));
                break;
            case AnchorPosition.BottomLeft:
                position.SetOffset(new Vector2(
                    0,
                    -(GetNormalizedValue(transform, x => x.Size.Height, scaleFactorY))
                ));
                break;
            case AnchorPosition.BottomCenter:
                position.SetOffset(new Vector2(
                    -(GetNormalizedValue(transform, x => x.Size.Width, scaleFactorX) / 2),
                    -(GetNormalizedValue(transform, x => x.Size.Height, scaleFactorY))
                ));
                break;
            case AnchorPosition.BottomRight:
                position.SetOffset(new Vector2(
                    -(GetNormalizedValue(transform, x => x.Size.Width, scaleFactorX)),
                    -(GetNormalizedValue(transform, x => x.Size.Height, scaleFactorY))
                ));
                break;
        }

        switch (transform.BoundingBoxAlignment)
        {
            case AnchorPosition.TopCenter:
                position.SetOrigin(new Vector2((_screenWidth / 2) + position.Origin.X, position.Origin.Y));
                break;
            case AnchorPosition.TopRight:
                position.SetOrigin(new Vector2(_screenWidth + position.Origin.X, position.Origin.Y));
                break;
            case AnchorPosition.MiddleLeft:
                position.SetOrigin(new Vector2(position.Origin.X, (_screenHeight / 2) + position.Origin.Y));
                break;
            case AnchorPosition.Center:
                position.SetOrigin(new Vector2((_screenWidth / 2) - position.Origin.X, (_screenHeight / 2) + position.Origin.Y));
                break;
            case AnchorPosition.MiddleRight:
                position.SetOrigin(new Vector2((_screenWidth) - position.Origin.X, (_screenHeight / 2) + position.Origin.Y));
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