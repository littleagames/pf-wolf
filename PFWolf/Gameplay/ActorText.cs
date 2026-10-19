using System.Text.RegularExpressions;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>
/// Text an actor's action or pickup message fills in: {tag} is the actor's tag (its map info
/// number: a log book's page, a dog tag's number), {count:Item} how many of an item the acting
/// player holds, and {left:Item} how many more they could (its inventory.maxamount less that).
/// Anything else in braces is left as it is.
/// </summary>
internal static partial class ActorText
{
    [GeneratedRegex(@"\{(tag|count:(?<count>\w+)|left:(?<left>\w+))\}", RegexOptions.IgnoreCase)]
    private static partial Regex Placeholder();

    internal static string Expand(string text, Entities.Actors.Actor actor, InventoryManager? inventory) =>
        !text.Contains('{') ? text : Placeholder().Replace(text, match =>
        {
            if (match.Groups["count"].Success)
                return inventory == null ? match.Value : inventory.GetCount(match.Groups["count"].Value).ToString();
            if (match.Groups["left"].Success)
            {
                var item = match.Groups["left"].Value;
                return inventory == null ? match.Value
                    : Math.Max(inventory.GetIntProperty(item, "inventory.maxamount", 0) - inventory.GetCount(item), 0).ToString();
            }
            return actor.Tag.ToString();
        });
}
