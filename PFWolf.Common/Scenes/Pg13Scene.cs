using PFWolf.Common.Components;

namespace PFWolf.Common.Scenes;

public class Pg13Scene : Scene
{
    public override void Start()
    {
        AddComponent(Background.Create(0x82));
        AddComponent(Graphic.Create("pg13", new Transform
        {
            Position = new Vector2(216*2, 110*2),
            PositionalAlignment = PositionalAlignment.BottomRight,
            BoundingBoxType = BoundingBoxType.Scale
        }));
    }

    public override void Update()
    {
        if (Input.IsAnyKeyPressed)
        {
            Input.ClearKeysDown();
            CompleteAndLoadNextScene("TitleScene");
        }
    }
    
    public override void Destroy()
    {
    }
}
