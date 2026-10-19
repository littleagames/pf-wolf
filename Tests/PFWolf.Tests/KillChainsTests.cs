using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

/// <summary>Kills in quick succession (Gameplay/KillChains.cs) and the enemies' health bars</summary>
public class KillChainsTests
{
    private const string Yaml = """
        Guard:
          properties:
            health: 25
        Dog:
          properties:
            health: 1
            killchain: noprize
        Commander:
          properties:
            health: 1000
            killchain: false
            healthbar: true
        """;

    private GameSession _session = null!;
    private InventoryManager _inventory = null!;
    private KillChainInfo _settings = null!;
    private List<string> _messages = null!, _held = null!;
    private List<(int X, int Y, string Item)> _drops = null!;
    private int _healed;
    private KillChains _chains = null!;

    [SetUp]
    public void Make()
    {
        _session = new GameSession();
        _inventory = new InventoryManager(ActorDefs.Metadata(Yaml));
        _settings = new KillChainInfo
        {
            Tics = 50,
            Messages = ["Double", "Triple", "Multi"],
            Prizes = [["Clip"], ["Shells if Shotgun"], ["heal 100"]],
            StealthPrize = 4,
        };
        (_messages, _held, _drops, _healed) = ([], [], [], 0);
        _chains = new KillChains(_session, new KillChainHooks(() => _settings, _messages.Add, _held.Contains,
            (x, y, item) => _drops.Add((x, y, item)), points => _healed += points));
    }

    private void KillAt(int time, string enemy = "Guard", bool unseen = false)
    {
        _session.GameState.TimeCount = time;
        _chains.Killed(_inventory.CreateActor(enemy)!, _session.PlayerState, 3, 4, unseen);
    }

    [Test]
    public void Quick_Kills_Make_A_Chain_With_Its_Messages_And_Prizes()
    {
        KillAt(100);
        KillAt(120);
        KillAt(160);

        Assert.That(_session.PlayerState.KillChain, Is.EqualTo(3));
        Assert.That(_messages, Is.EqualTo(new[] { "Double", "Triple" }));
        Assert.That(_drops, Is.EqualTo(new[] { (3, 4, "Clip") }));     // the triple's needs a shotgun
    }

    [Test]
    public void A_Kill_Too_Long_After_The_Last_Starts_Over()
    {
        KillAt(100);
        KillAt(150);

        Assert.That(_session.PlayerState.KillChain, Is.EqualTo(1));
        Assert.That(_messages, Is.Empty);
        Assert.That(_drops, Is.Empty);
    }

    [Test]
    public void Longer_Chains_Keep_The_Last_Message_And_Prize()
    {
        _held.Add("Shotgun");
        for (int i = 0; i < 6; i++)
            KillAt(100 + i * 10);

        Assert.That(_messages, Is.EqualTo(new[] { "Double", "Triple", "Multi", "Multi", "Multi" }));
        Assert.That(_drops, Is.EqualTo(new[] { (3, 4, "Clip"), (3, 4, "Shells") }));
        Assert.That(_healed, Is.EqualTo(300));
    }

    [Test]
    public void A_Noprize_Enemy_Carries_The_Chain_But_Drops_Nothing()
    {
        KillAt(100);
        KillAt(110, "Dog");

        Assert.That(_session.PlayerState.KillChain, Is.EqualTo(2));
        Assert.That(_messages, Is.EqualTo(new[] { "Double" }));
        Assert.That(_drops, Is.Empty);
    }

    [Test]
    public void One_Out_Of_Chains_Doesnt_Touch_Them()
    {
        KillAt(100);
        KillAt(110, "Commander");

        Assert.That(_session.PlayerState.KillChain, Is.EqualTo(1));
        Assert.That(_messages, Is.Empty);
    }

    [Test]
    public void Without_Settings_There_Are_No_Chains()
    {
        _settings = new KillChainInfo();
        KillAt(100);
        KillAt(110);

        Assert.That(_session.PlayerState.KillChain, Is.Zero);
        Assert.That(_messages, Is.Empty);
    }

    [Test]
    public void Unseen_Kills_Sometimes_Give_A_Prize_Of_Their_Own()
    {
        // A chain of 1 to 4 at random: over enough unseen single kills, every prize shows up
        _held.Add("Shotgun");
        for (int i = 0; i < 40; i++)
            KillAt(1000 * (i + 1), unseen: true);

        Assert.That(_drops.Select(d => d.Item).Distinct(), Is.EquivalentTo(new[] { "Clip", "Shells" }));
        Assert.That(_healed, Is.GreaterThan(0));
        Assert.That(_messages, Is.Empty);
    }

    [TestCase(1000, 1000, 25, 25)]
    [TestCase(500, 1000, 25, 12)]
    [TestCase(1, 1000, 25, 0)]
    [TestCase(-5, 1000, 25, 0)]
    [TestCase(10, 0, 25, 0)]
    public void A_Health_Bars_Width_Is_Its_Share_Of_The_Full(int left, int full, int width, int expected)
    {
        Assert.That(StatusBarDrawing.HealthBarWidth(left, full, width), Is.EqualTo(expected));
    }

    [Test]
    public void Health_Bars_Are_For_Living_Alerted_Enemies_That_Have_One()
    {
        // Arrange: a commander after the player, another not yet, and a guard after them
        var map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session, null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        Monster Add(string name, bool alerted, short hitpoints)
        {
            var monster = new Monster { Name = name, Properties = _inventory.CreateActor(name)!.Properties, Hitpoints = hitpoints };
            monster.RuntimeFlags |= ObjFlags.Shootable | (alerted ? ObjFlags.AttackMode : 0);
            map.GetActors().AddLast(monster);
            return monster;
        }
        Add("Commander", alerted: true, 400);
        Add("Commander", alerted: false, 1000);
        Add("Guard", alerted: true, 25);

        // Act / Assert
        Assert.That(map.HealthBars(), Is.EqualTo(new[] { (400, 1000) }));
    }
}
