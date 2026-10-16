using PFWolf.Enums;

namespace PFWolf.Tests;

public class WeaponZoomTests
{
    // A rifle with a 6x scope that turns 4 times slower zoomed in, and a pistol with none
    private const string Yaml = """
        Inventory:
          properties:
            "inventory.amount": 1
            "inventory.maxamount": 1
        Weapon:
          parent: Inventory
        Rifle:
          parent: Weapon
          properties:
            "weapon.zoom": 6
            "weapon.zoomturn": 4
            "weapon.zoompic": SCOPE
        Pistol:
          parent: Weapon
        """;

    private sealed class Rig
    {
        public readonly GameSession Session = new();
        public readonly WeaponZoom Zoom;

        public Rig(string weapon)
        {
            var inventory = new InventoryManager(ActorDefs.Metadata(Yaml));
            inventory.SetHolder(Session.playerstate.Items);
            Zoom = new WeaponZoom(inventory, new AudioManager(new Lazy<AssetManager>(() => null!), Session), Session);
            Session.playerstate.weapon = weapon;
            Session.tics = 1;
        }

        // The zoom key down (pressed now, and held from before after the first tic) for that many tics
        public void Hold(int tics)
        {
            for (int i = 0; i < tics; i++)
            {
                Session.playerinput.SetPressed(buttontypes.bt_zoom, true);
                Session.playerinput.SetHeld(buttontypes.bt_zoom, i > 0);
                Zoom.Tick(reloading: false);
            }
        }

        public void LetGo()
        {
            Session.playerinput.SetPressed(buttontypes.bt_zoom, false);
            Session.playerinput.SetHeld(buttontypes.bt_zoom, false);
            Zoom.Tick(reloading: false);
        }
    }

    [Test]
    public void Holding_Zoom_Zooms_In_And_Letting_Go_Stays_There()
    {
        // Arrange
        var rig = new Rig("Rifle");

        // Act: 26 tics held is 25 of zooming (the first is the press), 50 of 100
        rig.Hold(26);
        rig.LetGo();

        // Assert
        Assert.That(rig.Session.playerstate.ZoomLevel, Is.EqualTo(50));
        Assert.That(rig.Session.playerstate.ZoomMode, Is.EqualTo(ZoomMode.Zoomed));
        Assert.That(rig.Zoom.Magnification(), Is.EqualTo(3.5));        // 1 + 5 * 50%
        Assert.That(rig.Session.playerstate.TurnSlowdown, Is.EqualTo(4));
        Assert.That(rig.Zoom.ScopePicture(), Is.EqualTo("SCOPE"));
    }

    [Test]
    public void It_Stops_At_The_Scopes_Most()
    {
        var rig = new Rig("Rifle");
        rig.Hold(80);
        Assert.That(rig.Session.playerstate.ZoomLevel, Is.EqualTo(WeaponZoom.MaxLevel));
        Assert.That(rig.Zoom.Magnification(), Is.EqualTo(6));
    }

    [Test]
    public void Held_Again_It_Zooms_Out_And_Let_Go_It_Is_Done()
    {
        // Arrange: zoomed
        var rig = new Rig("Rifle");
        rig.Hold(11);
        rig.LetGo();

        // Act / Assert: 5 a tic out
        rig.Hold(3);
        Assert.That(rig.Session.playerstate.ZoomMode, Is.EqualTo(ZoomMode.ZoomOut));
        Assert.That(rig.Session.playerstate.ZoomLevel, Is.EqualTo(10));
        rig.LetGo();
        Assert.That(rig.Session.playerstate.ZoomMode, Is.EqualTo(ZoomMode.Ready));
        Assert.That(rig.Zoom.Magnification(), Is.EqualTo(1));
        Assert.That(rig.Zoom.ScopePicture(), Is.Null);
        Assert.That(rig.Session.playerstate.TurnSlowdown, Is.EqualTo(1));
    }

    [Test]
    public void Reloading_Or_A_Weapon_Without_A_Scope_Drops_The_Zoom()
    {
        // Arrange
        var rig = new Rig("Rifle");
        rig.Hold(20);

        // Act / Assert
        rig.Zoom.Tick(reloading: true);
        Assert.That(rig.Session.playerstate.ZoomLevel, Is.Zero);

        rig.Hold(20);
        rig.Session.playerstate.weapon = "Pistol";
        rig.Zoom.Tick(reloading: false);
        Assert.That(rig.Session.playerstate.ZoomMode, Is.EqualTo(ZoomMode.Ready));
    }

    [Test]
    public void A_Weapon_Without_A_Scope_Never_Zooms()
    {
        var rig = new Rig("Pistol");
        rig.Hold(20);
        Assert.That(rig.Session.playerstate.ZoomLevel, Is.Zero);
        Assert.That(rig.Zoom.Magnification(), Is.EqualTo(1));
    }
}
