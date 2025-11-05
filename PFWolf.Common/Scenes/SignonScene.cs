using PFWolf.Common.Components;

namespace PFWolf.Common.Scenes;

public class SignonScene : Scene
{
    private Text _pressAKeyText = null!;
    private Text _workingText = null!;
    private const byte YellowTextColor = 14;
    private const byte GreenTextColor = 10;

    private int _signonState = 1;

    public override void Start()
    {
        AddComponent(Graphic.Create("wolf3d-signon", new Transform
        {
            Position = Vector2.Zero,
            PositionalAlignment = PositionalAlignment.TopLeft,
            BoundingBoxType = BoundingBoxType.ScaleToScreen
        }));

        _pressAKeyText = Text.Create("Press A Key",
            new Transform
            {
                Position = new Vector2(0, 190),
                BoundingBoxType = BoundingBoxType.NoBounds
            },
            TextAlignment.Center,
            "smallfont", YellowTextColor);
        AddComponent(_pressAKeyText);

        _workingText = Text.Create("Working...",
            new Transform
            {
                Position = new Vector2(0, 190),
                BoundingBoxType = BoundingBoxType.NoBounds
            },
            TextAlignment.Center,
            "smallfont", GreenTextColor);
        _workingText.Hidden = true;
        AddComponent(_workingText);
    }

    public override void Update()
    {
        if (_signonState == 1)
        {
            if (Input.IsAnyKeyPressed)
            {
                Input.ClearKeysDown();
                _signonState = 2;
                _pressAKeyText.Hidden = true;
                _workingText.Hidden = false;

                CompleteAndLoadNextScene("Pg13Scene");
                //_fader.???
                //Console.WriteLine("Working...");
                // Transition to the next scene, e.g., main menu
                // This would typically involve notifying the SceneManager to change scenes
            }
        }
    }
    
    public override void Destroy()
    {
    }
}
