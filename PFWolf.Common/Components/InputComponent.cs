namespace PFWolf.Common.Components;

public record InputComponent : Component
{
    private InputState _lastState = new();

    public bool IsAnyKeyPressed => _lastState.AnyKeyPressed;

    public void ClearKeysDown()
    {
        _lastState.ClearKeysDown();
    }

    public void Update(InputState state)
    {
        _lastState = state;
    }
}
