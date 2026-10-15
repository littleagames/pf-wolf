using PFWolf.Fonts;
using PFWolf.Managers;

namespace PFWolf;

internal partial class Program
{
    // The HUD messages drawn over the view, and the obituary (Ui/HudMessageDrawing.cs)
    static HudMessageDrawing hudMessageDrawing = null!;     // made in InitServices

    static HudMessageDrawing MakeHudMessageDrawing() => new(_videoManager, _graphicManager, _inventoryManager, _hudMessageManager, ui,
        statusBarDrawing, playersManager, render, session);

    internal static void DrawHudMessages() => hudMessageDrawing.DrawHudMessages();
    internal static void ShowObituary() => hudMessageDrawing.ShowObituary();
    internal static string ActorTag(Entities.Actors.Actor actor) => hudMessageDrawing.ActorTag(actor);
    static List<string> WrapText(string text, string font, int width) => hudMessageDrawing.WrapText(text, font, width);
}