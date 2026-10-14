namespace PFWolf.SaveGames;

internal enum SaveKind { Normal, Quick, Auto }

/// <summary>
/// A mod a save was made with: its modinfo.yaml name (or file name) and version, and the file
/// or folder it was loaded from
/// </summary>
internal sealed record SavedMod(string Name, string Version, string FileName)
{
    public override string ToString() => Version.Length == 0 ? Name : $"{Name} {Version}";

    /// <summary>
    /// Whether two lists name the same mods, by name and version, in the same order (a
    /// different order can change which mod's files win)
    /// </summary>
    public static bool SameMods(IReadOnlyList<SavedMod> a, IReadOnlyList<SavedMod> b)
        => a.Count == b.Count && a.Zip(b).All(pair =>
            string.Equals(pair.First.Name, pair.Second.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(pair.First.Version, pair.Second.Version, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A save's picture of the game: game palette indices, a row at a time.</summary>
internal sealed record SaveThumbnail(int Width, int Height, byte[] Pixels);

/// <summary>
/// A save's header: what the load/save menus show about it, read without loading the game.
/// Map and skill names are kept as they read when saved, so a save from another game pack
/// or mod still shows them.
/// </summary>
internal sealed record SaveInfo
{
    /// <summary>The file it was read from; not stored in it.</summary>
    public string Path { get; init; } = "";

    public string Name { get; init; } = "";
    public DateTime SavedAt { get; init; }      // UTC
    public string GamePack { get; init; } = "";
    public string MapOn { get; init; } = "";
    public string MapName { get; init; } = "";
    public short Difficulty { get; init; }      // the skill's place in game-info's skills
    public string SkillName { get; init; } = "";
    public int LevelTime { get; init; }         // tics on this level
    public int PlayTime { get; init; }          // tics since the game began
    public int Score { get; init; }
    public short Lives { get; init; }
    public short Health { get; init; }
    public short Kills { get; init; }
    public short KillTotal { get; init; }
    public short Secrets { get; init; }
    public short SecretTotal { get; init; }
    public short Treasure { get; init; }
    public short TreasureTotal { get; init; }

    /// <summary>The mods loaded when it was saved, in load order</summary>
    public List<SavedMod> Mods { get; init; } = [];

    // Anything claiming more than this is garbage
    private const int MaxMods = 1000;

    public SaveKind Kind => System.IO.Path.GetFileName(Path).ToLowerInvariant() switch
    {
        SaveFile.QuickSaveFile => SaveKind.Quick,
        SaveFile.AutoSaveFile => SaveKind.Auto,
        _ => SaveKind.Normal,
    };

    public void Write(BinaryWriter bw)
    {
        bw.Write(Name);
        bw.Write(SavedAt.ToUniversalTime().Ticks);
        bw.Write(GamePack);
        bw.Write(MapOn);
        bw.Write(MapName);
        bw.Write((short)Difficulty);
        bw.Write(SkillName);
        bw.Write(LevelTime);
        bw.Write(PlayTime);
        bw.Write(Score);
        bw.Write(Lives);
        bw.Write(Health);
        bw.Write(Kills);
        bw.Write(KillTotal);
        bw.Write(Secrets);
        bw.Write(SecretTotal);
        bw.Write(Treasure);
        bw.Write(TreasureTotal);

        bw.Write(Mods.Count);
        foreach (var mod in Mods)
        {
            bw.Write(mod.Name);
            bw.Write(mod.Version);
            bw.Write(mod.FileName);
        }
    }

    public static SaveInfo Read(BinaryReader br)
    {
        var name = br.ReadString();
        var ticks = br.ReadInt64();
        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            throw new InvalidDataException($"Bad save time {ticks}.");

        var info = ReadStats(br, name, ticks);

        var modCount = br.ReadInt32();
        if (modCount < 0 || modCount > MaxMods)
            throw new InvalidDataException($"Bad mod count {modCount}.");
        for (int i = 0; i < modCount; i++)
            info.Mods.Add(new SavedMod(br.ReadString(), br.ReadString(), br.ReadString()));

        return info;
    }

    private static SaveInfo ReadStats(BinaryReader br, string name, long ticks)
    {
        return new SaveInfo
        {
            Name = name,
            SavedAt = new DateTime(ticks, DateTimeKind.Utc),
            GamePack = br.ReadString(),
            MapOn = br.ReadString(),
            MapName = br.ReadString(),
            Difficulty = br.ReadInt16(),
            SkillName = br.ReadString(),
            LevelTime = br.ReadInt32(),
            PlayTime = br.ReadInt32(),
            Score = br.ReadInt32(),
            Lives = br.ReadInt16(),
            Health = br.ReadInt16(),
            Kills = br.ReadInt16(),
            KillTotal = br.ReadInt16(),
            Secrets = br.ReadInt16(),
            SecretTotal = br.ReadInt16(),
            Treasure = br.ReadInt16(),
            TreasureTotal = br.ReadInt16(),
        };
    }
}
