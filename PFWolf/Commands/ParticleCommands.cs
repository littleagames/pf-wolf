using PFWolf.Configuration;
using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf.Commands;

/// <summary>Particle effects (Rendering/Particles.cs): the Particles setting, what there is, and a burst to look at</summary>
internal sealed class ParticleCommands(ConsoleManager console, Particles particles, GameSettings settings, GameSession session)
{
    internal void Register()
    {
        console.Register("r_particles", "How many particles a mod's effects throw out, as Options → Video's Particles: off, low, medium or high (0-3).",
            "r_particles [off|low|medium|high|0-3]", ParticlesSettingCommand, complete: Values("off", "low", "medium", "high"));
        console.Register("particles", "Lists particles.yaml's particle types and emitters, and how many particles there are now.",
            "particles", _ => ListParticles());
        console.Register("emitters", "Lists the actors running particles.yaml emitters (actordefs particles, states' particles:, A_StartEmitter), and which.",
            "emitters", _ => ListEmitters(), InLevel);
        console.Register("particle","Throws out a burst from a particles.yaml emitter or of a particle type, a tile in front of you; count in place of its own.",
            "particle <emitter|type> [count]", ParticleCommand, Cheat | InLevel,
            complete: (_, i) => i == 0 ? particles.Definitions.Emitters.Keys.Concat(particles.Definitions.Types.Keys).Order() : []);
    }

    void ParticlesSettingCommand(string[] args)
    {
        if (args.Length > 0)
            settings.Particles = ParseDetail(args[0]);
        console.Print($"r_particles {settings.Particles.ToString().ToLowerInvariant()} ({(int)settings.Particles})");
    }

    internal static ParticleDetail ParseDetail(string arg) => arg.Trim().ToLowerInvariant() switch
    {
        "0" or "off" => ParticleDetail.Off,
        "1" or "low" => ParticleDetail.Low,
        "2" or "medium" => ParticleDetail.Medium,
        "3" or "high" => ParticleDetail.High,
        _ => throw new ArgumentException($"expected off, low, medium or high (0-3), got \"{arg}\""),
    };

    void ListParticles()
    {
        var defs = particles.Definitions;
        console.Print($"{particles.Count} particles now, at most {Particles.MaxFor(settings.Particles)} (r_particles {settings.Particles.ToString().ToLowerInvariant()})");
        console.Print(defs.Types.Count == 0 ? "No particle types (particles.yaml)" : $"Types: {string.Join(", ", defs.Types.Keys.Order())}");
        if (defs.Emitters.Count > 0)
            console.Print($"Emitters: {string.Join(", ", defs.Emitters.Keys.Order())}");
    }

    void ListEmitters()
    {
        int shown = 0;
        foreach (var line in particles.DescribeEmitters())
        {
            console.Print(line);
            shown++;
        }
        if (shown == 0)
            console.Print("No actors run emitters");
    }

    void ParticleCommand(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: particle <emitter|type> [count]");
        int? count = args.Length > 1 ? ParseInt(args[1], 1, 4096) : null;

        // a tile in front of the player, facing back at them
        var player = session.Player;
        double angle = player.Angle * Math.PI / 180;
        const double tile = GameConstants.TextureSize;
        double x = player.X / (double)(MapConstants.TileGlobal / GameConstants.TextureSize) + Math.Cos(angle) * tile;
        double y = player.Y / (double)(MapConstants.TileGlobal / GameConstants.TextureSize) - Math.Sin(angle) * tile;

        int made = particles.Burst(args[0], x, y, 0, player.Angle + 180, count);
        if (made < 0)
            throw new ArgumentException($"no emitter or particle type named {args[0]} in particles.yaml");
        console.Print(settings.Particles == ParticleDetail.Off ? "Particles are off (r_particles)" : $"{made} particles");
    }
}
