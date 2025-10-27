using PFWolf.Common;
using PFWolf.Common.Scenes;

namespace Engine;

internal class SceneManager
{
    internal SceneManager(SDLVideoManager videoManager, AssetManager assetManager)
    {
        this.videoManager = videoManager;
        this.assetManager = assetManager;
    }

    private Scene? _currentScene = null;
    private readonly SDLVideoManager videoManager;
    private readonly AssetManager assetManager;

    private Dictionary<string, Asset> _loadedAssets = new Dictionary<string, Asset>();

    public void LoadScene(string sceneName)
    {
        _currentScene = new SignonScene();
        _currentScene.Start();

        _currentScene.GetComponents(out var components);
        foreach (var component in components)
        {
            if (component is RenderComponent renderComponent)
            {
                if (renderComponent is PFWolf.Common.Components.Graphic graphic)
                {
                    var asset = assetManager.Load<PFWolf.Common.Assets.Graphic>(graphic.AssetName);
                    _loadedAssets.Add(graphic.AssetName, asset);
                    // Store in graphic list (some can be reused, e.g. toggled buttons)
                }
                if (renderComponent is PFWolf.Common.Components.Text text)
                {
                    var font = assetManager.Load<PFWolf.Common.Assets.Font>(text.Font);
                    _loadedAssets.Add(text.Font, font);
                    // Store in font list
                }
                //videoManager.AddRenderComponent(renderComponent);
            }
        }
        // TODO: Gather components into a flat, manageable structure that is easier to update/render
    }

    public void UnloadScene()
    {
        _currentScene?.Destroy();
        _currentScene = null;
        _loadedAssets.Clear();
    }

    public void Update()
    {
        _currentScene?.Update();
        // TODO: Update components
        // TODO: Update video rendering (if anything changed)
    }
}
