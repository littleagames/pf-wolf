namespace PFWolf.Gameplay;

/// <summary>
/// A demo's controls, a frame's worth after another: played back from it by PollControls (or
/// recorded into it), loaded and saved by the demo code (GameHost.WL_GAME.cs).
/// </summary>
internal sealed class DemoBuffer
{
    internal byte[] demoData = null!;
    internal int demoptr, lastdemoptr;
}