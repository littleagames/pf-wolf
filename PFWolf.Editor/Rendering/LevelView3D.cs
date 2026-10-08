using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;

namespace PFWolf.Editor.Rendering;

// Inside the namespace, so it beats the game's PFWolf.Point
using Point = Avalonia.Point;

/// <summary>What the pointer is over in the 3D view, or nothing, and the tile an edit there goes to</summary>
public sealed class SurfaceHoverEventArgs(SurfaceHit? hit, (int X, int Y) target) : EventArgs
{
    public SurfaceHit? Hit { get; } = hit;
    public (int X, int Y) Target { get; } = target;
}

/// <summary>
/// The level in 3D through OpenGL, built by <see cref="LevelMesh"/> and rebuilt whenever the
/// level changes. Fly with WASD (Q and E down and up, Shift faster), look by dragging with the
/// right button, slide by dragging with the middle one, and move along the view with the wheel.
/// The left button works the map's tools on the tile under the pointer (see
/// <see cref="LevelMesh.TargetTile"/>): things go in front of a wall face, and so does
/// anything with Shift held.
/// </summary>
public sealed class LevelView3D : OpenGlControlBase, Avalonia.Rendering.ICustomHitTest
{
    // The OpenGL picture isn't something Avalonia hit-tests, so the view's whole area takes the pointer
    public bool HitTest(Point point) => IsVisible && new Rect(Bounds.Size).Contains(point);

    public static readonly StyledProperty<MapTiles?> TilesProperty = AvaloniaProperty.Register<LevelView3D, MapTiles?>(nameof(Tiles));
    public static readonly StyledProperty<ArtCache?> ArtProperty = AvaloniaProperty.Register<LevelView3D, ArtCache?>(nameof(Art));
    public static readonly StyledProperty<ToolController?> ControllerProperty = AvaloniaProperty.Register<LevelView3D, ToolController?>(nameof(Controller));
    public static readonly StyledProperty<Camera3D?> CameraProperty = AvaloniaProperty.Register<LevelView3D, Camera3D?>(nameof(Camera));
    public static readonly StyledProperty<bool> ShowThingsProperty = AvaloniaProperty.Register<LevelView3D, bool>(nameof(ShowThings), true);
    public static readonly StyledProperty<bool> ShowCeilingsProperty = AvaloniaProperty.Register<LevelView3D, bool>(nameof(ShowCeilings), true);

    static LevelView3D()
    {
        FocusableProperty.OverrideDefaultValue<LevelView3D>(true);
    }

    public MapTiles? Tiles { get => GetValue(TilesProperty); set => SetValue(TilesProperty, value); }
    public ArtCache? Art { get => GetValue(ArtProperty); set => SetValue(ArtProperty, value); }
    public ToolController? Controller { get => GetValue(ControllerProperty); set => SetValue(ControllerProperty, value); }
    public Camera3D? Camera { get => GetValue(CameraProperty); set => SetValue(CameraProperty, value); }
    public bool ShowThings { get => GetValue(ShowThingsProperty); set => SetValue(ShowThingsProperty, value); }
    public bool ShowCeilings { get => GetValue(ShowCeilingsProperty); set => SetValue(ShowCeilingsProperty, value); }

    /// <summary>The pointer moved onto another surface, or off them all</summary>
    public event EventHandler<SurfaceHoverEventArgs>? SurfaceHovered;

    /// <summary>OpenGL couldn't be set up: why, for the status line</summary>
    public event EventHandler<string>? Failed;

    /// <summary>A tool's key letter was pressed over the view (not one used for flying)</summary>
    public event EventHandler<string>? ToolKeyPressed;

    private const int FloatsPerVertex = 9;   // position 3, texture 2, color 4

    private Gl? _gl;
    private int _program, _viewProjection, _mode, _texture;
    private int _staticVao, _staticVbo, _dynamicVao, _dynamicVbo;
    private int _white, _missing;
    private readonly Dictionary<TextureRef, int> _glTextures = [];
    private bool _texturesStale;

    private LevelMesh? _mesh;
    private bool _meshDirty = true, _uploadPending = true;
    // The static mesh's draw calls: a texture and its run of vertices
    private readonly List<(TextureRef Texture, int First, int Count)> _batches = [];

    private SurfaceHit? _hover;
    private (int X, int Y) _target = (-1, -1);
    private Point? _pointer;
    private bool _shift;

    // Working a tool with the left button: picks go against the level as it was when the
    // button went down, so a wall painted under the pointer doesn't move the next tile along
    private bool _toolDown;
    private LevelMesh? _gestureMesh;

    // Flying: the keys held, and when the camera last moved
    private readonly HashSet<Key> _held = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastFrame;
    private Point? _lookStart, _panStart;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TilesProperty)
        {
            _meshDirty = true;
            SetHover(null, (-1, -1));
            Redraw();
        }
        else if (change.Property == ArtProperty)
        {
            _texturesStale = true;
            Redraw();
        }
        else if (change.Property == ControllerProperty)
        {
            if (change.OldValue is ToolController old)
                old.Changed -= OnLevelChanged;
            if (change.NewValue is ToolController controller)
                controller.Changed += OnLevelChanged;
        }
        else if (change.Property == CameraProperty)
        {
            if (change.OldValue is Camera3D old)
                old.Changed -= OnCameraChanged;
            if (change.NewValue is Camera3D camera)
                camera.Changed += OnCameraChanged;
            Redraw();
        }
        else if (change.Property == ShowThingsProperty || change.Property == ShowCeilingsProperty)
        {
            _uploadPending = true;
            Redraw();
        }
    }

    private void OnLevelChanged(object? sender, EventArgs e)
    {
        _meshDirty = true;
        Redraw();
    }

    private void OnCameraChanged(object? sender, EventArgs e) => Redraw();

    private void Redraw() => RequestNextFrameRendering();

    //
    // OpenGL
    //

    protected override void OnOpenGlInit(GlInterface glInterface)
    {
        base.OnOpenGlInit(glInterface);
        try
        {
            var gl = new Gl(glInterface);
            bool es = GlVersion.Type == GlProfileType.OpenGLES;
            string header = es ? "#version 300 es\nprecision mediump float;\n" : "#version 330 core\n";
            _program = gl.MakeProgram(header + VertexShader, header + FragmentShader, "aPosition", "aUv", "aColor");
            _viewProjection = gl.GetUniformLocation(_program, "uViewProjection");
            _mode = gl.GetUniformLocation(_program, "uMode");
            _texture = gl.GetUniformLocation(_program, "uTexture");

            (_staticVao, _staticVbo) = MakeVertexArray(gl);
            (_dynamicVao, _dynamicVbo) = MakeVertexArray(gl);
            _white = MakeTexture(gl, 1, 1, [255, 255, 255, 255]);
            _missing = MakeTexture(gl, 2, 2, [255, 0, 255, 255, 96, 96, 96, 255, 96, 96, 96, 255, 255, 0, 255, 255]);
            _gl = gl;
            _uploadPending = true;
        }
        catch (InvalidOperationException e)
        {
            _gl = null;
            Failed?.Invoke(this, e.Message);
        }
    }

    protected override void OnOpenGlDeinit(GlInterface glInterface)
    {
        if (_gl is { } gl)
        {
            DeleteTextures(gl);
            gl.DeleteTexture(_white);
            gl.DeleteTexture(_missing);
            gl.DeleteBuffer(_staticVbo);
            gl.DeleteBuffer(_dynamicVbo);
            gl.DeleteVertexArray(_staticVao);
            gl.DeleteVertexArray(_dynamicVao);
            gl.DeleteProgram(_program);
        }
        _gl = null;
        base.OnOpenGlDeinit(glInterface);
    }

    protected override void OnOpenGlLost()
    {
        _gl = null;
        _glTextures.Clear();
        base.OnOpenGlLost();
    }

    private static (int Vao, int Vbo) MakeVertexArray(Gl gl)
    {
        int vao = gl.GenVertexArray(), vbo = gl.GenBuffer();
        gl.BindVertexArray(vao);
        gl.BindBuffer(Gl.ARRAY_BUFFER, vbo);
        const int stride = FloatsPerVertex * sizeof(float);
        gl.VertexAttribPointer(0, 3, stride, 0);
        gl.VertexAttribPointer(1, 2, stride, 3 * sizeof(float));
        gl.VertexAttribPointer(2, 4, stride, 5 * sizeof(float));
        gl.EnableVertexAttribArray(0);
        gl.EnableVertexAttribArray(1);
        gl.EnableVertexAttribArray(2);
        gl.BindVertexArray(0);
        return (vao, vbo);
    }

    private static int MakeTexture(Gl gl, int width, int height, byte[] rgba)
    {
        int id = gl.GenTexture();
        gl.BindTexture(Gl.TEXTURE_2D, id);
        gl.TexImage2D(width, height, rgba);
        gl.TexParameter(Gl.TEXTURE_2D, Gl.TEXTURE_MIN_FILTER, Gl.NEAREST);
        gl.TexParameter(Gl.TEXTURE_2D, Gl.TEXTURE_MAG_FILTER, Gl.NEAREST);
        gl.TexParameter(Gl.TEXTURE_2D, Gl.TEXTURE_WRAP_S, Gl.REPEAT);
        gl.TexParameter(Gl.TEXTURE_2D, Gl.TEXTURE_WRAP_T, Gl.REPEAT);
        return id;
    }

    private void DeleteTextures(Gl gl)
    {
        foreach (var id in _glTextures.Values)
            gl.DeleteTexture(id);
        _glTextures.Clear();
    }

    private int TextureId(Gl gl, TextureRef texture)
    {
        if (texture.Source == TextureSource.Solid)
            return _white;
        if (_glTextures.TryGetValue(texture, out var id))
            return id;

        id = Art?.Pixels(texture) is var (width, height, rgba) ? MakeTexture(gl, width, height, rgba) : _missing;
        _glTextures[texture] = id;
        return id;
    }

    protected override void OnOpenGlRender(GlInterface glInterface, int framebuffer)
    {
        if (_gl is not { } gl)
            return;

        bool moving = Fly();
        if (_texturesStale)
        {
            DeleteTextures(gl);
            _texturesStale = false;
        }
        EnsureMesh();
        if (_uploadPending)
            Upload(gl);

        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        int width = Math.Max(1, (int)(Bounds.Width * scaling)), height = Math.Max(1, (int)(Bounds.Height * scaling));
        gl.Viewport(0, 0, width, height);
        gl.ClearColor(24 / 255f, 24 / 255f, 28 / 255f, 1);
        gl.Clear(Gl.COLOR_BUFFER_BIT | Gl.DEPTH_BUFFER_BIT);
        if (_mesh == null || Camera is not { } camera)
            return;

        gl.Enable(Gl.DEPTH_TEST);
        gl.DepthFunc(Gl.LEQUAL);
        gl.DepthMask(true);
        gl.Disable(Gl.BLEND);
        gl.UseProgram(_program);
        gl.UniformMatrix4(_viewProjection, camera.View * Camera3D.Projection((float)(Bounds.Width / Math.Max(1, Bounds.Height))));
        gl.Uniform1(_texture, 0);
        gl.Uniform1(_mode, 0);
        gl.ActiveTexture(Gl.TEXTURE0);

        // The level: one-sided faces, each texture's in one go
        gl.Enable(Gl.CULL_FACE);
        gl.CullFace(Gl.BACK);
        gl.BindVertexArray(_staticVao);
        foreach (var (texture, first, count) in _batches)
        {
            if (texture.Source == TextureSource.Sprite)
                gl.Disable(Gl.CULL_FACE);      // wall sprites show from behind too
            gl.BindTexture(Gl.TEXTURE_2D, TextureId(gl, texture));
            gl.DrawArrays(Gl.TRIANGLES, first, count);
            if (texture.Source == TextureSource.Sprite)
                gl.Enable(Gl.CULL_FACE);
        }

        // Things turned to the view, then a tint over what the pointer's on
        gl.Disable(Gl.CULL_FACE);
        var vertices = new List<float>();
        var spriteBatches = new List<(TextureRef Texture, int First, int Count)>();
        if (ShowThings)
        {
            foreach (var group in _mesh.Billboards.GroupBy(sprite => sprite.Texture))
            {
                int first = vertices.Count / FloatsPerVertex;
                foreach (var sprite in group)
                    AddQuad(vertices, BillboardCorners(sprite, camera), [new(0, 1), new(1, 1), new(1, 0), new(0, 0)], Vector4.One);
                spriteBatches.Add((group.Key, first, vertices.Count / FloatsPerVertex - first));
            }
        }
        int overlayFirst = vertices.Count / FloatsPerVertex;
        AddHoverOverlay(vertices, camera);
        int overlayCount = vertices.Count / FloatsPerVertex - overlayFirst;

        if (vertices.Count > 0)
        {
            gl.BindVertexArray(_dynamicVao);
            gl.BindBuffer(Gl.ARRAY_BUFFER, _dynamicVbo);
            gl.BufferData(Gl.ARRAY_BUFFER, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(vertices), Gl.DYNAMIC_DRAW);
            foreach (var (texture, first, count) in spriteBatches)
            {
                gl.BindTexture(Gl.TEXTURE_2D, TextureId(gl, texture));
                gl.DrawArrays(Gl.TRIANGLES, first, count);
            }

            if (overlayCount > 0)
            {
                gl.Enable(Gl.BLEND);
                gl.BlendFunc(Gl.SRC_ALPHA, Gl.ONE_MINUS_SRC_ALPHA);
                gl.Enable(Gl.POLYGON_OFFSET_FILL);
                gl.PolygonOffset(-1, -2);
                gl.DepthMask(false);
                gl.Uniform1(_mode, 1);
                gl.BindTexture(Gl.TEXTURE_2D, _white);
                gl.DrawArrays(Gl.TRIANGLES, overlayFirst, overlayCount);
                gl.Uniform1(_mode, 0);
                gl.DepthMask(true);
                gl.Disable(Gl.POLYGON_OFFSET_FILL);
                gl.Disable(Gl.BLEND);
            }
        }

        gl.BindVertexArray(0);
        gl.UseProgram(0);

        if (moving)
            RequestNextFrameRendering();
    }

    private void EnsureMesh()
    {
        if (!_meshDirty)
            return;
        _meshDirty = false;
        _mesh = Tiles is { } tiles ? LevelMesh.Build(tiles) : null;
        _uploadPending = true;

        // What's under the pointer may have changed
        if (_pointer is { } pointer)
            UpdateHover(pointer);
    }

    /// <summary>Puts the level's surfaces in the static buffer, grouped by texture</summary>
    private void Upload(Gl gl)
    {
        _uploadPending = false;
        _batches.Clear();
        var vertices = new List<float>();
        if (_mesh != null)
        {
            var shown = _mesh.Surfaces.Where(surface => (ShowCeilings || surface.Ref.Kind != SurfaceKind.Ceiling)
                                                       && (ShowThings || surface.Ref.Kind != SurfaceKind.Thing));
            foreach (var group in shown.GroupBy(surface => surface.Texture))
            {
                int first = vertices.Count / FloatsPerVertex;
                foreach (var surface in group)
                    AddPolygon(vertices, surface.Points, surface.Uvs, surface.Color);
                _batches.Add((group.Key, first, vertices.Count / FloatsPerVertex - first));
            }
        }

        gl.BindBuffer(Gl.ARRAY_BUFFER, _staticVbo);
        gl.BufferData(Gl.ARRAY_BUFFER, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(vertices), Gl.STATIC_DRAW);
    }

    /// <summary>A convex polygon as a fan of triangles</summary>
    private static void AddPolygon(List<float> vertices, Vector3[] points, Vector2[] uvs, Vector4 color)
    {
        for (int i = 1; i + 1 < points.Length; i++)
        {
            AddVertex(vertices, points[0], uvs[0], color);
            AddVertex(vertices, points[i], uvs[i], color);
            AddVertex(vertices, points[i + 1], uvs[i + 1], color);
        }
    }

    private static void AddQuad(List<float> vertices, Vector3[] corners, Vector2[] uvs, Vector4 color)
        => AddPolygon(vertices, corners, uvs, color);

    private static void AddVertex(List<float> vertices, Vector3 point, Vector2 uv, Vector4 color)
    {
        vertices.Add(point.X); vertices.Add(point.Y); vertices.Add(point.Z);
        vertices.Add(uv.X); vertices.Add(uv.Y);
        vertices.Add(color.X); vertices.Add(color.Y); vertices.Add(color.Z); vertices.Add(color.W);
    }

    /// <summary>A sprite's corners, square to the view (bottom left, bottom right, top right, top left)</summary>
    private static Vector3[] BillboardCorners(Billboard sprite, Camera3D camera)
    {
        var half = camera.Right * (sprite.Width / 2);
        var up = Vector3.UnitY * sprite.Height;
        return [sprite.Base - half, sprite.Base + half, sprite.Base + half + up, sprite.Base - half + up];
    }

    private static readonly Vector4 HoverColor = new(1f, 0.85f, 0.25f, 0.35f);
    private static readonly Vector4 TargetColor = new(0.3f, 0.95f, 1f, 0.45f);
    private static readonly Vector4 SelectionColor = new(0.31f, 0.78f, 1f, 0.3f);
    private static readonly Vector2[] SquareUvs = [new(0, 1), new(1, 1), new(1, 0), new(0, 0)];

    /// <summary>
    /// Tints over the selection's tiles, the surfaces the pointer's on (a wall face may be more
    /// than one), and the tile an edit there goes to when that's another one
    /// </summary>
    private void AddHoverOverlay(List<float> vertices, Camera3D camera)
    {
        if (Controller?.Selection is { } selection && Tiles != null)
        {
            for (int y = selection.Top; y <= selection.Bottom; y++)
                for (int x = selection.Left; x <= selection.Right; x++)
                    AddQuad(vertices, TileMarker(x, y, 0.03f), SquareUvs, SelectionColor);
        }

        if (_hover is not { } hover || _mesh == null)
            return;
        if (_target != (hover.Ref.X, hover.Ref.Y) && _target.X >= 0)
            AddQuad(vertices, TileMarker(_target.X, _target.Y, 0.02f), SquareUvs, TargetColor);

        if (hover.Ref.Kind == SurfaceKind.Thing)
        {
            foreach (var sprite in _mesh.Billboards.Where(sprite => sprite.Ref == hover.Ref))
                AddQuad(vertices, BillboardCorners(sprite, camera), [new(0, 1), new(1, 1), new(1, 0), new(0, 0)], HoverColor);
        }
        foreach (var surface in _mesh.Surfaces.Where(surface => surface.Ref == hover.Ref))
            AddPolygon(vertices, surface.Points, surface.Uvs, HoverColor);
    }

    /// <summary>A tile's square, facing up, on its floor or on top of its wall or door, just above it</summary>
    private Vector3[] TileMarker(int x, int y, float lift)
    {
        float height = lift;
        if (Tiles is { } tiles && tiles.IsSolid(x, y))
            height += tiles.Stories(x, y);
        return [new(x, height, y + 1), new(x + 1, height, y + 1), new(x + 1, height, y), new(x, height, y)];
    }

    //
    // Flying and looking
    //

    /// <summary>Moves the camera for the keys held since the last frame; true while it's still moving</summary>
    private bool Fly()
    {
        double now = _clock.Elapsed.TotalSeconds;
        float seconds = (float)Math.Min(now - _lastFrame, 0.1);
        _lastFrame = now;
        if (_held.Count == 0 || Camera is not { } camera)
            return false;

        bool fast = _held.Contains(Key.LeftShift) || _held.Contains(Key.RightShift);
        float speed = (fast ? 12f : 4f) * seconds, turn = 90f * seconds;
        float forward = 0, right = 0, up = 0, yaw = 0;
        if (_held.Contains(Key.W) || _held.Contains(Key.Up)) forward += speed;
        if (_held.Contains(Key.S) || _held.Contains(Key.Down)) forward -= speed;
        if (_held.Contains(Key.D)) right += speed;
        if (_held.Contains(Key.A)) right -= speed;
        if (_held.Contains(Key.E) || _held.Contains(Key.PageUp)) up += speed;
        if (_held.Contains(Key.Q) || _held.Contains(Key.PageDown)) up -= speed;
        if (_held.Contains(Key.Left)) yaw += turn;
        if (_held.Contains(Key.Right)) yaw -= turn;

        if (forward == 0 && right == 0 && up == 0 && yaw == 0)
            return false;
        camera.Yaw += yaw;
        camera.Move(forward, right, up);
        return true;
    }

    private static bool IsFlyKey(Key key) => key is Key.W or Key.A or Key.S or Key.D or Key.Q or Key.E
        or Key.Up or Key.Down or Key.Left or Key.Right or Key.PageUp or Key.PageDown or Key.LeftShift or Key.RightShift;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (e.Key is Key.LeftShift or Key.RightShift)
            SetShift(true);

        if (!ctrl && IsFlyKey(e.Key))
        {
            if (_held.Add(e.Key))
            {
                _lastFrame = _clock.Elapsed.TotalSeconds;
                Redraw();
            }
            e.Handled = true;
            return;
        }

        // The map's keys, on the tile under the pointer
        e.Handled = true;
        switch (e.Key)
        {
            case Key.Delete when _target.X >= 0:
                Controller?.EraseTile(_target.X, _target.Y);
                break;
            case Key.Delete:
                Controller?.Delete();
                break;
            case Key.Escape:
                Controller?.Escape();
                break;
            case Key.C when ctrl:
                Controller?.Copy();
                break;
            case Key.X when ctrl:
                Controller?.Cut();
                break;
            case Key.V when ctrl && _target.X >= 0:
                Controller?.StartPaste(_target.X, _target.Y);
                break;
            case >= Key.A and <= Key.Z when e.KeyModifiers == KeyModifiers.None:
                ToolKeyPressed?.Invoke(this, e.Key.ToString());
                break;
            default:
                e.Handled = false;
                break;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _held.Remove(e.Key);
        if (e.Key is Key.LeftShift or Key.RightShift)
            SetShift(false);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _held.Clear();
    }

    /// <summary>Shift moves the target in front of the wall face under the pointer</summary>
    private void SetShift(bool shift)
    {
        if (_shift == shift)
            return;
        _shift = shift;
        if (_pointer is { } pointer && !_toolDown)
            UpdateHover(pointer);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var point = e.GetCurrentPoint(this);
        _shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (point.Properties.IsRightButtonPressed)
            _lookStart = point.Position;
        else if (point.Properties.IsMiddleButtonPressed)
            _panStart = point.Position;
        else if (point.Properties.IsLeftButtonPressed && Controller is { } controller)
        {
            UpdateHover(point.Position);
            if (_target.X >= 0)
            {
                _toolDown = true;
                _gestureMesh = _mesh;
                controller.Press(_target.X, _target.Y);
            }
        }
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndGesture();
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndGesture();
    }

    private void EndGesture()
    {
        _lookStart = _panStart = null;
        if (_toolDown)
        {
            _toolDown = false;
            _gestureMesh = null;
            Controller?.Release();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);
        if (Camera is { } camera)
        {
            if (_lookStart is { } look)
            {
                var delta = position - look;
                camera.Yaw -= (float)delta.X * 0.3f;
                camera.Pitch -= (float)delta.Y * 0.3f;
                _lookStart = position;
            }
            else if (_panStart is { } pan)
            {
                var delta = position - pan;
                camera.Move(0, -(float)delta.X * 0.02f, (float)delta.Y * 0.02f);
                _panStart = position;
            }
        }

        _pointer = position;
        if (!_toolDown)
            _shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        UpdateHover(position);
        if (Controller is { } controller && _target.X >= 0 && (_toolDown || controller.IsPasting))
            controller.Move(_target.X, _target.Y);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointer = null;
        if (_lookStart == null && _panStart == null && !_toolDown)
            SetHover(null, (-1, -1));
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Camera is { } camera)
            camera.Position += camera.Forward * (float)e.Delta.Y * 0.75f;
        e.Handled = true;
    }

    /// <summary>Whether a surface is showing, and so can be picked</summary>
    private bool Shown(SurfaceRef surface)
        => (ShowThings || surface.Kind != SurfaceKind.Thing) && (ShowCeilings || surface.Kind != SurfaceKind.Ceiling);

    private void UpdateHover(Point position)
    {
        if (Camera is not { } camera || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;
        if (_meshDirty)
            EnsureMesh();
        var mesh = _gestureMesh ?? _mesh;
        if (mesh == null)
        {
            SetHover(null, (-1, -1));
            return;
        }

        float aspect = (float)(Bounds.Width / Bounds.Height);
        var (origin, direction) = camera.Ray((float)(position.X / Bounds.Width * 2 - 1), (float)(1 - position.Y / Bounds.Height * 2), aspect);
        var hit = mesh.Pick(origin, direction, camera, Shown);
        if (hit is not { } surface)
        {
            SetHover(null, (-1, -1));
            return;
        }

        // Things stand in front of a wall, not in it
        bool inFront = _shift || Controller?.Plane == 1;
        var target = LevelMesh.TargetTile(surface.Ref, inFront);
        if (Tiles is { } tiles && (target.X < 0 || target.Y < 0 || target.X >= tiles.Width || target.Y >= tiles.Height))
            target = (surface.Ref.X, surface.Ref.Y);
        SetHover(hit, target);
    }

    private void SetHover(SurfaceHit? hit, (int X, int Y) target)
    {
        bool same = _hover?.Ref == hit?.Ref && _target == target;
        _hover = hit;
        _target = target;
        if (same)
            return;
        Redraw();
        SurfaceHovered?.Invoke(this, new SurfaceHoverEventArgs(hit, target));
    }

    private const string VertexShader = """
        in vec3 aPosition;
        in vec2 aUv;
        in vec4 aColor;
        uniform mat4 uViewProjection;
        out vec2 vUv;
        out vec4 vColor;
        void main()
        {
            gl_Position = uViewProjection * vec4(aPosition, 1.0);
            vUv = aUv;
            vColor = aColor;
        }
        """;

    // Mode 0: the texture (see-through texels cut out) times the color; 1: just the color, blended
    private const string FragmentShader = """
        in vec2 vUv;
        in vec4 vColor;
        uniform sampler2D uTexture;
        uniform int uMode;
        out vec4 fragColor;
        void main()
        {
            if (uMode == 1)
            {
                fragColor = vColor;
                return;
            }
            vec4 texel = texture(uTexture, vUv);
            if (texel.a < 0.5)
                discard;
            fragColor = vec4(texel.rgb * vColor.rgb, 1.0);
        }
        """;
}
