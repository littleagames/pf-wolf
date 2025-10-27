using PFWolf.Common.Assets;

namespace PFWolf.Common;

public abstract class Scene
{
    private List<Component> _components = new List<Component>();

    public void AddComponent(Component component)
    {
        _components.Add(component);
    }

    public void GetComponents(out List<Component> components)
    {
        components = _components;
    }
    public void GetComponents<T>(out List<T> components) where T : Component
    {
        components = _components.OfType<T>().ToList();
    }

    public void UseDefinition()
    {
        // TODO: This will pull the asset "scene definition" and build the scene from it
    }

    public abstract void Start();

    public abstract void Update();

    public abstract void Destroy();
}
