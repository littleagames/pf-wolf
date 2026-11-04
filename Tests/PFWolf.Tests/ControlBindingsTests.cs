using PFWolf.Configuration;

namespace PFWolf.Tests;

public class ControlBindingsTests
{
    private static readonly ControlAction Attack = ControlAction.Of(ButtonType.Attack);
    private static readonly ControlAction Use = ControlAction.Of(ButtonType.Use);
    private static readonly ControlAction ZoomIn = ControlAction.Of(AutomapAction.ZoomIn);

    private static ControlBindings Empty()
    {
        var bindings = new ControlBindings();
        bindings.ClearAll();
        return bindings;
    }

    [TestCase("attack")]
    [TestCase("ATTACK")]
    [TestCase("slot1")]
    [TestCase("am_zoomin")]
    [TestCase("toggleheartbeat")]      // past bit 31 of a TicCmd's buttons
    public void ControlAction_TryParse_Reads_Its_Own_Names(string name)
    {
        // Act
        var parsed = ControlAction.TryParse(name, out var action);

        // Assert
        Assert.That(parsed, Is.True);
        Assert.That(action.Name, Is.EqualTo(name).IgnoreCase);
    }

    [Test]
    public void Every_Control_Keeps_The_Name_Controls_Cfg_Has_For_It()
    {
        // controls.cfg and menudefs' customize screens name the controls: a file format
        string[] names =
        [
            "attack", "strafe", "run", "use", "slot1", "slot2", "slot3", "slot4", "nextweapon", "prevweapon",
            "esc", "pause", "strafeleft", "straferight", "moveforward", "movebackward", "turnleft", "turnright",
            "automap", "slot5", "slot6", "slot7", "slot8", "slot9", "slot0", "lookup", "lookdown", "centerview",
            "radarzoomin", "radarzoomout", "reload", "zoom",
            "quickleft", "quickright", "turnaround", "toggleattackinfo", "togglelighting", "togglesound",
            "togglemusic", "toggleceilings", "togglefloors", "toggleheartbeat",
            "am_zoomin", "am_zoomout", "am_panup", "am_pandown", "am_panleft", "am_panright", "am_center",
            "am_follow", "am_rotate", "am_style", "am_overlay", "am_grid", "am_stats",
        ];

        Assert.That(ControlAction.All.Select(a => a.Name), Is.EqualTo(names));
    }

    [Test]
    public void ControlAction_TryParse_Reads_Old_Weapon_Names()
    {
        // Act
        var parsed = ControlAction.TryParse("readypistol", out var action);

        // Assert
        Assert.That(parsed, Is.True);
        Assert.That(action, Is.EqualTo(ControlAction.Of(ButtonType.Slot2)));
    }

    [Test]
    public void ControlAction_TryParse_Rejects_Unknown_Names()
    {
        Assert.That(ControlAction.TryParse("fly", out _), Is.False);
    }

    [Test]
    public void Every_Action_Name_Round_Trips()
    {
        foreach (var action in ControlAction.All)
        {
            Assert.That(ControlAction.TryParse(action.Name, out var parsed), Is.True, action.Name);
            Assert.That(parsed, Is.EqualTo(action), action.Name);
        }
    }

    [Test]
    public void Add_Fills_Key_Slots_Then_Refuses()
    {
        // Arrange
        var bindings = Empty();

        // Act
        var first = bindings.Add(Attack, InputCode.FromKey(ScanCodes.A));
        var second = bindings.Add(Attack, InputCode.FromKey(ScanCodes.B));
        var third = bindings.Add(Attack, InputCode.FromKey(ScanCodes.C));

        // Assert
        Assert.That(new[] { first, second, third }, Is.EqualTo(new[] { true, true, false }));
        Assert.That(bindings.Get(Attack).Count(), Is.EqualTo(2));
    }

    [Test]
    public void Add_Puts_Controller_Inputs_In_The_Controller_Slot()
    {
        // Arrange
        var bindings = Empty();
        var pad = InputCode.FromJoyButton(3);

        // Act
        bindings.Add(Attack, pad);

        // Assert
        Assert.That(bindings[Attack, ControlBindings.ControllerSlot], Is.EqualTo(pad));
        Assert.That(bindings[Attack, 0].IsNone, Is.True);
    }

    [Test]
    public void Add_Of_An_Input_Already_Bound_Succeeds_Without_Using_A_Slot()
    {
        // Arrange
        var bindings = Empty();
        var key = InputCode.FromKey(ScanCodes.A);
        bindings.Add(Attack, key);

        // Act
        var added = bindings.Add(Attack, key);

        // Assert
        Assert.That(added, Is.True);
        Assert.That(bindings.Get(Attack).Count(), Is.EqualTo(1));
    }

    [Test]
    public void Indexer_Refuses_A_Key_In_The_Controller_Slot()
    {
        // Arrange
        var bindings = Empty();

        // Act / Assert
        Assert.Throws<ArgumentException>(() => bindings[Attack, ControlBindings.ControllerSlot] = InputCode.FromKey(ScanCodes.A));
        Assert.Throws<ArgumentException>(() => bindings[Attack, 0] = InputCode.FromJoyButton(0));
    }

    [Test]
    public void Set_Takes_The_Input_Off_Other_Play_Buttons()
    {
        // Arrange
        var bindings = Empty();
        var key = InputCode.FromKey(ScanCodes.A);
        bindings.Add(Use, key);

        // Act
        var takenFrom = bindings.Set(Attack, 0, key);

        // Assert
        Assert.That(takenFrom, Is.EqualTo(new[] { Use }));
        Assert.That(bindings.IsBound(Use, key), Is.False);
        Assert.That(bindings[Attack, 0], Is.EqualTo(key));
    }

    [Test]
    public void Set_Leaves_The_Input_On_Automap_Keys()
    {
        // Arrange: the automap's keys only work while it's open, so they can share
        var bindings = Empty();
        var key = InputCode.FromKey(ScanCodes.Equal);
        bindings.Add(ZoomIn, key);

        // Act
        var takenFrom = bindings.Set(Attack, 0, key);

        // Assert
        Assert.That(takenFrom, Is.Empty);
        Assert.That(bindings.IsBound(ZoomIn, key), Is.True);
    }

    [Test]
    public void Set_Moves_The_Input_Between_The_Actions_Own_Slots()
    {
        // Arrange
        var bindings = Empty();
        var key = InputCode.FromKey(ScanCodes.A);
        bindings[Attack, 0] = key;

        // Act
        var takenFrom = bindings.Set(Attack, 1, key);

        // Assert
        Assert.That(takenFrom, Is.Empty);
        Assert.That(bindings[Attack, 0].IsNone, Is.True);
        Assert.That(bindings[Attack, 1], Is.EqualTo(key));
    }

    [Test]
    public void Clear_By_Device_Keeps_The_Others()
    {
        // Arrange
        var bindings = Empty();
        bindings.Add(Attack, InputCode.FromKey(ScanCodes.A));
        bindings.Add(Attack, InputCode.FromMouseButton(1));
        bindings.Add(Attack, InputCode.FromJoyButton(0));

        // Act
        bindings.Clear(Attack, InputDevice.MouseButton);

        // Assert
        Assert.That(bindings.Get(Attack), Is.EqualTo(new[] { InputCode.FromKey(ScanCodes.A), InputCode.FromJoyButton(0) }));
    }

    [Test]
    public void FormatInputs_Quotes_Each_Input_Or_Says_None()
    {
        // Arrange
        var bindings = Empty();
        bindings.Add(Attack, InputCode.FromMouseButton(1));
        bindings.Add(Attack, InputCode.WheelUp);

        // Act / Assert
        Assert.That(bindings.FormatInputs(Attack), Is.EqualTo("\"Mouse 1\" \"Wheel Up\""));
        Assert.That(bindings.FormatInputs(Use), Is.EqualTo("none"));
    }

    [Test]
    public void GetCommands_Has_One_Line_Per_Action()
    {
        // Act
        var commands = new ControlBindings().GetCommands().ToList();

        // Assert
        Assert.That(commands, Has.Count.EqualTo(ControlAction.Count));
        Assert.That(commands, Has.All.StartWith("bindaction "));
    }

    [Test]
    public void Defaults_Bind_Attack_To_Ctrl_And_Left_Mouse()
    {
        // Act
        var bindings = new ControlBindings();

        // Assert
        Assert.That(bindings.IsBound(Attack, InputCode.FromKey(ScanCodes.Control)), Is.True);
        Assert.That(bindings.IsBound(Attack, InputCode.FromMouseButton(1)), Is.True);
    }

    [Test]
    public void Defaults_Never_Put_An_Input_On_Two_Play_Buttons()
    {
        // Arrange
        var bindings = new ControlBindings();

        // Act
        var duplicates = ControlAction.All
            .Where(action => action.IsButton)
            .SelectMany(action => bindings.Get(action))
            .GroupBy(code => code)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.ToString());

        // Assert
        Assert.That(duplicates, Is.Empty);
    }

    [Test]
    public void Game_Defaults_Replace_A_Listed_Controls_Inputs()
    {
        // Arrange
        var bindings = new ControlBindings();
        var strafeLeft = ControlAction.Of(ButtonType.StrafeLeft);
        var slot9 = ControlAction.Of(ButtonType.Slot9);

        // Act: Midnight Assault's A to strafe left and M for the medkit, in place of 9
        var problems = bindings.SetGameDefaults(new Dictionary<string, List<string>>
        {
            ["strafeleft"] = ["A"],
            ["slot9"] = ["M"],
        });

        // Assert
        Assert.That(problems, Is.Empty);
        Assert.That(bindings.Get(strafeLeft), Is.EqualTo(new[] { InputCode.FromKey(ScanCodes.A) }));
        Assert.That(bindings.Get(slot9), Is.EqualTo(new[] { InputCode.FromKey(ScanCodes.M) }));
        Assert.That(bindings.Get(Attack), Does.Contain(InputCode.FromKey(ScanCodes.Control)), "unlisted controls keep the engine's");
    }

    [Test]
    public void Game_Defaults_Take_Their_Key_Off_Other_Play_Buttons()
    {
        // Arrange
        var bindings = new ControlBindings();
        var zoom = ControlAction.Of(ButtonType.Zoom);

        // Act: Z, the engine's zoom key, given to use
        bindings.SetGameDefaults(new Dictionary<string, List<string>> { ["use"] = ["Z"] });

        // Assert
        Assert.That(bindings.Get(Use), Is.EqualTo(new[] { InputCode.FromKey(ScanCodes.Z) }));
        Assert.That(bindings.Get(zoom), Does.Not.Contain(InputCode.FromKey(ScanCodes.Z)));
    }

    [Test]
    public void Game_Defaults_Are_What_Reset_Puts_Back()
    {
        // Arrange
        var bindings = new ControlBindings();
        var strafeLeft = ControlAction.Of(ButtonType.StrafeLeft);
        bindings.SetGameDefaults(new Dictionary<string, List<string>> { ["strafeleft"] = ["A"] });
        bindings.Clear(strafeLeft);

        // Act
        bindings.SetDefaults();

        // Assert
        Assert.That(bindings.Get(strafeLeft), Is.EqualTo(new[] { InputCode.FromKey(ScanCodes.A) }));
    }

    [Test]
    public void Game_Defaults_An_Empty_List_Leaves_A_Control_Unbound()
    {
        // Arrange
        var bindings = new ControlBindings();
        var lookUp = ControlAction.Of(ButtonType.LookUp);

        // Act
        bindings.SetGameDefaults(new Dictionary<string, List<string>> { ["lookup"] = [] });

        // Assert
        Assert.That(bindings.Get(lookUp), Is.Empty);
    }

    [Test]
    public void Game_Defaults_Report_And_Leave_Out_What_They_Cant_Read()
    {
        // Arrange
        var bindings = new ControlBindings();

        // Act
        var problems = bindings.SetGameDefaults(new Dictionary<string, List<string>>
        {
            ["fly"] = ["F"],
            ["use"] = ["Nonsense"],
            ["attack"] = ["A", "B", "C"],
        });

        // Assert
        Assert.That(problems, Has.Count.EqualTo(3));
        Assert.That(bindings.Get(Attack), Does.Contain(InputCode.FromKey(ScanCodes.Control)), "too many inputs: left as the engine's");
    }
}
