using PFWolf.Common;
using PFWolf.Common.Components;
using PFWolf.Common.Scenes;

namespace Engine;

internal class SceneManager
{
    internal SceneManager(
        SDLVideoManager videoManager,
        SDLInputManager inputManager,
        AssetManager assetManager)
    {
        this.videoManager = videoManager;
        this.inputManager = inputManager;
        this.assetManager = assetManager;
    }

    private Scene? _currentScene = null;
    private readonly SDLVideoManager videoManager;
    private readonly SDLInputManager inputManager;
    private readonly AssetManager assetManager;

    private Dictionary<string, Asset> _loadedAssets = new Dictionary<string, Asset>();

    public void LoadScene(string sceneName)
    {
        // TODO: Pull this info from the Script "asset".
        var type = sceneName switch
        {
            "SignonScene" => typeof(SignonScene),
            "Pg13Scene" => typeof(Pg13Scene),
            "TitleScene" => typeof(TitleScene),
            "MainMenuScene" => typeof(MainMenuScene),
            _ => null
        };

        _currentScene = (Scene?)Activator.CreateInstance(type);
        if (_currentScene is null)
        {
            throw new InvalidDataException($"Could not properly build the script for scene \"{sceneName}\"");
        }

        _currentScene.Start();

        _currentScene.GetComponents(out var components);
        // TODO: Build components into a render/update list
        // TODO: Load assets needed for components

        foreach (var component in components)
        {
            if (!component.Enabled) continue;
            if (component is RenderComponent renderComponent)
            {
                if (renderComponent is PFWolf.Common.Components.Graphic graphic)
                {
                    var asset = assetManager.Load<PFWolf.Common.Assets.Graphic>(graphic.AssetName);

                    var graphicTransform = graphic.Transform;
                    graphicTransform.Update(asset.Size);
                }
                if (renderComponent is PFWolf.Common.Components.Text text)
                {
                    var font = assetManager.Load<PFWolf.Common.Assets.Font>(text.Font);

                    var textTransform = text.Transform;

                    var fontGraphic = font.ToGraphic(text.String, text.ForeColor, maxBounds: new Dimension(640, 400)); // I'm thinking max gfx sizes would be 640x400. And any of these will just get clipped.
                    text.TempGraphicAssetName = $"tmp-{Guid.NewGuid()}";
                    assetManager.AddTempAsset(text.TempGraphicAssetName, fontGraphic);

                    textTransform.Update(fontGraphic.Size);
                }

                var updatedTransform = videoManager.CalculateTransform(renderComponent.Transform);

                if (renderComponent.Children.Count() > 0)
                {
                    foreach (var child in renderComponent.Children)
                    {
                        videoManager.CalculateTransform(child.Transform);
                    }
                }
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
        if (_currentScene is null)
            return;

        _currentScene.Update();
        _currentScene.GetComponents(out var components);
        foreach (var component in components)
        {
            if (component is RenderComponent renderComponent)
            {
                videoManager.DrawComponent(renderComponent);
            }
            else if (component is InputComponent inputComponent)
            {
                inputComponent.Update(inputManager.State);
            }
        }

        if (_currentScene.IsCompleted)
        {
            var nextScene = _currentScene.NextScene;
            if (nextScene is null)
            {
                throw new ArgumentException("No scene specified to in the 'nextScene'.");
            }

            UnloadScene();
            LoadScene(nextScene);
        }
    }
}
