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
        // TODO: Build components into a render/update list
        // TODO: Load assets needed for components

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
                //videoManager.DrawComponent(renderComponent);
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
                //if (renderComponent is PFWolf.Common.Components.Graphic graphicComponent)
                //{
                //    if (_loadedAssets.TryGetValue(graphicComponent.AssetName, out var asset))
                //    {
                //        var graphicAsset = asset as PFWolf.Common.Assets.Graphic;
                //        videoManager.Draw(graphicAsset, graphicComponent.Transform.Position, graphicComponent.Transform.Size);
                //    }
                //}
                //if (renderComponent is PFWolf.Common.Components.Text textComponent)
                //{
                //}
            }
        }

        //if (changed)
        //{
        //    // Render something here
        //    videoManager.Draw(signon,
        //    // Transform
        //    // Position (x,y)
        //    // HasChanged: bool
        //    // TODO: Turns into "offset: Vector2"
        //    // TODO: Orientation: Top, TopLeft, Left, Center, etc
        //    position: new Vector2(0, 0),
        //    // Scaling = Scaling.FitToScreen
        //    // Scaling.StretchToFit
        //    // Scaling.??
        //    size: new Dimension(ScreenWidth, ScreenHeight)); // parent.Width, parent.Height));

        //    if (signonWaitingForPressAKey)
        //    {
        //        videoManager.DrawRectangle(0, 189, 300, 11, 0x29);
        //        videoManager.Draw(smallFont, new Vector2(0, 190), TextAlignment.Center, "Press A Key", 14, 4);
        //    }
        //    else
        //    {
        //        videoManager.DrawRectangle(0, 189, 300, 11, 0x29);
        //        videoManager.Draw(smallFont, new Vector2(0, 190), TextAlignment.Center, "Working...", 10, 4);
        //    }

        //    //changed = false;
        //}
        // TODO: Update components
        // TODO: Update video rendering (if anything changed)
    }
}
