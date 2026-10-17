namespace PFWolf.Gameplay;

/// <summary>
/// A demo's controls, a frame's worth after another: played back from it by PollControls (or
/// recorded into it), loaded and saved by the demo code (Gameplay/Demos.cs).
/// </summary>
internal sealed class DemoBuffer
{
    internal byte[] DemoData = null!;
    internal int DemoPtr, LastDemoPtr;
}