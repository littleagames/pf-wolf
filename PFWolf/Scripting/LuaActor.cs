using System.Runtime.CompilerServices;
using Lua;

namespace PFWolf.Scripting;

/// <summary>
/// An actor as scripts see it: a Lua userdata whose fields and methods (`self.x`,
/// `self:jump("Fire")`) are Scripting/GameScripts.cs's metatable. Each actor has one, so the same
/// actor is the same value in Lua (`a == b`).
/// </summary>
internal sealed class LuaActor : ILuaUserData
{
    /// <summary>Every actor's metatable, set up with the scripts (GameHost.InstallActorApi)</summary>
    internal static LuaTable Meta { get; set; } = new();

    private static readonly ConditionalWeakTable<Entities.Actors.Actor, LuaActor> Wrappers = [];

    public Entities.Actors.Actor Actor { get; }

    private LuaActor(Entities.Actors.Actor actor) => Actor = actor;

    public LuaTable? Metatable
    {
        get => Meta;
        set { }
    }

    public Span<LuaValue> UserValues => [];

    /// <summary>The actor as a Lua value, or nil for none</summary>
    public static LuaValue Of(Entities.Actors.Actor? actor) =>
        actor == null ? LuaValue.Nil : LuaValue.FromUserData(Wrappers.GetValue(actor, a => new LuaActor(a)));
}
