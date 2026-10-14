using PFWolf.Extensions;

namespace PFWolf.Ui;

/// <summary>What the view size screen needs from the renderer</summary>
/// <param name="ViewSize">The view's size now (4 to 21)</param>
/// <param name="ShowViewSize">Draws the view's outline at a size</param>
/// <param name="NewViewSize">Sets the view to a size</param>
/// <param name="BorderColor">The color round the view</param>
internal sealed record ChangeViewHooks(Func<int> ViewSize, Action<int> ShowViewSize, Action<int> NewViewSize, Func<string> BorderColor);

/// <summary>The Change View screen (F5): the view made bigger or smaller, up to full screen</summary>
internal sealed class ChangeViewScreen(AssetManager assets, InputManager input, AudioManager audio, VideoManager video,
    MenuDrawing draw, MenuLoop loop, MenuInput menuInput, UiText ui, ChangeViewHooks hooks)
{
    internal int Run()
    {
        var language = assets.GetText("en-us");
        int exit = 0, oldview, newview;
        ControlInfo ci;

        newview = oldview = hooks.ViewSize();
        DrawChangeView(oldview);
        draw.MenuFadeIn();

        do
        {
            menuInput.CheckPause();
            GameEngineManager.DelayMs(5);
            menuInput.ReadAnyControl(out ci);
            switch (ci.dir)
            {
                case Direction.South:
                case Direction.West:
                    newview--;
                    if (newview < 4)
                        newview = 4;
                    if (newview >= 19) DrawChangeView(newview);
                    else hooks.ShowViewSize(newview);
                    video.Update();
                    audio.Play("world/hitwall");
                    menuInput.TicDelay(10);
                    break;

                case Direction.North:
                case Direction.East:
                    newview++;
                    if (newview >= 21)
                    {
                        newview = 21;
                        DrawChangeView(newview);
                    }
                    else hooks.ShowViewSize(newview);
                    video.Update();
                    audio.Play("world/hitwall");
                    menuInput.TicDelay(10);
                    break;
            }

            if (ci.button0 || input.IsKeyDown(ScanCodes.sc_Enter))
                exit = 1;
            else if (ci.button1 || input.IsKeyDown(ScanCodes.sc_Escape))
            {
                audio.Play("menu/escape");
                draw.MenuFadeOut();
                if (video.HasMargins)
                    video.ClearScreen(0);
                return 0;
            }
        }
        while (exit == 0);

        if (oldview != newview)
        {
            audio.Play("menu/activate");
            draw.Message("$STR_THINK".ToLanguageText(language) + "...");
            hooks.NewViewSize(newview);
        }

        loop.ShootSnd();
        draw.MenuFadeOut();
        if (video.HasMargins)
            video.ClearScreen(0);

        return 0;
    }

    internal void DrawChangeView(int view)
    {
        var language = assets.GetText("en-us");
        // The help goes in the bottom 40 lines of the screen, where the status bar is
        int top = video.ScreenYAboveBottom(40);
        if (view != 21)
            video.BarScaledCoord(0, top, video.screenWidth, video.screenHeight - top, hooks.BorderColor());

        hooks.ShowViewSize(view);

        using var _ = video.UseUiOrigin(UiAnchor.Bottom);
        var help = ui.CenteredText(0, 320, 200 - 39, UiFonts.MenuStyle("HIGHLIGHT"));
        help.CPrint("$STR_SIZE1".ToLanguageText(language) + "\n");
        help.CPrint("$STR_SIZE2".ToLanguageText(language) + "\n");
        help.CPrint("$STR_SIZE3".ToLanguageText(language));
        video.Update();
    }
}
