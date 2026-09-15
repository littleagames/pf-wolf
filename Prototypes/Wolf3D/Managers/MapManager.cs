using Wolf3D.Assets;

namespace Wolf3D.Managers;

public class MapDataConstants
{
    public const int ICONARROWS = 90;
    public const int PUSHABLETILE = 98;
    public const int EXITTILE = 99;          // at end of castle
    public const int AREATILE = 107;         // first of NUMAREAS floor tiles
    public const int NUMAREAS = 37;
    public const int ELEVATORTILE = 21;
    public const int AMBUSHTILE = 106;
    public const int ALTELEVATORTILE = 107;
}
struct maptype
{
    public int[] planestart;
    public UInt16[] planelength;
    public UInt16 width;
    public UInt16 height;
    public char[] name;

    public maptype()
    {
        planestart = new Int32[MapManager.MAPPLANES];
        planelength = new UInt16[MapManager.MAPPLANES];
        name = new char[16];
    }
}

internal class MapManager
{
    internal const int MAPSHIFT = 6;
    internal const int MAPSIZE = (1 << MAPSHIFT);
    internal const int MAPAREA = MAPSIZE * MAPSIZE;

    public const int NUMMAPS = 60;
    public const int MAPPLANES = 3;

    private readonly Lazy<AssetManager> assetManager;

    private UInt16[][] mapsegs = new ushort[MAPPLANES][];
    private maptype[] mapheaderseg = new maptype[NUMMAPS];

    private readonly LinkedList<Entities.Actors.Actor> _actors = new();

    public MapManager(Lazy<AssetManager> assetManager)
    {
        this.assetManager = assetManager;
    }

    internal ushort mapwidth, mapheight;
    internal byte[,] tilemap;
    internal bool[,] spotvis;
    internal Actor?[,] actorat;

    public ushort GetTile(int x, int y, int plane)
    {
        return mapsegs[0][y * mapwidth + x];
    }

    public void LoadMap(string mapName)
    {
        var mapAsset = assetManager.Value.Find<MapAsset>(mapName);
        if (mapAsset == null)
            throw new Exception($"Map not found {mapName}");

        mapwidth = mapAsset.Width;
        mapheight = mapAsset.Height;
        mapsegs = mapAsset.MapData;

#if USE_FEATUREFLAGS
    const int MXX = MAPSIZE - 1;
    
    // Read feature flags data from map corners and overwrite corners with adjacent tiles
    ffDataTopLeft     = MAPSPOT(0,   0,   0); MAPSPOT(0,   0,   0) = MAPSPOT(1,       0,       0);
    ffDataTopRight    = MAPSPOT(MXX, 0,   0); MAPSPOT(MXX, 0,   0) = MAPSPOT(MXX,     1,       0);
    ffDataBottomRight = MAPSPOT(MXX, MXX, 0); MAPSPOT(MXX, MXX, 0) = MAPSPOT(MXX - 1, MXX,     0);
    ffDataBottomLeft  = MAPSPOT(0,   MXX, 0); MAPSPOT(0,   MXX, 0) = MAPSPOT(0,       MXX - 1, 0);
#endif

        tilemap = new byte[MAPSIZE, MAPSIZE]; // wall values only
        spotvis = new bool[MAPSIZE, MAPSIZE];
        actorat = new Actor?[MAPSIZE, MAPSIZE];

        var data = GetMapData();

        for (int y = 0; y < mapheight; y++)
        {
            for (int x = 0; x < mapwidth; x++)
            {
                int tile = MAPSPOT(x, y, 0);
                if (tile < MapDataConstants.AMBUSHTILE)
                {
                    // solid wall
                    tilemap[x, y] = (byte)tile;
                    actorat[x, y] = new Wall(tile);// (uint)tile;
                }
                else
                {
                    // area floor
                    tilemap[x, y] = 0;
                    actorat[x, y] = null;
                }

                // TODO: SpawnDoor

                int objtile = MAPSPOT(x, y, 1);
                if (data.Things.TryGetValue(objtile, out var thingXlat))
                {
                    SpawnThing(x, y, thingXlat.Class);
                    continue;
                }
            }
        }
    }

    public void SpawnThing(int tilex, int tiley, string className)
    {
        var actorMetaData = assetManager.Value.GetActorMetadata();

        if (!actorMetaData.Actors.TryGetValue(className, out var actor))
            return;
        var builtActor = actorMetaData.CreateActor(className, actor); // TODO: Should this just create objects?
        if (builtActor == null)
            return;

        builtActor.SetPosition(tilex, tiley);

        //builtActor.flags = 0;
        if (builtActor.Flags.Any(f => f.Equals("COUNTITEM", StringComparison.OrdinalIgnoreCase)))
        {
            // TODO: GameManager? MapManager? who handles this?
           // if (!loadedgame)
           //     gamestate.treasuretotal++;
           // newstatobj.flags = objflags.FL_BONUS;
        }

        //if (builtActor.Properties.Keys.Any(x => x.StartsWith("inventory")))
        //{
        //    newstatobj.flags = objflags.FL_BONUS;
        //}
        _actors.AddLast(builtActor);
    }

    internal static bool VALIDAREA(int x) => (x) >= MapDataConstants.AREATILE && (x) < (MapDataConstants.AREATILE + MapDataConstants.NUMAREAS);

    internal static bool ISPOINTER(Actor? check)
    {
        return check is objstruct;
    }


    internal int MAPSPOT(int x, int y, int plane) => (mapsegs[(plane)][((y) << MAPSHIFT) + (x)]);
    internal void SetMapSpot(int x, int y, int plane, ushort value)
    {
        (mapsegs[(plane)][((y) << MAPSHIFT) + (x)]) = value;
    }

    internal MapObjectTranslationAsset GetMapData()
    {
        //var mapSpecific = assetManager.Value.Find<MapObjectTranslationAsset>("map01/mapdefs");
        var gameInfo = assetManager.Value.Find<MapObjectTranslationAsset>("wolf3d/mapdefs");
        if (gameInfo == null)
            throw new Exception("Map data not found");
        return gameInfo;
    }

    internal void RemoveActor(Entities.Actors.Inventory builtActor)
    {
        _actors.Remove(builtActor);
    }

    internal LinkedList<Entities.Actors.Actor> GetActors()
    {
        return _actors;
    }

    internal void DoActors(uint tics)
    {
        for (var actor = _actors.First; actor != null; actor = actor.Next)
        {
            DoActor(actor.Value, tics);
        }
    }

    internal void DoActor(Entities.Actors.Actor ob, uint tics)
    {
        var state = ob.CurrentState;
        if (state == null)
            return;

        // Mirrors Program.DoActor (Program.WL_PLAY.cs): a frame with TicTime == 0 holds forever
        // -- once TicCount sticks at 0, only Think runs each tic, Next is never consulted again.
        if (ob.TicCount == 0)
        {
            Entities.Actors.ActorActionRegistry.Invoke(state.Think, ob);
            return;
        }

        ob.TicCount -= (short)tics;
        while (ob.TicCount <= 0)
        {
            Entities.Actors.ActorActionRegistry.Invoke(state.Action, ob);

            state = state.Next;
            if (state == null)
                return; // the resolver never leaves Next null in practice; defensive only.
            ob.CurrentState = state;

            if (state.TicTime == 0)
            {
                ob.TicCount = 0;
                break;
            }
            ob.TicCount += state.TicTime;
        }

        Entities.Actors.ActorActionRegistry.Invoke(state.Think, ob);
    }
}
