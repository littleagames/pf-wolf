namespace PFWolf.Networking;

/// <summary>
/// One player in full, as the host sends the game to someone joining mid-game
/// (Networking/MidGameJoin.cs WriteNetState): what a save holds for them, and everything else of theirs
/// that decides how the game goes on.
/// </summary>
internal static class NetPlayerState
{
    /// <param name="indexOf">An actor's place among the level's saved actors, or -1 for none</param>
    internal static void Write(BinaryWriter bw, Entities.PlayerState p, Func<Entities.Actors.Actor?, int> indexOf)
    {
        bw.Write(p.Name ?? "");
        bw.Write(p.Gone);
        bw.Write(p.PlayerClass);
        bw.Write(p.Color ?? "");
        bw.Write(p.OldScore);
        bw.Write(p.Score);
        bw.Write(p.NextExtra);
        bw.Write(p.Lives);
        bw.Write(p.Health);
        bw.Write(p.Armor);
        bw.Write(p.ArmorPercent);
        bw.Write(p.Weapon ?? "");
        bw.Write(p.ChosenWeapon ?? "");
        bw.Write(p.FaceFrame);
        bw.Write(p.KillX);
        bw.Write(p.KillY);
        bw.Write(p.Frags);
        bw.Write(p.Kills);
        bw.Write(p.Team);
        bw.Write(p.Objective);
        bw.Write(p.Items.Count);
        foreach (var (item, count) in p.Items)
        {
            bw.Write(item);
            bw.Write(count);
        }
        bw.Write(p.ThrustSpeed);
        bw.Write(p.PlUX);
        bw.Write(p.PlUY);
        bw.Write(p.AngleFrac);
        bw.Write(indexOf(p.LastAttacker));
        bw.Write(p.FaceCount);
        bw.Write(p.FaceTimes);
        bw.Write(p.WeaponCharge);
        bw.Write(p.Pitch);
        bw.Write(p.EyeZ);
        bw.Write(p.DeadTics);
        bw.Write(p.DeathAngle);
        bw.Write(p.Input.Buttons);
        bw.Write(p.WeaponSprite != null);
        if (p.WeaponSprite != null)
            Entities.Actors.ActorSnapshot.Capture(p.WeaponSprite).Write(bw);
    }

    /// <param name="playerColor">The color as this machine has it: the translation, or null if there's no such one</param>
    /// <param name="lastAttacker">Who last hurt them, as a place among the level's saved actors (-1 for none)</param>
    /// <param name="weapon">The weapon in hand's state, if they had one</param>
    internal static void Read(BinaryReader br, Entities.PlayerState p, Func<string, string?> playerColor,
        out int lastAttacker, out Entities.Actors.ActorSnapshot? weapon)
    {
        string? NullIfEmpty(string s) => s.Length == 0 ? null : s;

        p.Name = NullIfEmpty(br.ReadString());
        p.Gone = br.ReadBoolean();
        p.PlayerClass = br.ReadString();
        p.Color = playerColor(br.ReadString());
        p.OldScore = br.ReadInt32();
        p.Score = br.ReadInt32();
        p.NextExtra = br.ReadInt32();
        p.Lives = br.ReadInt16();
        p.Health = br.ReadInt16();
        p.Armor = br.ReadInt16();
        p.ArmorPercent = br.ReadInt16();
        p.Weapon = NullIfEmpty(br.ReadString());
        p.ChosenWeapon = NullIfEmpty(br.ReadString());
        p.FaceFrame = br.ReadInt16();
        p.KillX = br.ReadInt32();
        p.KillY = br.ReadInt32();
        p.Frags = br.ReadInt32();
        p.Kills = br.ReadInt32();
        p.Team = br.ReadInt32();
        p.Objective = br.ReadInt32();
        for (int i = br.ReadInt32(); i > 0; i--)
            p.Items[br.ReadString()] = br.ReadInt32();
        p.ThrustSpeed = br.ReadInt32();
        p.PlUX = br.ReadUInt16();
        p.PlUY = br.ReadUInt16();
        p.AngleFrac = br.ReadInt16();
        lastAttacker = br.ReadInt32();
        p.FaceCount = br.ReadInt32();
        p.FaceTimes = br.ReadInt32();
        p.WeaponCharge = br.ReadInt32();
        p.Pitch = br.ReadDouble();
        p.EyeZ = br.ReadInt32();
        p.DeadTics = br.ReadInt32();
        p.DeathAngle = br.ReadInt16();
        p.Input.RestoreButtons(br.ReadUInt32());
        weapon = br.ReadBoolean() ? Entities.Actors.ActorSnapshot.Read(br) : null;
    }
}
