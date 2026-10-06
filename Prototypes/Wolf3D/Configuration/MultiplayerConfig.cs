namespace Wolf3D.Configuration;

/// <summary>
/// multiplayer.cfg in the game's config folder: what playing with others remembers, as
/// `key value` lines -- the player's name, and the address last joined (so it's offered again).
/// Blank lines and lines starting with # are skipped; unknown keys are kept as they are.
/// </summary>
internal sealed class MultiplayerConfig
{
    public const string FileName = "multiplayer.cfg";

    /// <summary>The name others see; the computer's user name until one is picked</summary>
    public string Name { get; set; } = Environment.UserName;

    /// <summary>The address last typed in to join, with its port if it isn't the default</summary>
    public string LastAddress { get; set; } = "";

    /// <summary>Whether hosting asks the router (UPnP or NAT-PMP) to open the game's port for players on the internet</summary>
    public bool OpenPort { get; set; } = true;

    private readonly List<string> _otherLines = [];

    public static MultiplayerConfig Read(string path)
    {
        var config = new MultiplayerConfig();
        if (!File.Exists(path))
            return config;

        try
        {
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;

                int space = line.IndexOf(' ');
                var key = space < 0 ? line : line[..space];
                var value = space < 0 ? "" : line[(space + 1)..].Trim();
                switch (key.ToLowerInvariant())
                {
                    case "name": config.Name = value; break;
                    case "last-address": config.LastAddress = value; break;
                    case "open-port": config.OpenPort = value is not ("false" or "0" or "off"); break;
                    default: config._otherLines.Add(line); break;
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Couldn't read {path} ({e.Message}).");
        }
        return config;
    }

    /// <returns>Whether it was written</returns>
    public bool Write(string path)
    {
        try
        {
            File.WriteAllLines(path,
            [
                "# Playing with others: your name, the address you last joined, and whether hosting opens the router's port",
                $"name {Name}",
                $"last-address {LastAddress}",
                $"open-port {(OpenPort ? "true" : "false")}",
                .. _otherLines,
            ]);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Couldn't write {path}: {e.Message}");
            return false;
        }
    }
}
