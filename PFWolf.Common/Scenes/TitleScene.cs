using PFWolf.Common.Components;

namespace PFWolf.Common.Scenes;

public class TitleScene : Scene
{
    public override void Start()
    {
        //AddComponent(Graphic.Create("title", new Transform
        //{
        //    Position = Position.Zero,
        //    BoundingBox = BoundingBoxType.StretchToScreen
        //}));
    }

    public override void Update()
    {
        if (Input.IsAnyKeyPressed)
        {
            Input.ClearKeysDown();
            CompleteAndLoadNextScene("MainMenuScene");
        }
    }
    
    public override void Destroy()
    {
    }
}
