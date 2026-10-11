using Avalonia.Controls;

namespace PFWolf.Editor.Views;

/// <summary>
/// A check box that doesn't tick itself when it's clicked: its Command decides (and may ask
/// first), and IsChecked, bound one way, shows what it decided
/// </summary>
public class CommandCheckBox : CheckBox
{
    protected override Type StyleKeyOverride => typeof(CheckBox);

    protected override void Toggle()
    {
    }
}
