using CSharpFunctionalExtensions;

namespace PFWolf.Common;

public class GameManager
{
    private static readonly Lazy<GameManager> _instance = new(() => new GameManager());
    private bool isInitialized = false;
    private AssetManager AssetManager;

    public static GameManager Instance => _instance.Value;

    private GameManager()
    {
        // Private constructor to prevent external instantiation
    }

    public Result Initialize(AssetManager assetManager)
    {
        if (assetManager == null)
        {
            return Result.Failure("AssetManager cannot be null");
        }
        AssetManager = assetManager;
        isInitialized = true;
        return Result.Success();
    }

    public Result BeginLoop()
    {
        if (!isInitialized)
        {
            return Result.Failure("GameManager is not initialized");
        }

        bool isRunning = true;
        do
        {
            // Main game loop logic would go here
            // For now, just a placeholder
            Console.WriteLine("Game loop iteration...");

            // Example: break after one iteration for demonstration
            if (Console.KeyAvailable)
            {
                isRunning = false;
            }
        }
        while (isRunning);

        Console.WriteLine("Game loop has ended.");

        return Result.Success();
    }

    public Result ShutDown()
    {
        if (!isInitialized)
        {
            return Result.Failure("GameManager is not initialized");
        }

        // Perform any necessary cleanup here
        //AssetManager.ShutDown();

        isInitialized = false;
        return Result.Success();
    }
}
