using PFWolf.Gameplay;

namespace PFWolf.Tests;

/// <summary>Doors locked by several items (doors.yaml `locks:` and `locks-needed`)</summary>
public class DoorLocksTests
{
    private const string Yaml = """
        Inventory: {}
        Key:
          parent: Inventory
        GoldKey: { parent: Key }
        SilverKey: { parent: Key }
        RedKey: { parent: Key }
        BlueKey: { parent: Key }
        """;

    private static readonly List<string> AllFour = ["GoldKey", "SilverKey", "RedKey", "BlueKey"];

    private InventoryManager _inventory = null!;
    private Doors _doors = null!;

    [SetUp]
    public void Make()
    {
        var session = new GameSession();
        _inventory = new InventoryManager(ActorDefs.Metadata(Yaml));
        _inventory.SetHolder(session.PlayerState.Items);
        _doors = new Doors(null!, null!, null!, _inventory, null!, session, null!);
    }

    private void Carry(params string[] keys)
    {
        foreach (var key in keys)
            _inventory.Give(key, 1);
    }

    [Test]
    public void A_door_needing_two_keys_opens_only_with_both()
    {
        var door = new DoorObject { MapLocks = ["RedKey", "BlueKey"] };
        Carry("RedKey");

        Assert.That(_doors.ItemsToOpen(door, out var missing), Is.Null);
        Assert.That(missing, Is.EqualTo("BlueKey"));

        Carry("BlueKey");
        Assert.That(_doors.ItemsToOpen(door, out _), Is.EqualTo(new[] { "RedKey", "BlueKey" }));
    }

    [Test]
    public void A_door_needing_all_four_stays_shut_with_three()
    {
        var door = new DoorObject { MapLocks = AllFour };
        Carry("GoldKey", "RedKey", "BlueKey");

        Assert.That(_doors.ItemsToOpen(door, out var missing), Is.Null);
        Assert.That(missing, Is.EqualTo("SilverKey"));

        Carry("SilverKey");
        Assert.That(_doors.ItemsToOpen(door, out _), Has.Count.EqualTo(4));
    }

    [Test]
    public void Locks_needed_takes_any_of_them()
    {
        var door = new DoorObject { MapLocks = AllFour, LocksNeeded = 2 };
        Carry("BlueKey");
        Assert.That(_doors.ItemsToOpen(door, out _), Is.Null);

        Carry("SilverKey", "RedKey");
        // only as many as it needs, the first carried in its list (what a takes-key door uses up)
        Assert.That(_doors.ItemsToOpen(door, out _), Is.EqualTo(new[] { "SilverKey", "RedKey" }));
        Assert.That(door.LocksRequired, Is.EqualTo(2));
    }

    [Test]
    public void A_lock_is_needed_on_top_of_the_locks()
    {
        var door = new DoorObject { MapLock = "GoldKey", MapLocks = ["RedKey"] };
        Carry("RedKey");
        Assert.That(_doors.ItemsToOpen(door, out var missing), Is.Null);
        Assert.That(missing, Is.EqualTo("GoldKey"));

        Carry("GoldKey");
        Assert.That(_doors.ItemsToOpen(door, out _), Is.EqualTo(new[] { "GoldKey", "RedKey" }));
    }

    [Test]
    public void Locks_needed_past_the_count_needs_them_all()
    {
        var door = new DoorObject { MapLocks = ["RedKey", "BlueKey"], LocksNeeded = 5 };
        Assert.That(door.LocksRequired, Is.EqualTo(2));
    }

    [Test]
    public void A_multi_key_door_counts_as_locked_until_unlocked()
    {
        var door = new DoorObject { MapLocks = ["RedKey", "BlueKey"] };
        Assert.That(door.IsLocked, Is.True);
        Assert.That(door.Lock, Is.Empty);

        door.Unlocked = true;
        Assert.That(door.IsLocked, Is.False);
    }

    [Test]
    public void Doors_yaml_reads_locks_and_locks_needed()
    {
        var defs = PFWolf.Loaders.YamlDataEntryLoader.Deserialize<PFWolf.Assets.MapObjectTranslationAsset>("""
            doors:
              '80': { North: DOOR3_1, South: DOOR3_1, locks: [GoldKey, SilverKey, RedKey, BlueKey], locks-needed: 2 }
            """);
        Assert.That(defs.Doors[80].Locks, Is.EqualTo(AllFour));
        Assert.That(defs.Doors[80].LocksNeeded, Is.EqualTo(2));
    }
}
