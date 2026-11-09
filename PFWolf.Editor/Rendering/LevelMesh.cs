using System.Numerics;
using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;
using PFWolf.Enums;

namespace PFWolf.Editor.Rendering;

/// <summary>Where a surface's picture comes from</summary>
public enum TextureSource
{
    /// <summary>A wall texture (or flat) by name</summary>
    Texture,
    /// <summary>A thing class's spawn sprite, by class name</summary>
    Sprite,
    /// <summary>No picture: the surface's color</summary>
    Solid,
    /// <summary>A picture (GraphicAsset, else a wall texture) by name: the sky</summary>
    Picture,
    /// <summary>A floor or ceiling texture by name, its see-through (0xFF) pixels in the Fill color</summary>
    Flat,
    /// <summary>A sky picture by name, seen through a ceiling: drawn by the way through each pixel, as the level's sky is</summary>
    Sky,
    /// <summary>A floor sky picture by name, seen through a floor, below the horizon</summary>
    FloorSky,
}

/// <param name="Fill">A Flat's color where it's see-through; with no alpha (default), they're clear (the sky shows)</param>
public readonly record struct TextureRef(TextureSource Source, string Name, Vector4 Fill = default)
{
    public static readonly TextureRef Solid = new(TextureSource.Solid, "");
}

/// <summary>What a surface in the 3D view belongs to</summary>
public enum SurfaceKind
{
    Wall,
    Door,
    Floor,
    Ceiling,
    Thing,
    PlayerStart,
    /// <summary>A block on open floor (an arch over it, a raised floor): its faces, underside and top</summary>
    Arch,
}

/// <summary>Which side of its tile a surface is on</summary>
public enum Face
{
    None,
    North,
    East,
    South,
    West,
    Diagonal,
    Top,
    /// <summary>An arch's underside</summary>
    Bottom,
}

/// <summary>A surface's tile and what it is there, for picking</summary>
public readonly record struct SurfaceRef(SurfaceKind Kind, int X, int Y, Face Face = Face.None);

/// <summary>
/// A flat polygon (3 or 4 points), wound anticlockwise seen from its front; only its front
/// shows, unless it's two-sided
/// </summary>
public sealed class Surface
{
    public required Vector3[] Points { get; init; }
    public required Vector2[] Uvs { get; init; }
    public TextureRef Texture { get; init; } = TextureRef.Solid;
    /// <summary>Multiplies the texture: a shade, or a solid surface's color</summary>
    public Vector4 Color { get; init; } = Vector4.One;
    public SurfaceRef Ref { get; init; }
    public bool TwoSided { get; init; }

    /// <summary>How far its texture moves a tic, in texture widths and heights: a scroller's (Rendering/Scrollers.cs)</summary>
    public Vector2 Scroll { get; init; }

    public Vector3 Normal => Vector3.Normalize(Vector3.Cross(Points[1] - Points[0], Points[2] - Points[0]));
}

/// <summary>A sprite standing on the floor that always turns to face the view, as the game's things do</summary>
public sealed class Billboard
{
    /// <summary>The thing's spot, at the height of the sprite's bottom edge</summary>
    public Vector3 Base { get; init; }

    /// <summary>How far right of the spot (as seen) the middle of the sprite is, in tiles: its offset's</summary>
    public float Shift { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }
    public TextureRef Texture { get; init; }
    public SurfaceRef Ref { get; init; }
    /// <summary>The light of the tile it stands on</summary>
    public Vector4 Color { get; init; } = Vector4.One;
}

/// <summary>The level's distance fade, for the view to apply: fade start and end in tiles, max fade 0 to 1</summary>
public sealed record LevelFog(Vector3 Color, float Start, float End, float MaxFade, bool Inverse);

/// <summary>Tiles the 3D view tints to stand out</summary>
public enum MarkerKind
{
    PushWall,
    Switch,
    Breakable,
}

/// <summary>What the pointer is on in the 3D view</summary>
public readonly record struct SurfaceHit(SurfaceRef Ref, Vector3 Point, float Distance, TextureRef Texture);

/// <summary>
/// A level as 3D surfaces, built the way the game draws it (Rendering/RayTrace.cs and WallPosts.cs): walls show their
/// mapdefs texture for each face (a face left out, the opposite one's) with any patch over it,
/// the faces beside a door show the door's side, tall walls repeat their texture story by story
/// from the floor, and things stand as sprites turned to the view.
/// </summary>
public sealed class LevelMesh
{
    private readonly MapTiles _tiles;
    private readonly bool _shade;
    private readonly Dictionary<string, float> _textureStories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, ZoneProperties> _zones;
    private readonly int _levelLight;
    private readonly Dictionary<int, ScrollerProperties> _scrollers;

    public List<Surface> Surfaces { get; } = [];
    public List<Billboard> Billboards { get; } = [];

    /// <summary>The sky picture shown in place of the ceiling color, or null</summary>
    public string? Sky { get; }

    /// <summary>How the level fades with distance, or null for no fade</summary>
    public LevelFog? Fog { get; }

    /// <summary>Whether some surface scrolls, so the view keeps drawing</summary>
    public bool Scrolls { get; private set; }

    /// <summary>Push walls, switches and breakable walls, for the view to tint</summary>
    public Dictionary<(int X, int Y), MarkerKind> Markers { get; } = [];

    private ILookup<(int X, int Y), Surface>? _byTile;

    /// <summary>The surfaces belonging to a tile</summary>
    public IEnumerable<Surface> SurfacesAt(int x, int y)
        => (_byTile ??= Surfaces.ToLookup(surface => (surface.Ref.X, surface.Ref.Y)))[(x, y)];

    // How much a wall's top is darkened, so the tops read apart from the faces
    private static readonly Vector4 TopShade = new(0.55f, 0.55f, 0.55f, 1);
    private static readonly Vector4 StartColor = new(0.25f, 0.86f, 0.38f, 1);
    private static readonly Vector4 UnknownColor = new(0.86f, 0.16f, 0.16f, 1);
    private static readonly Vector4 BrokenColor = new(1f, 0.55f, 0f, 1);

    private LevelMesh(MapTiles tiles, bool shade)
    {
        _tiles = tiles;
        _shade = shade;
        _zones = shade ? tiles.Zones : [];
        var shading = shade ? tiles.Shading : null;
        _levelLight = shading?.Light ?? 255;
        _scrollers = tiles.Scrollers;
        if (shading is { MaxFade: > 0 })
        {
            Fog = new LevelFog(ParseColor(shading.FadeColor) is { } fade ? new Vector3(fade.X, fade.Y, fade.Z) : Vector3.Zero,
                (float)Math.Max(shading.FadeStart, 0), (float)Math.Max(shading.FadeEnd, shading.FadeStart + 1 / 64.0),
                Math.Clamp(shading.MaxFade, 0, 100) / 100f, shading.InverseFade);
        }

        // Only a sky that's there to show
        Sky = Shown(tiles.Sky);
        (var floorSky, FloorSkyMirror) = tiles.FloorSky;
        FloorSky = Shown(floorSky);
    }

    /// <summary>The level's floor sky, shown where a floor has no flat (and through its see-through pixels), or null</summary>
    public string? FloorSky { get; }

    /// <summary>Floor skies are drawn upside down from the horizon (game-info floor-sky-mirror)</summary>
    public bool FloorSkyMirror { get; }

    // A sky picture (or wall texture) by name, when there is one to show
    private string? Shown(string? sky)
        => sky != null && (_tiles.Content.Find<GraphicAsset>(sky) != null || _tiles.Content.Find<TextureAsset>(sky) != null) ? sky : null;

    // How far behind a flat a sky seen through its see-through pixels lies
    private const float SkyGap = 1 / 128f;

    /// <param name="shade">Light the surfaces by the level's light and light zones, and fade them with distance</param>
    public static LevelMesh Build(MapTiles tiles, bool shade = true)
    {
        var mesh = new LevelMesh(tiles, shade);
        mesh.Build();
        return mesh;
    }

    /// <summary>
    /// The light a tile's surfaces get: its zone's (tinted), else the level's, as a color to
    /// multiply by (GameHost's light rows darken toward black, then the tint multiplies)
    /// </summary>
    private Vector4 Light(int x, int y)
    {
        if (!_shade || !InMap(x, y))
            return Vector4.One;
        var (light, tint) = _tiles.LightAt(x, y, _zones, _levelLight);
        var color = ParseColor(tint) ?? Vector4.One;
        float level = light / 255f;
        return new Vector4(color.X * level, color.Y * level, color.Z * level, 1);
    }

    /// <summary>The light a face gets: the open tile it's seen from, across its edge</summary>
    private Vector4 FaceLight(int x, int y, Face face)
    {
        var (nx, ny) = Step(x, y, face);
        return Light(nx, ny);
    }

    private void Build()
    {
        _floorColor = ParseColor(_tiles.FloorColor) ?? new Vector4(0.44f, 0.44f, 0.44f, 1);
        var floorColor = _floorColor;
        var ceilingColor = ParseColor(_tiles.CeilingColor) ?? new Vector4(0.22f, 0.22f, 0.22f, 1);
        _floor = _tiles.PlayerStory - 1;
        // The level's ceiling, opaque over the walls' height, unless that's at or under the player's floor
        float? ceiling = _tiles.LevelStories > _floor ? _tiles.LevelStories : null;

        for (int y = 0; y < _tiles.Height; y++)
        {
            for (int x = 0; x < _tiles.Width; x++)
            {
                switch (_tiles.KindAt(x, y))
                {
                    case TileKind.Wall:
                        AddWallTile(x, y, floorColor, ceilingColor, ceiling);
                        break;

                    case TileKind.Door:
                        bool lintel = AddDoor(x, y, ceilingColor);
                        // A lintel at least as high as the ceiling hides it, as a block does; the
                        // door's floor is the player's, on its frame
                        AddFloorAndCeiling(x, y, floorColor, ceilingColor, !lintel || _tiles.Stories(x, y) < ceiling ? ceiling : null, null, _floor);
                        break;

                    case TileKind.Unknown:
                        // A value the mapdefs don't know: marked on the floor
                        AddFlat(new SurfaceRef(SurfaceKind.Floor, x, y), Corners(x, y), _floor, up: true, TextureRef.Solid, UnknownColor);
                        break;

                    default:
                        var blocks = _tiles.Blocks(x, y);
                        AddFloorBlocks(x, y, blocks, ceilingColor);
                        AddFloorAndCeiling(x, y, floorColor, ceilingColor, CeilingOver(blocks, ceiling), null, Ground(blocks));
                        AddThing(x, y);
                        break;
                }
            }
        }

        foreach (var thing in _tiles.Map.ExtraThings)
            AddExtraThing(thing);
    }

    /// <summary>
    /// A UWMF level's extra thing (MapAsset.ExtraThings): its sprite where it stands, off the
    /// tile grid as the game places it; a wall sprite stays on its tile's panel
    /// </summary>
    private void AddExtraThing(MapThing thing)
    {
        int x = thing.TileX, y = thing.TileY;
        if (x < 0 || y < 0 || x >= _tiles.Width || y >= _tiles.Height)
            return;

        var content = _tiles.Content;
        if (content.BrokenThingReason(thing.Class) != null)
        {
            AddMarker(x, y, BrokenColor);
            return;
        }
        var sprite = content.ThingSprite(thing.Class);
        if (sprite is not { Width: > 0, Height: > 0 })
            return;

        float width = sprite.Width / (float)TextureAsset.StorySize, height = sprite.Height / (float)TextureAsset.StorySize;
        var texture = new TextureRef(TextureSource.Sprite, thing.Class);
        var thingRef = new SurfaceRef(SurfaceKind.Thing, x, y);
        var (originX, originY) = sprite.Origin;
        float lift = (originY - sprite.Height) / (float)TextureAsset.StorySize + _tiles.ThingStory(x, y, thing.Story) - 1;   // on its story's floor
        float shift = (sprite.Width / 2f - originX) / TextureAsset.StorySize;

        if (content.IsWallSprite(thing.Class))
            AddWallSprite(x, y, thing.Angle, content.WallSpriteOffset(thing.Class), width, height, lift, texture, thingRef);
        else
        {
            Billboards.Add(new Billboard
            {
                Base = new Vector3((float)thing.X, lift, (float)thing.Y), Shift = shift, Width = width, Height = height, Texture = texture, Ref = thingRef,
                Color = Light(x, y),
            });
        }
    }

    //
    // Walls
    //

    // The player's floor, in stories up from the ground: doors and things stand on it
    private int _floor;
    private Vector4 _floorColor;

    /// <summary>The ground under a tile's blocks (0), or null when one stands on it</summary>
    private static float? Ground(List<(int Bottom, int Top)> blocks) => blocks.Any(block => block.Bottom == 0) ? null : 0;

    /// <summary>The level's ceiling over a tile, or null where it has none or a block reaches it</summary>
    private static float? CeilingOver(List<(int Bottom, int Top)> blocks, float? ceiling)
        => ceiling is { } height && blocks.Any(block => block.Bottom < height && height <= block.Top) ? null : ceiling;

    /// <summary>
    /// What hides a face toward the tile at (nx, ny), seen across its <paramref name="side"/>: its
    /// blocks (a wall's or a floor tile's; a diagonal only along its solid edges); off the map, everything
    /// </summary>
    private List<(int Bottom, int Top)> CoverAt(int nx, int ny, Face side)
    {
        if (!InMap(nx, ny))
            return [(0, MapTiles.MaxStories)];
        return _tiles.KindAt(nx, ny) switch
        {
            TileKind.Wall when !CoversEdge(nx, ny, side) => [],
            TileKind.Wall or TileKind.Floor => _tiles.Blocks(nx, ny),
            _ => [],
        };
    }

    /// <summary>The parts of bottom to top that nothing in <paramref name="cover"/> hides</summary>
    private static List<(int Bottom, int Top)> Uncovered(int bottom, int top, List<(int Bottom, int Top)> cover)
    {
        var parts = new List<(int Bottom, int Top)> { (bottom, top) };
        foreach (var (b, t) in cover)
        {
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var (pb, pt) = parts[i];
                if (t <= pb || b >= pt)
                    continue;
                parts.RemoveAt(i);
                if (pb < b)
                    parts.Add((pb, b));
                if (t < pt)
                    parts.Add((t, pt));
            }
        }
        parts.Sort();
        return parts;
    }

    /// <summary>
    /// A block's top: where it's at or under the player's floor, the tile's floor (its flat, else
    /// the floor color), as the game draws it; over that, the block's own texture darkened, only
    /// seen from above in the editor
    /// </summary>
    private void AddBlockTop(SurfaceRef topRef, int x, int y, Vector2[] polygon, int top, string? texture, Vector4 light)
    {
        if (top <= _floor)
            AddFloor(x, y, polygon, top, _floorColor, topRef);
        else if (texture != null)
            AddFlat(topRef, polygon, top, up: true, Texture(texture), TopShade * light, TextureStories(texture));
    }

    private void AddWallTile(int x, int y, Vector4 floorColor, Vector4 ceilingColor, float? ceiling)
    {
        var wall = _tiles.Wall(x, y)!;
        var blocks = _tiles.Blocks(x, y);
        int stories = blocks[^1].Top;
        var diagonal = _tiles.Diagonal(x, y);
        var patch = _tiles.PatchDef(x, y);
        if (_tiles.IsPushWall(x, y))
            Markers[(x, y)] = MarkerKind.PushWall;
        else if (_tiles.IsSwitch(x, y))
            Markers[(x, y)] = MarkerKind.Switch;
        else if (_tiles.IsBreakable(x, y))
            Markers[(x, y)] = MarkerKind.Breakable;

        foreach (var face in SquareFaces)
        {
            // A diagonal keeps only its solid corner's two edges as square faces
            if (diagonal != null && !SolidEdges(diagonal.Shape).Contains(face))
                continue;

            var (nx, ny) = Step(x, y, face);
            if (!InMap(nx, ny))
                continue;

            // Each block's face, but where a wall or block beside it hides it
            var cover = CoverAt(nx, ny, Opposite(face));
            var (a, b) = Edge(x, y, face);
            var texture = wall.WallFace(FaceName(face));
            var faceRef = new SurfaceRef(SurfaceKind.Wall, x, y, face);
            var light = Light(nx, ny);
            bool flip = wall.IsFlipped(FaceName(face));
            float flipU0 = flip ? 1 : 0, flipU1 = flip ? 0 : 1;   // a flipped face's texture runs right to left
            var outward = new Vector2(nx - x, ny - y);

            foreach (var (blockBottom, blockTop) in blocks)
            {
                foreach (var (bottom, top) in Uncovered(blockBottom, blockTop, cover))
                {
                    if (_tiles.KindAt(nx, ny) == TileKind.Door && bottom <= _floor && _floor < top)
                    {
                        // The door's frame: its side texture for the door's story (the player's),
                        // the wall below and above (the face's direction is the way a ray travels
                        // from the door into the wall)
                        var door = _tiles.Door(nx, ny)!;
                        var jamb = DoorFace(door, nx, ny, TravelName(Opposite(face)));
                        if (_tiles.Content.Find<TextureAsset>(jamb) is { Masked: true } && texture.Length > 0)
                        {
                            // a see-through jamb is over the wall's own face, as a patch is (WallHits.SeeThroughJamb)
                            AddWall(faceRef, a, b, _floor, _floor + 1, texture, light, flipU0, flipU1);
                            AddPatch(faceRef, a, b, outward, _floor, _floor + 1, new MapPatchTranslation { Texture = jamb, Story = _floor + 1 }, light);
                        }
                        else
                            AddWall(faceRef, a, b, _floor, _floor + 1, jamb, light);
                        if (bottom < _floor)
                            AddWall(faceRef, a, b, bottom, _floor, texture, light, flipU0, flipU1);
                        if (_floor + 1 < top)
                            AddWall(faceRef, a, b, _floor + 1, top, texture, light, flipU0, flipU1);
                    }
                    else
                    {
                        AddWall(faceRef, a, b, bottom, top, texture, light, flipU0, flipU1, scroll: WallScroll(x, y, flipU1 - flipU0, texture));
                        if (patch != null && patch.IsOn(FaceName(face)))
                            AddPatch(faceRef, a, b, outward, bottom, top, patch, light, FaceName(face));
                    }
                }
            }
        }

        var topRef = new SurfaceRef(SurfaceKind.Wall, x, y, Face.Top);
        var bottomRef = new SurfaceRef(SurfaceKind.Wall, x, y, Face.Bottom);
        var tileLight = Light(x, y);
        var solidPart = Corners(x, y);
        if (diagonal != null)
        {
            // The slanted face of each block, toward the open half, which has the ground and the ceiling
            var (solid, open) = DiagonalHalves(x, y, diagonal.Shape);
            var (p, q) = Slant(x, y, diagonal.Shape);
            var (a, b) = Ordered(p, q, OpenDirection(diagonal.Shape));
            var texture = string.IsNullOrEmpty(diagonal.Texture) ? wall.North : diagonal.Texture;
            foreach (var (bottom, top) in blocks)
                AddWall(new SurfaceRef(SurfaceKind.Wall, x, y, Face.Diagonal), a, b, bottom, top, texture, tileLight, scroll: WallScroll(x, y, 1, texture));
            AddFloorAndCeiling(x, y, floorColor, ceilingColor, ceiling, open, 0);
            solidPart = solid;
        }

        // Each block's top and, off the ground, its underside; the ground and ceiling round them
        foreach (var (bottom, top) in blocks)
        {
            AddBlockTop(topRef, x, y, solidPart, top, wall.North, tileLight);
            if (bottom > 0)
                AddUnderside(bottomRef, x, y, solidPart, bottom, wall.North, ceilingColor, tileLight);
        }
        AddFloorAndCeiling(x, y, floorColor, ceilingColor, CeilingOver(blocks, ceiling), solidPart, Ground(blocks));
    }

    /// <summary>A block's underside: the tile's ceiling flat if it has one, else the block's texture (WallPosts.UndersideOf)</summary>
    private void AddUnderside(SurfaceRef bottomRef, int x, int y, Vector2[] polygon, int height, string? texture, Vector4 ceilingColor, Vector4 light)
    {
        var flat = _tiles.ArchCeilingFlat(x, y);
        if (flat != null)
            AddFlat(bottomRef, polygon, height, up: false, Flat(flat, ceilingColor), light);
        else if (texture != null)
            AddFlat(bottomRef, polygon, height, up: false, Texture(texture), light, TextureStories(texture));
    }

    /// <summary>
    /// A wall face from a to b (left to right seen from its front) between two heights; seeThrough:
    /// its texture's see-through pixels are clear (a see-through door's, Rendering/SeeThroughPosts.cs)
    /// </summary>
    private void AddWall(SurfaceRef faceRef, Vector2 a, Vector2 b, float bottom, float top, string texture, Vector4 light,
        float u0 = 0, float u1 = 1, bool twoSided = false, Vector2 scroll = default, bool seeThrough = false)
    {
        // The texture's stories repeat up the wall from the floor
        float stories = TextureStories(texture);
        float v0 = 1 - bottom / stories, v1 = 1 - top / stories;
        Surfaces.Add(new Surface
        {
            Points = [new(a.X, bottom, a.Y), new(b.X, bottom, b.Y), new(b.X, top, b.Y), new(a.X, top, a.Y)],
            Uvs = [new(u0, v0), new(u1, v0), new(u1, v1), new(u0, v1)],
            Texture = seeThrough ? Flat(texture, default) : Texture(texture),
            Color = light,
            Ref = faceRef,
            TwoSided = twoSided,
            Scroll = scroll,
        });
        Scrolls |= scroll != Vector2.Zero;
    }

    // How far in front of its wall face a patch is drawn, in tiles, so the two don't fight
    private const float PatchLift = 1 / 512f;

    /// <summary>
    /// A wall patch over the face from a to b (WallPosts.DrawPatch): from the floor of its story for
    /// as many stories as the picture is tall, or on every story (story 0) repeating as the wall's
    /// texture does; the part below <paramref name="bottom"/> is behind the wall in front. Its
    /// see-through pixels are clear, so the wall shows. Lifted off the face toward <paramref name="outward"/>.
    /// </summary>
    private void AddPatch(SurfaceRef faceRef, Vector2 a, Vector2 b, Vector2 outward, float bottom, int stories,
        MapPatchTranslation patch, Vector4 light, string faceName = "north")
    {
        var texture = patch.TextureFor(faceName);      // texture-ew on an east or west face
        float patchStories = TextureStories(texture);
        float first = patch.Story == 0 ? 0 : patch.Story - 1;
        float top = patch.Story == 0 ? stories : Math.Min(stories, first + patchStories);
        float from = Math.Max(first, bottom);
        if (from >= top)
            return;                     // on a story the wall doesn't reach, or hidden

        // Its texture's stories from the floor of its own story up (repeating for story 0)
        float v0 = 1 - (from - first) / patchStories, v1 = 1 - (top - first) / patchStories;
        var lift = outward * PatchLift;
        Vector2 pa = a + lift, pb = b + lift;
        Surfaces.Add(new Surface
        {
            Points = [new(pa.X, from, pa.Y), new(pb.X, from, pb.Y), new(pb.X, top, pb.Y), new(pa.X, top, pa.Y)],
            Uvs = [new(0, v0), new(1, v0), new(1, v1), new(0, v1)],
            Texture = Flat(texture, default),
            Color = light,
            Ref = faceRef,
        });
    }

    //
    // Blocks on open floor (a raised floor, an arch over it)
    //

    /// <summary>
    /// An open floor tile's blocks (plane 3): each one's faces where nothing beside hides them,
    /// textured from the tile's flats sides, else the walls ending its run (WallPosts.BlockFaceTexture);
    /// its top (the floor, at or under the player's) and, off the ground, its underside (its
    /// ceiling flat, else the run's texture). A face with nothing to take a texture from is left
    /// open, as the game leaves it.
    /// </summary>
    private void AddFloorBlocks(int x, int y, List<(int Bottom, int Top)> blocks, Vector4 ceilingColor)
    {
        if (blocks.Count == 0)
            return;
        var sides = _tiles.FlatSides(x, y);
        var faces = SquareFaces.Select(face => (Face: face, Texture: sides ?? _tiles.ArchFaceTexture(x, y, FaceName(face)))).ToList();
        var underside = sides ?? _tiles.ArchUndersideTexture(x, y) ?? faces.Select(face => face.Texture).FirstOrDefault(texture => texture != null);

        foreach (var (face, texture) in faces)
        {
            var (nx, ny) = Step(x, y, face);
            if (texture == null || !InMap(nx, ny))
                continue;
            var cover = CoverAt(nx, ny, Opposite(face));
            var (a, b) = Edge(x, y, face);
            foreach (var (blockBottom, blockTop) in blocks)
                foreach (var (bottom, top) in Uncovered(blockBottom, blockTop, cover))
                    AddWall(new SurfaceRef(SurfaceKind.Arch, x, y, face), a, b, bottom, top, texture, Light(nx, ny));
        }

        var light = Light(x, y);
        foreach (var (bottom, top) in blocks)
        {
            AddBlockTop(new SurfaceRef(SurfaceKind.Arch, x, y, Face.Top), x, y, Corners(x, y), top, underside, light);
            if (bottom > 0)
                AddUnderside(new SurfaceRef(SurfaceKind.Arch, x, y, Face.Bottom), x, y, Corners(x, y), bottom, underside, ceilingColor, light);
        }
    }

    //
    // Scrollers (Rendering/Scrollers.cs): a running one moves its tag's tiles' textures a tic
    //

    private ScrollerProperties? RunningScroller(int x, int y)
        => _tiles[MapConstants.TagPlane, x, y] is var tag and not 0 && _scrollers.TryGetValue(tag, out var scroller) && scroller.Active
            ? scroller : null;

    /// <summary>
    /// A wall face's texture moving right (x) and down (y) as seen, whichever way its u runs
    /// (<paramref name="across"/>: u1 - u0, negative on a flipped face). Moving right, a point
    /// shows the texel that was left of it; moving down, the one above it.
    /// </summary>
    private Vector2 WallScroll(int x, int y, float across, string texture)
    {
        if (RunningScroller(x, y) is not { Walls: true } scroller)
            return default;
        return new Vector2((float)(-scroller.X / TextureAsset.StorySize) * MathF.Sign(across),
            (float)(-scroller.Y / TextureAsset.StorySize) / TextureStories(texture));
    }

    /// <summary>A floor's or ceiling's texture moving east (x) and south (y): u runs east, v south, a tile each</summary>
    private Vector2 FlatScroll(int x, int y, Func<ScrollerProperties, bool> on)
        => RunningScroller(x, y) is { } scroller && on(scroller)
            ? new Vector2((float)(-scroller.X / TextureAsset.StorySize), (float)(-scroller.Y / TextureAsset.StorySize))
            : default;

    /// <summary>How many stories tall a wall texture is: 64 rows each (TextureAsset)</summary>
    private float TextureStories(string name)
    {
        if (!_textureStories.TryGetValue(name, out var stories))
        {
            var texture = _tiles.Content.Find<TextureAsset>(name);
            _textureStories[name] = stories = texture is { Height: > 0 } ? Math.Max(1f, texture.Height / (float)TextureAsset.StorySize) : 1f;
        }
        return stories;
    }

    //
    // Doors
    //

    /// <summary>
    /// A door, on the player's story; above it the lintel over it, and below it (on a story over
    /// the first) its frame down to the ground; true when the lintel's capped like a block (<see cref="AddLintel"/>)
    /// </summary>
    private bool AddDoor(int x, int y, Vector4 ceilingColor)
    {
        var door = _tiles.Door(x, y)!;
        int stories = _tiles.Stories(x, y);
        float bottom = _floor, top = _floor + 1;
        bool lintel = stories > top && AddLintel(x, y, door.Vertical, stories, ceilingColor);
        if (_floor > 0)
            AddDoorFrame(x, y, door.Vertical);

        if (door.Vertical)
        {
            // Across the tile north to south, at its middle; both faces map the texture from
            // the north, so one side shows it mirrored, as in the game
            Vector2 north = new(x + 0.5f, y), south = new(x + 0.5f, y + 1);
            Vector4 westLight = FaceLight(x, y, Face.West), eastLight = FaceLight(x, y, Face.East);
            string westFace = DoorFace(door, x, y, "west"), eastFace = DoorFace(door, x, y, "east");
            AddWall(new SurfaceRef(SurfaceKind.Door, x, y, Face.West), north, south, bottom, top, westFace, westLight, seeThrough: SeesThrough(westFace));
            AddWall(new SurfaceRef(SurfaceKind.Door, x, y, Face.East), south, north, bottom, top, eastFace, eastLight, 1, 0, seeThrough: SeesThrough(eastFace));
            if (stories > top && !lintel)
            {
                // Set into no wall: the door carries on up (WallHits.UnderLintel)
                AddWall(new SurfaceRef(SurfaceKind.Door, x, y, Face.West), north, south, top, stories, westFace, westLight, seeThrough: SeesThrough(westFace));
                AddWall(new SurfaceRef(SurfaceKind.Door, x, y, Face.East), south, north, top, stories, westFace, eastLight, seeThrough: SeesThrough(westFace));
            }
        }
        else
        {
            Vector2 west = new(x, y + 0.5f), east = new(x + 1, y + 0.5f);
            Vector4 northLight = FaceLight(x, y, Face.North), southLight = FaceLight(x, y, Face.South);
            string northFace = DoorFace(door, x, y, "north"), southFace = DoorFace(door, x, y, "south");
            AddWall(new SurfaceRef(SurfaceKind.Door, x, y, Face.North), east, west, bottom, top, northFace, northLight, 1, 0, seeThrough: SeesThrough(northFace));
            AddWall(new SurfaceRef(SurfaceKind.Door, x, y, Face.South), west, east, bottom, top, southFace, southLight, seeThrough: SeesThrough(southFace));
            if (stories > top && !lintel)
            {
                AddWall(new SurfaceRef(SurfaceKind.Door, x, y, Face.North), east, west, top, stories, northFace, northLight, seeThrough: SeesThrough(northFace));
                AddWall(new SurfaceRef(SurfaceKind.Door, x, y, Face.South), west, east, top, stories, northFace, southLight, seeThrough: SeesThrough(northFace));
            }
        }
        return lintel;
    }

    // A door face with clear pixels (a masked texture from a pk3) is seen through, as in the game
    private bool SeesThrough(string texture) => _tiles.Content.Find<TextureAsset>(texture) is { Masked: true };

    /// <summary>
    /// Under a door on a story over the first, its frame from the ground up to the player's floor
    /// (WallPosts.OpenTile): on its open sides, faced like the wall the door is set into
    /// </summary>
    private void AddDoorFrame(int x, int y, bool vertical)
    {
        Face[] open = vertical ? [Face.West, Face.East] : [Face.North, Face.South];
        foreach (var face in open)
        {
            var (nx, ny) = Step(x, y, face);
            if (!InMap(nx, ny) || LintelTexture(x, y, vertical, FaceName(face)) is not { } texture)
                continue;
            var (a, b) = Edge(x, y, face);
            foreach (var (bottom, top) in Uncovered(0, _floor, CoverAt(nx, ny, Opposite(face))))
                AddWall(new SurfaceRef(SurfaceKind.Door, x, y, face), a, b, bottom, top, texture, Light(nx, ny));
        }
    }

    /// <summary>
    /// Above a door, its tile capped like a block (WallPosts.OpenDoorArch): on its open sides a
    /// lintel flush with the wall the door is set into, from the door's top up to the tile's height,
    /// and an underside over the doorway, from its ceiling flat or else the wall beside it; false
    /// when it's set into no wall
    /// </summary>
    private bool AddLintel(int x, int y, bool vertical, int stories, Vector4 ceilingColor)
    {
        Face[] open = vertical ? [Face.West, Face.East] : [Face.North, Face.South];
        var faces = open.Select(face => (Face: face, Texture: LintelTexture(x, y, vertical, FaceName(face)))).ToList();
        if (faces.All(face => face.Texture == null))
            return false;

        int doorTop = _floor + 1;
        foreach (var (face, texture) in faces)
        {
            var (nx, ny) = Step(x, y, face);
            if (texture == null || !InMap(nx, ny))
                continue;

            // Hidden by a block or wall in front
            var (a, b) = Edge(x, y, face);
            foreach (var (bottom, top) in Uncovered(doorTop, stories, CoverAt(nx, ny, Opposite(face))))
                AddWall(new SurfaceRef(SurfaceKind.Door, x, y, face), a, b, bottom, top, texture, Light(nx, ny));
        }

        // The face of the wall beside the door that looks into the doorway (WallPosts.DoorArchUndersideTexture)
        var underside = (vertical
            ? _tiles.Wall(x, y - 1)?.WallFace("south") ?? _tiles.Wall(x, y + 1)?.WallFace("north")
            : _tiles.Wall(x - 1, y)?.WallFace("east") ?? _tiles.Wall(x + 1, y)?.WallFace("west"))
            ?? faces.First(face => face.Texture != null).Texture!;
        var light = Light(x, y);
        float undersideStories = TextureStories(underside);
        var topTexture = Texture(underside);
        var flat = _tiles.ArchCeilingFlat(x, y);
        AddFlat(new SurfaceRef(SurfaceKind.Door, x, y, Face.Bottom), Corners(x, y), doorTop, up: false,
            flat != null ? Flat(flat, ceilingColor) : topTexture, light, flat != null ? 1 : undersideStories);
        AddFlat(new SurfaceRef(SurfaceKind.Door, x, y, Face.Top), Corners(x, y), stories, up: true, topTexture, TopShade * light, undersideStories);
        return true;
    }

    /// <summary>
    /// A door's face (Doors.DoorFace): its locked face while it has a lock and one, else its own,
    /// else the opposite side's
    /// </summary>
    private string DoorFace(MapTextureTranslation door, int x, int y, string face)
    {
        bool locked = !string.IsNullOrEmpty(door.Lock) || door.Locks.Count > 0 ||_tiles.Defs.DoorLocks.ContainsKey(_tiles[1, x, y]);
        if (locked && door.Locked?.Side(face) is { Length: > 0 } lockedFace)
            return lockedFace;

        return door.WallFace(face);
    }

    /// <summary>
    /// The wall a door is set into, on either side in line with it, as seen on the lintel's
    /// <paramref name="face"/> (WallHits.LintelTexture)
    /// </summary>
    private string? LintelTexture(int x, int y, bool vertical, string face)
    {
        int dx = vertical ? 0 : 1, dy = vertical ? 1 : 0;
        foreach (var (tx, ty) in new[] { (x - dx, y - dy), (x + dx, y + dy) })
        {
            if (InMap(tx, ty) && _tiles.Wall(tx, ty) is { } wall)
                return wall.WallFace(face);
        }
        return null;
    }

    //
    // Floors, ceilings, things
    //

    /// <param name="ceiling">The ceiling's height, or null for none</param>
    /// <param name="floor">The floor's height (stories up from the ground), or null for none</param>
    private void AddFloorAndCeiling(int x, int y, Vector4 floorColor, Vector4 ceilingColor, float? ceiling, Vector2[]? polygon, float? floor = 0)
    {
        polygon ??= Corners(x, y);
        var light = Light(x, y);
        if (floor is { } floorHeight)
            AddFloor(x, y, polygon, floorHeight, floorColor, new SurfaceRef(SurfaceKind.Floor, x, y));

        // The sky shows in place of the ceiling color, and behind a ceiling flat's see-through pixels:
        // the level's is drawn behind everything, a tile's own (mapdefs flats skies) just over its ceiling
        var top = _tiles.CeilingFlat(x, y);
        var tileSky = Shown(_tiles.CeilingSky(x, y));
        bool sky = Sky != null || _tiles.CeilingSky(x, y) != null;
        if (ceiling is not { } height)
            return;
        var ceilingRef = new SurfaceRef(SurfaceKind.Ceiling, x, y);
        if (tileSky != null)
            AddFlat(ceilingRef, polygon, top != null ? height + SkyGap : height, up: false, new TextureRef(TextureSource.Sky, tileSky), Vector4.One);
        if (top != null || !sky)
        {
            AddFlat(ceilingRef, polygon, height, up: false,
                top != null ? Flat(top, !sky ? ceilingColor : default) : TextureRef.Solid, (top != null ? Vector4.One : ceilingColor) * light,
                scroll: top != null ? FlatScroll(x, y, scroller => scroller.Ceiling) : default);
        }
    }

    /// <summary>
    /// A tile's floor at a height (the ground, or a block's top): its floor flat, else the floor
    /// color; a floor sky (the tile's own, mapdefs flats floor-skies, else the level's) shows in
    /// place of the color, and behind a floor flat's see-through pixels, just under it
    /// </summary>
    private void AddFloor(int x, int y, Vector2[] polygon, float height, Vector4 floorColor, SurfaceRef floorRef)
    {
        var light = Light(x, y);
        var floor = _tiles.FloorFlat(x, y);
        var floorSky = Shown(_tiles.FloorTileSky(x, y)) ?? FloorSky;
        if (floorSky != null)
            AddFlat(floorRef, polygon, floor != null ? height - SkyGap : height, up: true, new TextureRef(TextureSource.FloorSky, floorSky), Vector4.One);
        if (floor != null || floorSky == null)
        {
            AddFlat(floorRef, polygon, height, up: true,
                floor != null ? Flat(floor, floorSky == null ? floorColor : default) : TextureRef.Solid, (floor != null ? Vector4.One : floorColor) * light,
                scroll: floor != null ? FlatScroll(x, y, scroller => scroller.Floor) : default);
        }
    }

    /// <summary>
    /// A level polygon at a height, given anticlockwise seen from above, facing up or down. A
    /// texture of more than one story shows only its bottom one.
    /// </summary>
    private void AddFlat(SurfaceRef flatRef, Vector2[] polygon, float height, bool up, TextureRef texture, Vector4 color, float textureStories = 1,
        Vector2 scroll = default)
    {
        var points = polygon.Select(p => new Vector3(p.X, height, p.Y)).ToArray();
        if (!up)
            Array.Reverse(points);
        Surfaces.Add(new Surface
        {
            Points = points,
            Uvs = points.Select(p => new Vector2(p.X - flatRef.X, 1 - (1 - (p.Z - flatRef.Y)) / textureStories)).ToArray(),
            Texture = texture,
            Color = color,
            Ref = flatRef,
            Scroll = scroll,
        });
        Scrolls |= scroll != Vector2.Zero;
    }

    private void AddThing(int x, int y)
    {
        var content = _tiles.Content;
        if (_tiles.Thing(x, y) is { } thing)
        {
            if (content.BrokenThingReason(thing.Class) != null)
            {
                AddMarker(x, y, BrokenColor);
                return;
            }

            var sprite = content.ThingSprite(thing.Class);
            if (sprite is not { Width: > 0, Height: > 0 })
                return;     // nothing to see in the game (a patrol point)

            float width = sprite.Width / (float)TextureAsset.StorySize, height = sprite.Height / (float)TextureAsset.StorySize;
            var texture = new TextureRef(TextureSource.Sprite, thing.Class);
            var thingRef = new SurfaceRef(SurfaceKind.Thing, x, y);

            // As the game places it: its origin pixel (a Wolf3D sprite's middle of the bottom of
            // 64 × 64) on the thing's spot on the floor
            var (originX, originY) = sprite.Origin;
            float lift = (originY - sprite.Height) / (float)TextureAsset.StorySize + _tiles.ThingStory(x, y) - 1;   // on its story's floor
            float shift = (sprite.Width / 2f - originX) / TextureAsset.StorySize;

            if (content.IsWallSprite(thing.Class))
                AddWallSprite(x, y, thing.Angles, content.WallSpriteOffset(thing.Class), width, height, lift, texture, thingRef);
            else
            {
                Billboards.Add(new Billboard
                {
                    Base = new Vector3(x + 0.5f, lift, y + 0.5f), Shift = shift, Width = width, Height = height, Texture = texture, Ref = thingRef,
                    Color = Light(x, y),
                });
            }
        }
        else if (_tiles.PlayerStart(x, y) is { } start)
        {
            // An arrow on the floor the way the player faces
            float radians = start.Angles * MathF.PI / 180;
            Vector2 center = new(x + 0.5f, y + 0.5f), front = new(MathF.Cos(radians), -MathF.Sin(radians)), side = new(-front.Y, front.X);
            Vector2[] arrow = [center + front * 0.4f, center - front * 0.3f + side * 0.3f, center - front * 0.3f - side * 0.3f];
            if (Cross(arrow[1] - arrow[0], arrow[2] - arrow[0]) > 0)
                (arrow[1], arrow[2]) = (arrow[2], arrow[1]);
            AddFlat(new SurfaceRef(SurfaceKind.PlayerStart, x, y), arrow, _floor + 0.01f, up: true, TextureRef.Solid, StartColor);
        }
        else if (!_tiles.ObjectKnown(x, y))
            AddMarker(x, y, UnknownColor);
    }

    /// <summary>
    /// An object-plane value that shows nothing (a code the mapdefs don't know, or a thing whose
    /// class or sprite is missing): a diamond on the floor in its color, hidden with the things
    /// </summary>
    private void AddMarker(int x, int y, Vector4 color)
    {
        Vector2 center = new(x + 0.5f, y + 0.5f);
        Vector2[] diamond = [center + new Vector2(0, 0.35f), center + new Vector2(0.35f, 0), center + new Vector2(0, -0.35f), center + new Vector2(-0.35f, 0)];
        if (Cross(diamond[1] - diamond[0], diamond[2] - diamond[0]) > 0)
            Array.Reverse(diamond);
        AddFlat(new SurfaceRef(SurfaceKind.Thing, x, y), diamond, _floor + 0.01f, up: true, TextureRef.Solid, color);
    }

    /// <summary>
    /// A flat panel across its tile at right angles to the way it faces, moved toward its front
    /// by its offset (Rendering/WallSprites.cs); its back shows the picture mirrored
    /// </summary>
    private void AddWallSprite(int x, int y, int angles, int offset, float width, float height, float lift, TextureRef texture, SurfaceRef thingRef)
    {
        float radians = angles * MathF.PI / 180;
        Vector2 front = new(MathF.Cos(radians), -MathF.Sin(radians));
        // Seen from the front (looking back along -front), its right
        Vector2 right = new(front.Y, -front.X);
        var middle = new Vector2(x + 0.5f, y + 0.5f) + front * (offset / (float)TextureAsset.StorySize);
        float half = (angles % 90 == 0 ? 0.5f : 0.7071f) * Math.Max(width, 1f);
        Vector2 a = middle - right * half, b = middle + right * half;

        Surfaces.Add(new Surface
        {
            // Stretched across its tile, as the game draws it: only its offset's height counts
            Points = [new(a.X, lift, a.Y), new(b.X, lift, b.Y), new(b.X, lift + height, b.Y), new(a.X, lift + height, a.Y)],
            Uvs = [new(0, 1), new(1, 1), new(1, 0), new(0, 0)],
            Texture = texture,
            Color = Light(x, y),
            Ref = thingRef,
            TwoSided = true,
        });
    }

    //
    // Tile geometry
    //

    private static readonly Face[] SquareFaces = [Face.North, Face.East, Face.South, Face.West];

    private bool InMap(int x, int y) => x >= 0 && y >= 0 && x < _tiles.Width && y < _tiles.Height;

    private static (int X, int Y) Step(int x, int y, Face face) => face switch
    {
        Face.North => (x, y - 1),
        Face.South => (x, y + 1),
        Face.East => (x + 1, y),
        _ => (x - 1, y),
    };

    private static Face Opposite(Face face) => face switch
    {
        Face.North => Face.South,
        Face.South => Face.North,
        Face.East => Face.West,
        _ => Face.East,
    };

    /// <summary>The way a ray heads to cross onto this side's face, named as the game's door faces are</summary>
    private static string TravelName(Face side) => FaceName(side);   // leaving the door toward its north wall heads north, and so on

    /// <summary>A square face's name in mapdefs (MapTextureTranslation.Side): the way it looks out</summary>
    private static string FaceName(Face face) => face switch
    {
        Face.North => "north",
        Face.South => "south",
        Face.East => "east",
        _ => "west",
    };

    /// <summary>A side of a tile, left to right seen from outside it</summary>
    private static (Vector2 A, Vector2 B) Edge(int x, int y, Face face) => face switch
    {
        Face.North => (new(x + 1, y), new(x, y)),
        Face.South => (new(x, y + 1), new(x + 1, y + 1)),
        Face.East => (new(x + 1, y + 1), new(x + 1, y)),
        _ => (new(x, y), new(x, y + 1)),
    };

    /// <summary>Whether a wall tile's side is solid all along: any side of a square wall, a diagonal's solid corner's two</summary>
    private bool CoversEdge(int x, int y, Face side)
        => _tiles.Diagonal(x, y) is not { } diagonal || SolidEdges(diagonal.Shape).Contains(side);

    private static Face[] SolidEdges(WallShape shape) => shape switch
    {
        WallShape.SolidNW => [Face.North, Face.West],
        WallShape.SolidNE => [Face.North, Face.East],
        WallShape.SolidSW => [Face.South, Face.West],
        WallShape.SolidSE => [Face.South, Face.East],
        _ => SquareFaces,
    };

    /// <summary>The square's corners anticlockwise seen from above (north up)</summary>
    private static Vector2[] Corners(int x, int y) => [new(x, y + 1), new(x + 1, y + 1), new(x + 1, y), new(x, y)];

    /// <summary>A diagonal's solid triangle and open triangle, each anticlockwise seen from above</summary>
    private static (Vector2[] Solid, Vector2[] Open) DiagonalHalves(int x, int y, WallShape shape)
    {
        Vector2 nw = new(x, y), ne = new(x + 1, y), sw = new(x, y + 1), se = new(x + 1, y + 1);
        return shape switch
        {
            WallShape.SolidNW => ([sw, ne, nw], [sw, se, ne]),
            WallShape.SolidNE => ([nw, se, ne], [nw, sw, se]),
            WallShape.SolidSW => ([sw, se, nw], [se, ne, nw]),
            _ => ([sw, se, ne], [sw, ne, nw]),
        };
    }

    private static Vector2[] OpenHalf(int x, int y, WallShape shape) => DiagonalHalves(x, y, shape).Open;

    /// <summary>The ends of a diagonal's slanted face: the two corners beside its solid one</summary>
    private static (Vector2, Vector2) Slant(int x, int y, WallShape shape) => shape is WallShape.SolidNW or WallShape.SolidSE
        ? (new(x + 1, y), new(x, y + 1))
        : (new(x, y), new(x + 1, y + 1));

    /// <summary>The way a diagonal's face looks: out of its solid corner, across the tile</summary>
    private static Vector2 OpenDirection(WallShape shape) => shape switch
    {
        WallShape.SolidNW => new(1, 1),
        WallShape.SolidNE => new(-1, 1),
        WallShape.SolidSW => new(1, -1),
        _ => new(-1, -1),
    };

    /// <summary>An edge's ends, ordered left to right as seen from the side its face looks to</summary>
    private static (Vector2 A, Vector2 B) Ordered(Vector2 p, Vector2 q, Vector2 outward)
    {
        // A face from a to b looks toward (-(b-a).Y, (b-a).X)
        var t = q - p;
        return Vector2.Dot(new Vector2(-t.Y, t.X), outward) > 0 ? (p, q) : (q, p);
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    private static TextureRef Texture(string name) => new(TextureSource.Texture, name);

    private static TextureRef Flat(string name, Vector4 fill) => new(TextureSource.Flat, name, fill);

    private static Vector4? ParseColor(string? text)
    {
        if (text is not { Length: 7 } || text[0] != '#' || !uint.TryParse(text.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            return null;
        return new Vector4(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, 1);
    }

    //
    // Picking
    //

    /// <summary>
    /// The tile an edit made on a surface goes to: the surface's own, or with
    /// <paramref name="inFront"/> the open tile a wall or door face looks onto (where a thing
    /// would stand, or a wall built out from it). Tops, floors, ceilings and things are their own tile.
    /// </summary>
    public static (int X, int Y) TargetTile(SurfaceRef surface, bool inFront)
    {
        if (!inFront || surface.Kind is not (SurfaceKind.Wall or SurfaceKind.Door or SurfaceKind.Arch))
            return (surface.X, surface.Y);
        return surface.Face switch
        {
            Face.North => (surface.X, surface.Y - 1),
            Face.South => (surface.X, surface.Y + 1),
            Face.East => (surface.X + 1, surface.Y),
            Face.West => (surface.X - 1, surface.Y),
            _ => (surface.X, surface.Y),
        };
    }

    /// <summary>The nearest surface or sprite along a ray, or null when it meets none; <paramref name="include"/> leaves out hidden ones</summary>
    public SurfaceHit? Pick(Vector3 origin, Vector3 direction, Camera3D camera, Func<SurfaceRef, bool>? include = null)
    {
        SurfaceHit? best = null;

        foreach (var surface in Surfaces)
        {
            if (include?.Invoke(surface.Ref) == false)
                continue;
            var normal = surface.Normal;
            float facing = Vector3.Dot(direction, normal);
            if (facing >= 0 && !surface.TwoSided || MathF.Abs(facing) < 1e-6f)
                continue;

            float t = Vector3.Dot(surface.Points[0] - origin, normal) / facing;
            if (t <= 0 || best is { } nearest && t >= nearest.Distance)
                continue;

            var point = origin + direction * t;
            if (Inside(surface.Points, normal, point))
                best = new SurfaceHit(surface.Ref, point, t, surface.Texture);
        }

        // Sprites turned to the view: a vertical rectangle across the line of sight
        var right = camera.Right;
        var back = -camera.FlatForward;
        foreach (var sprite in Billboards)
        {
            if (include?.Invoke(sprite.Ref) == false)
                continue;
            float facing = Vector3.Dot(direction, back);
            if (facing >= -1e-6f)
                continue;
            float t = Vector3.Dot(sprite.Base - origin, back) / facing;
            if (t <= 0 || best is { } nearest && t >= nearest.Distance)
                continue;

            var point = origin + direction * t;
            float across = Vector3.Dot(point - sprite.Base, right) - sprite.Shift;
            if (MathF.Abs(across) <= sprite.Width / 2 && point.Y >= sprite.Base.Y && point.Y <= sprite.Base.Y + sprite.Height)
                best = new SurfaceHit(sprite.Ref, point, t, sprite.Texture);
        }

        return best;
    }

    /// <summary>Whether a point on a convex polygon's plane is inside it</summary>
    private static bool Inside(Vector3[] points, Vector3 normal, Vector3 point)
    {
        // The same side of every edge as the normal, whichever way it's wound
        float? sign = null;
        for (int i = 0; i < points.Length; i++)
        {
            var edge = points[(i + 1) % points.Length] - points[i];
            float side = Vector3.Dot(Vector3.Cross(edge, point - points[i]), normal);
            if (MathF.Abs(side) < 1e-6f)
                continue;
            if (sign is { } s && MathF.Sign(side) != s)
                return false;
            sign = MathF.Sign(side);
        }
        return true;
    }
}
