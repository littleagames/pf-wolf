namespace PFWolf.Common;

public class InputState
{
    public bool QuitPressed { get; set; } = false;

    public HashSet<int> KeyPressed { get; } = new HashSet<int>();
}

public enum InputAction
{
    Quit,
}
