namespace PFWolf.Common;

public class InputState
{
    public bool QuitPressed { get; set; } = false;

    public HashSet<int> KeyPressed { get; } = new HashSet<int>();
    public bool AnyKeyPressed { get; set; }

    public void ClearKeysDown()
    {
        KeyPressed.Clear();
        AnyKeyPressed = false;
    }
}

public enum InputAction
{
    Quit,
}
