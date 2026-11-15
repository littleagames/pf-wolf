using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Gameplay.MatchModes;
using PFWolf.Ui;

namespace PFWolf.Tests;

// Objective markers (IMatchObjective.Markers): what each mode points a player to
public partial class MatchScoringTests
{
    private const long Tile = MapConstants.TileGlobal;

    private static ObjectiveMarker On((int x, int y) tile, string label, int team, MarkerStyle style = MarkerStyle.None) =>
        ObjectiveMarker.OnTile(tile, label, team, style);

    // A zone's marker: KOTH's hill and Domination's points reach a tile out (3x3)
    private static ObjectiveMarker Zone((int x, int y) tile, string label, int team, MarkerStyle style = MarkerStyle.None) =>
        ObjectiveMarker.OnTile(tile, label, team, style, zone: 1);

    private static KingOfTheHill HillAt(Rig rig, int now, int movesAt, int owner = -1)
    {
        var koth = new KingOfTheHill(rig.Map, rig.Session, rig.Match, rig.Hud, rig.Hooks);
        koth.Zones.Add(new ControlZones.Zone { At = (5, 5) });
        koth.Zones.Add(new ControlZones.Zone { At = (20, 8), Owner = owner });
        koth.Hill = 1;
        koth.MovesAt = movesAt;
        rig.Session.GameState.TimeCount = now;
        return koth;
    }

    [Test]
    public void The_Hill_Is_Marked_In_Its_Holders_Color_Flashing_Just_After_It_Moves()
    {
        // Arrange: it moved 2 s ago, blue holds it
        var rig = new Rig(GameMode.KingOfTheHill);
        var koth = HillAt(rig, 1000, 1000 - 2 * 70 + KingOfTheHill.HillTics, owner: 1);

        // Act
        var markers = ((IMatchObjective)koth).Markers(rig.Red1);

        // Assert: only the hill, not the other spot
        Assert.That(markers, Is.EqualTo(new[] { Zone((20, 8), "Hill", 1, MarkerStyle.Pulse) }));
    }

    [Test]
    public void The_Hill_Stops_Flashing_And_Counts_Down_Its_Last_Seconds()
    {
        var rig = new Rig(GameMode.KingOfTheHill);
        Assert.That(((IMatchObjective)HillAt(rig, 1000, 1000 + 30 * 70)).Markers(rig.Red1),
            Is.EqualTo(new[] { Zone((20, 8), "Hill", -1) }));
        Assert.That(((IMatchObjective)HillAt(rig, 1000, 1000 + 7 * 70)).Markers(rig.Red1),
            Is.EqualTo(new[] { Zone((20, 8), "Hill 7", -1) }));
        Assert.That(((IMatchObjective)HillAt(rig, 1000, 1000 + 7 * 70 - 30)).Markers(rig.Red1).Single().Label,
            Is.EqualTo("Hill 7"), "a part second rounds up");
    }

    [Test]
    public void Every_Control_Point_Is_Marked_By_Its_Letter_In_Its_Holders_Color()
    {
        var rig = new Rig(GameMode.Domination);
        var domination = new Domination(rig.Map, rig.Session, rig.Match, rig.Hud, rig.Hooks);
        domination.Zones.Add(new ControlZones.Zone { At = (3, 3), Owner = 0 });
        domination.Zones.Add(new ControlZones.Zone { At = (10, 10) });
        domination.Zones.Add(new ControlZones.Zone { At = (17, 3), Owner = 1 });

        Assert.That(((IMatchObjective)domination).Markers(rig.Blue),
            Is.EqualTo(new[] { Zone((3, 3), "A", 0), Zone((10, 10), "B", -1), Zone((17, 3), "C", 1) }));
    }

    // Capture the flag with the red base at (2, 2) and the blue at (30, 2), both flags home
    private static CaptureTheFlag FlagsHome(Rig rig)
    {
        var ctf = new CaptureTheFlag(rig.Map, rig.Session, rig.Match, rig.Hud, rig.Hooks);
        ctf.Flags[0].Home = ctf.Flags[0].At = (2, 2);
        ctf.Flags[1].Home = ctf.Flags[1].At = (30, 2);
        return ctf;
    }

    private static void Carry(CaptureTheFlag ctf, int team, Entities.PlayerState carrier)
    {
        ctf.Flags[team].State = CaptureTheFlag.FlagState.Carried;
        ctf.Flags[team].Carrier = carrier.Number;
    }

    [Test]
    public void With_Both_Flags_Home_A_Team_Sees_Only_The_Other_Teams_Flag()
    {
        var rig = new Rig(GameMode.CaptureTheFlag);
        var ctf = (IMatchObjective)FlagsHome(rig);

        Assert.That(ctf.Markers(rig.Red1), Is.EqualTo(new[] { On((30, 2), "Flag", 1) }));
        Assert.That(ctf.Markers(rig.Blue), Is.EqualTo(new[] { On((2, 2), "Flag", 0) }));
    }

    [Test]
    public void An_Enemy_Carrying_Your_Flag_Is_Marked_To_Kill()
    {
        // Arrange: blue runs off with the red flag
        var rig = new Rig(GameMode.CaptureTheFlag);
        var ctf = FlagsHome(rig);
        Carry(ctf, 0, rig.Blue);
        (rig.Blue.Pawn!.X, rig.Blue.Pawn.Y) = (12 * (int)Tile, 7 * (int)Tile);

        // Act
        var markers = ((IMatchObjective)ctf).Markers(rig.Red1);

        // Assert
        Assert.That(markers, Is.EqualTo(new[]
        {
            new ObjectiveMarker(12 * Tile, 7 * Tile, "Kill", 0, MarkerStyle.Pulse),
            On((30, 2), "Flag", 1),
        }));
    }

    [Test]
    public void Carrying_Their_Flag_Marks_Your_Base_And_Your_Teammates_Escort_You()
    {
        // Arrange: Red1 has the blue flag
        var rig = new Rig(GameMode.CaptureTheFlag);
        var ctf = FlagsHome(rig);
        Carry(ctf, 1, rig.Red1);
        (rig.Red1.Pawn!.X, rig.Red1.Pawn.Y) = (20 * (int)Tile, 4 * (int)Tile);

        // Act / Assert
        Assert.That(((IMatchObjective)ctf).Markers(rig.Red1), Is.EqualTo(new[] { On((2, 2), "Capture", 0, MarkerStyle.Pulse) }));
        Assert.That(((IMatchObjective)ctf).Markers(rig.Red2), Is.EqualTo(new[] { new ObjectiveMarker(20 * Tile, 4 * Tile, "Escort", 1) }));
    }

    [Test]
    public void Carrying_Their_Flag_While_Yours_Is_Away_Marks_Your_Base_Quietly()
    {
        var rig = new Rig(GameMode.CaptureTheFlag);
        var ctf = FlagsHome(rig);
        Carry(ctf, 1, rig.Red1);
        Carry(ctf, 0, rig.Blue);

        var markers = ((IMatchObjective)ctf).Markers(rig.Red1).ToList();

        Assert.That(markers.Select(m => m.Label), Is.EqualTo(new[] { "Kill", "Base" }));
        Assert.That(markers[1], Is.EqualTo(On((2, 2), "Base", 0, MarkerStyle.Quiet)));
    }

    [Test]
    public void A_Dropped_Flag_Is_To_Return_For_Its_Team_And_To_Take_For_The_Other_With_Its_Time_Left()
    {
        // Arrange: the red flag dropped 10 s ago at (9, 9)
        var rig = new Rig(GameMode.CaptureTheFlag);
        var ctf = FlagsHome(rig);
        ctf.Flags[0].State = CaptureTheFlag.FlagState.Dropped;
        ctf.Flags[0].At = (9, 9);
        ctf.Flags[0].DroppedAt = 500;
        rig.Session.GameState.TimeCount = 500 + 10 * 70;
        int left = CaptureTheFlag.ReturnTics / 70 - 10;

        // Act / Assert
        Assert.That(((IMatchObjective)ctf).Markers(rig.Red1),
            Is.EqualTo(new[] { On((9, 9), $"Return {left}", 0, MarkerStyle.Pulse), On((30, 2), "Flag", 1) }));
        Assert.That(((IMatchObjective)ctf).Markers(rig.Blue), Is.EqualTo(new[] { On((9, 9), $"Flag {left}", 0, MarkerStyle.Pulse) }));
    }

    [Test]
    public void A_Player_On_No_Team_Sees_Both_Flags()
    {
        var rig = new Rig(GameMode.CaptureTheFlag);
        var ctf = FlagsHome(rig);
        var watcher = new Entities.PlayerState { Number = 3, Team = -1 };

        Assert.That(((IMatchObjective)ctf).Markers(watcher), Is.EqualTo(new[] { On((2, 2), "Flag", 0), On((30, 2), "Flag", 1) }));
    }

    [Test]
    public void Modes_Without_A_Place_To_Go_Mark_Nothing()
    {
        var rig = new Rig(GameMode.Heist);
        Assert.That(((IMatchObjective)new Heist(rig.Map, rig.Session, rig.Match, rig.Hud, rig.Hooks)).Markers(rig.Red1), Is.Empty);
    }

    // ==== Where markers go on the view ===========================================================

    // Looking east from (10, 10), the view 320x160
    private static RenderState LookingEast()
    {
        var render = new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, new byte[256]);
        (render.ViewX, render.ViewY, render.ViewCos, render.ViewSin) = ((int)(10 * Tile), (int)(10 * Tile), 0x10000, 0);
        (render.CenterX, render.CenterY, render.Scale, render.HeightNumerator, render.ViewEyeZ) = (160, 80, 256, 0x10000, 32);
        (render.ViewWidth, render.ViewHeight) = (320, 160);
        return render;
    }

    [Test]
    public void A_Marker_Ahead_Is_In_View_Over_Its_Spot()
    {
        var render = LookingEast();

        var (side, x, y) = ObjectiveMarkerDrawing.Place(render, 14 * Tile, 10 * Tile, ObjectiveMarkerDrawing.MarkerZ);

        Assert.That((side, x), Is.EqualTo((ObjectiveMarkerDrawing.Side.InView, 160)));
        Assert.That(y, Is.LessThan(80), "floating up over the spot");
    }

    [TestCase(12, 30, 1)]       // well south (to the right), out of view
    [TestCase(12, -10, -1)]     // well north
    [TestCase(4, 12, 1)]        // behind, a little south: turn right
    [TestCase(4, 8, -1)]        // behind, a little north: turn left
    [TestCase(4, 10, 1)]        // straight behind
    public void A_Marker_Out_Of_View_Goes_To_The_Side_To_Turn_To(int tilex, int tiley, int expected)
    {
        var render = LookingEast();

        var (side, _, y) = ObjectiveMarkerDrawing.Place(render, tilex * Tile, tiley * Tile, ObjectiveMarkerDrawing.MarkerZ);

        Assert.That(((int)side, y), Is.EqualTo((expected, 80)));
    }

    // ==== On the automap =========================================================================

    // A 300x160 automap view at (10, 20) on the screen
    private static AutomapDrawing.AutomapView MapView() => new(10, 20, 300, 160, 8, 32, 32, 1, 0, 1);

    [Test]
    public void An_Objective_On_The_Map_In_View_Stays_On_Its_Spot()
    {
        Assert.That(AutomapDrawing.PinInside(MapView(), 100, 50, 4), Is.EqualTo((100f, 50f, false)));
    }

    [Test]
    public void An_Objective_Off_The_Map_In_View_Is_Pulled_In_To_The_Edge_The_Way_It_Lies()
    {
        // Arrange: inside is x 14..305, y 24..175, its middle (159.5, 99.5)
        var view = MapView();

        // Act: far off to the right, level with the middle; and straight up
        var right = AutomapDrawing.PinInside(view, 2000, 99.5f, 4);
        var up = AutomapDrawing.PinInside(view, 159.5f, -500, 4);

        // Assert
        Assert.That(right.Pinned && up.Pinned, Is.True);
        Assert.That(right.X, Is.EqualTo(305f).Within(0.01f));
        Assert.That(right.Y, Is.EqualTo(99.5f).Within(0.01f));
        Assert.That(up.X, Is.EqualTo(159.5f).Within(0.01f));
        Assert.That(up.Y, Is.EqualTo(24f).Within(0.01f));
    }

    [Test]
    public void An_Objective_Pinned_To_The_Top_Stays_Under_The_Match_Line()
    {
        var (_, y, pinned) = AutomapDrawing.PinInside(MapView(), 159.5f, -500, 4, top: 20);
        Assert.That(pinned, Is.True);
        Assert.That(y, Is.EqualTo(44f).Within(0.01f));
    }

    [Test]
    public void Distance_Is_In_Whole_Tiles_And_Standing_On_It_Hides_It()
    {
        var render = LookingEast();
        Assert.That(ObjectiveMarkerDrawing.TilesAway(render, 13 * Tile, 14 * Tile), Is.EqualTo(5));
        Assert.That(ObjectiveMarkerDrawing.Underfoot(render, 10 * Tile + Tile / 2, 10 * Tile - Tile / 2), Is.True);
        Assert.That(ObjectiveMarkerDrawing.Underfoot(render, 11 * Tile, 10 * Tile), Is.False);
    }
}
