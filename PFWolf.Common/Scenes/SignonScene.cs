using PFWolf.Common.Components;

namespace PFWolf.Common.Scenes;

public class SignonScene : Scene
{
    private Text _pressAKeyText = null!;

    public override void Start()
    {
        AddComponent(Graphic.Create("wolf3d-signon", new Transform
        {
            Position = Vector2.Zero,
            PositionalAlignment = PositionalAlignment.TopLeft,
            BoundingBoxType = BoundingBoxType.ScaleToScreen
        }));

        _pressAKeyText = Text.Create("Press A Key", Transform.Create(Vector2.Zero), "smallfont", 14);
        AddComponent(_pressAKeyText);
    }

    public override void Update()
    {
    }
    
    public override void Destroy()
    {
    }
}
