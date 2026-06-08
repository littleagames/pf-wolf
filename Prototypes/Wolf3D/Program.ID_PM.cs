using Wolf3D.Loaders;

namespace Wolf3D;

internal partial class Program
{
    [Obsolete("Move the asset loading out of this method.")]
    internal static void PM_Startup()
    {
        // TODO: This will be done with an asset loader
        //var vswapLoader = new Wolf3DVswapFileLoader("vswap", extension);
        //vswapLoader.GetAssets();//getassetreferences
    }

    [Obsolete("Throw exceptions instead of calling Quit when moved to file loaders.")]
    static void CA_CannotOpen(string text)
    {
        _gameEngineManager.Quit($"Can't open {text}!");
    }
}
