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
        Program.InstallScriptApi(host);
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

        // Assert
        Assert.That(_printed, Is.EqualTo(ScriptApi.ActorMethods.Select(method => $"{method.Name}\tfunction").Append("nothere\tnil")));
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
    public void The_Trigger_Actions_Are_The_Engines_Switch_Actions()
    {
        // Arrange
        Program.RegisterActorActions();

        // Act: less the Test_ names other tests register (the registries are shared, ActionRegistryTests)
        var engine = Entities.MapTriggerRegistry.Names.Where(name => !name.StartsWith("Test_", StringComparison.Ordinal)).ToList();

        // Assert
        Assert.That(ScriptApi.TriggerActions.Concat(ScriptApi.OwnTileActions), Is.EquivalentTo(engine));
        Assert.That(ScriptApi.OwnTileActions, Is.EquivalentTo(GameScripts.OwnTileActions.Keys));
    }
}
