using PFWolf.Configuration;

namespace PFWolf.Tests;

public class ControlBindingsTests
{
    private static readonly ControlAction Attack = ControlAction.Of(buttontypes.bt_attack);
    private static readonly ControlAction Use = ControlAction.Of(buttontypes.bt_use);
    private static readonly ControlAction ZoomIn = ControlAction.Of(Program.automapkeys.am_zoomin);

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
    public void ControlAction_TryParse_Reads_Its_Own_Names(string name)
    {
        // Act
        var parsed = ControlAction.TryParse(name, out var action);

        // Assert
        Assert.That(parsed, Is.True);
        Assert.That(action.Name, Is.EqualTo(name).IgnoreCase);
    }

    [Test]
    public void ControlAction_TryParse_Reads_Old_Weapon_Names()
    {
        // Act
        var parsed = ControlAction.TryParse("readypistol", out var action);

        // Assert
        Assert.That(parsed, Is.True);
        Assert.That(action, Is.EqualTo(ControlAction.Of(buttontypes.bt_slot2)));
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
        var first = bindings.Add(Attack, InputCode.FromKey(ScanCodes.sc_A));
        var second = bindings.Add(Attack, InputCode.FromKey(ScanCodes.sc_B));
        var third = bindings.Add(Attack, InputCode.FromKey(ScanCodes.sc_C));

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
        var key = InputCode.FromKey(ScanCodes.sc_A);
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
        Assert.Throws<ArgumentException>(() => bindings[Attack, ControlBindings.ControllerSlot] = InputCode.FromKey(ScanCodes.sc_A));
        Assert.Throws<ArgumentException>(() => bindings[Attack, 0] = InputCode.FromJoyButton(0));
    }

    [Test]
    public void Set_Takes_The_Input_Off_Other_Play_Buttons()
    {
        // Arrange
        var bindings = Empty();
        var key = InputCode.FromKey(ScanCodes.sc_A);
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
        var key = InputCode.FromKey(ScanCodes.sc_Equal);
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
        var key = InputCode.FromKey(ScanCodes.sc_A);
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
        bindings.Add(Attack, InputCode.FromKey(ScanCodes.sc_A));
        bindings.Add(Attack, InputCode.FromMouseButton(1));
        bindings.Add(Attack, InputCode.FromJoyButton(0));

        // Act
        bindings.Clear(Attack, InputDevice.MouseButton);

        // Assert
        Assert.That(bindings.Get(Attack), Is.EqualTo(new[] { InputCode.FromKey(ScanCodes.sc_A), InputCode.FromJoyButton(0) }));
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
        Assert.That(bindings.IsBound(Attack, InputCode.FromKey(ScanCodes.sc_Control)), Is.True);
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
}
