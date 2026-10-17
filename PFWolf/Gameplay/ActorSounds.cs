using PFWolf.Extensions;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>
/// The sounds actors make from their actordefs properties and states: death cries, an active
/// sound heard only where the player can hear it, and A_PlaySound.
/// </summary>
/// <param name="playSoundAt">A sound from where an actor is</param>
internal sealed class ActorSounds(
    MapManager map,
    AudioManager audio,
    GameSession session,
    Action<string, Entities.Actors.Actor> playSoundAt)
{
    internal void DeathScream(Entities.Actors.Actor ob) => PlayActorSound(ob, "deathsound");

    /// <summary>
    /// Plays an actor's sound property from where it is: one sound, or a list to pick one from
    /// at random (Blake Stone's guards have more than one death cry)
    /// </summary>
    internal void PlayActorSound(Entities.Actors.Actor ob, string key)
    {
        var sounds = ob.PropertyStrings(key);
        if (sounds.Count > 0)
            playSoundAt(sounds[sounds.Count == 1 ? 0 : session.Random.RndT() % sounds.Count], ob);
    }

    // A_ActiveSound: the actor's "activesound" (Mecha Hitler's stomp), only where the player can
    // hear it -- in an area connected to theirs, as vanilla's A_MechaSound was
    internal void ActiveSound(Entities.Actors.Actor ob)
    {
        if (ob.Properties.TryGetValue("activesound", out var sound) && sound is string soundName
            && (ob.AreaNumber >= map.Floors.NumAreas || session.Areas.AreaByPlayer[ob.AreaNumber] != 0))
            playSoundAt(soundName, ob);
    }

    // A_PlaySound("angel/breath"): plays a sound, not placed in the world; with "positional"
    // (A_PlaySound("misc/yeah", "positional")), from where the actor is
    internal void PlaySound(Entities.Actors.Actor ob, string[] args)
    {
        if (args.Length == 0)
            return;
        if (args.Skip(1).Any(a => a.Equals("positional", StringComparison.OrdinalIgnoreCase)))
            playSoundAt(args[0], ob);
        else
            audio.Play(args[0]);
    }
}
