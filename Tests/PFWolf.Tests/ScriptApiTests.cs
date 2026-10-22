using Lua;
using PFWolf.Scripting;
// PFWolf.Actor, the legacy wall/door base class, would win over a plain Actor alias
using EntityActor = PFWolf.Entities.Actors.Actor;

namespace PFWolf.Tests;

/// <summary>
/// PFWolf.Common's ScriptApi (the editor's autocomplete and help) against what the engine really
/// gives scripts: when the API changes, the list has to change with it
/// </summary>
public class ScriptApiTests
{
    private readonly List<string> _printed = [];

    [SetUp]
    public void ClearPrinted() => _printed.Clear();

    private ScriptHost EngineHost()
    {
        var host = new ScriptHost(_printed.Add);
        GameHost.InstallScriptApi(host);
        return host;
    }

    [Test]
    public void The_Game_Table_Is_The_Lists()
    {
        // Arrange
        var host = EngineHost();
        var game = host.Globals["game"].Read<LuaTable>();

        // Act
        var names = new List<string>();
        foreach (var (key, _) in game)
            names.Add(key.Read<string>());

        // Assert
        Assert.That(names, Is.EquivalentTo(ScriptApi.Game.Select(entry => entry.Name)));
    }

    [Test]
    public void Every_Actor_Method_Listed_Is_There_And_Nothing_Else_Answers()
    {
        // Arrange: an actor not on any level (its methods aren't called, only looked up)
        var host = EngineHost();
        host.Globals["a"] = Scripting.LuaActor.Of(new EntityActor { Name = "Thing" });
        var names = string.Join(", ", ScriptApi.ActorMethods.Select(method => $"'{method.Name}'"));

        // Act
        host.Execute($"for _, name in ipairs({{{names}}}) do print(name, type(a[name])) end print('nothere', type(a.nothere))");

        // Assert: less the warning for nothere (told once, so only if no test has read it before)
        Assert.That(_printed.Where(line => !line.StartsWith("Script: ", StringComparison.Ordinal)),
            Is.EqualTo(ScriptApi.ActorMethods.Select(method => $"{method.Name}\tfunction").Append("nothere\tnil")));
    }

    [Test]
    public void A_Field_An_Actor_Hasnt_Got_Reads_As_Nil_And_Is_Told_Once()
    {
        // Arrange: a typo of health, read three times
        var host = EngineHost();
        host.Globals["a"] = Scripting.LuaActor.Of(new EntityActor { Name = "Thing" });

        // Act
        host.Execute("for i = 1, 3 do print(a.helth == nil) end");

        // Assert
        Assert.That(_printed, Is.EqualTo(new[]
        {
            "Script: an actor has no helth, so it reads as nil (the scripting reference lists an actor's fields and methods)",
            "true", "true", "true",
        }));
    }

    [Test]
    public void Every_Actor_Field_Listed_Answers()
    {
        // Arrange: inwall needs a level, so it's left out here
        var host = EngineHost();
        host.Globals["a"] = Scripting.LuaActor.Of(new EntityActor { Name = "Thing" });
        var fields = ScriptApi.ActorFields.Where(field => field.Name != "inwall").ToList();
        var names = string.Join(", ", fields.Select(field => $"'{field.Name}'"));

        // Act
        host.Execute($"for _, name in ipairs({{{names}}}) do print(name, a[name] ~= nil or name == 'shooter' or name == 'state' or name == 'faction' or name == 'translation') end");

        // Assert: shooter, state, faction and translation are nil for an actor that has none, but don't error
        Assert.That(_printed, Is.EqualTo(fields.Select(field => $"{field.Name}\ttrue")));
    }

    [Test]
    public void The_Settable_Fields_Are_The_Ones_That_Can_Be_Set()
    {
        // Arrange
        var host = EngineHost();
        host.Globals["a"] = Scripting.LuaActor.Of(new EntityActor { Name = "Thing" });

        // Act / Assert
        foreach (var field in ScriptApi.ActorFields)
        {
            var error = host.Execute($"a.{field.Name} = 1");
            Assert.That(error == null, Is.EqualTo(field.Settable), $"{field.Name}: {error}");
        }
    }

    [Test]
    public void The_Lua_Names_Listed_Are_In_The_Sandbox()
    {
        // Arrange
        var host = EngineHost();
        var names = ScriptApi.Globals.Select(entry => entry.Name)
            .Concat(ScriptApi.Libraries.Select(entry => entry.Name))
            .Concat(ScriptApi.LibraryMembers.Select(entry => $"{entry.Library}.{entry.Name}"))
            .ToList();

        // Act
        foreach (var name in names)
            host.Execute($"print('{name}', {name} ~= nil)");

        // Assert
        Assert.That(_printed, Is.EqualTo(names.Select(name => $"{name}\ttrue")));
    }

    [Test]
    public void Nothing_In_The_Sandbox_Is_Left_Off_The_Lists()
    {
        // Arrange
        var host = EngineHost();
        var listed = ScriptApi.Globals.Select(entry => entry.Name)
            .Concat(ScriptApi.Libraries.Select(entry => entry.Name))
            .Concat(ScriptApi.LibraryMembers.Select(entry => $"{entry.Library}.{entry.Name}"))
            .Concat(ScriptApi.Game.Select(entry => $"game.{entry.Name}"))
            .ToHashSet();

        // Act: each global, and each member of a library table
        var found = new List<string>();
        foreach (var (key, value) in host.Globals)
        {
            var name = key.Read<string>();
            found.Add(name);
            if (name != "_G" && value.TryRead<LuaTable>(out var library))
                foreach (var (member, _) in library)
                    found.Add($"{name}.{member.Read<string>()}");
        }
        var unlisted = found.Where(name => !listed.Contains(name)).ToList();

        // Assert
        Assert.That(unlisted, Is.Empty, "add these to PFWolf.Common's ScriptApi, or take them out of the sandbox (ScriptHost)");
    }

    [Test]
    public void The_Trigger_Actions_Are_The_Engines_Switch_Actions()
    {
        // Arrange
        GameHost.RegisterActorActions();

        // Act: less the Test_ names other tests register (the registries are shared, ActionRegistryTests)
        var engine = Entities.MapTriggerRegistry.Names.Where(name => !name.StartsWith("Test_", StringComparison.Ordinal)).ToList();

        // Assert
        Assert.That(ScriptApi.TriggerActions.Concat(ScriptApi.OwnTileActions), Is.EquivalentTo(engine));
        Assert.That(ScriptApi.OwnTileActions, Is.EquivalentTo(GameScripts.OwnTileActions.Keys));
    }

    [Test]
    public void Game_Trigger_Names_Every_Action_It_Cant_Run()
    {
        // Act
        var trigger = ScriptApi.Game.Single(function => function.Name == "trigger");

        // Assert: its help lists them by hand
        Assert.That(ScriptApi.OwnTileActions, Has.All.Matches<string>(action => trigger.Summary.Contains(action, StringComparison.Ordinal)));
    }
}
