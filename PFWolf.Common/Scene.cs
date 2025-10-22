using PFWolf.Common.Assets;

namespace PFWolf.Common;

public abstract class Scene
{
    private List<string> _components = new List<string>(); // TODO: Change to a scene component

    public void AddComponent(string component)
    {
        _components.Add(component);
    }

    public abstract void Start();

    public abstract void Update();

    public abstract void Destroy();
}
