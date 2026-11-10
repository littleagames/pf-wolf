using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PFWolf.Editor.Rendering;

using Point = Avalonia.Point;

/// <summary>
/// An OPL operator's envelope, roughly: rising at the attack rate, falling at the decay rate to
/// the sustain level, holding there while the key's on (if it sustains; else falling on at the
/// release rate), then falling at the release rate once it's let go. Rates are 0 (never) to 15
/// (at once); the sustain level 0 (loudest) to 15.
/// </summary>
public sealed class EnvelopeView : Control
{
    public static readonly StyledProperty<int> AttackProperty = AvaloniaProperty.Register<EnvelopeView, int>(nameof(Attack));
    public static readonly StyledProperty<int> DecayProperty = AvaloniaProperty.Register<EnvelopeView, int>(nameof(Decay));
    public static readonly StyledProperty<int> SustainProperty = AvaloniaProperty.Register<EnvelopeView, int>(nameof(Sustain));
    public static readonly StyledProperty<int> ReleaseProperty = AvaloniaProperty.Register<EnvelopeView, int>(nameof(Release));
    public static readonly StyledProperty<bool> SustainingProperty = AvaloniaProperty.Register<EnvelopeView, bool>(nameof(Sustaining));

    static EnvelopeView() => AffectsRender<EnvelopeView>(AttackProperty, DecayProperty, SustainProperty, ReleaseProperty, SustainingProperty);

    public int Attack { get => GetValue(AttackProperty); set => SetValue(AttackProperty, value); }
    public int Decay { get => GetValue(DecayProperty); set => SetValue(DecayProperty, value); }
    public int Sustain { get => GetValue(SustainProperty); set => SetValue(SustainProperty, value); }
    public int Release { get => GetValue(ReleaseProperty); set => SetValue(ReleaseProperty, value); }
    public bool Sustaining { get => GetValue(SustainingProperty); set => SetValue(SustainingProperty, value); }

    private static readonly IBrush Back = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x22));
    private static readonly IPen Line = new Pen(new SolidColorBrush(Color.FromRgb(0x60, 0xc0, 0xff)), 2);
    private static readonly IPen KeyOff = new Pen(new SolidColorBrush(Color.FromArgb(0x60, 0xff, 0xff, 0xff)), 1, new DashStyle([3, 3], 0));

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        context.FillRectangle(Back, new Rect(size));
        double width = size.Width - 8, height = size.Height - 8, left = 4, top = 4;

        // A segment's width: slow rates long, 0 never ends (drawn as the most there is)
        static double Span(int rate) => rate <= 0 ? 4 : (16 - rate) / 15.0;
        double attack = Span(Attack) * 0.6, decay = Span(Decay), hold = 1.0, release = Span(Release);
        var total = attack + decay + hold + release;
        double X(double at) => left + at / total * width;
        double Y(double level) => top + (1 - level) * height;

        var sustain = 1 - Sustain / 15.0;
        var keyOffAt = attack + decay + hold;
        var points = new List<Point> { new(X(0), Y(0)), new(X(attack), Y(1)), new(X(attack + decay), Y(sustain)) };
        // Not sustaining: it goes on falling at the release rate while the key's still on
        var atKeyOff = Sustaining ? sustain : Math.Max(0, sustain - hold / Math.Max(0.05, release) * sustain);
        points.Add(new Point(X(keyOffAt), Y(atKeyOff)));
        points.Add(new Point(X(total), Y(0)));
        context.DrawGeometry(null, Line, new PolylineGeometry(points, false));
        context.DrawLine(KeyOff, new Point(X(keyOffAt), top), new Point(X(keyOffAt), top + height));
    }
}
