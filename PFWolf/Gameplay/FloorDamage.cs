using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <param name="HurtPlayer">Hurts a player (no attacker: the floor)</param>
/// <param name="HurtMonster">Hurts an enemy</param>
/// <param name="Flats">The mapdefs flats (their damage table)</param>
/// <param name="FloorTexture">The texture a tile's floor shows (MapManager.FlatTextureName)</param>
internal sealed record FloorDamageHooks(Action<Entities.Actors.PlayerPawn, int> HurtPlayer, Action<Entities.Actors.Monster, int> HurtMonster,
    Func<Assets.MapFlatsTranslation> Flats, Func<int, int, string?> FloorTexture);

/// <summary>
/// Floors that hurt (mapdefs flats `damage`, by texture name: Midnight Assault's electrified
/// water, CheckDamagingFloors): every so many tics of the level clock, each living player and
/// (unless the floor says not) each shootable enemy standing on one takes its damage. A switch
/// turns one on or off by changing the floor's texture (A_ChangeTexture).
/// </summary>
internal sealed class FloorDamage(MapManager map, GameSession session, FloorDamageHooks hooks)
{
    int lastTime;

    /// <summary>Each tic, after everyone's moved</summary>
    internal void Tick()
    {
        int now = session.gamestate.TimeCount;
        int before = now < lastTime ? now : lastTime;     // a new level's clock starts over
        lastTime = now;
        var flats = hooks.Flats();
        if (flats.Damage.Count == 0 || now == before)
            return;

        foreach (var pawn in map.Players.ToList())
        {
            if (pawn.State.health > 0 && DamageAt(flats, pawn.TileX, pawn.TileY, before, now) is { } damage)
                hooks.HurtPlayer(pawn, damage.Amount);
        }

        foreach (var monster in map.GetActors().OfType<Entities.Actors.Monster>().ToList())
        {
            if (!monster.IsRemoved && monster.Hitpoints > 0 && monster.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE)
                && DamageAt(flats, monster.TileX, monster.TileY, before, now) is { Enemies: true } damage)
                hooks.HurtMonster(monster, damage.Amount);
        }
    }

    // The tile's floor's damage, if it hurts and its interval came round since the last tic
    Assets.MapFloorDamage? DamageAt(Assets.MapFlatsTranslation flats, int x, int y, int before, int now)
    {
        if (x is < 0 or >= MapManager.MAPSIZE || y is < 0 or >= MapManager.MAPSIZE)
            return null;
        if (flats.DamageFor(hooks.FloorTexture(x, y)) is not { Amount: > 0 } damage)
            return null;
        int every = Math.Max(damage.Tics, 1);
        return now / every > before / every ? damage : null;
    }
}
