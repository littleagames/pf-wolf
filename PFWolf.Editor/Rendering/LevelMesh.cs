using System.Numerics;
using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Editor.Data;
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
}

public readonly record struct TextureRef(TextureSource Source, string Name)
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

    public Vector3 Normal => Vector3.Normalize(Vector3.Cross(Points[1] - Points[0], Points[2] - Points[0]));
}

/// <summary>A sprite standing on the floor that always turns to face the view, as the game's things do</summary>
public sealed class Billboard
{
    /// <summary>The middle of its bottom edge</summary>
    public Vector3 Base { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }
    public TextureRef Texture { get; init; }
    public SurfaceRef Ref { get; init; }
}

/// <summary>What the pointer is on in the 3D view</summary>
public readonly record struct SurfaceHit(SurfaceRef Ref, Vector3 Point, float Distance, TextureRef Texture);

/// <summary>
/// A level as 3D surfaces, built the way the game draws it (Program.WL_DRAW): walls show their
/// mapdefs East texture on east and west faces and North on north and south ones, the faces
/// beside a door show the door's side, tall walls repeat their texture story by story from the
/// floor, and things stand as sprites turned to the view.
/// </summary>
public sealed class LevelMesh
{
    private readonly MapTiles _tiles;
    private readonly Dictionary<string, float> _textureStories = new(StringComparer.OrdinalIgnoreCase);

    public List<Surface> Surfaces { get; } = [];
    public List<Billboard> Billboards { get; } = [];

    // How much a wall's top is darkened, so the tops read apart from the faces
    private static readonly Vector4 TopShade = new(0.55f, 0.55f, 0.55f, 1);
    private static readonly Vector4 StartColor = new(0.25f, 0.86f, 0.38f, 1);
    private static readonly Vector4 UnknownColor = new(0.86f, 0.16f, 0.16f, 1);

    private LevelMesh(MapTiles tiles) => _tiles = tiles;

    public static LevelMesh Build(MapTiles tiles)
    {
        var mesh = new LevelMesh(tiles);
        mesh.Build();
        return mesh;
    }

    private void Build()
    {
        var floorColor = ParseColor(_tiles.FloorColor) ?? new Vector4(0.44f, 0.44f, 0.44f, 1);
        var ceilingColor = ParseColor(_tiles.CeilingColor) ?? new Vector4(0.22f, 0.22f, 0.22f, 1);
        float ceiling = _tiles.LevelStories;

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
                        AddDoor(x, y);
                        AddFloorAndCeiling(x, y, floorColor, ceilingColor, ceiling, null);
                        break;

                    case TileKind.Unknown:
                        // A value the mapdefs don't know: marked on the floor
                        AddFlat(new SurfaceRef(SurfaceKind.Floor, x, y), Corners(x, y), 0, up: true, TextureRef.Solid, UnknownColor);
                        break;

                    default:
                        AddFloorAndCeiling(x, y, floorColor, ceilingColor, ceiling, null);
                        AddThing(x, y);
                        break;
                }
            }
        }
    }

    //
    // Walls
    //

    private void AddWallTile(int x, int y, Vector4 floorColor, Vector4 ceilingColor, float ceiling)
    {
        var wall = _tiles.Wall(x, y)!;
        int stories = _tiles.Stories(x, y);
        var diagonal = _tiles.Diagonal(x, y);

        foreach (var face in SquareFaces)
        {
            // A diagonal keeps only its solid corner's two edges as square faces
            if (diagonal != null && !SolidEdges(diagonal.Shape).Contains(face))
                continue;

            var (nx, ny) = Step(x, y, face);
            if (!InMap(nx, ny))
                continue;

            // Hidden behind a square wall at least as tall; a shorter one leaves the top showing
            float bottom = 0;
            if (_tiles.KindAt(nx, ny) == TileKind.Wall && CoversEdge(nx, ny, Opposite(face)))
            {
                bottom = _tiles.Stories(nx, ny);
                if (bottom >= stories)
                    continue;
            }

            var (a, b) = Edge(x, y, face);
            var texture = face is Face.North or Face.South ? wall.North : wall.East;
            var faceRef = new SurfaceRef(SurfaceKind.Wall, x, y, face);

            if (_tiles.KindAt(nx, ny) == TileKind.Door)
            {
                // The door's frame: its side texture for the door's story, the wall above (the
                // face's direction is the way a ray travels from the door into the wall)
                var door = _tiles.Door(nx, ny)!;
                AddWall(faceRef, a, b, 0, 1, DoorFace(door, nx, ny, TravelName(Opposite(face))));
                if (stories > 1)
                    AddWall(faceRef, a, b, 1, stories, texture);
            }
            else
                AddWall(faceRef, a, b, bottom, stories, texture);
        }

        var topRef = new SurfaceRef(SurfaceKind.Wall, x, y, Face.Top);
        if (diagonal != null)
        {
            // The slanted face, toward the open half, which has floor and ceiling
            var (solid, open) = DiagonalHalves(x, y, diagonal.Shape);
            var (p, q) = Slant(x, y, diagonal.Shape);
            var (a, b) = Ordered(p, q, OpenDirection(diagonal.Shape));
            var texture = string.IsNullOrEmpty(diagonal.Texture) ? wall.North : diagonal.Texture;
            AddWall(new SurfaceRef(SurfaceKind.Wall, x, y, Face.Diagonal), a, b, 0, stories, texture);

            AddFlat(topRef, solid, stories, up: true, Texture(wall.North), TopShade, TextureStories(wall.North));
            AddFloorAndCeiling(x, y, floorColor, ceilingColor, ceiling, OpenHalf(x, y, diagonal.Shape));
        }
        else
            AddFlat(topRef, Corners(x, y), stories, up: true, Texture(wall.North), TopShade, TextureStories(wall.North));
    }

    /// <summary>A wall face from a to b (left to right seen from its front) between two heights</summary>
    private void AddWall(SurfaceRef faceRef, Vector2 a, Vector2 b, float bottom, float top, string texture,
        float u0 = 0, float u1 = 1, bool twoSided = false)
    {
        // The texture's stories repeat up the wall from the floor
        float stories = TextureStories(texture);
        float v0 = 1 - bottom / stories, v1 = 1 - top / stories;
        Surfaces.Add(new Surface
        {
            Points = [new(a.X, bottom, a.Y), new(b.X, bottom, b.Y), new(b.X, top, b.Y), new(a.X, top, a.Y)],
            Uvs = [new(u0, v0), new(u1, v0), new(u1, v1), new(u0, v1)],
            Texture = Texture(texture),
            Ref = faceRef,
            TwoSided = twoSided,
        });
    }

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

    private void AddDoor(int x, int y)
    {
        var door = _tiles.Door(x, y)!;
        int stories = _tiles.Stories(x, y);
        var lintel = LintelTexture(x, y, door.Vertical);

        if (door.Vertical)
        {
            // Across the tile north to south, at its middle; both faces map the texture from
            // the north, so one side shows it mirrored, as in the game
            Vector2 north = new(x + 0.5f, y), south = new(x + 0.5f, y + 1);
            AddWall(new SurfaceRef(SurfaceKind.Door, x, y, Face.West), north, south, 0, 1, DoorFace(door, x, y, "west"));
            AddWall(new SurfaceRef(SurfaceKind.Door, x, y, Face.East), south, north, 0, 1, DoorFace(door, x, y, "east"), 1, 0);
            if (stories > 1)
            {
                var texture = lintel ?? DoorFace(door, x, y, "west");
                AddWall(new SurfaceRef(SurfaceKind.Door, x, y, Face.West), north, south, 1, stories, texture);
                AddWall(new SurfaceRef(SurfaceKind.Door, x, y, Face.East), south, north, 1, stories, texture);
            }
        }
        else
        {
            Vector2 west = new(x, y + 0.5f), east = new(x + 1, y + 0.5f);
            AddWall(new SurfaceRef(SurfaceKind.Door, x, y, Face.North), east, west, 0, 1, DoorFace(door, x, y, "north"), 1, 0);
            AddWall(new SurfaceRef(SurfaceKind.Door, x, y, Face.South), west, east, 0, 1, DoorFace(door, x, y, "south"));
            if (stories > 1)
            {
                var texture = lintel ?? DoorFace(door, x, y, "north");
                AddWall(new SurfaceRef(SurfaceKind.Door, x, y, Face.North), east, west, 1, stories, texture);
                AddWall(new SurfaceRef(SurfaceKind.Door, x, y, Face.South), west, east, 1, stories, texture);
            }
        }
    }

    /// <summary>
    /// A door's face (Program.DoorFace): its locked face while it has a lock and one, else its own,
    /// else the opposite side's
    /// </summary>
    private string DoorFace(MapTextureTranslation door, int x, int y, string face)
    {
        static string Face(MapTextureTranslation? textures, string face) => textures == null ? "" : face switch
        {
            "north" => textures.North,
            "south" => textures.South,
            "east" => textures.East,
            _ => textures.West,
        };
        static string Opposite(string face) => face switch { "north" => "south", "south" => "north", "east" => "west", _ => "east" };

        bool locked = !string.IsNullOrEmpty(door.Lock) || _tiles.Content.MapDefs.DoorLocks.ContainsKey(_tiles[1, x, y]);
        if (locked && Face(door.Locked, face) is { Length: > 0 } lockedFace)
            return lockedFace;

        var own = Face(door, face);
        return own.Length > 0 ? own : Face(door, Opposite(face));
    }

    /// <summary>The wall a door is set into, on either side in line with it (Program.LintelTexture)</summary>
    private string? LintelTexture(int x, int y, bool vertical)
    {
        int dx = vertical ? 0 : 1, dy = vertical ? 1 : 0;
        foreach (var (tx, ty) in new[] { (x - dx, y - dy), (x + dx, y + dy) })
        {
            if (InMap(tx, ty) && _tiles.Wall(tx, ty) is { } wall)
                return vertical ? wall.East : wall.North;
        }
        return null;
    }

    //
    // Floors, ceilings, things
    //

    private void AddFloorAndCeiling(int x, int y, Vector4 floorColor, Vector4 ceilingColor, float ceiling, Vector2[]? polygon)
    {
        polygon ??= Corners(x, y);
        var floor = _tiles.FloorFlat(x, y);
        AddFlat(new SurfaceRef(SurfaceKind.Floor, x, y), polygon, 0, up: true,
            floor != null ? Texture(floor) : TextureRef.Solid, floor != null ? Vector4.One : floorColor);

        var top = _tiles.CeilingFlat(x, y);
        AddFlat(new SurfaceRef(SurfaceKind.Ceiling, x, y), polygon, ceiling, up: false,
            top != null ? Texture(top) : TextureRef.Solid, top != null ? Vector4.One : ceilingColor);
    }

    /// <summary>
    /// A level polygon at a height, given anticlockwise seen from above, facing up or down. A
    /// texture of more than one story shows only its bottom one.
    /// </summary>
    private void AddFlat(SurfaceRef flatRef, Vector2[] polygon, float height, bool up, TextureRef texture, Vector4 color, float textureStories = 1)
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
        });
    }

    private void AddThing(int x, int y)
    {
        var content = _tiles.Content;
        if (_tiles.Thing(x, y) is { } thing)
        {
            var sprite = content.ThingSprite(thing.Class);
            if (sprite is not { Width: > 0, Height: > 0 })
                return;     // nothing to see in the game (a patrol point)

            float width = sprite.Width / (float)TextureAsset.StorySize, height = sprite.Height / (float)TextureAsset.StorySize;
            var texture = new TextureRef(TextureSource.Sprite, thing.Class);
            var thingRef = new SurfaceRef(SurfaceKind.Thing, x, y);

            if (content.IsWallSprite(thing.Class))
                AddWallSprite(x, y, thing.Angles, content.WallSpriteOffset(thing.Class), width, height, texture, thingRef);
            else
                Billboards.Add(new Billboard { Base = new Vector3(x + 0.5f, 0, y + 0.5f), Width = width, Height = height, Texture = texture, Ref = thingRef });
        }
        else if (_tiles.PlayerStart(x, y) is { } start)
        {
            // An arrow on the floor the way the player faces
            float radians = start.Angles * MathF.PI / 180;
            Vector2 center = new(x + 0.5f, y + 0.5f), front = new(MathF.Cos(radians), -MathF.Sin(radians)), side = new(-front.Y, front.X);
            Vector2[] arrow = [center + front * 0.4f, center - front * 0.3f + side * 0.3f, center - front * 0.3f - side * 0.3f];
            if (Cross(arrow[1] - arrow[0], arrow[2] - arrow[0]) > 0)
                (arrow[1], arrow[2]) = (arrow[2], arrow[1]);
            AddFlat(new SurfaceRef(SurfaceKind.PlayerStart, x, y), arrow, 0.01f, up: true, TextureRef.Solid, StartColor);
        }
    }

    /// <summary>
    /// A flat panel across its tile at right angles to the way it faces, moved toward its front
    /// by its offset (Program.WallSprites); its back shows the picture mirrored
    /// </summary>
    private void AddWallSprite(int x, int y, int angles, int offset, float width, float height, TextureRef texture, SurfaceRef thingRef)
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
            Points = [new(a.X, 0, a.Y), new(b.X, 0, b.Y), new(b.X, height, b.Y), new(a.X, height, a.Y)],
            Uvs = [new(0, 1), new(1, 1), new(1, 0), new(0, 0)],
            Texture = texture,
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
    private static string TravelName(Face side) => side switch
    {
        // Leaving the door toward its north wall heads north, and so on
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
        if (!inFront || surface.Kind is not (SurfaceKind.Wall or SurfaceKind.Door))
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
            float across = Vector3.Dot(point - sprite.Base, right);
            if (MathF.Abs(across) <= sprite.Width / 2 && point.Y >= 0 && point.Y <= sprite.Height)
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
