namespace PFWolf.Assets;

/// <summary>
/// A pk3's or mod's scripts/NAME.lua: a Lua script the engine runs at startup (PFWolf's
/// Scripting/ScriptHost). A later file of the same name replaces an earlier one.
/// </summary>
public record ScriptAsset : Asset
{
    /// <summary>The script's text, less any UTF-8 byte order mark</summary>
    public string Text { get; init; } = "";

    /// <summary>Where it came from, for messages ("fireball-demo: scripts/traps.lua")</summary>
    public string Where { get; init; } = "";

    /// <summary>Its place in load order: scripts run lowest first (pfwolf.pk3's, then each mod's)</summary>
    public int Order { get; init; }

    public static ScriptAsset FromFile(byte[] data, string where, int order)
    {
        if (data is [0xEF, 0xBB, 0xBF, ..])
            data = data[3..];
        return new ScriptAsset { RawData = data, Text = System.Text.Encoding.UTF8.GetString(data), Where = where, Order = order };
    }
}
