

namespace PFWolf.Common;

public record Component
{
    /// <summary>
    /// Allows component to be processed
    /// </summary>
    public bool Enabled { get; set; } = true;
}

public record RenderComponent : Component
{
    /// <summary>
    /// Disables visual rendering of the component
    /// </summary>
    public bool Hidden { get; set; } = false;
    public Transform Transform { get; init; }
}
