using Wolf3D.Assets;
using Wolf3D.Managers;

namespace Wolf3D.Fonts;

/// <summary>
/// A Wolf3D proportional font (VGAGRAPH's SmallFont/LargeFont): one-bit glyphs for the first
/// 256 characters, drawn in whatever color the text asks for.
/// </summary>
internal sealed class VgaFont : Font
{
    public VgaFont(FontAsset asset)
    {
        Asset = asset;
    }

    public FontAsset Asset { get; }

    public override int Height => Asset.Height;

    public override int Advance(char ch) => ch < Asset.Width.Length ? Asset.Width[ch] : Asset.Width[' '];

    public override void Draw(VideoManager video, int x, int y, string text, string color)
        => video.DrawPropString(x, y, text, color, Asset);
}
