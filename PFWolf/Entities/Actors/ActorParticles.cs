namespace PFWolf.Entities.Actors;

/// <summary>
/// An actor's emitters as it goes (Rendering/Particles.cs): its class's `particles` property
/// (read once), what A_StartEmitter / A_StopEmitter set in its place, and how far each emitter
/// is toward its next particle. Only the override is saved.
/// </summary>
internal sealed class ActorParticleState
{
    public bool Resolved;
    public List<string> ClassEmitters = [];
    public ParticleOverride? Override;

    // A share of a particle each emitter owes, from the tics it's run since its last one
    public Dictionary<string, double>? Owed;
}

/// <summary>
/// What A_StartEmitter / A_StopEmitter left an actor's emitters as, in place of its class's
/// `particles`: Off (A_StopEmitter with no name) stops them all, states' `particles:` too, until
/// A_StartEmitter; otherwise Names are the emitters running, states' `particles:` still
/// changing them frame by frame. Saved.
/// </summary>
internal sealed record ParticleOverride(bool Off, List<string> Names)
{
    const int MaxNames = 64;

    public void Write(BinaryWriter bw)
    {
        bw.Write(Off);
        bw.Write(Names.Count);
        foreach (var name in Names)
            bw.Write(name);
    }

    public static ParticleOverride Read(BinaryReader br)
    {
        bool off = br.ReadBoolean();
        int count = br.ReadInt32();
        if (count is < 0 or > MaxNames)
            throw new InvalidDataException("An actor's emitters are out of range.");
        var names = new List<string>(count);
        for (int i = 0; i < count; i++)
            names.Add(br.ReadString());
        return new ParticleOverride(off, names);
    }
}
