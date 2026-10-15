using PFWolf.Scripting;

namespace PFWolf;

internal partial class Program
{
    // The Lua scripts and the API they get (Scripting/GameScripts.cs)
    static GameScripts gameScripts = null!;     // made in InitServices

    static GameScripts MakeGameScripts() =>
        new(_assetManager, _mapManager, _audioManager, _hudMessageManager, _consoleManager, _translationManager,
            projectileActions ??= MakeProjectileActions(), session, PlaySoundLocActor);

    // The script API's tests install it with no services: it's made then
    internal static void InstallScriptApi(ScriptHost host) => (gameScripts ??= MakeGameScripts()).InstallScriptApi(host);

    static ScriptHost? _scripts => gameScripts?.Scripts;
    static void LoadScripts() => gameScripts.LoadScripts();
    static void RunLevelHooks(string reason) => gameScripts.RunLevelHooks(reason);
    internal static Dictionary<string, string> OwnTileActions => GameScripts.OwnTileActions;
    static void Cmd_Scripts(string[] args) => gameScripts.Cmd_Scripts(args);
    static void Cmd_Lua(string[] args) => gameScripts.Cmd_Lua(args);
}
